using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Buraqueira_Tools.Dashboard;
using Buraqueira_Tools.ProjectState;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Pill Dashboard: painel interativo no canvas que reúne controles (slider, toggle, botão, dropdown) e indicadores
    /// (texto, número, progresso, mini gráfico). O componente só orquestra: a configuração vem da entrada W (texto ou
    /// Pill Dashboard Builder), o estado dos controles fica no <see cref="DashboardController"/>, e os atributos
    /// traduzem o mouse. Controles com 'key=' publicam no PillHub; indicadores com 'key=' leem o PillHub na pintura.
    /// </summary>
    public class PillDashboard_Component : GH_Component, IPillControlStateProvider, IPillHubPublisher
    {
        private readonly HashSet<string> _publishedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _warnings = new List<string>();
        private string _pendingChanged;
        private string _lastLoadHash = "";

        public PillDashboard_Component()
            : base(
                "Pill Dashboard",
                "PillDash",
                "Painel interativo no canvas: reúne controles e indicadores num só componente, sem espalhar sliders, toggles e panels.\n" +
                "- W: uma linha por widget, ex: 'slider Largura | min=0 | max=10 | step=0.5 | key=[GEO] Largura', ou widgets do Pill Dashboard Builder.\n" +
                "  Tipos: label, number, slider, toggle, button, dropdown, progress, chart. Painel: 'title=', 'layout=stack|row|grid', 'columns=', 'width='.\n" +
                "- Controles saem em V (um ramo por controle) e, com 'key=', no PillHub (leia com Pill Receiver em qualquer lugar).\n" +
                "- Indicadores com 'key=' mostram canais do PillHub sem fios e sem recalcular nada; ou use D (PillBundle / 'nome=valor').\n" +
                "- Arrasto: commit=auto (padrão) entrega durante o arrasto enquanto a solução é leve e só ao soltar quando é pesada; commit=live|release força o modo.\n" +
                "- S/LS: estado dos controles em linhas 'id=valor' para presets, Pill DB Write, Pill Snapshot e Pill Restore.",
                "Glaux Tools",
                "Dashboard")
        {
            Controller.LiveSource = new HubLiveSource();
            Controller.BeforeChange = label => RecordUndoEvent("Dashboard: " + label);
        }

        public override Guid ComponentGuid => new Guid("49ee50b1-5842-466b-89ee-cd9ff6df435d");
        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override Bitmap Icon => GlauxToolsIcons.PillDashboard;

        internal DashboardController Controller { get; } = new DashboardController();

        internal string DisplayTitle => string.IsNullOrWhiteSpace(Controller.Spec.Title) || Controller.Spec.Title == "Pill Dashboard"
            ? (NickName == "PillDash" ? "Pill Dashboard" : NickName)
            : Controller.Spec.Title;

        internal string Badge
        {
            get
            {
                int n = Controller.Widgets.Count;
                return n == 0 ? null : n == 1 ? "1 widget" : n + " widgets";
            }
        }

        public IEnumerable<string> PublishedCleanKeys => _publishedKeys.ToList();

        public override void CreateAttributes()
        {
            m_attributes = new PillDashboard_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Widgets", "W", "Definições: linhas de texto ('slider Largura | min=0 | max=10') e/ou widgets do Pill Dashboard Builder.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Data", "D", "Dados dos indicadores: PillBundle(s) ou textos 'nome=valor' (casados por 'source=', id ou rótulo). Resultados que dependem dos controles deste painel devem vir por 'key=' (PillHub), não por fio: fio criaria um ciclo.", GH_ParamAccess.list);
            pManager.AddTextParameter("Load State", "LS", "Estado a aplicar ('id=valor' por linha), ex: saída S de outro painel, Pill Restore ou Pill DB Read. Aplicado só quando muda.", GH_ParamAccess.list);
            for (int i = 0; i < 3; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Values", "V", "Valores dos controles: um ramo por controle, na ordem da definição (slider = número, toggle/botão = booleano, dropdown = texto ou índice com output=index).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Names", "N", "Rótulo de cada controle (mesma ordem dos ramos de V).", GH_ParamAccess.list);
            pManager.AddTextParameter("State", "S", "Estado dos controles persistentes em linhas 'id=valor' (para presets e snapshots).", GH_ParamAccess.list);
            pManager.AddTextParameter("Changed", "C", "Id do controle que o usuário mudou e disparou esta solução (vazio se a solução veio de outro lugar).", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "I", "Resumo: widgets, ligações com o PillHub, política de commit, última solução e avisos.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            _warnings.Clear();

            // 1. Configuração
            var raw = new List<IGH_Goo>();
            DA.GetDataList(0, raw);
            var lines = new List<string>();
            var builderWidgets = new List<WidgetSpec>();
            foreach (var goo in raw)
            {
                if (goo is GH_DashboardWidgetGoo wg && wg.Value != null) builderWidgets.Add(wg.Value);
                else if (goo is GH_String s) lines.Add(s.Value);
                else if (goo != null) lines.Add(goo.ToString());
            }
            var spec = DashboardSpecParser.Parse(lines, builderWidgets);
            _warnings.AddRange(spec.Warnings);
            if (Controller.SetSpec(spec)) m_attributes?.ExpireLayout();

            // 2. Dados dos indicadores
            var data = new List<IGH_Goo>();
            DA.GetDataList(1, data);
            Controller.SetNamedData(DashboardGoo.NamedData(data, _warnings));

            // 3. Estado vindo de fora (só quando muda: não briga com o que o usuário mexe no painel)
            var load = new List<string>();
            DA.GetDataList(2, load);
            string loadHash = Hash(load);
            if (load.Count > 0 && loadHash != _lastLoadHash)
            {
                var changed = Controller.State.Apply(load, spec, _warnings);
                if (changed.Count > 0) _pendingChanged = string.Join(",", changed);
            }
            _lastLoadHash = load.Count > 0 ? loadHash : "";

            // 4. Saídas e PillHub
            var values = new GH_Structure<IGH_Goo>();
            var names = new List<string>();
            var doc = OnPingDocument();
            Guid docId = doc?.DocumentID ?? Guid.Empty;
            var currentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int index = 0;
            foreach (var w in Controller.Widgets)
            {
                if (!w.Spec.IsControl) continue;
                var value = Controller.State.Effective(w.Spec);
                var goo = DashboardGoo.ToGoo(w.Spec, value);
                var path = new GH_Path(index++);
                values.EnsurePath(path);
                if (goo != null) values.Append(goo, path);
                names.Add(w.Spec.Label.Length > 0 ? w.Spec.Label : w.Id);
                if (w.Spec.HubKey != null && goo != null)
                {
                    Publish(w.Spec, goo, docId);
                    currentKeys.Add(PillHub.CleanUpKey(w.Spec.HubKey));
                }
            }
            foreach (var old in _publishedKeys.ToList())
            {
                if (currentKeys.Contains(old)) continue;
                PillHub.Unpublish(old, InstanceGuid);
                _publishedKeys.Remove(old);
            }

            DA.SetDataTree(0, values);
            DA.SetDataList(1, names);
            DA.SetDataList(2, Controller.State.ToLines(spec));
            DA.SetData(3, _pendingChanged ?? "");
            DA.SetData(4, BuildInfo(spec));
            _pendingChanged = null;

            foreach (var w in _warnings.Distinct().Take(6)) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
            if (_warnings.Count > 6) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"… mais {_warnings.Count - 6} aviso(s); veja I.");
            Message = Controller.Widgets.Count == 0 ? "vazio" : null;
        }

        private void Publish(WidgetSpec spec, IGH_Goo goo, Guid docId)
        {
            var tree = new GH_Structure<IGH_Goo>();
            tree.Append(goo);
            PillHub.Publish(spec.HubKey, tree, InstanceGuid, docId, string.IsNullOrEmpty(spec.Unit) ? null : spec.Unit, false, NickName);
            _publishedKeys.Add(PillHub.CleanUpKey(spec.HubKey));
        }

        /// <summary>Estado mudou por interação: publica já (os receptores entram na mesma solução) e marca o controle.</summary>
        internal void PrepareCommit(string widgetId)
        {
            _pendingChanged = widgetId;
            var spec = Controller.Spec.Find(widgetId);
            if (spec?.HubKey == null) return;
            var goo = DashboardGoo.ToGoo(spec, Controller.State.Effective(spec));
            if (goo != null) Publish(spec, goo, OnPingDocument()?.DocumentID ?? Guid.Empty);
        }

        /// <summary>
        /// Commit disparado no canvas: publica, recalcula (síncrono) e mede quanto a solução levou; a medida alimenta o
        /// modo Auto (Live → Release) e o throttle adaptativo do próximo arrasto.
        /// </summary>
        internal void CommitFromCanvas(string widgetId)
        {
            PrepareCommit(widgetId);
            var sw = Stopwatch.StartNew();
            ExpireSolution(true);
            Controller.ReportSolution(sw.Elapsed.TotalMilliseconds);
        }

        private string BuildInfo(DashboardSpec spec)
        {
            var sb = new StringBuilder();
            int controls = Controller.Widgets.Count(w => w.Spec.IsControl);
            var size = Controller.Layout?.Size ?? SizeF.Empty;
            sb.AppendLine($"Pill Dashboard — {Controller.Widgets.Count} widget(s): {controls} controle(s), {Controller.Widgets.Count - controls} indicador(es)");
            sb.AppendLine($"Layout {spec.Layout.ToString().ToLowerInvariant()}{(spec.Layout == LayoutKind.Grid ? $" ({spec.Columns} colunas)" : "")}, {size.Width:0} × {size.Height:0}");
            foreach (var w in Controller.Widgets)
            {
                string value;
                if (w.Spec.IsControl) value = Controller.State.Effective(w.Spec).ToInvariantString();
                else
                {
                    var v = Controller.IndicatorValue(w.Spec, out string status);
                    value = v.IsNone ? $"({status ?? "sem dado"})" : Truncate(v.ToInvariantString(), 40);
                }
                string hub = w.Spec.HubKey != null ? (w.Spec.IsControl ? $"  → PillHub {w.Spec.HubKey}" : $"  ← PillHub {w.Spec.HubKey}") : "";
                string flags = (!w.Spec.Visible ? " [oculto]" : "") + (!w.Spec.Enabled ? " [desativado]" : "");
                sb.AppendLine($"  {WidgetKinds.Name(w.Kind),-8} {w.Id} = {value}{hub}{flags}");
            }
            string mode = Controller.LastSolutionMs > CommitGate.DefaultHeavyMs ? "Release (solução pesada)" : "Live";
            sb.AppendLine($"Commits: {Controller.CommitCount} | última solução disparada pelo painel: {Controller.LastSolutionMs:0.#} ms | modo Auto agora: {mode}");
            foreach (var w in _warnings.Distinct()) sb.AppendLine("Aviso: " + w);
            return sb.ToString().TrimEnd();
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        private static string Hash(IList<string> lines)
        {
            if (lines == null || lines.Count == 0) return "";
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
                return BitConverter.ToString(bytes, 0, 12).Replace("-", "");
            }
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            foreach (var key in _publishedKeys) PillHub.Unpublish(key, InstanceGuid);
            _publishedKeys.Clear();
            Controller.CancelGesture();
            base.RemovedFromDocument(document);
        }

        // =================================================================
        // Menu
        // =================================================================

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "Restaurar valores padrão", (s, e) =>
            {
                RecordUndoEvent("Dashboard: padrão");
                if (Controller.State.ResetToDefaults(Controller.Spec) > 0)
                {
                    _pendingChanged = "(padrão)";
                    ExpireSolution(true);
                }
            }, Controller.Widgets.Any(w => w.Spec.IsPersistent));
            Menu_AppendItem(menu, "Copiar definição (texto)", (s, e) => Clipboard.SetText(string.Join(Environment.NewLine, Controller.Spec.ToLines())), Controller.Widgets.Count > 0);
            Menu_AppendItem(menu, "Copiar estado (id=valor)", (s, e) => Clipboard.SetText(string.Join(Environment.NewLine, Controller.State.ToLines(Controller.Spec))), Controller.Widgets.Any(w => w.Spec.IsPersistent));
        }

        // =================================================================
        // Persistência (.gh e undo): só o estado de execução; a configuração vem das entradas
        // =================================================================

        public override bool Write(GH_IWriter writer)
        {
            DashboardVault.Write(writer, Controller.State, _lastLoadHash);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            _lastLoadHash = DashboardVault.Read(reader, Controller.State) ?? "";
            Controller.CancelGesture();
            return base.Read(reader);
        }

        // =================================================================
        // Preset Vault e Project Vault (só valores; configuração e posição do painel não mudam)
        // =================================================================

        public IEnumerable<ControlState> CaptureControlStates() => DashboardVault.Capture(InstanceGuid, NickName, Controller);

        public IDictionary<string, List<string>> ControlOptions() => DashboardVault.Options(InstanceGuid, Controller);

        public bool ApplyControlState(string controlId, ControlState saved)
        {
            bool changed = DashboardVault.Apply(Controller, controlId, saved);
            // Publica já (como no clique): publicar só no SolveInstance deixaria os receptores de key= com o valor antigo
            if (changed) PrepareCommit(controlId);
            return changed;
        }

        public IList<string> ExportStateLines() => Controller.State.ToLines(Controller.Spec);

        public bool ImportStateLines(IEnumerable<string> lines)
        {
            var changed = Controller.State.Apply(lines, Controller.Spec);
            if (changed.Count == 0) return false;
            foreach (var id in changed) PrepareCommit(id);
            _pendingChanged = string.Join(",", changed);
            ExpireSolution(false);
            return true;
        }
    }
}
