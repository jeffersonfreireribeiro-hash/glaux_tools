using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Ícones 24×24 das pilhas Data &amp; Persistence, Project Vault e Diagnostics.
    /// Primitivas compartilhadas (árvore, tabela, cilindro de banco, setas) mantêm o mesmo traço entre os ícones.
    /// </summary>
    public static partial class GlauxToolsIcons
    {
        private static readonly Color IconInk = Color.FromArgb(33, 37, 41);
        private static readonly Color IconIO = Color.FromArgb(14, 165, 233);
        private static readonly Color IconDB = Color.FromArgb(99, 102, 241);
        private static readonly Color IconVault = Color.FromArgb(245, 158, 11);
        private static readonly Color IconDiag = Color.FromArgb(236, 72, 153);
        private static readonly Color IconOk = Color.FromArgb(34, 197, 94);
        private static readonly Color IconWarn = Color.FromArgb(239, 68, 68);

        private static Bitmap _pillTreeExport, _pillTreeImport, _pillTreeTable, _pillTableToTree;

        // Mesma linguagem dos ícones de banco: seta para baixo = gravar, para cima = ler
        public static Bitmap PillTreeExport => _pillTreeExport ?? (_pillTreeExport = DrawStackIcon(g =>
        {
            DrawFileGlyph(g, new RectangleF(1.5f, 2, 13.5f, 20), IconIO, false);
            DrawTreeGlyph(g, 3, 8, IconIO, 0.72f);
            DrawArrow(g, new PointF(19.5f, 1.5f), new PointF(19.5f, 13), IconInk, 1.8f);
        }));

        public static Bitmap PillTreeImport => _pillTreeImport ?? (_pillTreeImport = DrawStackIcon(g =>
        {
            DrawFileGlyph(g, new RectangleF(1.5f, 2, 13.5f, 20), IconIO, false);
            DrawTreeGlyph(g, 3, 8, IconIO, 0.72f);
            DrawArrow(g, new PointF(19.5f, 13), new PointF(19.5f, 1.5f), IconInk, 1.8f);
        }));

        // Conversão lida da esquerda para a direita, com a seta embaixo
        public static Bitmap PillTreeTable => _pillTreeTable ?? (_pillTreeTable = DrawStackIcon(g =>
        {
            DrawTreeGlyph(g, 0.5f, 2.5f, IconIO, 0.85f);
            DrawTableGlyph(g, new RectangleF(12.5f, 2, 10, 14), IconIO);
            DrawArrow(g, new PointF(4, 20.5f), new PointF(20, 20.5f), IconInk, 1.5f);
        }));

        public static Bitmap PillTableToTree => _pillTableToTree ?? (_pillTableToTree = DrawStackIcon(g =>
        {
            DrawTableGlyph(g, new RectangleF(1.5f, 2, 10, 14), IconIO);
            DrawTreeGlyph(g, 13.5f, 2.5f, IconIO, 0.85f);
            DrawArrow(g, new PointF(4, 20.5f), new PointF(20, 20.5f), IconInk, 1.5f);
        }));

        // ==========================================
        // PILHA 2 — PERSISTENCE
        // ==========================================

        private static Bitmap _pillDbConnect, _pillDbWrite, _pillDbRead, _pillDbQuery, _pillSchemaInspector, _pillDataValidation, _pillDbSync;

        public static Bitmap PillDbConnect => _pillDbConnect ?? (_pillDbConnect = DrawStackIcon(g =>
        {
            DrawCylinder(g, new RectangleF(3, 3, 13, 17), IconDB);
            // Plugue
            using (var pen = new Pen(IconInk, 1.6f))
            {
                g.DrawLine(pen, 16, 14, 19, 14);
                g.DrawLine(pen, 19, 14, 19, 20);
            }
            using (var brush = new SolidBrush(IconOk))
            {
                g.FillRectangle(brush, 17, 18, 5, 4);
            }
        }));

        public static Bitmap PillDbWrite => _pillDbWrite ?? (_pillDbWrite = DrawStackIcon(g =>
        {
            DrawCylinder(g, new RectangleF(2, 7, 13, 15), IconDB);
            DrawArrow(g, new PointF(18.5f, 1.5f), new PointF(18.5f, 13), IconInk, 1.8f);
        }));

        public static Bitmap PillDbRead => _pillDbRead ?? (_pillDbRead = DrawStackIcon(g =>
        {
            DrawCylinder(g, new RectangleF(2, 7, 13, 15), IconDB);
            DrawArrow(g, new PointF(18.5f, 13), new PointF(18.5f, 1.5f), IconInk, 1.8f);
        }));

        public static Bitmap PillDbQuery => _pillDbQuery ?? (_pillDbQuery = DrawStackIcon(g =>
        {
            DrawCylinder(g, new RectangleF(2, 2, 13, 16), IconDB);
            using (var lens = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
            using (var pen = new Pen(IconInk, 1.6f))
            {
                g.FillEllipse(lens, 11, 10, 8, 8);
                g.DrawEllipse(pen, 11, 10, 8, 8);
                pen.Width = 2.4f;
                g.DrawLine(pen, 18, 17, 22, 21);
            }
        }));

        public static Bitmap PillSchemaInspector => _pillSchemaInspector ?? (_pillSchemaInspector = DrawStackIcon(g =>
        {
            DrawCylinder(g, new RectangleF(1.5f, 4, 10, 15), IconDB);
            DrawTableGlyph(g, new RectangleF(12.5f, 3.5f, 10, 16), IconDB);
        }));

        public static Bitmap PillDataValidation => _pillDataValidation ?? (_pillDataValidation = DrawStackIcon(g =>
        {
            var shield = new GraphicsPath();
            shield.AddLines(new[] { new PointF(12, 2), new PointF(20.5f, 5), new PointF(20, 13) });
            shield.AddBezier(new PointF(20, 13), new PointF(19, 18), new PointF(15, 20.5f), new PointF(12, 22));
            shield.AddBezier(new PointF(12, 22), new PointF(9, 20.5f), new PointF(5, 18), new PointF(4, 13));
            shield.AddLines(new[] { new PointF(4, 13), new PointF(3.5f, 5) });
            shield.CloseFigure();
            using (var fill = new LinearGradientBrush(new RectangleF(3, 2, 18, 20), ControlPaint.Light(IconDB, 0.35f), IconDB, LinearGradientMode.Vertical))
            using (var pen = new Pen(IconInk, 1.1f))
            {
                g.FillPath(fill, shield);
                g.DrawPath(pen, shield);
            }
            shield.Dispose();
            using (var check = new Pen(Color.White, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.DrawLines(check, new[] { new PointF(8, 12), new PointF(11, 15.5f), new PointF(16.5f, 8.5f) });
            }
        }));

        public static Bitmap PillDbSync => _pillDbSync ?? (_pillDbSync = DrawStackIcon(g =>
        {
            DrawCylinder(g, new RectangleF(7, 6, 10, 12), IconDB);
            using (var pen = new Pen(IconInk, 1.6f))
            {
                pen.CustomEndCap = new AdjustableArrowCap(3f, 3f, true);
                g.DrawArc(pen, 2, 2, 20, 20, 200, 130);
                g.DrawArc(pen, 2, 2, 20, 20, 20, 130);
            }
        }));

        // ==========================================
        // PILHA 3 — PROJECT VAULT & PROVENANCE
        // ==========================================

        private static Bitmap _pillSnapshot, _pillHistory, _pillCompare, _pillRestore, _pillExperimentLogger;

        public static Bitmap PillSnapshot => _pillSnapshot ?? (_pillSnapshot = DrawStackIcon(g =>
        {
            // Câmera
            using (var body = new LinearGradientBrush(new RectangleF(2, 7, 20, 14), ControlPaint.Light(IconVault, 0.3f), IconVault, LinearGradientMode.Vertical))
            using (var pen = new Pen(IconInk, 1.1f))
            {
                g.FillRectangle(body, 2, 7, 20, 14);
                g.DrawRectangle(pen, 2, 7, 20, 14);
                g.FillRectangle(body, 7, 4, 8, 3);
                g.DrawRectangle(pen, 7, 4, 8, 3);
            }
            using (var lens = new SolidBrush(Color.White))
            using (var pen = new Pen(IconInk, 1.3f))
            {
                g.FillEllipse(lens, 7.5f, 9.5f, 9, 9);
                g.DrawEllipse(pen, 7.5f, 9.5f, 9, 9);
            }
            using (var core = new SolidBrush(IconInk))
            {
                g.FillEllipse(core, 10, 12, 4, 4);
            }
        }));

        public static Bitmap PillHistory => _pillHistory ?? (_pillHistory = DrawStackIcon(g =>
        {
            using (var face = new SolidBrush(Color.White))
            using (var pen = new Pen(IconInk, 1.2f))
            {
                g.FillEllipse(face, 4, 4, 17, 17);
                g.DrawEllipse(pen, 4, 4, 17, 17);
                g.DrawLine(pen, 12.5f, 12.5f, 12.5f, 7);
                g.DrawLine(pen, 12.5f, 12.5f, 16, 14);
            }
            using (var arrow = new Pen(IconVault, 2f))
            {
                arrow.CustomEndCap = new AdjustableArrowCap(3f, 3f, true);
                g.DrawArc(arrow, 1.5f, 1.5f, 22, 22, 150, 110);
            }
        }));

        public static Bitmap PillCompare => _pillCompare ?? (_pillCompare = DrawStackIcon(g =>
        {
            using (var a = new SolidBrush(ControlPaint.Light(IconVault, 0.55f)))
            using (var b = new SolidBrush(IconVault))
            using (var pen = new Pen(IconInk, 1.1f))
            {
                g.FillRectangle(a, 2, 3, 9, 13);
                g.DrawRectangle(pen, 2, 3, 9, 13);
                g.FillRectangle(b, 13, 8, 9, 13);
                g.DrawRectangle(pen, 13, 8, 9, 13);
            }
            using (var pen = new Pen(IconInk, 1.5f))
            {
                g.DrawLine(pen, 7, 18, 17, 5);
            }
        }));

        public static Bitmap PillRestore => _pillRestore ?? (_pillRestore = DrawStackIcon(g =>
        {
            using (var doc = new SolidBrush(Color.White))
            using (var pen = new Pen(IconInk, 1.1f))
            {
                g.FillRectangle(doc, 8, 5, 12, 15);
                g.DrawRectangle(pen, 8, 5, 12, 15);
                g.DrawLine(pen, 10.5f, 9, 17.5f, 9);
                g.DrawLine(pen, 10.5f, 12, 17.5f, 12);
                g.DrawLine(pen, 10.5f, 15, 15, 15);
            }
            using (var arrow = new Pen(IconVault, 2.2f))
            {
                arrow.CustomEndCap = new AdjustableArrowCap(3f, 3f, true);
                g.DrawArc(arrow, 1, 3, 14, 18, 300, -200);
            }
        }));

        public static Bitmap PillExperimentLogger => _pillExperimentLogger ?? (_pillExperimentLogger = DrawStackIcon(g =>
        {
            // Frasco de laboratório
            var flask = new GraphicsPath();
            flask.AddLines(new[] { new PointF(6, 2), new PointF(12, 2), new PointF(12, 8), new PointF(17, 20), new PointF(1, 20), new PointF(6, 8) });
            flask.CloseFigure();
            using (var glass = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
            using (var pen = new Pen(IconInk, 1.1f))
            {
                g.FillPath(glass, flask);
                var liquid = new GraphicsPath();
                liquid.AddLines(new[] { new PointF(3.3f, 14), new PointF(14.7f, 14), new PointF(17, 20), new PointF(1, 20) });
                liquid.CloseFigure();
                using (var fill = new SolidBrush(IconVault)) g.FillPath(fill, liquid);
                liquid.Dispose();
                g.DrawPath(pen, flask);
            }
            flask.Dispose();
            // Lista de execuções
            using (var pen = new Pen(IconInk, 1.4f))
            {
                g.DrawLine(pen, 17, 5, 23, 5);
                g.DrawLine(pen, 17, 9, 23, 9);
                g.DrawLine(pen, 19, 13, 23, 13);
            }
        }));

        // ==========================================
        // PILHA 4 — PERFORMANCE & DIAGNOSTICS
        // ==========================================

        private static Bitmap _pillRuntimeProfiler;

        public static Bitmap PillRuntimeProfiler => _pillRuntimeProfiler ?? (_pillRuntimeProfiler = DrawStackIcon(g =>
        {
            // Barras de ranking + cronômetro
            float[] widths = { 13, 9.5f, 6.5f, 4 };
            for (int i = 0; i < widths.Length; i++)
            {
                using (var bar = new SolidBrush(i == 0 ? IconDiag : ControlPaint.Light(IconDiag, 0.25f + 0.18f * i)))
                using (var pen = new Pen(IconInk, 0.8f))
                {
                    g.FillRectangle(bar, 2, 3 + i * 4.5f, widths[i], 3.2f);
                    g.DrawRectangle(pen, 2, 3 + i * 4.5f, widths[i], 3.2f);
                }
            }
            using (var face = new SolidBrush(Color.White))
            using (var pen = new Pen(IconInk, 1.2f))
            {
                g.FillEllipse(face, 12, 12, 10, 10);
                g.DrawEllipse(pen, 12, 12, 10, 10);
                g.DrawLine(pen, 17, 17, 17, 13.8f);
                g.DrawLine(pen, 17, 17, 19.6f, 18.3f);
                g.DrawLine(pen, 16, 11, 18, 11);
            }
        }));

        // ==========================================
        // PILHA 6 — DASHBOARD
        // ==========================================

        private static readonly Color IconDash = Color.FromArgb(139, 92, 246);
        private static Bitmap _pillDashboard, _pillDashboardBuilder;

        public static Bitmap PillDashboard => _pillDashboard ?? (_pillDashboard = DrawStackIcon(g =>
        {
            DrawPanelGlyph(g, new RectangleF(1.5f, 2.5f, 21, 19));
        }));

        public static Bitmap PillDashboardBuilder => _pillDashboardBuilder ?? (_pillDashboardBuilder = DrawStackIcon(g =>
        {
            // Linhas de definição ao lado do painel que elas geram
            using (var pen = new Pen(IconInk, 1.6f))
            {
                g.DrawLine(pen, 1.5f, 7, 8f, 7);
                g.DrawLine(pen, 1.5f, 11.5f, 6.5f, 11.5f);
                g.DrawLine(pen, 1.5f, 16, 7.5f, 16);
            }
            DrawPanelGlyph(g, new RectangleF(10f, 3.5f, 12.5f, 17));
        }));

        /// <summary>Cartão com cabeçalho violeta, um trilho de slider e um switch (linguagem do Slider Pool).</summary>
        private static void DrawPanelGlyph(Graphics g, RectangleF r)
        {
            using (var path = RoundedPath(r, 2.5f))
            using (var body = new SolidBrush(Color.FromArgb(248, 250, 252)))
            using (var border = new Pen(IconInk, 1.1f))
            {
                g.FillPath(body, path);
                var header = new RectangleF(r.X, r.Y, r.Width, r.Height * 0.26f);
                var old = g.Clip;
                g.SetClip(path, CombineMode.Intersect);
                using (var head = new SolidBrush(IconDash))
                {
                    g.FillRectangle(head, header);
                }
                g.Clip = old;
                g.DrawPath(border, path);
            }

            float left = r.X + r.Width * 0.14f, right = r.Right - r.Width * 0.14f;
            float y1 = r.Y + r.Height * 0.5f, y2 = r.Y + r.Height * 0.78f;
            using (var rail = new Pen(Color.FromArgb(160, 170, 185), 1.4f))
            using (var fill = new Pen(IconDash, 1.8f))
            {
                g.DrawLine(rail, left, y1, right, y1);
                g.DrawLine(fill, left, y1, left + (right - left) * 0.6f, y1);
            }
            float kr = Math.Max(1.6f, r.Height * 0.11f);
            float kx = left + (right - left) * 0.6f;
            using (var knob = new SolidBrush(Color.White))
            using (var knobPen = new Pen(IconDash, 1.1f))
            {
                g.FillEllipse(knob, kx - kr, y1 - kr, 2 * kr, 2 * kr);
                g.DrawEllipse(knobPen, kx - kr, y1 - kr, 2 * kr, 2 * kr);
            }

            float sw = r.Width * 0.36f, sh = Math.Max(3f, r.Height * 0.17f);
            var swRect = new RectangleF(right - sw, y2 - sh / 2f, sw, sh);
            using (var swPath = RoundedPath(swRect, sh / 2f))
            using (var on = new SolidBrush(IconOk))
            {
                g.FillPath(on, swPath);
            }
            using (var dot = new SolidBrush(Color.White))
            {
                g.FillEllipse(dot, swRect.Right - sh + 0.8f, swRect.Y + 0.8f, sh - 1.6f, sh - 1.6f);
            }
            using (var label = new Pen(Color.FromArgb(120, IconInk), 1.2f))
            {
                g.DrawLine(label, left, y2, swRect.X - 2.5f, y2);
            }
        }

        private static GraphicsPath RoundedPath(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ==========================================
        // PRIMITIVAS COMPARTILHADAS
        // ==========================================

        private static Bitmap DrawStackIcon(Action<Graphics> draw)
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                draw(g);
            }
            return bmp;
        }

        /// <summary>Árvore de dados: raiz à esquerda ramificando em três folhas (largura ~10 px × escala).</summary>
        private static void DrawTreeGlyph(Graphics g, float x, float y, Color color, float scale = 1f)
        {
            PointF P(float px, float py) => new PointF(x + px * scale, y + py * scale);
            var root = P(1.5f, 8);
            var mid = P(5, 8);
            var leaves = new[] { P(10, 2), P(10, 8), P(10, 14) };
            using (var pen = new Pen(IconInk, 1.2f))
            {
                g.DrawLine(pen, root, mid);
                foreach (var leaf in leaves) g.DrawLine(pen, mid, leaf);
            }
            using (var brush = new SolidBrush(color))
            using (var outline = new Pen(IconInk, 0.8f))
            {
                float r = 2.1f * scale;
                g.FillEllipse(brush, root.X - r, root.Y - r, 2 * r, 2 * r);
                g.DrawEllipse(outline, root.X - r, root.Y - r, 2 * r, 2 * r);
                foreach (var leaf in leaves)
                {
                    g.FillRectangle(brush, leaf.X - r, leaf.Y - r, 2 * r, 2 * r);
                    g.DrawRectangle(outline, leaf.X - r, leaf.Y - r, 2 * r, 2 * r);
                }
            }
        }

        /// <summary>Folha de arquivo com canto dobrado e linhas de texto.</summary>
        private static void DrawFileGlyph(Graphics g, RectangleF r, Color accent, bool textLines = true)
        {
            float fold = Math.Min(r.Width, r.Height) * 0.35f;
            var page = new GraphicsPath();
            page.AddLines(new[]
            {
                new PointF(r.X, r.Y), new PointF(r.Right - fold, r.Y), new PointF(r.Right, r.Y + fold),
                new PointF(r.Right, r.Bottom), new PointF(r.X, r.Bottom)
            });
            page.CloseFigure();
            using (var body = new SolidBrush(Color.White))
            using (var pen = new Pen(IconInk, 1.1f))
            using (var lines = new Pen(accent, 1.4f))
            {
                g.FillPath(body, page);
                for (int i = 0; textLines && i < 3; i++)
                {
                    float yy = r.Y + fold + 2.5f + i * 3.2f;
                    if (yy < r.Bottom - 1.5f) g.DrawLine(lines, r.X + 1.8f, yy, r.Right - 1.8f, yy);
                }
                g.DrawPath(pen, page);
                g.DrawLines(pen, new[] { new PointF(r.Right - fold, r.Y), new PointF(r.Right - fold, r.Y + fold), new PointF(r.Right, r.Y + fold) });
            }
            page.Dispose();
        }

        private static void DrawTableGlyph(Graphics g, RectangleF r, Color header)
        {
            using (var body = new SolidBrush(Color.White))
            using (var head = new SolidBrush(header))
            using (var grid = new Pen(Color.FromArgb(120, IconInk), 0.7f))
            using (var border = new Pen(IconInk, 1.1f))
            {
                g.FillRectangle(body, r);
                g.FillRectangle(head, r.X, r.Y, r.Width, r.Height / 4f);
                for (int i = 2; i < 4; i++)
                {
                    float yy = r.Y + r.Height * i / 4f;
                    g.DrawLine(grid, r.X, yy, r.Right, yy);
                }
                g.DrawLine(grid, r.X + r.Width / 2f, r.Y + r.Height / 4f, r.X + r.Width / 2f, r.Bottom);
                g.DrawRectangle(border, r.X, r.Y, r.Width, r.Height);
            }
        }

        /// <summary>Cilindro de banco de dados.</summary>
        private static void DrawCylinder(Graphics g, RectangleF r, Color color)
        {
            float capH = r.Height * 0.28f;
            using (var body = new LinearGradientBrush(r, ControlPaint.Light(color, 0.4f), color, LinearGradientMode.Horizontal))
            using (var top = new SolidBrush(ControlPaint.Light(color, 0.9f)))
            using (var pen = new Pen(IconInk, 1.1f))
            {
                var bodyPath = new GraphicsPath();
                bodyPath.AddLine(r.X, r.Y + capH / 2, r.X, r.Bottom - capH / 2);
                bodyPath.AddArc(r.X, r.Bottom - capH, r.Width, capH, 180, -180);
                bodyPath.AddLine(r.Right, r.Bottom - capH / 2, r.Right, r.Y + capH / 2);
                bodyPath.CloseFigure();
                g.FillPath(body, bodyPath);
                g.DrawPath(pen, bodyPath);
                g.FillEllipse(top, r.X, r.Y, r.Width, capH);
                g.DrawEllipse(pen, r.X, r.Y, r.Width, capH);
                using (var ring = new Pen(Color.FromArgb(150, IconInk), 0.7f))
                {
                    g.DrawArc(ring, r.X, r.Y + r.Height * 0.33f, r.Width, capH, 0, 180);
                }
                bodyPath.Dispose();
            }
        }

        private static void DrawArrow(Graphics g, PointF from, PointF to, Color color, float width)
        {
            using (var pen = new Pen(color, width))
            {
                pen.CustomEndCap = new AdjustableArrowCap(3.2f, 3.2f, true);
                g.DrawLine(pen, from, to);
            }
        }

        private static void DrawBadgeCircle(Graphics g, float cx, float cy, float radius, Color fill)
        {
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(Color.White, 1.1f))
            {
                g.FillEllipse(brush, cx - radius, cy - radius, 2 * radius, 2 * radius);
                g.DrawEllipse(pen, cx - radius, cy - radius, 2 * radius, 2 * radius);
            }
        }

        private static class ControlPaint
        {
            /// <summary>Clareia a cor em direção ao branco (0 = igual, 1 = branco), sem depender de WinForms.</summary>
            public static Color Light(Color c, float amount)
            {
                amount = Math.Max(0f, Math.Min(1f, amount));
                return Color.FromArgb(c.A,
                    (int)(c.R + (255 - c.R) * amount),
                    (int)(c.G + (255 - c.G) * amount),
                    (int)(c.B + (255 - c.B) * amount));
            }
        }
    }
}
