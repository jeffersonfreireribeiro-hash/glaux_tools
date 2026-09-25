using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class StandardDeviation_Component : GH_Component
    {
        public StandardDeviation_Component()
            : base(
                "Standard Deviation",
                "StdDev",
                "Calcula o Desvio Padrão Amostral (s), Desvio Padrão Populacional (σ), Erro Padrão da Média (SE) e Coeficiente de Variação (CV%).",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("5d6e7f8a-9b0c-1d2e-3f4a-5b6c7d8e9f0a");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Values", "V", "Conjunto de dados numéricos (lista ou árvore de números).", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Sample StdDev", "s", "Desvio Padrão Amostral: s = √(Σ(x - x̄)² / (N - 1)).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Population StdDev", "σ", "Desvio Padrão Populacional: σ = √(Σ(x - x̄)² / N).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Standard Error", "SE", "Erro Padrão da Média: SE = s / √N.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Coefficient of Variation", "CV%", "Coeficiente de Variação percentual: CV = (s / x̄) * 100%.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Mean", "Avg", "Média aritmética dos valores (x̄).", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Count", "N", "Quantidade total de elementos numéricos válidos.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rawList = new List<double>();
            if (!DA.GetDataList(0, rawList) || rawList == null || rawList.Count == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            var list = new List<double>();
            for (int i = 0; i < rawList.Count; i++)
            {
                double v = rawList[i];
                if (!double.IsNaN(v) && !double.IsInfinity(v))
                {
                    list.Add(v);
                }
            }

            int n = list.Count;
            if (n == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum número válido fornecido.");
                this.Message = "N = 0";
                return;
            }

            // Média
            double sum = 0.0;
            for (int i = 0; i < n; i++) sum += list[i];
            double mean = sum / n;

            // Soma dos quadrados dos desvios
            double ss = 0.0;
            for (int i = 0; i < n; i++)
            {
                double diff = list[i] - mean;
                ss += diff * diff;
            }

            double popVar = ss / n;
            double sampleVar = (n > 1) ? (ss / (n - 1)) : 0.0;

            double popStdDev = Math.Sqrt(popVar);
            double sampleStdDev = Math.Sqrt(sampleVar);
            double stdError = (n > 0) ? (sampleStdDev / Math.Sqrt(n)) : 0.0;
            double cv = (Math.Abs(mean) > 1e-12) ? ((sampleStdDev / Math.Abs(mean)) * 100.0) : 0.0;

            DA.SetData(0, sampleStdDev);
            DA.SetData(1, popStdDev);
            DA.SetData(2, stdError);
            DA.SetData(3, cv);
            DA.SetData(4, mean);
            DA.SetData(5, n);

            this.Message = $"s: ±{sampleStdDev:F3}\nCV: {cv:F1}% (N={n})";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.StandardDeviation;
    }
}
