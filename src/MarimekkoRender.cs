using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Buraqueira_Tools
{
    public enum MarimekkoLabelMode
    {
        None = 0,
        Label = 1,
        Value = 2,
        Percent = 3,        // % do segmento DENTRO da categoria (não é a participação na área do gráfico)
        LabelPercent = 4
    }

    /// <summary>Configuração visual compartilhada por Canvas, viewport, PNG e SVG (mesmo gráfico, mesmas regras).</summary>
    public sealed class MarimekkoStyle
    {
        public string Title = "";
        public MarimekkoLabelMode LabelMode = MarimekkoLabelMode.LabelPercent;
        public bool ShowLegend = true;
        public bool ShowCategoryLabels = true;
        public bool ShowCategoryShare = true;      // "% do total" sob o rótulo da categoria (participação na LARGURA)
        public List<Color> Palette = new List<Color>(Chart3DColumn_Component.DefaultPalette);
    }

    /// <summary>Retângulos em coordenadas de tela/imagem (origem no canto superior esquerdo) para um gráfico W × H.</summary>
    public struct MarimekkoFrame
    {
        public RectangleF Plot, Legend, TitleBox;
        public float FontPx;

        public static MarimekkoFrame Compute(float w, float h, MarimekkoStyle style, MarimekkoLayout layout)
        {
            float font = Math.Max(7.5f, Math.Min(15f, h * 0.026f));
            float left = Math.Max(26f, w * 0.055f);                       // eixo 0–100 % da composição
            float top = string.IsNullOrWhiteSpace(style.Title) ? h * 0.035f : Math.Max(font * 2.6f, h * 0.10f);
            float bottom = style.ShowCategoryLabels ? Math.Max(font * 4.2f, h * 0.17f) : h * 0.04f;
            float legendW = 0f;
            if (style.ShowLegend && layout != null && layout.Segments.Count > 0)
            {
                int longest = layout.Segments.Max(s => s.Label.Length);
                legendW = Math.Max(w * 0.12f, Math.Min(w * 0.26f, (longest * font * 0.56f) + font * 3.2f));
            }
            float right = w * 0.02f + legendW;
            var f = new MarimekkoFrame { FontPx = font };
            f.Plot = new RectangleF(left, top, Math.Max(10f, w - left - right), Math.Max(10f, h - top - bottom));
            f.Legend = legendW > 0 ? new RectangleF(f.Plot.Right + w * 0.015f, f.Plot.Top, legendW - w * 0.005f, f.Plot.Height) : RectangleF.Empty;
            f.TitleBox = new RectangleF(left, h * 0.012f, f.Plot.Width, top - h * 0.02f);
            return f;
        }

        public RectangleF CellRect(MarimekkoCell c)
        {
            float x = Plot.X + (float)c.X0 * Plot.Width;
            float x1 = Plot.X + (float)c.X1 * Plot.Width;
            float y = Plot.Bottom - (float)c.Y1 * Plot.Height;
            float y0 = Plot.Bottom - (float)c.Y0 * Plot.Height;
            return new RectangleF(x, y, x1 - x, y0 - y);
        }
    }

    /// <summary>Textos, formatação e regras de visibilidade — iguais em GDI+, SVG e viewport.</summary>
    public static class MarimekkoText
    {
        public static string Percent(double fraction)
        {
            double p = fraction * 100.0;
            return p.ToString(p >= 10 ? "0.#" : "0.##", CultureInfo.CurrentCulture) + "%";
        }

        public static string Value(double v) => v.ToString("0.##", CultureInfo.CurrentCulture);

        /// <summary>Texto interno da célula (linhas). Percent = fração do segmento na categoria.</summary>
        public static List<string> CellLines(MarimekkoCell c, MarimekkoLabelMode mode)
        {
            var l = new List<string>();
            switch (mode)
            {
                case MarimekkoLabelMode.Label: l.Add(c.SegmentLabel); break;
                case MarimekkoLabelMode.Value: l.Add(Value(c.SegmentValue)); break;
                case MarimekkoLabelMode.Percent: l.Add(Percent(c.NormalizedHeight)); break;
                case MarimekkoLabelMode.LabelPercent: l.Add(c.SegmentLabel); l.Add(Percent(c.NormalizedHeight)); break;
            }
            return l;
        }

        public static float EstimateWidth(string text, float fontPx) => (text?.Length ?? 0) * fontPx * 0.56f;

        /// <summary>Linhas que cabem na célula; vazio quando a célula é pequena demais (o dado continua existindo).</summary>
        public static List<string> VisibleCellLines(MarimekkoCell c, RectangleF px, MarimekkoLabelMode mode, float fontPx)
        {
            var lines = CellLines(c, mode);
            while (lines.Count > 0)
            {
                float needW = lines.Max(s => EstimateWidth(s, fontPx)) + 6f;
                float needH = lines.Count * fontPx * 1.25f + 4f;
                if (px.Width >= needW && px.Height >= needH) return lines;
                if (lines.Count > 1) lines.RemoveAt(0); else break;   // tenta só a última linha (percentual)
            }
            return new List<string>();
        }

        public struct CategoryLabelPlan { public string Text; public string Share; public bool Rotated; public bool Visible; }

        public static CategoryLabelPlan PlanCategoryLabel(MarimekkoCategoryLayout cat, float cellPxW, float fontPx, float bottomRoomPx, MarimekkoStyle style)
        {
            var p = new CategoryLabelPlan { Text = cat.Label, Share = style.ShowCategoryShare ? Percent(cat.NormalizedWidth) : null };
            float w = EstimateWidth(cat.Label, fontPx);
            if (w + 4f <= cellPxW) { p.Visible = true; if (p.Share != null && EstimateWidth(p.Share, fontPx) + 4f > cellPxW) p.Share = null; return p; }
            // não cabe na horizontal: gira 90° se couber na margem inferior e a coluna comporta uma linha
            if (cellPxW >= fontPx * 1.15f && w <= bottomRoomPx - 4f) { p.Rotated = true; p.Visible = true; p.Share = null; return p; }
            p.Visible = false; p.Share = null;      // coluna estreita demais: rótulo omitido (continua no tooltip/SVG/CSV)
            return p;
        }

        public static Color Ink(Color fill) => (0.299 * fill.R + 0.587 * fill.G + 0.114 * fill.B) > 150 ? Color.FromArgb(25, 30, 38) : Color.White;

        public static Color SegmentColor(int segmentIndex, IList<Color> palette)
            => (palette == null || palette.Count == 0) ? Color.SteelBlue : palette[segmentIndex % palette.Count];

        public static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        public static string TooltipText(MarimekkoCell c, double totalWidthValue)
            => $"Categoria: {c.CategoryLabel}\nLargura: {Value(c.WidthValue)} ({Percent(c.NormalizedWidth)} do total)\n" +
               $"Segmento: {c.SegmentLabel}\nValor: {Value(c.SegmentValue)} ({Percent(c.NormalizedHeight)} da categoria)\nÁrea relativa da célula: {Percent(c.RelativeArea)}";
    }

    /// <summary>Desenho GDI+ (Canvas do Grasshopper e PNG) a partir do layout compartilhado.</summary>
    public static class MarimekkoGdi
    {
        public static void Draw(Graphics g, RectangleF area, MarimekkoLayout layout, MarimekkoStyle style, bool dark)
        {
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.TranslateTransform(area.X, area.Y);

            var frame = MarimekkoFrame.Compute(area.Width, area.Height, style, layout);
            Color ink = dark ? Color.FromArgb(215, 222, 232) : Color.FromArgb(30, 36, 46);
            Color faint = dark ? Color.FromArgb(120, 130, 145) : Color.FromArgb(120, 128, 140);
            Color gap = dark ? Color.FromArgb(14, 17, 22) : Color.White;
            string fam = "Segoe UI";
            using (var font = new Font(fam, frame.FontPx, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var bold = new Font(fam, frame.FontPx * 1.25f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var inkBrush = new SolidBrush(ink))
            using (var faintBrush = new SolidBrush(faint))
            {
                var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                var near = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                var far = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

                if (!string.IsNullOrWhiteSpace(style.Title))
                    g.DrawString(style.Title, bold, inkBrush, frame.TitleBox, near);

                if (layout == null || !layout.IsValid)
                {
                    g.DrawString("Marimekko: entrada inválida (veja as mensagens do componente)", font, faintBrush, frame.Plot, center);
                    g.Restore(state);
                    return;
                }

                // eixo 0 / 50 / 100 % (composição dentro de cada categoria)
                foreach (double t in new[] { 0.0, 0.5, 1.0 })
                {
                    float y = frame.Plot.Bottom - (float)t * frame.Plot.Height;
                    g.DrawString((t * 100).ToString("0", CultureInfo.CurrentCulture) + "%", font, faintBrush, new RectangleF(0, y - frame.FontPx, frame.Plot.Left - 3, frame.FontPx * 2), far);
                }

                using (var borderPen = new Pen(gap, Math.Max(1f, frame.FontPx * 0.12f)) { LineJoin = LineJoin.Miter })
                {
                    foreach (var c in layout.Cells)
                    {
                        var r = frame.CellRect(c);
                        Color fill = MarimekkoText.SegmentColor(c.SegmentIndex, style.Palette);
                        using (var b = new SolidBrush(fill)) g.FillRectangle(b, r);
                        g.DrawRectangle(borderPen, r.X, r.Y, r.Width, r.Height);

                        var lines = MarimekkoText.VisibleCellLines(c, r, style.LabelMode, frame.FontPx);
                        if (lines.Count > 0)
                        {
                            using (var tb = new SolidBrush(MarimekkoText.Ink(fill)))
                                g.DrawString(string.Join("\n", lines), font, tb, r, center);
                        }
                    }
                }
                using (var outline = new Pen(faint, 1f)) g.DrawRectangle(outline, frame.Plot.X, frame.Plot.Y, frame.Plot.Width, frame.Plot.Height);

                if (style.ShowCategoryLabels)
                {
                    float room = area.Height - frame.Plot.Bottom;
                    foreach (var cat in layout.Categories)
                    {
                        float cx = frame.Plot.X + (float)(cat.X0 + cat.X1) * 0.5f * frame.Plot.Width;
                        float cw = (float)cat.NormalizedWidth * frame.Plot.Width;
                        var plan = MarimekkoText.PlanCategoryLabel(cat, cw, frame.FontPx, room, style);
                        if (!plan.Visible) continue;
                        if (plan.Rotated)
                        {
                            var st = g.Save();
                            g.TranslateTransform(cx, frame.Plot.Bottom + 4f);
                            g.RotateTransform(90f);
                            g.DrawString(plan.Text, font, inkBrush, 0f, -frame.FontPx * 0.62f, new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near, FormatFlags = StringFormatFlags.NoWrap });
                            g.Restore(st);
                        }
                        else
                        {
                            g.DrawString(plan.Text, font, inkBrush, cx, frame.Plot.Bottom + frame.FontPx * 0.9f, center);
                            if (plan.Share != null) g.DrawString(plan.Share, font, faintBrush, cx, frame.Plot.Bottom + frame.FontPx * 2.2f, center);
                        }
                    }
                }

                if (!frame.Legend.IsEmpty)
                {
                    float rowH = Math.Min(frame.FontPx * 1.9f, frame.Legend.Height / Math.Max(1, layout.Segments.Count));
                    float sw = frame.FontPx * 1.0f;
                    for (int k = 0; k < layout.Segments.Count; k++)
                    {
                        var seg = layout.Segments[layout.Segments.Count - 1 - k];     // do topo para a base, como na pilha
                        float y = frame.Legend.Y + k * rowH;
                        using (var b = new SolidBrush(MarimekkoText.SegmentColor(seg.Index, style.Palette)))
                            g.FillRectangle(b, frame.Legend.X, y + (rowH - sw) * 0.5f, sw, sw);
                        g.DrawString(seg.Label, font, inkBrush, new RectangleF(frame.Legend.X + sw + 5f, y, frame.Legend.Width - sw - 5f, rowH), near);
                    }
                }
            }
            g.Restore(state);
        }

        public static Bitmap RenderPng(MarimekkoLayout layout, MarimekkoStyle style, int width, int height)
        {
            var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                Draw(g, new RectangleF(0, 0, width, height), layout, style, dark: false);
            }
            return bmp;
        }
    }

    /// <summary>SVG vetorial real (um elemento por célula) e CSV semântico — leem o mesmo layout, sem efeitos colaterais.</summary>
    public static class MarimekkoExport
    {
        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        private static string Esc(string s) => System.Security.SecurityElement.Escape(s ?? "") ?? "";

        public static string BuildSvg(MarimekkoLayout layout, MarimekkoStyle style, float width = 1200f, float height = 720f)
        {
            var frame = MarimekkoFrame.Compute(width, height, style, layout);
            var sb = new StringBuilder();
            string font = "font-family=\"'Segoe UI', Arial, sans-serif\"";
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(width)}\" height=\"{F(height)}\" viewBox=\"0 0 {F(width)} {F(height)}\" {font}>");
            sb.AppendLine($"  <rect width=\"{F(width)}\" height=\"{F(height)}\" fill=\"#ffffff\"/>");
            if (!string.IsNullOrWhiteSpace(style.Title))
                sb.AppendLine($"  <text x=\"{F(frame.TitleBox.X)}\" y=\"{F(frame.TitleBox.Y + frame.TitleBox.Height * 0.5f)}\" font-size=\"{F(frame.FontPx * 1.25f)}\" font-weight=\"bold\" fill=\"#1e242e\" dominant-baseline=\"central\">{Esc(style.Title)}</text>");
            if (layout == null || !layout.IsValid)
            {
                sb.AppendLine($"  <text x=\"{F(frame.Plot.X)}\" y=\"{F(frame.Plot.Y + 20)}\" font-size=\"{F(frame.FontPx)}\" fill=\"#78808c\">Marimekko: entrada inválida</text>");
                sb.AppendLine("</svg>");
                return sb.ToString();
            }

            foreach (double t in new[] { 0.0, 0.5, 1.0 })
            {
                float y = frame.Plot.Bottom - (float)t * frame.Plot.Height;
                sb.AppendLine($"  <text x=\"{F(frame.Plot.Left - 3)}\" y=\"{F(y)}\" font-size=\"{F(frame.FontPx)}\" fill=\"#78808c\" text-anchor=\"end\" dominant-baseline=\"central\">{(t * 100).ToString("0", CultureInfo.CurrentCulture)}%</text>");
            }

            sb.AppendLine("  <g id=\"marimekko_cells\">");
            foreach (var c in layout.Cells)
            {
                var r = frame.CellRect(c);
                Color fill = MarimekkoText.SegmentColor(c.SegmentIndex, style.Palette);
                string id = "cell_" + Esc(c.CategoryId).Replace("{", "").Replace("}", "").Replace(";", "_") + "_" + c.SegmentId;
                sb.AppendLine($"    <g id=\"{id}\" data-category=\"{Esc(c.CategoryLabel)}\" data-segment=\"{Esc(c.SegmentLabel)}\" data-width-value=\"{F(c.WidthValue)}\" data-segment-value=\"{F(c.SegmentValue)}\" " +
                              $"data-category-share=\"{F(c.NormalizedWidth)}\" data-segment-share=\"{F(c.NormalizedHeight)}\" data-area-share=\"{F(c.RelativeArea)}\">");
                sb.AppendLine($"      <title>{Esc(MarimekkoText.TooltipText(c, layout.TotalWidthValue))}</title>");
                sb.AppendLine($"      <rect x=\"{F(r.X)}\" y=\"{F(r.Y)}\" width=\"{F(r.Width)}\" height=\"{F(r.Height)}\" fill=\"{MarimekkoText.Hex(fill)}\" stroke=\"#ffffff\" stroke-width=\"{F(Math.Max(1f, frame.FontPx * 0.12f))}\"/>");
                var lines = MarimekkoText.VisibleCellLines(c, r, style.LabelMode, frame.FontPx);
                for (int k = 0; k < lines.Count; k++)
                {
                    float ty = r.Y + r.Height * 0.5f + (k - (lines.Count - 1) * 0.5f) * frame.FontPx * 1.25f;
                    sb.AppendLine($"      <text x=\"{F(r.X + r.Width * 0.5f)}\" y=\"{F(ty)}\" font-size=\"{F(frame.FontPx)}\" fill=\"{MarimekkoText.Hex(MarimekkoText.Ink(fill))}\" text-anchor=\"middle\" dominant-baseline=\"central\">{Esc(lines[k])}</text>");
                }
                sb.AppendLine("    </g>");
            }
            sb.AppendLine("  </g>");
            sb.AppendLine($"  <rect x=\"{F(frame.Plot.X)}\" y=\"{F(frame.Plot.Y)}\" width=\"{F(frame.Plot.Width)}\" height=\"{F(frame.Plot.Height)}\" fill=\"none\" stroke=\"#78808c\" stroke-width=\"1\" id=\"marimekko_boundary\"/>");

            if (style.ShowCategoryLabels)
            {
                sb.AppendLine("  <g id=\"marimekko_category_labels\">");
                float room = height - frame.Plot.Bottom;
                foreach (var cat in layout.Categories)
                {
                    float cx = frame.Plot.X + (float)(cat.X0 + cat.X1) * 0.5f * frame.Plot.Width;
                    float cw = (float)cat.NormalizedWidth * frame.Plot.Width;
                    var plan = MarimekkoText.PlanCategoryLabel(cat, cw, frame.FontPx, room, style);
                    if (!plan.Visible) continue;
                    if (plan.Rotated)
                        sb.AppendLine($"    <text transform=\"translate({F(cx)} {F(frame.Plot.Bottom + 4)}) rotate(90)\" x=\"0\" y=\"{F(frame.FontPx * 0.35f)}\" font-size=\"{F(frame.FontPx)}\" fill=\"#1e242e\">{Esc(plan.Text)}</text>");
                    else
                    {
                        sb.AppendLine($"    <text x=\"{F(cx)}\" y=\"{F(frame.Plot.Bottom + frame.FontPx * 0.9f)}\" font-size=\"{F(frame.FontPx)}\" fill=\"#1e242e\" text-anchor=\"middle\" dominant-baseline=\"central\">{Esc(plan.Text)}</text>");
                        if (plan.Share != null)
                            sb.AppendLine($"    <text x=\"{F(cx)}\" y=\"{F(frame.Plot.Bottom + frame.FontPx * 2.2f)}\" font-size=\"{F(frame.FontPx)}\" fill=\"#78808c\" text-anchor=\"middle\" dominant-baseline=\"central\">{Esc(plan.Share)}</text>");
                    }
                }
                sb.AppendLine("  </g>");
            }

            if (!frame.Legend.IsEmpty)
            {
                sb.AppendLine("  <g id=\"marimekko_legend\">");
                float rowH = Math.Min(frame.FontPx * 1.9f, frame.Legend.Height / Math.Max(1, layout.Segments.Count));
                float sw = frame.FontPx * 1.0f;
                for (int k = 0; k < layout.Segments.Count; k++)
                {
                    var seg = layout.Segments[layout.Segments.Count - 1 - k];
                    float y = frame.Legend.Y + k * rowH;
                    sb.AppendLine($"    <rect x=\"{F(frame.Legend.X)}\" y=\"{F(y + (rowH - sw) * 0.5f)}\" width=\"{F(sw)}\" height=\"{F(sw)}\" fill=\"{MarimekkoText.Hex(MarimekkoText.SegmentColor(seg.Index, style.Palette))}\"/>");
                    sb.AppendLine($"    <text x=\"{F(frame.Legend.X + sw + 5)}\" y=\"{F(y + rowH * 0.5f)}\" font-size=\"{F(frame.FontPx)}\" fill=\"#1e242e\" dominant-baseline=\"central\">{Esc(seg.Label)}</text>");
                }
                sb.AppendLine("  </g>");
            }
            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        /// <summary>Uma linha por célula. Percentuais em 0–100 (numéricos), invariante de cultura, vírgulas/aspas escapadas.</summary>
        public static string BuildCsv(MarimekkoLayout layout)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CategoryID,CategoryLabel,CategoryWidthValue,CategoryWidthPercent,SegmentID,SegmentLabel,SegmentValue,SegmentPercent,RelativeAreaPercent,X0,X1,Y0,Y1");
            if (layout == null) return sb.ToString();
            string P(double fraction) => (fraction * 100.0).ToString("0.######", CultureInfo.InvariantCulture);
            string N(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
            foreach (var c in layout.Cells)
                sb.AppendLine(string.Join(",", Q(c.CategoryId), Q(c.CategoryLabel), N(c.WidthValue), P(c.NormalizedWidth),
                    Q(c.SegmentId), Q(c.SegmentLabel), N(c.SegmentValue), P(c.NormalizedHeight), P(c.RelativeArea),
                    N(c.X0), N(c.X1), N(c.Y0), N(c.Y1)));
            return sb.ToString();
        }

        private static string Q(string s)
        {
            s = s ?? "";
            return (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0) ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
    }
}
