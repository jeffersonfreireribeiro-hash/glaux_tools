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
    public class ChartLine_Component : GH_Component
    {
        public static readonly Color[] Palette = new Color[]
        {
            Color.FromArgb(0, 220, 255),   // 0: Ciano brilhante
            Color.FromArgb(255, 145, 40),  // 1: Laranja vibrante
            Color.FromArgb(50, 225, 120),  // 2: Verde esmeralda
            Color.FromArgb(240, 80, 200),  // 3: Magenta / Rosa neon
            Color.FromArgb(255, 215, 40),  // 4: Amarelo ouro
            Color.FromArgb(170, 110, 255), // 5: Violeta
            Color.FromArgb(255, 80, 80),   // 6: Coral avermelhado
            Color.FromArgb(100, 190, 255), // 7: Azul celeste
            Color.FromArgb(180, 230, 80),  // 8: Verde limão
            Color.FromArgb(255, 120, 180)  // 9: Rosa chá
        };

        // Cache para renderização no Canvas e Exportação
        public List<SeriesData> DisplaySeries = new List<SeriesData>();
        public string DisplayTitle = "Line Chart & Statistics";
        public string DisplayXLabel = "X Axis";
        public string DisplayYLabel = "Y Axis";
        public bool DisplayShowStats = true;
        public bool IsColumnsMode = false;
        public bool ShowCombinedCurve = true;
        public double GlobalMinX = 0, GlobalMaxX = 1;
        public double GlobalMinY = 0, GlobalMaxY = 1;
        public Bitmap CachedChartBmp;

        // Metas / Target e Tolerância
        public double? TargetValue = null;
        public double TargetMin = 0;
        public double TargetMax = 0;
        public double? DeltaTarget = null;

        // Dados Agregados / Histograma
        public List<Point3d> CachedCombinedMeanPts = new List<Point3d>();
        public List<Point3d> CachedCombinedModePts = new List<Point3d>();
        public HistogramData CachedHistData = null;

        public ChartLine_Component()
            : base(
                "Line Chart & Statistics",
                "ChartLine",
                "Gera gráficos 2D de linhas ou colunas/histogramas de alta definição com suporte a curva agregada (Média/Moda/KDE), múltiplas curvas (DataTree), valor alvo (Target) com tolerância e sobreposição de indicadores estatísticos completos.",
                "Glaux Tools",
                "Visual")
        {
        }

        public override Guid ComponentGuid => new Guid("1c2d3e4f-5a6b-7c8d-9e0f-1a2b3c4d5e6f");

        protected override Bitmap Icon => GlauxToolsIcons.ChartLine;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override void CreateAttributes()
        {
            m_attributes = new ChartLine_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("X Values", "X", "Valores do eixo X (Lista ou Árvore). Se omitido, utiliza índices incrementais 0, 1, 2...", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Y Values", "Y", "Valores do eixo Y (Lista ou Árvore). Cada ramo representa uma curva/série independente.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Title", "T", "Título principal do gráfico.", GH_ParamAccess.item, "Line Chart & Statistics");
            pManager.AddTextParameter("X Label", "XLab", "Rótulo/Nome do eixo X.", GH_ParamAccess.item, "X Axis");
            pManager.AddTextParameter("Y Label", "YLab", "Rótulo/Nome do eixo Y.", GH_ParamAccess.item, "Y Axis");
            pManager.AddBooleanParameter("Show Stats", "Stats", "Exibir linhas de referência estatística (Média, Mediana, Moda e Faixa ±1σ).", GH_ParamAccess.item, true);
            pManager.AddIntegerParameter("Width", "W", "Largura da imagem exportada em pixels.", GH_ParamAccess.item, 900);
            pManager.AddIntegerParameter("Height", "H", "Altura da imagem exportada em pixels.", GH_ParamAccess.item, 550);

            // Novos parâmetros adicionais
            pManager.AddGenericParameter("Chart Mode", "Mode", "Modo do gráfico: 0 ou 'Lines' para Gráfico de Linhas; 1 ou 'Columns'/'Histogram' para Gráfico de Colunas/Histograma de distribuição com curva KDE suave (idêntico à referência). Padrão: 'Lines'.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Combined Curve", "Combined", "Quando verdadeiro, calcula e plota a CURVA AGREGADA que junta todos os dados (Curva Média/Moda nas linhas, ou Curva KDE contínua nas colunas), atenuando as curvas individuais ao fundo como nuvem translúcida. Padrão: true.", GH_ParamAccess.item, true);
            pManager.AddGenericParameter("Target Value", "Target", "Valor alvo opcional ou faixa ideal (ex.: 1.40, ou Interval(1.2, 1.6), ou '1.2 To 1.6'). Quando fornecido, plota a linha do alvo ('Id'), faixa sombreada de tolerância ('Tol') e badge de desvio 'Δ: (Valor - Alvo)' no topo.", GH_ParamAccess.item);

            pManager[0].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
            pManager[8].Optional = true;
            pManager[9].Optional = true;
            pManager[10].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Chart Image", "Img", "Imagem renderizada do gráfico (System.Drawing.Bitmap).", GH_ParamAccess.item);
            pManager.AddTextParameter("Stats Report", "Rep", "Relatório quantitativo com Média, Mediana, Moda, Desvio Padrão, Alvo e Extremos por série.", GH_ParamAccess.item);
            pManager.AddPointParameter("Series Points", "Pts", "Árvore de pontos 3D (X, Y, 0) das curvas ou das colunas.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Series Curves", "Crv", "Curvas/Polilinhas das séries de dados no espaço do Rhino.", GH_ParamAccess.tree);
            pManager.AddLineParameter("Reference Lines", "Refs", "Linhas de referência estatística no Rhino (Média, Mediana, Moda, ±1σ e Alvo).", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Combined Curve", "Trend", "Curva agregada sintetizada (Curva Média no modo Linhas ou Curva KDE no modo Colunas) no espaço 3D do Rhino.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(1, out GH_Structure<GH_Number> yTree) || yTree == null || yTree.IsEmpty)
            {
                this.Message = "Sem Dados Y";
                DisplaySeries.Clear();
                CachedCombinedMeanPts.Clear();
                CachedCombinedModePts.Clear();
                CachedHistData = null;
                return;
            }

            DA.GetDataTree(0, out GH_Structure<GH_Number> xTree);

            string title = "Line Chart & Statistics";
            DA.GetData(2, ref title);
            DisplayTitle = title;

            string xLabel = "X Axis";
            DA.GetData(3, ref xLabel);
            DisplayXLabel = xLabel;

            string yLabel = "Y Axis";
            DA.GetData(4, ref yLabel);
            DisplayYLabel = yLabel;

            bool showStats = DisplayShowStats;
            DA.GetData(5, ref showStats);
            DisplayShowStats = showStats;

            int width = 900;
            DA.GetData(6, ref width);
            if (width < 300) width = 300;
            if (width > 4000) width = 4000;

            int height = 550;
            DA.GetData(7, ref height);
            if (height < 200) height = 200;
            if (height > 3000) height = 3000;

            // 1. Modo do Gráfico (Linhas vs Colunas)
            bool isColumns = IsColumnsMode;
            object rawMode = null;
            if (DA.GetData(8, ref rawMode) && rawMode != null)
            {
                isColumns = IsColumnsModeInput(rawMode);
            }
            IsColumnsMode = isColumns;

            // 2. Curva Agregada / Seguimento de Dados
            bool combined = ShowCombinedCurve;
            DA.GetData(9, ref combined);
            ShowCombinedCurve = combined;

            // 3. Valor Alvo / Target & Tolerância
            double? targetVal = null;
            double tMin = 0, tMax = 0;
            object rawTarget = null;
            if (DA.GetData(10, ref rawTarget) && rawTarget != null)
            {
                if (TryParseTarget(rawTarget, out double parsedTgt, out double pMin, out double pMax))
                {
                    targetVal = parsedTgt;
                    tMin = pMin;
                    tMax = pMax;
                }
            }
            TargetValue = targetVal;
            TargetMin = tMin;
            TargetMax = tMax;

            // Coleta de Séries de Dados
            var seriesList = new List<SeriesData>();
            int branchIdx = 0;

            double globalMinX = double.MaxValue, globalMaxX = double.MinValue;
            double globalMinY = double.MaxValue, globalMaxY = double.MinValue;

            foreach (GH_Path path in yTree.Paths)
            {
                var yBranch = yTree.get_Branch(path);
                if (yBranch == null || yBranch.Count == 0) continue;

                var xBranch = (xTree != null && xTree.PathExists(path)) ? xTree.get_Branch(path) : null;
                if (xBranch == null && xTree != null && xTree.Paths.Count > 0 && branchIdx < xTree.Paths.Count)
                {
                    xBranch = xTree.get_Branch(xTree.Paths[branchIdx]);
                }

                var sData = new SeriesData
                {
                    Path = path,
                    Name = $"Série {path}",
                    Color = Palette[branchIdx % Palette.Length]
                };

                for (int i = 0; i < yBranch.Count; i++)
                {
                    if (!GH_Convert.ToDouble(yBranch[i], out double yVal, GH_Conversion.Both) || double.IsNaN(yVal) || double.IsInfinity(yVal))
                        continue;

                    double xVal = i;
                    if (xBranch != null && i < xBranch.Count)
                    {
                        if (GH_Convert.ToDouble(xBranch[i], out double xParsed, GH_Conversion.Both) && !double.IsNaN(xParsed) && !double.IsInfinity(xParsed))
                        {
                            xVal = xParsed;
                        }
                    }

                    sData.X.Add(xVal);
                    sData.Y.Add(yVal);

                    if (xVal < globalMinX) globalMinX = xVal;
                    if (xVal > globalMaxX) globalMaxX = xVal;
                    if (yVal < globalMinY) globalMinY = yVal;
                    if (yVal > globalMaxY) globalMaxY = yVal;
                }

                if (sData.Y.Count > 0)
                {
                    sData.ComputeStatistics();
                    seriesList.Add(sData);
                }
                branchIdx++;
            }

            if (seriesList.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum dado numérico válido encontrado.");
                DisplaySeries.Clear();
                return;
            }

            // Expandir limites se Target foi fornecido
            if (targetVal.HasValue)
            {
                if (isColumns)
                {
                    if (tMin < globalMinY) globalMinY = tMin;
                    if (tMax > globalMaxY) globalMaxY = tMax;
                }
                else
                {
                    if (tMin < globalMinY) globalMinY = tMin;
                    if (tMax > globalMaxY) globalMaxY = tMax;
                }
            }

            if (globalMinX >= globalMaxX) { globalMinX -= 1.0; globalMaxX += 1.0; }
            if (globalMinY >= globalMaxY) { globalMinY -= 1.0; globalMaxY += 1.0; }

            DisplaySeries = seriesList;
            GlobalMinX = globalMinX;
            GlobalMaxX = globalMaxX;
            GlobalMinY = globalMinY;
            GlobalMaxY = globalMaxY;

            // 4. Curva Agregada / Seguimento Suave dos Dados
            CachedCombinedMeanPts.Clear();
            CachedCombinedModePts.Clear();

            if (seriesList.Count > 1)
            {
                // MÚLTIPLAS SÉRIES: Agrupa dados por coordenada X (Média e Moda em cada X)
                var pointsByX = new SortedDictionary<double, List<double>>();
                foreach (var s in seriesList)
                {
                    for (int i = 0; i < s.X.Count; i++)
                    {
                        double rx = Math.Round(s.X[i], 4);
                        if (!pointsByX.ContainsKey(rx)) pointsByX[rx] = new List<double>();
                        pointsByX[rx].Add(s.Y[i]);
                    }
                }

                foreach (var kvp in pointsByX)
                {
                    double x = kvp.Key;
                    double mean = kvp.Value.Average();
                    double mode = CalculateMode(kvp.Value);
                    CachedCombinedMeanPts.Add(new Point3d(x, mean, 0.0));
                    CachedCombinedModePts.Add(new Point3d(x, mode, 0.0));
                }
            }
            else if (seriesList.Count == 1)
            {
                // UMA MESMA LISTA: Gera a Curva de Tendência Suave (Média Móvel Gaussiana Ponderada / Kernel Trend)
                var single = seriesList[0];
                int nPts = single.X.Count;
                if (nPts > 2)
                {
                    // Ordenar pares (X, Y)
                    var sortedPairs = single.X.Select((x, idx) => new { X = x, Y = single.Y[idx] })
                                             .OrderBy(p => p.X)
                                             .ToList();

                    double xMinL = sortedPairs.First().X;
                    double xMaxL = sortedPairs.Last().X;
                    double xSpanL = Math.Max(1e-4, xMaxL - xMinL);

                    // Largura de banda adaptativa proporcional à dispersão em X
                    double h = xSpanL / Math.Max(5.0, Math.Min(35.0, Math.Sqrt(nPts) * 2.5));

                    int evalSteps = Math.Max(nPts, 50);
                    for (int step = 0; step < evalSteps; step++)
                    {
                        double curX = xMinL + (xSpanL * step) / (evalSteps - 1.0);

                        double sumW = 0.0;
                        double sumWY = 0.0;
                        var localWindowY = new List<double>();

                        for (int j = 0; j < nPts; j++)
                        {
                            double dx = sortedPairs[j].X - curX;
                            double w = Math.Exp(-0.5 * (dx * dx) / (h * h));
                            sumW += w;
                            sumWY += w * sortedPairs[j].Y;
                            if (Math.Abs(dx) <= 1.5 * h)
                            {
                                localWindowY.Add(sortedPairs[j].Y);
                            }
                        }

                        double trendMean = sumW > 1e-9 ? (sumWY / sumW) : sortedPairs[Math.Min(step, nPts - 1)].Y;
                        double trendMode = localWindowY.Count > 2 ? CalculateMode(localWindowY) : trendMean;

                        CachedCombinedMeanPts.Add(new Point3d(curX, trendMean, 0.0));
                        CachedCombinedModePts.Add(new Point3d(curX, trendMode, 0.0));
                    }
                }
                else if (nPts > 0)
                {
                    for (int i = 0; i < nPts; i++)
                    {
                        CachedCombinedMeanPts.Add(new Point3d(single.X[i], single.Y[i], 0.0));
                        CachedCombinedModePts.Add(new Point3d(single.X[i], single.Y[i], 0.0));
                    }
                }
            }

            // 5. Histograma e Curva KDE para Modo Colunas
            var allVals = seriesList.SelectMany(s => s.Y).ToList();
            if (isColumns && allVals.Count > 0)
            {
                CachedHistData = ComputeHistogramData(allVals, targetVal, tMin, tMax);
                if (targetVal.HasValue)
                {
                    DeltaTarget = CachedHistData.Mode - targetVal.Value;
                }
            }
            else
            {
                CachedHistData = null;
                if (targetVal.HasValue && CachedCombinedMeanPts.Count > 0)
                {
                    double globalMean = allVals.Average();
                    DeltaTarget = globalMean - targetVal.Value;
                }
                else
                {
                    DeltaTarget = null;
                }
            }

            // Renderizar Gráfico GDI+ de Alta Resolução para saída Img
            CachedChartBmp = RenderLineChart(seriesList, title, xLabel, yLabel, showStats, width, height,
                globalMinX, globalMaxX, globalMinY, globalMaxY, isColumns, combined, targetVal, tMin, tMax, DeltaTarget, CachedHistData, CachedCombinedMeanPts, CachedCombinedModePts);

            // Montar Relatório e Geometrias Rhino
            var repBuilder = new StringBuilder();
            repBuilder.AppendLine("=================================================");
            repBuilder.AppendLine($"       BURAQUEIRA CHART & STATISTICS REPORT");
            repBuilder.AppendLine($"       Título: {title}");
            repBuilder.AppendLine($"       Modo:   {(isColumns ? "Colunas / Histograma (KDE)" : "Linhas / Curvas")}");
            repBuilder.AppendLine($"       Séries Analisadas: {seriesList.Count} ({allVals.Count} pontos no total)");
            if (targetVal.HasValue)
            {
                repBuilder.AppendLine($"       Meta Ideal (Alvo): {targetVal.Value:F4} (Faixa Tol: [{tMin:F4} .. {tMax:F4}])");
                if (DeltaTarget.HasValue)
                {
                    repBuilder.AppendLine($"       Desvio Δ:          {DeltaTarget.Value:+0.00;-0.00;0.00}");
                }
            }
            repBuilder.AppendLine("=================================================");

            var outPtsTree = new GH_Structure<GH_Point>();
            var outCrvTree = new GH_Structure<GH_Curve>();
            var outRefsTree = new GH_Structure<GH_Line>();
            Curve outTrendCurve = null;

            if (isColumns && CachedHistData != null)
            {
                repBuilder.AppendLine();
                repBuilder.AppendLine("--- [DISTRIBUIÇÃO HISTOGRAMA & KDE] ---");
                repBuilder.AppendLine($"  Média (μ):           {CachedHistData.Mean:F4}");
                repBuilder.AppendLine($"  Mediana (Q2):        {CachedHistData.Median:F4}");
                repBuilder.AppendLine($"  Moda (Mo):           {CachedHistData.Mode:F4}");
                repBuilder.AppendLine($"  Desvio Padrão (σ):   {CachedHistData.StdDev:F4}");
                repBuilder.AppendLine($"  Número de Bins:      {CachedHistData.BinCounts.Length}");
                repBuilder.AppendLine($"  Largura do Bin:      {CachedHistData.BinWidth:F4}");

                // Curva KDE como curva 3D no Rhino
                if (CachedHistData.KdePoints != null && CachedHistData.KdePoints.Length > 1)
                {
                    var kde3d = CachedHistData.KdePoints.Select(p => new Point3d(p.X, p.Y, 0.0)).ToList();
                    outTrendCurve = Curve.CreateInterpolatedCurve(kde3d, 3);
                }

                // Barras do histograma como polilinhas
                var histPath = new GH_Path(0);
                outPtsTree.EnsurePath(histPath);
                outCrvTree.EnsurePath(histPath);
                outRefsTree.EnsurePath(histPath);

                for (int b = 0; b < CachedHistData.BinCounts.Length; b++)
                {
                    double bx0 = CachedHistData.MinVal + b * CachedHistData.BinWidth;
                    double bx1 = bx0 + CachedHistData.BinWidth;
                    double by = CachedHistData.BinCounts[b];

                    outPtsTree.Append(new GH_Point(new Point3d(CachedHistData.BinCenters[b], by, 0.0)), histPath);

                    var rectPts = new Point3d[]
                    {
                        new Point3d(bx0, 0, 0),
                        new Point3d(bx0, by, 0),
                        new Point3d(bx1, by, 0),
                        new Point3d(bx1, 0, 0),
                        new Point3d(bx0, 0, 0)
                    };
                    outCrvTree.Append(new GH_Curve(new Polyline(rectPts).ToNurbsCurve()), histPath);
                }

                if (targetVal.HasValue)
                {
                    outRefsTree.Append(new GH_Line(new Line(new Point3d(targetVal.Value, 0, 0), new Point3d(targetVal.Value, CachedHistData.MaxBinCount, 0))), histPath);
                    outRefsTree.Append(new GH_Line(new Line(new Point3d(CachedHistData.Mode, 0, 0), new Point3d(CachedHistData.Mode, CachedHistData.MaxBinCount, 0))), histPath);
                }
            }
            else
            {
                foreach (var s in seriesList)
                {
                    repBuilder.AppendLine();
                    repBuilder.AppendLine($"--- [{s.Name}] (N = {s.Y.Count}) ---");
                    repBuilder.AppendLine($"  Média (μ):           {s.Mean:F4}");
                    repBuilder.AppendLine($"  Mediana (Q2):        {s.Median:F4}");
                    repBuilder.AppendLine($"  Moda (Mo):           {s.Mode:F4}");
                    repBuilder.AppendLine($"  Desvio Padrão (σ):   {s.StdDev:F4}");
                    repBuilder.AppendLine($"  Variância (σ²):      {s.Variance:F4}");
                    repBuilder.AppendLine($"  Mínimo:              {s.MinY:F4}");
                    repBuilder.AppendLine($"  Máximo:              {s.MaxY:F4}");
                    repBuilder.AppendLine($"  Faixa [μ-σ, μ+σ]:    [{s.Mean - s.StdDev:F4}, {s.Mean + s.StdDev:F4}]");

                    outPtsTree.EnsurePath(s.Path);
                    outCrvTree.EnsurePath(s.Path);
                    outRefsTree.EnsurePath(s.Path);

                    var pts3d = new List<Point3d>();
                    for (int i = 0; i < s.X.Count; i++)
                    {
                        var pt = new Point3d(s.X[i], s.Y[i], 0.0);
                        pts3d.Add(pt);
                        outPtsTree.Append(new GH_Point(pt), s.Path);
                    }

                    if (pts3d.Count > 1)
                    {
                        var poly = new Polyline(pts3d);
                        outCrvTree.Append(new GH_Curve(poly.ToNurbsCurve()), s.Path);
                    }

                    if (showStats && pts3d.Count > 0)
                    {
                        double xStart = s.X.Min();
                        double xEnd = s.X.Max();
                        if (Math.Abs(xStart - xEnd) < 1e-6)
                        {
                            xStart -= 1.0;
                            xEnd += 1.0;
                        }

                        outRefsTree.Append(new GH_Line(new Line(new Point3d(xStart, s.Mean, 0), new Point3d(xEnd, s.Mean, 0))), s.Path);
                        outRefsTree.Append(new GH_Line(new Line(new Point3d(xStart, s.Median, 0), new Point3d(xEnd, s.Median, 0))), s.Path);
                        outRefsTree.Append(new GH_Line(new Line(new Point3d(xStart, s.Mode, 0), new Point3d(xEnd, s.Mode, 0))), s.Path);
                        outRefsTree.Append(new GH_Line(new Line(new Point3d(xStart, s.Mean + s.StdDev, 0), new Point3d(xEnd, s.Mean + s.StdDev, 0))), s.Path);
                        outRefsTree.Append(new GH_Line(new Line(new Point3d(xStart, s.Mean - s.StdDev, 0), new Point3d(xEnd, s.Mean - s.StdDev, 0))), s.Path);
                    }
                }

                // Curva Agregada em Rhino 3D
                if (combined && CachedCombinedMeanPts.Count > 1)
                {
                    try
                    {
                        outTrendCurve = Curve.CreateInterpolatedCurve(CachedCombinedMeanPts, 3);
                    }
                    catch
                    {
                        outTrendCurve = new Polyline(CachedCombinedMeanPts).ToNurbsCurve();
                    }
                }

                if (targetVal.HasValue && seriesList.Count > 0)
                {
                    var rPath = new GH_Path(999);
                    outRefsTree.EnsurePath(rPath);
                    outRefsTree.Append(new GH_Line(new Line(new Point3d(globalMinX, targetVal.Value, 0), new Point3d(globalMaxX, targetVal.Value, 0))), rPath);
                }
            }

            if (isColumns)
            {
                this.Message = targetVal.HasValue ? $"Colunas | Δ: {DeltaTarget:F2}" : "Colunas / Hist";
            }
            else
            {
                string sCount = seriesList.Count == 1 ? "1 Série" : $"{seriesList.Count} Séries";
                this.Message = targetVal.HasValue ? $"{sCount} | Δ: {DeltaTarget:F2}" : sCount;
            }

            DA.SetData(0, CachedChartBmp);
            DA.SetData(1, repBuilder.ToString());
            DA.SetDataTree(2, outPtsTree);
            DA.SetDataTree(3, outCrvTree);
            DA.SetDataTree(4, outRefsTree);
            if (outTrendCurve != null) DA.SetData(5, outTrendCurve);
        }

        #region Context Menu & Serialization

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            Menu_AppendSeparator(menu);
            var miMode = Menu_AppendItem(menu, "Modo do Gráfico (Chart Mode)");
            Menu_AppendItem(miMode.DropDown, "Gráfico de Linhas (Lines)", (s, e) =>
            {
                RecordUndoEvent("Mode Lines");
                IsColumnsMode = false;
                ExpireSolution(true);
            }, true, !IsColumnsMode);

            Menu_AppendItem(miMode.DropDown, "Gráfico de Colunas / Histograma (Columns/KDE)", (s, e) =>
            {
                RecordUndoEvent("Mode Columns");
                IsColumnsMode = true;
                ExpireSolution(true);
            }, true, IsColumnsMode);

            Menu_AppendItem(menu, "Exibir Linhas Estatísticas (Stats: μ, Med, Mo, ±1σ)", (s, e) =>
            {
                RecordUndoEvent("Toggle Show Stats");
                DisplayShowStats = !DisplayShowStats;
                ExpireSolution(true);
            }, true, DisplayShowStats);

            Menu_AppendItem(menu, "Exibir Curva Combinada / Agregada (Trend / KDE)", (s, e) =>
            {
                RecordUndoEvent("Toggle Combined Curve");
                ShowCombinedCurve = !ShowCombinedCurve;
                ExpireSolution(true);
            }, true, ShowCombinedCurve);
        }

        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            writer.SetBoolean("IsColumnsMode", IsColumnsMode);
            writer.SetBoolean("DisplayShowStats", DisplayShowStats);
            writer.SetBoolean("ShowCombinedCurve", ShowCombinedCurve);
            return base.Write(writer);
        }

        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            if (reader.ItemExists("IsColumnsMode")) IsColumnsMode = reader.GetBoolean("IsColumnsMode");
            if (reader.ItemExists("DisplayShowStats")) DisplayShowStats = reader.GetBoolean("DisplayShowStats");
            if (reader.ItemExists("ShowCombinedCurve")) ShowCombinedCurve = reader.GetBoolean("ShowCombinedCurve");
            return base.Read(reader);
        }

        #endregion

        public string SavePngDialog()
        {
            if (CachedChartBmp == null) return null;
            try
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Title = "Salvar Gráfico Editorial - BURAQUEIRA Tools";
                    sfd.Filter = "PNG Image (*.png)|*.png|All files (*.*)|*.*";
                    string safeTitle = DisplayTitle.Replace(":", "_").Replace("/", "_").Replace("\\", "_").Replace("[", "").Replace("]", "").Trim();
                    sfd.FileName = $"{safeTitle}_{(IsColumnsMode ? "Histogram" : "LineChart")}.png";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        CachedChartBmp.Save(sfd.FileName, ImageFormat.Png);
                        return sfd.FileName;
                    }
                }
            }
            catch (Exception ex)
            {
                Rhino.RhinoApp.WriteLine($"[ChartLine] Erro ao salvar imagem: {ex.Message}");
            }
            return null;
        }

        public static FontFamily GetUIFontFamily()
        {
            try
            {
                if (GH_FontServer.Standard != null && GH_FontServer.Standard.FontFamily != null)
                    return GH_FontServer.Standard.FontFamily;
            }
            catch { }
            try
            {
                return new FontFamily("Segoe UI");
            }
            catch
            {
                return FontFamily.GenericSansSerif;
            }
        }

        public static bool IsColumnsModeInput(object raw)
        {
            if (raw == null) return false;
            if (raw is IGH_Goo goo) raw = goo.SafeScriptVariable();
            if (raw is GH_ObjectWrapper wrap) raw = wrap.Value;
            if (raw is bool b) return b;

            if (GH_Convert.ToInt32(raw, out int intVal, GH_Conversion.Both))
            {
                return intVal == 1;
            }
            if (GH_Convert.ToDouble(raw, out double dVal, GH_Conversion.Both))
            {
                return Math.Abs(dVal - 1.0) < 0.1;
            }

            string s = raw.ToString().Trim().ToLowerInvariant();
            return s == "1" || s.Contains("col") || s.Contains("bar") || s.Contains("hist");
        }

        public static bool TryParseTarget(object raw, out double target, out double tMin, out double tMax)
        {
            target = 0;
            tMin = 0;
            tMax = 0;
            if (raw == null) return false;

            if (raw is IGH_Goo goo)
            {
                if (goo.CastTo(out Interval iv) && iv.IsValid)
                {
                    tMin = Math.Min(iv.Min, iv.Max);
                    tMax = Math.Max(iv.Min, iv.Max);
                    target = (tMin + tMax) * 0.5;
                    return true;
                }
                raw = goo.SafeScriptVariable();
            }

            if (raw is Interval directIv && directIv.IsValid)
            {
                tMin = Math.Min(directIv.Min, directIv.Max);
                tMax = Math.Max(directIv.Min, directIv.Max);
                target = (tMin + tMax) * 0.5;
                return true;
            }

            if (raw is double d && !double.IsNaN(d) && !double.IsInfinity(d))
            {
                target = d;
                double tol = Math.Abs(d) * 0.15;
                if (tol < 1e-4) tol = 0.5;
                tMin = target - tol;
                tMax = target + tol;
                return true;
            }

            if (raw is int iVal)
            {
                target = iVal;
                double tol = Math.Abs(target) * 0.15;
                if (tol < 1e-4) tol = 0.5;
                tMin = target - tol;
                tMax = target + tol;
                return true;
            }

            string s = raw.ToString();
            if (!string.IsNullOrWhiteSpace(s))
            {
                string[] parts = s.Split(new string[] { "To", "to", "TO", "..", ";", "," }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 &&
                    double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double p0) &&
                    double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double p1))
                {
                    tMin = Math.Min(p0, p1);
                    tMax = Math.Max(p0, p1);
                    target = (tMin + tMax) * 0.5;
                    return true;
                }
                else if (parts.Length == 1 &&
                    double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double singleVal))
                {
                    target = singleVal;
                    double tol = Math.Abs(target) * 0.15;
                    if (tol < 1e-4) tol = 0.5;
                    tMin = target - tol;
                    tMax = target + tol;
                    return true;
                }
            }
            return false;
        }

        public static double CalculateMode(List<double> vals)
        {
            if (vals == null || vals.Count == 0) return 0.0;
            if (vals.Count == 1) return vals[0];
            double min = vals.Min();
            double max = vals.Max();
            double span = max - min;
            if (span <= 1e-6) return vals.Average();

            int bins = Math.Max(5, (int)Math.Sqrt(vals.Count));
            double binW = span / bins;
            int[] counts = new int[bins];
            double[] sums = new double[bins];

            foreach (var v in vals)
            {
                int b = (int)((v - min) / binW);
                if (b >= bins) b = bins - 1;
                counts[b]++;
                sums[b] += v;
            }

            int maxB = 0;
            for (int i = 1; i < bins; i++)
            {
                if (counts[i] > counts[maxB]) maxB = i;
            }

            return counts[maxB] > 0 ? (sums[maxB] / counts[maxB]) : (min + (maxB + 0.5) * binW);
        }

        public static double CalculateMedian(List<double> vals)
        {
            if (vals == null || vals.Count == 0) return 0.0;
            var sorted = vals.OrderBy(v => v).ToList();
            int n = sorted.Count;
            if (n % 2 == 1) return sorted[n / 2];
            return (sorted[(n / 2) - 1] + sorted[n / 2]) * 0.5;
        }

        public static HistogramData ComputeHistogramData(List<double> allVals, double? target, double tMin, double tMax)
        {
            if (allVals == null || allVals.Count == 0) return null;

            double mean = allVals.Average();
            double min = allVals.Min();
            double max = allVals.Max();
            double stdDev = 0;
            if (allVals.Count > 1)
            {
                double sumSq = allVals.Sum(v => (v - mean) * (v - mean));
                stdDev = Math.Sqrt(sumSq / (allVals.Count - 1));
            }
            double mode = CalculateMode(allVals);
            double median = CalculateMedian(allVals);

            double spanMin = min;
            double spanMax = max;
            if (target.HasValue)
            {
                spanMin = Math.Min(spanMin, tMin);
                spanMax = Math.Max(spanMax, tMax);
            }

            double span = spanMax - spanMin;
            if (span <= 1e-6) span = 1.0;

            spanMin -= span * 0.08;
            spanMax += span * 0.08;
            span = spanMax - spanMin;

            int numBins = Math.Max(8, Math.Min(22, (int)Math.Ceiling(Math.Sqrt(allVals.Count) * 1.5)));
            double binWidth = span / numBins;
            if (binWidth < 1e-4) binWidth = 0.1;

            int[] binCounts = new int[numBins];
            double[] binCenters = new double[numBins];
            for (int b = 0; b < numBins; b++)
            {
                binCenters[b] = spanMin + (b + 0.5) * binWidth;
            }

            int maxBinCount = 0;
            foreach (var v in allVals)
            {
                int bIdx = (int)Math.Floor((v - spanMin) / binWidth);
                if (bIdx < 0) bIdx = 0;
                if (bIdx >= numBins) bIdx = numBins - 1;
                binCounts[bIdx]++;
                if (binCounts[bIdx] > maxBinCount) maxBinCount = binCounts[bIdx];
            }
            if (maxBinCount == 0) maxBinCount = 1;

            // Curva KDE contínua (80 passos)
            int kdeSteps = 80;
            PointF[] kdePts = new PointF[kdeSteps];
            double h = (stdDev > 0.001) ? 1.06 * stdDev * Math.Pow(allVals.Count, -0.2) : (binWidth * 0.8);
            if (h < 0.01) h = 0.01;

            for (int k = 0; k < kdeSteps; k++)
            {
                double x = spanMin + (spanMax - spanMin) * (k / (double)(kdeSteps - 1));
                double kdeSum = 0.0;
                for (int i = 0; i < allVals.Count; i++)
                {
                    double z = (x - allVals[i]) / h;
                    kdeSum += Math.Exp(-0.5 * z * z);
                }
                double kdeDensity = kdeSum / (allVals.Count * h * Math.Sqrt(2.0 * Math.PI));
                double kdeScaledCount = kdeDensity * allVals.Count * binWidth;
                kdePts[k] = new PointF((float)x, (float)kdeScaledCount);
            }

            return new HistogramData
            {
                MinVal = spanMin,
                MaxVal = spanMax,
                BinWidth = binWidth,
                BinCounts = binCounts,
                BinCenters = binCenters,
                MaxBinCount = maxBinCount,
                KdePoints = kdePts,
                Mean = mean,
                Mode = mode,
                Median = median,
                StdDev = stdDev,
                TargetIdeal = target,
                TargetMin = tMin,
                TargetMax = tMax,
                DeltaModeTarget = target.HasValue ? (mode - target.Value) : (double?)null
            };
        }

        #region Chart Rendering GDI+

        private Bitmap RenderLineChart(List<SeriesData> series, string title, string xLabel, string yLabel,
            bool showStats, int w, int h, double minX, double maxX, double minY, double maxY,
            bool isColumns, bool combined, double? target, double tMin, double tMax, double? deltaTarget,
            HistogramData hist, List<Point3d> combMean, List<Point3d> combMode)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                int padLeft = 80;
                int padRight = 180;
                int padTop = 60;
                int padBottom = 60;

                int plotW = w - padLeft - padRight;
                int plotH = h - padTop - padBottom;

                // Fundo Geral Branco Puro Editorial
                using (var bgBrush = new SolidBrush(Color.FromArgb(248, 250, 252)))
                {
                    g.FillRectangle(bgBrush, 0, 0, w, h);
                }

                // Plot Box
                var plotRect = new Rectangle(padLeft, padTop, plotW, plotH);
                using (var plotBg = new SolidBrush(Color.White))
                using (var plotBorder = new Pen(Color.FromArgb(215, 222, 232), 1.2f))
                {
                    g.FillRectangle(plotBg, plotRect);
                    g.DrawRectangle(plotBorder, plotRect);
                }

                FontFamily fam = GetUIFontFamily();

                // Cabeçalho Principal
                using (var fontTitle = new Font(fam, 14f, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(Color.FromArgb(15, 23, 42)))
                {
                    g.DrawString(title, fontTitle, titleBrush, padLeft, 20);
                }

                // Badge Delta se Target configurado
                if (target.HasValue && deltaTarget.HasValue)
                {
                    float badgeW = 90;
                    float badgeH = 26;
                    float badgeX = padLeft + plotW - badgeW;
                    float badgeY = 18;
                    var badgeRect = new RectangleF(badgeX, badgeY, badgeW, badgeH);

                    Color badgeBg = (Math.Abs(deltaTarget.Value) <= (tMax - tMin) * 0.5)
                        ? Color.FromArgb(20, 16, 185, 129)
                        : Color.FromArgb(30, 234, 88, 12);
                    Color badgeBorder = (Math.Abs(deltaTarget.Value) <= (tMax - tMin) * 0.5)
                        ? Color.FromArgb(16, 185, 129)
                        : Color.FromArgb(234, 88, 12);
                    Color badgeTxt = (Math.Abs(deltaTarget.Value) <= (tMax - tMin) * 0.5)
                        ? Color.FromArgb(16, 140, 95)
                        : Color.FromArgb(234, 88, 12);

                    using (var bBrush = new SolidBrush(badgeBg))
                    using (var bPen = new Pen(badgeBorder, 1.2f))
                    using (var bFont = new Font(fam, 9f, FontStyle.Bold))
                    using (var bTxtBrush = new SolidBrush(badgeTxt))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.FillRectangle(bBrush, badgeRect);
                        g.DrawRectangle(bPen, badgeRect.X, badgeRect.Y, badgeRect.Width, badgeRect.Height);
                        g.DrawString($"Δ: {deltaTarget.Value:+0.00;-0.00;0.00}", bFont, bTxtBrush, badgeRect, sf);
                    }
                }

                if (isColumns && hist != null)
                {
                    // RENDERIZAÇÃO MODO COLUNAS / HISTOGRAMA COM KDE
                    double plotMinX = hist.MinVal;
                    double plotMaxX = hist.MaxVal;
                    double plotSpanX = Math.Max(1e-6, plotMaxX - plotMinX);

                    double plotMaxY = hist.MaxBinCount * 1.2;
                    double plotSpanY = Math.Max(1, plotMaxY);

                    Func<double, float> mapX = (x) => (float)(padLeft + ((x - plotMinX) / plotSpanX) * plotW);
                    Func<double, float> mapY = (y) => (float)(padTop + plotH - (y / plotSpanY) * plotH);

                    // Faixa de Tolerância (Tol)
                    if (target.HasValue)
                    {
                        float xTMin = mapX(tMin);
                        float xTMax = mapX(tMax);
                        float tolLeft = Math.Max(padLeft, Math.Min(xTMin, xTMax));
                        float tolRight = Math.Min(padLeft + plotW, Math.Max(xTMin, xTMax));

                        if (tolRight > tolLeft)
                        {
                            using (var tolBrush = new SolidBrush(Color.FromArgb(25, 16, 185, 129)))
                            using (var tolPen = new Pen(Color.FromArgb(120, 16, 185, 129), 1f) { DashStyle = DashStyle.Dash })
                            {
                                g.FillRectangle(tolBrush, tolLeft, padTop, tolRight - tolLeft, plotH);
                                g.DrawLine(tolPen, tolLeft, padTop, tolLeft, padTop + plotH);
                                g.DrawLine(tolPen, tolRight, padTop, tolRight, padTop + plotH);
                            }
                        }
                    }

                    // Grid Y
                    using (var gridPen = new Pen(Color.FromArgb(235, 240, 246), 1f))
                    using (var axisTextBrush = new SolidBrush(Color.FromArgb(100, 115, 130)))
                    using (var fontAxis = new Font(fam, 8.5f, FontStyle.Regular))
                    using (var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                    {
                        for (int i = 0; i <= 5; i++)
                        {
                            double val = (plotSpanY * i / 5.0);
                            float py = mapY(val);
                            if (py >= padTop && py <= padTop + plotH)
                            {
                                g.DrawLine(gridPen, padLeft, py, padLeft + plotW, py);
                                g.DrawString($"{val:F0}", fontAxis, axisTextBrush, padLeft - 8, py, sfRight);
                            }
                        }
                    }

                    // Grid X
                    using (var gridPen = new Pen(Color.FromArgb(235, 240, 246), 1f))
                    using (var axisTextBrush = new SolidBrush(Color.FromArgb(100, 115, 130)))
                    using (var fontAxis = new Font(fam, 8.5f, FontStyle.Regular))
                    using (var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
                    {
                        for (int i = 0; i <= 6; i++)
                        {
                            double val = plotMinX + (plotSpanX * i / 6.0);
                            float px = mapX(val);
                            if (px >= padLeft && px <= padLeft + plotW)
                            {
                                g.DrawLine(gridPen, px, padTop, px, padTop + plotH);
                                g.DrawString($"{val:F2}", fontAxis, axisTextBrush, px, padTop + plotH + 6, sfCenter);
                            }
                        }
                    }

                    // Desenhar Colunas / Barras do Histograma
                    int nBins = hist.BinCounts.Length;
                    using (var barBrush = new SolidBrush(Color.FromArgb(140, 37, 99, 235)))
                    using (var barBorder = new Pen(Color.FromArgb(37, 99, 235), 1.2f))
                    {
                        for (int b = 0; b < nBins; b++)
                        {
                            double bx0 = hist.MinVal + b * hist.BinWidth;
                            double bx1 = bx0 + hist.BinWidth;
                            float px0 = mapX(bx0);
                            float px1 = mapX(bx1);
                            float bw = Math.Max(2f, px1 - px0 - 1.5f);

                            float py = mapY(hist.BinCounts[b]);
                            float bh = (padTop + plotH) - py;

                            if (bh > 0)
                            {
                                g.FillRectangle(barBrush, px0, py, bw, bh);
                                g.DrawRectangle(barBorder, px0, py, bw, bh);
                            }
                        }
                    }

                    // Linhas de Referência Estatística (Show Stats)
                    if (showStats)
                    {
                        float pxMean = mapX(hist.Mean);
                        float pxMed = mapX(hist.Median);
                        float pxMo = mapX(hist.Mode);
                        float pxStd0 = mapX(hist.Mean - hist.StdDev);
                        float pxStd1 = mapX(hist.Mean + hist.StdDev);

                        // Faixa ±1σ vertical
                        float bandL = Math.Max(padLeft, Math.Min(pxStd0, pxStd1));
                        float bandR = Math.Min(padLeft + plotW, Math.Max(pxStd0, pxStd1));
                        if (bandR > bandL)
                        {
                            using (var bandB = new SolidBrush(Color.FromArgb(20, 37, 99, 235)))
                            using (var bandP = new Pen(Color.FromArgb(70, 37, 99, 235), 1f) { DashStyle = DashStyle.Dot })
                            {
                                g.FillRectangle(bandB, bandL, padTop, bandR - bandL, plotH);
                                g.DrawLine(bandP, bandL, padTop, bandL, padTop + plotH);
                                g.DrawLine(bandP, bandR, padTop, bandR, padTop + plotH);
                            }
                        }

                        // Linha Mediana (Med - Roxo)
                        using (var medP = new Pen(Color.FromArgb(168, 85, 247), 1.8f) { DashPattern = new float[] { 4, 2, 1, 2 } })
                        {
                            if (pxMed >= padLeft && pxMed <= padLeft + plotW)
                                g.DrawLine(medP, pxMed, padTop, pxMed, padTop + plotH);
                        }

                        // Linha Média (μ - Azul)
                        using (var meanP = new Pen(Color.FromArgb(37, 99, 235), 2.0f) { DashPattern = new float[] { 6, 3 } })
                        {
                            if (pxMean >= padLeft && pxMean <= padLeft + plotW)
                                g.DrawLine(meanP, pxMean, padTop, pxMean, padTop + plotH);
                        }

                        // Linha Moda (Mo - Laranja)
                        using (var moP = new Pen(Color.FromArgb(234, 88, 12), 2.2f) { DashStyle = DashStyle.Dot })
                        {
                            if (pxMo >= padLeft && pxMo <= padLeft + plotW)
                                g.DrawLine(moP, pxMo, padTop, pxMo, padTop + plotH);
                        }
                    }

                    // Desenhar Curva KDE Suave (se Combined Curve estiver ativo)
                    if (combined && hist.KdePoints != null && hist.KdePoints.Length > 1)
                    {
                        var kdeScr = new PointF[hist.KdePoints.Length];
                        for (int k = 0; k < hist.KdePoints.Length; k++)
                        {
                            kdeScr[k] = new PointF(mapX(hist.KdePoints[k].X), mapY(hist.KdePoints[k].Y));
                        }

                        using (var kdePen = new Pen(Color.FromArgb(0, 210, 255), 3.2f) { LineJoin = LineJoin.Round })
                        {
                            g.DrawLines(kdePen, kdeScr);
                        }
                    }

                    // Linha do Alvo (Id)
                    if (target.HasValue)
                    {
                        using (var idPen = new Pen(Color.FromArgb(16, 185, 129), 2.4f) { DashStyle = DashStyle.Dash })
                        {
                            float pxId = mapX(target.Value);
                            if (pxId >= padLeft && pxId <= padLeft + plotW)
                            {
                                g.DrawLine(idPen, pxId, padTop, pxId, padTop + plotH);
                            }
                        }
                    }

                    // Legenda na base do gráfico (idêntica à referência)
                    float bottomLegY = padTop + plotH + 28;
                    float curLegX = padLeft + 10;
                    using (var lFont = new Font(fam, 8.5f, FontStyle.Bold))
                    using (var lBrush = new SolidBrush(Color.FromArgb(51, 65, 85)))
                    {
                        if (target.HasValue)
                        {
                            using (var penId = new Pen(Color.FromArgb(16, 185, 129), 2f) { DashStyle = DashStyle.Dash })
                            {
                                g.DrawLine(penId, curLegX, bottomLegY + 6, curLegX + 18, bottomLegY + 6);
                            }
                            g.DrawString($"Id: {target.Value:F2}", lFont, lBrush, curLegX + 22, bottomLegY);
                            curLegX += 85;
                        }

                        using (var penMo = new Pen(Color.FromArgb(234, 88, 12), 2f) { DashStyle = DashStyle.Dot })
                        {
                            g.DrawLine(penMo, curLegX, bottomLegY + 6, curLegX + 18, bottomLegY + 6);
                        }
                        g.DrawString($"Mo: {hist.Mode:F2}", lFont, lBrush, curLegX + 22, bottomLegY);
                        curLegX += 85;

                        if (target.HasValue)
                        {
                            using (var tolB = new SolidBrush(Color.FromArgb(30, 16, 185, 129)))
                            using (var tolP = new Pen(Color.FromArgb(16, 185, 129), 1f))
                            {
                                g.FillRectangle(tolB, curLegX, bottomLegY + 2, 12, 10);
                                g.DrawRectangle(tolP, curLegX, bottomLegY + 2, 12, 10);
                            }
                            g.DrawString("Tol", lFont, lBrush, curLegX + 16, bottomLegY);
                            curLegX += 50;
                        }

                        // Indicador da Curva KDE
                        using (var kdeP = new Pen(Color.FromArgb(0, 210, 255), 2.2f))
                        {
                            g.DrawLine(kdeP, curLegX, bottomLegY + 6, curLegX + 18, bottomLegY + 6);
                        }
                        g.DrawString("Curva KDE", lFont, lBrush, curLegX + 22, bottomLegY);
                    }
                }
                else
                {
                    // RENDERIZAÇÃO MODO LINHAS CLÁSSICO COM SUPORTE A CURVA AGREGADA
                    double spanY = maxY - minY;
                    if (spanY <= 0) spanY = 1.0;
                    double plotMinY = minY - spanY * 0.08;
                    double plotMaxY = maxY + spanY * 0.08;
                    double plotSpanY = plotMaxY - plotMinY;

                    double spanX = maxX - minX;
                    if (spanX <= 0) spanX = 1.0;
                    double plotMinX = minX;
                    double plotMaxX = maxX;
                    double plotSpanX = plotMaxX - plotMinX;

                    Func<double, float> mapX = (x) => (float)(padLeft + ((x - plotMinX) / plotSpanX) * plotW);
                    Func<double, float> mapY = (y) => (float)(padTop + plotH - ((y - plotMinY) / plotSpanY) * plotH);

                    // Faixa de Tolerância Horizontal se Target configurado
                    if (target.HasValue)
                    {
                        float yTMin = mapY(tMin);
                        float yTMax = mapY(tMax);
                        float tolTop = Math.Max(padTop, Math.Min(yTMin, yTMax));
                        float tolBot = Math.Min(padTop + plotH, Math.Max(yTMin, yTMax));

                        if (tolBot > tolTop)
                        {
                            using (var tolBrush = new SolidBrush(Color.FromArgb(25, 16, 185, 129)))
                            using (var tolPen = new Pen(Color.FromArgb(120, 16, 185, 129), 1f) { DashStyle = DashStyle.Dash })
                            {
                                g.FillRectangle(tolBrush, padLeft, tolTop, plotW, tolBot - tolTop);
                                g.DrawLine(tolPen, padLeft, tolTop, padLeft + plotW, tolTop);
                                g.DrawLine(tolPen, padLeft, tolBot, padLeft + plotW, tolBot);
                            }
                        }

                        using (var targetPen = new Pen(Color.FromArgb(16, 185, 129), 2f) { DashStyle = DashStyle.Dash })
                        {
                            float yT = mapY(target.Value);
                            if (yT >= padTop && yT <= padTop + plotH)
                            {
                                g.DrawLine(targetPen, padLeft, yT, padLeft + plotW, yT);
                            }
                        }
                    }

                    // Grid e Ticks Y
                    using (var gridPen = new Pen(Color.FromArgb(235, 240, 246), 1f))
                    using (var axisTextBrush = new SolidBrush(Color.FromArgb(100, 115, 130)))
                    using (var fontAxis = new Font(fam, 8.5f, FontStyle.Regular))
                    using (var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
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
                    }

                    // Grid e Ticks X
                    using (var gridPen = new Pen(Color.FromArgb(235, 240, 246), 1f))
                    using (var axisTextBrush = new SolidBrush(Color.FromArgb(100, 115, 130)))
                    using (var fontAxis = new Font(fam, 8.5f, FontStyle.Regular))
                    using (var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
                    {
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

                    // Linhas de Referência Estatística em Modo Linha (Show Stats)
                    if (showStats && series.Count > 0)
                    {
                        var allYVals = series.SelectMany(s => s.Y).ToList();
                        if (allYVals.Count > 0)
                        {
                            double meanY = allYVals.Average();
                            double medianY = CalculateMedian(allYVals);
                            double modeY = CalculateMode(allYVals);
                            double stdDevY = 0;
                            if (allYVals.Count > 1)
                            {
                                double sumSq = allYVals.Sum(v => (v - meanY) * (v - meanY));
                                stdDevY = Math.Sqrt(sumSq / (allYVals.Count - 1));
                            }

                            float yMean = mapY(meanY);
                            float yMed = mapY(medianY);
                            float yMo = mapY(modeY);
                            float yStdTop = mapY(meanY + stdDevY);
                            float yStdBot = mapY(meanY - stdDevY);

                            // Faixa ±1σ sombreada
                            float bandTop = Math.Max(padTop, Math.Min(yStdTop, yStdBot));
                            float bandBot = Math.Min(padTop + plotH, Math.Max(yStdTop, yStdBot));
                            if (bandBot > bandTop)
                            {
                                using (var bandB = new SolidBrush(Color.FromArgb(18, 59, 130, 246)))
                                using (var bandP = new Pen(Color.FromArgb(70, 59, 130, 246), 1f) { DashStyle = DashStyle.Dot })
                                {
                                    g.FillRectangle(bandB, padLeft, bandTop, plotW, bandBot - bandTop);
                                    g.DrawLine(bandP, padLeft, bandTop, padLeft + plotW, bandTop);
                                    g.DrawLine(bandP, padLeft, bandBot, padLeft + plotW, bandBot);
                                }
                            }

                            // Linha Mediana (Roxo)
                            using (var medP = new Pen(Color.FromArgb(168, 85, 247), 1.8f) { DashPattern = new float[] { 4, 2, 1, 2 } })
                            {
                                if (yMed >= padTop && yMed <= padTop + plotH)
                                    g.DrawLine(medP, padLeft, yMed, padLeft + plotW, yMed);
                            }

                            // Linha Moda (Laranja)
                            using (var moP = new Pen(Color.FromArgb(234, 88, 12), 1.8f) { DashPattern = new float[] { 2, 2 } })
                            {
                                if (yMo >= padTop && yMo <= padTop + plotH)
                                    g.DrawLine(moP, padLeft, yMo, padLeft + plotW, yMo);
                            }

                            // Linha Média (Azul)
                            using (var meanP = new Pen(Color.FromArgb(37, 99, 235), 2.0f) { DashPattern = new float[] { 6, 3 } })
                            {
                                if (yMean >= padTop && yMean <= padTop + plotH)
                                    g.DrawLine(meanP, padLeft, yMean, padLeft + plotW, yMean);
                            }
                        }
                    }

                    // Desenhar Séries
                    bool isMulti = series.Count > 1;
                    int alphaSeries = combined ? (isMulti ? 40 : 80) : 255;

                    for (int sIdx = 0; sIdx < series.Count; sIdx++)
                    {
                        var s = series[sIdx];
                        Color col = Color.FromArgb(alphaSeries, s.Color.R, s.Color.G, s.Color.B);

                        var pts = new List<PointF>();
                        for (int i = 0; i < s.X.Count; i++)
                        {
                            pts.Add(new PointF(mapX(s.X[i]), mapY(s.Y[i])));
                        }

                        if (pts.Count > 1)
                        {
                            using (var linePen = new Pen(col, combined ? 1.2f : 2.2f) { LineJoin = LineJoin.Round })
                            {
                                g.DrawLines(linePen, pts.ToArray());
                            }
                        }

                        if (!combined)
                        {
                            using (var ptBrush = new SolidBrush(col))
                            using (var ptBorder = new Pen(Color.White, 1.2f))
                            {
                                for (int i = 0; i < pts.Count; i++)
                                {
                                    var p = pts[i];
                                    g.FillEllipse(ptBrush, p.X - 3f, p.Y - 3f, 6f, 6f);
                                    g.DrawEllipse(ptBorder, p.X - 3f, p.Y - 3f, 6f, 6f);
                                }
                            }
                        }
                    }

                    // Desenhar Curva Agregada / Suavizada (Média & Moda) com Destaque
                    if (combined && combMean != null && combMean.Count > 1)
                    {
                        // 1. Curva da Moda (Laranja)
                        if (combMode != null && combMode.Count == combMean.Count)
                        {
                            var modePts = combMode.Select(p => new PointF(mapX(p.X), mapY(p.Y))).ToArray();
                            using (var modePen = new Pen(Color.FromArgb(234, 88, 12), 2.2f) { LineJoin = LineJoin.Round })
                            using (var modeFill = new SolidBrush(Color.FromArgb(234, 88, 12)))
                            using (var whiteFill = new SolidBrush(Color.White))
                            {
                                g.DrawLines(modePen, modePts);
                                foreach (var p in modePts)
                                {
                                    g.FillEllipse(whiteFill, p.X - 4f, p.Y - 4f, 8f, 8f);
                                    g.FillEllipse(modeFill, p.X - 2.5f, p.Y - 2.5f, 5f, 5f);
                                }
                            }
                        }

                        // 2. Curva da Média (Ciano Vibrante com maior destaque)
                        var meanPts = combMean.Select(p => new PointF(mapX(p.X), mapY(p.Y))).ToArray();
                        using (var meanPen = new Pen(Color.FromArgb(2, 132, 199), 3.2f) { LineJoin = LineJoin.Round })
                        using (var ringPen = new Pen(Color.FromArgb(2, 132, 199), 2f))
                        using (var whiteFill = new SolidBrush(Color.White))
                        using (var centerDot = new SolidBrush(Color.FromArgb(2, 132, 199)))
                        {
                            g.DrawLines(meanPen, meanPts);
                            foreach (var p in meanPts)
                            {
                                g.FillEllipse(whiteFill, p.X - 5.5f, p.Y - 5.5f, 11f, 11f);
                                g.DrawEllipse(ringPen, p.X - 5.5f, p.Y - 5.5f, 11f, 11f);
                                g.FillEllipse(centerDot, p.X - 2.5f, p.Y - 2.5f, 5f, 5f);
                            }
                        }
                    }
                }

                // Painel de Legenda Lateral
                int legX = padLeft + plotW + 16;
                int legY = padTop + 5;
                using (var legTitleFont = new Font(fam, 9f, FontStyle.Bold))
                using (var legTitleBrush = new SolidBrush(Color.FromArgb(51, 65, 85)))
                {
                    g.DrawString("LEGENDA", legTitleFont, legTitleBrush, legX, legY);
                }

                int curY = legY + 22;
                using (var fontName = new Font(fam, 8.5f, FontStyle.Bold))
                using (var fontStat = new Font(fam, 7.5f, FontStyle.Regular))
                using (var textBrush = new SolidBrush(Color.FromArgb(51, 65, 85)))
                using (var subTextBrush = new SolidBrush(Color.FromArgb(100, 115, 130)))
                {
                    if (isColumns)
                    {
                        g.DrawString("Histograma de Dados", fontName, textBrush, legX + 16, curY);
                        curY += 16;
                        if (hist != null)
                        {
                            g.DrawString($"N = {series.SelectMany(s => s.Y).Count()} valores", fontStat, subTextBrush, legX + 16, curY);
                            curY += 15;
                            g.DrawString($"Média: {hist.Mean:F2}", fontStat, subTextBrush, legX + 16, curY);
                            curY += 14;
                            g.DrawString($"Moda: {hist.Mode:F2}", fontStat, subTextBrush, legX + 16, curY);
                            curY += 14;
                            g.DrawString($"σ: {hist.StdDev:F2}", fontStat, subTextBrush, legX + 16, curY);
                        }
                    }
                    else if (combined && series.Count > 1)
                    {
                        // Legenda sintética
                        using (var dot = new SolidBrush(Color.FromArgb(2, 132, 199)))
                        {
                            g.FillEllipse(dot, legX, curY + 2, 8, 8);
                        }
                        g.DrawString("Média Agregada", fontName, textBrush, legX + 14, curY);
                        curY += 20;

                        using (var dot = new SolidBrush(Color.FromArgb(234, 88, 12)))
                        {
                            g.FillEllipse(dot, legX, curY + 2, 8, 8);
                        }
                        g.DrawString("Moda Agregada", fontName, textBrush, legX + 14, curY);
                        curY += 20;

                        using (var pen = new Pen(Color.FromArgb(120, 140, 160), 1.5f))
                        {
                            g.DrawLine(pen, legX, curY + 6, legX + 12, curY + 6);
                        }
                        g.DrawString($"N = {series.Count} séries", fontStat, subTextBrush, legX + 14, curY);
                    }
                    else
                    {
                        for (int sIdx = 0; sIdx < Math.Min(series.Count, 6); sIdx++)
                        {
                            var s = series[sIdx];
                            using (var dotBrush = new SolidBrush(s.Color))
                            {
                                g.FillEllipse(dotBrush, legX, curY + 2, 8, 8);
                            }
                            g.DrawString(s.Name, fontName, textBrush, legX + 14, curY - 1);
                            curY += 16;
                            g.DrawString($"μ = {s.Mean:F2} | σ = {s.StdDev:F2}", fontStat, subTextBrush, legX + 14, curY);
                            curY += 16;
                        }
                    }
                }
            }

            return bmp;
        }

        #endregion

        #region Helper Series & Histogram Classes

        public class SeriesData
        {
            public GH_Path Path { get; set; }
            public string Name { get; set; }
            public Color Color { get; set; }
            public List<double> X { get; set; } = new List<double>();
            public List<double> Y { get; set; } = new List<double>();

            public double Mean { get; private set; }
            public double Median { get; private set; }
            public double Mode { get; private set; }
            public double StdDev { get; private set; }
            public double Variance { get; private set; }
            public double MinY { get; private set; }
            public double MaxY { get; private set; }

            public void ComputeStatistics()
            {
                if (Y.Count == 0) return;

                int n = Y.Count;
                Mean = Y.Average();
                MinY = Y.Min();
                MaxY = Y.Max();

                if (n > 1)
                {
                    double sumSq = Y.Sum(v => (v - Mean) * (v - Mean));
                    Variance = sumSq / (n - 1);
                    StdDev = Math.Sqrt(Variance);
                }
                else
                {
                    Variance = 0;
                    StdDev = 0;
                }

                Median = CalculateMedian(Y);
                Mode = CalculateMode(Y);
            }
        }

        #endregion
    }

    /// <summary>
    /// Atributos gráficos customizados para renderizar o painel interativo no Canvas do Grasshopper.
    /// </summary>
    public class ChartLine_Attributes : GH_ComponentAttributes
    {
        private const int GRAPH_WIDTH = 400;
        private const int GRAPH_HEIGHT = 260;
        private RectangleF m_btnExportRect;

        public ChartLine_Attributes(ChartLine_Component owner) : base(owner)
        {
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && m_btnExportRect.Contains(e.CanvasLocation))
            {
                var comp = Owner as ChartLine_Component;
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
                var comp = Owner as ChartLine_Component;
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

                FontFamily fam = ChartLine_Component.GetUIFontFamily();

                // 1. Fundo do Painel Principal Escuro
                using (var bgBrush = new SolidBrush(Color.FromArgb(20, 23, 29)))
                {
                    graphics.FillRectangle(bgBrush, graphRect);
                }
                using (var borderPen = new Pen(Color.FromArgb(65, 72, 85), 1.2f))
                {
                    graphics.DrawRectangle(borderPen, graphRect.X, graphRect.Y, graphRect.Width, graphRect.Height);
                }

                // Cabeçalho
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

                // Título no Cabeçalho
                using (var titleFont = new Font(fam, 8f, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(Color.FromArgb(240, 245, 250)))
                {
                    graphics.DrawString($"▪ {comp.DisplayTitle}", titleFont, titleBrush, headerRect.X + 8, headerRect.Y + 4);
                }

                // Botão "💾 Salvar PNG"
                float btnW = 75f;
                float btnH = 18f;
                m_btnExportRect = new RectangleF(headerRect.Right - btnW - 6, headerRect.Y + 3, btnW, btnH);

                // Badge Delta (se Target ativo) ou Badge de contagem
                if (comp.TargetValue.HasValue && comp.DeltaTarget.HasValue)
                {
                    using (var badgeFont = new Font(fam, 7f, FontStyle.Bold))
                    using (var badgeBrush = new SolidBrush(Color.FromArgb(255, 165, 0)))
                    {
                        var sfFar = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                        graphics.DrawString($"[Δ: {comp.DeltaTarget.Value:+0.00;-0.00;0.00}]", badgeFont, badgeBrush, m_btnExportRect.Left - 8, headerRect.Y + headerRect.Height * 0.5f, sfFar);
                    }
                }
                else
                {
                    using (var badgeFont = new Font(fam, 7.5f, FontStyle.Bold))
                    using (var badgeBrush = new SolidBrush(comp.IsColumnsMode ? Color.FromArgb(255, 175, 40) : Color.FromArgb(0, 220, 255)))
                    {
                        string modeText = comp.IsColumnsMode
                            ? "[Colunas / Hist (KDE)]"
                            : (comp.ShowCombinedCurve ? $"[Linhas + Tendência ({comp.DisplaySeries.Count}s)]" : $"[Linhas ({comp.DisplaySeries.Count}s)]");
                        var sfFar = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                        graphics.DrawString(modeText, badgeFont, badgeBrush, m_btnExportRect.Left - 8, headerRect.Y + headerRect.Height * 0.5f, sfFar);
                    }
                }

                using (var btnBg = new SolidBrush(Color.FromArgb(44, 52, 64)))
                using (var btnBorder = new Pen(Color.FromArgb(80, 92, 110), 1f))
                using (var btnFont = new Font(fam, 6.5f, FontStyle.Bold))
                using (var btnTextBrush = new SolidBrush(Color.FromArgb(220, 230, 242)))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    graphics.FillRectangle(btnBg, m_btnExportRect);
                    graphics.DrawRectangle(btnBorder, m_btnExportRect.X, m_btnExportRect.Y, m_btnExportRect.Width, m_btnExportRect.Height);
                    graphics.DrawString("💾 Salvar PNG", btnFont, btnTextBrush, m_btnExportRect, sf);
                }

                // 2. Área de Plotagem
                using (var plotBrush = new SolidBrush(Color.FromArgb(12, 14, 18)))
                {
                    graphics.FillRectangle(plotBrush, plotRect);
                }

                if (comp.DisplaySeries.Count == 0)
                {
                    using (var emptyFont = new Font(fam, 7.5f, FontStyle.Italic))
                    using (var emptyBrush = new SolidBrush(Color.FromArgb(130, 140, 155)))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        graphics.DrawString("Conecte os dados X e Y para visualizar.", emptyFont, emptyBrush, plotRect, sf);
                    }
                    return;
                }

                if (comp.IsColumnsMode && comp.CachedHistData != null)
                {
                    // RENDERIZAÇÃO CANVAS: MODO COLUNAS / HISTOGRAMA COM CURVA KDE (Estilo Imagem 2)
                    var hist = comp.CachedHistData;
                    double plotMinX = hist.MinVal;
                    double plotMaxX = hist.MaxVal;
                    double plotSpanX = Math.Max(1e-6, plotMaxX - plotMinX);

                    double plotMaxY = hist.MaxBinCount * 1.25;
                    double plotSpanY = Math.Max(1, plotMaxY);

                    Func<double, float> mapX = (x) => (float)(plotRect.X + ((x - plotMinX) / plotSpanX) * plotRect.Width);
                    Func<double, float> mapY = (y) => (float)(plotRect.Bottom - (y / plotSpanY) * plotRect.Height);

                    var oldClip = graphics.Clip;
                    graphics.SetClip(plotRect);

                    // Faixa de Tolerância (Tol)
                    if (comp.TargetValue.HasValue)
                    {
                        float xTMin = mapX(comp.TargetMin);
                        float xTMax = mapX(comp.TargetMax);
                        float tolLeft = Math.Max(plotRect.X, Math.Min(xTMin, xTMax));
                        float tolRight = Math.Min(plotRect.Right, Math.Max(xTMin, xTMax));

                        if (tolRight > tolLeft)
                        {
                            using (var tolBrush = new SolidBrush(Color.FromArgb(30, 16, 185, 129)))
                            using (var tolPen = new Pen(Color.FromArgb(120, 16, 185, 129), 1f) { DashStyle = DashStyle.Dash })
                            {
                                graphics.FillRectangle(tolBrush, tolLeft, plotRect.Y, tolRight - tolLeft, plotRect.Height);
                                graphics.DrawLine(tolPen, tolLeft, plotRect.Y, tolLeft, plotRect.Bottom);
                                graphics.DrawLine(tolPen, tolRight, plotRect.Y, tolRight, plotRect.Bottom);
                            }
                        }
                    }

                    // Colunas / Barras
                    int nBins = hist.BinCounts.Length;
                    using (var barBrush = new SolidBrush(Color.FromArgb(160, 30, 85, 160)))
                    using (var barBorder = new Pen(Color.FromArgb(70, 130, 220), 1f))
                    {
                        for (int binIdx = 0; binIdx < nBins; binIdx++)
                        {
                            double bx0 = hist.MinVal + binIdx * hist.BinWidth;
                            double bx1 = bx0 + hist.BinWidth;
                            float px0 = mapX(bx0);
                            float px1 = mapX(bx1);
                            float bw = Math.Max(2f, px1 - px0 - 1.2f);

                            float py = mapY(hist.BinCounts[binIdx]);
                            float bh = plotRect.Bottom - py;

                            if (bh > 0)
                            {
                                graphics.FillRectangle(barBrush, px0, py, bw, bh);
                                graphics.DrawRectangle(barBorder, px0, py, bw, bh);
                            }
                        }
                    }

                    // Linhas de Referência Estatística em Colunas (Show Stats)
                    if (comp.DisplayShowStats)
                    {
                        float pxMean = mapX(hist.Mean);
                        float pxMed = mapX(hist.Median);
                        float pxMo = mapX(hist.Mode);
                        float pxStd0 = mapX(hist.Mean - hist.StdDev);
                        float pxStd1 = mapX(hist.Mean + hist.StdDev);

                        // Faixa ±1σ vertical sombreada
                        float bandL = Math.Max(plotRect.X, Math.Min(pxStd0, pxStd1));
                        float bandR = Math.Min(plotRect.Right, Math.Max(pxStd0, pxStd1));
                        if (bandR > bandL)
                        {
                            using (var bandB = new SolidBrush(Color.FromArgb(20, 59, 130, 246)))
                            using (var bandP = new Pen(Color.FromArgb(70, 59, 130, 246), 1f) { DashStyle = DashStyle.Dot })
                            {
                                graphics.FillRectangle(bandB, bandL, plotRect.Y, bandR - bandL, plotRect.Height);
                                graphics.DrawLine(bandP, bandL, plotRect.Y, bandL, plotRect.Bottom);
                                graphics.DrawLine(bandP, bandR, plotRect.Y, bandR, plotRect.Bottom);
                            }
                        }

                        // Linha Mediana (Roxo)
                        using (var medP = new Pen(Color.FromArgb(168, 85, 247), 1.5f) { DashPattern = new float[] { 4, 2, 1, 2 } })
                        {
                            if (pxMed >= plotRect.X && pxMed <= plotRect.Right)
                                graphics.DrawLine(medP, pxMed, plotRect.Y, pxMed, plotRect.Bottom);
                        }

                        // Linha Média (Azul)
                        using (var meanP = new Pen(Color.FromArgb(37, 99, 235), 1.8f) { DashPattern = new float[] { 6, 3 } })
                        {
                            if (pxMean >= plotRect.X && pxMean <= plotRect.Right)
                                graphics.DrawLine(meanP, pxMean, plotRect.Y, pxMean, plotRect.Bottom);
                        }

                        // Linha Moda (Laranja)
                        using (var moP = new Pen(Color.FromArgb(255, 150, 20), 1.8f) { DashStyle = DashStyle.Dot })
                        {
                            if (pxMo >= plotRect.X && pxMo <= plotRect.Right)
                                graphics.DrawLine(moP, pxMo, plotRect.Y, pxMo, plotRect.Bottom);
                        }
                    }

                    // Curva KDE Suave (se Combined estiver ativo)
                    if (comp.ShowCombinedCurve && hist.KdePoints != null && hist.KdePoints.Length > 1)
                    {
                        var kdeScr = new PointF[hist.KdePoints.Length];
                        for (int k = 0; k < hist.KdePoints.Length; k++)
                        {
                            kdeScr[k] = new PointF(mapX(hist.KdePoints[k].X), mapY(hist.KdePoints[k].Y));
                        }

                        using (var kdePen = new Pen(Color.FromArgb(0, 220, 255), 2.5f) { LineJoin = LineJoin.Round })
                        {
                            graphics.DrawLines(kdePen, kdeScr);
                        }
                    }

                    // Linha do Alvo (Id)
                    if (comp.TargetValue.HasValue)
                    {
                        using (var idPen = new Pen(Color.FromArgb(16, 215, 130), 2f) { DashStyle = DashStyle.Dash })
                        {
                            float pxId = mapX(comp.TargetValue.Value);
                            if (pxId >= plotRect.X && pxId <= plotRect.Right)
                            {
                                graphics.DrawLine(idPen, pxId, plotRect.Y, pxId, plotRect.Bottom);
                            }
                        }
                    }

                    graphics.Clip = oldClip;

                    // Ticks Eixo X
                    using (var tickFont = new Font(fam, 5.8f, FontStyle.Regular))
                    using (var tickBrush = new SolidBrush(Color.FromArgb(145, 155, 170)))
                    using (var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
                    {
                        for (int i = 0; i <= 4; i++)
                        {
                            double val = plotMinX + (plotSpanX * i / 4.0);
                            float px = mapX(val);
                            graphics.DrawString(val.ToString("G3"), tickFont, tickBrush, px, plotRect.Bottom + 3, sfCenter);
                        }
                    }

                    // Rodapé com resumo
                    using (var footerFont = new Font(fam, 6.2f, FontStyle.Regular))
                    using (var footerBrush = new SolidBrush(Color.FromArgb(140, 155, 175)))
                    {
                        string info = comp.TargetValue.HasValue
                            ? $"Id: {comp.TargetValue.Value:F2} | Mo: {hist.Mode:F2} | Méd: {hist.Mean:F2} | σ: {hist.StdDev:F2} | Δ: {comp.DeltaTarget:F2}"
                            : $"Média: {hist.Mean:F2} | Mediana: {hist.Median:F2} | Moda: {hist.Mode:F2} | σ: {hist.StdDev:F2}";
                        graphics.DrawString(info, footerFont, footerBrush, footerRect.X + 8, footerRect.Y + 4);
                    }
                }
                else
                {
                    // RENDERIZAÇÃO CANVAS: MODO LINHAS COM CURVA AGREGADA
                    double yMin = comp.GlobalMinY;
                    double yMax = comp.GlobalMaxY;
                    double spanY = yMax - yMin;
                    if (spanY <= 1e-6) spanY = 1.0;
                    double plotMinY = yMin - spanY * 0.05;
                    double plotMaxY = yMax + spanY * 0.05;
                    double plotSpanY = plotMaxY - plotMinY;

                    double xMin = comp.GlobalMinX;
                    double xMax = comp.GlobalMaxX;
                    double plotSpanX = Math.Max(1e-6, xMax - xMin);

                    Func<double, float> mapX = (x) => (float)(plotRect.X + ((x - xMin) / plotSpanX) * plotRect.Width);
                    Func<double, float> mapY = (y) => (float)(plotRect.Bottom - ((y - plotMinY) / plotSpanY) * plotRect.Height);

                    var oldClip = graphics.Clip;
                    graphics.SetClip(plotRect);

                    // Faixa de Tolerância Horizontal se Target ativo
                    if (comp.TargetValue.HasValue)
                    {
                        float yTMin = mapY(comp.TargetMin);
                        float yTMax = mapY(comp.TargetMax);
                        float tolTop = Math.Max(plotRect.Y, Math.Min(yTMin, yTMax));
                        float tolBot = Math.Min(plotRect.Bottom, Math.Max(yTMin, yTMax));

                        if (tolBot > tolTop)
                        {
                            using (var tolBrush = new SolidBrush(Color.FromArgb(25, 16, 185, 129)))
                            using (var tolPen = new Pen(Color.FromArgb(120, 16, 185, 129), 1f) { DashStyle = DashStyle.Dash })
                            {
                                graphics.FillRectangle(tolBrush, plotRect.X, tolTop, plotRect.Width, tolBot - tolTop);
                                graphics.DrawLine(tolPen, plotRect.X, tolTop, plotRect.Right, tolTop);
                                graphics.DrawLine(tolPen, plotRect.X, tolBot, plotRect.Right, tolBot);
                            }
                        }

                        using (var targetPen = new Pen(Color.FromArgb(16, 185, 129), 1.8f) { DashStyle = DashStyle.Dash })
                        {
                            float yT = mapY(comp.TargetValue.Value);
                            if (yT >= plotRect.Y && yT <= plotRect.Bottom)
                            {
                                graphics.DrawLine(targetPen, plotRect.X, yT, plotRect.Right, yT);
                            }
                        }
                    }

                    // Linhas de Referência Estatística em Modo Linhas (Show Stats)
                    if (comp.DisplayShowStats && comp.DisplaySeries.Count > 0)
                    {
                        var allYVals = comp.DisplaySeries.SelectMany(s => s.Y).ToList();
                        if (allYVals.Count > 0)
                        {
                            double meanY = allYVals.Average();
                            double medianY = ChartLine_Component.CalculateMedian(allYVals);
                            double modeY = ChartLine_Component.CalculateMode(allYVals);
                            double stdDevY = 0;
                            if (allYVals.Count > 1)
                            {
                                double sumSq = allYVals.Sum(v => (v - meanY) * (v - meanY));
                                stdDevY = Math.Sqrt(sumSq / (allYVals.Count - 1));
                            }

                            float yMean = mapY(meanY);
                            float yMed = mapY(medianY);
                            float yMo = mapY(modeY);
                            float yStdTop = mapY(meanY + stdDevY);
                            float yStdBot = mapY(meanY - stdDevY);

                            // Faixa ±1σ horizontal sombreada
                            float bandTop = Math.Max(plotRect.Y, Math.Min(yStdTop, yStdBot));
                            float bandBot = Math.Min(plotRect.Bottom, Math.Max(yStdTop, yStdBot));
                            if (bandBot > bandTop)
                            {
                                using (var bandB = new SolidBrush(Color.FromArgb(16, 59, 130, 246)))
                                using (var bandP = new Pen(Color.FromArgb(70, 59, 130, 246), 1f) { DashStyle = DashStyle.Dot })
                                {
                                    graphics.FillRectangle(bandB, plotRect.X, bandTop, plotRect.Width, bandBot - bandTop);
                                    graphics.DrawLine(bandP, plotRect.X, bandTop, plotRect.Right, bandTop);
                                    graphics.DrawLine(bandP, plotRect.X, bandBot, plotRect.Right, bandBot);
                                }
                            }

                            // Linha Mediana (Roxo)
                            using (var medP = new Pen(Color.FromArgb(168, 85, 247), 1.5f) { DashPattern = new float[] { 4, 2, 1, 2 } })
                            {
                                if (yMed >= plotRect.Y && yMed <= plotRect.Bottom)
                                    graphics.DrawLine(medP, plotRect.X, yMed, plotRect.Right, yMed);
                            }

                            // Linha Moda (Laranja)
                            using (var moP = new Pen(Color.FromArgb(234, 88, 12), 1.5f) { DashPattern = new float[] { 2, 2 } })
                            {
                                if (yMo >= plotRect.Y && yMo <= plotRect.Bottom)
                                    graphics.DrawLine(moP, plotRect.X, yMo, plotRect.Right, yMo);
                            }

                            // Linha Média (Azul)
                            using (var meanP = new Pen(Color.FromArgb(37, 99, 235), 1.8f) { DashPattern = new float[] { 6, 3 } })
                            {
                                if (yMean >= plotRect.Y && yMean <= plotRect.Bottom)
                                    graphics.DrawLine(meanP, plotRect.X, yMean, plotRect.Right, yMean);
                            }
                        }
                    }

                    // Séries individuais (translúcidas se Curva Agregada ativada)
                    bool isMulti = comp.DisplaySeries.Count > 1;
                    int alpha = comp.ShowCombinedCurve ? (isMulti ? 40 : 80) : 255;

                    for (int sIdx = 0; sIdx < comp.DisplaySeries.Count; sIdx++)
                    {
                        var s = comp.DisplaySeries[sIdx];
                        Color col = Color.FromArgb(alpha, s.Color.R, s.Color.G, s.Color.B);

                        var pts = new List<PointF>();
                        for (int i = 0; i < s.X.Count; i++)
                        {
                            pts.Add(new PointF(mapX(s.X[i]), mapY(s.Y[i])));
                        }

                        if (pts.Count > 1)
                        {
                            using (var linePen = new Pen(col, comp.ShowCombinedCurve ? 1.0f : 2.0f) { LineJoin = LineJoin.Round })
                            {
                                graphics.DrawLines(linePen, pts.ToArray());
                            }
                        }

                        if (!comp.ShowCombinedCurve)
                        {
                            using (var ptBrush = new SolidBrush(col))
                            {
                                for (int i = 0; i < pts.Count; i++)
                                {
                                    var p = pts[i];
                                    graphics.FillEllipse(ptBrush, p.X - 2.5f, p.Y - 2.5f, 5f, 5f);
                                }
                            }
                        }
                    }

                    // Curva Agregada / Tendência em Linhas (Ciano + Laranja)
                    if (comp.ShowCombinedCurve && comp.CachedCombinedMeanPts != null && comp.CachedCombinedMeanPts.Count > 1)
                    {
                        // 1. Moda Agregada
                        if (comp.CachedCombinedModePts != null && comp.CachedCombinedModePts.Count == comp.CachedCombinedMeanPts.Count)
                        {
                            var modePts = comp.CachedCombinedModePts.Select(p => new PointF(mapX(p.X), mapY(p.Y))).ToArray();
                            using (var modePen = new Pen(Color.FromArgb(255, 145, 40), 2.0f) { LineJoin = LineJoin.Round })
                            using (var modeDot = new SolidBrush(Color.FromArgb(255, 145, 40)))
                            {
                                graphics.DrawLines(modePen, modePts);
                                foreach (var p in modePts)
                                {
                                    graphics.FillEllipse(modeDot, p.X - 2f, p.Y - 2f, 4f, 4f);
                                }
                            }
                        }

                        // 2. Média Agregada (Destaque Ciano)
                        var meanPts = comp.CachedCombinedMeanPts.Select(p => new PointF(mapX(p.X), mapY(p.Y))).ToArray();
                        using (var meanPen = new Pen(Color.FromArgb(0, 230, 255), 3.0f) { LineJoin = LineJoin.Round })
                        using (var ringPen = new Pen(Color.FromArgb(0, 230, 255), 1.8f))
                        using (var ringFill = new SolidBrush(Color.FromArgb(20, 23, 29)))
                        using (var dot = new SolidBrush(Color.White))
                        {
                            graphics.DrawLines(meanPen, meanPts);
                            foreach (var p in meanPts)
                            {
                                graphics.FillEllipse(ringFill, p.X - 4f, p.Y - 4f, 8f, 8f);
                                graphics.DrawEllipse(ringPen, p.X - 4f, p.Y - 4f, 8f, 8f);
                                graphics.FillEllipse(dot, p.X - 1.5f, p.Y - 1.5f, 3f, 3f);
                            }
                        }
                    }

                    graphics.Clip = oldClip;

                    // Ticks Eixo X
                    using (var tickFont = new Font(fam, 5.8f, FontStyle.Regular))
                    using (var tickBrush = new SolidBrush(Color.FromArgb(145, 155, 170)))
                    using (var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
                    {
                        for (int i = 0; i <= 5; i++)
                        {
                            double val = xMin + (plotSpanX * i / 5.0);
                            float px = mapX(val);
                            graphics.DrawString(val.ToString("G3"), tickFont, tickBrush, px, plotRect.Bottom + 3, sfCenter);
                        }
                    }

                    // Rodapé com resumo
                    using (var footerFont = new Font(fam, 6.2f, FontStyle.Regular))
                    using (var footerBrush = new SolidBrush(Color.FromArgb(140, 155, 175)))
                    {
                        if (comp.DisplaySeries.Count > 0)
                        {
                            var first = comp.DisplaySeries[0];
                            string summary = comp.DisplaySeries.Count == 1
                                ? $"Média: {first.Mean:F2} | Mediana: {first.Median:F2} | Moda: {first.Mode:F2} | σ: {first.StdDev:F2}"
                                : (comp.ShowCombinedCurve ? $"{comp.DisplaySeries.Count} curvas sintetizadas em Curva Média e Moda." : $"{comp.DisplaySeries.Count} curvas plotadas.");
                            if (comp.TargetValue.HasValue)
                            {
                                summary += $" | Alvo: {comp.TargetValue.Value:F2} (Δ: {comp.DeltaTarget:F2})";
                            }
                            graphics.DrawString(summary, footerFont, footerBrush, footerRect.X + 8, footerRect.Y + 4);
                        }
                    }
                }

                // Moldura da plotagem
                using (var plotBorderPen = new Pen(Color.FromArgb(50, 60, 75), 1.2f))
                {
                    graphics.DrawRectangle(plotBorderPen, plotRect.X, plotRect.Y, plotRect.Width, plotRect.Height);
                }
            }
        }
    }

    public class HistogramData
    {
        public double MinVal { get; set; }
        public double MaxVal { get; set; }
        public double BinWidth { get; set; }
        public int[] BinCounts { get; set; }
        public double[] BinCenters { get; set; }
        public int MaxBinCount { get; set; }
        public PointF[] KdePoints { get; set; }
        public double Mean { get; set; }
        public double Mode { get; set; }
        public double Median { get; set; }
        public double StdDev { get; set; }
        public double? TargetIdeal { get; set; }
        public double TargetMin { get; set; }
        public double TargetMax { get; set; }
        public double? DeltaModeTarget { get; set; }
    }
}
