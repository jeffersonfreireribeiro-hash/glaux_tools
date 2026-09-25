using GH_IO.Serialization;
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
using Rhino.Display;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    public enum HeatmapDisplayMode
    {
        Standard = 0,        // 0: Gradiente Contínuo Padrão (Valores Absolutos de MinVal a MaxVal)
        TargetDeviation = 1, // 1: Desvio do Alvo (|v - Target|, 0 a Max Dev)
        TargetDiagnostic = 2 // 2: Diagnóstico Editorial do Professor (Tricolor Aquém/Além + Máscara de Tolerância)
    }

    public class SpatialHeatmap_Component : GH_Component
    {
        public HeatmapDisplayMode DisplayMode = HeatmapDisplayMode.Standard;
        private Mesh _cachedMesh;
        private BoundingBox _cachedBbox;
        private bool _previewInViewport = true;
        private List<Point3d> _cachedSamplePts = new List<Point3d>();
        private List<double> _cachedSampleVals = new List<double>();

        // Cache para renderização no Canvas e Exportação
        public Bitmap CanvasHeatmapBmp;
        public string ActiveGradientName = "Turbo";
        public List<Color> ActiveColorStops = new List<Color>();
        public double MinVal = 0, MaxVal = 1;
        public double DataMinVal = 0, DataMaxVal = 1, DataAvgVal = 0;
        public int SamplePointsCount = 0;
        public double? IdealTargetVal = null;
        public int GridResX = 40, GridResY = 40;
        public double IdwPower = 2.0;

        // Campos editoriais
        public string TitleText = "Heatmap Espacial";
        public string UnitText = "";
        public string DataSourceText = "Pachyderm Simulation · ISO 3382";
        public Interval? UserMinMax = null;
        public bool FollowPointsSlope = true;
        public bool Has3DSlope = false;
        public bool NormalizeElevation = false;

        // Modo Diagnóstico de Alvo (Sugestão Editorial do Professor: Tricolor + Máscara de Tolerância)
        public bool UseTargetMaskMode = false;
        public double TargetTolerance = 0.10; // 10% por padrão (JND acústico ISO 3382)
        public bool ToleranceIsPercent = true;
        public Color TargetMaskColor = Color.FromArgb(255, 0, 160); // Magenta vibrante de contraste
        public bool IsTricolorActive = false;
        public double TolMinVal = 0, TolMaxVal = 0, TolAbsVal = 0;
        public double PercentInTarget = 0, PercentBelowTarget = 0, PercentAboveTarget = 0;

        // Cache de matriz para exportação em alta resolução
        public double[,] CachedGridMatrix;
        public double[,] CachedDevMatrix;
        public double CachedMaxDev = 1.0;
        public bool CachedHasTarget = false;
        public double CachedMinX = 0, CachedMaxX = 1, CachedMinY = 0, CachedMaxY = 1;

        public SpatialHeatmap_Component()
            : base(
                "Spatial Grid & Viewport Heatmap",
                "SpatialHeatmap",
                "Espalha e interpola dados escalares em uma grade espacial com gradiente customizável, exibindo o MAPA DE CALOR DIRETAMENTE NO PAINEL DO CANVAS DO GRASSHOPPER e NO VIEWPORT 3D DO RHINO. Inclui limites manuais Min/Max, título, unidade de medida, fonte dos dados e exportação editorial.",
                "Glaux Tools",
                "Visual")
        {
        }

        public override Guid ComponentGuid => new Guid("7d2e3f4a-5b6c-7d8e-9f0a-1b2c3d4e5f6a");

        protected override Bitmap Icon => GlauxToolsIcons.SpatialHeatmap;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override void CreateAttributes()
        {
            m_attributes = new SpatialHeatmap_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Sample Points", "Pts", "Pontos de medição ou coordenadas dos sensores no espaço (2D ou 3D).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Values", "V", "Valores numéricos associados a cada ponto de medição.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Ideal Target", "Target", "Valor alvo ideal opcional. Aceita:\n1. Número direto (ex: 1.12)\n2. Intervalo/Domínio normativo (ex: 0.95 To 1.29) com tolerância automática\n3. Lista de metas por oitava (seleção inteligente pela frequência no título do gráfico)", GH_ParamAccess.list);
            pManager.AddGenericParameter("Gradient / Palette", "Grad", "Seletor de Gradiente ou Cores customizadas. Aceita:\n1. Lista de Cores personalizadas (do componente Gradient ou Swatch do GH)\n2. Número Inteiro de Preset (0 a 14)\n3. Nome do Preset ('Turbo', 'Viridis', 'Thermal', 'Plasma', 'Magma', 'Inferno', 'Target', 'TargetDiverging', 'CoolWarm', 'Cividis', 'Spectral', 'Sunset', 'Ocean', 'Forest', 'Greyscale')", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Invert Gradient", "Inv", "Inverter o sentido das cores no gradiente.", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("Resolution", "Res", "Resolução da grade/número de divisões por eixo (ex.: 35 a 100). Padrão: 40.", GH_ParamAccess.item, 40);
            pManager.AddNumberParameter("IDW Power", "P", "Expoente da ponderação pelo inverso da distância (IDW Power). Padrão: 2.0.", GH_ParamAccess.item, 2.0);
            pManager.AddBooleanParameter("Show in Viewport", "Preview", "Renderizar o heatmap colorido e malha diretamente no Viewport 3D do Rhino.", GH_ParamAccess.item, true);
            pManager.AddGenericParameter("Z Elevation Scale", "ZScale", "Fator de elevação Z dos vértices da malha (Heatmap 3D / Landscape). Aceita:\n1. Fator multiplicador direto (ex: 1.0, 5.0, amplificando até 10x ou mais)\n2. Domínio/Intervalo de normalização (ex: '0.2 To 2.5' ou Interval(0.2, 2.5)), onde o primeiro valor é a distância vertical inicial acima do ponto (ZBase) e o segundo é a cota máxima (ZMax), distribuindo os dados normalizados nessa altura\n3. Texto (ex: 'norm 2.5', '0.5, 3.0', '10x'). Padrão: 0.0 (plano).", GH_ParamAccess.item);

            // Novos parâmetros editoriais solicitados pelo usuário
            pManager.AddTextParameter("Chart Title", "Title", "Título do mapa de calor (ex.: 'Distribuição Espacial: C80' ou 'Mapa de T30'). Se omitido, deduce automaticamente da entrada V ou adota 'Heatmap Espacial'.", GH_ParamAccess.item);
            pManager.AddTextParameter("Unit", "Unit", "Unidade de medida dos dados (ex.: 'dB', 's', '°C', '%', 'm/s'). Se omitido, deduce automaticamente se houver indicação no dado conectado.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Value Limits (Min/Max)", "Limits", "Limites manuais de Mínimo e Máximo como Domínio/Intervalo (ex: Interval(0, 2) ou '0 To 2.5' ou '-25 To 25'). Se não fornecido, ajusta automaticamente à faixa real dos dados.", GH_ParamAccess.item);
            pManager.AddTextParameter("Data Source", "Source", "Fonte dos dados / metadados exibidos no rodapé do gráfico (ex: 'Pachyderm Simulation · ISO 3382', 'Medição In Situ').", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Follow Slope", "Slope", "Seguir a inclinação 3D da malha de pontos no gradiente gerado no Rhino (ex.: plateia inclinada de teatro/auditório). Quando falso, gera o gradiente em um plano horizontal nivelado (Z constante). Padrão: true.", GH_ParamAccess.item, true);
            pManager.AddGenericParameter("Target Tolerance & Mask", "TolMask", "Tolerância do Alvo e Máscara de Conformidade (Modo Diagnóstico do Professor).\nPermite diagnosticar visualmente o que está ABAIXO (aquém) e ACIMA (além) do alvo em gradiente tricolor (Azul = Abaixo, Vermelho = Acima) com sobreposição de MÁSCARA contrastante (Magenta, Ciano, Verde, etc.) exatamente nas áreas em conformidade com o TR ideal.\nAceita:\n- Valor de tolerância numérico ou texto (ex: 0.15 ou '10%')\n- Cor da máscara (ex: Magenta, Ciano, Swatch do GH)\n- Texto combinado (ex: '0.15 Magenta', '±10% Ciano', '0.2 Verde').", GH_ParamAccess.item);
            pManager.AddGenericParameter("Display Mode", "Mode", "Modo de Visualização do Heatmap (Escolha entre as 3 Opções):\n0 = Gradiente Contínuo Padrão (Valores Absolutos de Mínimo a Máximo)\n1 = Desvio do Alvo (|v - Target|, de 0 a Desvio Máximo)\n2 = Diagnóstico de Alvo (Sugestão do Professor: Tricolor [Azul/Vermelho] + Máscara de Tolerância [Magenta/Ciano]).\nAceita: 0, 1, 2 ou textos ('Padrão', 'Desvio', 'Diagnóstico', 'Tricolor').\nSe não conectado: adota Modo 2 se houver Alvo e TolMask; Modo 1 se houver Alvo; ou Modo 0.", GH_ParamAccess.item);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
            pManager[8].Optional = true;
            pManager[9].Optional = true;
            pManager[10].Optional = true;
            pManager[11].Optional = true;
            pManager[12].Optional = true;
            pManager[13].Optional = true;
            pManager[14].Optional = true;
            pManager[15].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Heatmap Mesh", "M", "Malha Rhino (Mesh) gerada com gradiente de cores aplicado aos vértices.", GH_ParamAccess.item);
            pManager.AddPointParameter("Grid Points", "GridPts", "Árvore de pontos amostrados da grade regular (U, V).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Interpolated Values", "GridVals", "Árvore de valores escalares interpolados em cada nó da grade.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Target Deviations", "Devs", "Árvore de desvios absolutos (|v - Target|) em relação ao valor ideal.", GH_ParamAccess.tree);
            pManager.AddColourParameter("Vertex Colors", "Cols", "Lista de cores (System.Drawing.Color) aplicadas aos vértices.", GH_ParamAccess.list);
            pManager.AddColourParameter("Palette Stops", "Pal", "Lista de cores de parada do gradiente ativo.", GH_ParamAccess.list);
            pManager.AddTextParameter("Heatmap Report", "Rep", "Diagnóstico espacial com estatísticas de interpolação, extremos, gradiente e conformidade com o alvo.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var inPts = new List<Point3d>();
            if (!DA.GetDataList(0, inPts) || inPts == null || inPts.Count == 0)
            {
                this.Message = "Sem Pontos";
                _cachedMesh = null;
                CanvasHeatmapBmp = null;
                return;
            }

            var inVals = new List<double>();
            if (!DA.GetDataList(1, inVals) || inVals == null || inVals.Count == 0)
            {
                this.Message = "Sem Valores";
                _cachedMesh = null;
                CanvasHeatmapBmp = null;
                return;
            }

            // 1. Título e Unidade com Auto-Detecção
            string userTitle = "";
            DA.GetData(9, ref userTitle);

            string userUnit = "";
            DA.GetData(10, ref userUnit);

            AutoDetectTitleAndUnit(out string autoTitle, out string autoUnit);

            TitleText = !string.IsNullOrWhiteSpace(userTitle) ? userTitle.Trim() : autoTitle;
            UnitText = !string.IsNullOrWhiteSpace(userUnit) ? userUnit.Trim() : autoUnit;

            // 2. Alvo Ideal com Suporte a Lista de Bandas, Intervalo ou Número
            var rawTargets = new List<object>();
            DA.GetDataList(2, rawTargets);

            double? idealTarget = null;
            string autoTargetMsg = "";
            double tolVal = TargetTolerance;
            bool tolIsPct = ToleranceIsPercent;
            Color maskCol = TargetMaskColor;
            bool isTricolorActive = false;

            if (rawTargets != null && rawTargets.Count > 0)
            {
                ParseTargetAndTolerance(rawTargets, TitleText, ref idealTarget, ref tolVal, ref tolIsPct, ref isTricolorActive, ref autoTargetMsg);
            }
            IdealTargetVal = idealTarget;

            var rawGradInputs = new List<object>();
            DA.GetDataList(3, rawGradInputs);

            bool invertGrad = false;
            DA.GetData(4, ref invertGrad);

            int res = 40;
            DA.GetData(5, ref res);
            if (res < 5) res = 5;
            if (res > 250) res = 250;
            GridResX = res;
            GridResY = res;

            double power = 2.0;
            DA.GetData(6, ref power);
            if (power <= 0) power = 2.0;
            IdwPower = power;

            bool showInVp = true;
            DA.GetData(7, ref showInVp);
            _previewInViewport = showInVp;

            IGH_Goo rawZGoo = null;
            DA.GetData(8, ref rawZGoo);

            bool isZNormalized = NormalizeElevation;
            double zBase = 0.0;
            double zSpan = 0.0;
            double zScale = 0.0;

            if (rawZGoo != null)
            {
                TryParseZElevation(rawZGoo, 0.0, NormalizeElevation, out isZNormalized, out zBase, out zSpan, out zScale);
            }

            // 3. Limites Min/Max (Intervalo/Domínio)
            Interval? userLim = null;
            IGH_Goo rawLimitsGoo = null;
            if (DA.GetData(11, ref rawLimitsGoo) && rawLimitsGoo != null)
            {
                userLim = ParseInterval(rawLimitsGoo);
            }
            UserMinMax = userLim;

            // 4. Fonte de Dados
            string userSource = "";
            DA.GetData(12, ref userSource);
            DataSourceText = !string.IsNullOrWhiteSpace(userSource) ? userSource.Trim() : "Pachyderm Simulation · ISO 3382";

            // 5. Seguir a Inclinação dos Pontos (3D Slope)
            bool followSlope = FollowPointsSlope;
            DA.GetData(13, ref followSlope);
            FollowPointsSlope = followSlope;

            // 6. Tolerância e Máscara de Destaque do Alvo (Sugestão do Professor)
            object rawTolMask = null;
            DA.GetData(14, ref rawTolMask);

            if (rawTolMask != null)
            {
                ParseToleranceAndMask(rawTolMask, ref tolVal, ref tolIsPct, ref maskCol);
            }

            // 7. Modo de Exibição / Diagnóstico (Input 15 para alternar entre as 3 opções)
            object rawMode = null;
            DA.GetData(15, ref rawMode);

            HeatmapDisplayMode currentMode = DisplayMode;
            if (rawMode != null)
            {
                currentMode = ParseDisplayMode(rawMode);
            }
            else if (rawTolMask != null && TryParseDisplayMode(rawTolMask, out HeatmapDisplayMode modeFromTol))
            {
                currentMode = modeFromTol;
            }
            else if (isTricolorActive || UseTargetMaskMode)
            {
                currentMode = HeatmapDisplayMode.TargetDiagnostic;
            }
            else if (idealTarget.HasValue)
            {
                if (rawTolMask != null)
                    currentMode = HeatmapDisplayMode.TargetDiagnostic;
                else
                    currentMode = HeatmapDisplayMode.TargetDeviation;
            }
            else
            {
                currentMode = HeatmapDisplayMode.Standard;
            }

            // Garantir que cor da máscara nunca seja transparente/preta vazia
            if (maskCol.IsEmpty || maskCol.A < 20 || (maskCol.R == 0 && maskCol.G == 0 && maskCol.B == 0))
            {
                maskCol = Color.FromArgb(255, 0, 160); // Magenta vibrante
            }

            DisplayMode = currentMode;
            isTricolorActive = (currentMode == HeatmapDisplayMode.TargetDiagnostic && idealTarget.HasValue);
            bool hasTargetDev = (currentMode == HeatmapDisplayMode.TargetDeviation && idealTarget.HasValue);

            if (inPts.Count != inVals.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Pts e V devem ter o mesmo tamanho. Preserve NaN e a ordem dos receptores; remover apenas valores desloca o mapa.");
                _cachedMesh = null;
                CanvasHeatmapBmp = null;
                return;
            }
            int count = inPts.Count;
            var validPts = new List<Point3d>();
            var validVals = new List<double>();

            for (int i = 0; i < count; i++)
            {
                var pt = inPts[i];
                double v = inVals[i];
                if (!double.IsNaN(v) && !double.IsInfinity(v) && pt.IsValid)
                {
                    validPts.Add(pt);
                    validVals.Add(v);
                }
            }

            if (validPts.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum ponto ou valor válido.");
                _cachedMesh = null;
                CanvasHeatmapBmp = null;
                return;
            }

            if (validPts.Count < count)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    $"{count - validPts.Count}/{count} pares indisponíveis. IDW interpola entre pares válidos; isso não valida o TR ausente.");
            _cachedSamplePts = validPts;
            _cachedSampleVals = validVals;
            SamplePointsCount = validPts.Count;

            var bbox = new BoundingBox(validPts);
            if (bbox.Diagonal.Length < 1e-6)
            {
                bbox.Inflate(1.0, 1.0, 1.0);
            }
            else
            {
                double dx = bbox.Max.X - bbox.Min.X;
                double dy = bbox.Max.Y - bbox.Min.Y;
                double inflX = dx > 1e-6 ? dx * 0.05 : 1.0;
                double inflY = dy > 1e-6 ? dy * 0.05 : 1.0;
                bbox.Inflate(inflX, inflY, 0.0);
            }

            double minX = bbox.Min.X, maxX = bbox.Max.X;
            double minY = bbox.Min.Y, maxY = bbox.Max.Y;
            double baseZ = (bbox.Min.Z + bbox.Max.Z) * 0.5;

            CachedMinX = minX;
            CachedMaxX = maxX;
            CachedMinY = minY;
            CachedMaxY = maxY;

            double dataMin = validVals.Min();
            double dataMax = validVals.Max();
            double dataAvg = validVals.Average();

            DataMinVal = dataMin;
            DataMaxVal = dataMax;
            DataAvgVal = dataAvg;

            // Verificar se o gradiente selecionado é o preset Tricolor/TargetMask
            List<Color> colorStops = ResolveGradientStops(rawGradInputs, hasTargetDev, out string gradientName);
            if (gradientName.IndexOf("Tricolor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gradientName.IndexOf("Mask", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                isTricolorActive = true;
                DisplayMode = HeatmapDisplayMode.TargetDiagnostic;
            }

            double tolAbs = 0.0;
            double tMin = 0.0, tMax = 0.0;
            if (idealTarget.HasValue && isTricolorActive)
            {
                tolAbs = tolIsPct ? (idealTarget.Value * tolVal) : tolVal;
                if (tolAbs <= 1e-6) tolAbs = idealTarget.Value * 0.10; // 10% JND
                tMin = idealTarget.Value - tolAbs;
                tMax = idealTarget.Value + tolAbs;
            }

            // Determinar MinVal e MaxVal ativos (manuais ou automáticos com regra do professor /2 e 2x)
            double activeMin = dataMin;
            double activeMax = dataMax;

            if (userLim.HasValue && userLim.Value.Length > 1e-6)
            {
                activeMin = userLim.Value.Min;
                activeMax = userLim.Value.Max;
            }
            else if (idealTarget.HasValue && isTricolorActive)
            {
                // Alvo perfeitamente centralizado no meio da escala (50% do domínio)
                double tgt = idealTarget.Value;
                double delta = tgt; // padrão: 2x do TR ideal (delta = tgt -> Min = 0, Max = 2*tgt)
                if (dataMax - tgt > delta) delta = dataMax - tgt;
                if (tgt - dataMin > delta) delta = tgt - dataMin;
                if (tolAbs * 2.0 > delta) delta = tolAbs * 2.0;

                // Manter simetria absoluta em torno do Alvo para que ele fique rigorosamente no centro (50%)
                activeMin = Math.Max(0.0, tgt - delta);
                activeMax = tgt + delta;
                if (activeMin > tMin - tolAbs) activeMin = Math.Max(0.0, tMin - tolAbs);
                if (activeMax < tMax + tolAbs) activeMax = tMax + tolAbs;

                double distLeft = tgt - activeMin;
                double distRight = activeMax - tgt;
                double maxDist = Math.Max(distLeft, distRight);
                activeMin = Math.Max(0.0, tgt - maxDist);
                activeMax = tgt + maxDist;
            }

            double valSpan = activeMax - activeMin;
            if (valSpan <= 1e-9) valSpan = 1.0;

            MinVal = activeMin;
            MaxVal = activeMax;
            IsTricolorActive = isTricolorActive && idealTarget.HasValue;
            TolMinVal = tMin;
            TolMaxVal = tMax;
            TolAbsVal = tolAbs;
            TargetMaskColor = maskCol;
            if (invertGrad && colorStops != null && colorStops.Count > 1)
            {
                colorStops = new List<Color>(colorStops);
                colorStops.Reverse();
                gradientName += " (Invertido)";
            }

            if (IsTricolorActive)
            {
                ActiveGradientName = "Diagnóstico: Tricolor + Máscara";
            }
            else if (hasTargetDev)
            {
                ActiveGradientName = $"{gradientName} (Desvio)";
            }
            else
            {
                ActiveGradientName = gradientName;
            }
            ActiveColorStops = colorStops;

            int nx = res;
            int ny = res;
            double stepX = (maxX - minX) / (nx - 1);
            double stepY = (maxY - minY) / (ny - 1);

            var mesh = new Mesh();
            var outGridPts = new GH_Structure<GH_Point>();
            var outGridVals = new GH_Structure<GH_Number>();
            var outDevs = new GH_Structure<GH_Number>();
            var outColors = new List<GH_Colour>();

            double[,] gridMatrix = new double[nx, ny];
            double[,] devMatrix = new double[nx, ny];

            double maxDev = 0.0;
            if (idealTarget.HasValue)
            {
                if (userLim.HasValue && userLim.Value.Max > 0)
                {
                    maxDev = userLim.Value.Max;
                }
                else
                {
                    for (int i = 0; i < validVals.Count; i++)
                    {
                        double d = Math.Abs(validVals[i] - idealTarget.Value);
                        if (d > maxDev) maxDev = d;
                    }
                    if (maxDev <= 1e-9) maxDev = 1.0;
                }
            }

            // 1. Modelo de Inclinação 3D dos Pontos (Plateia / Rake / Topografia)
            PointSlopeModel slopeModel = followSlope ? PointSlopeModel.Build(validPts) : null;
            Has3DSlope = (followSlope && slopeModel != null && slopeModel.HasSlope);
            double[,] gridZMatrix = new double[nx, ny];

            // Interpolação IDW (com coordenadas Z inclinadas para distância 3D exata)
            for (int ix = 0; ix < nx; ix++)
            {
                double gx = minX + ix * stepX;
                for (int iy = 0; iy < ny; iy++)
                {
                    double gy = minY + iy * stepY;
                    double gz = Has3DSlope ? slopeModel.EvaluateZ(gx, gy, power) : baseZ;
                    gridZMatrix[ix, iy] = gz;

                    var nodePt = new Point3d(gx, gy, gz);

                    double interpVal = InterpolateIDW(nodePt, validPts, validVals, power);
                    gridMatrix[ix, iy] = interpVal;

                    if (idealTarget.HasValue)
                    {
                        double dev = IsTricolorActive ? (interpVal - idealTarget.Value) : Math.Abs(interpVal - idealTarget.Value);
                        devMatrix[ix, iy] = dev;
                        double absDev = Math.Abs(interpVal - idealTarget.Value);
                        if (!userLim.HasValue && absDev > maxDev) maxDev = absDev;
                    }
                }
            }

            CachedGridMatrix = gridMatrix;
            CachedDevMatrix = devMatrix;
            CachedMaxDev = maxDev;
            CachedHasTarget = hasTargetDev;

            // 2. Construção da Malha e Cores (seguindo a inclinação 3D no Rhino)
            // Determinar extremos reais da distribuição interpolada na grade para normalização fiel de Z
            double gridMinVal = double.MaxValue;
            double gridMaxVal = double.MinValue;
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iy = 0; iy < ny; iy++)
                {
                    double gv = gridMatrix[ix, iy];
                    if (gv < gridMinVal) gridMinVal = gv;
                    if (gv > gridMaxVal) gridMaxVal = gv;
                }
            }

            double zNormMin = (gridMinVal < double.MaxValue) ? gridMinVal : dataMin;
            double zNormMax = (gridMaxVal > double.MinValue) ? gridMaxVal : dataMax;
            double zNormRange = zNormMax - zNormMin;

            for (int ix = 0; ix < nx; ix++)
            {
                var ghPath = new GH_Path(ix);
                outGridPts.EnsurePath(ghPath);
                outGridVals.EnsurePath(ghPath);
                outDevs.EnsurePath(ghPath);

                double gx = minX + ix * stepX;
                for (int iy = 0; iy < ny; iy++)
                {
                    double gy = minY + iy * stepY;
                    double gz = gridZMatrix[ix, iy];
                    double v = gridMatrix[ix, iy];
                    double vz = gz;

                    if (isZNormalized)
                    {
                        double tNorm = zNormRange > 1e-9 ? Math.Max(0.0, Math.Min(1.0, (v - zNormMin) / zNormRange)) : 0.0;
                        vz = gz + zBase + tNorm * zSpan;
                    }
                    else if (zScale != 0.0)
                    {
                        vz = gz + (v - zNormMin) * zScale;
                    }

                    var pt = new Point3d(gx, gy, vz);
                    mesh.Vertices.Add((float)pt.X, (float)pt.Y, (float)pt.Z);

                    Color c;
                    if (isTricolorActive && idealTarget.HasValue)
                    {
                        c = GetTricolorMaskColor(v, idealTarget.Value, tolAbs, activeMin, activeMax, maskCol);
                        double devSigned = v - idealTarget.Value;
                        outDevs.Append(new GH_Number(devSigned), ghPath);
                    }
                    else if (hasTargetDev)
                    {
                        double dev = devMatrix[ix, iy];
                        double t = Math.Max(0.0, Math.Min(1.0, dev / (maxDev > 1e-9 ? maxDev : 1.0)));
                        c = SampleGradient(colorStops, t);
                        outDevs.Append(new GH_Number(dev), ghPath);
                    }
                    else
                    {
                        double t = Math.Max(0.0, Math.Min(1.0, (v - activeMin) / valSpan));
                        c = SampleGradient(colorStops, t);
                        outDevs.Append(new GH_Number(idealTarget.HasValue ? (v - idealTarget.Value) : 0.0), ghPath);
                    }

                    mesh.VertexColors.Add(c);
                    outColors.Add(new GH_Colour(c));
                    outGridPts.Append(new GH_Point(pt), ghPath);
                    outGridVals.Append(new GH_Number(v), ghPath);
                }
            }

            for (int ix = 0; ix < nx - 1; ix++)
            {
                for (int iy = 0; iy < ny - 1; iy++)
                {
                    int i0 = ix * ny + iy;
                    int i1 = (ix + 1) * ny + iy;
                    int i2 = (ix + 1) * ny + (iy + 1);
                    int i3 = ix * ny + (iy + 1);
                    mesh.Faces.AddFace(i0, i1, i2, i3);
                }
            }

            mesh.Normals.ComputeNormals();
            mesh.Compact();

            _cachedMesh = mesh;
            _cachedBbox = mesh.GetBoundingBox(true);

            // Calcular estatísticas de conformidade com o Alvo (Dentro, Abaixo, Acima)
            if (IsTricolorActive && idealTarget.HasValue)
            {
                int cIn = 0, cBelow = 0, cAbove = 0;
                int totalN = nx * ny;
                for (int ix = 0; ix < nx; ix++)
                {
                    for (int iy = 0; iy < ny; iy++)
                    {
                        double v = gridMatrix[ix, iy];
                        if (v >= tMin && v <= tMax) cIn++;
                        else if (v < tMin) cBelow++;
                        else cAbove++;
                    }
                }
                PercentInTarget = (cIn * 100.0) / totalN;
                PercentBelowTarget = (cBelow * 100.0) / totalN;
                PercentAboveTarget = (cAbove * 100.0) / totalN;
            }

            // Gerar Imagem 2D do Heatmap para o Painel do Canvas com interpolação suave
            CanvasHeatmapBmp = RenderCanvasHeatmap(gridMatrix, devMatrix, nx, ny, activeMin, valSpan, maxDev, colorStops, hasTargetDev);

            // Relatório
            var repBuilder = new StringBuilder();
            repBuilder.AppendLine("=================================================");
            repBuilder.AppendLine($"       BURAQUEIRA SPATIAL HEATMAP REPORT");
            repBuilder.AppendLine($"       Título:            {GetFullTitle()}");
            repBuilder.AppendLine($"       Fonte dos Dados:   {DataSourceText}");
            repBuilder.AppendLine($"       Unidade:           {UnitText}");
            repBuilder.AppendLine($"       Gradiente Ativo:   {gradientName} ({colorStops.Count} cores de parada)");
            repBuilder.AppendLine($"       Pontos de Amostra: {validPts.Count}");
            repBuilder.AppendLine($"       Resolução:         {nx} x {ny} ({nx * ny} vértices, {(nx - 1) * (ny - 1)} faces)");
            repBuilder.AppendLine($"       IDW Power:         {power:F1}");
            string slopeDesc = "Horizontal Nivelado (Z = " + baseZ.ToString("F2") + ")";
            if (Has3DSlope)
            {
                slopeDesc = slopeModel.IsPlanar
                    ? $"Plano Inclinado (Normal: [{slopeModel.Normal.X:F2}, {slopeModel.Normal.Y:F2}, {slopeModel.Normal.Z:F2}], Z: {validPts.Min(p => p.Z):F2} a {validPts.Max(p => p.Z):F2})"
                    : $"Superfície Inclinada com Resíduos (Z: {validPts.Min(p => p.Z):F2} a {validPts.Max(p => p.Z):F2})";
            }
            repBuilder.AppendLine($"       Ajuste de Inclinação: {slopeDesc}");
            repBuilder.AppendLine("=================================================");
            repBuilder.AppendLine($"  Faixa Real (Dados):  [{dataMin:F4} .. {dataMax:F4}] {UnitText} (Span = {dataMax - dataMin:F4})");
            repBuilder.AppendLine($"  Limites Ativos:      [{activeMin:F4} .. {activeMax:F4}] {UnitText}" + (userLim.HasValue ? " [Definido Manualmente]" : " [Automático]"));
            repBuilder.AppendLine($"  Média Amostral:      {dataAvg:F4} {UnitText}");
            repBuilder.AppendLine($"  Dimensões Bounding:  ΔX = {maxX - minX:F2}, ΔY = {maxY - minY:F2}");
            if (isZNormalized)
            {
                repBuilder.AppendLine($"  Elevação 3D (Z):     Normalizada [ZMin = {zBase:F2}, ZMax = {zBase + zSpan:F2}] m acima da superfície/pontos (Valores mínimos em ZMin e máximos em ZMax)");
            }
            else if (zScale != 0.0)
            {
                repBuilder.AppendLine($"  Elevação 3D (Z):     Escalar [{zScale:F2}x] a partir dos valores mínimos");
            }
            else
            {
                repBuilder.AppendLine($"  Elevação 3D (Z):     Plano 2D (0.0)");
            }

            if (idealTarget.HasValue)
            {
                double target = idealTarget.Value;
                double rmseTarget = Math.Sqrt(validVals.Average(v => (v - target) * (v - target)));
                double bestVal = validVals.OrderBy(v => Math.Abs(v - target)).First();

                repBuilder.AppendLine($"\n--- ANÁLISE DE PROXIMIDADE DE ALVO ---");
                repBuilder.AppendLine($"  Valor Alvo Ideal:    {target:F4} {UnitText}");
                if (!string.IsNullOrWhiteSpace(autoTargetMsg))
                {
                    repBuilder.AppendLine($"  Seleção Inteligente: {autoTargetMsg}");
                }
                if (IsTricolorActive)
                {
                    repBuilder.AppendLine($"  Modo Diagnóstico:    Tricolor Divergente com Máscara de Tolerância");
                    repBuilder.AppendLine($"  Faixa de Tolerância: [{TolMinVal:F2} .. {TolMaxVal:F2}] {UnitText} (±{TolAbsVal:F2})");
                    repBuilder.AppendLine($"  Em Conformidade:     {PercentInTarget:F1}% da área mapeada [Máscara de Destaque]");
                    repBuilder.AppendLine($"  Aquém / Abaixo (Azul): {PercentBelowTarget:F1}% da área (som seco / absorção excessiva)");
                    repBuilder.AppendLine($"  Além / Acima (Vermelho): {PercentAboveTarget:F1}% da área (som reverberante / falta de absorção)");
                }
                repBuilder.AppendLine($"  Melhor Valor Medido: {bestVal:F4} {UnitText} (Desvio = {Math.Abs(bestVal - target):F4})");
                repBuilder.AppendLine($"  Desvio Máximo:       {maxDev:F4} {UnitText}");
                repBuilder.AppendLine($"  RMSE contra Alvo:    {rmseTarget:F4} {UnitText}");
            }

            string slopeTag = Has3DSlope ? " | 3D Slope" : "";
            if (isTricolorActive && idealTarget.HasValue)
            {
                this.Message = $"Alvo: {idealTarget.Value:G3} ({PercentInTarget:F0}% ok){slopeTag}";
            }
            else if (hasTargetDev)
            {
                this.Message = $"Desvio: {maxDev:G3} {UnitText}{slopeTag}";
            }
            else if (!string.IsNullOrWhiteSpace(UnitText))
            {
                this.Message = $"{activeMin:G3}..{activeMax:G3} {UnitText}{slopeTag}";
            }
            else
            {
                this.Message = $"{nx}x{ny} Grid{slopeTag}";
            }

            var outPaletteStops = colorStops.Select(c => new GH_Colour(c)).ToList();

            DA.SetData(0, mesh);
            DA.SetDataTree(1, outGridPts);
            DA.SetDataTree(2, outGridVals);
            DA.SetDataTree(3, outDevs);
            DA.SetDataList(4, outColors);
            DA.SetDataList(5, outPaletteStops);
            DA.SetData(6, repBuilder.ToString());
        }

        public string GetFullTitle()
        {
            string t = !string.IsNullOrWhiteSpace(TitleText) ? TitleText : "Heatmap Espacial";
            if (!string.IsNullOrWhiteSpace(UnitText) && !t.Contains($"[{UnitText}]"))
            {
                return $"{t} [{UnitText}]";
            }
            return t;
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

        private void AutoDetectTitleAndUnit(out string detectedTitle, out string detectedUnit)
        {
            detectedTitle = "Heatmap Espacial";
            detectedUnit = "";

            if (Params.Input.Count > 1 && Params.Input[1].Sources.Count > 0)
            {
                var src = Params.Input[1].Sources[0];
                string nick = src.NickName;
                string name = src.Name;

                string target = !string.IsNullOrWhiteSpace(nick) && nick != "V" ? nick : name;
                if (!string.IsNullOrWhiteSpace(target) && target != "Values" && target != "V")
                {
                    int bOpen = target.IndexOf('[');
                    int bClose = target.IndexOf(']');
                    if (bOpen >= 0 && bClose > bOpen)
                    {
                        detectedTitle = target.Substring(0, bOpen).Trim();
                        detectedUnit = target.Substring(bOpen + 1, bClose - bOpen - 1).Trim();
                        return;
                    }

                    int pOpen = target.IndexOf('(');
                    int pClose = target.IndexOf(')');
                    if (pOpen >= 0 && pClose > pOpen)
                    {
                        detectedTitle = target.Substring(0, pOpen).Trim();
                        detectedUnit = target.Substring(pOpen + 1, pClose - pOpen - 1).Trim();
                        return;
                    }

                    detectedTitle = target.Trim();
                    string upper = detectedTitle.ToUpperInvariant();
                    if (upper.Contains("T60") || upper.Contains("T30") || upper.Contains("T20") || upper.Contains("EDT"))
                        detectedUnit = "s";
                    else if (upper.Contains("C80") || upper.Contains("C50") || upper.Contains("SPL") || upper.Contains("SNR") || upper.Contains("G"))
                        detectedUnit = "dB";
                    else if (upper.Contains("D50") || upper.Contains("D80"))
                        detectedUnit = "%";
                    else if (upper.Contains("TS"))
                        detectedUnit = "ms";
                    else if (upper.Contains("TEMP"))
                        detectedUnit = "°C";
                    else if (upper.Contains("VEL"))
                        detectedUnit = "m/s";
                }
            }
        }

        public static Interval? ParseInterval(object raw)
        {
            if (raw == null) return null;

            if (raw is IGH_Goo goo)
            {
                if (goo.CastTo(out Interval iv) && iv.IsValid)
                {
                    return new Interval(Math.Min(iv.Min, iv.Max), Math.Max(iv.Min, iv.Max));
                }
                raw = goo.SafeScriptVariable();
            }

            if (raw is Interval directIv && directIv.IsValid)
            {
                return new Interval(Math.Min(directIv.Min, directIv.Max), Math.Max(directIv.Min, directIv.Max));
            }

            if (raw is GH_Interval ghInt && ghInt.Value.IsValid)
            {
                return new Interval(Math.Min(ghInt.Value.Min, ghInt.Value.Max), Math.Max(ghInt.Value.Min, ghInt.Value.Max));
            }

            string s = raw.ToString();
            if (!string.IsNullOrWhiteSpace(s))
            {
                string[] seps = new[] { " To ", " to ", " TO ", " a ", " A ", "..", ";" };
                string foundSep = null;
                foreach (var sep in seps)
                {
                    if (s.Contains(sep)) { foundSep = sep; break; }
                }

                if (foundSep != null)
                {
                    var parts = s.Split(new[] { foundSep }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 &&
                        double.TryParse(parts[0].Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double p0) &&
                        double.TryParse(parts[1].Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double p1))
                    {
                        return new Interval(Math.Min(p0, p1), Math.Max(p0, p1));
                    }
                }
                else if (s.Contains(","))
                {
                    var parts = s.Split(',');
                    if (parts.Length == 2 && parts[0].Contains(".") && parts[1].Contains(".") &&
                        double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double p0) &&
                        double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double p1))
                    {
                        return new Interval(Math.Min(p0, p1), Math.Max(p0, p1));
                    }
                }
                else if (double.TryParse(s.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double singleVal))
                {
                    if (singleVal >= 0) return new Interval(0, singleVal);
                    return new Interval(singleVal, 0);
                }
            }
            return null;
        }

        public string SavePngDialog()
        {
            if (CanvasHeatmapBmp == null) return null;
            try
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Title = "Salvar Heatmap Espacial Editorial - BURAQUEIRA Tools";
                    sfd.Filter = "PNG Image (*.png)|*.png|All files (*.*)|*.*";
                    string safeTitle = GetFullTitle().Replace(":", "_").Replace("/", "_").Replace("\\", "_").Replace("[", "").Replace("]", "").Trim();
                    sfd.FileName = $"Heatmap_{safeTitle}.png";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        using (var exportBmp = RenderExportImageWhiteBackground())
                        {
                            exportBmp.Save(sfd.FileName, ImageFormat.Png);
                        }
                        return sfd.FileName;
                    }
                }
            }
            catch (Exception ex)
            {
                Rhino.RhinoApp.WriteLine($"[SpatialHeatmap] Erro ao salvar imagem: {ex.Message}");
            }
            return null;
        }

        public Bitmap RenderExportImageWhiteBackground(int width = 1400, int height = 880)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // Fundo Branco Puro
                g.Clear(Color.White);

                // Moldura externa sutil
                using (var borderPen = new Pen(Color.FromArgb(226, 232, 240), 1.5f))
                {
                    g.DrawRectangle(borderPen, 1, 1, width - 3, height - 3);
                }

                float marginL = 80f;
                float marginR = 80f;
                float headerY = 32f;
                float headerH = 54f;
                float footerH = 48f;
                float footerY = height - footerH - 24f;

                float plotTop = headerY + headerH + 24f;
                float plotBottom = footerY - 56f;
                float plotLeft = marginL;
                float plotRight = width - marginR;
                float plotWidth = plotRight - plotLeft;
                float plotHeight = plotBottom - plotTop;

                FontFamily fam = GetUIFontFamily();

                // 1. Cabeçalho Editorial
                using (var titleFont = new Font(fam, 15f, FontStyle.Bold))
                using (var subFont = new Font(fam, 9.5f, FontStyle.Regular))
                using (var titleBrush = new SolidBrush(Color.FromArgb(15, 23, 42)))
                using (var subBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
                using (var headerLinePen = new Pen(Color.FromArgb(226, 232, 240), 1.5f))
                {
                    string fullTitle = GetFullTitle();
                    g.DrawString(fullTitle, titleFont, titleBrush, plotLeft, headerY);

                    string subDesc = $"{SamplePointsCount} pontos amostrados · Grade {GridResX}×{GridResY} · Interpolação IDW (P={IdwPower:F1})" +
    (Has3DSlope ? " · [3D Slope]" : "") +
    (IsTricolorActive && IdealTargetVal.HasValue ? $" · [Alvo: {IdealTargetVal.Value:F2} ± {TolAbsVal:F2}{UnitText} | {PercentInTarget:F0}% em Conformidade]" : "");
                    if (IdealTargetVal.HasValue && !IsTricolorActive)
                    {
                        subDesc += $" · Alvo Ideal: {IdealTargetVal.Value:F2} {UnitText}".Trim();
                    }
                    g.DrawString(subDesc, subFont, subBrush, plotLeft, headerY + 28f);

                    g.DrawLine(headerLinePen, plotLeft, headerY + headerH, plotRight, headerY + headerH);
                }

                // 2. Área do Mapa de Calor com Aspect Ratio Fiel
                RectangleF availablePlotRect = new RectangleF(plotLeft, plotTop, plotWidth, plotHeight);

                double dx = CachedMaxX - CachedMinX;
                double dy = CachedMaxY - CachedMinY;
                float targetAspect = (dx > 1e-6 && dy > 1e-6) ? (float)(dx / dy) : (plotWidth / plotHeight);

                float renderW = plotWidth;
                float renderH = renderW / targetAspect;
                if (renderH > plotHeight)
                {
                    renderH = plotHeight;
                    renderW = renderH * targetAspect;
                }

                float plotX = availablePlotRect.X + (plotWidth - renderW) * 0.5f;
                float plotY = availablePlotRect.Y + (plotHeight - renderH) * 0.5f;
                RectangleF heatRect = new RectangleF(plotX, plotY, renderW, renderH);

                // Fundo da área de plotagem
                using (var bgBrush = new SolidBrush(Color.FromArgb(248, 250, 252)))
                using (var borderPen = new Pen(Color.FromArgb(203, 213, 225), 1.2f))
                {
                    g.FillRectangle(bgBrush, heatRect);
                }

                // Renderizar grade de pixels interpolados usando interpolação bicúbica suave
                if (CachedGridMatrix != null && (IsTricolorActive || (ActiveColorStops != null && ActiveColorStops.Count > 0)))
                {
                    int nx = GridResX;
                    int ny = GridResY;
                    double valSpan = MaxVal - MinVal;
                    if (valSpan <= 1e-9) valSpan = 1.0;

                    using (var rawGridBmp = new Bitmap(nx, ny, PixelFormat.Format32bppArgb))
                    {
                        for (int ix = 0; ix < nx; ix++)
                        {
                            for (int iy = 0; iy < ny; iy++)
                            {
                                Color c;
                                if (IsTricolorActive && IdealTargetVal.HasValue)
                                {
                                    c = GetTricolorMaskColor(CachedGridMatrix[ix, iy], IdealTargetVal.Value, TolAbsVal, MinVal, MaxVal, TargetMaskColor);
                                }
                                else if (CachedHasTarget)
                                {
                                    double dev = CachedDevMatrix[ix, iy];
                                    double t = Math.Max(0.0, Math.Min(1.0, dev / (CachedMaxDev > 1e-9 ? CachedMaxDev : 1.0)));
                                    c = SampleGradient(ActiveColorStops, t);
                                }
                                else
                                {
                                    double v = CachedGridMatrix[ix, iy];
                                    double t = Math.Max(0.0, Math.Min(1.0, (v - MinVal) / valSpan));
                                    c = SampleGradient(ActiveColorStops, t);
                                }

                                rawGridBmp.SetPixel(ix, ny - 1 - iy, c);
                            }
                        }

                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(rawGridBmp, heatRect);
                    }
                }

                // Borda do heatmap
                using (var borderPen = new Pen(Color.FromArgb(203, 213, 225), 1.5f))
                {
                    g.DrawRectangle(borderPen, heatRect.X, heatRect.Y, heatRect.Width, heatRect.Height);
                }

                // 3. Barra de Gradiente de Cores Editorial na parte inferior
                float rampW = Math.Min(heatRect.Width, 680f);
                float rampH = 16f;
                float rampX = availablePlotRect.X + (plotWidth - rampW) * 0.5f;
                float rampY = availablePlotRect.Bottom + 12f;
                RectangleF rampRect = new RectangleF(rampX, rampY, rampW, rampH);

                if (IsTricolorActive && IdealTargetVal.HasValue)
                {
                    // Renderização Diagnóstica: Alvo rigorosamente centralizado no meio (50%) da escala
                    float midX = rampRect.X + rampRect.Width * 0.5f;
                    double valSpan = MaxVal - MinVal;
                    if (valSpan < 1e-9) valSpan = 1.0;
                    float halfMaskW = (float)Math.Max(26.0, Math.Min(rampRect.Width * 0.22, (TolAbsVal / valSpan) * rampRect.Width));
                    float split1 = midX - halfMaskW;
                    float split2 = midX + halfMaskW;

                    // Bloco Aquém (Azul)
                    if (split1 > rampRect.X)
                    {
                        var rLeft = new RectangleF(rampRect.X, rampRect.Y, split1 - rampRect.X, rampRect.Height);
                        using (var br = new LinearGradientBrush(rLeft, Color.FromArgb(12, 32, 140), Color.FromArgb(160, 215, 255), LinearGradientMode.Horizontal))
                            g.FillRectangle(br, rLeft);
                    }

                    // Bloco Alvo (Máscara de Conformidade)
                    if (split2 > split1)
                    {
                        var rMid = new RectangleF(split1, rampRect.Y, split2 - split1, rampRect.Height);
                        using (var br = new SolidBrush(TargetMaskColor))
                            g.FillRectangle(br, rMid);

                        using (var fMask = new Font(fam, 7.5f, FontStyle.Bold))
                        using (var brText = new SolidBrush(Color.White))
                        {
                            var sfMid = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString($"★ ALVO [{PercentInTarget:F0}%]", fMask, brText, rMid, sfMid);
                        }
                    }

                    // Bloco Além (Vermelho)
                    if (rampRect.Right > split2)
                    {
                        var rRight = new RectangleF(split2, rampRect.Y, rampRect.Right - split2, rampRect.Height);
                        using (var br = new LinearGradientBrush(rRight, Color.FromArgb(255, 175, 45), Color.FromArgb(185, 15, 25), LinearGradientMode.Horizontal))
                            g.FillRectangle(br, rRight);
                    }

                    using (var border = new Pen(Color.FromArgb(100, 116, 139), 1.2f))
                    {
                        g.DrawRectangle(border, rampRect.X, rampRect.Y, rampRect.Width, rampRect.Height);
                        if (split1 > rampRect.X && split1 < rampRect.Right)
                            g.DrawLine(border, split1, rampRect.Y, split1, rampRect.Bottom);
                        if (split2 > rampRect.X && split2 < rampRect.Right)
                            g.DrawLine(border, split2, rampRect.Y, split2, rampRect.Bottom);
                    }

                    // Rótulos explicativos
                    using (var labelFont = new Font(fam, 8.5f, FontStyle.Bold))
                    using (var subFont = new Font(fam, 7.5f, FontStyle.Regular))
                    using (var brBlue = new SolidBrush(Color.FromArgb(15, 65, 145)))
                    using (var brRed = new SolidBrush(Color.FromArgb(185, 15, 25)))
                    using (var brMask = new SolidBrush(TargetMaskColor))
                    {
                        var sfNear = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
                        var sfFar = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near };
                        var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };

                        string u = string.IsNullOrWhiteSpace(UnitText) ? "" : $" {UnitText}";
                        g.DrawString($"◀ Aquém (<{TolMinVal:F2}{u}) [{PercentBelowTarget:F0}%]", labelFont, brBlue, rampRect.X, rampRect.Bottom + 5, sfNear);
                        g.DrawString($"Ideal: {IdealTargetVal.Value:F2} ± {TolAbsVal:F2}{u}", labelFont, brMask, (split1 + split2) * 0.5f, rampRect.Bottom + 5, sfCenter);
                        g.DrawString($"Além (>{TolMaxVal:F2}{u}) [{PercentAboveTarget:F0}%] ▶", labelFont, brRed, rampRect.Right, rampRect.Bottom + 5, sfFar);
                    }

                    using (var gradTitleFont = new Font(fam, 8f, FontStyle.Bold))
                    using (var gradTitleBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
                    {
                        var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                        g.DrawString("Diagnóstico:", gradTitleFont, gradTitleBrush, rampRect.X - 10, rampRect.Y + rampRect.Height * 0.5f, sfRight);
                    }
                }
                else if (ActiveColorStops != null && ActiveColorStops.Count > 1)
                {
                    int nStops = ActiveColorStops.Count;
                    float stopW = rampRect.Width / (nStops - 1);
                    for (int i = 0; i < nStops - 1; i++)
                    {
                        var r = new RectangleF(rampRect.X + i * stopW, rampRect.Y, stopW + 0.6f, rampRect.Height);
                        using (var brush = new LinearGradientBrush(r, ActiveColorStops[i], ActiveColorStops[i + 1], LinearGradientMode.Horizontal))
                        {
                            g.FillRectangle(brush, r);
                        }
                    }
                    using (var border = new Pen(Color.FromArgb(148, 163, 184), 1.2f))
                    {
                        g.DrawRectangle(border, rampRect.X, rampRect.Y, rampRect.Width, rampRect.Height);
                    }

                    // Ticks e Rótulos ao longo da barra
                    using (var tickFont = new Font(fam, 8.5f, FontStyle.Regular))
                    using (var tickBrush = new SolidBrush(Color.FromArgb(71, 85, 105)))
                    using (var tickPen = new Pen(Color.FromArgb(148, 163, 184), 1f))
                    {
                        int nTicks = 5;
                        for (int i = 0; i < nTicks; i++)
                        {
                            float tNorm = i / (float)(nTicks - 1);
                            float tx = rampRect.X + tNorm * rampRect.Width;
                            g.DrawLine(tickPen, tx, rampRect.Bottom, tx, rampRect.Bottom + 4);

                            double val = CachedHasTarget
                                ? (tNorm * CachedMaxDev)
                                : (MinVal + tNorm * (MaxVal - MinVal));

                            string label = string.IsNullOrWhiteSpace(UnitText) ? $"{val:F2}" : $"{val:F2} {UnitText}";
                            var sf = new StringFormat
                            {
                                Alignment = (i == 0) ? StringAlignment.Near : ((i == nTicks - 1) ? StringAlignment.Far : StringAlignment.Center),
                                LineAlignment = StringAlignment.Near
                            };
                            g.DrawString(label, tickFont, tickBrush, tx, rampRect.Bottom + 5, sf);
                        }

                        // Indicador de Alvo Ideal se presente
                        if (IdealTargetVal.HasValue && !CachedHasTarget && IdealTargetVal.Value >= MinVal && IdealTargetVal.Value <= MaxVal && MaxVal > MinVal)
                        {
                            float targetNorm = (float)((IdealTargetVal.Value - MinVal) / (MaxVal - MinVal));
                            float tx = rampRect.X + targetNorm * rampRect.Width;
                            using (var targetPen = new Pen(Color.FromArgb(16, 185, 129), 2f))
                            using (var targetBrush = new SolidBrush(Color.FromArgb(16, 140, 95)))
                            using (var targetFont = new Font(fam, 8f, FontStyle.Bold))
                            {
                                g.DrawLine(targetPen, tx, rampRect.Y - 4, tx, rampRect.Bottom + 4);
                                var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Far };
                                g.DrawString($"Alvo ({IdealTargetVal.Value:F2})", targetFont, targetBrush, tx, rampRect.Y - 5, sfCenter);
                            }
                        }
                    }

                    // Unidade de medida no lado esquerdo da barra de cor (ao invés do nome da escala)
                    string unitDisplay = string.IsNullOrWhiteSpace(UnitText) ? "" : (UnitText.StartsWith("[") && UnitText.EndsWith("]") ? UnitText : $"[{UnitText}]");
                    if (!string.IsNullOrWhiteSpace(unitDisplay))
                    {
                        using (var gradTitleFont = new Font(fam, 8.5f, FontStyle.Bold))
                        using (var gradTitleBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
                        {
                            var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                            g.DrawString(unitDisplay, gradTitleFont, gradTitleBrush, rampRect.X - 10, rampRect.Y + rampRect.Height * 0.5f, sfRight);
                        }
                    }
                }

                // 4. Rodapé Editorial (Footer)
                var footerRect = new RectangleF(plotLeft, footerY, plotWidth, footerH);
                using (var footerBgBrush = new SolidBrush(Color.FromArgb(248, 250, 252)))
                using (var footerTopPen = new Pen(Color.FromArgb(226, 232, 240), 1.2f))
                using (var footFont = new Font(fam, 9.5f, FontStyle.Bold))
                using (var footTextBrush = new SolidBrush(Color.FromArgb(51, 65, 85)))
                using (var waterFont = new Font(fam, 8.5f, FontStyle.Regular))
                using (var waterBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                {
                    g.FillRectangle(footerBgBrush, footerRect);
                    g.DrawLine(footerTopPen, footerRect.Left, footerRect.Top, footerRect.Right, footerRect.Top);

                    string unitLabel = string.IsNullOrWhiteSpace(UnitText) ? "" : $" {UnitText}";
                    string footText = $"Mín = {DataMinVal:F2}{unitLabel}  |  Média = {DataAvgVal:F2}{unitLabel}  |  Máx = {DataMaxVal:F2}{unitLabel}";
                    if (IdealTargetVal.HasValue)
                    {
                        footText += $"  |  Alvo = {IdealTargetVal.Value:F2}{unitLabel}";
                    }
                    g.DrawString(footText, footFont, footTextBrush, footerRect.Left + 14, footerRect.Top + 15);

                    var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    string stamp = $"{DataSourceText} · {DateTime.Now:yyyy-MM-dd HH:mm}";
                    g.DrawString(stamp, waterFont, waterBrush, footerRect.Right - 14, footerRect.Top + 24, sfRight);
                }
            }
            return bmp;
        }

        #region Component Context Menu & Serialization

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Seguir Inclinação dos Pontos (3D Slope)", (sender, e) =>
            {
                RecordUndoEvent("Toggle Follow Slope");
                FollowPointsSlope = !FollowPointsSlope;
                ExpireSolution(true);
            }, true, FollowPointsSlope);

            Menu_AppendItem(menu, "Normalizar Elevação 3D (ZBase / ZMax)", (sender, e) =>
            {
                RecordUndoEvent("Toggle Normalize Elevation");
                NormalizeElevation = !NormalizeElevation;
                ExpireSolution(true);
            }, true, NormalizeElevation);

            Menu_AppendSeparator(menu);

            var miModeGroup = Menu_AppendItem(menu, "Modo de Visualização do Heatmap");
            Menu_AppendItem(miModeGroup.DropDown, "0: Gradiente Padrão (Valores Absolutos)", (s, e) =>
            {
                RecordUndoEvent("Mode Standard");
                DisplayMode = HeatmapDisplayMode.Standard;
                UseTargetMaskMode = false;
                ExpireSolution(true);
            }, true, DisplayMode == HeatmapDisplayMode.Standard);

            Menu_AppendItem(miModeGroup.DropDown, "1: Desvio de Alvo (|v - Target|)", (s, e) =>
            {
                RecordUndoEvent("Mode Target Deviation");
                DisplayMode = HeatmapDisplayMode.TargetDeviation;
                UseTargetMaskMode = false;
                ExpireSolution(true);
            }, true, DisplayMode == HeatmapDisplayMode.TargetDeviation);

            Menu_AppendItem(miModeGroup.DropDown, "2: Diagnóstico Editorial (Tricolor + Máscara)", (s, e) =>
            {
                RecordUndoEvent("Mode Target Diagnostic");
                DisplayMode = HeatmapDisplayMode.TargetDiagnostic;
                UseTargetMaskMode = true;
                ExpireSolution(true);
            }, true, DisplayMode == HeatmapDisplayMode.TargetDiagnostic);

            var miColors = Menu_AppendItem(miModeGroup.DropDown, "Cor da Máscara de Conformidade");
            Menu_AppendItem(miColors.DropDown, "Magenta Vibrante (Padrão)", (s, e) => { TargetMaskColor = Color.FromArgb(255, 0, 160); ExpireSolution(true); }, true, TargetMaskColor.R == 255 && TargetMaskColor.B == 160);
            Menu_AppendItem(miColors.DropDown, "Ciano Fluorescente", (s, e) => { TargetMaskColor = Color.FromArgb(0, 240, 255); ExpireSolution(true); }, true, TargetMaskColor.G == 240 && TargetMaskColor.B == 255);
            Menu_AppendItem(miColors.DropDown, "Verde Esmeralda", (s, e) => { TargetMaskColor = Color.FromArgb(16, 210, 140); ExpireSolution(true); }, true, TargetMaskColor.G == 210 && TargetMaskColor.B == 140);
            Menu_AppendItem(miColors.DropDown, "Amarelo Destaque", (s, e) => { TargetMaskColor = Color.FromArgb(255, 220, 0); ExpireSolution(true); }, true, TargetMaskColor.R == 255 && TargetMaskColor.G == 220);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetBoolean("FollowPointsSlope", FollowPointsSlope);
            writer.SetBoolean("NormalizeElevation", NormalizeElevation);
            writer.SetBoolean("UseTargetMaskMode", UseTargetMaskMode);
            writer.SetInt32("DisplayMode", (int)DisplayMode);
            writer.SetDouble("TargetTolerance", TargetTolerance);
            writer.SetDrawingColor("TargetMaskColor", TargetMaskColor);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("FollowPointsSlope"))
                FollowPointsSlope = reader.GetBoolean("FollowPointsSlope");
            if (reader.ItemExists("NormalizeElevation"))
                NormalizeElevation = reader.GetBoolean("NormalizeElevation");
            if (reader.ItemExists("UseTargetMaskMode"))
                UseTargetMaskMode = reader.GetBoolean("UseTargetMaskMode");
            if (reader.ItemExists("DisplayMode"))
                DisplayMode = (HeatmapDisplayMode)reader.GetInt32("DisplayMode");
            if (reader.ItemExists("TargetTolerance"))
                TargetTolerance = reader.GetDouble("TargetTolerance");
            if (reader.ItemExists("TargetMaskColor"))
                TargetMaskColor = reader.GetDrawingColor("TargetMaskColor");
            return base.Read(reader);
        }

        public static bool TryParseZElevation(object raw, double defaultScale, bool defaultNormalize,
            out bool isNormalized, out double zBase, out double zSpan, out double scaleFactor)
        {
            isNormalized = defaultNormalize;
            zBase = 0.0;
            zSpan = 0.0;
            scaleFactor = defaultScale;

            if (raw == null) return false;

            // 1. Tentar CastTo<Interval> via Grasshopper Goo (cobre GH_Interval, GH_ObjectWrapper, etc.)
            if (raw is IGH_Goo goo)
            {
                if (goo.CastTo(out Interval iv) && iv.IsValid)
                {
                    isNormalized = true;
                    zBase = iv.T0;
                    zSpan = iv.T1 - iv.T0;
                    scaleFactor = Math.Abs(zSpan) > 1e-9 ? Math.Abs(zSpan) : iv.T1;
                    return true;
                }
                raw = goo.SafeScriptVariable() ?? goo;
            }

            // 2. Interval nativo do Rhino
            if (raw is Interval directIv && directIv.IsValid)
            {
                isNormalized = true;
                zBase = directIv.T0;
                zSpan = directIv.T1 - directIv.T0;
                scaleFactor = Math.Abs(zSpan) > 1e-9 ? Math.Abs(zSpan) : directIv.T1;
                return true;
            }

            // 3. GH_Interval caso não desempacotado
            if (raw is GH_Interval ghInt && ghInt.Value.IsValid)
            {
                isNormalized = true;
                zBase = ghInt.Value.T0;
                zSpan = ghInt.Value.T1 - ghInt.Value.T0;
                scaleFactor = Math.Abs(zSpan) > 1e-9 ? Math.Abs(zSpan) : ghInt.Value.T1;
                return true;
            }

            // 4. Coleção de 2 números (ex: lista [0.2, 2.5] vinda de 2 sliders conectados)
            if (raw is System.Collections.IEnumerable enumerable && !(raw is string))
            {
                var numList = new List<double>();
                foreach (var item in enumerable)
                {
                    if (item == null) continue;
                    object u = (item is IGH_Goo g) ? (g.SafeScriptVariable() ?? g) : item;
                    if (double.TryParse(u.ToString().Trim().Replace(',', '.'),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double dItem))
                    {
                        numList.Add(dItem);
                    }
                }
                if (numList.Count >= 2)
                {
                    isNormalized = true;
                    zBase = numList[0];
                    zSpan = numList[1] - numList[0];
                    scaleFactor = Math.Abs(zSpan) > 1e-9 ? Math.Abs(zSpan) : numList[1];
                    return true;
                }
                else if (numList.Count == 1)
                {
                    raw = numList[0];
                }
            }

            // 5. Numéricos escalares diretos
            if (raw is double d)
            {
                scaleFactor = d;
                zSpan = d;
                if (defaultNormalize) { isNormalized = true; zBase = 0.0; }
                return true;
            }

            if (raw is int iVal)
            {
                scaleFactor = iVal;
                zSpan = iVal;
                if (defaultNormalize) { isNormalized = true; zBase = 0.0; }
                return true;
            }

            if (raw is float f)
            {
                scaleFactor = f;
                zSpan = f;
                if (defaultNormalize) { isNormalized = true; zBase = 0.0; }
                return true;
            }

            // 6. Parsing textual flexível
            string s = raw.ToString().Trim();
            if (string.IsNullOrEmpty(s)) return false;

            if (s.IndexOf("norm", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                isNormalized = true;
                s = System.Text.RegularExpressions.Regex.Replace(s, "(?i)norm(alize|alizado|alizar|al)?", "").Trim();
            }

            if (s.EndsWith("x", StringComparison.OrdinalIgnoreCase) || s.EndsWith("X", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(0, s.Length - 1).Trim();
            }

            // Separadores comuns de domínio: "To", "to", "TO", "a", "A", "..", ";", " "
            string[] seps = new[] { " To ", " to ", " TO ", " a ", " A ", "..", ";" };
            string foundSep = null;
            foreach (var sep in seps)
            {
                if (s.Contains(sep))
                {
                    foundSep = sep;
                    break;
                }
            }

            string[] parts = null;
            if (foundSep != null)
            {
                parts = s.Split(new[] { foundSep }, StringSplitOptions.RemoveEmptyEntries);
            }
            else if (!s.Contains(",") && s.Contains(" "))
            {
                parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }
            else if (s.Contains(","))
            {
                var commaParts = s.Split(',');
                if (commaParts.Length == 2 && commaParts[0].Contains(".") && commaParts[1].Contains("."))
                {
                    parts = commaParts;
                }
            }

            if (parts != null && parts.Length >= 2)
            {
                string s0 = parts[0].Trim().Replace(',', '.');
                string s1 = parts[1].Trim().Replace(',', '.');
                if (double.TryParse(s0, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double p0) &&
                    double.TryParse(s1, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double p1))
                {
                    isNormalized = true;
                    zBase = p0;
                    zSpan = p1 - p0;
                    scaleFactor = Math.Abs(zSpan) > 1e-9 ? Math.Abs(zSpan) : p1;
                    return true;
                }
            }
            else
            {
                string sSingle = s.Trim().Replace(',', '.');
                if (double.TryParse(sSingle, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    scaleFactor = val;
                    zSpan = val;
                    if (isNormalized) zBase = 0.0;
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Display Mode Parsing Helpers

        public static HeatmapDisplayMode ParseDisplayMode(object raw)
        {
            if (TryParseDisplayMode(raw, out HeatmapDisplayMode m)) return m;
            return HeatmapDisplayMode.Standard;
        }

        public static bool TryParseDisplayMode(object raw, out HeatmapDisplayMode mode)
        {
            mode = HeatmapDisplayMode.Standard;
            if (raw == null) return false;

            object obj = raw;
            if (obj is IGH_Goo goo) obj = goo.SafeScriptVariable();
            if (obj is GH_ObjectWrapper wrap) obj = wrap.Value;

            if (GH_Convert.ToInt32(obj, out int val, GH_Conversion.Both))
            {
                if (val == 1) { mode = HeatmapDisplayMode.TargetDeviation; return true; }
                if (val == 2) { mode = HeatmapDisplayMode.TargetDiagnostic; return true; }
                if (val == 0) { mode = HeatmapDisplayMode.Standard; return true; }
            }

            if (GH_Convert.ToString(obj, out string str, GH_Conversion.Both) && !string.IsNullOrWhiteSpace(str))
            {
                string lower = str.Trim().ToLowerInvariant();
                if (lower.Contains("2") || lower.Contains("diag") || lower.Contains("tri") || lower.Contains("mask") || lower.Contains("alvo"))
                {
                    mode = HeatmapDisplayMode.TargetDiagnostic;
                    return true;
                }
                if (lower.Contains("1") || lower.Contains("desv") || lower.Contains("dev") || lower.Contains("modul"))
                {
                    mode = HeatmapDisplayMode.TargetDeviation;
                    return true;
                }
                if (lower.Contains("0") || lower.Contains("pad") || lower.Contains("stan") || lower.Contains("abs"))
                {
                    mode = HeatmapDisplayMode.Standard;
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Tricolor Target Mask & Diagnosis Helpers (Professor's Suggestion)

        public static Color GetTricolorMaskColor(double v, double target, double tol, double minBound, double maxBound, Color maskColor)
        {
            double tMin = target - tol;
            double tMax = target + tol;

            // 1. ZONA DENTRO DO ALVO: Máscara com cor contrastante (Magenta, Ciano, etc.)
            if (v >= tMin && v <= tMax)
            {
                return maskColor;
            }

            // 2. ZONA ABAIXO DO ALVO (Aquém / Som Seco / Absorção Excessiva): Escala Fria Azul
            if (v < tMin)
            {
                double range = Math.Max(1e-6, tMin - minBound);
                double t = Math.Max(0.0, Math.Min(1.0, (v - minBound) / range));
                // t = 0 (muito abaixo) -> Azul Marinho Profundo (12, 30, 140)
                // t = 1 (quase no alvo) -> Azul Celeste / Gelo (160, 215, 255)
                return InterpolateColor(Color.FromArgb(12, 32, 140), Color.FromArgb(160, 215, 255), t);
            }
            // 3. ZONA ACIMA DO ALVO (Além / Som Reverberante / Falta de Absorção): Escala Quente Vermelha
            else
            {
                double range = Math.Max(1e-6, maxBound - tMax);
                double t = Math.Max(0.0, Math.Min(1.0, (v - tMax) / range));
                // t = 0 (saindo do alvo) -> Âmbar / Laranja Suave (255, 175, 45)
                // t = 1 (muito acima) -> Carmesim / Vermelho Escuro (185, 15, 25)
                return InterpolateColor(Color.FromArgb(255, 175, 45), Color.FromArgb(185, 15, 25), t);
            }
        }

        private static Color InterpolateColor(Color c1, Color c2, double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            int r = (int)(c1.R + (c2.R - c1.R) * t);
            int g = (int)(c1.G + (c2.G - c1.G) * t);
            int b = (int)(c1.B + (c2.B - c1.B) * t);
            return Color.FromArgb(r, g, b);
        }

        private static void ParseToleranceAndMask(object raw, ref double tol, ref bool isPercent, ref Color maskCol)
        {
            if (raw == null) return;
            object obj = raw;
            if (obj is IGH_Goo goo) obj = goo.SafeScriptVariable();
            if (obj is GH_ObjectWrapper wrap) obj = wrap.Value;

            if (obj is Color directColor)
            {
                maskCol = directColor;
                return;
            }

            if (GH_Convert.ToColor(obj, out Color parsedColor, GH_Conversion.Both))
            {
                maskCol = parsedColor;
                return;
            }

            if (GH_Convert.ToDouble(obj, out double num, GH_Conversion.Both))
            {
                if (num > 0)
                {
                    if (num < 1.0)
                    {
                        // Assume tolerância absoluta em segundos (ex: 0.15s) ou percentual
                        tol = num;
                        isPercent = false;
                    }
                    else if (num <= 50.0)
                    {
                        // Assume porcentagem (ex: 10 para 10%)
                        tol = num / 100.0;
                        isPercent = true;
                    }
                }
                return;
            }

            string s = obj.ToString().Trim();
            if (string.IsNullOrWhiteSpace(s)) return;

            // Detectar cor mencionada em texto
            if (s.IndexOf("magenta", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("rosa", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                maskCol = Color.FromArgb(255, 0, 160);
            }
            else if (s.IndexOf("ciano", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("cyan", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                maskCol = Color.FromArgb(0, 240, 255);
            }
            else if (s.IndexOf("verde", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("green", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                maskCol = Color.FromArgb(16, 210, 140);
            }
            else if (s.IndexOf("amarelo", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("yellow", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                maskCol = Color.FromArgb(255, 220, 0);
            }

            // Detectar porcentagem no texto
            int pctIdx = s.IndexOf('%');
            if (pctIdx > 0)
            {
                string numPart = "";
                for (int i = pctIdx - 1; i >= 0 && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == ','); i--)
                {
                    numPart = s[i] + numPart;
                }
                if (double.TryParse(numPart.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedPct))
                {
                    tol = parsedPct / 100.0;
                    isPercent = true;
                    return;
                }
            }

            // Detectar número absoluto no texto
            string[] tokens = s.Split(new char[] { ' ', '±', '+', '-', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var tok in tokens)
            {
                if (double.TryParse(tok.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedNum))
                {
                    if (parsedNum > 0)
                    {
                        tol = parsedNum;
                        isPercent = false;
                        break;
                    }
                }
            }
        }

        private static void ParseTargetAndTolerance(
            List<object> rawTargets,
            string title,
            ref double? idealTarget,
            ref double tolVal,
            ref bool tolIsPct,
            ref bool isTricolorActive,
            ref string autoTargetMsg)
        {
            if (rawTargets == null || rawTargets.Count == 0) return;

            object targetObj = null;
            if (rawTargets.Count > 1)
            {
                int detectedBandIndex = DetectOctaveBandIndexFromTitle(title, rawTargets.Count);
                if (detectedBandIndex >= 0 && detectedBandIndex < rawTargets.Count)
                {
                    targetObj = rawTargets[detectedBandIndex];
                    autoTargetMsg = $"Alvo auto-selecionado para banda {GetBandName(detectedBandIndex, rawTargets.Count)}: ";
                }
                else
                {
                    targetObj = rawTargets[0];
                }
            }
            else
            {
                targetObj = rawTargets[0];
            }

            if (targetObj == null) return;
            if (targetObj is IGH_Goo goo)
            {
                if (goo.CastTo(out Interval ivGoo) && ivGoo.IsValid)
                {
                    idealTarget = (ivGoo.Min + ivGoo.Max) * 0.5;
                    tolVal = (ivGoo.Max - ivGoo.Min) * 0.5;
                    tolIsPct = false;
                    isTricolorActive = true;
                    autoTargetMsg += $"Domínio [{ivGoo.Min:F2} .. {ivGoo.Max:F2}] -> Alvo: {idealTarget.Value:F2} ± {tolVal:F2}";
                    return;
                }
                targetObj = goo.SafeScriptVariable();
            }
            if (targetObj is GH_ObjectWrapper wrap) targetObj = wrap.Value;

            if (targetObj is Interval iv && iv.IsValid)
            {
                idealTarget = (iv.Min + iv.Max) * 0.5;
                tolVal = (iv.Max - iv.Min) * 0.5;
                tolIsPct = false;
                isTricolorActive = true;
                autoTargetMsg += $"Domínio [{iv.Min:F2} .. {iv.Max:F2}] -> Alvo: {idealTarget.Value:F2} ± {tolVal:F2}";
                return;
            }

            string s = targetObj.ToString().Trim();
            if (s.IndexOf("to", StringComparison.OrdinalIgnoreCase) >= 0 || s.Contains(".."))
            {
                string[] parts = s.Split(new string[] { "to", "To", "TO", "..", " - " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 &&
                    double.TryParse(parts[0].Replace(',', '.').Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double pMin) &&
                    double.TryParse(parts[1].Replace(',', '.').Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double pMax))
                {
                    idealTarget = (pMin + pMax) * 0.5;
                    tolVal = (pMax - pMin) * 0.5;
                    tolIsPct = false;
                    isTricolorActive = true;
                    autoTargetMsg += $"Domínio [{pMin:F2} .. {pMax:F2}] -> Alvo: {idealTarget.Value:F2} ± {tolVal:F2}";
                    return;
                }
            }

            if (GH_Convert.ToDouble(targetObj, out double num, GH_Conversion.Both))
            {
                idealTarget = num;
                if (!string.IsNullOrEmpty(autoTargetMsg)) autoTargetMsg += $"{num:F2}";
                return;
            }
        }

        private static int DetectOctaveBandIndexFromTitle(string title, int listCount)
        {
            if (string.IsNullOrWhiteSpace(title)) return -1;
            string lower = title.ToLowerInvariant();

            if (listCount == 8)
            {
                if (lower.Contains("8000") || lower.Contains("8k") || lower.Contains("8.0k")) return 7;
                if (lower.Contains("4000") || lower.Contains("4k") || lower.Contains("4.0k")) return 6;
                if (lower.Contains("2000") || lower.Contains("2k") || lower.Contains("2.0k")) return 5;
                if (lower.Contains("1000") || lower.Contains("1k") || lower.Contains("1.0k")) return 4;
                if (lower.Contains("500")) return 3;
                if (lower.Contains("250")) return 2;
                if (lower.Contains("125")) return 1;
                if (lower.Contains("63")) return 0;
            }
            else if (listCount == 6)
            {
                if (lower.Contains("4000") || lower.Contains("4k") || lower.Contains("4.0k")) return 5;
                if (lower.Contains("2000") || lower.Contains("2k") || lower.Contains("2.0k")) return 4;
                if (lower.Contains("1000") || lower.Contains("1k") || lower.Contains("1.0k")) return 3;
                if (lower.Contains("500")) return 2;
                if (lower.Contains("250")) return 1;
                if (lower.Contains("125")) return 0;
            }
            else
            {
                if (lower.Contains("4000") || lower.Contains("4k")) return Math.Min(listCount - 1, 6);
                if (lower.Contains("2000") || lower.Contains("2k")) return Math.Min(listCount - 1, 5);
                if (lower.Contains("1000") || lower.Contains("1k")) return Math.Min(listCount - 1, 4);
                if (lower.Contains("500")) return Math.Min(listCount - 1, 3);
                if (lower.Contains("250")) return Math.Min(listCount - 1, 2);
                if (lower.Contains("125")) return Math.Min(listCount - 1, 1);
                if (lower.Contains("63")) return 0;
                if (lower.Contains("8000") || lower.Contains("8k")) return listCount - 1;
            }
            return -1;
        }

        private static string GetBandName(int idx, int count)
        {
            if (count == 8)
            {
                string[] names = { "63 Hz", "125 Hz", "250 Hz", "500 Hz", "1.0 kHz", "2.0 kHz", "4.0 kHz", "8.0 kHz" };
                if (idx >= 0 && idx < names.Length) return names[idx];
            }
            else if (count == 6)
            {
                string[] names = { "125 Hz", "250 Hz", "500 Hz", "1.0 kHz", "2.0 kHz", "4.0 kHz" };
                if (idx >= 0 && idx < names.Length) return names[idx];
            }
            return $"[{idx}]";
        }

        #endregion

        #region 3D Point Slope Modeling

        public class PointSlopeModel
        {
            public bool HasSlope { get; set; }
            public Plane BestFitPlane { get; set; }
            public Vector3d Normal { get; set; }
            public double BaseZ { get; set; }
            public bool IsPlanar { get; set; }
            public double[] Residuals { get; set; }
            public List<Point3d> SamplePts { get; set; }

            public static PointSlopeModel Build(List<Point3d> pts)
            {
                var model = new PointSlopeModel
                {
                    SamplePts = pts,
                    BaseZ = pts.Average(p => p.Z)
                };

                if (pts == null || pts.Count < 3)
                {
                    model.HasSlope = false;
                    return model;
                }

                double minZ = pts.Min(p => p.Z);
                double maxZ = pts.Max(p => p.Z);
                if (maxZ - minZ < 1e-4)
                {
                    model.HasSlope = false;
                    return model;
                }

                Plane fitPlane;
                PlaneFitResult res = Plane.FitPlaneToPoints(pts, out fitPlane);
                if (res == PlaneFitResult.Failure || !fitPlane.IsValid)
                {
                    model.HasSlope = false;
                    return model;
                }

                Vector3d normal = fitPlane.ZAxis;
                if (normal.Z < 0)
                {
                    normal = -normal;
                    fitPlane = new Plane(fitPlane.Origin, fitPlane.XAxis, -fitPlane.YAxis);
                }

                if (Math.Abs(normal.Z) < 1e-4)
                {
                    model.HasSlope = false;
                    return model;
                }

                model.BestFitPlane = fitPlane;
                model.Normal = normal;
                model.HasSlope = true;

                double[] residuals = new double[pts.Count];
                double maxResidual = 0.0;
                Point3d origin = fitPlane.Origin;

                for (int i = 0; i < pts.Count; i++)
                {
                    double zPln = origin.Z - (normal.X * (pts[i].X - origin.X) + normal.Y * (pts[i].Y - origin.Y)) / normal.Z;
                    double r = pts[i].Z - zPln;
                    residuals[i] = r;
                    if (Math.Abs(r) > maxResidual) maxResidual = Math.Abs(r);
                }

                model.Residuals = residuals;
                model.IsPlanar = (maxResidual < 1e-3);

                return model;
            }

            public double EvaluateZ(double x, double y, double idwPower = 2.0)
            {
                if (!HasSlope) return BaseZ;

                Point3d origin = BestFitPlane.Origin;
                double zPlane = origin.Z - (Normal.X * (x - origin.X) + Normal.Y * (y - origin.Y)) / Normal.Z;

                if (IsPlanar || Residuals == null || Residuals.Length == 0)
                {
                    return zPlane;
                }

                double sumW = 0.0;
                double sumWR = 0.0;

                for (int i = 0; i < SamplePts.Count; i++)
                {
                    double dx = x - SamplePts[i].X;
                    double dy = y - SamplePts[i].Y;
                    double dSq = dx * dx + dy * dy;
                    if (dSq < 1e-8) return zPlane + Residuals[i];

                    double dist = Math.Sqrt(dSq);
                    double w = 1.0 / Math.Pow(dist, idwPower);
                    sumW += w;
                    sumWR += w * Residuals[i];
                }

                double deltaZ = sumW > 0 ? (sumWR / sumW) : 0.0;
                return zPlane + deltaZ;
            }
        }

        #endregion

        private Bitmap RenderCanvasHeatmap(double[,] grid, double[,] devs, int nx, int ny, double minVal, double valSpan, double maxDev, List<Color> stops, bool hasTarget)
        {
            int w = 500;
            int h = 350;
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (var bg = new SolidBrush(Color.FromArgb(20, 23, 29)))
                {
                    g.FillRectangle(bg, 0, 0, w, h);
                }

                // Renderizar grade interpolada contínua suave via bicúbico
                using (var rawGridBmp = new Bitmap(nx, ny, PixelFormat.Format32bppArgb))
                {
                    for (int ix = 0; ix < nx; ix++)
                    {
                        for (int iy = 0; iy < ny; iy++)
                        {
                            Color c;
                            if (IsTricolorActive && IdealTargetVal.HasValue)
                            {
                                c = GetTricolorMaskColor(grid[ix, iy], IdealTargetVal.Value, TolAbsVal, minVal, minVal + valSpan, TargetMaskColor);
                            }
                            else
                            {
                                double t = hasTarget ? Math.Max(0, Math.Min(1, devs[ix, iy] / (maxDev > 1e-9 ? maxDev : 1.0))) : Math.Max(0, Math.Min(1, (grid[ix, iy] - minVal) / valSpan));
                                c = SampleGradient(stops, t);
                            }
                            rawGridBmp.SetPixel(ix, ny - 1 - iy, c);
                        }
                    }

                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(rawGridBmp, 0, 0, w, h);
                }
            }
            return bmp;
        }

        #region Rhino Viewport Custom Display (IGH_PreviewObject)

        public override BoundingBox ClippingBox => _cachedMesh != null ? _cachedBbox : base.ClippingBox;

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (!_previewInViewport || _cachedMesh == null || !this.Locked && this.Hidden) return;
            args.Display.DrawMeshFalseColors(_cachedMesh);
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (!_previewInViewport || _cachedMesh == null || !this.Locked && this.Hidden) return;

            args.Display.DrawMeshWires(_cachedMesh, Color.FromArgb(40, 30, 40, 50), 1);

            if (_cachedSamplePts != null && _cachedSamplePts.Count > 0)
            {
                foreach (var pt in _cachedSamplePts)
                {
                    args.Display.DrawPoint(pt, PointStyle.RoundSimple, 4f, Color.FromArgb(240, 255, 255, 255));
                    args.Display.DrawPoint(pt, PointStyle.RoundControlPoint, 6f, Color.FromArgb(230, 41, 128, 185));
                }
            }
        }

        #endregion

        #region IDW & Gradient Sampling

        private static double PercentileSorted(IList<double> sorted, double probability)
        {
            if (sorted == null || sorted.Count == 0) return double.NaN;
            if (sorted.Count == 1) return sorted[0];

            double position = Math.Max(0.0, Math.Min(1.0, probability)) * (sorted.Count - 1);
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            if (lower == upper) return sorted[lower];
            double fraction = position - lower;
            return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
        }

        private static double InterpolateIDW(Point3d node, List<Point3d> pts, List<double> vals, double power)
        {
            double sumWeights = 0.0;
            double sumWeightedVals = 0.0;

            for (int i = 0; i < pts.Count; i++)
            {
                double dist = node.DistanceTo(pts[i]);
                if (dist < 1e-6) return vals[i];

                double w = 1.0 / Math.Pow(dist, power);
                sumWeights += w;
                sumWeightedVals += w * vals[i];
            }

            return sumWeights > 0 ? (sumWeightedVals / sumWeights) : 0.0;
        }

        private static Color SampleGradient(List<Color> stops, double t)
        {
            if (stops == null || stops.Count == 0) return Color.DodgerBlue;
            if (stops.Count == 1) return stops[0];

            t = Math.Max(0.0, Math.Min(1.0, t));
            double segment = t * (stops.Count - 1);
            int idx = (int)segment;
            if (idx >= stops.Count - 1) return stops[stops.Count - 1];

            double localT = segment - idx;
            Color c0 = stops[idx];
            Color c1 = stops[idx + 1];

            int a = (int)(c0.A + (c1.A - c0.A) * localT);
            int r = (int)(c0.R + (c1.R - c0.R) * localT);
            int g = (int)(c0.G + (c1.G - c0.G) * localT);
            int b = (int)(c0.B + (c1.B - c0.B) * localT);

            return Color.FromArgb(a, r, g, b);
        }

        private static List<Color> ResolveGradientStops(List<object> rawInputs, bool hasIdealTarget, out string gradientName)
        {
            if (rawInputs != null && rawInputs.Count > 0)
            {
                var directColors = new List<Color>();
                foreach (var obj in rawInputs)
                {
                    if (obj == null) continue;
                    if (GH_Convert.ToColor(obj, out Color col, GH_Conversion.Both))
                    {
                        directColors.Add(col);
                    }
                }

                if (directColors.Count >= 2)
                {
                    gradientName = $"Custom ({directColors.Count} stops)";
                    return directColors;
                }
                else if (directColors.Count == 1)
                {
                    gradientName = "Custom Mono";
                    return new List<Color> { Color.FromArgb(30, 30, 30), directColors[0], Color.White };
                }

                var first = rawInputs[0];
                if (first != null)
                {
                    if (GH_Convert.ToInt32(first, out int intVal, GH_Conversion.Both))
                    {
                        return GetPresetByIndex(intVal, out gradientName);
                    }

                    if (GH_Convert.ToString(first, out string strVal, GH_Conversion.Both) && !string.IsNullOrWhiteSpace(strVal))
                    {
                        return GetPresetByName(strVal.Trim(), out gradientName);
                    }
                }
            }

            if (hasIdealTarget)
            {
                gradientName = "Target Proximity";
                return PresetTargetProximity;
            }
            else
            {
                gradientName = "Turbo";
                return PresetTurbo;
            }
        }

        private static List<Color> GetPresetByIndex(int index, out string name)
        {
            switch (index)
            {
                case 0: name = "Turbo"; return PresetTurbo;
                case 1: name = "Viridis"; return PresetViridis;
                case 2: name = "Thermal / Jet"; return PresetThermal;
                case 3: name = "Plasma"; return PresetPlasma;
                case 4: name = "Magma"; return PresetMagma;
                case 5: name = "Inferno"; return PresetInferno;
                case 6: name = "Target Proximity"; return PresetTargetProximity;
                case 7: name = "Target Diverging"; return PresetTargetDiverging;
                case 8: name = "Cool-Warm"; return PresetCoolWarm;
                case 9: name = "Cividis"; return PresetCividis;
                case 10: name = "Spectral"; return PresetSpectral;
                case 11: name = "Sunset"; return PresetSunset;
                case 12: name = "Ocean / Blues"; return PresetOcean;
                case 13: name = "Forest / Greens"; return PresetForest;
                case 14: name = "Greyscale"; return PresetGreyscale;
                default: name = "Turbo"; return PresetTurbo;
            }
        }

        private static List<Color> GetPresetByName(string name, out string matchedName)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains("turbo")) { matchedName = "Turbo"; return PresetTurbo; }
            if (lower.Contains("virid")) { matchedName = "Viridis"; return PresetViridis; }
            if (lower.Contains("therm") || lower.Contains("jet") || lower.Contains("rain")) { matchedName = "Thermal / Jet"; return PresetThermal; }
            if (lower.Contains("plasm")) { matchedName = "Plasma"; return PresetPlasma; }
            if (lower.Contains("magm")) { matchedName = "Magma"; return PresetMagma; }
            if (lower.Contains("infern")) { matchedName = "Inferno"; return PresetInferno; }
            if (lower.Contains("target") || lower.Contains("ideal") || lower.Contains("proxim")) { matchedName = "Target Proximity"; return PresetTargetProximity; }
            if (lower.Contains("diverg")) { matchedName = "Target Diverging"; return PresetTargetDiverging; }
            if (lower.Contains("cool") || lower.Contains("warm") || lower.Contains("bwr")) { matchedName = "Cool-Warm"; return PresetCoolWarm; }
            if (lower.Contains("civid")) { matchedName = "Cividis"; return PresetCividis; }
            if (lower.Contains("spectr")) { matchedName = "Spectral"; return PresetSpectral; }
            if (lower.Contains("sunset") || lower.Contains("ylorrd")) { matchedName = "Sunset"; return PresetSunset; }
            if (lower.Contains("ocean") || lower.Contains("blue")) { matchedName = "Ocean / Blues"; return PresetOcean; }
            if (lower.Contains("forest") || lower.Contains("green")) { matchedName = "Forest / Greens"; return PresetForest; }
            if (lower.Contains("grey") || lower.Contains("gray") || lower.Contains("cinza")) { matchedName = "Greyscale"; return PresetGreyscale; }

            matchedName = "Turbo";
            return PresetTurbo;
        }

        #endregion

        #region Palettes

        private static readonly List<Color> PresetTurbo = new List<Color>
        {
            Color.FromArgb(48, 18, 59),
            Color.FromArgb(70, 134, 251),
            Color.FromArgb(27, 229, 181),
            Color.FromArgb(164, 252, 60),
            Color.FromArgb(251, 185, 56),
            Color.FromArgb(227, 68, 10),
            Color.FromArgb(122, 4, 3)
        };

        private static readonly List<Color> PresetViridis = new List<Color>
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

        private static readonly List<Color> PresetThermal = new List<Color>
        {
            Color.FromArgb(0, 0, 143),
            Color.FromArgb(0, 0, 255),
            Color.FromArgb(0, 255, 255),
            Color.FromArgb(128, 255, 128),
            Color.FromArgb(255, 255, 0),
            Color.FromArgb(255, 128, 0),
            Color.FromArgb(255, 0, 0),
            Color.FromArgb(128, 0, 0)
        };

        private static readonly List<Color> PresetPlasma = new List<Color>
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

        private static readonly List<Color> PresetMagma = new List<Color>
        {
            Color.FromArgb(0, 0, 4),
            Color.FromArgb(40, 17, 81),
            Color.FromArgb(100, 26, 128),
            Color.FromArgb(160, 48, 137),
            Color.FromArgb(215, 87, 126),
            Color.FromArgb(251, 150, 114),
            Color.FromArgb(252, 219, 161),
            Color.FromArgb(251, 252, 253)
        };

        private static readonly List<Color> PresetInferno = new List<Color>
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

        private static readonly List<Color> PresetTargetProximity = new List<Color>
        {
            Color.FromArgb(46, 204, 113),
            Color.FromArgb(118, 215, 196),
            Color.FromArgb(241, 196, 15),
            Color.FromArgb(230, 126, 34),
            Color.FromArgb(231, 76, 60)
        };

        private static readonly List<Color> PresetTargetDiverging = new List<Color>
        {
            Color.FromArgb(41, 128, 185),
            Color.FromArgb(133, 193, 233),
            Color.FromArgb(46, 204, 113),
            Color.FromArgb(245, 176, 65),
            Color.FromArgb(231, 76, 60)
        };

        private static readonly List<Color> PresetCoolWarm = new List<Color>
        {
            Color.FromArgb(59, 76, 192),
            Color.FromArgb(122, 157, 248),
            Color.FromArgb(221, 221, 221),
            Color.FromArgb(244, 154, 123),
            Color.FromArgb(180, 4, 38)
        };

        private static readonly List<Color> PresetCividis = new List<Color>
        {
            Color.FromArgb(0, 32, 77),
            Color.FromArgb(65, 83, 109),
            Color.FromArgb(124, 137, 138),
            Color.FromArgb(188, 193, 133),
            Color.FromArgb(255, 254, 153)
        };

        private static readonly List<Color> PresetSpectral = new List<Color>
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

        private static readonly List<Color> PresetSunset = new List<Color>
        {
            Color.FromArgb(255, 255, 204),
            Color.FromArgb(255, 237, 160),
            Color.FromArgb(254, 217, 118),
            Color.FromArgb(254, 178, 76),
            Color.FromArgb(253, 141, 60),
            Color.FromArgb(252, 78, 42),
            Color.FromArgb(227, 26, 28),
            Color.FromArgb(189, 0, 38),
            Color.FromArgb(128, 0, 38)
        };

        private static readonly List<Color> PresetOcean = new List<Color>
        {
            Color.FromArgb(8, 29, 88),
            Color.FromArgb(37, 52, 148),
            Color.FromArgb(34, 94, 168),
            Color.FromArgb(29, 145, 192),
            Color.FromArgb(65, 182, 196),
            Color.FromArgb(127, 205, 187),
            Color.FromArgb(199, 233, 180),
            Color.FromArgb(237, 248, 217)
        };

        private static readonly List<Color> PresetForest = new List<Color>
        {
            Color.FromArgb(0, 68, 27),
            Color.FromArgb(0, 109, 44),
            Color.FromArgb(35, 139, 69),
            Color.FromArgb(65, 171, 93),
            Color.FromArgb(116, 196, 118),
            Color.FromArgb(161, 217, 155),
            Color.FromArgb(199, 233, 192),
            Color.FromArgb(247, 252, 245)
        };

        private static readonly List<Color> PresetGreyscale = new List<Color>
        {
            Color.FromArgb(20, 20, 20),
            Color.FromArgb(70, 70, 70),
            Color.FromArgb(130, 130, 130),
            Color.FromArgb(190, 190, 190),
            Color.FromArgb(250, 250, 250)
        };

        #endregion
    }

    /// <summary>
    /// Atributos gráficos customizados para o Heatmap Espacial no Canvas do Grasshopper com visual editorial de alta qualidade.
    /// </summary>
    public class SpatialHeatmap_Attributes : GH_ComponentAttributes
    {
        private const int GRAPH_WIDTH = 420;
        private const int GRAPH_HEIGHT = 270;
        private RectangleF m_btnExportRect;

        public SpatialHeatmap_Attributes(SpatialHeatmap_Component owner) : base(owner)
        {
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && m_btnExportRect.Contains(e.CanvasLocation))
            {
                var comp = Owner as SpatialHeatmap_Component;
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
                var comp = Owner as SpatialHeatmap_Component;
                if (comp == null) return;

                RectangleF b = Bounds;
                RectangleF graphRect = new RectangleF(b.X + 12, b.Bottom - GRAPH_HEIGHT - 10, b.Width - 24, GRAPH_HEIGHT);
                RectangleF headerRect = new RectangleF(graphRect.X, graphRect.Y, graphRect.Width, 26);
                RectangleF footerRect = new RectangleF(graphRect.X, graphRect.Bottom - 24, graphRect.Width, 24);

                float plotTop = headerRect.Bottom + 6;
                float plotBottom = footerRect.Y - 26;
                float plotHeight = Math.Max(90, plotBottom - plotTop);

                RectangleF plotRect = new RectangleF(
                    graphRect.X + 10,
                    plotTop,
                    graphRect.Width - 20,
                    plotHeight);

                FontFamily fam = SpatialHeatmap_Component.GetUIFontFamily();

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

                // Título Editorial
                string titleToDraw = comp.GetFullTitle();
                using (var titleFont = new Font(fam, 8f, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(Color.FromArgb(240, 245, 250)))
                {
                    graphics.DrawString($"▪ {titleToDraw}", titleFont, titleBrush, headerRect.X + 8, headerRect.Y + 6);
                }

                // Botão "💾 Salvar PNG"
                float btnW = 82f;
                float btnH = 18f;
                m_btnExportRect = new RectangleF(headerRect.Right - btnW - 6, headerRect.Y + 4, btnW, btnH);

                // Badge do Gradiente
                using (var badgeFont = new Font(fam, 7f, FontStyle.Bold))
                using (var badgeBrush = new SolidBrush(Color.FromArgb(0, 220, 255)))
                {
                    var sfBadge = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    graphics.DrawString($"[{comp.ActiveGradientName}]", badgeFont, badgeBrush, m_btnExportRect.Left - 8, headerRect.Y + headerRect.Height * 0.5f, sfBadge);
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

                // 2. Área do Mapa de Calor
                using (var plotBrush = new SolidBrush(Color.FromArgb(12, 14, 18)))
                {
                    graphics.FillRectangle(plotBrush, plotRect);
                }

                if (comp.CanvasHeatmapBmp != null)
                {
                    graphics.DrawImage(comp.CanvasHeatmapBmp, plotRect);

                    using (var border = new Pen(Color.FromArgb(60, 70, 85), 1.2f))
                    {
                        graphics.DrawRectangle(border, plotRect.X, plotRect.Y, plotRect.Width, plotRect.Height);
                    }

                    // Barra de Gradiente de Cores na parte inferior
                    RectangleF rampRect = new RectangleF(plotRect.X, plotRect.Bottom + 4, plotRect.Width, 12);

                    if (comp.IsTricolorActive && comp.IdealTargetVal.HasValue)
                    {
                        // Alvo rigorosamente centralizado no meio (50%) da escala
                        float midX = rampRect.X + rampRect.Width * 0.5f;
                        double valSpan = comp.MaxVal - comp.MinVal;
                        if (valSpan < 1e-9) valSpan = 1.0;
                        float halfMaskW = (float)Math.Max(18.0, Math.Min(rampRect.Width * 0.22, (comp.TolAbsVal / valSpan) * rampRect.Width));
                        float split1 = midX - halfMaskW;
                        float split2 = midX + halfMaskW;

                        // 1. Bloco Aquém (Azul)
                        if (split1 > rampRect.X)
                        {
                            var rLeft = new RectangleF(rampRect.X, rampRect.Y, split1 - rampRect.X, rampRect.Height);
                            using (var br = new LinearGradientBrush(rLeft, Color.FromArgb(12, 32, 140), Color.FromArgb(160, 215, 255), LinearGradientMode.Horizontal))
                                graphics.FillRectangle(br, rLeft);
                        }

                        // 2. Bloco Alvo (Máscara de Conformidade)
                        if (split2 > split1)
                        {
                            var rMid = new RectangleF(split1, rampRect.Y, split2 - split1, rampRect.Height);
                            using (var br = new SolidBrush(comp.TargetMaskColor))
                                graphics.FillRectangle(br, rMid);

                            using (var fMask = new Font(fam, 6f, FontStyle.Bold))
                            using (var brText = new SolidBrush(Color.White))
                            {
                                var sfMid = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                                graphics.DrawString($"★ ALVO [{comp.PercentInTarget:F0}%]", fMask, brText, rMid, sfMid);
                            }
                        }

                        // 3. Bloco Além (Vermelho)
                        if (rampRect.Right > split2)
                        {
                            var rRight = new RectangleF(split2, rampRect.Y, rampRect.Right - split2, rampRect.Height);
                            using (var br = new LinearGradientBrush(rRight, Color.FromArgb(255, 175, 45), Color.FromArgb(185, 15, 25), LinearGradientMode.Horizontal))
                                graphics.FillRectangle(br, rRight);
                        }

                        using (var border = new Pen(Color.FromArgb(90, 100, 120), 1f))
                        {
                            graphics.DrawRectangle(border, rampRect.X, rampRect.Y, rampRect.Width, rampRect.Height);
                            if (split1 > rampRect.X && split1 < rampRect.Right)
                                graphics.DrawLine(border, split1, rampRect.Y, split1, rampRect.Bottom);
                            if (split2 > rampRect.X && split2 < rampRect.Right)
                                graphics.DrawLine(border, split2, rampRect.Y, split2, rampRect.Bottom);
                        }

                        // Rótulos de Aquém, Alvo e Além com percentuais
                        using (var tickFont = new Font(fam, 6f, FontStyle.Bold))
                        using (var brBlue = new SolidBrush(Color.FromArgb(100, 185, 255)))
                        using (var brRed = new SolidBrush(Color.FromArgb(255, 130, 130)))
                        using (var brMask = new SolidBrush(comp.TargetMaskColor))
                        {
                            string unitS = string.IsNullOrWhiteSpace(comp.UnitText) ? "" : $" {comp.UnitText}";
                            var sfNear = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                            var sfFar = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                            var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                            graphics.DrawString($"◀ Aquém (<{comp.TolMinVal:F2}{unitS}) [{comp.PercentBelowTarget:F0}%]", tickFont, brBlue, rampRect.X, rampRect.Bottom + 6, sfNear);
                            graphics.DrawString($"Ideal: {comp.IdealTargetVal.Value:F2} ± {comp.TolAbsVal:F2}{unitS}", tickFont, brMask, (split1 + split2) * 0.5f, rampRect.Bottom + 6, sfCenter);
                            graphics.DrawString($"Além (>{comp.TolMaxVal:F2}{unitS}) [{comp.PercentAboveTarget:F0}%] ▶", tickFont, brRed, rampRect.Right, rampRect.Bottom + 6, sfFar);
                        }
                    }
                    else if (comp.ActiveColorStops != null && comp.ActiveColorStops.Count > 1)
                    {
                        int nStops = comp.ActiveColorStops.Count;
                        float stopW = rampRect.Width / (nStops - 1);
                        for (int i = 0; i < nStops - 1; i++)
                        {
                            var r = new RectangleF(rampRect.X + i * stopW, rampRect.Y, stopW + 0.5f, rampRect.Height);
                            using (var brush = new LinearGradientBrush(r, comp.ActiveColorStops[i], comp.ActiveColorStops[i + 1], LinearGradientMode.Horizontal))
                            {
                                graphics.FillRectangle(brush, r);
                            }
                        }
                        using (var border = new Pen(Color.FromArgb(80, 90, 105), 1f))
                        {
                            graphics.DrawRectangle(border, rampRect.X, rampRect.Y, rampRect.Width, rampRect.Height);
                        }

                        // Rótulos de Mínimo, Centro e Máximo na barra com unidade
                        using (var tickFont = new Font(fam, 6.2f, FontStyle.Regular))
                        using (var tickBrush = new SolidBrush(Color.FromArgb(200, 215, 230)))
                        using (var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
                        using (var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                        using (var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        {
                            string unitS = string.IsNullOrWhiteSpace(comp.UnitText) ? "" : $" {comp.UnitText}";
                            bool isDevMode = comp.CachedHasTarget;

                            string minLabel = isDevMode ? "0 (Ideal)" : $"{comp.MinVal:F2}{unitS}";
                            string maxLabel = isDevMode ? $"Max Dev ({comp.CachedMaxDev:F2}{unitS})" : $"{comp.MaxVal:F2}{unitS}";
                            string midLabel = isDevMode ? "" : $"{(comp.MinVal + comp.MaxVal) * 0.5:F2}{unitS}";

                            graphics.DrawString(minLabel, tickFont, tickBrush, rampRect.X, rampRect.Bottom + 6, sfLeft);
                            if (!string.IsNullOrEmpty(midLabel))
                            {
                                graphics.DrawString(midLabel, tickFont, tickBrush, rampRect.X + rampRect.Width * 0.5f, rampRect.Bottom + 6, sfCenter);
                            }
                            graphics.DrawString(maxLabel, tickFont, tickBrush, rampRect.Right, rampRect.Bottom + 6, sfRight);
                        }
                    }
                }
                else
                {
                    using (var emptyFont = new Font(fam, 7.5f, FontStyle.Italic))
                    using (var emptyBrush = new SolidBrush(Color.FromArgb(130, 140, 155)))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        graphics.DrawString("Conecte os pontos e valores para renderizar o heatmap.", emptyFont, emptyBrush, plotRect, sf);
                    }
                }

                // Rodapé
                using (var footerFont = new Font(fam, 6.5f, FontStyle.Regular))
                using (var footerBrush = new SolidBrush(Color.FromArgb(140, 155, 175)))
                {
                    string unitS = string.IsNullOrWhiteSpace(comp.UnitText) ? "" : $"{comp.UnitText}";
                    string statInfo = $"Mín: {comp.DataMinVal:F2}{unitS} | Méd: {comp.DataAvgVal:F2}{unitS} | Máx: {comp.DataMaxVal:F2}{unitS}";
                    if (comp.IdealTargetVal.HasValue)
                    {
                        statInfo += $" | Alvo: {comp.IdealTargetVal.Value:F2}{unitS}";
                    }
                    graphics.DrawString(statInfo, footerFont, footerBrush, footerRect.X + 8, footerRect.Y + 5);

                    // Lado direito do rodapé: Fonte dos dados
                    var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    string srcTrunc = comp.DataSourceText.Length > 35 ? comp.DataSourceText.Substring(0, 32) + "…" : comp.DataSourceText;
                    graphics.DrawString(srcTrunc, footerFont, footerBrush, footerRect.Right - 8, footerRect.Y + footerRect.Height * 0.5f, sfRight);
                }
            }
        }
    }
}
