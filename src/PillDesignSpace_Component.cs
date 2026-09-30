using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Buraqueira_Tools.Explore;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Define as variáveis de projeto a explorar: controles ligados por fio e/ou escolhidos pelo nome, com faixas,
    /// passos e níveis opcionais. Não altera nada no canvas.
    /// </summary>
    public class PillDesignSpace_Component : GlauxCapsuleComponent
    {
        public PillDesignSpace_Component()
            : base(
                "Pill Design Space",
                "DesignSpace",
                "Define as variáveis de projeto a explorar (o que o Pill Sampler vai variar e o Pill Batch Runner vai aplicar):\n" +
                "- Controls: ligue sliders, toggles, value lists, um Pill Slider Pool ou um Pill Dashboard (todos os controles dele);\n" +
                "- Names: ou escolha controles pelo nome, com curingas ('Largura', '[VAR]*', '*');\n" +
                "- Ranges: ajuste faixas e níveis: 'Largura | min=2 | max=8 | step=0.5', 'Material | levels=Concreto;Madeira', 'Rotação | off'.\n" +
                "Por padrão vale a faixa do próprio controle; o passo vem da precisão do slider (inteiro, 1 casa decimal...).",
                "Explore",
                "EXPLORE",
                ColorExplore)
        {
        }

        public override Guid ComponentGuid => new Guid("36f19785-0348-4a2c-8fe3-60c984a432f7");
        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override Bitmap Icon => GlauxToolsIcons.PillDesignSpace;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Controls", "C", "Controles a variar (ligue sliders, toggles, value lists, Pill Slider Pool ou Pill Dashboard). Só a origem do fio importa, não o valor.", GH_ParamAccess.list);
            pManager.AddTextParameter("Names", "N", "Controles pelo nome ou apelido, com curingas (* e ?). Sliders do Slider Pool: '[CATEGORIA] Nome'; do Dashboard: 'Painel: Rótulo'.", GH_ParamAccess.list);
            pManager.AddTextParameter("Ranges", "R", "Uma linha por ajuste: 'Nome | min=… | max=… | step=…', 'Nome | levels=A;B;C' ou 'Nome | off'. O nome aceita curingas.", GH_ParamAccess.list);
            for (int i = 0; i < 3; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Space", "DS", "Espaço de projeto (ligue no Pill Sampler).", GH_ParamAccess.item);
            pManager.AddTextParameter("Names", "N", "Nome de cada variável, na ordem das colunas das amostras.", GH_ParamAccess.list);
            pManager.AddTextParameter("Ranges", "R", "Faixa, passo ou níveis de cada variável.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Count", "k", "Quantidade de variáveis.", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "I", "Resumo (tamanho do espaço discreto, avisos).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var doc = OnPingDocument();
            var warnings = new List<string>();
            var variables = new List<DesignVariable>();

            // 1. Controles ligados por fio (a origem de cada fio; Slider Pool e Dashboard entram inteiros)
            var seenOwners = new HashSet<Guid>();
            foreach (var src in Params.Input[0].Sources)
            {
                var owner = src?.Attributes?.GetTopLevel?.DocObject ?? src;
                if (owner == null || !seenOwners.Add(owner.InstanceGuid)) continue;
                if (!DesignControls.IsSupported(owner))
                {
                    warnings.Add($"'{DesignControls.Label(owner)}' não é um controle suportado (slider, toggle, value list, Pill Slider Pool, Pill Dashboard).");
                    continue;
                }
                variables.AddRange(DesignControls.FromObject(owner, warnings));
            }

            // 2. Controles pelo nome
            var names = new List<string>();
            DA.GetDataList(1, names);
            names = names.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList();
            if (names.Count > 0)
            {
                var all = DesignControls.Discover(doc, null);
                foreach (var pattern in names)
                {
                    var hits = all.Where(v => DesignRangeOverride.WildcardMatch(pattern, v.Name) || string.Equals(pattern, v.Id, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (hits.Count == 0) warnings.Add($"Nenhum controle chamado '{pattern}'.");
                    variables.AddRange(hits);
                }
            }

            if (variables.Count == 0)
            {
                SetCapsule("Sem variáveis", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Ligue controles em Controls ou escreva os nomes em Names.");
                ReportWarnings(warnings);
                return;
            }

            // 3. Faixas
            var rules = new List<DesignRangeOverride>();
            var ranges = new List<string>();
            DA.GetDataList(2, ranges);
            foreach (var line in ranges)
            {
                if (DesignRangeOverride.TryParse(line, out var rule, out string error)) rules.Add(rule);
                else if (error != null) AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error);
            }

            DesignSpace space;
            try
            {
                var original = new DesignSpace(variables);
                space = original.WithOverrides(rules, warnings);
                foreach (var v in space.Variables)
                {
                    int i = original.IndexOf(v.Id);
                    if (i >= 0) DesignControls.CheckAgainstControl(original.Variables[i], v, warnings);
                }
            }
            catch (ArgumentException ex)
            {
                SetCapsule("Erro", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                return;
            }

            var duplicated = space.Variables.GroupBy(v => v.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicated.Count > 0) warnings.Add($"Nomes repetidos ({string.Join(", ", duplicated)}): dê apelidos diferentes aos controles para ler a sensibilidade sem ambiguidade.");
            if (space.Count == 0) warnings.Add("Todas as variáveis foram desligadas por 'off'.");

            ReportWarnings(warnings);
            double? size = space.DiscreteSize();
            string sizeText = size.HasValue ? (size.Value > 1e15 ? $"{size.Value:E2} combinações" : $"{size.Value:N0} combinações") : "espaço contínuo";
            SetCapsule($"{space.Count} variáveis", space.Count > 0, warnings.Count > 0);
            Message = $"{space.Count} var";

            DA.SetData(0, new GH_DesignSpaceGoo(space));
            DA.SetDataList(1, space.Variables.Select(v => v.Name));
            DA.SetDataList(2, space.Describe());
            DA.SetData(3, space.Count);
            DA.SetData(4, $"{space.Count} variáveis · {sizeText}");
        }
    }
}
