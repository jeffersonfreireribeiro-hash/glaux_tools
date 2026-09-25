using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    public class PillTreePivot_Component : GH_Component
    {
        public PillTreePivot_Component()
            : base(
                "Pill Tree Pivot & Join",
                "Pill_Pivot",
                "Transforma DataTrees N-dimensionais arbitrárias {a; b; c; ...; n} no formato tabular Wide / Pivot (Folha como Colunas).\n" +
                "- Cruza automaticamente árvores auxiliares (ex: Fitness) com prefixos de caminho comuns.\n" +
                "- Permite ordenar e filtrar os Top N melhores indivíduos por score de minimização.\n" +
                "- Saída em DataTree tabular {Linha}, lista de cabeçalhos e pré-visualização no Canvas.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("5cdfb60a-1081-4727-8a7d-1cf2f82d3b6a");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Tree", "T", "Árvore principal de dados N-dimensional (ex: TRs do Wallacei com caminhos {Gen; Ind; Ponto}).", GH_ParamAccess.tree);
            pManager.AddGenericParameter("AuxData", "Aux", "Árvore auxiliar de metadados (ex: Fitness {Gen; Ind}). Acoplada automaticamente por prefixo de caminho.", GH_ParamAccess.tree);
            pManager.AddTextParameter("DimNames", "Dim", "Nomes opcionais para as dimensões do caminho (ex: ['Gen', 'Ind', 'Ponto']). Se omitido, usa Dim_0, Dim_1, etc.", GH_ParamAccess.list);
            pManager.AddTextParameter("ColHeaders", "Col", "Nomes opcionais para os itens da folha (ex: ['125', '250', '500', '1000', '2000', '4000']).", GH_ParamAccess.list);
            pManager.AddIntegerParameter("TopCount", "Top", "Filtrar apenas os N melhores indivíduos por score (menor fitness primeiro). 0 ou vazio = todos.", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("ScoreIndex", "ScIdx", "Índice do objetivo em AuxData para o ranking de minimização (-1 = soma de todos os objetivos).", GH_ParamAccess.item, -1);
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Table", "Tbl", "Tabela resultante onde cada ramo {i} representa uma linha completa de dados.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Headers", "H", "Lista ordenada com os nomes de todas as colunas da tabela.", GH_ParamAccess.list);
            pManager.AddTextParameter("Preview", "Prev", "Amostra tabular formatada (até 30 linhas) para visualização rápida no Canvas.", GH_ParamAccess.list);
            pManager.AddTextParameter("Rank", "Rank", "Ranking dos melhores indivíduos classificados.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<IGH_Goo> mainTree = null;
            if (!DA.GetDataTree(0, out mainTree) || mainTree == null || mainTree.DataCount == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            GH_Structure<IGH_Goo> auxTree = null;
            DA.GetDataTree(1, out auxTree);

            var dimNames = new List<string>();
            DA.GetDataList(2, dimNames);

            var colHeaders = new List<string>();
            DA.GetDataList(3, colHeaders);

            int top = 0;
            DA.GetData(4, ref top);

            int scoreIdx = -1;
            DA.GetData(5, ref scoreIdx);

            // ── 1. Análise Dimensional da Árvore Principal ───────────────
            int maxDim = 0;
            int maxLeafCount = 0;
            foreach (var path in mainTree.Paths)
            {
                if (path.Indices.Length > maxDim) maxDim = path.Indices.Length;
            }
            foreach (var branch in mainTree.Branches)
            {
                if (branch.Count > maxLeafCount) maxLeafCount = branch.Count;
            }

            var finalDimNames = new List<string>();
            for (int d = 0; d < maxDim; d++)
            {
                if (d < dimNames.Count && !string.IsNullOrWhiteSpace(dimNames[d]))
                    finalDimNames.Add(dimNames[d]);
                else
                    finalDimNames.Add($"Dim_{d}");
            }

            var finalColHeaders = new List<string>();
            for (int c = 0; c < maxLeafCount; c++)
            {
                if (c < colHeaders.Count && !string.IsNullOrWhiteSpace(colHeaders[c]))
                    finalColHeaders.Add(colHeaders[c]);
                else
                    finalColHeaders.Add($"Col_{c}");
            }

            // ── 2. Mapeamento de AuxData (ex: Fitness) ───────────────────
            var auxMap = new Dictionary<string, List<double>>();
            int auxColsCount = 0;
            if (auxTree != null && auxTree.DataCount > 0)
            {
                foreach (var path in auxTree.Paths)
                {
                    string key = string.Join("_", path.Indices);
                    var branch = auxTree.get_Branch(path);
                    var vals = new List<double>();
                    for (int i = 0; i < branch.Count; i++)
                    {
                        vals.Add(ParseDouble(branch[i]));
                    }
                    auxMap[key] = vals;
                    if (vals.Count > auxColsCount) auxColsCount = vals.Count;
                }
            }

            // ── 3. Mapeamento de Pontos/Geometria (Removido) ───────────────
            var ptsList = new List<Point3d>();
            var ptsMap = new Dictionary<string, Point3d>();
            bool hasPoints = false;

            // ── 4. Classificação e Ranking ───────────────────────────────
            var candidates = new List<CandidateInfo>();
            var selectedPrefixes = new HashSet<string>();

            if (auxMap.Count > 0)
            {
                foreach (var kvp in auxMap)
                {
                    double score = 0.0;
                    if (scoreIdx >= 0 && scoreIdx < kvp.Value.Count)
                    {
                        score = kvp.Value[scoreIdx];
                    }
                    else
                    {
                        foreach (var v in kvp.Value) score += v;
                    }

                    candidates.Add(new CandidateInfo
                    {
                        PrefixKey = kvp.Key,
                        Score = score,
                        Values = kvp.Value
                    });
                }

                candidates.Sort((a, b) => a.Score.CompareTo(b.Score));
                for (int i = 0; i < candidates.Count; i++)
                {
                    candidates[i].Rank = i + 1;
                }

                int take = (top > 0 && top < candidates.Count) ? top : candidates.Count;
                for (int i = 0; i < take; i++)
                {
                    selectedPrefixes.Add(candidates[i].PrefixKey);
                }
            }

            // ── 5. Construção dos Cabeçalhos Completos ───────────────────
            var headers = new List<string>();
            if (candidates.Count > 0) headers.Add("Rank");
            headers.AddRange(finalDimNames);

            if (auxColsCount > 0)
            {
                headers.Add("Score_Fitness");
                for (int a = 0; a < auxColsCount; a++)
                {
                    headers.Add($"Fit_{a + 1}");
                }
            }

            if (hasPoints)
            {
                headers.Add("X");
                headers.Add("Y");
                headers.Add("Z");
            }

            headers.AddRange(finalColHeaders);

            // ── 6. Montagem da Tabela de Saída ───────────────────────────
            var outTable = new GH_Structure<IGH_Goo>();
            var previewLines = new List<string>();
            var rankOutput = new List<string>();

            if (candidates.Count > 0)
            {
                rankOutput.Add("Rank | Chave | Score");
                rankOutput.Add("────────────────────────────");
                int rLim = Math.Min(20, candidates.Count);
                for (int i = 0; i < rLim; i++)
                {
                    var c = candidates[i];
                    rankOutput.Add(string.Format("#{0:D2} | {1} | Score: {2:F4}", c.Rank, c.PrefixKey, c.Score));
                }
            }

            previewLines.Add(string.Join(" | ", headers));
            previewLines.Add(new string('-', Math.Min(120, headers.Count * 12)));

            int rowIndex = 0;
            var prefixRankMap = candidates.ToDictionary(c => c.PrefixKey, c => c);

            for (int b = 0; b < mainTree.Branches.Count; b++)
            {
                var ghPath = mainTree.Paths[b];
                var branch = mainTree.Branches[b];
                int[] idxs = ghPath.Indices;

                string prefixKey = (idxs.Length >= 2) ? $"{idxs[0]}_{idxs[1]}" : (idxs.Length > 0 ? idxs[0].ToString() : "0");
                if (selectedPrefixes.Count > 0 && !selectedPrefixes.Contains(prefixKey))
                {
                    continue;
                }

                var rowGoo = new List<IGH_Goo>();

                CandidateInfo cand = null;
                if (candidates.Count > 0 && prefixRankMap.TryGetValue(prefixKey, out cand))
                {
                    rowGoo.Add(new GH_Integer(cand.Rank));
                }
                else if (candidates.Count > 0)
                {
                    rowGoo.Add(new GH_Integer(0));
                }

                for (int d = 0; d < maxDim; d++)
                {
                    int val = (d < idxs.Length) ? idxs[d] : 0;
                    rowGoo.Add(new GH_Integer(val));
                }

                if (auxColsCount > 0)
                {
                    if (cand != null)
                    {
                        rowGoo.Add(new GH_Number(cand.Score));
                        for (int a = 0; a < auxColsCount; a++)
                        {
                            double fVal = (a < cand.Values.Count) ? cand.Values[a] : 0.0;
                            rowGoo.Add(new GH_Number(fVal));
                        }
                    }
                    else
                    {
                        rowGoo.Add(new GH_Number(0.0));
                        for (int a = 0; a < auxColsCount; a++) rowGoo.Add(new GH_Number(0.0));
                    }
                }

                if (hasPoints)
                {
                    int lastIdx = (idxs.Length > 0) ? idxs[idxs.Length - 1] : 0;
                    Point3d pt = Point3d.Unset;

                    if (lastIdx < ptsList.Count)
                    {
                        pt = ptsList[lastIdx];
                    }
                    else if (ptsMap.TryGetValue(lastIdx.ToString(), out Point3d mappedPt))
                    {
                        pt = mappedPt;
                    }

                    if (pt.IsValid)
                    {
                        rowGoo.Add(new GH_Number(pt.X));
                        rowGoo.Add(new GH_Number(pt.Y));
                        rowGoo.Add(new GH_Number(pt.Z));
                    }
                    else
                    {
                        rowGoo.Add(new GH_String(""));
                        rowGoo.Add(new GH_String(""));
                        rowGoo.Add(new GH_String(""));
                    }
                }

                for (int c = 0; c < maxLeafCount; c++)
                {
                    if (c < branch.Count && branch[c] != null)
                    {
                        double num = ParseDouble(branch[c]);
                        rowGoo.Add(new GH_Number(num));
                    }
                    else
                    {
                        rowGoo.Add(new GH_Number(0.0));
                    }
                }

                outTable.AppendRange(rowGoo, new GH_Path(rowIndex));

                if (rowIndex < 30)
                {
                    var strVals = rowGoo.Select(g => g?.ToString() ?? "").ToArray();
                    previewLines.Add(string.Join(" | ", strVals));
                }

                rowIndex++;
            }

            if (rowIndex > 30)
            {
                previewLines.Add($"... mais {rowIndex - 30:N0} linhas omitidas no preview");
            }

            DA.SetDataTree(0, outTable);
            DA.SetDataList(1, headers);
            DA.SetDataList(2, previewLines);
            DA.SetDataList(3, rankOutput);

            this.Message = $"{rowIndex:N0} Linhas\n{headers.Count} Colunas";
        }

        private static double ParseDouble(object item)
        {
            if (item == null) return 0.0;
            if (item is IGH_Goo goo) item = goo.SafeScriptVariable() ?? goo;
            if (item is double d) return d;
            if (item is float f) return f;
            if (item is int i) return i;
            string s = item.ToString().Replace(",", ".");
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double res))
                return res;
            return 0.0;
        }

        private static bool TryExtractPoint(object item, out Point3d pt)
        {
            pt = Point3d.Unset;
            if (item == null) return false;

            if (item is GH_Point ghPt)
            {
                pt = ghPt.Value;
                return true;
            }
            if (item is IGH_Goo goo) item = goo.SafeScriptVariable() ?? goo;
            if (item is Point3d p3d)
            {
                pt = p3d;
                return true;
            }
            if (item is Vector3d v3d)
            {
                pt = new Point3d(v3d.X, v3d.Y, v3d.Z);
                return true;
            }
            return false;
        }

        private class CandidateInfo
        {
            public string PrefixKey;
            public double Score;
            public List<double> Values;
            public int Rank;
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.PillTreePivot;
    }
}
