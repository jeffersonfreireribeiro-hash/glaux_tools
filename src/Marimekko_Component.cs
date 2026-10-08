// Marimekko_Component.cs
// Marimekko Chart (Mekko / Mosaic / Variable-Width Stacked Bar): a LARGURA de cada coluna é um dado (valor da categoria)
// e a altura de cada segmento é a composição da categoria; a ÁREA da célula = participação da categoria × participação do segmento.
//
// Arquitetura: Values/Widths/Labels → MarimekkoLayout (MarimekkoModel.cs, sem GH/Rhino) → { Canvas, Viewport, PNG, SVG, PDF, CSV, saídas }.
// Todas as representações leem o MESMO layout; o export é somente leitura.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Display;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    public class Marimekko_Component : GH_Component
    {
        // ---- configurações do menu (serializadas) ----
        public bool ShowCanvasChart = true;
        public bool ShowViewportPreview = true;
        public bool AlsoExportPdf = false;
        public MarimekkoLabelMode LabelMode = MarimekkoLabelMode.LabelPercent;
        public bool ShowLegend = true;
        public bool ShowCategoryLabels = true;
        public bool ShowCategoryShare = true;

        // ---- estado da última solução: um modelo, um layout, vários renderizadores ----
        public MarimekkoLayout Layout { get; private set; }
        public MarimekkoStyle Style { get; private set; } = new MarimekkoStyle();
        public string ExportFolder = "";
        public string LastExportMessage = "";
        private Plane _plane = Plane.WorldXY;
        private double _chartW = 10.0, _chartH = 6.0;

        // cache do viewport
        private Mesh _vpMesh;
        private readonly List<Line> _vpLines = new List<Line>();
        private readonly List<(string Text, Point3d Pos, bool Cat)> _vpCategoryLabels = new List<(string, Point3d, bool)>();
        private readonly List<(string Text, Point3d Pos)> _vpLegendLabels = new List<(string, Point3d)>();
        private BoundingBox _clip = BoundingBox.Empty;

        // export só quando pedido: borda de subida do gatilho
        private bool _lastTrigger;

        public Marimekko_Component()
            : base(
                "Marimekko Chart",
                "Marimekko",
                "Creates a variable-width stacked Marimekko chart from categorical and compositional data.\n" +
                "- Values: Data Tree, cada ramo {i} = uma categoria; cada item = um segmento (composição);\n" +
                "- Category Widths: valor de largura de cada categoria (não precisa somar 100; se omitido, usa o total da categoria);\n" +
                "- Largura = dado; altura do segmento = valor ÷ total da categoria; área da célula = participação da categoria × participação do segmento;\n" +
                "- Canvas, viewport do Rhino, PNG, SVG, PDF e CSV usam o mesmo modelo e o mesmo layout.",
                "Glaux Tools",
                "Visual")
        {
        }

        public override Guid ComponentGuid => new Guid("b7110017-e1ef-4000-8000-000000000017");

        protected override Bitmap Icon => GlauxToolsIcons.Marimekko;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override void CreateAttributes()
        {
            m_attributes = new Marimekko_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Values", "V",
                "Composição de cada categoria como Data Tree: cada ramo {i} é uma categoria e cada item do ramo é um segmento (ex.: {0}: 20, 30, 50 / {1}: 60, 20, 20). " +
                "Os valores não precisam somar 100: a altura de cada segmento é valor ÷ total da categoria. Valores negativos, NaN, ∞ ou nulos são inválidos (não há Abs()).",
                GH_ParamAccess.tree);

            pManager.AddGenericParameter("Category Widths", "W",
                "Valor que define a LARGURA de cada categoria (um por ramo, na mesma ordem; ex.: 20, 55, 25 ou 200, 550, 250 — dá a mesma geometria). " +
                "Não precisa somar 100. 1 item vale para todas as categorias (larguras iguais); vazio = cada categoria usa o seu próprio total. " +
                "Quantidade intermediária (nem 1, nem igual ao número de categorias) é erro. Largura 0 = categoria sem largura; negativa = inválida.",
                GH_ParamAccess.list);
            pManager[1].Optional = true;

            pManager.AddTextParameter("Category Labels", "CL",
                "Rótulos das categorias, na ordem dos ramos de Values (ex.: Residencial, Comercial, Institucional). Faltando: 'Categoria n'.",
                GH_ParamAccess.list);
            pManager[2].Optional = true;

            pManager.AddTextParameter("Segment Labels", "SL",
                "Rótulos dos segmentos, na ordem dos itens de cada ramo (lista comum a todas as categorias; ex.: Tipo A, Tipo B, Tipo C). " +
                "Cada segmento mantém a mesma cor em todas as categorias. Faltando: 'Segmento n'.",
                GH_ParamAccess.list);
            pManager[3].Optional = true;

            pManager.AddPlaneParameter("Base Plane", "P",
                "Plano do gráfico (padrão: World XY). X do plano = largura (categorias), Y do plano = altura (composição).",
                GH_ParamAccess.item, Plane.WorldXY);
            pManager[4].Optional = true;

            pManager.AddGenericParameter("Chart Size", "Size",
                "Dimensões do gráfico no modelo: número único (largura; altura = 60%) ou 'largura, altura' (ex.: '10, 6'), vetor ou ponto. Padrão: 10 × 6. " +
                "Só muda o enquadramento: proporções entre categorias e segmentos continuam exatas.",
                GH_ParamAccess.item);
            pManager[5].Optional = true;

            pManager.AddGenericParameter("Colors / Palette", "Col",
                "Cores por SEGMENT (mesmo segmento = mesma cor em todas as categorias). Aceita lista de cores, 'Glaux', 'Turbo', 'Viridis', 'Jet' ou 'CoolWarm'. Padrão: paleta Glaux Tools.",
                GH_ParamAccess.list);
            pManager[6].Optional = true;

            pManager.AddTextParameter("Title", "T", "Título do gráfico (opcional).", GH_ParamAccess.item, "");
            pManager[7].Optional = true;

            pManager.AddTextParameter("Export Folder", "Folder",
                "Pasta de destino da exportação (PNG, SVG, CSV e, se ativado no menu, PDF). Vazio = Área de Trabalho.", GH_ParamAccess.item, "");
            pManager[8].Optional = true;

            pManager.AddBooleanParameter("Export", "Export",
                "Gatilho de exportação (PNG + SVG + CSV). Exporta uma vez quando muda de False para True; não escreve arquivos a cada solução. Os botões do canvas exportam cada formato.",
                GH_ParamAccess.item, false);
            pManager[9].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddRectangleParameter("Cells", "Cells",
                "Retângulos das células no plano do gráfico, em Data Tree {categoria}[segmento] (sem Flatten).", GH_ParamAccess.tree);
            pManager.AddRectangleParameter("Chart Boundary", "Bnd", "Limite geral do gráfico (um retângulo).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Category Share", "CatShare",
                "Participação de cada categoria na LARGURA total (0–1), na ordem das categorias incluídas.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Segment Share", "SegShare",
                "Participação de cada segmento DENTRO da sua categoria (0–1), em árvore {categoria}[segmento]. Não é a área da célula.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Cell Area Share", "AreaShare",
                "Participação de cada célula na ÁREA do gráfico (= Category Share × Segment Share), em árvore {categoria}[segmento].", GH_ParamAccess.tree);
            pManager.AddTextParameter("Report", "Rep", "Resumo do gráfico, invariantes e diagnósticos de entrada.", GH_ParamAccess.item);
            pManager.AddTextParameter("Exported Files", "Files", "Caminhos dos arquivos exportados na última exportação.", GH_ParamAccess.list);
        }

        // ==========================================================================
        // SOLVE
        // ==========================================================================
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Layout = null;
            _vpMesh = null; _vpLines.Clear(); _vpCategoryLabels.Clear(); _vpLegendLabels.Clear(); _clip = BoundingBox.Empty;

            DA.GetDataTree(0, out GH_Structure<IGH_Goo> tree);
            var widthsRaw = new List<IGH_Goo>(); DA.GetDataList(1, widthsRaw);
            var catLabels = new List<string>(); DA.GetDataList(2, catLabels);
            var segLabels = new List<string>(); DA.GetDataList(3, segLabels);
            Plane plane = Plane.WorldXY; DA.GetData(4, ref plane);
            IGH_Goo sizeGoo = null; DA.GetData(5, ref sizeGoo);
            var colorsRaw = new List<IGH_Goo>(); DA.GetDataList(6, colorsRaw);
            string title = ""; DA.GetData(7, ref title);
            string folder = ""; DA.GetData(8, ref folder);
            bool trigger = false; DA.GetData(9, ref trigger);
            ExportFolder = folder ?? "";

            if (plane == null || !plane.IsValid) plane = Plane.WorldXY;
            _plane = plane;
            ParseSize(sizeGoo, out _chartW, out _chartH);

            var diag = new List<MarimekkoDiagnostic>();
            var inputs = BuildInputs(tree, widthsRaw, catLabels, diag);

            Layout = inputs == null ? new MarimekkoLayout() : MarimekkoLayout.Build(inputs, segLabels);
            if (inputs == null) Layout.Diagnostics.Add(new MarimekkoDiagnostic(MarimekkoSeverity.Error, "Nenhuma categoria (ramo) em Values."));
            foreach (var d in diag.Concat(Layout.Diagnostics))
            {
                var lvl = d.Severity == MarimekkoSeverity.Error ? GH_RuntimeMessageLevel.Error
                        : d.Severity == MarimekkoSeverity.Warning ? GH_RuntimeMessageLevel.Warning : GH_RuntimeMessageLevel.Remark;
                AddRuntimeMessage(lvl, d.Message);
            }
            if (diag.Any(d => d.Severity == MarimekkoSeverity.Error) && Layout.IsValid)
                Layout = new MarimekkoLayout();   // erros de entrada (larguras) invalidam o gráfico: nada de geometria parcial

            Style = new MarimekkoStyle
            {
                Title = (title ?? "").Trim(),
                LabelMode = LabelMode, ShowLegend = ShowLegend,
                ShowCategoryLabels = ShowCategoryLabels, ShowCategoryShare = ShowCategoryShare,
                Palette = ResolvePalette(colorsRaw)
            };

            var cells = new GH_Structure<GH_Rectangle>();
            var segShare = new GH_Structure<GH_Number>();
            var areaShare = new GH_Structure<GH_Number>();
            var catShare = new List<GH_Number>();
            var report = new System.Text.StringBuilder();

            if (Layout.IsValid)
            {
                foreach (var cat in Layout.Categories)
                {
                    var path = new GH_Path(cat.Index);
                    catShare.Add(new GH_Number(cat.NormalizedWidth));
                    foreach (var c in cat.Cells)
                    {
                        cells.Append(new GH_Rectangle(CellRectangle(c)), path);
                        segShare.Append(new GH_Number(c.NormalizedHeight), path);
                        areaShare.Append(new GH_Number(c.RelativeArea), path);
                    }
                }
                BuildViewportCache();
                Message = $"{Layout.Categories.Count} × {Layout.Segments.Count}";
            }
            else Message = "Entrada inválida";

            report.AppendLine("=== MARIMEKKO CHART ===");
            if (Layout.IsValid)
            {
                report.AppendLine($"Categorias: {Layout.Categories.Count} | Segmentos: {Layout.Segments.Count} | Células: {Layout.Cells.Count}");
                report.AppendLine($"Soma das larguras (valor): {Layout.TotalWidthValue:0.###}");
                report.AppendLine($"Invariantes: Σ larguras normalizadas = 1 e Σ alturas por categoria = 1 (erro máx. {Layout.MaxInvariantError():E2}); área geométrica = largura × altura (erro máx. {Layout.MaxAreaError():E2})");
                report.AppendLine("Valores mantidos separados: Category Share (largura) | Segment Share (dentro da categoria) | Cell Area Share (= produto).");
                foreach (var cat in Layout.Categories)
                    report.AppendLine($"  {cat.Label}: largura {cat.WidthValue:0.###} ({cat.NormalizedWidth * 100:0.##}% do total), total da composição {cat.CategoryTotal:0.###}");
            }
            foreach (var d in diag.Concat(Layout.Diagnostics)) report.AppendLine(d.ToString());

            // ---- exportação: somente quando solicitada (borda de subida), nunca altera o modelo ----
            var files = new List<string>();
            if (trigger && !_lastTrigger && Layout.IsValid)
            {
                files = ExportAll(out string err);
                if (!string.IsNullOrEmpty(err)) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Exportação: " + err);
                LastExportMessage = files.Count > 0 ? $"{files.Count} arquivo(s) exportado(s)" : "";
            }
            _lastTrigger = trigger;

            DA.SetDataTree(0, cells);
            if (Layout.IsValid) DA.SetData(1, new Rectangle3d(_plane, new Interval(0, _chartW), new Interval(0, _chartH)));
            DA.SetDataList(2, catShare);
            DA.SetDataTree(3, segShare);
            DA.SetDataTree(4, areaShare);
            DA.SetData(5, report.ToString());
            DA.SetDataList(6, files);
        }

        // ---- entrada: preserva a árvore, sem Flatten; identidade estável = caminho do ramo ----
        private List<MarimekkoCategoryInput> BuildInputs(GH_Structure<IGH_Goo> tree, List<IGH_Goo> widthsRaw, List<string> catLabels, List<MarimekkoDiagnostic> diag)
        {
            if (tree == null || tree.IsEmpty || tree.PathCount == 0) return null;

            var list = new List<MarimekkoCategoryInput>();
            int idx = 0;
            foreach (var path in tree.Paths)
            {
                var branch = tree.get_Branch(path);
                var cat = new MarimekkoCategoryInput
                {
                    Id = path.ToString(),
                    Label = (idx < catLabels.Count && !string.IsNullOrWhiteSpace(catLabels[idx])) ? catLabels[idx].Trim() : $"Categoria {idx + 1}"
                };
                if (branch != null)
                    foreach (var item in branch) cat.SegmentValues.Add(ToDouble(item as IGH_Goo));
                list.Add(cat);
                idx++;
            }
            if (catLabels.Count > list.Count)
                diag.Add(new MarimekkoDiagnostic(MarimekkoSeverity.Info, $"Category Labels tem {catLabels.Count} itens para {list.Count} categoria(s): excedentes ignorados."));

            int n = list.Count;
            if (widthsRaw.Count == 0) return list;                         // largura = total da categoria
            if (widthsRaw.Count == 1)                                       // 1 valor vale para todas (larguras iguais)
            {
                double w = ToDouble(widthsRaw[0]);
                foreach (var c in list) c.WidthValue = w;
            }
            else if (widthsRaw.Count >= n)
            {
                for (int i = 0; i < n; i++) list[i].WidthValue = ToDouble(widthsRaw[i]);
                if (widthsRaw.Count > n)
                    diag.Add(new MarimekkoDiagnostic(MarimekkoSeverity.Warning, $"Category Widths tem {widthsRaw.Count} itens para {n} categoria(s): excedentes ignorados."));
            }
            else
            {
                diag.Add(new MarimekkoDiagnostic(MarimekkoSeverity.Error,
                    $"Category Widths tem {widthsRaw.Count} itens para {n} categorias. Use 1 item (todas iguais), exatamente {n}, ou deixe vazio (largura = total da categoria)."));
            }
            return list;
        }

        private static double ToDouble(IGH_Goo goo)
        {
            if (goo == null) return double.NaN;
            if (GH_Convert.ToDouble(goo, out double v, GH_Conversion.Both)) return v;
            return double.NaN;
        }

        private static void ParseSize(IGH_Goo goo, out double w, out double h)
        {
            w = 10.0; h = 6.0;
            if (goo == null) return;
            object raw = goo.ScriptVariable();
            double a = double.NaN, b = double.NaN;
            if (raw is double d) { a = d; b = d * 0.6; }
            else if (raw is int i) { a = i; b = i * 0.6; }
            else if (raw is Vector3d vec) { a = vec.X; b = vec.Y; }
            else if (raw is Point3d pt) { a = pt.X; b = pt.Y; }
            else if (raw is string s)
            {
                var parts = s.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1 && double.TryParse(parts[0].Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double p0)) { a = p0; b = p0 * 0.6; }
                if (parts.Length >= 2 && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double p1)) b = p1;
            }
            if (a > 1e-9 && b > 1e-9 && !double.IsInfinity(a) && !double.IsInfinity(b)) { w = a; h = b; }
        }

        private static List<Color> ResolvePalette(List<IGH_Goo> raw)
        {
            if (raw == null || raw.Count == 0) return new List<Color>(Chart3DColumn_Component.DefaultPalette);
            var parsed = Chart3DColumn_Component.ParseColors(raw.Cast<object>().ToList());
            return parsed ?? new List<Color>(Chart3DColumn_Component.DefaultPalette);
        }

        // ==========================================================================
        // GEOMETRIA (um layout → rectângulos / viewport)
        // ==========================================================================
        private Point3d P(double nx, double ny) => _plane.PointAt(nx * _chartW, ny * _chartH);

        private Rectangle3d CellRectangle(MarimekkoCell c)
        {
            var origin = P(c.X0, c.Y0);
            var pl = new Plane(origin, _plane.XAxis, _plane.YAxis);
            return new Rectangle3d(pl, (c.X1 - c.X0) * _chartW, (c.Y1 - c.Y0) * _chartH);
        }

        private void BuildViewportCache()
        {
            var mesh = new Mesh();
            foreach (var c in Layout.Cells)
            {
                int v0 = mesh.Vertices.Count;
                mesh.Vertices.Add(P(c.X0, c.Y0)); mesh.Vertices.Add(P(c.X1, c.Y0));
                mesh.Vertices.Add(P(c.X1, c.Y1)); mesh.Vertices.Add(P(c.X0, c.Y1));
                mesh.Faces.AddFace(v0, v0 + 1, v0 + 2, v0 + 3);
                Color col = MarimekkoText.SegmentColor(c.SegmentIndex, Style.Palette);
                for (int k = 0; k < 4; k++) mesh.VertexColors.Add(col);

                _vpLines.Add(new Line(P(c.X0, c.Y0), P(c.X1, c.Y0)));
                _vpLines.Add(new Line(P(c.X1, c.Y0), P(c.X1, c.Y1)));
                _vpLines.Add(new Line(P(c.X1, c.Y1), P(c.X0, c.Y1)));
                _vpLines.Add(new Line(P(c.X0, c.Y1), P(c.X0, c.Y0)));
            }

            // legenda: amostras na malha (cor), textos 2D
            if (Style.ShowLegend)
            {
                double sw = 0.03, pitch = 0.055;
                for (int k = 0; k < Layout.Segments.Count; k++)
                {
                    var seg = Layout.Segments[Layout.Segments.Count - 1 - k];
                    double y1 = 1.0 - k * pitch, y0 = y1 - 0.035;
                    double x0 = 1.03, x1 = 1.03 + sw * (_chartH / _chartW);
                    int v0 = mesh.Vertices.Count;
                    mesh.Vertices.Add(P(x0, y0)); mesh.Vertices.Add(P(x1, y0)); mesh.Vertices.Add(P(x1, y1)); mesh.Vertices.Add(P(x0, y1));
                    mesh.Faces.AddFace(v0, v0 + 1, v0 + 2, v0 + 3);
                    Color col = MarimekkoText.SegmentColor(seg.Index, Style.Palette);
                    for (int q = 0; q < 4; q++) mesh.VertexColors.Add(col);
                    _vpLegendLabels.Add((seg.Label, P(x1 + 0.008, (y0 + y1) * 0.5)));
                }
            }
            mesh.Normals.ComputeNormals();
            _vpMesh = mesh;

            // limite geral
            _vpLines.Add(new Line(P(0, 0), P(1, 0))); _vpLines.Add(new Line(P(1, 0), P(1, 1)));
            _vpLines.Add(new Line(P(1, 1), P(0, 1))); _vpLines.Add(new Line(P(0, 1), P(0, 0)));

            if (Style.ShowCategoryLabels)
                foreach (var cat in Layout.Categories)
                    _vpCategoryLabels.Add((cat.Label, P((cat.X0 + cat.X1) * 0.5, -0.04), true));

            _clip = mesh.GetBoundingBox(true);
            _clip.Union(P(0, -0.12)); _clip.Union(P(1.25, 1.0)); _clip.Union(P(0, 1.06));
        }

        // ==========================================================================
        // VIEWPORT (DisplayPipeline; nada é adicionado ao documento do Rhino)
        // ==========================================================================
        public override BoundingBox ClippingBox => _clip.IsValid ? _clip : base.ClippingBox;

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (!ShowViewportPreview || _vpMesh == null || Hidden || Locked) return;
            args.Display.DrawMeshFalseColors(_vpMesh);
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (!ShowViewportPreview || _vpMesh == null || Hidden || Locked || Layout == null || !Layout.IsValid) return;

            bool selected = Attributes != null && Attributes.Selected;
            Color wire = selected ? args.WireColour_Selected : Color.FromArgb(235, 255, 255, 255);
            args.Display.DrawLines(_vpLines, wire, 1);

            Color ink = ViewportInk();
            foreach (var (text, pos, _) in _vpCategoryLabels)
                args.Display.Draw2dText(text, ink, pos, true, 11);
            foreach (var (text, pos) in _vpLegendLabels)
                args.Display.Draw2dText(text, ink, pos, false, 11);

            if (!string.IsNullOrWhiteSpace(Style.Title))
                args.Display.Draw2dText(Style.Title, ink, P(0.0, 1.06), false, 13);

            // rótulos internos: só quando a célula tem espaço na tela (mesma regra do canvas/SVG)
            if (Style.LabelMode != MarimekkoLabelMode.None)
            {
                foreach (var c in Layout.Cells)
                {
                    var a = args.Viewport.WorldToClient(P(c.X0, c.Y0));
                    var b = args.Viewport.WorldToClient(P(c.X1, c.Y1));
                    var px = new RectangleF((float)Math.Min(a.X, b.X), (float)Math.Min(a.Y, b.Y), (float)Math.Abs(b.X - a.X), (float)Math.Abs(b.Y - a.Y));
                    var lines = MarimekkoText.VisibleCellLines(c, px, Style.LabelMode, 11f);
                    if (lines.Count == 0) continue;
                    var fill = MarimekkoText.SegmentColor(c.SegmentIndex, Style.Palette);
                    args.Display.Draw2dText(string.Join("\n", lines), MarimekkoText.Ink(fill), P((c.X0 + c.X1) * 0.5, (c.Y0 + c.Y1) * 0.5), true, 11);
                }
            }
        }

        private static Color ViewportInk()
        {
            try
            {
                var bg = Rhino.ApplicationSettings.AppearanceSettings.ViewportBackgroundColor;
                return (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) < 115 ? Color.FromArgb(235, 240, 245) : Color.FromArgb(20, 28, 38);
            }
            catch { return Color.FromArgb(20, 28, 38); }
        }

        // ==========================================================================
        // EXPORTAÇÃO (somente leitura sobre Layout/Style)
        // ==========================================================================
        private string ResolveFolder()
        {
            string f = ExportFolder;
            if (string.IsNullOrWhiteSpace(f) || !Directory.Exists(f)) f = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            return f;
        }

        private string BaseName() => "Marimekko_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

        public string ExportPng(string baseName, out string error)
        {
            error = null;
            if (Layout == null || !Layout.IsValid) { error = "Sem gráfico válido."; return null; }
            try
            {
                string path = Path.Combine(ResolveFolder(), baseName + ".png");
                using (var bmp = MarimekkoGdi.RenderPng(Layout, Style, 2400, 1440)) bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                return path;
            }
            catch (Exception ex) { error = ex.Message; return null; }
        }

        public string ExportSvg(string baseName, out string error)
        {
            error = null;
            if (Layout == null || !Layout.IsValid) { error = "Sem gráfico válido."; return null; }
            try
            {
                string path = Path.Combine(ResolveFolder(), baseName + ".svg");
                File.WriteAllText(path, MarimekkoExport.BuildSvg(Layout, Style), new System.Text.UTF8Encoding(false));
                return path;
            }
            catch (Exception ex) { error = ex.Message; return null; }
        }

        public string ExportCsv(string baseName, out string error)
        {
            error = null;
            if (Layout == null || !Layout.IsValid) { error = "Sem gráfico válido."; return null; }
            try
            {
                string path = Path.Combine(ResolveFolder(), baseName + ".csv");
                File.WriteAllText(path, MarimekkoExport.BuildCsv(Layout), new System.Text.UTF8Encoding(true));
                return path;
            }
            catch (Exception ex) { error = ex.Message; return null; }
        }

        public string ExportPdf(string baseName, out string error)
        {
            error = null;
            if (Layout == null || !Layout.IsValid) { error = "Sem gráfico válido."; return null; }
            if (!GlauxVectorPdf.IsAvailable) { error = "PDF vetorial exige o Microsoft Edge ou o Google Chrome instalado."; return null; }
            string path = Path.Combine(ResolveFolder(), baseName + ".pdf");
            string svg = MarimekkoExport.BuildSvg(Layout, Style);
            if (GlauxVectorPdf.TryPrintSvgToPdf(svg, 1200, 720, path)) return path;
            error = "Falha ao gerar o PDF."; return null;
        }

        public List<string> ExportAll(out string error)
        {
            var files = new List<string>(); var errs = new List<string>();
            string bn = BaseName();
            foreach (var step in new Func<string>[]
            {
                () => ExportPng(bn, out string e) ?? Fail(errs, e),
                () => ExportSvg(bn, out string e) ?? Fail(errs, e),
                () => ExportCsv(bn, out string e) ?? Fail(errs, e),
                () => AlsoExportPdf ? (ExportPdf(bn, out string e) ?? Fail(errs, e)) : null
            })
            {
                string p = step();
                if (!string.IsNullOrEmpty(p)) files.Add(p);
            }
            error = errs.Count > 0 ? string.Join(" | ", errs) : "";
            return files;
        }

        private static string Fail(List<string> errs, string e) { if (!string.IsNullOrEmpty(e)) errs.Add(e); return null; }

        // ==========================================================================
        // MENU + SERIALIZAÇÃO
        // ==========================================================================
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var mode = Menu_AppendItem(menu, "Rótulos das células");
            void AddMode(string text, MarimekkoLabelMode m) =>
                Menu_AppendItem(mode.DropDown, text, (s, e) => { RecordUndoEvent("Marimekko label mode"); LabelMode = m; ExpireSolution(true); }, true, LabelMode == m);
            AddMode("Nenhum", MarimekkoLabelMode.None);
            AddMode("Segmento", MarimekkoLabelMode.Label);
            AddMode("Valor", MarimekkoLabelMode.Value);
            AddMode("% do segmento na categoria", MarimekkoLabelMode.Percent);
            AddMode("Segmento + %", MarimekkoLabelMode.LabelPercent);

            void Toggle(string text, Func<bool> get, Action<bool> set) =>
                Menu_AppendItem(menu, text, (s, e) => { RecordUndoEvent("Marimekko " + text); set(!get()); ExpireSolution(true); }, true, get());
            Toggle("Legenda dos segmentos", () => ShowLegend, v => ShowLegend = v);
            Toggle("Rótulos das categorias", () => ShowCategoryLabels, v => ShowCategoryLabels = v);
            Toggle("% do total sob a categoria (largura)", () => ShowCategoryShare, v => ShowCategoryShare = v);
            Menu_AppendSeparator(menu);
            Toggle("Gráfico no canvas do Grasshopper", () => ShowCanvasChart, v => ShowCanvasChart = v);
            Toggle("Preview no viewport do Rhino", () => ShowViewportPreview, v => ShowViewportPreview = v);
            Toggle("Exportar também PDF (Edge/Chrome)", () => AlsoExportPdf, v => AlsoExportPdf = v);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetBoolean("ShowCanvasChart", ShowCanvasChart);
            writer.SetBoolean("ShowViewportPreview", ShowViewportPreview);
            writer.SetBoolean("AlsoExportPdf", AlsoExportPdf);
            writer.SetInt32("LabelMode", (int)LabelMode);
            writer.SetBoolean("ShowLegend", ShowLegend);
            writer.SetBoolean("ShowCategoryLabels", ShowCategoryLabels);
            writer.SetBoolean("ShowCategoryShare", ShowCategoryShare);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("ShowCanvasChart")) ShowCanvasChart = reader.GetBoolean("ShowCanvasChart");
            if (reader.ItemExists("ShowViewportPreview")) ShowViewportPreview = reader.GetBoolean("ShowViewportPreview");
            if (reader.ItemExists("AlsoExportPdf")) AlsoExportPdf = reader.GetBoolean("AlsoExportPdf");
            if (reader.ItemExists("LabelMode")) LabelMode = (MarimekkoLabelMode)reader.GetInt32("LabelMode");
            if (reader.ItemExists("ShowLegend")) ShowLegend = reader.GetBoolean("ShowLegend");
            if (reader.ItemExists("ShowCategoryLabels")) ShowCategoryLabels = reader.GetBoolean("ShowCategoryLabels");
            if (reader.ItemExists("ShowCategoryShare")) ShowCategoryShare = reader.GetBoolean("ShowCategoryShare");
            return base.Read(reader);
        }
    }

    // ==============================================================================
    // CANVAS: mesmo painel escuro dos demais gráficos Glaux, desenhando o MESMO layout
    // ==============================================================================
    public class Marimekko_Attributes : GH_ComponentAttributes
    {
        private const int GRAPH_WIDTH = 460;
        private const int GRAPH_HEIGHT = 300;
        private RectangleF _btnPng, _btnSvg, _btnCsv, _chartRect;

        public Marimekko_Attributes(Marimekko_Component owner) : base(owner) { }

        private Marimekko_Component Comp => Owner as Marimekko_Component;

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            var comp = Comp;
            if (comp != null && comp.ShowCanvasChart && e.Button == MouseButtons.Left)
            {
                string bn = "Marimekko_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string path = null, err = null;
                if (_btnPng.Contains(e.CanvasLocation)) path = comp.ExportPng(bn, out err);
                else if (_btnSvg.Contains(e.CanvasLocation)) path = comp.ExportSvg(bn, out err);
                else if (_btnCsv.Contains(e.CanvasLocation)) path = comp.ExportCsv(bn, out err);
                else return base.RespondToMouseDown(sender, e);

                if (!string.IsNullOrEmpty(path))
                {
                    comp.LastExportMessage = path;
                    comp.Message = "Exportado!";
                    Rhino.RhinoApp.WriteLine($"[Marimekko] Exportado: {path}");
                    sender.Refresh();
                }
                else if (!string.IsNullOrEmpty(err)) comp.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Exportação: " + err);
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseDown(sender, e);
        }

        protected override void Layout()
        {
            base.Layout();
            var comp = Comp;
            if (comp == null || !comp.ShowCanvasChart) return;
            float oldRight = Bounds.Right;
            RectangleF b = Bounds;
            b.Width = Math.Max(b.Width, GRAPH_WIDTH + 24);
            b.Height += GRAPH_HEIGHT + 18;
            Bounds = b;

            float dx = Bounds.Right - oldRight;
            if (Math.Abs(dx) > 0.5f && Owner.Params?.Output != null)
            {
                foreach (var p in Owner.Params.Output)
                {
                    if (p.Attributes == null) continue;
                    var pb = p.Attributes.Bounds; pb.X += dx; p.Attributes.Bounds = pb;
                    var piv = p.Attributes.Pivot; piv.X += dx; p.Attributes.Pivot = piv;
                }
            }
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            var comp = Comp;
            bool draw = comp != null && comp.ShowCanvasChart;
            if (channel == GH_CanvasChannel.Objects && draw)
            {
                var saved = Pivot;
                Pivot = new PointF(Bounds.X + Bounds.Width / 2f, saved.Y);
                base.Render(canvas, graphics, channel);
                Pivot = saved;
            }
            else base.Render(canvas, graphics, channel);

            if (channel != GH_CanvasChannel.Objects || !draw) return;

            RectangleF b = Bounds;
            var panel = new RectangleF(b.X + 12, b.Bottom - GRAPH_HEIGHT - 10, b.Width - 24, GRAPH_HEIGHT);
            var header = new RectangleF(panel.X, panel.Y, panel.Width, 24);
            _chartRect = new RectangleF(panel.X + 6, header.Bottom + 4, panel.Width - 12, panel.Height - header.Height - 10);

            using (var bg = new SolidBrush(Color.FromArgb(14, 17, 22))) graphics.FillRectangle(bg, panel);
            using (var pen = new Pen(Color.FromArgb(65, 72, 85), 1.2f)) graphics.DrawRectangle(pen, panel.X, panel.Y, panel.Width, panel.Height);
            using (var hb = new SolidBrush(Color.FromArgb(30, 34, 43))) graphics.FillRectangle(hb, header);

            using (var f = new Font(GH_FontServer.Standard.FontFamily, 8f, FontStyle.Bold))
            using (var tb = new SolidBrush(Color.FromArgb(240, 243, 248)))
            {
                string t = string.IsNullOrWhiteSpace(comp.Style?.Title) ? "Marimekko" : comp.Style.Title;
                if (t.Length > 30) t = t.Substring(0, 27) + "...";
                graphics.DrawString(t, f, tb, header.X + 8, header.Y + 5);
            }

            // botões de exportação (cada um grava só o seu formato)
            float bw = 38f, bh = 16f, by = header.Y + 4f, bx = header.Right - 3 * bw - 12f;
            _btnPng = new RectangleF(bx, by, bw, bh); _btnSvg = new RectangleF(bx + bw + 4, by, bw, bh); _btnCsv = new RectangleF(bx + 2 * (bw + 4), by, bw, bh);
            using (var bb = new SolidBrush(Color.FromArgb(40, 48, 62)))
            using (var bp = new Pen(Color.FromArgb(80, 92, 110), 1f))
            using (var bf = new Font(GH_FontServer.Standard.FontFamily, 7f, FontStyle.Bold))
            using (var bt = new SolidBrush(Color.FromArgb(180, 210, 245)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                foreach (var (r, txt) in new[] { (_btnPng, "PNG"), (_btnSvg, "SVG"), (_btnCsv, "CSV") })
                {
                    graphics.FillRectangle(bb, r);
                    graphics.DrawRectangle(bp, r.X, r.Y, r.Width, r.Height);
                    graphics.DrawString(txt, bf, bt, r, sf);
                }
            }

            // o gráfico em si: MESMO layout/estilo usados no viewport e na exportação
            var clip = graphics.Clip;
            graphics.SetClip(_chartRect);
            MarimekkoGdi.Draw(graphics, _chartRect, comp.Layout, comp.Style, dark: true);
            graphics.Clip = clip;
        }
    }
}
