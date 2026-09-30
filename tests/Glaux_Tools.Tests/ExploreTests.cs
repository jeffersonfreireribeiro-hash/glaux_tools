using System;
using System.Collections.Generic;
using System.Linq;
using Buraqueira_Tools.Explore;
using Xunit;

namespace Glaux_Tools.Tests
{
    public class ExploreTests
    {
        // ------------------------------------------------------------------ helpers

        private static DesignSpace Space(params DesignVariable[] vars) => new DesignSpace(vars);

        private static DesignVariable Cont(string name, double min, double max, double step = 0) =>
            DesignVariable.Continuous(Guid.NewGuid().ToString("D"), "Slider", name, min, max, step);

        private static DesignVariable Choice(string name, params string[] levels) =>
            DesignVariable.Choice(Guid.NewGuid().ToString("D"), "ValueList", name, levels);

        private static double[][] Evaluate(SamplePlan plan, Func<double[], double> f) =>
            plan.Values.Select(row => new[] { f(row) }).ToArray();

        // ------------------------------------------------------------------ Sobol

        // scipy.stats.qmc.Sobol(d=12, scramble=False).random(16) × 16
        private static readonly int[][] SobolRef16x12 =
        {
            new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, new[] { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8 },
            new[] { 12, 4, 4, 4, 12, 12, 4, 12, 12, 12, 12, 12 }, new[] { 4, 12, 12, 12, 4, 4, 12, 4, 4, 4, 4, 4 },
            new[] { 6, 6, 10, 14, 6, 2, 6, 14, 14, 10, 14, 6 }, new[] { 14, 14, 2, 6, 14, 10, 14, 6, 6, 2, 6, 14 },
            new[] { 10, 2, 14, 10, 10, 14, 2, 2, 2, 6, 2, 10 }, new[] { 2, 10, 6, 2, 2, 6, 10, 10, 10, 14, 10, 2 },
            new[] { 3, 5, 15, 7, 9, 5, 7, 15, 15, 5, 11, 1 }, new[] { 11, 13, 7, 15, 1, 13, 15, 7, 7, 13, 3, 9 },
            new[] { 15, 1, 11, 3, 5, 9, 3, 3, 3, 9, 7, 13 }, new[] { 7, 9, 3, 11, 13, 1, 11, 11, 11, 1, 15, 5 },
            new[] { 5, 3, 5, 9, 15, 7, 1, 1, 1, 15, 5, 7 }, new[] { 13, 11, 13, 1, 7, 15, 9, 9, 9, 7, 13, 15 },
            new[] { 9, 7, 1, 13, 3, 11, 5, 13, 13, 3, 9, 11 }, new[] { 1, 15, 9, 5, 11, 3, 13, 5, 5, 11, 1, 3 }
        };

        // scipy.stats.qmc.Sobol(d=256, scramble=False).random(64)[:, d] × 64 (dimensões altas da tabela)
        private static readonly Dictionary<int, int[]> SobolRefHighDims = new Dictionary<int, int[]>
        {
            [37] = new[] { 0, 32, 16, 48, 56, 24, 40, 8, 52, 20, 36, 4, 12, 44, 28, 60, 2, 34, 18, 50, 58, 26, 42, 10, 54, 22, 38, 6, 14, 46, 30, 62, 51, 19, 35, 3, 11, 43, 27, 59, 7, 39, 23, 55, 63, 31, 47, 15, 49, 17, 33, 1, 9, 41, 25, 57, 5, 37, 21, 53, 61, 29, 45, 13 },
            [100] = new[] { 0, 32, 16, 48, 40, 8, 56, 24, 4, 36, 20, 52, 44, 12, 60, 28, 38, 6, 54, 22, 14, 46, 30, 62, 34, 2, 50, 18, 10, 42, 26, 58, 31, 63, 15, 47, 55, 23, 39, 7, 27, 59, 11, 43, 51, 19, 35, 3, 57, 25, 41, 9, 17, 49, 1, 33, 61, 29, 45, 13, 21, 53, 5, 37 },
            [199] = new[] { 0, 32, 16, 48, 56, 24, 40, 8, 4, 36, 20, 52, 60, 28, 44, 12, 54, 22, 38, 6, 14, 46, 30, 62, 50, 18, 34, 2, 10, 42, 26, 58, 35, 3, 51, 19, 27, 59, 11, 43, 39, 7, 55, 23, 31, 63, 15, 47, 21, 53, 5, 37, 45, 13, 61, 29, 17, 49, 1, 33, 41, 9, 57, 25 },
            [255] = new[] { 0, 32, 48, 16, 24, 56, 40, 8, 28, 60, 44, 12, 4, 36, 52, 20, 50, 18, 2, 34, 42, 10, 26, 58, 46, 14, 30, 62, 54, 22, 6, 38, 37, 5, 21, 53, 61, 29, 13, 45, 57, 25, 9, 41, 33, 1, 17, 49, 23, 55, 39, 7, 15, 47, 63, 31, 11, 43, 59, 27, 19, 51, 35, 3 }
        };

        [Fact]
        public void Sobol_Unscrambled_MatchesScipyReference()
        {
            var seq = new SobolSequence(12);
            for (int i = 0; i < 16; i++)
            {
                var p = seq.Next();
                for (int j = 0; j < 12; j++) Assert.Equal(SobolRef16x12[i][j] / 16.0, p[j], 12);
            }

            var high = new SobolSequence(256);
            var pts = Enumerable.Range(0, 64).Select(_ => high.Next()).ToArray();
            foreach (var kv in SobolRefHighDims)
            {
                for (int i = 0; i < 64; i++) Assert.Equal(kv.Value[i] / 64.0, pts[i][kv.Key], 12);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData(7)]
        public void Sobol_EveryDimension_HasOnePointPerDyadicInterval(int? scramble)
        {
            // Propriedade de rede (t, m, s): os primeiros 2^m pontos caem um em cada intervalo [i/2^m, (i+1)/2^m)
            // em cada dimensão; o deslocamento digital preserva isso.
            const int m = 7, n = 1 << m, dims = 256;
            var seq = new SobolSequence(dims, scramble);
            var hits = new int[dims, n];
            for (int i = 0; i < n; i++)
            {
                var p = seq.Next();
                for (int j = 0; j < dims; j++) hits[j, (int)Math.Floor(p[j] * n)]++;
            }
            for (int j = 0; j < dims; j++)
            {
                for (int c = 0; c < n; c++) Assert.Equal(1, hits[j, c]);
            }
        }

        [Fact]
        public void Sobol_Scrambled_IsDeterministicPerSeed_AndAvoidsOrigin()
        {
            var a = new SobolSequence(5, 3).Next();
            var b = new SobolSequence(5, 3).Next();
            var c = new SobolSequence(5, 4).Next();
            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
            Assert.Contains(a, v => v > 0);
            Assert.Throws<ArgumentException>(() => new SobolSequence(257));
        }

        // ------------------------------------------------------------------ variáveis

        [Fact]
        public void SteppedVariable_EveryValueGetsAnEqualShareOfTheUnitInterval()
        {
            var v = Cont("Largura", 0, 1, 0.1); // 11 valores
            Assert.Equal(11, v.ValueCount);
            var counts = new Dictionary<double, int>();
            const int n = 11000;
            for (int i = 0; i < n; i++)
            {
                double value = v.FromUnit((i + 0.5) / n);
                counts[value] = counts.TryGetValue(value, out int c) ? c + 1 : 1;
            }
            Assert.Equal(11, counts.Count);
            Assert.All(counts.Values, c => Assert.Equal(1000, c));
            // Sem ruído de ponto flutuante (0.30000000000000004)
            Assert.Contains(0.3, counts.Keys);
            Assert.Equal(1.0, v.FromUnit(1.0));
            Assert.Equal(0.0, v.FromUnit(0.0));
        }

        [Fact]
        public void Variables_MapUnitValues_AndRoundTripDiscreteValues()
        {
            var cont = Cont("Altura", -5, 5);
            Assert.Equal(-5, cont.FromUnit(0));
            Assert.Equal(0, cont.FromUnit(0.5), 12);
            Assert.Equal(5, cont.FromUnit(1));
            Assert.Equal(0.25, cont.ToUnit(-2.5), 12);

            var even = Cont("Par", 2, 10, 2); // 2,4,6,8,10
            Assert.Equal(5, even.ValueCount);
            foreach (var value in new[] { 2.0, 4, 6, 8, 10 }) Assert.Equal(value, even.FromUnit(even.ToUnit(value)));

            var uneven = Cont("Passo que não fecha", 0, 1, 0.3); // 0, 0.3, 0.6, 0.9 (nunca passa do máximo)
            Assert.Equal(4, uneven.ValueCount);
            Assert.Equal(0.9, uneven.FromUnit(1), 12);

            var choice = Choice("Material", "Concreto", "Madeira", "Vidro");
            Assert.Equal(0, choice.FromUnit(0));
            Assert.Equal(2, choice.FromUnit(1));
            Assert.Equal("Madeira", choice.Format(choice.FromUnit(0.5)));
            Assert.Equal(1, choice.NumericValue(1));

            var numericLevels = DesignVariable.Choice("id", "Slider", "Espessura", new[] { "2", "4.5", "8" });
            Assert.True(numericLevels.NumericLevels);
            Assert.Equal(4.5, numericLevels.NumericValue(1));

            var constant = Cont("Fixo", 3, 3);
            Assert.Equal(3, constant.FromUnit(0.7));
            Assert.Equal(1, constant.ValueCount);
        }

        [Fact]
        public void RangeOverrides_Parse_Apply_AndReportProblems()
        {
            Assert.True(DesignRangeOverride.TryParse("Largura | min=2 | max=8 | step=0,5", out var o, out _));
            Assert.Equal(2, o.Min);
            Assert.Equal(8, o.Max);
            Assert.Equal(0.5, o.Step);
            Assert.True(DesignRangeOverride.TryParse("[VAR]* | levels=10; 20 ;30", out var lv, out _));
            Assert.Equal(new[] { "10", "20", "30" }, lv.Levels);
            Assert.True(DesignRangeOverride.TryParse("Rotação | off", out var off, out _));
            Assert.True(off.Exclude);

            Assert.False(DesignRangeOverride.TryParse("Largura | min=abc", out _, out string e1));
            Assert.Contains("abc", e1);
            Assert.False(DesignRangeOverride.TryParse("Largura | cor=azul", out _, out string e2));
            Assert.Contains("cor", e2);
            Assert.False(DesignRangeOverride.TryParse(" | min=1", out _, out _));
            Assert.False(DesignRangeOverride.TryParse("Largura | step=-1", out _, out _));

            var space = Space(Cont("Largura", 0, 10), Cont("[VAR] Altura", 0, 10), Cont("[VAR] Recuo", 0, 1), Choice("Material", "A", "B"), Cont("Rotação", 0, 360));
            var warnings = new List<string>();
            var rules = new[] { o, lv, off, Parse("Material | min=1"), Parse("Inexistente | max=3") };
            var result = space.WithOverrides(rules, warnings);
            Assert.Equal(4, result.Count); // Rotação saiu
            Assert.Equal(0.5, result.Variables[0].Step);
            Assert.Equal(DesignVariableType.Choice, result.Variables[1].Type);
            Assert.Equal(DesignVariableType.Choice, result.Variables[2].Type);
            Assert.Contains(warnings, w => w.Contains("Material") && w.Contains("levels"));
            Assert.Contains(warnings, w => w.Contains("Inexistente"));
            Assert.NotEqual(space.Hash, result.Hash);
        }

        private static DesignRangeOverride Parse(string line)
        {
            Assert.True(DesignRangeOverride.TryParse(line, out var o, out string error), error);
            return o;
        }

        [Fact]
        public void DesignSpace_IgnoresDuplicateIds_AndHashDependsOnlyOnDefinition()
        {
            var a = DesignVariable.Continuous("id-1", "Slider", "A", 0, 1);
            var dup = DesignVariable.Continuous("ID-1", "Slider", "A outra vez", 0, 5);
            var b = DesignVariable.Choice("id-2", "Toggle", "B", new[] { "False", "True" });
            var space = new DesignSpace(new[] { a, dup, b });
            Assert.Equal(2, space.Count);
            Assert.Equal(space.Hash, new DesignSpace(new[] { a, b }).Hash);
            Assert.NotEqual(space.Hash, new DesignSpace(new[] { b, a }).Hash);
            Assert.NotEqual(space.Hash, new DesignSpace(new[] { DesignVariable.Continuous("id-1", "Slider", "A", 0, 2), b }).Hash);
            Assert.Equal(1, space.IndexOf("b"));
        }

        [Fact]
        public void DiscreteSize_IsNullWithContinuousVariables()
        {
            Assert.Null(Space(Cont("A", 0, 1), Choice("B", "x", "y")).DiscreteSize());
            Assert.Equal(22, Space(Cont("A", 0, 1, 0.1), Choice("B", "x", "y")).DiscreteSize());
        }

        [Fact]
        public void WildcardMatch_Works()
        {
            Assert.True(DesignRangeOverride.WildcardMatch("[VAR]*", "[var] Altura"));
            Assert.True(DesignRangeOverride.WildcardMatch("Lar?ura", "Largura"));
            Assert.True(DesignRangeOverride.WildcardMatch("*", ""));
            Assert.False(DesignRangeOverride.WildcardMatch("Alt*", "Largura"));
            Assert.True(DesignRangeOverride.WildcardMatch("*ura", "Largura"));
        }

        // ------------------------------------------------------------------ amostragem

        [Fact]
        public void LatinHypercube_HasOneSamplePerStratumInEveryVariable()
        {
            var space = Space(Cont("A", 0, 1), Cont("B", 10, 20), Cont("C", -1, 1), Cont("D", 0, 100), Cont("E", 0, 1));
            foreach (int n in new[] { 1, 7, 50, 300 })
            {
                var plan = DesignSampler.Generate(space, SamplingMethod.LatinHypercube, new SamplerOptions { Count = n, Seed = 11 });
                Assert.Equal(n, plan.Count);
                for (int j = 0; j < space.Count; j++)
                {
                    var strata = plan.Unit.Select(r => (int)Math.Floor(r[j] * n)).OrderBy(x => x).ToArray();
                    Assert.Equal(Enumerable.Range(0, n), strata);
                }
            }
        }

        [Fact]
        public void LatinHypercube_Maximin_SpreadsSamplesAtLeastAsWellAsASingleDraw()
        {
            var space = Space(Cont("A", 0, 1), Cont("B", 0, 1), Cont("C", 0, 1));
            var single = DesignSampler.LatinHypercube(3, 40, new SamplerOptions { Seed = 5, MaximinCandidates = 1 });
            var best = DesignSampler.LatinHypercube(3, 40, new SamplerOptions { Seed = 5, MaximinCandidates = 50 });
            // O primeiro candidato do maximin é o mesmo desenho único (mesma semente): o escolhido nunca é pior
            Assert.True(DesignSampler.MinSquaredDistance(best) >= DesignSampler.MinSquaredDistance(single));
            var centered = DesignSampler.LatinHypercube(2, 8, new SamplerOptions { Seed = 1, Centered = true, MaximinCandidates = 1 });
            Assert.All(centered.SelectMany(r => r), u => Assert.Equal(0.5, u * 8 - Math.Floor(u * 8), 12));
        }

        [Fact]
        public void Plans_AreDeterministicPerSeed()
        {
            var space = Space(Cont("A", 0, 1), Choice("B", "x", "y", "z"));
            foreach (SamplingMethod m in Enum.GetValues(typeof(SamplingMethod)))
            {
                var o1 = new SamplerOptions { Count = 16, Seed = 42 };
                var p1 = DesignSampler.Generate(space, m, o1);
                var p2 = DesignSampler.Generate(space, m, new SamplerOptions { Count = 16, Seed = 42 });
                Assert.Equal(p1.Hash, p2.Hash);
                if (m != SamplingMethod.Grid)
                {
                    var p3 = DesignSampler.Generate(space, m, new SamplerOptions { Count = 16, Seed = 43 });
                    Assert.NotEqual(p1.Hash, p3.Hash);
                }
                Assert.All(p1.Values, row =>
                {
                    Assert.InRange(row[0], 0, 1);
                    Assert.Contains(row[1], new[] { 0.0, 1, 2 });
                });
            }
        }

        [Fact]
        public void Grid_IsAFullFactorial_WithEndpointsAndAllLevels()
        {
            var space = Space(Cont("A", 0, 10), Cont("B", 1, 5, 1), Choice("C", "x", "y"), DesignVariable.Choice("t", "Toggle", "T", new[] { "False", "True" }));
            var notes = new List<string>();
            var plan = DesignSampler.Generate(space, SamplingMethod.Grid, new SamplerOptions { Count = 100, Levels = 3 }, notes);
            Assert.Equal(3 * 3 * 2 * 2, plan.Count);
            Assert.Equal(new[] { 0.0, 5, 10 }, plan.Values.Select(r => r[0]).Distinct().OrderBy(x => x));
            Assert.Equal(new[] { 1.0, 3, 5 }, plan.Values.Select(r => r[1]).Distinct().OrderBy(x => x));
            Assert.Equal(new[] { 0.0, 1 }, plan.Values.Select(r => r[2]).Distinct().OrderBy(x => x));
            Assert.Equal(plan.Count, plan.Values.Select(r => string.Join(",", r)).Distinct().Count());

            // Sem níveis explícitos: L = ⌊N^(1/k)⌋ nas contínuas; aviso quando o total difere do pedido
            var auto = DesignSampler.Generate(Space(Cont("A", 0, 1), Cont("B", 0, 1)), SamplingMethod.Grid, new SamplerOptions { Count = 50 }, notes);
            Assert.Equal(49, auto.Count);
            Assert.Contains(notes, n => n.Contains("49"));

            Assert.Throws<ArgumentException>(() => DesignSampler.Generate(Space(Enumerable.Range(0, 8).Select(i => Cont("V" + i, 0, 1)).ToArray()), SamplingMethod.Grid, new SamplerOptions { Levels = 10 }));
        }

        [Fact]
        public void Sampler_RejectsEmptySpacesAndOversizedPlans()
        {
            Assert.Throws<ArgumentException>(() => DesignSampler.Generate(Space(), SamplingMethod.Random, new SamplerOptions()));
            Assert.Throws<ArgumentException>(() => DesignSampler.Generate(Space(Cont("A", 0, 1)), SamplingMethod.Random, new SamplerOptions { Count = 0 }));
            Assert.Throws<ArgumentException>(() => DesignSampler.Generate(Space(Cont("A", 0, 1)), SamplingMethod.Random, new SamplerOptions { Count = DesignSampler.MaxSamples + 1 }));
            var notes = new List<string>();
            DesignSampler.Generate(Space(Cont("A", 0, 1)), SamplingMethod.Sobol, new SamplerOptions { Count = 100 }, notes);
            Assert.Contains(notes, n => n.Contains("64") && n.Contains("128"));
        }

        [Fact]
        public void Morris_Trajectories_ChangeOneVariableByDeltaPerStep()
        {
            var space = Space(Cont("A", 0, 1), Cont("B", 0, 1), Cont("C", 0, 1), Cont("D", 0, 1));
            var plan = DesignSampler.Generate(space, SamplingMethod.Morris, new SamplerOptions { Count = 12, Levels = 4, Seed = 3 });
            Assert.Equal(12 * 5, plan.Count);
            double delta = 4 / (2.0 * 3);
            for (int t = 0; t < 12; t++)
            {
                var changed = new HashSet<int>();
                for (int s = 0; s < 4; s++)
                {
                    var a = plan.Unit[t * 5 + s];
                    var b = plan.Unit[t * 5 + s + 1];
                    var diff = Enumerable.Range(0, 4).Where(j => Math.Abs(a[j] - b[j]) > 1e-12).ToList();
                    Assert.Single(diff);
                    Assert.Equal(delta, Math.Abs(a[diff[0]] - b[diff[0]]), 9);
                    changed.Add(diff[0]);
                }
                Assert.Equal(4, changed.Count); // cada variável muda exatamente uma vez por trajetória
                Assert.All(plan.Unit.Skip(t * 5).Take(5).SelectMany(r => r), u => Assert.InRange(u, 0, 1));
            }
        }

        [Fact]
        public void Saltelli_RowsAreA_ABi_B()
        {
            var space = Space(Cont("A", 0, 1), Cont("B", 0, 1), Cont("C", 0, 1));
            var plan = DesignSampler.Generate(space, SamplingMethod.Saltelli, new SamplerOptions { Count = 8 });
            Assert.Equal(8 * 5, plan.Count);
            for (int r = 0; r < 8; r++)
            {
                var a = plan.Unit[r * 5];
                var b = plan.Unit[r * 5 + 4];
                for (int i = 0; i < 3; i++)
                {
                    var ab = plan.Unit[r * 5 + 1 + i];
                    for (int j = 0; j < 3; j++) Assert.Equal(j == i ? b[j] : a[j], ab[j]);
                }
            }
            // O limite de variáveis do espaço (128) garante as 2k ≤ 256 dimensões de Sobol do Saltelli
            Assert.Throws<ArgumentException>(() => Space(Enumerable.Range(0, DesignSpace.MaxVariables + 1).Select(i => Cont("V" + i, 0, 1)).ToArray()));
            Assert.Equal(2 * DesignSpace.MaxVariables, SobolDirectionNumbers.MaxDimensions);
        }

        [Fact]
        public void MethodNames_Parse()
        {
            Assert.True(SamplerOptions.TryParseMethod("Latin Hypercube", out var m1));
            Assert.Equal(SamplingMethod.LatinHypercube, m1);
            Assert.True(SamplerOptions.TryParseMethod("", out var m2));
            Assert.Equal(SamplingMethod.LatinHypercube, m2);
            Assert.True(SamplerOptions.TryParseMethod("Monte-Carlo", out var m3));
            Assert.Equal(SamplingMethod.Random, m3);
            Assert.True(SamplerOptions.TryParseMethod("grade", out var m4));
            Assert.Equal(SamplingMethod.Grid, m4);
            Assert.False(SamplerOptions.TryParseMethod("genético", out _));
            foreach (SamplingMethod m in Enum.GetValues(typeof(SamplingMethod)))
            {
                Assert.True(SamplerOptions.TryParseMethod(SamplerOptions.MethodName(m), out var back));
                Assert.Equal(m, back);
            }
        }

        // ------------------------------------------------------------------ sensibilidade

        [Fact]
        public void Correlation_OnALinearModel_RanksBySrc_WithPerfectFit()
        {
            var space = Space(Cont("A", 0, 1), Cont("B", 0, 10), Cont("C", 0, 1));
            var plan = DesignSampler.Generate(space, SamplingMethod.LatinHypercube, new SamplerOptions { Count = 200, Seed = 9 });
            var y = Evaluate(plan, x => 3 * x[0] - 0.5 * x[1] + 0 * x[2] + 7);
            var r = SensitivityAnalysis.Analyze(plan, null, null, y, SensitivityMethod.Auto, new[] { "Custo" });
            Assert.Equal(SensitivityMethod.Correlation, r.Method);
            var res = r.Results[0];
            Assert.Equal("Custo", res.Name);
            Assert.Equal(1.0, res.Fit.Value, 9);
            // SRC = coeficiente × σx / σy
            double sy = Std(y.Select(v => v[0]));
            double sa = Std(plan.Values.Select(v => v[0])), sb = Std(plan.Values.Select(v => v[1]));
            Assert.Equal(3 * sa / sy, res.Details[0][3], 6);
            Assert.Equal(-0.5 * sb / sy, res.Details[1][3], 6);
            Assert.Equal(0, res.Details[2][3], 6);
            Assert.Equal(new[] { 1, 0, 2 }, res.Ranking);
            Assert.Empty(res.Notes);
            Assert.Contains("Custo: 1. B", r.RankingLines()[0]);
        }

        private static double Std(IEnumerable<double> values)
        {
            var v = values.ToArray();
            double m = v.Average();
            return Math.Sqrt(v.Sum(x => (x - m) * (x - m)) / (v.Length - 1));
        }

        [Fact]
        public void Correlation_MatchesScipy_ForPearsonSpearmanAndPValue()
        {
            var x = new double[] { 1, 2, 2, 3, 4, 4, 4, 5, 6, 7 };
            var y = new double[] { 2, 1, 4, 3, 6, 5, 5, 8, 7, 9 };
            Assert.Equal(0.9186304243492507, SensitivityAnalysis.Pearson(x, y), 12);
            Assert.Equal(0.9383431168171102, SensitivityAnalysis.Pearson(SensitivityAnalysis.Ranks(x), SensitivityAnalysis.Ranks(y)), 12);
            Assert.Equal(0.00017369061703229489, SensitivityAnalysis.PValue(0.9186304243492507, 10), 6);
            Assert.Equal(0.004899933667068085, SensitivityAnalysis.PValue(0.5, 30), 6);
            Assert.Equal(new[] { 1, 2.5, 2.5, 4, 6, 6, 6, 8, 9, 10 }, SensitivityAnalysis.Ranks(x));
        }

        [Fact]
        public void Correlation_HandlesNaN_ConstantColumns_TooFewRuns_AndNonLinearity()
        {
            var x = Enumerable.Range(0, 30).Select(i => new double[] { i, 5, (i * 7) % 11 }).ToArray();
            var y = x.Select((r, i) => new[] { i == 3 ? double.NaN : r[0] * 2 + r[2], (r[0] - 15) * (r[0] - 15) }).ToArray();
            var res = SensitivityAnalysis.Analyze(null, x, null, y, SensitivityMethod.Correlation, variableNames: new[] { "i", "cte", "z" });
            Assert.Equal(29, res.Results[0].Used);
            Assert.True(double.IsNaN(res.Results[0].Details[1][0])); // coluna constante
            Assert.True(double.IsNaN(res.Results[0].Importance[1]));
            Assert.Equal(1, res.Results[0].Ranking.Last()); // NaN por último
            Assert.Equal(1.0, res.Results[0].Fit.Value, 9);
            Assert.Contains(res.Results[1].Notes, n => n.Contains("linear"));

            var few = SensitivityAnalysis.Analyze(null, x.Take(3).ToArray(), null, y.Take(3).Select(r => new[] { r[1] }).ToArray(), SensitivityMethod.Correlation);
            Assert.Contains(few.Results[0].Notes, n => n.Contains("regressão"));
        }

        [Fact]
        public void Morris_OnAnAdditiveModel_GivesExactEffectsAndZeroSigma()
        {
            var space = Space(Cont("A", 0, 1), Cont("B", 0, 1), Cont("C", 0, 1));
            var plan = DesignSampler.Generate(space, SamplingMethod.Morris, new SamplerOptions { Count = 15, Levels = 6, Seed = 2 });
            // y linear na coordenada unitária: EE = coeficiente exato
            var y = plan.Unit.Select(u => new[] { 4 * u[0] - 2 * u[1] + 0.5 * u[2], u[0] * u[1] }).ToArray();
            var r = SensitivityAnalysis.Analyze(plan, null, null, y, SensitivityMethod.Auto);
            Assert.Equal(SensitivityMethod.Morris, r.Method);
            var lin = r.Results[0];
            Assert.Equal(4, lin.Details[0][0], 9);
            Assert.Equal(2, lin.Details[1][0], 9);
            Assert.Equal(-2, lin.Details[1][1], 9);
            Assert.Equal(0.5, lin.Details[2][0], 9);
            Assert.All(lin.Details, d => Assert.Equal(0, d[2], 9));
            Assert.Equal(new[] { 0, 1, 2 }, lin.Ranking);
            Assert.Equal(15, lin.Used);

            // Interação A·B: σ > 0 em A e B, efeito nulo em C
            var inter = r.Results[1];
            Assert.True(inter.Details[0][2] > 0.01);
            Assert.True(inter.Details[1][2] > 0.01);
            Assert.Equal(0, inter.Details[2][0], 12);
        }

        [Fact]
        public void Morris_UsesOnlyCompleteSteps_WhenRunsAreMissing()
        {
            var space = Space(Cont("A", 0, 1), Cont("B", 0, 1));
            var plan = DesignSampler.Generate(space, SamplingMethod.Morris, new SamplerOptions { Count = 4, Seed = 1 });
            // Só as 7 primeiras execuções (2 trajetórias completas + 1 ponto da 3ª)
            var rows = Enumerable.Range(0, 7).ToList();
            var y = rows.Select(i => new[] { 3 * plan.Unit[i][0] + plan.Unit[i][1] }).ToArray();
            var r = SensitivityAnalysis.Analyze(plan, null, rows, y, SensitivityMethod.Morris);
            Assert.Equal(2, r.Results[0].Used);
            Assert.Equal(3, r.Results[0].Details[0][0], 9);
            Assert.Contains(r.Results[0].Notes, n => n.Contains("2 de 4"));
        }

        [Fact]
        public void Sobol_OnIshigami_MatchesAnalyticalIndices()
        {
            // Ishigami (a = 7, b = 0,1), xᵢ ∈ [−π, π]: S1 = [0,3139; 0,4424; 0], ST = [0,5576; 0,4424; 0,2437]
            var space = Space(Cont("x1", -Math.PI, Math.PI), Cont("x2", -Math.PI, Math.PI), Cont("x3", -Math.PI, Math.PI));
            var plan = DesignSampler.Generate(space, SamplingMethod.Saltelli, new SamplerOptions { Count = 8192, Seed = 1 });
            var y = Evaluate(plan, x => Math.Sin(x[0]) + 7 * Math.Pow(Math.Sin(x[1]), 2) + 0.1 * Math.Pow(x[2], 4) * Math.Sin(x[0]));
            var r = SensitivityAnalysis.Analyze(plan, null, null, y, SensitivityMethod.Auto);
            Assert.Equal(SensitivityMethod.Sobol, r.Method);
            var d = r.Results[0].Details;
            double[] s1 = { 0.3139, 0.4424, 0.0 }, st = { 0.5576, 0.4424, 0.2437 };
            for (int j = 0; j < 3; j++)
            {
                Assert.InRange(d[j][0], s1[j] - 0.03, s1[j] + 0.03);
                Assert.InRange(d[j][1], st[j] - 0.03, st[j] + 0.03);
            }
            Assert.Equal(new[] { 0, 1, 2 }, r.Results[0].Ranking);
            Assert.InRange(r.Results[0].Fit.Value, 0.70, 0.82); // soma dos S1 < 1: há interação x1·x3
            Assert.Contains(r.Results[0].Notes, n => n.Contains("interações"));
        }

        [Fact]
        public void Sobol_SkipsIncompleteRows_AndRequiresASaltelliPlan()
        {
            var space = Space(Cont("A", 0, 1), Cont("B", 0, 1));
            var plan = DesignSampler.Generate(space, SamplingMethod.Saltelli, new SamplerOptions { Count = 64 });
            var rows = Enumerable.Range(0, plan.Count - 2).ToList(); // última linha incompleta
            var y = rows.Select(i => new[] { plan.Unit[i][0] * 2 + plan.Unit[i][1] }).ToArray();
            var r = SensitivityAnalysis.Analyze(plan, null, rows, y, SensitivityMethod.Sobol);
            Assert.Equal(63, r.Results[0].Used);

            var lhs = DesignSampler.Generate(space, SamplingMethod.LatinHypercube, new SamplerOptions { Count = 10 });
            Assert.Throws<ArgumentException>(() => SensitivityAnalysis.Analyze(lhs, null, null, Evaluate(lhs, x => x[0]), SensitivityMethod.Sobol));
            Assert.Throws<ArgumentException>(() => SensitivityAnalysis.Analyze(lhs, null, null, Evaluate(lhs, x => x[0]), SensitivityMethod.Morris));
            Assert.Throws<ArgumentException>(() => SensitivityAnalysis.Analyze(lhs, null, new[] { 99 }, new[] { new[] { 1.0 } }, SensitivityMethod.Correlation));
        }

        // ------------------------------------------------------------------ lote

        [Fact]
        public void BatchSession_RunsResumesAndCompletes()
        {
            var s = new BatchSession();
            Assert.Equal(BatchState.Idle, s.State);
            Assert.True(s.TryStart("plan-a", 4, out _));
            Assert.Equal(BatchState.Running, s.State);
            Assert.Equal(0, s.NextIndex);
            s.Record(0, new[] { 1.0 }, new[] { 10.0 }, 100);
            s.Record(2, new[] { 3.0 }, new[] { 30.0 }, 300);
            Assert.Equal(1, s.NextIndex);
            Assert.Equal(0.5, s.Progress);
            Assert.Equal(TimeSpan.FromMilliseconds(400), s.Remaining);
            s.Pause("Run desligado");
            Assert.Equal(BatchState.Paused, s.State);
            Assert.Contains("Run desligado", s.StatusText());

            Assert.True(s.TryStart("plan-a", 4, out _));
            s.Record(1, new[] { 2.0 }, new[] { 20.0 }, 200);
            s.Record(3, new[] { 4.0 }, new[] { 40.0 }, 400);
            Assert.Equal(BatchState.Completed, s.State);
            Assert.Null(s.NextIndex);
            Assert.Equal(new[] { 0, 1, 2, 3 }, s.Runs.Select(r => r.Index));

            // Plano diferente: não descarta nada sem Reset
            Assert.False(s.TryStart("plan-b", 4, out string reason));
            Assert.Contains("Reset", reason);
            Assert.Equal(4, s.DoneCount);
            s.Reset();
            Assert.True(s.TryStart("plan-b", 2, out _));
            Assert.Equal(0, s.DoneCount);
            Assert.False(new BatchSession().TryStart("x", 0, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Record(5, null, null, 0));
        }

        [Fact]
        public void BatchSession_Serialize_RoundTrips_AndRunningBecomesPaused()
        {
            var s = new BatchSession();
            s.TryStart("hash-1", 3, out _);
            s.Record(0, new[] { 0.1, 2 }, new[] { 1.5, double.NaN }, 12.5);
            s.Record(1, new[] { 0.2, 1 }, Array.Empty<double>(), 13);
            var back = BatchSession.Deserialize(s.Serialize());
            Assert.Equal("hash-1", back.PlanHash);
            Assert.Equal(3, back.Total);
            Assert.Equal(BatchState.Paused, back.State);
            Assert.Contains("salvo", back.StopReason);
            Assert.Equal(2, back.DoneCount);
            var r0 = back.Runs.First();
            Assert.Equal(new[] { 0.1, 2 }, r0.Values);
            Assert.Equal(1.5, r0.Results[0]);
            Assert.True(double.IsNaN(r0.Results[1]));
            Assert.Equal(12.5, r0.Milliseconds);
            Assert.Empty(back.Runs.Last().Results);
            Assert.Equal(2, back.NextIndex);

            Assert.Equal(BatchState.Idle, BatchSession.Deserialize("").State);
            Assert.Equal(0, BatchSession.Deserialize("outro formato\nrun=1;2;3;4").DoneCount);
        }

        [Fact]
        public void BatchSession_FormatsDurations()
        {
            Assert.Equal("250 ms", BatchSession.FormatDuration(TimeSpan.FromMilliseconds(250)));
            Assert.Equal("12.5 s", BatchSession.FormatDuration(TimeSpan.FromSeconds(12.5)).Replace(',', '.'));
            Assert.Equal("3 min 05 s", BatchSession.FormatDuration(TimeSpan.FromSeconds(185)));
            Assert.Equal("2 h 03 min", BatchSession.FormatDuration(TimeSpan.FromMinutes(123)));
        }
    }
}
