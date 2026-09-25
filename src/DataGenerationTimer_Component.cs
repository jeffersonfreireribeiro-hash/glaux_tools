using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class DataGenerationTimer_Component : GH_Component
    {
        private readonly Stopwatch _wallClock = new Stopwatch();
        private readonly Stopwatch _deltaWatch = new Stopwatch();
        private double _totalElapsedSec = 0.0;
        private int _cycleCount = 0;
        private double _lastDurationMs = 0.0;
        private readonly List<double> _historyMs = new List<double>();
        private DateTime _lastUpdate = DateTime.MinValue;

        public DataGenerationTimer_Component()
            : base(
                "Data Generation Stopwatch & Benchmark",
                "DataTimer",
                "Contador de tempo de geração de dados, perfilador de performance (Profiler) e cronômetro analítico.\n" +
                "- Mede o tempo exato (ms e s) gasto para calcular e gerar dados no Grasshopper.\n" +
                "- Modo Pass-Through: repassa os dados intactos e computa custo unitário (ms/item) e vazão (itens/s).\n" +
                "- Modos de Medição: Upstream Profiler (processador dos componentes a montante), Solução do Documento, Intervalo Delta e Cronômetro Contínuo.\n" +
                "- Estatísticas acumuladas: Média, Mínimo, Máximo, Desvio Padrão e Histórico para gráficos (ChartLine).",
                "Glaux Tools",
                "Automation")
        {
        }

        public override Guid ComponentGuid => new Guid("660d217d-3903-4935-b00e-e9d5dd92b8be");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Árvore ou lista de dados/geometrias sendo geradas (pass-through transparente).", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Enable / Gate", "G", "Habilita ou pausa a contagem do cronômetro (True = Ativo/Medindo, False = Pausado).", GH_ParamAccess.item, true);
            pManager.AddIntegerParameter("Mode", "M", "Modo de Medição do Tempo:\n" +
                                                      "0 = Upstream Profiler (Mede o tempo real de CPU dos componentes que geraram os dados conectados)\n" +
                                                      "1 = Solution Span (Tempo total da solução atual do documento do Grasshopper)\n" +
                                                      "2 = Delta Interval (Tempo decorrido entre cada atualização/chegada de dados — ideal para loops e timers)\n" +
                                                      "3 = Continuous Stopwatch (Cronômetro corrido acumulando tempo contínuo de sessão)", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Reset", "R", "Reseta o cronômetro, contador de ciclos e estatísticas acumuladas.", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("Max History", "Hist", "Limite de registros no histórico para cálculo de médias e plotagem gráfica (Padrão: 100).", GH_ParamAccess.item, 100);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Dados repassados intactos da entrada (Pass-Through transparente).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Last Time (ms)", "ms", "Duração da última geração de dados em milissegundos.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Last Time (s)", "s", "Duração da última geração de dados em segundos.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Total Time (s)", "Total", "Tempo total acumulado de geração de dados em segundos.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Average Time (ms)", "Avg", "Tempo médio por geração de dados em milissegundos.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Cost per Item (ms)", "Unit", "Custo médio de processamento por item gerado (ms/item).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Throughput (items/s)", "Rate", "Velocidade/taxa de geração de dados em itens por segundo.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Cycle Count", "N", "Número total de gerações / ciclos computados.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Time History (ms)", "Hist", "Lista com os tempos das últimas N gerações em milissegundos (pronto para ChartLine).", GH_ParamAccess.list);
            pManager.AddTextParameter("Report", "Rep", "Relatório analítico completo com métricas de desempenho e diagnóstico.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool reset = false;
            DA.GetData(3, ref reset);

            if (reset)
            {
                _wallClock.Reset();
                _deltaWatch.Reset();
                _totalElapsedSec = 0.0;
                _cycleCount = 0;
                _lastDurationMs = 0.0;
                _historyMs.Clear();
                _lastUpdate = DateTime.MinValue;
            }

            bool hasData = DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) && inTree != null;
            if (!hasData) inTree = new GH_Structure<IGH_Goo>();

            bool enabled = true;
            DA.GetData(1, ref enabled);

            int mode = 0;
            DA.GetData(2, ref mode);
            if (mode < 0) mode = 0;
            if (mode > 3) mode = 3;

            int maxHistory = 100;
            DA.GetData(4, ref maxHistory);
            if (maxHistory < 5) maxHistory = 5;
            if (maxHistory > 10000) maxHistory = 10000;

            int totalItems = inTree.DataCount;
            int branchCount = inTree.Branches.Count;

            double durationMs = 0.0;
            string modeName = "Upstream Profiler";

            if (enabled)
            {
                switch (mode)
                {
                    case 0: // Upstream Profiler
                        modeName = "Upstream Profiler (Tempo de CPU dos Componentes Conectados)";
                        durationMs = MeasureUpstreamProcessorTime();
                        if (durationMs <= 0.001)
                        {
                            var doc0 = OnPingDocument();
                            if (doc0 != null && doc0.SolutionSpan.TotalMilliseconds > 0)
                            {
                                durationMs = doc0.SolutionSpan.TotalMilliseconds;
                            }
                        }
                        break;

                    case 1: // Solution Span
                        modeName = "Document Solution Span (Tempo Total da Solução)";
                        var doc1 = OnPingDocument();
                        if (doc1 != null)
                        {
                            durationMs = doc1.SolutionSpan.TotalMilliseconds;
                        }
                        break;

                    case 2: // Delta Interval
                        modeName = "Delta Interval (Tempo entre Atualizações / Ciclos)";
                        if (_deltaWatch.IsRunning)
                        {
                            durationMs = _deltaWatch.Elapsed.TotalMilliseconds;
                            _deltaWatch.Restart();
                        }
                        else
                        {
                            _deltaWatch.Start();
                            durationMs = 0.0;
                        }
                        break;

                    case 3: // Continuous Stopwatch
                        modeName = "Continuous Stopwatch (Cronômetro Contínuo de Sessão)";
                        if (!_wallClock.IsRunning) _wallClock.Start();
                        durationMs = _wallClock.Elapsed.TotalMilliseconds - (_totalElapsedSec * 1000.0);
                        if (durationMs < 0) durationMs = 0;
                        break;
                }

                if (durationMs > 0.0 || mode == 2 || mode == 3)
                {
                    _lastDurationMs = durationMs;
                    _cycleCount++;
                    _totalElapsedSec += (durationMs / 1000.0);
                    _historyMs.Add(durationMs);

                    while (_historyMs.Count > maxHistory)
                    {
                        _historyMs.RemoveAt(0);
                    }
                    _lastUpdate = DateTime.Now;
                }
            }
            else
            {
                if (_wallClock.IsRunning) _wallClock.Stop();
                if (_deltaWatch.IsRunning) _deltaWatch.Stop();
                modeName = "Pausado (Enable = False)";
            }

            // Estatísticas
            double avgMs = _historyMs.Count > 0 ? _historyMs.Average() : 0.0;
            double minMs = _historyMs.Count > 0 ? _historyMs.Min() : 0.0;
            double maxMs = _historyMs.Count > 0 ? _historyMs.Max() : 0.0;
            double stdDevMs = 0.0;
            if (_historyMs.Count > 1)
            {
                double sumSq = _historyMs.Sum(v => (v - avgMs) * (v - avgMs));
                stdDevMs = Math.Sqrt(sumSq / (_historyMs.Count - 1));
            }

            double costPerItem = (totalItems > 0 && _lastDurationMs > 0) ? (_lastDurationMs / totalItems) : 0.0;
            double throughput = (_lastDurationMs > 0.001) ? (totalItems / (_lastDurationMs / 1000.0)) : 0.0;

            // Relatório formatado
            string formattedElapsed = FormatTimeSpan(TimeSpan.FromSeconds(_totalElapsedSec));
            string report = $"=== BURAQUEIRA - DATA GENERATION BENCHMARK ===\n" +
                            $"• Modo: {modeName}\n" +
                            $"• Última Geração: {_lastDurationMs:F2} ms ({(_lastDurationMs / 1000.0):F3} s)\n" +
                            $"• Tempo Total Acumulado: {formattedElapsed} ({_totalElapsedSec:F2} s)\n" +
                            $"• Ciclos Computados (N): {_cycleCount:N0}\n" +
                            $"• Média Histórica: {avgMs:F2} ms (Mín: {minMs:F2} ms | Máx: {maxMs:F2} ms | σ: ±{stdDevMs:F2} ms)\n" +
                            $"• Volume de Dados: {totalItems:N0} itens em {branchCount:N0} ramos\n" +
                            $"• Custo Unitário: {costPerItem:F4} ms/item\n" +
                            $"• Taxa de Geração (Throughput): {throughput:N0} itens/segundo\n" +
                            $"• Última Atualização: {(_lastUpdate != DateTime.MinValue ? _lastUpdate.ToString("yyyy-MM-dd HH:mm:ss.fff") : "--")}\n" +
                            $"• Estado: {(enabled ? "⏱ Ativo / Medindo" : "⏸ Pausado")}";

            // Saídas
            DA.SetDataTree(0, inTree);
            DA.SetData(1, _lastDurationMs);
            DA.SetData(2, _lastDurationMs / 1000.0);
            DA.SetData(3, _totalElapsedSec);
            DA.SetData(4, avgMs);
            DA.SetData(5, costPerItem);
            DA.SetData(6, throughput);
            DA.SetData(7, _cycleCount);
            DA.SetDataList(8, _historyMs);
            DA.SetData(9, report);

            // Mensagem no Canvas
            string timeStr = _lastDurationMs >= 1000.0 ? $"{_lastDurationMs / 1000.0:F2}s" : $"{_lastDurationMs:F1}ms";
            this.Message = $"{timeStr}\nN = {_cycleCount} ({FormatShortTime(_totalElapsedSec)})";
        }

        private double MeasureUpstreamProcessorTime()
        {
            try
            {
                if (Params.Input == null || Params.Input.Count == 0) return 0.0;
                var dataParam = Params.Input[0];
                if (dataParam.Sources == null || dataParam.Sources.Count == 0) return 0.0;

                double totalMs = 0.0;
                foreach (var src in dataParam.Sources)
                {
                    if (src == null) continue;
                    var comp = src.Attributes?.GetTopLevel?.DocObject as GH_Component;
                    if (comp != null)
                    {
                        totalMs += comp.ProcessorTime.TotalMilliseconds;
                    }
                }
                return totalMs;
            }
            catch
            {
                return 0.0;
            }
        }

        private static string FormatTimeSpan(TimeSpan t)
        {
            return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}.{t.Milliseconds:D3}";
        }

        private static string FormatShortTime(double totalSec)
        {
            TimeSpan t = TimeSpan.FromSeconds(totalSec);
            if (t.TotalHours >= 1.0)
            {
                return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
            }
            return $"{t.Minutes:D2}:{t.Seconds:D2}";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.DataGenerationTimer;
    }
}
