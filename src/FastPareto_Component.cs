using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class FastPareto_Component : GH_Component
    {
        public enum ParetoOrientationMode
        {
            Auto = 0,
            ByRows = 1,
            ByBranches = 2
        }

        private ParetoOrientationMode _orientationMode = ParetoOrientationMode.Auto;

        public FastPareto_Component()
            : base(
                "Fast Pareto Frontier (Multi-Objective Ranker)",
                "Pareto",
                "Calcula a Fronteira de Pareto e classificação de Não-Dominância (NSGA-II) para múltiplos objetivos de fitness em alta performance.\n" +
                "- Suporta tanto árvores onde cada ramo {i} é um indivíduo quanto tabelas importadas (CSV/Excel) onde os ramos {col} são colunas e os itens são as LINHAS.\n" +
                "- Encontra a melhor linha (Top 1) e o ranking completo de todas as linhas da melhor para a pior.\n" +
                "- Calcula os Ranks de Pareto (1, 2, 3...) e a Distância de Aglomeração (Crowding Distance para diversidade).\n" +
                "- Permite filtrar quais colunas são os objetivos e extrair os Top N indivíduos ou apenas a Fronteira Rank 1 absoluta.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("f97f9481-9c1e-4ac0-923d-c58c21259142");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Fitness / Data Tree", "F", "Árvore de dados contendo os vetores de fitness. Suporta árvore por ramos {indivíduo} OU tabela por colunas {sheet; col} onde cada item [i] é uma linha de dados (ex: CSV/Excel com N linhas).", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Directions", "Dir", "Direção para cada objetivo:\n0 ou 'min' = Minimizar (Padrão: menor é melhor)\n1 ou 'max' = Maximizar (maior é melhor).\nSe omitido, minimiza todos os objetivos.", GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager.AddIntegerParameter("Top N", "N", "Quantidade de indivíduos/linhas a extrair (ex: 10). Se 0, extrai todos ordenados pelo ranking de Pareto.", GH_ParamAccess.item, 0);
            pManager[2].Optional = true;
            pManager.AddBooleanParameter("Only Rank 1", "R1", "Se True, extrai estritamente apenas as soluções da Fronteira de Pareto Rank 1 (não-dominadas absolutas). Padrão: False.", GH_ParamAccess.item, false);
            pManager[3].Optional = true;
            pManager.AddGenericParameter("Columns / Objectives", "Cols", "Filtro opcional de índices ou nomes de colunas a avaliar como objetivos (ex: [0, 1, 2] ou lista de índices). Se vazio ou '*', avalia todas as colunas numéricas disponíveis.", GH_ParamAccess.list);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("By Rows", "Row", "Modo de avaliação:\n- True = Linhas são os indivíduos (cada ramo {col} é uma coluna e cada item [i] é uma linha, ex: CSV/Excel com 100 linhas).\n- False = Ramos são os indivíduos (cada ramo {i} é um indivíduo).\n- Desconectado = Auto-detecta automaticamente com base na topologia da árvore.", GH_ParamAccess.item);
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Pareto Tree", "P", "Árvore com os valores de fitness dos indivíduos/linhas selecionados, ordenados pelo ranking de Pareto.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Selected Paths", "Paths", "Lista dos caminhos GH_Path correspondentes aos indivíduos/linhas selecionados (no modo Linhas, caminhos {row}).", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Pareto Ranks", "R", "Rank de Pareto de cada indivíduo/linha selecionada (1 = Fronteira de Pareto não-dominada, 2 = Segunda fronteira, etc.).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Crowding Distance", "CD", "Distância de aglomeração (métrica de diversidade espacial das soluções NSGA-II).", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Frontier 1 Count", "N_F1", "Quantidade total de soluções não-dominadas da Fronteira 1.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Row Indices", "iRow", "Lista com os índices das LINHAS originais (0, 1, 2... N-1) ordenadas da melhor para a pior! Ligue no 'List Item' para extrair os modelos 3D, geometrias ou dados das melhores linhas.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Best Row", "Top1", "Índice da linha correspondente ao MELHOR resultado absoluto (1º lugar da Fronteira de Pareto Rank 1 com maior diversidade).", GH_ParamAccess.item);
            pManager.AddTextParameter("Report", "Rep", "Diagnóstico e resumo da classificação de Pareto (quantidade de linhas avaliadas, objetivos, distribuição dos ranks e Top 10).", GH_ParamAccess.item);
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var orientMenu = new ToolStripMenuItem("Modo de Leitura / Orientação:");

            var autoItem = new ToolStripMenuItem("Auto-detectar (Padrão)")
            {
                Checked = _orientationMode == ParetoOrientationMode.Auto
            };
            autoItem.Click += (s, e) =>
            {
                RecordUndoEvent("Mudar Orientação Pareto");
                _orientationMode = ParetoOrientationMode.Auto;
                ExpireSolution(true);
            };
            orientMenu.DropDownItems.Add(autoItem);

            var rowsItem = new ToolStripMenuItem("Linhas como Indivíduos (Tabela de Colunas {col} -> Linhas [i])")
            {
                Checked = _orientationMode == ParetoOrientationMode.ByRows
            };
            rowsItem.Click += (s, e) =>
            {
                RecordUndoEvent("Mudar Orientação Pareto");
                _orientationMode = ParetoOrientationMode.ByRows;
                ExpireSolution(true);
            };
            orientMenu.DropDownItems.Add(rowsItem);

            var branchItem = new ToolStripMenuItem("Ramos como Indivíduos (Ramos {i} -> Objetivos)")
            {
                Checked = _orientationMode == ParetoOrientationMode.ByBranches
            };
            branchItem.Click += (s, e) =>
            {
                RecordUndoEvent("Mudar Orientação Pareto");
                _orientationMode = ParetoOrientationMode.ByBranches;
                ExpireSolution(true);
            };
            orientMenu.DropDownItems.Add(branchItem);

            menu.Items.Add(orientMenu);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetInt32("OrientationMode", (int)_orientationMode);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("OrientationMode"))
            {
                _orientationMode = (ParetoOrientationMode)reader.GetInt32("OrientationMode");
            }
            return base.Read(reader);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) || inTree == null || inTree.PathCount == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            var rawDirs = new List<object>();
            DA.GetDataList(1, rawDirs);

            int topN = 0;
            DA.GetData(2, ref topN);

            bool onlyRank1 = false;
            DA.GetData(3, ref onlyRank1);

            var rawCols = new List<object>();
            DA.GetDataList(4, rawCols);

            bool explicitByRows = false;
            bool hasExplicitByRows = DA.GetData(5, ref explicitByRows);

            // Determina se vamos operar por linhas (tabela de colunas) ou por ramos
            bool byRows = false;
            if (hasExplicitByRows)
            {
                byRows = explicitByRows;
            }
            else if (_orientationMode == ParetoOrientationMode.ByRows)
            {
                byRows = true;
            }
            else if (_orientationMode == ParetoOrientationMode.ByBranches)
            {
                byRows = false;
            }
            else
            {
                // Auto-detecção inteligente:
                // Se o input Cols foi fornecido, ou se todos os ramos têm o mesmo tamanho N >= 2
                // e (N > PathCount ou os caminhos têm profundidade >= 2 ex: {sheet; col})
                int pCount = inTree.Paths.Count;
                if (rawCols != null && rawCols.Count > 0)
                {
                    byRows = true;
                }
                else if (pCount > 0)
                {
                    int firstCount = inTree.get_Branch(inTree.Paths[0])?.Count ?? 0;
                    bool allEqual = firstCount > 0;
                    for (int i = 1; i < pCount; i++)
                    {
                        if ((inTree.get_Branch(inTree.Paths[i])?.Count ?? 0) != firstCount)
                        {
                            allEqual = false;
                            break;
                        }
                    }

                    if (allEqual && firstCount >= 2)
                    {
                        // Ex: 35 ramos (colunas) com 100 itens cada (linhas)
                        if (firstCount > pCount || inTree.Paths[0].Length >= 2)
                        {
                            byRows = true;
                        }
                    }
                }
            }

            // 1. Extrair indivíduos e matriz de objetivos
            var paths = new List<GH_Path>();
            var dataList = new List<List<double>>();
            var origRowIndices = new List<int>();
            int maxObjCount = 0;
            var evaluatedColIndices = new List<int>();

            if (byRows)
            {
                int numCols = inTree.Paths.Count;
                var colPaths = inTree.Paths.ToList();
                var colBranches = new List<List<IGH_Goo>>();

                int rowCount = 0;
                for (int c = 0; c < numCols; c++)
                {
                    var b = inTree.get_Branch(colPaths[c]);
                    var list = b != null ? b.Cast<IGH_Goo>().ToList() : new List<IGH_Goo>();
                    colBranches.Add(list);
                    if (list.Count > rowCount) rowCount = list.Count;
                }

                if (rowCount == 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "A tabela de colunas não possui linhas de dados.");
                    return;
                }

                // Filtrar colunas / objetivos
                if (rawCols != null && rawCols.Count > 0)
                {
                    foreach (var cObj in rawCols)
                    {
                        if (cObj == null) continue;
                        string cStr = cObj.ToString().Trim();
                        if (cStr == "*" || cStr.Equals("ALL", StringComparison.OrdinalIgnoreCase) || cStr.Equals("TODOS", StringComparison.OrdinalIgnoreCase))
                        {
                            evaluatedColIndices.Clear();
                            for (int c = 0; c < numCols; c++) evaluatedColIndices.Add(c);
                            break;
                        }
                        if (int.TryParse(cStr, out int cIdx) && cIdx >= 0 && cIdx < numCols)
                        {
                            if (!evaluatedColIndices.Contains(cIdx)) evaluatedColIndices.Add(cIdx);
                        }
                    }
                }

                // Se não especificado em Cols, seleciona todas as colunas que têm números
                if (evaluatedColIndices.Count == 0)
                {
                    for (int c = 0; c < numCols; c++)
                    {
                        int numNumeric = 0;
                        for (int r = 0; r < Math.Min(rowCount, 20); r++)
                        {
                            if (r < colBranches[c].Count && GH_Convert.ToDouble(colBranches[c][r], out _, GH_Conversion.Both))
                            {
                                numNumeric++;
                            }
                        }
                        if (numNumeric > 0)
                        {
                            evaluatedColIndices.Add(c);
                        }
                    }
                }

                if (evaluatedColIndices.Count == 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Nenhuma coluna com dados numéricos foi encontrada para avaliar como objetivo.");
                    return;
                }

                maxObjCount = evaluatedColIndices.Count;

                for (int r = 0; r < rowCount; r++)
                {
                    var row = new List<double>(maxObjCount);
                    for (int k = 0; k < maxObjCount; k++)
                    {
                        int cIdx = evaluatedColIndices[k];
                        double v = 0.0;
                        if (r < colBranches[cIdx].Count)
                        {
                            GH_Convert.ToDouble(colBranches[cIdx][r], out v, GH_Conversion.Both);
                        }
                        row.Add(v);
                    }
                    paths.Add(new GH_Path(r));
                    dataList.Add(row);
                    origRowIndices.Add(r);
                }
            }
            else
            {
                // Modo Clássico: cada ramo é um indivíduo
                int pIdx = 0;
                foreach (GH_Path p in inTree.Paths)
                {
                    var branch = inTree.get_Branch(p);
                    if (branch == null || branch.Count == 0)
                    {
                        pIdx++;
                        continue;
                    }

                    var row = new List<double>();
                    for (int i = 0; i < branch.Count; i++)
                    {
                        if (GH_Convert.ToDouble(branch[i], out double val, GH_Conversion.Both))
                        {
                            row.Add(val);
                        }
                    }

                    if (row.Count > 0)
                    {
                        paths.Add(p);
                        dataList.Add(row);
                        origRowIndices.Add(pIdx);
                        if (row.Count > maxObjCount) maxObjCount = row.Count;
                    }
                    pIdx++;
                }
            }

            int M = dataList.Count;
            if (M == 0 || maxObjCount == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum dado numérico válido encontrado para avaliação.");
                return;
            }

            // 2. Parse das direções (min vs max)
            // Normalizar tudo para "minimizar": se for maximizar, inverte o sinal (-val)
            bool[] isMaximize = new bool[maxObjCount];
            for (int k = 0; k < maxObjCount; k++)
            {
                if (rawDirs != null && rawDirs.Count > 0)
                {
                    int dirIdx = Math.Min(k, rawDirs.Count - 1);
                    object dObj = rawDirs[dirIdx];
                    if (dObj != null)
                    {
                        string str = dObj.ToString().Trim().ToLowerInvariant();
                        if (str == "1" || str == "max" || str == "maximizar" || str == "true")
                        {
                            isMaximize[k] = true;
                        }
                    }
                }
            }

            // 3. Matriz normalizada [M, maxObjCount]
            double[][] normObj = new double[M][];
            for (int i = 0; i < M; i++)
            {
                normObj[i] = new double[maxObjCount];
                var row = dataList[i];
                for (int k = 0; k < maxObjCount; k++)
                {
                    double v = (k < row.Count) ? row[k] : 0.0;
                    normObj[i][k] = isMaximize[k] ? -v : v;
                }
            }

            // 4. Algoritmo Fast Non-Dominated Sorting (Deb et al., NSGA-II)
            var S = new List<int>[M];
            int[] n = new int[M];
            var frontiers = new List<List<int>>();
            var F1 = new List<int>();

            for (int p = 0; p < M; p++)
            {
                S[p] = new List<int>();
                n[p] = 0;

                for (int q = 0; q < M; q++)
                {
                    if (p == q) continue;

                    // Verificar se p domina q
                    bool pDominatesQ = Dominates(normObj[p], normObj[q]);
                    bool qDominatesP = Dominates(normObj[q], normObj[p]);

                    if (pDominatesQ)
                    {
                        S[p].Add(q);
                    }
                    else if (qDominatesP)
                    {
                        n[p]++;
                    }
                }

                if (n[p] == 0)
                {
                    F1.Add(p);
                }
            }

            frontiers.Add(F1);

            int fIdx = 0;
            while (frontiers[fIdx].Count > 0)
            {
                var nextF = new List<int>();
                foreach (int p in frontiers[fIdx])
                {
                    foreach (int q in S[p])
                    {
                        n[q]--;
                        if (n[q] == 0)
                        {
                            nextF.Add(q);
                        }
                    }
                }

                if (nextF.Count == 0) break;
                fIdx++;
                frontiers.Add(nextF);
            }

            // 5. Atribuir Ranks e calcular Crowding Distance
            int[] ranks = new int[M];
            double[] crowdingDist = new double[M];

            for (int r = 0; r < frontiers.Count; r++)
            {
                var frontier = frontiers[r];
                int rankNum = r + 1;

                foreach (int idx in frontier)
                {
                    ranks[idx] = rankNum;
                }

                // Crowding Distance para essa fronteira
                if (frontier.Count <= 2)
                {
                    foreach (int idx in frontier) crowdingDist[idx] = double.PositiveInfinity;
                }
                else
                {
                    for (int k = 0; k < maxObjCount; k++)
                    {
                        // Ordenar fronteira pelo objetivo k
                        frontier.Sort((a, b) => normObj[a][k].CompareTo(normObj[b][k]));

                        crowdingDist[frontier[0]] = double.PositiveInfinity;
                        crowdingDist[frontier[frontier.Count - 1]] = double.PositiveInfinity;

                        double minVal = normObj[frontier[0]][k];
                        double maxVal = normObj[frontier[frontier.Count - 1]][k];
                        double range = maxVal - minVal;
                        if (range < 1e-12) range = 1.0;

                        for (int i = 1; i < frontier.Count - 1; i++)
                        {
                            if (!double.IsInfinity(crowdingDist[frontier[i]]))
                            {
                                crowdingDist[frontier[i]] += (normObj[frontier[i + 1]][k] - normObj[frontier[i - 1]][k]) / range;
                            }
                        }
                    }
                }
            }

            // 6. Ordenar todos os indivíduos globalmente
            // Critério primário: Rank (menor é melhor)
            // Critério secundário: Crowding Distance (maior é melhor para manter diversidade)
            var allIndices = Enumerable.Range(0, M).ToList();
            allIndices.Sort((a, b) =>
            {
                int rCmp = ranks[a].CompareTo(ranks[b]);
                if (rCmp != 0) return rCmp;
                return crowdingDist[b].CompareTo(crowdingDist[a]);
            });

            // 7. Filtrar Top N ou apenas Rank 1
            if (onlyRank1)
            {
                allIndices = allIndices.Where(idx => ranks[idx] == 1).ToList();
            }

            if (topN > 0 && topN < allIndices.Count)
            {
                allIndices = allIndices.Take(topN).ToList();
            }

            // 8. Construir saídas
            var outTree = new GH_Structure<IGH_Goo>();
            var outPaths = new List<GH_Path>();
            var outRanks = new List<int>();
            var outCD = new List<double>();
            var outRowIndices = new List<int>();

            for (int i = 0; i < allIndices.Count; i++)
            {
                int origIdx = allIndices[i];
                GH_Path p = paths[origIdx];
                outPaths.Add(p);
                outRanks.Add(ranks[origIdx]);
                outCD.Add(double.IsInfinity(crowdingDist[origIdx]) ? 999.0 : Math.Round(crowdingDist[origIdx], 4));
                outRowIndices.Add(origRowIndices[origIdx]);

                outTree.EnsurePath(p);
                if (byRows)
                {
                    var rowVals = dataList[origIdx];
                    for (int k = 0; k < rowVals.Count; k++)
                    {
                        outTree.Append(new GH_Number(rowVals[k]), p);
                    }
                }
                else
                {
                    var originalBranch = inTree.get_Branch(p);
                    if (originalBranch != null)
                    {
                        for (int b = 0; b < originalBranch.Count; b++)
                        {
                            outTree.Append((IGH_Goo)originalBranch[b], p);
                        }
                    }
                }
            }

            int top1Row = outRowIndices.Count > 0 ? outRowIndices[0] : -1;

            // 9. Relatório detalhado para painel
            var sbRep = new StringBuilder();
            sbRep.AppendLine("=== FAST PARETO FRONTIER ===");
            sbRep.AppendLine($"Modo de Avaliação: {(byRows ? "Linhas como Indivíduos (Tabela de Colunas)" : "Ramos como Indivíduos")}");
            sbRep.AppendLine($"Total de {(byRows ? "Linhas" : "Indivíduos")} Avaliados: {M}");
            sbRep.AppendLine($"Objetivos de Fitness: {maxObjCount} {(byRows && evaluatedColIndices.Count > 0 ? $"(Colunas {string.Join(", ", evaluatedColIndices)})" : "")}");
            sbRep.AppendLine($"Fronteira 1 (Soluções Ótimas Não-Dominadas): {F1.Count}");
            sbRep.AppendLine($"Linhas Selecionadas no Filtro: {allIndices.Count}");
            if (top1Row >= 0)
            {
                sbRep.AppendLine($"★ MELHOR LINHA ABSOLUTA (TOP 1): Linha #{top1Row} (Rank 1)");
            }
            sbRep.AppendLine("-----------------------------------------");
            sbRep.AppendLine("Top Melhores Resultados:");
            for (int i = 0; i < Math.Min(10, allIndices.Count); i++)
            {
                int rIdx = origRowIndices[allIndices[i]];
                int rnk = ranks[allIndices[i]];
                double cd = crowdingDist[allIndices[i]];
                string cdStr = double.IsInfinity(cd) ? "Inf" : cd.ToString("F3", CultureInfo.InvariantCulture);
                sbRep.AppendLine($"  #{i + 1} -> Linha [{rIdx}] | Rank {rnk} | Crowding: {cdStr}");
            }

            DA.SetDataTree(0, outTree);
            DA.SetDataList(1, outPaths);
            DA.SetDataList(2, outRanks);
            DA.SetDataList(3, outCD);
            DA.SetData(4, F1.Count);
            DA.SetDataList(5, outRowIndices);
            DA.SetData(6, top1Row);
            DA.SetData(7, sbRep.ToString());

            if (byRows)
            {
                this.Message = $"Linhas ({allIndices.Count})\nTop: #{top1Row} (F1: {F1.Count})";
            }
            else
            {
                this.Message = $"Pareto\n{allIndices.Count} sel | F1: {F1.Count}";
            }
        }

        private static bool Dominates(double[] p, double[] q)
        {
            bool strictlyBetterInAny = false;
            for (int k = 0; k < p.Length; k++)
            {
                if (p[k] > q[k]) return false; // p é pior que q em pelo menos um objetivo
                if (p[k] < q[k]) strictlyBetterInAny = true;
            }
            return strictlyBetterInAny;
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.FastPareto;
    }
}
