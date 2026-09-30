using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Buraqueira_Tools.Dashboard;
using Buraqueira_Tools.Explore;
using Buraqueira_Tools.ProjectState;
using Grasshopper.GUI.Base;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>Espaço de projeto no Grasshopper (saída do Pill Design Space).</summary>
    public class GH_DesignSpaceGoo : GH_Goo<DesignSpace>
    {
        public GH_DesignSpaceGoo()
        {
        }

        public GH_DesignSpaceGoo(DesignSpace space)
        {
            Value = space;
        }

        public override bool IsValid => Value != null && Value.Count > 0;
        public override string TypeName => "Design Space";
        public override string TypeDescription => "Variáveis de projeto (controles do canvas com faixa, passo ou níveis).";
        public override IGH_Goo Duplicate() => new GH_DesignSpaceGoo(Value);
        public override string ToString() => Value?.ToString() ?? "Design Space (vazio)";

        public override bool CastFrom(object source)
        {
            switch (source)
            {
                case DesignSpace s:
                    Value = s;
                    return true;
                case GH_SamplePlanGoo p when p.Value != null:
                    Value = p.Value.Space;
                    return true;
                case SamplePlan plan:
                    Value = plan.Space;
                    return true;
            }
            return false;
        }
    }

    /// <summary>Plano de amostras no Grasshopper (saída do Pill Sampler e do Pill Batch Runner).</summary>
    public class GH_SamplePlanGoo : GH_Goo<SamplePlan>
    {
        public GH_SamplePlanGoo()
        {
        }

        public GH_SamplePlanGoo(SamplePlan plan)
        {
            Value = plan;
        }

        public override bool IsValid => Value != null && Value.Count > 0;
        public override string TypeName => "Sample Plan";
        public override string TypeDescription => "Amostras de um espaço de projeto (método, valores e estrutura para a análise de sensibilidade).";
        public override IGH_Goo Duplicate() => new GH_SamplePlanGoo(Value);
        public override string ToString() => Value?.ToString() ?? "Sample Plan (vazio)";

        public override bool CastFrom(object source)
        {
            if (source is SamplePlan p)
            {
                Value = p;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Ponte entre variáveis de projeto e controles do canvas: descobre variáveis (sliders, toggles, value lists,
    /// Pill Slider Pool, Pill Dashboard), converte valores de amostra em <see cref="ControlState"/> no formato do
    /// controle atual e lê de volta o que o controle aceitou.
    /// </summary>
    internal static class DesignControls
    {
        public static readonly string[] BooleanLevels = { "False", "True" };

        /// <summary>Variáveis de um objeto do canvas (vazio se não for um controle suportado).</summary>
        public static List<DesignVariable> FromObject(IGH_DocumentObject obj, ICollection<string> warnings)
        {
            var list = new List<DesignVariable>();
            switch (obj)
            {
                case GH_NumberSlider s:
                    list.Add(FromSlider(s));
                    break;
                case GH_BooleanToggle t:
                    list.Add(DesignVariable.Choice(t.InstanceGuid.ToString("D"), ControlKinds.Toggle, Label(t), BooleanLevels));
                    break;
                case GH_ValueList v:
                    var names = v.ListItems.Select(i => i.Name).ToList();
                    if (names.Count == 0) warnings?.Add($"Value list '{Label(v)}' está vazia.");
                    else list.Add(DesignVariable.Choice(v.InstanceGuid.ToString("D"), ControlKinds.ValueList, Label(v), names));
                    break;
                case PillSliderPool_Component pool:
                    list.AddRange(FromPool(pool, warnings));
                    break;
                case PillDashboard_Component dash:
                    list.AddRange(FromDashboard(dash, warnings));
                    break;
            }
            return list;
        }

        public static bool IsSupported(IGH_DocumentObject obj) =>
            obj is GH_NumberSlider || obj is GH_BooleanToggle || obj is GH_ValueList || obj is PillSliderPool_Component || obj is PillDashboard_Component;

        /// <summary>Todas as variáveis possíveis do documento, na ordem do canvas.</summary>
        public static List<DesignVariable> Discover(GH_Document doc, ICollection<string> warnings)
        {
            var list = new List<DesignVariable>();
            if (doc == null) return list;
            foreach (var obj in doc.Objects) list.AddRange(FromObject(obj, warnings));
            return list;
        }

        public static DesignVariable FromSlider(GH_NumberSlider s)
        {
            var sl = s.Slider;
            double min = (double)sl.Minimum, max = (double)sl.Maximum, step;
            switch (sl.Type)
            {
                case GH_SliderAccuracy.Integer:
                    step = 1;
                    min = Math.Ceiling(min);
                    break;
                case GH_SliderAccuracy.Even:
                    step = 2;
                    min = Math.Ceiling(min / 2) * 2;
                    break;
                case GH_SliderAccuracy.Odd:
                    step = 2;
                    min = Math.Floor(min / 2) * 2 + 1;
                    if (min < (double)sl.Minimum) min += 2;
                    break;
                default:
                    step = Math.Pow(10, -Math.Max(0, sl.DecimalPlaces));
                    break;
            }
            if (min > max) min = max;
            return DesignVariable.Continuous(s.InstanceGuid.ToString("D"), ControlKinds.Slider, Label(s), min, max, step);
        }

        private static IEnumerable<DesignVariable> FromPool(PillSliderPool_Component pool, ICollection<string> warnings)
        {
            foreach (var s in pool.Sliders)
            {
                string id = pool.InstanceGuid.ToString("D") + "|" + s.CleanKey;
                switch (s.DataType)
                {
                    case PillDataType.Float:
                        yield return DesignVariable.Continuous(id, ControlKinds.PoolSlider, s.FullKey, s.Min, s.Max, Math.Pow(10, -Math.Max(0, s.Decimals)));
                        break;
                    case PillDataType.Integer:
                        yield return DesignVariable.Continuous(id, ControlKinds.PoolSlider, s.FullKey, Math.Ceiling(s.Min), Math.Max(Math.Ceiling(s.Min), Math.Floor(s.Max)), 1);
                        break;
                    case PillDataType.Boolean:
                        yield return DesignVariable.Choice(id, ControlKinds.PoolSlider, s.FullKey, BooleanLevels);
                        break;
                    case PillDataType.String:
                    case PillDataType.ValueList:
                        if (s.StringOptions.Count > 0) yield return DesignVariable.Choice(id, ControlKinds.PoolSlider, s.FullKey, s.StringOptions);
                        else warnings?.Add($"'{s.FullKey}' (texto livre) não tem opções para amostrar; use levels= em Ranges.");
                        break;
                    default:
                        warnings?.Add($"'{s.FullKey}' ({s.TypeBadge}) não é amostrado: use sliders numéricos, booleanos ou listas.");
                        break;
                }
            }
        }

        private static IEnumerable<DesignVariable> FromDashboard(PillDashboard_Component dash, ICollection<string> warnings)
        {
            foreach (var w in dash.Controller.Widgets)
            {
                var spec = w.Spec;
                if (!spec.IsPersistent) continue;
                string id = DashboardVault.ControlId(dash.InstanceGuid, w.Id);
                string name = dash.NickName + ": " + (spec.Label.Length > 0 ? spec.Label : w.Id);
                switch (spec.Kind)
                {
                    case WidgetKind.Slider:
                        yield return DesignVariable.Continuous(id, ControlKinds.Dashboard, name, spec.Min, spec.Max, spec.Step);
                        break;
                    case WidgetKind.Toggle:
                        yield return DesignVariable.Choice(id, ControlKinds.Dashboard, name, BooleanLevels);
                        break;
                    case WidgetKind.Dropdown:
                        if (spec.Options.Count > 0) yield return DesignVariable.Choice(id, ControlKinds.Dashboard, name, spec.Options.ToList());
                        else warnings?.Add($"Dropdown '{name}' sem opções.");
                        break;
                }
            }
        }

        /// <summary>Avisa quando a faixa sobreposta passa dos limites do controle (o controle vai limitar os valores).</summary>
        public static void CheckAgainstControl(DesignVariable original, DesignVariable edited, ICollection<string> warnings)
        {
            if (original == null || edited == null || ReferenceEquals(original, edited)) return;
            if (original.Type == DesignVariableType.Continuous)
            {
                double lo, hi;
                if (edited.Type == DesignVariableType.Continuous)
                {
                    lo = edited.Min;
                    hi = edited.Max;
                }
                else if (edited.NumericLevels)
                {
                    var nums = edited.Levels.Select(l => { DesignVariable.TryParseNumber(l, out double v); return v; }).ToList();
                    lo = nums.Min();
                    hi = nums.Max();
                }
                else
                {
                    warnings?.Add($"'{edited.Name}': níveis não numéricos num controle numérico.");
                    return;
                }
                double tol = 1e-9 * Math.Max(1, Math.Abs(original.Max - original.Min));
                if (lo < original.Min - tol || hi > original.Max + tol)
                {
                    warnings?.Add($"'{edited.Name}': faixa [{DesignVariable.FormatNumber(lo)}, {DesignVariable.FormatNumber(hi)}] passa dos limites do controle " +
                                  $"[{DesignVariable.FormatNumber(original.Min)}, {DesignVariable.FormatNumber(original.Max)}]; o controle vai limitar os valores.");
                }
                if (edited.Type == DesignVariableType.Continuous && original.Step > 0 && (edited.Step <= 0 || edited.Step < original.Step - 1e-12))
                {
                    warnings?.Add($"'{edited.Name}': passo menor que a precisão do controle ({DesignVariable.FormatNumber(original.Step)}); os valores serão arredondados pelo controle.");
                }
            }
            else if (edited.Type == DesignVariableType.Choice)
            {
                var known = new HashSet<string>(original.Levels, StringComparer.OrdinalIgnoreCase);
                var unknown = edited.Levels.Where(l => !known.Contains(l)).ToList();
                if (unknown.Count > 0) warnings?.Add($"'{edited.Name}': nível(is) {string.Join(", ", unknown)} não existe(m) no controle e serão ignorados.");
            }
        }

        /// <summary>Estado a aplicar para o valor de amostra, no mesmo formato do estado atual do controle.</summary>
        public static ControlState ToSaved(DesignVariable v, double value, ControlState current)
        {
            var s = new ControlState { Kind = current.Kind, Id = current.Id, Name = current.Name };
            string level = v.Type == DesignVariableType.Choice ? v.Format(value) : null;
            if (current.Boolean.HasValue)
            {
                s.Boolean = level != null ? (bool.TryParse(level, out bool b) ? b : Math.Round(value) >= 1) : value >= 0.5;
            }
            else if (current.Text != null && !current.Number.HasValue)
            {
                s.Text = level ?? DesignVariable.FormatNumber(value);
            }
            else
            {
                if (level == null) s.Number = value;
                else if (bool.TryParse(level, out bool b)) s.Number = b ? 1 : 0;
                else if (DesignVariable.TryParseNumber(level, out double n)) s.Number = n;
                else s.Number = value;
                if (current.Extra.HasValue) s.Extra = current.Extra;
            }
            return s;
        }

        /// <summary>Valor de amostra correspondente ao que o controle realmente tem (escolha → índice do nível).</summary>
        public static double ActualValue(DesignVariable v, ControlState actual, double requested)
        {
            if (actual == null) return requested;
            if (v.Type == DesignVariableType.Continuous) return actual.Number ?? requested;
            for (int i = 0; i < v.Levels.Count; i++)
            {
                string level = v.Levels[i];
                if (actual.Boolean.HasValue)
                {
                    if (bool.TryParse(level, out bool b) && b == actual.Boolean.Value) return i;
                }
                else if (actual.Text != null && !actual.Number.HasValue)
                {
                    if (string.Equals(level, actual.Text, StringComparison.OrdinalIgnoreCase)) return i;
                }
                else if (actual.Number.HasValue)
                {
                    if (bool.TryParse(level, out bool b) && (b ? 1 : 0) == Math.Round(actual.Number.Value)) return i;
                    if (DesignVariable.TryParseNumber(level, out double n) && Math.Abs(n - actual.Number.Value) <= 1e-9 * Math.Max(1, Math.Abs(n))) return i;
                }
            }
            return requested;
        }

        /// <summary>Mesmo valor de controle (tolerância relativa para números).</summary>
        public static bool SameValue(ControlState a, ControlState b)
        {
            if (a == null || b == null) return a == b;
            if (a.Number.HasValue || b.Number.HasValue)
            {
                if (!a.Number.HasValue || !b.Number.HasValue) return false;
                double x = a.Number.Value, y = b.Number.Value;
                if (Math.Abs(x - y) > 1e-9 * Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y)))) return false;
                return a.Extra.HasValue == b.Extra.HasValue && (!a.Extra.HasValue || Math.Abs(a.Extra.Value - b.Extra.Value) <= 1e-9 * Math.Max(1, Math.Abs(a.Extra.Value)));
            }
            if (a.Boolean.HasValue || b.Boolean.HasValue) return a.Boolean == b.Boolean;
            return string.Equals(a.Text ?? "", b.Text ?? "", StringComparison.Ordinal);
        }

        /// <summary>Estado atual de cada controle do documento, por Id.</summary>
        public static Dictionary<string, ControlState> CaptureById(GH_Document doc)
        {
            var map = new Dictionary<string, ControlState>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in ControlStateService.Capture(doc, includePanels: false))
            {
                if (!string.IsNullOrEmpty(s.Id)) map[s.Id] = s;
            }
            return map;
        }

        /// <summary>Valor de amostra como dado do Grasshopper: número, ou o texto do nível (escolhas).</summary>
        public static IGH_Goo Display(DesignVariable v, double value)
        {
            if (v == null) return new GH_Number(value);
            if (v.Type == DesignVariableType.Choice)
            {
                if (v.NumericLevels) return new GH_Number(v.NumericValue(value));
                string level = v.Format(value);
                return bool.TryParse(level, out bool b) ? (IGH_Goo)new GH_Boolean(b) : new GH_String(level);
            }
            return v.Step > 0 && Math.Abs(v.Step - Math.Round(v.Step)) < 1e-12 && Math.Abs(v.Min - Math.Round(v.Min)) < 1e-12 && Math.Abs(value) < int.MaxValue
                ? (IGH_Goo)new GH_Integer((int)Math.Round(value))
                : new GH_Number(value);
        }

        /// <summary>Número de um item de resultado (NaN se não for numérico).</summary>
        public static double ToNumber(IGH_Goo goo)
        {
            switch (goo)
            {
                case null:
                    return double.NaN;
                case GH_Number n:
                    return n.Value;
                case GH_Integer i:
                    return i.Value;
                case GH_Boolean b:
                    return b.Value ? 1 : 0;
                case GH_String s:
                    return DesignVariable.TryParseNumber(s.Value, out double v) ? v : double.NaN;
            }
            var number = new GH_Number();
            return number.CastFrom(goo) ? number.Value : double.NaN;
        }

        public static string Label(IGH_DocumentObject obj) => string.IsNullOrWhiteSpace(obj.NickName) ? obj.Name : obj.NickName;

        public static string Invariant(double v) => v.ToString("G6", CultureInfo.InvariantCulture);
    }
}
