using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Buraqueira_Tools.Visual;

namespace Buraqueira_Tools.Dashboard
{
    /// <summary>Moldura do painel numa pintura (o que vem do componente, não dos widgets).</summary>
    public sealed class DashboardChrome
    {
        public string Title { get; set; } = "Pill Dashboard";
        public bool Selected { get; set; }

        /// <summary>Componente desativado (Disable): tudo esmaecido e sem interação.</summary>
        public bool Locked { get; set; }

        /// <summary>0 = nenhum, 1 = aviso, 2 = erro (runtime messages do componente).</summary>
        public int MessageLevel { get; set; }

        public string Badge { get; set; }
        public Func<WidgetSpec, Color> AccentOf { get; set; }
    }

    /// <summary>
    /// Desenha o painel inteiro: cartão e cabeçalho na linguagem do Pill Slider Pool, widgets nas posições do layout,
    /// estado vazio com ajuda, e uma versão simplificada (sem texto) quando o zoom do canvas é baixo.
    /// </summary>
    public static class DashboardRenderer
    {
        public const float CornerRadius = 6f;

        public static void Render(DashboardRenderContext ctx, DashboardController controller, RectangleF bounds, DashboardChrome chrome)
        {
            var g = ctx.Graphics;
            var t = ctx.Theme;
            var layout = controller.Layout ?? controller.Arrange(t.Metrics.HeaderHeight);
            var oldSmoothing = g.SmoothingMode;
            var oldText = g.TextRenderingHint;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            DrawCard(g, bounds, chrome, t);
            DrawHeader(ctx, bounds, chrome);

            var oldClip = g.Clip;
            g.SetClip(RectangleF.Inflate(bounds, -1f, -1f), CombineMode.Intersect);
            try
            {
                if (controller.Visible.Count == 0)
                {
                    DrawEmpty(ctx, Offset(layout.Content, bounds.Location), controller.Spec.Widgets.Count > 0);
                }
                else
                {
                    for (int i = 0; i < controller.Visible.Count && i < layout.Items.Length; i++)
                    {
                        var w = controller.Visible[i];
                        var rect = Offset(layout.Items[i], bounds.Location);
                        var value = controller.DisplayValue(w, out bool pending, out string status);
                        var accent = chrome.AccentOf != null ? chrome.AccentOf(w.Spec) : t.Accent;
                        var visual = new WidgetVisual
                        {
                            Value = value,
                            Pending = pending,
                            Status = status,
                            Hover = controller.HoverIndex == i,
                            Active = controller.ActiveIndex == i,
                            Enabled = w.Spec.Enabled && !chrome.Locked,
                            Accent = accent.IsEmpty ? t.Accent : accent
                        };
                        if (ctx.LowDetail) DrawBlock(g, rect, visual);
                        else w.Render(ctx, rect, visual);
                    }
                }
            }
            finally
            {
                g.Clip = oldClip;
                g.SmoothingMode = oldSmoothing;
                g.TextRenderingHint = oldText;
            }

            if (chrome.Locked)
            {
                using (var path = PillVisualKit.RoundedRect(bounds, CornerRadius))
                using (var veil = new SolidBrush(Color.FromArgb(90, 240, 240, 240)))
                {
                    g.FillPath(veil, path);
                }
            }
        }

        public static RectangleF Offset(RectangleF r, PointF origin) => new RectangleF(r.X + origin.X, r.Y + origin.Y, r.Width, r.Height);

        private static void DrawCard(Graphics g, RectangleF b, DashboardChrome chrome, DashboardTheme t)
        {
            using (var path = PillVisualKit.RoundedRect(b, CornerRadius))
            {
                if (chrome.Selected)
                {
                    using (var halo = new Pen(Color.FromArgb(140, t.Accent), 4f))
                    {
                        g.DrawPath(halo, path);
                    }
                }
                using (var bg = new LinearGradientBrush(b, PillVisualKit.CardTop, PillVisualKit.CardBottom, LinearGradientMode.Vertical))
                using (var border = new Pen(chrome.MessageLevel >= 2 ? PillVisualKit.LedError : chrome.MessageLevel == 1 ? PillVisualKit.LedWarning : t.Accent, chrome.Selected ? 2f : 1.2f))
                {
                    g.FillPath(bg, path);
                    g.DrawPath(border, path);
                }
            }
        }

        private static void DrawHeader(DashboardRenderContext ctx, RectangleF b, DashboardChrome chrome)
        {
            var g = ctx.Graphics;
            var t = ctx.Theme;
            float h = t.Metrics.HeaderHeight;
            var header = new RectangleF(b.X, b.Y, b.Width, h);
            float d = CornerRadius * 2f;
            using (var path = new GraphicsPath())
            using (var brush = new LinearGradientBrush(header, t.Accent, PillVisualKit.AccentDark, LinearGradientMode.Vertical))
            {
                path.AddArc(b.X, b.Y, d, d, 180, 90);
                path.AddArc(b.Right - d, b.Y, d, d, 270, 90);
                path.AddLine(b.Right, header.Bottom, b.X, header.Bottom);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
            if (ctx.LowDetail) return;

            float right = header.Right - 8f;
            if (chrome.MessageLevel > 0)
            {
                PillVisualKit.DrawLed(g, new PointF(right - 4f, header.Y + h / 2f), 4f, chrome.MessageLevel >= 2 ? PillVisualKit.LedError : PillVisualKit.LedWarning);
                right -= 14f;
            }
            if (!string.IsNullOrEmpty(chrome.Badge))
            {
                float bw = Math.Min(header.Width * 0.4f, ctx.Measure(chrome.Badge, t.SmallFont) + 10f);
                var badge = new RectangleF(right - bw, header.Y + 5f, bw, h - 10f);
                using (var path = PillVisualKit.RoundedRect(badge, 3f))
                using (var bg = new SolidBrush(Color.FromArgb(80, 255, 255, 255)))
                using (var ink = new SolidBrush(Color.White))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                {
                    g.FillPath(bg, path);
                    g.DrawString(chrome.Badge, t.SmallFont, ink, badge, sf);
                }
                right = badge.X - 6f;
            }
            using (var ink = new SolidBrush(Color.White))
            using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(chrome.Title ?? "", t.TitleFont, ink, new RectangleF(header.X + 8f, header.Y, Math.Max(0, right - header.X - 8f), h), sf);
            }
        }

        private static void DrawEmpty(DashboardRenderContext ctx, RectangleF content, bool allHidden)
        {
            if (ctx.LowDetail) return;
            var t = ctx.Theme;
            using (var pen = new Pen(PillVisualKit.Rail, 1f) { DashStyle = DashStyle.Dash })
            using (var path = PillVisualKit.RoundedRect(content, 4f))
            {
                ctx.Graphics.DrawPath(pen, path);
            }
            string title = allHidden ? "Todos os widgets estão ocultos" : "Painel vazio";
            string hint = allHidden ? "remova 'hidden' ou 'visible=false'" : "Conecte em W linhas como:  slider Largura | min=0 | max=10";
            using (var titleBrush = new SolidBrush(PillVisualKit.Muted))
            using (var hintBrush = new SolidBrush(PillVisualKit.Faint))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                float mid = content.Y + content.Height / 2f;
                ctx.Graphics.DrawString(title, t.CaptionFont, titleBrush, new RectangleF(content.X + 4f, mid - 14f, content.Width - 8f, 14f), sf);
                ctx.Graphics.DrawString(hint, t.SmallFont, hintBrush, new RectangleF(content.X + 4f, mid, content.Width - 8f, 12f), sf);
            }
        }

        /// <summary>Versão sem texto para zoom baixo: blocos claros com a cor do widget (rápido e legível de longe).</summary>
        private static void DrawBlock(Graphics g, RectangleF rect, WidgetVisual v)
        {
            var r = RectangleF.Inflate(rect, 0f, -1f);
            using (var path = PillVisualKit.RoundedRect(r, 3f))
            using (var bg = new SolidBrush(Color.FromArgb(v.Enabled ? 40 : 18, v.Accent)))
            {
                g.FillPath(bg, path);
            }
            using (var strip = new SolidBrush(v.Enabled ? v.Accent : PillVisualKit.Faint))
            {
                g.FillRectangle(strip, r.X, r.Y, 3f, r.Height);
            }
        }
    }
}
