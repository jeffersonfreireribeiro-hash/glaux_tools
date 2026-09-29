using System;
using System.Collections.Generic;
using System.Drawing;
using Buraqueira_Tools.Data;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Xunit;

namespace Glaux_Tools.Tests
{
    /// <summary>Árvores de teste e comparação estrutural independente dos codecs.</summary>
    internal static class TestTrees
    {
        public static GH_Structure<IGH_Goo> Empty() => new GH_Structure<IGH_Goo>();

        public static GH_Structure<IGH_Goo> SingleEmptyBranch()
        {
            var t = new GH_Structure<IGH_Goo>();
            t.EnsurePath(new GH_Path(0));
            return t;
        }

        public static GH_Structure<IGH_Goo> Irregular()
        {
            var t = new GH_Structure<IGH_Goo>();
            t.AppendRange(new IGH_Goo[] { new GH_Number(1), new GH_Number(2), new GH_Number(3) }, new GH_Path(0));
            t.EnsurePath(new GH_Path(1));
            t.Append(new GH_Number(4), new GH_Path(2));
            for (int i = 0; i < 7; i++) t.Append(new GH_Integer(i), new GH_Path(5, 3));
            t.EnsurePath(new GH_Path(9, 9, 9));
            return t;
        }

        public static GH_Structure<IGH_Goo> Deep(int depth = 12)
        {
            var t = new GH_Structure<IGH_Goo>();
            var idx = new int[depth];
            for (int i = 0; i < depth; i++) idx[i] = i * 7 % 5;
            t.AppendRange(new IGH_Goo[] { new GH_String("fundo"), new GH_Number(Math.PI) }, new GH_Path(idx));
            idx[depth - 1] = 99;
            t.Append(new GH_Boolean(true), new GH_Path(idx));
            t.Append(new GH_Number(-1), new GH_Path(0));
            return t;
        }

        public static GH_Structure<IGH_Goo> WithNulls()
        {
            var t = new GH_Structure<IGH_Goo>();
            t.Append(null, new GH_Path(0));
            t.Append(new GH_Number(1), new GH_Path(0));
            t.Append(null, new GH_Path(0));
            t.Append(null, new GH_Path(1));
            return t;
        }

        public static GH_Structure<IGH_Goo> MultiType()
        {
            var t = new GH_Structure<IGH_Goo>();
            var p = new GH_Path(0, 1);
            t.Append(new GH_Number(0.1), p);
            t.Append(new GH_Number(1.0 / 3.0), p);
            t.Append(new GH_Number(double.NaN), p);
            t.Append(new GH_Number(double.PositiveInfinity), p);
            t.Append(new GH_Number(double.NegativeInfinity), p);
            t.Append(new GH_Number(-0.0), p);
            t.Append(new GH_Number(double.Epsilon), p);
            t.Append(new GH_Number(double.MaxValue), p);
            t.Append(new GH_Number(123456789012345678.0), p);
            t.Append(new GH_Integer(int.MinValue), p);
            t.Append(new GH_Integer(int.MaxValue), p);
            t.Append(new GH_Boolean(false), p);
            t.Append(new GH_String("olá, \"mundo\"\nlinha 2; ç"), p);
            t.Append(new GH_String(""), p);
            t.Append(new GH_String("  espaços  "), p);
            t.Append(new GH_String((string)null), p);
            t.Append(new GH_Point(new Point3d(1.5, -2, 1e-12)), p);
            t.Append(new GH_Vector(new Vector3d(0, 0, 1)), p);
            t.Append(new GH_Interval(new Interval(-3, 7.25)), p);
            t.Append(new GH_Colour(Color.FromArgb(128, 10, 20, 30)), p);
            t.Append(new GH_Time(new DateTime(2026, 9, 29, 10, 11, 12, DateTimeKind.Utc).AddTicks(1234567)), p);
            t.Append(new GH_Time(new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Unspecified)), p);
            t.Append(new GH_Guid(new Guid("0f8fad5b-d9cb-469f-a165-70867728950e")), p);
            t.Append(new GH_ComplexNumber(new Complex(1, -2)), p);
            t.Append(new GH_Transform(Transform.Translation(1, 2, 3)), p);
            t.Append(null, p);
            return t;
        }

        public static GH_Structure<IGH_Goo> Large(int branches, int itemsPerBranch)
        {
            var t = new GH_Structure<IGH_Goo>();
            var rnd = new Random(42);
            for (int b = 0; b < branches; b++)
            {
                var path = new GH_Path(b / 10, b % 10);
                var list = new List<IGH_Goo>(itemsPerBranch);
                for (int i = 0; i < itemsPerBranch; i++)
                {
                    list.Add(i % 5 == 0 ? (IGH_Goo)new GH_Integer(rnd.Next()) : new GH_Number(rnd.NextDouble() * 1000));
                }
                t.AppendRange(list, path);
            }
            return t;
        }

        public static IEnumerable<object[]> AllShapes()
        {
            yield return new object[] { "vazia", Empty() };
            yield return new object[] { "um ramo vazio", SingleEmptyBranch() };
            yield return new object[] { "irregular", Irregular() };
            yield return new object[] { "profunda", Deep() };
            yield return new object[] { "com nulos", WithNulls() };
            yield return new object[] { "multi-tipo", MultiType() };
            yield return new object[] { "grande", Large(50, 200) };
        }

        /// <summary>
        /// Igualdade estrutural feita direto sobre o GH_Structure (sem os codecs):
        /// mesmos caminhos na mesma ordem, mesma contagem por ramo, mesmo tipo e valor exato por item.
        /// </summary>
        public static void AssertStructurallyEqual(GH_Structure<IGH_Goo> expected, GH_Structure<IGH_Goo> actual)
        {
            Assert.Equal(expected.PathCount, actual.PathCount);
            for (int b = 0; b < expected.PathCount; b++)
            {
                Assert.Equal(expected.Paths[b].ToString(), actual.Paths[b].ToString());
                var eb = expected.Branches[b];
                var ab = actual.Branches[b];
                Assert.Equal(eb.Count, ab.Count);
                for (int i = 0; i < eb.Count; i++)
                {
                    AssertGooEqual(eb[i], ab[i], $"{expected.Paths[b]}[{i}]");
                }
            }
        }

        public static void AssertGooEqual(IGH_Goo e, IGH_Goo a, string where)
        {
            if (e == null)
            {
                Assert.True(a == null, $"{where}: esperado nulo, veio {a}");
                return;
            }
            Assert.True(a != null, $"{where}: esperado {e}, veio nulo");
            Assert.True(e.GetType() == a.GetType(), $"{where}: tipo {e.GetType().Name} virou {a.GetType().Name}");

            switch (e)
            {
                case GH_Number n:
                    Assert.True(BitConverter.DoubleToInt64Bits(n.Value) == BitConverter.DoubleToInt64Bits(((GH_Number)a).Value), $"{where}: {n.Value:R} != {((GH_Number)a).Value:R}");
                    break;
                case GH_Integer i: Assert.Equal(i.Value, ((GH_Integer)a).Value); break;
                case GH_Boolean bo: Assert.Equal(bo.Value, ((GH_Boolean)a).Value); break;
                case GH_String s: Assert.Equal(s.Value, ((GH_String)a).Value); break;
                case GH_Point p: Assert.Equal(p.Value, ((GH_Point)a).Value); break;
                case GH_Vector v: Assert.Equal(v.Value, ((GH_Vector)a).Value); break;
                case GH_Interval iv: Assert.Equal(iv.Value, ((GH_Interval)a).Value); break;
                case GH_Colour c: Assert.Equal(c.Value.ToArgb(), ((GH_Colour)a).Value.ToArgb()); break;
                case GH_Time t:
                    Assert.Equal(t.Value.Ticks, ((GH_Time)a).Value.Ticks);
                    Assert.Equal(t.Value.Kind, ((GH_Time)a).Value.Kind);
                    break;
                case GH_Guid g: Assert.Equal(g.Value, ((GH_Guid)a).Value); break;
                case GH_ComplexNumber cx:
                    Assert.Equal(cx.Value.Real, ((GH_ComplexNumber)a).Value.Real);
                    Assert.Equal(cx.Value.Imaginary, ((GH_ComplexNumber)a).Value.Imaginary);
                    break;
                case GH_Transform tr:
                    for (int r = 0; r < 4; r++)
                        for (int col = 0; col < 4; col++)
                            Assert.Equal(tr.Value[r, col], ((GH_Transform)a).Value[r, col]);
                    break;
                default:
                    Assert.Equal(e.ToString(), a.ToString());
                    break;
            }
        }
    }
}
