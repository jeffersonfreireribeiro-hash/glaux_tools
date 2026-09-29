using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Buraqueira_Tools.Dashboard
{
    /// <summary>Tipos de widget do MVP. Um tipo novo = um valor aqui + uma classe em <c>DashboardWidgets</c> + uma linha no registro.</summary>
    public enum WidgetKind
    {
        Label,
        Number,
        Slider,
        Toggle,
        Button,
        Dropdown,
        Progress,
        MiniChart
    }

    public enum LayoutKind
    {
        Stack,
        Row,
        Grid
    }

    public enum WidgetAlign
    {
        Stretch,
        Left,
        Center,
        Right
    }

    /// <summary>
    /// Quando um controle arrastado entrega o valor ao Grasshopper:
    /// Live = durante o arrasto (com throttle), Release = só ao soltar, Auto = Live enquanto a solução for leve.
    /// </summary>
    public enum CommitMode
    {
        Auto,
        Live,
        Release
    }

    public static class WidgetKinds
    {
        private static readonly Dictionary<string, WidgetKind> s_aliases = new Dictionary<string, WidgetKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["label"] = WidgetKind.Label, ["text"] = WidgetKind.Label, ["status"] = WidgetKind.Label,
            ["number"] = WidgetKind.Number, ["value"] = WidgetKind.Number, ["kpi"] = WidgetKind.Number,
            ["slider"] = WidgetKind.Slider,
            ["toggle"] = WidgetKind.Toggle, ["switch"] = WidgetKind.Toggle, ["bool"] = WidgetKind.Toggle,
            ["button"] = WidgetKind.Button, ["btn"] = WidgetKind.Button,
            ["dropdown"] = WidgetKind.Dropdown, ["list"] = WidgetKind.Dropdown, ["select"] = WidgetKind.Dropdown,
            ["progress"] = WidgetKind.Progress, ["bar"] = WidgetKind.Progress,
            ["minichart"] = WidgetKind.MiniChart, ["chart"] = WidgetKind.MiniChart, ["sparkline"] = WidgetKind.MiniChart
        };

        public static bool TryParse(string text, out WidgetKind kind)
        {
            kind = WidgetKind.Label;
            return !string.IsNullOrWhiteSpace(text) && s_aliases.TryGetValue(text.Trim(), out kind);
        }

        public static string Name(WidgetKind kind) => kind == WidgetKind.MiniChart ? "chart" : kind.ToString().ToLowerInvariant();

        /// <summary>Controles produzem valores para o Grasshopper; os demais só exibem dados.</summary>
        public static bool IsControl(WidgetKind kind) =>
            kind == WidgetKind.Slider || kind == WidgetKind.Toggle || kind == WidgetKind.Button || kind == WidgetKind.Dropdown;

        /// <summary>Controles cujo valor é estado do projeto (gravado no .gh e nos presets). O botão é momentâneo: não entra.</summary>
        public static bool IsPersistent(WidgetKind kind) =>
            kind == WidgetKind.Slider || kind == WidgetKind.Toggle || kind == WidgetKind.Dropdown;
    }

    public enum WidgetValueKind
    {
        None,
        Number,
        Boolean,
        Text,
        Series
    }

    /// <summary>Valor imutável de um widget (número, booleano, texto ou série numérica).</summary>
    public readonly struct WidgetValue : IEquatable<WidgetValue>
    {
        private readonly double[] _series;

        private WidgetValue(WidgetValueKind kind, double number, bool boolean, string text, double[] series)
        {
            Kind = kind;
            Number = number;
            Boolean = boolean;
            Text = text;
            _series = series;
        }

        public WidgetValueKind Kind { get; }
        public double Number { get; }
        public bool Boolean { get; }
        public string Text { get; }
        public IReadOnlyList<double> Series => _series ?? Array.Empty<double>();

        public bool IsNone => Kind == WidgetValueKind.None;

        public static WidgetValue None => default;
        public static WidgetValue FromNumber(double value) => new WidgetValue(WidgetValueKind.Number, value, false, null, null);
        public static WidgetValue FromBoolean(bool value) => new WidgetValue(WidgetValueKind.Boolean, value ? 1 : 0, value, null, null);
        public static WidgetValue FromText(string value) => new WidgetValue(WidgetValueKind.Text, double.NaN, false, value ?? "", null);
        public static WidgetValue FromSeries(IEnumerable<double> values) => new WidgetValue(WidgetValueKind.Series, double.NaN, false, null, values?.ToArray() ?? Array.Empty<double>());

        public bool TryGetNumber(out double value)
        {
            switch (Kind)
            {
                case WidgetValueKind.Number:
                    value = Number;
                    return !double.IsNaN(value);
                case WidgetValueKind.Boolean:
                    value = Boolean ? 1 : 0;
                    return true;
                case WidgetValueKind.Text:
                    return DashboardText.TryParseNumber(Text, out value);
                case WidgetValueKind.Series:
                    value = _series != null && _series.Length > 0 ? _series[_series.Length - 1] : double.NaN;
                    return !double.IsNaN(value);
                default:
                    value = double.NaN;
                    return false;
            }
        }

        public bool TryGetBoolean(out bool value)
        {
            switch (Kind)
            {
                case WidgetValueKind.Boolean:
                    value = Boolean;
                    return true;
                case WidgetValueKind.Number:
                    value = Math.Abs(Number) > 1e-12;
                    return !double.IsNaN(Number);
                case WidgetValueKind.Text:
                    return DashboardText.TryParseBoolean(Text, out value);
                default:
                    value = false;
                    return false;
            }
        }

        /// <summary>Texto invariante (ponto decimal), usado no estado gravado e nas saídas de texto.</summary>
        public string ToInvariantString()
        {
            switch (Kind)
            {
                case WidgetValueKind.Number: return DashboardText.FormatRoundTrip(Number);
                case WidgetValueKind.Boolean: return Boolean ? "true" : "false";
                case WidgetValueKind.Text: return Text ?? "";
                case WidgetValueKind.Series: return string.Join(";", Series.Select(DashboardText.FormatRoundTrip));
                default: return "";
            }
        }

        public override string ToString() => ToInvariantString();

        public bool Equals(WidgetValue other)
        {
            if (Kind != other.Kind) return false;
            switch (Kind)
            {
                case WidgetValueKind.Number: return Number.Equals(other.Number);
                case WidgetValueKind.Boolean: return Boolean == other.Boolean;
                case WidgetValueKind.Text: return string.Equals(Text, other.Text, StringComparison.Ordinal);
                case WidgetValueKind.Series:
                    var a = Series;
                    var b = other.Series;
                    if (a.Count != b.Count) return false;
                    for (int i = 0; i < a.Count; i++)
                    {
                        if (!a[i].Equals(b[i])) return false;
                    }
                    return true;
                default: return true;
            }
        }

        public override bool Equals(object obj) => obj is WidgetValue v && Equals(v);

        public override int GetHashCode()
        {
            switch (Kind)
            {
                case WidgetValueKind.Number: return Number.GetHashCode();
                case WidgetValueKind.Boolean: return Boolean ? 1 : 2;
                case WidgetValueKind.Text: return StringComparer.Ordinal.GetHashCode(Text ?? "");
                case WidgetValueKind.Series: return Series.Count;
                default: return 0;
            }
        }

        public static bool operator ==(WidgetValue a, WidgetValue b) => a.Equals(b);
        public static bool operator !=(WidgetValue a, WidgetValue b) => !a.Equals(b);
    }

    /// <summary>Conversões de texto invariantes compartilhadas pelo Dashboard.</summary>
    public static class DashboardText
    {
        public static bool TryParseNumber(string text, out double value)
        {
            value = double.NaN;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim();
            // Aceita vírgula decimal quando não há ponto ("2,5"), comum em planilhas em português
            if (t.IndexOf(',') >= 0 && t.IndexOf('.') < 0) t = t.Replace(',', '.');
            return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value);
        }

        public static bool TryParseBoolean(string text, out bool value)
        {
            value = false;
            if (string.IsNullOrWhiteSpace(text)) return false;
            switch (text.Trim().ToLowerInvariant())
            {
                case "true": case "1": case "yes": case "sim": case "on": case "verdadeiro":
                    value = true;
                    return true;
                case "false": case "0": case "no": case "não": case "nao": case "off": case "falso":
                    value = false;
                    return true;
                default:
                    return false;
            }
        }

        public static string FormatRoundTrip(double value)
        {
            if (double.IsNaN(value)) return "NaN";
            if (double.IsPositiveInfinity(value)) return "Infinity";
            if (double.IsNegativeInfinity(value)) return "-Infinity";
            string r = value.ToString("R", CultureInfo.InvariantCulture);
            return r;
        }

        public static List<double> ParseSeries(string text)
        {
            var list = new List<double>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            char[] seps = text.IndexOf(';') >= 0 ? new[] { ';' } : new[] { ',', ' ', '\t' };
            foreach (var part in text.Split(seps, StringSplitOptions.RemoveEmptyEntries))
            {
                if (double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) list.Add(v);
            }
            return list;
        }

        /// <summary>Identificador estável a partir de um texto: minúsculas, sem acentos, só letras/dígitos/_ .</summary>
        public static string Slug(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string decomposed = text.Trim().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            bool lastUnderscore = false;
            foreach (char c in decomposed)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                    lastUnderscore = false;
                }
                else if (!lastUnderscore && sb.Length > 0)
                {
                    sb.Append('_');
                    lastUnderscore = true;
                }
            }
            return sb.ToString().TrimEnd('_');
        }
    }

    /// <summary>
    /// Configuração de um widget (o que o autor do painel define): tipo, rótulo, faixa, opções, ligação com o PillHub, layout e estilo.
    /// Imutável; o valor atual de um controle é estado de execução e fica em <see cref="DashboardState"/>.
    /// </summary>
    public sealed class WidgetSpec
    {
        private readonly List<KeyValuePair<string, string>> _settings;

        private WidgetSpec(WidgetKind kind, string label, List<KeyValuePair<string, string>> settings)
        {
            Kind = kind;
            Label = label ?? "";
            _settings = settings;
        }

        public WidgetKind Kind { get; }
        public string Label { get; }

        /// <summary>Identificador estável (chave do estado): 'id=', senão a chave do Hub, senão tipo + rótulo.</summary>
        public string Id { get; private set; }

        /// <summary>Configurações na ordem em que foram escritas (preservadas para ida e volta).</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Settings => _settings;

        public string HubKey { get; private set; }
        public string Source { get; private set; }
        public WidgetValue Default { get; private set; }
        public double Min { get; private set; }
        public double Max { get; private set; }
        public bool HasRange { get; private set; }
        public double Step { get; private set; }
        public int Decimals { get; private set; }
        public string Unit { get; private set; }
        public IReadOnlyList<string> Options { get; private set; }
        public bool OutputIndex { get; private set; }
        public CommitMode Commit { get; private set; }
        public int ThrottleMs { get; private set; }
        public int Order { get; private set; }
        public bool Visible { get; private set; }
        public bool Enabled { get; private set; }
        public int Span { get; private set; }
        public float Height { get; private set; }
        public WidgetAlign Align { get; private set; }
        public string Color { get; private set; }
        public int Lines { get; private set; }
        public string Caption { get; private set; }
        public int MaxPoints { get; private set; }

        /// <summary>Valor de dados embutido pelo Builder (indicadores). Não faz parte da definição textual.</summary>
        public WidgetValue EmbeddedValue { get; private set; }

        public bool IsControl => WidgetKinds.IsControl(Kind);
        public bool IsPersistent => WidgetKinds.IsPersistent(Kind);

        public string Setting(string name)
        {
            for (int i = _settings.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_settings[i].Key, name, StringComparison.OrdinalIgnoreCase)) return _settings[i].Value;
            }
            return null;
        }

        public WidgetSpec WithEmbeddedValue(WidgetValue value)
        {
            var copy = (WidgetSpec)MemberwiseClone();
            copy.EmbeddedValue = value;
            return copy;
        }

        internal WidgetSpec WithId(string id)
        {
            var copy = (WidgetSpec)MemberwiseClone();
            copy.Id = id;
            return copy;
        }

        internal WidgetSpec WithOrder(int order)
        {
            var copy = (WidgetSpec)MemberwiseClone();
            copy.Order = order;
            return copy;
        }

        /// <summary>Cria a configuração validando os parâmetros; problemas viram avisos, nunca exceções.</summary>
        public static WidgetSpec Create(WidgetKind kind, string label, IEnumerable<KeyValuePair<string, string>> settings, ICollection<string> warnings = null)
        {
            var list = settings?.Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                .Select(kv => new KeyValuePair<string, string>(kv.Key.Trim().ToLowerInvariant(), (kv.Value ?? "").Trim()))
                .ToList() ?? new List<KeyValuePair<string, string>>();
            string cleanLabel = string.IsNullOrWhiteSpace(label) ? "" : label.Trim();
            var spec = new WidgetSpec(kind, cleanLabel, list);
            spec.Normalize(warnings ?? new List<string>());
            return spec;
        }

        private void Normalize(ICollection<string> warnings)
        {
            string who = $"{WidgetKinds.Name(Kind)} '{(Label.Length > 0 ? Label : "?")}'";

            HubKey = NullIfEmpty(Setting("key"));
            Source = NullIfEmpty(Setting("source"));
            Unit = Setting("unit") ?? "";
            Color = NullIfEmpty(Setting("color"));
            Caption = NullIfEmpty(Setting("text"));

            string explicitId = NullIfEmpty(Setting("id"));
            if (explicitId != null) Id = DashboardText.Slug(explicitId);
            if (string.IsNullOrEmpty(Id) && HubKey != null) Id = DashboardText.Slug(HubKey);
            if (string.IsNullOrEmpty(Id)) Id = WidgetKinds.Name(Kind) + "_" + (DashboardText.Slug(Label).Length > 0 ? DashboardText.Slug(Label) : "w");

            Visible = !Flag("hidden") && ParseBool("visible", true);
            Enabled = !Flag("disabled") && ParseBool("enabled", true);
            Order = ParseInt("order", int.MinValue, warnings, who);
            Span = Math.Max(1, ParseInt("span", 1, warnings, who));
            Lines = Math.Max(1, Math.Min(6, ParseInt("lines", 1, warnings, who)));
            MaxPoints = Math.Max(2, ParseInt("points", 240, warnings, who));
            ThrottleMs = Math.Max(0, ParseInt("throttle", 80, warnings, who));
            Height = (float)ParseDouble("height", double.NaN, warnings, who);
            if (!(Height > 0)) Height = float.NaN;

            string align = Setting("align");
            Align = WidgetAlign.Stretch;
            if (align != null)
            {
                if (TryParseName(align, out WidgetAlign a)) Align = a;
                else warnings.Add($"{who}: align '{align}' inválido (stretch, left, center, right).");
            }

            string commit = Setting("commit");
            Commit = CommitMode.Auto;
            if (commit != null)
            {
                if (TryParseName(commit, out CommitMode mode)) Commit = mode;
                else warnings.Add($"{who}: commit '{commit}' inválido (auto, live, release).");
            }

            OutputIndex = string.Equals(Setting("output"), "index", StringComparison.OrdinalIgnoreCase);

            var options = new List<string>();
            string rawOptions = Setting("options");
            if (!string.IsNullOrEmpty(rawOptions))
            {
                char sep = rawOptions.IndexOf(';') >= 0 ? ';' : ',';
                foreach (var o in rawOptions.Split(sep))
                {
                    string t = o.Trim();
                    if (t.Length > 0 && !options.Contains(t, StringComparer.OrdinalIgnoreCase)) options.Add(t);
                }
            }
            Options = options;

            double min = ParseDouble("min", double.NaN, warnings, who);
            double max = ParseDouble("max", double.NaN, warnings, who);
            Step = Math.Max(0, ParseDouble("step", 0, warnings, who));
            int decimals = ParseInt("decimals", -1, warnings, who);

            switch (Kind)
            {
                case WidgetKind.Slider:
                    if (double.IsNaN(min)) min = 0;
                    if (double.IsNaN(max)) max = min < 1 ? 1 : min + 1;
                    break;
                case WidgetKind.Progress:
                    if (double.IsNaN(min)) min = 0;
                    if (double.IsNaN(max)) max = 1;
                    break;
            }

            if (!double.IsNaN(min) && !double.IsNaN(max))
            {
                if (min > max)
                {
                    warnings.Add($"{who}: min > max; faixa invertida corrigida.");
                    double t = min;
                    min = max;
                    max = t;
                }
                HasRange = true;
            }
            Min = min;
            Max = max;

            if (decimals < 0)
            {
                decimals = Step > 0 ? DecimalsOf(Step) : (Kind == WidgetKind.Slider ? 2 : -1);
            }
            Decimals = Math.Min(12, decimals);

            if (Kind == WidgetKind.Dropdown && Options.Count == 0) warnings.Add($"{who}: sem 'options' (ex: options=A;B;C).");

            Default = DefaultFor(Setting("value"), warnings, who);
        }

        private WidgetValue DefaultFor(string raw, ICollection<string> warnings, string who)
        {
            switch (Kind)
            {
                case WidgetKind.Slider:
                {
                    double v = Min;
                    if (raw != null && !DashboardText.TryParseNumber(raw, out v))
                    {
                        warnings.Add($"{who}: value '{raw}' não é número.");
                        v = Min;
                    }
                    var coerced = WidgetValueRules.Coerce(this, WidgetValue.FromNumber(v));
                    if (raw != null && Math.Abs(coerced.Number - v) > 1e-9 && (v < Min || v > Max)) warnings.Add($"{who}: value {raw} fora da faixa [{DashboardText.FormatRoundTrip(Min)}, {DashboardText.FormatRoundTrip(Max)}].");
                    return coerced;
                }
                case WidgetKind.Toggle:
                case WidgetKind.Button:
                {
                    bool b = false;
                    if (raw != null && !DashboardText.TryParseBoolean(raw, out b)) warnings.Add($"{who}: value '{raw}' não é booleano.");
                    return WidgetValue.FromBoolean(Kind == WidgetKind.Button ? false : b);
                }
                case WidgetKind.Dropdown:
                {
                    if (Options.Count == 0) return WidgetValue.FromText("");
                    var v = WidgetValueRules.Coerce(this, WidgetValue.FromText(raw ?? Options[0]));
                    if (raw != null && v.IsNone) warnings.Add($"{who}: value '{raw}' não está nas opções.");
                    return v.IsNone ? WidgetValue.FromText(Options[0]) : v;
                }
                case WidgetKind.Number:
                case WidgetKind.Progress:
                    return raw != null && DashboardText.TryParseNumber(raw, out double n) ? WidgetValue.FromNumber(n) : WidgetValue.None;
                case WidgetKind.MiniChart:
                    return raw != null ? WidgetValue.FromSeries(DashboardText.ParseSeries(raw)) : WidgetValue.None;
                default:
                    return raw != null ? WidgetValue.FromText(raw) : WidgetValue.None;
            }
        }

        private bool Flag(string name)
        {
            string v = Setting(name);
            if (v == null) return false;
            return v.Length == 0 || !DashboardText.TryParseBoolean(v, out bool b) || b;
        }

        private bool ParseBool(string name, bool fallback)
        {
            string v = Setting(name);
            return v != null && DashboardText.TryParseBoolean(v, out bool b) ? b : fallback;
        }

        private int ParseInt(string name, int fallback, ICollection<string> warnings, string who)
        {
            string v = Setting(name);
            if (string.IsNullOrEmpty(v)) return fallback;
            if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r)) return r;
            if (DashboardText.TryParseNumber(v, out double d) && !double.IsInfinity(d)) return (int)Math.Round(d);
            warnings.Add($"{who}: {name} '{v}' não é número.");
            return fallback;
        }

        private double ParseDouble(string name, double fallback, ICollection<string> warnings, string who)
        {
            string v = Setting(name);
            if (string.IsNullOrEmpty(v)) return fallback;
            if (DashboardText.TryParseNumber(v, out double r) && !double.IsInfinity(r)) return r;
            warnings.Add($"{who}: {name} '{v}' não é número.");
            return fallback;
        }

        private static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>Enum só pelo nome (Enum.TryParse aceitaria "7" como valor fora do enum).</summary>
        internal static bool TryParseName<T>(string text, out T value) where T : struct
        {
            value = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (var name in Enum.GetNames(typeof(T)))
            {
                if (string.Equals(name, text.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    value = (T)Enum.Parse(typeof(T), name);
                    return true;
                }
            }
            return false;
        }

        internal static int DecimalsOf(double step)
        {
            for (int d = 0; d <= 10; d++)
            {
                if (Math.Abs(step * Math.Pow(10, d) - Math.Round(step * Math.Pow(10, d))) < 1e-9) return d;
            }
            return 10;
        }

        /// <summary>Linha textual equivalente (ida e volta com <see cref="DashboardSpecParser"/>).</summary>
        public string ToSpecLine()
        {
            var sb = new StringBuilder();
            sb.Append(WidgetKinds.Name(Kind));
            if (Label.Length > 0)
            {
                sb.Append(' ');
                sb.Append(DashboardSpecParser.Quote(Label));
            }
            foreach (var kv in _settings)
            {
                sb.Append(" | ");
                sb.Append(kv.Key);
                sb.Append('=');
                sb.Append(DashboardSpecParser.Quote(kv.Value));
            }
            return sb.ToString();
        }

        public override string ToString() => ToSpecLine();
    }

    /// <summary>Regras de valor por tipo de widget: faixa, passo, casas decimais e opções válidas.</summary>
    public static class WidgetValueRules
    {
        /// <summary>
        /// Converte um valor qualquer para o valor válido do widget (ex: texto "2,5" → número dentro da faixa, arredondado ao passo).
        /// Devolve <see cref="WidgetValue.None"/> quando não há conversão possível.
        /// </summary>
        public static WidgetValue Coerce(WidgetSpec spec, WidgetValue raw)
        {
            switch (spec.Kind)
            {
                case WidgetKind.Slider:
                    return raw.TryGetNumber(out double n) && !double.IsInfinity(n) ? WidgetValue.FromNumber(Snap(spec, n)) : WidgetValue.None;
                case WidgetKind.Toggle:
                case WidgetKind.Button:
                    return raw.TryGetBoolean(out bool b) ? WidgetValue.FromBoolean(b) : WidgetValue.None;
                case WidgetKind.Dropdown:
                {
                    if (spec.Options.Count == 0) return WidgetValue.None;
                    if (raw.Kind == WidgetValueKind.Text)
                    {
                        foreach (var o in spec.Options)
                        {
                            if (string.Equals(o, raw.Text?.Trim(), StringComparison.OrdinalIgnoreCase)) return WidgetValue.FromText(o);
                        }
                        // Índice escrito como texto ("2")
                        if (int.TryParse(raw.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ti) && ti >= 0 && ti < spec.Options.Count)
                        {
                            return WidgetValue.FromText(spec.Options[ti]);
                        }
                        return WidgetValue.None;
                    }
                    if (raw.TryGetNumber(out double idx))
                    {
                        int i = (int)Math.Round(idx);
                        return i >= 0 && i < spec.Options.Count ? WidgetValue.FromText(spec.Options[i]) : WidgetValue.None;
                    }
                    return WidgetValue.None;
                }
                default:
                    return raw;
            }
        }

        /// <summary>Limita à faixa, arredonda ao passo (a partir do mínimo) e às casas decimais.</summary>
        public static double Snap(WidgetSpec spec, double value)
        {
            double v = value;
            if (spec.HasRange) v = Math.Max(spec.Min, Math.Min(spec.Max, v));
            if (spec.Step > 0)
            {
                double baseValue = spec.HasRange ? spec.Min : 0;
                v = baseValue + Math.Round((v - baseValue) / spec.Step) * spec.Step;
                if (spec.HasRange && v > spec.Max + 1e-12) v -= spec.Step;
            }
            if (spec.Decimals >= 0) v = Math.Round(v, spec.Decimals, MidpointRounding.AwayFromZero);
            if (spec.HasRange) v = Math.Max(spec.Min, Math.Min(spec.Max, v));
            return v == 0 ? 0 : v; // sem -0
        }

        /// <summary>Posição 0..1 do valor na faixa do slider/progresso.</summary>
        public static double Ratio(WidgetSpec spec, double value)
        {
            if (!spec.HasRange || spec.Max <= spec.Min || double.IsNaN(value)) return 0;
            return Math.Max(0, Math.Min(1, (value - spec.Min) / (spec.Max - spec.Min)));
        }

        /// <summary>Valor do slider para uma posição 0..1 do trilho (já com passo e decimais).</summary>
        public static double FromRatio(WidgetSpec spec, double ratio)
        {
            double r = double.IsNaN(ratio) ? 0 : Math.Max(0, Math.Min(1, ratio));
            if (!spec.HasRange) return 0;
            return Snap(spec, spec.Min + r * (spec.Max - spec.Min));
        }
    }

    /// <summary>Configuração do painel: título, layout e widgets. O hash identifica mudanças de configuração.</summary>
    public sealed class DashboardSpec
    {
        public string Title { get; set; } = "Pill Dashboard";
        public LayoutKind Layout { get; set; } = LayoutKind.Stack;
        public int Columns { get; set; } = 2;
        public float Width { get; set; } = 300f;
        public float Padding { get; set; } = 8f;
        public float Spacing { get; set; } = 6f;
        public List<WidgetSpec> Widgets { get; } = new List<WidgetSpec>();
        public List<string> Warnings { get; } = new List<string>();

        public static DashboardSpec Empty => new DashboardSpec();

        public WidgetSpec Find(string id)
        {
            foreach (var w in Widgets)
            {
                if (string.Equals(w.Id, id, StringComparison.OrdinalIgnoreCase)) return w;
            }
            return null;
        }

        /// <summary>Linhas de texto que reconstroem esta configuração.</summary>
        public List<string> ToLines()
        {
            var lines = new List<string>
            {
                "title = " + Title,
                "layout = " + Layout.ToString().ToLowerInvariant(),
                "columns = " + Columns.ToString(CultureInfo.InvariantCulture),
                "width = " + Width.ToString("R", CultureInfo.InvariantCulture),
                "padding = " + Padding.ToString("R", CultureInfo.InvariantCulture),
                "spacing = " + Spacing.ToString("R", CultureInfo.InvariantCulture)
            };
            foreach (var w in Widgets) lines.Add(w.ToSpecLine());
            return lines;
        }

        /// <summary>Hash da configuração (inclui valores embutidos pelo Builder).</summary>
        public string ComputeHash()
        {
            var sb = new StringBuilder();
            foreach (var l in ToLines()) sb.Append(l).Append('\n');
            foreach (var w in Widgets)
            {
                if (!w.EmbeddedValue.IsNone) sb.Append(w.Id).Append('=').Append((int)w.EmbeddedValue.Kind).Append(':').Append(w.EmbeddedValue.ToInvariantString()).Append('\n');
            }
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return BitConverter.ToString(bytes, 0, 12).Replace("-", "");
            }
        }
    }

    /// <summary>
    /// Lê a definição textual de um painel. Uma linha por widget: <c>tipo Rótulo | chave=valor | ...</c>;
    /// linhas <c>title = ...</c>, <c>layout = stack|row|grid</c>, <c>columns</c>, <c>width</c>, <c>padding</c>, <c>spacing</c> configuram o painel;
    /// <c>#</c> inicia comentário. Nada gera exceção: problemas viram avisos.
    /// </summary>
    public static class DashboardSpecParser
    {
        private static readonly HashSet<string> s_panelKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "title", "titulo", "título", "layout", "columns", "colunas", "width", "largura", "padding", "spacing"
        };

        public static DashboardSpec Parse(IEnumerable<string> lines, IEnumerable<WidgetSpec> extraWidgets = null)
        {
            var spec = new DashboardSpec();
            var raw = new List<WidgetSpec>();
            int lineNo = 0;
            foreach (var block in lines ?? Enumerable.Empty<string>())
            {
                if (block == null) continue;
                foreach (var rawLine in block.Replace("\r\n", "\n").Split('\n'))
                {
                    lineNo++;
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("//", StringComparison.Ordinal)) continue;

                    if (TryParsePanelSetting(line, spec)) continue;

                    var w = ParseWidgetLine(line, spec.Warnings, lineNo);
                    if (w != null) raw.Add(w);
                }
            }
            if (extraWidgets != null)
            {
                foreach (var w in extraWidgets)
                {
                    if (w != null) raw.Add(w);
                }
            }
            AddWidgets(spec, raw);
            return spec;
        }

        /// <summary>Garante ids únicos (sufixo _2, _3...) e ordem de exibição estável.</summary>
        public static void AddWidgets(DashboardSpec spec, IEnumerable<WidgetSpec> widgets)
        {
            var used = new HashSet<string>(spec.Widgets.Select(w => w.Id), StringComparer.OrdinalIgnoreCase);
            int index = spec.Widgets.Count;
            foreach (var w in widgets)
            {
                var widget = w;
                if (used.Contains(widget.Id))
                {
                    int n = 2;
                    while (used.Contains(widget.Id + "_" + n)) n++;
                    spec.Warnings.Add($"id '{widget.Id}' repetido; usando '{widget.Id}_{n}' (defina 'id=' para manter o estado ao editar).");
                    widget = widget.WithId(widget.Id + "_" + n);
                }
                used.Add(widget.Id);
                if (widget.Order == int.MinValue) widget = widget.WithOrder(index);
                spec.Widgets.Add(widget);
                index++;
            }
        }

        private static bool TryParsePanelSetting(string line, DashboardSpec spec)
        {
            int eq = line.IndexOf('=');
            if (eq <= 0 || line.IndexOf('|') >= 0) return false;
            string key = line.Substring(0, eq).Trim();
            if (!s_panelKeys.Contains(key)) return false;
            string value = Unquote(line.Substring(eq + 1).Trim());
            switch (key.ToLowerInvariant())
            {
                case "title":
                case "titulo":
                case "título":
                    spec.Title = value;
                    break;
                case "layout":
                    if (WidgetSpec.TryParseName(value, out LayoutKind kind)) spec.Layout = kind;
                    else if (string.Equals(value, "vertical", StringComparison.OrdinalIgnoreCase)) spec.Layout = LayoutKind.Stack;
                    else if (string.Equals(value, "horizontal", StringComparison.OrdinalIgnoreCase)) spec.Layout = LayoutKind.Row;
                    else spec.Warnings.Add($"layout '{value}' inválido (stack, row, grid).");
                    break;
                case "columns":
                case "colunas":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int c) && c >= 1) spec.Columns = Math.Min(12, c);
                    else spec.Warnings.Add($"columns '{value}' inválido.");
                    break;
                case "width":
                case "largura":
                    if (DashboardText.TryParseNumber(value, out double w) && w > 0) spec.Width = (float)Math.Max(120, Math.Min(2000, w));
                    else spec.Warnings.Add($"width '{value}' inválido.");
                    break;
                case "padding":
                    if (DashboardText.TryParseNumber(value, out double p) && p >= 0) spec.Padding = (float)Math.Min(40, p);
                    break;
                case "spacing":
                    if (DashboardText.TryParseNumber(value, out double s) && s >= 0) spec.Spacing = (float)Math.Min(40, s);
                    break;
            }
            return true;
        }

        public static WidgetSpec ParseWidgetLine(string line, ICollection<string> warnings, int lineNo = 0)
        {
            var parts = SplitOutsideQuotes(line, '|');
            string head = parts[0].Trim();
            int space = IndexOfWhiteSpace(head);
            string kindText = space < 0 ? head : head.Substring(0, space);
            string label = space < 0 ? "" : Unquote(head.Substring(space + 1).Trim());

            if (!WidgetKinds.TryParse(kindText, out WidgetKind kind))
            {
                warnings?.Add($"linha {lineNo}: tipo '{kindText}' desconhecido (label, number, slider, toggle, button, dropdown, progress, chart).");
                return null;
            }

            var settings = new List<KeyValuePair<string, string>>();
            for (int i = 1; i < parts.Count; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) continue;
                int eq = p.IndexOf('=');
                if (eq < 0) settings.Add(new KeyValuePair<string, string>(p, ""));
                else settings.Add(new KeyValuePair<string, string>(p.Substring(0, eq).Trim(), Unquote(p.Substring(eq + 1).Trim())));
            }
            return WidgetSpec.Create(kind, label, settings, warnings);
        }

        /// <summary>Configurações no formato <c>chave=valor | chave=valor</c> (também aceita uma por linha).</summary>
        public static List<KeyValuePair<string, string>> ParseSettings(string text)
        {
            var list = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            foreach (var block in text.Replace("\r\n", "\n").Split('\n'))
            {
                foreach (var part in SplitOutsideQuotes(block, '|'))
                {
                    string p = part.Trim();
                    if (p.Length == 0) continue;
                    int eq = p.IndexOf('=');
                    if (eq < 0) list.Add(new KeyValuePair<string, string>(p, ""));
                    else list.Add(new KeyValuePair<string, string>(p.Substring(0, eq).Trim(), Unquote(p.Substring(eq + 1).Trim())));
                }
            }
            return list;
        }

        internal static string Quote(string text)
        {
            if (text == null) return "";
            bool needs = text.IndexOf('|') >= 0 || text.IndexOf('"') >= 0 || text.IndexOf('\n') >= 0 ||
                         (text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[text.Length - 1])));
            if (!needs) return text;
            return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
        }

        internal static string Unquote(string text)
        {
            if (text == null || text.Length < 2 || text[0] != '"' || text[text.Length - 1] != '"') return text ?? "";
            var sb = new StringBuilder(text.Length);
            for (int i = 1; i < text.Length - 1; i++)
            {
                char c = text[i];
                if (c == '\\' && i + 1 < text.Length - 1)
                {
                    char n = text[++i];
                    sb.Append(n == 'n' ? '\n' : n);
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static List<string> SplitOutsideQuotes(string text, char separator)
        {
            var parts = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\' && inQuotes && i + 1 < text.Length)
                {
                    sb.Append(c).Append(text[++i]);
                    continue;
                }
                if (c == '"') inQuotes = !inQuotes;
                if (c == separator && !inQuotes)
                {
                    parts.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }
                sb.Append(c);
            }
            parts.Add(sb.ToString());
            return parts;
        }

        private static int IndexOfWhiteSpace(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsWhiteSpace(s[i])) return i;
            }
            return -1;
        }
    }
}

namespace Buraqueira_Tools.Dashboard
{
    /// <summary>
    /// Montagem de widgets a partir de listas paralelas (entrada do Pill Dashboard Builder): lista mais longa para tipo e
    /// configurações, rótulo por item, valores por widget (indicadores exibem; controles usam como padrão).
    /// </summary>
    public static class DashboardBuilder
    {
        public static List<WidgetSpec> Build(IList<string> kinds, IList<string> labels, IList<string> settings, IList<WidgetValue> values, ICollection<string> warnings)
        {
            var result = new List<WidgetSpec>();
            kinds = kinds ?? new List<string>();
            labels = labels ?? new List<string>();
            settings = settings ?? new List<string>();
            int n = kinds.Count == 0 ? 0 : Math.Max(kinds.Count, Math.Max(labels.Count, settings.Count));
            for (int i = 0; i < n; i++)
            {
                string kindText = Longest(kinds, i);
                if (!WidgetKinds.TryParse(kindText, out WidgetKind kind))
                {
                    warnings?.Add($"item {i}: tipo '{kindText}' desconhecido.");
                    continue;
                }
                string label = i < labels.Count ? labels[i] ?? "" : ""; // rótulo não se repete: geraria ids duplicados
                var kv = DashboardSpecParser.ParseSettings(Longest(settings, i));
                var value = values != null && i < values.Count ? values[i] : WidgetValue.None;

                bool control = WidgetKinds.IsControl(kind);
                if (control && !value.IsNone && !kv.Any(p => string.Equals(p.Key, "value", StringComparison.OrdinalIgnoreCase)))
                {
                    kv.Add(new KeyValuePair<string, string>("value", value.ToInvariantString()));
                }
                var spec = WidgetSpec.Create(kind, label, kv, warnings);
                if (!control && !value.IsNone) spec = spec.WithEmbeddedValue(value);
                result.Add(spec);
            }
            return result;
        }

        /// <summary>Ramo i → widget i; um único ramo com exatamente um item por widget também vale (lista simples).</summary>
        public static List<WidgetValue> ValuesPerWidget(IList<IList<WidgetValue>> branches, Func<IList<WidgetValue>, WidgetValue> combine, int widgetCount)
        {
            var result = new List<WidgetValue>();
            if (branches == null || branches.Count == 0) return result;
            if (branches.Count == 1 && widgetCount > 1 && branches[0].Count == widgetCount)
            {
                result.AddRange(branches[0]);
                return result;
            }
            foreach (var b in branches) result.Add(combine(b));
            return result;
        }

        private static string Longest(IList<string> list, int i) => list.Count == 0 ? null : list[Math.Min(i, list.Count - 1)];
    }
}
