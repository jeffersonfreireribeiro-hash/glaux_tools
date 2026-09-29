using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Buraqueira_Tools.Data;
using Buraqueira_Tools.Persistence;

namespace Buraqueira_Tools.ProjectState
{
    /// <summary>Nomes de árvores e chaves de metadados das entradas de snapshot/experimento.</summary>
    public static class VaultNames
    {
        public const string ParamPrefix = "param:";
        public const string HubPrefix = "hub:";
        public const string Inputs = "in";
        public const string Outputs = "out";
        public const string Results = "result";
        public const string Controls = "controls";

        public const string MetaFormat = "vault.format";
        public const string MetaNotes = "vault.notes";
        public const string MetaTags = "vault.tags";
        public const string MetaRuntimeMs = "runtime.ms";
        public const string MetaInputsHash = "hash.inputs";
        public const string MetaOutputsHash = "hash.outputs";
        public const string MetaConfigPrefix = "config.";
        public const string MetaUnitPrefix = "unit.";

        public const string FormatVersion = "1";

        public static bool IsInputTree(string name) =>
            name.StartsWith(ParamPrefix, StringComparison.Ordinal) || name == Inputs || name == Controls || name.StartsWith(HubPrefix, StringComparison.Ordinal);

        public static bool IsOutputTree(string name) => name == Outputs || name == Results;
    }

    /// <summary>
    /// Ambiente de execução registrado em cada snapshot/experimento (proveniência):
    /// versões do Glaux, Rhino, Grasshopper, sistema, runtime e documento.
    /// </summary>
    public static class EnvironmentInfo
    {
        public const string GlauxVersion = "glaux.version";
        public const string RhinoVersion = "rhino.version";
        public const string GrasshopperVersion = "grasshopper.version";
        public const string OperatingSystem = "os";
        public const string Runtime = "runtime";
        public const string Document = "document";
        public const string DocumentId = "document.id";

        /// <summary>
        /// Captura o ambiente atual. Cada leitura é isolada: fora do Rhino (testes) as versões do host
        /// ficam como "indisponível" em vez de lançar exceção.
        /// </summary>
        public static Dictionary<string, string> Capture(string documentName = null, Guid documentId = default(Guid))
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GlauxVersion] = SafeGet(() =>
                {
                    var asm = typeof(EnvironmentInfo).Assembly;
                    var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                    return info?.InformationalVersion ?? asm.GetName().Version.ToString();
                }),
                [RhinoVersion] = SafeGet(RhinoVersionString),
                [GrasshopperVersion] = SafeGet(() => Grasshopper.Versioning.VersionString + " (" + typeof(Grasshopper.Kernel.GH_Document).Assembly.GetName().Version + ")"),
                [OperatingSystem] = SafeGet(() => Environment.OSVersion.VersionString),
                [Runtime] = SafeGet(() => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription)
            };
            if (!string.IsNullOrEmpty(documentName)) d[Document] = documentName;
            if (documentId != Guid.Empty) d[DocumentId] = documentId.ToString("D");
            return d;
        }

        // Método separado: o JIT só carrega RhinoCommon se este método for chamado
        private static string RhinoVersionString() => Rhino.RhinoApp.Version.ToString();

        private static string SafeGet(Func<string> read)
        {
            try
            {
                string v = read();
                return string.IsNullOrEmpty(v) ? "indisponível" : v;
            }
            catch
            {
                return "indisponível";
            }
        }
    }

    /// <summary>Tipos de controle do canvas registrados em snapshots.</summary>
    public static class ControlKinds
    {
        public const string Slider = "Slider";
        public const string Toggle = "Toggle";
        public const string ValueList = "ValueList";
        public const string Panel = "Panel";
        public const string PoolSlider = "PoolSlider";

        /// <summary>Controle de um Pill Dashboard (Id = "guidDoPainel|idDoWidget").</summary>
        public const string Dashboard = "Dashboard";
    }

    /// <summary>
    /// Estado de um controle do canvas (slider, toggle, value list, panel de entrada ou slider do Pill Slider Pool),
    /// independente do Grasshopper em memória. Guardado na árvore "controls" do snapshot (um ramo por controle).
    /// </summary>
    public sealed class ControlState
    {
        public string Kind { get; set; }

        /// <summary>InstanceGuid do objeto; para sliders do Slider Pool: "guidDoPool|chaveLimpa".</summary>
        public string Id { get; set; }

        public string Name { get; set; }
        public double? Number { get; set; }
        public bool? Boolean { get; set; }
        public string Text { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }

        /// <summary>Fim do domínio (sliders de domínio do Slider Pool).</summary>
        public double? Extra { get; set; }

        public string DisplayValue
        {
            get
            {
                if (Number.HasValue) return Number.Value.ToString("G10", CultureInfo.InvariantCulture) + (Extra.HasValue ? " … " + Extra.Value.ToString("G10", CultureInfo.InvariantCulture) : "");
                if (Boolean.HasValue) return Boolean.Value ? "True" : "False";
                return Text ?? "";
            }
        }

        public override string ToString() => $"{Kind} '{Name}' = {DisplayValue}";

        public static GlauxTreeTable ToTable(IList<ControlState> states)
        {
            var table = new GlauxTreeTable();
            for (int i = 0; i < states.Count; i++)
            {
                var s = states[i];
                var items = new List<GlauxValue>
                {
                    GlauxValue.FromText(s.Kind),
                    GlauxValue.FromText(s.Id),
                    GlauxValue.FromText(s.Name),
                    s.Number.HasValue ? GlauxValue.FromNumber(s.Number.Value)
                        : s.Boolean.HasValue ? GlauxValue.FromBoolean(s.Boolean.Value)
                        : GlauxValue.FromText(s.Text),
                    s.Min.HasValue ? GlauxValue.FromNumber(s.Min.Value) : GlauxValue.Null,
                    s.Max.HasValue ? GlauxValue.FromNumber(s.Max.Value) : GlauxValue.Null,
                    s.Extra.HasValue ? GlauxValue.FromNumber(s.Extra.Value) : GlauxValue.Null
                };
                table.Branches.Add(new GlauxBranch(new[] { i }, items));
            }
            return table;
        }

        public static List<ControlState> FromTable(GlauxTreeTable table)
        {
            var list = new List<ControlState>();
            if (table == null) return list;
            foreach (var b in table.Branches)
            {
                if (b.Items.Count < 4) continue;
                var s = new ControlState
                {
                    Kind = b.Items[0].Text,
                    Id = b.Items[1].Text,
                    Name = b.Items[2].Text
                };
                var v = b.Items[3];
                if (v.Kind == GlauxValueKind.Number) s.Number = v.X;
                else if (v.Kind == GlauxValueKind.Boolean) s.Boolean = v.BooleanValue;
                else s.Text = v.Text;
                if (b.Items.Count > 4 && b.Items[4].Kind == GlauxValueKind.Number) s.Min = b.Items[4].X;
                if (b.Items.Count > 5 && b.Items[5].Kind == GlauxValueKind.Number) s.Max = b.Items[5].X;
                if (b.Items.Count > 6 && b.Items[6].Kind == GlauxValueKind.Number) s.Extra = b.Items[6].X;
                list.Add(s);
            }
            return list;
        }
    }

    /// <summary>Resultado da checagem de um controle salvo contra o controle atual do canvas.</summary>
    public sealed class ControlMatch
    {
        public ControlState Saved { get; set; }
        public ControlState Current { get; set; }
        public bool Compatible { get; set; }
        public bool MatchedByName { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// Decide se um estado salvo pode ser restaurado no canvas atual: o controle precisa existir (pelo id ou,
    /// na falta, por tipo + nome único), ser do mesmo tipo e o valor caber na faixa atual (sliders) ou existir
    /// entre as opções (value lists). Nada é "forçado": incompatíveis são reportados e ignorados.
    /// </summary>
    public static class ControlCompatibility
    {
        public static List<ControlMatch> Match(IList<ControlState> saved, IList<ControlState> current, Func<ControlState, ICollection<string>> optionsOf = null)
        {
            var byId = new Dictionary<string, ControlState>(StringComparer.OrdinalIgnoreCase);
            var byName = new Dictionary<string, List<ControlState>>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in current)
            {
                if (!string.IsNullOrEmpty(c.Id)) byId[c.Id] = c;
                string nk = c.Kind + "\u0001" + (c.Name ?? "");
                if (!byName.TryGetValue(nk, out var list)) byName[nk] = list = new List<ControlState>();
                list.Add(c);
            }

            var result = new List<ControlMatch>();
            foreach (var s in saved)
            {
                var m = new ControlMatch { Saved = s };
                if (!string.IsNullOrEmpty(s.Id) && byId.TryGetValue(s.Id, out var cur))
                {
                    m.Current = cur;
                }
                else if (byName.TryGetValue(s.Kind + "\u0001" + (s.Name ?? ""), out var candidates))
                {
                    if (candidates.Count == 1)
                    {
                        m.Current = candidates[0];
                        m.MatchedByName = true;
                    }
                    else
                    {
                        m.Reason = $"{candidates.Count} controles '{s.Name}' do tipo {s.Kind}: ambíguo.";
                        result.Add(m);
                        continue;
                    }
                }

                if (m.Current == null)
                {
                    m.Reason = "controle não existe mais no documento.";
                }
                else if (!string.Equals(m.Current.Kind, s.Kind, StringComparison.Ordinal))
                {
                    m.Reason = $"tipo mudou ({s.Kind} → {m.Current.Kind}).";
                }
                else if (s.Number.HasValue && m.Current.Min.HasValue && m.Current.Max.HasValue &&
                         (s.Number.Value < m.Current.Min.Value || s.Number.Value > m.Current.Max.Value ||
                          (s.Extra.HasValue && (s.Extra.Value < m.Current.Min.Value || s.Extra.Value > m.Current.Max.Value))))
                {
                    m.Reason = $"valor {s.DisplayValue} fora da faixa atual [{m.Current.Min.Value.ToString(CultureInfo.InvariantCulture)}, {m.Current.Max.Value.ToString(CultureInfo.InvariantCulture)}].";
                }
                else if (s.Kind == ControlKinds.Dashboard && s.Text != null && optionsOf?.Invoke(m.Current) is ICollection<string> dashboardOptions)
                {
                    // Dropdown do Pill Dashboard: a opção salva precisa existir na lista atual
                    bool found = false;
                    foreach (var o in dashboardOptions)
                    {
                        if (string.Equals(o, s.Text, StringComparison.OrdinalIgnoreCase)) found = true;
                    }
                    if (!found) m.Reason = $"opção '{s.Text}' não existe mais na lista.";
                    else m.Compatible = true;
                }
                else if (s.Kind == ControlKinds.ValueList && optionsOf != null)
                {
                    var options = optionsOf(m.Current);
                    bool found = false;
                    if (options != null)
                    {
                        foreach (var o in options)
                        {
                            if (string.Equals(o, s.Text, StringComparison.OrdinalIgnoreCase))
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                    if (!found) m.Reason = $"opção '{s.Text}' não existe mais na lista.";
                    else m.Compatible = true;
                }
                else
                {
                    m.Compatible = true;
                }
                result.Add(m);
            }
            return result;
        }
    }

    /// <summary>Conversão entre <see cref="PillBundle"/> (parâmetros nomeados) e árvores "param:*" do snapshot.</summary>
    public static class BundleTrees
    {
        public static Dictionary<string, GlauxTreeTable> FromBundle(PillBundle bundle, IDictionary<string, string> unitsOut = null)
        {
            var result = new Dictionary<string, GlauxTreeTable>(StringComparer.Ordinal);
            if (bundle?.Entries == null) return result;
            foreach (var kv in bundle.Entries)
            {
                var items = new List<GlauxValue>();
                if (kv.Value is System.Collections.IList list && !(kv.Value is string))
                {
                    foreach (var o in list) items.Add(FromObject(o));
                }
                else
                {
                    items.Add(FromObject(kv.Value));
                }
                var table = new GlauxTreeTable();
                table.Branches.Add(new GlauxBranch(new[] { 0 }, items));
                result[kv.Key] = table;
                if (unitsOut != null && bundle.Units != null && bundle.Units.TryGetValue(kv.Key, out string unit) && !string.IsNullOrEmpty(unit))
                {
                    unitsOut[kv.Key] = unit;
                }
            }
            return result;
        }

        public static PillBundle ToBundle(IDictionary<string, GlauxTreeTable> parameters, IDictionary<string, string> units, string ns = "SNAPSHOT")
        {
            var bundle = new PillBundle { Namespace = ns, Timestamp = DateTime.Now };
            foreach (var kv in parameters)
            {
                var values = new List<object>();
                foreach (var b in kv.Value.Branches)
                {
                    foreach (var v in b.Items) values.Add(ToObject(v));
                }
                bundle.Entries[kv.Key] = values.Count == 1 ? values[0] : values;
                if (units != null && units.TryGetValue(kv.Key, out string unit)) bundle.Units[kv.Key] = unit;
            }
            return bundle;
        }

        private static GlauxValue FromObject(object o)
        {
            switch (o)
            {
                case null: return GlauxValue.Null;
                case double d: return GlauxValue.FromNumber(d);
                case float f: return GlauxValue.FromNumber(f);
                case int i: return GlauxValue.FromInteger(i);
                case long l: return GlauxValue.FromInteger(l);
                case short s: return GlauxValue.FromInteger(s);
                case bool b: return GlauxValue.FromBoolean(b);
                case string str: return GlauxValue.FromText(str);
                case DateTime dt: return GlauxValue.FromTime(dt);
                case Guid g: return GlauxValue.FromGuid(g);
                case Grasshopper.Kernel.Types.IGH_Goo goo: return GooCodec.Encode(goo);
            }
            var converted = Grasshopper.Kernel.GH_Convert.ToGoo(o);
            return converted != null ? GooCodec.Encode(converted) : GlauxValue.FromText(o.ToString());
        }

        private static object ToObject(GlauxValue v)
        {
            switch (v.Kind)
            {
                case GlauxValueKind.Null: return null;
                case GlauxValueKind.Number: return v.X;
                case GlauxValueKind.Integer: return v.Int >= int.MinValue && v.Int <= int.MaxValue ? (object)(int)v.Int : v.Int;
                case GlauxValueKind.Boolean: return v.BooleanValue;
                case GlauxValueKind.Text: return v.Text;
                case GlauxValueKind.Time: return v.TimeValue;
                case GlauxValueKind.Guid: return v.GuidValue;
                default:
                    var goo = GooCodec.Decode(v);
                    return goo?.SafeScriptVariable() ?? goo;
            }
        }
    }

    /// <summary>Partes de um snapshot antes de gravar.</summary>
    public sealed class SnapshotParts
    {
        public Dictionary<string, GlauxTreeTable> Parameters { get; } = new Dictionary<string, GlauxTreeTable>(StringComparer.Ordinal);
        public Dictionary<string, string> Units { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public GlauxTreeTable Inputs { get; set; }
        public GlauxTreeTable Outputs { get; set; }
        public List<ControlState> Controls { get; } = new List<ControlState>();
        public Dictionary<string, GlauxTreeTable> HubChannels { get; } = new Dictionary<string, GlauxTreeTable>(StringComparer.Ordinal);
        public Dictionary<string, string> Environment { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, string> Config { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public string Notes { get; set; }
        public List<string> Tags { get; } = new List<string>();
        public double? RuntimeMs { get; set; }
    }

    /// <summary>Monta e lê entradas de snapshot/experimento no store.</summary>
    public static class SnapshotCodec
    {
        public static StoreEntryDraft BuildDraft(string kind, string key, SnapshotParts parts, bool resultsAsOutputs = false)
        {
            var draft = new StoreEntryDraft(kind, key);
            foreach (var kv in parts.Parameters) draft.AddTree(VaultNames.ParamPrefix + kv.Key, kv.Value);
            if (parts.Inputs != null) draft.AddTree(VaultNames.Inputs, parts.Inputs);
            if (parts.Controls.Count > 0) draft.AddTree(VaultNames.Controls, ControlState.ToTable(parts.Controls));
            foreach (var kv in parts.HubChannels) draft.AddTree(VaultNames.HubPrefix + kv.Key, kv.Value);
            if (parts.Outputs != null) draft.AddTree(resultsAsOutputs ? VaultNames.Results : VaultNames.Outputs, parts.Outputs);

            foreach (var kv in parts.Environment) draft.Metadata[kv.Key] = kv.Value;
            foreach (var kv in parts.Config) draft.Metadata[VaultNames.MetaConfigPrefix + kv.Key] = kv.Value;
            foreach (var kv in parts.Units) draft.Metadata[VaultNames.MetaUnitPrefix + kv.Key] = kv.Value;
            draft.Metadata[VaultNames.MetaFormat] = VaultNames.FormatVersion;
            if (!string.IsNullOrWhiteSpace(parts.Notes)) draft.Metadata[VaultNames.MetaNotes] = parts.Notes.Trim();
            if (parts.Tags.Count > 0) draft.Metadata[VaultNames.MetaTags] = string.Join(",", parts.Tags);
            if (parts.RuntimeMs.HasValue) draft.Metadata[VaultNames.MetaRuntimeMs] = parts.RuntimeMs.Value.ToString("R", CultureInfo.InvariantCulture);

            draft.Metadata[VaultNames.MetaInputsHash] = CombinedHash(draft, VaultNames.IsInputTree);
            draft.Metadata[VaultNames.MetaOutputsHash] = CombinedHash(draft, VaultNames.IsOutputTree);
            return draft;
        }

        /// <summary>
        /// Identidade combinada de um grupo de árvores (entradas ou saídas): SHA-256 de "nome=hash" em ordem.
        /// Dois snapshots com o mesmo hash de entradas foram produzidos com exatamente os mesmos parâmetros.
        /// </summary>
        public static string CombinedHash(StoreEntryDraft draft, Func<string, bool> filter)
        {
            var parts = new List<string>();
            foreach (var kv in draft.Trees)
            {
                if (filter(kv.Key)) parts.Add(kv.Key + "=" + TreeHash.Compute(kv.Value));
            }
            parts.Sort(StringComparer.Ordinal);
            return parts.Count == 0 ? "" : TreeHash.Sha256Hex(Encoding.UTF8.GetBytes(string.Join("\n", parts)));
        }

        /// <summary>Reconstrói as partes a partir das árvores carregadas de uma entrada.</summary>
        public static SnapshotParts Read(StoreEntryHeader header, IDictionary<string, GlauxTreeTable> trees)
        {
            var parts = new SnapshotParts();
            foreach (var kv in trees)
            {
                if (kv.Key.StartsWith(VaultNames.ParamPrefix, StringComparison.Ordinal)) parts.Parameters[kv.Key.Substring(VaultNames.ParamPrefix.Length)] = kv.Value;
                else if (kv.Key.StartsWith(VaultNames.HubPrefix, StringComparison.Ordinal)) parts.HubChannels[kv.Key.Substring(VaultNames.HubPrefix.Length)] = kv.Value;
                else if (kv.Key == VaultNames.Inputs) parts.Inputs = kv.Value;
                else if (kv.Key == VaultNames.Outputs || kv.Key == VaultNames.Results) parts.Outputs = kv.Value;
                else if (kv.Key == VaultNames.Controls) parts.Controls.AddRange(ControlState.FromTable(kv.Value));
            }
            foreach (var kv in header.Metadata)
            {
                if (kv.Key.StartsWith(VaultNames.MetaConfigPrefix, StringComparison.Ordinal)) parts.Config[kv.Key.Substring(VaultNames.MetaConfigPrefix.Length)] = kv.Value;
                else if (kv.Key.StartsWith(VaultNames.MetaUnitPrefix, StringComparison.Ordinal)) parts.Units[kv.Key.Substring(VaultNames.MetaUnitPrefix.Length)] = kv.Value;
                else if (kv.Key == VaultNames.MetaNotes) parts.Notes = kv.Value;
                else if (kv.Key == VaultNames.MetaTags) parts.Tags.AddRange(kv.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                else if (kv.Key == VaultNames.MetaRuntimeMs && double.TryParse(kv.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double ms)) parts.RuntimeMs = ms;
                else if (!kv.Key.StartsWith("hash.", StringComparison.Ordinal) && kv.Key != VaultNames.MetaFormat) parts.Environment[kv.Key] = kv.Value;
            }
            return parts;
        }
    }

    /// <summary>
    /// Referência textual a uma entrada: "chave" (última), "chave@N" (revisão N), "chave@-1" (penúltima).
    /// </summary>
    public static class EntryRef
    {
        public static bool TryParse(string text, out string key, out long revision)
        {
            key = null;
            revision = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim();
            int at = t.LastIndexOf('@');
            if (at > 0 && long.TryParse(t.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long rev))
            {
                key = t.Substring(0, at).Trim();
                revision = rev;
                return key.Length > 0;
            }
            key = t;
            return true;
        }

        /// <summary>Resolve a referência (0 = última, negativo = relativo à última).</summary>
        public static StoreEntryHeader Resolve(GlauxFileStore store, string kind, string text)
        {
            if (!TryParse(text, out string key, out long revision)) return null;
            var revisions = store.GetRevisions(kind, key);
            if (revisions.Count == 0) return null;
            if (revision == 0) return revisions[revisions.Count - 1];
            if (revision < 0)
            {
                long idx = revisions.Count - 1 + revision;
                return idx >= 0 ? revisions[(int)idx] : null;
            }
            foreach (var r in revisions)
            {
                if (r.Revision == revision) return r;
            }
            return null;
        }
    }
}
