using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace Buraqueira_Tools
{
    // =====================================================================================
    //  Modelo de dados do Line Chart & Statistics / Histogram (sem dependência do Grasshopper).
    //
    //  Fluxo:  entrada → pares X/Y validados (ChartSeries) → estatísticas (valores ORIGINAIS)
    //          → ChartScene (geometria em coordenadas de DADOS) → Canvas / PNG / Rhino.
    //  Canvas e exportação usam a MESMA ChartScene e o MESMO ChartMapper: nada é recalculado por superfície.
    // =====================================================================================

    /// <summary>Tipo de gráfico. Lines = séries X/Y; Distribution = histograma estatístico (observações em Y); XYBars = barras com pares X/Y explícitos.</summary>
    public enum ChartLineKind { Lines = 0, Distribution = 1, XYBars = 2 }

    /// <summary>Como a linha principal é obtida. Raw = pontos reais; Smooth = PCHIP (monotônica, sem overshoot); Trend = tendência estatística (média móvel ponderada) sobreposta aos dados.</summary>
    public enum ChartLineMode { Raw = 0, Smooth = 1, Trend = 2 }

    /// <summary>Tratamento de X repetido (malhas espaciais: vários pontos na mesma coluna X com valores diferentes). Raw preserva as amostras; os demais agrupam por X (com tolerância) e agregam.</summary>
    public enum ChartRepeatMode { Raw = 0, Mean = 1, Median = 2, Min = 3, Max = 4, Range = 5, StdDev = 6 }

    public struct ChartPt
    {
        public double X, Y;
        public ChartPt(double x, double y) { X = x; Y = y; }
    }

    /// <summary>Uma série: pares (X[i], Y[i]) ordenados CONJUNTAMENTE por X (ordenação estável).</summary>
    public sealed class ChartSeries
    {
        public string Key;                       // caminho do ramo (texto)
        public string Name;
        public int Index;
        public List<double> X = new List<double>();
        public List<double> Y = new List<double>();
        public List<int> SourceIndex = new List<int>();   // posição original de cada par na entrada
        public bool XExplicit;                   // false = X é o índice 0,1,2... (X não foi conectado)
        public bool InputWasUnsorted;            // a entrada tinha X fora de ordem
        public int DuplicateX;                   // pares cujo X repete o anterior (após ordenar)
        public int Dropped;                      // pares descartados (X ou Y não numérico / não finito)

        public double Mean, Median, Mode, StdDev, Variance, MinY, MaxY, MinX, MaxX;

        public int Count => Y.Count;

        /// <summary>Grupos de X (derivados; as amostras originais permanecem intactas em X/Y/SourceIndex).</summary>
        public List<ChartGroup> Groups;
        public double GroupTolerance;
        public bool GroupToleranceAuto;
        public bool HasRepeats => Groups != null && Groups.Any(g => g.Count > 1);
        public int RepeatedSamples => Groups == null ? 0 : Groups.Where(g => g.Count > 1).Sum(g => g.Count);

        /// <summary>Agrupa por X com tolerância (tol &lt;= 0 = automática). Reaproveita o resultado quando a tolerância pedida não mudou.</summary>
        public List<ChartGroup> EnsureGroups(double tol)
        {
            if (Groups != null && (tol <= 0 ? GroupToleranceAuto : (!GroupToleranceAuto && GroupTolerance == tol))) return Groups;
            Groups = ChartGrouping.Build(this, tol);
            return Groups;
        }

        public void ComputeStatistics()
        {
            if (Y.Count == 0) return;
            Mean = ChartStats.Mean(Y);
            MinY = Y.Min(); MaxY = Y.Max();
            MinX = X.Min(); MaxX = X.Max();
            Variance = ChartStats.Variance(Y, Mean);
            StdDev = Math.Sqrt(Variance);
            Median = ChartStats.Median(Y);
            Mode = ChartStats.Mode(Y);
        }
    }

    public static class ChartStats
    {
        public static double Mean(IList<double> v) => v == null || v.Count == 0 ? 0.0 : v.Sum() / v.Count;

        /// <summary>Variância amostral (n−1); 0 para n &lt; 2.</summary>
        public static double Variance(IList<double> v, double mean)
        {
            if (v == null || v.Count < 2) return 0.0;
            double ss = 0; for (int i = 0; i < v.Count; i++) { double d = v[i] - mean; ss += d * d; }
            return ss / (v.Count - 1);
        }

        public static double StdDev(IList<double> v) => Math.Sqrt(Variance(v, Mean(v)));

        public static double Median(IList<double> vals)
        {
            if (vals == null || vals.Count == 0) return 0.0;
            var sorted = vals.OrderBy(v => v).ToList();
            int n = sorted.Count;
            return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) * 0.5;
        }

        /// <summary>Moda aproximada de dados contínuos: média do bin mais povoado (√n bins). Informativa; não é usada para desenhar a linha de dados.</summary>
        public static double Mode(IList<double> vals)
        {
            if (vals == null || vals.Count == 0) return 0.0;
            if (vals.Count == 1) return vals[0];
            double min = vals.Min(), max = vals.Max(), span = max - min;
            if (span <= 1e-6) return Mean(vals);
            int bins = Math.Max(5, (int)Math.Sqrt(vals.Count));
            double binW = span / bins;
            int[] counts = new int[bins]; double[] sums = new double[bins];
            foreach (var v in vals)
            {
                int b = (int)((v - min) / binW); if (b >= bins) b = bins - 1;
                counts[b]++; sums[b] += v;
            }
            int maxB = 0;
            for (int i = 1; i < bins; i++) if (counts[i] > counts[maxB]) maxB = i;
            return counts[maxB] > 0 ? sums[maxB] / counts[maxB] : min + (maxB + 0.5) * binW;
        }
    }

    public static class ChartSeriesBuilder
    {
        private static bool Ok(double? v) => v.HasValue && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value);

        /// <summary>
        /// Pareia X[i] ↔ Y[i] (xs == null: X = índice), descarta o PAR inteiro quando X ou Y é inválido e ordena os pares
        /// conjuntamente por X (estável). Os tamanhos de xs e ys devem ser iguais (verificado por quem chama).
        /// </summary>
        public static ChartSeries Pair(string key, string name, int index, IList<double?> xs, IList<double?> ys) => Pair(key, name, index, xs, ys, null);

        /// <summary>sourceIndex (opcional): posição ORIGINAL de cada par na entrada (quando a lista já foi filtrada, p.ex. perfil espacial).</summary>
        public static ChartSeries Pair(string key, string name, int index, IList<double?> xs, IList<double?> ys, IList<int> sourceIndex)
        {
            var s = new ChartSeries { Key = key, Name = name, Index = index, XExplicit = xs != null };
            var pts = new List<KeyValuePair<int, ChartPt>>();
            for (int i = 0; i < ys.Count; i++)
            {
                double? y = ys[i];
                double? x = xs == null ? (double?)i : xs[i];
                if (!Ok(y) || !Ok(x)) { s.Dropped++; continue; }
                pts.Add(new KeyValuePair<int, ChartPt>(sourceIndex != null ? sourceIndex[i] : i, new ChartPt(x.Value, y.Value)));
            }
            for (int k = 1; k < pts.Count; k++)
                if (pts[k].Value.X < pts[k - 1].Value.X) { s.InputWasUnsorted = true; break; }
            var sorted = pts.OrderBy(p => p.Value.X).ToList();   // LINQ OrderBy é estável
            for (int k = 0; k < sorted.Count; k++)
            {
                s.X.Add(sorted[k].Value.X); s.Y.Add(sorted[k].Value.Y); s.SourceIndex.Add(sorted[k].Key);
                if (k > 0 && sorted[k].Value.X == sorted[k - 1].Value.X) s.DuplicateX++;
            }
            s.ComputeStatistics();
            return s;
        }
    }

    /// <summary>Curvas derivadas dos dados. Nenhuma cria pontos fora do domínio nem valores fora do intervalo dos dados.</summary>
    public static class ChartCurves
    {
        /// <summary>Colapsa X iguais (entrada ORDENADA) na média dos Y — necessário para PCHIP/tendência, que exigem X estritamente crescente.</summary>
        public static void Collapse(IList<double> x, IList<double> y, out double[] ux, out double[] uy)
        {
            var lx = new List<double>(); var ly = new List<double>();
            int i = 0;
            while (i < x.Count)
            {
                int j = i; double sum = 0;
                while (j < x.Count && x[j] == x[i]) { sum += y[j]; j++; }
                lx.Add(x[i]); ly.Add(sum / (j - i));
                i = j;
            }
            ux = lx.ToArray(); uy = ly.ToArray();
        }

        /// <summary>Derivadas de Fritsch–Carlson (PCHIP): derivada 0 em extremos locais e média harmônica nos demais pontos ⇒ monotônica por intervalo, sem overshoot.</summary>
        public static double[] PchipSlopes(double[] x, double[] y)
        {
            int n = x.Length;
            var d = new double[n];
            if (n < 2) return d;
            var h = new double[n - 1]; var del = new double[n - 1];
            for (int k = 0; k < n - 1; k++) { h[k] = x[k + 1] - x[k]; del[k] = (y[k + 1] - y[k]) / h[k]; }
            if (n == 2) { d[0] = d[1] = del[0]; return d; }
            for (int k = 1; k < n - 1; k++)
            {
                if (del[k - 1] * del[k] <= 0) d[k] = 0;
                else
                {
                    double w1 = 2 * h[k] + h[k - 1], w2 = h[k] + 2 * h[k - 1];
                    d[k] = (w1 + w2) / (w1 / del[k - 1] + w2 / del[k]);
                }
            }
            d[0] = EndSlope(h[0], h[1], del[0], del[1]);
            d[n - 1] = EndSlope(h[n - 2], h[n - 3], del[n - 2], del[n - 3]);
            return d;
        }

        private static double EndSlope(double h0, double h1, double del0, double del1)
        {
            double d = ((2 * h0 + h1) * del0 - h0 * del1) / (h0 + h1);
            if (Math.Sign(d) != Math.Sign(del0)) return 0;
            if (Math.Sign(del0) != Math.Sign(del1) && Math.Abs(d) > 3 * Math.Abs(del0)) return 3 * del0;
            return d;
        }

        /// <summary>Amostra a spline PCHIP (perSegment passos por intervalo) — passa exatamente pelos pontos originais.</summary>
        public static ChartPt[] PchipSample(double[] x, double[] y, int perSegment)
        {
            int n = x.Length;
            if (n < 2) return n == 1 ? new[] { new ChartPt(x[0], y[0]) } : new ChartPt[0];
            var d = PchipSlopes(x, y);
            var res = new List<ChartPt>();
            for (int k = 0; k < n - 1; k++)
            {
                double h = x[k + 1] - x[k];
                for (int s = 0; s < perSegment; s++)
                {
                    double t = s / (double)perSegment, t2 = t * t, t3 = t2 * t;
                    double h00 = 2 * t3 - 3 * t2 + 1, h10 = t3 - 2 * t2 + t, h01 = -2 * t3 + 3 * t2, h11 = t3 - t2;
                    res.Add(new ChartPt(x[k] + t * h, h00 * y[k] + h10 * h * d[k] + h01 * y[k + 1] + h11 * h * d[k + 1]));
                }
            }
            res.Add(new ChartPt(x[n - 1], y[n - 1]));
            return res.ToArray();
        }

        /// <summary>Segmentos de Bézier cúbica EXATOS da PCHIP (4 pontos de controle por intervalo), para a curva do Rhino.</summary>
        public static ChartPt[][] PchipBezier(double[] x, double[] y)
        {
            int n = x.Length;
            if (n < 2) return new ChartPt[0][];
            var d = PchipSlopes(x, y);
            var segs = new ChartPt[n - 1][];
            for (int k = 0; k < n - 1; k++)
            {
                double h = x[k + 1] - x[k];
                segs[k] = new[]
                {
                    new ChartPt(x[k], y[k]),
                    new ChartPt(x[k] + h / 3.0, y[k] + h * d[k] / 3.0),
                    new ChartPt(x[k + 1] - h / 3.0, y[k + 1] - h * d[k + 1] / 3.0),
                    new ChartPt(x[k + 1], y[k + 1])
                };
            }
            return segs;
        }

        /// <summary>
        /// Tendência: média móvel ponderada (kernel tricúbico) sobre a vizinhança dos k pontos mais próximos EM X (k = span × n, mínimo 5).
        /// Cada valor é uma combinação convexa dos Y vizinhos ⇒ nunca ultrapassa o intervalo dos dados (sem overshoot).
        /// Avaliada somente nos X originais (sem pontos artificiais). X deve ser estritamente crescente.
        /// </summary>
        public static ChartPt[] MovingTrend(double[] x, double[] y, double span, out int window)
        {
            int n = x.Length;
            window = Math.Min(n, Math.Min(2000, Math.Max(5, (int)Math.Ceiling(span * n))));   // limite 2000 pontos de janela (desempenho)
            var res = new ChartPt[n];
            if (n < 3) { for (int i = 0; i < n; i++) res[i] = new ChartPt(x[i], y[i]); return res; }
            for (int i = 0; i < n; i++)
            {
                // os `window` vizinhos mais próximos em X formam uma janela contígua (X estritamente crescente)
                int lo = i, hi = i;
                while (hi - lo + 1 < window)
                {
                    if (lo == 0) hi++;
                    else if (hi == n - 1) lo--;
                    else if (x[i] - x[lo - 1] <= x[hi + 1] - x[i]) lo--; else hi++;
                }
                double dmax = Math.Max(x[i] - x[lo], x[hi] - x[i]) * 1.000001;
                if (dmax <= 0) dmax = 1;
                double sw = 0, swy = 0;
                for (int j = lo; j <= hi; j++)
                {
                    double u = Math.Abs(x[j] - x[i]) / dmax;
                    double w = 1 - u * u * u; w = w * w * w;
                    sw += w; swy += w * y[j];
                }
                res[i] = new ChartPt(x[i], sw > 0 ? swy / sw : y[i]);
            }
            return res;
        }

        /// <summary>Média agregada por X exato (várias séries): Y médio de todos os pares com o mesmo X. Não interpola nem arredonda X.</summary>
        public static ChartPt[] Aggregate(IList<ChartSeries> series, double tol = 0)
        {
            var all = new List<ChartPt[]>();
            foreach (var s in series) { var a = new ChartPt[s.X.Count]; for (int i = 0; i < a.Length; i++) a[i] = new ChartPt(s.X[i], s.Y[i]); all.Add(a); }
            return AggregatePoints(all, tol);
        }

        /// <summary>Média por X (tolerância tol; 0 = igualdade exata) de vários conjuntos de pontos. Não interpola.</summary>
        public static ChartPt[] AggregatePoints(IList<ChartPt[]> sets, double tol)
        {
            var all = sets.SelectMany(a => a).OrderBy(p => p.X).ToList();
            var res = new List<ChartPt>();
            int i = 0;
            while (i < all.Count)
            {
                int j = i + 1; double sx = all[i].X, sy = all[i].Y;
                while (j < all.Count && all[j].X - all[j - 1].X <= tol) { sx += all[j].X; sy += all[j].Y; j++; }
                res.Add(new ChartPt(sx / (j - i), sy / (j - i)));
                i = j;
            }
            return res.ToArray();
        }
    }

    // ------------------------------------------------------------------ agrupamento por X (malhas espaciais)
    /// <summary>Um grupo de amostras com X igual (dentro da tolerância). Derivado: as amostras originais permanecem na série.</summary>
    public sealed class ChartGroup
    {
        public double X;                 // média dos X do grupo
        public int Count;
        public double Mean, Median, Min, Max, StdDev;
        public int Start, End;           // [Start, End): faixa nos arrays X/Y/SourceIndex da série (já ordenados por X) — rastreabilidade
        /// <summary>Valor representativo do grupo para o modo (Raw/Range/StdDev: a média).</summary>
        public double Value(ChartRepeatMode m)
        {
            switch (m)
            {
                case ChartRepeatMode.Median: return Median;
                case ChartRepeatMode.Min: return Min;
                case ChartRepeatMode.Max: return Max;
                default: return Mean;
            }
        }
    }

    public static class ChartGrouping
    {
        /// <summary>
        /// Tolerância automática de agrupamento (X ordenado). Procura o maior salto (≥ 1000×) entre os espaçamentos positivos
        /// onde os menores são ruído numérico (≤ 1e-5 × escala): a tolerância fica entre ruído e estrutura (geométrica) e nunca passa de 25 % do
        /// menor espaçamento real (colunas distintas da malha jamais se fundem). Sem ruído: apenas o épsilon numérico 1e-9 × escala.
        /// </summary>
        public static double AutoTolerance(IList<double> sortedX)
        {
            int n = sortedX == null ? 0 : sortedX.Count;
            if (n < 2) return 1e-9;
            double span = sortedX[n - 1] - sortedX[0];
            double scale = Math.Max(1.0, Math.Max(Math.Abs(sortedX[0]), Math.Max(Math.Abs(sortedX[n - 1]), span)));
            double eps = 1e-9 * scale;
            var gaps = new List<double>();
            for (int i = 0; i + 1 < n; i++) { double g = sortedX[i + 1] - sortedX[i]; if (g > 1e-15 * scale) gaps.Add(g); }
            if (gaps.Count < 2) return eps;
            gaps.Sort();
            double bestRatio = 1000.0; int best = -1;
            for (int k = 0; k + 1 < gaps.Count; k++)
            {
                if (gaps[k] > 1e-5 * scale) break;               // a partir daqui os espaçamentos já são estruturais
                double r = gaps[k + 1] / gaps[k];
                if (r >= bestRatio) { bestRatio = r; best = k; }
            }
            if (best < 0) return eps;
            double tol = Math.Sqrt(gaps[best] * gaps[best + 1]);
            return Math.Max(eps, Math.Min(tol, 0.25 * gaps[best + 1]));
        }

        /// <summary>Agrupa por X (single-linkage: um novo grupo começa quando o salto até a amostra anterior excede a tolerância).</summary>
        public static List<ChartGroup> Build(ChartSeries s, double tol)
        {
            s.GroupToleranceAuto = !(tol > 0);
            if (!(tol > 0)) tol = AutoTolerance(s.X);
            s.GroupTolerance = tol;
            var groups = new List<ChartGroup>();
            int n = s.X.Count, i = 0;
            while (i < n)
            {
                int j = i + 1;
                while (j < n && s.X[j] - s.X[j - 1] <= tol) j++;
                var ys = new List<double>(j - i); double sx = 0;
                for (int k = i; k < j; k++) { ys.Add(s.Y[k]); sx += s.X[k]; }
                double mean = ChartStats.Mean(ys);
                groups.Add(new ChartGroup { X = sx / (j - i), Count = j - i, Mean = mean, Median = ChartStats.Median(ys), Min = ys.Min(), Max = ys.Max(), StdDev = Math.Sqrt(ChartStats.Variance(ys, mean)), Start = i, End = j });
                i = j;
            }
            return groups;
        }
    }

    /// <summary>Texto tabular (CSV com ';') das amostras originais e dos grupos agregados — permite distinguir os dois conjuntos.</summary>
    public static class ChartCsv
    {
        private static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        public static string SamplesHeader => "Series;OriginalIndex;X;Y;Group;GroupX";
        public static string GroupsHeader => "Series;Group;X;Count;Mean;Median;Min;Max;StdDev;OriginalIndices";

        public static IEnumerable<string> SampleRows(ChartSeries s)
        {
            var groups = s.Groups;
            int gi = 0;
            for (int i = 0; i < s.Count; i++)
            {
                while (groups != null && gi < groups.Count - 1 && i >= groups[gi].End) gi++;
                string g = groups != null && groups.Count > 0 ? gi.ToString(CultureInfo.InvariantCulture) : "";
                string gx = groups != null && groups.Count > 0 ? F(groups[gi].X) : "";
                yield return $"{s.Key};{s.SourceIndex[i]};{F(s.X[i])};{F(s.Y[i])};{g};{gx}";
            }
        }

        public static IEnumerable<string> GroupRows(ChartSeries s, bool withIndices = true)
        {
            if (s.Groups == null) yield break;
            for (int k = 0; k < s.Groups.Count; k++)
            {
                var g = s.Groups[k];
                string idx = withIndices ? string.Join("|", s.SourceIndex.Skip(g.Start).Take(g.Count)) : "";
                yield return $"{s.Key};{k};{F(g.X)};{g.Count};{F(g.Mean)};{F(g.Median)};{F(g.Min)};{F(g.Max)};{F(g.StdDev)};{idx}";
            }
        }
    }

    public class HistogramData
    {
        public double MinVal { get; set; }
        public double MaxVal { get; set; }
        public double BinWidth { get; set; }
        public int[] BinCounts { get; set; }
        public double[] BinCenters { get; set; }
        /// <summary>Limites dos bins: BinEdges[b] ≤ x &lt; BinEdges[b+1] (o último bin é fechado à direita). Comprimento = bins + 1.</summary>
        public double[] BinEdges { get; set; }
        public int TotalCount { get; set; }
        public int MaxBinCount { get; set; }
        public PointF[] KdePoints { get; set; }
        public double Mean { get; set; }
        public double Mode { get; set; }
        public double Median { get; set; }
        public double StdDev { get; set; }
        public double? TargetIdeal { get; set; }
        public double TargetMin { get; set; }
        public double TargetMax { get; set; }
        public double? DeltaModeTarget { get; set; }
    }

    public static class ChartHistogram
    {
        /// <summary>
        /// Histograma ESTATÍSTICO: as observações (Y) são classificadas em bins de largura igual; X não participa.
        /// Cada observação cai em exatamente um bin ([e_b, e_{b+1}), último fechado) e a soma das contagens = nº de observações.
        /// </summary>
        public static HistogramData Compute(List<double> allVals, double? target, double tMin, double tMax)
        {
            if (allVals == null || allVals.Count == 0) return null;
            double mean = ChartStats.Mean(allVals);
            double min = allVals.Min(), max = allVals.Max();
            double stdDev = ChartStats.StdDev(allVals);
            double mode = ChartStats.Mode(allVals), median = ChartStats.Median(allVals);

            double spanMin = min, spanMax = max;
            if (target.HasValue) { spanMin = Math.Min(spanMin, tMin); spanMax = Math.Max(spanMax, tMax); }
            double span = spanMax - spanMin;
            if (span <= 1e-6) span = 1.0;
            spanMin -= span * 0.08; spanMax += span * 0.08; span = spanMax - spanMin;

            int numBins = Math.Max(8, Math.Min(22, (int)Math.Ceiling(Math.Sqrt(allVals.Count) * 1.5)));
            double binWidth = span / numBins;
            if (binWidth < 1e-4) binWidth = 0.1;

            var edges = new double[numBins + 1];
            for (int b = 0; b <= numBins; b++) edges[b] = spanMin + b * binWidth;
            var centers = new double[numBins];
            for (int b = 0; b < numBins; b++) centers[b] = spanMin + (b + 0.5) * binWidth;

            int[] counts = new int[numBins]; int maxBin = 0;
            foreach (var v in allVals)
            {
                int bi = (int)Math.Floor((v - spanMin) / binWidth);
                if (bi < 0) bi = 0; if (bi >= numBins) bi = numBins - 1;
                counts[bi]++; if (counts[bi] > maxBin) maxBin = counts[bi];
            }
            if (maxBin == 0) maxBin = 1;

            int kdeSteps = 80;
            var kde = new PointF[kdeSteps];
            double h = stdDev > 0.001 ? 1.06 * stdDev * Math.Pow(allVals.Count, -0.2) : binWidth * 0.8;
            if (h < 0.01) h = 0.01;
            for (int k = 0; k < kdeSteps; k++)
            {
                double x = spanMin + (spanMax - spanMin) * (k / (double)(kdeSteps - 1));
                double sum = 0;
                for (int i = 0; i < allVals.Count; i++) { double z = (x - allVals[i]) / h; sum += Math.Exp(-0.5 * z * z); }
                double dens = sum / (allVals.Count * h * Math.Sqrt(2.0 * Math.PI));
                kde[k] = new PointF((float)x, (float)(dens * allVals.Count * binWidth));
            }

            return new HistogramData
            {
                MinVal = spanMin, MaxVal = spanMax, BinWidth = binWidth, BinCounts = counts, BinCenters = centers, BinEdges = edges,
                TotalCount = allVals.Count, MaxBinCount = maxBin, KdePoints = kde,
                Mean = mean, Mode = mode, Median = median, StdDev = stdDev,
                TargetIdeal = target, TargetMin = tMin, TargetMax = tMax,
                DeltaModeTarget = target.HasValue ? (mode - target.Value) : (double?)null
            };
        }
    }

    // ------------------------------------------------------------------ eixos
    public sealed class ChartTick { public double Value; public string Label; }

    public static class ChartTicks
    {
        public static double NiceStep(double span, int target)
        {
            if (!(span > 0)) return 1.0;
            double raw = span / Math.Max(1, target);
            double exp = Math.Floor(Math.Log10(raw));
            double f = raw / Math.Pow(10, exp);
            double nf = f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10;
            return nf * Math.Pow(10, exp);
        }

        public static List<ChartTick> Make(double min, double max, int target, bool integersOnly = false)
        {
            var res = new List<ChartTick>();
            if (!(max > min)) return res;
            double step = NiceStep(max - min, target);
            if (integersOnly) step = Math.Max(1.0, Math.Ceiling(step));
            double first = Math.Ceiling(min / step - 1e-9);
            for (int k = 0; k < 200; k++)
            {
                double v = (first + k) * step;
                if (v > max + step * 1e-9) break;
                v = Math.Round(v, 12);
                res.Add(new ChartTick { Value = v, Label = Format(v, step) });
            }
            return res;
        }

        public static string Format(double v, double step)
        {
            if (Math.Abs(v) >= 1e7) return v.ToString("G6", CultureInfo.InvariantCulture);
            if (Math.Abs(v) < step * 1e-9) v = 0;
            int dec = step >= 1 ? 0 : Math.Min(8, (int)Math.Ceiling(-Math.Log10(step) - 1e-9));
            return v.ToString("F" + dec, CultureInfo.InvariantCulture);
        }
    }

    // ------------------------------------------------------------------ cena (geometria em coordenadas de DADOS)
    public enum ChartLineRole { Raw, RawContext, Smooth, Trend, Aggregate, Kde }
    public enum ChartRefKind { Mean, Median, Mode, Target }

    public sealed class ChartPoly { public ChartLineRole Role; public int SeriesIndex = -1; public ChartPt[] Pts; }
    public sealed class ChartMarkers { public int SeriesIndex; public bool Muted; public bool Dense; public ChartPt[] Pts; }
    public sealed class ChartBar { public int SeriesIndex; public double X0, X1, Base, Value; }
    public sealed class ChartRef { public ChartRefKind Kind; public bool Horizontal; public double Value; public string Label; }
    public sealed class ChartBand { public bool Horizontal; public double Min, Max; public bool Tolerance; }
    /// <summary>Faixa de dispersão por X (mín–máx ou média ± σ): polígono entre Lo e Hi (mesmos X).</summary>
    public sealed class ChartBandSeries { public int SeriesIndex; public bool IsStdDev; public ChartPt[] Lo, Hi; }
    /// <summary>Barra de erro vertical (mín–máx ou ± σ) numa barra agregada.</summary>
    public sealed class ChartWhisker { public int SeriesIndex; public double X, Lo, Hi; }

    public sealed class ChartScene
    {
        public ChartLineKind Kind;
        public ChartLineMode Mode;
        public ChartRepeatMode RepeatMode;
        public double MinX, MaxX, MinY, MaxY;
        public List<ChartTick> XTicks = new List<ChartTick>(), YTicks = new List<ChartTick>();
        public List<ChartPoly> Polylines = new List<ChartPoly>();
        public List<ChartMarkers> Markers = new List<ChartMarkers>();
        public List<ChartBar> Bars = new List<ChartBar>();
        public List<ChartRef> Refs = new List<ChartRef>();
        public List<ChartBand> Bands = new List<ChartBand>();
        public List<ChartBandSeries> BandSeries = new List<ChartBandSeries>();
        public List<ChartWhisker> Whiskers = new List<ChartWhisker>();
        public double BarSlotWidth;        // XYBars: largura do "slot" por X (unidades de dados)
        public double PooledMean, PooledMedian, PooledStdDev; public int PooledCount;
        public bool AnyAggregated;         // alguma série foi representada por grupos de X
        public bool AnyPointsOnly;         // alguma série com X repetido em Raw: somente pontos (sem linha em zigue-zague)
        public int SamplesDrawn, SamplesTotal;
        public string XUnitNote = "";
    }

    public sealed class ChartSceneOptions
    {
        public ChartLineKind Kind = ChartLineKind.Lines;
        public ChartLineMode Mode = ChartLineMode.Raw;
        public ChartRepeatMode RepeatMode = ChartRepeatMode.Raw;
        public bool ShowSamples = true;          // com agregação: mostrar também as amostras originais
        public double GroupTolerance = 0;        // ≤ 0: automática
        public bool ShowStats = true;
        public bool Combined = true;
        public double? Target; public double TargetMin, TargetMax;
        public double TrendSpan = 0.25;
        public int SmoothSamplesPerSegment = 16;
        public int MarkerLimit = 300;
        public int MaxMarkers = 20000;
        public double BarSlotFraction = 0.8;
    }

    /// <summary>Constrói a cena: ÚNICA fonte de verdade geométrica para Canvas, PNG e saídas do Rhino.</summary>
    public static class ChartSceneBuilder
    {
        public static ChartScene Build(IList<ChartSeries> series, ChartSceneOptions o, HistogramData hist)
        {
            var sc = new ChartScene { Kind = o.Kind, Mode = o.Mode, RepeatMode = o.RepeatMode };
            if (o.Kind == ChartLineKind.Distribution && hist != null) { BuildDistribution(sc, o, hist); return sc; }
            foreach (var s in series) s.EnsureGroups(o.GroupTolerance);
            if (o.Kind == ChartLineKind.XYBars) { BuildBars(sc, series, o); return sc; }
            BuildLines(sc, series, o);
            return sc;
        }

        private static bool Aggregated(ChartSeries s, ChartSceneOptions o) => s.HasRepeats && o.RepeatMode != ChartRepeatMode.Raw;
        private static bool Dispersion(ChartSceneOptions o) => o.RepeatMode == ChartRepeatMode.Range || o.RepeatMode == ChartRepeatMode.StdDev;
        private static void Extent(ChartGroup g, ChartRepeatMode m, out double lo, out double hi)
        {
            if (m == ChartRepeatMode.Range) { lo = g.Min; hi = g.Max; } else { lo = g.Mean - g.StdDev; hi = g.Mean + g.StdDev; }
        }

        private static void Pooled(ChartScene sc, IList<ChartSeries> series)
        {
            var all = series.SelectMany(s => s.Y).ToList();
            sc.PooledCount = all.Count;
            if (all.Count == 0) return;
            sc.PooledMean = ChartStats.Mean(all); sc.PooledMedian = ChartStats.Median(all); sc.PooledStdDev = ChartStats.StdDev(all);
        }

        private static void AddTarget(ChartScene sc, ChartSceneOptions o, bool horizontal)
        {
            if (!o.Target.HasValue) return;
            sc.Bands.Add(new ChartBand { Horizontal = horizontal, Min = o.TargetMin, Max = o.TargetMax, Tolerance = true });
            sc.Refs.Add(new ChartRef { Kind = ChartRefKind.Target, Horizontal = horizontal, Value = o.Target.Value, Label = "Id" });
        }

        private static void AddSamples(ChartScene sc, int seriesIndex, ChartPt[] raw, bool muted, bool dense, int maxMarkers)
        {
            sc.SamplesTotal += raw.Length;
            if (raw.Length <= maxMarkers) { sc.Markers.Add(new ChartMarkers { SeriesIndex = seriesIndex, Pts = raw, Muted = muted, Dense = dense }); sc.SamplesDrawn += raw.Length; return; }
            int stride = (int)Math.Ceiling(raw.Length / (double)maxMarkers);
            var sub = new List<ChartPt>(maxMarkers + 1);
            for (int i = 0; i < raw.Length; i += stride) sub.Add(raw[i]);
            sc.Markers.Add(new ChartMarkers { SeriesIndex = seriesIndex, Pts = sub.ToArray(), Muted = muted, Dense = true });
            sc.SamplesDrawn += sub.Count;
        }

        private static void BuildLines(ChartScene sc, IList<ChartSeries> series, ChartSceneOptions o)
        {
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            foreach (var s in series)
            {
                for (int i = 0; i < s.X.Count; i++)
                {
                    minX = Math.Min(minX, s.X[i]); maxX = Math.Max(maxX, s.X[i]);
                    minY = Math.Min(minY, s.Y[i]); maxY = Math.Max(maxY, s.Y[i]);
                }
                if (Aggregated(s, o) && Dispersion(o))
                    foreach (var g in s.Groups) { Extent(g, o.RepeatMode, out double lo, out double hi); minY = Math.Min(minY, lo); maxY = Math.Max(maxY, hi); }
            }
            if (o.Target.HasValue) { minY = Math.Min(minY, o.TargetMin); maxY = Math.Max(maxY, o.TargetMax); }
            SetDomain(sc, minX, maxX, minY, maxY, 0.02, 0.06);
            Pooled(sc, series);

            var lineSets = new List<ChartPt[]>();
            foreach (var s in series)
            {
                var raw = new ChartPt[s.X.Count];
                for (int i = 0; i < raw.Length; i++) raw[i] = new ChartPt(s.X[i], s.Y[i]);
                bool rep = s.HasRepeats, agg = Aggregated(s, o);
                var aggPts = s.Groups.Select(g => new ChartPt(g.X, g.Value(o.RepeatMode))).ToArray();
                double[] ux = aggPts.Select(p => p.X).ToArray(), uy = aggPts.Select(p => p.Y).ToArray();

                bool samples = agg ? o.ShowSamples : (rep || raw.Length <= o.MarkerLimit);
                if (samples) AddSamples(sc, s.Index, raw, agg || (!rep && o.Mode == ChartLineMode.Trend), raw.Length > o.MarkerLimit, o.MaxMarkers);

                if (!rep)
                {
                    switch (o.Mode)
                    {
                        case ChartLineMode.Smooth:
                            sc.Polylines.Add(new ChartPoly { Role = ChartLineRole.Smooth, SeriesIndex = s.Index, Pts = ux.Length >= 3 ? ChartCurves.PchipSample(ux, uy, o.SmoothSamplesPerSegment) : aggPts });
                            break;
                        case ChartLineMode.Trend:
                            sc.Polylines.Add(new ChartPoly { Role = ChartLineRole.RawContext, SeriesIndex = s.Index, Pts = raw });
                            int w; sc.Polylines.Add(new ChartPoly { Role = ChartLineRole.Trend, SeriesIndex = s.Index, Pts = ChartCurves.MovingTrend(ux, uy, o.TrendSpan, out w) });
                            break;
                        default:
                            sc.Polylines.Add(new ChartPoly { Role = ChartLineRole.Raw, SeriesIndex = s.Index, Pts = raw });
                            break;
                    }
                }
                else if (!agg && o.Mode == ChartLineMode.Raw)
                {
                    sc.AnyPointsOnly = true;       // amostras com X repetido: pontos individuais, sem conectar em sequência
                }
                else
                {
                    if (agg) sc.AnyAggregated = true;
                    switch (o.Mode)
                    {
                        case ChartLineMode.Smooth:
                            sc.Polylines.Add(new ChartPoly { Role = ChartLineRole.Smooth, SeriesIndex = s.Index, Pts = ux.Length >= 3 ? ChartCurves.PchipSample(ux, uy, o.SmoothSamplesPerSegment) : aggPts });
                            break;
                        case ChartLineMode.Trend:
                            int w2; sc.Polylines.Add(new ChartPoly { Role = ChartLineRole.Trend, SeriesIndex = s.Index, Pts = ChartCurves.MovingTrend(ux, uy, o.TrendSpan, out w2) });
                            break;
                        default:
                            sc.Polylines.Add(new ChartPoly { Role = ChartLineRole.Raw, SeriesIndex = s.Index, Pts = aggPts });
                            break;
                    }
                }
                lineSets.Add(rep ? aggPts : raw);

                if (agg && Dispersion(o))
                {
                    var lo = new ChartPt[s.Groups.Count]; var hi = new ChartPt[s.Groups.Count];
                    for (int k = 0; k < lo.Length; k++) { Extent(s.Groups[k], o.RepeatMode, out double l, out double h); lo[k] = new ChartPt(s.Groups[k].X, l); hi[k] = new ChartPt(s.Groups[k].X, h); }
                    sc.BandSeries.Add(new ChartBandSeries { SeriesIndex = s.Index, IsStdDev = o.RepeatMode == ChartRepeatMode.StdDev, Lo = lo, Hi = hi });
                }
            }
            if (o.Combined && series.Count > 1)
                sc.Polylines.Add(new ChartPoly { Role = ChartLineRole.Aggregate, SeriesIndex = -1, Pts = ChartCurves.AggregatePoints(lineSets, series.Max(s => s.GroupTolerance)) });

            if (o.ShowStats && sc.PooledCount > 0)
            {
                sc.Refs.Add(new ChartRef { Kind = ChartRefKind.Mean, Horizontal = true, Value = sc.PooledMean, Label = "μ" });
                sc.Refs.Add(new ChartRef { Kind = ChartRefKind.Median, Horizontal = true, Value = sc.PooledMedian, Label = "Mediana" });
                sc.Bands.Add(new ChartBand { Horizontal = true, Min = sc.PooledMean - sc.PooledStdDev, Max = sc.PooledMean + sc.PooledStdDev });
            }
            AddTarget(sc, o, true);
            sc.XTicks = ChartTicks.Make(sc.MinX, sc.MaxX, 8);
            sc.YTicks = ChartTicks.Make(sc.MinY, sc.MaxY, 6);
        }

        private static ChartPt[] Pairs(double[] x, double[] y) { var r = new ChartPt[x.Length]; for (int i = 0; i < r.Length; i++) r[i] = new ChartPt(x[i], y[i]); return r; }

        private static void BuildBars(ChartScene sc, IList<ChartSeries> series, ChartSceneOptions o)
        {
            // centros distintos de X (agrupados com a maior tolerância das séries)
            double gtol = series.Max(s => s.GroupTolerance);
            var allX = series.SelectMany(s => s.Groups.Select(g => g.X)).OrderBy(v => v).ToList();
            var centers = new List<double>();
            for (int i = 0; i < allX.Count; i++)
                if (centers.Count == 0 || allX[i] - centers[centers.Count - 1] > gtol) centers.Add(allX[i]);
            double spacing = double.MaxValue;
            for (int i = 1; i < centers.Count; i++) spacing = Math.Min(spacing, centers[i] - centers[i - 1]);
            if (spacing == double.MaxValue || spacing <= 0) spacing = 1.0;
            double slot = spacing * o.BarSlotFraction;
            sc.BarSlotWidth = slot;
            Func<double, double> centerOf = x =>
            {
                int k = centers.BinarySearch(x); if (k >= 0) return centers[k];
                k = ~k; if (k == 0) return centers[0]; if (k >= centers.Count) return centers[centers.Count - 1];
                return x - centers[k - 1] <= centers[k] - x ? centers[k - 1] : centers[k];
            };

            int m = Math.Max(1, series.Count);
            double minY = 0, maxY = 0;
            for (int si = 0; si < series.Count; si++)
            {
                var s = series[si]; double sub = slot / m;
                bool agg = Aggregated(s, o);
                foreach (var g in s.Groups)
                {
                    double c = centerOf(g.X), left = c - slot / 2 + si * sub;
                    if (agg)
                    {
                        sc.AnyAggregated = true;
                        double v = g.Value(o.RepeatMode);
                        sc.Bars.Add(new ChartBar { SeriesIndex = s.Index, X0 = left, X1 = left + sub, Base = 0, Value = v });
                        minY = Math.Min(minY, v); maxY = Math.Max(maxY, v);
                        if (Dispersion(o))
                        {
                            Extent(g, o.RepeatMode, out double lo, out double hi);
                            sc.Whiskers.Add(new ChartWhisker { SeriesIndex = s.Index, X = left + sub / 2, Lo = lo, Hi = hi });
                            minY = Math.Min(minY, lo); maxY = Math.Max(maxY, hi);
                        }
                    }
                    else
                    {
                        double w = sub / g.Count;
                        for (int k = 0; k < g.Count; k++)
                        {
                            double yv = s.Y[g.Start + k];
                            sc.Bars.Add(new ChartBar { SeriesIndex = s.Index, X0 = left + k * w, X1 = left + (k + 1) * w, Base = 0, Value = yv });
                            minY = Math.Min(minY, yv); maxY = Math.Max(maxY, yv);
                        }
                    }
                }
            }
            if (o.Target.HasValue) { minY = Math.Min(minY, o.TargetMin); maxY = Math.Max(maxY, o.TargetMax); }
            double minX = sc.Bars.Min(b => b.X0), maxX = sc.Bars.Max(b => b.X1);
            double padY = (maxY - minY) * 0.06; if (padY <= 0) padY = 1.0;
            sc.MinX = minX - (maxX - minX) * 0.03 - 1e-12; sc.MaxX = maxX + (maxX - minX) * 0.03 + 1e-12;
            if (sc.MaxX <= sc.MinX) { sc.MinX -= 1; sc.MaxX += 1; }
            sc.MinY = minY < 0 ? minY - padY : 0; sc.MaxY = maxY > 0 ? maxY + padY : 0;
            if (sc.MaxY <= sc.MinY) { sc.MinY = -1; sc.MaxY = 1; }
            Pooled(sc, series);
            if (o.ShowStats && sc.PooledCount > 0)
            {
                sc.Refs.Add(new ChartRef { Kind = ChartRefKind.Mean, Horizontal = true, Value = sc.PooledMean, Label = "μ" });
                sc.Refs.Add(new ChartRef { Kind = ChartRefKind.Median, Horizontal = true, Value = sc.PooledMedian, Label = "Mediana" });
                sc.Bands.Add(new ChartBand { Horizontal = true, Min = sc.PooledMean - sc.PooledStdDev, Max = sc.PooledMean + sc.PooledStdDev });
            }
            AddTarget(sc, o, true);
            sc.XTicks = ChartTicks.Make(sc.MinX, sc.MaxX, 8);
            sc.YTicks = ChartTicks.Make(sc.MinY, sc.MaxY, 6);
        }

        private static void BuildDistribution(ChartScene sc, ChartSceneOptions o, HistogramData hist)
        {
            sc.MinX = hist.MinVal; sc.MaxX = hist.MaxVal; sc.MinY = 0; sc.MaxY = Math.Max(1.0, hist.MaxBinCount * 1.2);
            for (int b = 0; b < hist.BinCounts.Length; b++)
                sc.Bars.Add(new ChartBar { SeriesIndex = 0, X0 = hist.BinEdges[b], X1 = hist.BinEdges[b + 1], Base = 0, Value = hist.BinCounts[b] });
            if (o.ShowStats)
            {
                sc.Refs.Add(new ChartRef { Kind = ChartRefKind.Mean, Horizontal = false, Value = hist.Mean, Label = "μ" });
                sc.Refs.Add(new ChartRef { Kind = ChartRefKind.Median, Horizontal = false, Value = hist.Median, Label = "Mediana" });
                sc.Refs.Add(new ChartRef { Kind = ChartRefKind.Mode, Horizontal = false, Value = hist.Mode, Label = "Mo" });
                sc.Bands.Add(new ChartBand { Horizontal = false, Min = hist.Mean - hist.StdDev, Max = hist.Mean + hist.StdDev });
            }
            if (o.Combined && hist.KdePoints != null && hist.KdePoints.Length > 1)
                sc.Polylines.Add(new ChartPoly { Role = ChartLineRole.Kde, Pts = hist.KdePoints.Select(p => new ChartPt(p.X, p.Y)).ToArray() });
            AddTarget(sc, o, false);
            sc.PooledMean = hist.Mean; sc.PooledMedian = hist.Median; sc.PooledStdDev = hist.StdDev; sc.PooledCount = hist.TotalCount;
            sc.XTicks = ChartTicks.Make(sc.MinX, sc.MaxX, 7);
            sc.YTicks = ChartTicks.Make(sc.MinY, sc.MaxY, 5, true);
        }

        private static void SetDomain(ChartScene sc, double minX, double maxX, double minY, double maxY, double padX, double padY)
        {
            if (!(maxX > minX)) { minX -= 1; maxX += 1; }
            if (!(maxY > minY)) { minY -= 1; maxY += 1; }
            double dx = (maxX - minX) * padX, dy = (maxY - minY) * padY;
            sc.MinX = minX - dx; sc.MaxX = maxX + dx; sc.MinY = minY - dy; sc.MaxY = maxY + dy;
        }
    }

    /// <summary>Transformação ÚNICA dados → tela (Y invertido):  ScreenX = L + (X−Xmin)/(Xmax−Xmin)·W;  ScreenY = B − (Y−Ymin)/(Ymax−Ymin)·H.</summary>
    public sealed class ChartMapper
    {
        private readonly RectangleF _plot; private readonly ChartScene _s;
        public ChartMapper(RectangleF plot, ChartScene scene) { _plot = plot; _s = scene; }
        public float X(double x) => (float)(_plot.Left + (x - _s.MinX) / (_s.MaxX - _s.MinX) * _plot.Width);
        public float Y(double y) => (float)(_plot.Bottom - (y - _s.MinY) / (_s.MaxY - _s.MinY) * _plot.Height);
        public PointF P(ChartPt p) => new PointF(X(p.X), Y(p.Y));
    }
}
