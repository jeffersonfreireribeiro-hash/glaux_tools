using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Buraqueira_Tools;
using Xunit;

namespace Glaux_Tools.ChartTests
{
    /// <summary>Malhas de simulação espacial: X repetido, agrupamento com tolerância, agregação (média/mediana/mín/máx/faixa/σ), rastreabilidade.</summary>
    public class ChartLineSpatialTests
    {
        public ChartLineSpatialTests() { CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; }

        private static ChartSeries S(double[] x, double[] y, int idx = 0)
            => ChartSeriesBuilder.Pair("{" + idx + "}", "Série {" + idx + "}", idx, x?.Select(v => (double?)v).ToList(), y.Select(v => (double?)v).ToList());

        // malha 3×3 do enunciado: colunas X = 0, 1, 2; lux em cada linha Y
        private static readonly double[] GridX = { 0, 0, 0, 1, 1, 1, 2, 2, 2 };
        private static readonly double[] GridLux = { 100, 200, 300, 350, 400, 450, 450, 500, 600 };

        private static ChartScene Scene(IList<ChartSeries> ss, ChartRepeatMode rm, ChartLineKind kind = ChartLineKind.Lines, ChartLineMode mode = ChartLineMode.Raw, bool samples = true, double gtol = 0)
            => ChartSceneBuilder.Build(ss, new ChartSceneOptions { Kind = kind, Mode = mode, RepeatMode = rm, ShowSamples = samples, GroupTolerance = gtol }, null);

        // ---------------------------------------------------------------- TESTE A
        [Fact]
        public void A_Grid3x3_MeanMinMaxCountPerX()
        {
            var s = S(GridX, GridLux);
            s.EnsureGroups(0);
            Assert.Equal(3, s.Groups.Count);
            Assert.Equal(new[] { 200.0, 400.0, 516.6666666666666 }, s.Groups.Select(g => g.Mean), new DoubleComparer());
            Assert.Equal(new[] { 100.0, 350, 450 }, s.Groups.Select(g => g.Min));
            Assert.Equal(new[] { 300.0, 450, 600 }, s.Groups.Select(g => g.Max));
            Assert.Equal(new[] { 200.0, 400, 500 }, s.Groups.Select(g => g.Median));
            Assert.All(s.Groups, g => Assert.Equal(3, g.Count));
            Assert.Equal(9, s.Count);                                   // nenhuma amostra perdida
            Assert.True(s.HasRepeats);
            var sc = Scene(new[] { s }, ChartRepeatMode.Mean);
            var line = sc.Polylines.Single(p => p.Role == ChartLineRole.Raw);
            Assert.Equal(new[] { 0.0, 1, 2 }, line.Pts.Select(p => p.X));
            Assert.Equal(200.0, line.Pts[0].Y, 9); Assert.Equal(400.0, line.Pts[1].Y, 9); Assert.Equal(516.6667, line.Pts[2].Y, 3);
            Assert.Equal(9, sc.Markers.Sum(m => m.Pts.Length));          // amostras originais visíveis junto da linha
            Assert.True(sc.Markers.All(m => m.Muted));
        }

        [Theory]
        [InlineData(ChartRepeatMode.Median, 200, 400, 500)]
        [InlineData(ChartRepeatMode.Min, 100, 350, 450)]
        [InlineData(ChartRepeatMode.Max, 300, 450, 600)]
        public void A_OtherAggregations(ChartRepeatMode mode, double v0, double v1, double v2)
        {
            var sc = Scene(new[] { S(GridX, GridLux) }, mode);
            Assert.Equal(new[] { v0, v1, v2 }, sc.Polylines.Single(p => p.Role == ChartLineRole.Raw).Pts.Select(p => p.Y));
        }

        [Fact]
        public void A_Range_And_StdDev_Bands()
        {
            var s = S(GridX, GridLux);
            var r = Scene(new[] { s }, ChartRepeatMode.Range);
            var band = r.BandSeries.Single();
            Assert.False(band.IsStdDev);
            Assert.Equal(new[] { 100.0, 350, 450 }, band.Lo.Select(p => p.Y)); Assert.Equal(new[] { 300.0, 450, 600 }, band.Hi.Select(p => p.Y));
            Assert.Equal(200.0, r.Polylines.Single(p => p.Role == ChartLineRole.Raw).Pts[0].Y, 9);   // a linha principal é a média
            var d = Scene(new[] { s }, ChartRepeatMode.StdDev);
            var sb = d.BandSeries.Single(); Assert.True(sb.IsStdDev);
            Assert.Equal(100.0, sb.Hi[0].Y - 200.0, 9);                                              // σ amostral de {100,200,300} = 100
            Assert.Equal(200.0 - 100.0, sb.Lo[0].Y, 9);
            Assert.True(d.MinY <= sb.Lo.Min(p => p.Y) && d.MaxY >= sb.Hi.Max(p => p.Y));              // o domínio contém a faixa
        }

        // ---------------------------------------------------------------- TESTE B
        private static ChartSeries Grid55(Random rnd, double noise = 0)
        {
            var x = new List<double>(); var y = new List<double>();
            for (int i = 0; i < 55; i++)
                for (int j = 0; j < 55; j++) { x.Add(i * 0.2 + (noise > 0 ? (rnd.NextDouble() - 0.5) * noise : 0)); y.Add(300 + 200 * Math.Sin(i / 9.0) * Math.Cos(j / 11.0) + rnd.NextDouble() * 20); }
            return S(x.ToArray(), y.ToArray());
        }

        [Fact]
        public void B_Grid55x55_NothingLost_NoArtificialPeaks_Fast()
        {
            var rnd = new Random(11);
            var s = Grid55(rnd);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var sRaw = Scene(new[] { s }, ChartRepeatMode.Raw);
            var sMean = Scene(new[] { s }, ChartRepeatMode.Mean, mode: ChartLineMode.Smooth);
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 3000, sw.ElapsedMilliseconds + " ms");
            Assert.Equal(3025, s.Count);
            Assert.Equal(55, s.Groups.Count); Assert.All(s.Groups, g => Assert.Equal(55, g.Count));
            Assert.Equal(3025, s.Groups.Sum(g => g.Count));
            // Raw: somente pontos, sem a linha em zigue-zague de 3025 vértices
            Assert.DoesNotContain(sRaw.Polylines, p => p.Role == ChartLineRole.Raw);
            Assert.Equal(3025, sRaw.Markers.Sum(m => m.Pts.Length)); Assert.True(sRaw.AnyPointsOnly);
            // Smooth sobre a série agregada: sem picos fora do intervalo das médias
            var means = s.Groups.Select(g => g.Mean).ToList();
            var smooth = sMean.Polylines.Single(p => p.Role == ChartLineRole.Smooth);
            Assert.All(smooth.Pts, p => Assert.InRange(p.Y, means.Min() - 1e-9, means.Max() + 1e-9));
        }

        // ---------------------------------------------------------------- TESTE C
        [Fact]
        public void C_NearlyEqualX_GroupedByTolerance_NotByExactEquality()
        {
            var x = new List<double>(); var y = new List<double>();
            foreach (double col in new[] { 1.5, 2.0, 2.5 })
            {
                x.Add(col); x.Add(col + 1e-9); x.Add(col - 1e-9); x.Add(col + 2e-9);
                y.AddRange(new double[] { 10, 20, 30, 40 });
            }
            var s = S(x.ToArray(), y.ToArray());
            Assert.Equal(12, s.X.Distinct().Count());                    // igualdade exata veria 12 colunas
            s.EnsureGroups(0);
            Assert.Equal(3, s.Groups.Count);                              // tolerância automática vê as 3 colunas reais
            Assert.All(s.Groups, g => Assert.Equal(4, g.Count));
            Assert.True(s.GroupToleranceAuto);
            Assert.True(s.GroupTolerance >= 1e-9 && s.GroupTolerance < 0.1);       // nunca perto do espaçamento real (0,5)
            s.EnsureGroups(1e-12);
            Assert.Equal(12, s.Groups.Count);                             // tolerância do usuário respeitada
            Assert.False(s.GroupToleranceAuto);
        }

        [Fact]
        public void C_RealColumns_AreNeverMerged_EvenWhenClose()
        {
            var s = S(new[] { 0.0, 0.001, 0.002, 0.003 }, new[] { 1.0, 2, 3, 4 });
            s.EnsureGroups(0);
            Assert.Equal(4, s.Groups.Count);                              // sem ruído: só épsilon numérico
            var rnd = new Random(2);
            var x = new List<double>(); var y = new List<double>();
            for (int c = 0; c < 20; c++) for (int k = 0; k < 6; k++) { x.Add(c * 0.01 + (rnd.NextDouble() - 0.5) * 1e-10); y.Add(c); }
            var s2 = S(x.ToArray(), y.ToArray()); s2.EnsureGroups(0);
            Assert.Equal(20, s2.Groups.Count); Assert.All(s2.Groups, g => Assert.Equal(6, g.Count));
        }

        [Fact]
        public void C_AutoTolerance_ScalesWithData()
        {
            Assert.Equal(1e-9 * 1000, ChartGrouping.AutoTolerance(new[] { 0.0, 500, 1000 }), 12);           // sem ruído: épsilon relativo
            var big = new List<double>(); for (int c = 0; c < 10; c++) { big.Add(1e6 + c * 100.0); big.Add(1e6 + c * 100.0 + 1e-6); }
            double t = ChartGrouping.AutoTolerance(big);
            Assert.InRange(t, 1e-6, 25.0);
        }

        // ---------------------------------------------------------------- TESTE D
        [Fact]
        public void D_Unsorted_PairsReorderedTogether_GroupsTraceable()
        {
            var rnd = new Random(5);
            var idx = Enumerable.Range(0, 9).OrderBy(_ => rnd.Next()).ToArray();
            var s = S(idx.Select(i => GridX[i]).ToArray(), idx.Select(i => GridLux[i]).ToArray());
            s.EnsureGroups(0);
            Assert.Equal(new[] { 200.0, 400.0, 516.6666666666666 }, s.Groups.Select(g => g.Mean), new DoubleComparer());
            // cada amostra mantém o par original: SourceIndex aponta para a posição na entrada embaralhada
            for (int i = 0; i < s.Count; i++) { Assert.Equal(GridX[idx[s.SourceIndex[i]]], s.X[i]); Assert.Equal(GridLux[idx[s.SourceIndex[i]]], s.Y[i]); }
            // rastreabilidade: união dos índices dos grupos = todas as amostras, cada uma uma vez
            var all = s.Groups.SelectMany(g => s.SourceIndex.Skip(g.Start).Take(g.Count)).OrderBy(v => v).ToList();
            Assert.Equal(Enumerable.Range(0, 9), all);
        }

        // ---------------------------------------------------------------- TESTE E
        [Fact]
        public void E_UnequalGroupSizes_GlobalMeanDiffersFromMeanOfMeans()
        {
            var x = new List<double>(); var y = new List<double>();
            void Add(double col, params double[] v) { foreach (var q in v) { x.Add(col); y.Add(q); } }
            Add(0, 100, 200);                                   // 2 amostras → média 150
            Add(1, 300, 300, 300, 300, 300);                    // 5 → 300
            Add(2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1000);            // 10 → 100
            var s = S(x.ToArray(), y.ToArray()); s.EnsureGroups(0);
            Assert.Equal(new[] { 2, 5, 10 }, s.Groups.Select(g => g.Count));
            double global = y.Average(), meanOfMeans = s.Groups.Average(g => g.Mean);
            Assert.Equal(2800.0 / 17.0, global, 9); Assert.Equal(550.0 / 3.0, meanOfMeans, 9);
            Assert.NotEqual(Math.Round(global, 3), Math.Round(meanOfMeans, 3));
            var sc = Scene(new[] { s }, ChartRepeatMode.Mean);
            Assert.Equal(global, sc.PooledMean, 9);             // a linha μ do gráfico é a média GLOBAL das amostras originais
        }

        // ---------------------------------------------------------------- TESTE F
        [Fact]
        public void F_Constant500_FlatLine_AllModes()
        {
            var x = new List<double>(); var y = new List<double>();
            for (int c = 0; c < 6; c++) for (int k = 0; k < 4; k++) { x.Add(c); y.Add(500); }
            var s = S(x.ToArray(), y.ToArray());
            foreach (var rm in new[] { ChartRepeatMode.Mean, ChartRepeatMode.Median, ChartRepeatMode.Min, ChartRepeatMode.Max, ChartRepeatMode.Range, ChartRepeatMode.StdDev })
                foreach (var lm in new[] { ChartLineMode.Raw, ChartLineMode.Smooth, ChartLineMode.Trend })
                {
                    var sc = Scene(new[] { s }, rm, mode: lm);
                    foreach (var pl in sc.Polylines) Assert.All(pl.Pts, p => Assert.Equal(500.0, p.Y, 9));
                    Assert.True(sc.MaxY > sc.MinY);
                    foreach (var b in sc.BandSeries) { Assert.All(b.Lo, p => Assert.Equal(500.0, p.Y, 9)); Assert.All(b.Hi, p => Assert.Equal(500.0, p.Y, 9)); }
                }
        }

        // ---------------------------------------------------------------- Raw / Smooth / Trend com X repetido
        [Fact]
        public void RawMode_RepeatedX_PointsOnly_SmoothAndTrendUseGroupMeans()
        {
            var s = S(GridX, GridLux);
            var raw = Scene(new[] { s }, ChartRepeatMode.Raw, mode: ChartLineMode.Raw);
            Assert.Empty(raw.Polylines);                                       // nada de zigue-zague vertical
            Assert.False(raw.Markers[0].Muted);
            var sm = Scene(new[] { s }, ChartRepeatMode.Raw, mode: ChartLineMode.Smooth);
            Assert.Single(sm.Polylines.Where(p => p.Role == ChartLineRole.Smooth));
            Assert.Equal(9, sm.Markers.Sum(m => m.Pts.Length));                // amostras preservadas
            var tr = Scene(new[] { s }, ChartRepeatMode.Mean, mode: ChartLineMode.Trend);
            Assert.DoesNotContain(tr.Polylines, p => p.Role == ChartLineRole.RawContext);
            var trend = tr.Polylines.Single(p => p.Role == ChartLineRole.Trend);
            Assert.Equal(3, trend.Pts.Length);
            Assert.All(trend.Pts, p => Assert.InRange(p.Y, 200 - 1e-9, 516.67));
        }

        [Fact]
        public void ShowSamples_Off_RemovesOriginalPoints_KeepsLineAndBand()
        {
            var sc = Scene(new[] { S(GridX, GridLux) }, ChartRepeatMode.Range, samples: false);
            Assert.Empty(sc.Markers);
            Assert.Single(sc.BandSeries); Assert.Single(sc.Polylines);
        }

        [Fact]
        public void SeriesWithoutRepeats_BehaveAsBefore_InAnyRepeatMode()
        {
            var s = S(new double[] { 0, 1, 2, 3 }, new double[] { 5, 9, 2, 7 });
            foreach (var rm in Enum.GetValues(typeof(ChartRepeatMode)).Cast<ChartRepeatMode>())
            {
                var sc = Scene(new[] { s }, rm);
                Assert.Equal(new[] { 5.0, 9, 2, 7 }, sc.Polylines.Single(p => p.Role == ChartLineRole.Raw).Pts.Select(p => p.Y));
                Assert.Empty(sc.BandSeries);
            }
        }

        [Fact]
        public void OriginalSamples_AreNeverModifiedByGroupingOrScene()
        {
            var s = S(GridX, GridLux);
            var x0 = s.X.ToArray(); var y0 = s.Y.ToArray();
            foreach (var rm in Enum.GetValues(typeof(ChartRepeatMode)).Cast<ChartRepeatMode>()) { Scene(new[] { s }, rm); Scene(new[] { s }, rm, ChartLineKind.XYBars); }
            Assert.Equal(x0, s.X); Assert.Equal(y0, s.Y); Assert.Equal(9, s.Count);
        }

        // ---------------------------------------------------------------- XY Bars
        [Fact]
        public void XYBars_Aggregated_OneBarPerX_WithWhiskers_RawKeepsSamples()
        {
            var s = S(GridX, GridLux);
            var mean = Scene(new[] { s }, ChartRepeatMode.Mean, ChartLineKind.XYBars);
            Assert.Equal(3, mean.Bars.Count);
            Assert.Equal(new[] { 200.0, 400.0, 516.6666666666666 }, mean.Bars.Select(b => b.Value), new DoubleComparer());
            Assert.Empty(mean.Whiskers);
            var range = Scene(new[] { s }, ChartRepeatMode.Range, ChartLineKind.XYBars);
            Assert.Equal(3, range.Whiskers.Count);
            Assert.Equal(new[] { 100.0, 350, 450 }, range.Whiskers.Select(w => w.Lo)); Assert.Equal(new[] { 300.0, 450, 600 }, range.Whiskers.Select(w => w.Hi));
            Assert.True(range.MaxY >= 600);
            var raw = Scene(new[] { s }, ChartRepeatMode.Raw, ChartLineKind.XYBars);
            Assert.Equal(9, raw.Bars.Count);                                  // amostras originais, explícito
            for (int i = 1; i < raw.Bars.Count; i++) Assert.True(raw.Bars[i].X0 >= raw.Bars[i - 1].X1 - 1e-12);   // sem sobreposição
        }

        [Fact]
        public void XYBars_NoisyX_StillOneSlotPerColumn()
        {
            var x = new List<double>(); var y = new List<double>();
            for (int c = 0; c < 4; c++) for (int k = 0; k < 5; k++) { x.Add(c + k * 1e-9); y.Add(10 * c + k); }
            var s = S(x.ToArray(), y.ToArray());
            var sc = Scene(new[] { s }, ChartRepeatMode.Mean, ChartLineKind.XYBars);
            Assert.Equal(4, sc.Bars.Count); Assert.Equal(0.8, sc.BarSlotWidth, 6);
        }

        // ---------------------------------------------------------------- Vários ramos
        [Fact]
        public void MultipleSeries_AreGroupedIndependently_AggregateOverlayUsesTolerance()
        {
            var a = S(new double[] { 0, 0, 1, 1 }, new double[] { 10, 30, 50, 70 }, 0);
            var b = S(new double[] { 0, 0, 1, 1 }, new double[] { 100, 300, 500, 700 }, 1);
            var sc = Scene(new[] { a, b }, ChartRepeatMode.Mean);
            Assert.Equal(2, sc.Polylines.Count(p => p.Role == ChartLineRole.Raw));
            Assert.Equal(new[] { 20.0, 60 }, sc.Polylines.First(p => p.SeriesIndex == 0 && p.Role == ChartLineRole.Raw).Pts.Select(p => p.Y));
            Assert.Equal(new[] { 200.0, 600 }, sc.Polylines.First(p => p.SeriesIndex == 1 && p.Role == ChartLineRole.Raw).Pts.Select(p => p.Y));
            Assert.Equal(new[] { 110.0, 330 }, sc.Polylines.Single(p => p.Role == ChartLineRole.Aggregate).Pts.Select(p => p.Y));
        }

        // ---------------------------------------------------------------- CSV / rastreabilidade
        [Fact]
        public void Csv_DistinguishesOriginalSamplesAndGroups()
        {
            var s = S(GridX, GridLux); s.EnsureGroups(0);
            var samples = ChartCsv.SampleRows(s).ToList(); var groups = ChartCsv.GroupRows(s).ToList();
            Assert.Equal(9, samples.Count); Assert.Equal(3, groups.Count);
            Assert.StartsWith("{0};0;0;100;0;0", samples[0]);
            Assert.EndsWith(";0|1|2", groups[0]);                              // índices originais das 3 amostras do grupo 0
            Assert.Contains(";3;200;200;100;300;100;", groups[0]);
            Assert.Equal("Series;OriginalIndex;X;Y;Group;GroupX", ChartCsv.SamplesHeader);
        }

        // ---------------------------------------------------------------- Mode tokens
        [Fact]
        public void ModeTokens_CombineKindLineAndRepeat()
        {
            Assert.True(ChartLine_Component.TryParseMode("xy mean", out ChartLineKind? k, out ChartLineMode? lm, out ChartRepeatMode? rm));
            Assert.Equal(ChartLineKind.XYBars, k); Assert.Null(lm); Assert.Equal(ChartRepeatMode.Mean, rm);
            Assert.True(ChartLine_Component.TryParseMode("smooth range", out k, out lm, out rm));
            Assert.Equal(ChartLineKind.Lines, k); Assert.Equal(ChartLineMode.Smooth, lm); Assert.Equal(ChartRepeatMode.Range, rm);
            Assert.True(ChartLine_Component.TryParseMode("median", out k, out lm, out rm)); Assert.Null(k); Assert.Equal(ChartRepeatMode.Median, rm);
            Assert.True(ChartLine_Component.TryParseMode("sd", out k, out lm, out rm)); Assert.Equal(ChartRepeatMode.StdDev, rm);
            Assert.True(ChartLine_Component.TryParseMode("samples", out k, out lm, out rm)); Assert.Equal(ChartRepeatMode.Raw, rm);
            Assert.False(ChartLine_Component.TryParseMode("banana", out k, out lm, out rm));
            Assert.False(ChartLine_Component.TryParseMode("mean banana", out k, out lm, out rm));
            Assert.True(ChartLine_Component.TryParseMode(1, out k, out lm, out rm)); Assert.Equal(ChartLineKind.Distribution, k);
            Assert.True(ChartLine_Component.TryParseMode(true, out k, out lm, out rm)); Assert.Equal(ChartLineKind.Distribution, k);
            Assert.True(ChartLine_Component.TryParseMode("raw", out k, out lm, out rm)); Assert.Equal(ChartLineKind.Lines, k); Assert.Equal(ChartLineMode.Raw, lm); Assert.Null(rm);
        }

        // ---------------------------------------------------------------- TESTE I (renderizadores) — marcadores de amostras nos dois temas
        [Fact]
        public void I_SamplePointsLandOnSamePixels_InCanvasAndExportThemes()
        {
            var s = S(GridX, GridLux);
            var scene = Scene(new[] { s }, ChartRepeatMode.Raw);        // somente pontos, não suavizados nem densos: cor exata
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
                        var px = m.P(p); Color c = bmp.GetPixel((int)Math.Round(px.X), (int)Math.Round(px.Y));
                        Assert.True(c.R == expected.R && c.G == expected.G && c.B == expected.B, $"amostra ({p.X},{p.Y}) fora da posição esperada {px}: {c}");
                    }
                }
            }
        }

        [Fact]
        public void I_Render_AggregatedRangeAndBars_DoNotThrow_AndUseSameScene()
        {
            var rnd = new Random(3); var s = Grid55(rnd);
            foreach (var kind in new[] { ChartLineKind.Lines, ChartLineKind.XYBars })
                foreach (var rm in new[] { ChartRepeatMode.Raw, ChartRepeatMode.Mean, ChartRepeatMode.Range, ChartRepeatMode.StdDev })
                {
                    var scene = Scene(new[] { s }, rm, kind);
                    var plot = new RectangleF(60, 40, 640, 380);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    using (var bmp = new Bitmap(800, 480)) using (var g = Graphics.FromImage(bmp))
                    { ChartLineRenderer.DrawPlot(g, plot, scene, ChartTheme.Light(1f), new FontFamily("Arial")); ChartLineRenderer.DrawPlot(g, plot, scene, ChartTheme.Dark(1f), new FontFamily("Arial")); }
                    sw.Stop();
                    Assert.True(sw.ElapsedMilliseconds < 3000, $"{kind}/{rm}: {sw.ElapsedMilliseconds} ms");
                }
        }

        private sealed class DoubleComparer : IEqualityComparer<double>
        {
            public bool Equals(double a, double b) => Math.Abs(a - b) < 1e-9;
            public int GetHashCode(double v) => 0;
        }
    }
}
