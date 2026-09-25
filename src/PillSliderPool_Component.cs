// PillSliderPool_Component.cs
// Concentrador e banco vertical de sliders de parâmetros interativos para o Grasshopper (estilo Gene Pool enriquecido).
// Cada slider possui grupo/categoria com badge colorido, nome explícito, arrasto direto no Canvas,
// limites (Min/Max) e publicação automática em tempo real no barramento sem fios PillHub.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public enum PillDataType
    {
        Float,
        Integer,
        Boolean,
        Button,
        String,
        Domain,
        ValueList
    }

    public class PillSliderItem
    {
        public string Name { get; set; } = "Param";
        public string Category { get; set; } = "VAR";
        public Color CategoryColor { get; set; } = Color.FromArgb(139, 92, 246);
        public PillDataType DataType { get; set; } = PillDataType.Float;
        public double Value { get; set; } = 50.0;
        public double DefaultValue { get; set; } = 50.0;
        public double DomainEnd { get; set; } = 80.0;
        public double DefaultDomainEnd { get; set; } = 80.0;
        public double Min { get; set; } = 0.0;
        public double Max { get; set; } = 100.0;
        public int Decimals { get; set; } = 2;
        public string Unit { get; set; } = "";
        public bool ResetOnOpen { get; set; } = false;
        public DateTime LastToggleTime { get; set; } = DateTime.MinValue;

        public double DomainStart
        {
            get => Value;
            set => Value = value;
        }

        public string StringValue { get; set; } = "";
        public string DefaultStringValue { get; set; } = "";
        public List<string> StringOptions { get; set; } = new List<string>();

        public bool BoolValue
        {
            get => Value >= 0.5;
            set => Value = value ? 1.0 : 0.0;
        }

        public int IntValue
        {
            get => (int)Math.Round(Value);
            set => Value = value;
        }

        public string TypeBadge
        {
            get
            {
                switch (DataType)
                {
                    case PillDataType.Integer: return "int";
                    case PillDataType.Boolean: return "bool";
                    case PillDataType.Button: return "btn";
                    case PillDataType.String: return "str";
                    case PillDataType.Domain: return "dom";
                    case PillDataType.ValueList: return "list";
                    default: return "float";
                }
            }
        }

        public string FullKey => string.IsNullOrWhiteSpace(Category) ? Name : $"[{Category}] {Name}";
        public string CleanKey => PillHub.CleanUpKey(FullKey);

        public RectangleF BoundsRow { get; set; }
        public RectangleF BoundsBadge { get; set; }
        public RectangleF BoundsType { get; set; }
        public RectangleF BoundsName { get; set; }
        public RectangleF BoundsTrack { get; set; }
        public RectangleF BoundsValueBox { get; set; }

        public PillSliderItem Clone()
        {
            return new PillSliderItem
            {
                Name = this.Name,
                Category = this.Category,
                CategoryColor = this.CategoryColor,
                DataType = this.DataType,
                Value = this.Value,
                DefaultValue = this.DefaultValue,
                DomainEnd = this.DomainEnd,
                DefaultDomainEnd = this.DefaultDomainEnd,
                Min = this.Min,
                Max = this.Max,
                Decimals = this.Decimals,
                Unit = this.Unit,
                ResetOnOpen = this.ResetOnOpen,
                StringValue = this.StringValue,
                DefaultStringValue = this.DefaultStringValue,
                StringOptions = new List<string>(this.StringOptions)
            };
        }
    }

    public class PillSliderPool_Component : GH_Component
    {
        public List<PillSliderItem> Sliders { get; private set; } = new List<PillSliderItem>();
        public string PoolGroup { get; set; } = "VAR";
        public string PoolTitle { get; set; } = "Pill Slider Pool";

        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(NickName) &&
                    !NickName.Equals("SliderPool", StringComparison.OrdinalIgnoreCase))
                {
                    return NickName;
                }
                if (!string.IsNullOrWhiteSpace(PoolTitle))
                {
                    return PoolTitle;
                }
                return "Pill Slider Pool";
            }
        }

        private readonly HashSet<string> _publishedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (double Val, double DomEnd, string StrVal, bool BoolVal)> _lastPublishedValues
            = new Dictionary<string, (double, double, string, bool)>(StringComparer.OrdinalIgnoreCase);
        private bool _hasResetOnOpen = false;

        public PillSliderPool_Component()
            : base(
                "Pill Slider Pool",
                "Pill Slider Pool",
                "Banco concentrador de sliders com publicação sem fios para o ecossistema Pill.\n" +
                "- Reúne múltiplos parâmetros interativos com tipos (float, int, bool, string), nomes e grupos coloridos.\n" +
                "- Arraste os sliders ou alterne os toggles diretamente no Canvas para atualizar qualquer Pill Receiver em tempo real.\n" +
                "- Configure via painel (Panel), duplo-clique no cabeçalho ou menu de clique direito.",
                "Glaux Tools",
                "Pills")
        {
            // Sliders padrão iniciais demonstrando os diferentes tipos de dados
            Sliders.Add(new PillSliderItem { Name = "Raio", Category = "GEO", CategoryColor = PillHub.GetCategoryColor("GEO"), DataType = PillDataType.Float, Value = 12.5, DefaultValue = 12.5, Min = 1.0, Max = 50.0, Decimals = 1, Unit = "m" });
            Sliders.Add(new PillSliderItem { Name = "Pessoas", Category = "GEO", CategoryColor = PillHub.GetCategoryColor("GEO"), DataType = PillDataType.Integer, Value = 50.0, DefaultValue = 50.0, Min = 1.0, Max = 200.0, Decimals = 0 });
            Sliders.Add(new PillSliderItem { Name = "Frequencia", Category = "ACU", CategoryColor = PillHub.GetCategoryColor("ACU"), DataType = PillDataType.Domain, Value = 250.0, DefaultValue = 250.0, DomainEnd = 4000.0, DefaultDomainEnd = 4000.0, Min = 100.0, Max = 5000.0, Decimals = 0, Unit = "Hz" });
            Sliders.Add(new PillSliderItem { Name = "Simular", Category = "SIM", CategoryColor = PillHub.GetCategoryColor("SIM"), DataType = PillDataType.Boolean, Value = 1.0, DefaultValue = 0.0, Min = 0.0, Max = 1.0, Decimals = 0, ResetOnOpen = true });
            Sliders.Add(new PillSliderItem { Name = "Absorcao", Category = "MAT", CategoryColor = PillHub.GetCategoryColor("MAT"), DataType = PillDataType.Float, Value = 0.25, DefaultValue = 0.25, Min = 0.02, Max = 0.95, Decimals = 2 });
        }

        public override Guid ComponentGuid => new Guid("a110000f-e1ef-4000-8000-00000000000f");
        protected override Bitmap Icon => GlauxToolsIcons.PillSliderPool;

        public override void CreateAttributes()
        {
            m_attributes = new PillSliderPool_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Def", "C", "Definições opcionais via texto/Panel para configurar sliders dinamicamente (ex: '[GEO] Raio = 10 (0..50)').", GH_ParamAccess.list);
            pManager[0].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Values", "V", "Lista ordenada com os valores numéricos atuais de todos os sliders (compatível com Gene Pool).", GH_ParamAccess.list);
            pManager.AddTextParameter("Keys", "K", "Lista com os nomes e chaves de todos os parâmetros publicados no PillHub.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Tree", "T", "Árvore de dados (DataTree) com cada parâmetro em um ramo estruturado com seus metadados.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 0. Auto-Reset na primeira execução pós-abertura do arquivo (estilo Ladybug FalseStartToggle)
            if (!_hasResetOnOpen)
            {
                _hasResetOnOpen = true;
                foreach (var s in Sliders)
                {
                    if (s.ResetOnOpen)
                    {
                        s.Value = s.DefaultValue;
                        if (s.DataType == PillDataType.Domain)
                        {
                            s.DomainEnd = s.DefaultDomainEnd;
                        }
                    }
                }
            }

            // 1. Verificar se há definições conectadas via entrada Def (Panel)
            var defLines = new List<string>();
            if (DA.GetDataList(0, defLines) && defLines.Count > 0)
            {
                foreach (var l in defLines)
                {
                    if (string.IsNullOrWhiteSpace(l)) continue;
                    string trimmed = l.Trim();
                    if (trimmed.StartsWith("Title =", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("Titulo =", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("Nome =", StringComparison.OrdinalIgnoreCase))
                    {
                        int eq = trimmed.IndexOf('=');
                        if (eq > 0)
                        {
                            string tVal = trimmed.Substring(eq + 1).Trim();
                            if (!string.IsNullOrWhiteSpace(tVal) && !string.Equals(PoolTitle, tVal, StringComparison.Ordinal))
                            {
                                PoolTitle = tVal;
                                NickName = tVal;
                                m_attributes?.ExpireLayout();
                            }
                        }
                    }
                    else if (trimmed.StartsWith("[TITLE]", StringComparison.OrdinalIgnoreCase) ||
                             trimmed.StartsWith("[TITULO]", StringComparison.OrdinalIgnoreCase))
                    {
                        string tVal = trimmed.Substring(trimmed.IndexOf(']') + 1).Trim();
                        if (!string.IsNullOrWhiteSpace(tVal) && !string.Equals(PoolTitle, tVal, StringComparison.Ordinal))
                        {
                            PoolTitle = tVal;
                            NickName = tVal;
                            m_attributes?.ExpireLayout();
                        }
                    }
                }

                var parsed = ParseSliderDefinitions(defLines);
                if (parsed.Count > 0)
                {
                    SyncParsedSliders(parsed);
                }
            }

            // 2. Publicar todos os sliders no PillHub
            var doc = OnPingDocument();
            Guid docGuid = doc != null ? doc.DocumentID : Guid.Empty;

            var currentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var valuesList = new List<double>();
            var keysList = new List<string>();
            var tree = new GH_Structure<IGH_Goo>();

            for (int i = 0; i < Sliders.Count; i++)
            {
                var s = Sliders[i];
                keysList.Add(s.FullKey);
                currentKeys.Add(s.CleanKey);

                var branchPath = new GH_Path(i);

                IGH_Goo valGoo;
                double numVal;

                switch (s.DataType)
                {
                    case PillDataType.Integer:
                        valGoo = new GH_Integer(s.IntValue);
                        numVal = s.IntValue;
                        break;
                    case PillDataType.Boolean:
                    case PillDataType.Button:
                        valGoo = new GH_Boolean(s.BoolValue);
                        numVal = s.BoolValue ? 1.0 : 0.0;
                        break;
                    case PillDataType.String:
                    case PillDataType.ValueList:
                        valGoo = new GH_String(s.StringValue);
                        numVal = s.Value;
                        break;
                    case PillDataType.Domain:
                        valGoo = new GH_Interval(new Rhino.Geometry.Interval(s.DomainStart, s.DomainEnd));
                        numVal = s.DomainStart;
                        break;
                    default:
                        valGoo = new GH_Number(s.Value);
                        numVal = s.Value;
                        break;
                }

                valuesList.Add(numVal);
                tree.Append(valGoo, branchPath);

                // Só publica no canal PillHub se os valores mudaram ou se o canal ainda não foi publicado
                bool shouldPublish = true;
                if (_lastPublishedValues.TryGetValue(s.CleanKey, out var prev))
                {
                    bool valSame = Math.Abs(prev.Val - s.Value) < 1e-9;
                    bool domSame = Math.Abs(prev.DomEnd - s.DomainEnd) < 1e-9;
                    bool strSame = string.Equals(prev.StrVal ?? "", s.StringValue ?? "", StringComparison.Ordinal);
                    bool boolSame = prev.BoolVal == s.BoolValue;
                    if (valSame && domSame && strSame && boolSame)
                    {
                        shouldPublish = false;
                    }
                }

                if (shouldPublish)
                {
                    _lastPublishedValues[s.CleanKey] = (s.Value, s.DomainEnd, s.StringValue, s.BoolValue);

                    // Publica no canal PillHub
                    var sTree = new GH_Structure<IGH_Goo>();
                    sTree.Append(valGoo);

                    PillHub.Publish(s.FullKey, sTree, InstanceGuid, docGuid, s.Unit);

                    // Publica também sob chave simples para busca direta (ex: "GEO_Raio")
                    string simpleKey = $"{s.Category}_{s.Name}";
                    if (!simpleKey.Equals(s.FullKey, StringComparison.OrdinalIgnoreCase))
                    {
                        PillHub.Publish(simpleKey, sTree, InstanceGuid, docGuid, s.Unit);
                        currentKeys.Add(PillHub.CleanUpKey(simpleKey));
                    }
                }
            }

            // Despublicar chaves que foram removidas
            foreach (var oldKey in _publishedKeys.ToList())
            {
                if (!currentKeys.Contains(oldKey))
                {
                    PillHub.Unpublish(oldKey, InstanceGuid);
                    _publishedKeys.Remove(oldKey);
                    _lastPublishedValues.Remove(oldKey);
                }
            }
            foreach (var k in currentKeys) _publishedKeys.Add(k);

            // 3. Enviar saídas
            DA.SetDataList(0, valuesList);
            DA.SetDataList(1, keysList);
            DA.SetDataTree(2, tree);

            Message = $"{Sliders.Count} Sliders";
        }

        public bool HasConnectedOutputs()
        {
            if (Params == null || Params.Output == null) return false;
            foreach (var p in Params.Output)
            {
                if (p.Recipients == null || p.Recipients.Count == 0) continue;
                bool hasExternal = p.Recipients.Any(r => 
                {
                    var topObj = r.Attributes?.GetTopLevel?.DocObject;
                    if (topObj == null) return true;
                    // Componentes do ecossistema Pill operam de forma assíncrona/sem fios via barramento PillHub
                    if (topObj is PillHook_Component || topObj is PillReceiver_Component) return false;
                    return true;
                });
                if (hasExternal) return true;
            }
            return false;
        }

        public void PublishSingleSlider(PillSliderItem slider)
        {
            if (slider == null) return;
            var doc = OnPingDocument();
            Guid docGuid = doc != null ? doc.DocumentID : Guid.Empty;

            IGH_Goo valGoo;
            switch (slider.DataType)
            {
                case PillDataType.Integer:
                    valGoo = new GH_Integer(slider.IntValue);
                    break;
                case PillDataType.Boolean:
                case PillDataType.Button:
                    valGoo = new GH_Boolean(slider.BoolValue);
                    break;
                case PillDataType.String:
                case PillDataType.ValueList:
                    valGoo = new GH_String(slider.StringValue);
                    break;
                case PillDataType.Domain:
                    valGoo = new GH_Interval(new Rhino.Geometry.Interval(slider.DomainStart, slider.DomainEnd));
                    break;
                default:
                    valGoo = new GH_Number(slider.Value);
                    break;
            }

            var sTree = new GH_Structure<IGH_Goo>();
            sTree.Append(valGoo);

            _lastPublishedValues[slider.CleanKey] = (slider.Value, slider.DomainEnd, slider.StringValue, slider.BoolValue);

            PillHub.Publish(slider.FullKey, sTree, InstanceGuid, docGuid, slider.Unit);

            string simpleKey = $"{slider.Category}_{slider.Name}";
            if (!simpleKey.Equals(slider.FullKey, StringComparison.OrdinalIgnoreCase))
            {
                PillHub.Publish(simpleKey, sTree, InstanceGuid, docGuid, slider.Unit);
            }
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            foreach (var key in _publishedKeys)
            {
                PillHub.Unpublish(key, InstanceGuid);
            }
            _publishedKeys.Clear();
            _lastPublishedValues.Clear();
            base.RemovedFromDocument(document);
        }

        public void SyncParsedSliders(List<PillSliderItem> parsedList)
        {
            // Preserva valores existentes se o slider já existia, a menos que seja abertura de arquivo com ResetOnOpen
            for (int i = 0; i < parsedList.Count; i++)
            {
                var p = parsedList[i];
                var existing = Sliders.FirstOrDefault(s => string.Equals(s.CleanKey, p.CleanKey, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    if (p.ResetOnOpen && !_hasResetOnOpen)
                    {
                        p.Value = p.DefaultValue;
                        p.DomainEnd = p.DefaultDomainEnd;
                        p.StringValue = p.DefaultStringValue;
                    }
                    else
                    {
                        p.Value = Math.Max(p.Min, Math.Min(p.Max, existing.Value));
                        p.DomainEnd = Math.Max(p.Min, Math.Min(p.Max, existing.DomainEnd));
                        p.StringValue = existing.StringValue;
                        p.BoolValue = existing.BoolValue;
                        p.IntValue = existing.IntValue;
                    }
                }
                else
                {
                    p.Value = p.DefaultValue;
                    p.DomainEnd = p.DefaultDomainEnd;
                    p.StringValue = p.DefaultStringValue;
                }
            }
            Sliders = parsedList;
            m_attributes?.ExpireLayout();
        }

        public static List<PillSliderItem> ParseSliderDefinitions(IEnumerable<string> lines)
        {
            var result = new List<PillSliderItem>();
            if (lines == null) return result;

            foreach (var rawLine in lines)
            {
                if (string.IsNullOrWhiteSpace(rawLine)) continue;
                string line = rawLine.Trim();
                if (line.StartsWith("#") || line.StartsWith("//")) continue;

                if (line.StartsWith("Title =", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Titulo =", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Nome =", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("[TITLE]", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("[TITULO]", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Formato CSV / Semicolon: CAT; Name; Min; Max; Val; Unit; [type]; [r]
                if (line.Contains(";") || (line.Contains(",") && !line.Contains("=")))
                {
                    string[] parts = line.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        string cat = parts.Length >= 5 ? parts[0].Trim() : "VAR";
                        string name = parts.Length >= 5 ? parts[1].Trim() : parts[0].Trim();
                        double.TryParse(parts.Length >= 5 ? parts[2] : (parts.Length >= 3 ? parts[1] : "0"), NumberStyles.Any, CultureInfo.InvariantCulture, out double csvMin);
                        double.TryParse(parts.Length >= 5 ? parts[3] : (parts.Length >= 3 ? parts[2] : "100"), NumberStyles.Any, CultureInfo.InvariantCulture, out double csvMax);

                        double csvVal = 50.0;
                        double csvDomEnd = 80.0;
                        bool csvHasDomainVal = false;

                        string valPartRaw = parts.Length >= 5 ? parts[4].Trim() : (parts.Length >= 4 ? parts[3].Trim() : "50");
                        var csvDomMatch = Regex.Match(valPartRaw, @"(?<v0>[0-9\.,\-]+)\s*(?:\.\.|\s+to\s+)\s*(?<v1>[0-9\.,\-]+)", RegexOptions.IgnoreCase);
                        if (csvDomMatch.Success)
                        {
                            double.TryParse(csvDomMatch.Groups["v0"].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out csvVal);
                            double.TryParse(csvDomMatch.Groups["v1"].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out csvDomEnd);
                            csvHasDomainVal = true;
                        }
                        else
                        {
                            double.TryParse(valPartRaw.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out csvVal);
                            csvDomEnd = csvMax;
                        }

                        if (csvMax <= csvMin) csvMax = csvMin + 10.0;
                        csvVal = Math.Max(csvMin, Math.Min(csvMax, csvVal));
                        csvDomEnd = Math.Max(csvMin, Math.Min(csvMax, csvDomEnd));

                        string unit = "";
                        bool resetOnOpen = false;
                        PillDataType dt = PillDataType.Float;

                        for (int p = 5; p < parts.Length; p++)
                        {
                            string pVal = parts[p].Trim().ToLowerInvariant();
                            if (pVal == "r" || pVal == "reset") resetOnOpen = true;
                            else if (pVal == "int" || pVal == "integer") dt = PillDataType.Integer;
                            else if (pVal == "bool" || pVal == "boolean") dt = PillDataType.Boolean;
                            else if (pVal == "btn" || pVal == "button" || pVal == "trigger" || pVal == "pulse") dt = PillDataType.Button;
                            else if (pVal == "str" || pVal == "string" || pVal == "text") dt = PillDataType.String;
                            else if (pVal == "dom" || pVal == "domain" || pVal == "interval") dt = PillDataType.Domain;
                            else if (pVal == "float" || pVal == "double") dt = PillDataType.Float;
                            else if (string.IsNullOrEmpty(unit)) unit = parts[p].Trim();
                        }

                        if (csvHasDomainVal && dt == PillDataType.Float)
                        {
                            dt = PillDataType.Domain;
                        }

                        result.Add(new PillSliderItem
                        {
                            Category = cat.ToUpperInvariant(),
                            CategoryColor = PillHub.GetCategoryColor(cat),
                            Name = CleanIdentifier(name),
                            DataType = dt,
                            Min = dt == PillDataType.Integer ? Math.Round(csvMin) : csvMin,
                            Max = dt == PillDataType.Integer ? Math.Round(csvMax) : csvMax,
                            Value = dt == PillDataType.Integer ? Math.Round(csvVal) : csvVal,
                            DefaultValue = dt == PillDataType.Integer ? Math.Round(csvVal) : csvVal,
                            DomainEnd = dt == PillDataType.Integer ? Math.Round(csvDomEnd) : csvDomEnd,
                            DefaultDomainEnd = dt == PillDataType.Integer ? Math.Round(csvDomEnd) : csvDomEnd,
                            Decimals = dt == PillDataType.Integer ? 0 : ((csvVal.ToString(CultureInfo.InvariantCulture).Contains(".") || csvMin.ToString(CultureInfo.InvariantCulture).Contains(".")) ? 2 : 0),
                            Unit = unit,
                            ResetOnOpen = resetOnOpen
                        });
                        continue;
                    }
                }

                // Extrair Categoria no início se existir: [CAT]
                string category = "VAR";
                string remaining = line;
                if (remaining.StartsWith("["))
                {
                    int closeIdx = remaining.IndexOf(']');
                    if (closeIdx > 1)
                    {
                        category = remaining.Substring(1, closeIdx - 1).Trim().ToUpperInvariant();
                        remaining = remaining.Substring(closeIdx + 1).Trim();
                    }
                }

                // Identificar tags em colchetes: [m], [int], [float], [bool], [str], [dom], [r], etc.
                bool resetFlag = false;
                string detectedUnit = "";
                PillDataType detectedType = PillDataType.Float;
                bool typeExplicit = false;

                var tagMatches = Regex.Matches(remaining, @"\[(?<tag>[^\]]+)\]");
                foreach (Match tm in tagMatches)
                {
                    string t = tm.Groups["tag"].Value.Trim().ToLowerInvariant();
                    if (t == "r" || t == "reset")
                    {
                        resetFlag = true;
                    }
                    else if (t == "int" || t == "integer")
                    {
                        detectedType = PillDataType.Integer;
                        typeExplicit = true;
                    }
                    else if (t == "bool" || t == "boolean")
                    {
                        detectedType = PillDataType.Boolean;
                        typeExplicit = true;
                    }
                    else if (t == "btn" || t == "button" || t == "trigger" || t == "pulse" || t == "pulso" || t == "disparo")
                    {
                        detectedType = PillDataType.Button;
                        typeExplicit = true;
                    }
                    else if (t == "str" || t == "string" || t == "text")
                    {
                        detectedType = PillDataType.String;
                        typeExplicit = true;
                    }
                    else if (t == "dom" || t == "domain" || t == "interval")
                    {
                        detectedType = PillDataType.Domain;
                        typeExplicit = true;
                    }
                    else if (t == "list" || t == "valuelist" || t == "vlist" || t == "dropdown" || t == "options")
                    {
                        detectedType = PillDataType.ValueList;
                        typeExplicit = true;
                    }
                    else if (t == "float" || t == "double" || t == "num" || t == "number")
                    {
                        detectedType = PillDataType.Float;
                        typeExplicit = true;
                    }
                    else if (string.IsNullOrEmpty(detectedUnit))
                    {
                        detectedUnit = tm.Groups["tag"].Value.Trim();
                    }
                }

                // Remove os blocos de colchetes do corpo principal para ler Nome = Valor (Min .. Max)
                string core = Regex.Replace(remaining, @"\[[^\]]+\]", " ").Trim();

                int eqIdx = core.IndexOf('=');
                if (eqIdx > 0)
                {
                    string name = CleanIdentifier(core.Substring(0, eqIdx).Trim());
                    string rightSide = core.Substring(eqIdx + 1).Trim();

                    // 1. Caso BOOLEAN explícito ou valor True/False
                    if (detectedType == PillDataType.Boolean || (!typeExplicit && (rightSide.StartsWith("true", StringComparison.OrdinalIgnoreCase) || rightSide.StartsWith("false", StringComparison.OrdinalIgnoreCase))))
                    {
                        detectedType = PillDataType.Boolean;
                        bool bVal = rightSide.StartsWith("true", StringComparison.OrdinalIgnoreCase) || rightSide.StartsWith("1");
                        double boolNum = bVal ? 1.0 : 0.0;

                        result.Add(new PillSliderItem
                        {
                            Category = category,
                            CategoryColor = PillHub.GetCategoryColor(category),
                            Name = name,
                            DataType = PillDataType.Boolean,
                            Value = boolNum,
                            DefaultValue = boolNum,
                            Min = 0.0,
                            Max = 1.0,
                            Decimals = 0,
                            Unit = detectedUnit,
                            ResetOnOpen = resetFlag
                        });
                        continue;
                    }

                    // 1b. Caso BUTTON explícito (Disparo / Pulso)
                    if (detectedType == PillDataType.Button)
                    {
                        result.Add(new PillSliderItem
                        {
                            Category = category,
                            CategoryColor = PillHub.GetCategoryColor(category),
                            Name = name,
                            DataType = PillDataType.Button,
                            Value = 0.0,
                            DefaultValue = 0.0,
                            Min = 0.0,
                            Max = 1.0,
                            Decimals = 0,
                            Unit = detectedUnit,
                            ResetOnOpen = true
                        });
                        continue;
                    }

                    // 2. Caso STRING explícito ou valor com aspas ou lista de opções textuais
                    if (detectedType == PillDataType.String || (!typeExplicit && rightSide.StartsWith("\"")))
                    {
                        detectedType = PillDataType.String;
                        var options = new List<string>();
                        string strVal = "";

                        var optMatch = Regex.Match(rightSide, @"\((?<opts>[^\)]+)\)");
                        if (optMatch.Success)
                        {
                            string rawOpts = optMatch.Groups["opts"].Value;
                            string[] sep = rawOpts.Contains("..") ? new[] { ".." } : new[] { ",", ";" };
                            options = rawOpts.Split(sep, StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim().Trim('"', '\'')).Where(o => !string.IsNullOrEmpty(o)).ToList();

                            string strValPart = rightSide.Substring(0, optMatch.Index).Trim().Trim('"', '\'');
                            strVal = !string.IsNullOrEmpty(strValPart) ? strValPart : (options.Count > 0 ? options[0] : "");
                        }
                        else
                        {
                            strVal = rightSide.Trim('"', '\'');
                        }

                        int selIdx = options.FindIndex(o => o.Equals(strVal, StringComparison.OrdinalIgnoreCase));
                        if (selIdx < 0) selIdx = 0;
                        if (options.Count > 0 && selIdx < options.Count) strVal = options[selIdx];

                        result.Add(new PillSliderItem
                        {
                            Category = category,
                            CategoryColor = PillHub.GetCategoryColor(category),
                            Name = name,
                            DataType = PillDataType.String,
                            StringValue = strVal,
                            DefaultStringValue = strVal,
                            StringOptions = options,
                            Value = selIdx,
                            DefaultValue = selIdx,
                            Min = 0.0,
                            Max = Math.Max(1.0, options.Count > 0 ? options.Count - 1 : 1.0),
                            Decimals = 0,
                            Unit = detectedUnit,
                            ResetOnOpen = resetFlag
                        });
                        continue;
                    }

                    // 3. Limites opcionais (Min .. Max)
                    double min = 0.0;
                    double max = 100.0;
                    var rangeMatch = Regex.Match(rightSide, @"[\(\[]\s*(?<min>[0-9\.,\-]+)\s*(?:\.\.|\:|\-|\s+a\s+|\s+to\s+)\s*(?<max>[0-9\.,\-]+)\s*[\)\]]", RegexOptions.IgnoreCase);
                    string valPart = rightSide;
                    if (rangeMatch.Success)
                    {
                        string minStr = rangeMatch.Groups["min"].Value.Replace(',', '.');
                        string maxStr = rangeMatch.Groups["max"].Value.Replace(',', '.');
                        double.TryParse(minStr, NumberStyles.Any, CultureInfo.InvariantCulture, out min);
                        double.TryParse(maxStr, NumberStyles.Any, CultureInfo.InvariantCulture, out max);
                        valPart = rightSide.Substring(0, rangeMatch.Index).Trim();
                    }

                    // Caso DOMÍNIO / INTERVALO (Domain / Interval) explícito ou formato Início .. Fim
                    var domValMatch = Regex.Match(valPart, @"(?<v0>[0-9\.,\-]+)\s*(?:\.\.|\s+to\s+|\s+a\s+)\s*(?<v1>[0-9\.,\-]+)", RegexOptions.IgnoreCase);
                    if (detectedType == PillDataType.Domain || domValMatch.Success)
                    {
                        detectedType = PillDataType.Domain;
                        double dStart = 20.0;
                        double dEnd = 80.0;
                        int domDecimals = 2;

                        if (domValMatch.Success)
                        {
                            string v0Str = domValMatch.Groups["v0"].Value.Replace(',', '.');
                            string v1Str = domValMatch.Groups["v1"].Value.Replace(',', '.');
                            double.TryParse(v0Str, NumberStyles.Any, CultureInfo.InvariantCulture, out dStart);
                            double.TryParse(v1Str, NumberStyles.Any, CultureInfo.InvariantCulture, out dEnd);

                            if (!rangeMatch.Success)
                            {
                                min = Math.Min(0.0, Math.Min(dStart, dEnd));
                                max = Math.Max(100.0, Math.Max(dStart, dEnd) * 1.5);
                            }

                            if (v0Str.Contains(".") || v1Str.Contains("."))
                            {
                                int d0 = v0Str.Contains(".") ? v0Str.Length - v0Str.IndexOf('.') - 1 : 0;
                                int d1 = v1Str.Contains(".") ? v1Str.Length - v1Str.IndexOf('.') - 1 : 0;
                                domDecimals = Math.Min(4, Math.Max(d0, d1));
                            }
                            else
                            {
                                domDecimals = (rangeMatch.Success && (rangeMatch.Groups["min"].Value.Contains(".") || rangeMatch.Groups["max"].Value.Contains("."))) ? 2 : 0;
                            }
                        }
                        else
                        {
                            var vMatch = Regex.Match(valPart, @"(?<val>[0-9\.,\-]+)");
                            if (vMatch.Success)
                            {
                                string vStr = vMatch.Groups["val"].Value.Replace(',', '.');
                                double.TryParse(vStr, NumberStyles.Any, CultureInfo.InvariantCulture, out dStart);
                                dEnd = Math.Min(max, dStart + (max - min) * 0.25);
                                if (vStr.Contains(".")) domDecimals = Math.Min(4, vStr.Length - vStr.IndexOf('.') - 1);
                                else domDecimals = 0;
                            }
                            else
                            {
                                dStart = min;
                                dEnd = max;
                            }
                        }

                        if (max <= min) max = min + 10.0;
                        if (dStart > dEnd) { double t = dStart; dStart = dEnd; dEnd = t; }
                        dStart = Math.Max(min, Math.Min(max, dStart));
                        dEnd = Math.Max(min, Math.Min(max, dEnd));

                        result.Add(new PillSliderItem
                        {
                            Category = category,
                            CategoryColor = PillHub.GetCategoryColor(category),
                            Name = name,
                            DataType = PillDataType.Domain,
                            Value = dStart,
                            DefaultValue = dStart,
                            DomainEnd = dEnd,
                            DefaultDomainEnd = dEnd,
                            Min = min,
                            Max = max,
                            Decimals = domDecimals,
                            Unit = detectedUnit,
                            ResetOnOpen = resetFlag
                        });
                        continue;
                    }

                    // 4. Caso NUMÉRICO (Float ou Integer)
                    double val = 50.0;
                    int decimals = (detectedType == PillDataType.Integer) ? 0 : 2;

                    if (rangeMatch.Success)
                    {
                        var valMatch = Regex.Match(valPart, @"(?<val>[0-9\.,\-]+)");
                        if (valMatch.Success)
                        {
                            string valStr = valMatch.Groups["val"].Value.Replace(',', '.');
                            double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out val);
                            if (detectedType != PillDataType.Integer)
                            {
                                if (valStr.Contains(".")) decimals = Math.Min(4, valStr.Length - valStr.IndexOf('.') - 1);
                                else decimals = (rangeMatch.Groups["min"].Value.Contains(".") || rangeMatch.Groups["max"].Value.Contains(".")) ? 2 : 0;
                            }
                        }
                        else
                        {
                            val = min;
                        }
                    }
                    else
                    {
                        var valMatch = Regex.Match(rightSide, @"(?<val>[0-9\.,\-]+)");
                        if (valMatch.Success)
                        {
                            string valStr = valMatch.Groups["val"].Value.Replace(',', '.');
                            double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out val);
                            min = Math.Min(0.0, val);
                            max = Math.Max(100.0, val > 0 ? val * 2.0 : 100.0);
                            if (detectedType != PillDataType.Integer && valStr.Contains("."))
                            {
                                decimals = Math.Min(4, valStr.Length - valStr.IndexOf('.') - 1);
                            }
                        }
                    }

                    if (max <= min) max = min + 10.0;

                    if (detectedType == PillDataType.Integer)
                    {
                        min = Math.Round(min);
                        max = Math.Round(max);
                        val = Math.Round(val);
                        decimals = 0;
                    }
                    else
                    {
                        val = Math.Max(min, Math.Min(max, val));
                    }

                    result.Add(new PillSliderItem
                    {
                        Category = category,
                        CategoryColor = PillHub.GetCategoryColor(category),
                        Name = name,
                        DataType = detectedType,
                        Value = val,
                        DefaultValue = val,
                        Min = min,
                        Max = max,
                        Decimals = decimals,
                        Unit = detectedUnit,
                        ResetOnOpen = resetFlag
                    });
                }
                else
                {
                    // Fallback para linha simples com apenas o nome
                    string name = CleanIdentifier(core);
                    result.Add(new PillSliderItem
                    {
                        Category = category,
                        CategoryColor = PillHub.GetCategoryColor(category),
                        Name = name,
                        DataType = detectedType,
                        Min = 0.0,
                        Max = (detectedType == PillDataType.Button) ? 1.0 : 100.0,
                        Value = (detectedType == PillDataType.Domain) ? 20.0 : ((detectedType == PillDataType.Button) ? 0.0 : 50.0),
                        DefaultValue = (detectedType == PillDataType.Domain) ? 20.0 : ((detectedType == PillDataType.Button) ? 0.0 : 50.0),
                        DomainEnd = 80.0,
                        DefaultDomainEnd = 80.0,
                        Decimals = (detectedType == PillDataType.Integer || detectedType == PillDataType.Button) ? 0 : 1,
                        Unit = detectedUnit,
                        ResetOnOpen = (detectedType == PillDataType.Button) ? true : resetFlag
                    });
                }
            }
            return result;
        }

        private static string CleanIdentifier(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Param";
            string s = raw.Trim().Replace(" ", "_").Replace("-", "_");
            return Regex.Replace(s, @"[^\p{L}\p{Nd}_]", "");
        }

        public void PromptSliderEditor()
        {
            var form = new Form
            {
                Text = "Editor de Parâmetros - Pill Slider Pool",
                Width = 640,
                Height = 540,
                MinimumSize = new Size(540, 400),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.Sizable,
                MinimizeBox = false,
                MaximizeBox = false,
                Font = new Font("Segoe UI", 9f)
            };

            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 128,
                BackColor = Color.FromArgb(248, 250, 252)
            };

            var lblTitle = new Label
            {
                Text = "Nome da Pilha:",
                Location = new Point(14, 13),
                Size = new Size(105, 22),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var txtTitle = new TextBox
            {
                Text = DisplayTitle,
                Location = new Point(122, 11),
                Size = new Size(form.ClientSize.Width - 138, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9.5f)
            };

            var lblHeader = new Label
            {
                Text = "Defina os sliders escrevendo uma linha por parâmetro:\n" +
                       "• Tipos suportados: [float], [int], [bool], [btn], [str], [dom]   |   Tag [r]: reseta ao abrir o arquivo\n" +
                       "• Ex: [ACU] Banda = 20 .. 80 (0 .. 100) [dom] [r]   |   [SIM] Disparar [btn]   |   [CFG] Ativo = False [bool] [r]",
                Location = new Point(14, 46),
                Size = new Size(form.ClientSize.Width - 28, 70),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 8.8f),
                ForeColor = Color.FromArgb(51, 65, 85)
            };

            var lineSep = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Color.FromArgb(226, 232, 240)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(txtTitle);
            pnlHeader.Controls.Add(lblHeader);
            pnlHeader.Controls.Add(lineSep);

            var txtEditor = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10f),
                Padding = new Padding(8),
                AcceptsReturn = true,
                AcceptsTab = true
            };

            // Preenche texto com a configuração atual
            var lines = Sliders.Select(s =>
            {
                string unStr = !string.IsNullOrEmpty(s.Unit) ? $" [{s.Unit}]" : "";
                string typeStr = $" [{s.TypeBadge}]";
                string rStr = s.ResetOnOpen ? " [r]" : "";

                if (s.DataType == PillDataType.Boolean)
                {
                    bool bVal = s.ResetOnOpen ? (s.DefaultValue >= 0.5) : s.BoolValue;
                    return $"[{s.Category}] {s.Name} = {(bVal ? "True" : "False")}{typeStr}{rStr}";
                }
                else if (s.DataType == PillDataType.ValueList)
                {
                    string strVal = s.ResetOnOpen ? (s.DefaultStringValue ?? s.StringValue) : s.StringValue;
                    string optRange = s.StringOptions.Count > 0 ? string.Join(" .. ", s.StringOptions) : "Opcao_A .. Opcao_B";
                    return $"[{s.Category}] {s.Name} = {strVal} ({optRange}) [list]{rStr}";
                }
                else if (s.DataType == PillDataType.Button)
                {
                    return $"[{s.Category}] {s.Name} [btn]{rStr}";
                }
                else if (s.DataType == PillDataType.String)
                {
                    string strVal = s.ResetOnOpen ? (s.DefaultStringValue ?? s.StringValue) : s.StringValue;
                    if (s.StringOptions.Count > 0)
                    {
                        string optRange = string.Join(" .. ", s.StringOptions);
                        return $"[{s.Category}] {s.Name} = {strVal} ({optRange}){typeStr}{rStr}";
                    }
                    return $"[{s.Category}] {s.Name} = \"{strVal}\"{typeStr}{rStr}";
                }
                else if (s.DataType == PillDataType.Integer)
                {
                    int iVal = s.ResetOnOpen ? (int)Math.Round(s.DefaultValue) : s.IntValue;
                    return $"[{s.Category}] {s.Name} = {iVal} ({(int)s.Min} .. {(int)s.Max}){unStr}{typeStr}{rStr}";
                }
                else if (s.DataType == PillDataType.Domain)
                {
                    double d0 = s.ResetOnOpen ? s.DefaultValue : s.DomainStart;
                    double d1 = s.ResetOnOpen ? s.DefaultDomainEnd : s.DomainEnd;
                    string fmt = s.Decimals == 0 ? "F0" : (s.Decimals == 1 ? "F1" : "F2");
                    return $"[{s.Category}] {s.Name} = {d0.ToString(fmt, CultureInfo.InvariantCulture)} .. {d1.ToString(fmt, CultureInfo.InvariantCulture)} ({s.Min.ToString(CultureInfo.InvariantCulture)} .. {s.Max.ToString(CultureInfo.InvariantCulture)}){unStr}{typeStr}{rStr}";
                }
                else
                {
                    double displayVal = s.ResetOnOpen ? s.DefaultValue : s.Value;
                    return $"[{s.Category}] {s.Name} = {displayVal.ToString(CultureInfo.InvariantCulture)} ({s.Min.ToString(CultureInfo.InvariantCulture)} .. {s.Max.ToString(CultureInfo.InvariantCulture)}){unStr}{typeStr}{rStr}";
                }
            });
            txtEditor.Text = string.Join(Environment.NewLine, lines);

            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(12, 8, 12, 8)
            };

            var lblHint = new Label
            {
                Text = "Enter: nova linha  |  Ctrl+Enter: salvar e aplicar",
                Dock = DockStyle.Left,
                Width = 320,
                ForeColor = Color.FromArgb(100, 116, 139),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.2f)
            };
            pnlBottom.Controls.Add(lblHint);

            var btnOk = new Button
            {
                Text = "Aplicar e Atualizar",
                DialogResult = DialogResult.OK,
                Dock = DockStyle.Right,
                Width = 140,
                BackColor = Color.FromArgb(139, 92, 246),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            var btnCancel = new Button
            {
                Text = "Cancelar",
                DialogResult = DialogResult.Cancel,
                Dock = DockStyle.Right,
                Width = 90
            };

            pnlBottom.Controls.Add(btnCancel);
            pnlBottom.Controls.Add(btnOk);

            form.Controls.Add(txtEditor);
            form.Controls.Add(pnlHeader);
            form.Controls.Add(pnlBottom);

            // Permite Ctrl+Enter para salvar rapidamente mantendo Enter para criar novas linhas
            txtEditor.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    btnOk.PerformClick();
                }
            };

            // IMPORTANTE: NÃO vincular form.AcceptButton = btnOk para que Enter não feche o diálogo!
            form.CancelButton = btnCancel;

            if (form.ShowDialog() == DialogResult.OK)
            {
                string newTitle = txtTitle.Text.Trim();
                if (!string.IsNullOrWhiteSpace(newTitle))
                {
                    PoolTitle = newTitle;
                    NickName = newTitle;
                }

                string[] newLines = txtEditor.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var parsed = ParseSliderDefinitions(newLines);
                if (parsed.Count > 0)
                {
                    RecordUndoEvent("Editar Sliders Pill");
                    SyncParsedSliders(parsed);
                }
                m_attributes?.ExpireLayout();
                ExpireSolution(true);
            }
        }

        public void PromptValueInput(PillSliderItem slider)
        {
            if (slider == null) return;

            if (slider.DataType == PillDataType.Boolean)
            {
                double elapsed = (DateTime.Now - slider.LastToggleTime).TotalMilliseconds;
                if (elapsed < 350) return;

                slider.LastToggleTime = DateTime.Now;
                RecordUndoEvent("Alternar Booleano");
                slider.BoolValue = !slider.BoolValue;
                PublishSingleSlider(slider);
                if (HasConnectedOutputs())
                {
                    ExpireSolution(true);
                }
                else
                {
                    Grasshopper.Instances.ActiveCanvas?.Invalidate();
                }
                return;
            }

            using (var prompt = new Form())
            {
                prompt.Width = 420;
                prompt.Height = 195;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.Text = $"Ajustar Parâmetro [{slider.TypeBadge.ToUpperInvariant()}]";
                prompt.StartPosition = FormStartPosition.CenterScreen;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;

                string resetNote = slider.ResetOnOpen ? $" [↺ Reseta ao abrir]" : "";
                string rangeNote = "";
                if (slider.DataType == PillDataType.String && slider.StringOptions.Count > 0)
                {
                    rangeNote = $"\nOpções: {string.Join(", ", slider.StringOptions)}";
                }
                else if (slider.DataType == PillDataType.Domain)
                {
                    rangeNote = $"\n(Formato: Início .. Fim | Limites: {slider.Min} .. {slider.Max})";
                }
                else
                {
                    rangeNote = $"\n(Mín: {slider.Min}, Máx: {slider.Max})";
                }

                var textLabel = new Label() { Left = 20, Top = 12, Width = 365, Height = 46, Text = $"Digite o novo valor para [{slider.Category}] {slider.Name}{resetNote}{rangeNote}:" };

                string initText;
                if (slider.DataType == PillDataType.String)
                    initText = slider.StringValue;
                else if (slider.DataType == PillDataType.Integer)
                    initText = slider.IntValue.ToString();
                else if (slider.DataType == PillDataType.Domain)
                    initText = $"{slider.DomainStart.ToString(CultureInfo.InvariantCulture)} .. {slider.DomainEnd.ToString(CultureInfo.InvariantCulture)}";
                else
                    initText = slider.Value.ToString(CultureInfo.InvariantCulture);

                var textBox = new TextBox() { Left = 20, Top = 64, Width = 365, Text = initText };
                var confirmation = new Button() { Text = "OK", Left = 195, Width = 90, Top = 105, DialogResult = DialogResult.OK };
                var cancel = new Button() { Text = "Cancelar", Left = 295, Width = 90, Top = 105, DialogResult = DialogResult.Cancel };

                prompt.Controls.Add(textLabel);
                prompt.Controls.Add(textBox);
                prompt.Controls.Add(confirmation);
                prompt.Controls.Add(cancel);
                prompt.AcceptButton = confirmation;
                prompt.CancelButton = cancel;

                if (prompt.ShowDialog() == DialogResult.OK)
                {
                    string input = textBox.Text.Trim();
                    if (slider.DataType == PillDataType.String)
                    {
                        RecordUndoEvent("Ajustar Texto");
                        slider.StringValue = input;
                        if (slider.StringOptions.Count > 0)
                        {
                            int idx = slider.StringOptions.FindIndex(o => o.Equals(input, StringComparison.OrdinalIgnoreCase));
                            if (idx >= 0) slider.Value = idx;
                        }
                        PublishSingleSlider(slider);
                        if (HasConnectedOutputs()) ExpireSolution(true); else Grasshopper.Instances.ActiveCanvas?.Invalidate();
                    }
                    else if (slider.DataType == PillDataType.Domain)
                    {
                        var domMatch = Regex.Match(input, @"(?<v0>[0-9\.,\-]+)\s*(?:\.\.|\:|\,|\s+a\s+|\s+to\s+)\s*(?<v1>[0-9\.,\-]+)", RegexOptions.IgnoreCase);
                        if (domMatch.Success)
                        {
                            double.TryParse(domMatch.Groups["v0"].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double d0);
                            double.TryParse(domMatch.Groups["v1"].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double d1);
                            if (d0 > d1) { double tmp = d0; d0 = d1; d1 = tmp; }
                            d0 = Math.Max(slider.Min, Math.Min(slider.Max, Math.Round(d0, slider.Decimals)));
                            d1 = Math.Max(slider.Min, Math.Min(slider.Max, Math.Round(d1, slider.Decimals)));
                            RecordUndoEvent("Ajustar Domínio");
                            slider.DomainStart = d0;
                            slider.DomainEnd = d1;
                            PublishSingleSlider(slider);
                            if (HasConnectedOutputs()) ExpireSolution(true); else Grasshopper.Instances.ActiveCanvas?.Invalidate();
                        }
                    }
                    else if (slider.DataType == PillDataType.Integer && int.TryParse(input, out int iVal))
                    {
                        RecordUndoEvent("Ajustar Inteiro");
                        slider.Value = Math.Max(slider.Min, Math.Min(slider.Max, iVal));
                        PublishSingleSlider(slider);
                        if (HasConnectedOutputs()) ExpireSolution(true); else Grasshopper.Instances.ActiveCanvas?.Invalidate();
                    }
                    else if (double.TryParse(input.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                    {
                        RecordUndoEvent("Ajustar Valor Float");
                        slider.Value = Math.Max(slider.Min, Math.Min(slider.Max, Math.Round(val, slider.Decimals)));
                        PublishSingleSlider(slider);
                        if (HasConnectedOutputs()) ExpireSolution(true); else Grasshopper.Instances.ActiveCanvas?.Invalidate();
                    }
                }
            }
        }

        public void PromptRenamePool()
        {
            using (var prompt = new Form())
            {
                prompt.Width = 380;
                prompt.Height = 160;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.Text = "Renomear Pilha de Sliders";
                prompt.StartPosition = FormStartPosition.CenterScreen;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;

                var textLabel = new Label() { Left = 20, Top = 15, Width = 325, Height = 20, Text = "Digite o novo nome para esta pilha:" };
                var textBox = new TextBox() { Left = 20, Top = 40, Width = 325, Text = DisplayTitle };
                var btnOk = new Button() { Text = "OK", Left = 150, Width = 85, Top = 75, DialogResult = DialogResult.OK };
                var btnCancel = new Button() { Text = "Cancelar", Left = 245, Width = 85, Top = 75, DialogResult = DialogResult.Cancel };

                prompt.Controls.Add(textLabel);
                prompt.Controls.Add(textBox);
                prompt.Controls.Add(btnOk);
                prompt.Controls.Add(btnCancel);
                prompt.AcceptButton = btnOk;
                prompt.CancelButton = btnCancel;

                if (prompt.ShowDialog() == DialogResult.OK)
                {
                    string newName = textBox.Text.Trim();
                    if (!string.IsNullOrWhiteSpace(newName))
                    {
                        RecordUndoEvent("Renomear Pilha");
                        NickName = newName;
                        PoolTitle = newName;
                        m_attributes?.ExpireLayout();
                        ExpireSolution(true);
                    }
                }
            }
        }

        public void PromptNewValueList()
        {
            using (var form = new Form())
            {
                form.Text = "Adicionar Nova Value List / Lista de OpÃ§Ãµes";
                form.Size = new Size(400, 320);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.Font = new Font("Segoe UI", 9f);

                var lblName = new Label { Text = "Nome do ParÃ¢metro:", Location = new Point(16, 12), Size = new Size(350, 18) };
                var txtName = new TextBox { Text = $"Lista_{Sliders.Count + 1}", Location = new Point(16, 32), Size = new Size(350, 24) };

                var lblOpts = new Label { Text = "OpÃ§Ãµes da Lista (uma por linha):", Location = new Point(16, 64), Size = new Size(350, 18) };
                var txtOpts = new TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    Location = new Point(16, 84),
                    Size = new Size(350, 140),
                    Text = "Opcao_A" + Environment.NewLine + "Opcao_B" + Environment.NewLine + "Opcao_C"
                };

                var btnOk = new Button { Text = "Criar Lista", Location = new Point(186, 238), Size = new Size(95, 28), DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Cancelar", Location = new Point(286, 238), Size = new Size(80, 28), DialogResult = DialogResult.Cancel };

                form.Controls.Add(lblName);
                form.Controls.Add(txtName);
                form.Controls.Add(lblOpts);
                form.Controls.Add(txtOpts);
                form.Controls.Add(btnOk);
                form.Controls.Add(btnCancel);
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    string pName = txtName.Text.Trim();
                    if (string.IsNullOrWhiteSpace(pName)) pName = $"Lista_{Sliders.Count + 1}";

                    var opts = txtOpts.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList();

                    if (opts.Count == 0) opts.Add("Padrao");

                    RecordUndoEvent("Adicionar Value List");
                    Sliders.Add(new PillSliderItem
                    {
                        Name = pName,
                        Category = PoolGroup,
                        CategoryColor = PillHub.GetCategoryColor(PoolGroup),
                        DataType = PillDataType.ValueList,
                        StringOptions = opts,
                        StringValue = opts[0],
                        DefaultStringValue = opts[0],
                        Value = 0,
                        DefaultValue = 0,
                        Min = 0.0,
                        Max = Math.Max(1.0, opts.Count - 1),
                        Decimals = 0
                    });
                    m_attributes?.ExpireLayout();
                    ExpireSolution(true);
                }
            }
        }

        public void PromptEditValueListOptions(PillSliderItem slider)
        {
            if (slider == null) return;
            using (var form = new Form())
            {
                form.Text = $"OpÃ§Ãµes da Lista - [{slider.Category}] {slider.Name}";
                form.Size = new Size(380, 310);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.Font = new Font("Segoe UI", 9f);

                var lbl = new Label { Text = "Digite uma opÃ§Ã£o por linha para a lista:", Location = new Point(16, 12), Size = new Size(330, 18) };
                var txt = new TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    Location = new Point(16, 34),
                    Size = new Size(330, 180),
                    Text = string.Join(Environment.NewLine, slider.StringOptions)
                };

                var btnOk = new Button { Text = "Salvar OpÃ§Ãµes", Location = new Point(166, 226), Size = new Size(100, 28), DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Cancelar", Location = new Point(272, 226), Size = new Size(74, 28), DialogResult = DialogResult.Cancel };

                form.Controls.Add(lbl);
                form.Controls.Add(txt);
                form.Controls.Add(btnOk);
                form.Controls.Add(btnCancel);
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    var newOpts = txt.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList();

                    if (newOpts.Count > 0)
                    {
                        RecordUndoEvent("Editar OpÃ§Ãµes da Lista");
                        slider.StringOptions = newOpts;
                        if (!slider.StringOptions.Contains(slider.StringValue))
                        {
                            slider.StringValue = slider.StringOptions[0];
                            slider.Value = 0;
                        }
                        slider.Max = Math.Max(1.0, slider.StringOptions.Count - 1);
                        PublishSingleSlider(slider);
                        m_attributes?.ExpireLayout();
                        ExpireSolution(true);
                    }
                }
            }
        }

        public bool MatchesSliderKey(string testKey)
        {
            if (string.IsNullOrWhiteSpace(testKey)) return false;
            string clean = PillHub.CleanUpKey(testKey);
            foreach (var s in Sliders)
            {
                if (string.Equals(s.CleanKey, clean, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(PillHub.CleanUpKey(s.FullKey), clean, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(PillHub.CleanUpKey($"{s.Category}_{s.Name}"), clean, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(PillHub.CleanUpKey(s.Name), clean, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.FullKey, testKey, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.Name, testKey, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals($"{s.Category}_{s.Name}", testKey, StringComparison.OrdinalIgnoreCase) ||
                    clean.EndsWith("_" + PillHub.CleanUpKey(s.Name), StringComparison.OrdinalIgnoreCase) ||
                    clean.EndsWith("::" + PillHub.CleanUpKey(s.Name), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public int ConnectMyReceiversWithHiddenWires(GH_Document doc = null)
        {
            if (doc == null) doc = OnPingDocument();
            if (doc == null || Sliders.Count == 0) return 0;

            int connectedCount = 0;

            foreach (var obj in doc.Objects)
            {
                if (obj is PillReceiver_Component rx && MatchesSliderKey(rx.CurrentCleanKey))
                {
                    if (rx.AutoConnectHiddenWire(doc, rx.CurrentCleanKey)) connectedCount++;
                }
                else if (obj is PillHook_Component hook && MatchesSliderKey(hook.CurrentCleanKey))
                {
                    if (hook.AutoConnectHiddenWire(doc, hook.CurrentCleanKey)) connectedCount++;
                }
            }
            return connectedCount;
        }

        public int DisconnectMyReceivers(GH_Document doc = null)
        {
            if (doc == null) doc = OnPingDocument();
            if (doc == null || Sliders.Count == 0) return 0;

            int disconnectedCount = 0;

            foreach (var obj in doc.Objects)
            {
                if (obj is PillReceiver_Component rx && MatchesSliderKey(rx.CurrentCleanKey))
                {
                    rx.DisconnectHiddenWire();
                    disconnectedCount++;
                }
                else if (obj is PillHook_Component hook && MatchesSliderKey(hook.CurrentCleanKey))
                {
                    hook.DisconnectHiddenWire();
                    disconnectedCount++;
                }
            }
            return disconnectedCount;
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Menu_AppendSeparator(menu);

            // ==============================================================
            // CABO FÍSICO OCULTO (MODO WALLACEI SÍNCRONO)
            // ==============================================================
            var wireMenu = new ToolStripMenuItem("⚡ Conexão Oculta (Modo Wallacei / Hidden Wire)");

            var connectMyBtn = new ToolStripMenuItem("➔ Conectar Receptores de Todos os Sliders desta Pilha com Cabos Ocultos");
            connectMyBtn.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                int c = ConnectMyReceiversWithHiddenWires(doc);
                MessageBox.Show($"Conectados {c} receptores aos sliders desta pilha com cabos físicos invisíveis (Wire Display -> Hidden).", "Pill Hidden Wire", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ExpireSolution(true);
            };
            wireMenu.DropDownItems.Add(connectMyBtn);

            var disconnectMyBtn = new ToolStripMenuItem("✕ Desconectar Receptores desta Pilha");
            disconnectMyBtn.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                int c = DisconnectMyReceivers(doc);
                MessageBox.Show($"Desconectados {c} receptores desta pilha.", "Pill Hidden Wire", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ExpireSolution(true);
            };
            wireMenu.DropDownItems.Add(disconnectMyBtn);

            wireMenu.DropDownItems.Add(new ToolStripSeparator());

            var connectAllBtn = new ToolStripMenuItem("⚡ CONECTAR TODOS os Pills do Canvas com Cabos Ocultos (Recomendado para Wallacei)");
            connectAllBtn.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                int c = PillHub.ConnectAllDocumentPillsHidden(doc);
                MessageBox.Show($"Sucesso! {c} canais foram conectados com cabos físicos invisíveis (Wire Display -> Hidden).\n\nO Wallacei agora possui sincronização sequencial perfeita garantida pelo Grasshopper!", "Pill Hidden Wire", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ExpireSolution(true);
            };
            wireMenu.DropDownItems.Add(connectAllBtn);

            var disconnectAllBtn = new ToolStripMenuItem("🔌 DESCONECTAR TODOS os Cabos Ocultos do Canvas");
            disconnectAllBtn.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                int c = PillHub.DisconnectAllDocumentPillsHidden(doc);
                MessageBox.Show($"Todos os {c} cabos ocultos foram desconectados. O Canvas voltou ao modo sem fio tradicional.", "Pill Hidden Wire", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ExpireSolution(true);
            };
            wireMenu.DropDownItems.Add(disconnectAllBtn);

            menu.Items.Add(wireMenu);
            menu.Items.Add(new ToolStripSeparator());

            var renameItem = new ToolStripMenuItem("🏷️ Renomear Pilha...") { Font = new Font(menu.Font, FontStyle.Bold) };
            renameItem.Click += (sender, ev) => PromptRenamePool();
            menu.Items.Add(renameItem);

            var editItem = new ToolStripMenuItem("✏️ Editar Sliders & Parâmetros...");
            editItem.Click += (sender, ev) => PromptSliderEditor();
            menu.Items.Add(editItem);

            var addSliderItem = new ToolStripMenuItem("➕ Adicionar Novo Slider Numérico");
            addSliderItem.Click += (sender, ev) =>
            {
                RecordUndoEvent("Adicionar Slider");
                Sliders.Add(new PillSliderItem
                {
                    Name = $"Param_{Sliders.Count + 1}",
                    Category = PoolGroup,
                    CategoryColor = PillHub.GetCategoryColor(PoolGroup),
                    DataType = PillDataType.Float,
                    Min = 0.0,
                    Max = 100.0,
                    Value = 50.0,
                    DefaultValue = 50.0,
                    Decimals = 1
                });
                ExpireSolution(true);
            };
            menu.Items.Add(addSliderItem);

            var addBoolItem = new ToolStripMenuItem("🔘 Adicionar Toggle Booleano");
            addBoolItem.Click += (sender, ev) =>
            {
                RecordUndoEvent("Adicionar Toggle");
                Sliders.Add(new PillSliderItem
                {
                    Name = $"Toggle_{Sliders.Count + 1}",
                    Category = PoolGroup,
                    CategoryColor = PillHub.GetCategoryColor(PoolGroup),
                    DataType = PillDataType.Boolean,
                    Value = 0.0,
                    DefaultValue = 0.0,
                    Min = 0.0,
                    Max = 1.0,
                    Decimals = 0
                });
                ExpireSolution(true);
            };
            menu.Items.Add(addBoolItem);

            var addBtnItem = new ToolStripMenuItem("⚡ Adicionar Botão Pulso (Trigger)");
            addBtnItem.Click += (sender, ev) =>
            {
                RecordUndoEvent("Adicionar Botão Pulso");
                Sliders.Add(new PillSliderItem
                {
                    Name = $"Disparo_{Sliders.Count + 1}",
                    Category = PoolGroup,
                    CategoryColor = PillHub.GetCategoryColor(PoolGroup),
                    DataType = PillDataType.Button,
                    Value = 0.0,
                    DefaultValue = 0.0,
                    Min = 0.0,
                    Max = 1.0,
                    Decimals = 0,
                    ResetOnOpen = true
                });
                ExpireSolution(true);
            };
            menu.Items.Add(addBtnItem);

            var addDomItem = new ToolStripMenuItem("📐 Adicionar Domínio / Intervalo");
            addDomItem.Click += (sender, ev) =>
            {
                RecordUndoEvent("Adicionar Domínio");
                Sliders.Add(new PillSliderItem
                {
                    Name = $"Dominio_{Sliders.Count + 1}",
                    Category = PoolGroup,
                    CategoryColor = PillHub.GetCategoryColor(PoolGroup),
                    DataType = PillDataType.Domain,
                    Min = 0.0,
                    Max = 100.0,
                    Value = 20.0,
                    DefaultValue = 20.0,
                    DomainEnd = 80.0,
                    DefaultDomainEnd = 80.0,
                    Decimals = 1
                });
                ExpireSolution(true);
            };
            menu.Items.Add(addDomItem);

            var resetAllItem = new ToolStripMenuItem("🔄 Resetar Todos para Padrão");
            resetAllItem.Click += (sender, ev) =>
            {
                RecordUndoEvent("Resetar Sliders");
                foreach (var s in Sliders)
                {
                    s.Value = s.ResetOnOpen ? s.DefaultValue : (s.Min + s.Max) * 0.5;
                    if (s.DataType == PillDataType.Domain)
                    {
                        s.DomainEnd = s.ResetOnOpen ? s.DefaultDomainEnd : s.Max;
                    }
                    PublishSingleSlider(s);
                }
                ExpireSolution(true);
            };
            menu.Items.Add(resetAllItem);

            var resetAutoItem = new ToolStripMenuItem("↺ Resetar Sliders [r] para Iniciais");
            resetAutoItem.Click += (sender, ev) =>
            {
                RecordUndoEvent("Resetar Sliders [r]");
                foreach (var s in Sliders.Where(x => x.ResetOnOpen))
                {
                    s.Value = s.DefaultValue;
                    s.DomainEnd = s.DefaultDomainEnd;
                    s.StringValue = s.DefaultStringValue;
                    PublishSingleSlider(s);
                }
                ExpireSolution(true);
            };
            menu.Items.Add(resetAutoItem);

            var setDefItem = new ToolStripMenuItem("📌 Fixar Valores Atuais como Iniciais de Reset");
            setDefItem.Click += (sender, ev) =>
            {
                RecordUndoEvent("Fixar Iniciais de Reset");
                foreach (var s in Sliders)
                {
                    s.DefaultValue = s.Value;
                    s.DefaultDomainEnd = s.DomainEnd;
                    s.DefaultStringValue = s.StringValue;
                }
                ExpireSolution(true);
            };
            menu.Items.Add(setDefItem);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetInt32("SliderCount", Sliders.Count);
            writer.SetString("PoolGroup", PoolGroup);
            writer.SetString("PoolTitle", DisplayTitle);

            for (int i = 0; i < Sliders.Count; i++)
            {
                var s = Sliders[i];
                writer.SetString($"S_Name_{i}", s.Name);
                writer.SetString($"S_Cat_{i}", s.Category);
                writer.SetString($"S_Type_{i}", s.DataType.ToString());
                writer.SetDouble($"S_Val_{i}", s.Value);
                writer.SetDouble($"S_DefVal_{i}", s.DefaultValue);
                writer.SetDouble($"S_DomEnd_{i}", s.DomainEnd);
                writer.SetDouble($"S_DefDomEnd_{i}", s.DefaultDomainEnd);
                writer.SetDouble($"S_Min_{i}", s.Min);
                writer.SetDouble($"S_Max_{i}", s.Max);
                writer.SetInt32($"S_Dec_{i}", s.Decimals);
                writer.SetString($"S_Unit_{i}", s.Unit ?? "");
                writer.SetBoolean($"S_ResetOnOpen_{i}", s.ResetOnOpen);
                writer.SetString($"S_StrVal_{i}", s.StringValue ?? "");
                writer.SetString($"S_DefStrVal_{i}", s.DefaultStringValue ?? "");
                writer.SetString($"S_Options_{i}", string.Join(";", s.StringOptions));
            }
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("PoolGroup")) PoolGroup = reader.GetString("PoolGroup");
            if (reader.ItemExists("PoolTitle"))
            {
                PoolTitle = reader.GetString("PoolTitle");
                if (!string.IsNullOrWhiteSpace(PoolTitle) && !PoolTitle.Equals("SliderPool", StringComparison.OrdinalIgnoreCase))
                {
                    NickName = PoolTitle;
                }
            }

            if (reader.ItemExists("SliderCount"))
            {
                int count = reader.GetInt32("SliderCount");
                Sliders.Clear();
                for (int i = 0; i < count; i++)
                {
                    string name = reader.ItemExists($"S_Name_{i}") ? reader.GetString($"S_Name_{i}") : $"P_{i}";
                    string cat = reader.ItemExists($"S_Cat_{i}") ? reader.GetString($"S_Cat_{i}") : "VAR";
                    string typeStr = reader.ItemExists($"S_Type_{i}") ? reader.GetString($"S_Type_{i}") : "Float";
                    PillDataType dt = PillDataType.Float;
                    if (Enum.TryParse(typeStr, true, out PillDataType parsedType)) dt = parsedType;

                    double val = reader.ItemExists($"S_Val_{i}") ? reader.GetDouble($"S_Val_{i}") : 50.0;
                    double min = reader.ItemExists($"S_Min_{i}") ? reader.GetDouble($"S_Min_{i}") : 0.0;
                    double max = reader.ItemExists($"S_Max_{i}") ? reader.GetDouble($"S_Max_{i}") : 100.0;
                    double domEnd = reader.ItemExists($"S_DomEnd_{i}") ? reader.GetDouble($"S_DomEnd_{i}") : 80.0;
                    int dec = reader.ItemExists($"S_Dec_{i}") ? reader.GetInt32($"S_Dec_{i}") : (dt == PillDataType.Integer ? 0 : 2);
                    string unit = reader.ItemExists($"S_Unit_{i}") ? reader.GetString($"S_Unit_{i}") : "";
                    bool resetOnOpen = reader.ItemExists($"S_ResetOnOpen_{i}") && reader.GetBoolean($"S_ResetOnOpen_{i}");
                    double defVal = reader.ItemExists($"S_DefVal_{i}") ? reader.GetDouble($"S_DefVal_{i}") : (resetOnOpen ? min : val);
                    double defDomEnd = reader.ItemExists($"S_DefDomEnd_{i}") ? reader.GetDouble($"S_DefDomEnd_{i}") : (resetOnOpen ? max : domEnd);

                    string strVal = reader.ItemExists($"S_StrVal_{i}") ? reader.GetString($"S_StrVal_{i}") : "";
                    string defStrVal = reader.ItemExists($"S_DefStrVal_{i}") ? reader.GetString($"S_DefStrVal_{i}") : strVal;
                    string optStr = reader.ItemExists($"S_Options_{i}") ? reader.GetString($"S_Options_{i}") : "";
                    var options = !string.IsNullOrEmpty(optStr) ? optStr.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>();

                    double initialVal = resetOnOpen ? defVal : val;
                    double initialDomEnd = resetOnOpen ? defDomEnd : domEnd;
                    string initialStr = resetOnOpen ? defStrVal : strVal;

                    Sliders.Add(new PillSliderItem
                    {
                        Name = name,
                        Category = cat,
                        CategoryColor = PillHub.GetCategoryColor(cat),
                        DataType = dt,
                        Value = initialVal,
                        DefaultValue = defVal,
                        DomainEnd = initialDomEnd,
                        DefaultDomainEnd = defDomEnd,
                        Min = min,
                        Max = max,
                        Decimals = dec,
                        Unit = unit,
                        ResetOnOpen = resetOnOpen,
                        StringValue = initialStr,
                        DefaultStringValue = defStrVal,
                        StringOptions = options
                    });
                }
            }
            _hasResetOnOpen = false;
            return base.Read(reader);
        }
    }

    // =========================================================================
    // ATTRIBUTES: RENDER DA PISCINA DE SLIDERS COM INTERATIVIDADE NO CANVAS
    // =========================================================================
    public class PillSliderPool_Attributes : GH_ComponentAttributes
    {
        private readonly PillSliderPool_Component _pool;

        private const float HEADER_HEIGHT = 26f;
        private const float ROW_HEIGHT = 24f;
        private const float DEFAULT_WIDTH = 295f;

        private int _activeDragIndex = -1;
        private int _activeDomainThumb = -1;
        private DateTime _lastDragPublishTime = DateTime.MinValue;

        public PillSliderPool_Attributes(PillSliderPool_Component owner) : base(owner)
        {
            _pool = owner;
        }

        private static SizeF MeasureTextSafe(string text, Font font)
        {
            if (string.IsNullOrEmpty(text)) return SizeF.Empty;
            try
            {
                return GH_FontServer.MeasureString(text, font);
            }
            catch
            {
                var sz = TextRenderer.MeasureText(text, font);
                return new SizeF(sz.Width, sz.Height);
            }
        }

        protected override void Layout()
        {
            int rowCount = Math.Max(1, _pool.Sliders.Count);
            float totalHeight = HEADER_HEIGHT + (rowCount * ROW_HEIGHT) + 6f;

            // 1. Determina larguras dinâmicas para que os textos nunca sejam cortados
            float maxBadgeW = 28f;
            float maxNameW = 60f;
            float maxValW = 48f;

            using (var fBadge = new Font("Segoe UI", 7.0f, FontStyle.Bold))
            using (var fName = new Font("Segoe UI", 7.8f, FontStyle.Bold))
            using (var fTitle = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var fVal = new Font("Segoe UI", 7.0f, FontStyle.Bold))
            {
                foreach (var s in _pool.Sliders)
                {
                    if (!string.IsNullOrEmpty(s.Category))
                    {
                        float bw = MeasureTextSafe(s.Category, fBadge).Width + 8f;
                        if (bw > maxBadgeW) maxBadgeW = bw;
                    }

                    string dispName = s.ResetOnOpen ? s.Name + " ↺" : s.Name;
                    float nw = MeasureTextSafe(dispName, fName).Width + 10f;
                    if (nw > maxNameW) maxNameW = nw;

                    string sampleVal;
                    if (s.DataType == PillDataType.Domain)
                    {
                        string fmt = s.Decimals == 0 ? "F0" : (s.Decimals == 1 ? "F1" : "F2");
                        sampleVal = $"{s.DomainStart.ToString(fmt, CultureInfo.InvariantCulture)}..{s.DomainEnd.ToString(fmt, CultureInfo.InvariantCulture)}{s.Unit}";
                    }
                    else if (s.DataType == PillDataType.Boolean)
                    {
                        sampleVal = "FALSE";
                    }
                    else if (s.DataType == PillDataType.String)
                    {
                        sampleVal = s.StringValue ?? "";
                        if (sampleVal.Length > 7) sampleVal = sampleVal.Substring(0, 6) + "…";
                    }
                    else
                    {
                        string fmt = s.Decimals == 0 ? "F0" : (s.Decimals == 1 ? "F1" : "F2");
                        sampleVal = $"{s.Value.ToString(fmt, CultureInfo.InvariantCulture)}{s.Unit}";
                    }
                    float vw = MeasureTextSafe(sampleVal, fVal).Width + 12f;
                    if (vw > maxValW) maxValW = vw;
                }

                float titleW = MeasureTextSafe(_pool.DisplayTitle, fTitle).Width + 85f;

                float typeW = 24f;
                float valBoxW = Math.Max(48f, maxValW);
                float minTrackW = 65f;
                float rowRequiredW = 4f + maxBadgeW + 3f + typeW + 3f + maxNameW + 4f + minTrackW + 4f + valBoxW + 4f;

                float totalWidth = Math.Max(DEFAULT_WIDTH, Math.Max(rowRequiredW, titleW));

                Bounds = new RectangleF(Pivot.X - totalWidth * 0.5f, Pivot.Y - totalHeight * 0.5f, totalWidth, totalHeight);

                // Posiciona os pinos de saída no lado direito
                if (Owner.Params.Output.Count > 0)
                {
                    float stepY = totalHeight / (Owner.Params.Output.Count + 1);
                    for (int i = 0; i < Owner.Params.Output.Count; i++)
                    {
                        var pOut = Owner.Params.Output[i];
                        pOut.Attributes.Bounds = new RectangleF(Bounds.Right - 10f, Bounds.Y + (i + 1) * stepY - 6f, 10f, 12f);
                        pOut.Attributes.Pivot = new PointF(Bounds.Right, Bounds.Y + (i + 1) * stepY);
                    }
                }

                // Posiciona o pino de entrada Def no lado esquerdo
                if (Owner.Params.Input.Count > 0)
                {
                    var pIn = Owner.Params.Input[0];
                    pIn.Attributes.Bounds = new RectangleF(Bounds.Left, Bounds.Y + HEADER_HEIGHT * 0.5f - 6f, 10f, 12f);
                    pIn.Attributes.Pivot = new PointF(Bounds.Left, Bounds.Y + HEADER_HEIGHT * 0.5f);
                }

                // Calcula geometrias internas de cada slider
                float curY = Bounds.Y + HEADER_HEIGHT + 3f;
                for (int i = 0; i < _pool.Sliders.Count; i++)
                {
                    var s = _pool.Sliders[i];
                    s.BoundsRow = new RectangleF(Bounds.X + 4f, curY, Bounds.Width - 8f, ROW_HEIGHT - 2f);

                    // Badge [GEO]
                    s.BoundsBadge = new RectangleF(s.BoundsRow.X + 2f, s.BoundsRow.Y + 2f, maxBadgeW, s.BoundsRow.Height - 4f);

                    // Type Badge [flt], [int], [bool], [str], [dom]
                    s.BoundsType = new RectangleF(s.BoundsBadge.Right + 3f, s.BoundsRow.Y + 2.5f, typeW, s.BoundsRow.Height - 5f);

                    // Nome do Parâmetro
                    s.BoundsName = new RectangleF(s.BoundsType.Right + 3f, s.BoundsRow.Y, maxNameW, s.BoundsRow.Height);

                    // Caixa de Valor no final
                    s.BoundsValueBox = new RectangleF(s.BoundsRow.Right - valBoxW - 2f, s.BoundsRow.Y + 2f, valBoxW, s.BoundsRow.Height - 4f);

                    // Trilho do Slider no meio
                    float trackLeft = s.BoundsName.Right + 4f;
                    float trackRight = s.BoundsValueBox.Left - 4f;
                    float trackW = Math.Max(30f, trackRight - trackLeft);
                    s.BoundsTrack = new RectangleF(trackLeft, s.BoundsRow.Y + 4f, trackW, s.BoundsRow.Height - 8f);

                    curY += ROW_HEIGHT;
                }
            }
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left)
            {
                PointF pt = e.CanvasLocation;

                for (int i = 0; i < _pool.Sliders.Count; i++)
                {
                    var s = _pool.Sliders[i];

                    // 1. Booleano: clique no trilho/switch ou na caixa de valor alterna o booleano com debounce/cooldown
                    if (s.DataType == PillDataType.Boolean)
                    {
                        bool inSwitchOrVal = s.BoundsTrack.Contains(pt) || s.BoundsValueBox.Contains(pt) ||
                            (pt.Y >= s.BoundsRow.Y && pt.Y <= s.BoundsRow.Bottom && pt.X >= s.BoundsTrack.Left && pt.X <= s.BoundsValueBox.Right);

                        if (inSwitchOrVal)
                        {
                            double elapsed = (DateTime.Now - s.LastToggleTime).TotalMilliseconds;
                            if (elapsed >= 350)
                            {
                                s.LastToggleTime = DateTime.Now;
                                _pool.RecordUndoEvent("Alternar Booleano");
                                s.BoolValue = !s.BoolValue;
                                _pool.PublishSingleSlider(s);
                                if (_pool.HasConnectedOutputs())
                                {
                                    _pool.ExpireSolution(true);
                                }
                                else
                                {
                                    sender.Invalidate();
                                }
                            }
                            return GH_ObjectResponse.Handled;
                        }
                    }

                    // 1b. Botão de Disparo / Pulso Momentâneo (Trigger)
                    if (s.DataType == PillDataType.Button)
                    {
                        bool inBtn = s.BoundsTrack.Contains(pt) || s.BoundsValueBox.Contains(pt) ||
                            (pt.Y >= s.BoundsRow.Y && pt.Y <= s.BoundsRow.Bottom && pt.X >= s.BoundsTrack.Left && pt.X <= s.BoundsValueBox.Right);

                        if (inBtn)
                        {
                            s.LastToggleTime = DateTime.Now;
                            _pool.RecordUndoEvent("Disparar Pulso");
                            s.BoolValue = true;
                            _pool.PublishSingleSlider(s);
                            if (_pool.HasConnectedOutputs())
                            {
                                _pool.ExpireSolution(true);
                            }
                            else
                            {
                                sender.Invalidate();
                            }

                            // Agenda reset automático para False
                            var doc = _pool.OnPingDocument();
                            if (doc != null)
                            {
                                doc.ScheduleSolution(80, d =>
                                {
                                    s.BoolValue = false;
                                    _pool.PublishSingleSlider(s);
                                    if (_pool.HasConnectedOutputs())
                                    {
                                        _pool.ExpireSolution(false);
                                    }
                                    else
                                    {
                                        Grasshopper.Instances.ActiveCanvas?.Invalidate();
                                    }
                                });
                            }
                            return GH_ObjectResponse.Handled;
                        }
                    }

                    // 1c. Value List: clique no trilho ou valor abre o dropdown suspenso
                    if (s.DataType == PillDataType.ValueList)
                    {
                        bool inTrackOrVal = s.BoundsTrack.Contains(pt) || s.BoundsValueBox.Contains(pt) ||
                            (pt.Y >= s.BoundsRow.Y && pt.Y <= s.BoundsRow.Bottom && pt.X >= s.BoundsTrack.Left && pt.X <= s.BoundsValueBox.Right);
                        if (inTrackOrVal)
                        {
                            ShowValueListDropdown(sender, s);
                            return GH_ObjectResponse.Handled;
                        }
                    }

                    // 2. String sem opções: clique no trilho ou valor abre prompt de texto
                    if (s.DataType == PillDataType.String && s.StringOptions.Count == 0)
                    {
                        if (s.BoundsValueBox.Contains(pt) || s.BoundsTrack.Contains(pt))
                        {
                            _pool.PromptValueInput(s);
                            return GH_ObjectResponse.Handled;
                        }
                    }

                    // 3. Clique na caixa de valor -> digitação numérica / edição direta
                    if (s.BoundsValueBox.Contains(pt))
                    {
                        _pool.PromptValueInput(s);
                        return GH_ObjectResponse.Handled;
                    }

                    // 4. Domínio (Domain / Interval): clique no trilho do slider -> selecionar manípulo mais próximo e arrastar
                    if (s.DataType == PillDataType.Domain && (s.BoundsTrack.Contains(pt) || (pt.Y >= s.BoundsRow.Y && pt.Y <= s.BoundsRow.Bottom && pt.X >= s.BoundsTrack.Left && pt.X <= s.BoundsTrack.Right)))
                    {
                        _activeDragIndex = i;
                        float tLeft = s.BoundsTrack.Left;
                        float tWidth = s.BoundsTrack.Width;
                        double r0 = (s.Max > s.Min) ? Math.Max(0.0, Math.Min(1.0, (s.DomainStart - s.Min) / (s.Max - s.Min))) : 0.0;
                        double r1 = (s.Max > s.Min) ? Math.Max(0.0, Math.Min(1.0, (s.DomainEnd - s.Min) / (s.Max - s.Min))) : 1.0;
                        float k0X = tLeft + (float)(r0 * tWidth);
                        float k1X = tLeft + (float)(r1 * tWidth);

                        float d0 = Math.Abs(pt.X - k0X);
                        float d1 = Math.Abs(pt.X - k1X);
                        if (d0 < d1) _activeDomainThumb = 0;
                        else if (d1 < d0) _activeDomainThumb = 1;
                        else _activeDomainThumb = (pt.X <= k0X) ? 0 : 1;

                        UpdateDomainFromMouse(s, pt.X, _activeDomainThumb);
                        return GH_ObjectResponse.Capture;
                    }

                    // 5. Clique no trilho do slider -> iniciar arrasto (Float, Integer, String com Opções)
                    if (s.DataType != PillDataType.Boolean && s.DataType != PillDataType.Button && s.DataType != PillDataType.Domain && (s.BoundsTrack.Contains(pt) || (pt.Y >= s.BoundsRow.Y && pt.Y <= s.BoundsRow.Bottom && pt.X >= s.BoundsTrack.Left && pt.X <= s.BoundsTrack.Right)))
                    {
                        _activeDragIndex = i;
                        UpdateSliderFromMouse(s, pt.X);
                        return GH_ObjectResponse.Capture;
                    }
                }
            }
            return base.RespondToMouseDown(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (_activeDragIndex >= 0 && _activeDragIndex < _pool.Sliders.Count)
            {
                var s = _pool.Sliders[_activeDragIndex];
                if (s.DataType == PillDataType.Domain)
                {
                    UpdateDomainFromMouse(s, e.CanvasLocation.X, _activeDomainThumb);
                }
                else
                {
                    UpdateSliderFromMouse(s, e.CanvasLocation.X);
                }
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseMove(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (_activeDragIndex >= 0)
            {
                if (_activeDragIndex < _pool.Sliders.Count)
                {
                    var s = _pool.Sliders[_activeDragIndex];
                    _lastDragPublishTime = DateTime.Now;
                    _pool.PublishSingleSlider(s);
                    if (_pool.HasConnectedOutputs())
                    {
                        _pool.ExpireSolution(true);
                    }
                    else
                    {
                        sender.Invalidate();
                    }
                }
                _activeDragIndex = -1;
                _activeDomainThumb = -1;
                return GH_ObjectResponse.Release;
            }
            return base.RespondToMouseUp(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left)
            {
                PointF pt = e.CanvasLocation;

                // Duplo-clique no cabeçalho abre o editor completo
                if (pt.Y <= Bounds.Y + HEADER_HEIGHT)
                {
                    _pool.PromptSliderEditor();
                    return GH_ObjectResponse.Handled;
                }

                // Duplo-clique em qualquer linha de slider abre a edição daquele valor
                for (int i = 0; i < _pool.Sliders.Count; i++)
                {
                    var s = _pool.Sliders[i];
                    if (s.BoundsRow.Contains(pt))
                    {
                        // Ignora duplo clique em booleano ou botão para evitar reversão imediata indesejada
                        if (s.DataType == PillDataType.Boolean || s.DataType == PillDataType.Button)
                        {
                            return GH_ObjectResponse.Handled;
                        }
                        _pool.PromptValueInput(s);
                        return GH_ObjectResponse.Handled;
                    }
                }
            }
            return base.RespondToMouseDoubleClick(sender, e);
        }

        private void ShowValueListDropdown(GH_Canvas sender, PillSliderItem slider)
        {
            if (slider == null) return;
            if (slider.StringOptions == null || slider.StringOptions.Count == 0)
            {
                _pool.PromptEditValueListOptions(slider);
                return;
            }

            var menu = new ContextMenuStrip { Font = new Font("Segoe UI", 9f) };

            for (int i = 0; i < slider.StringOptions.Count; i++)
            {
                int idx = i;
                string opt = slider.StringOptions[i];
                bool isSel = (idx == (int)slider.Value) || opt.Equals(slider.StringValue, StringComparison.OrdinalIgnoreCase);

                var item = new ToolStripMenuItem(opt)
                {
                    Checked = isSel,
                    Font = isSel ? new Font("Segoe UI", 9f, FontStyle.Bold) : new Font("Segoe UI", 9f)
                };
                item.Click += (s, e) =>
                {
                    _pool.RecordUndoEvent("Selecionar " + slider.Name);
                    slider.Value = idx;
                    slider.StringValue = opt;
                    _pool.PublishSingleSlider(slider);
                    if (_pool.HasConnectedOutputs())
                    {
                        _pool.ExpireSolution(true);
                    }
                    else
                    {
                        sender.Invalidate();
                    }
                };
                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());
            var editOpts = new ToolStripMenuItem("⚙️ Editar Opções da Lista...");
            editOpts.Click += (s, e) => _pool.PromptEditValueListOptions(slider);
            menu.Items.Add(editOpts);

            Point screenPt = sender.PointToScreen(new Point((int)slider.BoundsTrack.Left, (int)slider.BoundsTrack.Bottom + 2));
            menu.Show(screenPt);
        }

        private void UpdateDomainFromMouse(PillSliderItem slider, float mouseX, int thumb)
        {
            float tLeft = slider.BoundsTrack.Left;
            float tWidth = slider.BoundsTrack.Width;
            if (tWidth <= 0) return;

            double ratio = Math.Max(0.0, Math.Min(1.0, (mouseX - tLeft) / tWidth));
            double raw = slider.Min + ratio * (slider.Max - slider.Min);
            double newVal = Math.Round(raw, slider.Decimals);

            if (thumb == 0)
            {
                newVal = Math.Min(newVal, slider.DomainEnd);
                newVal = Math.Max(slider.Min, newVal);
                if (Math.Abs(newVal - slider.DomainStart) > 1e-9)
                {
                    slider.DomainStart = newVal;
                    Grasshopper.Instances.ActiveCanvas?.Invalidate();

                    double elapsed = (DateTime.Now - _lastDragPublishTime).TotalMilliseconds;
                    if (elapsed >= 50)
                    {
                        _lastDragPublishTime = DateTime.Now;
                        _pool.PublishSingleSlider(slider);
                        if (_pool.HasConnectedOutputs())
                        {
                            _pool.ExpireSolution(true);
                        }
                    }
                }
            }
            else
            {
                newVal = Math.Max(newVal, slider.DomainStart);
                newVal = Math.Min(slider.Max, newVal);
                if (Math.Abs(newVal - slider.DomainEnd) > 1e-9)
                {
                    slider.DomainEnd = newVal;
                    Grasshopper.Instances.ActiveCanvas?.Invalidate();

                    double elapsed = (DateTime.Now - _lastDragPublishTime).TotalMilliseconds;
                    if (elapsed >= 50)
                    {
                        _lastDragPublishTime = DateTime.Now;
                        _pool.PublishSingleSlider(slider);
                        if (_pool.HasConnectedOutputs())
                        {
                            _pool.ExpireSolution(true);
                        }
                    }
                }
            }
        }

        private void UpdateSliderFromMouse(PillSliderItem slider, float mouseX)
        {
            if (slider.DataType == PillDataType.Boolean || slider.DataType == PillDataType.Button || slider.DataType == PillDataType.Domain)
            {
                return;
            }

            float tLeft = slider.BoundsTrack.Left;
            float tWidth = slider.BoundsTrack.Width;
            if (tWidth <= 0) return;

            double ratio = Math.Max(0.0, Math.Min(1.0, (mouseX - tLeft) / tWidth));

            if (slider.DataType == PillDataType.String)
            {
                if (slider.StringOptions.Count > 0)
                {
                    int optCount = slider.StringOptions.Count;
                    int idx = (int)Math.Round(ratio * (optCount - 1));
                    idx = Math.Max(0, Math.Min(optCount - 1, idx));
                    string newStr = slider.StringOptions[idx];
                    if (newStr != slider.StringValue || (int)slider.Value != idx)
                    {
                        slider.Value = idx;
                        slider.StringValue = newStr;
                        Grasshopper.Instances.ActiveCanvas?.Invalidate();

                        double elapsed = (DateTime.Now - _lastDragPublishTime).TotalMilliseconds;
                        if (elapsed >= 50)
                        {
                            _lastDragPublishTime = DateTime.Now;
                            _pool.PublishSingleSlider(slider);
                            if (_pool.HasConnectedOutputs())
                            {
                                _pool.ExpireSolution(true);
                            }
                        }
                    }
                }
                return;
            }

            if (slider.DataType == PillDataType.Integer)
            {
                double rawVal = slider.Min + ratio * (slider.Max - slider.Min);
                int newInt = (int)Math.Round(rawVal);
                newInt = (int)Math.Max(slider.Min, Math.Min(slider.Max, newInt));
                if (Math.Abs(newInt - slider.Value) > 1e-6)
                {
                    slider.Value = newInt;
                    Grasshopper.Instances.ActiveCanvas?.Invalidate();

                    double elapsed = (DateTime.Now - _lastDragPublishTime).TotalMilliseconds;
                    if (elapsed >= 50)
                    {
                        _lastDragPublishTime = DateTime.Now;
                        _pool.PublishSingleSlider(slider);
                        if (_pool.HasConnectedOutputs())
                        {
                            _pool.ExpireSolution(true);
                        }
                    }
                }
                return;
            }

            // Float
            double rawFloat = slider.Min + ratio * (slider.Max - slider.Min);
            double newVal = Math.Round(rawFloat, slider.Decimals);
            if (Math.Abs(newVal - slider.Value) > 1e-9)
            {
                slider.Value = newVal;
                Grasshopper.Instances.ActiveCanvas?.Invalidate();

                double elapsed = (DateTime.Now - _lastDragPublishTime).TotalMilliseconds;
                if (elapsed >= 50)
                {
                    _lastDragPublishTime = DateTime.Now;
                    _pool.PublishSingleSlider(slider);
                    if (_pool.HasConnectedOutputs())
                    {
                        _pool.ExpireSolution(true);
                    }
                }
            }
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            if (channel == GH_CanvasChannel.Wires)
            {
                base.Render(canvas, graphics, channel);
                return;
            }

            if (channel != GH_CanvasChannel.Objects)
            {
                base.Render(canvas, graphics, channel);
                return;
            }

            RectangleF b = Bounds;
            float cornerRadius = 6f;

            // 1. Moldura Externa Principal
            using (var path = CreateRoundedRectangle(b, cornerRadius))
            {
                // Halo de Seleção
                if (Selected)
                {
                    using (var selPen = new Pen(Color.FromArgb(140, 139, 92, 246), 4f))
                    {
                        graphics.DrawPath(selPen, path);
                    }
                }

                // Fundo Dark / Light Card
                using (var bgBrush = new LinearGradientBrush(b, Color.FromArgb(248, 250, 252), Color.FromArgb(238, 242, 246), LinearGradientMode.Vertical))
                using (var borderPen = new Pen(Color.FromArgb(139, 92, 246), Selected ? 2.0f : 1.2f))
                {
                    graphics.FillPath(bgBrush, path);
                    graphics.DrawPath(borderPen, path);
                }
            }

            // 2. Cabeçalho do Pool (Header)
            RectangleF headerRect = new RectangleF(b.X, b.Y, b.Width, HEADER_HEIGHT);
            using (var headerBrush = new LinearGradientBrush(headerRect, Color.FromArgb(139, 92, 246), Color.FromArgb(124, 58, 237), LinearGradientMode.Vertical))
            {
                using (var headerPath = new GraphicsPath())
                {
                    headerPath.AddArc(b.X, b.Y, cornerRadius * 2, cornerRadius * 2, 180, 90);
                    headerPath.AddArc(b.Right - cornerRadius * 2, b.Y, cornerRadius * 2, cornerRadius * 2, 270, 90);
                    headerPath.AddLine(b.Right, headerRect.Bottom, b.X, headerRect.Bottom);
                    headerPath.CloseFigure();
                    graphics.FillPath(headerBrush, headerPath);
                }
            }

            // Texto do Cabeçalho
            using (var fontTitle = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var brushTitle = new SolidBrush(Color.White))
            {
                var sfTitle = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                graphics.DrawString(_pool.DisplayTitle, fontTitle, brushTitle, new RectangleF(b.X + 8f, b.Y, b.Width - 65f, HEADER_HEIGHT), sfTitle);
            }

            // Badge de Quantidade no Cabeçalho (ex: "4 Vars")
            string countText = $"{_pool.Sliders.Count} Vars";
            using (var fCount = new Font("Segoe UI", 7.0f, FontStyle.Bold))
            {
                SizeF sz = graphics.MeasureString(countText, fCount);
                float cW = Math.Max(26f, sz.Width + 8f);
                RectangleF countRect = new RectangleF(b.Right - cW - 8f, b.Y + 4.5f, cW, HEADER_HEIGHT - 9f);

                using (var cPath = CreateRoundedRectangle(countRect, 3f))
                using (var cBg = new SolidBrush(Color.FromArgb(80, 255, 255, 255)))
                using (var cTextBrush = new SolidBrush(Color.White))
                {
                    graphics.FillPath(cBg, cPath);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    graphics.DrawString(countText, fCount, cTextBrush, countRect, sf);
                }
            }

            // 3. Renderizar Linhas dos Sliders
            for (int i = 0; i < _pool.Sliders.Count; i++)
            {
                var s = _pool.Sliders[i];
                bool isDragging = (_activeDragIndex == i);

                // Fundo alternado da linha
                if (i % 2 == 1)
                {
                    using (var rowBg = new SolidBrush(Color.FromArgb(12, 0, 0, 0)))
                    {
                        graphics.FillRectangle(rowBg, s.BoundsRow);
                    }
                }

                // A1. Badge da Categoria [GEO], [ACU], etc.
                using (var bPath = CreateRoundedRectangle(s.BoundsBadge, 2.5f))
                using (var bBrush = new SolidBrush(s.CategoryColor))
                using (var tBrush = new SolidBrush(Color.White))
                using (var fBadge = new Font("Segoe UI", 7.0f, FontStyle.Bold))
                {
                    graphics.FillPath(bBrush, bPath);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    graphics.DrawString(s.Category, fBadge, tBrush, s.BoundsBadge, sf);
                }

                // A2. Badge do Tipo de Dado [flt], [int], [bool], [str], [dom]
                Color typeBg;
                Color typeFg;
                string typeText = s.TypeBadge;
                switch (s.DataType)
                {
                    case PillDataType.Integer:
                        typeBg = Color.FromArgb(254, 243, 199); // Amber-100
                        typeFg = Color.FromArgb(180, 83, 9);   // Amber-700
                        typeText = "int";
                        break;
                    case PillDataType.Boolean:
                        typeBg = Color.FromArgb(209, 250, 229); // Emerald-100
                        typeFg = Color.FromArgb(4, 120, 87);    // Emerald-700
                        typeText = "bool";
                        break;
                    case PillDataType.Button:
                        typeBg = Color.FromArgb(254, 243, 199); // Amber-100
                        typeFg = Color.FromArgb(180, 83, 9);    // Amber-700
                        typeText = "btn";
                        break;
                    case PillDataType.String:
                        typeBg = Color.FromArgb(243, 232, 255); // Purple-100
                        typeFg = Color.FromArgb(109, 40, 217);  // Purple-700
                        typeText = "str";
                        break;
                    case PillDataType.Domain:
                        typeBg = Color.FromArgb(204, 251, 241); // Teal-100
                        typeFg = Color.FromArgb(15, 118, 110);  // Teal-700
                        typeText = "dom";
                        break;
                    case PillDataType.ValueList:
                        typeBg = Color.FromArgb(237, 233, 254); // Purple-100
                        typeFg = Color.FromArgb(109, 40, 217);  // Purple-700
                        typeText = "list";
                        break;
                    default: // Float
                        typeBg = Color.FromArgb(224, 242, 254); // Sky-100
                        typeFg = Color.FromArgb(3, 105, 161);   // Sky-700
                        typeText = "flt";
                        break;
                }

                using (var tPath = CreateRoundedRectangle(s.BoundsType, 2f))
                using (var tBgBrush = new SolidBrush(typeBg))
                using (var tFgBrush = new SolidBrush(typeFg))
                using (var fType = new Font("Segoe UI", 6.5f, FontStyle.Bold))
                {
                    graphics.FillPath(tBgBrush, tPath);
                    var sfT = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    graphics.DrawString(typeText, fType, tFgBrush, s.BoundsType, sfT);
                }

                // B. Nome do Parâmetro
                using (var fName = new Font("Segoe UI", 7.8f, FontStyle.Bold))
                using (var bName = new SolidBrush(Color.FromArgb(30, 41, 59)))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    string displayName = s.ResetOnOpen ? s.Name + " ↺" : s.Name;
                    graphics.DrawString(displayName, fName, bName, s.BoundsName, sf);
                }

                // C. Trilho / Controle Interativo
                if (s.DataType == PillDataType.Boolean)
                {
                    // Renderizar Toggle Switch
                    float switchW = 32f;
                    float switchH = 14f;
                    float switchX = s.BoundsTrack.X + (s.BoundsTrack.Width - switchW) * 0.5f;
                    float switchY = s.BoundsTrack.Y + (s.BoundsTrack.Height - switchH) * 0.5f;
                    RectangleF switchRect = new RectangleF(switchX, switchY, switchW, switchH);

                    bool isOn = s.BoolValue;
                    Color switchBgColor = isOn ? Color.FromArgb(16, 185, 129) : Color.FromArgb(203, 213, 225);
                    using (var sPath = CreateRoundedRectangle(switchRect, 7f))
                    using (var sBrush = new SolidBrush(switchBgColor))
                    {
                        graphics.FillPath(sBrush, sPath);
                    }

                    float knobDiam = 10f;
                    float knobOffX = isOn ? (switchRect.Right - knobDiam - 2f) : (switchRect.X + 2f);
                    float knobOffY = switchRect.Y + (switchRect.Height - knobDiam) * 0.5f;
                    RectangleF boolKnobRect = new RectangleF(knobOffX, knobOffY, knobDiam, knobDiam);

                    using (var bkBrush = new SolidBrush(Color.White))
                    {
                        graphics.FillEllipse(bkBrush, boolKnobRect);
                    }
                }
                else if (s.DataType == PillDataType.Button)
                {
                    // Renderizar Botão de Disparo / Pulso
                    float btnW = Math.Min(s.BoundsTrack.Width - 4f, 65f);
                    float btnH = 14f;
                    float btnX = s.BoundsTrack.X + (s.BoundsTrack.Width - btnW) * 0.5f;
                    float btnY = s.BoundsTrack.Y + (s.BoundsTrack.Height - btnH) * 0.5f;
                    RectangleF btnRect = new RectangleF(btnX, btnY, btnW, btnH);

                    bool isPushed = s.BoolValue;
                    Color btnBg = isPushed ? Color.FromArgb(16, 185, 129) : Color.FromArgb(226, 232, 240);
                    Color btnBorder = isPushed ? Color.FromArgb(5, 150, 105) : Color.FromArgb(203, 213, 225);
                    Color btnTextCol = isPushed ? Color.White : Color.FromArgb(71, 85, 105);

                    using (var bPath = CreateRoundedRectangle(btnRect, 3f))
                    using (var bBrush = new SolidBrush(btnBg))
                    using (var bPen = new Pen(btnBorder, 1f))
                    using (var fBtn = new Font("Segoe UI", 6.6f, FontStyle.Bold))
                    using (var tBrush = new SolidBrush(btnTextCol))
                    using (var sfBtn = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        graphics.FillPath(bBrush, bPath);
                        graphics.DrawPath(bPen, bPath);
                        graphics.DrawString(isPushed ? "ATIVO" : "DISPARAR", fBtn, tBrush, btnRect, sfBtn);
                    }
                }
                else if (s.DataType == PillDataType.String && s.StringOptions.Count == 0)
                {
                    // Renderizar campo de texto editável
                    RectangleF tr = s.BoundsTrack;
                    float railH = 13f;
                    float railY = tr.Y + (tr.Height - railH) * 0.5f;
                    RectangleF railRect = new RectangleF(tr.X, railY, tr.Width, railH);
                    using (var railPath = CreateRoundedRectangle(railRect, 2.5f))
                    using (var railBg = new SolidBrush(Color.FromArgb(241, 245, 249)))
                    using (var railPen = new Pen(Color.FromArgb(203, 213, 225), 1f))
                    using (var fPh = new Font("Segoe UI", 6.8f, FontStyle.Italic))
                    using (var bPh = new SolidBrush(Color.FromArgb(148, 163, 184)))
                    {
                        graphics.FillPath(railBg, railPath);
                        graphics.DrawPath(railPen, railPath);
                        var sfPh = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        graphics.DrawString("✎ texto", fPh, bPh, railRect, sfPh);
                    }
                }
                else if (s.DataType == PillDataType.Domain)
                {
                    // Renderizar Range Slider duplo com faixa preenchida e dois manípulos
                    RectangleF tr = s.BoundsTrack;
                    float railH = 5f;
                    float railY = tr.Y + (tr.Height - railH) * 0.5f;
                    RectangleF railRect = new RectangleF(tr.X, railY, tr.Width, railH);

                    using (var railPath = CreateRoundedRectangle(railRect, 2.5f))
                    using (var railBg = new SolidBrush(Color.FromArgb(203, 213, 225)))
                    {
                        graphics.FillPath(railBg, railPath);
                    }

                    double r0 = (s.Max > s.Min) ? Math.Max(0.0, Math.Min(1.0, (s.DomainStart - s.Min) / (s.Max - s.Min))) : 0.0;
                    double r1 = (s.Max > s.Min) ? Math.Max(0.0, Math.Min(1.0, (s.DomainEnd - s.Min) / (s.Max - s.Min))) : 1.0;
                    if (r0 > r1) { double t = r0; r0 = r1; r1 = t; }

                    float fillX0 = tr.X + (float)(tr.Width * r0);
                    float fillX1 = tr.X + (float)(tr.Width * r1);
                    float fillW = Math.Max(2f, fillX1 - fillX0);

                    RectangleF fillRect = new RectangleF(fillX0, railY, fillW, railH);
                    using (var fillPath = CreateRoundedRectangle(fillRect, 2.5f))
                    using (var fillBrush = new SolidBrush(s.CategoryColor))
                    {
                        graphics.FillPath(fillBrush, fillPath);
                    }

                    // Manípulo 0 (Início)
                    bool isDragging0 = isDragging && (_activeDomainThumb == 0);
                    float knob0Radius = isDragging0 ? 6.5f : 5.5f;
                    RectangleF knob0Rect = new RectangleF(fillX0 - knob0Radius, tr.Y + (tr.Height - knob0Radius * 2) * 0.5f, knob0Radius * 2, knob0Radius * 2);

                    using (var knobBrush = new SolidBrush(Color.White))
                    using (var knobPen = new Pen(s.CategoryColor, isDragging0 ? 2.2f : 1.5f))
                    {
                        graphics.FillEllipse(knobBrush, knob0Rect);
                        graphics.DrawEllipse(knobPen, knob0Rect);
                    }

                    // Manípulo 1 (Fim)
                    bool isDragging1 = isDragging && (_activeDomainThumb == 1);
                    float knob1Radius = isDragging1 ? 6.5f : 5.5f;
                    RectangleF knob1Rect = new RectangleF(fillX1 - knob1Radius, tr.Y + (tr.Height - knob1Radius * 2) * 0.5f, knob1Radius * 2, knob1Radius * 2);

                    using (var knobBrush = new SolidBrush(Color.White))
                    using (var knobPen = new Pen(s.CategoryColor, isDragging1 ? 2.2f : 1.5f))
                    {
                        graphics.FillEllipse(knobBrush, knob1Rect);
                        graphics.DrawEllipse(knobPen, knob1Rect);
                    }
                }
                else
                {
                    // Float, Integer ou String com Opções: Renderizar trilho com progresso e knob
                    RectangleF tr = s.BoundsTrack;
                    float railH = 5f;
                    float railY = tr.Y + (tr.Height - railH) * 0.5f;
                    RectangleF railRect = new RectangleF(tr.X, railY, tr.Width, railH);

                    using (var railPath = CreateRoundedRectangle(railRect, 2.5f))
                    using (var railBg = new SolidBrush(Color.FromArgb(203, 213, 225)))
                    {
                        graphics.FillPath(railBg, railPath);
                    }

                    // Progresso preenchido
                    double ratio = 0.0;
                    if (s.DataType == PillDataType.String && s.StringOptions.Count > 1)
                    {
                        ratio = Math.Max(0.0, Math.Min(1.0, s.Value / (s.StringOptions.Count - 1)));
                    }
                    else if (s.Max > s.Min)
                    {
                        ratio = Math.Max(0.0, Math.Min(1.0, (s.Value - s.Min) / (s.Max - s.Min)));
                    }
                    float fillW = (float)(tr.Width * ratio);

                    if (fillW > 3f)
                    {
                        RectangleF fillRect = new RectangleF(tr.X, railY, fillW, railH);
                        using (var fillPath = CreateRoundedRectangle(fillRect, 2.5f))
                        using (var fillBrush = new SolidBrush(s.CategoryColor))
                        {
                            graphics.FillPath(fillBrush, fillPath);
                        }
                    }

                    // Se String com opções, desenha risquinhos de divisão das opções
                    if (s.DataType == PillDataType.String && s.StringOptions.Count > 1)
                    {
                        using (var penTick = new Pen(Color.FromArgb(148, 163, 184), 1f))
                        {
                            int optCount = s.StringOptions.Count;
                            for (int o = 0; o < optCount; o++)
                            {
                                float tickX = tr.X + (float)(o / (double)(optCount - 1) * tr.Width);
                                graphics.DrawLine(penTick, tickX, railY - 2f, tickX, railY + railH + 2f);
                            }
                        }
                    }

                    // Manípulo / Botão deslizante do Slider (Knob)
                    float knobX = tr.X + fillW;
                    float knobRadius = isDragging ? 6.5f : 5.5f;
                    RectangleF knobRect = new RectangleF(knobX - knobRadius, tr.Y + (tr.Height - knobRadius * 2) * 0.5f, knobRadius * 2, knobRadius * 2);

                    using (var knobBrush = new SolidBrush(Color.White))
                    using (var knobPen = new Pen(s.CategoryColor, isDragging ? 2.2f : 1.5f))
                    {
                        graphics.FillEllipse(knobBrush, knobRect);
                        graphics.DrawEllipse(knobPen, knobRect);
                    }
                }

                // D. Caixa de Valor
                using (var vBoxPath = CreateRoundedRectangle(s.BoundsValueBox, 2f))
                using (var vBoxBg = new SolidBrush(Color.White))
                using (var vBoxPen = new Pen(s.ResetOnOpen ? Color.FromArgb(245, 158, 11) : Color.FromArgb(203, 213, 225), s.ResetOnOpen ? 1.4f : 1f))
                {
                    graphics.FillPath(vBoxBg, vBoxPath);
                    graphics.DrawPath(vBoxPen, vBoxPath);
                }

                string valStr = "";
                Color valTextColor = Color.FromArgb(15, 23, 42);

                if (s.DataType == PillDataType.Boolean)
                {
                    valStr = s.BoolValue ? "TRUE" : "FALSE";
                    valTextColor = s.BoolValue ? Color.FromArgb(16, 185, 129) : Color.FromArgb(100, 116, 139);
                }
                else if (s.DataType == PillDataType.Button)
                {
                    valStr = s.BoolValue ? "TRUE" : "FALSE";
                    valTextColor = s.BoolValue ? Color.FromArgb(16, 185, 129) : Color.FromArgb(148, 163, 184);
                }
                else if (s.DataType == PillDataType.ValueList)
                {
                    valStr = s.StringValue ?? "";
                    if (valStr.Length > 7) valStr = valStr.Substring(0, 6) + "...";
                    valTextColor = Color.FromArgb(109, 40, 217);
                }
                else if (s.DataType == PillDataType.String)
                {
                    valStr = s.StringValue ?? "";
                    if (valStr.Length > 7) valStr = valStr.Substring(0, 6) + "…";
                }
                else if (s.DataType == PillDataType.Integer)
                {
                    valStr = s.IntValue.ToString();
                    if (!string.IsNullOrEmpty(s.Unit)) valStr += s.Unit;
                }
                else if (s.DataType == PillDataType.Domain)
                {
                    string fmt = s.Decimals == 0 ? "F0" : (s.Decimals == 1 ? "F1" : "F2");
                    valStr = $"{s.DomainStart.ToString(fmt, CultureInfo.InvariantCulture)}..{s.DomainEnd.ToString(fmt, CultureInfo.InvariantCulture)}";
                    if (!string.IsNullOrEmpty(s.Unit)) valStr += s.Unit;
                }
                else
                {
                    valStr = s.Value.ToString(s.Decimals == 0 ? "F0" : (s.Decimals == 1 ? "F1" : "F2"), CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(s.Unit)) valStr += s.Unit;
                }

                using (var fVal = new Font("Segoe UI", 7.0f, FontStyle.Bold))
                using (var bVal = new SolidBrush(valTextColor))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    graphics.DrawString(valStr, fVal, bVal, s.BoundsValueBox, sf);
                }
            }
        }

        private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
