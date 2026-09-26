using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class AnovaFTest_Component : GH_Component
    {
        public AnovaFTest_Component()
            : base(
                "ANOVA & F-Test (Variance & Means)",
                "ANOVA",
                "Executa a Análise de Variância One-Way (ANOVA F-Test = MS_between / MS_within), Teste F de Razão de Variâncias (F = s1² / s2²) e Teste de Aderência Qui-Quadrado (χ² = Σ(O - E)² / E) com p-valores exatos e tabela ANOVA.",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("4234c1c8-6ff4-4da1-ad5f-c04ecd32ebfa");

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.AnovaFTest;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Groups / Data", "D", "Árvore de dados numéricos onde cada ramo {k} representa um grupo/tratamento para ANOVA One-Way (ou lista de dados observados).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Second Group", "G2", "Opcional: segundo grupo de dados numéricos para teste F de duas variâncias F = s1² / s2².", GH_ParamAccess.list);
            pManager.AddNumberParameter("Expected", "Exp", "Opcional: frequências ou valores esperados E para teste Qui-Quadrado χ² = Σ(O - E)² / E.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Alpha Level", "α", "Nível de significância estatística α (ex.: 0.05 para 95% de confiança). Padrão: 0.05.", GH_ParamAccess.item, 0.05);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("F-Statistic", "F", "Estatística F calculada: MS_between / MS_within para ANOVA ou s1² / s2² para 2 grupos.", GH_ParamAccess.item);
            pManager.AddNumberParameter("p-Value", "p", "P-valor exato da distribuição F sob a hipótese nula H0.", GH_ParamAccess.item);
            pManager.AddNumberParameter("F-Critical", "F_crit", "Valor crítico de corte F_(α, df1, df2).", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Reject H0", "H0_Rej", "Booleano True se p < α (há diferença estatisticamente significativa entre as médias/variâncias dos grupos).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Chi-Square χ²", "χ²", "Estatística Qui-Quadrado de aderência χ² = Σ(O - E)² / E.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Chi p-Value", "p_Chi", "P-valor da distribuição Qui-Quadrado para a estatística χ².", GH_ParamAccess.item);
            pManager.AddTextParameter("Report", "Rep", "Tabela ANOVA completa e relatório interpretativo de hipótese.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> tree) || tree == null || tree.PathCount == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            var g2List = new List<double>();
            bool hasG2 = DA.GetDataList(1, g2List) && g2List != null && g2List.Count > 1;

            var expList = new List<double>();
            bool hasExp = DA.GetDataList(2, expList) && expList != null && expList.Count > 0;

            double alpha = 0.05;
            DA.GetData(3, ref alpha);
            if (alpha <= 0.0 || alpha >= 1.0) alpha = 0.05;

            // Extrair grupos numéricos de tree
            var groups = new List<List<double>>();
            foreach (var path in tree.Paths)
            {
                var branch = tree.get_Branch(path);
                var g = new List<double>();
                foreach (var item in branch)
                {
                    if (item is IGH_Goo goo && goo.CastTo(out double val) && !double.IsNaN(val) && !double.IsInfinity(val))
                    {
                        g.Add(val);
                    }
                }
                if (g.Count > 0)
                {
                    groups.Add(g);
                }
            }

            if (groups.Count == 0)
            {
                this.Message = "Sem Dados Válidos";
                return;
            }

            var sb = new StringBuilder();

            // Cenário A: Teste F de Duas Variâncias (se G2 foi fornecido explicitamente)
            if (hasG2 && groups.Count == 1)
            {
                var g1 = groups[0];
                var g2Clean = new List<double>();
                foreach (var v in g2List)
                {
                    if (!double.IsNaN(v) && !double.IsInfinity(v)) g2Clean.Add(v);
                }

                if (g1.Count >= 2 && g2Clean.Count >= 2)
                {
                    double var1 = CalcVariance(g1);
                    double var2 = CalcVariance(g2Clean);

                    double fStat = var2 > 1e-15 ? var1 / var2 : double.PositiveInfinity;
                    int df1 = g1.Count - 1;
                    int df2 = g2Clean.Count - 1;

                    double fCdf = SpecialFunctions.FDistCdf(fStat, df1, df2);
                    double pVal = 2.0 * Math.Min(fCdf, 1.0 - fCdf); // bilateral
                    double fCrit = SpecialFunctions.FDistInv(1.0 - alpha, df1, df2);
                    bool rej = pVal < alpha;

                    DA.SetData(0, fStat);
                    DA.SetData(1, pVal);
                    DA.SetData(2, fCrit);
                    DA.SetData(3, rej);

                    sb.AppendLine("=== TESTE F DE IGUALDADE DE DUAS VARIÂNCIAS ===");
                    sb.AppendLine($"Amostra 1 (n1={g1.Count}): Variância s1² = {var1:F4}");
                    sb.AppendLine($"Amostra 2 (n2={g2Clean.Count}): Variância s2² = {var2:F4}");
                    sb.AppendLine($"Razão F = s1² / s2²:     {fStat:F4}");
                    sb.AppendLine($"Graus de Liberdade:      df1 = {df1}, df2 = {df2}");
                    sb.AppendLine($"F Crítico (α={alpha}):   {fCrit:F4}");
                    sb.AppendLine($"P-Valor Bilateral:       {pVal:E4} ({(rej ? "REJEITA H0: Variâncias Diferentes" : "NÃO REJEITA H0")})");
                    sb.AppendLine("================================================");
                }
            }
            // Cenário B: ANOVA One-Way (se há 2 ou mais ramos na árvore)
            else if (groups.Count >= 2)
            {
                int k = groups.Count; // número de grupos
                int N = 0;           // total de observações
                double grandSum = 0.0;

                var groupMeans = new List<double>();
                var groupNs = new List<int>();

                foreach (var grp in groups)
                {
                    double gSum = 0.0;
                    foreach (var v in grp) gSum += v;
                    double gMean = grp.Count > 0 ? gSum / grp.Count : 0.0;
                    groupMeans.Add(gMean);
                    groupNs.Add(grp.Count);
                    grandSum += gSum;
                    N += grp.Count;
                }

                double grandMean = grandSum / N;

                // Soma dos Quadrados Entre Grupos (SS_between)
                double ssBetween = 0.0;
                for (int i = 0; i < k; i++)
                {
                    double diff = groupMeans[i] - grandMean;
                    ssBetween += groupNs[i] * diff * diff;
                }

                // Soma dos Quadrados Dentro dos Grupos (SS_within)
                double ssWithin = 0.0;
                for (int i = 0; i < k; i++)
                {
                    double m = groupMeans[i];
                    foreach (var v in groups[i])
                    {
                        double diff = v - m;
                        ssWithin += diff * diff;
                    }
                }

                double ssTotal = ssBetween + ssWithin;
                int dfBetween = k - 1;
                int dfWithin = N - k;

                double msBetween = dfBetween > 0 ? ssBetween / dfBetween : 0.0;
                double msWithin = dfWithin > 0 ? ssWithin / dfWithin : 0.0;

                double fStat = msWithin > 1e-15 ? msBetween / msWithin : 0.0;
                double pVal = 1.0 - SpecialFunctions.FDistCdf(fStat, dfBetween, dfWithin);
                double fCrit = SpecialFunctions.FDistInv(1.0 - alpha, dfBetween, dfWithin);
                bool rej = pVal < alpha;
                double etaSquared = ssTotal > 1e-15 ? ssBetween / ssTotal : 0.0;

                DA.SetData(0, fStat);
                DA.SetData(1, pVal);
                DA.SetData(2, fCrit);
                DA.SetData(3, rej);

                sb.AppendLine("=== TABELA ANOVA ONE-WAY (ANÁLISE DE VARIÂNCIA) ===");
                sb.AppendLine($"Fonte         |    SS      | df  |    MS      |    F     |   p-value");
                sb.AppendLine($"-------------+------------+-----+------------+----------+-----------");
                sb.AppendLine($"Entre Grupos  | {ssBetween,10:F4} | {dfBetween,3} | {msBetween,10:F4} | {fStat,8:F3} | {pVal,9:E3}");
                sb.AppendLine($"Dentro/Erro   | {ssWithin,10:F4} | {dfWithin,3} | {msWithin,10:F4} |          |");
                sb.AppendLine($"Total         | {ssTotal,10:F4} | {N - 1,3} |            |          |");
                sb.AppendLine("-------------------------------------------------------------------");
                sb.AppendLine($"Média Geral:            {grandMean:F4} (N total = {N}, k = {k} grupos)");
                sb.AppendLine($"F Crítico (α = {alpha}):   {fCrit:F4}");
                sb.AppendLine($"Eta-Squared (η²):       {etaSquared:P2} (proporção da variância total devida aos grupos)");
                sb.AppendLine($"Conclusão:              {(rej ? "REJEITA H0 (Diferença estatisticamente significativa entre as médias dos grupos)" : "NÃO REJEITA H0 (Médias homogêneas)")}");
                sb.AppendLine("===================================================================");
            }
            else
            {
                this.Message = "Necessita >= 2 Grupos";
            }

            // Cenário C: Teste de Aderência Qui-Quadrado (se Expected foi conectado)
            if (hasExp && groups.Count > 0)
            {
                var obs = groups[0];
                int minLen = Math.Min(obs.Count, expList.Count);
                if (minLen >= 1)
                {
                    double chiSq = 0.0;
                    for (int i = 0; i < minLen; i++)
                    {
                        double o = obs[i];
                        double e = expList[i];
                        if (e > 1e-12)
                        {
                            double diff = o - e;
                            chiSq += (diff * diff) / e;
                        }
                    }

                    int dfChi = minLen - 1;
                    double pChi = dfChi > 0 ? (1.0 - SpecialFunctions.ChiSqCdf(chiSq, dfChi)) : double.NaN;

                    DA.SetData(4, chiSq);
                    if (!double.IsNaN(pChi)) DA.SetData(5, pChi);

                    sb.AppendLine();
                    sb.AppendLine("=== TESTE QUI-QUADRADO DE ADERÊNCIA (GOODNESS-OF-FIT) ===");
                    sb.AppendLine($"Estatística χ² = Σ(O - E)² / E: {chiSq:F4}");
                    sb.AppendLine($"Graus de Liberdade (df):        {dfChi}");
                    sb.AppendLine($"P-Valor do Teste:               {(double.IsNaN(pChi) ? "N/A" : $"{pChi:E4}")}");
                    sb.AppendLine($"Interpretação (α={alpha}):      {(pChi < alpha ? "REJEITA H0 (Distribuição observada difere da esperada)" : "NÃO REJEITA H0 (Boa aderência)")}");
                    sb.AppendLine("=========================================================");
                }
            }

            DA.SetData(6, sb.ToString());

            if (groups.Count >= 2)
            {
                this.Message = "ANOVA One-Way";
            }
            else if (hasG2)
            {
                this.Message = "F-Test 2 Amostras";
            }
        }

        private static double CalcVariance(List<double> list)
        {
            if (list == null || list.Count < 2) return 0.0;
            double sum = 0.0;
            for (int i = 0; i < list.Count; i++) sum += list[i];
            double m = sum / list.Count;
            double ssq = 0.0;
            for (int i = 0; i < list.Count; i++)
            {
                double d = list[i] - m;
                ssq += d * d;
            }
            return ssq / (list.Count - 1);
        }
    }
}
