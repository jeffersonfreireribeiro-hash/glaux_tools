using GH_IO.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Componente no estilo "Pill" que diagrama e exporta pranchas técnicas vetoriais (SVG e PDF)
    /// a partir de curvas e geometrias do Grasshopper.
    /// Suporta templates SVG com carimbo customizado, detecção de área de desenho (drawing_area),
    /// presets de auto-distribuição (Hero, Split, Quad, Grid 3x2), escalas gráficas paramétricas,
    /// norte dinâmico e botão interativo para pré-visualização instantânea no navegador.
    /// </summary>
    public class PillVectorSheetLayout_Component : GH_Component
    {
        // ==========================================
        // PROPRIEDADES E CONFIGURAÇÕES
        // ==========================================
        public string ActiveTemplate = "A3_Quad";
        public string DistributionMode = "Auto"; // "Auto", "Fit", "Split_H", "Split_V", "Master_2", "Quad", "Grid_3x2", "Explicit"
        public double GutterMm = 10.0;
        public string ProjectTitle = "Projeto Glaux";
        public string AuthorName = "Arquiteto / Engenheiro";
        public string SheetNumber = "01/01";
        public string GeneralScale = "Indicada";
        public bool AutoOpenBrowser = false;

        // Cache da Prancha Gerada
        public string LastGeneratedSvg { get; private set; } = string.Empty;
        public string LastSvgPath { get; private set; } = string.Empty;
        public string LastPdfPath { get; private set; } = string.Empty;
        public List<RectangleF> ComputedViewports { get; private set; } = new List<RectangleF>();
        public Color CategoryColor => Color.FromArgb(14, 165, 233); // Cyan Glaux Visual

        // Triggers de Ação Manual
        private bool _forcePreview = false;
        private bool _forceExport = false;

        public PillVectorSheetLayout_Component()
            : base(
                "Pill Vector Sheet Layout",
                "PillSheet",
                "Diagrama e exporta pranchas técnicas vetoriais (SVG e PDF) a partir de curvas e geometrias do Grasshopper. Suporta templates SVG com carimbo customizado, detecção de área de desenho (drawing_area), presets de auto-distribuição (Hero, Split, Quad, Grid 3x2), escalas gráficas paramétricas, norte e botão interativo para pré-visualização instantânea no navegador.",
                "Glaux Tools",
                "Visual")
        {
        }

        public override Guid ComponentGuid => new Guid("b7110015-e1ef-4000-8000-000000000016");

        protected override Bitmap Icon => GlauxToolsIcons.PillVectorSheetLayout;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override void CreateAttributes()
        {
            m_attributes = new PillVectorSheetLayout_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter(
                "Curves / Geometry", "C",
                "Curvas ou geometrias 2D/3D a diagramar na prancha (Curve, Line, Polyline, etc.). Use uma DataTree onde cada ramo {0}, {1}, etc., corresponde a uma vista diferente. Se uma lista simples for fornecida, distribui conforme o layout.",
                GH_ParamAccess.tree);

            pManager.AddTextParameter(
                "Template / Preset", "T",
                "Modelo de prancha SVG. Aceita o nome de um preset nativo ('A3_Hero', 'A3_Split_H', 'A3_Split_V', 'A3_Master2', 'A3_Quad', 'A3_Grid3x2', 'A4_Portrait', 'A4_Landscape') ou o caminho de um arquivo .svg customizado com carimbo próprio.",
                GH_ParamAccess.item,
                "A3_Quad");

            pManager.AddTextParameter(
                "Distribution", "Dist",
                "Modo de distribuição das vistas na área útil de desenho: 'Auto' (escolhe pelo número de vistas), 'Fit' (1 vista 100%), 'Split_H' (2 lado a lado), 'Split_V' (2 sobrepostas), 'Master_2' (1 mestre 65% + 2 detalhes), 'Quad' (4 em 2x2), 'Grid_3x2' (6 em 3x2), ou 'Explicit' (respeita os IDs view_0, view_1... do SVG).",
                GH_ParamAccess.item,
                "Auto");

            pManager.AddTextParameter(
                "Names / Titles", "Names",
                "Títulos editoriais para cada vista (ex.: ['01 | PLANTA BAIXA', '02 | CORTE AA', '03 | ISOMÉTRICA 3D', '04 | VISTA FRONTAL']).",
                GH_ParamAccess.list);
            pManager[3].Optional = true;

            pManager.AddTextParameter(
                "Scales", "S",
                "Escala técnica de cada vista (ex.: ['1:50', '1:100', 'Fit']). 'Fit' ajusta ao quadro com margem de respiro; valores numéricos como '1:50' desenham em escala física real com base nas unidades do modelo.",
                GH_ParamAccess.list);
            pManager[4].Optional = true;

            pManager.AddGenericParameter(
                "North Direction", "N",
                "Direção ou ângulo do Norte para desenhar a Rosa dos Ventos / Seta de Norte nas vistas. Aceita Vector3d (ex.: (0,1,0)) ou valor numérico em graus (0° = X, 90° = Y).",
                GH_ParamAccess.item);
            pManager[5].Optional = true;

            pManager.AddTextParameter(
                "Data / Tags", "D",
                "Metadados do carimbo no formato chave-valor (ex.: 'PROJETO: Teatro Escola', 'AUTOR: Jefferson', 'DATA: 26/09/2026', 'FOLHA: 01/01', 'CLIENTE: PMB'). Substitui automaticamente as tags {{CHAVE}} no SVG.",
                GH_ParamAccess.list);
            pManager[6].Optional = true;

            pManager.AddBooleanParameter(
                "Export", "On",
                "Quando True, grava os arquivos SVG e PDF vetoriais no disco no caminho especificado.",
                GH_ParamAccess.item,
                false);

            pManager.AddTextParameter(
                "File Path", "Path",
                "Caminho do arquivo ou diretório de destino para gravação do SVG e PDF. Se omitido, salva na pasta temporária ou ao lado do arquivo do Rhino.",
                GH_ParamAccess.item,
                "");
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("PDF File", "PDF", "Caminho do arquivo PDF vetorial gerado.", GH_ParamAccess.item);
            pManager.AddTextParameter("SVG File", "SVG", "Caminho do arquivo SVG vetorial gerado.", GH_ParamAccess.item);
            pManager.AddTextParameter("SVG Code", "Code", "Código-fonte XML do SVG completo da prancha gerada.", GH_ParamAccess.item);
            pManager.AddRectangleParameter("Viewports", "V", "Retângulos dos enquadramentos calculados na prancha em milímetros.", GH_ParamAccess.list);
            pManager.AddTextParameter("Status / Log", "L", "Resumo das vistas diagramadas, escalas aplicadas e status de exportação.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Obter Geometrias de Entrada (Tree ou List)
            if (!DA.GetDataTree(0, out GH_Structure<IGH_GeometricGoo> geomTree)) return;

            string templateInput = "A3_Quad";
            DA.GetData(1, ref templateInput);
            ActiveTemplate = templateInput;

            string distMode = "Auto";
            DA.GetData(2, ref distMode);
            DistributionMode = distMode;

            List<string> names = new List<string>();
            DA.GetDataList(3, names);

            List<string> scales = new List<string>();
            DA.GetDataList(4, scales);

            object northObj = null;
            DA.GetData(5, ref northObj);
            double northAngleDeg = ParseNorthAngle(northObj);

            List<string> tagList = new List<string>();
            DA.GetDataList(6, tagList);

            bool exportActive = false;
            DA.GetData(7, ref exportActive);

            string customPath = "";
            DA.GetData(8, ref customPath);

            // Ação disparada por botão
            if (_forceExport)
            {
                exportActive = true;
                _forceExport = false;
            }

            // 2. Organizar Geometrias por Vista (Ramos da Árvore)
            List<List<Curve>> viewCurves = ExtractCurvesByView(geomTree);
            int numViews = Math.Max(1, viewCurves.Count);

            // 3. Carregar Template SVG Base (Nativo ou Customizado)
            SheetTemplateDef sheetDef = LoadSheetTemplate(ActiveTemplate);

            // 4. Mapear Substituições do Carimbo (Tags)
            var tagDict = ParseTags(tagList);
            ApplyDefaultTags(tagDict, numViews);

            // 5. Determinar Modo de Distribuição
            string effectiveDist = ResolveDistributionMode(DistributionMode, numViews);

            // 6. Subdividir Área de Desenho em Viewports
            List<ViewportLayout> viewports = SubdivideDrawingArea(sheetDef, effectiveDist, numViews);
            ComputedViewports.Clear();
            foreach (var vp in viewports)
            {
                ComputedViewports.Add(new RectangleF(vp.Box.X, vp.Box.Y, vp.Box.Width, vp.Box.Height));
            }

            // 7. Renderizar Geometria e Rodapé de Cada Viewport no SVG
            StringBuilder svgContent = new StringBuilder(sheetDef.SvgXml);

            // Substituir Tags do Carimbo no Texto
            foreach (var kvp in tagDict)
            {
                svgContent.Replace("{{" + kvp.Key + "}}", kvp.Value);
            }

            // Injetar Elementos de Cada Vista no SVG
            StringBuilder viewportsGroup = new StringBuilder();
            viewportsGroup.AppendLine("<g id=\"glaux_viewports_layer\">");

            List<string> statusLog = new List<string>();
            statusLog.Add($"Prancha: {sheetDef.Name} ({sheetDef.WidthMm:0} x {sheetDef.HeightMm:0} mm)");
            statusLog.Add($"Distribuição: {effectiveDist} ({numViews} vistas)");

            for (int i = 0; i < viewports.Count; i++)
            {
                var vp = viewports[i];
                List<Curve> curves = (i < viewCurves.Count) ? viewCurves[i] : new List<Curve>();

                string viewTitle = (i < names.Count && !string.IsNullOrWhiteSpace(names[i])) ? names[i] : $"0{i + 1} | VISTA {i + 1}";
                string scaleSpec = (i < scales.Count && !string.IsNullOrWhiteSpace(scales[i])) ? scales[i] : "Fit";

                // Gerar SVG do Viewport (Curvas, ClipPath, Escala Gráfica, Seta Norte)
                string vpSvg = RenderViewportSvg(vp, curves, viewTitle, scaleSpec, northAngleDeg, i, out string resolvedScaleText);
                viewportsGroup.AppendLine(vpSvg);

                statusLog.Add($"  [Vista {i + 1}] {viewTitle} -> Escala: {resolvedScaleText} ({curves.Count} curvas)");
            }

            viewportsGroup.AppendLine("</g>");

            // Inserir antes da tag de fechamento </svg>
            int closeIndex = svgContent.ToString().LastIndexOf("</svg>", StringComparison.OrdinalIgnoreCase);
            if (closeIndex >= 0)
            {
                svgContent.Insert(closeIndex, viewportsGroup.ToString());
            }
            else
            {
                svgContent.Append(viewportsGroup.ToString()).Append("</svg>");
            }

            LastGeneratedSvg = svgContent.ToString();

            // 8. Gravação em Disco e Compilação de PDF Vetorial
            string outSvgPath = "";
            string outPdfPath = "";

            if (exportActive || _forcePreview || AutoOpenBrowser)
            {
                string targetDir = ResolveTargetDirectory(customPath);
                string baseName = ResolveBaseFileName(customPath, sheetDef.Name);

                outSvgPath = Path.Combine(targetDir, baseName + ".svg");
                File.WriteAllText(outSvgPath, LastGeneratedSvg, Encoding.UTF8);
                LastSvgPath = outSvgPath;

                // Compilar PDF Vetorial via Chromium/Edge Headless
                outPdfPath = Path.Combine(targetDir, baseName + ".pdf");
                bool pdfSuccess = CompileVectorPdf(outSvgPath, outPdfPath, sheetDef.WidthMm, sheetDef.HeightMm);
                if (pdfSuccess) LastPdfPath = outPdfPath;

                statusLog.Add($"Exportação Concluída:");
                statusLog.Add($"  SVG: {outSvgPath}");
                if (pdfSuccess) statusLog.Add($"  PDF: {outPdfPath}");
            }

            // Ação de Preview no Navegador
            if (_forcePreview)
            {
                _forcePreview = false;
                OpenBrowserPreview(LastGeneratedSvg, sheetDef.WidthMm, sheetDef.HeightMm);
            }

            // 9. Emitir Saídas do Grasshopper
            DA.SetData(0, LastPdfPath);
            DA.SetData(1, LastSvgPath);
            DA.SetData(2, LastGeneratedSvg);

            List<Rhino.Geometry.Rectangle3d> outRects = new List<Rhino.Geometry.Rectangle3d>();
            foreach (var vp in viewports)
            {
                Plane pl = new Plane(new Point3d(vp.Box.X, -vp.Box.Y, 0), Vector3d.ZAxis);
                outRects.Add(new Rhino.Geometry.Rectangle3d(pl, vp.Box.Width, -vp.Box.Height));
            }
            DA.SetDataList(3, outRects);
            DA.SetData(4, string.Join("\n", statusLog));
        }

        // ==========================================
        // MÉTODOS DE AÇÃO MANUAL (TRIGGERS)
        // ==========================================
        public void TriggerPreview()
        {
            _forcePreview = true;
            ExpireSolution(true);
        }

        public void TriggerExport()
        {
            _forceExport = true;
            ExpireSolution(true);
        }

        public void CycleDistributionPreset()
        {
            string[] modes = new[] { "Auto", "Fit", "Split_H", "Split_V", "Master_2", "Quad", "Grid_3x2" };
            int idx = Array.IndexOf(modes, DistributionMode);
            if (idx < 0) idx = 0;
            idx = (idx + 1) % modes.Length;
            DistributionMode = modes[idx];
            ExpireSolution(true);
        }

        // ==========================================
        // MOTOR DE RENDERIZAÇÃO VETORIAL SVG
        // ==========================================
        private string RenderViewportSvg(ViewportLayout vp, List<Curve> curves, string title, string scaleSpec, double northAngleDeg, int viewIdx, out string resolvedScaleText)
        {
            StringBuilder sb = new StringBuilder();
            string clipId = $"clip_view_{viewIdx}_{vp.Box.X:0}_{vp.Box.Y:0}";

            // Área Útil de Geometria (reserva espaço inferior para o rodapé)
            float footerHeight = Math.Min(18f, vp.Box.Height * 0.22f);
            float geomWidth = vp.Box.Width;
            float geomHeight = vp.Box.Height - footerHeight;
            float geomX = vp.Box.X;
            float geomY = vp.Box.Y;

            // 1. ClipPath para impedir que qualquer linha vaze para fora do quadro
            sb.AppendLine($"<clipPath id=\"{clipId}\">");
            sb.AppendLine($"  <rect x=\"{geomX.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{geomY.ToString("F2", CultureInfo.InvariantCulture)}\" width=\"{geomWidth.ToString("F2", CultureInfo.InvariantCulture)}\" height=\"{geomHeight.ToString("F2", CultureInfo.InvariantCulture)}\" />");
            sb.AppendLine("</clipPath>");

            // 2. Moldura Sutil do Viewport
            sb.AppendLine($"<rect x=\"{vp.Box.X.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{vp.Box.Y.ToString("F2", CultureInfo.InvariantCulture)}\" width=\"{vp.Box.Width.ToString("F2", CultureInfo.InvariantCulture)}\" height=\"{vp.Box.Height.ToString("F2", CultureInfo.InvariantCulture)}\" fill=\"none\" stroke=\"#e2e8f0\" stroke-width=\"0.35\" stroke-dasharray=\"2,2\" />");

            // 3. Calcular Enquadramento e Escala da Geometria
            BoundingBox bbox2D = ComputeCurves2DBBox(curves);
            double scaleFactor = 1.0;
            double geomCenterX = 0;
            double geomCenterY = 0;
            resolvedScaleText = scaleSpec;

            if (bbox2D.IsValid && bbox2D.Diagonal.Length > 1e-4)
            {
                geomCenterX = (bbox2D.Min.X + bbox2D.Max.X) * 0.5;
                geomCenterY = (bbox2D.Min.Y + bbox2D.Max.Y) * 0.5;

                double rawW = Math.Max(1e-3, bbox2D.Max.X - bbox2D.Min.X);
                double rawH = Math.Max(1e-3, bbox2D.Max.Y - bbox2D.Min.Y);

                if (scaleSpec.Equals("Fit", StringComparison.OrdinalIgnoreCase) || !scaleSpec.Contains(":"))
                {
                    double marginScale = 0.88; // 12% de margem de respiro
                    scaleFactor = Math.Min((geomWidth * marginScale) / rawW, (geomHeight * marginScale) / rawH);
                    resolvedScaleText = "Ajustada (Fit)";
                }
                else
                {
                    // Escala Técnica Arquitetônica Exata (ex.: 1:50, 1:100)
                    string[] parts = scaleSpec.Split(':');
                    if (parts.Length == 2 && double.TryParse(parts[1], out double denom) && denom > 0)
                    {
                        // Assume modelo em metros -> papel em milímetros: 1m = (1000 / denom) mm
                        // Se o modelo estiver em milímetros no Rhino, converte proporcionalmente
                        var unit = RhinoDoc.ActiveDoc?.ModelUnitSystem ?? UnitSystem.Meters;
                        double unitToMm = (unit == UnitSystem.Millimeters) ? 1.0 : (unit == UnitSystem.Centimeters ? 10.0 : 1000.0);
                        scaleFactor = unitToMm / denom;
                        resolvedScaleText = $"1:{denom:0}";
                    }
                    else
                    {
                        scaleFactor = Math.Min(geomWidth / rawW, geomHeight / rawH) * 0.85;
                        resolvedScaleText = "Ajustada (Fit)";
                    }
                }
            }

            // 4. Desenhar Curvas do Modelo Projetadas em 2D
            float boxCenterX = geomX + geomWidth * 0.5f;
            float boxCenterY = geomY + geomHeight * 0.5f;

            sb.AppendLine($"<g id=\"view_{viewIdx}_geometry\" clip-path=\"url(#{clipId})\">");

            foreach (var crv in curves)
            {
                if (crv == null || !crv.IsValid) continue;
                string pathD = ConvertCurveToSvgPath(crv, geomCenterX, geomCenterY, boxCenterX, boxCenterY, scaleFactor);
                if (!string.IsNullOrEmpty(pathD))
                {
                    sb.AppendLine($"  <path d=\"{pathD}\" fill=\"none\" stroke=\"#0f172a\" stroke-width=\"0.35\" stroke-linecap=\"round\" stroke-linejoin=\"round\" vector-effect=\"non-scaling-stroke\" />");
                }
            }
            sb.AppendLine("</g>");

            // 5. Rodapé Técnico do Viewport (Título, Escala Numérica, Escala Gráfica, Seta de Norte)
            float footerY = vp.Box.Bottom - footerHeight;
            float titleX = vp.Box.X + 4f;
            float titleY = footerY + 5.5f;

            sb.AppendLine($"<g id=\"view_{viewIdx}_footer\">");

            // Linha Divisória Superior do Rodapé
            sb.AppendLine($"  <line x1=\"{vp.Box.X.ToString("F2", CultureInfo.InvariantCulture)}\" y1=\"{footerY.ToString("F2", CultureInfo.InvariantCulture)}\" x2=\"{vp.Box.Right.ToString("F2", CultureInfo.InvariantCulture)}\" y2=\"{footerY.ToString("F2", CultureInfo.InvariantCulture)}\" stroke=\"#cbd5e0\" stroke-width=\"0.3\" />");

            // Título Editorial da Vista
            sb.AppendLine($"  <text x=\"{titleX.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{titleY.ToString("F2", CultureInfo.InvariantCulture)}\" font-family=\"'Segoe UI', Helvetica, Arial, sans-serif\" font-size=\"3.2\" font-weight=\"bold\" fill=\"#0f172a\">{EscapeXml(title)}</text>");

            // Escala Numérica
            float scaleY = titleY + 4f;
            sb.AppendLine($"  <text x=\"{titleX.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{scaleY.ToString("F2", CultureInfo.InvariantCulture)}\" font-family=\"'Segoe UI', Helvetica, Arial, sans-serif\" font-size=\"2.3\" fill=\"#475569\">Escala: {EscapeXml(resolvedScaleText)}</text>");

            // Escala Gráfica Alternada (Branco e Preto)
            float scaleBarX = titleX + 32f;
            float scaleBarY = footerY + 4f;
            string scaleBarSvg = GenerateGraphicScaleBarSvg(scaleBarX, scaleBarY, scaleFactor, resolvedScaleText);
            sb.AppendLine(scaleBarSvg);

            // Seta do Norte / Rosa dos Ventos
            float northX = vp.Box.Right - 8f;
            float northY = footerY + (footerHeight * 0.5f);
            string northSvg = GenerateNorthArrowSvg(northX, northY, northAngleDeg, 5.5f);
            sb.AppendLine(northSvg);

            sb.AppendLine("</g>");

            return sb.ToString();
        }

        // ==========================================
        // CONVERSÃO DE CURVAS RHINO PARA PATH SVG
        // ==========================================
        private string ConvertCurveToSvgPath(Curve crv, double geomCenterX, double geomCenterY, float boxCenterX, float boxCenterY, double scale)
        {
            Polyline poly;
            if (!crv.TryGetPolyline(out poly))
            {
                var polyCurve = crv.ToPolyline(0.02, 0.1, 0.05, 50.0);
                if (polyCurve != null) polyCurve.TryGetPolyline(out poly);
            }

            if (poly == null || poly.Count < 2) return string.Empty;

            StringBuilder path = new StringBuilder();
            for (int i = 0; i < poly.Count; i++)
            {
                Point3d pt = poly[i];
                // Projeção 2D com inversão do eixo Y (SVG cresce para baixo, Rhino cresce para cima)
                double svgX = boxCenterX + (pt.X - geomCenterX) * scale;
                double svgY = boxCenterY - (pt.Y - geomCenterY) * scale;

                char cmd = (i == 0) ? 'M' : 'L';
                path.Append(cmd)
                    .Append(svgX.ToString("F2", CultureInfo.InvariantCulture))
                    .Append(' ')
                    .Append(svgY.ToString("F2", CultureInfo.InvariantCulture))
                    .Append(' ');
            }

            if (crv.IsClosed) path.Append("Z");
            return path.ToString().TrimEnd();
        }

        private BoundingBox ComputeCurves2DBBox(List<Curve> curves)
        {
            BoundingBox bbox = BoundingBox.Unset;
            foreach (var crv in curves)
            {
                if (crv == null || !crv.IsValid) continue;
                bbox.Union(crv.GetBoundingBox(true));
            }
            return bbox;
        }

        // ==========================================
        // GERADOR DE ESCALA GRÁFICA PARAMÉTRICA
        // ==========================================
        private string GenerateGraphicScaleBarSvg(float startX, float startY, double scaleFactor, string scaleText)
        {
            // Determina passo da escala (ex.: 1m, 2m, 5m em papel mm)
            double stepReal = 1.0; // 1 metro
            double segWidthMm = stepReal * scaleFactor;

            // Se o segmento ficar muito pequeno (< 5mm) ou muito grande (> 25mm), ajusta a métrica
            if (segWidthMm < 5.0) { stepReal = 5.0; segWidthMm = stepReal * scaleFactor; }
            if (segWidthMm < 5.0) { stepReal = 10.0; segWidthMm = stepReal * scaleFactor; }
            if (segWidthMm > 30.0) { stepReal = 0.5; segWidthMm = stepReal * scaleFactor; }

            segWidthMm = Math.Max(4.0, Math.Min(25.0, segWidthMm));
            float barH = 1.2f;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"  <g id=\"graphic_scale_bar\">");

            // Segmento 1: Branco com borda preta
            sb.AppendLine($"    <rect x=\"{startX.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{startY.ToString("F2", CultureInfo.InvariantCulture)}\" width=\"{segWidthMm.ToString("F2", CultureInfo.InvariantCulture)}\" height=\"{barH.ToString("F2", CultureInfo.InvariantCulture)}\" fill=\"#ffffff\" stroke=\"#0f172a\" stroke-width=\"0.2\" />");

            // Segmento 2: Preto preenchido
            float seg2X = (float)(startX + segWidthMm);
            sb.AppendLine($"    <rect x=\"{seg2X.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{startY.ToString("F2", CultureInfo.InvariantCulture)}\" width=\"{segWidthMm.ToString("F2", CultureInfo.InvariantCulture)}\" height=\"{barH.ToString("F2", CultureInfo.InvariantCulture)}\" fill=\"#0f172a\" stroke=\"#0f172a\" stroke-width=\"0.2\" />");

            // Segmento 3: Branco com borda preta
            float seg3X = (float)(seg2X + segWidthMm);
            sb.AppendLine($"    <rect x=\"{seg3X.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{startY.ToString("F2", CultureInfo.InvariantCulture)}\" width=\"{segWidthMm.ToString("F2", CultureInfo.InvariantCulture)}\" height=\"{barH.ToString("F2", CultureInfo.InvariantCulture)}\" fill=\"#ffffff\" stroke=\"#0f172a\" stroke-width=\"0.2\" />");

            // Números da Escala (0, 1m, 2m, 3m)
            float textY = startY + barH + 2.5f;
            sb.AppendLine($"    <text x=\"{startX.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{textY.ToString("F2", CultureInfo.InvariantCulture)}\" font-family=\"'Segoe UI', sans-serif\" font-size=\"1.8\" fill=\"#64748b\" text-anchor=\"middle\">0</text>");
            sb.AppendLine($"    <text x=\"{seg2X.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{textY.ToString("F2", CultureInfo.InvariantCulture)}\" font-family=\"'Segoe UI', sans-serif\" font-size=\"1.8\" fill=\"#64748b\" text-anchor=\"middle\">{stepReal:0}m</text>");
            sb.AppendLine($"    <text x=\"{(seg3X + segWidthMm).ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{textY.ToString("F2", CultureInfo.InvariantCulture)}\" font-family=\"'Segoe UI', sans-serif\" font-size=\"1.8\" fill=\"#64748b\" text-anchor=\"middle\">{(stepReal * 3):0}m</text>");

            sb.AppendLine("  </g>");
            return sb.ToString();
        }

        // ==========================================
        // GERADOR DE SETA DE NORTE / ROSA DOS VENTOS
        // ==========================================
        private string GenerateNorthArrowSvg(float cx, float cy, double angleDeg, float radius)
        {
            StringBuilder sb = new StringBuilder();
            // Inversão angular para o sistema de coordenadas do SVG
            double rotSvg = -angleDeg + 90.0;

            sb.AppendLine($"  <g id=\"north_arrow\" transform=\"translate({cx.ToString("F2", CultureInfo.InvariantCulture)}, {cy.ToString("F2", CultureInfo.InvariantCulture)}) rotate({rotSvg.ToString("F1", CultureInfo.InvariantCulture)})\">");

            // Círculo Guia Fino
            sb.AppendLine($"    <circle cx=\"0\" cy=\"0\" r=\"{radius.ToString("F2", CultureInfo.InvariantCulture)}\" fill=\"none\" stroke=\"#cbd5e0\" stroke-width=\"0.25\" />");

            // Metade Esquerda da Seta (Preta)
            float tipY = -radius * 1.05f;
            float baseY = radius * 0.85f;
            float wingX = radius * 0.45f;

            sb.AppendLine($"    <polygon points=\"0,{tipY.ToString("F2", CultureInfo.InvariantCulture)} -{wingX.ToString("F2", CultureInfo.InvariantCulture)},{baseY.ToString("F2", CultureInfo.InvariantCulture)} 0,0\" fill=\"#0f172a\" />");

            // Metade Direita da Seta (Branca com Contorno)
            sb.AppendLine($"    <polygon points=\"0,{tipY.ToString("F2", CultureInfo.InvariantCulture)} {wingX.ToString("F2", CultureInfo.InvariantCulture)},{baseY.ToString("F2", CultureInfo.InvariantCulture)} 0,0\" fill=\"#ffffff\" stroke=\"#0f172a\" stroke-width=\"0.2\" />");

            // Letra "N"
            float letterY = tipY - 1.2f;
            sb.AppendLine($"    <text x=\"0\" y=\"{letterY.ToString("F2", CultureInfo.InvariantCulture)}\" font-family=\"'Segoe UI', sans-serif\" font-size=\"2.6\" font-weight=\"bold\" fill=\"#0f172a\" text-anchor=\"middle\">N</text>");

            sb.AppendLine("  </g>");
            return sb.ToString();
        }

        // ==========================================
        // MOTOR DE SUBDIVISÃO DE ÁREA DE DESENHO
        // ==========================================
        private List<ViewportLayout> SubdivideDrawingArea(SheetTemplateDef sheet, string distMode, int numViews)
        {
            List<ViewportLayout> vps = new List<ViewportLayout>();
            RectangleF area = sheet.DrawingArea;
            float gutter = (float)Math.Max(2.0, GutterMm);

            switch (distMode.ToUpperInvariant())
            {
                case "FIT":
                case "1":
                    vps.Add(new ViewportLayout(area, 0));
                    break;

                case "SPLIT_H":
                case "2_H":
                    {
                        float w = (area.Width - gutter) * 0.5f;
                        vps.Add(new ViewportLayout(new RectangleF(area.X, area.Y, w, area.Height), 0));
                        vps.Add(new ViewportLayout(new RectangleF(area.X + w + gutter, area.Y, w, area.Height), 1));
                    }
                    break;

                case "SPLIT_V":
                case "2_V":
                    {
                        float h = (area.Height - gutter) * 0.5f;
                        vps.Add(new ViewportLayout(new RectangleF(area.X, area.Y, area.Width, h), 0));
                        vps.Add(new ViewportLayout(new RectangleF(area.X, area.Y + h + gutter, area.Width, h), 1));
                    }
                    break;

                case "MASTER_2":
                case "3":
                    {
                        // 1 Vista Master à esquerda (63%) e 2 Secundárias à direita (37%)
                        float wLeft = (area.Width - gutter) * 0.63f;
                        float wRight = area.Width - gutter - wLeft;
                        float hRight = (area.Height - gutter) * 0.5f;

                        vps.Add(new ViewportLayout(new RectangleF(area.X, area.Y, wLeft, area.Height), 0));
                        vps.Add(new ViewportLayout(new RectangleF(area.X + wLeft + gutter, area.Y, wRight, hRight), 1));
                        vps.Add(new ViewportLayout(new RectangleF(area.X + wLeft + gutter, area.Y + hRight + gutter, wRight, hRight), 2));
                    }
                    break;

                case "GRID_3X2":
                case "6":
                    {
                        float w = (area.Width - gutter * 2f) / 3f;
                        float h = (area.Height - gutter) * 0.5f;
                        for (int r = 0; r < 2; r++)
                        {
                            for (int c = 0; c < 3; c++)
                            {
                                float vx = area.X + c * (w + gutter);
                                float vy = area.Y + r * (h + gutter);
                                vps.Add(new ViewportLayout(new RectangleF(vx, vy, w, h), vps.Count));
                            }
                        }
                    }
                    break;

                case "EXPLICIT":
                    if (sheet.ExplicitViewports != null && sheet.ExplicitViewports.Count > 0)
                    {
                        for (int i = 0; i < sheet.ExplicitViewports.Count; i++)
                        {
                            vps.Add(new ViewportLayout(sheet.ExplicitViewports[i], i));
                        }
                        break;
                    }
                    goto case "QUAD";

                case "QUAD":
                case "4":
                default:
                    {
                        float w = (area.Width - gutter) * 0.5f;
                        float h = (area.Height - gutter) * 0.5f;
                        vps.Add(new ViewportLayout(new RectangleF(area.X, area.Y, w, h), 0));
                        vps.Add(new ViewportLayout(new RectangleF(area.X + w + gutter, area.Y, w, h), 1));
                        vps.Add(new ViewportLayout(new RectangleF(area.X, area.Y + h + gutter, w, h), 2));
                        vps.Add(new ViewportLayout(new RectangleF(area.X + w + gutter, area.Y + h + gutter, w, h), 3));
                    }
                    break;
            }

            return vps;
        }

        private string ResolveDistributionMode(string mode, int numViews)
        {
            if (string.IsNullOrWhiteSpace(mode) || mode.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                if (numViews <= 1) return "Fit";
                if (numViews == 2) return "Split_H";
                if (numViews == 3) return "Master_2";
                if (numViews <= 4) return "Quad";
                return "Grid_3x2";
            }
            return mode;
        }

        // ==========================================
        // CARREGAMENTO DE TEMPLATES SVG
        // ==========================================
        private SheetTemplateDef LoadSheetTemplate(string templateInput)
        {
            // 1. Verificar se é arquivo SVG em disco
            if (!string.IsNullOrWhiteSpace(templateInput) && File.Exists(templateInput))
            {
                try
                {
                    string svgXml = File.ReadAllText(templateInput, Encoding.UTF8);
                    return ParseSvgTemplate(svgXml, Path.GetFileNameWithoutExtension(templateInput));
                }
                catch (Exception ex)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Falha ao ler SVG customizado: {ex.Message}. Usando template padrão A3.");
                }
            }

            // 2. Templates Embutidos
            string tName = templateInput?.ToUpperInvariant() ?? "A3_QUAD";
            if (tName.Contains("A4"))
            {
                return GenerateBuiltInA4Template();
            }
            return GenerateBuiltInA3Template();
        }

        private SheetTemplateDef ParseSvgTemplate(string svgXml, string name)
        {
            float width = 420f;
            float height = 297f;
            RectangleF drawingArea = new RectangleF(25f, 15f, 310f, 267f);
            List<RectangleF> explicitVps = new List<RectangleF>();

            try
            {
                var doc = XDocument.Parse(svgXml);
                var svgRoot = doc.Root;

                // Lê viewBox ou width/height
                var viewBoxAttr = svgRoot.Attribute("viewBox");
                if (viewBoxAttr != null)
                {
                    var parts = viewBoxAttr.Value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 4)
                    {
                        float.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out width);
                        float.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out height);
                    }
                }

                // Procura elemento com ID drawing_area ou area_desenho ou work_area
                var areaElem = svgRoot.Descendants().FirstOrDefault(e =>
                {
                    var id = e.Attribute("id")?.Value?.ToLowerInvariant();
                    return id == "drawing_area" || id == "area_desenho" || id == "work_area" || id == "canvas_area";
                });

                if (areaElem != null)
                {
                    float.TryParse(areaElem.Attribute("x")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out float x);
                    float.TryParse(areaElem.Attribute("y")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out float y);
                    float.TryParse(areaElem.Attribute("width")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out float w);
                    float.TryParse(areaElem.Attribute("height")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out float h);
                    if (w > 10f && h > 10f) drawingArea = new RectangleF(x, y, w, h);
                }
                else
                {
                    // Fallback para margens padrão: 25mm esquerda, 10mm outros, 70mm carimbo à direita
                    drawingArea = new RectangleF(25f, 10f, width - 25f - 85f, height - 20f);
                }

                // Procura viewports explícitos (view_0, view_1...)
                for (int i = 0; i < 16; i++)
                {
                    string targetId = $"view_{i}";
                    var vpElem = svgRoot.Descendants().FirstOrDefault(e => e.Attribute("id")?.Value?.ToLowerInvariant() == targetId);
                    if (vpElem != null)
                    {
                        float.TryParse(vpElem.Attribute("x")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out float vx);
                        float.TryParse(vpElem.Attribute("y")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out float vy);
                        float.TryParse(vpElem.Attribute("width")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out float vw);
                        float.TryParse(vpElem.Attribute("height")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out float vh);
                        explicitVps.Add(new RectangleF(vx, vy, vw, vh));
                    }
                }
            }
            catch { }

            return new SheetTemplateDef(name, width, height, drawingArea, svgXml, explicitVps);
        }

        private SheetTemplateDef GenerateBuiltInA3Template()
        {
            float w = 420f;
            float h = 297f;
            RectangleF drawingArea = new RectangleF(25f, 10f, 315f, 277f);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{w}mm\" height=\"{h}mm\" viewBox=\"0 0 {w} {h}\">");
            sb.AppendLine("  <defs>");
            sb.AppendLine("    <style>");
            sb.AppendLine("      @page { size: 420mm 297mm; margin: 0; }");
            sb.AppendLine("      text { font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Arial, sans-serif; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("  </defs>");

            // Fundo Branco da Folha
            sb.AppendLine($"  <rect width=\"{w}\" height=\"{h}\" fill=\"#ffffff\" />");

            // Margem ABNT Oficial: 25mm Esquerda, 10mm Superior, Inferior e Direita
            float mx = 25f; float my = 10f; float mw = w - 35f; float mh = h - 20f;
            sb.AppendLine($"  <rect x=\"{mx}\" y=\"{my}\" width=\"{mw}\" height=\"{mh}\" fill=\"none\" stroke=\"#0f172a\" stroke-width=\"0.5\" />");

            // Carimbo / Selo Técnico Vertical no Canto Direito (Largura 65mm)
            float carimboW = 65f;
            float carimboX = w - 10f - carimboW;
            float carimboY = my;
            float carimboH = mh;

            sb.AppendLine($"  <g id=\"glaux_carimbo\">");
            sb.AppendLine($"    <rect x=\"{carimboX}\" y=\"{carimboY}\" width=\"{carimboW}\" height=\"{carimboH}\" fill=\"#f8fafc\" stroke=\"#0f172a\" stroke-width=\"0.4\" />");

            // Cabeçalho da Empresa / Plugin
            sb.AppendLine($"    <rect x=\"{carimboX}\" y=\"{carimboY}\" width=\"{carimboW}\" height=\"22\" fill=\"#0284c7\" />");
            sb.AppendLine($"    <text x=\"{carimboX + carimboW * 0.5f}\" y=\"{carimboY + 11}\" font-size=\"4.2\" font-weight=\"bold\" fill=\"#ffffff\" text-anchor=\"middle\">GLAUX TOOLS</text>");
            sb.AppendLine($"    <text x=\"{carimboX + carimboW * 0.5f}\" y=\"{carimboY + 17}\" font-size=\"2.3\" fill=\"#e0f2fe\" text-anchor=\"middle\">ARQUITETURA &amp; ACÚSTICA</text>");

            // Linhas de Campos
            float fieldY = carimboY + 22f;
            void DrawField(string label, string tagValue, float fieldHeight)
            {
                sb.AppendLine($"    <line x1=\"{carimboX}\" y1=\"{fieldY}\" x2=\"{carimboX + carimboW}\" y2=\"{fieldY}\" stroke=\"#cbd5e0\" stroke-width=\"0.25\" />");
                sb.AppendLine($"    <text x=\"{carimboX + 3}\" y=\"{fieldY + 4.5}\" font-size=\"1.9\" fill=\"#64748b\" font-weight=\"bold\">{label}</text>");
                sb.AppendLine($"    <text x=\"{carimboX + 3}\" y=\"{fieldY + 10}\" font-size=\"2.8\" fill=\"#0f172a\">{tagValue}</text>");
                fieldY += fieldHeight;
            }

            DrawField("PROJETO", "{{PROJETO}}", 15f);
            DrawField("CONTEÚDO DA FOLHA", "{{TITULO}}", 15f);
            DrawField("AUTOR / RESPONSÁVEL", "{{AUTOR}}", 14f);
            DrawField("CLIENTE", "{{CLIENTE}}", 13f);
            DrawField("DATA", "{{DATA}}", 12f);
            DrawField("ESCALA GERAL", "{{ESCALA}}", 12f);
            DrawField("Nº DA FOLHA", "{{FOLHA}}", 14f);

            // Rodapé do Selo com Software
            sb.AppendLine($"    <line x1=\"{carimboX}\" y1=\"{carimboY + carimboH - 10}\" x2=\"{carimboX + carimboW}\" y2=\"{carimboY + carimboH - 10}\" stroke=\"#cbd5e0\" stroke-width=\"0.25\" />");
            sb.AppendLine($"    <text x=\"{carimboX + carimboW * 0.5f}\" y=\"{carimboY + carimboH - 4}\" font-size=\"2.0\" fill=\"#94a3b8\" text-anchor=\"middle\">Desenvolvido via Glaux Engine</text>");
            sb.AppendLine("  </g>");

            // Marcação da Área Livre de Desenho
            sb.AppendLine($"  <rect id=\"drawing_area\" x=\"{drawingArea.X}\" y=\"{drawingArea.Y}\" width=\"{drawingArea.Width}\" height=\"{drawingArea.Height}\" fill=\"none\" stroke=\"none\" />");

            sb.AppendLine("</svg>");
            return new SheetTemplateDef("A3 Horizontal Padrão", w, h, drawingArea, sb.ToString());
        }

        private SheetTemplateDef GenerateBuiltInA4Template()
        {
            float w = 210f;
            float h = 297f;
            RectangleF drawingArea = new RectangleF(25f, 10f, 175f, 230f);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{w}mm\" height=\"{h}mm\" viewBox=\"0 0 {w} {h}\">");
            sb.AppendLine("  <defs>");
            sb.AppendLine("    <style>");
            sb.AppendLine("      @page { size: 210mm 297mm; margin: 0; }");
            sb.AppendLine("      text { font-family: 'Segoe UI', -apple-system, Arial, sans-serif; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("  </defs>");
            sb.AppendLine($"  <rect width=\"{w}\" height=\"{h}\" fill=\"#ffffff\" />");

            // Margem A4: 25mm Esquerda, 10mm Outras
            float mx = 25f; float my = 10f; float mw = w - 35f; float mh = h - 20f;
            sb.AppendLine($"  <rect x=\"{mx}\" y=\"{my}\" width=\"{mw}\" height=\"{mh}\" fill=\"none\" stroke=\"#0f172a\" stroke-width=\"0.45\" />");

            // Carimbo Inferior Horizontal (Altura 42mm)
            float carimboH = 42f;
            float carimboY = my + mh - carimboH;
            sb.AppendLine($"  <g id=\"glaux_carimbo\">");
            sb.AppendLine($"    <rect x=\"{mx}\" y=\"{carimboY}\" width=\"{mw}\" height=\"{carimboH}\" fill=\"#f8fafc\" stroke=\"#0f172a\" stroke-width=\"0.35\" />");
            sb.AppendLine($"    <text x=\"{mx + 4}\" y=\"{carimboY + 8}\" font-size=\"3.8\" font-weight=\"bold\" fill=\"#0284c7\">GLAUX TOOLS</text>");
            sb.AppendLine($"    <text x=\"{mx + 4}\" y=\"{carimboY + 16}\" font-size=\"2.6\" fill=\"#0f172a\">{{PROJETO}}</text>");
            sb.AppendLine($"    <text x=\"{mx + 4}\" y=\"{carimboY + 23}\" font-size=\"2.3\" fill=\"#475569\">Autor: {{AUTOR}} | Data: {{DATA}}</text>");
            sb.AppendLine($"    <text x=\"{mx + mw - 4}\" y=\"{carimboY + 23}\" font-size=\"2.5\" font-weight=\"bold\" fill=\"#0f172a\" text-anchor=\"end\">Folha: {{FOLHA}}</text>");
            sb.AppendLine("  </g>");

            sb.AppendLine($"  <rect id=\"drawing_area\" x=\"{drawingArea.X}\" y=\"{drawingArea.Y}\" width=\"{drawingArea.Width}\" height=\"{drawingArea.Height}\" fill=\"none\" stroke=\"none\" />");
            sb.AppendLine("</svg>");

            return new SheetTemplateDef("A4 Vertical Relatório", w, h, drawingArea, sb.ToString());
        }

        // ==========================================
        // UTILITÁRIOS DE DADOS, TAGS E GEOMETRIA
        // ==========================================
        private List<List<Curve>> ExtractCurvesByView(GH_Structure<IGH_GeometricGoo> geomTree)
        {
            List<List<Curve>> result = new List<List<Curve>>();

            if (geomTree.PathCount == 0) return result;

            foreach (var path in geomTree.Paths)
            {
                var branch = geomTree.get_Branch(path);
                List<Curve> branchCurves = new List<Curve>();
                foreach (var item in branch)
                {
                    if (item == null) continue;
                    Curve crv = null;
                    if (GH_Convert.ToCurve(item, ref crv, GH_Conversion.Both) && crv != null && crv.IsValid)
                    {
                        branchCurves.Add(crv);
                    }
                }
                if (branchCurves.Count > 0) result.Add(branchCurves);
            }

            // Fallback se a árvore não tinha curvas em ramos separados mas tinha uma lista plana
            if (result.Count == 0 && geomTree.AllData(true).Any())
            {
                List<Curve> allCrvs = new List<Curve>();
                foreach (var item in geomTree.AllData(true))
                {
                    if (item == null) continue;
                    Curve crv = null;
                    if (GH_Convert.ToCurve(item, ref crv, GH_Conversion.Both) && crv != null && crv.IsValid)
                    {
                        allCrvs.Add(crv);
                    }
                }
                if (allCrvs.Count > 0) result.Add(allCrvs);
            }

            return result;
        }

        private Dictionary<string, string> ParseTags(List<string> tagList)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (tagList == null) return dict;

            foreach (var item in tagList)
            {
                if (string.IsNullOrWhiteSpace(item)) continue;
                int colonIdx = item.IndexOf(':');
                if (colonIdx > 0)
                {
                    string key = item.Substring(0, colonIdx).Trim();
                    string val = item.Substring(colonIdx + 1).Trim();
                    dict[key] = val;
                }
            }
            return dict;
        }

        private void ApplyDefaultTags(Dictionary<string, string> dict, int numViews)
        {
            if (!dict.ContainsKey("PROJETO")) dict["PROJETO"] = ProjectTitle;
            if (!dict.ContainsKey("AUTOR")) dict["AUTOR"] = AuthorName;
            if (!dict.ContainsKey("DATA")) dict["DATA"] = DateTime.Now.ToString("dd/MM/yyyy");
            if (!dict.ContainsKey("FOLHA")) dict["FOLHA"] = SheetNumber;
            if (!dict.ContainsKey("ESCALA")) dict["ESCALA"] = GeneralScale;
            if (!dict.ContainsKey("CLIENTE")) dict["CLIENTE"] = "Geral";
            if (!dict.ContainsKey("TITULO")) dict["TITULO"] = $"Estudo Gráfico - {numViews} Vistas";
        }

        private double ParseNorthAngle(object northObj)
        {
            if (northObj == null) return 90.0; // Padrão: YAxis / Norte para cima

            if (northObj is GH_Vector ghVec) northObj = ghVec.Value;
            if (northObj is Vector3d vec)
            {
                if (vec.Length < 1e-6) return 90.0;
                double rad = Math.Atan2(vec.Y, vec.X);
                double deg = rad * (180.0 / Math.PI);
                return (deg + 360.0) % 360.0;
            }

            if (northObj is GH_Number ghNum) return ghNum.Value;
            if (double.TryParse(northObj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
            {
                return val;
            }
            return 90.0;
        }

        // ==========================================
        // EXPORTAÇÃO VETORIAL E PREVIEW NO NAVEGADOR
        // ==========================================
        public void OpenBrowserPreview(string svgContent, float widthMm, float heightMm)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "Glaux_Sheets");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                string svgFile = Path.Combine(tempDir, "preview_sheet.svg");
                File.WriteAllText(svgFile, svgContent, Encoding.UTF8);

                // Criar wrapper HTML elegante com controles de zoom e fundo escuro profissional
                string htmlFile = Path.Combine(tempDir, "preview_sheet.html");
                string htmlContent = $@"<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
<meta charset=""UTF-8"">
<title>Glaux Tools - Visualizador de Prancha Vetorial</title>
<style>
  body {{
    margin: 0; padding: 20px; background: #0f172a; color: #f8fafc;
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    display: flex; flex-direction: column; align-items: center; min-height: 100vh; box-sizing: border-box;
  }}
  .toolbar {{
    margin-bottom: 16px; display: flex; gap: 12px; align-items: center;
    background: #1e293b; padding: 8px 18px; border-radius: 8px; border: 1px solid #334155;
  }}
  .btn {{
    background: #0284c7; color: white; border: none; padding: 6px 14px; border-radius: 4px;
    font-weight: 600; cursor: pointer; font-size: 13px; text-decoration: none;
  }}
  .btn:hover {{ background: #0369a1; }}
  .sheet-container {{
    background: white; box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.5), 0 8px 10px -6px rgba(0, 0, 0, 0.5);
    max-width: 95vw; max-height: 85vh; overflow: auto; display: flex; justify-content: center;
  }}
  svg {{ width: 100%; height: auto; display: block; }}
  @media print {{
    body {{ background: white; padding: 0; }}
    .toolbar {{ display: none; }}
    .sheet-container {{ box-shadow: none; max-width: 100%; max-height: 100%; }}
  }}
</style>
</head>
<body>
<div class=""toolbar"">
  <span style=""font-weight: bold; color: #38bdf8;"">GLAUX TOOLS | Prancha Vetorial</span>
  <span>Formato: {widthMm:0} x {heightMm:0} mm</span>
  <button class=""btn"" onclick=""window.print()"">🖨️ Imprimir / Salvar PDF</button>
  <a class=""btn"" href=""preview_sheet.svg"" download=""prancha_glaux.svg"">💾 Baixar SVG</a>
</div>
<div class=""sheet-container"">
  {svgContent}
</div>
</body>
</html>";
                File.WriteAllText(htmlFile, htmlContent, Encoding.UTF8);

                Process.Start(new ProcessStartInfo { FileName = htmlFile, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[Glaux Tools] Erro ao abrir preview no navegador: {ex.Message}");
            }
        }

        private bool CompileVectorPdf(string svgPath, string pdfPath, float widthMm, float heightMm)
        {
            string[] browsers = new[]
            {
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Google\Chrome\Application\chrome.exe"
            };

            string browserPath = browsers.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(browserPath)) return false;

            try
            {
                string uri = "file:///" + svgPath.Replace("\\", "/");
                var psi = new ProcessStartInfo
                {
                    FileName = browserPath,
                    Arguments = $"--headless --disable-gpu --no-pdf-header-footer --run-all-compositor-stages-before-draw --virtual-time-budget=2000 --print-to-pdf=\"{pdfPath}\" \"{uri}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc?.WaitForExit(6000);
                }
                return File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private string ResolveTargetDirectory(string customPath)
        {
            if (!string.IsNullOrWhiteSpace(customPath))
            {
                if (Directory.Exists(customPath)) return customPath;
                string dir = Path.GetDirectoryName(customPath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir)) return dir;
            }

            var doc = RhinoDoc.ActiveDoc;
            if (doc != null && !string.IsNullOrWhiteSpace(doc.Path))
            {
                string pDir = Path.GetDirectoryName(doc.Path);
                if (Directory.Exists(pDir)) return pDir;
            }

            string temp = Path.Combine(Path.GetTempPath(), "Glaux_Sheets");
            if (!Directory.Exists(temp)) Directory.CreateDirectory(temp);
            return temp;
        }

        private string ResolveBaseFileName(string customPath, string defaultPrefix)
        {
            if (!string.IsNullOrWhiteSpace(customPath))
            {
                string fName = Path.GetFileNameWithoutExtension(customPath);
                if (!string.IsNullOrWhiteSpace(fName)) return fName;
            }
            return $"{defaultPrefix}_{DateTime.Now:yyyyMMdd_HHmmss}";
        }

        private static string EscapeXml(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }
    }

    // ==========================================
    // ESTRUTURAS AUXILIARES
    // ==========================================
    public class SheetTemplateDef
    {
        public string Name { get; set; }
        public float WidthMm { get; set; }
        public float HeightMm { get; set; }
        public RectangleF DrawingArea { get; set; }
        public string SvgXml { get; set; }
        public List<RectangleF> ExplicitViewports { get; set; }

        public SheetTemplateDef(string name, float w, float h, RectangleF drawingArea, string svgXml, List<RectangleF> explicitVps = null)
        {
            Name = name;
            WidthMm = w;
            HeightMm = h;
            DrawingArea = drawingArea;
            SvgXml = svgXml;
            ExplicitViewports = explicitVps ?? new List<RectangleF>();
        }
    }

    public class ViewportLayout
    {
        public RectangleF Box { get; set; }
        public int Index { get; set; }

        public ViewportLayout(RectangleF box, int idx)
        {
            Box = box;
            Index = idx;
        }
    }

    // ==========================================
    // ATRIBUTOS E BOTÃO INTERATIVO NA TELA
    // ==========================================
    public class PillVectorSheetLayout_Attributes : GH_ComponentAttributes
    {
        private const float CONTROL_BAR_HEIGHT = 42f;
        private const float MIN_WIDTH = 190f;

        private RectangleF m_btnPreviewRect;
        private RectangleF m_btnExportRect;
        private RectangleF m_badgePresetRect;

        public PillVectorSheetLayout_Attributes(PillVectorSheetLayout_Component owner) : base(owner)
        {
        }

        protected override void Layout()
        {
            base.Layout();

            RectangleF b = Bounds;
            b.Width = Math.Max(b.Width, MIN_WIDTH);
            float oldRight = Bounds.Right;
            b.Height += CONTROL_BAR_HEIGHT;
            Bounds = b;

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

            float barY = Bounds.Bottom - CONTROL_BAR_HEIGHT + 3f;
            float barW = Bounds.Width - 10f;
            float barX = Bounds.X + 5f;

            // Linha 1: [ 👁️ Visualizar no Navegador ] e [ 📑 Exportar ]
            float row1Y = barY + 2f;
            float halfBtnW = (barW - 4f) * 0.5f;
            m_btnPreviewRect = new RectangleF(barX + 1f, row1Y, halfBtnW, 17f);
            m_btnExportRect = new RectangleF(barX + 3f + halfBtnW, row1Y, halfBtnW, 17f);

            // Linha 2: Badge de Preset / Modo de Distribuição
            float row2Y = barY + 21f;
            m_badgePresetRect = new RectangleF(barX + 1f, row2Y, barW - 2f, 16f);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            if (channel == GH_CanvasChannel.Objects)
            {
                var savedPivot = Pivot;
                Pivot = new PointF(Bounds.X + Bounds.Width / 2f, savedPivot.Y);
                base.Render(canvas, graphics, channel);
                Pivot = savedPivot;

                var comp = Owner as PillVectorSheetLayout_Component;
                if (comp == null) return;

                RectangleF b = Bounds;
                RectangleF barRect = new RectangleF(b.X + 4f, b.Bottom - CONTROL_BAR_HEIGHT + 2f, b.Width - 8f, CONTROL_BAR_HEIGHT - 4f);

                // 1. Fundo da Cápsula Inferior (Dark Slate com Borda Técnica)
                using (var path = CreateRoundedRectangle(barRect, 5f))
                using (var bgBrush = new LinearGradientBrush(barRect, Color.FromArgb(30, 41, 59), Color.FromArgb(15, 23, 42), LinearGradientMode.Vertical))
                using (var borderPen = new Pen(Color.FromArgb(51, 65, 85), 1.0f))
                {
                    graphics.FillPath(bgBrush, path);
                    graphics.DrawPath(borderPen, path);
                }

                // 2. Botão [ 👁️ Preview ] e Botão [ 📑 Export ]
                DrawActionPill(graphics, m_btnPreviewRect, "👁️ Preview Web", Color.FromArgb(14, 165, 233));
                DrawActionPill(graphics, m_btnExportRect, "📑 Exportar PDF", Color.FromArgb(16, 185, 129));

                // 3. Badge Inferior com Preset e Modo de Distribuição
                string badgeText = $"Prancha: {comp.ActiveTemplate} | Modo: {comp.DistributionMode}";
                DrawBadgePill(graphics, m_badgePresetRect, badgeText);
            }
            else
            {
                base.Render(canvas, graphics, channel);
            }
        }

        private void DrawActionPill(Graphics g, RectangleF rect, string text, Color accent)
        {
            using (var path = CreateRoundedRectangle(rect, 3.5f))
            using (var bgBrush = new SolidBrush(Color.FromArgb(35, 45, 60)))
            using (var borderPen = new Pen(accent, 0.9f))
            using (var font = new Font("Segoe UI", 6.8f, FontStyle.Bold))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
                using (var textBrush = new SolidBrush(Color.FromArgb(241, 245, 249)))
                {
                    g.DrawString(text, font, textBrush, rect, sf);
                }
            }
        }

        private void DrawBadgePill(Graphics g, RectangleF rect, string text)
        {
            using (var path = CreateRoundedRectangle(rect, 3f))
            using (var bgBrush = new SolidBrush(Color.FromArgb(15, 23, 42)))
            using (var borderPen = new Pen(Color.FromArgb(71, 85, 105), 0.7f))
            using (var font = new Font("Segoe UI", 6.5f, FontStyle.Regular))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
                using (var textBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                {
                    g.DrawString(text, font, textBrush, rect, sf);
                }
            }
        }

        private GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var comp = Owner as PillVectorSheetLayout_Component;
                if (comp != null)
                {
                    if (m_btnPreviewRect.Contains(e.CanvasLocation))
                    {
                        comp.TriggerPreview();
                        return GH_ObjectResponse.Handled;
                    }
                    if (m_btnExportRect.Contains(e.CanvasLocation))
                    {
                        comp.TriggerExport();
                        return GH_ObjectResponse.Handled;
                    }
                    if (m_badgePresetRect.Contains(e.CanvasLocation))
                    {
                        comp.CycleDistributionPreset();
                        return GH_ObjectResponse.Handled;
                    }
                }
            }
            return base.RespondToMouseDown(sender, e);
        }
    }
}
