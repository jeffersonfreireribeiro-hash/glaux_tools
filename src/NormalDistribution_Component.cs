using System;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class NormalDistribution_Component : GH_Component
    {
        public NormalDistribution_Component()
            : base(
                "Normal Distribution (NORM.DIST / NORM.INV)",
                "NormDist",
                "Calcula a distribuição Normal / Gaussiana cumulativa (NORM.DIST CDF), densidade de probabilidade (PDF), função quantil inversa (NORM.INV) e o escore Z = (x - μ) / σ, compatível com Microsoft Excel 2010+.",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("25614b02-6748-48ca-926f-923c58b9ac68");

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.NormalDistribution;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Value / Prob", "X", "Valor numérico x (para calcular PDF/CDF) ou probabilidade acumulada p em [0, 1] (para calcular NORM.INV). Padrão: 0.0.", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Mean", "μ", "Média aritmética ou centro da distribuição normal μ. Padrão: 0.0.", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("StdDev", "σ", "Desvio padrão da distribuição normal σ (deve ser > 0). Padrão: 1.0.", GH_ParamAccess.item, 1.0);
            pManager.AddBooleanParameter("Cumulative", "Cum", "Se True, calcula a distribuição cumulativa CDF P(X <= x). Se False, calcula a densidade de probabilidade pontual PDF f(x). Padrão: True.", GH_ParamAccess.item, true);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Distribution", "Dist", "Resultado de NORM.DIST: probabilidade cumulativa P(X <= x) ou densidade f(x).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Inverse", "Inv", "Resultado de NORM.INV: quantil x correspondente à probabilidade X em [0, 1].", GH_ParamAccess.item);
            pManager.AddNumberParameter("Z-Score", "Z", "Escore padronizado Z = (x - μ) / σ.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Mean", "μ", "Média teórica da distribuição.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Variance", "σ²", "Variância teórica da distribuição normal: σ².", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double x = 0.0;
            double mu = 0.0;
            double sigma = 1.0;
            bool cumulative = true;

            DA.GetData(0, ref x);
            DA.GetData(1, ref mu);
            DA.GetData(2, ref sigma);
            DA.GetData(3, ref cumulative);

            if (sigma <= 0.0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "O desvio padrão (σ) deve ser estritamente maior que zero.");
                this.Message = "σ <= 0";
                return;
            }

            double dist = cumulative ? SpecialFunctions.NormCdf(x, mu, sigma) : SpecialFunctions.NormPdf(x, mu, sigma);
            double zScore = (x - mu) / sigma;

            // Se o valor de entrada X estiver no intervalo de probabilidade [0, 1], calculamos também NORM.INV
            double inv = double.NaN;
            if (x >= 0.0 && x <= 1.0)
            {
                inv = SpecialFunctions.NormInv(x, mu, sigma);
            }

            double variance = sigma * sigma;

            DA.SetData(0, dist);
            if (!double.IsNaN(inv))
            {
                DA.SetData(1, inv);
            }
            DA.SetData(2, zScore);
            DA.SetData(3, mu);
            DA.SetData(4, variance);

            this.Message = cumulative ? $"CDF={dist:F4}" : $"PDF={dist:F4}";
        }
    }
}
