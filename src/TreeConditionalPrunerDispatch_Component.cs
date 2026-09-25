using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class TreeConditionalPrunerDispatch_Component : GH_Component
    {
        public TreeConditionalPrunerDispatch_Component()
            : base(
                "Tree Dispatch (Preserve Paths)",
                "TreeDispatch",
                "Divide uma Árvore de Dados em duas saídas (True / False) preservando a matriz original de caminhos GH_Path (com ramos vazios ou preenchimento com null) para não quebrar árvores a jusante.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("0a1b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Tree", "T", "Árvore de dados de entrada a ser despachada / dividida.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Pattern", "P", "Padrão ou máscara booleana (True = Saída A, False = Saída B).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Preserve Mode", "M", "Modo de preservação de caminhos:\n0 = Manter ramos vazios (caminho existe, sem itens)\n1 = Preencher slots excluídos com <null> (mantém índices e comprimentos idênticos)\n2 = Podar ramos vazios (comportamento nativo)", GH_ParamAccess.item, 0);

            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Tree True (A)", "A", "Árvore contendo os elementos avaliados como True.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Tree False (B)", "B", "Árvore contendo os elementos avaliados como False.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Paths", "P", "Lista dos caminhos processados.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Count A", "NA", "Total de elementos despachados para A.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Count B", "NB", "Total de elementos despachados para B.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) || inTree == null)
            {
                this.Message = "Sem Árvore";
                return;
            }

            if (!DA.GetDataTree(1, out GH_Structure<GH_Boolean> patternTree) || patternTree == null)
            {
                this.Message = "Sem Padrão";
                return;
            }

            int mode = 0;
            DA.GetData(2, ref mode);

            var treeA = new GH_Structure<IGH_Goo>();
            var treeB = new GH_Structure<IGH_Goo>();
            var pathList = new List<string>();

            int countA = 0;
            int countB = 0;

            foreach (GH_Path path in inTree.Paths)
            {
                pathList.Add(path.ToString());
                var branch = inTree.get_Branch(path);

                List<GH_Boolean> patBranch = null;
                if (patternTree.PathExists(path))
                {
                    patBranch = (List<GH_Boolean>)patternTree.get_Branch(path);
                }
                else if (patternTree.Paths.Count == 1)
                {
                    patBranch = (List<GH_Boolean>)patternTree.get_Branch(0);
                }

                var listA = new List<IGH_Goo>();
                var listB = new List<IGH_Goo>();

                for (int i = 0; i < branch.Count; i++)
                {
                    bool isTrue = false;
                    if (patBranch != null && patBranch.Count > 0)
                    {
                        int pIdx = (patBranch.Count == 1) ? 0 : (i % patBranch.Count);
                        if (patBranch[pIdx] != null) isTrue = patBranch[pIdx].Value;
                    }

                    IGH_Goo item = (IGH_Goo)branch[i];

                    if (isTrue)
                    {
                        listA.Add(item);
                        countA++;
                        if (mode == 1) listB.Add(null);
                    }
                    else
                    {
                        listB.Add(item);
                        countB++;
                        if (mode == 1) listA.Add(null);
                    }
                }

                if (mode == 0 || mode == 1)
                {
                    treeA.EnsurePath(path);
                    foreach (var it in listA) treeA.Append(it, path);

                    treeB.EnsurePath(path);
                    foreach (var it in listB) treeB.Append(it, path);
                }
                else
                {
                    if (listA.Count > 0)
                    {
                        treeA.EnsurePath(path);
                        foreach (var it in listA) treeA.Append(it, path);
                    }
                    if (listB.Count > 0)
                    {
                        treeB.EnsurePath(path);
                        foreach (var it in listB) treeB.Append(it, path);
                    }
                }
            }

            DA.SetDataTree(0, treeA);
            DA.SetDataTree(1, treeB);
            DA.SetDataList(2, pathList);
            DA.SetData(3, countA);
            DA.SetData(4, countB);

            this.Message = $"A: {countA} | B: {countB}\n{(mode == 1 ? "Null-Fill" : (mode == 0 ? "PreservePaths" : "Pruned"))}";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.TreeConditionalPrunerDispatch;
    }
}
