using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class DataNormalization_Component : GH_Component
    {
        public DataNormalization_Component()
            : base(
                "Data Normalization",
                "Norm",
                "Normaliza e padroniza conjuntos de dados numéricos usando Min-Max customizável, Z-Score ((x - μ) / σ), Sigmóide Logística, Logarítmica ou Softmax.",
                "Glaux Tools",
                "Transform")
        {
        }

        public override Guid ComponentGuid => new Guid("8a9b0c1d-2e3f-4a5b-6c7d-8e9f0a1b2c3d");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Values", "V", "Conjunto de dados numéricos de entrada.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Method", "M", "Método de normalização:\n0 = Min-Max personalizado [min, max]\n1 = Z-Score ((x - μ) / σ)\n2 = Sigmóide Logística (1 / (1 + e^-z))\n3 = Logarítmica (sign(x) * ln(1 + |x|))\n4 = Softmax (probabilidades que somam 1.0)", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Target Min", "min", "Limite inferior para o método Min-Max (padrão 0.0).", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Target Max", "max", "Limite superior para o método Min-Max (padrão 1.0).", GH_ParamAccess.item, 1.0);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Normalized", "N", "Valores normalizados no mesmo formato e ordem da entrada.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Param 1", "P1", "Primeiro parâmetro do modelo (ex.: Mínimo original ou Média μ).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Param 2", "P2", "Segundo parâmetro do modelo (ex.: Máximo original ou Desvio Padrão σ).", GH_ParamAccess.item);
            pManager.AddTextParameter("Formula / Summary", "Desc", "Descrição do método e parâmetros matemáticos aplicados.", GH_ParamAccess.item);
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
            double targetMin = 0.0;
            DA.GetData(2, ref targetMin);
            double targetMax = 1.0;
            DA.GetData(3, ref targetMax);

            int n = rawList.Count;
            var cleanIndices = new List<int>();
            var validVals = new List<double>();

            for (int i = 0; i < n; i++)
            {
                double v = rawList[i];
                if (!double.IsNaN(v) && !double.IsInfinity(v))
                {
                    cleanIndices.Add(i);
                    validVals.Add(v);
                }
            }

            if (validVals.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum valor numérico finito válido fornecido.");
                this.Message = "N = 0";
                return;
            }

            var normalized = new List<double>(new double[n]);
            double p1 = 0.0;
            double p2 = 0.0;
            string summary = "";

            switch (method)
            {
                case 0: // Min-Max Personalizado
                    {
                        double origMin = double.MaxValue;
                        double origMax = double.MinValue;
                        for (int i = 0; i < validVals.Count; i++)
                        {
                            double v = validVals[i];
                            if (v < origMin) origMin = v;
                            if (v > origMax) origMax = v;
                        }

                        p1 = origMin;
                        p2 = origMax;
                        double range = origMax - origMin;
                        double targetRange = targetMax - targetMin;

                        for (int i = 0; i < n; i++)
                        {
                            double v = rawList[i];
                            if (double.IsNaN(v) || double.IsInfinity(v))
                            {
                                normalized[i] = double.NaN;
                            }
                            else if (Math.Abs(range) < 1e-15)
                            {
                                normalized[i] = (targetMin + targetMax) * 0.5;
                            }
                            else
                            {
                                normalized[i] = targetMin + ((v - origMin) / range) * targetRange;
                            }
                        }

                        summary = $"Min-Max [{targetMin}, {targetMax}] | Orig: [{origMin:G4}, {origMax:G4}]";
                        this.Message = $"Min-Max [{targetMin:G2}, {targetMax:G2}]";
                    }
                    break;

                case 1: // Z-Score: (x - μ) / σ
                    {
                        double sum = 0.0;
                        for (int i = 0; i < validVals.Count; i++) sum += validVals[i];
                        double mean = sum / validVals.Count;

                        double sumSq = 0.0;
                        for (int i = 0; i < validVals.Count; i++)
                        {
                            double diff = validVals[i] - mean;
                            sumSq += diff * diff;
                        }

                        double stdDev = (validVals.Count > 1) ? Math.Sqrt(sumSq / (validVals.Count - 1)) : 0.0;
                        p1 = mean;
                        p2 = stdDev;

                        for (int i = 0; i < n; i++)
                        {
                            double v = rawList[i];
                            if (double.IsNaN(v) || double.IsInfinity(v))
                            {
                                normalized[i] = double.NaN;
                            }
                            else if (stdDev < 1e-15)
                            {
                                normalized[i] = 0.0;
                            }
                            else
                            {
                                normalized[i] = (v - mean) / stdDev;
                            }
                        }

                        summary = $"Z-Score: (x - μ) / σ | μ = {mean:G4}, σ = {stdDev:G4}";
                        this.Message = $"Z-Score\nμ={mean:F2}, σ={stdDev:F2}";
                    }
                    break;

                case 2: // Sigmóide Logística: 1 / (1 + e^-z)
                    {
                        double sum = 0.0;
                        for (int i = 0; i < validVals.Count; i++) sum += validVals[i];
                        double mean = sum / validVals.Count;

                        double sumSq = 0.0;
                        for (int i = 0; i < validVals.Count; i++)
                        {
                            double diff = validVals[i] - mean;
                            sumSq += diff * diff;
                        }
                        double stdDev = (validVals.Count > 1) ? Math.Sqrt(sumSq / (validVals.Count - 1)) : 1.0;
                        if (stdDev < 1e-15) stdDev = 1.0;

                        p1 = mean;
                        p2 = stdDev;

                        for (int i = 0; i < n; i++)
                        {
                            double v = rawList[i];
                            if (double.IsNaN(v) || double.IsInfinity(v))
                            {
                                normalized[i] = double.NaN;
                            }
                            else
                            {
                                double z = (v - mean) / stdDev;
                                normalized[i] = 1.0 / (1.0 + Math.Exp(-z));
                            }
                        }

                        summary = $"Sigmoid: 1 / (1 + exp(-(x - μ)/σ)) | μ = {mean:G4}, σ = {stdDev:G4}";
                        this.Message = "Sigmoid\n[0.0, 1.0]";
                    }
                    break;

                case 3: // Logarítmica: sign(x) * ln(1 + |x|)
                    {
                        p1 = 0.0;
                        p2 = 1.0;

                        for (int i = 0; i < n; i++)
                        {
                            double v = rawList[i];
                            if (double.IsNaN(v) || double.IsInfinity(v))
                            {
                                normalized[i] = double.NaN;
                            }
                            else
                            {
                                normalized[i] = Math.Sign(v) * Math.Log(1.0 + Math.Abs(v));
                            }
                        }

                        summary = "Log Transformation: sign(x) * ln(1 + |x|)";
                        this.Message = "Log(1+|x|)";
                    }
                    break;

                case 4: // Softmax: exp(x_i - max) / sum(exp(x_j - max))
                    {
                        double maxVal = double.MinValue;
                        for (int i = 0; i < validVals.Count; i++)
                        {
                            if (validVals[i] > maxVal) maxVal = validVals[i];
                        }

                        double expSum = 0.0;
                        var expVals = new List<double>(validVals.Count);
                        for (int i = 0; i < validVals.Count; i++)
                        {
                            double exp = Math.Exp(validVals[i] - maxVal);
                            expVals.Add(exp);
                            expSum += exp;
                        }

                        p1 = maxVal;
                        p2 = expSum;

                        int validIdx = 0;
                        for (int i = 0; i < n; i++)
                        {
                            double v = rawList[i];
                            if (double.IsNaN(v) || double.IsInfinity(v))
                            {
                                normalized[i] = double.NaN;
                            }
                            else
                            {
                                normalized[i] = (expSum > 0) ? (expVals[validIdx] / expSum) : 0.0;
                                validIdx++;
                            }
                        }

                        summary = $"Softmax: exp(x - max) / Σexp | Max = {maxVal:G4}, SumExp = {expSum:G4}";
                        this.Message = "Softmax\nΣ = 1.0";
                    }
                    break;

                default:
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Método de normalização desconhecido: {method}. Use 0 a 4.");
                    return;
            }

            DA.SetDataList(0, normalized);
            DA.SetData(1, p1);
            DA.SetData(2, p2);
            DA.SetData(3, summary);
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.DataNormalization;
    }
}
