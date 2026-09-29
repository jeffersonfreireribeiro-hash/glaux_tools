using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Buraqueira_Tools.Dashboard;
using Buraqueira_Tools.ProjectState;
using Buraqueira_Tools.Visual;
using GH_IO.Serialization;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Componente cujo estado de controles (não a configuração nem a posição) pode ser capturado e restaurado pelo
    /// Pill Preset Vault e pelos snapshots do Project Vault.
    /// </summary>
    internal interface IPillControlStateProvider
    {
        /// <summary>Um <see cref="ControlState"/> por controle persistente (Id = "guidDoComponente|idDoControle").</summary>
        IEnumerable<ControlState> CaptureControlStates();

        /// <summary>Opções válidas por Id de controle (para checar compatibilidade de listas antes de restaurar).</summary>
        IDictionary<string, List<string>> ControlOptions();

        /// <summary>Aplica um estado salvo; devolve true se algo mudou (o chamador recalcula uma vez no fim).</summary>
        bool ApplyControlState(string controlId, ControlState saved);

        /// <summary>Estado em linhas <c>id=valor</c> (formato do Preset Vault).</summary>
        IList<string> ExportStateLines();

        /// <summary>Aplica linhas <c>id=valor</c>; devolve true se algo mudou.</summary>
        bool ImportStateLines(IEnumerable<string> lines);
    }

    /// <summary>Componente que publica canais no PillHub (evita que o PurgeOrphanChannels os trate como órfãos).</summary>
    internal interface IPillHubPublisher
    {
        IEnumerable<string> PublishedCleanKeys { get; }
    }

    /// <summary>Conversões entre dados do Grasshopper e valores de widget.</summary>
    internal static class DashboardGoo
    {
        public static WidgetValue FromGoo(IGH_Goo goo)
        {
            switch (goo)
            {
                case null:
                    return WidgetValue.None;
                case GH_Number n:
                    return WidgetValue.FromNumber(n.Value);
                case GH_Integer i:
                    return WidgetValue.FromNumber(i.Value);
                case GH_Boolean b:
                    return WidgetValue.FromBoolean(b.Value);
                case GH_String s:
                    return WidgetValue.FromText(s.Value);
                case GH_PillBundleGoo _:
                    return WidgetValue.FromText(goo.ToString());
            }
            if (goo is GH_ObjectWrapper w) return FromObject(w.Value);
            var number = new GH_Number();
            if (number.CastFrom(goo)) return WidgetValue.FromNumber(number.Value);
            return WidgetValue.FromText(goo.ToString());
        }

        /// <summary>Lista de itens → valor: um item vira escalar; vários números viram série; senão o primeiro item.</summary>
        public static WidgetValue FromGooList(IList<IGH_Goo> items)
        {
            if (items == null || items.Count == 0) return WidgetValue.None;
            var values = new List<WidgetValue>(items.Count);
            foreach (var g in items) values.Add(FromGoo(g));
            return FromValues(values);
        }

        /// <summary>Vários valores já convertidos → um valor (mesma regra de <see cref="FromGooList"/>).</summary>
        public static WidgetValue FromValues(IList<WidgetValue> values)
        {
            if (values == null || values.Count == 0) return WidgetValue.None;
            if (values.Count == 1) return values[0];
            var numbers = new List<double>(values.Count);
            foreach (var v in values)
            {
                if (v.Kind == WidgetValueKind.Number) numbers.Add(v.Number);
                else if (v.Kind == WidgetValueKind.Boolean) numbers.Add(v.Boolean ? 1 : 0);
                else if (v.IsNone) numbers.Add(double.NaN);
                else return values.First(x => !x.IsNone);
            }
            return WidgetValue.FromSeries(numbers);
        }

        /// <summary>Valores de PillBundle (double, int, bool, texto, listas) → valor de widget.</summary>
        public static WidgetValue FromObject(object value)
        {
            switch (value)
            {
                case null:
                    return WidgetValue.None;
                case IGH_Goo goo:
                    return FromGoo(goo);
                case bool b:
                    return WidgetValue.FromBoolean(b);
                case string s:
                    return WidgetValue.FromText(s);
                case double d:
                    return WidgetValue.FromNumber(d);
                case float f:
                    return WidgetValue.FromNumber(f);
                case int i:
                    return WidgetValue.FromNumber(i);
                case long l:
                    return WidgetValue.FromNumber(l);
                case decimal m:
                    return WidgetValue.FromNumber((double)m);
                case IEnumerable seq:
                {
                    var list = new List<IGH_Goo>();
                    foreach (var o in seq)
                    {
                        var v = FromObject(o);
                        switch (v.Kind)
                        {
                            case WidgetValueKind.Number: list.Add(new GH_Number(v.Number)); break;
                            case WidgetValueKind.Boolean: list.Add(new GH_Boolean(v.Boolean)); break;
                            case WidgetValueKind.None: list.Add(null); break;
                            default: list.Add(new GH_String(v.ToInvariantString())); break;
                        }
                    }
                    return FromGooList(list);
                }
                default:
                    return WidgetValue.FromText(Convert.ToString(value, CultureInfo.InvariantCulture));
            }
        }

        /// <summary>Valor de controle → item de saída (dropdown com output=index sai como inteiro).</summary>
        public static IGH_Goo ToGoo(WidgetSpec spec, WidgetValue value)
        {
            if (spec.Kind == WidgetKind.Dropdown && spec.OutputIndex)
            {
                int idx = -1;
                for (int i = 0; i < spec.Options.Count; i++)
                {
                    if (string.Equals(spec.Options[i], value.Text, StringComparison.OrdinalIgnoreCase)) idx = i;
                }
                return new GH_Integer(idx);
            }
            switch (value.Kind)
            {
                case WidgetValueKind.Number:
                    return spec.Kind == WidgetKind.Slider && spec.Decimals == 0 && Math.Abs(value.Number) < int.MaxValue
                        ? (IGH_Goo)new GH_Integer((int)Math.Round(value.Number))
                        : new GH_Number(value.Number);
                case WidgetValueKind.Boolean:
                    return new GH_Boolean(value.Boolean);
                case WidgetValueKind.Text:
                    return new GH_String(value.Text);
                default:
                    return null;
            }
        }

        /// <summary>Dados nomeados da entrada Data: PillBundles (entradas por nome, também sem o namespace) e textos "nome=valor".</summary>
        public static Dictionary<string, WidgetValue> NamedData(IEnumerable<IGH_Goo> items, ICollection<string> warnings)
        {
            var result = new Dictionary<string, WidgetValue>(StringComparer.OrdinalIgnoreCase);
            if (items == null) return result;
            foreach (var goo in items)
            {
                PillBundle bundle = null;
                if (goo is GH_PillBundleGoo bg) bundle = bg.Value;
                else if (goo is GH_ObjectWrapper ow && ow.Value is PillBundle pb) bundle = pb;

                if (bundle != null)
                {
                    foreach (var kv in bundle.Entries)
                    {
                        var v = FromObject(kv.Value);
                        result[kv.Key] = v;
                        int ns = kv.Key.LastIndexOf("::", StringComparison.Ordinal);
                        if (ns >= 0 && !result.ContainsKey(kv.Key.Substring(ns + 2))) result[kv.Key.Substring(ns + 2)] = v;
                    }
                    continue;
                }

                if (goo is GH_String s)
                {
                    foreach (var line in (s.Value ?? "").Replace("\r\n", "\n").Split('\n'))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0)
                        {
                            if (line.Trim().Length > 0) warnings?.Add($"Data: '{line.Trim()}' não está no formato nome=valor.");
                            continue;
                        }
                        string name = line.Substring(0, eq).Trim();
                        string text = line.Substring(eq + 1).Trim();
                        if (DashboardText.TryParseNumber(text, out double d)) result[name] = WidgetValue.FromNumber(d);
                        else if (text.IndexOf(';') >= 0 && DashboardText.ParseSeries(text).Count > 1) result[name] = WidgetValue.FromSeries(DashboardText.ParseSeries(text));
                        else result[name] = WidgetValue.FromText(text);
                    }
                    continue;
                }
                if (goo != null) warnings?.Add($"Data: item {goo.TypeName} ignorado (use PillBundle ou texto nome=valor).");
            }
            return result;
        }
    }

    /// <summary>
    /// Indicadores com <c>key=</c>: lê o canal do PillHub no momento da pintura (TryPeekChannel, O(1), sem cópia).
    /// Não inscreve o painel como receptor de propósito: se o Hub expirasse o Dashboard a cada resultado novo, tudo que
    /// depende dos controles dele recalcularia de novo (laço controle → simulação → resultado → controle).
    /// A conversão é guardada por canal e só refeita quando o canal é republicado.
    /// </summary>
    internal sealed class HubLiveSource : IDashboardLiveSource
    {
        private readonly Dictionary<string, (PillChannel channel, DateTime stamp, WidgetValue value, string status)> _cache =
            new Dictionary<string, (PillChannel, DateTime, WidgetValue, string)>(StringComparer.OrdinalIgnoreCase);

        public bool TryGet(string key, out WidgetValue value, out string status)
        {
            value = WidgetValue.None;
            status = null;
            if (string.IsNullOrWhiteSpace(key)) return false;
            if (!PillHub.TryPeekChannel(key, out var channel) || channel == null)
            {
                _cache.Remove(key);
                status = "canal ausente";
                return false;
            }
            if (_cache.TryGetValue(key, out var hit) && ReferenceEquals(hit.channel, channel) && hit.stamp == channel.LastUpdated)
            {
                value = hit.value;
                status = hit.status;
                return !value.IsNone;
            }

            var items = new List<IGH_Goo>();
            if (channel.Data != null)
            {
                foreach (var goo in channel.Data.AllData(false)) items.Add(goo);
            }
            value = DashboardGoo.FromGooList(items);
            status = value.IsNone ? "canal vazio" : null;
            if (_cache.Count > 512) _cache.Clear();
            _cache[key] = (channel, channel.LastUpdated, value, status);
            return !value.IsNone;
        }
    }

    internal static class DashboardColors
    {
        /// <summary>Cor de destaque: 'color=' (#hex, nome ou categoria das Pills), senão a categoria da chave do Hub, senão o padrão.</summary>
        public static Color Resolve(WidgetSpec spec, Color fallback)
        {
            if (spec.Color != null && TryParse(spec.Color, out Color c)) return c;
            if (spec.HubKey != null)
            {
                try
                {
                    // Só chaves com categoria ([GEO] Raio, ACU_T60); "GEN" é o cinza neutro de chaves sem categoria
                    PillHub.ParseKeyMetadata(spec.HubKey, null, out _, out string category, out _, out Color catColor);
                    if (!string.IsNullOrEmpty(category) && !category.Equals("GEN", StringComparison.OrdinalIgnoreCase)) return catColor;
                }
                catch
                {
                    // chave malformada: cor padrão
                }
            }
            return fallback;
        }

        public static bool TryParse(string text, out Color color)
        {
            color = Color.Empty;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim();
            if (t.StartsWith("#", StringComparison.Ordinal))
            {
                string hex = t.Substring(1);
                if ((hex.Length == 6 || hex.Length == 8) && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
                {
                    color = hex.Length == 6 ? Color.FromArgb((int)(0xFF000000 | argb)) : Color.FromArgb((int)argb);
                    return true;
                }
                return false;
            }
            var named = Color.FromName(t);
            if (named.IsKnownColor)
            {
                color = named;
                return true;
            }
            // Categoria das Pills (GEO, ACU, MAT...); categorias desconhecidas caem no cinza neutro e não contam
            string category = PillHub.NormalizeCategory(t);
            color = PillHub.GetCategoryColor(category);
            if (color.ToArgb() != PillHub.GetCategoryColor("GEN").ToArgb()) return true;
            color = Color.Empty;
            return false;
        }
    }

    /// <summary>Definição de widget como dado do Grasshopper (saída do Pill Dashboard Builder).</summary>
    public class GH_DashboardWidgetGoo : GH_Goo<WidgetSpec>
    {
        public GH_DashboardWidgetGoo()
        {
        }

        public GH_DashboardWidgetGoo(WidgetSpec spec)
        {
            Value = spec;
        }

        public override bool IsValid => Value != null;
        public override string TypeName => "Dashboard Widget";
        public override string TypeDescription => "Definição de um widget do Pill Dashboard (configuração + dado embutido).";
        public override IGH_Goo Duplicate() => new GH_DashboardWidgetGoo(Value);
        public override string ToString() => Value?.ToSpecLine() ?? "(vazio)";

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(GH_String)) && Value != null)
            {
                target = (Q)(object)new GH_String(Value.ToSpecLine());
                return true;
            }
            return false;
        }

        public override bool CastFrom(object source)
        {
            string text = null;
            if (source is GH_String s) text = s.Value;
            else if (source is string str) text = str;
            if (text == null) return false;
            var parsed = DashboardSpecParser.ParseWidgetLine(text.Trim(), null);
            if (parsed == null) return false;
            Value = parsed;
            return true;
        }

        public override bool Write(GH_IWriter writer)
        {
            if (Value != null)
            {
                writer.SetString("Spec", Value.ToSpecLine());
                if (!Value.EmbeddedValue.IsNone)
                {
                    writer.SetInt32("EmbeddedKind", (int)Value.EmbeddedValue.Kind);
                    writer.SetString("Embedded", Value.EmbeddedValue.ToInvariantString());
                }
            }
            return true;
        }

        public override bool Read(GH_IReader reader)
        {
            if (!reader.ItemExists("Spec")) return true;
            Value = DashboardSpecParser.ParseWidgetLine(reader.GetString("Spec"), null);
            if (Value != null && reader.ItemExists("EmbeddedKind"))
            {
                string text = reader.GetString("Embedded");
                WidgetValue embedded;
                switch ((WidgetValueKind)reader.GetInt32("EmbeddedKind"))
                {
                    case WidgetValueKind.Number:
                        embedded = DashboardText.TryParseNumber(text, out double d) ? WidgetValue.FromNumber(d) : WidgetValue.None;
                        break;
                    case WidgetValueKind.Boolean:
                        embedded = DashboardText.TryParseBoolean(text, out bool b) ? WidgetValue.FromBoolean(b) : WidgetValue.None;
                        break;
                    case WidgetValueKind.Series:
                        embedded = WidgetValue.FromSeries(DashboardText.ParseSeries(text));
                        break;
                    default:
                        embedded = WidgetValue.FromText(text);
                        break;
                }
                Value = Value.WithEmbeddedValue(embedded);
            }
            return true;
        }
    }
}
