using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Buraqueira_Tools;
using Xunit;

namespace Glaux_Tools.Tests
{
    public class MarimekkoModelTests
    {
        private const double Tol = 1e-12;

        private static MarimekkoCategoryInput Cat(string id, double? width, params double[] seg)
            => new MarimekkoCategoryInput { Id = id, Label = "Cat " + id, WidthValue = width, SegmentValues = seg.ToList() };

        private static List<MarimekkoCategoryInput> Sample(double wa, double wb, double wc, double k = 1.0) => new List<MarimekkoCategoryInput>
        {
            Cat("{0}", wa, 20 * k, 30 * k, 50 * k),
            Cat("{1}", wb, 60 * k, 20 * k, 20 * k),
            Cat("{2}", wc, 10 * k, 40 * k, 50 * k)
        };

        [Fact]
        public void T01_Simple_Invariants()
        {
            var lay = MarimekkoLayout.Build(Sample(20, 50, 30));
            Assert.True(lay.IsValid);
            Assert.Equal(3, lay.Categories.Count);
            Assert.Equal(9, lay.Cells.Count);
            Assert.Equal(1.0, lay.Categories.Sum(c => c.NormalizedWidth), 12);
            foreach (var c in lay.Categories) Assert.Equal(1.0, c.Cells.Sum(x => x.NormalizedHeight), 12);
            Assert.True(lay.MaxInvariantError() < Tol);
            Assert.Equal(0.2, lay.Categories[0].NormalizedWidth, 12);
            Assert.Equal(0.5, lay.Categories[1].NormalizedWidth, 12);
            Assert.Equal(0.3, lay.Categories[2].NormalizedWidth, 12);
        }

        [Fact]
        public void T02_UnnormalizedWidths_SameGeometry()
        {
            var a = MarimekkoLayout.Build(Sample(20, 50, 30));
            var b = MarimekkoLayout.Build(Sample(200, 500, 300));
            for (int i = 0; i < a.Cells.Count; i++)
            {
                Assert.Equal(a.Cells[i].X0, b.Cells[i].X0, 12);
                Assert.Equal(a.Cells[i].X1, b.Cells[i].X1, 12);
                Assert.Equal(a.Cells[i].Y0, b.Cells[i].Y0, 12);
                Assert.Equal(a.Cells[i].Y1, b.Cells[i].Y1, 12);
            }
        }

        [Fact]
        public void T03_UnnormalizedSegments()
        {
            var lay = MarimekkoLayout.Build(new List<MarimekkoCategoryInput> { Cat("{0}", 1, 200, 300, 500) });
            Assert.Equal(new[] { 0.2, 0.3, 0.5 }, lay.Cells.Select(c => c.NormalizedHeight).ToArray());
            Assert.Equal(0.0, lay.Cells[0].Y0, 12);
            Assert.Equal(0.2, lay.Cells[0].Y1, 12);
            Assert.Equal(1.0, lay.Cells[2].Y1, 12);
        }

        [Fact]
        public void T04_CategoryZeroWidth_NoCellNoCrash()
        {
            var lay = MarimekkoLayout.Build(Sample(20, 0, 30));
            Assert.True(lay.IsValid);
            Assert.Equal(2, lay.Categories.Count);
            Assert.DoesNotContain(lay.Cells, c => c.CategoryId == "{1}");
            Assert.Contains(lay.Diagnostics, d => d.Severity == MarimekkoSeverity.Warning && d.Message.Contains("largura 0"));
            Assert.Equal(1.0, lay.Categories.Sum(c => c.NormalizedWidth), 12);
        }

        [Fact]
        public void T05_TotalWidthZero_IsInvalid()
        {
            var lay = MarimekkoLayout.Build(Sample(0, 0, 0));
            Assert.False(lay.IsValid);
            Assert.Empty(lay.Cells);
            Assert.Contains(lay.Diagnostics, d => d.Severity == MarimekkoSeverity.Error);
        }

        [Fact]
        public void T06_Negative_IsInvalid_NoAbs()
        {
            var w = MarimekkoLayout.Build(Sample(20, -10, 90));
            Assert.False(w.IsValid);
            Assert.Empty(w.Cells);
            Assert.Contains(w.Diagnostics, d => d.Severity == MarimekkoSeverity.Error && d.Message.Contains("negativa"));

            var s = MarimekkoLayout.Build(new List<MarimekkoCategoryInput> { Cat("{0}", 10, 20, -5, 85) });
            Assert.False(s.IsValid);
            Assert.Empty(s.Cells);
            Assert.Contains(s.Diagnostics, d => d.Severity == MarimekkoSeverity.Error && d.Message.Contains("negativo"));
        }

        [Fact]
        public void NaNAndInfinity_AreErrors()
        {
            Assert.False(MarimekkoLayout.Build(new List<MarimekkoCategoryInput> { Cat("{0}", 10, 1, double.NaN) }).IsValid);
            Assert.False(MarimekkoLayout.Build(new List<MarimekkoCategoryInput> { Cat("{0}", double.PositiveInfinity, 1, 2) }).IsValid);
        }

        [Fact]
        public void T07_IrregularTree_WarnsAndKeepsPositionalIdentity()
        {
            var lay = MarimekkoLayout.Build(new List<MarimekkoCategoryInput>
            {
                Cat("{0}", null, 1, 2, 3),
                Cat("{1}", null, 4, 5),
                Cat("{2}", null, 1, 1, 1, 1)
            });
            Assert.True(lay.IsValid);
            Assert.Equal(new[] { 3, 2, 4 }, lay.Categories.Select(c => c.Cells.Count).ToArray());
            Assert.Equal(4, lay.Segments.Count);
            Assert.Contains(lay.Diagnostics, d => d.Severity == MarimekkoSeverity.Warning && d.Message.Contains("irregular"));
            Assert.True(lay.MaxInvariantError() < Tol);
            Assert.All(lay.Categories, c => Assert.Equal(1.0, c.Cells.Sum(x => x.NormalizedHeight), 12));
        }

        [Fact]
        public void EmptyBranchAndZeroTotal_AreOmittedWithWarning()
        {
            var lay = MarimekkoLayout.Build(new List<MarimekkoCategoryInput>
            {
                Cat("{0}", 10, 1, 1), Cat("{1}", 10), Cat("{2}", 10, 0, 0), Cat("{3}", 10, 3, 1)
            });
            Assert.True(lay.IsValid);
            Assert.Equal(new[] { "{0}", "{3}" }, lay.Categories.Select(c => c.Id).ToArray());
            Assert.Equal(2, lay.Diagnostics.Count(d => d.Severity == MarimekkoSeverity.Warning && d.Message.Contains("omitida")));
        }

        [Fact]
        public void T08_ManyCells_100x20()
        {
            var rnd = new Random(3);
            var inputs = Enumerable.Range(0, 100).Select(i => Cat("{" + i + "}", 1 + rnd.NextDouble() * 99, Enumerable.Range(0, 20).Select(_ => 1 + rnd.NextDouble() * 50).ToArray())).ToList();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var lay = MarimekkoLayout.Build(inputs);
            sw.Stop();
            Assert.Equal(2000, lay.Cells.Count);
            Assert.True(lay.MaxInvariantError() < 1e-12);
            Assert.True(sw.ElapsedMilliseconds < 500, $"layout levou {sw.ElapsedMilliseconds} ms");
        }

        [Fact]
        public void T74_CellArea_EqualsWidthShareTimesSegmentShare()
        {
            var lay = MarimekkoLayout.Build(Sample(20, 50, 30));
            Assert.Equal(1.0, lay.Cells.Sum(c => c.RelativeArea), 12);
            Assert.True(lay.MaxAreaError() < Tol);
            var cell = lay.Cells.First(c => c.CategoryId == "{1}" && c.SegmentId == "S1");   // 50% da largura × 60% da altura
            Assert.Equal(0.5 * 0.6, cell.RelativeArea, 12);
            Assert.Equal(0.30, cell.RelativeArea, 12);
        }

        [Fact]
        public void ThreeValuesStaySeparate()
        {
            var lay = MarimekkoLayout.Build(new List<MarimekkoCategoryInput> { Cat("{0}", 40, 25, 75), Cat("{1}", 60, 50, 50) });
            var c = lay.Cells[0];
            Assert.Equal(0.40, c.NormalizedWidth, 12);       // participação da categoria na largura
            Assert.Equal(0.25, c.NormalizedHeight, 12);      // participação do segmento na categoria
            Assert.Equal(0.10, c.RelativeArea, 12);          // participação na área: produto, não 25%
        }

        [Fact]
        public void WidthOmitted_UsesCategoryTotal_AndOrderIsPreserved()
        {
            var lay = MarimekkoLayout.Build(new List<MarimekkoCategoryInput> { Cat("{0}", null, 10, 10), Cat("{1}", null, 60, 20), Cat("{2}", null, 5, 5) });
            Assert.Equal(new[] { "{0}", "{1}", "{2}" }, lay.Categories.Select(c => c.Id).ToArray());   // sem ordenar por largura
            Assert.Equal(20.0 / 110, lay.Categories[0].NormalizedWidth, 12);
            Assert.Equal(80.0 / 110, lay.Categories[1].NormalizedWidth, 12);
        }

        [Fact]
        public void StableKey_And_Labels()
        {
            var lay = MarimekkoLayout.Build(Sample(1, 1, 1), new[] { "Tipo A", "Tipo B", "Tipo C" });
            Assert.Equal("{2}|S3", lay.Cells.Last().StableKey);
            Assert.Equal("Tipo C", lay.Cells.Last().SegmentLabel);
            Assert.Equal(lay.Cells.Select(c => c.StableKey).Distinct().Count(), lay.Cells.Count);
        }

        [Fact]
        public void T11_Csv_SemanticAndRecomputable()
        {
            var inputs = Sample(20, 50, 30);
            inputs[0].Label = "Residencial, Norte";   // vírgula: precisa de aspas
            var lay = MarimekkoLayout.Build(inputs, new[] { "A", "B", "C" });
            string csv = MarimekkoExport.BuildCsv(lay);
            var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.StartsWith("CategoryID,CategoryLabel,CategoryWidthValue,CategoryWidthPercent,SegmentID,SegmentLabel,SegmentValue,SegmentPercent,RelativeAreaPercent", lines[0]);
            Assert.Equal(1 + 9, lines.Length);                 // uma linha por célula
            Assert.Contains("\"Residencial, Norte\"", lines[1]);

            var rows = lines.Skip(1).Select(Parse).ToList();
            double widthPct = rows.GroupBy(r => r[0]).Sum(g => double.Parse(g.First()[3], CultureInfo.InvariantCulture));
            Assert.Equal(100.0, widthPct, 6);
            foreach (var g in rows.GroupBy(r => r[0]))
                Assert.Equal(100.0, g.Sum(r => double.Parse(r[7], CultureInfo.InvariantCulture)), 6);
            Assert.Equal(100.0, rows.Sum(r => double.Parse(r[8], CultureInfo.InvariantCulture)), 6);
        }

        private static List<string> Parse(string line)
        {
            var res = new List<string>(); var sb = new System.Text.StringBuilder(); bool q = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (q) { if (c == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else q = false; } else sb.Append(c); }
                else if (c == '"') q = true;
                else if (c == ',') { res.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            res.Add(sb.ToString());
            return res;
        }
    }
}
