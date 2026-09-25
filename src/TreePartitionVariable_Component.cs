using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class TreePartitionVariable_Component : GH_Component
    {
        public TreePartitionVariable_Component()
            : base(
                "Tree Partition (Variable Lengths)",
                "PartVar",
                "Divide listas ou ramos de uma árvore em novos ramos com comprimentos variados e personalizados por ramo (ex.: tamanhos [2, 5, 3, 1]).",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("7d8e9f0a-1b2c-3d4e-5f6a-7b8c9d0e1f2a");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Dados de entrada (lista ou árvore a ser particionada).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Sizes", "S", "Lista de tamanhos de cada partição (ex.: 2, 5, 3).", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Repeat Pattern", "R", "Se True, repete ciclicamente a lista de tamanhos até esgotar os dados. Se False, coloca o restante no último ramo.", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Per Branch", "B", "Se True, particiona cada ramo individualmente gerando sub-ramos {path; i}. Se False, aplana a árvore antes de particionar.", GH_ParamAccess.item, true);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Partitioned Tree", "T", "Árvore de dados resultante com os ramos particionados.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Branch Lengths", "L", "Tamanho real de cada ramo gerado.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Branch Count", "N", "Total de ramos criados.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) || inTree == null)
            {
                this.Message = "Sem Dados";
                return;
            }

            var rawSizes = new List<int>();
            if (!DA.GetDataList(1, rawSizes) || rawSizes == null || rawSizes.Count == 0)
            {
                this.Message = "Sem Tamanhos";
                return;
            }

            var sizes = new List<int>();
            for (int i = 0; i < rawSizes.Count; i++)
            {
                if (rawSizes[i] > 0) sizes.Add(rawSizes[i]);
            }
            if (sizes.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Todos os tamanhos fornecidos são inválidos (devem ser maiores que zero).");
                return;
            }

            bool repeat = true;
            DA.GetData(2, ref repeat);

            bool perBranch = true;
            DA.GetData(3, ref perBranch);

            var outTree = new GH_Structure<IGH_Goo>();
            var branchLengths = new List<int>();

            if (perBranch)
            {
                foreach (GH_Path path in inTree.Paths)
                {
                    var items = inTree.get_Branch(path);
                    int totalItems = items.Count;
                    if (totalItems == 0)
                    {
                        GH_Path subPath = path.AppendElement(0);
                        outTree.EnsurePath(subPath);
                        branchLengths.Add(0);
                        continue;
                    }

                    int cursor = 0;
                    int sizeIdx = 0;
                    int chunkIdx = 0;

                    while (cursor < totalItems)
                    {
                        int currentSize;
                        if (sizeIdx < sizes.Count)
                        {
                            currentSize = sizes[sizeIdx];
                            sizeIdx++;
                        }
                        else if (repeat)
                        {
                            sizeIdx = 0;
                            currentSize = sizes[sizeIdx];
                            sizeIdx++;
                        }
                        else
                        {
                            currentSize = totalItems - cursor;
                        }

                        int take = Math.Min(currentSize, totalItems - cursor);
                        GH_Path subPath = path.AppendElement(chunkIdx);
                        outTree.EnsurePath(subPath);

                        for (int k = 0; k < take; k++)
                        {
                            outTree.Append((IGH_Goo)items[cursor + k], subPath);
                        }

                        branchLengths.Add(take);
                        cursor += take;
                        chunkIdx++;
                    }
                }
            }
            else // Flatten tree first
            {
                var allItems = inTree.AllData(true).ToList();
                int totalItems = allItems.Count;
                int cursor = 0;
                int sizeIdx = 0;
                int chunkIdx = 0;

                while (cursor < totalItems)
                {
                    int currentSize;
                    if (sizeIdx < sizes.Count)
                    {
                        currentSize = sizes[sizeIdx];
                        sizeIdx++;
                    }
                    else if (repeat)
                    {
                        sizeIdx = 0;
                        currentSize = sizes[sizeIdx];
                        sizeIdx++;
                    }
                    else
                    {
                        currentSize = totalItems - cursor;
                    }

                    int take = Math.Min(currentSize, totalItems - cursor);
                    GH_Path path = new GH_Path(chunkIdx);
                    outTree.EnsurePath(path);

                    for (int k = 0; k < take; k++)
                    {
                        outTree.Append(allItems[cursor + k], path);
                    }

                    branchLengths.Add(take);
                    cursor += take;
                    chunkIdx++;
                }
            }

            DA.SetDataTree(0, outTree);
            DA.SetDataList(1, branchLengths);
            DA.SetData(2, outTree.PathCount);

            this.Message = $"{outTree.PathCount} partições\n({string.Join(",", sizes)})";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.TreePartitionVariable;
    }
}
