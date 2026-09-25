using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class TreeFilter_Component : GH_Component
    {
        public TreeFilter_Component()
            : base(
                "Tree Filter (Preserve Paths)",
                "TreeFilter",
                "Filtra itens em uma Árvore de Dados (DataTree) com base em uma máscara booleana ou condição, sem perder, colapsar ou alterar os caminhos (GH_Path) originais.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("5b6c7d8e-9f0a-1b2c-3d4e-5f6a7b8c9d0e");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Tree", "T", "Árvore de dados de entrada a ser filtrada.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Mask", "M", "Máscara booleana (DataTree ou lista com valores True para manter e False para filtrar).", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Preserve Empty Branches", "E", "Se True, mantém os ramos onde todos os itens foram filtrados na árvore (ramos vazios). Se False, remove o ramo.", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Replace With Null", "N", "Se True, substitui os itens filtrados por <null> mantendo o índice e contagem exatos por ramo.", GH_ParamAccess.item, false);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Filtered Tree", "T", "Árvore de dados resultante com os mesmos caminhos GH_Path da original.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Retained Indices", "i", "Índices originais dos itens mantidos em cada ramo.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Count", "N", "Quantidade total de itens retidos na árvore.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) || inTree == null)
            {
                this.Message = "Sem Árvore";
                return;
            }

            if (!DA.GetDataTree(1, out GH_Structure<GH_Boolean> maskTree) || maskTree == null)
            {
                this.Message = "Sem Máscara";
                return;
            }

            bool preserveEmpty = true;
            DA.GetData(2, ref preserveEmpty);

            bool replaceWithNull = false;
            DA.GetData(3, ref replaceWithNull);

            var outTree = new GH_Structure<IGH_Goo>();
            var indexTree = new GH_Structure<GH_Integer>();
            int totalRetained = 0;

            foreach (GH_Path path in inTree.Paths)
            {
                var branchItems = inTree.get_Branch(path);
                int count = branchItems.Count;

                List<GH_Boolean> maskBranch = null;
                if (maskTree.PathExists(path))
                {
                    maskBranch = (List<GH_Boolean>)maskTree.get_Branch(path);
                }
                else if (maskTree.Paths.Count == 1)
                {
                    maskBranch = (List<GH_Boolean>)maskTree.get_Branch(0);
                }

                var filteredBranch = new List<IGH_Goo>();
                var retainedIdx = new List<GH_Integer>();

                for (int i = 0; i < count; i++)
                {
                    bool keep = false;
                    if (maskBranch != null && maskBranch.Count > 0)
                    {
                        int maskIdx = (maskBranch.Count == 1) ? 0 : (i % maskBranch.Count);
                        if (maskBranch[maskIdx] != null)
                        {
                            keep = maskBranch[maskIdx].Value;
                        }
                    }

                    if (keep)
                    {
                        filteredBranch.Add((IGH_Goo)branchItems[i]);
                        retainedIdx.Add(new GH_Integer(i));
                        totalRetained++;
                    }
                    else if (replaceWithNull)
                    {
                        filteredBranch.Add(null);
                    }
                }

                if (filteredBranch.Count > 0 || preserveEmpty)
                {
                    outTree.EnsurePath(path);
                    foreach (var item in filteredBranch)
                    {
                        outTree.Append(item, path);
                    }

                    indexTree.EnsurePath(path);
                    foreach (var idx in retainedIdx)
                    {
                        indexTree.Append(idx, path);
                    }
                }
            }

            DA.SetDataTree(0, outTree);
            DA.SetDataTree(1, indexTree);
            DA.SetData(2, totalRetained);

            this.Message = $"{totalRetained} itens\n{outTree.PathCount} ramos";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.TreeFilter;
    }
}
