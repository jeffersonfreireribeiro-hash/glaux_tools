using System.Globalization;
using System.Linq;
using Buraqueira_Tools;
using Xunit;

namespace Glaux_Tools.Tests
{
    public class SpatialHeatmapZScaleTests
    {
        public SpatialHeatmapZScaleTests()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

        private static ZScaleDefinition Build(double lo, double hi, double offsetB, double a = 0, double baseZ = 0, double valueRef = 0, string unit = "", int div = 5)
            => ZScaleBuilder.Build(lo, hi, div, unit, valueRef, a, offsetB, baseZ, 0, 10, 0, 10);

        [Fact]
        public void Ticks_ZeroToHundred()
        {
            var t = ZScaleBuilder.NiceTicks(0, 100, 5, out double step, out int dec);
            Assert.Equal(new[] { 0.0, 25, 50, 75, 100 }, t);
            Assert.Equal(25, step);
            Assert.Equal(0, dec);
        }

        [Fact]
        public void Ticks_NonZeroMinimum_DoNotAssumeZero()
        {
            var t = ZScaleBuilder.NiceTicks(20, 80, 5, out _, out _);
            Assert.Equal(new[] { 20.0, 40, 60, 80 }, t);
        }

        [Fact]
        public void Ticks_Negative_IncludeZero()
        {
            var t = ZScaleBuilder.NiceTicks(-10, 10, 5, out _, out _);
            Assert.Equal(new[] { -10.0, -5, 0, 5, 10 }, t);
            Assert.All(t, v => Assert.False(v == 0.0 && double.IsNegative(v))); // sem -0
        }

        [Fact]
        public void Ticks_FractionalRanges_HaveEnoughDecimals()
        {
            var t = ZScaleBuilder.NiceTicks(0, 1, 5, out _, out int dec);
            Assert.Equal(new[] { 0.0, 0.25, 0.5, 0.75, 1.0 }, t);
            Assert.Equal(2, dec);
            ZScaleBuilder.NiceTicks(0, 0.001, 5, out double step, out int dec2);
            Assert.Equal(0.00025, step, 12);
            Assert.Equal(5, dec2);
            Assert.Equal("0.00025", ZScaleBuilder.FormatValue(0.00025, dec2, ""));
        }

        [Fact]
        public void Ticks_NarrowRange_FallsBackToEndpoints()
        {
            var t = ZScaleBuilder.NiceTicks(1.234, 1.236, 5, out _, out int dec);
            Assert.True(t.Count >= 2);
            Assert.InRange(t.First(), 1.234 - 1e-9, 1.236);
            Assert.True(dec >= 3);
        }

        [Fact]
        public void Format_NoAbsurdPrecision_NoNegativeZero_WithUnit()
        {
            Assert.Equal("0", ZScaleBuilder.FormatValue(-0.0000001, 0, ""));
            Assert.Equal("1.33", ZScaleBuilder.FormatValue(1.3333333333333333, 2, ""));
            Assert.Equal("80 dB", ZScaleBuilder.FormatValue(80, 0, "dB"));
        }

        [Fact]
        public void HeightOfValue_IsDataValueNotDisplayHeight()
        {
            // dados 0..100, fator de elevação 0.1: altura 0..10, rótulos 0..100
            var s = Build(0, 100, offsetB: 0.1);
            Assert.NotNull(s);
            Assert.Equal(0.0, s.HeightOf(0), 9);
            Assert.Equal(10.0, s.HeightOf(100), 9);
            Assert.Equal(new[] { "0", "25", "50", "75", "100" }, s.Labels);
            Assert.Equal(2.5, s.HeightOf(25), 9);
        }

        [Fact]
        public void Normalized_ZBaseAndSpan()
        {
            // '0.2 To 2.5' sobre grade 20..80: valor 20 → 0.2 ; 80 → 2.5
            var s = ZScaleBuilder.Build(20, 80, 5, "", valueRef: 20, offsetA: 0.2, offsetB: (2.5 - 0.2) / 60.0, baseZ: 3, 0, 10, 0, 10);
            Assert.Equal(3.2, s.HeightOf(20), 9);
            Assert.Equal(5.5, s.HeightOf(80), 9);
        }

        [Fact]
        public void ManualDomain_ExtendsBeyondData()
        {
            var s = Build(0, 100, offsetB: 0.1, valueRef: 17); // dados 17..83, domínio manual 0..100
            Assert.Equal(new[] { 0.0, 25, 50, 75, 100 }, s.Ticks);
            Assert.True(s.HeightOf(0) < s.HeightOf(17));
        }

        [Fact]
        public void NoDeformation_ReturnsNull()
        {
            Assert.Null(Build(0, 100, offsetB: 0.0));
        }

        [Fact]
        public void AllSameValue_IsStable()
        {
            var s = Build(50, 50, offsetB: 0.1, valueRef: 50);
            Assert.NotNull(s);
            Assert.True(s.IsDegenerate);
            Assert.Single(s.Ticks);
            Assert.Equal("50", s.Labels[0]);
            Assert.True(s.Bounds.IsValid);
        }

        [Fact]
        public void NegativeFactor_KeepsOrderingOfLabels()
        {
            var s = Build(0, 100, offsetB: -0.1);
            Assert.Equal(-10.0, s.HeightOf(100), 9);
            Assert.Equal(0.0, s.HeightOf(0), 9);
        }

        [Fact]
        public void Axis_StaysBesideTheGrid_AndBoundsIncludeIt()
        {
            var s = Build(0, 100, offsetB: 0.1);
            Assert.True(s.AxisX < 0.0);          // à esquerda da borda mínima da grade
            Assert.Equal(0.0, s.AxisY);
            Assert.True(s.Bounds.Contains(s.AxisPoint(0)));
            Assert.True(s.Bounds.Contains(s.AxisPoint(100)));
        }

        [Fact]
        public void Divisions_AreClamped()
        {
            Assert.True(Build(0, 100, 0.1, div: 1).Ticks.Count >= 2);
            Assert.True(Build(0, 100, 0.1, div: 500).Ticks.Count <= 30);
        }
    }
}
