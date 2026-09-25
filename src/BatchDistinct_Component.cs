using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class BatchDistinct_Component : GH_Component
    {
        public BatchDistinct_Component()
            : base(
                "Batch Distinct (Keep Order & Map)",
                "Distinct",
                "Extrai elementos únicos preservando a ordem original de aparição, retornando a contagem de frequências, índices da primeira ocorrência e o mapa de índices mapeados.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("9f0a1b2c-3d4e-5f6a-7b8c-9d0e1f2a3b4c");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Lista ou Árvore de dados para filtrar elementos únicos.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Tolerance", "Tol", "Tolerância para igualdade numérica e de coordenadas (padrão: 1e-6).", GH_ParamAccess.item, 1e-6);
            pManager.AddBooleanParameter("Per Branch", "B", "Se True, extrai únicos individualmente por ramo. Se False, extrai sobre toda a árvore globalmente.", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Unique Values", "U", "Elementos únicos preservando a ordem de primeira aparição.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Counts", "C", "Frequência / quantidade de vezes que cada elemento único apareceu.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("First Indices", "iFirst", "Índice de primeira ocorrência de cada elemento único.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Index Map", "Map", "Mapeamento para cada item original indicando o índice do seu correspondente único (0..K-1).", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Duplicates", "Dup", "Lista de elementos que se repetiram mais de uma vez.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) || inTree == null)
            {
                this.Message = "Sem Dados";
                return;
            }

            double tol = 1e-6;
            DA.GetData(1, ref tol);

            bool perBranch = false;
            DA.GetData(2, ref perBranch);

            var outUnique = new GH_Structure<IGH_Goo>();
            var outCounts = new GH_Structure<GH_Integer>();
            var outFirstIdx = new GH_Structure<GH_Integer>();
            var outIndexMap = new GH_Structure<GH_Integer>();
            var outDuplicates = new GH_Structure<IGH_Goo>();

            int totalUniqueGlobal = 0;

            if (perBranch)
            {
                foreach (GH_Path path in inTree.Paths)
                {
                    var items = inTree.get_Branch(path);
                    ProcessBranch(items, tol, out var uList, out var cList, out var fList, out var mList, out var dList);

                    outUnique.EnsurePath(path);
                    foreach (var u in uList) outUnique.Append(u, path);

                    outCounts.EnsurePath(path);
                    foreach (var c in cList) outCounts.Append(new GH_Integer(c), path);

                    outFirstIdx.EnsurePath(path);
                    foreach (var f in fList) outFirstIdx.Append(new GH_Integer(f), path);

                    outIndexMap.EnsurePath(path);
                    foreach (var m in mList) outIndexMap.Append(new GH_Integer(m), path);

                    outDuplicates.EnsurePath(path);
                    foreach (var d in dList) outDuplicates.Append(d, path);

                    totalUniqueGlobal += uList.Count;
                }
            }
            else // Global
            {
                var allItems = inTree.AllData(true).ToList();
                ProcessBranch(allItems, tol, out var uList, out var cList, out var fList, out var mList, out var dList);

                GH_Path path0 = new GH_Path(0);
                outUnique.EnsurePath(path0);
                foreach (var u in uList) outUnique.Append(u, path0);

                outCounts.EnsurePath(path0);
                foreach (var c in cList) outCounts.Append(new GH_Integer(c), path0);

                outFirstIdx.EnsurePath(path0);
                foreach (var f in fList) outFirstIdx.Append(new GH_Integer(f), path0);

                outDuplicates.EnsurePath(path0);
                foreach (var d in dList) outDuplicates.Append(d, path0);

                int cursor = 0;
                foreach (GH_Path p in inTree.Paths)
                {
                    var branch = inTree.get_Branch(p);
                    outIndexMap.EnsurePath(p);
                    for (int i = 0; i < branch.Count; i++)
                    {
                        outIndexMap.Append(new GH_Integer(mList[cursor]), p);
                        cursor++;
                    }
                }

                totalUniqueGlobal = uList.Count;
            }

            DA.SetDataTree(0, outUnique);
            DA.SetDataTree(1, outCounts);
            DA.SetDataTree(2, outFirstIdx);
            DA.SetDataTree(3, outIndexMap);
            DA.SetDataTree(4, outDuplicates);

            this.Message = $"{totalUniqueGlobal} únicos\n(Original: {inTree.DataCount})";
        }

        private static void ProcessBranch(
            IList items,
            double tol,
            out List<IGH_Goo> uniqueList,
            out List<int> countList,
            out List<int> firstIdxList,
            out List<int> indexMapList,
            out List<IGH_Goo> duplicateList)
        {
            uniqueList = new List<IGH_Goo>();
            countList = new List<int>();
            firstIdxList = new List<int>();
            indexMapList = new List<int>(items.Count);
            duplicateList = new List<IGH_Goo>();

            var keyToIndex = new Dictionary<string, int>();

            for (int i = 0; i < items.Count; i++)
            {
                IGH_Goo item = (IGH_Goo)items[i];
                string key = GetGooKey(item, tol);

                if (keyToIndex.TryGetValue(key, out int existingIdx))
                {
                    countList[existingIdx]++;
                    indexMapList.Add(existingIdx);
                    if (countList[existingIdx] == 2)
                    {
                        duplicateList.Add(uniqueList[existingIdx]);
                    }
                }
                else
                {
                    int newIdx = uniqueList.Count;
                    keyToIndex[key] = newIdx;
                    uniqueList.Add(item);
                    countList.Add(1);
                    firstIdxList.Add(i);
                    indexMapList.Add(newIdx);
                }
            }
        }

        public static string GetGooKey(IGH_Goo goo, double tol)
        {
            if (goo == null) return "null";
            if (goo is GH_Number num)
            {
                if (tol > 0)
                {
                    double rounded = Math.Round(num.Value / tol) * tol;
                    return $"num_{rounded:G12}";
                }
                return $"num_{num.Value}";
            }
            if (goo is GH_Integer integer)
            {
                return $"int_{integer.Value}";
            }
            if (goo is GH_String str)
            {
                return $"str_{str.Value}";
            }
            if (goo is GH_Boolean b)
            {
                return $"bool_{b.Value}";
            }
            if (goo is GH_Point pt)
            {
                if (tol > 0)
                {
                    double rx = Math.Round(pt.Value.X / tol) * tol;
                    double ry = Math.Round(pt.Value.Y / tol) * tol;
                    double rz = Math.Round(pt.Value.Z / tol) * tol;
                    return $"pt_{rx:G8}_{ry:G8}_{rz:G8}";
                }
                return $"pt_{pt.Value.X}_{pt.Value.Y}_{pt.Value.Z}";
            }
            return PillDataFingerprint.ComputeGooFingerprint(goo, tol);
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.BatchDistinct;
    }
}
