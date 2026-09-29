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

        public static Bitmap PillTreeExport => _pillTreeExport ?? (_pillTreeExport = DrawStackIcon(g =>
        {
            DrawTreeGlyph(g, 2, 4, IconIO);
            DrawArrow(g, new PointF(12, 12), new PointF(22, 12), IconInk, 1.8f);
        }));

        public static Bitmap PillTreeImport => _pillTreeImport ?? (_pillTreeImport = DrawStackIcon(g =>
        {
            DrawTreeGlyph(g, 10, 4, IconIO);
            DrawArrow(g, new PointF(1, 12), new PointF(10, 12), IconInk, 1.8f);
        }));

        public static Bitmap PillTreeTable => _pillTreeTable ?? (_pillTreeTable = DrawStackIcon(g =>
        {
            DrawTreeGlyph(g, 0, 4, IconIO, 0.75f);
            DrawArrow(g, new PointF(9, 12), new PointF(13, 12), IconInk, 1.4f);
            DrawTableGlyph(g, new RectangleF(13.5f, 4.5f, 9, 15), IconIO);
        }));

        public static Bitmap PillTableToTree => _pillTableToTree ?? (_pillTableToTree = DrawStackIcon(g =>
        {
            DrawTableGlyph(g, new RectangleF(1.5f, 4.5f, 9, 15), IconIO);
            DrawArrow(g, new PointF(11, 12), new PointF(15, 12), IconInk, 1.4f);
            DrawTreeGlyph(g, 15, 4, IconIO, 0.75f);
        }));

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
