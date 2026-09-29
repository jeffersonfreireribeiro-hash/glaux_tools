using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Grasshopper.Kernel;

namespace Buraqueira_Tools.Visual
{
    /// <summary>
    /// Kit visual compartilhado das Pills: paleta, cantos arredondados, fontes em cache e primitivas de controle
    /// (trilho de slider, switch, botão, campo, LED). Valores extraídos do Pill Slider Pool para que componentes novos
    /// tenham a mesma aparência sem copiar código de desenho; fontes são criadas uma vez (não a cada pintura).
    /// </summary>
    public static class PillVisualKit
    {
        // Paleta dos painéis de controle (Pill Slider Pool)
        public static readonly Color Accent = Color.FromArgb(139, 92, 246);
        public static readonly Color AccentDark = Color.FromArgb(124, 58, 237);
        public static readonly Color CardTop = Color.FromArgb(248, 250, 252);
        public static readonly Color CardBottom = Color.FromArgb(238, 242, 246);
        public static readonly Color Ink = Color.FromArgb(30, 41, 59);
        public static readonly Color InkStrong = Color.FromArgb(15, 23, 42);
        public static readonly Color Muted = Color.FromArgb(100, 116, 139);
        public static readonly Color Faint = Color.FromArgb(148, 163, 184);
        public static readonly Color Rail = Color.FromArgb(203, 213, 225);
        public static readonly Color Field = Color.FromArgb(241, 245, 249);
        public static readonly Color FieldBorder = Color.FromArgb(203, 213, 225);
        public static readonly Color ButtonFace = Color.FromArgb(226, 232, 240);
        public static readonly Color On = Color.FromArgb(16, 185, 129);
        public static readonly Color OnDark = Color.FromArgb(5, 150, 105);

        // LEDs de status das cápsulas (Pill_Attributes)
        public static readonly Color LedOk = Color.FromArgb(46, 204, 113);
        public static readonly Color LedWarning = Color.FromArgb(243, 156, 18);
        public static readonly Color LedError = Color.FromArgb(231, 76, 60);

        private static readonly Dictionary<(float, FontStyle), Font> s_fonts = new Dictionary<(float, FontStyle), Font>();
        private static FontFamily s_family;

        /// <summary>Família de fonte da interface do Grasshopper (Segoe UI no Windows), com alternativas quando não existir.</summary>
        public static FontFamily UIFontFamily()
        {
            if (s_family != null) return s_family;
            try
            {
                if (GH_FontServer.Standard != null && GH_FontServer.Standard.FontFamily != null) return s_family = GH_FontServer.Standard.FontFamily;
            }
            catch
            {
                // Fora do Grasshopper (testes de renderização)
            }
            try
            {
                return s_family = new FontFamily("Segoe UI");
            }
            catch
            {
                return s_family = FontFamily.GenericSansSerif;
            }
        }

        /// <summary>Fonte da interface em cache (tamanho em pontos no espaço do canvas; o zoom do canvas escala).</summary>
        public static Font Font(float size, FontStyle style = FontStyle.Regular)
        {
            var key = (size, style);
            if (!s_fonts.TryGetValue(key, out var font))
            {
                var family = UIFontFamily();
                if (!family.IsStyleAvailable(style)) style = FontStyle.Regular;
                font = new Font(family, size, style, GraphicsUnit.Point);
                s_fonts[key] = font;
            }
            return font;
        }

        public static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;
            if (d <= 0.01f)
            {
                path.AddRectangle(rect);
                return path;
            }
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Color Disabled(Color c) => Color.FromArgb(Math.Min(c.A, (byte)110), c);

        /// <summary>Trilho com preenchimento e manípulo (mesma geometria do Slider Pool).</summary>
        public static void DrawRail(Graphics g, RectangleF track, double ratio, Color accent, bool active, bool enabled, bool hover = false)
        {
            float railH = 5f;
            float railY = track.Y + (track.Height - railH) * 0.5f;
            var railRect = new RectangleF(track.X, railY, track.Width, railH);
            using (var railPath = RoundedRect(railRect, 2.5f))
            using (var railBrush = new SolidBrush(enabled ? Rail : Disabled(Rail)))
            {
                g.FillPath(railBrush, railPath);
            }

            double r = double.IsNaN(ratio) ? 0 : Math.Max(0, Math.Min(1, ratio));
            float fillW = (float)(track.Width * r);
            var fillColor = enabled ? accent : Disabled(accent);
            if (fillW > 3f)
            {
                using (var fillPath = RoundedRect(new RectangleF(track.X, railY, fillW, railH), 2.5f))
                using (var fillBrush = new SolidBrush(fillColor))
                {
                    g.FillPath(fillBrush, fillPath);
                }
            }

            float knobR = active ? 6.5f : hover ? 6f : 5.5f;
            var knob = new RectangleF(track.X + fillW - knobR, track.Y + (track.Height - knobR * 2) * 0.5f, knobR * 2, knobR * 2);
            using (var knobBrush = new SolidBrush(Color.White))
            using (var knobPen = new Pen(fillColor, active ? 2.2f : 1.5f))
            {
                g.FillEllipse(knobBrush, knob);
                g.DrawEllipse(knobPen, knob);
            }
        }

        /// <summary>Switch liga/desliga (Slider Pool: 32 × 14, verde quando ligado).</summary>
        public static void DrawSwitch(Graphics g, RectangleF rect, bool on, bool enabled, bool hover = false)
        {
            var bg = on ? On : Rail;
            if (hover && enabled) bg = on ? OnDark : Faint;
            if (!enabled) bg = Disabled(bg);
            using (var path = RoundedRect(rect, rect.Height / 2f))
            using (var brush = new SolidBrush(bg))
            {
                g.FillPath(brush, path);
            }
            float d = rect.Height - 4f;
            float x = on ? rect.Right - d - 2f : rect.X + 2f;
            using (var knob = new SolidBrush(Color.White))
            {
                g.FillEllipse(knob, x, rect.Y + 2f, d, d);
            }
        }

        public static void DrawButtonFace(Graphics g, RectangleF rect, string text, Font font, bool pressed, bool hover, bool enabled, Color accent)
        {
            Color face = pressed ? accent : hover ? Color.FromArgb(237, 233, 254) : ButtonFace;
            Color border = pressed ? AccentDark : hover ? accent : Rail;
            Color ink = pressed ? Color.White : hover ? AccentDark : Color.FromArgb(71, 85, 105);
            if (!enabled)
            {
                face = Disabled(ButtonFace);
                border = Disabled(Rail);
                ink = Faint;
            }
            using (var path = RoundedRect(rect, 3.5f))
            using (var brush = new SolidBrush(face))
            using (var pen = new Pen(border, 1f))
            using (var inkBrush = new SolidBrush(ink))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
                g.DrawString(text ?? "", font, inkBrush, rect, sf);
            }
        }

        /// <summary>Caixa de campo (fundo claro e borda), usada por dropdowns e caixas de valor.</summary>
        public static void DrawField(Graphics g, RectangleF rect, bool hover, bool enabled, Color accent)
        {
            using (var path = RoundedRect(rect, 3f))
            using (var brush = new SolidBrush(enabled ? Color.White : Field))
            using (var pen = new Pen(hover && enabled ? accent : FieldBorder, hover && enabled ? 1.3f : 1f))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
        }

        public static void DrawLed(Graphics g, PointF center, float radius, Color color)
        {
            var r = new RectangleF(center.X - radius, center.Y - radius, radius * 2, radius * 2);
            using (var brush = new SolidBrush(color))
            using (var pen = new Pen(Color.FromArgb(200, 255, 255, 255), 0.8f))
            {
                g.FillEllipse(brush, r);
                g.DrawEllipse(pen, r);
            }
        }
    }
}
