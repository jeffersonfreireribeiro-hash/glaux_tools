using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class OutlierDetection_Component : GH_Component
    {
        public OutlierDetection_Component()
            : base(
                "Outlier Filter",
                "Outliers",
                "Detecta, separa e filtra valores aberrantes (outliers) utilizando Intervalo Interquartil (IQR / Tukey), Z-Score (|z| > limite) ou Z-Score Modificado por MAD.",
                "Glaux Tools",
                "Transform")
        {
        }

        public override Guid ComponentGuid => new Guid("9b0c1d2e-3f4a-5b6c-7d8e-9f0a1b2c3d4e");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Values", "V", "Conjunto de dados numéricos a inspecionar.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Method", "M", "Método de detecção de Outliers:\n0 = IQR (Tukey's Fences: [Q1 - k*IQR, Q3 + k*IQR])\n1 = Z-Score (|z| > Limite)\n2 = Z-Score Modificado (Baseado na Mediana / MAD)", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Threshold", "T", "Multiplicador / Limite de sensibilidade (padrão: 1.5 para IQR, 3.0 para Z-Score, 3.5 para MAD).", GH_ParamAccess.item, 1.5);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Clean Data", "C", "Valores inliers (limpos, sem os outliers) mantendo a ordem original.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Outliers", "O", "Lista com apenas os valores aberrantes detectados.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Clean Mask", "M", "Máscara booleana do tamanho da lista original (True = Inlier, False = Outlier).", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Clean Indices", "iC", "Índices originais dos valores limpos.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Outlier Indices", "iO", "Índices originais dos valores aberrantes.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Lower Bound", "L", "Limite inferior de corte aceitável.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Upper Bound", "U", "Limite superior de corte aceitável.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Outlier Count", "N_out", "Total de outliers identificados.", GH_ParamAccess.item);
            pManager.AddTextParameter("Report", "Rep", "Resumo detalhado com percentis, quartis e taxa de anomalia.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rawList = new List<double>();
            if (!DA.GetDataList(0, rawList) || rawList == null || rawList.Count == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            int method = 0;
            DA.GetData(1, ref method);
            double threshold = 1.5;
            bool thresholdSet = DA.GetData(2, ref threshold);

            // Se o usuário não definiu limite manualmente, ajustamos para o padrão estatístico do método
            if (!thresholdSet)
            {
                if (method == 1) threshold = 3.0;
                else if (method == 2) threshold = 3.5;
                else threshold = 1.5;
            }

            int n = rawList.Count;
            var validVals = new List<double>();
            var validIndices = new List<int>();

            for (int i = 0; i < n; i++)
            {
                double v = rawList[i];
                if (!double.IsNaN(v) && !double.IsInfinity(v))
                {
                    validVals.Add(v);
                    validIndices.Add(i);
                }
            }

            int numValid = validVals.Count;
            if (numValid < 3)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Amostra insuficiente para detecção robusta de outliers (mínimo 3 números).");
                this.Message = "N < 3";
                return;
            }

            var cleanData = new List<double>();
            var outliers = new List<double>();
            var cleanIndices = new List<int>();
            var outlierIndices = new List<int>();
            var mask = new List<bool>(new bool[n]);

            double lowerBound = double.MinValue;
            double upperBound = double.MaxValue;
            string report = "";

            if (method == 0) // IQR (Tukey's Fences)
            {
                var sorted = new List<double>(validVals);
                sorted.Sort();

                double q1 = CalculatePercentile(sorted, 0.25);
                double q2 = CalculatePercentile(sorted, 0.50);
                double q3 = CalculatePercentile(sorted, 0.75);
                double iqr = q3 - q1;

                lowerBound = q1 - threshold * iqr;
                upperBound = q3 + threshold * iqr;

                for (int i = 0; i < n; i++)
                {
                    double v = rawList[i];
                    if (double.IsNaN(v) || double.IsInfinity(v))
                    {
                        mask[i] = false;
                        outliers.Add(v);
                        outlierIndices.Add(i);
                    }
                    else if (v < lowerBound || v > upperBound)
                    {
                        mask[i] = false;
                        outliers.Add(v);
                        outlierIndices.Add(i);
                    }
                    else
                    {
                        mask[i] = true;
                        cleanData.Add(v);
                        cleanIndices.Add(i);
                    }
                }

                report = $"IQR Method (k = {threshold:G3}):\nQ1 (25%): {q1:G4} | Mediana (50%): {q2:G4} | Q3 (75%): {q3:G4}\nIQR: {iqr:G4} | Fences: [{lowerBound:G4}, {upperBound:G4}]\nOutliers: {outliers.Count} / {n} ({((double)outliers.Count / n * 100.0):F1}%)";
                this.Message = $"IQR (k={threshold:G2})\n{outliers.Count} Outliers ({((double)outliers.Count / n * 100.0):F1}%)";
            }
            else if (method == 1) // Z-Score Thresholding (|z| > T)
            {
                double sum = 0.0;
                for (int i = 0; i < numValid; i++) sum += validVals[i];
                double mean = sum / numValid;

                double sumSq = 0.0;
                for (int i = 0; i < numValid; i++)
                {
                    double diff = validVals[i] - mean;
                    sumSq += diff * diff;
                }
                double stdDev = Math.Sqrt(sumSq / (numValid - 1));

                lowerBound = mean - threshold * stdDev;
                upperBound = mean + threshold * stdDev;

                for (int i = 0; i < n; i++)
                {
                    double v = rawList[i];
                    if (double.IsNaN(v) || double.IsInfinity(v))
                    {
                        mask[i] = false;
                        outliers.Add(v);
                        outlierIndices.Add(i);
                    }
                    else if (stdDev > 1e-15 && Math.Abs((v - mean) / stdDev) > threshold)
                    {
                        mask[i] = false;
                        outliers.Add(v);
                        outlierIndices.Add(i);
                    }
                    else
                    {
                        mask[i] = true;
                        cleanData.Add(v);
                        cleanIndices.Add(i);
                    }
                }

                report = $"Z-Score Method (|z| > {threshold:G3}):\nMédia μ: {mean:G4} | Desvio Padrão σ: {stdDev:G4}\nFaixa válida: [{lowerBound:G4}, {upperBound:G4}]\nOutliers: {outliers.Count} / {n} ({((double)outliers.Count / n * 100.0):F1}%)";
                this.Message = $"Z-Score (|z|>{threshold:G2})\n{outliers.Count} Outliers";
            }
            else if (method == 2) // Modified Z-Score (MAD)
            {
                var sorted = new List<double>(validVals);
                sorted.Sort();
                double median = CalculatePercentile(sorted, 0.50);

                var absDevs = new List<double>(numValid);
                for (int i = 0; i < numValid; i++)
                {
                    absDevs.Add(Math.Abs(validVals[i] - median));
                }
                absDevs.Sort();
                double mad = CalculatePercentile(absDevs, 0.50);
                if (mad < 1e-15) mad = 1e-6; // Proteção contra divisão por zero em dados idênticos

                lowerBound = median - (threshold * mad) / 0.6745;
                upperBound = median + (threshold * mad) / 0.6745;

                for (int i = 0; i < n; i++)
                {
                    double v = rawList[i];
                    if (double.IsNaN(v) || double.IsInfinity(v))
                    {
                        mask[i] = false;
                        outliers.Add(v);
                        outlierIndices.Add(i);
                    }
                    else
                    {
                        double modZ = (0.6745 * Math.Abs(v - median)) / mad;
                        if (modZ > threshold)
                        {
                            mask[i] = false;
                            outliers.Add(v);
                            outlierIndices.Add(i);
                        }
                        else
                        {
                            mask[i] = true;
                            cleanData.Add(v);
                            cleanIndices.Add(i);
                        }
                    }
                }

                report = $"Modified Z-Score (MAD, T = {threshold:G3}):\nMediana: {median:G4} | MAD: {mad:G4}\nFaixa válida: [{lowerBound:G4}, {upperBound:G4}]\nOutliers: {outliers.Count} / {n} ({((double)outliers.Count / n * 100.0):F1}%)";
                this.Message = $"MAD (T={threshold:G2})\n{outliers.Count} Outliers";
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Método desconhecido: {method}. Escolha 0 (IQR), 1 (Z-Score) ou 2 (MAD).");
                return;
            }

            DA.SetDataList(0, cleanData);
            DA.SetDataList(1, outliers);
            DA.SetDataList(2, mask);
            DA.SetDataList(3, cleanIndices);
            DA.SetDataList(4, outlierIndices);
            DA.SetData(5, lowerBound);
            DA.SetData(6, upperBound);
            DA.SetData(7, outliers.Count);
            DA.SetData(8, report);
        }

        private static double CalculatePercentile(List<double> sorted, double p)
        {
            if (sorted == null || sorted.Count == 0) return 0.0;
            if (sorted.Count == 1) return sorted[0];

            double rank = p * (sorted.Count - 1);
            int low = (int)Math.Floor(rank);
            int high = (int)Math.Ceiling(rank);

            if (low == high) return sorted[low];
            double weight = rank - low;
            return sorted[low] * (1.0 - weight) + sorted[high] * weight;
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.OutlierDetection;
    }
}
