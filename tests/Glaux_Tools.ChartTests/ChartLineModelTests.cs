using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Buraqueira_Tools;
using Xunit;

namespace Glaux_Tools.ChartTests
{
    /// <summary>Line Chart &amp; Statistics / Histogram: modelo de dados, curvas, estatÃ­sticas, cena e mapeamento (sem Rhino/Grasshopper).</summary>
    public class ChartLineModelTests
    {
        public ChartLineModelTests() { CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; }

        private static ChartSeries S(double[] x, double[] y, int idx = 0)
            => ChartSeriesBuilder.Pair("{" + idx + "}", "SÃ©rie {" + idx + "}", idx, x?.Select(v => (double?)v).ToList(), y.Select(v => (double?)v).ToList());

        private static ChartScene Scene(IList<ChartSeries> ss, ChartLineKind kind = ChartLineKind.Lines, ChartLineMode mode = ChartLineMode.Raw, bool stats = true, bool combined = true)
            => ChartSceneBuilder.Build(ss, new ChartSceneOptions { Kind = kind, Mode = mode, ShowStats = stats, Combined = combined }, null);

        // ---------------------------------------------------------------- TEST 01
        [Fact]
        public void T01_IrregularX_RealDistanceOnScreen()
        {
            var s = S(new double[] { 0, 1, 2, 10 }, new double[] { 0, 10, 5, 20 });
            var sc = Scene(new[] { s });
            var plot = new RectangleF(100, 50, 400, 200);
            var m = new ChartMapper(plot, sc);
            double d12 = m.X(2) - m.X(1), d210 = m.X(10) - m.X(2);
            Assert.Equal(8.0, d210 / d12, 4);                       // 8 unidades de X Ã· 1 unidade
            Assert.Equal(m.X(1) - m.X(0), d12, 4);                  // espaÃ§amentos iguais para distÃ¢ncias iguais
            // fÃ³rmula do enunciado
            Assert.Equal(plot.Left + (5 - sc.MinX) / (sc.MaxX - sc.MinX) * plot.Width, m.X(5), 3);
            Assert.True(m.Y(20) < m.Y(0));                           // Y de tela invertido
            Assert.Equal(plot.Bottom - (7 - sc.MinY) / (sc.MaxY - sc.MinY) * plot.Height, m.Y(7), 3);
        }

        [Fact]
        public void Raw_IsExactlyThePairs_NoArtificialPoints()
        {
            var s = S(new double[] { 3, 1, 2 }, new double[] { 30, 10, 20 });
            var sc = Scene(new[] { s });
            var line = sc.Polylines.Single(p => p.Role == ChartLineRole.Raw);
            Assert.Equal(new[] { 1.0, 2, 3 }, line.Pts.Select(p => p.X));
            Assert.Equal(new[] { 10.0, 20, 30 }, line.Pts.Select(p => p.Y));
        }

        // ---------------------------------------------------------------- TEST 02
        [Fact]
        public void T02_Smooth_Peak_NoOvershoot_PassesThroughPoints()
        {
            double[] x = { 0, 1, 2, 3, 4 }, y = { 0, 10, 0, 10, 0 };
            var pts = ChartCurves.PchipSample(x, y, 32);
            Assert.True(pts.All(p => p.Y <= 10 + 1e-12 && p.Y >= 0 - 1e-12), "PCHIP nÃ£o pode ultrapassar os extremos dos dados");
            foreach (int k in Enumerable.Range(0, 5))                                      // passa exatamente pelos pontos originais
                Assert.Contains(pts, p => Math.Abs(p.X - x[k]) < 1e-12 && Math.Abs(p.Y - y[k]) < 1e-12);
            Assert.True(pts.All(p => p.X >= 0 && p.X <= 4));                               // sem extrapolaÃ§Ã£o do domÃ­nio
            // mÃ¡ximos/mÃ­nimos locais preservados
            Assert.Equal(10.0, pts.Max(p => p.Y), 12);
            Assert.Equal(0.0, pts.Min(p => p.Y), 12);
        }

        [Fact]
        public void Smooth_RandomData_NeverLeavesIntervalRange()
        {
            var rnd = new Random(12345);
            for (int trial = 0; trial < 200; trial++)
            {
                int n = rnd.Next(3, 40);
                var x = new double[n]; var y = new double[n];
                double cx = 0; for (int i = 0; i < n; i++) { cx += 0.1 + rnd.NextDouble() * 5; x[i] = cx; y[i] = rnd.NextDouble() * 200 - 100; }
                var pts = ChartCurves.PchipSample(x, y, 12);
                for (int k = 0; k < n - 1; k++)
                {
                    double lo = Math.Min(y[k], y[k + 1]) - 1e-9, hi = Math.Max(y[k], y[k + 1]) + 1e-9;
                    foreach (var p in pts.Where(q => q.X >= x[k] && q.X <= x[k + 1])) Assert.InRange(p.Y, lo, hi);
                }
            }
        }

        [Fact]
        public void Bezier_Segments_MatchSampledPchip()
        {
            double[] x = { 0, 1, 2.5, 4 }, y = { 1, 5, 2, 8 };
            var segs = ChartCurves.PchipBezier(x, y);
            var d = ChartCurves.PchipSlopes(x, y);
            for (int k = 0; k < 3; k++)
            {
                Assert.Equal(y[k], segs[k][0].Y, 12); Assert.Equal(y[k + 1], segs[k][3].Y, 12);
                double h = x[k + 1] - x[k];
                Assert.Equal(y[k] + h * d[k] / 3, segs[k][1].Y, 12);
            }
            // pontos de controle em X igualmente espaÃ§ados â‡’ y(x) Ã© funÃ§Ã£o de x (sem laÃ§os)
            foreach (var s in segs) { Assert.True(s[0].X < s[1].X && s[1].X < s[2].X && s[2].X < s[3].X); }
        }

        // ---------------------------------------------------------------- TEST 03
        [Fact]
        public void T03_UnsortedX_PairsSortedTogether()
        {
            var s = S(new double[] { 3, 1, 2 }, new double[] { 30, 10, 20 });
            Assert.Equal(new[] { 1.0, 2, 3 }, s.X);
            Assert.Equal(new[] { 10.0, 20, 30 }, s.Y);
            Assert.Equal(new[] { 1, 2, 0 }, s.SourceIndex);
            Assert.True(s.InputWasUnsorted);
        }

        [Fact]
        public void IndexIsNotUsedAsXWhenXIsProvided_AndInvalidPairsDroppedTogether()
        {
            var xs = new List<double?> { 5, null, 7, 6 }; var ys = new List<double?> { 1, 2, double.NaN, 4 };
            var s = ChartSeriesBuilder.Pair("{0}", "s", 0, xs, ys);
            Assert.Equal(2, s.Dropped);
            Assert.Equal(new[] { 5.0, 6 }, s.X); Assert.Equal(new[] { 1.0, 4 }, s.Y);
            var noX = S(null, new double[] { 9, 8, 7 });
            Assert.Equal(new[] { 0.0, 1, 2 }, noX.X); Assert.False(noX.XExplicit);
        }

        [Fact]
        public void DuplicateX_IsCountedAndCollapsedForSmooth()
        {
            var s = S(new double[] { 1, 2, 2, 3 }, new double[] { 0, 4, 8, 10 });
            Assert.Equal(1, s.DuplicateX);
            var sc = Scene(new[] { s }, mode: ChartLineMode.Smooth);
            var line = sc.Polylines.Single(p => p.Role == ChartLineRole.Smooth);
            Assert.Contains(line.Pts, p => Math.Abs(p.X - 2) < 1e-12 && Math.Abs(p.Y - 6) < 1e-12);   // mÃ©dia de 4 e 8
            Assert.True(line.Pts.Zip(line.Pts.Skip(1), (a, b) => b.X >= a.X).All(v => v));            // funÃ§Ã£o de X
        }

        // ---------------------------------------------------------------- TEST 04
        [Fact]
        public void T04_Constant_StaysFlat_AllModes()
        {
            var s = S(new double[] { 0, 1, 2, 3 }, new double[] { 5, 5, 5, 5 });
            foreach (var mode in new[] { ChartLineMode.Raw, ChartLineMode.Smooth, ChartLineMode.Trend })
            {
                var sc = Scene(new[] { s }, mode: mode);
                foreach (var pl in sc.Polylines) Assert.All(pl.Pts, p => Assert.Equal(5.0, p.Y, 12));
                Assert.True(sc.MaxY > sc.MinY);      // domÃ­nio nÃ£o degenera
            }
        }

        // ---------------------------------------------------------------- TEST 05
        [Fact]
        public void T05_Statistics_KnownValues_IndependentOfMode()
        {
            double[] y = { 2, 4, 4, 4, 5, 5, 7, 9 };
            var s = S(Enumerable.Range(0, 8).Select(i => (double)i).ToArray(), y);
            Assert.Equal(5.0, s.Mean, 12);
            Assert.Equal(4.5, s.Median, 12);
            Assert.Equal(Math.Sqrt(32.0 / 7.0), s.StdDev, 12);       // amostral (nâˆ’1)
            Assert.Equal(2.0, s.MinY); Assert.Equal(9.0, s.MaxY);
            foreach (var mode in new[] { ChartLineMode.Raw, ChartLineMode.Smooth, ChartLineMode.Trend })
            {
                var sc = Scene(new[] { s }, mode: mode);
                Assert.Equal(5.0, sc.Refs.Single(r => r.Kind == ChartRefKind.Mean).Value, 12);         // linha de mÃ©dia horizontal em Y = mÃ©dia
                Assert.True(sc.Refs.Single(r => r.Kind == ChartRefKind.Mean).Horizontal);
                var band = sc.Bands.Single(b => !b.Tolerance);
                Assert.Equal(5.0 - s.StdDev, band.Min, 12); Assert.Equal(5.0 + s.StdDev, band.Max, 12);
            }
        }

        [Fact]
        public void Statistics_UseOriginalValues_NotInterpolatedOnes()
        {
            var s = S(new double[] { 0, 1, 2, 3, 4 }, new double[] { 0, 10, 0, 10, 0 });
            var sc = Scene(new[] { s }, mode: ChartLineMode.Smooth);
            Assert.Equal(4.0, sc.PooledMean, 12);
            Assert.Equal(5, sc.PooledCount);                          // pontos suavizados nÃ£o entram na contagem
        }

        // ---------------------------------------------------------------- Trend
        [Fact]
        public void Trend_NeverExceedsDataRange_EvaluatedAtOriginalX()
        {
            var rnd = new Random(7);
            var x = Enumerable.Range(0, 120).Select(i => i * 0.0225 + 0.1255).ToArray();
            var y = x.Select(v => 30 + 8 * Math.Sin(v * 3) + rnd.NextDouble() * 60 - 30).ToArray();
            var trend = ChartCurves.MovingTrend(x, y, 0.25, out int win);
            Assert.Equal(x.Length, trend.Length);
            Assert.True(trend.Select(p => p.X).SequenceEqual(x));
            Assert.All(trend, p => Assert.InRange(p.Y, y.Min(), y.Max()));
            Assert.True(win >= 5);
            double sdRaw = ChartStats.StdDev(y), sdTrend = ChartStats.StdDev(trend.Select(p => p.Y).ToList());
            Assert.True(sdTrend < sdRaw * 0.6, "a tendÃªncia deve ser mais suave que os dados");
        }

        [Fact]
        public void Trend_Mode_KeepsRawDataVisible_AndSeparate()
        {
            var s = S(Enumerable.Range(0, 20).Select(i => (double)i).ToArray(), Enumerable.Range(0, 20).Select(i => (double)(i % 5)).ToArray());
            var sc = Scene(new[] { s }, mode: ChartLineMode.Trend);
            Assert.Contains(sc.Polylines, p => p.Role == ChartLineRole.RawContext && p.Pts.Length == 20);
            Assert.Contains(sc.Polylines, p => p.Role == ChartLineRole.Trend);
            Assert.DoesNotContain(sc.Polylines, p => p.Role == ChartLineRole.Smooth);
        }

        [Fact]
        public void Aggregate_MeanByExactX_NoRoundingNoInterpolation()
        {
            var a = S(new double[] { 1, 2 }, new double[] { 2, 4 }, 0); var b = S(new double[] { 1, 2 }, new double[] { 4, 8 }, 1);
            var agg = ChartCurves.Aggregate(new[] { a, b });
            Assert.Equal(new[] { 1.0, 2 }, agg.Select(p => p.X)); Assert.Equal(new[] { 3.0, 6 }, agg.Select(p => p.Y));
            var sc = Scene(new[] { a, b });
            Assert.Contains(sc.Polylines, p => p.Role == ChartLineRole.Aggregate);
            Assert.Equal(2, sc.Polylines.Count(p => p.Role == ChartLineRole.Raw));   // dados reais continuam desenhados
        }

        // ---------------------------------------------------------------- TEST 06 / 07
        [Fact]
        public void T06_XYBars_PositionsAndHeights()
        {
            var s = S(new double[] { 10, 20, 30 }, new double[] { 5, 12, 8 });
            var sc = Scene(new[] { s }, ChartLineKind.XYBars);
            Assert.Equal(3, sc.Bars.Count);
            Assert.Equal(new[] { 10.0, 20, 30 }, sc.Bars.Select(b => (b.X0 + b.X1) / 2).Select(v => Math.Round(v, 9)));
            Assert.Equal(new[] { 5.0, 12, 8 }, sc.Bars.Select(b => b.Value));
            Assert.All(sc.Bars, b => Assert.Equal(0.0, b.Base));
            Assert.All(sc.Bars, b => Assert.Equal(8.0, b.X1 - b.X0, 9));      // 0,8 Ã— espaÃ§amento 10
            Assert.Equal(0.0, sc.MinY);
        }

        [Fact]
        public void T07_XYBars_IrregularX_NotEquallySpaced_NoOverlap()
        {
            var s = S(new double[] { 1, 2, 8, 20 }, new double[] { 5, 10, 15, 20 });
            var sc = Scene(new[] { s }, ChartLineKind.XYBars);
            var plot = new RectangleF(0, 0, 1000, 300); var m = new ChartMapper(plot, sc);
            double c1 = sc.Bars[0].X0 + (sc.Bars[0].X1 - sc.Bars[0].X0) / 2, c2 = sc.Bars[1].X0 + (sc.Bars[1].X1 - sc.Bars[1].X0) / 2,
                   c8 = sc.Bars[2].X0 + (sc.Bars[2].X1 - sc.Bars[2].X0) / 2, c20 = sc.Bars[3].X0 + (sc.Bars[3].X1 - sc.Bars[3].X0) / 2;
            Assert.Equal((20 - 8) / (double)(2 - 1), (m.X(c20) - m.X(c8)) / (m.X(c2) - m.X(c1)), 6);
            for (int i = 1; i < sc.Bars.Count; i++) Assert.True(sc.Bars[i].X0 >= sc.Bars[i - 1].X1 - 1e-12);
        }

        [Fact]
        public void XYBars_MultiSeries_SideBySide_DuplicateXSplitSlot()
        {
            var a = S(new double[] { 1, 2 }, new double[] { 3, 4 }, 0); var b = S(new double[] { 1, 2 }, new double[] { 5, 6 }, 1);
            var sc = Scene(new[] { a, b }, ChartLineKind.XYBars);
            var at1 = sc.Bars.Where(x => x.X0 < 1.5).OrderBy(x => x.X0).ToList();
            Assert.Equal(2, at1.Count); Assert.True(at1[0].X1 <= at1[1].X0 + 1e-12);
            var dup = S(new double[] { 1, 1, 2 }, new double[] { 3, 4, 5 });
            var sd = Scene(new[] { dup }, ChartLineKind.XYBars);
            var d1 = sd.Bars.Where(x => x.X0 < 1.5).OrderBy(x => x.X0).ToList();
            Assert.Equal(2, d1.Count); Assert.True(d1[0].X1 <= d1[1].X0 + 1e-12);
            Assert.Equal(new[] { 3.0, 4 }, d1.Select(x => x.Value));
        }

        // ---------------------------------------------------------------- TEST 08
        [Fact]
        public void T08_Histogram_CountsLimitsAndTotal()
        {
            var vals = Enumerable.Range(0, 100).Select(i => (double)i).ToList();
            var h = ChartHistogram.Compute(vals, null, 0, 0);
            Assert.Equal(100, h.TotalCount); Assert.Equal(100, h.BinCounts.Sum());
            Assert.Equal(h.BinCounts.Length + 1, h.BinEdges.Length);
            Assert.True(h.BinEdges.First() <= 0 && h.BinEdges.Last() >= 99);
            for (int b = 0; b < h.BinCounts.Length; b++)
            {
                Assert.Equal(h.BinWidth, h.BinEdges[b + 1] - h.BinEdges[b], 9);
                Assert.Equal((h.BinEdges[b] + h.BinEdges[b + 1]) / 2, h.BinCenters[b], 9);       // centro â‰  limite
                int expected = vals.Count(v => v >= h.BinEdges[b] && (v < h.BinEdges[b + 1] || (b == h.BinCounts.Length - 1 && v <= h.BinEdges[b + 1])));
                Assert.Equal(expected, h.BinCounts[b]);
            }
        }

        [Fact]
        public void Histogram_Repeated_Observations()
        {
            var vals = new List<double> { 1, 1, 2, 2, 2, 3, 4 };
            var h = ChartHistogram.Compute(vals, null, 0, 0);
            Assert.Equal(7, h.BinCounts.Sum());
            Assert.Equal(3, h.MaxBinCount);                              // o valor 2 aparece 3 vezes
            Assert.Equal(2.0, h.Median);
            var sc = ChartSceneBuilder.Build(new[] { S(null, vals.ToArray()) }, new ChartSceneOptions { Kind = ChartLineKind.Distribution }, h);
            Assert.Equal(h.BinCounts.Length, sc.Bars.Count);
            Assert.Equal(h.BinCounts.Select(c => (double)c), sc.Bars.Select(b => b.Value));
            Assert.All(sc.YTicks, t => Assert.Equal(Math.Round(t.Value), t.Value));            // eixo de contagem sÃ³ com inteiros
        }

        // ---------------------------------------------------------------- TEST 09
        [Fact]
        public void T09_NegativeBars_BaselineAtZero()
        {
            var s = S(new double[] { 1, 2, 3, 4 }, new double[] { -10, 20, -5, 15 });
            var sc = Scene(new[] { s }, ChartLineKind.XYBars);
            Assert.True(sc.MinY < -10 && sc.MaxY > 20);
            var m = new ChartMapper(new RectangleF(0, 0, 400, 300), sc);
            float y0 = m.Y(0);
            Assert.All(sc.Bars, b => Assert.Equal(0.0, b.Base));
            Assert.True(m.Y(sc.Bars[0].Value) > y0);        // negativa: abaixo da linha de base (Y de tela maior)
            Assert.True(m.Y(sc.Bars[1].Value) < y0);        // positiva: acima
            Assert.True(y0 > 0 && y0 < 300);
        }

        [Fact]
        public void AllNegativeBars_DomainStillIncludesZero()
        {
            var s = S(new double[] { 1, 2 }, new double[] { -3, -8 });
            var sc = Scene(new[] { s }, ChartLineKind.XYBars);
            Assert.Equal(0.0, sc.MaxY); Assert.True(sc.MinY < -8);
        }

        // ---------------------------------------------------------------- eixos
        [Fact]
        public void Ticks_AreNiceNumbers()
        {
            var t = ChartTicks.Make(0, 100, 5);
            Assert.Equal(new[] { 0.0, 20, 40, 60, 80, 100 }, t.Select(x => x.Value));
            var t2 = ChartTicks.Make(10.27, 95.55, 6);
            Assert.All(t2, x => Assert.Equal(0.0, Math.Round(x.Value / 10) * 10 - x.Value, 9));
            Assert.Equal("0.2", ChartTicks.Make(0, 1, 4)[1].Label);
        }

        // ---------------------------------------------------------------- TEST 11 (mesma cena, dois temas e tamanhos)
        [Fact]
        public void T11_SameScene_SameNormalizedPositions_InCanvasAndExportRenderers()
        {
            var s = S(new double[] { 0, 1, 2, 10 }, new double[] { 0, 10, 5, 20 });
            var scene = Scene(new[] { s }, stats: false);
            Color expected = ChartLine_Component.Palette[0];
            foreach (var (theme, plot) in new[] { (ChartTheme.Light(1f), new RectangleF(60, 40, 640, 380)), (ChartTheme.Dark(1f), new RectangleF(40, 20, 340, 190)) })
            {
                using (var bmp = new Bitmap((int)(plot.Right + 40), (int)(plot.Bottom + 40)))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Gray);
                    ChartLineRenderer.DrawPlot(g, plot, scene, theme, new FontFamily("Arial"));
                    var m = new ChartMapper(plot, scene);
                    foreach (var p in scene.Markers.Single().Pts)
                    {
                        var px = m.P(p);
                        Color c = bmp.GetPixel((int)Math.Round(px.X), (int)Math.Round(px.Y));
                        Assert.True(c.R == expected.R && c.G == expected.G && c.B == expected.B, $"marcador de ({p.X},{p.Y}) nÃ£o estÃ¡ na posiÃ§Ã£o esperada ({px.X:F1},{px.Y:F1}): {c}");
                    }
                }
            }
            // as proporÃ§Ãµes (fraÃ§Ã£o do retÃ¢ngulo de plotagem) sÃ£o idÃªnticas nas duas superfÃ­cies
            var a = new ChartMapper(new RectangleF(60, 40, 640, 380), scene); var b2 = new ChartMapper(new RectangleF(40, 20, 340, 190), scene);
            foreach (var p in scene.Markers.Single().Pts)
            {
                Assert.Equal((a.X(p.X) - 60) / 640.0, (b2.X(p.X) - 40) / 340.0, 6);
                Assert.Equal((380 + 40 - a.Y(p.Y)) / 380.0, (190 + 20 - b2.Y(p.Y)) / 190.0, 6);
            }
        }

        [Fact]
        public void Large_Series_Performance_And_MarkerLimit()
        {
            int n = 20000; var rnd = new Random(1);
            var s = S(Enumerable.Range(0, n).Select(i => (double)i).ToArray(), Enumerable.Range(0, n).Select(i => rnd.NextDouble()).ToArray());
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var raw = Scene(new[] { s });
            var smooth = Scene(new[] { s }, mode: ChartLineMode.Smooth);
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 5000, $"raw+smooth em {sw.ElapsedMilliseconds} ms");
            Assert.Empty(raw.Markers);                     // acima do limite: sem marcadores
        }
    }
}


