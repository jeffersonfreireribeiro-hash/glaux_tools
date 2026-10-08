using System.Collections.Generic;
using Buraqueira_Tools;
using Xunit;

namespace Glaux_Tools.Tests
{
    public class GlauxListMatchTests
    {
        [Theory]
        [InlineData(0, 0, -1)]   // lista vazia
        [InlineData(5, 0, -1)]
        [InlineData(-1, 3, -1)]  // índice negativo nunca lança
        [InlineData(0, 1, 0)]    // item único vale para todos
        [InlineData(9, 1, 0)]
        [InlineData(1, 3, 1)]    // correspondência por índice
        [InlineData(3, 3, 2)]    // acabou: repete o último
        [InlineData(9, 3, 2)]
        public void RepeatLast(int index, int count, int expected)
        {
            Assert.Equal(expected, GlauxListMatch.Resolve(index, count, GlauxListMatchMode.RepeatLast));
        }

        [Theory]
        [InlineData(0, 1, 0)]
        [InlineData(9, 1, 0)]
        [InlineData(3, 3, 0)]    // recomeça do primeiro
        [InlineData(4, 3, 1)]
        [InlineData(9, 3, 0)]
        [InlineData(5, 0, -1)]
        public void Cycle(int index, int count, int expected)
        {
            Assert.Equal(expected, GlauxListMatch.Resolve(index, count, GlauxListMatchMode.Cycle));
        }

        [Fact]
        public void Get_TenRowsAgainstThreeItems()
        {
            var list = new List<double> { 1.5, 2.0, 2.5 };
            var repeatLast = new List<double>();
            var cycle = new List<double>();
            for (int i = 0; i < 10; i++)
            {
                repeatLast.Add(GlauxListMatch.Get(list, i));
                cycle.Add(GlauxListMatch.Get(list, i, GlauxListMatchMode.Cycle));
            }
            Assert.Equal(new[] { 1.5, 2.0, 2.5, 2.5, 2.5, 2.5, 2.5, 2.5, 2.5, 2.5 }, repeatLast);
            Assert.Equal(new[] { 1.5, 2.0, 2.5, 1.5, 2.0, 2.5, 1.5, 2.0, 2.5, 1.5 }, cycle);
        }

        [Fact]
        public void Get_NullOrEmptyList_ReturnsDefault()
        {
            Assert.Null(GlauxListMatch.Get<string>(null, 0));
            Assert.Null(GlauxListMatch.Get(new List<string>(), 3));
        }

        [Fact]
        public void Get_NullItemDoesNotInheritFromNeighbour()
        {
            var list = new List<string> { "a", null, "c" };
            Assert.Null(GlauxListMatch.Get(list, 1));
            Assert.Equal("c", GlauxListMatch.Get(list, 2));
        }

        [Fact]
        public void Describe_OnlyWhenLengthsDiffer()
        {
            Assert.Null(GlauxListMatch.Describe("X", 0, 10, GlauxListMatchMode.RepeatLast));
            Assert.Null(GlauxListMatch.Describe("X", 1, 10, GlauxListMatchMode.RepeatLast));
            Assert.Null(GlauxListMatch.Describe("X", 10, 10, GlauxListMatchMode.RepeatLast));
            Assert.Contains("repete o último", GlauxListMatch.Describe("X", 3, 10, GlauxListMatchMode.RepeatLast));
            Assert.Contains("ciclicamente", GlauxListMatch.Describe("X", 3, 10, GlauxListMatchMode.Cycle));
            Assert.Contains("2 excedente(s)", GlauxListMatch.Describe("X", 12, 10, GlauxListMatchMode.RepeatLast));
        }
    }
}
