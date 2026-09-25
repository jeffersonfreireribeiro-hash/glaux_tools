// PillIsometricSurfaceGraph_Component.cs
// Componente para visualização de Superfície 3D em Projeção Isométrica no Canvas do Grasshopper
// Suporta seleção dos 4 quadrantes isométricos (SW, SE, NE, NW), malha com gradiente de cor e wireframe,
// planos de grade (bounding box 3D), eixos rotulados (x1, x2, D1), legenda colorbar vertical,
// saída de malha nativa Rhino.Geometry.Mesh e exportação PNG de alta resolução para publicações científicas.
// Glaux Tools - Categoria "Visual"

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
    /// Quadrante isométrico de visualização.
    /// </summary>
    public enum IsometricQuadrant
    {
        SW = 0, // Sudoeste (225° - visão frontal padrão clássica, igual à referência matplotlib)
        SE = 1, // Sudeste (135°)
        NE = 2, // Nordeste (45°)
        NW = 3  // Noroeste (315°)
    }

    /// <summary>
    /// Quad facet para ordenação de profundidade (Painter's algorithm).
    /// </summary>
    internal class QuadFace3D
    {
        public PointF[] ScreenPoints;
        public double Depth;
        public Color FillColor;
        public int I;
        public int J;
    }

    /// <summary>
    /// Componente GH para visualização de Superfície 3D com Gradiente e Projeção Isométrica no Canvas.
    /// </summary>
    public class PillIsometricSurfaceGraph_Component : GH_Component
    {
        // Dados de malha e geometria em cache
        public double[] GridX = new double[0];
        public double[] GridY = new double[0];
        public double[,] GridZ = new double[0, 0];
        public int Nx = 0;
        public int Ny = 0;

        public double MinX = 0, MaxX = 1;
        public double MinY = 0, MaxY = 1;
        public double MinZ = 0, MaxZ = 1;
        public double MeanZ = 0;
        public double StdDevZ = 0;

        // Configurações de visualização
        public IsometricQuadrant Quadrant = IsometricQuadrant.SW;
        public double ElevationDeg = 30.0;
        public bool ShowWireframe = true;
        public string ChartTitle = "Shoebox EDT";
        public string XLabel = "x1";
        public string YLabel = "x2";
        public string ZLabel = "D1";
        public string ActiveColormapName = "Jet";
        public List<Color> ActiveColormap = new List<Color>();

        // Exportação PNG
        public string LastExportFolder = "";
        public string LastSavedPath = "";
        public bool JustSaved = false;

        public PillIsometricSurfaceGraph_Component()
            : base(
                "Pill Isometric Surface Graph",
                "IsoSurface",
                "Renderiza um gráfico de superfície 3D em projeção isométrica diretamente no Canvas do Grasshopper.\n" +
                "- Permite escolher qualquer um dos 4 quadrantes isométricos (SW, SE, NE, NW);\n" +
                "- Recebe coordenadas X, Y e grade/valores Z com colormap gradiente suave e linhas de malha (wireframe);\n" +
                "- Exibe caixa delimitadora 3D com eixos (x1, x2, D1), ticks numéricos e legenda lateral de cores (Colorbar);\n" +
                "- Gera malha nativa Rhino.Geometry.Mesh com vertex colors e suporta exportação PNG em alta resolução (300 DPI).",
                "Glaux Tools",
                "Visual")
        {
        }

        public override Guid ComponentGuid => new Guid("4b1e8f2a-7c9d-4e5a-8b3c-9d0e1f2a3b4c");

        protected override Bitmap Icon => GlauxToolsIcons.IsometricSurfaceGraph;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override void CreateAttributes()
        {
            m_attributes = new PillIsometricSurfaceGraph_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0: Coordenadas X
            pManager.AddNumberParameter(
                "X Values", "X",
                "Lista de coordenadas no eixo X (ex: x1). Se omitido, adota índices 0, 1, 2...",
                GH_ParamAccess.list);
            pManager[0].Optional = true;

            // 1: Coordenadas Y
            pManager.AddNumberParameter(
                "Y Values", "Y",
                "Lista de coordenadas no eixo Y (ex: x2). Se omitido, adota índices 0, 1, 2...",
                GH_ParamAccess.list);
            pManager[1].Optional = true;

            // 2: Valores Z (DataTree, Lista plana ou Pontos 3D)
            pManager.AddGenericParameter(
                "Values (Z)", "V",
                "Valores numéricos de altura/elevação Z (ex: D1, EDT, T30, SPL, deformações).\n" +
                "Aceita:\n" +
                "1. Árvore de dados (DataTree) com Ny ramos de Nx valores (grade regular);\n" +
                "2. Lista plana de Nx * Ny valores;\n" +
                "3. Lista de pontos 3D (Point3d) com coordenadas (x, y, z).",
                GH_ParamAccess.tree);

            // 3: Quadrante Isométrico (Ponto de vista)
            pManager.AddGenericParameter(
                "Quadrant / View", "View",
                "Ponto de vista isométrico (escolha entre os 4 quadrantes):\n" +
                "0 ou 'SW' / 'Sudoeste' (Visão frontal padrão, idêntica ao matplotlib);\n" +
                "1 ou 'SE' / 'Sudeste';\n" +
                "2 ou 'NE' / 'Nordeste';\n" +
                "3 ou 'NW' / 'Noroeste'.\n" +
                "Padrão: 0 (SW).",
                GH_ParamAccess.item);
            pManager[3].Optional = true;

            // 4: Ângulo de Elevação
            pManager.AddNumberParameter(
                "Elevation", "Elev",
                "Ângulo de elevação vertical da perspectiva isométrica em graus (10° a 80°). Padrão: 30°.",
                GH_ParamAccess.item, 30.0);
            pManager[4].Optional = true;

            // 5: Colormap / Paleta
            pManager.AddGenericParameter(
                "Colormap", "Col",
                "Gradiente de cores para a superfície:\n" +
                "Nomes: 'Jet', 'Turbo', 'Viridis', 'Inferno', 'Plasma', 'CoolWarm', 'Spectral', 'Sunset', 'Greyscale'.\n" +
                "Também aceita lista de cores customizadas (System.Drawing.Color). Padrão: 'Jet'.",
                GH_ParamAccess.list);
            pManager[5].Optional = true;

            // 6: Título do Gráfico
            pManager.AddTextParameter(
                "Title", "T",
                "Título principal no cabeçalho do gráfico. Padrão: 'Shoebox EDT'.",
                GH_ParamAccess.item, "Shoebox EDT");
            pManager[6].Optional = true;

            // 7: Rótulo do Eixo X
            pManager.AddTextParameter(
                "X Label", "XLab",
                "Rótulo do eixo X. Padrão: 'x1'.",
                GH_ParamAccess.item, "x1");
            pManager[7].Optional = true;

            // 8: Rótulo do Eixo Y
            pManager.AddTextParameter(
                "Y Label", "YLab",
                "Rótulo do eixo Y. Padrão: 'x2'.",
                GH_ParamAccess.item, "x2");
            pManager[8].Optional = true;

            // 9: Rótulo do Eixo Z
            pManager.AddTextParameter(
                "Z Label", "ZLab",
                "Rótulo do eixo vertical Z. Padrão: 'D1'.",
                GH_ParamAccess.item, "D1");
            pManager[9].Optional = true;

            // 10: Wireframe (Linhas de malha)
            pManager.AddBooleanParameter(
                "Wireframe", "Wire",
                "Exibir linhas pretas de malha (wireframe) sobre os quadriláteros da superfície. Padrão: true.",
                GH_ParamAccess.item, true);
            pManager[10].Optional = true;

            // 11: Limites manuais de Z (Domain)
            pManager.AddGenericParameter(
                "Domain (Z Limits)", "D",
                "Limites manuais de Mínimo e Máximo de Z como Intervalo/Domínio (ex: Interval(-0.05, 0.0) ou '-0.05 To 0').\n" +
                "Se omitido, adota a faixa real dos dados.",
                GH_ParamAccess.item);
            pManager[11].Optional = true;

            // 12: Pasta de Exportação
            pManager.AddTextParameter(
                "Export Folder", "Folder",
                "Pasta de destino para gravação da imagem PNG. Se vazio, adota a Área de Trabalho (Desktop).",
                GH_ParamAccess.item, "");
            pManager[12].Optional = true;

            // 13: Gatilho de Salvamento
            pManager.AddBooleanParameter(
                "Save Image", "Save",
                "Gatilho booleano para exportar imagem PNG em altíssima resolução (300 DPI) com fundo branco para publicações.",
                GH_ParamAccess.item, false);
            pManager[13].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // 0: Imagem do Gráfico
            pManager.AddGenericParameter(
                "Chart Image", "Img",
                "Imagem renderizada da superfície 3D em projeção isométrica (System.Drawing.Bitmap).",
                GH_ParamAccess.item);

            // 1: Malha 3D Nativa Rhino
            pManager.AddMeshParameter(
                "Mesh 3D", "M",
                "Malha tridimensional nativa do Rhino (Rhino.Geometry.Mesh) com cores aplicadas por vértice correspondentes ao gradiente.",
                GH_ParamAccess.item);

            // 2: Domínio Z
            pManager.AddIntervalParameter(
                "Domain", "D",
                "Intervalo adotado de variação do eixo Z [MinZ, MaxZ].",
                GH_ParamAccess.item);

            // 3: Resumo Estatístico
            pManager.AddTextParameter(
                "Summary", "T",
                "Relatório textual com resumo das dimensões da grade e estatísticas de Z (Min, Max, Média, Desvio Padrão).",
                GH_ParamAccess.item);

            // 4: Caminho do Arquivo Salvo
            pManager.AddTextParameter(
                "Saved Path", "Path",
                "Caminho absoluto do arquivo PNG gravado em disco.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Obter Entradas
            var xList = new List<double>();
            DA.GetDataList(0, xList);

            var yList = new List<double>();
            DA.GetDataList(1, yList);

            if (!DA.GetDataTree(2, out GH_Structure<IGH_Goo> zTree) || zTree == null || zTree.DataCount == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Conecte os dados de elevação Z (Values).");
                return;
            }

            // Quadrante
            object viewObj = null;
            if (DA.GetData(3, ref viewObj) && viewObj != null)
            {
                Quadrant = ParseQuadrant(viewObj);
            }
            else
            {
                Quadrant = IsometricQuadrant.SW;
            }

            // Elevação
            double elevDeg = 30.0;
            if (DA.GetData(4, ref elevDeg))
            {
                ElevationDeg = Math.Max(5.0, Math.Min(85.0, elevDeg));
            }

            // Colormap
            var colObjList = new List<object>();
            DA.GetDataList(5, colObjList);
            ActiveColormap = ParseColormap(colObjList, out ActiveColormapName);

            // Títulos e Rótulos
            string title = "Shoebox EDT";
            DA.GetData(6, ref title);
            ChartTitle = string.IsNullOrWhiteSpace(title) ? "Shoebox EDT" : title;

            string xLab = "x1";
            DA.GetData(7, ref xLab);
            XLabel = string.IsNullOrWhiteSpace(xLab) ? "x1" : xLab;

            string yLab = "x2";
            DA.GetData(8, ref yLab);
            YLabel = string.IsNullOrWhiteSpace(yLab) ? "x2" : yLab;

            string zLab = "D1";
            DA.GetData(9, ref zLab);
            ZLabel = string.IsNullOrWhiteSpace(zLab) ? "D1" : zLab;

            bool wireframe = true;
            DA.GetData(10, ref wireframe);
            ShowWireframe = wireframe;

            // Domínio Manual de Z
            Interval? userDomain = null;
            object domObj = null;
            if (DA.GetData(11, ref domObj) && domObj != null)
            {
                userDomain = ParseInterval(domObj);
            }

            string exportFolder = "";
            DA.GetData(12, ref exportFolder);
            LastExportFolder = exportFolder;

            bool saveTrigger = false;
            DA.GetData(13, ref saveTrigger);

            // 2. Extrair e Estruturar Grade 2D (X, Y, Z)
            if (!ExtractGridData(xList, yList, zTree, out double[] finalX, out double[] finalY, out double[,] finalZ))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Falha ao construir a grade bidimensional a partir dos dados fornecidos.");
                return;
            }

            GridX = finalX;
            GridY = finalY;
            GridZ = finalZ;
            Nx = finalX.Length;
            Ny = finalY.Length;

            MinX = finalX.Min();
            MaxX = finalX.Max();
            MinY = finalY.Min();
            MaxY = finalY.Max();

            // Estatísticas de Z
            double sumZ = 0;
            double minZ = double.MaxValue;
            double maxZ = double.MinValue;
            int countZ = 0;

            for (int i = 0; i < Nx; i++)
            {
                for (int j = 0; j < Ny; j++)
                {
                    double v = finalZ[i, j];
                    if (!double.IsNaN(v) && !double.IsInfinity(v))
                    {
                        if (v < minZ) minZ = v;
                        if (v > maxZ) maxZ = v;
                        sumZ += v;
                        countZ++;
                    }
                }
            }

            if (countZ == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum valor numérico válido encontrado na grade Z.");
                return;
            }

            MeanZ = sumZ / countZ;
            double varSum = 0;
            for (int i = 0; i < Nx; i++)
            {
                for (int j = 0; j < Ny; j++)
                {
                    double v = finalZ[i, j];
                    if (!double.IsNaN(v) && !double.IsInfinity(v))
                    {
                        varSum += (v - MeanZ) * (v - MeanZ);
                    }
                }
            }
            StdDevZ = countZ > 1 ? Math.Sqrt(varSum / (countZ - 1)) : 0.0;

            if (userDomain.HasValue && userDomain.Value.Length > 1e-9)
            {
                MinZ = userDomain.Value.Min;
                MaxZ = userDomain.Value.Max;
            }
            else
            {
                MinZ = minZ;
                MaxZ = maxZ;
            }

            if (Math.Abs(MaxZ - MinZ) < 1e-9)
            {
                MaxZ += 1.0;
                MinZ -= 1.0;
            }

            // 3. Construir Malha 3D Nativa do Rhino com Cores por Vértice
            Mesh rhinoMesh = BuildRhinoMesh(finalX, finalY, finalZ, MinZ, MaxZ, ActiveColormap);

            // 4. Montar Resumo Textual
            var sb = new StringBuilder();
            sb.AppendLine("=================================================================");
            sb.AppendLine($"  {ChartTitle.ToUpper()} - ISOMETRIC 3D SURFACE SUMMARY");
            sb.AppendLine("=================================================================");
            sb.AppendLine($"Dimensões da Grade : {Nx} colunas (X) x {Ny} linhas (Y) = {Nx * Ny} pontos");
            sb.AppendLine($"Eixo X ({XLabel})      : Min = {MinX:F3}, Max = {MaxX:F3}");
            sb.AppendLine($"Eixo Y ({YLabel})      : Min = {MinY:F3}, Max = {MaxY:F3}");
            sb.AppendLine($"Eixo Z ({ZLabel})      : Min = {MinZ:F4}, Max = {MaxZ:F4}");
            sb.AppendLine($"Média de Z         : {MeanZ:F4}");
            sb.AppendLine($"Desvio Padrão (σ)  : {StdDevZ:F4}");
            sb.AppendLine($"Quadrante Isométrico: {Quadrant} (Elevação: {ElevationDeg:F1}°)");
            sb.AppendLine($"Paleta de Cores    : {ActiveColormapName}");
            sb.AppendLine("=================================================================");

            // 5. Configurar Saídas
            Bitmap exportBmp = RenderWhiteBackgroundBitmap(1100, 850);
            DA.SetData(0, exportBmp);
            DA.SetData(1, rhinoMesh);
            DA.SetData(2, new Interval(MinZ, MaxZ));
            DA.SetData(3, sb.ToString());

            // 6. Exportação PNG se gatilho ativo
            if (saveTrigger)
            {
                string saved = ExportImageWhiteBackground(exportFolder, out string err);
                if (!string.IsNullOrEmpty(saved))
                {
                    LastSavedPath = saved;
                    JustSaved = true;
                    Message = "PNG Salvo!";
                }
                else if (!string.IsNullOrEmpty(err))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Erro ao exportar PNG: {err}");
                }
            }

            DA.SetData(4, LastSavedPath);
        }

        #region Extração de Dados da Grade

        private bool ExtractGridData(List<double> inX, List<double> inY, GH_Structure<IGH_Goo> zTree,
            out double[] outX, out double[] outY, out double[,] outZ)
        {
            outX = null;
            outY = null;
            outZ = null;

            // Caso A: Verificar se são pontos 3D diretos em zTree
            var pts = new List<Point3d>();
            foreach (var goo in zTree.AllData(true))
            {
                if (goo is GH_Point ghPt)
                {
                    pts.Add(ghPt.Value);
                }
                else if (goo != null && goo.CastTo<Point3d>(out Point3d p))
                {
                    pts.Add(p);
                }
            }

            if (pts.Count >= 4 && inX.Count == 0 && inY.Count == 0)
            {
                // Extrai grade de coordenadas únicas de X e Y
                var uniqueX = pts.Select(p => Math.Round(p.X, 6)).Distinct().OrderBy(v => v).ToArray();
                var uniqueY = pts.Select(p => Math.Round(p.Y, 6)).Distinct().OrderBy(v => v).ToArray();

                if (uniqueX.Length >= 2 && uniqueY.Length >= 2 && uniqueX.Length * uniqueY.Length == pts.Count)
                {
                    int nX = uniqueX.Length;
                    int nY = uniqueY.Length;
                    var zMat = new double[nX, nY];

                    for (int i = 0; i < nX; i++)
                        for (int j = 0; j < nY; j++)
                            zMat[i, j] = double.NaN;

                    foreach (var p in pts)
                    {
                        double rx = Math.Round(p.X, 6);
                        double ry = Math.Round(p.Y, 6);
                        int ix = Array.BinarySearch(uniqueX, rx);
                        int iy = Array.BinarySearch(uniqueY, ry);
                        if (ix >= 0 && iy >= 0)
                        {
                            zMat[ix, iy] = p.Z;
                        }
                    }

                    outX = uniqueX;
                    outY = uniqueY;
                    outZ = zMat;
                    return true;
                }
            }

            // Caso B: zTree com múltiplos ramos (DataTree clássica onde cada ramo é uma linha ou coluna)
            if (zTree.Branches.Count > 1)
            {
                int numBranches = zTree.Branches.Count;
                int maxBranchLen = zTree.Branches.Max(b => b.Count);

                if (maxBranchLen >= 2)
                {
                    int nX, nY;
                    bool branchIsY = true;

                    if (inX.Count > 0 && inY.Count > 0)
                    {
                        if (inY.Count == numBranches && inX.Count == maxBranchLen)
                        {
                            branchIsY = true;
                            nX = inX.Count;
                            nY = inY.Count;
                        }
                        else if (inX.Count == numBranches && inY.Count == maxBranchLen)
                        {
                            branchIsY = false;
                            nX = inX.Count;
                            nY = inY.Count;
                        }
                        else if (inY.Count == numBranches)
                        {
                            branchIsY = true;
                            nX = inX.Count > 0 ? inX.Count : maxBranchLen;
                            nY = inY.Count;
                        }
                        else if (inX.Count == numBranches)
                        {
                            branchIsY = false;
                            nX = inX.Count;
                            nY = inY.Count > 0 ? inY.Count : maxBranchLen;
                        }
                        else
                        {
                            nX = inX.Count > 0 ? inX.Count : maxBranchLen;
                            nY = inY.Count > 0 ? inY.Count : numBranches;
                        }
                    }
                    else if (inX.Count > 0)
                    {
                        if (inX.Count == maxBranchLen)
                        {
                            nX = inX.Count;
                            nY = numBranches;
                            branchIsY = true;
                        }
                        else
                        {
                            nX = numBranches;
                            nY = maxBranchLen;
                            branchIsY = false;
                        }
                    }
                    else if (inY.Count > 0)
                    {
                        if (inY.Count == numBranches)
                        {
                            nX = maxBranchLen;
                            nY = inY.Count;
                            branchIsY = true;
                        }
                        else
                        {
                            nX = inY.Count;
                            nY = maxBranchLen;
                            branchIsY = false;
                        }
                    }
                    else
                    {
                        nX = maxBranchLen;
                        nY = numBranches;
                        branchIsY = true;
                    }

                    var zMat = new double[nX, nY];
                    for (int i = 0; i < nX; i++)
                        for (int j = 0; j < nY; j++)
                            zMat[i, j] = double.NaN;

                    for (int b = 0; b < numBranches; b++)
                    {
                        var branch = zTree.Branches[b];
                        for (int k = 0; k < branch.Count; k++)
                        {
                            if (GH_Convert.ToDouble(branch[k], out double val, GH_Conversion.Both))
                            {
                                int ix = branchIsY ? k : b;
                                int iy = branchIsY ? b : k;
                                if (ix < nX && iy < nY)
                                {
                                    zMat[ix, iy] = val;
                                }
                            }
                        }
                    }

                    FillGridGaps(zMat, nX, nY, 0.0);

                    outX = (inX.Count == nX) ? inX.ToArray() : Enumerable.Range(0, nX).Select(i => (double)i).ToArray();
                    outY = (inY.Count == nY) ? inY.ToArray() : Enumerable.Range(0, nY).Select(j => (double)j).ToArray();
                    outZ = zMat;
                    return true;
                }
            }

            // Caso C: Lista plana de dados em zTree (1 ramo ou AllData)
            var allVals = new List<double>();
            foreach (var goo in zTree.AllData(true))
            {
                if (GH_Convert.ToDouble(goo, out double val, GH_Conversion.Both))
                {
                    allVals.Add(val);
                }
                else
                {
                    allVals.Add(double.NaN);
                }
            }

            if (allVals.Count < 4) return false;

            int dimX = inX.Count;
            int dimY = inY.Count;

            // Se dimensões X e Y fornecidas correspondem exatamente ao total de pontos
            if (dimX >= 2 && dimY >= 2 && dimX * dimY == allVals.Count)
            {
                var zMat = new double[dimX, dimY];
                int idx = 0;
                for (int j = 0; j < dimY; j++)
                {
                    for (int i = 0; i < dimX; i++)
                    {
                        zMat[i, j] = allVals[idx++];
                    }
                }
                FillGridGaps(zMat, dimX, dimY, 0.0);

                outX = inX.ToArray();
                outY = inY.ToArray();
                outZ = zMat;
                return true;
            }

            // Se dimensões X e Y fornecidas, mas com lacunas ou ligeira diferença
            if (dimX >= 2 && dimY >= 2 && Math.Abs(allVals.Count - dimX * dimY) <= Math.Max(dimX, dimY))
            {
                var zMat = new double[dimX, dimY];
                for (int i = 0; i < dimX; i++)
                    for (int j = 0; j < dimY; j++)
                        zMat[i, j] = double.NaN;

                int maxIdx = Math.Min(allVals.Count, dimX * dimY);
                int idx = 0;
                for (int j = 0; j < dimY; j++)
                {
                    for (int i = 0; i < dimX; i++)
                    {
                        if (idx < maxIdx)
                            zMat[i, j] = allVals[idx++];
                    }
                }
                FillGridGaps(zMat, dimX, dimY, 0.0);

                outX = inX.ToArray();
                outY = inY.ToArray();
                outZ = zMat;
                return true;
            }

            if (dimX >= 2 && allVals.Count % dimX == 0 && allVals.Count >= dimX * 2)
            {
                dimY = allVals.Count / dimX;
                var zMat = new double[dimX, dimY];
                int idx = 0;
                for (int j = 0; j < dimY; j++)
                {
                    for (int i = 0; i < dimX; i++)
                    {
                        zMat[i, j] = allVals[idx++];
                    }
                }
                FillGridGaps(zMat, dimX, dimY, 0.0);

                outX = inX.ToArray();
                outY = (inY.Count == dimY) ? inY.ToArray() : Enumerable.Range(0, dimY).Select(j => (double)j).ToArray();
                outZ = zMat;
                return true;
            }

            if (dimY >= 2 && allVals.Count % dimY == 0 && allVals.Count >= dimY * 2)
            {
                dimX = allVals.Count / dimY;
                var zMat = new double[dimX, dimY];
                int idx = 0;
                for (int j = 0; j < dimY; j++)
                {
                    for (int i = 0; i < dimX; i++)
                    {
                        zMat[i, j] = allVals[idx++];
                    }
                }
                FillGridGaps(zMat, dimX, dimY, 0.0);

                outX = (inX.Count == dimX) ? inX.ToArray() : Enumerable.Range(0, dimX).Select(i => (double)i).ToArray();
                outY = inY.ToArray();
                outZ = zMat;
                return true;
            }

            // Fallback: tentar aproximação quadrada
            int side = (int)Math.Sqrt(allVals.Count);
            if (side >= 2 && side * side == allVals.Count)
            {
                dimX = side;
                dimY = side;
                var zMat = new double[dimX, dimY];
                int idx = 0;
                for (int j = 0; j < dimY; j++)
                {
                    for (int i = 0; i < dimX; i++)
                    {
                        zMat[i, j] = allVals[idx++];
                    }
                }
                FillGridGaps(zMat, dimX, dimY, 0.0);

                outX = Enumerable.Range(0, dimX).Select(i => (double)i).ToArray();
                outY = Enumerable.Range(0, dimY).Select(j => (double)j).ToArray();
                outZ = zMat;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Preenche lacunas (NaN ou Infinity) em uma grade bidimensional usando interpolação por vizinhos válidos (IDW 2D).
        /// </summary>
        private static void FillGridGaps(double[,] zMat, int nx, int ny, double defaultVal = 0.0)
        {
            var validPts = new List<(int x, int y, double val)>();
            var gapPts = new List<(int x, int y)>();

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    double v = zMat[i, j];
                    if (!double.IsNaN(v) && !double.IsInfinity(v))
                    {
                        validPts.Add((i, j, v));
                    }
                    else
                    {
                        gapPts.Add((i, j));
                    }
                }
            }

            if (gapPts.Count == 0) return;

            if (validPts.Count == 0)
            {
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < ny; j++)
                        zMat[i, j] = defaultVal;
                return;
            }

            foreach (var (gx, gy) in gapPts)
            {
                double sumWeights = 0.0;
                double sumVals = 0.0;

                int nearestCount = Math.Min(8, validPts.Count);
                var nearest = validPts
                    .Select(p => {
                        double dx = p.x - gx;
                        double dy = p.y - gy;
                        return (p.val, distSq: dx * dx + dy * dy);
                    })
                    .OrderBy(p => p.distSq)
                    .Take(nearestCount);

                bool exactMatch = false;
                foreach (var item in nearest)
                {
                    if (item.distSq < 1e-6)
                    {
                        zMat[gx, gy] = item.val;
                        exactMatch = true;
                        break;
                    }
                    double w = 1.0 / item.distSq;
                    sumWeights += w;
                    sumVals += w * item.val;
                }

                if (!exactMatch)
                {
                    zMat[gx, gy] = sumWeights > 0 ? (sumVals / sumWeights) : validPts[0].val;
                }
            }
        }

        #endregion

        #region Construção da Malha Rhino

        private static Mesh BuildRhinoMesh(double[] xArr, double[] yArr, double[,] zMat, double minZ, double maxZ, List<Color> cmap)
        {
            var mesh = new Mesh();
            int nx = xArr.Length;
            int ny = yArr.Length;

            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    double zVal = zMat[i, j];
                    if (double.IsNaN(zVal) || double.IsInfinity(zVal))
                    {
                        zVal = minZ;
                    }
                    mesh.Vertices.Add(xArr[i], yArr[j], zVal);
                    Color col = EvaluateColormap(cmap, zVal, minZ, maxZ);
                    mesh.VertexColors.Add(col);
                }
            }

            for (int j = 0; j < ny - 1; j++)
            {
                for (int i = 0; i < nx - 1; i++)
                {
                    int v0 = j * nx + i;
                    int v1 = j * nx + (i + 1);
                    int v2 = (j + 1) * nx + (i + 1);
                    int v3 = (j + 1) * nx + i;

                    mesh.Faces.AddFace(v0, v1, v2, v3);
                }
            }

            mesh.Normals.ComputeNormals();
            mesh.Compact();
            return mesh;
        }

        #endregion

        #region Projeção Isométrica 3D -> 2D

        /// <summary>
        /// Projeta um ponto 3D normalizado [-1, 1] no plano de tela 2D com profundidade de ordenação.
        /// </summary>
        internal static void ProjectIsometricPoint(
            double xNorm, double yNorm, double zNorm,
            double thetaRad, double sinPhi, double cosPhi,
            float cx, float cy, float scaleX, float scaleY,
            out PointF screenPt, out double depth)
        {
            double cosTheta = Math.Cos(thetaRad);
            double sinTheta = Math.Sin(thetaRad);

            double xRot = xNorm * cosTheta - yNorm * sinTheta;
            double yRot = xNorm * sinTheta + yNorm * cosTheta;
            double zRot = zNorm;

            double u = xRot;
            double v = yRot * sinPhi - zRot * cosPhi;
            depth = yRot * cosPhi + zRot * sinPhi;

            screenPt = new PointF(
                cx + (float)(u * scaleX),
                cy + (float)(v * scaleY));
        }

        public static double GetQuadrantAzimuth(IsometricQuadrant quad)
        {
            switch (quad)
            {
                case IsometricQuadrant.SW: return 225.0 * Math.PI / 180.0; // Sudoeste (perspectiva matplotlib padrão)
                case IsometricQuadrant.SE: return 135.0 * Math.PI / 180.0; // Sudeste
                case IsometricQuadrant.NE: return 45.0 * Math.PI / 180.0;  // Nordeste
                case IsometricQuadrant.NW: return 315.0 * Math.PI / 180.0; // Noroeste
                default: return 225.0 * Math.PI / 180.0;
            }
        }

        /// <summary>
        /// Calcula a escala e o centro analíticos para que a caixa 3D, superfície, eixos, rótulos e ticks caibam 100% dentro do retângulo alvo.
        /// </summary>
        public static void ComputeFitTransform(
            double thetaRad, double sinPhi, double cosPhi,
            RectangleF targetRect,
            float padLeft, float padRight, float padTop, float padBottom,
            out float cx, out float cy, out float scaleX, out float scaleY)
        {
            double cosTheta = Math.Cos(thetaRad);
            double sinTheta = Math.Sin(thetaRad);

            double uMin = double.MaxValue, uMax = double.MinValue;
            double vMin = double.MaxValue, vMax = double.MinValue;

            // Percorrer os 8 cantos da caixa delimitadora normalizada [-1, 1]^3
            foreach (double xn in new double[] { -1.0, 1.0 })
            {
                foreach (double yn in new double[] { -1.0, 1.0 })
                {
                    double xRot = xn * cosTheta - yn * sinTheta;
                    double yRot = xn * sinTheta + yn * cosTheta;
                    double u = xRot;

                    foreach (double zn in new double[] { -1.0, 1.0 })
                    {
                        double v = yRot * sinPhi - zn * cosPhi;
                        if (u < uMin) uMin = u;
                        if (u > uMax) uMax = u;
                        if (v < vMin) vMin = v;
                        if (v > vMax) vMax = v;
                    }
                }
            }

            float wAvail = targetRect.Width - (padLeft + padRight);
            float hAvail = targetRect.Height - (padTop + padBottom);
            if (wAvail < 20f) wAvail = 20f;
            if (hAvail < 20f) hAvail = 20f;

            float uSpan = (float)(uMax - uMin);
            if (uSpan < 1e-4f) uSpan = 1f;
            float vSpan = (float)(vMax - vMin);
            if (vSpan < 1e-4f) vSpan = 1f;

            float s = Math.Min(wAvail / uSpan, hAvail / vSpan);

            scaleX = s;
            scaleY = s;

            cx = targetRect.Left + padLeft - (float)uMin * s + (wAvail - uSpan * s) * 0.5f;
            cy = targetRect.Top + padTop - (float)vMin * s + (hAvail - vSpan * s) * 0.5f;
        }

        #endregion

        #region Renderização Editorial com Fundo Branco (300 DPI)

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

                if (Nx < 2 || Ny < 2)
                {
                    using (var font = new Font("Arial", 12f))
                    using (var brush = new SolidBrush(Color.FromArgb(120, 120, 120)))
                    {
                        g.DrawString("Sem dados suficientes para renderização de superfície 3D.", font, brush, 40f, 40f);
                    }
                    return bmp;
                }

                // 1. Título do Gráfico (centralizado no topo)
                float titleY = 32f;
                using (var titleFont = new Font("Arial", 22f, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(Color.Black))
                {
                    var sfTitle = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(ChartTitle, titleFont, titleBrush, width * 0.44f, titleY, sfTitle);
                }

                // Área do gráfico 3D
                float padLeft = 40f;
                float padTop = 75f;
                float padRight = 30f;
                float padBottom = 40f;

                // Barra de cores à direita
                float cbWidth = 22f;
                float cbTotalW = 100f; // barra + ticks + números
                float cbRight = width - padRight;
                float cbLeft = cbRight - cbTotalW;
                RectangleF colorbarRect = new RectangleF(cbLeft + 10f, padTop + 40f, cbWidth, height - padTop - padBottom - 80f);

                // Área da superfície 3D à esquerda da barra de cores
                RectangleF plotRect = new RectangleF(padLeft, padTop, cbLeft - padLeft - 20f, height - padTop - padBottom);

                // Parâmetros de projeção
                double theta = GetQuadrantAzimuth(Quadrant);
                double phi = ElevationDeg * Math.PI / 180.0;
                double sinPhi = Math.Sin(phi);
                double cosPhi = Math.Cos(phi);

                // Ajuste de escala analítico para 100% de enquadramento
                ComputeFitTransform(theta, sinPhi, cosPhi, plotRect,
                    padLeft: 70f, padRight: 20f, padTop: 20f, padBottom: 45f,
                    out float cx, out float cy, out float scaleX, out float scaleY);

                // 2. Desenhar Planos de Grade da Caixa Delimitadora (Bounding Box 3D)
                DrawBoundingBoxPlanes(g, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, isDark: false);

                // 3. Renderizar Facetas da Superfície com Gradiente (Painter's Algorithm)
                DrawSurfaceFaces(g, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, isDark: false);

                // 4. Desenhar Eixos Frontais, Ticks Numéricos e Rótulos (x1, x2, D1)
                DrawAxisTicksAndLabels(g, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, isDark: false);

                // 5. Desenhar Barra de Cores Lateral (Colorbar Legend)
                DrawColorbar(g, colorbarRect, MinZ, MaxZ, ActiveColormap, isDark: false);
            }

            return bmp;
        }

        #endregion

        #region Desenho da Bounding Box e Superfície

        internal void DrawBoundingBoxPlanes(
            Graphics g, double theta, double sinPhi, double cosPhi,
            float cx, float cy, float scaleX, float scaleY, bool isDark)
        {
            Color wallBg = isDark ? Color.FromArgb(16, 20, 27) : Color.FromArgb(250, 252, 254);
            Color gridLineCol = isDark ? Color.FromArgb(38, 46, 58) : Color.FromArgb(220, 226, 235);
            Color boxBorderCol = isDark ? Color.FromArgb(60, 72, 90) : Color.FromArgb(190, 198, 210);

            using (var wallBrush = new SolidBrush(wallBg))
            using (var gridPen = new Pen(gridLineCol, 1f) { DashStyle = DashStyle.Solid })
            using (var borderPen = new Pen(boxBorderCol, 1.2f))
            {
                // Projetar os 4 cantos do chão:
                ProjectIsometricPoint(-1, -1, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF f00, out double _);
                ProjectIsometricPoint(1, -1, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF f10, out double _);
                ProjectIsometricPoint(1, 1, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF f11, out double _);
                ProjectIsometricPoint(-1, 1, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF f01, out double _);

                // Plano do chão (z = -1)
                PointF[] floorPts = new PointF[] { f00, f10, f11, f01 };
                g.FillPolygon(wallBrush, floorPts);
                g.DrawPolygon(borderPen, floorPts);

                // Linhas de grade no chão
                int gridDivs = 4;
                for (int d = 1; d < gridDivs; d++)
                {
                    double t = -1.0 + 2.0 * d / gridDivs;
                    // Linhas paralelas a Y
                    ProjectIsometricPoint(t, -1, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF pA, out _);
                    ProjectIsometricPoint(t, 1, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF pB, out _);
                    g.DrawLine(gridPen, pA, pB);

                    // Linhas paralelas a X
                    ProjectIsometricPoint(-1, t, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF pC, out _);
                    ProjectIsometricPoint(1, t, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF pD, out _);
                    g.DrawLine(gridPen, pC, pD);
                }

                // Paredes de fundo (backplanes) analíticas para qualquer quadrante
                double frontX = (Math.Sin(theta) >= 0) ? 1.0 : -1.0;
                double frontY = (Math.Cos(theta) >= 0) ? 1.0 : -1.0;
                double backX = -frontX;
                double backY = -frontY;

                // Parede de trás em X = backX
                ProjectIsometricPoint(backX, -1, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF wX00, out _);
                ProjectIsometricPoint(backX, 1, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF wX10, out _);
                ProjectIsometricPoint(backX, 1, 1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF wX11, out _);
                ProjectIsometricPoint(backX, -1, 1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF wX01, out _);

                PointF[] wallXPts = new PointF[] { wX00, wX10, wX11, wX01 };
                g.FillPolygon(wallBrush, wallXPts);
                g.DrawPolygon(borderPen, wallXPts);

                for (int d = 1; d < gridDivs; d++)
                {
                    double tz = -1.0 + 2.0 * d / gridDivs;
                    ProjectIsometricPoint(backX, -1, tz, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF p1, out _);
                    ProjectIsometricPoint(backX, 1, tz, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF p2, out _);
                    g.DrawLine(gridPen, p1, p2);

                    double ty = -1.0 + 2.0 * d / gridDivs;
                    ProjectIsometricPoint(backX, ty, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF p3, out _);
                    ProjectIsometricPoint(backX, ty, 1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF p4, out _);
                    g.DrawLine(gridPen, p3, p4);
                }

                // Parede de trás em Y = backY
                ProjectIsometricPoint(-1, backY, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF wY00, out _);
                ProjectIsometricPoint(1, backY, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF wY10, out _);
                ProjectIsometricPoint(1, backY, 1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF wY11, out _);
                ProjectIsometricPoint(-1, backY, 1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF wY01, out _);

                PointF[] wallYPts = new PointF[] { wY00, wY10, wY11, wY01 };
                g.FillPolygon(wallBrush, wallYPts);
                g.DrawPolygon(borderPen, wallYPts);

                for (int d = 1; d < gridDivs; d++)
                {
                    double tz = -1.0 + 2.0 * d / gridDivs;
                    ProjectIsometricPoint(-1, backY, tz, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF p1, out _);
                    ProjectIsometricPoint(1, backY, tz, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF p2, out _);
                    g.DrawLine(gridPen, p1, p2);

                    double tx = -1.0 + 2.0 * d / gridDivs;
                    ProjectIsometricPoint(tx, backY, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF p3, out _);
                    ProjectIsometricPoint(tx, backY, 1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF p4, out _);
                    g.DrawLine(gridPen, p3, p4);
                }
            }
        }

        internal void DrawSurfaceFaces(
            Graphics g, double theta, double sinPhi, double cosPhi,
            float cx, float cy, float scaleX, float scaleY, bool isDark)
        {
            if (Nx < 2 || Ny < 2) return;

            PointF[,] projPts = new PointF[Nx, Ny];
            double[,] depths = new double[Nx, Ny];

            double spanX = MaxX - MinX; if (Math.Abs(spanX) < 1e-9) spanX = 1.0;
            double spanY = MaxY - MinY; if (Math.Abs(spanY) < 1e-9) spanY = 1.0;
            double spanZ = MaxZ - MinZ; if (Math.Abs(spanZ) < 1e-9) spanZ = 1.0;

            for (int i = 0; i < Nx; i++)
            {
                double xNorm = -1.0 + 2.0 * (GridX[i] - MinX) / spanX;
                for (int j = 0; j < Ny; j++)
                {
                    double yNorm = -1.0 + 2.0 * (GridY[j] - MinY) / spanY;
                    double zVal = GridZ[i, j];
                    if (double.IsNaN(zVal) || double.IsInfinity(zVal)) zVal = MinZ;
                    double zNorm = -1.0 + 2.0 * (zVal - MinZ) / spanZ;
                    if (double.IsNaN(zNorm) || double.IsInfinity(zNorm)) zNorm = 0.0;
                    zNorm = Math.Max(-1.2, Math.Min(1.2, zNorm));

                    ProjectIsometricPoint(xNorm, yNorm, zNorm, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY,
                        out PointF p, out double d);
                    projPts[i, j] = p;
                    depths[i, j] = d;
                }
            }

            var faceList = new List<QuadFace3D>((Nx - 1) * (Ny - 1));

            for (int i = 0; i < Nx - 1; i++)
            {
                for (int j = 0; j < Ny - 1; j++)
                {
                    double z00 = GridZ[i, j];
                    double z10 = GridZ[i + 1, j];
                    double z11 = GridZ[i + 1, j + 1];
                    double z01 = GridZ[i, j + 1];

                    double avgZ = (z00 + z10 + z11 + z01) * 0.25;
                    Color col = EvaluateColormap(ActiveColormap, avgZ, MinZ, MaxZ);

                    double avgDepth = (depths[i, j] + depths[i + 1, j] + depths[i + 1, j + 1] + depths[i, j + 1]) * 0.25;

                    var face = new QuadFace3D
                    {
                        ScreenPoints = new PointF[]
                        {
                            projPts[i, j],
                            projPts[i + 1, j],
                            projPts[i + 1, j + 1],
                            projPts[i, j + 1]
                        },
                        Depth = avgDepth,
                        FillColor = col,
                        I = i,
                        J = j
                    };
                    faceList.Add(face);
                }
            }

            faceList.Sort((a, b) => a.Depth.CompareTo(b.Depth));

            Color wireColor = isDark ? Color.FromArgb(20, 24, 30) : Color.FromArgb(25, 25, 25);
            using (var wirePen = new Pen(wireColor, 0.85f))
            {
                foreach (var face in faceList)
                {
                    using (var fillBrush = new SolidBrush(face.FillColor))
                    {
                        g.FillPolygon(fillBrush, face.ScreenPoints);
                    }

                    if (ShowWireframe)
                    {
                        g.DrawPolygon(wirePen, face.ScreenPoints);
                    }
                }
            }
        }

        internal void DrawAxisTicksAndLabels(
            Graphics g, double theta, double sinPhi, double cosPhi,
            float cx, float cy, float scaleX, float scaleY, bool isDark)
        {
            Color axisLineCol = isDark ? Color.FromArgb(200, 210, 225) : Color.FromArgb(40, 40, 40);
            Color textCol = isDark ? Color.FromArgb(220, 230, 245) : Color.FromArgb(30, 30, 30);
            float fontLabelSz = isDark ? 8.5f : 12f;
            float fontTickSz = isDark ? 7f : 8.5f;

            string fontName = isDark ? GH_FontServer.Standard.FontFamily.Name : "Arial";

            using (var axisPen = new Pen(axisLineCol, isDark ? 1.2f : 1.4f))
            using (var tickPen = new Pen(axisLineCol, 1f))
            using (var labelFont = new Font(fontName, fontLabelSz, FontStyle.Italic | FontStyle.Bold))
            using (var tickFont = new Font(fontName, fontTickSz, FontStyle.Regular))
            using (var textBrush = new SolidBrush(textCol))
            {
                double cosTheta = Math.Cos(theta);
                double sinTheta = Math.Sin(theta);

                // Determinar as bordas frontais voltadas para baixo/frente da tela
                double frontX = (sinTheta >= 0) ? 1.0 : -1.0;
                double frontY = (cosTheta >= 0) ? 1.0 : -1.0;

                // 1. Eixo X
                ProjectIsometricPoint(-1, frontY, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF xStart, out _);
                ProjectIsometricPoint(1, frontY, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF xEnd, out _);
                g.DrawLine(axisPen, xStart, xEnd);

                int xTicksCount = 4;
                for (int i = 0; i <= xTicksCount; i++)
                {
                    double t = -1.0 + 2.0 * i / xTicksCount;
                    double realVal = MinX + (MaxX - MinX) * (i / (double)xTicksCount);
                    ProjectIsometricPoint(t, frontY, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF pTick, out _);

                    float dyTick = isDark ? 3f : 5f;
                    g.DrawLine(tickPen, pTick.X, pTick.Y, pTick.X, pTick.Y + dyTick);

                    string valStr = FormatTickValue(realVal);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
                    g.DrawString(valStr, tickFont, textBrush, pTick.X, pTick.Y + dyTick + 1f, sf);
                }

                PointF xMid = new PointF((xStart.X + xEnd.X) * 0.5f, (xStart.Y + xEnd.Y) * 0.5f);
                var sfXLab = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
                g.DrawString(XLabel, labelFont, textBrush, xMid.X, xMid.Y + (isDark ? 13f : 20f), sfXLab);

                // 2. Eixo Y
                ProjectIsometricPoint(frontX, -1, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF yStart, out _);
                ProjectIsometricPoint(frontX, 1, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF yEnd, out _);
                g.DrawLine(axisPen, yStart, yEnd);

                int yTicksCount = 4;
                for (int j = 0; j <= yTicksCount; j++)
                {
                    double t = -1.0 + 2.0 * j / yTicksCount;
                    double realVal = MinY + (MaxY - MinY) * (j / (double)yTicksCount);
                    ProjectIsometricPoint(frontX, t, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF pTick, out _);

                    float dxTick = isDark ? -3f : -5f;
                    float dyTick = isDark ? 2f : 3f;
                    g.DrawLine(tickPen, pTick.X, pTick.Y, pTick.X + dxTick, pTick.Y + dyTick);

                    string valStr = FormatTickValue(realVal);
                    var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    g.DrawString(valStr, tickFont, textBrush, pTick.X + dxTick - 2f, pTick.Y + dyTick, sf);
                }

                PointF yMid = new PointF((yStart.X + yEnd.X) * 0.5f, (yStart.Y + yEnd.Y) * 0.5f);
                var sfYLab = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                g.DrawString(YLabel, labelFont, textBrush, yMid.X - (isDark ? 14f : 22f), yMid.Y, sfYLab);

                // 3. Eixo Z vertical (no canto que fica mais à esquerda da projeção para não cortar a malha)
                double minU = double.MaxValue;
                double zCornerX = -1.0;
                double zCornerY = -1.0;

                foreach (double cxVal in new double[] { -1.0, 1.0 })
                {
                    foreach (double cyVal in new double[] { -1.0, 1.0 })
                    {
                        double uVal = cxVal * cosTheta - cyVal * sinTheta;
                        if (uVal < minU)
                        {
                            minU = uVal;
                            zCornerX = cxVal;
                            zCornerY = cyVal;
                        }
                    }
                }

                ProjectIsometricPoint(zCornerX, zCornerY, -1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF zBottom, out _);
                ProjectIsometricPoint(zCornerX, zCornerY, 1, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF zTop, out _);
                g.DrawLine(axisPen, zBottom, zTop);

                int zTicksCount = 5;
                for (int k = 0; k <= zTicksCount; k++)
                {
                    double t = -1.0 + 2.0 * k / zTicksCount;
                    double realVal = MinZ + (MaxZ - MinZ) * (k / (double)zTicksCount);
                    ProjectIsometricPoint(zCornerX, zCornerY, t, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, out PointF pTick, out _);

                    float tickLen = isDark ? 3.5f : 5f;
                    g.DrawLine(tickPen, pTick.X - tickLen, pTick.Y, pTick.X, pTick.Y);
                    string valStr = FormatTickValue(realVal);
                    var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    g.DrawString(valStr, tickFont, textBrush, pTick.X - tickLen - 2f, pTick.Y, sf);
                }

                float zMidY = (zBottom.Y + zTop.Y) * 0.5f;
                var sfZLab = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                g.DrawString(ZLabel, labelFont, textBrush, zBottom.X - (isDark ? 28f : 42f), zMidY, sfZLab);
            }
        }

        internal void DrawColorbar(
            Graphics g, RectangleF rect, double minVal, double maxVal, List<Color> cmap, bool isDark)
        {
            int steps = (int)rect.Height;
            if (steps < 20) steps = 20;

            float stepH = rect.Height / steps;
            for (int s = 0; s < steps; s++)
            {
                double factor = 1.0 - (s / (double)steps);
                double val = minVal + factor * (maxVal - minVal);
                Color c = EvaluateColormap(cmap, val, minVal, maxVal);

                using (var b = new SolidBrush(c))
                {
                    g.FillRectangle(b, rect.X, rect.Y + s * stepH, rect.Width, stepH + 0.5f);
                }
            }

            Color borderCol = isDark ? Color.FromArgb(80, 95, 115) : Color.FromArgb(40, 40, 40);
            using (var borderPen = new Pen(borderCol, 1.2f))
            {
                g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);
            }

            int ticksCount = 5;
            Color textCol = isDark ? Color.FromArgb(220, 230, 245) : Color.FromArgb(20, 20, 20);
            float fontSz = isDark ? 7f : 8.5f;

            string fontName = isDark ? GH_FontServer.Standard.FontFamily.Name : "Arial";

            using (var tickPen = new Pen(borderCol, 1f))
            using (var font = new Font(fontName, fontSz, FontStyle.Regular))
            using (var brush = new SolidBrush(textCol))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                for (int i = 0; i <= ticksCount; i++)
                {
                    double factor = 1.0 - (i / (double)ticksCount);
                    double val = minVal + factor * (maxVal - minVal);
                    float y = rect.Y + (float)(i / (double)ticksCount) * rect.Height;

                    g.DrawLine(tickPen, rect.Right, y, rect.Right + (isDark ? 3f : 5f), y);

                    string valStr = FormatTickValue(val);
                    g.DrawString(valStr, font, brush, rect.Right + (isDark ? 4f : 7f), y, sf);
                }
            }
        }

        private static string FormatTickValue(double val)
        {
            if (Math.Abs(val) < 1e-9) return "0.000";
            if (Math.Abs(val) >= 100) return val.ToString("F0");
            if (Math.Abs(val) >= 10) return val.ToString("F1");
            if (Math.Abs(val) >= 1) return val.ToString("F2");
            if (Math.Abs(val) >= 0.01) return val.ToString("F3");
            return val.ToString("F4");
        }

        #endregion

        #region Paletas e Colormaps

        public static Color EvaluateColormap(List<Color> cmap, double val, double min, double max)
        {
            if (cmap == null || cmap.Count == 0) return Color.DodgerBlue;
            if (cmap.Count == 1) return cmap[0];

            if (double.IsNaN(val) || double.IsInfinity(val))
                return Color.FromArgb(160, 160, 160);

            double span = max - min;
            if (Math.Abs(span) < 1e-9) return cmap[cmap.Count / 2];

            double t = (val - min) / span;
            if (double.IsNaN(t) || t <= 0.0) return cmap[0];
            if (t >= 1.0) return cmap[cmap.Count - 1];

            double scaled = t * (cmap.Count - 1);
            int idx = (int)scaled;
            if (idx < 0) idx = 0;
            if (idx >= cmap.Count - 1) return cmap[cmap.Count - 1];

            double frac = scaled - idx;
            Color c1 = cmap[idx];
            Color c2 = (idx + 1 < cmap.Count) ? cmap[idx + 1] : c1;

            int r = (int)(c1.R + frac * (c2.R - c1.R));
            int g = (int)(c1.G + frac * (c2.G - c1.G));
            int b = (int)(c1.B + frac * (c2.B - c1.B));

            return Color.FromArgb(Math.Max(0, Math.Min(255, r)), Math.Max(0, Math.Min(255, g)), Math.Max(0, Math.Min(255, b)));
        }

        private static List<Color> ParseColormap(List<object> objs, out string matchedName)
        {
            var colors = new List<Color>();
            string textName = "";

            if (objs != null)
            {
                foreach (var obj in objs)
                {
                    if (obj == null) continue;
                    if (obj is GH_Colour ghCol)
                    {
                        colors.Add(ghCol.Value);
                    }
                    else if (obj is Color c)
                    {
                        colors.Add(c);
                    }
                    else
                    {
                        string s = obj.ToString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            textName = s;
                        }
                    }
                }
            }

            if (colors.Count >= 2)
            {
                matchedName = "Custom Palette";
                return colors;
            }

            return GetPresetColormap(textName, out matchedName);
        }

        public static List<Color> GetPresetColormap(string name, out string matchedName)
        {
            string lower = (name ?? "").ToLowerInvariant();

            if (lower.Contains("turbo"))
            {
                matchedName = "Turbo";
                return new List<Color>
                {
                    Color.FromArgb(48, 18, 59),
                    Color.FromArgb(70, 134, 251),
                    Color.FromArgb(27, 229, 181),
                    Color.FromArgb(164, 252, 60),
                    Color.FromArgb(251, 185, 56),
                    Color.FromArgb(227, 68, 10),
                    Color.FromArgb(122, 4, 3)
                };
            }

            if (lower.Contains("virid"))
            {
                matchedName = "Viridis";
                return new List<Color>
                {
                    Color.FromArgb(68, 1, 84),
                    Color.FromArgb(71, 44, 122),
                    Color.FromArgb(59, 81, 139),
                    Color.FromArgb(44, 113, 142),
                    Color.FromArgb(33, 144, 141),
                    Color.FromArgb(39, 173, 129),
                    Color.FromArgb(92, 200, 99),
                    Color.FromArgb(170, 220, 50),
                    Color.FromArgb(253, 231, 37)
                };
            }

            if (lower.Contains("infern"))
            {
                matchedName = "Inferno";
                return new List<Color>
                {
                    Color.FromArgb(0, 0, 4),
                    Color.FromArgb(40, 11, 84),
                    Color.FromArgb(101, 21, 110),
                    Color.FromArgb(159, 42, 99),
                    Color.FromArgb(212, 72, 66),
                    Color.FromArgb(245, 125, 21),
                    Color.FromArgb(250, 193, 39),
                    Color.FromArgb(252, 255, 164)
                };
            }

            if (lower.Contains("plasm"))
            {
                matchedName = "Plasma";
                return new List<Color>
                {
                    Color.FromArgb(13, 8, 135),
                    Color.FromArgb(84, 2, 163),
                    Color.FromArgb(139, 10, 165),
                    Color.FromArgb(185, 50, 137),
                    Color.FromArgb(219, 92, 104),
                    Color.FromArgb(244, 136, 73),
                    Color.FromArgb(254, 188, 43),
                    Color.FromArgb(240, 249, 33)
                };
            }

            if (lower.Contains("cool") || lower.Contains("warm") || lower.Contains("bwr"))
            {
                matchedName = "Cool-Warm";
                return new List<Color>
                {
                    Color.FromArgb(59, 76, 192),
                    Color.FromArgb(112, 148, 243),
                    Color.FromArgb(180, 207, 252),
                    Color.FromArgb(238, 238, 238),
                    Color.FromArgb(246, 176, 148),
                    Color.FromArgb(226, 101, 78),
                    Color.FromArgb(180, 4, 38)
                };
            }

            if (lower.Contains("spectr"))
            {
                matchedName = "Spectral";
                return new List<Color>
                {
                    Color.FromArgb(158, 1, 66),
                    Color.FromArgb(213, 62, 79),
                    Color.FromArgb(244, 109, 67),
                    Color.FromArgb(253, 174, 97),
                    Color.FromArgb(254, 224, 139),
                    Color.FromArgb(230, 245, 152),
                    Color.FromArgb(171, 221, 164),
                    Color.FromArgb(102, 194, 165),
                    Color.FromArgb(50, 136, 189),
                    Color.FromArgb(94, 79, 162)
                };
            }

            if (lower.Contains("sunset"))
            {
                matchedName = "Sunset";
                return new List<Color>
                {
                    Color.FromArgb(10, 24, 80),
                    Color.FromArgb(60, 40, 120),
                    Color.FromArgb(140, 40, 120),
                    Color.FromArgb(210, 60, 80),
                    Color.FromArgb(240, 120, 50),
                    Color.FromArgb(250, 200, 70),
                    Color.FromArgb(255, 240, 180)
                };
            }

            if (lower.Contains("grey") || lower.Contains("gray") || lower.Contains("cinza"))
            {
                matchedName = "Greyscale";
                return new List<Color>
                {
                    Color.FromArgb(20, 20, 20),
                    Color.FromArgb(80, 80, 80),
                    Color.FromArgb(140, 140, 140),
                    Color.FromArgb(200, 200, 200),
                    Color.FromArgb(250, 250, 250)
                };
            }

            // Padrão clássico idêntico à imagem de referência: Jet / Rainbow
            matchedName = "Jet";
            return new List<Color>
            {
                Color.FromArgb(0, 0, 143),       // Azul profundo
                Color.FromArgb(0, 0, 255),       // Azul puro
                Color.FromArgb(0, 255, 255),     // Ciano
                Color.FromArgb(128, 255, 128),   // Verde claro
                Color.FromArgb(255, 255, 0),     // Amarelo
                Color.FromArgb(255, 128, 0),     // Laranja
                Color.FromArgb(255, 0, 0),       // Vermelho puro
                Color.FromArgb(128, 0, 0)        // Vermelho escuro
            };
        }

        #endregion

        #region Utilitários de Parsing e Exportação

        private static IsometricQuadrant ParseQuadrant(object obj)
        {
            if (obj == null) return IsometricQuadrant.SW;

            if (obj is int i)
            {
                if (i >= 0 && i <= 3) return (IsometricQuadrant)i;
            }

            if (GH_Convert.ToInt32(obj, out int val, GH_Conversion.Both))
            {
                if (val >= 0 && val <= 3) return (IsometricQuadrant)val;
            }

            string s = obj.ToString().Trim().ToUpperInvariant();
            if (s.Contains("SE") || s.Contains("SUDESTE")) return IsometricQuadrant.SE;
            if (s.Contains("NE") || s.Contains("NORDESTE")) return IsometricQuadrant.NE;
            if (s.Contains("NW") || s.Contains("NOROESTE")) return IsometricQuadrant.NW;
            return IsometricQuadrant.SW;
        }

        private static Interval? ParseInterval(object obj)
        {
            if (obj == null) return null;
            if (obj is GH_Interval ghInt) return ghInt.Value;
            if (obj is Interval interval) return interval;

            string s = obj.ToString().Trim();
            if (s.IndexOf("To", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var parts = s.Split(new[] { "To", "to", "TO", ".." }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 &&
                    double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double min) &&
                    double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double max))
                {
                    return new Interval(min, max);
                }
            }

            return null;
        }

        public string ExportImageWhiteBackground(string folder, out string error)
        {
            error = "";
            try
            {
                string targetDir = folder;
                if (string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir))
                {
                    targetDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                }

                string safeTitle = string.Concat(ChartTitle.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');
                if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Isometric_Surface_Graph";

                string fileName = $"{safeTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string fullPath = Path.Combine(targetDir, fileName);

                using (var bmp = RenderWhiteBackgroundBitmap(1600, 1200))
                {
                    bmp.SetResolution(300, 300);
                    bmp.Save(fullPath, ImageFormat.Png);
                }

                return fullPath;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return "";
            }
        }

        #endregion
    }

    /// <summary>
    /// Atributos gráficos para renderização interativa da Superfície 3D Isométrica no Canvas do Grasshopper.
    /// </summary>
    public class PillIsometricSurfaceGraph_Attributes : GH_ComponentAttributes
    {
        private const int GRAPH_WIDTH = 460;
        private const int GRAPH_HEIGHT = 320;
        private RectangleF m_btnExportRect;

        public PillIsometricSurfaceGraph_Attributes(PillIsometricSurfaceGraph_Component owner) : base(owner)
        {
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && m_btnExportRect.Contains(e.CanvasLocation))
            {
                var comp = Owner as PillIsometricSurfaceGraph_Component;
                if (comp != null)
                {
                    string saved = comp.ExportImageWhiteBackground(comp.LastExportFolder, out string err);
                    if (!string.IsNullOrEmpty(saved))
                    {
                        comp.LastSavedPath = saved;
                        comp.JustSaved = true;
                        comp.Message = "PNG Salvo!";
                        Rhino.RhinoApp.WriteLine($"[IsoSurface] Gráfico de Superfície 3D salvo com fundo branco (300 DPI) em: {saved}");
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
                var comp = Owner as PillIsometricSurfaceGraph_Component;
                if (comp == null) return;

                RectangleF b = Bounds;
                RectangleF graphRect = new RectangleF(b.X + 10, b.Bottom - GRAPH_HEIGHT - 6, b.Width - 20, GRAPH_HEIGHT);
                RectangleF headerRect = new RectangleF(graphRect.X, graphRect.Y, graphRect.Width, 24);
                RectangleF footerRect = new RectangleF(graphRect.X, graphRect.Bottom - 20, graphRect.Width, 20);

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

                // 2. Cabeçalho (Título e Botão Salvar PNG)
                using (var titleFont = new Font(GH_FontServer.Standard.FontFamily, 8f, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(Color.FromArgb(240, 243, 248)))
                {
                    var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                    string displayTitle = comp.ChartTitle;
                    if (displayTitle.Length > 34) displayTitle = displayTitle.Substring(0, 31) + "...";
                    graphics.DrawString(displayTitle, titleFont, titleBrush, headerRect.X + 8, headerRect.Y + headerRect.Height * 0.5f, sfLeft);
                }

                // Botão "📷 Salvar PNG"
                float btnW = 90f;
                float btnH = 16f;
                m_btnExportRect = new RectangleF(headerRect.Right - btnW - 6, headerRect.Y + 4, btnW, btnH);

                using (var btnBrush = new SolidBrush(Color.FromArgb(40, 48, 62)))
                using (var btnPen = new Pen(Color.FromArgb(80, 92, 110), 1f))
                using (var btnFont = new Font(GH_FontServer.Standard.FontFamily, 7f, FontStyle.Bold))
                using (var btnTextBrush = new SolidBrush(Color.FromArgb(180, 210, 245)))
                {
                    graphics.FillRectangle(btnBrush, m_btnExportRect);
                    graphics.DrawRectangle(btnPen, m_btnExportRect.X, m_btnExportRect.Y, m_btnExportRect.Width, m_btnExportRect.Height);
                    var sfBtn = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    graphics.DrawString("📷 Salvar PNG", btnFont, btnTextBrush, m_btnExportRect, sfBtn);
                }

                // 3. Layout Interno do Gráfico e Colorbar
                float innerTop = headerRect.Bottom + 4;
                float innerBottom = footerRect.Y - 4;
                float innerHeight = innerBottom - innerTop;

                // Colorbar à direita
                float cbWidth = 12f;
                float cbTotalW = 54f;
                float cbRight = graphRect.Right - 8f;
                float cbLeft = cbRight - cbTotalW;
                RectangleF colorbarRect = new RectangleF(cbLeft, innerTop + 10f, cbWidth, innerHeight - 20f);

                // Área da Superfície 3D
                float plotLeft = graphRect.X + 6;
                float plotRight = cbLeft - 6;
                RectangleF plotRect = new RectangleF(plotLeft, innerTop, plotRight - plotLeft, innerHeight);

                // Fundo da área do gráfico
                using (var plotBrush = new SolidBrush(Color.FromArgb(12, 14, 18)))
                using (var plotBorderPen = new Pen(Color.FromArgb(45, 52, 65), 1f))
                {
                    graphics.FillRectangle(plotBrush, plotRect);
                    graphics.DrawRectangle(plotBorderPen, plotRect.X, plotRect.Y, plotRect.Width, plotRect.Height);
                }

                // 4. Renderização do Gráfico 3D Isométrico com Clip de Proteção Absoluta
                var prevClip = graphics.Clip;
                graphics.SetClip(graphRect);

                try
                {
                    if (comp.Nx >= 2 && comp.Ny >= 2)
                    {
                        double theta = PillIsometricSurfaceGraph_Component.GetQuadrantAzimuth(comp.Quadrant);
                        double phi = comp.ElevationDeg * Math.PI / 180.0;
                        double sinPhi = Math.Sin(phi);
                        double cosPhi = Math.Cos(phi);

                        // Ajuste de escala analítico para que todo o cubo 3D + eixos + ticks + rótulos caiba 100%
                        PillIsometricSurfaceGraph_Component.ComputeFitTransform(
                            theta, sinPhi, cosPhi, plotRect,
                            padLeft: 46f, padRight: 10f, padTop: 10f, padBottom: 28f,
                            out float cx, out float cy, out float scaleX, out float scaleY);

                        // Planos de fundo da Bounding Box
                        comp.DrawBoundingBoxPlanes(graphics, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, isDark: true);

                        // Superfície 3D
                        comp.DrawSurfaceFaces(graphics, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, isDark: true);

                        // Eixos e ticks (com rótulos x1, x2, D1 perfeitamente encaixados)
                        comp.DrawAxisTicksAndLabels(graphics, theta, sinPhi, cosPhi, cx, cy, scaleX, scaleY, isDark: true);

                        // Colorbar lateral à direita
                        comp.DrawColorbar(graphics, colorbarRect, comp.MinZ, comp.MaxZ, comp.ActiveColormap, isDark: true);
                    }
                    else
                    {
                        using (var emptyFont = new Font(GH_FontServer.Standard.FontFamily, 7.5f, FontStyle.Regular))
                        using (var emptyBrush = new SolidBrush(Color.FromArgb(140, 150, 165)))
                        {
                            var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            graphics.DrawString("Conecte os dados X, Y e Z para renderizar a superfície 3D isométrica", emptyFont, emptyBrush, plotRect, sfCenter);
                        }
                    }
                }
                finally
                {
                    graphics.Clip = prevClip;
                }

                // 5. Rodapé (Metadados da visualização)
                using (var footerFont = new Font(GH_FontServer.Standard.FontFamily, 7f, FontStyle.Regular))
                using (var footerBrush = new SolidBrush(Color.FromArgb(140, 150, 165)))
                {
                    var sfFooter = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                    string info = $"Grade: {comp.Nx}x{comp.Ny} ({comp.Nx * comp.Ny} pts) | Visão: {comp.Quadrant} ({comp.ElevationDeg:F0}°) | Paleta: {comp.ActiveColormapName}";
                    graphics.DrawString(info, footerFont, footerBrush, footerRect.X + 8, footerRect.Y + footerRect.Height * 0.5f, sfFooter);
                }
            }
        }
    }
}
