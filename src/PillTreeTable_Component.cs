using System;
using System.Collections.Generic;
using System.Drawing;
using Buraqueira_Tools.Data;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Visão relacional ("tabela longa") de uma DataTree: uma linha por item com caminho, índice, tipo e valor,
    /// mais a lista de todos os ramos (inclusive vazios). É o inverso do Pill Table To Tree.
    /// </summary>
    public class PillTreeTable_Component : GlauxCapsuleComponent
    {
        public PillTreeTable_Component()
            : base(
                "Pill Tree Table",
                "PillTable",
                "Explode uma DataTree numa tabela longa (uma linha por item): Paths, Indices, Types e Values, sem assumir 'ramo = linha'.\n" +
                "- Branch Paths/Counts listam todos os ramos, inclusive os vazios, para reconstrução exata no Pill Table To Tree.\n" +
                "- Útil para filtrar, ordenar e visualizar árvores irregulares no Data Table Visualizer ou exportar para CSV.",
                "I/O",
                "IO",
                ColorIO)
        {
        }

        public override Guid ComponentGuid => new Guid("8c2a194f-b728-482b-bf33-6e0d91dd5b98");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;
        protected override Bitmap Icon => GlauxToolsIcons.PillTreeTable;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Árvore de dados a explodir.", GH_ParamAccess.tree);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Paths", "P", "Caminho de cada item (ex: '{0;2}').", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Indices", "i", "Índice de cada item dentro do seu ramo.", GH_ParamAccess.list);
            pManager.AddTextParameter("Types", "T", "Tipo de cada item (Number, Integer, Text, Point... ou o tipo Goo completo).", GH_ParamAccess.list);
            pManager.AddGenericParameter("Values", "V", "Valor original de cada item (nulos preservados).", GH_ParamAccess.list);
            pManager.AddTextParameter("Text Values", "TV", "Valor de cada item como texto invariante (ponto decimal, datas ISO 8601).", GH_ParamAccess.list);
            pManager.AddTextParameter("Branch Paths", "BP", "Todos os ramos, inclusive vazios.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Branch Counts", "BC", "Quantidade de itens de cada ramo.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> tree)) tree = new GH_Structure<IGH_Goo>();

            var warnings = new List<string>();
            var table = TreeMapper.ToTable(tree, warnings);
            int n = table.ItemCount;
            var paths = new List<string>(n);
            var indices = new List<int>(n);
            var types = new List<string>(n);
            var values = new List<IGH_Goo>(n);
            var texts = new List<string>(n);
            var branchPaths = new List<string>(table.BranchCount);
            var branchCounts = new List<int>(table.BranchCount);

            for (int b = 0; b < table.BranchCount; b++)
            {
                var branch = table.Branches[b];
                string pathText = branch.PathText;
                branchPaths.Add(pathText);
                branchCounts.Add(branch.Items.Count);
                var gooBranch = tree.Branches[b];
                for (int i = 0; i < branch.Items.Count; i++)
                {
                    var v = branch.Items[i];
                    paths.Add(pathText);
                    indices.Add(i);
                    types.Add(v.TypeTag);
                    values.Add(gooBranch[i]);
                    texts.Add(v.ToDisplayString());
                }
            }

            ReportWarnings(warnings);
            SetCapsule($"{n} linhas", true, false, $"{table.BranchCount} ramos");
            Message = $"{n} × 4";

            DA.SetDataList(0, paths);
            DA.SetDataList(1, indices);
            DA.SetDataList(2, types);
            DA.SetDataList(3, values);
            DA.SetDataList(4, texts);
            DA.SetDataList(5, branchPaths);
            DA.SetDataList(6, branchCounts);
        }
    }
}
