using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class DataGroupFinder_Component : GH_Component
    {
        public DataGroupFinder_Component()
            : base(
                "Data Group Finder (Cluster & Runs)",
                "GroupFinder",
                "Encontra e segmenta automaticamente grupos de dados através de múltiplos métodos: ilhas de repetição consecutivas (runs), clusters de proximidade contínua, faixas de limiares (thresholds) ou quantis. Retorna grupos em DataTree com métricas e IDs 1:1.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("e6f7a8b9-0c1d-2e3f-4a5b-6c7d8e9f0a1b");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Lista ou árvore de dados a serem agrupados.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Mode", "M", "Modo de detecção: 0=Consecutive Runs (ilhas idênticas/estáveis contíguas), 1=Proximity Clusters (tolerância contínua entre vizinhos), 2=Threshold Bins (faixas por limiares), 3=Equal Quantiles (K grupos balanceados).", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Tolerance / Step", "Tol", "Tolerância para runs e clusters por proximidade (padrão: 1.0).", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Thresholds / K", "Thresh", "Lista de valores limiares para o Modo 2 ou quantidade K de grupos para o Modo 3.", GH_ParamAccess.list);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Groups", "G", "Árvore de dados estruturada em {path; group_id} contendo os elementos de cada grupo.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Group Indices", "i", "Árvore com os índices originais dos elementos em cada grupo.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Group Means", "Avg", "Média numérica dos elementos de cada grupo.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Group Counts", "N", "Quantidade de elementos em cada grupo.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Item Group IDs", "ID", "Lista ou árvore 1:1 com o ID numérico do grupo atribuído a cada item original.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> dataTree) || dataTree == null || dataTree.IsEmpty)
            {
                this.Message = "Sem Dados";
                return;
            }

            int mode = 0;
            DA.GetData(1, ref mode);

            double tol = 1.0;
            DA.GetData(2, ref tol);

            var threshList = new List<double>();
            DA.GetDataList(3, threshList);

            var outGroups = new GH_Structure<IGH_Goo>();
            var outIndices = new GH_Structure<GH_Integer>();
            var outMeans = new GH_Structure<GH_Number>();
            var outCounts = new GH_Structure<GH_Integer>();
            var outItemIDs = new GH_Structure<GH_Integer>();

            int totalGroupsFound = 0;

            for (int p = 0; p < dataTree.Paths.Count; p++)
            {
                var path = dataTree.Paths[p];
                var branch = dataTree.Branches[p];
                if (branch == null || branch.Count == 0) continue;

                int n = branch.Count;
                int[] groupAssignment = new int[n];
                var groupItems = new List<List<IGH_Goo>>();
                var groupOrigIndices = new List<List<int>>();

                switch (mode)
                {
                    case 0: // Consecutive Runs
                        FindConsecutiveRuns(branch, tol, groupAssignment, groupItems, groupOrigIndices);
                        break;

                    case 1: // Proximity Clusters
                        FindProximityClusters(branch, tol, groupAssignment, groupItems, groupOrigIndices);
                        break;

                    case 2: // Threshold Bins
                        FindThresholdBins(branch, threshList, groupAssignment, groupItems, groupOrigIndices);
                        break;

                    case 3: // Equal Quantiles
                        int k = threshList.Count > 0 ? (int)Math.Max(2, Math.Round(threshList[0])) : 4;
                        FindEqualQuantiles(branch, k, groupAssignment, groupItems, groupOrigIndices);
                        break;

                    default:
                        FindConsecutiveRuns(branch, tol, groupAssignment, groupItems, groupOrigIndices);
                        break;
                }

                // Registrar saídas
                for (int g = 0; g < groupItems.Count; g++)
                {
                    var subPath = path.AppendElement(g);
                    var items = groupItems[g];
                    var indices = groupOrigIndices[g];

                    double sum = 0.0;
                    int numCount = 0;

                    for (int i = 0; i < items.Count; i++)
                    {
                        outGroups.Append(items[i], subPath);
                        outIndices.Append(new GH_Integer(indices[i]), subPath);

                        if (TryGetNumber(items[i], out double val))
                        {
                            sum += val;
                            numCount++;
                        }
                    }

                    double mean = numCount > 0 ? sum / numCount : double.NaN;
                    outMeans.Append(new GH_Number(mean), path);
                    outCounts.Append(new GH_Integer(items.Count), path);
                }

                // IDs 1:1 para cada item original
                for (int i = 0; i < n; i++)
                {
                    outItemIDs.Append(new GH_Integer(groupAssignment[i]), path);
                }

                totalGroupsFound += groupItems.Count;
            }

            DA.SetDataTree(0, outGroups);
            DA.SetDataTree(1, outIndices);
            DA.SetDataTree(2, outMeans);
            DA.SetDataTree(3, outCounts);
            DA.SetDataTree(4, outItemIDs);

            string[] modeNames = new string[] { "Runs Consecutivos", "Clusters Proximidade", "Faixas Limiares", "Quantis" };
            string modeStr = (mode >= 0 && mode < modeNames.Length) ? modeNames[mode] : "Grupos";
            this.Message = $"{modeStr}\n{totalGroupsFound} Grupos";
        }

        private static void FindConsecutiveRuns(List<IGH_Goo> branch, double tol, int[] groupAssignment, List<List<IGH_Goo>> groupItems, List<List<int>> groupOrigIndices)
        {
            int currentGroupId = 0;
            var currentGroup = new List<IGH_Goo> { branch[0] };
            var currentIndices = new List<int> { 0 };
            groupAssignment[0] = 0;

            for (int i = 1; i < branch.Count; i++)
            {
                var prev = branch[i - 1];
                var curr = branch[i];

                bool same = IsSimilar(prev, curr, tol);
                if (same)
                {
                    currentGroup.Add(curr);
                    currentIndices.Add(i);
                    groupAssignment[i] = currentGroupId;
                }
                else
                {
                    groupItems.Add(currentGroup);
                    groupOrigIndices.Add(currentIndices);
                    currentGroupId++;

                    currentGroup = new List<IGH_Goo> { curr };
                    currentIndices = new List<int> { i };
                    groupAssignment[i] = currentGroupId;
                }
            }

            groupItems.Add(currentGroup);
            groupOrigIndices.Add(currentIndices);
        }

        private static void FindProximityClusters(List<IGH_Goo> branch, double tol, int[] groupAssignment, List<List<IGH_Goo>> groupItems, List<List<int>> groupOrigIndices)
        {
            // Agrupamento 1D por distância entre valores ordenados
            var pairs = new List<Tuple<double, int, IGH_Goo>>();
            for (int i = 0; i < branch.Count; i++)
            {
                double val = TryGetNumber(branch[i], out double num) ? num : i;
                pairs.Add(new Tuple<double, int, IGH_Goo>(val, i, branch[i]));
            }

            pairs.Sort((a, b) => a.Item1.CompareTo(b.Item1));

            int currentGroupId = 0;
            var currentGroup = new List<IGH_Goo> { pairs[0].Item3 };
            var currentIndices = new List<int> { pairs[0].Item2 };
            groupAssignment[pairs[0].Item2] = 0;

            for (int i = 1; i < pairs.Count; i++)
            {
                double diff = Math.Abs(pairs[i].Item1 - pairs[i - 1].Item1);
                if (diff <= tol)
                {
                    currentGroup.Add(pairs[i].Item3);
                    currentIndices.Add(pairs[i].Item2);
                    groupAssignment[pairs[i].Item2] = currentGroupId;
                }
                else
                {
                    groupItems.Add(currentGroup);
                    groupOrigIndices.Add(currentIndices);
                    currentGroupId++;

                    currentGroup = new List<IGH_Goo> { pairs[i].Item3 };
                    currentIndices = new List<int> { pairs[i].Item2 };
                    groupAssignment[pairs[i].Item2] = currentGroupId;
                }
            }

            groupItems.Add(currentGroup);
            groupOrigIndices.Add(currentIndices);
        }

        private static void FindThresholdBins(List<IGH_Goo> branch, List<double> thresholds, int[] groupAssignment, List<List<IGH_Goo>> groupItems, List<List<int>> groupOrigIndices)
        {
            var sortedThresh = new List<double>(thresholds);
            sortedThresh.Sort();

            int numBins = sortedThresh.Count + 1;
            for (int g = 0; g < numBins; g++)
            {
                groupItems.Add(new List<IGH_Goo>());
                groupOrigIndices.Add(new List<int>());
            }

            for (int i = 0; i < branch.Count; i++)
            {
                double val = TryGetNumber(branch[i], out double num) ? num : 0.0;
                int bin = 0;
                while (bin < sortedThresh.Count && val > sortedThresh[bin])
                {
                    bin++;
                }

                groupAssignment[i] = bin;
                groupItems[bin].Add(branch[i]);
                groupOrigIndices[bin].Add(i);
            }

            // Remover faixas vazias se houver
            for (int g = groupItems.Count - 1; g >= 0; g--)
            {
                if (groupItems[g].Count == 0)
                {
                    groupItems.RemoveAt(g);
                    groupOrigIndices.RemoveAt(g);
                }
            }
        }

        private static void FindEqualQuantiles(List<IGH_Goo> branch, int k, int[] groupAssignment, List<List<IGH_Goo>> groupItems, List<List<int>> groupOrigIndices)
        {
            var pairs = new List<Tuple<double, int, IGH_Goo>>();
            for (int i = 0; i < branch.Count; i++)
            {
                double val = TryGetNumber(branch[i], out double num) ? num : i;
                pairs.Add(new Tuple<double, int, IGH_Goo>(val, i, branch[i]));
            }

            pairs.Sort((a, b) => a.Item1.CompareTo(b.Item1));

            for (int g = 0; g < k; g++)
            {
                groupItems.Add(new List<IGH_Goo>());
                groupOrigIndices.Add(new List<int>());
            }

            double itemsPerGroup = (double)pairs.Count / k;

            for (int i = 0; i < pairs.Count; i++)
            {
                int g = Math.Min(k - 1, (int)(i / itemsPerGroup));
                groupAssignment[pairs[i].Item2] = g;
                groupItems[g].Add(pairs[i].Item3);
                groupOrigIndices[g].Add(pairs[i].Item2);
            }
        }

        private static bool IsSimilar(IGH_Goo a, IGH_Goo b, double tol)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            if (TryGetNumber(a, out double na) && TryGetNumber(b, out double nb))
            {
                return Math.Abs(na - nb) <= tol;
            }

            return string.Equals(a.ToString()?.Trim(), b.ToString()?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetNumber(IGH_Goo goo, out double val)
        {
            val = 0.0;
            if (goo == null) return false;
            if (goo is GH_Number ghNum) { val = ghNum.Value; return true; }
            if (goo is GH_Integer ghInt) { val = ghInt.Value; return true; }
            string s = goo.ToString()?.Trim();
            if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val)) return true;
            return false;
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.DataGroupFinder;
    }
}
