using System;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class ChiSquareDistribution_Component : GH_Component
    {
        public ChiSquareDistribution_Component()
            : base(
                "Chi-Square Distribution (CHISQ.DIST / CHISQ.INV)",
                "ChiSqDist",
                "Calcula a distribuição Qui-Quadrado cumulativa (CHISQ.DIST), densidade de probabilidade (PDF) e a função inversa (CHISQ.INV), compatível com Microsoft Excel 2010+.",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("b3c4d5e6-f7a8-9b0c-1d2e-3f4a5b6c7d8e");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Value / Prob", "X", "Valor x >= 0 (para DIST) ou probabilidade p em [0, 1] (para INV).", GH_ParamAccess.item, 3.5);
            pManager.AddNumberParameter("Degrees of Freedom", "DF", "Graus de liberdade da distribuição (DF >= 1).", GH_ParamAccess.item, 4.0);
            pManager.AddBooleanParameter("Cumulative", "Cum", "Se True, calcula a distribuição cumulativa (CDF). Se False, calcula a densidade de probabilidade (PDF).", GH_ParamAccess.item, true);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Distribution", "Dist", "Resultado de CHISQ.DIST: probabilidade cumulativa (CDF) ou densidade (PDF).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Inverse", "Inv", "Resultado de CHISQ.INV: quantil x correspondente à probabilidade X em [0, 1].", GH_ParamAccess.item);
            pManager.AddNumberParameter("Mean", "μ", "Média teórica da distribuição Qui-Quadrado: DF.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Variance", "σ²", "Variância teórica da distribuição Qui-Quadrado: 2 * DF.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double x = 3.5;
            double df = 4.0;
            bool cumulative = true;

            DA.GetData(0, ref x);
            DA.GetData(1, ref df);
            DA.GetData(2, ref cumulative);

            if (df <= 0.0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Graus de liberdade (DF) deve ser estritamente maior que zero.");
                this.Message = "DF Inválido";
                return;
            }

            double dist = cumulative ? SpecialFunctions.ChiSqCdf(x, df) : SpecialFunctions.ChiSqPdf(x, df);

            double inv = double.NaN;
            if (x >= 0.0 && x <= 1.0)
            {
                inv = SpecialFunctions.ChiSqInv(x, df);
            }

            double mean = df;
            double variance = 2.0 * df;

            DA.SetData(0, dist);
            DA.SetData(1, inv);
            DA.SetData(2, mean);
            DA.SetData(3, variance);

            this.Message = cumulative ? $"CDF: {dist:F4}" : $"PDF: {dist:F4}";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.ChiSquareDistribution;
    }
}
