using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class DataStack_Component : GH_Component
    {
        public DataStack_Component()
            : base(
                "Data Stack (VSTACK / HSTACK)",
                "DataStack",
                "Empilha e combina conjuntos de dados em pilha vertical (VSTACK / concatenação de ramos) ou horizontal (HSTACK / alinhamento de colunas/matrizes lado a lado com preenchimento seguro), similar às novas funções de matriz do Excel e NumPy.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("f7a8b90c-1d2e-3f4a-5b6c-7d8e9f0a1b2c");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data A", "A", "Primeiro conjunto de dados / árvore.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Data B", "B", "Segundo conjunto de dados / árvore.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Data C", "C", "Terceiro conjunto opcional de dados / árvore.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Data D", "D", "Quarto conjunto opcional de dados / árvore.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Mode", "M", "Modo: 0=Vertical Stack (VSTACK: empilha listas/ramos sequencialmente), 1=Horizontal Stack (HSTACK: alinha colunas lado a lado por linha {row; col}).", GH_ParamAccess.item, 0);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Stacked Tree", "T", "Árvore de dados resultante com a estrutura empilhada.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Total Branches", "B", "Quantidade total de ramos na árvore resultante.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Total Items", "N", "Quantidade total de elementos empilhados.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var trees = new List<GH_Structure<IGH_Goo>>();

            if (DA.GetDataTree(0, out GH_Structure<IGH_Goo> treeA) && treeA != null && !treeA.IsEmpty)
                trees.Add(treeA);

            if (DA.GetDataTree(1, out GH_Structure<IGH_Goo> treeB) && treeB != null && !treeB.IsEmpty)
                trees.Add(treeB);

            if (DA.GetDataTree(2, out GH_Structure<IGH_Goo> treeC) && treeC != null && !treeC.IsEmpty)
                trees.Add(treeC);

            if (DA.GetDataTree(3, out GH_Structure<IGH_Goo> treeD) && treeD != null && !treeD.IsEmpty)
                trees.Add(treeD);

            if (trees.Count == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            int mode = 0;
            DA.GetData(4, ref mode);

            var outTree = new GH_Structure<IGH_Goo>();
            int totalItems = 0;

            if (mode == 0)
            {
                // ==============================================================
                // VSTACK: EMPILHAMENTO VERTICAL (Adiciona os ramos sequencialmente)
                // ==============================================================
                int branchIndex = 0;
                for (int t = 0; t < trees.Count; t++)
                {
                    var tree = trees[t];
                    for (int b = 0; b < tree.Branches.Count; b++)
                    {
                        var branch = tree.Branches[b];
                        var newPath = new GH_Path(branchIndex);

                        for (int i = 0; i < branch.Count; i++)
                        {
                            outTree.Append(branch[i], newPath);
                            totalItems++;
                        }
                        branchIndex++;
                    }
                }
            }
            else
            {
                // ==============================================================
                // HSTACK: EMPILHAMENTO HORIZONTAL (Alinha colunas {row; col})
                // ==============================================================
                // Determina a quantidade máxima de itens entre todas as primeiras branches ou colunas
                int maxRows = 0;
                for (int t = 0; t < trees.Count; t++)
                {
                    foreach (var branch in trees[t].Branches)
                    {
                        if (branch.Count > maxRows) maxRows = branch.Count;
                    }
                }

                int colIndex = 0;
                for (int t = 0; t < trees.Count; t++)
                {
                    var tree = trees[t];
                    for (int b = 0; b < tree.Branches.Count; b++)
                    {
                        var branch = tree.Branches[b];
                        for (int r = 0; r < maxRows; r++)
                        {
                            var rowPath = new GH_Path(r, colIndex);
                            if (r < branch.Count)
                            {
                                outTree.Append(branch[r], rowPath);
                                totalItems++;
                            }
                            else
                            {
                                outTree.Append(new GH_String(""), rowPath);
                            }
                        }
                        colIndex++;
                    }
                }
            }

            DA.SetDataTree(0, outTree);
            DA.SetData(1, outTree.Paths.Count);
            DA.SetData(2, totalItems);

            this.Message = (mode == 0 ? "VSTACK" : "HSTACK") + $"\n{outTree.Paths.Count} Ramos";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.DataStack;
    }
}
