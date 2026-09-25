using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class GeometricClusterFrequency_Component : GH_Component
    {
        public GeometricClusterFrequency_Component()
            : base(
                "Geometric Frequency (1D Clustering)",
                "GeoCluster",
                "Agrupa valores numéricos/geométricos com tolerância de proximidade configurável (Clustering 1D / Create Set com Tolerância). Retorna valores canônicos médios, quantitativo de repetições, porcentagens e o mapa de índices para rotulagem.",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("7a8b9c0d-1e2f-3a4b-5c6d-7e8f9a0b1c2d");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Values", "V", "Conjunto de dados numéricos / dimensões de peças a agrupar (comprimentos, áreas, ângulos, coordenadas).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Tolerance", "Tol", "Tolerância geométrica / distância máxima para pertencer ao mesmo grupo (ex.: 0.005 m = 5 mm). Padrão: 0.005.", GH_ParamAccess.item, 0.005);
            pManager.AddIntegerParameter("Canonical Mode", "M", "Modo do valor canônico representativo:\n0 = Média do Grupo (Cluster Mean)\n1 = Mediana do Grupo\n2 = Primeiro Valor Encontrado\n3 = Arredondado para Múltiplo de Tol", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Per Branch", "B", "Se True, processa individualmente por ramo. Se False, agrupa globalmente sobre toda a árvore.", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Canonical Values", "C", "Valores médios/canônicos de cada tipo de peça/grupo identificado.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Counts", "N", "Quantitativo / Número de repetições de cada tipo de peça.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Percentages %", "%", "Frequência relativa percentual de cada grupo em relação ao total (0 a 100%).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Index Map", "Map", "Mapa de índices (0..K-1) para cada elemento original (ideal para rotular geometrias 1:1 com tags de tipo).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Grouped Tree", "T", "Árvore de dados onde cada ramo {k} contém todos os valores pertencentes ao tipo k.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Original Indices", "iTree", "Árvore com os índices originais dos elementos em cada grupo {k}.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Quant Report", "Rep", "Tabela quantitativa formatada e diagnóstico de tipologia.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<GH_Number> inTree) || inTree == null)
            {
                this.Message = "Sem Dados";
                return;
            }

            double tol = 0.005;
            DA.GetData(1, ref tol);
            if (tol <= 0) tol = 1e-6;

            int mode = 0;
            DA.GetData(2, ref mode);

            bool perBranch = false;
            DA.GetData(3, ref perBranch);

            var outCanonical = new GH_Structure<GH_Number>();
            var outCounts = new GH_Structure<GH_Integer>();
            var outPercents = new GH_Structure<GH_Number>();
            var outIndexMap = new GH_Structure<GH_Integer>();
            var outGroupedTree = new GH_Structure<GH_Number>();
            var outIndicesTree = new GH_Structure<GH_Integer>();

            var reportBuilder = new StringBuilder();
            int totalTypesGlobal = 0;
            int totalItemsGlobal = 0;

            if (perBranch)
            {
                foreach (GH_Path path in inTree.Paths)
                {
                    var branch = inTree.get_Branch(path);
                    var rawVals = new List<double>();
                    for (int i = 0; i < branch.Count; i++)
                    {
                        if (GH_Convert.ToDouble(branch[i], out double val, GH_Conversion.Both))
                        {
                            if (!double.IsNaN(val) && !double.IsInfinity(val)) rawVals.Add(val);
                        }
                    }

                    Cluster1D(rawVals, tol, mode, out var clusters, out var indexMap);

                    outCanonical.EnsurePath(path);
                    outCounts.EnsurePath(path);
                    outPercents.EnsurePath(path);
                    outIndexMap.EnsurePath(path);

                    for (int k = 0; k < clusters.Count; k++)
                    {
                        var c = clusters[k];
                        outCanonical.Append(new GH_Number(c.CanonicalValue), path);
                        outCounts.Append(new GH_Integer(c.Count), path);
                        outPercents.Append(new GH_Number(c.Percentage), path);

                        GH_Path groupSubPath = path.AppendElement(k);
                        outGroupedTree.EnsurePath(groupSubPath);
                        outIndicesTree.EnsurePath(groupSubPath);

                        foreach (var v in c.Values) outGroupedTree.Append(new GH_Number(v), groupSubPath);
                        foreach (var idx in c.OriginalIndices) outIndicesTree.Append(new GH_Integer(idx), groupSubPath);
                    }

                    for (int i = 0; i < indexMap.Count; i++)
                    {
                        outIndexMap.Append(new GH_Integer(indexMap[i]), path);
                    }

                    totalTypesGlobal += clusters.Count;
                    totalItemsGlobal += rawVals.Count;
                }
            }
            else // Global Clustering
            {
                var allItems = new List<double>();
                foreach (var item in inTree.AllData(true))
                {
                    if (GH_Convert.ToDouble(item, out double val, GH_Conversion.Both))
                    {
                        if (!double.IsNaN(val) && !double.IsInfinity(val)) allItems.Add(val);
                    }
                }

                totalItemsGlobal = allItems.Count;
                Cluster1D(allItems, tol, mode, out var clusters, out var indexMap);
                totalTypesGlobal = clusters.Count;

                GH_Path rootPath = new GH_Path(0);
                outCanonical.EnsurePath(rootPath);
                outCounts.EnsurePath(rootPath);
                outPercents.EnsurePath(rootPath);

                reportBuilder.AppendLine($"=== TABELA QUANTITATIVA COM TOLERÂNCIA (±{tol:G3}) ===");
                reportBuilder.AppendLine($"Total de Peças: {totalItemsGlobal} | Tipos Únicos Encontrados: {totalTypesGlobal}");
                reportBuilder.AppendLine("---------------------------------------------------------------");
                reportBuilder.AppendLine(string.Format("{0,-8} | {1,-14} | {2,-8} | {3,-10} | {4}", "Tipo", "Valor Médio", "Qtd (N)", "Freq %", "Variação [Min - Max]"));
                reportBuilder.AppendLine("---------------------------------------------------------------");

                for (int k = 0; k < clusters.Count; k++)
                {
                    var c = clusters[k];
                    outCanonical.Append(new GH_Number(c.CanonicalValue), rootPath);
                    outCounts.Append(new GH_Integer(c.Count), rootPath);
                    outPercents.Append(new GH_Number(c.Percentage), rootPath);

                    GH_Path groupPath = new GH_Path(k);
                    outGroupedTree.EnsurePath(groupPath);
                    outIndicesTree.EnsurePath(groupPath);

                    foreach (var v in c.Values) outGroupedTree.Append(new GH_Number(v), groupPath);
                    foreach (var idx in c.OriginalIndices) outIndicesTree.Append(new GH_Integer(idx), groupPath);

                    string typeTag = $"Tipo {(char)('A' + (k % 26))}" + (k >= 26 ? (k / 26).ToString() : "");
                    reportBuilder.AppendLine(string.Format("{0,-8} | {1,-14:F4} | {2,-8} | {3,-9:F1}% | [{4:F4} ; {5:F4}]",
                        typeTag, c.CanonicalValue, c.Count, c.Percentage, c.MinValue, c.MaxValue));
                }

                // Mapear o indexMap de volta na mesma estrutura de árvore da entrada
                int cursor = 0;
                foreach (GH_Path p in inTree.Paths)
                {
                    var branch = inTree.get_Branch(p);
                    outIndexMap.EnsurePath(p);
                    for (int i = 0; i < branch.Count; i++)
                    {
                        if (cursor < indexMap.Count)
                        {
                            outIndexMap.Append(new GH_Integer(indexMap[cursor]), p);
                            cursor++;
                        }
                    }
                }
            }

            DA.SetDataTree(0, outCanonical);
            DA.SetDataTree(1, outCounts);
            DA.SetDataTree(2, outPercents);
            DA.SetDataTree(3, outIndexMap);
            DA.SetDataTree(4, outGroupedTree);
            DA.SetDataTree(5, outIndicesTree);
            DA.SetData(6, reportBuilder.ToString());

            this.Message = $"{totalTypesGlobal} tipos únicos\n(±{tol:G2}) N={totalItemsGlobal}";
        }

        private class ClusterInfo
        {
            public double CanonicalValue;
            public int Count => Values.Count;
            public double Percentage;
            public double MinValue;
            public double MaxValue;
            public List<double> Values = new List<double>();
            public List<int> OriginalIndices = new List<int>();
        }

        private static void Cluster1D(
            List<double> values,
            double tol,
            int canonicalMode,
            out List<ClusterInfo> clusters,
            out List<int> indexMap)
        {
            clusters = new List<ClusterInfo>();
            indexMap = new List<int>(new int[values.Count]);

            if (values.Count == 0) return;

            // Indexar e ordenar
            var indexed = new List<KeyValuePair<double, int>>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                indexed.Add(new KeyValuePair<double, int>(values[i], i));
            }
            indexed.Sort((a, b) => a.Key.CompareTo(b.Key));

            // Agrupamento aglomerativo 1D
            ClusterInfo currentCluster = new ClusterInfo();
            currentCluster.Values.Add(indexed[0].Key);
            currentCluster.OriginalIndices.Add(indexed[0].Value);
            double clusterAnchor = indexed[0].Key;

            for (int i = 1; i < indexed.Count; i++)
            {
                double val = indexed[i].Key;
                int origIdx = indexed[i].Value;

                if (Math.Abs(val - clusterAnchor) <= tol)
                {
                    currentCluster.Values.Add(val);
                    currentCluster.OriginalIndices.Add(origIdx);
                }
                else
                {
                    clusters.Add(currentCluster);
                    currentCluster = new ClusterInfo();
                    currentCluster.Values.Add(val);
                    currentCluster.OriginalIndices.Add(origIdx);
                    clusterAnchor = val;
                }
            }
            clusters.Add(currentCluster);

            int totalCount = values.Count;
            for (int k = 0; k < clusters.Count; k++)
            {
                var c = clusters[k];
                c.MinValue = c.Values.Min();
                c.MaxValue = c.Values.Max();
                c.Percentage = (double)c.Count / totalCount * 100.0;

                switch (canonicalMode)
                {
                    case 0: // Média do grupo
                        c.CanonicalValue = c.Values.Average();
                        break;
                    case 1: // Mediana
                        {
                            var sorted = new List<double>(c.Values);
                            sorted.Sort();
                            int mid = sorted.Count / 2;
                            c.CanonicalValue = (sorted.Count % 2 == 1) ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) * 0.5;
                        }
                        break;
                    case 2: // Primeiro valor
                        c.CanonicalValue = c.Values[0];
                        break;
                    case 3: // Arredondado para múltiplo de tol
                        c.CanonicalValue = Math.Round(c.Values.Average() / tol) * tol;
                        break;
                    default:
                        c.CanonicalValue = c.Values.Average();
                        break;
                }

                foreach (var origIdx in c.OriginalIndices)
                {
                    indexMap[origIdx] = k;
                }
            }
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.GeometricClusterFrequency;
    }
}
