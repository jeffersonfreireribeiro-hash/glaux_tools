using System;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class BetaDistribution_Component : GH_Component
    {
        public BetaDistribution_Component()
            : base(
                "Beta Distribution (BETA.DIST / BETA.INV)",
                "BetaDist",
                "Calcula a distribuição cumulativa Beta (BETA.DIST), a função de densidade de probabilidade (PDF) e a função inversa (BETA.INV), compatível com Microsoft Excel 2010+.",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("f1a2b3c4-d5e6-7f8a-9b0c-1d2e3f4a5b6c");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Value / Prob", "X", "Valor x em [A, B] (para DIST) ou probabilidade p em [0, 1] (para INV).", GH_ParamAccess.item, 0.5);
            pManager.AddNumberParameter("Alpha", "α", "Parâmetro de forma Alfa (α > 0).", GH_ParamAccess.item, 2.0);
            pManager.AddNumberParameter("Beta", "β", "Parâmetro de forma Beta (β > 0).", GH_ParamAccess.item, 5.0);
            pManager.AddBooleanParameter("Cumulative", "Cum", "Se True, calcula a probabilidade cumulativa (CDF). Se False, calcula a densidade de probabilidade (PDF).", GH_ParamAccess.item, true);
            pManager.AddNumberParameter("Lower Bound", "A", "Limite inferior opcional do intervalo [A, B] (padrão: 0.0).", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Upper Bound", "B", "Limite superior opcional do intervalo [A, B] (padrão: 1.0).", GH_ParamAccess.item, 1.0);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Distribution", "Dist", "Resultado de BETA.DIST: probabilidade cumulativa (CDF) ou densidade (PDF).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Inverse", "Inv", "Resultado de BETA.INV: valor x correspondente à probabilidade X em [0, 1].", GH_ParamAccess.item);
            pManager.AddNumberParameter("Mean", "μ", "Média teórica da distribuição no intervalo [A, B].", GH_ParamAccess.item);
            pManager.AddNumberParameter("Variance", "σ²", "Variância teórica da distribuição.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double x = 0.5;
            double alpha = 2.0;
            double beta = 5.0;
            bool cumulative = true;
            double a = 0.0;
            double b = 1.0;

            DA.GetData(0, ref x);
            DA.GetData(1, ref alpha);
            DA.GetData(2, ref beta);
            DA.GetData(3, ref cumulative);
            DA.GetData(4, ref a);
            DA.GetData(5, ref b);

            if (alpha <= 0.0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Alpha (α) deve ser estritamente maior que zero.");
                this.Message = "α Inválido";
                return;
            }

            if (beta <= 0.0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Beta (β) deve ser estritamente maior que zero.");
                this.Message = "β Inválido";
                return;
            }

            if (b <= a)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Limite superior (B) deve ser maior que limite inferior (A).");
                this.Message = "Intervalo Inválido";
                return;
            }

            double dist = cumulative ? SpecialFunctions.BetaCdf(x, alpha, beta, a, b) : SpecialFunctions.BetaPdf(x, alpha, beta, a, b);

            double inv = double.NaN;
            if (x >= 0.0 && x <= 1.0)
            {
                inv = SpecialFunctions.BetaInv(x, alpha, beta, a, b);
            }

            double mean = a + (b - a) * (alpha / (alpha + beta));
            double variance = Math.Pow(b - a, 2) * (alpha * beta) / (Math.Pow(alpha + beta, 2) * (alpha + beta + 1.0));

            DA.SetData(0, dist);
            DA.SetData(1, inv);
            DA.SetData(2, mean);
            DA.SetData(3, variance);

            this.Message = cumulative ? $"CDF: {dist:F4}" : $"PDF: {dist:F4}";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.BetaDistribution;
    }
}
