using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using Buraqueira_Tools.Data;
using Buraqueira_Tools.Diagnostics;
using Buraqueira_Tools.Persistence;
using Buraqueira_Tools.ProjectState;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Profiler do documento inteiro: ranking dos componentes por tempo, estatísticas por solução, memória,
    /// cache e o custo do próprio profiler, a partir dos tempos que o Grasshopper já mede. Opcionalmente grava
    /// séries de métricas no store.
    /// </summary>
    public class PillRuntimeProfiler_Component : GlauxCapsuleComponent
    {
        private ProfilerHost _host;
        private bool _lastReset;
        private long _loggedAtSolution;

        public PillRuntimeProfiler_Component()
            : base(
                "Pill Runtime Profiler",
                "PillProfiler",
                "Identifica gargalos da definição inteira sem instrumentar componentes: lê, no fim de cada solução, o tempo que o próprio Grasshopper mediu para cada componente.\n" +
                "- Ranking com último, média, mediana, p95, execuções e participação no tempo.\n" +
                "- Separa tempo dos componentes do tempo total da solução (a diferença é custo interno do Grasshopper: coleta de dados, conversões, preview).\n" +
                "- Soluções/minuto, memória (GC e processo), acertos/falhas do Pill Compute Cache e o custo medido do próprio profiler.\n" +
                "- 'Live' atualiza após cada solução (sem laço); 'Store' + 'Log' gravam a série no store (tipo 'metrics').\n" +
                "Mostra a solução anterior: o profiler calcula dentro da solução que está medindo.",
                "Diagnostics",
                "DIAG",
                ColorDiagnostics)
        {
        }

        public override Guid ComponentGuid => new Guid("7da3dffc-951c-461d-92d5-5558ecb80f04");
        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override Bitmap Icon => GlauxToolsIcons.PillRuntimeProfiler;

        internal bool Live { get; private set; }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Enabled", "On", "Liga o profiler (desligado = nenhum evento inscrito, custo zero).", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Live", "Lv", "Atualiza este componente após cada solução.", GH_ParamAccess.item, true);
            pManager.AddIntegerParameter("Window", "W", "Quantidade de soluções na janela das estatísticas.", GH_ParamAccess.item, 50);
            pManager.AddIntegerParameter("Top", "N", "Quantos componentes listar.", GH_ParamAccess.item, 15);
            pManager.AddIntegerParameter("Sort", "So", "Ordenação: 0 = tempo total na janela, 1 = média, 2 = último, 3 = p95, 4 = execuções.", GH_ParamAccess.item, 0);
            pManager.AddTextParameter("Filter", "F", "Só componentes cujo nome/categoria contém este texto.", GH_ParamAccess.item, "");
            pManager.AddBooleanParameter("Reset", "R", "Zera as estatísticas (borda False → True).", GH_ParamAccess.item, false);
            pManager.AddGenericParameter("Store", "S", "Store para gravar a série de métricas (opcional; ver Log).", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Log", "L", "Grava as métricas no store a cada 'Log Every' soluções registradas.", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("Log Every", "LE", "Intervalo de soluções entre gravações.", GH_ParamAccess.item, 1);
            for (int i = 0; i < 10; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Components", "C", "Componentes no ranking ('Apelido (Nome)').", GH_ParamAccess.list);
            pManager.AddNumberParameter("Last ms", "Last", "Tempo do último cálculo de cada componente (ms).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Mean ms", "Mean", "Média na janela (ms).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Median ms", "Med", "Mediana na janela (ms).", GH_ParamAccess.list);
            pManager.AddNumberParameter("P95 ms", "P95", "Percentil 95 na janela (ms).", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Runs", "Runs", "Execuções registradas desde o início/reset.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Share %", "%", "Participação no tempo total dos componentes na janela.", GH_ParamAccess.list);
            pManager.AddTextParameter("Ids", "Id", "InstanceGuid de cada componente (para localizar no canvas).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Solution ms", "Sol", "Tempo total da última solução registrada (ms).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Overhead ms", "Ovh", "Custo do profiler na última solução (ms).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Memory MB", "Mem", "Memória gerenciada (GC) após a última solução (MB).", GH_ParamAccess.item);
            pManager.AddTextParameter("Report", "Rp", "Resumo legível: solução, componentes, GH, frequência, memória, cache e custo do profiler.", GH_ParamAccess.item);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            ProfilerHost.Release(this);
            _host = null;
            base.RemovedFromDocument(document);
        }

        /// <summary>Chamado pelo PillHub ao fechar o documento.</summary>
        internal void Detach()
        {
            ProfilerHost.Release(this);
            _host = null;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool enabled = true, live = true, reset = false, log = false;
            int window = 50, top = 15, sort = 0, logEvery = 1;
            string filter = "";
            DA.GetData(0, ref enabled);
            DA.GetData(1, ref live);
            DA.GetData(2, ref window);
            DA.GetData(3, ref top);
            DA.GetData(4, ref sort);
            DA.GetData(5, ref filter);
            DA.GetData(6, ref reset);
            DA.GetData(8, ref log);
            DA.GetData(9, ref logEvery);
            Live = live;

            if (!enabled)
            {
                Detach();
                SetCapsule("Desligado", false, true);
                Message = "Off";
                return;
            }

            var doc = OnPingDocument();
            _host = ProfilerHost.Attach(doc, this, Math.Max(2, Math.Min(10000, window)));
            if (_host == null) return;

            bool rising = reset && !_lastReset;
            _lastReset = reset;
            if (rising)
            {
                _host.Profiler.Reset();
                _loggedAtSolution = 0;
            }

            var profiler = _host.Profiler;
            var ranking = profiler.Ranking((ProfileSort)Math.Max(0, Math.Min(4, sort)), Math.Max(1, top), filter);
            double totalWindow = profiler.TotalAttributedInWindow();

            var names = new List<string>();
            var last = new List<double>();
            var mean = new List<double>();
            var median = new List<double>();
            var p95 = new List<double>();
            var runs = new List<int>();
            var share = new List<double>();
            var ids = new List<string>();
            foreach (var p in ranking)
            {
                var s = p.Times.Summarize();
                names.Add(p.Label);
                last.Add(Round(s.Last));
                mean.Add(Round(s.Mean));
                median.Add(Round(s.Median));
                p95.Add(Round(s.P95));
                runs.Add((int)Math.Min(s.TotalCount, int.MaxValue));
                share.Add(totalWindow > 0 ? Math.Round(100.0 * s.Sum / totalWindow, 2) : 0);
                ids.Add(p.Id.ToString("D"));
            }

            var sol = profiler.LastSolution;
            var solStats = profiler.SolutionTimes.Summarize();
            var ovhStats = profiler.OverheadTimes.Summarize();
            string report = BuildReport(profiler, sol, solStats, ovhStats);

            if (profiler.RecordedSolutions == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Aguardando a próxima solução para medir (altere algo na definição).");
            }

            if (log && profiler.RecordedSolutions > 0 && profiler.RecordedSolutions - _loggedAtSolution >= Math.Max(1, logEvery))
            {
                LogMetrics(DA, doc, ranking, sol, solStats, ovhStats);
                _loggedAtSolution = profiler.RecordedSolutions;
            }

            string key = sol != null ? $"{Round(sol.SolutionMs)} ms" : "—";
            SetCapsule(key, profiler.RecordedSolutions > 0, ovhStats.Count > 0 && solStats.Mean > 0 && ovhStats.Mean / solStats.Mean > 0.05);
            Message = $"{profiler.RecordedSolutions} soluções";

            DA.SetDataList(0, names);
            DA.SetDataList(1, last);
            DA.SetDataList(2, mean);
            DA.SetDataList(3, median);
            DA.SetDataList(4, p95);
            DA.SetDataList(5, runs);
            DA.SetDataList(6, share);
            DA.SetDataList(7, ids);
            DA.SetData(8, sol != null ? Round(sol.SolutionMs) : 0);
            DA.SetData(9, sol != null ? Math.Round(sol.OverheadMs, 4) : 0);
            DA.SetData(10, Math.Round(_host.GcMegabytes, 2));
            DA.SetData(11, report);
        }

        private string BuildReport(SolutionProfiler profiler, SolutionRecord sol, StatsSummary solStats, StatsSummary ovhStats)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            if (sol == null)
            {
                sb.Append("Nenhuma solução registrada ainda.");
                return sb.ToString();
            }
            sb.AppendLine($"Última solução: {sol.SolutionMs.ToString("F2", inv)} ms | componentes: {sol.AttributedMs.ToString("F2", inv)} ms ({sol.ComponentsRun} calcularam) | Grasshopper (coleta, conversões, preview): {sol.UnattributedMs.ToString("F2", inv)} ms");
            sb.AppendLine($"Janela ({solStats.Count} soluções): média {solStats.Mean.ToString("F2", inv)} ms, mediana {solStats.Median.ToString("F2", inv)} ms, p95 {solStats.P95.ToString("F2", inv)} ms, máx {solStats.Max.ToString("F2", inv)} ms");
            sb.AppendLine($"Frequência: {profiler.SolutionsPerMinute} solução(ões) no último minuto | registradas: {profiler.RecordedSolutions}, ignoradas (só o profiler): {profiler.IgnoredSolutions}");
            sb.AppendLine($"Memória: GC {_host.GcMegabytes.ToString("F1", inv)} MB, processo {_host.WorkingSetMegabytes.ToString("F1", inv)} MB");
            sb.AppendLine($"Pill Compute Cache na última solução: {_host.CacheHitsLastSolution} acerto(s), {_host.CacheMissesLastSolution} falha(s) | total: {PillCache_Component.GlobalHits}/{PillCache_Component.GlobalMisses}");
            double pct = solStats.Mean > 0 ? 100.0 * ovhStats.Mean / solStats.Mean : 0;
            sb.Append($"Custo do profiler: {ovhStats.Mean.ToString("F3", inv)} ms por solução em média ({pct.ToString("F2", inv)}% do tempo da solução), máx {ovhStats.Max.ToString("F3", inv)} ms");
            return sb.ToString();
        }

        private void LogMetrics(IGH_DataAccess DA, GH_Document doc, List<ComponentProfile> ranking, SolutionRecord sol, StatsSummary solStats, StatsSummary ovhStats)
        {
            if (!StoreInput.TryResolve(DA, 7, doc, out var store, out bool readOnly, out string error))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Log de métricas: " + error);
                return;
            }
            if (readOnly)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Log de métricas: store somente leitura.");
                return;
            }

            var components = new GlauxTreeTable();
            for (int i = 0; i < ranking.Count; i++)
            {
                var s = ranking[i].Times.Summarize();
                components.Branches.Add(new GlauxBranch(new[] { i }, new List<GlauxValue>
                {
                    GlauxValue.FromText(ranking[i].Label),
                    GlauxValue.FromNumber(s.Last),
                    GlauxValue.FromNumber(s.Mean),
                    GlauxValue.FromNumber(s.Median),
                    GlauxValue.FromNumber(s.P95),
                    GlauxValue.FromInteger(s.TotalCount),
                    GlauxValue.FromInteger(ranking[i].LastOutputItems)
                }));
            }
            var solution = new GlauxTreeTable();
            solution.Branches.Add(new GlauxBranch(new[] { 0 }, new List<GlauxValue>
            {
                GlauxValue.FromNumber(sol.SolutionMs),
                GlauxValue.FromNumber(sol.AttributedMs),
                GlauxValue.FromNumber(sol.UnattributedMs),
                GlauxValue.FromNumber(sol.OverheadMs),
                GlauxValue.FromNumber(_host.GcMegabytes),
                GlauxValue.FromNumber(_host.WorkingSetMegabytes),
                GlauxValue.FromInteger(_host.CacheHitsLastSolution),
                GlauxValue.FromInteger(_host.CacheMissesLastSolution)
            }));

            string key = "profile:" + (PillSnapshot_Component.DocumentName(doc) is string n && n.Length > 0 ? n : "documento");
            var draft = new StoreEntryDraft(StoreKinds.Metrics, key)
                .AddTree("components", components)
                .AddTree("solution", solution);
            draft.Metadata["columns.components"] = "name,last_ms,mean_ms,median_ms,p95_ms,runs,output_items";
            draft.Metadata["columns.solution"] = "solution_ms,components_ms,grasshopper_ms,profiler_ms,gc_mb,working_set_mb,cache_hits,cache_misses";
            draft.Metadata["window"] = solStats.Count.ToString(CultureInfo.InvariantCulture);
            foreach (var kv in EnvironmentInfo.Capture(PillSnapshot_Component.DocumentName(doc), doc?.DocumentID ?? Guid.Empty)) draft.Metadata[kv.Key] = kv.Value;

            try
            {
                store.Append(draft, false, InstanceGuid);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Log de métricas falhou: {ex.Message}");
            }
        }

        private static double Round(double v) => Math.Round(v, 3);
    }
}
