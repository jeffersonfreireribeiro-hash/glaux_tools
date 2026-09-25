using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class WeightedMean_Component : GH_Component
    {
        public WeightedMean_Component()
            : base(
                "Weighted Mean",
                "WAvg",
                "Calcula a Média Ponderada (Weighted Mean), Soma Total de Pesos e Pesos Normalizados a partir de listas de valores e pesos correspondentes.",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("6e7f8a9b-0c1d-2e3f-4a5b-6c7d8e9f0a1b");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Values", "V", "Conjunto de valores numéricos (x_i).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Weights", "W", "Pesos correspondentes a cada valor (w_i).", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Weighted Mean", "WAvg", "Média ponderada: x̄_w = Σ(x_i * w_i) / Σ(w_i).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Total Weight", "SumW", "Soma de todos os pesos válidos: Σ(w_i).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Normalized Weights", "NW", "Pesos normalizados proporcionais (w_i / Σw).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Simple Mean", "Avg", "Média aritmética simples para comparação rápida.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Count", "N", "Quantidade total de pares (valor, peso) processados.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rawValues = new List<double>();
            if (!DA.GetDataList(0, rawValues) || rawValues == null || rawValues.Count == 0)
            {
                this.Message = "Sem Valores";
                return;
            }

            var rawWeights = new List<double>();
            DA.GetDataList(1, rawWeights);

            if (rawWeights == null || rawWeights.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum peso fornecido. Assumindo peso 1.0 para todos os valores.");
                rawWeights = new List<double>();
                for (int i = 0; i < rawValues.Count; i++) rawWeights.Add(1.0);
            }

            var validValues = new List<double>();
            var validWeights = new List<double>();

            int count = rawValues.Count;
            int weightCount = rawWeights.Count;

            for (int i = 0; i < count; i++)
            {
                double v = rawValues[i];
                // Se a lista de pesos for menor que a de valores, repete o último peso
                double w = (i < weightCount) ? rawWeights[i] : rawWeights[weightCount - 1];

                if (!double.IsNaN(v) && !double.IsInfinity(v) && !double.IsNaN(w) && !double.IsInfinity(w))
                {
                    if (w < 0)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Pesos negativos foram convertidos para positivos em módulo.");
                        w = Math.Abs(w);
                    }
                    validValues.Add(v);
                    validWeights.Add(w);
                }
            }

            int n = validValues.Count;
            if (n == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Nenhum par válido de valor e peso foi encontrado.");
                this.Message = "N = 0";
                return;
            }

            double sumWeights = 0.0;
            double weightedSum = 0.0;
            double simpleSum = 0.0;

            for (int i = 0; i < n; i++)
            {
                double v = validValues[i];
                double w = validWeights[i];

                weightedSum += v * w;
                sumWeights += w;
                simpleSum += v;
            }

            if (sumWeights <= 1e-15)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A soma total dos pesos é zero.");
                this.Message = "Σw = 0";
                return;
            }

            double weightedMean = weightedSum / sumWeights;
            double simpleMean = simpleSum / n;

            var normWeights = new List<double>(n);
            for (int i = 0; i < n; i++)
            {
                normWeights.Add(validWeights[i] / sumWeights);
            }

            DA.SetData(0, weightedMean);
            DA.SetData(1, sumWeights);
            DA.SetDataList(2, normWeights);
            DA.SetData(3, simpleMean);
            DA.SetData(4, n);

            this.Message = $"x̄_w: {weightedMean:F2}\nΣw: {sumWeights:F2} (N={n})";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.WeightedMean;
    }
}
