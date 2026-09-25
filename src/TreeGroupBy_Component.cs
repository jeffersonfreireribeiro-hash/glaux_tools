using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class TreeGroupBy_Component : GH_Component
    {
        public TreeGroupBy_Component()
            : base(
                "Tree GroupBy (Bucket by Key)",
                "GroupBy",
                "Agrupa itens de uma lista ou árvore em novos ramos com base em uma chave (ex.: orientação solar, tipo construtivo, área, camada). Substitui todo o processo manual de sets, map e split.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("1b2c3d4e-5f6a-7b8c-9d0e-1f2a3b4c5d6e");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Elementos a serem agrupados (geometria, números, texto, etc.).", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Keys", "K", "Chaves de agrupamento correspondentes 1:1 com os dados.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Key Tolerance", "Tol", "Tolerância para chaves numéricas contínuas (padrão: 1e-4).", GH_ParamAccess.item, 1e-4);
            pManager.AddBooleanParameter("Per Branch", "B", "Se True, agrupa individualmente por ramo gerando sub-ramos {path; key_idx}. Se False, agrupa globalmente.", GH_ParamAccess.item, false);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Grouped Tree", "T", "Árvore de dados com os elementos organizados em ramos por chave.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Unique Keys", "K", "Chaves únicas correspondentes a cada ramo da árvore de saída.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Counts", "C", "Quantidade de elementos em cada grupo.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Original Indices", "i", "Índices originais dos itens em cada grupo.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> dataTree) || dataTree == null)
            {
                this.Message = "Sem Dados";
                return;
            }

            if (!DA.GetDataTree(1, out GH_Structure<IGH_Goo> keysTree) || keysTree == null)
            {
                this.Message = "Sem Chaves";
                return;
            }

            double tol = 1e-4;
            DA.GetData(2, ref tol);

            bool perBranch = false;
            DA.GetData(3, ref perBranch);

            var outTree = new GH_Structure<IGH_Goo>();
            var outKeys = new GH_Structure<IGH_Goo>();
            var outCounts = new GH_Structure<GH_Integer>();
            var outIndices = new GH_Structure<GH_Integer>();

            int totalGroups = 0;

            if (perBranch)
            {
                foreach (GH_Path path in dataTree.Paths)
                {
                    var dItems = dataTree.get_Branch(path);
                    IList kItems = null;

                    if (keysTree.PathExists(path))
                    {
                        kItems = keysTree.get_Branch(path);
                    }
                    else if (keysTree.Paths.Count == 1)
                    {
                        kItems = keysTree.get_Branch(0);
                    }

                    if (kItems == null || kItems.Count == 0) continue;

                    GroupItems(dItems, kItems, tol, out var groupMap, out var keyOrder, out var keyGooMap);

                    for (int k = 0; k < keyOrder.Count; k++)
                    {
                        string kStr = keyOrder[k];
                        var (items, origIdx) = groupMap[kStr];

                        GH_Path subPath = path.AppendElement(k);
                        outTree.EnsurePath(subPath);
                        foreach (var it in items) outTree.Append(it, subPath);

                        outKeys.EnsurePath(path);
                        outKeys.Append(keyGooMap[kStr], path);

                        outCounts.EnsurePath(path);
                        outCounts.Append(new GH_Integer(items.Count), path);

                        outIndices.EnsurePath(subPath);
                        foreach (var idx in origIdx) outIndices.Append(new GH_Integer(idx), subPath);
                    }

                    totalGroups += keyOrder.Count;
                }
            }
            else // Global Grouping
            {
                var allData = dataTree.AllData(true).ToList();
                var allKeys = keysTree.AllData(true).ToList();

                if (allKeys.Count == 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Nenhuma chave fornecida.");
                    return;
                }

                GroupItems(allData, allKeys, tol, out var groupMap, out var keyOrder, out var keyGooMap);

                GH_Path keyRootPath = new GH_Path(0);
                for (int k = 0; k < keyOrder.Count; k++)
                {
                    string kStr = keyOrder[k];
                    var (items, origIdx) = groupMap[kStr];

                    GH_Path groupPath = new GH_Path(k);
                    outTree.EnsurePath(groupPath);
                    foreach (var it in items) outTree.Append(it, groupPath);

                    outKeys.EnsurePath(keyRootPath);
                    outKeys.Append(keyGooMap[kStr], keyRootPath);

                    outCounts.EnsurePath(keyRootPath);
                    outCounts.Append(new GH_Integer(items.Count), keyRootPath);

                    outIndices.EnsurePath(groupPath);
                    foreach (var idx in origIdx) outIndices.Append(new GH_Integer(idx), groupPath);
                }

                totalGroups = keyOrder.Count;
            }

            DA.SetDataTree(0, outTree);
            DA.SetDataTree(1, outKeys);
            DA.SetDataTree(2, outCounts);
            DA.SetDataTree(3, outIndices);

            this.Message = $"{totalGroups} grupos criados\n(Total: {dataTree.DataCount})";
        }

        private static void GroupItems(
            IList data,
            IList keys,
            double tol,
            out Dictionary<string, (List<IGH_Goo> items, List<int> indices)> groupMap,
            out List<string> keyOrder,
            out Dictionary<string, IGH_Goo> keyGooMap)
        {
            groupMap = new Dictionary<string, (List<IGH_Goo>, List<int>)>();
            keyOrder = new List<string>();
            keyGooMap = new Dictionary<string, IGH_Goo>();

            int count = data.Count;
            int keyCount = keys.Count;

            for (int i = 0; i < count; i++)
            {
                IGH_Goo kGoo = (IGH_Goo)keys[i % keyCount];
                string kStr = BatchDistinct_Component.GetGooKey(kGoo, tol);

                if (!groupMap.TryGetValue(kStr, out var tuple))
                {
                    tuple = (new List<IGH_Goo>(), new List<int>());
                    groupMap[kStr] = tuple;
                    keyOrder.Add(kStr);
                    keyGooMap[kStr] = kGoo;
                }

                tuple.items.Add((IGH_Goo)data[i]);
                tuple.indices.Add(i);
            }
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.TreeGroupBy;
    }
}
