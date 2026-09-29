using System;
using System.Collections.Generic;
using System.Diagnostics;
using Buraqueira_Tools.Diagnostics;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Liga um <see cref="SolutionProfiler"/> aos eventos de solução de um documento. Existe enquanto houver
    /// pelo menos um Pill Runtime Profiler ativo no documento; sem profiler, nenhum evento fica inscrito
    /// (custo zero para quem não usa).
    /// </summary>
    internal sealed class ProfilerHost
    {
        private static readonly Dictionary<GH_Document, ProfilerHost> s_hosts = new Dictionary<GH_Document, ProfilerHost>();
        private static readonly object s_sync = new object();

        private readonly GH_Document _doc;
        private readonly HashSet<Guid> _expiredAtStart = new HashSet<Guid>();
        private readonly HashSet<PillRuntimeProfiler_Component> _owners = new HashSet<PillRuntimeProfiler_Component>();
        private double _startOverheadMs;
        private long _hitsAtStart;
        private long _missesAtStart;

        private ProfilerHost(GH_Document doc, int window)
        {
            _doc = doc;
            Profiler = new SolutionProfiler(window);
            doc.SolutionStart += OnSolutionStart;
            doc.SolutionEnd += OnSolutionEnd;
        }

        public SolutionProfiler Profiler { get; private set; }
        public double GcMegabytes { get; private set; }
        public double WorkingSetMegabytes { get; private set; }
        public long CacheHitsLastSolution { get; private set; }
        public long CacheMissesLastSolution { get; private set; }

        public static ProfilerHost Attach(GH_Document doc, PillRuntimeProfiler_Component owner, int window)
        {
            if (doc == null) return null;
            lock (s_sync)
            {
                if (!s_hosts.TryGetValue(doc, out var host))
                {
                    host = new ProfilerHost(doc, window);
                    s_hosts[doc] = host;
                }
                host._owners.Add(owner);
                if (host.Profiler.Window != Math.Max(2, window)) host.Profiler = new SolutionProfiler(window);
                return host;
            }
        }

        public static void Release(PillRuntimeProfiler_Component owner)
        {
            lock (s_sync)
            {
                var empty = new List<GH_Document>();
                foreach (var kv in s_hosts)
                {
                    if (kv.Value._owners.Remove(owner) && kv.Value._owners.Count == 0) empty.Add(kv.Key);
                }
                foreach (var doc in empty)
                {
                    doc.SolutionStart -= s_hosts[doc].OnSolutionStart;
                    doc.SolutionEnd -= s_hosts[doc].OnSolutionEnd;
                    s_hosts.Remove(doc);
                }
            }
        }

        private void OnSolutionStart(object sender, GH_SolutionEventArgs e)
        {
            var sw = Stopwatch.StartNew();
            _expiredAtStart.Clear();
            foreach (var obj in _doc.Objects)
            {
                if (obj is IGH_ActiveObject active && active.Phase == GH_SolutionPhase.Blank) _expiredAtStart.Add(obj.InstanceGuid);
            }
            _hitsAtStart = PillCache_Component.GlobalHits;
            _missesAtStart = PillCache_Component.GlobalMisses;
            _startOverheadMs = sw.Elapsed.TotalMilliseconds;
        }

        private void OnSolutionEnd(object sender, GH_SolutionEventArgs e)
        {
            var sw = Stopwatch.StartNew();
            var excluded = ExcludedClosure();
            var samples = new List<ObjectSample>(_doc.ObjectCount);
            foreach (var obj in _doc.Objects)
            {
                double ms;
                int items = 0;
                if (obj is IGH_Component comp)
                {
                    ms = comp.ProcessorTime.TotalMilliseconds;
                    foreach (var p in comp.Params.Output) items += p.VolatileDataCount;
                }
                else if (obj is IGH_Param param)
                {
                    ms = param.ProcessorTime.TotalMilliseconds;
                    items = param.VolatileDataCount;
                }
                else
                {
                    continue;
                }

                samples.Add(new ObjectSample
                {
                    Id = obj.InstanceGuid,
                    Name = obj.Name,
                    NickName = obj.NickName,
                    Category = obj.Category + "/" + obj.SubCategory,
                    ProcessorMs = ms,
                    ExpiredAtStart = _expiredAtStart.Contains(obj.InstanceGuid),
                    OutputItems = items,
                    Excluded = excluded.Contains(obj.InstanceGuid)
                });
            }

            GcMegabytes = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            WorkingSetMegabytes = Environment.WorkingSet / (1024.0 * 1024.0);
            CacheHitsLastSolution = PillCache_Component.GlobalHits - _hitsAtStart;
            CacheMissesLastSolution = PillCache_Component.GlobalMisses - _missesAtStart;

            double overhead = _startOverheadMs + sw.Elapsed.TotalMilliseconds;
            var record = Profiler.Record(samples, e.Duration.TotalMilliseconds, DateTime.UtcNow, overhead);

            if (!record.Recorded) return;
            List<PillRuntimeProfiler_Component> live;
            lock (s_sync) live = new List<PillRuntimeProfiler_Component>(_owners);
            live.RemoveAll(o => !o.Live);
            if (live.Count == 0 || _doc.Context == GH_DocumentContext.Close) return;

            // Atualiza só o profiler (e o que depende dele) numa solução curta; essa solução não é registrada,
            // então não gera nova atualização (sem laço)
            try
            {
                _doc.ScheduleSolution(200, d =>
                {
                    foreach (var o in live) o.ExpireSolution(false);
                });
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>O profiler e tudo que depende dele não contam como trabalho da solução.</summary>
        private HashSet<Guid> ExcludedClosure()
        {
            var result = new HashSet<Guid>();
            var queue = new Queue<IGH_DocumentObject>();
            lock (s_sync)
            {
                foreach (var o in _owners) queue.Enqueue(o);
            }
            while (queue.Count > 0)
            {
                var obj = queue.Dequeue();
                if (obj == null || !result.Add(obj.InstanceGuid)) continue;
                IEnumerable<IGH_Param> outputs;
                if (obj is IGH_Component c) outputs = c.Params.Output;
                else if (obj is IGH_Param p) outputs = new[] { p };
                else continue;
                foreach (var output in outputs)
                {
                    foreach (var recipient in output.Recipients)
                    {
                        var top = recipient.Attributes?.GetTopLevel?.DocObject;
                        if (top != null && !result.Contains(top.InstanceGuid)) queue.Enqueue(top);
                    }
                }
            }
            return result;
        }
    }
}
