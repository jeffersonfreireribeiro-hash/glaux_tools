using System;
using System.Collections.Generic;
using System.Drawing;
using Buraqueira_Tools.Data;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    /// <summary>Valida uma DataTree antes de persistir: tipos, nulos, duplicatas, faixa e estrutura.</summary>
    public class PillDataValidation_Component : GlauxCapsuleComponent
    {
        public PillDataValidation_Component()
            : base(
                "Pill Data Validation",
                "PillValidate",
                "Valida uma DataTree antes de gravar ou exportar, reunindo num só lugar regras de tipo, nulos, duplicatas (por ramo ou globais), faixa numérica e estrutura (profundidade, número de ramos, comprimento uniforme, ramos obrigatórios).\n" +
                "Emite a lista de problemas com caminho/índice, uma máscara paralela à árvore e a árvore só com os itens aprovados. Conecte 'Valid' ao 'Write' do Pill DB Write para só gravar dados válidos.",
                "Data",
                "DB",
                ColorDB)
        {
        }

        public override Guid ComponentGuid => new Guid("89a5e962-0c8e-4877-91ac-a61aaa196072");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;
        protected override Bitmap Icon => GlauxToolsIcons.PillDataValidation;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Árvore a validar.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Types", "Ty", "Tipos aceitos (Number, Integer, Text, Point, Curve...). Vazio = qualquer.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Allow Nulls", "N", "Aceita itens nulos.", GH_ParamAccess.item, true);
            pManager.AddIntegerParameter("Unique", "U", "Duplicatas: 0 = permitidas, 1 = proibidas no mesmo ramo, 2 = proibidas na árvore toda.", GH_ParamAccess.item, 0);
            pManager.AddIntervalParameter("Range", "Rg", "Faixa numérica aceita (itens não numéricos falham quando há faixa).", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Depth", "Dp", "Profundidade exata exigida dos caminhos (ex: 2 para {a;b}).", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Branch Count", "BC", "Número exato de ramos exigido.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Uniform Length", "UL", "Todos os ramos com a mesma quantidade de itens.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Required Paths", "RP", "Ramos que precisam existir (ex: '{0;0}').", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Require Items", "RI", "A árvore não pode estar vazia.", GH_ParamAccess.item, false);
            for (int i = 1; i < 10; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Valid", "OK", "True se nenhuma regra falhou.", GH_ParamAccess.item);
            pManager.AddTextParameter("Issues", "Is", "Problemas encontrados (regra, caminho, índice e motivo).", GH_ParamAccess.list);
            pManager.AddTextParameter("Issue Paths", "IP", "Caminho de cada problema.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Mask", "M", "Máscara paralela à árvore: True = item aprovado.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Valid Data", "VD", "A árvore só com os itens aprovados (caminhos preservados).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Summary", "S", "Resumo por regra.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> tree)) tree = new GH_Structure<IGH_Goo>();
            var rules = new ValidationRules();
            var types = new List<string>();
            DA.GetDataList(1, types);
            rules.AllowedTypes.AddRange(types);
            bool allowNulls = true;
            DA.GetData(2, ref allowNulls);
            rules.AllowNulls = allowNulls;
            int unique = 0;
            DA.GetData(3, ref unique);
            rules.Unique = (UniqueScope)Math.Max(0, Math.Min(2, unique));
            Interval range = Interval.Unset;
            if (DA.GetData(4, ref range) && range.IsValid)
            {
                rules.Min = range.Min;
                rules.Max = range.Max;
            }
            int depth = 0;
            if (DA.GetData(5, ref depth)) rules.ExpectedDepth = depth;
            int branchCount = 0;
            if (DA.GetData(6, ref branchCount)) rules.ExpectedBranchCount = branchCount;
            bool uniform = false;
            DA.GetData(7, ref uniform);
            rules.UniformBranchLength = uniform;
            var required = new List<string>();
            DA.GetDataList(8, required);
            rules.RequiredPaths.AddRange(required);
            bool requireItems = false;
            DA.GetData(9, ref requireItems);
            rules.RequireItems = requireItems;

            var table = TreeMapper.ToTable(tree);
            var result = TreeValidator.Validate(table, rules);

            var issues = new List<string>();
            var issuePaths = new List<string>();
            var byRule = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var i in result.Issues)
            {
                issues.Add(i.ToString());
                issuePaths.Add(i.PathText);
                byRule.TryGetValue(i.Rule, out int c);
                byRule[i.Rule] = c + 1;
            }

            var mask = new GH_Structure<GH_Boolean>();
            var valid = new GH_Structure<IGH_Goo>();
            for (int b = 0; b < table.BranchCount; b++)
            {
                var path = tree.Paths[b];
                var items = tree.Branches[b];
                var m = result.ItemMask[b];
                mask.EnsurePath(path);
                valid.EnsurePath(path);
                for (int i = 0; i < m.Length; i++)
                {
                    mask.Append(new GH_Boolean(m[i]), path);
                    if (m[i]) valid.Append(items[i], path);
                }
            }

            var parts = new List<string>();
            foreach (var kv in byRule) parts.Add($"{kv.Key}: {kv.Value}");
            string summary = result.IsValid ? $"Válido: {table.ItemCount} item(ns) em {table.BranchCount} ramo(s)." : $"{result.IssueCount} problema(s) — {string.Join(", ", parts)}";
            if (result.Truncated) summary += " (lista de problemas truncada)";

            SetCapsule(result.IsValid ? "Válido" : $"{result.IssueCount} problema(s)", result.IsValid, false);
            Message = result.IsValid ? "OK" : $"{result.IssueCount} falha(s)";
            if (!result.IsValid) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, summary);

            DA.SetData(0, result.IsValid);
            DA.SetDataList(1, issues);
            DA.SetDataList(2, issuePaths);
            DA.SetDataTree(3, mask);
            DA.SetDataTree(4, valid);
            DA.SetData(5, summary);
        }
    }
}
