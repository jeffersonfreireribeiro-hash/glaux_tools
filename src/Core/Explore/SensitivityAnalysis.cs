using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Buraqueira_Tools.Explore
{
    public enum SensitivityMethod
    {
        /// <summary>Escolhe pelo plano: Morris → Morris; Saltelli → Sobol; demais → correlação.</summary>
        Auto,

        /// <summary>Pearson, Spearman e coeficientes de regressão padronizados (SRC); qualquer amostragem.</summary>
        Correlation,

        /// <summary>Efeitos elementares (μ*, μ, σ); exige plano de Morris.</summary>
        Morris,

        /// <summary>Índices de Sobol de 1ª ordem (S1) e totais (ST); exige plano de Saltelli.</summary>
        Sobol
    }

    /// <summary>Sensibilidade de um resultado a cada variável.</summary>
    public sealed class ResultSensitivity
    {
        public string Name { get; internal set; }

        /// <summary>Medida principal por variável: |SRC| (correlação), μ* (Morris) ou ST (Sobol).</summary>
        public double[] Importance { get; internal set; }

        /// <summary>Todas as medidas por variável (variável × medida, na ordem de <see cref="SensitivityResult.Measures"/>).</summary>
        public double[][] Details { get; internal set; }

        /// <summary>Índices das variáveis da mais para a menos importante (NaN por último).</summary>
        public int[] Ranking { get; internal set; }

        /// <summary>Execuções válidas (correlação) ou trajetórias/linhas completas usadas (Morris/Sobol).</summary>
        public int Used { get; internal set; }

        /// <summary>R² do modelo linear (correlação); soma dos S1 (Sobol).</summary>
        public double? Fit { get; internal set; }

        public List<string> Notes { get; } = new List<string>();
    }

    public sealed class SensitivityResult
    {
        public SensitivityMethod Method { get; internal set; }
        public string[] Variables { get; internal set; }
        public string[] Measures { get; internal set; }
        public string ImportanceMeasure { get; internal set; }
        public List<ResultSensitivity> Results { get; } = new List<ResultSensitivity>();
        public List<string> Notes { get; } = new List<string>();

        /// <summary>Linhas legíveis: "Resultado: 1. Var (medida) …".</summary>
        public List<string> RankingLines()
        {
            var lines = new List<string>();
            foreach (var r in Results)
            {
                var parts = new List<string>();
                int pos = 1;
                foreach (int j in r.Ranking)
                {
                    if (double.IsNaN(r.Importance[j])) continue;
                    parts.Add($"{pos++}. {Variables[j]} ({ImportanceMeasure} {Fmt(r.Importance[j])})");
                }
                lines.Add($"{r.Name}: " + (parts.Count == 0 ? "sem dados suficientes" : string.Join(" · ", parts)));
            }
            return lines;
        }

        internal static string Fmt(double v) => double.IsNaN(v) ? "—" : v.ToString(Math.Abs(v) >= 1000 ? "G4" : "0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Análises de sensibilidade sobre um plano de amostras e os resultados de cada execução.
    /// Linhas de resultado com NaN (execução sem resultado numérico) são descartadas só onde afetam.
    /// </summary>
    public static class SensitivityAnalysis
    {
        public static readonly string[] CorrelationMeasures = { "pearson", "p_value", "spearman", "src" };
        public static readonly string[] MorrisMeasures = { "mu_star", "mu", "sigma" };
        public static readonly string[] SobolMeasures = { "S1", "ST" };

        /// <param name="plan">Plano executado (obrigatório para Morris e Sobol).</param>
        /// <param name="x">Valores numéricos das variáveis por execução (n × k); se null, vêm do plano.</param>
        /// <param name="rows">Índice da amostra do plano de cada linha de <paramref name="y"/> (null = 0, 1, 2 …).</param>
        /// <param name="y">Resultados por execução (n × m; linhas podem ter tamanhos diferentes).</param>
        public static SensitivityResult Analyze(SamplePlan plan, double[][] x, IList<int> rows, double[][] y, SensitivityMethod method, IList<string> resultNames = null, IList<string> variableNames = null)
        {
            if (y == null || y.Length == 0) throw new ArgumentException("Sem resultados para analisar.");
            if (method == SensitivityMethod.Auto)
            {
                method = plan?.Method == SamplingMethod.Morris ? SensitivityMethod.Morris
                    : plan?.Method == SamplingMethod.Saltelli ? SensitivityMethod.Sobol
                    : SensitivityMethod.Correlation;
            }
            if (method == SensitivityMethod.Morris && plan?.Method != SamplingMethod.Morris) throw new ArgumentException("Morris exige um plano gerado com o método 'morris' no Pill Sampler.");
            if (method == SensitivityMethod.Sobol && plan?.Method != SamplingMethod.Saltelli) throw new ArgumentException("Índices de Sobol exigem um plano gerado com o método 'saltelli' no Pill Sampler.");

            int m = y.Max(r => r?.Length ?? 0);
            if (m == 0) throw new ArgumentException("Os resultados não têm valores numéricos.");

            // Linha do plano de cada resultado
            var sampleIndex = new int[y.Length];
            for (int i = 0; i < y.Length; i++) sampleIndex[i] = rows != null && i < rows.Count ? rows[i] : i;

            if (x == null)
            {
                if (plan == null) throw new ArgumentException("Informe o plano (Pill Sampler / Batch Runner) ou os valores das variáveis.");
                x = new double[y.Length][];
                for (int i = 0; i < y.Length; i++)
                {
                    int s = sampleIndex[i];
                    if (s < 0 || s >= plan.Count) throw new ArgumentException($"Resultado da amostra {s}, mas o plano tem {plan.Count} amostras.");
                    var row = new double[plan.Space.Count];
                    for (int j = 0; j < row.Length; j++) row[j] = plan.Space.Variables[j].NumericValue(plan.Values[s][j]);
                    x[i] = row;
                }
            }
            if (x.Length != y.Length) throw new ArgumentException($"{x.Length} linhas de variáveis para {y.Length} linhas de resultados.");
            int k = x.Length == 0 ? 0 : x.Max(r => r?.Length ?? 0);
            if (k == 0) throw new ArgumentException("Sem variáveis para analisar.");

            var result = new SensitivityResult
            {
                Method = method,
                Variables = Enumerable.Range(0, k).Select(j => variableNames != null && j < variableNames.Count ? variableNames[j]
                    : plan != null && j < plan.Space.Count ? plan.Space.Variables[j].Name : $"x{j}").ToArray()
            };

            for (int c = 0; c < m; c++)
            {
                var yc = new double[y.Length];
                for (int i = 0; i < y.Length; i++) yc[i] = y[i] != null && c < y[i].Length ? y[i][c] : double.NaN;
                ResultSensitivity rs;
                switch (method)
                {
                    case SensitivityMethod.Morris:
                        rs = Morris(plan, sampleIndex, yc);
                        break;
                    case SensitivityMethod.Sobol:
                        rs = Sobol(plan, sampleIndex, yc);
                        break;
                    default:
                        rs = Correlation(x, yc, k);
                        break;
                }
                rs.Name = resultNames != null && c < resultNames.Count && !string.IsNullOrWhiteSpace(resultNames[c]) ? resultNames[c] : $"R{c}";
                rs.Ranking = Rank(rs.Importance);
                result.Results.Add(rs);
            }

            switch (method)
            {
                case SensitivityMethod.Morris:
                    result.Measures = MorrisMeasures;
                    result.ImportanceMeasure = "μ*";
                    break;
                case SensitivityMethod.Sobol:
                    result.Measures = SobolMeasures;
                    result.ImportanceMeasure = "ST";
                    break;
                default:
                    result.Measures = CorrelationMeasures;
                    result.ImportanceMeasure = "|SRC|";
                    break;
            }
            return result;
        }

        // ---------------------------------------------------------------- correlação

        private static ResultSensitivity Correlation(double[][] x, double[] y, int k)
        {
            var valid = new List<int>();
            for (int i = 0; i < y.Length; i++)
            {
                if (IsFinite(y[i]) && x[i] != null && x[i].Length >= k && x[i].Take(k).All(IsFinite)) valid.Add(i);
            }
            int n = valid.Count;
            var rs = new ResultSensitivity { Used = n, Importance = new double[k], Details = new double[k][] };
            var ys = valid.Select(i => y[i]).ToArray();
            var cols = new double[k][];
            for (int j = 0; j < k; j++) cols[j] = valid.Select(i => x[i][j]).ToArray();

            double r2 = double.NaN;
            var src = n > k + 1 ? StandardizedRegression(cols, ys, out r2) : null;
            double fit = double.NaN;
            if (src != null)
            {
                fit = r2;
                rs.Fit = r2;
            }
            var ranksY = Ranks(ys);
            for (int j = 0; j < k; j++)
            {
                double pearson = Pearson(cols[j], ys);
                double spearman = Pearson(Ranks(cols[j]), ranksY);
                double p = PValue(pearson, n);
                double s = src != null ? src[j] : double.NaN;
                rs.Details[j] = new[] { pearson, p, spearman, s };
                rs.Importance[j] = src != null ? Math.Abs(s) : Math.Abs(spearman);
            }

            if (n < 3) rs.Notes.Add($"Só {n} execução(ões) válida(s): correlação indefinida.");
            else if (src == null) rs.Notes.Add($"Execuções ({n}) insuficientes para a regressão com {k} variáveis (precisa de mais de {k + 1}); importância = |Spearman|.");
            else if (fit < 0.7) rs.Notes.Add($"R² = {SensitivityResult.Fmt(fit)}: o resultado não é bem explicado por um modelo linear (interações ou não linearidade). Use Morris ou Sobol para uma leitura confiável.");
            return rs;
        }

        /// <summary>Coeficientes da regressão linear de y sobre as colunas padronizadas (SRC) e o R².</summary>
        internal static double[] StandardizedRegression(double[][] cols, double[] y, out double r2)
        {
            int k = cols.Length, n = y.Length;
            r2 = double.NaN;
            double my = y.Average();
            double sy = Math.Sqrt(y.Sum(v => (v - my) * (v - my)) / (n - 1));
            var src = Enumerable.Repeat(double.NaN, k).ToArray();
            if (!(sy > 0))
            {
                r2 = double.NaN;
                return src;
            }
            // Colunas constantes ficam de fora (SRC indefinido)
            var active = new List<int>();
            var z = new double[k][];
            for (int j = 0; j < k; j++)
            {
                double mx = cols[j].Average();
                double sx = Math.Sqrt(cols[j].Sum(v => (v - mx) * (v - mx)) / (n - 1));
                if (!(sx > 0)) continue;
                z[j] = cols[j].Select(v => (v - mx) / sx).ToArray();
                active.Add(j);
            }
            var zy = y.Select(v => (v - my) / sy).ToArray();
            int a = active.Count;
            if (a == 0)
            {
                r2 = 0;
                return src;
            }
            var ata = new double[a, a];
            var aty = new double[a];
            for (int p = 0; p < a; p++)
            {
                var zp = z[active[p]];
                for (int q = p; q < a; q++)
                {
                    var zq = z[active[q]];
                    double s = 0;
                    for (int i = 0; i < n; i++) s += zp[i] * zq[i];
                    ata[p, q] = ata[q, p] = s;
                }
                double t = 0;
                for (int i = 0; i < n; i++) t += zp[i] * zy[i];
                aty[p] = t;
            }
            var beta = Solve(ata, aty);
            if (beta == null) return src;
            double ssRes = 0, ssTot = 0;
            for (int i = 0; i < n; i++)
            {
                double pred = 0;
                for (int p = 0; p < a; p++) pred += beta[p] * z[active[p]][i];
                ssRes += (zy[i] - pred) * (zy[i] - pred);
                ssTot += zy[i] * zy[i];
            }
            r2 = ssTot > 0 ? 1 - ssRes / ssTot : double.NaN;
            for (int p = 0; p < a; p++) src[active[p]] = beta[p];
            return src;
        }

        /// <summary>Eliminação de Gauss com pivoteamento parcial; null se singular (variáveis colineares).</summary>
        internal static double[] Solve(double[,] a, double[] b)
        {
            int n = b.Length;
            var m = (double[,])a.Clone();
            var v = (double[])b.Clone();
            double scale = 0;
            for (int i = 0; i < n; i++) scale = Math.Max(scale, Math.Abs(m[i, i]));
            double tol = 1e-10 * Math.Max(1, scale);
            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < n; r++)
                {
                    if (Math.Abs(m[r, col]) > Math.Abs(m[pivot, col])) pivot = r;
                }
                if (Math.Abs(m[pivot, col]) < tol) return null;
                if (pivot != col)
                {
                    for (int c = 0; c < n; c++) (m[col, c], m[pivot, c]) = (m[pivot, c], m[col, c]);
                    (v[col], v[pivot]) = (v[pivot], v[col]);
                }
                for (int r = col + 1; r < n; r++)
                {
                    double f = m[r, col] / m[col, col];
                    if (f == 0) continue;
                    for (int c = col; c < n; c++) m[r, c] -= f * m[col, c];
                    v[r] -= f * v[col];
                }
            }
            var x = new double[n];
            for (int r = n - 1; r >= 0; r--)
            {
                double s = v[r];
                for (int c = r + 1; c < n; c++) s -= m[r, c] * x[c];
                x[r] = s / m[r, r];
            }
            return x;
        }

        internal static double Pearson(double[] a, double[] b)
        {
            int n = Math.Min(a.Length, b.Length);
            if (n < 2) return double.NaN;
            double ma = 0, mb = 0;
            for (int i = 0; i < n; i++)
            {
                ma += a[i];
                mb += b[i];
            }
            ma /= n;
            mb /= n;
            double sab = 0, saa = 0, sbb = 0;
            for (int i = 0; i < n; i++)
            {
                double da = a[i] - ma, db = b[i] - mb;
                sab += da * db;
                saa += da * da;
                sbb += db * db;
            }
            return saa > 0 && sbb > 0 ? Math.Max(-1, Math.Min(1, sab / Math.Sqrt(saa * sbb))) : double.NaN;
        }

        /// <summary>Postos com média nos empates (base do Spearman).</summary>
        internal static double[] Ranks(double[] values)
        {
            int n = values.Length;
            var idx = Enumerable.Range(0, n).ToArray();
            Array.Sort(idx, (p, q) => values[p].CompareTo(values[q]));
            var ranks = new double[n];
            int i = 0;
            while (i < n)
            {
                int j = i;
                while (j + 1 < n && values[idx[j + 1]] == values[idx[i]]) j++;
                double r = (i + j) / 2.0 + 1;
                for (int t = i; t <= j; t++) ranks[idx[t]] = r;
                i = j + 1;
            }
            return ranks;
        }

        /// <summary>P-valor bilateral do teste t da correlação de Pearson (H0: r = 0).</summary>
        internal static double PValue(double r, int n)
        {
            if (double.IsNaN(r) || n < 3) return double.NaN;
            if (Math.Abs(r) >= 1) return 0;
            double t = r * Math.Sqrt((n - 2) / (1 - r * r));
            return Math.Max(0, Math.Min(1, 2 * (1 - SpecialFunctions.StudentTCdf(Math.Abs(t), n - 2))));
        }

        // ---------------------------------------------------------------- Morris

        /// <summary>
        /// Efeitos elementares por trajetória: EE = Δy / Δu (u = coordenada unitária da variável que mudou).
        /// μ* = média de |EE| (importância), μ = média de EE (sinal), σ = desvio de EE (não linearidade/interação).
        /// Trajetórias incompletas (execução faltando ou NaN) contribuem só com os passos completos.
        /// </summary>
        private static ResultSensitivity Morris(SamplePlan plan, int[] sampleIndex, double[] y)
        {
            int k = plan.Space.Count, len = plan.TrajectoryLength;
            var bySample = new Dictionary<int, double>();
            for (int i = 0; i < y.Length; i++) bySample[sampleIndex[i]] = y[i];

            var effects = new List<double>[k];
            for (int j = 0; j < k; j++) effects[j] = new List<double>();
            int trajectories = plan.Count / len, complete = 0;
            for (int t = 0; t < trajectories; t++)
            {
                bool all = true;
                for (int s = 0; s < len - 1; s++)
                {
                    int a = t * len + s, b = a + 1;
                    if (!bySample.TryGetValue(a, out double ya) || !bySample.TryGetValue(b, out double yb) || !IsFinite(ya) || !IsFinite(yb))
                    {
                        all = false;
                        continue;
                    }
                    int changed = -1;
                    double du = 0;
                    for (int j = 0; j < k; j++)
                    {
                        double d = plan.Unit[b][j] - plan.Unit[a][j];
                        if (Math.Abs(d) > 1e-12)
                        {
                            changed = j;
                            du = d;
                        }
                    }
                    if (changed >= 0) effects[changed].Add((yb - ya) / du);
                }
                if (all) complete++;
            }

            var rs = new ResultSensitivity { Used = complete, Importance = new double[k], Details = new double[k][] };
            for (int j = 0; j < k; j++)
            {
                var e = effects[j];
                if (e.Count == 0)
                {
                    rs.Details[j] = new[] { double.NaN, double.NaN, double.NaN };
                    rs.Importance[j] = double.NaN;
                    continue;
                }
                double mu = e.Average();
                double muStar = e.Average(Math.Abs);
                double sigma = e.Count > 1 ? Math.Sqrt(e.Sum(v => (v - mu) * (v - mu)) / (e.Count - 1)) : double.NaN;
                rs.Details[j] = new[] { muStar, mu, sigma };
                rs.Importance[j] = muStar;
            }
            if (complete < trajectories) rs.Notes.Add($"{complete} de {trajectories} trajetórias completas (as incompletas contribuem só com os passos avaliados).");
            if (complete < 4) rs.Notes.Add("Poucas trajetórias: o ranking de Morris costuma estabilizar a partir de 10–20.");
            return rs;
        }

        // ---------------------------------------------------------------- Sobol

        /// <summary>
        /// Índices de Sobol a partir do esquema de Saltelli (linhas A, AB₁ … ABₖ, B):
        /// S1ᵢ = média(f_B · (f_ABᵢ − f_A)) / V (Saltelli 2010) e STᵢ = média((f_A − f_ABᵢ)²) / (2V) (Jansen 1999),
        /// com V = variância de [f_A, f_B]. Usa só linhas completas.
        /// </summary>
        private static ResultSensitivity Sobol(SamplePlan plan, int[] sampleIndex, double[] y)
        {
            int k = plan.Space.Count, block = k + 2, rowsTotal = plan.BaseCount;
            var bySample = new Dictionary<int, double>();
            for (int i = 0; i < y.Length; i++) bySample[sampleIndex[i]] = y[i];

            var fa = new List<double>();
            var fb = new List<double>();
            var fab = new List<double[]>();
            for (int r = 0; r < rowsTotal; r++)
            {
                int start = r * block;
                var vals = new double[block];
                bool ok = true;
                for (int s = 0; s < block && ok; s++)
                {
                    ok = bySample.TryGetValue(start + s, out vals[s]) && IsFinite(vals[s]);
                }
                if (!ok) continue;
                fa.Add(vals[0]);
                fab.Add(vals.Skip(1).Take(k).ToArray());
                fb.Add(vals[block - 1]);
            }

            int n = fa.Count;
            var rs = new ResultSensitivity { Used = n, Importance = new double[k], Details = new double[k][] };
            var both = fa.Concat(fb).ToArray();
            double mean = both.Length > 0 ? both.Average() : 0;
            double variance = both.Length > 0 ? both.Sum(v => (v - mean) * (v - mean)) / both.Length : 0;
            if (n < 2 || !(variance > 0))
            {
                for (int j = 0; j < k; j++)
                {
                    rs.Details[j] = new[] { double.NaN, double.NaN };
                    rs.Importance[j] = double.NaN;
                }
                rs.Notes.Add(n < 2 ? $"Só {n} linha(s) completa(s) de {rowsTotal}: índices indefinidos." : "O resultado não varia: índices indefinidos.");
                return rs;
            }

            double sumS1 = 0;
            for (int j = 0; j < k; j++)
            {
                double s1 = 0, st = 0;
                for (int r = 0; r < n; r++)
                {
                    s1 += fb[r] * (fab[r][j] - fa[r]);
                    double d = fa[r] - fab[r][j];
                    st += d * d;
                }
                s1 = s1 / n / variance;
                st = st / (2.0 * n) / variance;
                rs.Details[j] = new[] { s1, st };
                rs.Importance[j] = st;
                sumS1 += s1;
            }
            rs.Fit = sumS1;
            if (n < rowsTotal) rs.Notes.Add($"{n} de {rowsTotal} linhas completas.");
            if (n < 256) rs.Notes.Add($"N = {n}: estimativas de Sobol oscilam com N pequeno (erro típico > 0,05); prefira N ≥ 512.");
            if (sumS1 < 0.8) rs.Notes.Add($"Soma dos S1 = {SensitivityResult.Fmt(sumS1)}: parte da variância vem de interações entre variáveis (compare S1 com ST).");
            return rs;
        }

        // ---------------------------------------------------------------- comum

        private static int[] Rank(double[] importance)
        {
            var idx = Enumerable.Range(0, importance.Length).ToArray();
            Array.Sort(idx, (a, b) =>
            {
                double va = importance[a], vb = importance[b];
                bool na = double.IsNaN(va), nb = double.IsNaN(vb);
                if (na && nb) return a.CompareTo(b);
                if (na) return 1;
                if (nb) return -1;
                int c = vb.CompareTo(va);
                return c != 0 ? c : a.CompareTo(b);
            });
            return idx;
        }

        private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
