using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class TreeAlignTopology_Component : GH_Component
    {
        public TreeAlignTopology_Component()
            : base(
                "Tree Align (Match Topology)",
                "TreeAlign",
                "Emparelha e alinha duas Árvores de Dados com topologias assimétricas, preenchendo ramos faltantes com <null> ou valor padrão para evitar quebras em operações 1:1.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("8e9f0a1b-2c3d-4e5f-6a7b-8c9d0e1f2a3b");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Tree A", "A", "Primeira árvore de dados (A).", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Tree B", "B", "Segunda árvore de dados (B).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Mode", "M", "Modo de emparelhamento:\n0 = União (ambas recebem todos os ramos de A e B)\n1 = Alinhar B para A (seguir caminhos de A)\n2 = Alinhar A para B (seguir caminhos de B)\n3 = Interseção (manter apenas caminhos existentes em ambas)", GH_ParamAccess.item, 0);
            pManager.AddGenericParameter("Default Fill A", "fillA", "Valor opcional para preencher ramos inexistentes em A (padrão <null>).", GH_ParamAccess.item);
            pManager.AddGenericParameter("Default Fill B", "fillB", "Valor opcional para preencher ramos inexistentes em B (padrão <null>).", GH_ParamAccess.item);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Aligned A", "A", "Árvore A com topologia alinhada e sincronizada.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Aligned B", "B", "Árvore B com topologia alinhada e sincronizada.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Shared Paths", "P", "Lista dos caminhos unificados na topologia final.", GH_ParamAccess.list);
            pManager.AddTextParameter("Report", "Rep", "Relatório de ramos alinhados e preenchidos.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> treeA) || treeA == null)
            {
                this.Message = "Sem Árvore A";
                return;
            }
            if (!DA.GetDataTree(1, out GH_Structure<IGH_Goo> treeB) || treeB == null)
            {
                this.Message = "Sem Árvore B";
                return;
            }

            int mode = 0;
            DA.GetData(2, ref mode);

            IGH_Goo fillA = null;
            DA.GetData(3, ref fillA);

            IGH_Goo fillB = null;
            DA.GetData(4, ref fillB);

            var pathsA = new HashSet<GH_Path>(treeA.Paths);
            var pathsB = new HashSet<GH_Path>(treeB.Paths);

            List<GH_Path> targetPaths = new List<GH_Path>();

            switch (mode)
            {
                case 0: // União
                    targetPaths = pathsA.Union(pathsB).OrderBy(p => p).ToList();
                    break;
                case 1: // Apenas caminhos de A
                    targetPaths = treeA.Paths.ToList();
                    break;
                case 2: // Apenas caminhos de B
                    targetPaths = treeB.Paths.ToList();
                    break;
                case 3: // Interseção
                    targetPaths = pathsA.Intersect(pathsB).OrderBy(p => p).ToList();
                    break;
                default:
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Modo desconhecido: {mode}. Escolha entre 0 e 3.");
                    return;
            }

            var outA = new GH_Structure<IGH_Goo>();
            var outB = new GH_Structure<IGH_Goo>();
            var pathStrings = new List<string>();

            int filledInA = 0;
            int filledInB = 0;

            foreach (GH_Path path in targetPaths)
            {
                pathStrings.Add(path.ToString());

                // Processar Árvore A
                if (treeA.PathExists(path))
                {
                    var branch = treeA.get_Branch(path);
                    outA.EnsurePath(path);
                    for (int i = 0; i < branch.Count; i++) outA.Append((IGH_Goo)branch[i], path);
                }
                else
                {
                    outA.EnsurePath(path);
                    if (fillA != null) outA.Append(fillA, path);
                    filledInA++;
                }

                // Processar Árvore B
                if (treeB.PathExists(path))
                {
                    var branch = treeB.get_Branch(path);
                    outB.EnsurePath(path);
                    for (int i = 0; i < branch.Count; i++) outB.Append((IGH_Goo)branch[i], path);
                }
                else
                {
                    outB.EnsurePath(path);
                    if (fillB != null) outB.Append(fillB, path);
                    filledInB++;
                }
            }

            string report = $"Tree Alignment Report:\n--------------------\nTopologia Final: {targetPaths.Count} ramos unificados\nRamos preenchidos em A: {filledInA}\nRamos preenchidos em B: {filledInB}\nModo aplicado: {(mode == 0 ? "União" : (mode == 1 ? "Alinhar B->A" : (mode == 2 ? "Alinhar A->B" : "Interseção")))}";

            DA.SetDataTree(0, outA);
            DA.SetDataTree(1, outB);
            DA.SetDataList(2, pathStrings);
            DA.SetData(3, report);

            this.Message = $"{targetPaths.Count} ramos alinhados\n+A:{filledInA} | +B:{filledInB}";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.TreeAlignTopology;
    }
}
