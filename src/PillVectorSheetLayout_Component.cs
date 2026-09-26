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
    /// a partir de curvas e geometrias universais do Grasshopper (Curvas, Breps, Meshes, Superfícies, Pontos e Hachuras).
    /// Suporta templates SVG com carimbo customizado, detecção de área de desenho (drawing_area),
    /// presets de auto-distribuição (Hero, Split, Quad, Grid 3x2), escalas gráficas paramétricas,
    /// norte dinâmico, estilos de pena inspirados no QGIS/ABNT e botão interativo para pré-visualização instantânea no navegador.
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
                "Diagrama e exporta pranchas técnicas vetoriais (SVG e PDF) a partir de curvas e geometrias universais do Grasshopper (Curvas, Breps, Meshes, Superfícies, Pontos e Hachuras). Suporta templates SVG com carimbo customizado, detecção de área de desenho (drawing_area), presets de auto-distribuição (Hero, Split, Quad, Grid 3x2), escalas gráficas paramétricas, norte dinâmico, estilos de pena inspirados no QGIS/ABNT e botão interativo para pré-visualização instantânea no navegador.",
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
                "Geometry", "G",
                "Geometrias 2D/3D a diagramar na prancha (Curvas, Breps, Meshes, Superfícies, Pontos, Hachuras). Use uma DataTree onde cada ramo {0}, {1}, etc., corresponde a uma vista ou camada diferente.",
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

            pManager.AddGenericParameter(
                "Styles / Pens", "Sty",
                "Estilos de linha/pena para os conjuntos/ramos de objetos. Aceita a saída do Pill Pen Style, strings shorthand por ramo ('0.50, solid, #000' ou '0: 0.5, dashed, #0284c7'), regras de camada ('Corte: 0.6mm') ou strings CSS/SVG. Se omitido, adota a hierarquia canônica ABNT.",
                GH_ParamAccess.tree);
            pManager[6].Optional = true;

            pManager.AddTextParameter(
                "Data / Tags", "D",
                "Metadados do carimbo no formato chave-valor (ex.: 'PROJETO: Teatro Escola', 'AUTOR: Jefferson', 'DATA: 26/09/2026', 'FOLHA: 01/01', 'CLIENTE: PMB'). Substitui automaticamente as tags {{CHAVE}} no SVG.",
                GH_ParamAccess.list);
            pManager[7].Optional = true;

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
            pManager[9].Optional = true;
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
            // 1. Obter Geometrias de Entrada
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

            GH_Structure<IGH_Goo> stylesTree = null;
            DA.GetDataTree(6, out stylesTree);

            List<string> tagList = new List<string>();
            DA.GetDataList(7, tagList);

            bool exportActive = false;
            DA.GetData(8, ref exportActive);

            string customPath = "";
            DA.GetData(9, ref customPath);

            if (_forceExport)
            {
                exportActive = true;
                _forceExport = false;
            }

            // 2. Mapear Estilos por Ramo e Dicionário
            var styleMap = ParseStyles(stylesTree);

            // 3. Extrair e Processar Geometrias Universais (Curvas, Breps, Meshes, Pontos)
            List<List<StyledGeometryItem>> viewElements = ExtractUniversalGeometries(geomTree, styleMap);
            int numViews = Math.Max(1, viewElements.Count);

            // 4. Carregar Template SVG Base (Nativo ou Customizado)
            SheetTemplateDef sheetDef = LoadSheetTemplate(ActiveTemplate);

            // 5. Mapear Substituições do Carimbo (Tags)
            var tagDict = ParseTags(tagList);
            ApplyDefaultTags(tagDict, numViews);

            // 6. Determinar Modo de Distribuição
            string effectiveDist = ResolveDistributionMode(DistributionMode, numViews);

            // 7. Subdividir Área de Desenho em Viewports
            List<ViewportLayout> viewports = SubdivideDrawingArea(sheetDef, effectiveDist, numViews);
            ComputedViewports.Clear();
            foreach (var vp in viewports)
            {
                ComputedViewports.Add(new RectangleF(vp.Box.X, vp.Box.Y, vp.Box.Width, vp.Box.Height));
            }

            // 8. Renderizar Geometria e Rodapé de Cada Viewport no SVG
            StringBuilder svgContent = new StringBuilder(sheetDef.SvgXml);

            foreach (var kvp in tagDict)
            {
                svgContent.Replace("{{" + kvp.Key + "}}", kvp.Value);
            }

            StringBuilder viewportsGroup = new StringBuilder();
            viewportsGroup.AppendLine("<g id=\"glaux_viewports_layer\">");

            List<string> statusLog = new List<string>();
            statusLog.Add($"Prancha: {sheetDef.Name} ({sheetDef.WidthMm:0} x {sheetDef.HeightMm:0} mm)");
            statusLog.Add($"Distribuição: {effectiveDist} ({numViews} vistas)");

            for (int i = 0; i < viewports.Count; i++)
            {
                var vp = viewports[i];
                List<StyledGeometryItem> elements = (i < viewElements.Count) ? viewElements[i] : new List<StyledGeometryItem>();

                string viewTitle = (i < names.Count && !string.IsNullOrWhiteSpace(names[i])) ? names[i] : $"0{i + 1} | VISTA {i + 1}";
                string scaleSpec = (i < scales.Count && !string.IsNullOrWhiteSpace(scales[i])) ? scales[i] : "Fit";

                string vpSvg = RenderViewportSvg(vp, elements, viewTitle, scaleSpec, northAngleDeg, i, out string resolvedScaleText);
                viewportsGroup.AppendLine(vpSvg);

                statusLog.Add($"  [Vista {i + 1}] {viewTitle} -> Escala: {resolvedScaleText} ({elements.Count} elementos)");
            }

            viewportsGroup.AppendLine("</g>");

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

            // 9. Gravação em Disco e Compilação de PDF Vetorial
            string outSvgPath = "";
            string outPdfPath = "";

            if (exportActive || _forcePreview || AutoOpenBrowser)
            {
                string targetDir = ResolveTargetDirectory(customPath);
                string baseName = ResolveBaseFileName(customPath, sheetDef.Name);

                outSvgPath = Path.Combine(targetDir, baseName + ".svg");
                File.WriteAllText(outSvgPath, LastGeneratedSvg, Encoding.UTF8);
                LastSvgPath = outSvgPath;

                outPdfPath = Path.Combine(targetDir, baseName + ".pdf");
                bool pdfSuccess = CompileVectorPdf(outSvgPath, outPdfPath, sheetDef.WidthMm, sheetDef.HeightMm);
                if (pdfSuccess) LastPdfPath = outPdfPath;

                statusLog.Add($"Exportação Concluída:");
                statusLog.Add($"  SVG: {outSvgPath}");
                if (pdfSuccess) statusLog.Add($"  PDF: {outPdfPath}");
            }

            if (_forcePreview)
            {
                _forcePreview = false;
                OpenBrowserPreview(LastGeneratedSvg, sheetDef.WidthMm, sheetDef.HeightMm);
            }

            // 10. Emitir Saídas do Grasshopper
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
        // MOTOR DE RENDERIZAÇÃO VETORIAL SVG UNIVERSAL
        // ==========================================
        private string RenderViewportSvg(ViewportLayout vp, List<StyledGeometryItem> elements, string title, string scaleSpec, double northAngleDeg, int viewIdx, out string resolvedScaleText)
        {
            StringBuilder sb = new StringBuilder();
            string clipId = $"clip_view_{viewIdx}_{vp.Box.X:0}_{vp.Box.Y:0}";

            float footerHeight = Math.Min(18f, vp.Box.Height * 0.22f);
            float geomWidth = vp.Box.Width;
            float geomHeight = vp.Box.Height - footerHeight;
            float geomX = vp.Box.X;
            float geomY = vp.Box.Y;

            sb.AppendLine($"<clipPath id=\"{clipId}\">");
            sb.AppendLine($"  <rect x=\"{geomX.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{geomY.ToString("F2", CultureInfo.InvariantCulture)}\" width=\"{geomWidth.ToString("F2", CultureInfo.InvariantCulture)}\" height=\"{geomHeight.ToString("F2", CultureInfo.InvariantCulture)}\" />");
            sb.AppendLine("</clipPath>");

            sb.AppendLine($"<rect x=\"{vp.Box.X.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{vp.Box.Y.ToString("F2", CultureInfo.InvariantCulture)}\" width=\"{vp.Box.Width.ToString("F2", CultureInfo.InvariantCulture)}\" height=\"{vp.Box.Height.ToString("F2", CultureInfo.InvariantCulture)}\" fill=\"none\" stroke=\"#e2e8f0\" stroke-width=\"0.35\" stroke-dasharray=\"2,2\" />");

            // Calcular BoundingBox conjunto
            BoundingBox bbox2D = BoundingBox.Unset;
            foreach (var el in elements)
            {
                if (el.Geometry != null) bbox2D.Union(el.Geometry.GetBoundingBox(true));
                else if (el.Point.HasValue) bbox2D.Union(el.Point.Value);
            }

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
                    double marginScale = 0.88;
                    scaleFactor = Math.Min((geomWidth * marginScale) / rawW, (geomHeight * marginScale) / rawH);
                    resolvedScaleText = "Ajustada (Fit)";
                }
                else
                {
                    string[] parts = scaleSpec.Split(':');
                    if (parts.Length == 2 && double.TryParse(parts[1], out double denom) && denom > 0)
                    {
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

            float boxCenterX = geomX + geomWidth * 0.5f;
            float boxCenterY = geomY + geomHeight * 0.5f;

            sb.AppendLine($"<g id=\"view_{viewIdx}_geometry\" clip-path=\"url(#{clipId})\">");

            foreach (var el in elements)
            {
                RenderSingleElementSvg(sb, el, geomCenterX, geomCenterY, boxCenterX, boxCenterY, scaleFactor);
            }

            sb.AppendLine("</g>");

            // Rodapé Técnico do Viewport
            float footerY = vp.Box.Bottom - footerHeight;
            float titleX = vp.Box.X + 4f;
            float titleY = footerY + 5.5f;

            sb.AppendLine($"<g id=\"view_{viewIdx}_footer\">");
            sb.AppendLine($"  <line x1=\"{vp.Box.X.ToString("F2", CultureInfo.InvariantCulture)}\" y1=\"{footerY.ToString("F2", CultureInfo.InvariantCulture)}\" x2=\"{vp.Box.Right.ToString("F2", CultureInfo.InvariantCulture)}\" y2=\"{footerY.ToString("F2", CultureInfo.InvariantCulture)}\" stroke=\"#cbd5e0\" stroke-width=\"0.3\" />");
            sb.AppendLine($"  <text x=\"{titleX.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{titleY.ToString("F2", CultureInfo.InvariantCulture)}\" font-family=\"'Segoe UI', Helvetica, Arial, sans-serif\" font-size=\"3.2\" font-weight=\"bold\" fill=\"#0f172a\">{EscapeXml(title)}</text>");

            float scaleY = titleY + 4f;
            sb.AppendLine($"  <text x=\"{titleX.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{scaleY.ToString("F2", CultureInfo.InvariantCulture)}\" font-family=\"'Segoe UI', Helvetica, Arial, sans-serif\" font-size=\"2.3\" fill=\"#475569\">Escala: {EscapeXml(resolvedScaleText)}</text>");

            float scaleBarX = titleX + 32f;
            float scaleBarY = footerY + 4f;
            string scaleBarSvg = GenerateGraphicScaleBarSvg(scaleBarX, scaleBarY, scaleFactor, resolvedScaleText);
            sb.AppendLine(scaleBarSvg);

            float northX = vp.Box.Right - 8f;
            float northY = footerY + (footerHeight * 0.5f);
            string northSvg = GenerateNorthArrowSvg(northX, northY, northAngleDeg, 5.5f);
            sb.AppendLine(northSvg);

            sb.AppendLine("</g>");

            return sb.ToString();
        }

        private void RenderSingleElementSvg(StringBuilder sb, StyledGeometryItem el, double geomCenterX, double geomCenterY, float boxCenterX, float boxCenterY, double scale)
        {
            var sty = el.Style;

            // 1. Ponto / Marcador Vetorial
            if (el.Point.HasValue)
            {
                Point3d pt = el.Point.Value;
                double sx = boxCenterX + (pt.X - geomCenterX) * scale;
                double sy = boxCenterY - (pt.Y - geomCenterY) * scale;
                RenderMarkerSvg(sb, sx, sy, sty);
                return;
            }

            // 2. Malha (Mesh)
            if (el.Geometry is Mesh mesh)
            {
                // REGRA CRÍTICA: Malhas de gráficos / Heatmaps com cores nos vértices devem preservar suas cores!
                if (mesh.VertexColors != null && mesh.VertexColors.Count > 0)
                {
                    RenderColoredMeshSvg(sb, mesh, geomCenterX, geomCenterY, boxCenterX, boxCenterY, scale);
                }
                else
                {
                    // Malha arquitetônica comum: desenha wireframe com o estilo de linha configurado
                    if (mesh.TopologyEdges != null && mesh.TopologyEdges.Count > 0)
                    {
                        for (int e = 0; e < mesh.TopologyEdges.Count; e++)
                        {
                            var pair = mesh.TopologyEdges.GetTopologyVertices(e);
                            Point3d ptA = mesh.TopologyVertices[pair.I];
                            Point3d ptB = mesh.TopologyVertices[pair.J];
                            Line line = new Line(ptA, ptB);
                            string d = LineToSvgPath(line, geomCenterX, geomCenterY, boxCenterX, boxCenterY, scale);
                            sb.AppendLine($"  <path d=\"{d}\" fill=\"none\" stroke=\"{sty.StrokeColorHex}\" stroke-width=\"{sty.WeightMm.ToString("F2", CultureInfo.InvariantCulture)}\" stroke-dasharray=\"{sty.DashArray}\" opacity=\"{sty.Opacity.ToString("F2", CultureInfo.InvariantCulture)}\" vector-effect=\"non-scaling-stroke\" />");
                        }
                    }
                }
                return;
            }

            // 3. Curvas (Curves, Lines, Arcs, Polylines)
            if (el.Geometry is Curve crv)
            {
                string pathD = ConvertCurveToSvgPath(crv, geomCenterX, geomCenterY, boxCenterX, boxCenterY, scale);
                if (!string.IsNullOrEmpty(pathD))
                {
                    string fillAttr = crv.IsClosed ? sty.Fill : "none";
                    sb.AppendLine($"  <path d=\"{pathD}\" fill=\"{fillAttr}\" stroke=\"{sty.StrokeColorHex}\" stroke-width=\"{sty.WeightMm.ToString("F2", CultureInfo.InvariantCulture)}\" stroke-dasharray=\"{sty.DashArray}\" opacity=\"{sty.Opacity.ToString("F2", CultureInfo.InvariantCulture)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" vector-effect=\"non-scaling-stroke\" />");
                }
            }
        }

        // ==========================================
        // RENDERIZAÇÃO DE MALHAS COLORIDAS (HEATMAPS)
        // ==========================================
        private void RenderColoredMeshSvg(StringBuilder sb, Mesh mesh, double geomCenterX, double geomCenterY, float boxCenterX, float boxCenterY, double scale)
        {
            sb.AppendLine("  <g id=\"colored_mesh_graphic\">");
            for (int i = 0; i < mesh.Faces.Count; i++)
            {
                var f = mesh.Faces[i];
                Point3d pA = mesh.Vertices[f.A];
                Point3d pB = mesh.Vertices[f.B];
                Point3d pC = mesh.Vertices[f.C];

                double sAx = boxCenterX + (pA.X - geomCenterX) * scale;
                double sAy = boxCenterY - (pA.Y - geomCenterY) * scale;
                double sBx = boxCenterX + (pB.X - geomCenterX) * scale;
                double sBy = boxCenterY - (pB.Y - geomCenterY) * scale;
                double sCx = boxCenterX + (pC.X - geomCenterX) * scale;
                double sCy = boxCenterY - (pC.Y - geomCenterY) * scale;

                Color cA = mesh.VertexColors[f.A];
                Color cB = mesh.VertexColors[f.B];
                Color cC = mesh.VertexColors[f.C];

                int avgR = (cA.R + cB.R + cC.R) / 3;
                int avgG = (cA.G + cB.G + cC.G) / 3;
                int avgB = (cA.B + cB.B + cC.B) / 3;
                string faceColor = $"#{avgR:X2}{avgG:X2}{avgB:X2}";

                if (f.IsQuad)
                {
                    Point3d pD = mesh.Vertices[f.D];
                    double sDx = boxCenterX + (pD.X - geomCenterX) * scale;
                    double sDy = boxCenterY - (pD.Y - geomCenterY) * scale;

                    sb.AppendLine($"    <polygon points=\"{sAx:F2},{sAy:F2} {sBx:F2},{sBy:F2} {sCx:F2},{sCy:F2} {sDx:F2},{sDy:F2}\" fill=\"{faceColor}\" stroke=\"{faceColor}\" stroke-width=\"0.05\" />");
                }
                else
                {
                    sb.AppendLine($"    <polygon points=\"{sAx:F2},{sAy:F2} {sBx:F2},{sBy:F2} {sCx:F2},{sCy:F2}\" fill=\"{faceColor}\" stroke=\"{faceColor}\" stroke-width=\"0.05\" />");
                }
            }
            sb.AppendLine("  </g>");
        }

        // ==========================================
        // RENDERIZAÇÃO DE MARCADORES DE PONTO (QGIS)
        // ==========================================
        private void RenderMarkerSvg(StringBuilder sb, double sx, double sy, PenStyleDef sty)
        {
            float r = (float)(sty.MarkerSizeMm * 0.5);
            string col = sty.StrokeColorHex;
            string fill = sty.Fill == "none" ? col : sty.Fill;
            string op = sty.Opacity.ToString("F2", CultureInfo.InvariantCulture);

            switch (sty.Marker.ToLowerInvariant())
            {
                case "square":
                    sb.AppendLine($"  <rect x=\"{(sx - r):F2}\" y=\"{(sy - r):F2}\" width=\"{(r * 2):F2}\" height=\"{(r * 2):F2}\" fill=\"{fill}\" stroke=\"{col}\" stroke-width=\"0.3\" opacity=\"{op}\" />");
                    break;
                case "cross":
                    sb.AppendLine($"  <line x1=\"{(sx - r):F2}\" y1=\"{sy:F2}\" x2=\"{(sx + r):F2}\" y2=\"{sy:F2}\" stroke=\"{col}\" stroke-width=\"0.4\" opacity=\"{op}\" />");
                    sb.AppendLine($"  <line x1=\"{sx:F2}\" y1=\"{(sy - r):F2}\" x2=\"{sx:F2}\" y2=\"{(sy + r):F2}\" stroke=\"{col}\" stroke-width=\"0.4\" opacity=\"{op}\" />");
                    break;
                case "x":
                    sb.AppendLine($"  <line x1=\"{(sx - r):F2}\" y1=\"{(sy - r):F2}\" x2=\"{(sx + r):F2}\" y2=\"{(sy + r):F2}\" stroke=\"{col}\" stroke-width=\"0.4\" opacity=\"{op}\" />");
                    sb.AppendLine($"  <line x1=\"{(sx - r):F2}\" y1=\"{(sy + r):F2}\" x2=\"{(sx + r):F2}\" y2=\"{(sy - r):F2}\" stroke=\"{col}\" stroke-width=\"0.4\" opacity=\"{op}\" />");
                    break;
                case "target":
                    sb.AppendLine($"  <circle cx=\"{sx:F2}\" cy=\"{sy:F2}\" r=\"{r:F2}\" fill=\"none\" stroke=\"{col}\" stroke-width=\"0.3\" opacity=\"{op}\" />");
                    sb.AppendLine($"  <line x1=\"{(sx - r * 1.3):F2}\" y1=\"{sy:F2}\" x2=\"{(sx + r * 1.3):F2}\" y2=\"{sy:F2}\" stroke=\"{col}\" stroke-width=\"0.3\" opacity=\"{op}\" />");
                    sb.AppendLine($"  <line x1=\"{sx:F2}\" y1=\"{(sy - r * 1.3):F2}\" x2=\"{sx:F2}\" y2=\"{(sy + r * 1.3):F2}\" stroke=\"{col}\" stroke-width=\"0.3\" opacity=\"{op}\" />");
                    break;
                case "triangle":
                    sb.AppendLine($"  <polygon points=\"{sx:F2},{(sy - r):F2} {(sx + r):F2},{(sy + r):F2} {(sx - r):F2},{(sy + r):F2}\" fill=\"{fill}\" stroke=\"{col}\" stroke-width=\"0.3\" opacity=\"{op}\" />");
                    break;
                case "circle":
                default:
                    sb.AppendLine($"  <circle cx=\"{sx:F2}\" cy=\"{sy:F2}\" r=\"{r:F2}\" fill=\"{fill}\" stroke=\"{col}\" stroke-width=\"0.3\" opacity=\"{op}\" />");
                    break;
            }
        }

        // ==========================================
        // EXTRAÇÃO UNIVERSAL DE GEOMETRIAS (BREPS, MESHES, PONTOS)
        // ==========================================
        private List<List<StyledGeometryItem>> ExtractUniversalGeometries(GH_Structure<IGH_GeometricGoo> geomTree, Dictionary<int, PenStyleDef> styleMap)
        {
            List<List<StyledGeometryItem>> result = new List<List<StyledGeometryItem>>();
            if (geomTree.PathCount == 0) return result;

            int branchIdx = 0;
            foreach (var path in geomTree.Paths)
            {
                var branch = geomTree.get_Branch(path);
                List<StyledGeometryItem> viewItems = new List<StyledGeometryItem>();

                // Estilo padrão do ramo (resolvido por Sty ou ABNT fallback)
                PenStyleDef branchStyle = styleMap.ContainsKey(branchIdx) ? styleMap[branchIdx] : GetAbntDefaultStyle(branchIdx);

                foreach (var item in branch)
                {
                    if (item == null) continue;
                    var scriptObj = (item as IGH_Goo)?.ScriptVariable();

                    // 1. Checar se tem estilo individual anexado via Pill Pen Style
                    PenStyleDef itemStyle = branchStyle;
                    if (scriptObj is GeometryBase gb && gb.UserDictionary != null && gb.UserDictionary.ContainsKey("glaux_pen_style"))
                    {
                        string embeddedStyle = gb.UserDictionary.GetString("glaux_pen_style");
                        if (!string.IsNullOrWhiteSpace(embeddedStyle))
                        {
                            itemStyle = PenStyleDef.Parse(embeddedStyle);
                        }
                    }

                    // 2. Extrair conforme o tipo geométrico
                    if (scriptObj is Point3d pt3d)
                    {
                        viewItems.Add(new StyledGeometryItem { Point = pt3d, Style = itemStyle });
                    }
                    else if (scriptObj is GH_Point ghPt)
                    {
                        viewItems.Add(new StyledGeometryItem { Point = ghPt.Value, Style = itemStyle });
                    }
                    else if (scriptObj is Mesh mesh)
                    {
                        viewItems.Add(new StyledGeometryItem { Geometry = mesh, Style = itemStyle });
                    }
                    else if (scriptObj is Brep brep)
                    {
                        var edgeCurves = brep.DuplicateEdgeCurves();
                        if (edgeCurves != null)
                        {
                            foreach (var ec in edgeCurves) viewItems.Add(new StyledGeometryItem { Geometry = ec, Style = itemStyle });
                        }
                    }
                    else if (scriptObj is Surface srf)
                    {
                        var edgeCurves = srf.ToBrep()?.DuplicateEdgeCurves();
                        if (edgeCurves != null)
                        {
                            foreach (var ec in edgeCurves) viewItems.Add(new StyledGeometryItem { Geometry = ec, Style = itemStyle });
                        }
                    }
                    else if (scriptObj is Hatch hatch)
                    {
                        var hCurves = hatch.Get3dCurves(true);
                        if (hCurves != null)
                        {
                            foreach (var hc in hCurves) viewItems.Add(new StyledGeometryItem { Geometry = hc, Style = itemStyle });
                        }
                    }
                    else
                    {
                        Curve crv = null;
                        if (GH_Convert.ToCurve(item, ref crv, GH_Conversion.Both) && crv != null && crv.IsValid)
                        {
                            viewItems.Add(new StyledGeometryItem { Geometry = crv, Style = itemStyle });
                        }
                    }
                }

                if (viewItems.Count > 0) result.Add(viewItems);
                branchIdx++;
            }

            return result;
        }

        // ==========================================
        // PARSER DE ESTILOS DE PENA
        // ==========================================
        private Dictionary<int, PenStyleDef> ParseStyles(GH_Structure<IGH_Goo> stylesTree)
        {
            var dict = new Dictionary<int, PenStyleDef>();
            if (stylesTree == null || stylesTree.IsEmpty) return dict;

            int branchIdx = 0;
            foreach (var path in stylesTree.Paths)
            {
                var branch = stylesTree.get_Branch(path);
                if (branch != null && branch.Count > 0)
                {
                    string str = branch[0]?.ToString();
                    if (!string.IsNullOrWhiteSpace(str))
                    {
                        dict[branchIdx] = PenStyleDef.Parse(str);
                    }
                }
                branchIdx++;
            }

            return dict;
        }

        private static PenStyleDef GetAbntDefaultStyle(int branchIdx)
        {
            switch (branchIdx)
            {
                case 0: // Corte ABNT: Linha Grossa Contínua Preta
                    return new PenStyleDef { WeightMm = 0.50, StrokeColorHex = "#0f172a", DashArray = "none", Opacity = 1.0 };
                case 1: // Vista ABNT: Linha Média Contínua Grafite
                    return new PenStyleDef { WeightMm = 0.25, StrokeColorHex = "#334155", DashArray = "none", Opacity = 1.0 };
                case 2: // Projeção ABNT: Linha Fina Tracejada
                    return new PenStyleDef { WeightMm = 0.18, StrokeColorHex = "#64748b", DashArray = "4,2", Opacity = 0.85 };
                case 3: // Eixo ABNT: Linha Fina Traço-Ponto
                    return new PenStyleDef { WeightMm = 0.13, StrokeColorHex = "#94a3b8", DashArray = "5,2,1,2", Opacity = 0.75, Marker = "cross" };
                default:
                    return new PenStyleDef { WeightMm = 0.20, StrokeColorHex = "#0f172a", DashArray = "none", Opacity = 1.0 };
            }
        }

        private string LineToSvgPath(Line line, double geomCenterX, double geomCenterY, float boxCenterX, float boxCenterY, double scale)
        {
            double x1 = boxCenterX + (line.FromX - geomCenterX) * scale;
            double y1 = boxCenterY - (line.FromY - geomCenterY) * scale;
            double x2 = boxCenterX + (line.ToX - geomCenterX) * scale;
            double y2 = boxCenterY - (line.ToY - geomCenterY) * scale;

            return $"M {x1.ToString("F2", CultureInfo.InvariantCulture)} {y1.ToString("F2", CultureInfo.InvariantCulture)} L {x2.ToString("F2", CultureInfo.InvariantCulture)} {y2.ToString("F2", CultureInfo.InvariantCulture)}";
        }

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

        // ==========================================
        // GERADOR DE ESCALA GRÁFICA & NORTE
        // ==========================================
        private string GenerateGraphicScaleBarSvg(float startX, float startY, double scaleFactor, string scaleText)
        {
            double stepReal = 1.0;
            double segWidthMm = stepReal * scaleFactor;

            if (segWidthMm < 5.0) { stepReal = 5.0; segWidthMm = stepReal * scaleFactor; }
            if (segWidthMm < 5.0) { stepReal = 10.0; segWidthMm = stepReal * scaleFactor; }
            if (segWidthMm > 30.0) { stepReal = 0.5; segWidthMm = stepReal * scaleFactor; }

            segWidthMm = Math.Max(4.0, Math.Min(25.0, segWidthMm));
            float barH = 1.2f;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"  <g id=\"graphic_scale_bar\">");

            sb.AppendLine($"    <rect x=\"{startX.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{startY.ToString("F2", CultureInfo.InvariantCulture)}\" width=\"{segWidthMm.ToString("F2", CultureInfo.InvariantCulture)}\" height=\"{barH.ToString("F2", CultureInfo.InvariantCulture)}\" fill=\"#ffffff\" stroke=\"#0f172a\" stroke-width=\"0.2\" />");

            float seg2X = (float)(startX + segWidthMm);
            sb.AppendLine($"    <rect x=\"{seg2X.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{startY.ToString("F2", CultureInfo.InvariantCulture)}\" width=\"{segWidthMm.ToString("F2", CultureInfo.InvariantCulture)}\" height=\"{barH.ToString("F2", CultureInfo.InvariantCulture)}\" fill=\"#0f172a\" stroke=\"#0f172a\" stroke-width=\"0.2\" />");

            float seg3X = (float)(seg2X + segWidthMm);
            sb.AppendLine($"    <rect x=\"{seg3X.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{startY.ToString("F2", CultureInfo.InvariantCulture)}\" width=\"{segWidthMm.ToString("F2", CultureInfo.InvariantCulture)}\" height=\"{barH.ToString("F2", CultureInfo.InvariantCulture)}\" fill=\"#ffffff\" stroke=\"#0f172a\" stroke-width=\"0.2\" />");

            float textY = startY + barH + 2.5f;
            sb.AppendLine($"    <text x=\"{startX.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{textY.ToString("F2", CultureInfo.InvariantCulture)}\" font-family=\"'Segoe UI', sans-serif\" font-size=\"1.8\" fill=\"#64748b\" text-anchor=\"middle\">0</text>");
            sb.AppendLine($"    <text x=\"{seg2X.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{textY.ToString("F2", CultureInfo.InvariantCulture)}\" font-family=\"'Segoe UI', sans-serif\" font-size=\"1.8\" fill=\"#64748b\" text-anchor=\"middle\">{stepReal:0}m</text>");
            sb.AppendLine($"    <text x=\"{(seg3X + segWidthMm).ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{textY.ToString("F2", CultureInfo.InvariantCulture)}\" font-family=\"'Segoe UI', sans-serif\" font-size=\"1.8\" fill=\"#64748b\" text-anchor=\"middle\">{(stepReal * 3):0}m</text>");

            sb.AppendLine("  </g>");
            return sb.ToString();
        }

        private string GenerateNorthArrowSvg(float cx, float cy, double angleDeg, float radius)
        {
            StringBuilder sb = new StringBuilder();
            double rotSvg = -angleDeg + 90.0;

            sb.AppendLine($"  <g id=\"north_arrow\" transform=\"translate({cx.ToString("F2", CultureInfo.InvariantCulture)}, {cy.ToString("F2", CultureInfo.InvariantCulture)}) rotate({rotSvg.ToString("F1", CultureInfo.InvariantCulture)})\">");
            sb.AppendLine($"    <circle cx=\"0\" cy=\"0\" r=\"{radius.ToString("F2", CultureInfo.InvariantCulture)}\" fill=\"none\" stroke=\"#cbd5e0\" stroke-width=\"0.25\" />");

            float tipY = -radius * 1.05f;
            float baseY = radius * 0.85f;
            float wingX = radius * 0.45f;

            sb.AppendLine($"    <polygon points=\"0,{tipY.ToString("F2", CultureInfo.InvariantCulture)} -{wingX.ToString("F2", CultureInfo.InvariantCulture)},{baseY.ToString("F2", CultureInfo.InvariantCulture)} 0,0\" fill=\"#0f172a\" />");
            sb.AppendLine($"    <polygon points=\"0,{tipY.ToString("F2", CultureInfo.InvariantCulture)} {wingX.ToString("F2", CultureInfo.InvariantCulture)},{baseY.ToString("F2", CultureInfo.InvariantCulture)} 0,0\" fill=\"#ffffff\" stroke=\"#0f172a\" stroke-width=\"0.2\" />");

            float letterY = tipY - 1.2f;
            sb.AppendLine($"    <text x=\"0\" y=\"{letterY.ToString("F2", CultureInfo.InvariantCulture)}\" font-family=\"'Segoe UI', sans-serif\" font-size=\"2.6\" font-weight=\"bold\" fill=\"#0f172a\" text-anchor=\"middle\">N</text>");
            sb.AppendLine("  </g>");
            return sb.ToString();
        }

        // ==========================================
        // SUBDIVISÃO DE ÁREA DE DESENHO
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
                        for (int i = 0; i < sheet.ExplicitViewports.Count; i++) vps.Add(new ViewportLayout(sheet.ExplicitViewports[i], i));
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

        private SheetTemplateDef LoadSheetTemplate(string templateInput)
        {
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

            string tName = templateInput?.ToUpperInvariant() ?? "A3_QUAD";
            if (tName.Contains("A4")) return GenerateBuiltInA4Template();
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
                    drawingArea = new RectangleF(25f, 10f, width - 25f - 85f, height - 20f);
                }

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
            float w = 420f; float h = 297f;
            RectangleF drawingArea = new RectangleF(25f, 10f, 315f, 277f);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{w}mm\" height=\"{h}mm\" viewBox=\"0 0 {w} {h}\">");
            sb.AppendLine("  <defs><style>@page { size: 420mm 297mm; margin: 0; } text { font-family: 'Segoe UI', -apple-system, Arial, sans-serif; }</style></defs>");
            sb.AppendLine($"  <rect width=\"{w}\" height=\"{h}\" fill=\"#ffffff\" />");

            float mx = 25f; float my = 10f; float mw = w - 35f; float mh = h - 20f;
            sb.AppendLine($"  <rect x=\"{mx}\" y=\"{my}\" width=\"{mw}\" height=\"{mh}\" fill=\"none\" stroke=\"#0f172a\" stroke-width=\"0.5\" />");

            float carimboW = 65f; float carimboX = w - 10f - carimboW; float carimboY = my; float carimboH = mh;
            sb.AppendLine($"  <g id=\"glaux_carimbo\">");
            sb.AppendLine($"    <rect x=\"{carimboX}\" y=\"{carimboY}\" width=\"{carimboW}\" height=\"{carimboH}\" fill=\"#f8fafc\" stroke=\"#0f172a\" stroke-width=\"0.4\" />");
            sb.AppendLine($"    <rect x=\"{carimboX}\" y=\"{carimboY}\" width=\"{carimboW}\" height=\"22\" fill=\"#0284c7\" />");
            sb.AppendLine($"    <text x=\"{carimboX + carimboW * 0.5f}\" y=\"{carimboY + 11}\" font-size=\"4.2\" font-weight=\"bold\" fill=\"#ffffff\" text-anchor=\"middle\">GLAUX TOOLS</text>");
            sb.AppendLine($"    <text x=\"{carimboX + carimboW * 0.5f}\" y=\"{carimboY + 17}\" font-size=\"2.3\" fill=\"#e0f2fe\" text-anchor=\"middle\">ARQUITETURA &amp; ACÚSTICA</text>");

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

            sb.AppendLine($"    <line x1=\"{carimboX}\" y1=\"{carimboY + carimboH - 10}\" x2=\"{carimboX + carimboW}\" y2=\"{carimboY + carimboH - 10}\" stroke=\"#cbd5e0\" stroke-width=\"0.25\" />");
            sb.AppendLine($"    <text x=\"{carimboX + carimboW * 0.5f}\" y=\"{carimboY + carimboH - 4}\" font-size=\"2.0\" fill=\"#94a3b8\" text-anchor=\"middle\">Desenvolvido via Glaux Engine</text>");
            sb.AppendLine("  </g>");
            sb.AppendLine($"  <rect id=\"drawing_area\" x=\"{drawingArea.X}\" y=\"{drawingArea.Y}\" width=\"{drawingArea.Width}\" height=\"{drawingArea.Height}\" fill=\"none\" stroke=\"none\" />");
            sb.AppendLine("</svg>");
            return new SheetTemplateDef("A3 Horizontal Padrão", w, h, drawingArea, sb.ToString());
        }

        private SheetTemplateDef GenerateBuiltInA4Template()
        {
            float w = 210f; float h = 297f;
            RectangleF drawingArea = new RectangleF(25f, 10f, 175f, 230f);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{w}mm\" height=\"{h}mm\" viewBox=\"0 0 {w} {h}\">");
            sb.AppendLine("  <defs><style>@page { size: 210mm 297mm; margin: 0; } text { font-family: 'Segoe UI', Arial, sans-serif; }</style></defs>");
            sb.AppendLine($"  <rect width=\"{w}\" height=\"{h}\" fill=\"#ffffff\" />");

            float mx = 25f; float my = 10f; float mw = w - 35f; float mh = h - 20f;
            sb.AppendLine($"  <rect x=\"{mx}\" y=\"{my}\" width=\"{mw}\" height=\"{mh}\" fill=\"none\" stroke=\"#0f172a\" stroke-width=\"0.45\" />");

            float carimboH = 42f; float carimboY = my + mh - carimboH;
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
        // UTILITÁRIOS DE DADOS, TAGS E EXPORT
        // ==========================================
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
                    dict[item.Substring(0, colonIdx).Trim()] = item.Substring(colonIdx + 1).Trim();
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
            if (northObj == null) return 90.0;
            if (northObj is GH_Vector ghVec) northObj = ghVec.Value;
            if (northObj is Vector3d vec)
            {
                if (vec.Length < 1e-6) return 90.0;
                double rad = Math.Atan2(vec.Y, vec.X);
                return ((rad * (180.0 / Math.PI)) + 360.0) % 360.0;
            }
            if (northObj is GH_Number ghNum) return ghNum.Value;
            if (double.TryParse(northObj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double val)) return val;
            return 90.0;
        }

        public void OpenBrowserPreview(string svgContent, float widthMm, float heightMm)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "Glaux_Sheets");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                string svgFile = Path.Combine(tempDir, "preview_sheet.svg");
                File.WriteAllText(svgFile, svgContent, Encoding.UTF8);

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
            catch { return false; }
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
    // ESTRUTURAS AUXILIARES & DEFINIÇÃO DE PENAS
    // ==========================================
    public class StyledGeometryItem
    {
        public GeometryBase Geometry { get; set; }
        public Point3d? Point { get; set; }
        public PenStyleDef Style { get; set; }
    }

    public class PenStyleDef
    {
        public double WeightMm { get; set; } = 0.25;
        public string StrokeColorHex { get; set; } = "#0f172a";
        public string DashArray { get; set; } = "none";
        public string Fill { get; set; } = "none";
        public double Opacity { get; set; } = 1.0;
        public string Marker { get; set; } = "circle";
        public double MarkerSizeMm { get; set; } = 2.5;

        public static PenStyleDef Parse(string raw)
        {
            var def = new PenStyleDef();
            if (string.IsNullOrWhiteSpace(raw)) return def;

            string s = raw.Trim();

            // Formato CSS Key-Value (ex.: stroke:#0f172a; stroke-width:0.5mm; fill:none;)
            if (s.Contains(":") && (s.Contains("stroke") || s.Contains("width") || s.Contains(";")))
            {
                var pairs = s.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    int colon = pair.IndexOf(':');
                    if (colon > 0)
                    {
                        string k = pair.Substring(0, colon).Trim().ToLowerInvariant();
                        string v = pair.Substring(colon + 1).Trim();

                        if (k == "stroke" || k == "color") def.StrokeColorHex = v;
                        else if (k == "stroke-width" || k == "width" || k == "weight")
                        {
                            string numOnly = v.Replace("mm", "").Replace("px", "").Trim();
                            if (double.TryParse(numOnly, NumberStyles.Any, CultureInfo.InvariantCulture, out double w)) def.WeightMm = w;
                        }
                        else if (k == "stroke-dasharray" || k == "pattern" || k == "dash") def.DashArray = PillPenStyle_Component.ResolveDashPattern(v);
                        else if (k == "fill") def.Fill = v;
                        else if (k == "opacity")
                        {
                            if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out double op)) def.Opacity = op;
                        }
                        else if (k == "marker") def.Marker = v;
                        else if (k == "marker-size")
                        {
                            string numOnly = v.Replace("mm", "").Trim();
                            if (double.TryParse(numOnly, NumberStyles.Any, CultureInfo.InvariantCulture, out double ms)) def.MarkerSizeMm = ms;
                        }
                    }
                }
                return def;
            }

            // Formato Shorthand separado por vírgula (ex.: "0.50, dashed, #0284c7")
            var tokens = s.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string tok = tokens[i].Trim();
                if (double.TryParse(tok.Replace("mm", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out double wVal))
                {
                    def.WeightMm = wVal;
                }
                else if (tok.StartsWith("#") || tok.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
                {
                    def.StrokeColorHex = tok;
                }
                else
                {
                    def.DashArray = PillPenStyle_Component.ResolveDashPattern(tok);
                }
            }

            return def;
        }
    }

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

            float row1Y = barY + 2f;
            float halfBtnW = (barW - 4f) * 0.5f;
            m_btnPreviewRect = new RectangleF(barX + 1f, row1Y, halfBtnW, 17f);
            m_btnExportRect = new RectangleF(barX + 3f + halfBtnW, row1Y, halfBtnW, 17f);

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

                using (var path = CreateRoundedRectangle(barRect, 5f))
                using (var bgBrush = new LinearGradientBrush(barRect, Color.FromArgb(30, 41, 59), Color.FromArgb(15, 23, 42), LinearGradientMode.Vertical))
                using (var borderPen = new Pen(Color.FromArgb(51, 65, 85), 1.0f))
                {
                    graphics.FillPath(bgBrush, path);
                    graphics.DrawPath(borderPen, path);
                }

                DrawActionPill(graphics, m_btnPreviewRect, "👁️ Preview Web", Color.FromArgb(14, 165, 233));
                DrawActionPill(graphics, m_btnExportRect, "📑 Exportar PDF", Color.FromArgb(16, 185, 129));

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
