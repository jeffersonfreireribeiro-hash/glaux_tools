using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Buraqueira_Tools.Dashboard;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Monta definições de widgets a partir de listas (tipo, rótulo, configurações, valores) ou dos canais de um grupo
    /// do PillHub. A saída é o mesmo modelo que o Pill Dashboard lê do texto, então dashboards podem ser gerados a partir
    /// de DataTrees, presets ou configurações salvas (a definição textual sai em Definition).
    /// </summary>
    public class PillDashboardBuilder_Component : GlauxCapsuleComponent
    {
        public PillDashboardBuilder_Component()
            : base(
                "Pill Dashboard Builder",
                "DashBuild",
                "Gera widgets para o Pill Dashboard a partir de listas paralelas (lista mais longa, como no Grasshopper):\n" +
                "- Kind: label, number, slider, toggle, button, dropdown, progress, chart.\n" +
                "- Settings: 'min=0 | max=10 | step=0.5 | key=[GEO] Raio' (mesmas chaves das linhas de texto).\n" +
                "- Values: ramo i → widget i. Indicadores mostram o valor; controles o usam como valor padrão.\n" +
                "- Hub Group: cria um indicador por canal do PillHub do grupo (ex: 'ACU'), lido ao vivo pelo painel. A lista de canais é lida quando o Builder calcula; recalcule-o se surgirem canais novos.",
                "Dashboard",
                "DASH",
                ColorDashboard)
        {
        }

        public override Guid ComponentGuid => new Guid("d7eae36c-a08f-4120-bce0-bce93f4c80d9");
        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override Bitmap Icon => GlauxToolsIcons.PillDashboardBuilder;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Kind", "K", "Tipo de cada widget (label, number, slider, toggle, button, dropdown, progress, chart).", GH_ParamAccess.list);
            pManager.AddTextParameter("Label", "L", "Rótulo de cada widget.", GH_ParamAccess.list);
            pManager.AddTextParameter("Settings", "S", "Configurações de cada widget: 'chave=valor | chave=valor'.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Values", "V", "Valores: ramo i para o widget i (ou uma lista simples com um valor por widget).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Hub Group", "H", "Grupo ou chave do PillHub (ex: 'ACU', 'SALA_'): um indicador por canal.", GH_ParamAccess.item);
            for (int i = 0; i < 5; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Widgets", "W", "Widgets para a entrada W do Pill Dashboard.", GH_ParamAccess.list);
            pManager.AddTextParameter("Definition", "Def", "Definição textual equivalente (uma linha por widget), para salvar ou editar num Panel.", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "Resumo e avisos.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var kinds = new List<string>();
            var labels = new List<string>();
            var settings = new List<string>();
            string hubGroup = null;
            DA.GetDataList(0, kinds);
            DA.GetDataList(1, labels);
            DA.GetDataList(2, settings);
            if (!DA.GetDataTree(3, out GH_Structure<IGH_Goo> values)) values = new GH_Structure<IGH_Goo>();
            DA.GetData(4, ref hubGroup);

            var warnings = new List<string>();
            int n = kinds.Count == 0 ? 0 : Math.Max(kinds.Count, Math.Max(labels.Count, settings.Count));
            var branches = values.Branches.Select(b => (IList<WidgetValue>)b.Select(DashboardGoo.FromGoo).ToList()).ToList();
            var perWidget = DashboardBuilder.ValuesPerWidget(branches, DashboardGoo.FromValues, n);
            var widgets = DashboardBuilder.Build(kinds, labels, settings, perWidget, warnings);

            int fromHub = 0;
            if (!string.IsNullOrWhiteSpace(hubGroup))
            {
                foreach (var channel in PillHub.GetChannelsByGroupOrKey(hubGroup))
                {
                    widgets.Add(FromChannel(channel, warnings));
                    fromHub++;
                }
                if (fromHub == 0) warnings.Add($"nenhum canal do PillHub corresponde a '{hubGroup}'.");
            }

            // Mesmas regras de id do painel (ids repetidos ganham sufixo)
            var spec0 = new DashboardSpec();
            DashboardSpecParser.AddWidgets(spec0, widgets);
            warnings.AddRange(spec0.Warnings);

            DA.SetDataList(0, spec0.Widgets.Select(w => new GH_DashboardWidgetGoo(w)));
            DA.SetDataList(1, spec0.Widgets.Select(w => w.ToSpecLine()));
            DA.SetData(2, $"{spec0.Widgets.Count} widget(s)" + (fromHub > 0 ? $", {fromHub} do PillHub" : "") +
                          (warnings.Count > 0 ? Environment.NewLine + string.Join(Environment.NewLine, warnings.Distinct()) : ""));

            SetCapsule(spec0.Widgets.Count == 1 ? "1 widget" : $"{spec0.Widgets.Count} widgets", spec0.Widgets.Count > 0, warnings.Count > 0);
            ReportWarnings(warnings);
        }

        private static WidgetSpec FromChannel(PillChannel channel, ICollection<string> warnings)
        {
            var items = new List<IGH_Goo>();
            if (channel.Data != null) items.AddRange(channel.Data.AllData(false));
            var value = DashboardGoo.FromGooList(items);
            WidgetKind kind = value.Kind == WidgetValueKind.Series ? WidgetKind.MiniChart
                : value.Kind == WidgetValueKind.Number ? WidgetKind.Number
                : WidgetKind.Label;
            var kv = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("key", channel.RawKey ?? channel.CleanKey) };
            if (!string.IsNullOrEmpty(channel.Unit)) kv.Add(new KeyValuePair<string, string>("unit", channel.Unit));
            return WidgetSpec.Create(kind, channel.CleanKey, kv, warnings);
        }
    }
}
