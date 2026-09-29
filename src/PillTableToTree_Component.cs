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
    /// Reconstrói uma DataTree a partir de colunas (caminho, índice, valor, tipo), inverso do Pill Tree Table.
    /// </summary>
    public class PillTableToTree_Component : GlauxCapsuleComponent
    {
        public PillTableToTree_Component()
            : base(
                "Pill Table To Tree",
                "PillToTree",
                "Monta uma DataTree a partir de uma tabela longa: Paths (um por valor ou um único para todos), Indices opcionais (posicionam os itens; lacunas viram nulos), Values e Types opcionais.\n" +
                "- Com Types, valores em texto são convertidos ao tipo informado (Number, Integer, Boolean, Point 'x;y;z', Interval 't0;t1', Colour '#AARRGGBB', Time ISO 8601, Guid).\n" +
                "- Branch Paths recria ramos vazios. Inverso exato do Pill Tree Table.",
                "I/O",
                "IO",
                ColorIO)
        {
        }

        public override Guid ComponentGuid => new Guid("9865fde4-306b-43a5-805c-924e3170a4a0");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;
        protected override Bitmap Icon => GlauxToolsIcons.PillTableToTree;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Paths", "P", "Caminho de cada valor ('{0;1}' ou '0;1'). Um único caminho vale para todos.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Indices", "i", "Índice de cada valor no ramo (opcional). Negativo = linha que só declara o ramo.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Values", "V", "Valores (nulos permitidos).", GH_ParamAccess.list);
            pManager.AddTextParameter("Types", "T", "Tipo de cada valor (opcional). Converte valores em texto para o tipo informado.", GH_ParamAccess.list);
            pManager.AddTextParameter("Branch Paths", "BP", "Ramos que devem existir mesmo sem itens (opcional).", GH_ParamAccess.list);
            for (int i = 0; i < 5; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Árvore reconstruída.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Info", "I", "Resumo e avisos da reconstrução.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var paths = new List<string>();
            DA.GetDataList(0, paths);
            var indices = new List<int>();
            DA.GetDataList(1, indices);
            var gooValues = new List<IGH_Goo>();
            DA.GetDataList(2, gooValues);
            var types = new List<string>();
            DA.GetDataList(3, types);
            var branchPaths = new List<string>();
            DA.GetDataList(4, branchPaths);

            var warnings = new List<string>();
            var values = new List<GlauxValue>(gooValues.Count);
            bool useTypes = types.Count > 0;
            if (useTypes && types.Count != gooValues.Count && types.Count != 1)
            {
                warnings.Add($"Quantidade de tipos ({types.Count}) diferente da de valores ({gooValues.Count}); tipos ignorados.");
                useTypes = false;
            }

            for (int r = 0; r < gooValues.Count; r++)
            {
                var goo = gooValues[r];
                string type = useTypes ? (types.Count == 1 ? types[0] : types[r]) : null;
                if (useTypes && goo is GH_String s && !string.IsNullOrEmpty(type) && type != GlauxTypeTags.Text)
                {
                    try
                    {
                        values.Add(TreeCsvCodec.ParseValue(type, s.Value, ""));
                        continue;
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Linha {r}: não foi possível converter '{s.Value}' para {type} ({ex.Message}); mantido como texto.");
                    }
                }
                values.Add(GooCodec.Encode(goo, warnings));
            }

            var table = TreeMapper.FromColumns(paths, indices, values, branchPaths, warnings);
            var tree = TreeMapper.ToTree(table, warnings);

            ReportWarnings(warnings);
            SetCapsule($"{table.ItemCount} itens", table.BranchCount > 0, warnings.Count > 0, $"{table.BranchCount} ramos");
            Message = $"{table.BranchCount} ramos";

            DA.SetDataTree(0, tree);
            DA.SetData(1, $"{table.BranchCount} ramo(s), {table.ItemCount} item(ns), {warnings.Count} aviso(s)");
        }
    }
}
