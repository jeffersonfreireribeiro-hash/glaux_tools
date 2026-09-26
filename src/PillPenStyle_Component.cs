using GH_IO.Serialization;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Componente no estilo "Pill" que define estilos de linha, penas de desenho e simbologia
    /// inspirada no QGIS e nas normas ABNT NBR 6492 (espessuras em mm, traçados sólido/tracejado/traço-ponto,
    /// cores, preenchimento, transparência e marcadores de ponto).
    /// Etiqueta geometrias com metadados de pena para renderização direta no Pill Vector Sheet Layout.
    /// </summary>
    public class PillPenStyle_Component : GH_Component
    {
        // Propriedades Ativas
        public double ActiveWeightMm = 0.25;
        public Color ActiveStrokeColor = Color.FromArgb(15, 23, 42); // Slate-900
        public string ActivePattern = "Solid"; // "Solid", "Dashed", "Dotted", "DashDot", "DashDotDot"
        public string ActiveFill = "none";
        public double ActiveOpacity = 1.0;
        public string ActiveMarker = "Circle"; // "Circle", "Cross", "X", "Square", "Target", "Triangle"
        public double ActiveMarkerSizeMm = 2.5;

        public PillPenStyle_Component()
            : base(
                "Pill Pen Style",
                "PillPen",
                "Configura penas e simbologia gráfica inspirada no QGIS e ABNT (espessura de linha em mm, traço sólido/tracejado/traço-ponto, cor, preenchimento, opacidade e marcadores de ponto). Etiqueta geometrias para renderização vetorial no Pill Vector Sheet Layout.",
                "Glaux Tools",
                "Visual")
        {
        }

        public override Guid ComponentGuid => new Guid("b7110015-e1ef-4000-8000-000000000017");

        protected override Bitmap Icon => GlauxToolsIcons.PillPenStyle;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override void CreateAttributes()
        {
            m_attributes = new PillPenStyle_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter(
                "Geometry", "G",
                "Geometrias a estilizar (Curvas, Breps, Meshes, Superfícies, Pontos, Hachuras). Mantém rigorosamente a estrutura de árvore de dados (DataTree).",
                GH_ParamAccess.tree);

            pManager.AddNumberParameter(
                "Weight / Thickness (mm)", "W",
                "Espessura do traço em milímetros no papel (padrões ABNT: 0.13, 0.18, 0.25, 0.35, 0.50, 0.70, 1.00). Padrão: 0.25 mm.",
                GH_ParamAccess.item,
                0.25);
            pManager[1].Optional = true;

            pManager.AddColourParameter(
                "Color", "C",
                "Cor do contorno/traço (Colour Swatch, RGB ou Hex). Padrão: Preto técnico (#0F172A).",
                GH_ParamAccess.item,
                Color.FromArgb(15, 23, 42));
            pManager[2].Optional = true;

            pManager.AddTextParameter(
                "Pattern", "Pat",
                "Padrão do traçado: 'Solid' (contínua), 'Dashed' (tracejada), 'Dotted' (pontilhada), 'DashDot' (traço-ponto), 'DashDotDot' (traço-dois-pontos), ou string SVG dasharray customizada (ex.: '5,2,1,2'). Padrão: 'Solid'.",
                GH_ParamAccess.item,
                "Solid");
            pManager[3].Optional = true;

            pManager.AddGenericParameter(
                "Fill", "F",
                "Preenchimento para curvas fechadas, superfícies ou polígonos (Colour Swatch, Hex ou 'none' para transparente). Padrão: 'none'.",
                GH_ParamAccess.item);
            pManager[4].Optional = true;

            pManager.AddNumberParameter(
                "Opacity", "A",
                "Fator de opacidade geral (0.0 = transparente, 1.0 = opaco). Padrão: 1.0.",
                GH_ParamAccess.item,
                1.0);
            pManager[5].Optional = true;

            pManager.AddTextParameter(
                "Marker", "M",
                "Símbolo para pontos, vértices ou receptores: 'Circle', 'Cross', 'X', 'Square', 'Target', 'Triangle'. Padrão: 'Circle'.",
                GH_ParamAccess.item,
                "Circle");
            pManager[6].Optional = true;

            pManager.AddNumberParameter(
                "Marker Size (mm)", "S",
                "Tamanho do marcador de ponto em milímetros na prancha. Padrão: 2.5 mm.",
                GH_ParamAccess.item,
                2.5);
            pManager[7].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("Styled Geometry", "G", "Geometrias com metadados de estilo anexados, prontas para o Pill Vector Sheet Layout.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Style String", "Sty", "String canônica de estilo CSS/SVG gerada (compatível com a entrada Styles do PillSheet).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_GeometricGoo> inTree)) return;

            double weight = 0.25;
            DA.GetData(1, ref weight);
            ActiveWeightMm = Math.Max(0.01, weight);

            Color strokeColor = Color.FromArgb(15, 23, 42);
            DA.GetData(2, ref strokeColor);
            ActiveStrokeColor = strokeColor;

            string pattern = "Solid";
            DA.GetData(3, ref pattern);
            ActivePattern = pattern;

            object fillObj = null;
            DA.GetData(4, ref fillObj);
            string fillColorStr = ParseFillColor(fillObj);
            ActiveFill = fillColorStr;

            double opacity = 1.0;
            DA.GetData(5, ref opacity);
            ActiveOpacity = Math.Max(0.0, Math.Min(1.0, opacity));

            string marker = "Circle";
            DA.GetData(6, ref marker);
            ActiveMarker = marker;

            double markerSize = 2.5;
            DA.GetData(7, ref markerSize);
            ActiveMarkerSizeMm = Math.Max(0.1, markerSize);

            // Montar String Canônica de Estilo QGIS/SVG
            string styleString = BuildStyleString(ActiveWeightMm, ActiveStrokeColor, ActivePattern, ActiveFill, ActiveOpacity, ActiveMarker, ActiveMarkerSizeMm);

            // Anexar metadados a cada geometria na árvore de dados
            GH_Structure<IGH_GeometricGoo> outTree = new GH_Structure<IGH_GeometricGoo>();

            foreach (var path in inTree.Paths)
            {
                var branch = inTree.get_Branch(path);
                foreach (var item in branch)
                {
                    if (item is IGH_Goo ghGoo)
                    {
                        var dup = ghGoo.Duplicate() as IGH_GeometricGoo;
                        if (dup != null)
                        {
                            AttachStyleToGeometry(dup, styleString);
                            outTree.Append(dup, path);
                        }
                    }
                }
            }

            DA.SetDataTree(0, outTree);
            DA.SetData(1, styleString);
        }

        public void ApplyPreset(string presetName)
        {
            switch (presetName.ToUpperInvariant())
            {
                case "CORTE":
                case "ABNT_CORTE":
                    ActiveWeightMm = 0.50;
                    ActiveStrokeColor = Color.FromArgb(15, 23, 42);
                    ActivePattern = "Solid";
                    ActiveFill = "none";
                    ActiveOpacity = 1.0;
                    break;

                case "VISTA":
                case "ABNT_VISTA":
                    ActiveWeightMm = 0.25;
                    ActiveStrokeColor = Color.FromArgb(51, 65, 85);
                    ActivePattern = "Solid";
                    ActiveFill = "none";
                    ActiveOpacity = 1.0;
                    break;

                case "PROJECAO":
                case "ABNT_PROJECAO":
                    ActiveWeightMm = 0.18;
                    ActiveStrokeColor = Color.FromArgb(71, 85, 105);
                    ActivePattern = "Dashed";
                    ActiveFill = "none";
                    ActiveOpacity = 0.85;
                    break;

                case "EIXO":
                case "ABNT_EIXO":
                    ActiveWeightMm = 0.13;
                    ActiveStrokeColor = Color.FromArgb(148, 163, 184);
                    ActivePattern = "DashDot";
                    ActiveFill = "none";
                    ActiveOpacity = 0.75;
                    break;

                case "QGIS_CYAN":
                case "DESTAQUE":
                    ActiveWeightMm = 0.35;
                    ActiveStrokeColor = Color.FromArgb(14, 165, 233); // Ciano Elétrico Glaux
                    ActivePattern = "Solid";
                    ActiveFill = "#e0f2fe";
                    ActiveOpacity = 0.95;
                    break;
            }

            ExpireSolution(true);
        }

        private static string BuildStyleString(double w, Color c, string pat, string fill, double op, string mkr, double mSize)
        {
            string hexColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            string dash = ResolveDashPattern(pat);

            return $"stroke:{hexColor}; stroke-width:{w.ToString("F2", CultureInfo.InvariantCulture)}mm; stroke-dasharray:{dash}; fill:{fill}; opacity:{op.ToString("F2", CultureInfo.InvariantCulture)}; marker:{mkr.ToLowerInvariant()}; marker-size:{mSize.ToString("F2", CultureInfo.InvariantCulture)}mm;";
        }

        public static string ResolveDashPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return "none";
            switch (pattern.ToUpperInvariant())
            {
                case "SOLID":
                case "CONTINUOUS":
                    return "none";
                case "DASHED":
                case "TRACEJADO":
                    return "4,2";
                case "DOTTED":
                case "PONTILHADO":
                    return "1,2";
                case "DASHDOT":
                case "TRACO_PONTO":
                    return "5,2,1,2";
                case "DASHDOTDOT":
                    return "5,2,1,2,1,2";
                default:
                    // Se for string customizada de números separados por vírgula (ex: "6,2")
                    return pattern;
            }
        }

        private static string ParseFillColor(object fillObj)
        {
            if (fillObj == null) return "none";
            if (fillObj is GH_Colour ghCol)
            {
                var c = ghCol.Value;
                if (c.A == 0) return "none";
                return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            }
            if (fillObj is Color col)
            {
                if (col.A == 0) return "none";
                return $"#{col.R:X2}{col.G:X2}{col.B:X2}";
            }
            string str = fillObj.ToString().Trim();
            if (string.Equals(str, "none", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(str))
            {
                return "none";
            }
            return str;
        }

        private static void AttachStyleToGeometry(IGH_GeometricGoo goo, string styleStr)
        {
            if (goo is IGH_Goo ghGoo)
            {
                var scriptObj = ghGoo.ScriptVariable();
                if (scriptObj is GeometryBase geom && geom.UserDictionary != null)
                {
                    geom.UserDictionary.Set("glaux_pen_style", styleStr);
                }
            }
        }
    }

    // ==========================================
    // ATRIBUTOS E BOTÕES DE PRESET INTERATIVOS
    // ==========================================
    public class PillPenStyle_Attributes : GH_ComponentAttributes
    {
        private const float CONTROL_BAR_HEIGHT = 38f;
        private const float MIN_WIDTH = 195f;

        private RectangleF m_btnCorte;
        private RectangleF m_btnVista;
        private RectangleF m_btnProj;
        private RectangleF m_btnEixo;
        private RectangleF m_btnCyan;

        public PillPenStyle_Attributes(PillPenStyle_Component owner) : base(owner)
        {
        }

        protected override void Layout()
        {
            base.Layout();

            RectangleF b = Bounds;
            b.Width = Math.Max(b.Width, MIN_WIDTH);
            float oldRight = Bounds.Right;
            b.Height += CONTROL_BAR_HEIGHT;
            Bounds = b;

            float deltaX = Bounds.Right - oldRight;
            if (Math.Abs(deltaX) > 0.5f && Owner.Params?.Output != null)
            {
                foreach (var p in Owner.Params.Output)
                {
                    if (p.Attributes != null)
                    {
                        var pb = p.Attributes.Bounds;
                        pb.X += deltaX;
                        p.Attributes.Bounds = pb;
                        var piv = p.Attributes.Pivot;
                        piv.X += deltaX;
                        p.Attributes.Pivot = piv;
                    }
                }
            }

            float barY = Bounds.Bottom - CONTROL_BAR_HEIGHT + 3f;
            float barW = Bounds.Width - 8f;
            float barX = Bounds.X + 4f;

            // 5 Botões de Presets Rápidos QGIS / ABNT
            float btnW = (barW - 8f) / 5f;
            float btnH = 22f;
            float btnY = barY + 5f;

            m_btnCorte = new RectangleF(barX, btnY, btnW, btnH);
            m_btnVista = new RectangleF(barX + (btnW + 2f) * 1, btnY, btnW, btnH);
            m_btnProj = new RectangleF(barX + (btnW + 2f) * 2, btnY, btnW, btnH);
            m_btnEixo = new RectangleF(barX + (btnW + 2f) * 3, btnY, btnW, btnH);
            m_btnCyan = new RectangleF(barX + (btnW + 2f) * 4, btnY, btnW, btnH);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            if (channel == GH_CanvasChannel.Objects)
            {
                var savedPivot = Pivot;
                Pivot = new PointF(Bounds.X + Bounds.Width / 2f, savedPivot.Y);
                base.Render(canvas, graphics, channel);
                Pivot = savedPivot;

                var comp = Owner as PillPenStyle_Component;
                if (comp == null) return;

                RectangleF b = Bounds;
                RectangleF barRect = new RectangleF(b.X + 4f, b.Bottom - CONTROL_BAR_HEIGHT + 2f, b.Width - 8f, CONTROL_BAR_HEIGHT - 4f);

                // Fundo Dark Slate da Barra de Presets
                using (var path = CreateRoundedRectangle(barRect, 5f))
                using (var bgBrush = new LinearGradientBrush(barRect, Color.FromArgb(30, 41, 59), Color.FromArgb(15, 23, 42), LinearGradientMode.Vertical))
                using (var borderPen = new Pen(Color.FromArgb(51, 65, 85), 1.0f))
                {
                    graphics.FillPath(bgBrush, path);
                    graphics.DrawPath(borderPen, path);
                }

                // Renderizar Botões de Pena Rápidos
                DrawPresetBtn(graphics, m_btnCorte, "Corte", "0.50", Color.FromArgb(15, 23, 42));
                DrawPresetBtn(graphics, m_btnVista, "Vista", "0.25", Color.FromArgb(71, 85, 105));
                DrawPresetBtn(graphics, m_btnProj, "Proj.", "Trac.", Color.FromArgb(100, 116, 139));
                DrawPresetBtn(graphics, m_btnEixo, "Eixo", "Ponto", Color.FromArgb(148, 163, 184));
                DrawPresetBtn(graphics, m_btnCyan, "QGIS", "Ciano", Color.FromArgb(14, 165, 233));
            }
            else
            {
                base.Render(canvas, graphics, channel);
            }
        }

        private void DrawPresetBtn(Graphics g, RectangleF rect, string title, string sub, Color accent)
        {
            using (var path = CreateRoundedRectangle(rect, 3f))
            using (var bgBrush = new SolidBrush(Color.FromArgb(35, 45, 60)))
            using (var borderPen = new Pen(accent, 1.0f))
            using (var titleFont = new Font("Segoe UI", 6.2f, FontStyle.Bold))
            using (var subFont = new Font("Segoe UI", 5.5f, FontStyle.Regular))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);

                RectangleF topRect = new RectangleF(rect.X, rect.Y + 1f, rect.Width, rect.Height * 0.5f);
                RectangleF botRect = new RectangleF(rect.X, rect.Y + rect.Height * 0.5f - 1f, rect.Width, rect.Height * 0.5f);

                using (var textBrush = new SolidBrush(Color.FromArgb(241, 245, 249)))
                using (var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                {
                    g.DrawString(title, titleFont, textBrush, topRect, sf);
                    g.DrawString(sub, subFont, subBrush, botRect, sf);
                }
            }
        }

        private GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var comp = Owner as PillPenStyle_Component;
                if (comp != null)
                {
                    if (m_btnCorte.Contains(e.CanvasLocation)) { comp.ApplyPreset("CORTE"); return GH_ObjectResponse.Handled; }
                    if (m_btnVista.Contains(e.CanvasLocation)) { comp.ApplyPreset("VISTA"); return GH_ObjectResponse.Handled; }
                    if (m_btnProj.Contains(e.CanvasLocation)) { comp.ApplyPreset("PROJECAO"); return GH_ObjectResponse.Handled; }
                    if (m_btnEixo.Contains(e.CanvasLocation)) { comp.ApplyPreset("EIXO"); return GH_ObjectResponse.Handled; }
                    if (m_btnCyan.Contains(e.CanvasLocation)) { comp.ApplyPreset("QGIS_CYAN"); return GH_ObjectResponse.Handled; }
                }
            }
            return base.RespondToMouseDown(sender, e);
        }
    }
}
