using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class ProbabilityBayes_Component : GH_Component
    {
        public ProbabilityBayes_Component()
            : base(
                "Probability & Bayes' Theorem",
                "Bayes",
                "Calcula regras fundamentais de probabilidade e inferência Bayesiana:\n" +
                "- Probabilidade Clássica P(A) = favoráveis / total\n" +
                "- Regra do Complementar P(A') = 1 - P(A)\n" +
                "- Regra da Adição P(A∪B) = P(A) + P(B) - P(A∩B)\n" +
                "- Regra da Multiplicação P(A∩B) = P(A) * P(B|A)\n" +
                "- Probabilidade Condicional P(A|B) = P(A∩B) / P(B)\n" +
                "- Teorema de Bayes P(A|B) = [P(B|A) * P(A)] / P(B)\n" +
                "- Valor Esperado E[X] = Σ x_i * P(x_i) e Razão de Chances (Odds).",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("f541c883-e869-4d02-be47-f1553fae13a7");

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.ProbabilityBayes;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Prob A / Favorable", "A", "Probabilidade prévia P(A) em [0, 1] ou contagem de casos favoráveis. Padrão: 0.4.", GH_ParamAccess.item, 0.4);
            pManager.AddNumberParameter("Prob B / Total", "B", "Probabilidade marginal P(B) em [0, 1] ou total de casos possíveis (se A for contagem). Padrão: 0.5.", GH_ParamAccess.item, 0.5);
            pManager.AddNumberParameter("Likelihood P(B|A)", "B|A", "Verossimilhança / Probabilidade condicional de observar B dado A. Padrão: 0.8.", GH_ParamAccess.item, 0.8);
            pManager.AddNumberParameter("Joint P(A∩B)", "A∩B", "Probabilidade conjunta opcional P(A∩B). Se omitida, calculada via P(A) * P(B|A).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Values X", "X", "Lista opcional de valores discretos x_i para cálculo do Valor Esperado E[X].", GH_ParamAccess.list);
            pManager.AddNumberParameter("Probabilities P", "P", "Lista opcional de probabilidades P(x_i) correspondentes aos valores X.", GH_ParamAccess.list);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Bayes Posterior P(A|B)", "A|B", "Probabilidade a posteriori calculada pelo Teorema de Bayes: P(A|B) = [P(B|A) * P(A)] / P(B).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Complement P(A')", "A'", "Regra do complementar: P(A') = 1 - P(A).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Union P(A∪B)", "A∪B", "Regra da adição de probabilidades: P(A∪B) = P(A) + P(B) - P(A∩B).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Joint P(A∩B)", "A∩B", "Probabilidade conjunta da interseção: P(A∩B) = P(A) * P(B|A).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Expected Value E[X]", "E[X]", "Valor esperado matemático: E[X] = Σ x_i * P(x_i).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Odds Ratio", "Odds", "Razão de chances para o evento A: Odds = P(A) / (1 - P(A)).", GH_ParamAccess.item);
            pManager.AddTextParameter("Report", "Rep", "Relatório de probabilidades e fórmulas detalhado.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double aInput = 0.4;
            double bInput = 0.5;
            double pBgivenA = 0.8;

            DA.GetData(0, ref aInput);
            DA.GetData(1, ref bInput);
            DA.GetData(2, ref pBgivenA);

            // Se A e B forem inteiros ou > 1, assume cálculo clássico: P(A) = favorable / total
            double pA = aInput;
            double pB = bInput;

            if (aInput > 1.0 && bInput >= aInput)
            {
                pA = aInput / bInput;
                pB = 1.0;
            }

            pA = Math.Max(0.0, Math.Min(1.0, pA));
            pB = Math.Max(0.0, Math.Min(1.0, pB));
            pBgivenA = Math.Max(0.0, Math.Min(1.0, pBgivenA));

            // Interseção P(A ∩ B)
            double joint = pA * pBgivenA;
            double manualJoint = 0.0;
            if (DA.GetData(3, ref manualJoint))
            {
                joint = Math.Max(0.0, Math.Min(1.0, manualJoint));
            }

            // Teorema de Bayes: P(A|B) = joint / P(B)
            double bayesPosterior = double.NaN;
            if (pB > 1e-12)
            {
                bayesPosterior = Math.Min(1.0, joint / pB);
            }

            // Complemento P(A')
            double pCompA = 1.0 - pA;

            // União P(A ∪ B) = P(A) + P(B) - P(A ∩ B)
            double union = Math.Max(0.0, Math.Min(1.0, pA + pB - joint));

            // Odds = p / (1 - p)
            double odds = pCompA > 1e-12 ? pA / pCompA : double.PositiveInfinity;

            // Valor Esperado E[X] = Σ x_i * P(x_i)
            var rawX = new List<double>();
            var rawP = new List<double>();
            double expectedVal = double.NaN;
            bool hasExpected = false;

            if (DA.GetDataList(4, rawX) && rawX != null && rawX.Count > 0)
            {
                if (DA.GetDataList(5, rawP) && rawP != null && rawP.Count == rawX.Count)
                {
                    double sumP = 0.0;
                    double ev = 0.0;
                    for (int i = 0; i < rawX.Count; i++)
                    {
                        if (!double.IsNaN(rawX[i]) && !double.IsNaN(rawP[i]))
                        {
                            ev += rawX[i] * rawP[i];
                            sumP += rawP[i];
                        }
                    }
                    if (sumP > 1e-12)
                    {
                        expectedVal = ev / sumP; // Normaliza se as probabilidades não somarem 1.0
                        hasExpected = true;
                    }
                }
            }

            DA.SetData(0, bayesPosterior);
            DA.SetData(1, pCompA);
            DA.SetData(2, union);
            DA.SetData(3, joint);
            if (hasExpected) DA.SetData(4, expectedVal);
            DA.SetData(5, odds);

            // Relatório
            var sb = new StringBuilder();
            sb.AppendLine("=== RELATÓRIO DE PROBABILIDADE E INFERÊNCIA BAYESIANA ===");
            sb.AppendLine($"1. Probabilidade Prior P(A):     {pA:P2} ({pA:F4})");
            sb.AppendLine($"2. Regra do Complementar P(A'):  {pCompA:P2} ({pCompA:F4})");
            sb.AppendLine($"3. Probabilidade Marginal P(B):  {pB:P2} ({pB:F4})");
            sb.AppendLine($"4. Verossimilhança P(B|A):       {pBgivenA:P2} ({pBgivenA:F4})");
            sb.AppendLine($"5. Interseção P(A∩B):            {joint:P2} ({joint:F4})");
            sb.AppendLine($"6. União P(A∪B):                 {union:P2} ({union:F4})");
            sb.AppendLine($"7. Teorema de Bayes P(A|B):      {(double.IsNaN(bayesPosterior) ? "Indefinido (P(B)=0)" : $"{bayesPosterior:P2} ({bayesPosterior:F4})")}");
            sb.AppendLine($"8. Razão de Chances (Odds A):    {odds:F4} : 1");
            if (hasExpected)
            {
                sb.AppendLine($"9. Valor Esperado E[X]:          {expectedVal:F4} (calculado sobre {rawX.Count} estados)");
            }
            sb.AppendLine("=========================================================");

            DA.SetData(6, sb.ToString());

            this.Message = double.IsNaN(bayesPosterior) ? $"P(A)={pA:F2}" : $"P(A|B)={bayesPosterior:F3}";
        }
    }
}
