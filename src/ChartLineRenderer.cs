using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace Buraqueira_Tools
{
    /// <summary>Aparência (claro = PNG exportado; escuro = canvas). A GEOMETRIA nunca depende do tema.</summary>
    public sealed class ChartTheme
    {
        public Color PlotBg, PlotBorder, Grid, TickText, ZeroLine;
        public Color Mean, Median, Mode, Target, TolFill, SigmaFill, SigmaLine, Aggregate, Kde, BarDistribution;
        public float LineScale = 1f, FontSize = 8.5f, MarkerRadius = 3f, BarGapPx = 1.5f, StatWidth = 2f;
        public bool MarkerRing = true;

        public static ChartTheme Light(float scale = 1f) => new ChartTheme
        {
            PlotBg = Color.White, PlotBorder = Color.FromArgb(215, 222, 232), Grid = Color.FromArgb(235, 240, 246), TickText = Color.FromArgb(100, 115, 130), ZeroLine = Color.FromArgb(120, 130, 145),
            Mean = Color.FromArgb(37, 99, 235), Median = Color.FromArgb(168, 85, 247), Mode = Color.FromArgb(234, 88, 12), Target = Color.FromArgb(16, 185, 129),
            TolFill = Color.FromArgb(25, 16, 185, 129), SigmaFill = Color.FromArgb(18, 59, 130, 246), SigmaLine = Color.FromArgb(70, 59, 130, 246),
            Aggregate = Color.FromArgb(2, 132, 199), Kde = Color.FromArgb(0, 190, 235), BarDistribution = Color.FromArgb(37, 99, 235),
            LineScale = scale, FontSize = 8.5f * scale, MarkerRadius = 3f * scale, BarGapPx = 1.5f * scale, StatWidth = 2f * scale, MarkerRing = true
        };

        public static ChartTheme Dark(float scale = 1f) => new ChartTheme
        {
            PlotBg = Color.FromArgb(12, 14, 18), PlotBorder = Color.FromArgb(50, 60, 75), Grid = Color.FromArgb(34, 40, 50), TickText = Color.FromArgb(145, 155, 170), ZeroLine = Color.FromArgb(120, 130, 145),
            Mean = Color.FromArgb(37, 99, 235), Median = Color.FromArgb(168, 85, 247), Mode = Color.FromArgb(255, 150, 20), Target = Color.FromArgb(16, 185, 129),
            TolFill = Color.FromArgb(30, 16, 185, 129), SigmaFill = Color.FromArgb(22, 59, 130, 246), SigmaLine = Color.FromArgb(70, 59, 130, 246),
            Aggregate = Color.FromArgb(0, 230, 255), Kde = Color.FromArgb(0, 220, 255), BarDistribution = Color.FromArgb(60, 120, 215),
            LineScale = scale, FontSize = 5.8f * scale, MarkerRadius = 2.2f * scale, BarGapPx = 1.2f * scale, StatWidth = 1.6f * scale, MarkerRing = false
        };
    }

    /// <summary>Desenha uma ChartScene num retângulo de plotagem. Usado IDENTICAMENTE pelo Canvas (tema escuro) e pelo PNG (tema claro).</summary>
    public static class ChartLineRenderer
    {
        private static Color SeriesColor(int idx) => idx < 0 ? Color.Gray : ChartLine_Component.Palette[idx % ChartLine_Component.Palette.Length];

        public static void DrawPlot(Graphics g, RectangleF plot, ChartScene sc, ChartTheme th, FontFamily fam)
        {
            if (sc == null || plot.Width < 4 || plot.Height < 4) return;
            var m = new ChartMapper(plot, sc);
            SmoothingMode oldSm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var bg = new SolidBrush(th.PlotBg)) g.FillRectangle(bg, plot);

            // grade + rótulos dos eixos (fora do recorte)
            using (var gridPen = new Pen(th.Grid, 1f))
            using (var txt = new SolidBrush(th.TickText))
            using (var font = new Font(fam, th.FontSize, FontStyle.Regular))
            using (var sfR = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            using (var sfC = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
            {
                foreach (var t in sc.YTicks)
                {
                    float py = m.Y(t.Value);
                    if (py < plot.Top - 0.5f || py > plot.Bottom + 0.5f) continue;
                    g.DrawLine(gridPen, plot.Left, py, plot.Right, py);
                    g.DrawString(t.Label, font, txt, plot.Left - 6, py, sfR);
                }
                foreach (var t in sc.XTicks)
                {
                    float px = m.X(t.Value);
                    if (px < plot.Left - 0.5f || px > plot.Right + 0.5f) continue;
                    g.DrawLine(gridPen, px, plot.Top, px, plot.Bottom);
                    g.DrawString(t.Label, font, txt, px, plot.Bottom + 5, sfC);
                }
            }

            Region oldClip = g.Clip.Clone();
            g.SetClip(plot);
            try
            {
                // linha de base Y=0 (barras positivas/negativas, séries com valores negativos)
                if (sc.MinY < 0 && sc.MaxY > 0)
                    using (var zp = new Pen(th.ZeroLine, 1.2f * th.LineScale)) g.DrawLine(zp, plot.Left, m.Y(0), plot.Right, m.Y(0));

                foreach (var b in sc.Bands)
                {
                    using (var br = new SolidBrush(b.Tolerance ? th.TolFill : th.SigmaFill))
                    using (var pen = new Pen(b.Tolerance ? Color.FromArgb(120, th.Target) : th.SigmaLine, 1f) { DashStyle = b.Tolerance ? DashStyle.Dash : DashStyle.Dot })
                    {
                        if (b.Horizontal)
                        {
                            float y0 = m.Y(b.Max), y1 = m.Y(b.Min);
                            g.FillRectangle(br, plot.Left, y0, plot.Width, Math.Max(0.5f, y1 - y0));
                            g.DrawLine(pen, plot.Left, y0, plot.Right, y0); g.DrawLine(pen, plot.Left, y1, plot.Right, y1);
                        }
                        else
                        {
                            float x0 = m.X(b.Min), x1 = m.X(b.Max);
                            g.FillRectangle(br, x0, plot.Top, Math.Max(0.5f, x1 - x0), plot.Height);
                            g.DrawLine(pen, x0, plot.Top, x0, plot.Bottom); g.DrawLine(pen, x1, plot.Top, x1, plot.Bottom);
                        }
                    }
                }

                // faixas de dispersão por X (mín–máx ou média ± σ) das séries agregadas
                foreach (var bs in sc.BandSeries)
                {
                    if (bs.Lo.Length < 1) continue;
                    var poly = bs.Lo.Select(p => m.P(p)).Concat(bs.Hi.Reverse().Select(p => m.P(p))).ToArray();
                    Color c = SeriesColor(bs.SeriesIndex);
                    if (poly.Length >= 3)
                        using (var br = new SolidBrush(Color.FromArgb(bs.IsStdDev ? 55 : 70, c))) g.FillPolygon(br, poly);
                    using (var pen = new Pen(Color.FromArgb(150, c), 0.9f * th.LineScale) { DashStyle = bs.IsStdDev ? DashStyle.Dot : DashStyle.Solid })
                    {
                        if (bs.Lo.Length > 1) { g.DrawLines(pen, bs.Lo.Select(p => m.P(p)).ToArray()); g.DrawLines(pen, bs.Hi.Select(p => m.P(p)).ToArray()); }
                    }
                }

                // barras (histograma ou XY): base em Y = Base; positivas para cima, negativas para baixo
                foreach (var bar in sc.Bars)
                {
                    float px0 = m.X(bar.X0), px1 = m.X(bar.X1);
                    float w = px1 - px0, gap = Math.Min(th.BarGapPx, w * 0.15f);
                    float x = px0 + gap / 2f; w -= gap;
                    if (w < 1.5f) { x -= (1.5f - w) / 2f; w = 1.5f; }
                    float yv = m.Y(bar.Value), yb = m.Y(bar.Base);
                    float top = Math.Min(yv, yb), h = Math.Abs(yv - yb);
                    if (h < 0.5f) continue;
                    Color c = sc.Kind == ChartLineKind.Distribution ? th.BarDistribution : SeriesColor(bar.SeriesIndex);
                    using (var br = new SolidBrush(Color.FromArgb(sc.Kind == ChartLineKind.Distribution ? 140 : 170, c)))
                    using (var pen = new Pen(c, 1.1f * th.LineScale))
                    {
                        g.FillRectangle(br, x, top, w, h);
                        g.DrawRectangle(pen, x, top, w, h);
                    }
                }

                foreach (var wk in sc.Whiskers)
                {
                    float px = m.X(wk.X), y0 = m.Y(wk.Lo), y1 = m.Y(wk.Hi), cap = 3f * th.LineScale;
                    using (var pen = new Pen(Color.FromArgb(235, th.TickText), 1.3f * th.LineScale))
                    {
                        g.DrawLine(pen, px, y0, px, y1); g.DrawLine(pen, px - cap, y0, px + cap, y0); g.DrawLine(pen, px - cap, y1, px + cap, y1);
                    }
                }

                foreach (var r in sc.Refs)
                {
                    Color c = r.Kind == ChartRefKind.Mean ? th.Mean : r.Kind == ChartRefKind.Median ? th.Median : r.Kind == ChartRefKind.Mode ? th.Mode : th.Target;
                    using (var pen = new Pen(c, th.StatWidth))
                    {
                        if (r.Kind == ChartRefKind.Mean) pen.DashPattern = new[] { 6f, 3f };
                        else if (r.Kind == ChartRefKind.Median) pen.DashPattern = new[] { 4f, 2f, 1f, 2f };
                        else if (r.Kind == ChartRefKind.Mode) pen.DashStyle = DashStyle.Dot;
                        else pen.DashStyle = DashStyle.Dash;
                        if (r.Horizontal) { float y = m.Y(r.Value); g.DrawLine(pen, plot.Left, y, plot.Right, y); }
                        else { float x = m.X(r.Value); g.DrawLine(pen, x, plot.Top, x, plot.Bottom); }
                    }
                }

                // ordem: contexto → dados/suave → tendência → agregada → KDE
                foreach (var role in new[] { ChartLineRole.RawContext, ChartLineRole.Raw, ChartLineRole.Smooth, ChartLineRole.Trend, ChartLineRole.Aggregate, ChartLineRole.Kde })
                    foreach (var pl in sc.Polylines.Where(p => p.Role == role))
                        DrawPoly(g, m, pl, th);

                foreach (var mk in sc.Markers)
                {
                    Color c = SeriesColor(mk.SeriesIndex);
                    float r = mk.Dense ? Math.Max(1.2f, th.MarkerRadius * 0.55f) : th.MarkerRadius;
                    using (var br = new SolidBrush(Color.FromArgb(mk.Dense ? (mk.Muted ? 120 : 170) : (mk.Muted ? 150 : 255), c)))
                    using (var ring = new Pen(Color.White, 1.1f))
                        foreach (var p in mk.Pts)
                        {
                            var s = m.P(p);
                            g.FillEllipse(br, s.X - r, s.Y - r, 2 * r, 2 * r);
                            if (th.MarkerRing && !mk.Muted && !mk.Dense) g.DrawEllipse(ring, s.X - r, s.Y - r, 2 * r, 2 * r);
                        }
                }
            }
            finally
            {
                g.Clip = oldClip;
            }

            using (var bp = new Pen(th.PlotBorder, 1.2f)) g.DrawRectangle(bp, plot.X, plot.Y, plot.Width, plot.Height);
            g.SmoothingMode = oldSm;
        }

        private static void DrawPoly(Graphics g, ChartMapper m, ChartPoly pl, ChartTheme th)
        {
            if (pl.Pts == null || pl.Pts.Length < 2) return;
            var pts = pl.Pts.Select(p => m.P(p)).Where(p => !float.IsNaN(p.X) && !float.IsNaN(p.Y) && !float.IsInfinity(p.X) && !float.IsInfinity(p.Y)).ToArray();
            if (pts.Length < 2) return;
            Color c = SeriesColor(pl.SeriesIndex);
            float s = th.LineScale;
            switch (pl.Role)
            {
                case ChartLineRole.RawContext:
                    using (var pen = new Pen(Color.FromArgb(95, c), 1f * s) { LineJoin = LineJoin.Round }) g.DrawLines(pen, pts);
                    break;
                case ChartLineRole.Raw:
                    using (var pen = new Pen(Color.FromArgb(240, c), 1.8f * s) { LineJoin = LineJoin.Round }) g.DrawLines(pen, pts);
                    break;
                case ChartLineRole.Smooth:
                    using (var pen = new Pen(Color.FromArgb(240, c), 2.2f * s) { LineJoin = LineJoin.Round }) g.DrawLines(pen, pts);
                    break;
                case ChartLineRole.Trend:
                    using (var halo = new Pen(Color.FromArgb(150, Color.White), 4.6f * s) { LineJoin = LineJoin.Round }) g.DrawLines(halo, pts);
                    using (var pen = new Pen(Darken(c), 3f * s) { LineJoin = LineJoin.Round }) g.DrawLines(pen, pts);
                    break;
                case ChartLineRole.Aggregate:
                    using (var pen = new Pen(th.Aggregate, 2.6f * s) { LineJoin = LineJoin.Round, DashPattern = new[] { 5f, 2.5f } }) g.DrawLines(pen, pts);
                    break;
                case ChartLineRole.Kde:
                    using (var pen = new Pen(th.Kde, 3f * s) { LineJoin = LineJoin.Round }) g.DrawLines(pen, pts);
                    break;
            }
        }

        private static Color Darken(Color c) => Color.FromArgb(c.A, (int)(c.R * 0.62), (int)(c.G * 0.62), (int)(c.B * 0.62));
    }
}
