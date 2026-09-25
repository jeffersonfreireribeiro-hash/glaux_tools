using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Componente Gráfico Interativo no Canvas para Funções de Perda (Loss Functions).
    /// Renderiza a curva analítica contínua (Huber, Quantile, MAE, MSE, Hinge, BCE),
    /// a dispersão dos pontos de dados reais (resíduos) sobre a curva de custo,
    /// a região de tolerância a outliers, e a derivada/gradiente para otimização.
    /// </summary>
    public class ChartLoss_Component : GH_Component
    {
        public LossType SelectedLossType = LossType.Huber;
        public double UserParam = 1.0;
        public string DisplayTitle = "Loss Function & Residual Distribution";
        public double MeanLoss = 0.0;
        public int OutlierCount = 0;
        public int SampleCount = 0;

        // Cache dos pontos reais para renderização
        public List<double> CachedResiduals = new List<double>();
        public List<double> CachedPointwiseLoss = new List<double>();
        public List<double> CachedGradients = new List<double>();

        public Bitmap CachedHiResBmp = null;

        public ChartLoss_Component()
            : base(
                "Loss Curve Visualizer",
                "ChartLoss",
                "Renderiza no Canvas do Grasshopper o gráfico interativo de Funções de Perda (Loss Functions):\n" +
                "  - Curva teórica contínua (Huber, Quantile, MAE, MSE, Hinge, BCE)\n" +
                "  - Dispersão das amostras reais (Scatter Dots) plotadas sobre a curva\n" +
                "  - Zonas de transição quadrática vs linear e amortecimento de outliers\n" +
                "  - Gradiente/derivada e exportação de imagem PNG em alta definição.",
                "Glaux Tools",
                "Visual")
        {
        }

        public override Guid ComponentGuid => new Guid("b8c9d0e1-f2a3-4b5c-6d7e-8f9a0b1c2d3e");

        protected override Bitmap Icon => GlauxToolsIcons.ChartLoss;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override void CreateAttributes()
        {
            m_attributes = new ChartLoss_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0: Y_pred
            pManager.AddNumberParameter(
                "Predicted", "Y_pred",
                "Valores preditos ou simulados ŷ (conecte saídas do modelo ou genes de otimização).",
                GH_ParamAccess.list);

            // 1: Y_true
            pManager.AddNumberParameter(
                "Target / True", "Y_true",
                "Valores reais medidos em campo ou metas de projeto y (Ground Truth).",
                GH_ParamAccess.list);

            // 2: Tipo de Perda
            pManager.AddGenericParameter(
                "Loss Type", "Type",
                "0: Huber Loss (Smooth MAE)\n1: Quantile Loss (Pinball)\n2: MAE (L1)\n3: MSE (L2)\n4: Hinge Loss (SVM)\n5: Binary Cross-Entropy\nPadrão: 0 (Huber Loss). Alternável também via clique direito.",
                GH_ParamAccess.item);
            pManager[2].Optional = true;

            // 3: Parâmetro (Delta ou Quantil)
            pManager.AddNumberParameter(
                "Parameter", "Param",
                "Parâmetro adicional: Delta δ para Huber (padrão: 1.0) ou Quantil q/τ para Quantile Loss (padrão: 0.50).",
                GH_ParamAccess.item, 1.0);
            pManager[3].Optional = true;

            // 4: Título
            pManager.AddTextParameter(
                "Title", "T",
                "Título principal exibido no cabeçalho do gráfico.",
                GH_ParamAccess.item, "Loss Function & Residual Distribution");
            pManager[4].Optional = true;

            // 5: Largura da Imagem Exportada
            pManager.AddIntegerParameter(
                "Width", "W",
                "Largura em pixels da imagem bitmap exportada (padrão: 900 px).",
                GH_ParamAccess.item, 900);
            pManager[5].Optional = true;

            // 6: Altura da Imagem Exportada
            pManager.AddIntegerParameter(
                "Height", "H",
                "Altura em pixels da imagem bitmap exportada (padrão: 550 px).",
                GH_ParamAccess.item, 550);
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // 0: Total Loss
            pManager.AddNumberParameter(
                "Total Loss", "Loss",
                "Perda média calculada (Mean Loss).",
                GH_ParamAccess.item);

            // 1: Imagem Renderizada
            pManager.AddGenericParameter(
                "Chart Image", "Img",
                "Bitmap do gráfico renderizado em alta definição (System.Drawing.Bitmap).",
                GH_ParamAccess.item);

            // 2: Perdas Pontuais
            pManager.AddNumberParameter(
                "Pointwise Loss", "L_i",
                "Lista com o valor da perda de cada amostra individual.",
                GH_ParamAccess.list);

            // 3: Resíduos
            pManager.AddNumberParameter(
                "Residuals", "Res",
                "Resíduos brutos individuais (y_i - ŷ_i).",
                GH_ParamAccess.list);

            // 4: Gradientes
            pManager.AddNumberParameter(
                "Gradients", "Grad",
                "Lista de gradientes / derivadas parciais ∂L/∂ŷ_i.",
                GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rawPred = new List<double>();
            var rawTrue = new List<double>();

            if (!DA.GetDataList(0, rawPred) || rawPred == null || rawPred.Count == 0)
            {
                this.Message = "Sem Y_pred";
                ClearCache();
                return;
            }
            if (!DA.GetDataList(1, rawTrue) || rawTrue == null || rawTrue.Count == 0)
            {
                this.Message = "Sem Y_true";
                ClearCache();
                return;
            }

            if (rawPred.Count != rawTrue.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Tamanhos diferentes: Y_pred ({rawPred.Count}) != Y_true ({rawTrue.Count}).");
                this.Message = "Tamanhos ≠";
                ClearCache();
                return;
            }

            // Tipo de Perda
            object rawType = null;
            LossType lossType = SelectedLossType;
            if (DA.GetData(2, ref rawType) && rawType != null)
            {
                lossType = LossFunctions_Component.ParseLossType(rawType, SelectedLossType);
            }
            SelectedLossType = lossType;

            // Parâmetro
            double param = (lossType == LossType.Quantile) ? 0.5 : 1.0;
            DA.GetData(3, ref param);
            if (lossType == LossType.Huber && param <= 0.0) param = 1.0;
            if (lossType == LossType.Quantile) param = Math.Max(0.01, Math.Min(0.99, param));
            UserParam = param;

            string title = "Loss Function & Residual Distribution";
            DA.GetData(4, ref title);
            DisplayTitle = title;

            int imgW = 900;
            int imgH = 550;
            DA.GetData(5, ref imgW);
            DA.GetData(6, ref imgH);
            if (imgW < 300) imgW = 300;
            if (imgH < 200) imgH = 200;

            // Filtrar dados válidos
            int total = rawPred.Count;
            var cleanPred = new List<double>();
            var cleanTrue = new List<double>();

            for (int i = 0; i < total; i++)
            {
                double p = rawPred[i];
                double t = rawTrue[i];
                if (!double.IsNaN(p) && !double.IsInfinity(p) && !double.IsNaN(t) && !double.IsInfinity(t))
                {
                    cleanPred.Add(p);
                    cleanTrue.Add(t);
                }
            }

            int n = cleanPred.Count;
            SampleCount = n;
            if (n == 0)
            {
                this.Message = "Sem Dados";
                ClearCache();
                return;
            }

            // Calcular Perdas e Resíduos
            var pointwiseLoss = new List<double>(n);
            var residuals = new List<double>(n);
            var gradients = new List<double>(n);

            double sumLoss = 0.0;
            int outliers = 0;

            for (int i = 0; i < n; i++)
            {
                double yHat = cleanPred[i];
                double y = cleanTrue[i];
                double e = y - yHat;
                double absE = Math.Abs(e);

                residuals.Add(e);

                double l_i = 0.0;
                double grad_i = 0.0;

                switch (lossType)
                {
                    case LossType.Huber:
                        {
                            double delta = param;
                            if (absE <= delta)
                            {
                                l_i = 0.5 * e * e;
                                grad_i = yHat - y;
                            }
                            else
                            {
                                l_i = delta * (absE - 0.5 * delta);
                                grad_i = delta * (e > 0 ? -1.0 : 1.0);
                                outliers++;
                            }
                            break;
                        }
                    case LossType.Quantile:
                        {
                            double q = param;
                            if (e >= 0.0)
                            {
                                l_i = q * e;
                                grad_i = -q;
                            }
                            else
                            {
                                l_i = (1.0 - q) * (-e);
                                grad_i = 1.0 - q;
                            }
                            break;
                        }
                    case LossType.MAE:
                        {
                            l_i = absE;
                            grad_i = (e > 0) ? -1.0 : (e < 0 ? 1.0 : 0.0);
                            break;
                        }
                    case LossType.MSE:
                        {
                            l_i = e * e;
                            grad_i = 2.0 * (yHat - y);
                            break;
                        }
                    case LossType.Hinge:
                        {
                            double label = (y <= 0.0) ? -1.0 : 1.0;
                            double margin = label * yHat;
                            if (margin < 1.0)
                            {
                                l_i = 1.0 - margin;
                                grad_i = -label;
                            }
                            break;
                        }
                    case LossType.BinaryCrossEntropy:
                        {
                            double eps = 1e-15;
                            double yHatC = Math.Max(eps, Math.Min(1.0 - eps, yHat));
                            double label = Math.Max(0.0, Math.Min(1.0, y));
                            l_i = -(label * Math.Log(yHatC) + (1.0 - label) * Math.Log(1.0 - yHatC));
                            grad_i = (yHatC - label) / (yHatC * (1.0 - yHatC));
                            break;
                        }
                }

                pointwiseLoss.Add(l_i);
                gradients.Add(grad_i);
                sumLoss += l_i;
            }

            MeanLoss = sumLoss / n;
            OutlierCount = outliers;

            CachedResiduals = residuals;
            CachedPointwiseLoss = pointwiseLoss;
            CachedGradients = gradients;

            // Gerar Imagem em Alta Resolução
            CachedHiResBmp = RenderChartBitmap(imgW, imgH);

            DA.SetData(0, MeanLoss);
            if (CachedHiResBmp != null)
            {
                DA.SetData(1, new GH_ObjectWrapper(CachedHiResBmp));
            }
            DA.SetDataList(2, pointwiseLoss);
            DA.SetDataList(3, residuals);
            DA.SetDataList(4, gradients);

            this.Message = $"{LossFunctions_Component.GetShortName(lossType)}: {MeanLoss:G4}";
        }

        private void ClearCache()
        {
            CachedResiduals.Clear();
            CachedPointwiseLoss.Clear();
            CachedGradients.Clear();
            CachedHiResBmp = null;
            MeanLoss = 0.0;
            OutlierCount = 0;
            SampleCount = 0;
        }

        #region Renderizador do Gráfico em Bitmap (GDI+)

        public Bitmap RenderChartBitmap(int width, int height)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                var fullRect = new RectangleF(0, 0, width, height);
                RenderChartGraphics(g, fullRect, true);
            }
            return bmp;
        }

        public void RenderChartGraphics(Graphics g, RectangleF bounds, bool hiRes)
        {
            // 1. Fundo do Painel Escuro Sofisticado
            using (var bgBrush = new SolidBrush(Color.FromArgb(20, 23, 29)))
            {
                g.FillRectangle(bgBrush, bounds);
            }

            float padL = hiRes ? 65 : 45;
            float padR = hiRes ? 35 : 20;
            float padT = hiRes ? 60 : 42;
            float padB = hiRes ? 55 : 35;

            var plotRect = new RectangleF(
                bounds.X + padL,
                bounds.Y + padT,
                Math.Max(50, bounds.Width - padL - padR),
                Math.Max(50, bounds.Height - padT - padB));

            // 2. Cabeçalho / Badges
            using (var titleFont = new Font("Segoe UI", hiRes ? 12f : 9f, FontStyle.Bold))
            using (var badgeFont = new Font("Segoe UI", hiRes ? 9f : 7f, FontStyle.Regular))
            using (var whiteBrush = new SolidBrush(Color.FromArgb(235, 240, 250)))
            using (var mutedBrush = new SolidBrush(Color.FromArgb(145, 155, 175)))
            {
                string titleStr = string.IsNullOrEmpty(DisplayTitle) ? "Loss Function & Residual Distribution" : DisplayTitle;
                g.DrawString(titleStr, titleFont, whiteBrush, bounds.X + (hiRes ? 20 : 12), bounds.Y + (hiRes ? 14 : 8));

                string typeStr = LossFunctions_Component.GetLossTypeName(SelectedLossType);
                string badgeStr = $"{typeStr} | Loss: {MeanLoss:G5} | N: {SampleCount}";
                if (SelectedLossType == LossType.Huber)
                {
                    badgeStr += $" | Outliers (> δ): {OutlierCount} ({(SampleCount > 0 ? (double)OutlierCount / SampleCount * 100.0 : 0.0):F1}%)";
                }
                g.DrawString(badgeStr, badgeFont, mutedBrush, bounds.X + (hiRes ? 20 : 12), bounds.Y + (hiRes ? 36 : 24));
            }

            // 3. Determinar Limites dos Eixos (X = Erro / Resíduo, Y = Perda)
            double minX = -3.0;
            double maxX = 3.0;
            double maxY = 4.0;

            if (CachedResiduals.Count > 0)
            {
                double dataMin = CachedResiduals.Min();
                double dataMax = CachedResiduals.Max();
                double maxAbs = Math.Max(Math.Abs(dataMin), Math.Abs(dataMax));
                if (maxAbs > 0.1)
                {
                    minX = -maxAbs * 1.25;
                    maxX = maxAbs * 1.25;
                }
            }

            if (SelectedLossType == LossType.BinaryCrossEntropy)
            {
                minX = 0.0;
                maxX = 1.0;
                maxY = 4.0;
            }
            else if (SelectedLossType == LossType.Hinge)
            {
                minX = Math.Min(-3.0, minX);
                maxX = Math.Max(3.0, maxX);
                maxY = Math.Max(4.0, 1.0 - minX);
            }
            else
            {
                // Para Huber, MSE, MAE, calcularmaxY na borda
                maxY = EvaluateTheoreticalLoss(SelectedLossType, maxX, UserParam);
                if (CachedPointwiseLoss.Count > 0)
                {
                    maxY = Math.Max(maxY, CachedPointwiseLoss.Max() * 1.15);
                }
                if (maxY < 1.0) maxY = 1.0;
            }

            // 4. Fundo da Área de Plotagem
            using (var plotBg = new SolidBrush(Color.FromArgb(14, 16, 21)))
            using (var plotBorder = new Pen(Color.FromArgb(45, 52, 65), 1f))
            {
                g.FillRectangle(plotBg, plotRect);
                g.DrawRectangle(plotBorder, plotRect.X, plotRect.Y, plotRect.Width, plotRect.Height);
            }

            // 5. Grid e Eixos
            using (var gridPen = new Pen(Color.FromArgb(30, 36, 48), 1f) { DashStyle = DashStyle.Dash })
            using (var axisPen = new Pen(Color.FromArgb(80, 92, 112), 1.2f))
            using (var font = new Font("Segoe UI", hiRes ? 8f : 6.5f))
            using (var textBrush = new SolidBrush(Color.FromArgb(130, 140, 160)))
            {
                // Eixo Zero X (linha vertical central)
                if (minX <= 0 && maxX >= 0)
                {
                    float zeroX = plotRect.X + (float)((0.0 - minX) / (maxX - minX) * plotRect.Width);
                    g.DrawLine(axisPen, zeroX, plotRect.Y, zeroX, plotRect.Bottom);
                    g.DrawString("0", font, textBrush, zeroX - 4, plotRect.Bottom + 4);
                }

                // Linha Zero Y (base)
                float zeroY = plotRect.Bottom;
                g.DrawLine(axisPen, plotRect.X, zeroY, plotRect.Right, zeroY);

                // Grid Horizontal (4 divisões)
                for (int step = 1; step <= 4; step++)
                {
                    double valY = maxY * (step / 4.0);
                    float yPix = plotRect.Bottom - (float)((valY / maxY) * plotRect.Height);
                    g.DrawLine(gridPen, plotRect.X, yPix, plotRect.Right, yPix);
                    g.DrawString($"{valY:G3}", font, textBrush, plotRect.X - (hiRes ? 45 : 32), yPix - 6);
                }

                // Grid Vertical
                int xSteps = 6;
                for (int step = 0; step <= xSteps; step++)
                {
                    double valX = minX + (maxX - minX) * (step / (double)xSteps);
                    float xPix = plotRect.X + (float)((valX - minX) / (maxX - minX) * plotRect.Width);
                    g.DrawLine(gridPen, xPix, plotRect.Y, xPix, plotRect.Bottom);
                    g.DrawString($"{valX:G3}", font, textBrush, xPix - 10, plotRect.Bottom + 4);
                }

                // Rótulos dos eixos
                string xLabel = (SelectedLossType == LossType.BinaryCrossEntropy) ? "Probabilidade Prevista ŷ" :
                                (SelectedLossType == LossType.Hinge) ? "Margem / Produto (y · ŷ)" : "Erro Residual (y - ŷ)";
                g.DrawString(xLabel, font, textBrush, plotRect.X + plotRect.Width / 2 - 40, plotRect.Bottom + (hiRes ? 22 : 16));
            }

            // 6. Destaque de Zonas Especiais (Ex: Zona Quadrática do Huber Loss)
            if (SelectedLossType == LossType.Huber)
            {
                double delta = UserParam;
                float xL = plotRect.X + (float)((-delta - minX) / (maxX - minX) * plotRect.Width);
                float xR = plotRect.X + (float)((delta - minX) / (maxX - minX) * plotRect.Width);

                if (xR > plotRect.X && xL < plotRect.Right)
                {
                    float clampL = Math.Max(plotRect.X, xL);
                    float clampR = Math.Min(plotRect.Right, xR);

                    // Faixa sombreada da zona quadrática
                    using (var zoneBrush = new SolidBrush(Color.FromArgb(28, 46, 204, 113)))
                    {
                        g.FillRectangle(zoneBrush, clampL, plotRect.Y, clampR - clampL, plotRect.Height);
                    }

                    // Linhas verticais pontilhadas em ±δ
                    using (var deltaPen = new Pen(Color.FromArgb(46, 204, 113), 1.2f) { DashStyle = DashStyle.Dot })
                    using (var deltaFont = new Font("Segoe UI", hiRes ? 8f : 6f, FontStyle.Bold))
                    using (var deltaBrush = new SolidBrush(Color.FromArgb(46, 204, 113)))
                    {
                        if (xL >= plotRect.X && xL <= plotRect.Right)
                        {
                            g.DrawLine(deltaPen, xL, plotRect.Y, xL, plotRect.Bottom);
                            g.DrawString("-δ", deltaFont, deltaBrush, xL - 10, plotRect.Y + 8);
                        }
                        if (xR >= plotRect.X && xR <= plotRect.Right)
                        {
                            g.DrawLine(deltaPen, xR, plotRect.Y, xR, plotRect.Bottom);
                            g.DrawString("+δ", deltaFont, deltaBrush, xR + 3, plotRect.Y + 8);
                        }
                    }
                }
            }
            else if (SelectedLossType == LossType.Hinge)
            {
                // Linha de fronteira em z = 1.0
                float xBoundary = plotRect.X + (float)((1.0 - minX) / (maxX - minX) * plotRect.Width);
                if (xBoundary >= plotRect.X && xBoundary <= plotRect.Right)
                {
                    using (var bPen = new Pen(Color.FromArgb(255, 215, 40), 1.2f) { DashStyle = DashStyle.Dash })
                    using (var bFont = new Font("Segoe UI", hiRes ? 8f : 6f, FontStyle.Bold))
                    using (var bBrush = new SolidBrush(Color.FromArgb(255, 215, 40)))
                    {
                        g.DrawLine(bPen, xBoundary, plotRect.Y, xBoundary, plotRect.Bottom);
                        g.DrawString("Fronteira (z=1)", bFont, bBrush, xBoundary + 4, plotRect.Y + 10);
                    }
                }
            }

            // 7. Curva Teórica Contínua da Função de Perda
            int curveSteps = 150;
            var curvePoints = new List<PointF>();
            for (int i = 0; i <= curveSteps; i++)
            {
                double xVal = minX + (maxX - minX) * (i / (double)curveSteps);
                double yVal = EvaluateTheoreticalLoss(SelectedLossType, xVal, UserParam);

                float xPix = plotRect.X + (float)((xVal - minX) / (maxX - minX) * plotRect.Width);
                float yPix = plotRect.Bottom - (float)((Math.Min(yVal, maxY * 1.5) / maxY) * plotRect.Height);
                curvePoints.Add(new PointF(xPix, yPix));
            }

            if (curvePoints.Count > 1)
            {
                using (var curvePen = new Pen(Color.FromArgb(0, 220, 255), hiRes ? 2.5f : 1.8f))
                {
                    g.DrawLines(curvePen, curvePoints.ToArray());
                }
            }

            // 8. Scatter Dots: Dispersão dos Dados Reais do Usuário sobre a Curva
            if (CachedResiduals.Count > 0 && CachedPointwiseLoss.Count > 0)
            {
                using (var dotInlier = new SolidBrush(Color.FromArgb(220, 50, 225, 120)))
                using (var dotOutlier = new SolidBrush(Color.FromArgb(235, 255, 80, 80)))
                using (var dotBorder = new Pen(Color.White, 0.7f))
                {
                    float dotRadius = hiRes ? 4.0f : 2.8f;
                    int count = Math.Min(CachedResiduals.Count, CachedPointwiseLoss.Count);

                    for (int i = 0; i < count; i++)
                    {
                        double e = CachedResiduals[i];
                        double l = CachedPointwiseLoss[i];

                        float px = plotRect.X + (float)((e - minX) / (maxX - minX) * plotRect.Width);
                        float py = plotRect.Bottom - (float)((l / maxY) * plotRect.Height);

                        if (px >= plotRect.X && px <= plotRect.Right && py >= plotRect.Y && py <= plotRect.Bottom)
                        {
                            bool isOutlier = (SelectedLossType == LossType.Huber && Math.Abs(e) > UserParam);
                            var b = isOutlier ? dotOutlier : dotInlier;

                            g.FillEllipse(b, px - dotRadius, py - dotRadius, dotRadius * 2, dotRadius * 2);
                            g.DrawEllipse(dotBorder, px - dotRadius, py - dotRadius, dotRadius * 2, dotRadius * 2);
                        }
                    }
                }
            }
        }

        public static double EvaluateTheoreticalLoss(LossType type, double x, double param)
        {
            double absX = Math.Abs(x);
            switch (type)
            {
                case LossType.Huber:
                    double delta = param;
                    return (absX <= delta) ? 0.5 * x * x : delta * (absX - 0.5 * delta);

                case LossType.Quantile:
                    double q = param;
                    return (x >= 0.0) ? q * x : (1.0 - q) * (-x);

                case LossType.MAE:
                    return absX;

                case LossType.MSE:
                    return x * x;

                case LossType.Hinge:
                    return Math.Max(0.0, 1.0 - x);

                case LossType.BinaryCrossEntropy:
                    double eps = 1e-15;
                    double p = Math.Max(eps, Math.Min(1.0 - eps, x));
                    return -Math.Log(p); // Curva para y=1
            }
            return 0.0;
        }

        public string SavePngDialog()
        {
            try
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "PNG Image (*.png)|*.png";
                    sfd.FileName = $"LossChart_{LossFunctions_Component.GetShortName(SelectedLossType)}.png";
                    sfd.Title = "Salvar Gráfico da Função de Perda (PNG)";

                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        var bmp = RenderChartBitmap(1200, 750);
                        bmp.Save(sfd.FileName, ImageFormat.Png);
                        return sfd.FileName;
                    }
                }
            }
            catch { }
            return null;
        }

        #endregion

        #region Menu de Contexto e Serialização

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var lossMenu = new ToolStripMenuItem("Selecionar Função de Perda (Loss):");

            var items = new (LossType type, string label)[]
            {
                (LossType.Huber, "0: Huber Loss (Smooth MAE)"),
                (LossType.Quantile, "1: Quantile Loss (Pinball Loss)"),
                (LossType.MAE, "2: Mean Absolute Error (MAE / L1)"),
                (LossType.MSE, "3: Mean Squared Error (MSE / L2)"),
                (LossType.Hinge, "4: Hinge Loss (SVM / Margem)"),
                (LossType.BinaryCrossEntropy, "5: Binary Cross-Entropy (Log Loss)")
            };

            foreach (var item in items)
            {
                var menuItem = new ToolStripMenuItem(item.label)
                {
                    Checked = (SelectedLossType == item.type)
                };
                var targetType = item.type;
                menuItem.Click += (s, e) =>
                {
                    RecordUndoEvent("Mudar Função de Perda Gráfica");
                    SelectedLossType = targetType;
                    ExpireSolution(true);
                };
                lossMenu.DropDownItems.Add(menuItem);
            }

            menu.Items.Add(lossMenu);

            var exportItem = new ToolStripMenuItem("Salvar Imagem PNG em Alta Resolução...");
            exportItem.Click += (s, e) => SavePngDialog();
            menu.Items.Add(exportItem);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetInt32("SelectedLossType", (int)SelectedLossType);
            writer.SetDouble("UserParam", UserParam);
            writer.SetString("DisplayTitle", DisplayTitle ?? "");
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("SelectedLossType"))
            {
                SelectedLossType = (LossType)reader.GetInt32("SelectedLossType");
            }
            if (reader.ItemExists("UserParam"))
            {
                UserParam = reader.GetDouble("UserParam");
            }
            if (reader.ItemExists("DisplayTitle"))
            {
                DisplayTitle = reader.GetString("DisplayTitle");
            }
            return base.Read(reader);
        }

        #endregion
    }

    /// <summary>
    /// Atributos visuais que renderizam o gráfico de perda interativo diretamente no Canvas do Grasshopper.
    /// </summary>
    public class ChartLoss_Attributes : GH_ComponentAttributes
    {
        private const int GRAPH_WIDTH = 420;
        private const int GRAPH_HEIGHT = 270;
        private RectangleF m_btnExportRect;

        public ChartLoss_Attributes(ChartLoss_Component owner) : base(owner)
        {
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && m_btnExportRect.Contains(e.CanvasLocation))
            {
                var comp = Owner as ChartLoss_Component;
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
            b.Height += GRAPH_HEIGHT + 20;
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
                var comp = Owner as ChartLoss_Component;
                if (comp == null) return;

                RectangleF b = Bounds;
                RectangleF graphRect = new RectangleF(b.X + 12, b.Bottom - GRAPH_HEIGHT - 10, b.Width - 24, GRAPH_HEIGHT);

                // Renderiza o gráfico completo no canvas
                comp.RenderChartGraphics(graphics, graphRect, false);

                // Botão de Exportação no rodapé direito
                float btnW = 110;
                float btnH = 20;
                m_btnExportRect = new RectangleF(graphRect.Right - btnW - 8, graphRect.Bottom - btnH - 6, btnW, btnH);

                using (var btnBg = new SolidBrush(Color.FromArgb(41, 128, 185)))
                using (var btnBorder = new Pen(Color.FromArgb(52, 152, 219), 1f))
                using (var btnFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.White))
                {
                    graphics.FillRectangle(btnBg, m_btnExportRect);
                    graphics.DrawRectangle(btnBorder, m_btnExportRect.X, m_btnExportRect.Y, m_btnExportRect.Width, m_btnExportRect.Height);

                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    graphics.DrawString("Exportar PNG", btnFont, textBrush, m_btnExportRect, sf);
                }
            }
        }
    }
}
