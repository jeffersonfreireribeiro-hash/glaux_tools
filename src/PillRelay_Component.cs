// PillRelay_Component.cs
// Organizador e roteador de cabos vertical (Vertical Wire Relay) para Grasshopper.
// Permite que os fios fluam de cima para baixo no Canvas sem curvas horizontais desnecessárias.
// Pass-through instantâneo de dados (0ms de processamento) para itens, listas e DataTrees.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Grasshopper;
using GH_IO.Serialization;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public enum RelayOrientation
    {
        TopToBottom = 0,   // Fio entra por cima (Top) e sai por baixo (Bottom)
        BottomToTop = 1,   // Fio entra por baixo e sai por cima
        LeftToRight = 2    // Modo horizontal clássico
    }

    public enum RelayStyle
    {
        Arrow = 0,    // Seta indicativa de direção (↓)
        Label = 1,    // Rótulo de texto personalizado
        Dot = 2       // Ponto minimalista compacto
    }

    public class PillRelay_Component : GH_Component
    {
        public RelayOrientation Orientation { get; set; } = RelayOrientation.TopToBottom;
        public RelayStyle Style { get; set; } = RelayStyle.Arrow;
        public Color AccentColor { get; set; } = Color.FromArgb(6, 182, 212); // Ciano moderno
        public string CustomLabel { get; set; } = "";

        public PillRelay_Component()
            : base(
                "Pill Vertical Relay",
                "V-Relay",
                "Organizador e roteador de cabos vertical (Vertical Wire Relay).\n" +
                "- Permite conduzir fios de cima para baixo no Canvas sem curvas horizontais desnecessárias.\n" +
                "- Pass-through instantâneo de dados (0ms de processamento).\n" +
                "- Suporta múltiplos cabos de entrada e saída (itens, listas e DataTrees).\n" +
                "- Customização de rótulo de texto, orientação de fluxo e cores de destaque.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("a1100010-e1ef-4000-8000-000000000010");
        protected override Bitmap Icon => GlauxToolsIcons.PillRelay;

        public override void CreateAttributes()
        {
            m_attributes = new PillRelay_Attributes(this);
            SetupParamAttributes();
        }

        public void SetupParamAttributes()
        {
            if (Params.Input.Count > 0 && Params.Input[0] != null)
            {
                Params.Input[0].Attributes = new VerticalRelayInputParamAttributes(Params.Input[0], m_attributes, this);
            }
            if (Params.Output.Count > 0 && Params.Output[0] != null)
            {
                Params.Output[0].Attributes = new VerticalRelayOutputParamAttributes(Params.Output[0], m_attributes, this);
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("In", "↓", "Entrada de dados (topo do relay). Aceita qualquer tipo de dado ou árvore.", GH_ParamAccess.tree);
            pManager[0].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Out", "↓", "Saída de dados (base do relay). Repassa os dados diretamente em 0ms.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Pass-through instantâneo e transparente de dados
            if (DA.GetDataTree(0, out GH_Structure<IGH_Goo> tree) && tree != null)
            {
                DA.SetDataTree(0, tree);
            }
            else
            {
                DA.SetDataTree(0, new GH_Structure<IGH_Goo>());
            }
        }

        // =========================================================================
        // PERSISTÊNCIA NO ARQUIVO .GH
        // =========================================================================
        public override bool Write(GH_IWriter writer)
        {
            writer.SetInt32("RelayOrientation", (int)Orientation);
            writer.SetInt32("RelayStyle", (int)Style);
            writer.SetDrawingColor("RelayAccentColor", AccentColor);
            writer.SetString("RelayCustomLabel", CustomLabel ?? "");
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("RelayOrientation"))
                Orientation = (RelayOrientation)reader.GetInt32("RelayOrientation");
            if (reader.ItemExists("RelayStyle"))
                Style = (RelayStyle)reader.GetInt32("RelayStyle");
            if (reader.ItemExists("RelayAccentColor"))
                AccentColor = reader.GetDrawingColor("RelayAccentColor");
            if (reader.ItemExists("RelayCustomLabel"))
                CustomLabel = reader.GetString("RelayCustomLabel");

            return base.Read(reader);
        }

        // =========================================================================
        // MENU DE CONTEXTO (BOTÃO DIREITO)
        // =========================================================================
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            Menu_AppendSeparator(menu);

            // Submenu Orientação de Fluxo
            var miOrient = Menu_AppendItem(menu, "Orientação de Fluxo");
            Menu_AppendItem(miOrient.DropDown, "Vertical (Cima para Baixo ↓) [Padrão]", (s, e) =>
            {
                RecordUndoEvent("Mudar Orientação Relay");
                Orientation = RelayOrientation.TopToBottom;
                Params.Input[0].NickName = "↓";
                Params.Output[0].NickName = "↓";
                ExpireSolution(true);
            }, true, Orientation == RelayOrientation.TopToBottom);

            Menu_AppendItem(miOrient.DropDown, "Vertical Invertido (Baixo para Cima ↑)", (s, e) =>
            {
                RecordUndoEvent("Mudar Orientação Relay");
                Orientation = RelayOrientation.BottomToTop;
                Params.Input[0].NickName = "↑";
                Params.Output[0].NickName = "↑";
                ExpireSolution(true);
            }, true, Orientation == RelayOrientation.BottomToTop);

            Menu_AppendItem(miOrient.DropDown, "Horizontal (Esquerda para Direita →)", (s, e) =>
            {
                RecordUndoEvent("Mudar Orientação Relay");
                Orientation = RelayOrientation.LeftToRight;
                Params.Input[0].NickName = "→";
                Params.Output[0].NickName = "→";
                ExpireSolution(true);
            }, true, Orientation == RelayOrientation.LeftToRight);

            // Submenu Estilo Visual
            var miStyle = Menu_AppendItem(menu, "Estilo da Cápsula");
            Menu_AppendItem(miStyle.DropDown, "Cápsula com Seta (↓)", (s, e) =>
            {
                RecordUndoEvent("Mudar Estilo Relay");
                Style = RelayStyle.Arrow;
                Attributes.ExpireLayout();
                Instances.RedrawCanvas();
            }, true, Style == RelayStyle.Arrow);

            Menu_AppendItem(miStyle.DropDown, "Cápsula com Rótulo (Texto)", (s, e) =>
            {
                RecordUndoEvent("Mudar Estilo Relay");
                Style = RelayStyle.Label;
                Attributes.ExpireLayout();
                Instances.RedrawCanvas();
            }, true, Style == RelayStyle.Label);

            Menu_AppendItem(miStyle.DropDown, "Ponto Compacto (Dot)", (s, e) =>
            {
                RecordUndoEvent("Mudar Estilo Relay");
                Style = RelayStyle.Dot;
                Attributes.ExpireLayout();
                Instances.RedrawCanvas();
            }, true, Style == RelayStyle.Dot);

            // Submenu Cores de Destaque
            var miColors = Menu_AppendItem(menu, "Cor do Relay");
            Menu_AppendItem(miColors.DropDown, "Ciano Elétrico (#06B6D4)", (s, e) => { AccentColor = Color.FromArgb(6, 182, 212); Instances.RedrawCanvas(); }, true, AccentColor.ToArgb() == Color.FromArgb(6, 182, 212).ToArgb());
            Menu_AppendItem(miColors.DropDown, "Azul Real (#2563EB)", (s, e) => { AccentColor = Color.FromArgb(37, 99, 235); Instances.RedrawCanvas(); }, true, AccentColor.ToArgb() == Color.FromArgb(37, 99, 235).ToArgb());
            Menu_AppendItem(miColors.DropDown, "Laranja Âmbar (#F59E0B)", (s, e) => { AccentColor = Color.FromArgb(245, 158, 11); Instances.RedrawCanvas(); }, true, AccentColor.ToArgb() == Color.FromArgb(245, 158, 11).ToArgb());
            Menu_AppendItem(miColors.DropDown, "Verde Esmeralda (#10B981)", (s, e) => { AccentColor = Color.FromArgb(16, 185, 129); Instances.RedrawCanvas(); }, true, AccentColor.ToArgb() == Color.FromArgb(16, 185, 129).ToArgb());
            Menu_AppendItem(miColors.DropDown, "Roxo / Magenta (#A855F7)", (s, e) => { AccentColor = Color.FromArgb(168, 85, 247); Instances.RedrawCanvas(); }, true, AccentColor.ToArgb() == Color.FromArgb(168, 85, 247).ToArgb());
            Menu_AppendItem(miColors.DropDown, "Carvão Grafite (#475569)", (s, e) => { AccentColor = Color.FromArgb(71, 85, 105); Instances.RedrawCanvas(); }, true, AccentColor.ToArgb() == Color.FromArgb(71, 85, 105).ToArgb());

            // Editar Rótulo / Tag
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Editar Rótulo do Relay...", (s, e) =>
            {
                PromptForLabel();
            });
        }

        public void PromptForLabel()
        {
            string current = !string.IsNullOrWhiteSpace(CustomLabel) ? CustomLabel : NickName;
            var form = new Form
            {
                Text = "Rótulo do Vertical Relay",
                Width = 320,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lbl = new Label { Text = "Digite a identificação ou tag do cabo:", Left = 16, Top = 14, Width = 270 };
            var txt = new TextBox { Text = current, Left = 16, Top = 38, Width = 270 };
            var btnOk = new Button { Text = "OK", Left = 126, Top = 72, Width = 75, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Cancelar", Left = 211, Top = 72, Width = 75, DialogResult = DialogResult.Cancel };

            form.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
            form.AcceptButton = btnOk;
            form.CancelButton = btnCancel;

            if (form.ShowDialog() == DialogResult.OK)
            {
                RecordUndoEvent("Alterar Rótulo Relay");
                CustomLabel = txt.Text.Trim();
                if (!string.IsNullOrWhiteSpace(CustomLabel))
                {
                    NickName = CustomLabel;
                    Style = RelayStyle.Label;
                }
                Attributes.ExpireLayout();
                Instances.RedrawCanvas();
            }
        }
    }

    // =========================================================================
    // PARAMETER ATTRIBUTES COM GRIPS NO TOPO E NA BASE
    // =========================================================================
    public class VerticalRelayInputParamAttributes : GH_LinkedParamAttributes
    {
        private readonly PillRelay_Component _comp;

        public VerticalRelayInputParamAttributes(IGH_Param param, IGH_Attributes parent, PillRelay_Component comp)
            : base(param, parent)
        {
            _comp = comp;
        }

        public override PointF InputGrip
        {
            get
            {
                var p = Parent;
                if (p == null) return base.InputGrip;

                if (_comp.Orientation == RelayOrientation.TopToBottom)
                    return new PointF(p.Bounds.X + p.Bounds.Width * 0.5f, p.Bounds.Top);
                else if (_comp.Orientation == RelayOrientation.BottomToTop)
                    return new PointF(p.Bounds.X + p.Bounds.Width * 0.5f, p.Bounds.Bottom);
                else
                    return new PointF(p.Bounds.Left, p.Bounds.Y + p.Bounds.Height * 0.5f);
            }
        }

        public override PointF OutputGrip => InputGrip;
    }

    public class VerticalRelayOutputParamAttributes : GH_LinkedParamAttributes
    {
        private readonly PillRelay_Component _comp;

        public VerticalRelayOutputParamAttributes(IGH_Param param, IGH_Attributes parent, PillRelay_Component comp)
            : base(param, parent)
        {
            _comp = comp;
        }

        public override PointF OutputGrip
        {
            get
            {
                var p = Parent;
                if (p == null) return base.OutputGrip;

                if (_comp.Orientation == RelayOrientation.TopToBottom)
                    return new PointF(p.Bounds.X + p.Bounds.Width * 0.5f, p.Bounds.Bottom);
                else if (_comp.Orientation == RelayOrientation.BottomToTop)
                    return new PointF(p.Bounds.X + p.Bounds.Width * 0.5f, p.Bounds.Top);
                else
                    return new PointF(p.Bounds.Right, p.Bounds.Y + p.Bounds.Height * 0.5f);
            }
        }

        public override PointF InputGrip => OutputGrip;
    }

    // =========================================================================
    // COMPONENT ATTRIBUTES (RENDER DA CÁPSULA VERTICAL NO CANVAS)
    // =========================================================================
    public class PillRelay_Attributes : GH_ComponentAttributes
    {
        private readonly PillRelay_Component _relay;

        public PillRelay_Attributes(PillRelay_Component owner) : base(owner)
        {
            _relay = owner;
        }

        protected override void Layout()
        {
            _relay.SetupParamAttributes();

            float width = 24f;
            float height = 44f;

            string dispText = !string.IsNullOrWhiteSpace(_relay.CustomLabel) ? _relay.CustomLabel : _relay.NickName;

            if (_relay.Style == RelayStyle.Dot)
            {
                width = 18f;
                height = 28f;
            }
            else if (_relay.Style == RelayStyle.Label && !string.IsNullOrWhiteSpace(dispText))
            {
                using (var f = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                {
                    SizeF sf = GH_FontServer.MeasureString(dispText, f);
                    width = Math.Max(26f, sf.Width + 14f);
                    height = 42f;
                }
            }
            else
            {
                width = 24f;
                height = 44f;
            }

            if (_relay.Orientation == RelayOrientation.LeftToRight)
            {
                // Inverter proporções se estiver horizontal
                float temp = width;
                width = Math.Max(height, 44f);
                height = temp;
            }

            Bounds = new RectangleF(Pivot.X - width * 0.5f, Pivot.Y - height * 0.5f, width, height);

            // Posicionar Bounds dos parâmetros
            if (Owner.Params.Input.Count > 0)
            {
                var pIn = Owner.Params.Input[0];
                if (_relay.Orientation == RelayOrientation.TopToBottom)
                {
                    pIn.Attributes.Bounds = new RectangleF(Bounds.X, Bounds.Y, Bounds.Width, 12f);
                    pIn.Attributes.Pivot = new PointF(Bounds.X + Bounds.Width * 0.5f, Bounds.Top);
                }
                else if (_relay.Orientation == RelayOrientation.BottomToTop)
                {
                    pIn.Attributes.Bounds = new RectangleF(Bounds.X, Bounds.Bottom - 12f, Bounds.Width, 12f);
                    pIn.Attributes.Pivot = new PointF(Bounds.X + Bounds.Width * 0.5f, Bounds.Bottom);
                }
                else
                {
                    pIn.Attributes.Bounds = new RectangleF(Bounds.Left, Bounds.Y, 12f, Bounds.Height);
                    pIn.Attributes.Pivot = new PointF(Bounds.Left, Bounds.Y + Bounds.Height * 0.5f);
                }
            }

            if (Owner.Params.Output.Count > 0)
            {
                var pOut = Owner.Params.Output[0];
                if (_relay.Orientation == RelayOrientation.TopToBottom)
                {
                    pOut.Attributes.Bounds = new RectangleF(Bounds.X, Bounds.Bottom - 12f, Bounds.Width, 12f);
                    pOut.Attributes.Pivot = new PointF(Bounds.X + Bounds.Width * 0.5f, Bounds.Bottom);
                }
                else if (_relay.Orientation == RelayOrientation.BottomToTop)
                {
                    pOut.Attributes.Bounds = new RectangleF(Bounds.X, Bounds.Y, Bounds.Width, 12f);
                    pOut.Attributes.Pivot = new PointF(Bounds.X + Bounds.Width * 0.5f, Bounds.Top);
                }
                else
                {
                    pOut.Attributes.Bounds = new RectangleF(Bounds.Right - 12f, Bounds.Y, 12f, Bounds.Height);
                    pOut.Attributes.Pivot = new PointF(Bounds.Right, Bounds.Y + Bounds.Height * 0.5f);
                }
            }
        }

        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _relay.PromptForLabel();
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseDoubleClick(sender, e);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            if (channel == GH_CanvasChannel.Wires)
            {
                base.Render(canvas, graphics, channel);
                return;
            }

            if (channel != GH_CanvasChannel.Objects)
            {
                base.Render(canvas, graphics, channel);
                return;
            }

            // Renderizar a cápsula vertical estilizada
            RectangleF b = Bounds;
            float radius = Math.Min(b.Width, b.Height) * 0.5f;

            using (var path = CreateCapsulePath(b, radius))
            {
                // 1. Sombra / Halo de Seleção
                if (Selected)
                {
                    using (var selPen = new Pen(Color.FromArgb(120, 255, 255, 255), 4f))
                    {
                        graphics.DrawPath(selPen, path);
                    }
                }

                // 2. Fundo da Cápsula (Gradiente Dark Glass)
                using (var bgBrush = new LinearGradientBrush(b, Color.FromArgb(36, 44, 58), Color.FromArgb(18, 23, 33), LinearGradientMode.Vertical))
                {
                    graphics.FillPath(bgBrush, path);
                }

                // 3. Borda com a cor de destaque
                using (var borderPen = new Pen(_relay.AccentColor, Selected ? 2f : 1.2f))
                {
                    graphics.DrawPath(borderPen, path);
                }

                // 4. Terminais de Cabo (Grip Rings nos Polos)
                PointF inPos = _relay.Params.Input[0].Attributes.InputGrip;
                PointF outPos = _relay.Params.Output[0].Attributes.OutputGrip;

                using (var terminalBrush = new SolidBrush(_relay.AccentColor))
                using (var centerDotBrush = new SolidBrush(Color.FromArgb(240, 250, 255)))
                {
                    // Terminal Entrada
                    graphics.FillEllipse(terminalBrush, inPos.X - 3.5f, inPos.Y - 3.5f, 7f, 7f);
                    graphics.FillEllipse(centerDotBrush, inPos.X - 1.5f, inPos.Y - 1.5f, 3f, 3f);

                    // Terminal Saída
                    graphics.FillEllipse(terminalBrush, outPos.X - 3.5f, outPos.Y - 3.5f, 7f, 7f);
                    graphics.FillEllipse(centerDotBrush, outPos.X - 1.5f, outPos.Y - 1.5f, 3f, 3f);
                }

                // 5. Conteúdo Interno (Seta, Label ou Ponto)
                if (_relay.Style == RelayStyle.Arrow)
                {
                    DrawArrow(graphics, b, _relay.Orientation);
                }
                else if (_relay.Style == RelayStyle.Label)
                {
                    string label = !string.IsNullOrWhiteSpace(_relay.CustomLabel) ? _relay.CustomLabel : _relay.NickName;
                    using (var f = new Font("Segoe UI", 7.0f, FontStyle.Bold))
                    using (var bText = new SolidBrush(Color.FromArgb(240, 245, 255)))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        graphics.DrawString(label, f, bText, b, sf);
                    }
                }
                else // Dot
                {
                    using (var dotB = new SolidBrush(_relay.AccentColor))
                    {
                        graphics.FillEllipse(dotB, b.X + (b.Width - 5f) * 0.5f, b.Y + (b.Height - 5f) * 0.5f, 5f, 5f);
                    }
                }
            }
        }

        private void DrawArrow(Graphics g, RectangleF b, RelayOrientation orient)
        {
            float cx = b.X + b.Width * 0.5f;
            float cy = b.Y + b.Height * 0.5f;

            using (var p = new Pen(Color.FromArgb(230, 240, 255), 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (orient == RelayOrientation.TopToBottom)
                {
                    // Seta para baixo ↓
                    g.DrawLine(p, cx, cy - 7f, cx, cy + 6f);
                    g.DrawLine(p, cx - 3.5f, cy + 2f, cx, cy + 6f);
                    g.DrawLine(p, cx + 3.5f, cy + 2f, cx, cy + 6f);
                }
                else if (orient == RelayOrientation.BottomToTop)
                {
                    // Seta para cima ↑
                    g.DrawLine(p, cx, cy + 7f, cx, cy - 6f);
                    g.DrawLine(p, cx - 3.5f, cy - 2f, cx, cy - 6f);
                    g.DrawLine(p, cx + 3.5f, cy - 2f, cx, cy - 6f);
                }
                else
                {
                    // Seta para direita →
                    g.DrawLine(p, cx - 7f, cy, cx + 6f, cy);
                    g.DrawLine(p, cx + 2f, cy - 3.5f, cx + 6f, cy);
                    g.DrawLine(p, cx + 2f, cy + 3.5f, cx + 6f, cy);
                }
            }
        }

        private static GraphicsPath CreateCapsulePath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            if (rect.Width < d) d = rect.Width;
            if (rect.Height < d) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
