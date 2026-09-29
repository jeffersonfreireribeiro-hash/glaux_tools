using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Buraqueira_Tools.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Glaux_Tools.Tests
{
    public class DiagnosticsTests
    {
        private readonly ITestOutputHelper _output;

        public DiagnosticsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // Implementação de referência independente (lista completa ordenada)
        private static double RefPercentile(List<double> values, double p)
        {
            var s = values.OrderBy(x => x).ToList();
            if (s.Count == 1) return s[0];
            double rank = p * (s.Count - 1);
            int lo = (int)Math.Floor(rank), hi = (int)Math.Ceiling(rank);
            return s[lo] + (s[hi] - s[lo]) * (rank - lo);
        }

        [Fact]
        public void RollingStats_MatchesReference_AndEvictsOldSamples()
        {
            var rnd = new Random(7);
            var stats = new RollingStats(100);
            var all = new List<double>();
            for (int i = 0; i < 350; i++)
            {
                double v = Math.Exp(rnd.NextDouble() * 4); // cauda longa, como tempos reais
                stats.Add(v);
                all.Add(v);
            }
            stats.Add(double.NaN); // ignorado

            var window = all.Skip(all.Count - 100).ToList();
            var s = stats.Summarize();
            Assert.Equal(100, s.Count);
            Assert.Equal(350, s.TotalCount);
            Assert.Equal(window.Last(), s.Last, 12);
            Assert.Equal(window.Average(), s.Mean, 10);
            Assert.Equal(window.Min(), s.Min);
            Assert.Equal(window.Max(), s.Max);
            Assert.Equal(RefPercentile(window, 0.5), s.Median, 10);
            Assert.Equal(RefPercentile(window, 0.9), s.P90, 10);
            Assert.Equal(RefPercentile(window, 0.95), s.P95, 10);
            Assert.Equal(RefPercentile(window, 0.99), s.P99, 10);
            double mean = window.Average();
            double sd = Math.Sqrt(window.Sum(x => (x - mean) * (x - mean)) / (window.Count - 1));
            Assert.Equal(sd, s.StdDev, 10);
        }

        [Theory]
        [InlineData(new double[] { 5 }, 0.95, 5)]
        [InlineData(new double[] { 1, 3 }, 0.5, 2)]
        [InlineData(new double[] { 1, 2, 3, 4 }, 0.5, 2.5)]
        [InlineData(new double[] { 10, 20, 30, 40, 50 }, 0.9, 46)]
        public void Percentile_EdgeCases(double[] sorted, double p, double expected)
        {
            Assert.Equal(expected, RollingStats.Percentile(sorted, p), 10);
        }

        private static ObjectSample S(Guid id, string name, double ms, bool expired = false, bool excluded = false) =>
            new ObjectSample { Id = id, Name = name, NickName = name, Category = "Teste", ProcessorMs = ms, ExpiredAtStart = expired, Excluded = excluded };

        [Fact]
        public void Profiler_DetectsWhatRan_AndSeparatesComponentFromSolutionTime()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var prof = Guid.NewGuid();
            var p = new SolutionProfiler(10);
            var t0 = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

            // 1ª leitura: linha de base; só conta quem estava expirado
            var r1 = p.Record(new[] { S(a, "Malha", 40, expired: true), S(b, "Raio", 3) }, 60, t0, 0.1);
            Assert.True(r1.Recorded);
            Assert.Equal(1, r1.ComponentsRun);
            Assert.Equal(40, r1.AttributedMs);
            Assert.Equal(20, r1.UnattributedMs);

            // 2ª: B recalculou (tempo mudou), A não
            var r2 = p.Record(new[] { S(a, "Malha", 40), S(b, "Raio", 5) }, 9, t0.AddSeconds(1), 0.1);
            Assert.Equal(1, r2.ComponentsRun);
            Assert.Equal(5, r2.AttributedMs);

            // 3ª: só o profiler (excluído) calculou → solução ignorada (sem laço de atualização)
            var r3 = p.Record(new[] { S(a, "Malha", 40), S(b, "Raio", 5), S(prof, "Profiler", 0.3, expired: true, excluded: true) }, 1, t0.AddSeconds(2), 0.1);
            Assert.False(r3.Recorded);
            Assert.Equal(1, p.IgnoredSolutions);

            // Expirado com tempo zero não conta (bloqueado/desativado)
            var r4 = p.Record(new[] { S(a, "Malha", 42, expired: true), S(b, "Raio", 0, expired: true) }, 50, t0.AddSeconds(3), 0.1);
            Assert.Equal(1, r4.ComponentsRun);

            Assert.Equal(3, p.RecordedSolutions);
            Assert.Equal(3, p.SolutionsPerMinute);
            var ranking = p.Ranking(ProfileSort.TotalInWindow, 10, null);
            Assert.Equal(new[] { "Malha", "Raio" }, ranking.Select(r => r.Name).ToArray());
            Assert.Equal(2, ranking[0].Times.Count);
            Assert.Equal(new[] { "Raio" }, p.Ranking(ProfileSort.Last, 10, "rai").Select(r => r.Name).ToArray());
            Assert.Single(p.Ranking(ProfileSort.Mean, 1, null));

            p.Reset();
            Assert.Empty(p.Profiles);
            Assert.Equal(0, p.RecordedSolutions);
        }

        /// <summary>
        /// Medição instrumentada × referência: tempos de cargas reais medidos por um cronômetro independente
        /// alimentam o profiler; as estatísticas agregadas devem ser idênticas às calculadas direto,
        /// e o custo do profiler deve ser desprezível frente ao trabalho medido.
        /// </summary>
        [Fact]
        [Trait("Category", "Benchmark")]
        public void Profiler_AgreesWithReferenceMeasurements_AndCostIsSmall()
        {
            var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
            var reference = ids.ToDictionary(id => id, _ => new List<double>());
            var p = new SolutionProfiler(50);
            var lastSeen = new Dictionary<Guid, double>();
            double profilerCostMs = 0, workMs = 0;
            var rnd = new Random(3);

            for (int solution = 0; solution < 40; solution++)
            {
                var samples = new List<ObjectSample>();
                for (int c = 0; c < ids.Length; c++)
                {
                    // Carga real: laço ocupado de duração variável, cronometrado de forma independente
                    int iterations = 20000 * (c + 1) + rnd.Next(5000);
                    var sw = Stopwatch.StartNew();
                    double acc = 0;
                    for (int i = 0; i < iterations; i++) acc += Math.Sqrt(i + acc % 3);
                    sw.Stop();
                    double ms = sw.Elapsed.TotalMilliseconds + (acc < 0 ? 1 : 0);
                    reference[ids[c]].Add(ms);
                    workMs += ms;
                    samples.Add(S(ids[c], "C" + c, ms, expired: true));
                }

                var measure = Stopwatch.StartNew();
                p.Record(samples, samples.Sum(s => s.ProcessorMs), DateTime.UtcNow, 0);
                measure.Stop();
                profilerCostMs += measure.Elapsed.TotalMilliseconds;
            }

            foreach (var id in ids)
            {
                var prof = p.Profiles.Single(x => x.Id == id).Times.Summarize();
                var refList = reference[id];
                Assert.Equal(refList.Average(), prof.Mean, 9);
                Assert.Equal(RefPercentile(refList, 0.5), prof.Median, 9);
                Assert.Equal(RefPercentile(refList, 0.95), prof.P95, 9);
                Assert.Equal(refList.Last(), prof.Last, 12);
            }

            double pct = 100.0 * profilerCostMs / workMs;
            _output.WriteLine($"Trabalho medido: {workMs:F1} ms | custo do profiler: {profilerCostMs:F3} ms ({pct:F3}%)");
            Assert.True(pct < 5, $"custo do profiler alto demais: {pct:F2}%");
        }

        [Fact]
        [Trait("Category", "Benchmark")]
        public void Profiler_RecordCost_ScalesWithDocumentSize()
        {
            foreach (int objects in new[] { 100, 1000, 5000 })
            {
                var ids = Enumerable.Range(0, objects).Select(_ => Guid.NewGuid()).ToArray();
                var p = new SolutionProfiler(50);
                var rnd = new Random(1);
                var sw = new Stopwatch();
                const int solutions = 50;
                for (int s = 0; s < solutions; s++)
                {
                    var samples = new ObjectSample[objects];
                    for (int i = 0; i < objects; i++)
                    {
                        // ~10% dos objetos recalculam a cada solução
                        samples[i] = S(ids[i], "Obj" + i, rnd.NextDouble() < 0.1 ? rnd.NextDouble() * 5 : (s == 0 ? 1 : 1), expired: false);
                    }
                    sw.Start();
                    p.Record(samples, 100, DateTime.UtcNow, 0);
                    sw.Stop();
                }
                double perSolution = sw.Elapsed.TotalMilliseconds / solutions;
                _output.WriteLine($"{objects,5} objetos: {perSolution:F3} ms por solução para agregar");
                Assert.True(perSolution < 50, $"{objects} objetos: {perSolution:F2} ms");
            }
        }
    }
}
