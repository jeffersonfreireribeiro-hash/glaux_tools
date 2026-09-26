using System;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class PoissonDistribution_Component : GH_Component
    {
        public PoissonDistribution_Component()
            : base(
                "Poisson Distribution (POISSON.DIST)",
                "PoissonDist",
                "Calcula a distribuição discreta de Poisson pontual (PMF) P(X = k) = (λ^k * e^-λ) / k! ou cumulativa (CDF) P(X <= k), além do quantil inverso, compatível com Microsoft Excel 2010+.",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("43c3b38e-8539-4d64-b185-c4ba162923fc");

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.PoissonDistribution;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Events", "K", "Número de ocorrências ou eventos independentes k (inteiro >= 0). Padrão: 2.", GH_ParamAccess.item, 2);
            pManager.AddNumberParameter("Lambda (Rate)", "λ", "Taxa média esperada de ocorrência por intervalo λ (deve ser > 0). Padrão: 3.0.", GH_ParamAccess.item, 3.0);
            pManager.AddBooleanParameter("Cumulative", "Cum", "Se True, calcula a probabilidade cumulativa P(X <= k). Se False, calcula a probabilidade pontual exata P(X = k). Padrão: False.", GH_ParamAccess.item, false);
            pManager.AddNumberParameter("Criterion", "Crit", "Critério de probabilidade para quantil inverso (padrão: 0.5).", GH_ParamAccess.item, 0.5);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Probability", "Prob", "Resultado de POISSON.DIST: probabilidade pontual P(X = k) ou cumulativa P(X <= k).", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Inverse", "Inv", "Resultado inverso: menor k inteiro para o qual a distribuição cumulativa é >= Critério.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Mean", "μ", "Média teórica da distribuição de Poisson (λ).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Variance", "σ²", "Variância teórica da distribuição de Poisson (λ).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int k = 2;
            double lambda = 3.0;
            bool cumulative = false;
            double crit = 0.5;

            DA.GetData(0, ref k);
            DA.GetData(1, ref lambda);
            DA.GetData(2, ref cumulative);
            DA.GetData(3, ref crit);

            if (lambda <= 0.0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A taxa Lambda (λ) deve ser estritamente maior que zero.");
                this.Message = "λ <= 0";
                return;
            }

            if (k < 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "O número de eventos k não pode ser negativo. k = 0 assumido.");
                k = 0;
            }

            double prob = cumulative ? SpecialFunctions.PoissonCdf(k, lambda) : SpecialFunctions.PoissonPmf(k, lambda);
            int inv = SpecialFunctions.PoissonInv(crit, lambda);

            DA.SetData(0, prob);
            DA.SetData(1, inv);
            DA.SetData(2, lambda);
            DA.SetData(3, lambda);

            this.Message = cumulative ? $"P(X<={k})={prob:F4}" : $"P(X={k})={prob:F4}";
        }
    }
}
