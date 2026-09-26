using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class ClassificationMetrics_Component : GH_Component
    {
        public ClassificationMetrics_Component()
            : base(
                "Classification & Tree Metrics (Gini, InfoGain, Logit)",
                "ClassMetrics",
                "Calcula métricas essenciais de classificação, regressão logística e árvores de decisão (CART, C4.5, ID3):\n" +
                "- Odds = p / (1 - p)\n" +
                "- Logit = ln(p / (1 - p))\n" +
                "- Sigmóide Inversa = 1 / (1 + e^-z)\n" +
                "- Gini Impurity (Índice de Gini) = 1 - Σ p_i²\n" +
                "- Information Gain (Ganho de Informação) = H(parent) - Σ (n_i / n) * H(child_i)\n" +
                "- Gini Gain / Redução de Impureza no split.",
                "Glaux Tools",
                "Evaluation")
        {
        }

        public override Guid ComponentGuid => new Guid("6b7d3d38-22c6-47ef-9fc6-1df98751da14");

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.ClassificationMetrics;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Probabilities / Counts", "P", "Probabilidade escalar p em (0, 1) ou lista com as contagens/frequências das classes no nó pai (Parent).", GH_ParamAccess.list);
            pManager.AddGenericParameter("Child Partitions", "Children", "Árvore opcional onde cada ramo {k} representa um nó filho após uma partição, contendo as contagens das classes naquele ramo.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Log Base", "Base", "Base do logaritmo para cálculo da entropia:\n0 = Base 2 (Bits / Shannons - Padrão C4.5/ID3)\n1 = Base e (Nats)\n2 = Base 10 (Hartleys)", GH_ParamAccess.item, 0);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Odds", "Odds", "Razão de chances: Odds = p / (1 - p).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Logit", "Logit", "Log dos odds: Logit(p) = ln(p / (1 - p)).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Sigmoid", "Sigmoid", "Função sigmóide inversa σ(z) = 1 / (1 + e^-z) calculada a partir do primeiro valor de P.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Gini Impurity", "Gini", "Impureza de Gini do nó pai: G = 1 - Σ p_i² (0 = 100% puro; máx 0.5 para binário).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Parent Entropy", "H", "Entropia de Shannon do nó pai: H(X) = -Σ p_i log(p_i).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Information Gain", "IG", "Ganho de Informação da partição: IG = H(pai) - Σ (n_i / n) * H(filho_i).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Gini Gain", "ΔGini", "Redução da impureza de Gini com a partição: G(pai) - Σ (n_i / n) * G(filho_i).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Split Entropy", "H_split", "Entropia residual ponderada média dos nós filhos.", GH_ParamAccess.item);
            pManager.AddTextParameter("Report", "Rep", "Relatório interpretativo detalhado de pureza e ganho de divisão.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rawP = new List<double>();
            if (!DA.GetDataList(0, rawP) || rawP == null || rawP.Count == 0)
            {
                this.Message = "Sem Dados P";
                return;
            }

            int logBaseMode = 0;
            DA.GetData(2, ref logBaseMode);

            // 1. Odds, Logit e Sigmoide
            double pFirst = rawP[0];
            double odds = double.NaN;
            double logit = double.NaN;
            double sigmoid = 1.0 / (1.0 + Math.Exp(-pFirst));

            if (pFirst > 0.0 && pFirst < 1.0)
            {
                odds = pFirst / (1.0 - pFirst);
                logit = Math.Log(odds);
            }
            else if (pFirst <= 0.0)
            {
                odds = 0.0;
                logit = double.NegativeInfinity;
            }
            else if (pFirst >= 1.0)
            {
                odds = double.PositiveInfinity;
                logit = double.PositiveInfinity;
            }

            // 2. Normalizar vetor do nó pai para probabilidades p_i
            var probsParent = NormalizeToProbs(rawP, out double totalParent);

            // Gini e Entropia do pai
            double giniParent = CalcGini(probsParent);
            double entropyParent = CalcEntropy(probsParent, logBaseMode);

            // 3. Processar nós filhos se fornecidos
            double infoGain = double.NaN;
            double giniGain = double.NaN;
            double splitEntropy = double.NaN;
            bool hasChildren = false;

            if (DA.GetDataTree(1, out GH_Structure<IGH_Goo> childTree) && childTree != null && childTree.PathCount > 0)
            {
                double grandChildTotal = 0.0;
                var childSizes = new List<double>();
                var childGinis = new List<double>();
                var childEntropies = new List<double>();

                foreach (var path in childTree.Paths)
                {
                    var branch = childTree.get_Branch(path);
                    var childCounts = new List<double>();
                    foreach (var item in branch)
                    {
                        if (item is IGH_Goo goo && goo.CastTo(out double val) && !double.IsNaN(val) && val >= 0.0)
                        {
                            childCounts.Add(val);
                        }
                    }

                    if (childCounts.Count > 0)
                    {
                        var childProbs = NormalizeToProbs(childCounts, out double cTotal);
                        if (cTotal > 0)
                        {
                            childSizes.Add(cTotal);
                            childGinis.Add(CalcGini(childProbs));
                            childEntropies.Add(CalcEntropy(childProbs, logBaseMode));
                            grandChildTotal += cTotal;
                        }
                    }
                }

                if (grandChildTotal > 0 && childSizes.Count > 1)
                {
                    double weightedChildEntropy = 0.0;
                    double weightedChildGini = 0.0;

                    for (int i = 0; i < childSizes.Count; i++)
                    {
                        double weight = childSizes[i] / grandChildTotal;
                        weightedChildEntropy += weight * childEntropies[i];
                        weightedChildGini += weight * childGinis[i];
                    }

                    splitEntropy = weightedChildEntropy;
                    infoGain = Math.Max(0.0, entropyParent - weightedChildEntropy);
                    giniGain = Math.Max(0.0, giniParent - weightedChildGini);
                    hasChildren = true;
                }
            }

            DA.SetData(0, odds);
            DA.SetData(1, logit);
            DA.SetData(2, sigmoid);
            DA.SetData(3, giniParent);
            DA.SetData(4, entropyParent);
            if (hasChildren)
            {
                DA.SetData(5, infoGain);
                DA.SetData(6, giniGain);
                DA.SetData(7, splitEntropy);
            }

            // Relatório
            var sb = new StringBuilder();
            sb.AppendLine("=== MÉTRICAS DE CLASSIFICAÇÃO, GINI E GANHO DE INFORMAÇÃO ===");
            sb.AppendLine($"1. Odds (Razão de Chances):     {odds:F4}");
            sb.AppendLine($"2. Logit ln(p/(1-p)):           {logit:F4}");
            sb.AppendLine($"3. Sigmóide Inversa σ(p):       {sigmoid:F4}");
            sb.AppendLine($"4. Impureza de Gini (Pai):      {giniParent:F4} (1 - Σ p_i²)");
            sb.AppendLine($"5. Entropia de Shannon (Pai):   {entropyParent:F4} (Base {(logBaseMode == 0 ? "2: Bits" : logBaseMode == 1 ? "e: Nats" : "10: Dits")})");
            if (hasChildren)
            {
                sb.AppendLine("-------------------------------------------------------------");
                sb.AppendLine($"6. Entropia Ponderada (Split):  {splitEntropy:F4}");
                sb.AppendLine($"7. Information Gain (IG):       {infoGain:F4} (Redução da incerteza)");
                sb.AppendLine($"8. Gini Gain (ΔGini):           {giniGain:F4} (Aumento de pureza CART)");
            }
            sb.AppendLine("=============================================================");

            DA.SetData(8, sb.ToString());

            this.Message = hasChildren ? $"IG={infoGain:F3} | Gini={giniParent:F3}" : $"Gini={giniParent:F3} | Odds={odds:F2}";
        }

        private static List<double> NormalizeToProbs(List<double> values, out double total)
        {
            total = 0.0;
            var clean = new List<double>();
            foreach (var v in values)
            {
                if (!double.IsNaN(v) && !double.IsInfinity(v) && v >= 0.0)
                {
                    clean.Add(v);
                    total += v;
                }
            }

            var probs = new List<double>();
            if (total > 1e-15)
            {
                foreach (var v in clean) probs.Add(v / total);
            }
            else
            {
                foreach (var v in clean) probs.Add(0.0);
            }
            return probs;
        }

        private static double CalcGini(List<double> probs)
        {
            double sumSq = 0.0;
            foreach (var p in probs) sumSq += p * p;
            return Math.Max(0.0, 1.0 - sumSq);
        }

        private static double CalcEntropy(List<double> probs, int logBase)
        {
            double h = 0.0;
            double logDiv = logBase == 0 ? Math.Log(2.0) : logBase == 2 ? Math.Log(10.0) : 1.0;

            foreach (var p in probs)
            {
                if (p > 1e-15)
                {
                    h -= p * (Math.Log(p) / logDiv);
                }
            }
            return Math.Max(0.0, h);
        }
    }
}
