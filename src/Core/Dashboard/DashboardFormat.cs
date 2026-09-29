using System;
using System.Collections.Generic;
using System.Globalization;

namespace Buraqueira_Tools.Dashboard
{
    /// <summary>
    /// Formatação de números e textos para espaços pequenos: números grandes, negativos, NaN/∞ e textos longos.
    /// A medição de texto é injetada (<c>measure</c>), então as regras são testadas sem GDI+.
    /// </summary>
    public static class DashboardFormat
    {
        public const string Missing = "—";
        public const string Ellipsis = "…";

        // Espaço fino como separador de milhar: sem ambiguidade entre "1,234" (en) e "1,234" (pt)
        private const char ThinSpace = ' ';

        /// <summary>Número com casas fixas (decimals ≥ 0) ou automáticas (decimals &lt; 0), agrupando milhares e com unidade.</summary>
        public static string Number(double value, int decimals, string unit = null)
        {
            string core;
            if (double.IsNaN(value)) core = Missing;
            else if (double.IsPositiveInfinity(value)) core = "∞";
            else if (double.IsNegativeInfinity(value)) core = "-∞";
            else
            {
                double v = value;
                double abs = Math.Abs(v);
                if (decimals < 0)
                {
                    if (abs != 0 && (abs >= 1e15 || abs < 1e-4)) return WithUnit(Scientific(v, 3), unit);
                    decimals = abs >= 1000 ? 1 : abs >= 1 ? 3 : 4;
                    core = Grouped(v, decimals, trimZeros: true);
                }
                else
                {
                    if (abs >= 1e15) return WithUnit(Scientific(v, 3), unit);
                    core = Grouped(v, Math.Min(12, decimals), trimZeros: false);
                }
            }
            return WithUnit(core, unit);
        }

        /// <summary>Forma compacta com prefixo SI (1.23 k, 4.5 M, -2.1 G), 3 algarismos significativos.</summary>
        public static string Compact(double value, int significant = 3)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return Number(value, 0);
            double abs = Math.Abs(value);
            if (abs >= 999.5e12) return Scientific(value, significant);
            string[] suffixes = { "", " k", " M", " G", " T" };
            int tier = 0;
            double scaled = value;
            while (Math.Abs(scaled) >= 999.5 && tier < suffixes.Length - 1)
            {
                scaled /= 1000.0;
                tier++;
            }
            if (abs != 0 && abs < 1e-3) return Scientific(value, significant);
            int intDigits = Math.Abs(scaled) < 1 ? 1 : (int)Math.Floor(Math.Log10(Math.Abs(scaled))) + 1;
            int dec = Math.Max(0, significant - intDigits);
            double rounded = Math.Round(scaled, dec, MidpointRounding.AwayFromZero);
            if (rounded == 0) rounded = 0;
            string s = rounded.ToString("F" + dec, CultureInfo.InvariantCulture);
            if (s.IndexOf('.') >= 0) s = s.TrimEnd('0').TrimEnd('.');
            return s + suffixes[tier];
        }

        public static string Scientific(double value, int significant = 3)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return Number(value, 0);
            if (value == 0) return "0";
            string pattern = "0." + new string('#', Math.Max(0, significant - 1)) + "e0";
            return value.ToString(pattern, CultureInfo.InvariantCulture);
        }

        public static string Percent(double ratio, int decimals = 0)
        {
            if (double.IsNaN(ratio)) return Missing;
            return Number(ratio * 100.0, decimals) + "%";
        }

        /// <summary>Candidatos do mais completo ao mais curto, para caber em larguras cada vez menores.</summary>
        public static List<string> NumberCandidates(double value, int decimals, string unit)
        {
            var list = new List<string>();
            void Add(string s)
            {
                if (!string.IsNullOrEmpty(s) && !list.Contains(s)) list.Add(s);
            }
            Add(Number(value, decimals, unit));
            if (decimals > 1 || decimals < 0) Add(Number(value, 1, unit));
            if (decimals != 0) Add(Number(value, 0, unit));
            Add(WithUnit(Compact(value, 3), unit));
            Add(WithUnit(Compact(value, 2), unit));
            Add(WithUnit(Scientific(value, 2), unit));
            Add(Compact(value, 2));
            return list;
        }

        /// <summary>O candidato mais completo que cabe na largura; se nenhum couber, o mais curto com reticências.</summary>
        public static string FitNumber(double value, int decimals, string unit, float maxWidth, Func<string, float> measure)
        {
            var candidates = NumberCandidates(value, decimals, unit);
            foreach (var c in candidates)
            {
                if (measure(c) <= maxWidth) return c;
            }
            return Ellipsize(candidates[candidates.Count - 1], maxWidth, measure);
        }

        /// <summary>Corta o texto com "…" para caber na largura (busca binária; nunca devolve texto mais largo que o limite, exceto "…").</summary>
        public static string Ellipsize(string text, float maxWidth, Func<string, float> measure)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (measure(text) <= maxWidth) return text;
            if (measure(Ellipsis) > maxWidth) return "";
            int lo = 0, hi = text.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (measure(text.Substring(0, mid).TrimEnd() + Ellipsis) <= maxWidth) lo = mid;
                else hi = mid - 1;
            }
            return text.Substring(0, lo).TrimEnd() + Ellipsis;
        }

        private static string Grouped(double v, int decimals, bool trimZeros)
        {
            double rounded = Math.Round(v, decimals, MidpointRounding.AwayFromZero);
            if (rounded == 0) rounded = 0; // "-0.00" → "0.00"
            string s = rounded.ToString("N" + decimals, CultureInfo.InvariantCulture);
            if (trimZeros && s.IndexOf('.') >= 0) s = s.TrimEnd('0').TrimEnd('.');
            if (Math.Abs(rounded) < 10000) s = s.Replace(",", "");
            else s = s.Replace(',', ThinSpace);
            return s;
        }

        private static string WithUnit(string core, string unit)
        {
            if (string.IsNullOrWhiteSpace(unit) || core == Missing) return core;
            string u = unit.Trim();
            bool glued = u.StartsWith("%", StringComparison.Ordinal) || u.StartsWith("°", StringComparison.Ordinal) || u.StartsWith("'", StringComparison.Ordinal);
            return glued ? core + u : core + " " + u;
        }
    }

    /// <summary>Redução de séries longas para desenho (Mini Chart) sem perder picos.</summary>
    public static class SeriesSampling
    {
        /// <summary>
        /// Divide a série em <paramref name="buckets"/> faixas e guarda o mínimo e o máximo de cada uma, na ordem original.
        /// Resultado com no máximo 2 × buckets pontos (índice original, valor); NaN e ±∞ são ignorados.
        /// </summary>
        public static List<KeyValuePair<int, double>> MinMaxDecimate(IReadOnlyList<double> values, int buckets)
        {
            var result = new List<KeyValuePair<int, double>>();
            if (values == null || values.Count == 0) return result;
            buckets = Math.Max(1, buckets);
            int n = values.Count;
            if (n <= buckets * 2)
            {
                for (int i = 0; i < n; i++)
                {
                    if (IsFinite(values[i])) result.Add(new KeyValuePair<int, double>(i, values[i]));
                }
                return result;
            }

            for (int b = 0; b < buckets; b++)
            {
                int start = (int)((long)b * n / buckets);
                int end = (int)((long)(b + 1) * n / buckets);
                int iMin = -1, iMax = -1;
                for (int i = start; i < end; i++)
                {
                    double v = values[i];
                    if (!IsFinite(v)) continue;
                    if (iMin < 0 || v < values[iMin]) iMin = i;
                    if (iMax < 0 || v > values[iMax]) iMax = i;
                }
                if (iMin < 0) continue;
                if (iMin == iMax)
                {
                    result.Add(new KeyValuePair<int, double>(iMin, values[iMin]));
                }
                else if (iMin < iMax)
                {
                    result.Add(new KeyValuePair<int, double>(iMin, values[iMin]));
                    result.Add(new KeyValuePair<int, double>(iMax, values[iMax]));
                }
                else
                {
                    result.Add(new KeyValuePair<int, double>(iMax, values[iMax]));
                    result.Add(new KeyValuePair<int, double>(iMin, values[iMin]));
                }
            }
            return result;
        }

        /// <summary>Mínimo e máximo finitos; (NaN, NaN) se não houver valores finitos.</summary>
        public static void Range(IReadOnlyList<double> values, out double min, out double max)
        {
            min = double.NaN;
            max = double.NaN;
            if (values == null) return;
            foreach (var v in values)
            {
                if (!IsFinite(v)) continue;
                if (double.IsNaN(min) || v < min) min = v;
                if (double.IsNaN(max) || v > max) max = v;
            }
        }

        public static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
