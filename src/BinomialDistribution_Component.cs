using System;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class BinomialDistribution_Component : GH_Component
    {
        public BinomialDistribution_Component()
            : base(
                "Binomial Distribution (BINOM.DIST / BINOM.INV)",
                "BinomDist",
                "Calcula a probabilidade da distribuição binomial individual ou cumulativa (BINOM.DIST), intervalo de probabilidade (BINOM.DIST.INTERVALO / BINOM.DIST.RANGE) e o inverso (BINOM.INV), compatível com Microsoft Excel 2010/2013+.",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("a2b3c4d5-e6f7-8a9b-0c1d-2e3f4a5b6c7d");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Successes", "K", "Número de sucessos k na amostra (inteiro >= 0).", GH_ParamAccess.item, 4);
            pManager.AddIntegerParameter("Trials", "N", "Número total de tentativas independentes n (inteiro >= k).", GH_ParamAccess.item, 10);
            pManager.AddNumberParameter("Probability", "P", "Probabilidade de sucesso em cada tentativa p em [0, 1].", GH_ParamAccess.item, 0.3);
            pManager.AddBooleanParameter("Cumulative", "Cum", "Se True, calcula a probabilidade cumulativa P(X <= k). Se False, calcula a probabilidade pontual exata P(X = k).", GH_ParamAccess.item, false);
            pManager.AddNumberParameter("Criterion", "Crit", "Critério de probabilidade alfa para BINOM.INV (padrão: 0.5).", GH_ParamAccess.item, 0.5);
            pManager.AddIntegerParameter("Range S2", "S2", "Limite superior opcional de sucessos para BINOM.DIST.RANGE. Se fornecido, calcula P(k <= X <= S2).", GH_ParamAccess.item);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Probability", "Prob", "Resultado de BINOM.DIST: probabilidade pontual P(X = k) ou cumulativa P(X <= k).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Range Prob", "RngProb", "Resultado de BINOM.DIST.RANGE: probabilidade de obter entre k e S2 sucessos P(k <= X <= S2).", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Inverse", "Inv", "Resultado de BINOM.INV: menor valor de k para o qual a distribuição cumulativa é >= Critério.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Mean", "μ", "Média teórica da distribuição: n * p.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Variance", "σ²", "Variância teórica da distribuição: n * p * (1 - p).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int k = 4;
            int n = 10;
            double p = 0.3;
            bool cumulative = false;
            double crit = 0.5;

            DA.GetData(0, ref k);
            DA.GetData(1, ref n);
            DA.GetData(2, ref p);
            DA.GetData(3, ref cumulative);
            DA.GetData(4, ref crit);

            int s2 = k;
            bool hasS2 = DA.GetData(5, ref s2);

            if (n < 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Número de tentativas (N) deve ser >= 0.");
                this.Message = "N Inválido";
                return;
            }

            if (k < 0 || k > n)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Sucessos (K) deve estar no intervalo [0, N].");
            }

            if (p < 0.0 || p > 1.0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Probabilidade (P) deve estar no intervalo [0, 1].");
                this.Message = "P Inválido";
                return;
            }

            double prob = cumulative ? SpecialFunctions.BinomCdf(k, n, p) : SpecialFunctions.BinomPmf(k, n, p);

            double rngProb = double.NaN;
            if (hasS2)
            {
                rngProb = SpecialFunctions.BinomRange(n, p, k, s2);
            }

            int inv = SpecialFunctions.BinomInv(n, p, crit);
            double mean = n * p;
            double variance = n * p * (1.0 - p);

            DA.SetData(0, prob);
            if (hasS2) DA.SetData(1, rngProb);
            DA.SetData(2, inv);
            DA.SetData(3, mean);
            DA.SetData(4, variance);

            this.Message = cumulative ? $"CDF: {prob:P2}" : $"PMF: {prob:P2}";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.BinomialDistribution;
    }
}
