using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class Variance_Component : GH_Component
    {
        public Variance_Component()
            : base(
                "Variance",
                "Var",
                "Calcula a Variância Amostral (s² com divisor N-1), Variância Populacional (σ² com divisor N) e Soma dos Quadrados dos Desvios (SS).",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("4c5d6e7f-8a9b-0c1d-2e3f-4a5b6c7d8e9f");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Values", "V", "Conjunto de dados numéricos (lista ou árvore de números).", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Sample Variance", "s²", "Variância amostral não-viesada: s² = Σ(x - x̄)² / (N - 1).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Population Variance", "σ²", "Variância populacional exata: σ² = Σ(x - x̄)² / N.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Sum of Squares", "SS", "Soma dos quadrados dos desvios: SS = Σ(x - x̄)².", GH_ParamAccess.item);
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

            // Soma dos quadrados dos desvios (Two-pass algorithm para máxima precisão numérica sem cancelamento catastrófico)
            double ss = 0.0;
            for (int i = 0; i < n; i++)
            {
                double diff = list[i] - mean;
                ss += diff * diff;
            }

            double popVar = ss / n;
            double sampleVar = (n > 1) ? (ss / (n - 1)) : 0.0;

            DA.SetData(0, sampleVar);
            DA.SetData(1, popVar);
            DA.SetData(2, ss);
            DA.SetData(3, n);

            this.Message = $"s²: {sampleVar:F3}\nσ²: {popVar:F3} (N={n})";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.Variance;
    }
}
