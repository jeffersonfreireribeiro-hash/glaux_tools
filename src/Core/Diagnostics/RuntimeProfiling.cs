using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Buraqueira_Tools.Diagnostics
{
    /// <summary>
    /// Janela móvel de amostras com média, mediana, percentis (interpolação linear, como PERCENTIL.INC do Excel),
    /// mínimo, máximo e desvio padrão. Memória fixa: guarda só as últimas <see cref="Capacity"/> amostras.
    /// </summary>
    public sealed class RollingStats
    {
        private readonly double[] _buffer;
        private int _start;
        private int _count;

        public RollingStats(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _buffer = new double[capacity];
        }

        public int Capacity => _buffer.Length;
        public int Count => _count;

        /// <summary>Total de amostras já recebidas (inclusive as que saíram da janela).</summary>
        public long TotalCount { get; private set; }

        public double Last { get; private set; }

        public void Add(double value)
        {
            if (double.IsNaN(value)) return;
            int idx = (_start + _count) % _buffer.Length;
            _buffer[idx] = value;
            if (_count < _buffer.Length) _count++;
            else _start = (_start + 1) % _buffer.Length;
            Last = value;
            TotalCount++;
        }

        public void Clear()
        {
            _start = 0;
            _count = 0;
            TotalCount = 0;
            Last = 0;
        }

        public double[] ToArray()
        {
            var a = new double[_count];
            for (int i = 0; i < _count; i++) a[i] = _buffer[(_start + i) % _buffer.Length];
            return a;
        }

        public StatsSummary Summarize()
        {
            var s = new StatsSummary { Count = _count, TotalCount = TotalCount, Last = Last };
            if (_count == 0) return s;
            var values = ToArray();
            double sum = 0, min = double.MaxValue, max = double.MinValue;
            foreach (var v in values)
            {
                sum += v;
                if (v < min) min = v;
                if (v > max) max = v;
            }
            double mean = sum / values.Length;
            double sq = 0;
            foreach (var v in values) sq += (v - mean) * (v - mean);
            Array.Sort(values);
            s.Sum = sum;
            s.Mean = mean;
            s.Min = min;
            s.Max = max;
            s.StdDev = values.Length > 1 ? Math.Sqrt(sq / (values.Length - 1)) : 0;
            s.Median = Percentile(values, 0.5);
            s.P90 = Percentile(values, 0.90);
            s.P95 = Percentile(values, 0.95);
            s.P99 = Percentile(values, 0.99);
            return s;
        }

        /// <summary>Percentil de dados já ordenados (interpolação linear entre as posições vizinhas).</summary>
        public static double Percentile(double[] sorted, double p)
        {
            if (sorted.Length == 0) return 0;
            if (sorted.Length == 1) return sorted[0];
            double rank = p * (sorted.Length - 1);
            int lo = (int)Math.Floor(rank);
            int hi = (int)Math.Ceiling(rank);
            return sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo);
        }
    }

    public struct StatsSummary
    {
        public int Count;
        public long TotalCount;
        public double Last, Sum, Mean, Median, P90, P95, P99, Min, Max, StdDev;
    }

    /// <summary>Leitura de um objeto do documento no fim de uma solução.</summary>
    public struct ObjectSample
    {
        public Guid Id;
        public string Name;
        public string NickName;
        public string Category;

        /// <summary>Tempo de processamento que o próprio Grasshopper mediu no último cálculo do objeto.</summary>
        public double ProcessorMs;

        /// <summary>O objeto estava expirado quando a solução começou (vai calcular nesta solução).</summary>
        public bool ExpiredAtStart;

        /// <summary>Itens produzidos nas saídas.</summary>
        public int OutputItems;

        /// <summary>Faz parte do próprio profiler ou do que depende dele (não conta como trabalho da solução).</summary>
        public bool Excluded;
    }

    public sealed class ComponentProfile
    {
        internal ComponentProfile(Guid id, int window)
        {
            Id = id;
            Times = new RollingStats(window);
        }

        public Guid Id { get; }
        public string Name { get; internal set; }
        public string NickName { get; internal set; }
        public string Category { get; internal set; }
        public RollingStats Times { get; }
        public int LastOutputItems { get; internal set; }
        public DateTime LastRunUtc { get; internal set; }

        public string Label => string.IsNullOrWhiteSpace(NickName) || NickName == Name ? Name : $"{NickName} ({Name})";
    }

    public sealed class SolutionRecord
    {
        public DateTime EndUtc { get; internal set; }
        public double SolutionMs { get; internal set; }
        public double AttributedMs { get; internal set; }
        public double UnattributedMs => Math.Max(0, SolutionMs - AttributedMs);
        public int ComponentsRun { get; internal set; }
        public double OverheadMs { get; internal set; }
        public bool Recorded { get; internal set; }
    }

    public enum ProfileSort
    {
        TotalInWindow = 0,
        Mean = 1,
        Last = 2,
        P95 = 3,
        Runs = 4
    }

    /// <summary>
    /// Agrega os tempos que o Grasshopper já mede (<c>ProcessorTime</c>) por componente e por solução,
    /// sem instrumentar os componentes. Distingue tempo dos componentes (atribuído) do tempo total da solução
    /// (a diferença é custo do próprio Grasshopper: coleta de dados, conversões, preview, eventos).
    /// </summary>
    public sealed class SolutionProfiler
    {
        private readonly Dictionary<Guid, double> _lastSeenMs = new Dictionary<Guid, double>();
        private readonly Dictionary<Guid, ComponentProfile> _profiles = new Dictionary<Guid, ComponentProfile>();
        private readonly Queue<DateTime> _recentSolutions = new Queue<DateTime>();

        public SolutionProfiler(int window = 50)
        {
            Window = Math.Max(2, window);
            SolutionTimes = new RollingStats(Window);
            OverheadTimes = new RollingStats(Window);
        }

        public int Window { get; }
        public RollingStats SolutionTimes { get; }
        public RollingStats OverheadTimes { get; }
        public SolutionRecord LastSolution { get; private set; }
        public long RecordedSolutions { get; private set; }
        public long IgnoredSolutions { get; private set; }

        public IReadOnlyCollection<ComponentProfile> Profiles => _profiles.Values;

        /// <summary>
        /// Processa as amostras do fim de uma solução. Um objeto "calculou nesta solução" se estava expirado no início
        /// ou se o tempo medido pelo Grasshopper mudou (o GH zera e remede o tempo a cada cálculo).
        /// Soluções em que só o profiler e seus dependentes calcularam não são registradas (evita laço de atualização).
        /// </summary>
        public SolutionRecord Record(IList<ObjectSample> samples, double solutionMs, DateTime endUtc, double overheadMs)
        {
            var record = new SolutionRecord { EndUtc = endUtc, SolutionMs = solutionMs, OverheadMs = overheadMs };
            var ran = new List<ObjectSample>();
            foreach (var s in samples)
            {
                // Primeira leitura de um objeto é só linha de base (a menos que ele esteja expirado)
                bool changed = _lastSeenMs.TryGetValue(s.Id, out double prev) && Math.Abs(prev - s.ProcessorMs) > 1e-9;
                _lastSeenMs[s.Id] = s.ProcessorMs;
                // Tempo zero = expirado (o GH zera no ClearData) mas não calculou: bloqueado, desativado...
                if ((changed || s.ExpiredAtStart) && s.ProcessorMs > 0) ran.Add(s);
            }

            int realWork = 0;
            foreach (var s in ran)
            {
                if (!s.Excluded) realWork++;
            }
            if (realWork == 0)
            {
                IgnoredSolutions++;
                LastSolution = LastSolution ?? record;
                return record;
            }

            foreach (var s in ran)
            {
                if (s.Excluded) continue;
                if (!_profiles.TryGetValue(s.Id, out var p))
                {
                    p = new ComponentProfile(s.Id, Window);
                    _profiles[s.Id] = p;
                }
                p.Name = s.Name;
                p.NickName = s.NickName;
                p.Category = s.Category;
                p.LastOutputItems = s.OutputItems;
                p.LastRunUtc = endUtc;
                p.Times.Add(s.ProcessorMs);
                record.AttributedMs += s.ProcessorMs;
                record.ComponentsRun++;
            }

            record.Recorded = true;
            SolutionTimes.Add(solutionMs);
            OverheadTimes.Add(overheadMs);
            _recentSolutions.Enqueue(endUtc);
            while (_recentSolutions.Count > 0 && (endUtc - _recentSolutions.Peek()).TotalSeconds > 60) _recentSolutions.Dequeue();
            RecordedSolutions++;
            LastSolution = record;
            return record;
        }

        /// <summary>Soluções registradas no último minuto.</summary>
        public int SolutionsPerMinute => _recentSolutions.Count;

        public void Forget(Guid id)
        {
            _profiles.Remove(id);
            _lastSeenMs.Remove(id);
        }

        public void Reset()
        {
            _profiles.Clear();
            _lastSeenMs.Clear();
            _recentSolutions.Clear();
            SolutionTimes.Clear();
            OverheadTimes.Clear();
            LastSolution = null;
            RecordedSolutions = 0;
            IgnoredSolutions = 0;
        }

        public List<ComponentProfile> Ranking(ProfileSort sort, int top, string filter)
        {
            var list = new List<ComponentProfile>();
            foreach (var p in _profiles.Values)
            {
                if (!string.IsNullOrWhiteSpace(filter) &&
                    (p.Label ?? "").IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) < 0 &&
                    (p.Category ?? "").IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                list.Add(p);
            }

            double Key(ComponentProfile p)
            {
                var s = p.Times.Summarize();
                switch (sort)
                {
                    case ProfileSort.Mean: return s.Mean;
                    case ProfileSort.Last: return s.Last;
                    case ProfileSort.P95: return s.P95;
                    case ProfileSort.Runs: return s.TotalCount;
                    default: return s.Sum;
                }
            }

            var keys = new Dictionary<Guid, double>();
            foreach (var p in list) keys[p.Id] = Key(p);
            list.Sort((a, b) =>
            {
                int c = keys[b.Id].CompareTo(keys[a.Id]);
                return c != 0 ? c : string.CompareOrdinal(a.Label, b.Label);
            });
            if (top > 0 && list.Count > top) list.RemoveRange(top, list.Count - top);
            return list;
        }

        /// <summary>Soma dos tempos de todos os componentes na janela (base para a participação percentual).</summary>
        public double TotalAttributedInWindow()
        {
            double total = 0;
            foreach (var p in _profiles.Values) total += p.Times.Summarize().Sum;
            return total;
        }

        /// <summary>Mede o custo (ms) de uma ação — usado para reportar o custo do próprio profiler.</summary>
        public static double Measure(Action action)
        {
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds;
        }
    }
}
