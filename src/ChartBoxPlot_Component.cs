// ChartBoxPlot_Component.cs
// Componente para geração de Gráficos Box Plot (Box-and-Whisker plot / Dispersão Populacional)
// Renderizado diretamente no Canvas do Grasshopper com suporte a exportação PNG em alta resolução (300 DPI) com fundo branco para pranchas e publicações.
// Buraqueira Tools - Categoria "Visual"

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
    /// <summary>
    /// Dados estatísticos e valores de uma série/banda no Box Plot.
    /// </summary>
    public class BoxPlotSeriesData
    {
        public string Label { get; set; } = "";
        public GH_Path Path { get; set; } = new GH_Path(0);
        public List<double> Values { get; set; } = new List<double>();
        public Color FillColor { get; set; } = Color.FromArgb(236, 148, 154); // Coral padrão da referência

        public double Min { get; private set; }
        public double Q1 { get; private set; }
        public double Median { get; private set; }
        public double Mean { get; private set; }
        public double Q3 { get; private set; }
        public double Max { get; private set; }
        public double IQR { get; private set; }
        public double LowerFence { get; private set; }
        public double UpperFence { get; private set; }
        public double LowerWhisker { get; private set; }
        public double UpperWhisker { get; private set; }
        public double StdDev { get; private set; }
        public List<double> Outliers { get; } = new List<double>();

        public void Compute()
        {
            if (Values == null || Values.Count == 0) return;

            var sorted = Values.OrderBy(v => v).ToList();
            int n = sorted.Count;

            Min = sorted[0];
            Max = sorted[n - 1];
            Mean = sorted.Average();

            double sumSq = sorted.Sum(v => (v - Mean) * (v - Mean));
            StdDev = n > 1 ? Math.Sqrt(sumSq / (n - 1)) : 0.0;

            Q1 = Percentile(sorted, 0.25);
            Median = Percentile(sorted, 0.50);
            Q3 = Percentile(sorted, 0.75);
            IQR = Q3 - Q1;

            LowerFence = Q1 - 1.5 * IQR;
            UpperFence = Q3 + 1.5 * IQR;

            // Bigodes no estilo Tukey padrão (valor do dado mais extremo dentro dos limites dos cercas)
            LowerWhisker = sorted.FirstOrDefault(v => v >= LowerFence);
            UpperWhisker = sorted.LastOrDefault(v => v <= UpperFence);

            Outliers.Clear();
            foreach (var v in sorted)
            {
                if (v < LowerFence || v > UpperFence)
                {
                    Outliers.Add(v);
                }
            }
        }

        private static double Percentile(List<double> sorted, double p)
        {
            int n = sorted.Count;
            if (n == 0) return 0.0;
            if (n == 1) return sorted[0];

            double rank = p * (n - 1);
            int low = (int)Math.Floor(rank);
            int high = (int)Math.Ceiling(rank);
            double weight = rank - low;

            if (low == high) return sorted[low];
            return sorted[low] * (1.0 - weight) + sorted[high] * weight;
        }
    }

    /// <summary>
    /// Componente GH para visualização de Box Plot (Dispersão Populacional por Banda de Oitava / Categorias).
    /// </summary>
    public class ChartBoxPlot_Component : GH_Component
    {
        public static readonly Color DefaultCoral = Color.FromArgb(236, 148, 154); // Rosa coral da referência (Média)
        public static readonly Color DefaultSlate = Color.FromArgb(142, 158, 178); // Ardósia da referência (Variância)

        public static readonly Color[] Palette = new Color[]
        {
            Color.FromArgb(236, 148, 154), // Coral suave
            Color.FromArgb(142, 158, 178), // Ardósia azulada
            Color.FromArgb(144, 205, 171), // Verde sálvia
            Color.FromArgb(244, 187, 109), // Âmbar pastel
            Color.FromArgb(186, 153, 204), // Lilás pastel
            Color.FromArgb(120, 192, 224), // Azul céu
            Color.FromArgb(232, 131, 107), // Terracota
            Color.FromArgb(163, 177, 138)  // Oliva suave
        };

        // Cache para renderização no Canvas e Exportação
        public List<BoxPlotSeriesData> DisplaySeries = new List<BoxPlotSeriesData>();
        public string DisplayTitle = "Dispersão populacional por banda de oitava";
        public string DisplayYLabel = "Valor da média";
        public string DisplayXLabel = "Banda de oitava";
        public bool DisplayShowOutliers = true;
        public double GlobalYMin = 0.0;
        public double GlobalYMax = 1.0;

        // Exportação PNG
        public string LastExportFolder = "";
        public string LastSavedPath = "";
        public bool JustSaved = false;

        public ChartBoxPlot_Component()
            : base(
                "Box Plot Distribution",
                "ChartBoxPlot",
                "Gera gráficos Box Plot (Box-and-Whisker plot / gráfico de dispersão populacional com quartis, mediana, bigodes e outliers)\n" +
                "diretamente dentro da caixa do componente no Canvas do Grasshopper.\n" +
                "- Aceita dados numéricos por Árvore (DataTree) onde cada ramo é uma banda de oitava ou grupo independente;\n" +
                "- Calcula Min, Q1 (25%), Mediana (50%), Média, Q3 (75%), Max, IQR e pontos Outliers de forma analítica;\n" +
                "- Renderiza no Canvas com tema escuro e inclui botão '📷 Salvar PNG' para exportação em altíssima resolução com fundo branco pronta para publicação.",
                "Glaux Tools",
                "Visual")
        {
        }

        public override Guid ComponentGuid => new Guid("6a2b8b1d-3340-4f2d-a769-90008355b2e1");

        protected override Bitmap Icon => GlauxToolsIcons.ChartBoxPlot;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override void CreateAttributes()
        {
            m_attributes = new ChartBoxPlot_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0: Dados numéricos (Tree ou List)
            pManager.AddGenericParameter(
                "Data", "D",
                "Dados numéricos organizados em Árvore (DataTree) ou Lista.\n" +
                "Cada ramo da árvore representa uma coluna/banda independente no gráfico (ex: 125 Hz, 250 Hz, 500 Hz...).",
                GH_ParamAccess.tree);

            // 1: Rótulos das Categorias (X)
            pManager.AddTextParameter(
                "Labels", "L",
                "Rótulos para cada coluna/banda no eixo X (ex: '125 Hz', '250 Hz', '500 Hz'...). Se omitido, adota as frequências de oitava padrão ou caminhos do ramo.",
                GH_ParamAccess.list);
            pManager[1].Optional = true;

            // 2: Título do Gráfico
            pManager.AddTextParameter(
                "Title", "T",
                "Título principal do gráfico. Padrão: 'Dispersão populacional por banda de oitava'.",
                GH_ParamAccess.item, "Dispersão populacional por banda de oitava");
            pManager[2].Optional = true;

            // 3: Rótulo do Eixo Y
            pManager.AddTextParameter(
                "Y Label", "YLab",
                "Rótulo do eixo vertical Y (ex: 'Valor da média' ou 'Valor da variância'). Padrão: 'Valor da média'.",
                GH_ParamAccess.item, "Valor da média");
            pManager[3].Optional = true;

            // 4: Rótulo do Eixo X
            pManager.AddTextParameter(
                "X Label", "XLab",
                "Rótulo do eixo horizontal X (ex: 'Banda de oitava'). Padrão: 'Banda de oitava'.",
                GH_ParamAccess.item, "Banda de oitava");
            pManager[4].Optional = true;

            // 5: Cores das Caixas
            pManager.AddGenericParameter(
                "Box Colors", "Col",
                "Cor(es) de preenchimento das caixas do gráfico. Aceita cor única (ex: Coral '#EC949A' ou Ardósia '#8E9EB2') ou lista de cores.",
                GH_ParamAccess.list);
            pManager[5].Optional = true;

            // 6: Exibir Outliers
            pManager.AddBooleanParameter(
                "Show Outliers", "Out",
                "Exibir marcadores circulares de outliers além dos bigodes (1.5 * IQR). Padrão: true.",
                GH_ParamAccess.item, true);
            pManager[6].Optional = true;

            // 7: Pasta de Exportação
            pManager.AddTextParameter(
                "Export Folder", "Folder",
                "Pasta de destino para exportação da imagem PNG. Se vazio, adota a Área de Trabalho (Desktop).",
                GH_ParamAccess.item, "");
            pManager[7].Optional = true;

            // 8: Gatilho de Salvamento
            pManager.AddBooleanParameter(
                "Save Image", "Save",
                "Gatilho booleano para salvar imagem PNG em alta resolução (300 DPI) com fundo branco pronta para publicação.",
                GH_ParamAccess.item, false);
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter(
                "Chart Image", "Img",
                "Imagem renderizada do gráfico Box Plot (System.Drawing.Bitmap).",
                GH_ParamAccess.item);

            pManager.AddNumberParameter(
                "Statistics", "Stats",
                "Árvore com o resumo estatístico por coluna:\n" +
                "[0] Mínimo\n" +
                "[1] Q1 (25%)\n" +
                "[2] Mediana (50%)\n" +
                "[3] Média\n" +
                "[4] Q3 (75%)\n" +
                "[5] Máximo\n" +
                "[6] IQR (Q3 - Q1)\n" +
                "[7] Bigode Inferior\n" +
                "[8] Bigode Superior\n" +
                "[9] Qtd. Outliers\n" +
                "[10] Desvio Padrão",
                GH_ParamAccess.tree);

            pManager.AddNumberParameter(
                "Outliers", "Outliers",
                "Árvore contendo os valores exatos identificados como outliers em cada coluna.",
                GH_ParamAccess.tree);

            pManager.AddTextParameter(
                "Summary Table", "Table",
                "Tabela textual formatada com o resumo estatístico descritivo de cada grupo/banda.",
                GH_ParamAccess.item);

            pManager.AddTextParameter(
                "Saved PNG", "PNG",
                "Caminho absoluto do arquivo de imagem PNG salvo.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Obter Árvore de Dados
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> dataTree) || dataTree.IsEmpty)
            {
                DisplaySeries.Clear();
                return;
            }

            // 2. Parâmetros de Textos e Rótulos
            var userLabels = new List<string>();
            DA.GetDataList(1, userLabels);

            string title = "Dispersão populacional por banda de oitava";
            DA.GetData(2, ref title);
            DisplayTitle = string.IsNullOrWhiteSpace(title) ? "Dispersão populacional por banda de oitava" : title.Trim();

            string yLab = "Valor da média";
            DA.GetData(3, ref yLab);
            DisplayYLabel = string.IsNullOrWhiteSpace(yLab) ? "Valor da média" : yLab.Trim();

            string xLab = "Banda de oitava";
            DA.GetData(4, ref xLab);
            DisplayXLabel = string.IsNullOrWhiteSpace(xLab) ? "Banda de oitava" : xLab.Trim();

            // 3. Cores
            var rawColors = new List<IGH_Goo>();
            DA.GetDataList(5, rawColors);
            var parsedColors = new List<Color>();
            foreach (var goo in rawColors)
            {
                if (goo == null) continue;
                if (GH_Convert.ToColor(goo, out Color c, GH_Conversion.Both))
                {
                    parsedColors.Add(c);
                }
            }

            bool showOutliers = true;
            DA.GetData(6, ref showOutliers);
            DisplayShowOutliers = showOutliers;

            string folder = "";
            DA.GetData(7, ref folder);
            LastExportFolder = folder;

            bool saveTrigger = false;
            DA.GetData(8, ref saveTrigger);

            // 4. Padrões de Rótulos de Frequência para Acústica
            string[] standardOctaves6 = new string[] { "125 Hz", "250 Hz", "500 Hz", "1000 Hz", "2000 Hz", "4000 Hz" };
            string[] standardOctaves8 = new string[] { "63 Hz", "125 Hz", "250 Hz", "500 Hz", "1000 Hz", "2000 Hz", "4000 Hz", "8000 Hz" };

            var seriesList = new List<BoxPlotSeriesData>();
            int branchIndex = 0;
            int totalBranches = dataTree.Paths.Count;

            double globalMin = double.MaxValue;
            double globalMax = double.MinValue;

            foreach (var path in dataTree.Paths)
            {
                var branch = dataTree.get_Branch(path);
                if (branch == null || branch.Count == 0) continue;

                var series = new BoxPlotSeriesData
                {
                    Path = path
                };

                // Determinar Rótulo
                if (userLabels != null && branchIndex < userLabels.Count && !string.IsNullOrWhiteSpace(userLabels[branchIndex]))
                {
                    series.Label = userLabels[branchIndex].Trim();
                }
                else if (totalBranches == 6 && branchIndex < standardOctaves6.Length)
                {
                    series.Label = standardOctaves6[branchIndex];
                }
                else if (totalBranches == 8 && branchIndex < standardOctaves8.Length)
                {
                    series.Label = standardOctaves8[branchIndex];
                }
                else
                {
                    series.Label = $"Banda {branchIndex + 1}";
                }

                // Determinar Cor
                if (parsedColors.Count > 0)
                {
                    series.FillColor = parsedColors[branchIndex % parsedColors.Count];
                }
                else
                {
                    // Se não informado, adota o tom Coral suave padrão (ou Ardósia se o rótulo indicar variância)
                    if (series.Label.IndexOf("var", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        DisplayYLabel.IndexOf("var", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        series.FillColor = DefaultSlate;
                    }
                    else
                    {
                        series.FillColor = DefaultCoral;
                    }
                }

                // Extrair valores numéricos
                foreach (var item in branch)
                {
                    if (item == null) continue;
                    if (GH_Convert.ToDouble(item, out double val, GH_Conversion.Both))
                    {
                        if (!double.IsNaN(val) && !double.IsInfinity(val))
                        {
                            series.Values.Add(val);
                        }
                    }
                }

                if (series.Values.Count > 0)
                {
                    series.Compute();
                    seriesList.Add(series);

                    // Escala global
                    if (series.Min < globalMin) globalMin = series.Min;
                    if (series.Max > globalMax) globalMax = series.Max;
                }

                branchIndex++;
            }

            if (seriesList.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum dado numérico válido encontrado nas árvores de entrada.");
                DisplaySeries.Clear();
                return;
            }

            if (globalMin == double.MaxValue) globalMin = 0.0;
            if (globalMax == double.MinValue) globalMax = 1.0;
            if (Math.Abs(globalMax - globalMin) < 1e-9)
            {
                globalMax += 1.0;
                globalMin -= 1.0;
            }

            // Margem vertical de 5% para conforto visual
            double ySpan = globalMax - globalMin;
            GlobalYMin = globalMin - ySpan * 0.05;
            GlobalYMax = globalMax + ySpan * 0.05;

            DisplaySeries = seriesList;

            // 5. Construir Saídas GH
            var statsTree = new GH_Structure<GH_Number>();
            var outliersTree = new GH_Structure<GH_Number>();
            var sbSummary = new StringBuilder();

            sbSummary.AppendLine("==========================================================================================");
            sbSummary.AppendLine($"  {DisplayTitle.ToUpper()} - RESUMO BOX PLOT");
            sbSummary.AppendLine("==========================================================================================");
            sbSummary.AppendLine(string.Format("{0,-12} | {1,6} | {2,8} | {3,8} | {4,8} | {5,8} | {6,8} | {7,8} | {8,8}",
                "Banda/Grupo", "N", "Mínimo", "Q1 (25%)", "Mediana", "Média", "Q3 (75%)", "Máximo", "Outliers"));
            sbSummary.AppendLine(new string('-', 90));

            for (int i = 0; i < seriesList.Count; i++)
            {
                var s = seriesList[i];
                var p = s.Path;

                // [0] Min, [1] Q1, [2] Median, [3] Mean, [4] Q3, [5] Max, [6] IQR, [7] LowerWhisker, [8] UpperWhisker, [9] OutliersCount, [10] StdDev
                statsTree.Append(new GH_Number(s.Min), p);
                statsTree.Append(new GH_Number(s.Q1), p);
                statsTree.Append(new GH_Number(s.Median), p);
                statsTree.Append(new GH_Number(s.Mean), p);
                statsTree.Append(new GH_Number(s.Q3), p);
                statsTree.Append(new GH_Number(s.Max), p);
                statsTree.Append(new GH_Number(s.IQR), p);
                statsTree.Append(new GH_Number(s.LowerWhisker), p);
                statsTree.Append(new GH_Number(s.UpperWhisker), p);
                statsTree.Append(new GH_Number(s.Outliers.Count), p);
                statsTree.Append(new GH_Number(s.StdDev), p);

                foreach (var outVal in s.Outliers)
                {
                    outliersTree.Append(new GH_Number(outVal), p);
                }

                sbSummary.AppendLine(string.Format("{0,-12} | {1,6} | {2,8:F3} | {3,8:F3} | {4,8:F3} | {5,8:F3} | {6,8:F3} | {7,8:F3} | {8,8}",
                    s.Label, s.Values.Count, s.Min, s.Q1, s.Median, s.Mean, s.Q3, s.Max, s.Outliers.Count));
            }

            sbSummary.AppendLine("==========================================================================================");

            DA.SetDataTree(1, statsTree);
            DA.SetDataTree(2, outliersTree);
            DA.SetData(3, sbSummary.ToString());

            // 6. Renderizar Bitmap de saída e salvar se gatilho ativo
            Bitmap exportBmp = RenderWhiteBackgroundBitmap(1200, 600);
            DA.SetData(0, exportBmp);

            string savedPath = "";
            if (saveTrigger)
            {
                savedPath = ExportImageWhiteBackground(folder, out string err);
                if (!string.IsNullOrEmpty(savedPath))
                {
                    LastSavedPath = savedPath;
                    JustSaved = true;
                    Message = "PNG Salvo!";
                }
                else if (!string.IsNullOrEmpty(err))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Erro ao salvar PNG: {err}");
                }
            }

            DA.SetData(4, LastSavedPath);
        }

        /// <summary>
        /// Gera a imagem de alta definição com fundo branco, linhas finas e padrão editorial idêntico à referência.
        /// </summary>
        public Bitmap RenderWhiteBackgroundBitmap(int width, int height)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                // Fundo Branco Puro
                g.Clear(Color.White);

                float padLeft = 85f;
                float padRight = 35f;
                float padTop = 55f;
                float padBottom = 65f;

                RectangleF plotRect = new RectangleF(
                    padLeft,
                    padTop,
                    width - padLeft - padRight,
                    height - padTop - padBottom);

                // 1. Título do Gráfico (Bold, centralizado)
                using (var titleFont = new Font("Arial", 14f, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(Color.Black))
                {
                    var sfTitle = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(DisplayTitle, titleFont, titleBrush, plotRect.Left + plotRect.Width * 0.5f, 28f, sfTitle);
                }

                // 2. Grid Sutil e Eixo Y
                int yTicksCount = 6;
                double yMin = GlobalYMin;
                double yMax = GlobalYMax;
                double yStep = (yMax - yMin) / yTicksCount;

                using (var gridPen = new Pen(Color.FromArgb(232, 235, 238), 1f) { DashStyle = DashStyle.Dash })
                using (var tickPen = new Pen(Color.FromArgb(60, 60, 60), 1f))
                using (var axisFont = new Font("Arial", 9.5f, FontStyle.Regular))
                using (var axisBrush = new SolidBrush(Color.FromArgb(40, 40, 40)))
                {
                    var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

                    for (int i = 0; i <= yTicksCount; i++)
                    {
                        double val = yMin + i * yStep;
                        float yPix = plotRect.Bottom - (float)((val - yMin) / (yMax - yMin)) * plotRect.Height;

                        // Grid horizontal
                        g.DrawLine(gridPen, plotRect.Left, yPix, plotRect.Right, yPix);

                        // Marcador do tick
                        g.DrawLine(tickPen, plotRect.Left - 5f, yPix, plotRect.Left, yPix);

                        // Label do tick
                        string valStr = Math.Abs(val) < 0.01 ? "0.0" : (val >= 10 ? $"{val:F1}" : $"{val:F2}");
                        g.DrawString(valStr, axisFont, axisBrush, plotRect.Left - 8f, yPix, sfRight);
                    }
                }

                // 3. Grid Vertical nas categorias
                int numSeries = DisplaySeries.Count;
                if (numSeries > 0)
                {
                    float slotWidth = plotRect.Width / numSeries;
                    using (var vGridPen = new Pen(Color.FromArgb(240, 242, 245), 1f) { DashStyle = DashStyle.Dash })
                    {
                        for (int i = 0; i < numSeries; i++)
                        {
                            float cx = plotRect.Left + (i + 0.5f) * slotWidth;
                            g.DrawLine(vGridPen, cx, plotRect.Top, cx, plotRect.Bottom);
                        }
                    }
                }

                // 4. Moldura externa retangular do gráfico
                using (var framePen = new Pen(Color.FromArgb(50, 50, 50), 1.2f))
                {
                    g.DrawRectangle(framePen, plotRect.X, plotRect.Y, plotRect.Width, plotRect.Height);
                }

                // 5. Desenhar Cada Caixa (Box Plot)
                if (numSeries > 0)
                {
                    float slotWidth = plotRect.Width / numSeries;
                    float boxWidth = Math.Max(26f, Math.Min(65f, slotWidth * 0.50f));
                    float capWidth = boxWidth * 0.45f;

                    using (var labelFont = new Font("Arial", 9.5f, FontStyle.Regular))
                    using (var textBrush = new SolidBrush(Color.Black))
                    using (var whiskerPen = new Pen(Color.Black, 1.3f))
                    using (var medianPen = new Pen(Color.Black, 2.2f))
                    using (var boxBorderPen = new Pen(Color.Black, 1.3f))
                    using (var outlierPen = new Pen(Color.FromArgb(20, 20, 20), 1.1f))
                    using (var outlierBrush = new SolidBrush(Color.White))
                    {
                        var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };

                        for (int i = 0; i < numSeries; i++)
                        {
                            var s = DisplaySeries[i];
                            float cx = plotRect.Left + (i + 0.5f) * slotWidth;

                            // Label X (Banda)
                            g.DrawString(s.Label, labelFont, textBrush, cx, plotRect.Bottom + 8f, sfCenter);

                            if (s.Values.Count == 0) continue;

                            float yQ1 = plotRect.Bottom - (float)((s.Q1 - yMin) / (yMax - yMin)) * plotRect.Height;
                            float yQ3 = plotRect.Bottom - (float)((s.Q3 - yMin) / (yMax - yMin)) * plotRect.Height;
                            float yMed = plotRect.Bottom - (float)((s.Median - yMin) / (yMax - yMin)) * plotRect.Height;
                            float yLowW = plotRect.Bottom - (float)((s.LowerWhisker - yMin) / (yMax - yMin)) * plotRect.Height;
                            float yUppW = plotRect.Bottom - (float)((s.UpperWhisker - yMin) / (yMax - yMin)) * plotRect.Height;

                            // Bigode Inferior (da base da caixa até o bigode inferior)
                            g.DrawLine(whiskerPen, cx, yQ1, cx, yLowW);
                            g.DrawLine(whiskerPen, cx - capWidth * 0.5f, yLowW, cx + capWidth * 0.5f, yLowW);

                            // Bigode Superior (do topo da caixa até o bigode superior)
                            g.DrawLine(whiskerPen, cx, yQ3, cx, yUppW);
                            g.DrawLine(whiskerPen, cx - capWidth * 0.5f, yUppW, cx + capWidth * 0.5f, yUppW);

                            // Caixa Interquartil (Q1 a Q3)
                            float boxTop = Math.Min(yQ1, yQ3);
                            float boxH = Math.Max(2f, Math.Abs(yQ1 - yQ3));
                            var boxRect = new RectangleF(cx - boxWidth * 0.5f, boxTop, boxWidth, boxH);

                            using (var fillBrush = new SolidBrush(s.FillColor))
                            {
                                g.FillRectangle(fillBrush, boxRect);
                            }
                            g.DrawRectangle(boxBorderPen, boxRect.X, boxRect.Y, boxRect.Width, boxRect.Height);

                            // Linha da Mediana
                            g.DrawLine(medianPen, cx - boxWidth * 0.5f, yMed, cx + boxWidth * 0.5f, yMed);

                            // Outliers
                            if (DisplayShowOutliers && s.Outliers.Count > 0)
                            {
                                float outRadius = 3.2f;
                                foreach (var outVal in s.Outliers)
                                {
                                    float yOut = plotRect.Bottom - (float)((outVal - yMin) / (yMax - yMin)) * plotRect.Height;
                                    var outRect = new RectangleF(cx - outRadius, yOut - outRadius, outRadius * 2f, outRadius * 2f);
                                    g.FillEllipse(outlierBrush, outRect);
                                    g.DrawEllipse(outlierPen, outRect);
                                }
                            }
                        }
                    }
                }

                // 6. Rótulos dos Eixos (X e Y)
                using (var axisTitleFont = new Font("Arial", 10.5f, FontStyle.Regular))
                using (var textBrush = new SolidBrush(Color.Black))
                {
                    // Eixo X
                    var sfX = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(DisplayXLabel, axisTitleFont, textBrush, plotRect.Left + plotRect.Width * 0.5f, height - 20f, sfX);

                    // Eixo Y (Rotacionado -90°)
                    var sfY = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    var state = g.Save();
                    g.TranslateTransform(24f, plotRect.Top + plotRect.Height * 0.5f);
                    g.RotateTransform(-90f);
                    g.DrawString(DisplayYLabel, axisTitleFont, textBrush, 0, 0, sfY);
                    g.Restore(state);
                }
            }

            return bmp;
        }

        /// <summary>
        /// Exporta a imagem PNG com fundo branco para o disco em alta resolução.
        /// </summary>
        public string ExportImageWhiteBackground(string targetFolder, out string error)
        {
            error = "";
            try
            {
                if (string.IsNullOrWhiteSpace(targetFolder) || !Directory.Exists(targetFolder))
                {
                    targetFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"BoxPlot_{timestamp}.png";
                string fullPath = Path.Combine(targetFolder, fileName);

                using (var bmp = RenderWhiteBackgroundBitmap(1400, 700))
                {
                    bmp.Save(fullPath, ImageFormat.Png);
                }

                return fullPath;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }
    }

    /// <summary>
    /// Atributos visuais para renderização interativa do Box Plot dentro do Canvas do Grasshopper no tema escuro oficial.
    /// </summary>
    public class ChartBoxPlot_Attributes : GH_ComponentAttributes
    {
        private const int GRAPH_WIDTH = 440;
        private const int GRAPH_HEIGHT = 280;
        private RectangleF m_btnExportRect;

        public ChartBoxPlot_Attributes(ChartBoxPlot_Component owner) : base(owner)
        {
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && m_btnExportRect.Contains(e.CanvasLocation))
            {
                var comp = Owner as ChartBoxPlot_Component;
                if (comp != null)
                {
                    string saved = comp.ExportImageWhiteBackground(comp.LastExportFolder, out string err);
                    if (!string.IsNullOrEmpty(saved))
                    {
                        comp.LastSavedPath = saved;
                        comp.JustSaved = true;
                        comp.Message = "PNG Salvo!";
                        Rhino.RhinoApp.WriteLine($"[ChartBoxPlot] Gráfico Box Plot salvo com fundo branco em: {saved}");
                        sender.Refresh();
                    }
                    else if (!string.IsNullOrEmpty(err))
                    {
                        comp.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Erro ao exportar PNG: {err}");
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

            // Alinha os parâmetros de saída na borda direita expandida
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
                var comp = Owner as ChartBoxPlot_Component;
                if (comp == null) return;

                RectangleF b = Bounds;
                RectangleF graphRect = new RectangleF(b.X + 12, b.Bottom - GRAPH_HEIGHT - 10, b.Width - 24, GRAPH_HEIGHT);
                RectangleF headerRect = new RectangleF(graphRect.X, graphRect.Y, graphRect.Width, 26);
                RectangleF footerRect = new RectangleF(graphRect.X, graphRect.Bottom - 24, graphRect.Width, 24);

                float plotTop = headerRect.Bottom + 14;
                float plotBottom = footerRect.Y - 26;
                float plotHeight = Math.Max(80, plotBottom - plotTop);

                RectangleF plotRect = new RectangleF(
                    graphRect.X + 56,
                    plotTop,
                    graphRect.Width - 72,
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

                // Header e Footer
                using (var headerBrush = new SolidBrush(Color.FromArgb(30, 34, 43)))
                using (var footerBrush = new SolidBrush(Color.FromArgb(24, 27, 34)))
                using (var linePen = new Pen(Color.FromArgb(50, 56, 68), 1f))
                {
                    graphics.FillRectangle(headerBrush, headerRect);
                    graphics.FillRectangle(footerBrush, footerRect);
                    graphics.DrawLine(linePen, headerRect.X, headerRect.Bottom, headerRect.Right, headerRect.Bottom);
                    graphics.DrawLine(linePen, footerRect.X, footerRect.Y, footerRect.Right, footerRect.Y);
                }

                // 2. Área do Gráfico (Fundo escuro profundo)
                using (var plotBrush = new SolidBrush(Color.FromArgb(12, 14, 18)))
                {
                    graphics.FillRectangle(plotBrush, plotRect);
                }
                using (var plotBorderPen = new Pen(Color.FromArgb(55, 62, 75), 1f))
                {
                    graphics.DrawRectangle(plotBorderPen, plotRect.X, plotRect.Y, plotRect.Width, plotRect.Height);
                }

                // 3. Cabeçalho (Título e Botão Salvar PNG)
                using (var titleFont = new Font(GH_FontServer.Standard.FontFamily, 8.5f, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(Color.FromArgb(240, 243, 248)))
                {
                    var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                    string displayTitle = comp.DisplayTitle;
                    if (displayTitle.Length > 36) displayTitle = displayTitle.Substring(0, 33) + "...";
                    graphics.DrawString(displayTitle, titleFont, titleBrush, headerRect.X + 10, headerRect.Y + headerRect.Height * 0.5f, sfLeft);
                }

                // Botão "📷 Salvar PNG"
                float btnW = 90f;
                float btnH = 18f;
                m_btnExportRect = new RectangleF(headerRect.Right - btnW - 8, headerRect.Y + 4, btnW, btnH);

                using (var btnBrush = new SolidBrush(Color.FromArgb(40, 48, 62)))
                using (var btnPen = new Pen(Color.FromArgb(80, 92, 110), 1f))
                using (var btnFont = new Font(GH_FontServer.Standard.FontFamily, 7.5f, FontStyle.Bold))
                using (var btnTextBrush = new SolidBrush(Color.FromArgb(180, 210, 245)))
                {
                    graphics.FillRectangle(btnBrush, m_btnExportRect);
                    graphics.DrawRectangle(btnPen, m_btnExportRect.X, m_btnExportRect.Y, m_btnExportRect.Width, m_btnExportRect.Height);
                    var sfBtn = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    graphics.DrawString("📷 Salvar PNG", btnFont, btnTextBrush, m_btnExportRect, sfBtn);
                }

                // 4. Desenhar Eixo Y e Ticks
                double yMin = comp.GlobalYMin;
                double yMax = comp.GlobalYMax;
                int yTicksCount = 4;
                double yStep = (yMax - yMin) / yTicksCount;

                using (var gridPen = new Pen(Color.FromArgb(35, 42, 52), 1f) { DashStyle = DashStyle.Dash })
                using (var axisFont = new Font(GH_FontServer.Standard.FontFamily, 7f, FontStyle.Regular))
                using (var axisBrush = new SolidBrush(Color.FromArgb(160, 170, 185)))
                {
                    var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    for (int i = 0; i <= yTicksCount; i++)
                    {
                        double val = yMin + i * yStep;
                        float yPix = plotRect.Bottom - (float)((val - yMin) / (yMax - yMin)) * plotRect.Height;

                        graphics.DrawLine(gridPen, plotRect.Left, yPix, plotRect.Right, yPix);
                        string valStr = Math.Abs(val) < 0.01 ? "0.0" : (val >= 10 ? $"{val:F1}" : $"{val:F2}");
                        graphics.DrawString(valStr, axisFont, axisBrush, plotRect.Left - 5f, yPix, sfRight);
                    }
                }

                // 5. Desenhar Caixas do Box Plot
                int numSeries = comp.DisplaySeries.Count;
                if (numSeries > 0)
                {
                    float slotWidth = plotRect.Width / numSeries;
                    float boxWidth = Math.Max(16f, Math.Min(42f, slotWidth * 0.52f));
                    float capWidth = boxWidth * 0.45f;

                    using (var labelFont = new Font(GH_FontServer.Standard.FontFamily, 7f, FontStyle.Regular))
                    using (var textBrush = new SolidBrush(Color.FromArgb(200, 210, 225)))
                    using (var whiskerPen = new Pen(Color.FromArgb(210, 220, 235), 1.1f))
                    using (var medianPen = new Pen(Color.White, 1.8f))
                    using (var boxBorderPen = new Pen(Color.FromArgb(240, 245, 255), 1.1f))
                    using (var outlierPen = new Pen(Color.FromArgb(220, 230, 245), 1f))
                    using (var outlierBrush = new SolidBrush(Color.FromArgb(20, 25, 35)))
                    {
                        var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };

                        for (int i = 0; i < numSeries; i++)
                        {
                            var s = comp.DisplaySeries[i];
                            float cx = plotRect.Left + (i + 0.5f) * slotWidth;

                            // Label X
                            string shortLabel = s.Label;
                            if (shortLabel.Length > 8) shortLabel = shortLabel.Substring(0, 7) + ".";
                            graphics.DrawString(shortLabel, labelFont, textBrush, cx, plotRect.Bottom + 4f, sfCenter);

                            if (s.Values.Count == 0) continue;

                            float yQ1 = plotRect.Bottom - (float)((s.Q1 - yMin) / (yMax - yMin)) * plotRect.Height;
                            float yQ3 = plotRect.Bottom - (float)((s.Q3 - yMin) / (yMax - yMin)) * plotRect.Height;
                            float yMed = plotRect.Bottom - (float)((s.Median - yMin) / (yMax - yMin)) * plotRect.Height;
                            float yLowW = plotRect.Bottom - (float)((s.LowerWhisker - yMin) / (yMax - yMin)) * plotRect.Height;
                            float yUppW = plotRect.Bottom - (float)((s.UpperWhisker - yMin) / (yMax - yMin)) * plotRect.Height;

                            // Bigode Inferior
                            graphics.DrawLine(whiskerPen, cx, yQ1, cx, yLowW);
                            graphics.DrawLine(whiskerPen, cx - capWidth * 0.5f, yLowW, cx + capWidth * 0.5f, yLowW);

                            // Bigode Superior
                            graphics.DrawLine(whiskerPen, cx, yQ3, cx, yUppW);
                            graphics.DrawLine(whiskerPen, cx - capWidth * 0.5f, yUppW, cx + capWidth * 0.5f, yUppW);

                            // Caixa
                            float boxTop = Math.Min(yQ1, yQ3);
                            float boxH = Math.Max(2f, Math.Abs(yQ1 - yQ3));
                            var boxRect = new RectangleF(cx - boxWidth * 0.5f, boxTop, boxWidth, boxH);

                            using (var fillBrush = new SolidBrush(s.FillColor))
                            {
                                graphics.FillRectangle(fillBrush, boxRect);
                            }
                            graphics.DrawRectangle(boxBorderPen, boxRect.X, boxRect.Y, boxRect.Width, boxRect.Height);

                            // Linha da Mediana
                            graphics.DrawLine(medianPen, cx - boxWidth * 0.5f, yMed, cx + boxWidth * 0.5f, yMed);

                            // Outliers
                            if (comp.DisplayShowOutliers && s.Outliers.Count > 0)
                            {
                                float outRadius = 2.2f;
                                foreach (var outVal in s.Outliers)
                                {
                                    float yOut = plotRect.Bottom - (float)((outVal - yMin) / (yMax - yMin)) * plotRect.Height;
                                    var outRect = new RectangleF(cx - outRadius, yOut - outRadius, outRadius * 2f, outRadius * 2f);
                                    graphics.FillEllipse(outlierBrush, outRect);
                                    graphics.DrawEllipse(outlierPen, outRect);
                                }
                            }
                        }
                    }
                }

                // 6. Rodapé com Informações Rápidas
                using (var footFont = new Font(GH_FontServer.Standard.FontFamily, 7f, FontStyle.Regular))
                using (var footBrush = new SolidBrush(Color.FromArgb(130, 140, 155)))
                {
                    var sfFoot = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    string infoStr = $"{numSeries} Bandas/Categorias | Y: [{yMin:F2} a {yMax:F2}] | Clique em '📷 Salvar PNG' para alta resolução";
                    graphics.DrawString(infoStr, footFont, footBrush, footerRect.X + footerRect.Width * 0.5f, footerRect.Y + footerRect.Height * 0.5f, sfFoot);
                }
            }
        }
    }
}
