using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class ConvergenceWatcher_Component : GH_Component
    {
        private List<double> _previousValues = null;
        private int _stableStreak = 0;
        private int _iterationCount = 0;
        private readonly List<double> _deltaHistory = new List<double>();
        private bool _hasConverged = false;

        public ConvergenceWatcher_Component()
            : base(
                "Convergence Watcher (Stop Condition)",
                "Converge",
                "Monitora a taxa de variação (|Δx| / |x_t-1| < ε) em processos iterativos, otimizações ou solvers e emite um sinal booleano de parada (Stop) após N passos consecutivos estáveis.",
                "Glaux Tools",
                "Automation")
        {
        }

        public override Guid ComponentGuid => new Guid("4d5e6f7a-8b9c-0d1e-2f3a-4b5c6d7e8f9a");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Current Values", "X", "Valor escalar ou lista de parâmetros da iteração atual (x_t).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Epsilon (Tolerance)", "ε", "Tolerância máxima de variação para considerar o passo convergido (padrão: 1e-4).", GH_ParamAccess.item, 1e-4);
            pManager.AddIntegerParameter("Consecutive Steps", "N", "Número de iterações consecutivas abaixo de ε necessárias para declarar convergência (padrão: 3).", GH_ParamAccess.item, 3);
            pManager.AddIntegerParameter("Metric Mode", "M", "Métrica de convergência:\n0 = Variação Relativa Média (|Δx| / |x_t-1|)\n1 = Variação Absoluta Média (|Δx|)\n2 = Variação Relativa Máxima (Max |Δx_i| / |x_t-1,i|)\n3 = RMS Delta (Raiz do erro quadrático da variação)", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Reset", "R", "Reseta o histórico de iterações e o contador de convergência.", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Stop / Converged", "Stop", "True quando o processo atinge a estabilidade exigida (sinal para interromper o loop).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Current Delta (Δ)", "Δ", "Variação calculada no passo atual em relação ao passo anterior.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Stable Streak", "Streak", "Quantidade de passos consecutivos atuais abaixo da tolerância ε.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Iteration Count", "Iter", "Total de ciclos/iterações processados.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Delta History", "Hist", "Histórico das variações Δ das últimas iterações.", GH_ParamAccess.list);
            pManager.AddTextParameter("Report", "Rep", "Diagnóstico de convergência detalhado.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool reset = false;
            DA.GetData(4, ref reset);
            if (reset)
            {
                _previousValues = null;
                _stableStreak = 0;
                _iterationCount = 0;
                _deltaHistory.Clear();
                _hasConverged = false;
            }

            var currentRaw = new List<double>();
            if (!DA.GetDataList(0, currentRaw) || currentRaw == null || currentRaw.Count == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            double eps = 1e-4;
            DA.GetData(1, ref eps);
            if (eps <= 0) eps = 1e-6;

            int requiredSteps = 3;
            DA.GetData(2, ref requiredSteps);
            if (requiredSteps < 1) requiredSteps = 1;

            int mode = 0;
            DA.GetData(3, ref mode);

            _iterationCount++;

            double delta = 1.0;
            bool isBelowEps = false;

            if (_previousValues != null && _previousValues.Count > 0)
            {
                int count = Math.Min(currentRaw.Count, _previousValues.Count);
                if (count > 0)
                {
                    switch (mode)
                    {
                        case 0: // Variação Relativa Média
                            {
                                double sumRel = 0.0;
                                for (int i = 0; i < count; i++)
                                {
                                    double prev = _previousValues[i];
                                    double curr = currentRaw[i];
                                    double denom = Math.Abs(prev) > 1e-12 ? Math.Abs(prev) : 1.0;
                                    sumRel += Math.Abs(curr - prev) / denom;
                                }
                                delta = sumRel / count;
                            }
                            break;

                        case 1: // Variação Absoluta Média
                            {
                                double sumAbs = 0.0;
                                for (int i = 0; i < count; i++)
                                {
                                    sumAbs += Math.Abs(currentRaw[i] - _previousValues[i]);
                                }
                                delta = sumAbs / count;
                            }
                            break;

                        case 2: // Variação Relativa Máxima
                            {
                                double maxRel = 0.0;
                                for (int i = 0; i < count; i++)
                                {
                                    double prev = _previousValues[i];
                                    double curr = currentRaw[i];
                                    double denom = Math.Abs(prev) > 1e-12 ? Math.Abs(prev) : 1.0;
                                    double rel = Math.Abs(curr - prev) / denom;
                                    if (rel > maxRel) maxRel = rel;
                                }
                                delta = maxRel;
                            }
                            break;

                        case 3: // RMS Delta
                            {
                                double sumSq = 0.0;
                                for (int i = 0; i < count; i++)
                                {
                                    double diff = currentRaw[i] - _previousValues[i];
                                    sumSq += diff * diff;
                                }
                                delta = Math.Sqrt(sumSq / count);
                            }
                            break;
                    }

                    _deltaHistory.Add(delta);
                    if (_deltaHistory.Count > 50) _deltaHistory.RemoveAt(0);

                    isBelowEps = delta < eps;
                    if (isBelowEps)
                    {
                        _stableStreak++;
                    }
                    else
                    {
                        _stableStreak = 0;
                    }

                    if (_stableStreak >= requiredSteps)
                    {
                        _hasConverged = true;
                    }
                }
            }
            else
            {
                delta = double.NaN;
            }

            _previousValues = new List<double>(currentRaw);

            string report = $"Convergence Diagnostics (Iter #{_iterationCount}):\n---------------------------------------\nStatus: {(_hasConverged ? "CONVERGIDO (STOP)" : "EM EVOLUÇÃO")}\nΔ Atual: {(double.IsNaN(delta) ? "Primeiro passo" : delta.ToString("G5"))} (Limite ε = {eps:G3})\nSequência Estável: {_stableStreak} / {requiredSteps} passos\nMétrica: {(mode == 0 ? "Relativa Média" : (mode == 1 ? "Absoluta Média" : (mode == 2 ? "Relativa Máx" : "RMS")))}";

            DA.SetData(0, _hasConverged);
            DA.SetData(1, double.IsNaN(delta) ? 0.0 : delta);
            DA.SetData(2, _stableStreak);
            DA.SetData(3, _iterationCount);
            DA.SetDataList(4, _deltaHistory);
            DA.SetData(5, report);

            this.Message = _hasConverged ? $"🛑 CONVERGIDO\n(Iter #{_iterationCount})" : $"Δ: {(double.IsNaN(delta) ? "-" : delta.ToString("G3"))}\nStreak: {_stableStreak}/{requiredSteps}";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.ConvergenceWatcher;
    }
}
