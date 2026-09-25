using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
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
    public class ChartScatter_Component : GH_Component
    {
        public static readonly Color[] Palette = new Color[]
        {
            Color.FromArgb(0, 220, 255),   // 0: Ciano brilhante
            Color.FromArgb(255, 145, 40),  // 1: Laranja vibrante
            Color.FromArgb(50, 225, 120),  // 2: Verde esmeralda
            Color.FromArgb(240, 80, 200),  // 3: Magenta / Rosa neon
            Color.FromArgb(255, 215, 40),  // 4: Amarelo ouro
            Color.FromArgb(170, 110, 255), // 5: Violeta
            Color.FromArgb(255, 80, 80)    // 6: Coral
        };

        // Cache para renderização no Canvas
        internal List<ScatterSeries> DisplaySeries = new List<ScatterSeries>();
        internal string DisplayTitle = "Scatter & Bubble Plot";
        internal string DisplayXLabel = "X Axis";
        internal string DisplayYLabel = "Y Axis";
        internal bool DisplayShowStats = true;
        internal double GlobalMinX = 0, GlobalMaxX = 1;
        internal double GlobalMinY = 0, GlobalMaxY = 1;
        internal double GlobalMinC = 0, GlobalMaxC = 1;
        internal Bitmap CachedChartBmp;

        public ChartScatter_Component()
            : base(
                "Scatter & Bubble Plot",
                "ChartScatter",
                "Gera gráficos de dispersão 2D com marcadores de bolinhas (Scatter/Bubble Plot), centróide médio, regressão linear (R²) e elipse de dispersão, RENDERIZADO DIRETAMENTE NO PAINEL DO CANVAS DO GRASSHOPPER.",
                "Glaux Tools",
                "Visual")
        {
        }

        public override Guid ComponentGuid => new Guid("8c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f");

        protected override Bitmap Icon => GlauxToolsIcons.ChartScatter;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override void CreateAttributes()
        {
            m_attributes = new ChartScatter_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("X Values", "X", "Valores do eixo X (Lista ou Árvore).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Y Values", "Y", "Valores do eixo Y (Lista ou Árvore).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Bubble Radii", "R", "Raios/tamanhos opcionais das bolinhas para cada ponto (em pixels). Padrão: 5.0.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Color Values", "C", "Valores escalares opcionais para mapeamento em gradiente de cores térmico/esmeralda.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Title", "T", "Título principal do gráfico.", GH_ParamAccess.item, "Scatter & Bubble Plot");
            pManager.AddTextParameter("X Label", "XLab", "Rótulo do eixo X.", GH_ParamAccess.item, "X Axis");
            pManager.AddTextParameter("Y Label", "YLab", "Rótulo do eixo Y.", GH_ParamAccess.item, "Y Axis");
            pManager.AddBooleanParameter("Show Trend & Stats", "Stats", "Exibir reta de regressão linear, centróide (x̄, ȳ), elipse de dispersão e R².", GH_ParamAccess.item, true);
            pManager.AddIntegerParameter("Width", "W", "Largura da imagem exportada em pixels.", GH_ParamAccess.item, 900);
            pManager.AddIntegerParameter("Height", "H", "Altura da imagem exportada em pixels.", GH_ParamAccess.item, 550);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
            pManager[8].Optional = true;
            pManager[9].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Chart Image", "Img", "Imagem renderizada do gráfico de dispersão (System.Drawing.Bitmap).", GH_ParamAccess.item);
            pManager.AddTextParameter("Regression Report", "Rep", "Relatório com Equação Linear (y = ax + b), R², Pearson r, Covariância e Centróide.", GH_ParamAccess.item);
            pManager.AddPointParameter("Centroid", "Center", "Centróide médio dos pontos (x̄, ȳ, 0) no Rhino.", GH_ParamAccess.tree);
            pManager.AddLineParameter("Trend Line", "Trend", "Linha de regressão linear no espaço 3D do Rhino.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Dispersion Ellipse", "Ellipse", "Elipse de dispersão estatística (±1σ) no espaço do Rhino.", GH_ParamAccess.tree);
            pManager.AddPointParameter("Scatter Points", "Pts", "Pontos 3D das bolinhas mapeadas no Rhino.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<GH_Number> xTree) || xTree == null || xTree.IsEmpty ||
                !DA.GetDataTree(1, out GH_Structure<GH_Number> yTree) || yTree == null || yTree.IsEmpty)
            {
                this.Message = "Sem Dados X/Y";
                DisplaySeries.Clear();
                return;
            }

            DA.GetDataTree(2, out GH_Structure<GH_Number> rTree);
            DA.GetDataTree(3, out GH_Structure<GH_Number> cTree);

            string title = "Scatter & Bubble Plot";
            DA.GetData(4, ref title);
            DisplayTitle = title;

            string xLabel = "X Axis";
            DA.GetData(5, ref xLabel);
            DisplayXLabel = xLabel;

            string yLabel = "Y Axis";
            DA.GetData(6, ref yLabel);
            DisplayYLabel = yLabel;

            bool showStats = true;
            DA.GetData(7, ref showStats);
            DisplayShowStats = showStats;

            int width = 900;
            DA.GetData(8, ref width);
            if (width < 300) width = 300;
            if (width > 4000) width = 4000;

            int height = 550;
            DA.GetData(9, ref height);
            if (height < 200) height = 200;
            if (height > 3000) height = 3000;

            var seriesList = new List<ScatterSeries>();
            int branchIdx = 0;

            double globalMinX = double.MaxValue, globalMaxX = double.MinValue;
            double globalMinY = double.MaxValue, globalMaxY = double.MinValue;
            double globalMinC = double.MaxValue, globalMaxC = double.MinValue;

            foreach (GH_Path path in yTree.Paths)
            {
                var yBranch = yTree.get_Branch(path);
                if (yBranch == null || yBranch.Count == 0) continue;

                var xBranch = xTree.PathExists(path) ? xTree.get_Branch(path) : null;
                if (xBranch == null && branchIdx < xTree.Paths.Count)
                {
                    xBranch = xTree.get_Branch(xTree.Paths[branchIdx]);
                }
                if (xBranch == null || xBranch.Count == 0) continue;

                var rBranch = (rTree != null && rTree.PathExists(path)) ? rTree.get_Branch(path) : null;
                var cBranch = (cTree != null && cTree.PathExists(path)) ? cTree.get_Branch(path) : null;

                var sData = new ScatterSeries
                {
                    Path = path,
                    Name = $"Cluster {path}",
                    Color = Palette[branchIdx % Palette.Length]
                };

                int count = Math.Min(xBranch.Count, yBranch.Count);
                for (int i = 0; i < count; i++)
                {
                    if (!GH_Convert.ToDouble(xBranch[i], out double xVal, GH_Conversion.Both) || double.IsNaN(xVal) || double.IsInfinity(xVal))
                        continue;
                    if (!GH_Convert.ToDouble(yBranch[i], out double yVal, GH_Conversion.Both) || double.IsNaN(yVal) || double.IsInfinity(yVal))
                        continue;

                    double rVal = 5.0;
                    if (rBranch != null && i < rBranch.Count)
                    {
                        if (GH_Convert.ToDouble(rBranch[i], out double rParsed, GH_Conversion.Both) && rParsed > 0)
                            rVal = rParsed;
                    }

                    double? cVal = null;
                    if (cBranch != null && i < cBranch.Count)
                    {
                        if (GH_Convert.ToDouble(cBranch[i], out double cParsed, GH_Conversion.Both) && !double.IsNaN(cParsed))
                        {
                            cVal = cParsed;
                            if (cParsed < globalMinC) globalMinC = cParsed;
                            if (cParsed > globalMaxC) globalMaxC = cParsed;
                        }
                    }

                    sData.Points.Add(new ScatterPoint { X = xVal, Y = yVal, Radius = rVal, ColorVal = cVal });

                    if (xVal < globalMinX) globalMinX = xVal;
                    if (xVal > globalMaxX) globalMaxX = xVal;
                    if (yVal < globalMinY) globalMinY = yVal;
                    if (yVal > globalMaxY) globalMaxY = yVal;
                }

                if (sData.Points.Count > 0)
                {
                    sData.ComputeStatistics();
                    seriesList.Add(sData);
                }
                branchIdx++;
            }

            if (seriesList.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum par de coordenadas válido encontrado.");
                DisplaySeries.Clear();
                return;
            }

            if (globalMinX >= globalMaxX) { globalMinX -= 1.0; globalMaxX += 1.0; }
            if (globalMinY >= globalMaxY) { globalMinY -= 1.0; globalMaxY += 1.0; }
            if (globalMinC >= globalMaxC) { globalMinC = 0.0; globalMaxC = 1.0; }

            DisplaySeries = seriesList;
            GlobalMinX = globalMinX;
            GlobalMaxX = globalMaxX;
            GlobalMinY = globalMinY;
            GlobalMaxY = globalMaxY;
            GlobalMinC = globalMinC;
            GlobalMaxC = globalMaxC;

            CachedChartBmp = RenderScatterPlot(seriesList, title, xLabel, yLabel, showStats, width, height,
                globalMinX, globalMaxX, globalMinY, globalMaxY, globalMinC, globalMaxC);

            // Relatório e Geometrias
            var repBuilder = new StringBuilder();
            repBuilder.AppendLine("=================================================");
            repBuilder.AppendLine($"       BURAQUEIRA SCATTER & BUBBLE REPORT");
            repBuilder.AppendLine($"       Título: {title}");
            repBuilder.AppendLine($"       Séries/Clusters: {seriesList.Count}");
            repBuilder.AppendLine("=================================================");

            var outCenterTree = new GH_Structure<GH_Point>();
            var outTrendTree = new GH_Structure<GH_Line>();
            var outEllipseTree = new GH_Structure<GH_Curve>();
            var outPtsTree = new GH_Structure<GH_Point>();

            foreach (var s in seriesList)
            {
                repBuilder.AppendLine($"\n--- [{s.Name}] (N = {s.Points.Count}) ---");
                repBuilder.AppendLine($"  Centróide (x̄, ȳ):    ({s.MeanX:F4}, {s.MeanY:F4})");
                repBuilder.AppendLine($"  Desvio X (σx):       {s.StdDevX:F4}");
                repBuilder.AppendLine($"  Desvio Y (σy):       {s.StdDevY:F4}");
                repBuilder.AppendLine($"  Covariância Cov(X,Y):{s.Covariance:F4}");
                repBuilder.AppendLine($"  Pearson r:           {s.PearsonR:F4}");
                repBuilder.AppendLine($"  R² (Determinação):   {s.RSquared:F4} ({s.RSquared * 100:F1}%)");
                repBuilder.AppendLine($"  Regressão Linear:    y = {s.Slope:F4} · x + {s.Intercept:F4}");

                outCenterTree.EnsurePath(s.Path);
                outTrendTree.EnsurePath(s.Path);
                outEllipseTree.EnsurePath(s.Path);
                outPtsTree.EnsurePath(s.Path);

                var centerPt = new Point3d(s.MeanX, s.MeanY, 0.0);
                outCenterTree.Append(new GH_Point(centerPt), s.Path);

                foreach (var p in s.Points)
                {
                    outPtsTree.Append(new GH_Point(new Point3d(p.X, p.Y, 0.0)), s.Path);
                }

                if (showStats && s.Points.Count > 1)
                {
                    double xMin = s.Points.Min(pt => pt.X);
                    double xMax = s.Points.Max(pt => pt.X);
                    if (Math.Abs(xMin - xMax) < 1e-6) { xMin -= 1; xMax += 1; }

                    double y1 = s.Slope * xMin + s.Intercept;
                    double y2 = s.Slope * xMax + s.Intercept;
                    outTrendTree.Append(new GH_Line(new Line(new Point3d(xMin, y1, 0), new Point3d(xMax, y2, 0))), s.Path);

                    double angle = 0.5 * Math.Atan2(2 * s.Covariance, (s.StdDevX * s.StdDevX - s.StdDevY * s.StdDevY));
                    var plane = new Plane(centerPt, Vector3d.ZAxis);
                    plane.Rotate(angle, Vector3d.ZAxis);

                    double r1 = Math.Max(0.001, s.StdDevX);
                    double r2 = Math.Max(0.001, s.StdDevY);
                    var ellipse = new Ellipse(plane, r1, r2);
                    outEllipseTree.Append(new GH_Curve(ellipse.ToNurbsCurve()), s.Path);
                }
            }

            this.Message = $"{seriesList.Sum(s => s.Points.Count)} Pontos";

            DA.SetData(0, CachedChartBmp);
            DA.SetData(1, repBuilder.ToString());
            DA.SetDataTree(2, outCenterTree);
            DA.SetDataTree(3, outTrendTree);
            DA.SetDataTree(4, outEllipseTree);
            DA.SetDataTree(5, outPtsTree);
        }

        public string SavePngDialog()
        {
            if (CachedChartBmp == null) return null;
            try
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Title = "Salvar Gráfico de Dispersão - BURAQUEIRA Tools";
                    sfd.Filter = "PNG Image (*.png)|*.png|All files (*.*)|*.*";
                    sfd.FileName = "BURAQUEIRA_ScatterPlot.png";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        CachedChartBmp.Save(sfd.FileName, ImageFormat.Png);
                        return sfd.FileName;
                    }
                }
            }
            catch (Exception ex)
            {
                Rhino.RhinoApp.WriteLine($"[ChartScatter] Erro ao salvar imagem: {ex.Message}");
            }
            return null;
        }

        #region Rendering Scatter Plot GDI+

        private Bitmap RenderScatterPlot(List<ScatterSeries> series, string title, string xLabel, string yLabel,
            bool showStats, int w, int h, double minX, double maxX, double minY, double maxY, double minC, double maxC)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                int padLeft = 80;
                int padRight = 190;
                int padTop = 60;
                int padBottom = 60;

                int plotW = w - padLeft - padRight;
                int plotH = h - padTop - padBottom;

                using (var bgBrush = new SolidBrush(Color.FromArgb(248, 250, 252)))
                {
                    g.FillRectangle(bgBrush, 0, 0, w, h);
                }

                var plotRect = new Rectangle(padLeft, padTop, plotW, plotH);
                using (var plotBg = new SolidBrush(Color.White))
                using (var plotBorder = new Pen(Color.FromArgb(218, 225, 235), 1.2f))
                {
                    g.FillRectangle(plotBg, plotRect);
                    g.DrawRectangle(plotBorder, plotRect);
                }

                double spanY = maxY - minY;
                if (spanY <= 0) spanY = 1.0;
                double plotMinY = minY - spanY * 0.08;
                double plotMaxY = maxY + spanY * 0.08;
                double plotSpanY = plotMaxY - plotMinY;

                double spanX = maxX - minX;
                if (spanX <= 0) spanX = 1.0;
                double plotMinX = minX - spanX * 0.08;
                double plotMaxX = maxX + spanX * 0.08;
                double plotSpanX = plotMaxX - plotMinX;

                Func<double, float> mapX = (x) => (float)(padLeft + ((x - plotMinX) / plotSpanX) * plotW);
                Func<double, float> mapY = (y) => (float)(padTop + plotH - ((y - plotMinY) / plotSpanY) * plotH);

                using (var gridPen = new Pen(Color.FromArgb(235, 240, 246), 1f))
                using (var axisTextBrush = new SolidBrush(Color.FromArgb(100, 115, 130)))
                using (var fontAxis = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                using (var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                using (var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
                {
                    for (int i = 0; i <= 6; i++)
                    {
                        double val = plotMinY + (plotSpanY * i / 6);
                        float py = mapY(val);
                        if (py >= padTop && py <= padTop + plotH)
                        {
                            g.DrawLine(gridPen, padLeft, py, padLeft + plotW, py);
                            g.DrawString(val.ToString("G4"), fontAxis, axisTextBrush, padLeft - 8, py, sfRight);
                        }
                    }

                    for (int i = 0; i <= 8; i++)
                    {
                        double val = plotMinX + (plotSpanX * i / 8);
                        float px = mapX(val);
                        if (px >= padLeft && px <= padLeft + plotW)
                        {
                            g.DrawLine(gridPen, px, padTop, px, padTop + plotH);
                            g.DrawString(val.ToString("G4"), fontAxis, axisTextBrush, px, padTop + plotH + 6, sfCenter);
                        }
                    }
                }

                for (int sIdx = 0; sIdx < series.Count; sIdx++)
                {
                    var s = series[sIdx];
                    Color baseCol = s.Color;

                    if (showStats && s.Points.Count > 1)
                    {
                        float cx = mapX(s.MeanX);
                        float cy = mapY(s.MeanY);
                        float rx = (float)((s.StdDevX / plotSpanX) * plotW);
                        float ry = (float)((s.StdDevY / plotSpanY) * plotH);

                        if (rx > 2 && ry > 2)
                        {
                            using (var ellipsePen = new Pen(Color.FromArgb(140, baseCol.R, baseCol.G, baseCol.B), 1.2f) { DashStyle = DashStyle.Dash })
                            using (var ellipseFill = new SolidBrush(Color.FromArgb(20, baseCol.R, baseCol.G, baseCol.B)))
                            {
                                g.FillEllipse(ellipseFill, cx - rx, cy - ry, rx * 2, ry * 2);
                                g.DrawEllipse(ellipsePen, cx - rx, cy - ry, rx * 2, ry * 2);
                            }
                        }

                        float rx1 = mapX(plotMinX);
                        float ry1 = mapY(s.Slope * plotMinX + s.Intercept);
                        float rx2 = mapX(plotMaxX);
                        float ry2 = mapY(s.Slope * plotMaxX + s.Intercept);

                        using (var trendPen = new Pen(Color.FromArgb(220, 231, 76, 60), 1.8f) { DashStyle = DashStyle.Solid })
                        {
                            g.DrawLine(trendPen, rx1, ry1, rx2, ry2);
                        }
                    }

                    foreach (var pt in s.Points)
                    {
                        float px = mapX(pt.X);
                        float py = mapY(pt.Y);
                        float pr = (float)Math.Max(2.0, Math.Min(30.0, pt.Radius));

                        Color ptCol = baseCol;
                        if (pt.ColorVal.HasValue && maxC > minC)
                        {
                            double t = (pt.ColorVal.Value - minC) / (maxC - minC);
                            ptCol = GetThermalColor(t);
                        }

                        using (var bBrush = new SolidBrush(Color.FromArgb(180, ptCol.R, ptCol.G, ptCol.B)))
                        using (var bPen = new Pen(Color.FromArgb(240, ptCol.R, ptCol.G, ptCol.B), 1.2f))
                        {
                            g.FillEllipse(bBrush, px - pr, py - pr, pr * 2, pr * 2);
                            g.DrawEllipse(bPen, px - pr, py - pr, pr * 2, pr * 2);
                        }
                    }

                    if (showStats)
                    {
                        float cx = mapX(s.MeanX);
                        float cy = mapY(s.MeanY);

                        using (var crossPen = new Pen(Color.FromArgb(230, 126, 34), 2.2f))
                        using (var crossRing = new Pen(Color.White, 1.5f))
                        {
                            g.DrawLine(crossPen, cx - 7, cy, cx + 7, cy);
                            g.DrawLine(crossPen, cx, cy - 7, cx, cy + 7);
                            g.DrawEllipse(crossRing, cx - 4, cy - 4, 8, 8);
                        }
                    }
                }

                using (var fontTitle = new Font("Segoe UI", 12.5f, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(Color.FromArgb(35, 45, 60)))
                {
                    g.DrawString(title, fontTitle, titleBrush, padLeft, 18);
                }

                using (var fontLabel = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (var labelBrush = new SolidBrush(Color.FromArgb(70, 85, 105)))
                using (var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(xLabel, fontLabel, labelBrush, padLeft + plotW / 2f, padTop + plotH + 36, sfCenter);

                    var state = g.Save();
                    g.TranslateTransform(22, padTop + plotH / 2f);
                    g.RotateTransform(-90);
                    g.DrawString(yLabel, fontLabel, labelBrush, 0, 0, sfCenter);
                    g.Restore(state);
                }

                int legX = padLeft + plotW + 15;
                int legY = padTop;
                int legW = padRight - 25;

                using (var cardBg = new SolidBrush(Color.White))
                using (var cardBorder = new Pen(Color.FromArgb(220, 226, 235), 1f))
                {
                    g.FillRectangle(cardBg, legX, legY, legW, plotH);
                    g.DrawRectangle(cardBorder, legX, legY, legW, plotH);
                }

                using (var fontLegHeader = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (var legHeadBrush = new SolidBrush(Color.FromArgb(40, 50, 70)))
                {
                    g.DrawString("SCATTER & STATS", fontLegHeader, legHeadBrush, legX + 10, legY + 10);
                }

                int curY = legY + 34;
                using (var fontName = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                using (var fontStat = new Font("Segoe UI", 7.5f, FontStyle.Regular))
                using (var textBrush = new SolidBrush(Color.FromArgb(50, 60, 75)))
                using (var subTextBrush = new SolidBrush(Color.FromArgb(100, 115, 130)))
                {
                    for (int sIdx = 0; sIdx < Math.Min(series.Count, 4); sIdx++)
                    {
                        var s = series[sIdx];
                        Color col = s.Color;

                        using (var dotBrush = new SolidBrush(col))
                        {
                            g.FillEllipse(dotBrush, legX + 10, curY + 2, 8, 8);
                        }
                        g.DrawString(s.Name, fontName, textBrush, legX + 22, curY - 1);
                        curY += 16;

                        g.DrawString($"Centróide: ({s.MeanX:F2}, {s.MeanY:F2})", fontStat, subTextBrush, legX + 22, curY);
                        curY += 13;
                        g.DrawString($"R² = {s.RSquared:F3} | r = {s.PearsonR:F2}", fontStat, subTextBrush, legX + 22, curY);
                        curY += 13;
                        g.DrawString($"y = {s.Slope:F2}x + {s.Intercept:F2}", fontStat, subTextBrush, legX + 22, curY);
                        curY += 18;
                    }
                }
            }

            return bmp;
        }

        private static Color GetThermalColor(double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            if (t < 0.25)
            {
                double k = t / 0.25;
                return Color.FromArgb(41, (int)(128 + 127 * k), 255);
            }
            else if (t < 0.5)
            {
                double k = (t - 0.25) / 0.25;
                return Color.FromArgb(41, 255, (int)(255 * (1 - k)));
            }
            else if (t < 0.75)
            {
                double k = (t - 0.5) / 0.25;
                return Color.FromArgb((int)(41 + 200 * k), 240, 30);
            }
            else
            {
                double k = (t - 0.75) / 0.25;
                return Color.FromArgb(240, (int)(240 * (1 - k)), 40);
            }
        }

        #endregion

        #region Helper Classes

        internal class ScatterPoint
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Radius { get; set; }
            public double? ColorVal { get; set; }
        }

        internal class ScatterSeries
        {
            public GH_Path Path { get; set; }
            public string Name { get; set; }
            public Color Color { get; set; }
            public List<ScatterPoint> Points { get; set; } = new List<ScatterPoint>();

            public double MeanX { get; private set; }
            public double MeanY { get; private set; }
            public double StdDevX { get; private set; }
            public double StdDevY { get; private set; }
            public double Covariance { get; private set; }
            public double PearsonR { get; private set; }
            public double RSquared { get; private set; }
            public double Slope { get; private set; }
            public double Intercept { get; private set; }

            public void ComputeStatistics()
            {
                if (Points.Count == 0) return;
                int n = Points.Count;

                MeanX = Points.Average(p => p.X);
                MeanY = Points.Average(p => p.Y);

                if (n > 1)
                {
                    double sumSqX = Points.Sum(p => (p.X - MeanX) * (p.X - MeanX));
                    double sumSqY = Points.Sum(p => (p.Y - MeanY) * (p.Y - MeanY));
                    double sumCross = Points.Sum(p => (p.X - MeanX) * (p.Y - MeanY));

                    StdDevX = Math.Sqrt(sumSqX / (n - 1));
                    StdDevY = Math.Sqrt(sumSqY / (n - 1));
                    Covariance = sumCross / (n - 1);

                    if (StdDevX > 1e-12 && StdDevY > 1e-12)
                    {
                        PearsonR = Covariance / (StdDevX * StdDevY);
                        if (PearsonR > 1.0) PearsonR = 1.0;
                        if (PearsonR < -1.0) PearsonR = -1.0;
                        RSquared = PearsonR * PearsonR;
                    }

                    if (sumSqX > 1e-12)
                    {
                        Slope = sumCross / sumSqX;
                        Intercept = MeanY - Slope * MeanX;
                    }
                    else
                    {
                        Slope = 0;
                        Intercept = MeanY;
                    }
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Atributos gráficos customizados para o Gráfico de Dispersão no Canvas do Grasshopper.
    /// </summary>
    public class ChartScatter_Attributes : GH_ComponentAttributes
    {
        private const int GRAPH_WIDTH = 380;
        private const int GRAPH_HEIGHT = 240;
        private RectangleF m_btnExportRect;

        public ChartScatter_Attributes(ChartScatter_Component owner) : base(owner)
        {
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && m_btnExportRect.Contains(e.CanvasLocation))
            {
                var comp = Owner as ChartScatter_Component;
                if (comp != null)
                {
                    string saved = comp.SavePngDialog();
                    if (!string.IsNullOrEmpty(saved))
                    {
                        comp.Message = "PNG Salvo!";
                        sender.Refresh();
                    }
                }
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseDown(sender, e);
        }

        protected override void Layout()
        {
            base.Layout();
            float oldRight = Bounds.Right;
            RectangleF b = Bounds;
            b.Width = Math.Max(b.Width, GRAPH_WIDTH + 24);
            b.Height += GRAPH_HEIGHT + 18;
            Bounds = b;

            // Alinha outputs na borda direita expandida
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
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            // Centraliza o ícone do componente no Pivot real dos Bounds expandidos
            if (channel == GH_CanvasChannel.Objects)
            {
                var _savedPivot = Pivot;
                Pivot = new PointF(Bounds.X + Bounds.Width / 2f, _savedPivot.Y);
                base.Render(canvas, graphics, channel);
                Pivot = _savedPivot;
            }
            else
            {
                base.Render(canvas, graphics, channel);
            }


            if (channel == GH_CanvasChannel.Objects)
            {
                var comp = Owner as ChartScatter_Component;
                if (comp == null) return;

                RectangleF b = Bounds;
                RectangleF graphRect = new RectangleF(b.X + 12, b.Bottom - GRAPH_HEIGHT - 10, b.Width - 24, GRAPH_HEIGHT);
                RectangleF headerRect = new RectangleF(graphRect.X, graphRect.Y, graphRect.Width, 24);
                RectangleF footerRect = new RectangleF(graphRect.X, graphRect.Bottom - 22, graphRect.Width, 22);

                float plotTop = headerRect.Bottom + 18;
                float plotBottom = footerRect.Y - 28;
                float plotHeight = Math.Max(80, plotBottom - plotTop);

                RectangleF plotRect = new RectangleF(
                    graphRect.X + 46,
                    plotTop,
                    graphRect.Width - 58,
                    plotHeight);

                // 1. Painel Principal Escuro
                using (var bgBrush = new SolidBrush(Color.FromArgb(20, 23, 29)))
                {
                    graphics.FillRectangle(bgBrush, graphRect);
                }
                using (var borderPen = new Pen(Color.FromArgb(65, 72, 85), 1.2f))
                {
                    graphics.DrawRectangle(borderPen, graphRect.X, graphRect.Y, graphRect.Width, graphRect.Height);
                }

                using (var headerBrush = new SolidBrush(Color.FromArgb(30, 34, 43)))
                {
                    graphics.FillRectangle(headerBrush, headerRect);
                }
                using (var footerBrush = new SolidBrush(Color.FromArgb(24, 27, 34)))
                {
                    graphics.FillRectangle(footerBrush, footerRect);
                }
                using (var linePen = new Pen(Color.FromArgb(50, 56, 68), 1f))
                {
                    graphics.DrawLine(linePen, headerRect.X, headerRect.Bottom, headerRect.Right, headerRect.Bottom);
                    graphics.DrawLine(linePen, footerRect.X, footerRect.Y, footerRect.Right, footerRect.Y);
                }

                // Título
                using (var titleFont = new Font(GH_FontServer.Standard.FontFamily, 8f, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(Color.FromArgb(240, 245, 250)))
                {
                    graphics.DrawString($"▪ {comp.DisplayTitle}", titleFont, titleBrush, headerRect.X + 8, headerRect.Y + 4);
                }

                int totalPts = comp.DisplaySeries.Sum(s => s.Points.Count);
                using (var badgeFont = new Font(GH_FontServer.Standard.FontFamily, 7.5f, FontStyle.Bold))
                using (var badgeBrush = new SolidBrush(Color.FromArgb(0, 220, 255)))
                {
                    graphics.DrawString($"({totalPts} pontos)", badgeFont, badgeBrush, headerRect.Right - 150, headerRect.Y + 5);
                }

                // Botão "💾 Salvar PNG"
                float btnW = 75f;
                float btnH = 18f;
                m_btnExportRect = new RectangleF(headerRect.Right - btnW - 6, headerRect.Y + 3, btnW, btnH);

                using (var btnBg = new SolidBrush(Color.FromArgb(44, 52, 64)))
                using (var btnBorder = new Pen(Color.FromArgb(80, 92, 110), 1f))
                using (var btnFont = new Font(GH_FontServer.Standard.FontFamily, 6.5f, FontStyle.Bold))
                using (var btnTextBrush = new SolidBrush(Color.FromArgb(220, 230, 242)))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    graphics.FillRectangle(btnBg, m_btnExportRect);
                    graphics.DrawRectangle(btnBorder, m_btnExportRect.X, m_btnExportRect.Y, m_btnExportRect.Width, m_btnExportRect.Height);
                    graphics.DrawString("💾 Salvar PNG", btnFont, btnTextBrush, m_btnExportRect, sf);
                }

                // 2. Área do Gráfico
                using (var plotBrush = new SolidBrush(Color.FromArgb(12, 14, 18)))
                {
                    graphics.FillRectangle(plotBrush, plotRect);
                }

                if (comp.DisplaySeries.Count == 0)
                {
                    using (var emptyFont = new Font(GH_FontServer.Standard.FontFamily, 7.5f, FontStyle.Italic))
                    using (var emptyBrush = new SolidBrush(Color.FromArgb(130, 140, 155)))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        graphics.DrawString("Conecte os dados X e Y para visualizar a dispersão.", emptyFont, emptyBrush, plotRect, sf);
                    }
                    return;
                }

                // Título Eixo Y
                using (var yTitleFont = new Font(GH_FontServer.Standard.FontFamily, 6.5f, FontStyle.Bold))
                using (var yTitleBrush = new SolidBrush(Color.FromArgb(180, 195, 215)))
                {
                    graphics.DrawString(comp.DisplayYLabel, yTitleFont, yTitleBrush, graphRect.X + 6, plotRect.Y - 14);
                }

                double yMin = comp.GlobalMinY;
                double yMax = comp.GlobalMaxY;
                double spanY = yMax - yMin;
                if (spanY <= 1e-6) spanY = 1.0;
                double plotMinY = yMin - spanY * 0.05;
                double plotMaxY = yMax + spanY * 0.05;
                double plotSpanY = plotMaxY - plotMinY;

                double xMin = comp.GlobalMinX;
                double xMax = comp.GlobalMaxX;
                double spanX = xMax - xMin;
                if (spanX <= 1e-6) spanX = 1.0;
                double plotMinX = xMin - spanX * 0.05;
                double plotMaxX = xMax + spanX * 0.05;
                double plotSpanX = plotMaxX - plotMinX;

                Func<double, float> mapX = (x) => (float)(plotRect.X + ((x - plotMinX) / plotSpanX) * plotRect.Width);
                Func<double, float> mapY = (y) => (float)(plotRect.Bottom - ((y - plotMinY) / plotSpanY) * plotRect.Height);

                // Grid Y
                using (var gridPen = new Pen(Color.FromArgb(32, 38, 48), 1f) { DashStyle = DashStyle.Dot })
                using (var tickFont = new Font(GH_FontServer.Standard.FontFamily, 6f, FontStyle.Regular))
                using (var tickBrush = new SolidBrush(Color.FromArgb(145, 155, 170)))
                using (var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                {
                    for (int i = 0; i <= 4; i++)
                    {
                        double val = plotMinY + (plotSpanY * i / 4);
                        float py = mapY(val);
                        if (py >= plotRect.Top && py <= plotRect.Bottom)
                        {
                            graphics.DrawLine(gridPen, plotRect.X, py, plotRect.Right, py);
                            graphics.DrawString(val.ToString("G3"), tickFont, tickBrush, plotRect.X - 4, py, sfRight);
                        }
                    }
                }

                // Grid X
                using (var gridPen = new Pen(Color.FromArgb(32, 38, 48), 1f) { DashStyle = DashStyle.Dot })
                using (var tickFont = new Font(GH_FontServer.Standard.FontFamily, 6f, FontStyle.Regular))
                using (var tickBrush = new SolidBrush(Color.FromArgb(145, 155, 170)))
                using (var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
                {
                    for (int i = 0; i <= 5; i++)
                    {
                        double val = plotMinX + (plotSpanX * i / 5);
                        float px = mapX(val);
                        if (px >= plotRect.X && px <= plotRect.Right)
                        {
                            graphics.DrawLine(gridPen, px, plotRect.Top, px, plotRect.Bottom);
                            graphics.DrawString(val.ToString("G3"), tickFont, tickBrush, px, plotRect.Bottom + 4, sfCenter);
                        }
                    }
                }

                // Rótulo Eixo X
                using (var xTitleFont = new Font(GH_FontServer.Standard.FontFamily, 6.5f, FontStyle.Bold))
                using (var xTitleBrush = new SolidBrush(Color.FromArgb(180, 195, 215)))
                using (var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
                {
                    graphics.DrawString(comp.DisplayXLabel, xTitleFont, xTitleBrush, plotRect.X + plotRect.Width * 0.5f, plotRect.Bottom + 15, sfCenter);
                }

                using (var plotBorderPen = new Pen(Color.FromArgb(50, 60, 75), 1.2f))
                {
                    graphics.DrawRectangle(plotBorderPen, plotRect.X, plotRect.Y, plotRect.Width, plotRect.Height);
                }

                var oldClip = graphics.Clip;
                graphics.SetClip(plotRect);

                // Desenhar Dispersão
                foreach (var s in comp.DisplaySeries)
                {
                    Color baseCol = s.Color;

                    if (comp.DisplayShowStats && s.Points.Count > 1)
                    {
                        // 1. Linha de Regressão Linear
                        float rx1 = mapX(plotMinX);
                        float ry1 = mapY(s.Slope * plotMinX + s.Intercept);
                        float rx2 = mapX(plotMaxX);
                        float ry2 = mapY(s.Slope * plotMaxX + s.Intercept);

                        using (var trendPen = new Pen(Color.FromArgb(230, 231, 76, 60), 1.5f))
                        {
                            graphics.DrawLine(trendPen, rx1, ry1, rx2, ry2);
                        }

                        // 2. Elipse
                        float cx = mapX(s.MeanX);
                        float cy = mapY(s.MeanY);
                        float rx = (float)((s.StdDevX / plotSpanX) * plotRect.Width);
                        float ry = (float)((s.StdDevY / plotSpanY) * plotRect.Height);

                        if (rx > 2 && ry > 2)
                        {
                            using (var ellipsePen = new Pen(Color.FromArgb(100, baseCol.R, baseCol.G, baseCol.B), 1f) { DashStyle = DashStyle.Dash })
                            using (var ellipseFill = new SolidBrush(Color.FromArgb(20, baseCol.R, baseCol.G, baseCol.B)))
                            {
                                graphics.FillEllipse(ellipseFill, cx - rx, cy - ry, rx * 2, ry * 2);
                                graphics.DrawEllipse(ellipsePen, cx - rx, cy - ry, rx * 2, ry * 2);
                            }
                        }
                    }

                    // Bolinhas
                    foreach (var pt in s.Points)
                    {
                        float px = mapX(pt.X);
                        float py = mapY(pt.Y);
                        float pr = (float)Math.Max(2.0, Math.Min(12.0, pt.Radius * 0.7));

                        using (var bBrush = new SolidBrush(Color.FromArgb(190, baseCol.R, baseCol.G, baseCol.B)))
                        using (var bPen = new Pen(Color.FromArgb(20, 23, 29), 0.8f))
                        {
                            graphics.FillEllipse(bBrush, px - pr, py - pr, pr * 2, pr * 2);
                            graphics.DrawEllipse(bPen, px - pr, py - pr, pr * 2, pr * 2);
                        }
                    }

                    // Centróide
                    if (comp.DisplayShowStats)
                    {
                        float cx = mapX(s.MeanX);
                        float cy = mapY(s.MeanY);

                        using (var crossPen = new Pen(Color.FromArgb(255, 145, 40), 2f))
                        using (var ringPen = new Pen(Color.White, 1.2f))
                        {
                            graphics.DrawLine(crossPen, cx - 5, cy, cx + 5, cy);
                            graphics.DrawLine(crossPen, cx, cy - 5, cx, cy + 5);
                            graphics.DrawEllipse(ringPen, cx - 3, cy - 3, 6, 6);
                        }
                    }
                }

                graphics.Clip = oldClip;

                // Rodapé
                using (var footerFont = new Font(GH_FontServer.Standard.FontFamily, 6.5f, FontStyle.Regular))
                using (var footerBrush = new SolidBrush(Color.FromArgb(140, 155, 175)))
                {
                    if (comp.DisplaySeries.Count > 0)
                    {
                        var first = comp.DisplaySeries[0];
                        string summary = $"Centróide: ({first.MeanX:F2}, {first.MeanY:F2}) | R² = {first.RSquared:F3} | r = {first.PearsonR:F2} | y = {first.Slope:F2}x + {first.Intercept:F2}";
                        graphics.DrawString(summary, footerFont, footerBrush, footerRect.X + 8, footerRect.Y + 4);
                    }
                }
            }
        }
    }
}
