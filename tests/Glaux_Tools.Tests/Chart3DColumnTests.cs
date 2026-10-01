using System;
using System.Drawing;
using Buraqueira_Tools;
using Grasshopper.Kernel.Data;
using Xunit;

namespace Glaux_Tools.Tests
{
    public class Chart3DColumnTests
    {
        [Fact]
        public void Chart3DCell_PropertiesAndState()
        {
            var cell = new Chart3DCell
            {
                XIndex = 2,
                YIndex = 1,
                BranchPath = new GH_Path(1),
                RawValue = 42.5,
                DisplayHeight = 4.25,
                XLabel = "500 Hz",
                YLabel = "Paredes",
                CellColor = Color.FromArgb(0, 220, 255)
            };

            Assert.Equal(2, cell.XIndex);
            Assert.Equal(1, cell.YIndex);
            Assert.Equal(42.5, cell.RawValue);
            Assert.Equal(4.25, cell.DisplayHeight);
            Assert.Equal("500 Hz", cell.XLabel);
            Assert.Equal("Paredes", cell.YLabel);
            Assert.Equal(Color.FromArgb(0, 220, 255), cell.CellColor);
        }

        [Fact]
        public void Chart3DColorMode_EnumValues()
        {
            Assert.Equal(0, (int)Chart3DColorMode.SeriesPalette);
            Assert.Equal(1, (int)Chart3DColorMode.ValueGradient);
        }

        [Fact]
        public void Icon_LoadsVectorGfxWithoutException()
        {
            var icon = GlauxToolsIcons.Chart3DColumn;
            Assert.NotNull(icon);
            Assert.Equal(24, icon.Width);
            Assert.Equal(24, icon.Height);
        }
    }
}
