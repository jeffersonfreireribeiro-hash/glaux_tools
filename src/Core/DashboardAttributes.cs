using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Buraqueira_Tools.Dashboard;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Atributos do Pill Dashboard: só fazem a ponte entre o canvas e o <see cref="DashboardController"/>
    /// (coordenadas, pintura, mouse, menus e agendamento). Regras de commit, layout e estado ficam no controlador.
    /// </summary>
    public class PillDashboard_Attributes : GH_ComponentAttributes
    {
        private static readonly Stopwatch s_clock = Stopwatch.StartNew();
        private readonly PillDashboard_Component _owner;

        // O canvas entregou a captura do mouse a este painel (mesmo que o gesto já tenha terminado, ex: duplo clique)
        private bool _captured;

        public PillDashboard_Attributes(PillDashboard_Component owner) : base(owner)
        {
            _owner = owner;
        }

        private DashboardController Controller => _owner.Controller;
        private static double Now => s_clock.Elapsed.TotalMilliseconds;

        protected override void Layout()
        {
            var metrics = DashboardTheme.Default.Metrics;
            int nIn = Owner.Params.Input.Count, nOut = Owner.Params.Output.Count;
            // A área de widgets começa abaixo da faixa com os nomes das entradas e saídas
            var layout = Controller.Arrange(metrics.ContentTop(nIn, nOut));
            // Canto superior esquerdo fixo no Pivot: o painel cresce para baixo/direita ao ganhar widgets
            var pivot = new PointF((float)Math.Round(Pivot.X), (float)Math.Round(Pivot.Y));
            Bounds = new RectangleF(pivot.X, pivot.Y, layout.Size.Width, layout.Size.Height);
            PlaceGrips(Owner.Params.Input, true, metrics);
            PlaceGrips(Owner.Params.Output, false, metrics);
        }

        /// <summary>
        /// Cada parâmetro ocupa a linha do seu nome na faixa de parâmetros: o grip fica na borda do painel, na altura do
        /// nome, e os bounds cobrem o nome (tooltip e menu do parâmetro com o botão direito, como num componente comum).
        /// </summary>
        private void PlaceGrips(System.Collections.Generic.List<IGH_Param> list, bool input, DashboardMetrics m)
        {
            float half = Math.Max(20f, Bounds.Width / 2f - 4f);
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                float y = Bounds.Y + m.ParamRowCenterY(i);
                p.Attributes.Bounds = input
                    ? new RectangleF(Bounds.Left, y - m.ParamRowHeight / 2f, half, m.ParamRowHeight)
                    : new RectangleF(Bounds.Right - half, y - m.ParamRowHeight / 2f, half, m.ParamRowHeight);
                p.Attributes.Pivot = new PointF(input ? Bounds.Left : Bounds.Right, y);
            }
        }

        /// <summary>Nomes completos ou apelidos, conforme a opção "Draw Full Names" do Grasshopper.</summary>
        private static string[] ParamLabels(System.Collections.Generic.List<IGH_Param> list)
        {
            bool full = true;
            try
            {
                full = Grasshopper.CentralSettings.CanvasFullNames;
            }
            catch
            {
                // Configurações indisponíveis: nomes completos
            }
            var labels = new string[list.Count];
            for (int i = 0; i < list.Count; i++) labels[i] = full ? list[i].Name : list[i].NickName;
            return labels;
        }

        private PointF ToLocal(PointF canvasPoint) => new PointF(canvasPoint.X - Bounds.X, canvasPoint.Y - Bounds.Y);
        private RectangleF ToCanvas(RectangleF local) => DashboardRenderer.Offset(local, Bounds.Location);

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            if (channel != GH_CanvasChannel.Objects)
            {
                base.Render(canvas, graphics, channel);
                return;
            }

            // O canvas não avisa quando o ponteiro sai do painel: limpa o hover na pintura
            if (!Controller.IsCapturing && !Bounds.Contains(canvas.CursorCanvasPosition)) Controller.ClearHover();

            float zoom = canvas.Viewport.Zoom;
            var ctx = new DashboardRenderContext(graphics, DashboardTheme.Default, zoom);
            var chrome = new DashboardChrome
            {
                Title = _owner.DisplayTitle,
                Selected = Selected,
                Locked = Owner.Locked,
                MessageLevel = Owner.RuntimeMessageLevel == GH_RuntimeMessageLevel.Error ? 2 : Owner.RuntimeMessageLevel == GH_RuntimeMessageLevel.Warning ? 1 : 0,
                Badge = _owner.Badge,
                AccentOf = spec => DashboardColors.Resolve(spec, DashboardTheme.Default.Accent),
                InputLabels = ParamLabels(Owner.Params.Input),
                OutputLabels = ParamLabels(Owner.Params.Output)
            };
            DashboardRenderer.Render(ctx, Controller, Bounds, chrome);

            foreach (var p in Owner.Params.Input) GH_CapsuleRenderEngine.RenderInputGrip(graphics, zoom, p.Attributes.InputGrip, true);
            foreach (var p in Owner.Params.Output) GH_CapsuleRenderEngine.RenderOutputGrip(graphics, zoom, p.Attributes.OutputGrip, true);
        }

        /// <summary>Com zoom muito baixo os widgets não reagem: fica fácil arrastar o painel inteiro.</summary>
        private bool Interactive(GH_Canvas sender) => !Owner.Locked && sender.IsDocument && sender.Viewport.Zoom >= 0.45f;

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && Interactive(sender) && Bounds.Contains(e.CanvasLocation))
            {
                var response = Execute(sender, Controller.PointerDown(ToLocal(e.CanvasLocation), Now));
                if (response.HasValue) return response.Value;
            }
            return base.RespondToMouseDown(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (Interactive(sender) && (Controller.IsCapturing || Bounds.Contains(e.CanvasLocation)))
            {
                var effect = Controller.PointerMove(ToLocal(e.CanvasLocation), Now);
                sender.Cursor = Controller.IsCapturing || Controller.HoverIndex >= 0 ? Cursors.Hand : Cursors.Default;
                var response = Execute(sender, effect);
                if (response.HasValue) return response.Value;
            }
            return base.RespondToMouseMove(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (Controller.IsCapturing || _captured)
            {
                if (Controller.IsCapturing) Execute(sender, Controller.PointerUp(ToLocal(e.CanvasLocation), Now));
                _captured = false;
                sender.Invalidate();
                return GH_ObjectResponse.Release;
            }
            return base.RespondToMouseUp(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && Interactive(sender) && Bounds.Contains(e.CanvasLocation))
            {
                var response = Execute(sender, Controller.DoubleClick(ToLocal(e.CanvasLocation), Now));
                if (response.HasValue) return response.Value;
            }
            return base.RespondToMouseDoubleClick(sender, e);
        }

        public override bool IsTooltipRegion(PointF canvasPoint)
        {
            return WidgetIndexAt(canvasPoint) >= 0 || base.IsTooltipRegion(canvasPoint);
        }

        public override void SetupTooltip(PointF canvasPoint, GH_TooltipDisplayEventArgs e)
        {
            int i = WidgetIndexAt(canvasPoint);
            if (i < 0)
            {
                base.SetupTooltip(canvasPoint, e);
                return;
            }
            var w = Controller.Visible[i];
            var value = Controller.DisplayValue(w, out _, out string status);
            e.Title = w.Spec.Label.Length > 0 ? w.Spec.Label : w.Id;
            e.Text = $"{WidgetKinds.Name(w.Kind)} · id: {w.Id}" +
                     (w.Spec.HubKey != null ? $"\nPillHub: {w.Spec.HubKey}" : "") +
                     $"\nValor: {(value.IsNone ? status ?? DashboardFormat.Missing : value.ToInvariantString())}" +
                     (w.Kind == WidgetKind.Slider ? $"\nFaixa: {DashboardText.FormatRoundTrip(w.Spec.Min)} … {DashboardText.FormatRoundTrip(w.Spec.Max)} (duplo clique para digitar)" : "");
            e.Description = w.Spec.ToSpecLine();
        }

        private int WidgetIndexAt(PointF canvasPoint)
        {
            if (Controller.Layout == null || !Bounds.Contains(canvasPoint)) return -1;
            var local = ToLocal(canvasPoint);
            for (int i = 0; i < Controller.Visible.Count && i < Controller.Layout.Items.Length; i++)
            {
                if (Controller.Layout.Items[i].Contains(local)) return i;
            }
            return -1;
        }

        // =================================================================
        // Execução dos efeitos pedidos pelo controlador
        // =================================================================

        private GH_ObjectResponse? Execute(GH_Canvas sender, DashboardEffect effect)
        {
            if (effect == null) return null;
            if (effect.Has(DashboardAction.Commit)) _owner.CommitFromCanvas(effect.WidgetId);
            if (effect.Has(DashboardAction.ScheduleTrailing)) ScheduleTrailing(effect.DelayMs);
            if (effect.Has(DashboardAction.Repaint) && !effect.Has(DashboardAction.Commit)) sender.Invalidate();
            if (effect.Has(DashboardAction.OpenOptions)) ShowOptions(sender, effect);
            if (effect.Has(DashboardAction.EditValue)) PromptValue(sender, effect);

            if (effect.Has(DashboardAction.Capture))
            {
                _captured = true;
                return GH_ObjectResponse.Capture;
            }
            if (effect.Has(DashboardAction.Release))
            {
                _captured = false;
                return GH_ObjectResponse.Release;
            }
            if (effect.Has(DashboardAction.Handled)) return GH_ObjectResponse.Handled;
            return null;
        }

        /// <summary>
        /// Entrega atrasada do throttle: o callback roda no início da próxima solução (ou quando o intervalo vence),
        /// então basta expirar o painel ali dentro; nenhuma solução extra é criada.
        /// </summary>
        private void ScheduleTrailing(int delayMs)
        {
            var doc = _owner.OnPingDocument();
            if (doc == null) return;
            try
            {
                doc.ScheduleSolution(Math.Max(1, delayMs), d =>
                {
                    var effect = Controller.TrailingDue(Now);
                    if (effect.Has(DashboardAction.Commit))
                    {
                        _owner.PrepareCommit(effect.WidgetId);
                        _owner.ExpireSolution(false);
                    }
                });
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void ShowOptions(GH_Canvas sender, DashboardEffect effect)
        {
            var spec = Controller.Spec.Find(effect.WidgetId);
            if (spec == null || spec.Options.Count == 0) return;
            var menu = new ContextMenuStrip { Font = SystemFonts.MenuFont };
            string current = Controller.State.Effective(spec).Text;
            foreach (var option in spec.Options)
            {
                string opt = option;
                var item = new ToolStripMenuItem(opt) { Checked = string.Equals(opt, current, StringComparison.OrdinalIgnoreCase) };
                item.Click += (s, a) =>
                {
                    Execute(sender, Controller.Choose(spec.Id, opt));
                    sender.Invalidate();
                };
                menu.Items.Add(item);
            }
            // Coordenadas do canvas → controle → tela (respeita pan e zoom)
            var field = ToCanvas(effect.Rect);
            var controlPoint = sender.Viewport.ProjectPoint(new PointF(field.Left, field.Bottom + 1f));
            menu.Show(sender.PointToScreen(Point.Round(controlPoint)));
        }

        private void PromptValue(GH_Canvas sender, DashboardEffect effect)
        {
            var spec = Controller.Spec.Find(effect.WidgetId);
            if (spec == null || spec.Kind != WidgetKind.Slider) return;
            string current = effect.Value.TryGetNumber(out double v) ? DashboardText.FormatRoundTrip(v) : "";
            string hint = $"{DashboardText.FormatRoundTrip(spec.Min)} … {DashboardText.FormatRoundTrip(spec.Max)}" + (spec.Step > 0 ? $", passo {DashboardText.FormatRoundTrip(spec.Step)}" : "") + (spec.Unit.Length > 0 ? $" ({spec.Unit})" : "");
            string answer = AskText(sender, spec.Label.Length > 0 ? spec.Label : spec.Id, hint, current);
            if (answer == null) return;
            if (!DashboardText.TryParseNumber(answer, out double number))
            {
                MessageBox.Show(sender.FindForm(), $"'{answer}' não é um número.", "Pill Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Execute(sender, Controller.SetValue(spec.Id, WidgetValue.FromNumber(number)));
            sender.Invalidate();
        }

        private static string AskText(IWin32Window owner, string title, string hint, string current)
        {
            using (var form = new Form())
            using (var box = new TextBox { Text = current, Dock = DockStyle.Top })
            using (var label = new Label { Text = "Faixa: " + hint, Dock = DockStyle.Top, AutoSize = false, Height = 22 })
            using (var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 80 })
            using (var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 80 })
            using (var buttons = new Panel { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(0, 4, 0, 0) })
            {
                form.Text = title;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.AutoScaleMode = AutoScaleMode.Dpi;
                form.ClientSize = new Size(320, 84);
                form.Padding = new Padding(10);
                buttons.Controls.Add(ok);
                buttons.Controls.Add(cancel);
                form.Controls.Add(buttons);
                form.Controls.Add(box);
                form.Controls.Add(label);
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                box.SelectAll();
                return form.ShowDialog(owner) == DialogResult.OK ? box.Text : null;
            }
        }
    }
}
