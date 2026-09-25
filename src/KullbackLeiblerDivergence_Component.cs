using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class KullbackLeiblerDivergence_Component : GH_Component
    {
        public KullbackLeiblerDivergence_Component()
            : base(
                "KL Divergence (Relative Entropy)",
                "KLDivergence",
                "Calcula a Divergência de Kullback-Leibler D_KL(P || Q) = Σ P(x) log(P(x)/Q(x)), a Divergência Simétrica de Jensen-Shannon (JSD), a Distância JS e a Entropia Cruzada entre duas distribuições de probabilidade.",
                "Glaux Tools",
                "Evaluation")
        {
        }

        public override Guid ComponentGuid => new Guid("6f7a8b9c-0d1e-2f3a-4b5c-6d7e8f9a0b1c");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Distribution P", "P", "Distribuição de referência / probabilidade real P(x).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Distribution Q", "Q", "Distribuição aproximada / modelo simulado Q(x). Deve possuir o mesmo tamanho que P.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Log Base", "Base", "Base do logaritmo:\n0 = Base 2 (Bits)\n1 = Base e (Nats)\n2 = Base 10 (Hartleys)", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Epsilon Smoothing", "ε", "Suavização para evitar divisões por zero ou log(0) em probabilidades nulas (padrão: 1e-12).", GH_ParamAccess.item, 1e-12);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("KL Divergence D_KL(P||Q)", "D_KL", "Divergência de Kullback-Leibler direta D_KL(P || Q) em relação ao modelo Q.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Reverse KL D_KL(Q||P)", "D_KL_QP", "Divergência reversa / assimétrica D_KL(Q || P).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Jensen-Shannon Div (JSD)", "JSD", "Divergência simétrica e limitada de Jensen-Shannon (0.0 a 1.0 em bits).", GH_ParamAccess.item);
            pManager.AddNumberParameter("JS Distance", "JSDist", "Métrica formal de distância entre distribuições (raiz quadrada da JSD).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Cross Entropy H(P,Q)", "H_PQ", "Entropia Cruzada H(P, Q) = H(P) + D_KL(P || Q).", GH_ParamAccess.item);
            pManager.AddTextParameter("Report", "Desc", "Diagnóstico comparativo e aderência entre as distribuições.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rawP = new List<double>();
            var rawQ = new List<double>();

            if (!DA.GetDataList(0, rawP) || rawP == null || rawP.Count == 0)
            {
                this.Message = "Sem Dados P";
                return;
            }
            if (!DA.GetDataList(1, rawQ) || rawQ == null || rawQ.Count == 0)
            {
                this.Message = "Sem Dados Q";
                return;
            }

            if (rawP.Count != rawQ.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"As distribuições P e Q possuem tamanhos diferentes (P = {rawP.Count}, Q = {rawQ.Count}).");
                this.Message = "Tamanhos ≠";
                return;
            }

            int logBase = 0;
            DA.GetData(2, ref logBase);

            double eps = 1e-12;
            DA.GetData(3, ref eps);
            if (eps < 0) eps = 1e-15;

            int n = rawP.Count;

            // 1. Normalizar P e Q
            double sumP = 0.0, sumQ = 0.0;
            for (int i = 0; i < n; i++)
            {
                sumP += Math.Max(0.0, rawP[i]);
                sumQ += Math.Max(0.0, rawQ[i]);
            }

            if (sumP < 1e-15 || sumQ < 1e-15)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A soma das probabilidades de P ou Q é zero.");
                return;
            }

            double[] p = new double[n];
            double[] q = new double[n];
            double[] m = new double[n]; // Distribuição média M = 0.5 * (P + Q)

            for (int i = 0; i < n; i++)
            {
                p[i] = (Math.Max(0.0, rawP[i]) / sumP);
                q[i] = (Math.Max(0.0, rawQ[i]) / sumQ);

                // Aplicar smoothing para cálculo numérico seguro
                if (eps > 0)
                {
                    p[i] = Math.Max(p[i], eps);
                    q[i] = Math.Max(q[i], eps);
                }

                m[i] = 0.5 * (p[i] + q[i]);
            }

            // Renormalizar após epsilon
            double sumP_adj = p.Sum();
            double sumQ_adj = q.Sum();
            double sumM_adj = m.Sum();
            for (int i = 0; i < n; i++)
            {
                p[i] /= sumP_adj;
                q[i] /= sumQ_adj;
                m[i] /= sumM_adj;
            }

            Func<double, double> logFn = logBase switch
            {
                1 => Math.Log,
                2 => Math.Log10,
                _ => val => Math.Log(val, 2.0)
            };

            string unit = logBase switch
            {
                1 => "nats",
                2 => "dits",
                _ => "bits"
            };

            // 2. Cálculo das divergências
            double dKL_PQ = 0.0;
            double dKL_QP = 0.0;
            double dKL_PM = 0.0;
            double dKL_QM = 0.0;
            double entropyP = 0.0;

            for (int i = 0; i < n; i++)
            {
                double pi = p[i];
                double qi = q[i];
                double mi = m[i];

                if (pi > 1e-15)
                {
                    dKL_PQ += pi * logFn(pi / qi);
                    dKL_PM += pi * logFn(pi / mi);
                    entropyP -= pi * logFn(pi);
                }

                if (qi > 1e-15)
                {
                    dKL_QP += qi * logFn(qi / pi);
                    dKL_QM += qi * logFn(qi / mi);
                }
            }

            if (dKL_PQ < 0) dKL_PQ = 0.0; // Garantir não-negatividade teórica (Teorema de Gibbs)
            if (dKL_QP < 0) dKL_QP = 0.0;

            double jsd = 0.5 * dKL_PM + 0.5 * dKL_QM;
            if (jsd < 0) jsd = 0.0;
            double jsDist = Math.Sqrt(jsd);

            double crossEntropy = entropyP + dKL_PQ;

            string matchQuality;
            if (dKL_PQ < 0.01) matchQuality = "Distribuições Praticamente Idênticas (Ajuste Perfeito)";
            else if (dKL_PQ < 0.10) matchQuality = "Alta Similaridade / Baixa Perda de Informação";
            else if (dKL_PQ < 0.50) matchQuality = "Similaridade Moderada";
            else matchQuality = "Forte Divergência / Modelos Distintos";

            string report = $"Kullback-Leibler & Information Divergence:\n------------------------------------------\nD_KL(P || Q): {dKL_PQ:F4} {unit} -> {matchQuality}\nD_KL(Q || P) (Reverso): {dKL_QP:F4} {unit}\nJensen-Shannon Divergence (JSD): {jsd:F4}\nDistância de Jensen-Shannon: {jsDist:F4} (Métrica formal 0.0 a 1.0)\nEntropia de P: {entropyP:F4} {unit}\nEntropia Cruzada H(P, Q): {crossEntropy:F4} {unit}";

            DA.SetData(0, dKL_PQ);
            DA.SetData(1, dKL_QP);
            DA.SetData(2, jsd);
            DA.SetData(3, jsDist);
            DA.SetData(4, crossEntropy);
            DA.SetData(5, report);

            this.Message = $"D_KL: {dKL_PQ:F3} {unit}\nJSD: {jsd:F3}";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.KullbackLeiblerDivergence;
    }
}
