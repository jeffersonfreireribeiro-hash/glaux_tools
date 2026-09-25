using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class TreeStructuralDiff_Component : GH_Component
    {
        public TreeStructuralDiff_Component()
            : base(
                "Tree Structural Diff",
                "TreeDiff",
                "Compara duas árvores de dados (A e B) e diagnostica discrepâncias: ramos exclusivos de A, ramos exclusivos de B, contagens divergentes e equivalência topológica.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("3d4e5f6a-7b8c-9d0e-1f2a-3b4c5d6e7f8a");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Tree A", "A", "Primeira árvore de dados (A) para comparação.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Tree B", "B", "Segunda árvore de dados (B) para comparação.", GH_ParamAccess.tree);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Exclusive to A", "A_only", "Caminhos que existem somente na Árvore A.", GH_ParamAccess.list);
            pManager.AddTextParameter("Exclusive to B", "B_only", "Caminhos que existem somente na Árvore B.", GH_ParamAccess.list);
            pManager.AddTextParameter("Shared Paths", "Shared", "Caminhos que existem em ambas as árvores.", GH_ParamAccess.list);
            pManager.AddTextParameter("Divergent Count Paths", "DiffCnt", "Caminhos compartilhados onde a quantidade de itens difere entre A e B.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Count Delta (A - B)", "ΔCount", "Diferença numérica de itens (Count A - Count B) para os caminhos compartilhados.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Is Identical Topology?", "Identical?", "True se as árvores possuem exatamente os mesmos caminhos e contagens por ramo.", GH_ParamAccess.item);
            pManager.AddTextParameter("Report", "Rep", "Relatório de diagnóstico estrutural e topológico.", GH_ParamAccess.item);
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

            var setA = new HashSet<GH_Path>(treeA.Paths);
            var setB = new HashSet<GH_Path>(treeB.Paths);

            var aOnly = setA.Except(setB).OrderBy(p => p).Select(p => p.ToString()).ToList();
            var bOnly = setB.Except(setA).OrderBy(p => p).Select(p => p.ToString()).ToList();
            var shared = setA.Intersect(setB).OrderBy(p => p).ToList();

            var sharedStr = new List<string>();
            var diffCountPaths = new List<string>();
            var deltaCounts = new List<int>();

            foreach (var p in shared)
            {
                sharedStr.Add(p.ToString());
                int countA = treeA.get_Branch(p).Count;
                int countB = treeB.get_Branch(p).Count;
                int delta = countA - countB;
                deltaCounts.Add(delta);

                if (delta != 0)
                {
                    diffCountPaths.Add($"{p} (A:{countA} != B:{countB})");
                }
            }

            bool isIdentical = (aOnly.Count == 0) && (bOnly.Count == 0) && (diffCountPaths.Count == 0);

            string report = $"Tree Structural Diff Summary:\n-----------------------------\nTopologia Idêntica? {(isIdentical ? "SIM (100% Compatível)" : "NÃO (Divergências encontradas)")}\n\n• Árvore A: {treeA.PathCount} ramos, {treeA.DataCount} itens\n• Árvore B: {treeB.PathCount} ramos, {treeB.DataCount} itens\n• Ramos Compartilhados: {shared.Count}\n• Exclusivos de A: {aOnly.Count}\n• Exclusivos de B: {bOnly.Count}\n• Ramos com Contagens Diferentes: {diffCountPaths.Count}";

            DA.SetDataList(0, aOnly);
            DA.SetDataList(1, bOnly);
            DA.SetDataList(2, sharedStr);
            DA.SetDataList(3, diffCountPaths);
            DA.SetDataList(4, deltaCounts);
            DA.SetData(5, isIdentical);
            DA.SetData(6, report);

            this.Message = isIdentical ? "Topologia 100% Idêntica" : $"Diff: +A:{aOnly.Count} | +B:{bOnly.Count}\nΔCnt: {diffCountPaths.Count}";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.TreeStructuralDiff;
    }
}
