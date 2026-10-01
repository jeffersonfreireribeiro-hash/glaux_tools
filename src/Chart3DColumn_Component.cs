using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Display;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Modo de colorização do gráfico 3D de colunas.
    /// </summary>
    public enum Chart3DColorMode
    {
        /// <summary>Cada série/ramo Y recebe uma cor distinta da paleta Glaux.</summary>
        SeriesPalette = 0,
        /// <summary>Gradiente contínuo de cor mapeado pela altura Z (Min a Max).</summary>
        ValueGradient = 1
    }

    /// <summary>
    /// Representa uma célula de dados tridimensional no gráfico.
    /// </summary>
    public class Chart3DCell
    {
        public int XIndex { get; set; }
        public int YIndex { get; set; }
        public GH_Path BranchPath { get; set; }
        public double RawValue { get; set; }
        public double DisplayHeight { get; set; }
        public string XLabel { get; set; }
        public string YLabel { get; set; }
        public Color CellColor { get; set; }
        public Box ColumnBox { get; set; }
        public Point3d TopCenter { get; set; }
        public Point3d BaseCenter { get; set; }
    }

    /// <summary>
    /// Componente para visualização de dados através de gráfico tridimensional de colunas e grid no Rhino/Grasshopper.
    /// Suporta modo linear (1D) e matriz/grade (2D Data Tree), com valores positivos e negativos, escala, rótulos e preview fluido.
    /// Glaux Tools - Categoria "Visual"
    /// </summary>
    public class Chart3DColumn_Component : GH_Component
    {
        // ==========================================
        // PALETAS OFICIAIS DO GLAUX TOOLS
        // ==========================================
        public static readonly Color[] DefaultPalette = new Color[]
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

        // ==========================================
        // CACHE DE GEOMETRIA E DISPLAY NO VIEWPORT
        // ==========================================
        private Mesh _cachedMesh;
        private BoundingBox _cachedBbox = BoundingBox.Empty;
        private readonly List<Line> _cachedGridLines = new List<Line>();
        private Line _cachedAxisX, _cachedAxisY, _cachedAxisZ;
        private readonly List<Chart3DCell> _cachedCells = new List<Chart3DCell>();
        private readonly List<(string Text, Point3d Position)> _cachedXLabels = new List<(string, Point3d)>();
        private readonly List<(string Text, Point3d Position)> _cachedYLabels = new List<(string, Point3d)>();
        private readonly List<(string Text, Point3d Position, Color Color)> _cachedValLabels = new List<(string, Point3d, Color)>();

        // ==========================================
        // CONFIGURAÇÕES INTERATIVAS E SERIALIZADAS
        // ==========================================
        public bool ShowBaseGrid = true;
        public bool ShowAxes = true;
        public bool ShowCategoryLabels = true;
        public bool ShowValueLabels = false;
        public bool ShowWires = true;
        public Chart3DColorMode ColorMode = Chart3DColorMode.SeriesPalette;

        public Chart3DColumn_Component()
            : base(
                "3D Column & Grid Chart",
                "Chart3DCol",
                "Visualizador tridimensional de dados em colunas ou grade matricial no Rhino/Grasshopper.\n" +
                "- 1D Mode: lista linear de valores mapeada ao longo do eixo X;\n" +
                "- 2D Grid Mode: árvore de dados (Data Tree) ou matriz onde cada ramo representa uma série no eixo Y e cada item uma categoria no eixo X;\n" +
                "- Suporta valores positivos e negativos em relação ao plano base (Z = 0), escala de exibição, rótulos X/Y/Valor e paletas oficiais do Glaux Tools.",
                "Glaux Tools",
                "Visual")
        {
        }

        public override Guid ComponentGuid => new Guid("8e2b1c4a-9d3f-4a7b-b5c6-1e0f2a3b4c5d");

        protected override Bitmap Icon => GlauxToolsIcons.Chart3DColumn;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0: Values (V)
            pManager.AddGenericParameter(
                "Values", "V",
                "Valores numéricos de altura/elevação Z.\n" +
                "Aceita:\n" +
                "1. Lista linear de números: gera gráfico de colunas 1D ao longo do eixo X;\n" +
                "2. Árvore de dados (Data Tree) com múltiplos ramos: gera grade/matriz tridimensional (cada ramo é uma série em Y e cada item uma categoria em X).",
                GH_ParamAccess.tree);

            // 1: Base Plane (P)
            pManager.AddPlaneParameter(
                "Base Plane", "P",
                "Plano de referência para o gráfico tridimensional (padrão: World XY).\n" +
                "O plano define a origem (0, 0, 0), a direção X das categorias, a direção Y das séries e a direção Z das alturas.",
                GH_ParamAccess.item,
                Plane.WorldXY);
            pManager[1].Optional = true;

            // 2: Scale (S)
            pManager.AddNumberParameter(
                "Scale", "S",
                "Fator multiplicador de escala da altura de exibição (Display Height = Value * Scale).\n" +
                "O valor numérico original é preservado nos rótulos e relatórios. Padrão: 1.0.",
                GH_ParamAccess.item,
                1.0);
            pManager[2].Optional = true;

            // 3: Column Size (Size)
            pManager.AddGenericParameter(
                "Column Size", "Size",
                "Dimensões da seção transversal da coluna.\n" +
                "Aceita:\n" +
                "- Número único (ex: 1.0) para colunas de base quadrada (SizeX = SizeY);\n" +
                "- Texto ou Vetor (ex: '1.5, 2.0') para larguras distintas em X e Y.\n" +
                "Padrão: 1.0.",
                GH_ParamAccess.item);
            pManager[3].Optional = true;

            // 4: Spacing / Gap (Gap)
            pManager.AddGenericParameter(
                "Spacing / Gap", "Gap",
                "Espaçamento/folga entre colunas adjacentes.\n" +
                "Aceita:\n" +
                "- Número único (ex: 0.5) para espaçamento uniforme;\n" +
                "- Texto ou Vetor (ex: '0.5, 1.0') para folgas distintas em X e Y.\n" +
                "Padrão: 0.5.",
                GH_ParamAccess.item);
            pManager[4].Optional = true;

            // 5: X Labels (XLab)
            pManager.AddTextParameter(
                "X Labels", "XLab",
                "Rótulos das categorias no eixo X (ex: '125 Hz', '250 Hz', 'Jan', 'Fev'...). Se omitido, adota os índices numéricos 0, 1, 2...",
                GH_ParamAccess.list);
            pManager[5].Optional = true;

            // 6: Y Labels (YLab)
            pManager.AddTextParameter(
                "Y Labels", "YLab",
                "Rótulos das séries/linhas no eixo Y (ex: 'Paredes', 'Teto', '2024', '2025'...). Se omitido, adota os caminhos dos ramos {0}, {1}... ou índices 0, 1, 2...",
                GH_ParamAccess.list);
            pManager[6].Optional = true;

            // 7: Colors / Palette (Col)
            pManager.AddGenericParameter(
                "Colors / Palette", "Col",
                "Esquema de cores das colunas.\n" +
                "Aceita:\n" +
                "- Lista de cores (Color, Swatch ou Gradient do GH);\n" +
                "- Nome de paleta ('Glaux', 'Turbo', 'Viridis', 'Jet', 'CoolWarm');\n" +
                "- Se omitido, utiliza a paleta padrão de alto contraste do Glaux Tools.",
                GH_ParamAccess.list);
            pManager[7].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // 0: Columns Mesh (M)
            pManager.AddMeshParameter(
                "Columns Mesh", "M",
                "Malha 3D única unificada (Rhino Mesh) com vertex colors e sombreamento suave de alto desempenho, pronta para visualização fluida e Bake.",
                GH_ParamAccess.item);

            // 1: Column Boxes (B)
            pManager.AddBoxParameter(
                "Column Boxes", "B",
                "Árvore de caixas orientadas (Rhino.Geometry.Box) correspondentes a cada coluna no espaço tridimensional.",
                GH_ParamAccess.tree);

            // 2: Base Grid (Grid)
            pManager.AddCurveParameter(
                "Base Grid", "Grid",
                "Linhas e molduras da grade no plano base (Rhino.Geometry.Line/Polyline) para referência visual e diagramação espacial.",
                GH_ParamAccess.list);

            // 3: Column Points (Pts)
            pManager.AddPointParameter(
                "Column Points", "Pts",
                "Árvore de pontos 3D centrais no topo de cada coluna (ou base para negativos), ideais para dimensões e textos líderes.",
                GH_ParamAccess.tree);

            // 4: Value Labels (Lbl)
            pManager.AddTextParameter(
                "Value Labels", "Lbl",
                "Árvore com os textos formatados de cada célula (X, Y, Valor original e Altura de exibição).",
                GH_ParamAccess.tree);

            // 5: Report (Rep)
            pManager.AddTextParameter(
                "Report", "Rep",
                "Relatório técnico com contagem total de colunas, dimensões da matriz (Nx, Ny), valores extremos (Mínimo, Máximo), média e configuração espacial.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Extração dos Valores (V)
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> vTree) || vTree == null || vTree.IsEmpty)
            {
                this.Message = "Sem Valores";
                ClearCache();
                return;
            }

            // 2. Extração do Plano Base (P)
            Plane basePlane = Plane.WorldXY;
            DA.GetData(1, ref basePlane);
            if (!basePlane.IsValid) basePlane = Plane.WorldXY;

            // 3. Fator de Escala (S)
            double scale = 1.0;
            DA.GetData(2, ref scale);
            if (double.IsNaN(scale) || double.IsInfinity(scale)) scale = 1.0;

            // 4. Dimensões das Colunas (SizeX, SizeY)
            double sizeX = 1.0, sizeY = 1.0;
            object rawSize = null;
            if (DA.GetData(3, ref rawSize) && rawSize != null)
            {
                Parse2DDimensions(rawSize, 1.0, 1.0, out sizeX, out sizeY);
            }
            if (sizeX <= 1e-6) sizeX = 1.0;
            if (sizeY <= 1e-6) sizeY = 1.0;

            // 5. Espaçamento / Gap (GapX, GapY)
            double gapX = 0.5, gapY = 0.5;
            object rawGap = null;
            if (DA.GetData(4, ref rawGap) && rawGap != null)
            {
                Parse2DDimensions(rawGap, 0.5, 0.5, out gapX, out gapY);
            }
            if (gapX < 0) gapX = 0;
            if (gapY < 0) gapY = 0;

            // 6. Rótulos X (Categorias)
            var inXLabels = new List<string>();
            DA.GetDataList(5, inXLabels);

            // 7. Rótulos Y (Séries)
            var inYLabels = new List<string>();
            DA.GetDataList(6, inYLabels);

            // 8. Cores e Paleta
            var rawColors = new List<object>();
            DA.GetDataList(7, rawColors);
            List<Color> customColors = ParseColors(rawColors);

            // ==========================================
            // INTERPRETAÇÃO DA ÁRVORE DE DADOS (1D vs 2D)
            // ==========================================
            int branchCount = vTree.PathCount;
            bool is1DMode = branchCount <= 1;

            int maxItemsInBranch = 0;
            for (int b = 0; b < branchCount; b++)
            {
                var branch = vTree.Branches[b];
                if (branch != null && branch.Count > maxItemsInBranch)
                {
                    maxItemsInBranch = branch.Count;
                }
            }

            if (maxItemsInBranch == 0)
            {
                this.Message = "Árvore Vazia";
                ClearCache();
                return;
            }

            int nx = maxItemsInBranch;
            int ny = is1DMode ? 1 : branchCount;

            this.Message = is1DMode ? $"1D ({nx} Cols)" : $"2D Grid ({nx}×{ny})";

            // Coleta e validação das células
            var cells = new List<Chart3DCell>();
            double minVal = double.MaxValue;
            double maxVal = double.MinValue;
            double sumVal = 0.0;
            int validCount = 0;

            for (int j = 0; j < branchCount; j++)
            {
                var path = vTree.Paths[j];
                var branch = vTree.Branches[j];
                if (branch == null || branch.Count == 0) continue;

                int yIndex = is1DMode ? 0 : j;
                string yLabel = (inYLabels != null && yIndex < inYLabels.Count && !string.IsNullOrWhiteSpace(inYLabels[yIndex]))
                    ? inYLabels[yIndex].Trim()
                    : (is1DMode ? "Série 0" : (path.Indices != null && path.Indices.Length > 0 ? $"{{{string.Join(";", path.Indices)}}}" : $"Y{yIndex}"));

                for (int i = 0; i < branch.Count; i++)
                {
                    var item = branch[i];
                    if (item == null) continue;

                    if (!TryExtractDouble(item, out double val))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Dado não numérico no ramo {path} índice [{i}] ignorado.");
                        continue;
                    }

                    if (double.IsNaN(val) || double.IsInfinity(val))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Valor NaN ou Infinito no ramo {path} índice [{i}] ignorado.");
                        continue;
                    }

                    int xIndex = i;
                    string xLabel = (inXLabels != null && xIndex < inXLabels.Count && !string.IsNullOrWhiteSpace(inXLabels[xIndex]))
                        ? inXLabels[xIndex].Trim()
                        : $"X{xIndex}";

                    double displayHeight = val * scale;
                    if (val < minVal) minVal = val;
                    if (val > maxVal) maxVal = val;
                    sumVal += val;
                    validCount++;

                    cells.Add(new Chart3DCell
                    {
                        XIndex = xIndex,
                        YIndex = yIndex,
                        BranchPath = path,
                        RawValue = val,
                        DisplayHeight = displayHeight,
                        XLabel = xLabel,
                        YLabel = yLabel
                    });
                }
            }

            if (validCount == 0)
            {
                this.Message = "Sem Números";
                ClearCache();
                return;
            }

            double avgVal = sumVal / validCount;
            if (minVal > maxVal) { minVal = 0; maxVal = 0; }

            // ==========================================
            // ATRIBUIÇÃO DE CORES ÀS CÉLULAS
            // ==========================================
            double valSpan = Math.Max(1e-9, maxVal - minVal);
            foreach (var cell in cells)
            {
                if (customColors != null && customColors.Count > 0)
                {
                    if (ColorMode == Chart3DColorMode.ValueGradient && customColors.Count > 1)
                    {
                        double t = Math.Max(0.0, Math.Min(1.0, (cell.RawValue - minVal) / valSpan));
                        cell.CellColor = SampleGradient(customColors, t);
                    }
                    else
                    {
                        // Paleta por série Y ou ciclo de cores
                        int colIdx = (is1DMode ? cell.XIndex : cell.YIndex) % customColors.Count;
                        cell.CellColor = customColors[colIdx];
                    }
                }
                else
                {
                    if (ColorMode == Chart3DColorMode.ValueGradient)
                    {
                        double t = Math.Max(0.0, Math.Min(1.0, (cell.RawValue - minVal) / valSpan));
                        cell.CellColor = SampleTurboGradient(t);
                    }
                    else
                    {
                        int colIdx = (is1DMode ? cell.XIndex : cell.YIndex) % DefaultPalette.Length;
                        cell.CellColor = DefaultPalette[colIdx];
                    }
                }
            }

            // ==========================================
            // CONSTRUÇÃO DA GEOMETRIA (MESH, BOXES, GRID)
            // ==========================================
            Mesh combinedMesh = new Mesh();
            var outBoxes = new GH_Structure<GH_Box>();
            var outPoints = new GH_Structure<GH_Point>();
            var outLabels = new GH_Structure<GH_String>();
            var gridCurves = new List<Curve>();

            BoundingBox chartBbox = BoundingBox.Empty;
            _cachedGridLines.Clear();
            _cachedXLabels.Clear();
            _cachedYLabels.Clear();
            _cachedValLabels.Clear();
            _cachedCells.Clear();
            _cachedCells.AddRange(cells);

            double pitchX = sizeX + gapX;
            double pitchY = sizeY + gapY;

            // 1. Constrói colunas (Malha e Caixas)
            foreach (var cell in cells)
            {
                double localX = cell.XIndex * pitchX;
                double localY = cell.YIndex * pitchY;
                double h = cell.DisplayHeight;

                // Caixa orientada
                Interval xInterval = new Interval(localX, localX + sizeX);
                Interval yInterval = new Interval(localY, localY + sizeY);
                Interval zInterval = h >= 0 ? new Interval(0, h) : new Interval(h, 0);
                Box box = new Box(basePlane, xInterval, yInterval, zInterval);
                cell.ColumnBox = box;

                // Centros das faces
                double cx = localX + sizeX * 0.5;
                double cy = localY + sizeY * 0.5;
                cell.BaseCenter = basePlane.PointAt(cx, cy, 0);
                cell.TopCenter = basePlane.PointAt(cx, cy, h);

                chartBbox.Union(box.BoundingBox);

                // Saída de Box e Point na árvore correspondente ao ramo original
                GH_Path cellPath = cell.BranchPath ?? new GH_Path(cell.YIndex);
                outBoxes.Append(new GH_Box(box), cellPath);
                outPoints.Append(new GH_Point(cell.TopCenter), cellPath);
                string lblText = $"[{cell.XLabel}, {cell.YLabel}]: {cell.RawValue:G6} (H: {cell.DisplayHeight:F2})";
                outLabels.Append(new GH_String(lblText), cellPath);

                // Cache de rótulo para desenho no Viewport
                string valShort = Math.Abs(cell.RawValue) >= 1000 ? cell.RawValue.ToString("0.0k", CultureInfo.InvariantCulture) : cell.RawValue.ToString("0.##", CultureInfo.InvariantCulture);
                _cachedValLabels.Add((valShort, cell.TopCenter, cell.CellColor));

                // Adiciona facetas da coluna à malha combinada
                AppendColumnToMesh(combinedMesh, basePlane, localX, localY, sizeX, sizeY, h, cell.CellColor);
            }

            combinedMesh.Normals.ComputeNormals();
            combinedMesh.Compact();
            _cachedMesh = combinedMesh;

            // 2. Constrói Grade Base e Moldura de Referência
            double totalSpanX = nx * pitchX - gapX;
            double totalSpanY = ny * pitchY - gapY;

            // Moldura geral do plano base
            Point3d p00 = basePlane.PointAt(0, 0, 0);
            Point3d p10 = basePlane.PointAt(totalSpanX, 0, 0);
            Point3d p11 = basePlane.PointAt(totalSpanX, totalSpanY, 0);
            Point3d p01 = basePlane.PointAt(0, totalSpanY, 0);

            var borderPoly = new Polyline(new Point3d[] { p00, p10, p11, p01, p00 });
            gridCurves.Add(borderPoly.ToNurbsCurve());
            _cachedGridLines.Add(new Line(p00, p10));
            _cachedGridLines.Add(new Line(p10, p11));
            _cachedGridLines.Add(new Line(p11, p01));
            _cachedGridLines.Add(new Line(p01, p00));

            // Pegadas individuais de cada célula na grade
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iy = 0; iy < ny; iy++)
                {
                    double lx = ix * pitchX;
                    double ly = iy * pitchY;
                    Point3d c0 = basePlane.PointAt(lx, ly, 0);
                    Point3d c1 = basePlane.PointAt(lx + sizeX, ly, 0);
                    Point3d c2 = basePlane.PointAt(lx + sizeX, ly + sizeY, 0);
                    Point3d c3 = basePlane.PointAt(lx, ly + sizeY, 0);

                    var cellPoly = new Polyline(new Point3d[] { c0, c1, c2, c3, c0 });
                    gridCurves.Add(cellPoly.ToNurbsCurve());

                    _cachedGridLines.Add(new Line(c0, c1));
                    _cachedGridLines.Add(new Line(c1, c2));
                    _cachedGridLines.Add(new Line(c2, c3));
                    _cachedGridLines.Add(new Line(c3, c0));
                }
            }

            // Eixos de Coordenadas (X = Vermelho/Ciano, Y = Verde/Laranja, Z = Azul)
            double axisLenX = totalSpanX + pitchX * 0.5;
            double axisLenY = totalSpanY + pitchY * 0.5;
            double axisLenZ = Math.Max(pitchX, Math.Max(Math.Abs(maxVal * scale), Math.Abs(minVal * scale))) * 1.2;

            _cachedAxisX = new Line(basePlane.Origin, basePlane.PointAt(axisLenX, 0, 0));
            _cachedAxisY = new Line(basePlane.Origin, basePlane.PointAt(0, axisLenY, 0));
            _cachedAxisZ = new Line(basePlane.Origin, basePlane.PointAt(0, 0, axisLenZ));

            gridCurves.Add(_cachedAxisX.ToNurbsCurve());
            gridCurves.Add(_cachedAxisY.ToNurbsCurve());
            gridCurves.Add(_cachedAxisZ.ToNurbsCurve());

            chartBbox.Union(_cachedAxisX.BoundingBox);
            chartBbox.Union(_cachedAxisY.BoundingBox);
            chartBbox.Union(_cachedAxisZ.BoundingBox);

            // 3. Cache dos Rótulos de Categoria (X e Y) no Viewport
            for (int ix = 0; ix < nx; ix++)
            {
                double lx = ix * pitchX + sizeX * 0.5;
                Point3d xPt = basePlane.PointAt(lx, -gapY * 1.2, 0);
                string xLabel = (inXLabels != null && ix < inXLabels.Count && !string.IsNullOrWhiteSpace(inXLabels[ix]))
                    ? inXLabels[ix].Trim()
                    : $"X{ix}";
                _cachedXLabels.Add((xLabel, xPt));
                chartBbox.Union(xPt);
            }

            if (!is1DMode)
            {
                for (int iy = 0; iy < ny; iy++)
                {
                    double ly = iy * pitchY + sizeY * 0.5;
                    Point3d yPt = basePlane.PointAt(-gapX * 1.2, ly, 0);
                    string yLabel = (inYLabels != null && iy < inYLabels.Count && !string.IsNullOrWhiteSpace(inYLabels[iy]))
                        ? inYLabels[iy].Trim()
                        : $"Y{iy}";
                    _cachedYLabels.Add((yLabel, yPt));
                    chartBbox.Union(yPt);
                }
            }

            _cachedBbox = chartBbox;

            // ==========================================
            // RELATÓRIO QUANTITATIVO E ESTATÍSTICO
            // ==========================================
            var sb = new StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine("GLAUX TOOLS — 3D COLUMN & GRID CHART");
            sb.AppendLine("========================================");
            sb.AppendLine($"Modo de Exibição   : {(is1DMode ? "1D Linear (Eixo X)" : "2D Grade Matricial (X × Y)")}");
            sb.AppendLine($"Total de Colunas   : {validCount}");
            sb.AppendLine($"Dimensões da Grade : X={nx} Categorias, Y={ny} Séries");
            sb.AppendLine("----------------------------------------");
            sb.AppendLine("ESTATÍSTICAS DOS VALORES (DADOS):");
            sb.AppendLine($"  Mínimo (Min)     : {minVal:G6}");
            sb.AppendLine($"  Máximo (Max)     : {maxVal:G6}");
            sb.AppendLine($"  Amplitude (Span) : {valSpan:G6}");
            sb.AppendLine($"  Média (Mean)     : {avgVal:G6}");
            sb.AppendLine("----------------------------------------");
            sb.AppendLine("GEOMETRIA & EXIBIÇÃO NO RHINO:");
            sb.AppendLine($"  Fator de Escala  : {scale:G4} (H = Valor × Escala)");
            sb.AppendLine($"  Altura Mínima (H): {(minVal * scale):F3}");
            sb.AppendLine($"  Altura Máxima (H): {(maxVal * scale):F3}");
            sb.AppendLine($"  Tamanho Coluna   : X={sizeX:F2}, Y={sizeY:F2}");
            sb.AppendLine($"  Espaçamento (Gap): X={gapX:F2}, Y={gapY:F2}");
            sb.AppendLine($"  Origem do Plano  : ({basePlane.Origin.X:F2}, {basePlane.Origin.Y:F2}, {basePlane.Origin.Z:F2})");
            sb.AppendLine($"  Vetor Normal     : ({basePlane.ZAxis.X:F3}, {basePlane.ZAxis.Y:F3}, {basePlane.ZAxis.Z:F3})");
            sb.AppendLine($"  Modo de Cores    : {ColorMode}");

            // 4. Atribuição das Saídas
            DA.SetData(0, combinedMesh);
            DA.SetDataTree(1, outBoxes);
            DA.SetDataList(2, gridCurves);
            DA.SetDataTree(3, outPoints);
            DA.SetDataTree(4, outLabels);
            DA.SetData(5, sb.ToString());
        }

        private void ClearCache()
        {
            _cachedMesh = null;
            _cachedBbox = BoundingBox.Empty;
            _cachedGridLines.Clear();
            _cachedXLabels.Clear();
            _cachedYLabels.Clear();
            _cachedValLabels.Clear();
            _cachedCells.Clear();
        }

        // ==========================================
        // RENDERIZAÇÃO NO RHINO VIEWPORT (IGH_PreviewObject)
        // ==========================================
        public override BoundingBox ClippingBox => _cachedBbox.IsValid ? _cachedBbox : base.ClippingBox;

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (this.Hidden || this.Locked || args.Document.PreviewMode == GH_PreviewMode.Disabled) return;
            if (_cachedMesh == null || !_cachedMesh.IsValid) return;

            args.Display.DrawMeshFalseColors(_cachedMesh);
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (this.Hidden || this.Locked || args.Document.PreviewMode == GH_PreviewMode.Disabled) return;

            bool isSelected = this.Attributes != null && this.Attributes.Selected;
            Color wireCol = isSelected ? args.WireColour_Selected : (ShowWires ? Color.FromArgb(70, 20, 25, 35) : Color.Transparent);

            // 1. Arestas das colunas
            if (ShowWires && _cachedMesh != null && _cachedMesh.IsValid)
            {
                args.Display.DrawMeshWires(_cachedMesh, wireCol, isSelected ? 2 : 1);
            }

            // 2. Grade de base
            if (ShowBaseGrid && _cachedGridLines.Count > 0)
            {
                Color gridCol = isSelected ? Color.FromArgb(200, args.WireColour_Selected) : Color.FromArgb(130, 95, 115, 135);
                foreach (var line in _cachedGridLines)
                {
                    args.Display.DrawLine(line, gridCol, 1);
                }
            }

            // 3. Eixos de Coordenadas
            if (ShowAxes)
            {
                if (_cachedAxisX.IsValid) args.Display.DrawLine(_cachedAxisX, Color.FromArgb(230, 235, 75, 75), 2);
                if (_cachedAxisY.IsValid) args.Display.DrawLine(_cachedAxisY, Color.FromArgb(230, 60, 200, 110), 2);
                if (_cachedAxisZ.IsValid) args.Display.DrawLine(_cachedAxisZ, Color.FromArgb(230, 50, 150, 255), 2);
            }

            // 4. Rótulos de Categoria (X e Y) no plano base
            if (ShowCategoryLabels)
            {
                Color labelCol = isSelected ? Color.FromArgb(255, 255, 255) : Color.FromArgb(215, 230, 245);
                foreach (var (text, pt) in _cachedXLabels)
                {
                    args.Display.Draw2dText(text, labelCol, pt, true, 11);
                }
                foreach (var (text, pt) in _cachedYLabels)
                {
                    args.Display.Draw2dText(text, labelCol, pt, true, 11);
                }
            }

            // 5. Rótulos de Valor (Z) sobre as colunas
            if (ShowValueLabels && _cachedValLabels.Count > 0)
            {
                foreach (var (text, pt, col) in _cachedValLabels)
                {
                    args.Display.Draw2dText(text, Color.FromArgb(240, 255, 255), pt, true, 10);
                }
            }
        }

        // ==========================================
        // MENU DE CONTEXTO E SERIALIZAÇÃO
        // ==========================================
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            Menu_AppendItem(menu, "Mostrar Grade Base (Grid)", (s, e) =>
            {
                RecordUndoEvent("Toggle Base Grid");
                ShowBaseGrid = !ShowBaseGrid;
                ExpireSolution(true);
            }, true, ShowBaseGrid);

            Menu_AppendItem(menu, "Mostrar Eixos 3D (X, Y, Z)", (s, e) =>
            {
                RecordUndoEvent("Toggle Axes");
                ShowAxes = !ShowAxes;
                ExpireSolution(true);
            }, true, ShowAxes);

            Menu_AppendItem(menu, "Mostrar Rótulos de Categoria (X/Y)", (s, e) =>
            {
                RecordUndoEvent("Toggle Category Labels");
                ShowCategoryLabels = !ShowCategoryLabels;
                ExpireSolution(true);
            }, true, ShowCategoryLabels);

            Menu_AppendItem(menu, "Mostrar Rótulos de Valor no Topo (Z)", (s, e) =>
            {
                RecordUndoEvent("Toggle Value Labels");
                ShowValueLabels = !ShowValueLabels;
                ExpireSolution(true);
            }, true, ShowValueLabels);

            Menu_AppendItem(menu, "Desenhar Arestas (Wireframe)", (s, e) =>
            {
                RecordUndoEvent("Toggle Wires");
                ShowWires = !ShowWires;
                ExpireSolution(true);
            }, true, ShowWires);

            Menu_AppendSeparator(menu);

            var miColorMode = Menu_AppendItem(menu, "Modo de Cores");
            Menu_AppendItem(miColorMode.DropDown, "Por Série / Ramo Y (Paleta Glaux)", (s, e) =>
            {
                RecordUndoEvent("ColorMode Series");
                ColorMode = Chart3DColorMode.SeriesPalette;
                ExpireSolution(true);
            }, true, ColorMode == Chart3DColorMode.SeriesPalette);

            Menu_AppendItem(miColorMode.DropDown, "Por Altura / Valor Z (Gradiente)", (s, e) =>
            {
                RecordUndoEvent("ColorMode Gradient");
                ColorMode = Chart3DColorMode.ValueGradient;
                ExpireSolution(true);
            }, true, ColorMode == Chart3DColorMode.ValueGradient);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetBoolean("ShowBaseGrid", ShowBaseGrid);
            writer.SetBoolean("ShowAxes", ShowAxes);
            writer.SetBoolean("ShowCategoryLabels", ShowCategoryLabels);
            writer.SetBoolean("ShowValueLabels", ShowValueLabels);
            writer.SetBoolean("ShowWires", ShowWires);
            writer.SetInt32("ColorMode", (int)ColorMode);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("ShowBaseGrid")) ShowBaseGrid = reader.GetBoolean("ShowBaseGrid");
            if (reader.ItemExists("ShowAxes")) ShowAxes = reader.GetBoolean("ShowAxes");
            if (reader.ItemExists("ShowCategoryLabels")) ShowCategoryLabels = reader.GetBoolean("ShowCategoryLabels");
            if (reader.ItemExists("ShowValueLabels")) ShowValueLabels = reader.GetBoolean("ShowValueLabels");
            if (reader.ItemExists("ShowWires")) ShowWires = reader.GetBoolean("ShowWires");
            if (reader.ItemExists("ColorMode")) ColorMode = (Chart3DColorMode)reader.GetInt32("ColorMode");
            return base.Read(reader);
        }

        // ==========================================
        // HELPERS GEOMÉTRICOS E MATEMÁTICOS
        // ==========================================

        private static void AppendColumnToMesh(Mesh mesh, Plane basePlane, double localX, double localY, double sizeX, double sizeY, double h, Color baseColor)
        {
            if (Math.Abs(h) < 1e-9)
            {
                // Valor Zero: desenha uma placa/face plana no plano base sem gerar geometria colapsada degenerada
                int v0 = mesh.Vertices.Count;
                Point3d p0 = basePlane.PointAt(localX, localY, 0);
                Point3d p1 = basePlane.PointAt(localX + sizeX, localY, 0);
                Point3d p2 = basePlane.PointAt(localX + sizeX, localY + sizeY, 0);
                Point3d p3 = basePlane.PointAt(localX, localY + sizeY, 0);

                mesh.Vertices.Add(p0);
                mesh.Vertices.Add(p1);
                mesh.Vertices.Add(p2);
                mesh.Vertices.Add(p3);

                Color zeroCol = Color.FromArgb(180, baseColor.R, baseColor.G, baseColor.B);
                mesh.VertexColors.Add(zeroCol);
                mesh.VertexColors.Add(zeroCol);
                mesh.VertexColors.Add(zeroCol);
                mesh.VertexColors.Add(zeroCol);

                mesh.Faces.AddFace(v0, v0 + 1, v0 + 2, v0 + 3);
                return;
            }

            int baseIdx = mesh.Vertices.Count;

            // 8 vértices no espaço do Rhino
            // 0..3: Base (Z=0)
            Point3d b0 = basePlane.PointAt(localX, localY, 0);
            Point3d b1 = basePlane.PointAt(localX + sizeX, localY, 0);
            Point3d b2 = basePlane.PointAt(localX + sizeX, localY + sizeY, 0);
            Point3d b3 = basePlane.PointAt(localX, localY + sizeY, 0);

            // 4..7: Topo (Z=h)
            Point3d t0 = basePlane.PointAt(localX, localY, h);
            Point3d t1 = basePlane.PointAt(localX + sizeX, localY, h);
            Point3d t2 = basePlane.PointAt(localX + sizeX, localY + sizeY, h);
            Point3d t3 = basePlane.PointAt(localX, localY + sizeY, h);

            mesh.Vertices.Add(b0); // 0
            mesh.Vertices.Add(b1); // 1
            mesh.Vertices.Add(b2); // 2
            mesh.Vertices.Add(b3); // 3

            mesh.Vertices.Add(t0); // 4
            mesh.Vertices.Add(t1); // 5
            mesh.Vertices.Add(t2); // 6
            mesh.Vertices.Add(t3); // 7

            // Variação de tonalidade sutil para sombreamento natural
            Color topCol = Lighten(baseColor, 0.15f);
            Color sideCol = baseColor;
            Color botCol = Darken(baseColor, 0.20f);

            // Vertex colors (base mais escura, topo mais claro)
            mesh.VertexColors.Add(h >= 0 ? botCol : topCol);
            mesh.VertexColors.Add(h >= 0 ? botCol : topCol);
            mesh.VertexColors.Add(h >= 0 ? botCol : topCol);
            mesh.VertexColors.Add(h >= 0 ? botCol : topCol);

            mesh.VertexColors.Add(h >= 0 ? topCol : botCol);
            mesh.VertexColors.Add(h >= 0 ? topCol : botCol);
            mesh.VertexColors.Add(h >= 0 ? topCol : botCol);
            mesh.VertexColors.Add(h >= 0 ? topCol : botCol);

            if (h > 0)
            {
                // Valores Positivos (cresce em +Z)
                // Base (normal -Z): 0, 3, 2, 1
                mesh.Faces.AddFace(baseIdx + 0, baseIdx + 3, baseIdx + 2, baseIdx + 1);
                // Topo (normal +Z): 4, 5, 6, 7
                mesh.Faces.AddFace(baseIdx + 4, baseIdx + 5, baseIdx + 6, baseIdx + 7);
                // Frente (normal -Y): 0, 1, 5, 4
                mesh.Faces.AddFace(baseIdx + 0, baseIdx + 1, baseIdx + 5, baseIdx + 4);
                // Direita (normal +X): 1, 2, 6, 5
                mesh.Faces.AddFace(baseIdx + 1, baseIdx + 2, baseIdx + 6, baseIdx + 5);
                // Fundo (normal +Y): 2, 3, 7, 6
                mesh.Faces.AddFace(baseIdx + 2, baseIdx + 3, baseIdx + 7, baseIdx + 6);
                // Esquerda (normal -X): 3, 0, 4, 7
                mesh.Faces.AddFace(baseIdx + 3, baseIdx + 0, baseIdx + 4, baseIdx + 7);
            }
            else
            {
                // Valores Negativos (cresce em -Z)
                // Topo em Z=0 (normal +Z): 0, 1, 2, 3
                mesh.Faces.AddFace(baseIdx + 0, baseIdx + 1, baseIdx + 2, baseIdx + 3);
                // Fundo em Z=h (normal -Z): 4, 7, 6, 5
                mesh.Faces.AddFace(baseIdx + 4, baseIdx + 7, baseIdx + 6, baseIdx + 5);
                // Frente: 0, 4, 5, 1
                mesh.Faces.AddFace(baseIdx + 0, baseIdx + 4, baseIdx + 5, baseIdx + 1);
                // Direita: 1, 5, 6, 2
                mesh.Faces.AddFace(baseIdx + 1, baseIdx + 5, baseIdx + 6, baseIdx + 2);
                // Fundo: 2, 6, 7, 3
                mesh.Faces.AddFace(baseIdx + 2, baseIdx + 6, baseIdx + 7, baseIdx + 3);
                // Esquerda: 3, 7, 4, 0
                mesh.Faces.AddFace(baseIdx + 3, baseIdx + 7, baseIdx + 4, baseIdx + 0);
            }
        }

        private static Color Lighten(Color c, float factor)
        {
            int r = Math.Min(255, (int)(c.R + (255 - c.R) * factor));
            int g = Math.Min(255, (int)(c.G + (255 - c.G) * factor));
            int b = Math.Min(255, (int)(c.B + (255 - c.B) * factor));
            return Color.FromArgb(c.A, r, g, b);
        }

        private static Color Darken(Color c, float factor)
        {
            int r = Math.Max(0, (int)(c.R * (1.0f - factor)));
            int g = Math.Max(0, (int)(c.G * (1.0f - factor)));
            int b = Math.Max(0, (int)(c.B * (1.0f - factor)));
            return Color.FromArgb(c.A, r, g, b);
        }

        private static bool TryExtractDouble(object item, out double val)
        {
            val = double.NaN;
            if (item == null) return false;

            if (item is IGH_Goo goo)
            {
                object scriptObj = goo.SafeScriptVariable();
                if (scriptObj != null) item = scriptObj;
            }

            if (item is double d) { val = d; return true; }
            if (item is float f) { val = f; return true; }
            if (item is int i) { val = i; return true; }
            if (item is long l) { val = l; return true; }
            if (item is decimal dec) { val = (double)dec; return true; }

            string s = item.ToString();
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out val)
                || double.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out val);
        }

        private static void Parse2DDimensions(object raw, double defX, double defY, out double dimX, out double dimY)
        {
            dimX = defX;
            dimY = defY;
            if (raw == null) return;

            if (raw is IGH_Goo goo)
            {
                object sv = goo.SafeScriptVariable();
                if (sv != null) raw = sv;
            }

            if (raw is double d) { dimX = d; dimY = d; return; }
            if (raw is int i) { dimX = i; dimY = i; return; }
            if (raw is Vector2d v2) { dimX = v2.X; dimY = v2.Y; return; }
            if (raw is Vector3d v3) { dimX = v3.X; dimY = v3.Y; return; }
            if (raw is Interval iv) { dimX = iv.T0; dimY = iv.T1; return; }

            string s = raw.ToString();
            var parts = s.Split(new char[] { ',', ';', ' ', 'x', 'X' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double px)) dimX = px;
                if (double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double py)) dimY = py;
            }
            else if (parts.Length == 1)
            {
                if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double p)) { dimX = p; dimY = p; }
            }
        }

        private static List<Color> ParseColors(List<object> rawList)
        {
            if (rawList == null || rawList.Count == 0) return null;
            var list = new List<Color>();

            foreach (var item in rawList)
            {
                if (item == null) continue;
                object obj = item;
                if (obj is IGH_Goo goo)
                {
                    object sv = goo.SafeScriptVariable();
                    if (sv != null) obj = sv;
                }

                if (obj is Color c)
                {
                    list.Add(c);
                    continue;
                }
                if (obj is GH_Colour ghc)
                {
                    list.Add(ghc.Value);
                    continue;
                }

                string name = obj.ToString().Trim();
                if (name.Equals("Turbo", StringComparison.OrdinalIgnoreCase)) return GetTurboStops();
                if (name.Equals("Viridis", StringComparison.OrdinalIgnoreCase)) return GetViridisStops();
                if (name.Equals("Jet", StringComparison.OrdinalIgnoreCase)) return GetJetStops();
                if (name.Equals("CoolWarm", StringComparison.OrdinalIgnoreCase)) return GetCoolWarmStops();
                if (name.Equals("Glaux", StringComparison.OrdinalIgnoreCase)) return new List<Color>(DefaultPalette);

                try
                {
                    Color named = ColorTranslator.FromHtml(name);
                    if (!named.IsEmpty) list.Add(named);
                }
                catch { }
            }

            return list.Count > 0 ? list : null;
        }

        private static Color SampleGradient(IList<Color> stops, double t)
        {
            if (stops == null || stops.Count == 0) return Color.DodgerBlue;
            if (stops.Count == 1) return stops[0];
            t = Math.Max(0.0, Math.Min(1.0, t));

            double scaled = t * (stops.Count - 1);
            int idx = (int)Math.Floor(scaled);
            int next = Math.Min(stops.Count - 1, idx + 1);
            float f = (float)(scaled - idx);

            Color c0 = stops[idx];
            Color c1 = stops[next];

            int r = (int)(c0.R + (c1.R - c0.R) * f);
            int g = (int)(c0.G + (c1.G - c0.G) * f);
            int b = (int)(c0.B + (c1.B - c0.B) * f);
            return Color.FromArgb(255, r, g, b);
        }

        private static Color SampleTurboGradient(double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            // Coeficientes aproximados polinomiais do Turbo (Google Colormap)
            double r = 0.1357 + t * (4.597 + t * (-42.327 + t * (130.58 + t * (-150.56 + t * 58.137))));
            double g = 0.0914 + t * (2.1856 + t * (4.8052 + t * (-14.019 + t * (9.7753 + t * -1.8286))));
            double b = 0.1066 + t * (12.559 + t * (-60.197 + t * (109.07 + t * (-88.506 + t * 27.067))));
            return Color.FromArgb(
                255,
                (int)Math.Max(0, Math.Min(255, r * 255.0)),
                (int)Math.Max(0, Math.Min(255, g * 255.0)),
                (int)Math.Max(0, Math.Min(255, b * 255.0)));
        }

        private static List<Color> GetTurboStops()
        {
            return new List<Color>
            {
                Color.FromArgb(48, 18, 59),
                Color.FromArgb(70, 134, 251),
                Color.FromArgb(27, 229, 181),
                Color.FromArgb(164, 252, 60),
                Color.FromArgb(251, 185, 56),
                Color.FromArgb(227, 73, 24),
                Color.FromArgb(122, 4, 3)
            };
        }

        private static List<Color> GetViridisStops()
        {
            return new List<Color>
            {
                Color.FromArgb(68, 1, 84),
                Color.FromArgb(59, 82, 139),
                Color.FromArgb(33, 145, 140),
                Color.FromArgb(94, 201, 98),
                Color.FromArgb(253, 231, 37)
            };
        }

        private static List<Color> GetJetStops()
        {
            return new List<Color>
            {
                Color.FromArgb(0, 0, 143),
                Color.FromArgb(0, 0, 255),
                Color.FromArgb(0, 255, 255),
                Color.FromArgb(255, 255, 0),
                Color.FromArgb(255, 0, 0),
                Color.FromArgb(128, 0, 0)
            };
        }

        private static List<Color> GetCoolWarmStops()
        {
            return new List<Color>
            {
                Color.FromArgb(59, 76, 192),
                Color.FromArgb(153, 175, 242),
                Color.FromArgb(221, 221, 221),
                Color.FromArgb(242, 160, 135),
                Color.FromArgb(180, 4, 38)
            };
        }
    }
}
