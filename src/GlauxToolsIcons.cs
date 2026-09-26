using System;

using System.Drawing;

using System.Drawing.Drawing2D;

using System.Drawing.Imaging;

using System.Drawing.Text;

namespace Buraqueira_Tools

{

    /// <summary>

    /// Gerador e cache centralizado de ícones para o plugin Buraqueira Tools.

    /// Todos os ícones são desenhados de forma vetorial em 24x24 px com GDI+ e mantidos em memória.

    /// </summary>

    public static class GlauxToolsIcons

    {

        // ==========================================

        // ÍCONE OFICIAL DO PLUGIN (BURAQUEIRA com Prancheta)

        // ==========================================

        private const string Buraqueira_Tools_ICON_B64 =
            "iVBORw0KGgoAAAANSUhEUgAAADAAAAAwCAYAAABXAvmHAAADtklEQVR4nO1ZT0hUQRif3ZLIliDdCiWW3Q5lliWRe1kTlIiSyD10CkOFDmmgaHXr6K1wUdKOq2T3XaiMiA6SF62gSNdTK0JUoB3KhFhi4xv7XvPmzXs7894sbuQPhnnzzb/vz3zffDtLyBY2Fz7dC4YryvNO/Utf17Xu6dPNdH9rrePYxJMFrcL4vTIOJRIMUMaR+a6747ROzS5b5sAYHP/t83s63wsPrjTQfGhfPruyRt4tzJLkzU6DDgzHoyHLeCd6dmXNkzVcWQA2BQ22NbWY6OnpFwZjLJB5pKOF4tGQYRG3llAWADYCzYsYQ2vwdF4QkeD9rbWuhFAyG2yQXf1BWg7vN5g5e6bBNObZ8znbIwSa//h01HZ874M5EqncpT1SmQSQQWa8m9bD7adoYWmFEFa0grSksDBGGVbr5wcmjO+poQ5S0zHmuM7iRI9lDm8NCLXarQACgDZBk1AwhOYzxChIE2lbZk5mvJvuoWIFT/dAdmadkJo8LZWx4Eab0bTo22mOGygdITQ3HAHYGBjgsTqzQiKxcjJ/q8xEP3onV3DOFLO+7BHaToqMN4u5oq7vJ/84/LIDWZOCqcHkYHqno8BCZc6SQgRSsgAb/gDIEBZoI5arLgvXcJoj2kOrD/zRDA1xqLFITBzTWQSbN+gyc5YU47+rywLitYguqz07QY903lfmR2kCXDL4Dbex5UgFA6aslAcmcZhCswKxOVHf5Cv9PsAyD4ANgeF71xppffFkNWX8xqV62j89MkDr2+1xo439OD4SDNDCJ4D8Xk6QkhQWhExx5MrfHAjbmDLbad0OaI244IeOiiWkLcAyz7brQruFzKPm7eiqAmu7yEDzLAYnU8JxMvS0BiEKCsCfR94SqppH35DBsIQv+GQXEfnA9y/zRAeSzMOAqh84WoDNy+18QFbzCD46FUKh3waefUDVF5p6hxzHq0JZALeaL0QvugC85rHtJQrpgOd7oJhRSAbSUQjBRyN8ZStGBPIchfjXZC+adxOFEtzergTA/NzOBzCncROF2rgnRru9i54LASOoWVEW6kSXwcGxaB4L3+fzcq2zj7ePun6a+n51zZja25IxU/tCcofxLcpI8eyLmP7QM+tTtgDvTDLnUxYJbi0n5nm6iSmv/5bohn/wmIV2vG4vrVOnH/uEAjSEK0gp4fXVagvzCBBCKEDNgT2uNht9+Ylcb6wyatV+Ozw8t9PCPKLkLYDIDZ8g//fT4majrO+thSb0AVLiqE83m6LQFrZAvOE3+xaPSI35JmQAAAAASUVORK5CYII=";

        private static Bitmap _pluginTabIcon;

        public static Bitmap PluginTabIcon

        {

            get

            {

                if (_pluginTabIcon == null)

                {

                    try

                    {

                        byte[] bytes = Convert.FromBase64String(Buraqueira_Tools_ICON_B64);

                        using (var ms = new System.IO.MemoryStream(bytes))

                        {

                            _pluginTabIcon = new Bitmap(ms);

                        }

                    }

                    catch

                    {

                        _pluginTabIcon = DrawCSVImport();

                    }

                }

                return _pluginTabIcon;

            }

        }

        // ==========================================

        // CACHE DE ÍCONES DOS COMPONENTES (24x24 px)

        // ==========================================

        private static Bitmap _csvImport;

        public static Bitmap CSVImport => _csvImport ?? (_csvImport = DrawCSVImport());

        private static Bitmap _csvExport;

        public static Bitmap CSVExport => _csvExport ?? (_csvExport = DrawCSVExport());

        private static Bitmap _centralTendency;

        public static Bitmap CentralTendency => _centralTendency ?? (_centralTendency = DrawCentralTendency());

        private static Bitmap _variance;

        public static Bitmap Variance => _variance ?? (_variance = DrawVariance());

        private static Bitmap _standardDeviation;

        public static Bitmap StandardDeviation => _standardDeviation ?? (_standardDeviation = DrawStandardDeviation());

        private static Bitmap _weightedMean;

        public static Bitmap WeightedMean => _weightedMean ?? (_weightedMean = DrawWeightedMean());

        private static Bitmap _targetDeviation;

        public static Bitmap TargetDeviation => _targetDeviation ?? (_targetDeviation = DrawTargetDeviation());

        private static Bitmap _dataNormalization;

        public static Bitmap DataNormalization => _dataNormalization ?? (_dataNormalization = DrawDataNormalization());

        private static Bitmap _outlierDetection;

        public static Bitmap OutlierDetection => _outlierDetection ?? (_outlierDetection = DrawOutlierDetection());

        private static Bitmap _covarianceCorrelation;

        public static Bitmap CovarianceCorrelation => _covarianceCorrelation ?? (_covarianceCorrelation = DrawCovarianceCorrelation());

        private static Bitmap _skewnessKurtosis;

        public static Bitmap SkewnessKurtosis => _skewnessKurtosis ?? (_skewnessKurtosis = DrawSkewnessKurtosis());

        private static Bitmap _frequencyTable;

        public static Bitmap FrequencyTable => _frequencyTable ?? (_frequencyTable = DrawFrequencyTable());

        private static Bitmap _trimmedWinsorizedMean;

        public static Bitmap TrimmedWinsorizedMean => _trimmedWinsorizedMean ?? (_trimmedWinsorizedMean = DrawTrimmedWinsorizedMean());

        private static Bitmap _modelEvaluation;

        public static Bitmap ModelEvaluation => _modelEvaluation ?? (_modelEvaluation = DrawModelEvaluation());

        private static Bitmap _lossFunctions;

        public static Bitmap LossFunctions => _lossFunctions ?? (_lossFunctions = DrawLossFunctions());

        private static Bitmap _chartLoss;

        public static Bitmap ChartLoss => _chartLoss ?? (_chartLoss = DrawChartLoss());

        private static Bitmap _dataTableVisualizer;

        public static Bitmap DataTableVisualizer => _dataTableVisualizer ?? (_dataTableVisualizer = DrawDataTableVisualizer());

        // Tree / DataTree Icons

        private static Bitmap _treeFilter;

        public static Bitmap TreeFilter => _treeFilter ?? (_treeFilter = DrawTreeFilter());

        private static Bitmap _deepPathReplace;

        public static Bitmap DeepPathReplace => _deepPathReplace ?? (_deepPathReplace = DrawDeepPathReplace());

        private static Bitmap _treePartitionVariable;

        public static Bitmap TreePartitionVariable => _treePartitionVariable ?? (_treePartitionVariable = DrawTreePartitionVariable());

        private static Bitmap _treePathItem;

        public static Bitmap TreePathItem => _treePathItem ?? (_treePathItem = DrawTreePathItem());

        private static Bitmap _treeSearch;

        public static Bitmap TreeSearch => _treeSearch ?? (_treeSearch = DrawTreeSearch());

        private static Bitmap _treeAlignTopology;

        public static Bitmap TreeAlignTopology => _treeAlignTopology ?? (_treeAlignTopology = DrawTreeAlignTopology());

        private static Bitmap _batchDistinct;

        public static Bitmap BatchDistinct => _batchDistinct ?? (_batchDistinct = DrawBatchDistinct());

        private static Bitmap _treeConditionalPrunerDispatch;

        public static Bitmap TreeConditionalPrunerDispatch => _treeConditionalPrunerDispatch ?? (_treeConditionalPrunerDispatch = DrawTreeConditionalPrunerDispatch());

        private static Bitmap _treeGroupBy;

        public static Bitmap TreeGroupBy => _treeGroupBy ?? (_treeGroupBy = DrawTreeGroupBy());

        private static Bitmap _dynamicTreeWeaver;

        public static Bitmap DynamicTreeWeaver => _dynamicTreeWeaver ?? (_dynamicTreeWeaver = DrawDynamicTreeWeaver());

        private static Bitmap _treeStructuralDiff;

        public static Bitmap TreeStructuralDiff => _treeStructuralDiff ?? (_treeStructuralDiff = DrawTreeStructuralDiff());

        private static Bitmap _pathMath;

        public static Bitmap PathMath => _pathMath ?? (_pathMath = DrawPathMath());

        private static Bitmap _pathDictionary;

        public static Bitmap PathDictionary => _pathDictionary ?? (_pathDictionary = DrawPathDictionary());

        private static Bitmap _fastPareto;

        public static Bitmap FastPareto => _fastPareto ?? (_fastPareto = DrawFastPareto());

        private static Bitmap _vectorSimilarity;

        public static Bitmap VectorSimilarity => _vectorSimilarity ?? (_vectorSimilarity = DrawVectorSimilarity());

        // Automation & Flow Control Icons

        private static Bitmap _conditionalTrigger;

        public static Bitmap ConditionalTrigger => _conditionalTrigger ?? (_conditionalTrigger = DrawConditionalTrigger());

        private static Bitmap _stateLatch;

        public static Bitmap StateLatch => _stateLatch ?? (_stateLatch = DrawStateLatch());

        private static Bitmap _iterativeAccumulator;

        public static Bitmap IterativeAccumulator => _iterativeAccumulator ?? (_iterativeAccumulator = DrawIterativeAccumulator());

        private static Bitmap _convergenceWatcher;

        public static Bitmap ConvergenceWatcher => _convergenceWatcher ?? (_convergenceWatcher = DrawConvergenceWatcher());

        private static Bitmap _conditionalTimer;

        public static Bitmap ConditionalTimer => _conditionalTimer ?? (_conditionalTimer = DrawConditionalTimer());

        private static Bitmap _dataGenerationTimer;

        public static Bitmap DataGenerationTimer => _dataGenerationTimer ?? (_dataGenerationTimer = DrawDataGenerationTimer());

        private static Bitmap _multiLogicGate;

        public static Bitmap MultiLogicGate => _multiLogicGate ?? (_multiLogicGate = DrawMultiLogicGate());

        private static Bitmap _thresholdVotingGate;

        public static Bitmap ThresholdVotingGate => _thresholdVotingGate ?? (_thresholdVotingGate = DrawThresholdVotingGate());

        // Information Theory Icons

        private static Bitmap _shannonEntropy;

        public static Bitmap ShannonEntropy => _shannonEntropy ?? (_shannonEntropy = DrawShannonEntropy());

        private static Bitmap _kullbackLeiblerDivergence;

        public static Bitmap KullbackLeiblerDivergence => _kullbackLeiblerDivergence ?? (_kullbackLeiblerDivergence = DrawKullbackLeiblerDivergence());

        private static Bitmap _geometricClusterFrequency;

        public static Bitmap GeometricClusterFrequency => _geometricClusterFrequency ?? (_geometricClusterFrequency = DrawGeometricClusterFrequency());

        // Visual & Charting Icons

        private static Bitmap _chartLine;

        public static Bitmap ChartLine => _chartLine ?? (_chartLine = DrawChartLine());

        private static Bitmap _chartScatter;

        public static Bitmap ChartScatter => _chartScatter ?? (_chartScatter = DrawChartScatter());

        private static Bitmap _chartBoxPlot;

        public static Bitmap ChartBoxPlot => _chartBoxPlot ?? (_chartBoxPlot = DrawChartBoxPlot());

        private static Bitmap _hierarchicalCluster;

        public static Bitmap HierarchicalCluster => _hierarchicalCluster ?? (_hierarchicalCluster = DrawHierarchicalCluster());

        private static Bitmap _spatialHeatmap;

        public static Bitmap SpatialHeatmap => _spatialHeatmap ?? (_spatialHeatmap = DrawSpatialHeatmap());

        private static Bitmap _isometricSurfaceGraph;

        public static Bitmap IsometricSurfaceGraph => _isometricSurfaceGraph ?? (_isometricSurfaceGraph = DrawIsometricSurfaceGraph());

        // ==========================================

        // MÉTODOS DE RENDERIZAÇÃO GDI+ (24x24 px)

        // ==========================================

        
        // Excel Math & Statistical Distribution Icons
        private static Bitmap _betaDistribution;
        public static Bitmap BetaDistribution => _betaDistribution ?? (_betaDistribution = DrawBetaDistribution());

        private static Bitmap _binomialDistribution;
        public static Bitmap BinomialDistribution => _binomialDistribution ?? (_binomialDistribution = DrawBinomialDistribution());

        private static Bitmap _chiSquareDistribution;
        public static Bitmap ChiSquareDistribution => _chiSquareDistribution ?? (_chiSquareDistribution = DrawChiSquareDistribution());

        private static Bitmap _massMath;
        public static Bitmap MassMath => _massMath ?? (_massMath = DrawMassMath());

        private static Bitmap _sumIf;
        public static Bitmap SumIf => _sumIf ?? (_sumIf = DrawSumIf());

        private static Bitmap _dataGroupFinder;
        public static Bitmap DataGroupFinder => _dataGroupFinder ?? (_dataGroupFinder = DrawDataGroupFinder());

        private static Bitmap _dataStack;
        public static Bitmap DataStack => _dataStack ?? (_dataStack = DrawDataStack());

        
        // ==========================================
        // SUB-CATEGORIA PILLS (BARRAMENTO SEM FIOS & HUBS)
        // ==========================================
        private static Bitmap _pillTransmitter;
        public static Bitmap PillTransmitter => _pillTransmitter ?? (_pillTransmitter = DrawPillTransmitter());

        private static Bitmap _pillReceiver;
        public static Bitmap PillReceiver => _pillReceiver ?? (_pillReceiver = DrawPillReceiver());

        private static Bitmap _pillBundlePack;
        public static Bitmap PillBundlePack => _pillBundlePack ?? (_pillBundlePack = DrawPillBundlePack());

        private static Bitmap _pillBundleUnpack;
        public static Bitmap PillBundleUnpack => _pillBundleUnpack ?? (_pillBundleUnpack = DrawPillBundleUnpack());

        private static Bitmap _pillCatalog;
        public static Bitmap PillCatalog => _pillCatalog ?? (_pillCatalog = DrawPillCatalog());

        private static Bitmap _pillCache;
        public static Bitmap PillCache => _pillCache ?? (_pillCache = DrawPillCache());

        private static Bitmap _pillPresetManager;
        public static Bitmap PillPresetManager => _pillPresetManager ?? (_pillPresetManager = DrawPillPresetManager());

        private static Bitmap _pillConstraintChecker;
        public static Bitmap PillConstraintChecker => _pillConstraintChecker ?? (_pillConstraintChecker = DrawPillConstraintChecker());

        private static Bitmap _pillChangeDetector;
        public static Bitmap PillChangeDetector => _pillChangeDetector ?? (_pillChangeDetector = DrawPillChangeDetector());

        private static Bitmap _pillRelay;
        public static Bitmap PillRelay => _pillRelay ?? (_pillRelay = DrawPillRelay());

        private static Bitmap _pillHook;
        public static Bitmap PillHook => _pillHook ?? (_pillHook = DrawPillHook());

        private static Bitmap _pillSliderPool;
        public static Bitmap PillSliderPool => _pillSliderPool ?? (_pillSliderPool = DrawPillSliderPool());

        private static Bitmap _pillDisabler;
        public static Bitmap PillDisabler => _pillDisabler ?? (_pillDisabler = DrawPillDisabler());

        private static Bitmap _pillPresetVault;
        public static Bitmap PillPresetVault => _pillPresetVault ?? (_pillPresetVault = DrawPillPresetVault());

        private static Bitmap _pillPulseTimer;
        public static Bitmap PillPulseTimer => _pillPulseTimer ?? (_pillPulseTimer = DrawPillPulseTimer());

        private static Bitmap _pillLayerPipeline;
        public static Bitmap PillLayerPipeline => _pillLayerPipeline ?? (_pillLayerPipeline = DrawPillLayerPipeline());

        private static Bitmap _pillTreePivot;
        public static Bitmap PillTreePivot => _pillTreePivot ?? (_pillTreePivot = DrawPillTreePivot());

        private static Bitmap _pillTreeTableExport;
        public static Bitmap PillTreeTableExport => _pillTreeTableExport ?? (_pillTreeTableExport = DrawPillTreeTableExport());

        private static Bitmap _pillGeometryFilter;
        public static Bitmap PillGeometryFilter => _pillGeometryFilter ?? (_pillGeometryFilter = DrawPillGeometryFilter());

        private static Bitmap _pillNumberRounder;
        public static Bitmap PillNumberRounder => _pillNumberRounder ?? (_pillNumberRounder = DrawPillNumberRounder());

        private static Bitmap _pillDomainFilter;
        public static Bitmap PillDomainFilter => _pillDomainFilter ?? (_pillDomainFilter = DrawPillDomainFilter());

        private static Bitmap _pillInterpolator;
        public static Bitmap PillInterpolator => _pillInterpolator ?? (_pillInterpolator = DrawPillInterpolator());

        private static Bitmap _matrixConstruct;
        public static Bitmap MatrixConstruct => _matrixConstruct ?? (_matrixConstruct = DrawMatrixConstruct());

        private static Bitmap _matrixMultiply;
        public static Bitmap MatrixMultiply => _matrixMultiply ?? (_matrixMultiply = DrawMatrixMultiply());

        private static Bitmap _matrixInvertDet;
        public static Bitmap MatrixInvertDet => _matrixInvertDet ?? (_matrixInvertDet = DrawMatrixInvertDet());

        private static Bitmap _matrixSolver;
        public static Bitmap MatrixSolver => _matrixSolver ?? (_matrixSolver = DrawMatrixSolver());

        private static Bitmap _matrixEigen;
        public static Bitmap MatrixEigen => _matrixEigen ?? (_matrixEigen = DrawMatrixEigen());

        private static Bitmap _pillDiskSave;
        public static Bitmap PillDiskSave => _pillDiskSave ?? (_pillDiskSave = DrawPillDiskSave());

        private static Bitmap _pillDiskLoad;
        public static Bitmap PillDiskLoad => _pillDiskLoad ?? (_pillDiskLoad = DrawPillDiskLoad());

        private static Bitmap _pillViewportCapture;
        public static Bitmap PillViewportCapture => _pillViewportCapture ?? (_pillViewportCapture = DrawPillViewportCapture());

        private static Bitmap _pillViewGenerator;
        public static Bitmap PillViewGenerator => _pillViewGenerator ?? (_pillViewGenerator = DrawPillViewGenerator());

        private static Bitmap _duplicateInspector;
        public static Bitmap DuplicateInspector => _duplicateInspector ?? (_duplicateInspector = DrawDuplicateInspector());

        private static Graphics InitGfx(Bitmap bmp)

        {

            var g = Graphics.FromImage(bmp);

            g.SmoothingMode = SmoothingMode.AntiAlias;

            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            return g;

        }

        // 1. CSV Import: Arquivo de texto/tabela com seta verde entrando

        private static Bitmap DrawCSVImport()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Documento de fundo

                using (var docBrush = new SolidBrush(Color.FromArgb(245, 247, 250)))

                using (var docBorder = new Pen(Color.FromArgb(70, 80, 95), 1.2f))

                {

                    g.FillRectangle(docBrush, 2, 2, 14, 19);

                    g.DrawRectangle(docBorder, 2, 2, 14, 19);

                }

                // Linhas de tabela

                using (var linePen = new Pen(Color.FromArgb(140, 160, 180), 1f))

                {

                    g.DrawLine(linePen, 5, 6, 13, 6);

                    g.DrawLine(linePen, 5, 10, 13, 10);

                    g.DrawLine(linePen, 5, 14, 13, 14);

                }

                // Seta de Importação (Verde vibrante apontando para dentro)

                using (var arrowBrush = new SolidBrush(Color.FromArgb(46, 204, 113)))

                using (var arrowBorder = new Pen(Color.FromArgb(20, 100, 50), 1f))

                {

                    PointF[] arrow = new PointF[]

                    {

                        new PointF(22, 12),

                        new PointF(15, 12),

                        new PointF(15, 8),

                        new PointF(9, 15),

                        new PointF(15, 22),

                        new PointF(15, 18),

                        new PointF(22, 18)

                    };

                    g.FillPolygon(arrowBrush, arrow);

                    g.DrawPolygon(arrowBorder, arrow);

                }

            }

            return bmp;

        }

        // 2. CSV Export: Arquivo de texto/tabela com seta azul saindo

        private static Bitmap DrawCSVExport()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Documento de fundo

                using (var docBrush = new SolidBrush(Color.FromArgb(245, 247, 250)))

                using (var docBorder = new Pen(Color.FromArgb(70, 80, 95), 1.2f))

                {

                    g.FillRectangle(docBrush, 2, 2, 14, 19);

                    g.DrawRectangle(docBorder, 2, 2, 14, 19);

                }

                // Linhas de tabela

                using (var linePen = new Pen(Color.FromArgb(140, 160, 180), 1f))

                {

                    g.DrawLine(linePen, 5, 6, 13, 6);

                    g.DrawLine(linePen, 5, 10, 13, 10);

                    g.DrawLine(linePen, 5, 14, 13, 14);

                }

                // Seta de Exportação (Azul apontando para fora)

                using (var arrowBrush = new SolidBrush(Color.FromArgb(52, 152, 219)))

                using (var arrowBorder = new Pen(Color.FromArgb(25, 85, 140), 1f))

                {

                    PointF[] arrow = new PointF[]

                    {

                        new PointF(10, 12),

                        new PointF(17, 12),

                        new PointF(17, 8),

                        new PointF(23, 15),

                        new PointF(17, 22),

                        new PointF(17, 18),

                        new PointF(10, 18)

                    };

                    g.FillPolygon(arrowBrush, arrow);

                    g.DrawPolygon(arrowBorder, arrow);

                }

            }

            return bmp;

        }

        // 3. Central Tendency: Curva de Gauss com indicador de Média / Mediana / Moda

        private static Bitmap DrawCentralTendency()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Curva de Gauss (Distribuição)

                GraphicsPath bell = new GraphicsPath();

                bell.AddBezier(new PointF(2, 20), new PointF(7, 19), new PointF(9, 6), new PointF(12, 5));

                bell.AddBezier(new PointF(12, 5), new PointF(15, 6), new PointF(17, 19), new PointF(22, 20));

                using (var fillBrush = new SolidBrush(Color.FromArgb(180, 215, 245)))

                {

                    GraphicsPath closedBell = (GraphicsPath)bell.Clone();

                    closedBell.AddLine(22, 20, 2, 20);

                    g.FillPath(fillBrush, closedBell);

                }

                using (var bellPen = new Pen(Color.FromArgb(41, 128, 185), 1.6f))

                {

                    g.DrawPath(bellPen, bell);

                }

                // Linha central da Média / Mediana (Vermelho/Laranja)

                using (var meanPen = new Pen(Color.FromArgb(231, 76, 60), 1.5f) { DashStyle = DashStyle.Dash })

                {

                    g.DrawLine(meanPen, 12, 4, 12, 20);

                }

                // Marcador no topo da Moda

                using (var dotBrush = new SolidBrush(Color.FromArgb(231, 76, 60)))

                {

                    g.FillEllipse(dotBrush, 10.5f, 3.5f, 3f, 3f);

                }

                // Letra "x̄" ou "M" no canto

                using (var font = new Font("Arial", 7f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(40, 50, 65)))

                {

                    g.DrawString("x̄", font, textBrush, 1f, 1f);

                }

            }

            return bmp;

        }

        // 4. Variance (s²): Dispersão de pontos em torno da média com s² em destaque

        private static Bitmap DrawVariance()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Linha de referência da média

                using (var axisPen = new Pen(Color.FromArgb(160, 170, 185), 1.2f))

                {

                    g.DrawLine(axisPen, 2, 12, 22, 12);

                }

                // Setas de dispersão vertical

                using (var varPen = new Pen(Color.FromArgb(155, 89, 182), 1.3f))

                {

                    // Barra 1

                    g.DrawLine(varPen, 6, 5, 6, 19);

                    g.FillEllipse(new SolidBrush(Color.FromArgb(155, 89, 182)), 4.5f, 3.5f, 3f, 3f);

                    g.FillEllipse(new SolidBrush(Color.FromArgb(155, 89, 182)), 4.5f, 17.5f, 3f, 3f);

                    // Barra 2

                    g.DrawLine(varPen, 18, 7, 18, 17);

                    g.FillEllipse(new SolidBrush(Color.FromArgb(155, 89, 182)), 16.5f, 5.5f, 3f, 3f);

                    g.FillEllipse(new SolidBrush(Color.FromArgb(155, 89, 182)), 16.5f, 15.5f, 3f, 3f);

                }

                // Símbolo s² central

                using (var font = new Font("Arial", 8f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(100, 40, 140)))

                {

                    g.DrawString("s²", font, textBrush, 8.5f, 6.5f);

                }

            }

            return bmp;

        }

        // 5. Standard Deviation (σ): Curva com banda ±1σ e símbolo grego sigma

        private static Bitmap DrawStandardDeviation()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Faixa de desvio padrão sombreada

                using (var bandBrush = new SolidBrush(Color.FromArgb(220, 235, 252)))

                {

                    g.FillRectangle(bandBrush, 6, 3, 12, 18);

                }

                // Delimitadores de ±1σ

                using (var boundPen = new Pen(Color.FromArgb(52, 152, 219), 1.2f) { DashStyle = DashStyle.Dot })

                {

                    g.DrawLine(boundPen, 6, 2, 6, 22);

                    g.DrawLine(boundPen, 18, 2, 18, 22);

                }

                // Eixo central

                using (var centerPen = new Pen(Color.FromArgb(180, 190, 205), 1f))

                {

                    g.DrawLine(centerPen, 12, 2, 12, 22);

                }

                // Seta de largura de desvio horizontal

                using (var arrowPen = new Pen(Color.FromArgb(41, 128, 185), 1.2f))

                {

                    g.DrawLine(arrowPen, 6, 17, 18, 17);

                    g.DrawLine(arrowPen, 6, 17, 8, 15);

                    g.DrawLine(arrowPen, 6, 17, 8, 19);

                    g.DrawLine(arrowPen, 18, 17, 16, 15);

                    g.DrawLine(arrowPen, 18, 17, 16, 19);

                }

                // Símbolo grego σ

                using (var font = new Font("Georgia", 10f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(25, 75, 130)))

                {

                    g.DrawString("σ", font, textBrush, 7.5f, 2.5f);

                }

            }

            return bmp;

        }

        // 6. Weighted Mean: Balança / Pesos desiguais equilibrados

        private static Bitmap DrawWeightedMean()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Triângulo de apoio (Fulcro da balança)

                using (var fulcrumBrush = new SolidBrush(Color.FromArgb(70, 80, 95)))

                {

                    PointF[] tri = new PointF[] { new PointF(12, 14), new PointF(9, 21), new PointF(15, 21) };

                    g.FillPolygon(fulcrumBrush, tri);

                }

                // Barra da balança

                using (var beamPen = new Pen(Color.FromArgb(40, 50, 65), 2f))

                {

                    g.DrawLine(beamPen, 3, 14, 21, 14);

                }

                // Peso 1 (Pequeno, na ponta esquerda: x1 * w1)

                using (var w1Brush = new SolidBrush(Color.FromArgb(241, 196, 15)))

                using (var w1Border = new Pen(Color.FromArgb(180, 140, 10), 1f))

                {

                    g.FillEllipse(w1Brush, 3, 8, 5, 5);

                    g.DrawEllipse(w1Border, 3, 8, 5, 5);

                }

                // Peso 2 (Grande / Pesado, na direita: x2 * w2)

                using (var w2Brush = new SolidBrush(Color.FromArgb(230, 126, 34)))

                using (var w2Border = new Pen(Color.FromArgb(160, 75, 15), 1f))

                {

                    g.FillEllipse(w2Brush, 15, 5, 8, 8);

                    g.DrawEllipse(w2Border, 15, 5, 8, 8);

                }

                // Letra "w" no topo esquerdo

                using (var font = new Font("Arial", 6.5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(100, 60, 20)))

                {

                    g.DrawString("w̄", font, textBrush, 9.5f, 1f);

                }

            }

            return bmp;

        }

        // 7. Target Deviation: Alvo com seta e módulo |Δ|

        private static Bitmap DrawTargetDeviation()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Alvo (Círculos concêntricos)

                using (var targetBrush1 = new SolidBrush(Color.FromArgb(235, 240, 245)))

                using (var targetBrush2 = new SolidBrush(Color.FromArgb(231, 76, 60)))

                using (var targetPen = new Pen(Color.FromArgb(231, 76, 60), 1.2f))

                {

                    g.FillEllipse(targetBrush1, 2, 2, 20, 20);

                    g.DrawEllipse(targetPen, 2, 2, 20, 20);

                    g.DrawEllipse(targetPen, 5.5f, 5.5f, 13f, 13f);

                    // Centro do alvo (Bullseye)

                    g.FillEllipse(targetBrush2, 9f, 9f, 6f, 6f);

                }

                // Seta de desvio / distância em relação ao centro

                using (var deltaPen = new Pen(Color.FromArgb(41, 128, 185), 1.5f))

                using (var ptBrush = new SolidBrush(Color.FromArgb(41, 128, 185)))

                {

                    g.DrawLine(deltaPen, 12, 12, 19, 5);

                    g.FillEllipse(ptBrush, 17.5f, 3.5f, 4f, 4f);

                }

                // Símbolo |Δ|

                using (var font = new Font("Arial", 6.5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(20, 50, 90)))

                {

                    g.DrawString("|Δ|", font, textBrush, 1f, 14f);

                }

            }

            return bmp;

        }

        // 8. Data Normalization: Escala Min-Max [0, 1] e curva sigmoide / Z-score

        private static Bitmap DrawDataNormalization()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Linhas limites superior e inferior (0 e 1)

                using (var boundPen = new Pen(Color.FromArgb(180, 195, 210), 1f) { DashStyle = DashStyle.Dash })

                {

                    g.DrawLine(boundPen, 2, 5, 22, 5);

                    g.DrawLine(boundPen, 2, 19, 22, 19);

                }

                // Curva de Normalização Sigmóide suave em ciano/azul

                using (var curvePen = new Pen(Color.FromArgb(26, 188, 156), 2f))

                {

                    GraphicsPath curve = new GraphicsPath();

                    curve.AddBezier(new PointF(3, 18), new PointF(8, 18), new PointF(10, 12), new PointF(12, 12));

                    curve.AddBezier(new PointF(12, 12), new PointF(14, 12), new PointF(16, 6), new PointF(21, 6));

                    g.DrawPath(curvePen, curve);

                }

                // Pontos inicial e final

                using (var ptBrush = new SolidBrush(Color.FromArgb(22, 160, 133)))

                {

                    g.FillEllipse(ptBrush, 1.5f, 16.5f, 3.5f, 3.5f);

                    g.FillEllipse(ptBrush, 19.5f, 4.5f, 3.5f, 3.5f);

                }

                // Rótulos 0 e 1

                using (var font = new Font("Arial", 5.5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(40, 60, 80)))

                {

                    g.DrawString("1", font, textBrush, 2f, 1f);

                    g.DrawString("0", font, textBrush, 16.5f, 14f);

                }

            }

            return bmp;

        }

        // 9. Outlier Filter: Boxplot com ponto aberrante vermelho destacado e corte IQR

        private static Bitmap DrawOutlierDetection()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Boxplot central (Q1 a Q3)

                using (var boxBrush = new SolidBrush(Color.FromArgb(230, 240, 250)))

                using (var boxPen = new Pen(Color.FromArgb(41, 128, 185), 1.2f))

                {

                    // Bigodes (Whiskers)

                    g.DrawLine(boxPen, 12, 8, 12, 21);

                    g.DrawLine(boxPen, 8, 21, 16, 21); // Bigode inferior

                    // Caixa IQR

                    g.FillRectangle(boxBrush, 6, 11, 12, 7);

                    g.DrawRectangle(boxPen, 6, 11, 12, 7);

                    // Linha da Mediana

                    using (var medPen = new Pen(Color.FromArgb(230, 126, 34), 1.5f))

                    {

                        g.DrawLine(medPen, 6, 14, 18, 14);

                    }

                }

                // Linha de Corte de Outlier (Fences)

                using (var fencePen = new Pen(Color.FromArgb(231, 76, 60), 1f) { DashStyle = DashStyle.Dot })

                {

                    g.DrawLine(fencePen, 2, 7, 22, 7);

                }

                // Ponto Outlier Vermelho brilhante

                using (var outBrush = new SolidBrush(Color.FromArgb(231, 76, 60)))

                using (var outBorder = new Pen(Color.FromArgb(180, 40, 30), 1f))

                {

                    g.FillEllipse(outBrush, 9.5f, 1.5f, 5f, 5f);

                    g.DrawEllipse(outBorder, 9.5f, 1.5f, 5f, 5f);

                }

            }

            return bmp;

        }

        // 10. Covariance & Correlation: Dispersão com linha de tendência e 'r'

        private static Bitmap DrawCovarianceCorrelation()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Eixos X e Y

                using (var axisPen = new Pen(Color.FromArgb(160, 175, 190), 1.2f))

                {

                    g.DrawLine(axisPen, 3, 21, 21, 21);

                    g.DrawLine(axisPen, 3, 21, 3, 3);

                }

                // Linha de regressão / correlação linear

                using (var regPen = new Pen(Color.FromArgb(52, 152, 219), 1.5f))

                {

                    g.DrawLine(regPen, 4, 18, 20, 5);

                }

                // Pontos de dispersão ao redor da linha

                using (var ptBrush = new SolidBrush(Color.FromArgb(142, 68, 173)))

                {

                    g.FillEllipse(ptBrush, 5f, 15f, 2.5f, 2.5f);

                    g.FillEllipse(ptBrush, 8f, 17f, 2.5f, 2.5f);

                    g.FillEllipse(ptBrush, 10f, 11f, 2.5f, 2.5f);

                    g.FillEllipse(ptBrush, 13f, 13f, 2.5f, 2.5f);

                    g.FillEllipse(ptBrush, 15f, 7f, 2.5f, 2.5f);

                    g.FillEllipse(ptBrush, 18f, 9f, 2.5f, 2.5f);

                }

                // Símbolo 'r'

                using (var font = new Font("Georgia", 7f, FontStyle.Bold | FontStyle.Italic))

                using (var textBrush = new SolidBrush(Color.FromArgb(41, 128, 185)))

                {

                    g.DrawString("r", font, textBrush, 15.5f, 1.5f);

                }

            }

            return bmp;

        }

        // 11. Skewness & Kurtosis: Curva assimétrica com cauda longa

        private static Bitmap DrawSkewnessKurtosis()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Linha de base

                using (var basePen = new Pen(Color.FromArgb(170, 180, 195), 1f))

                {

                    g.DrawLine(basePen, 2, 20, 22, 20);

                }

                // Curva assimétrica (Right-skewed, cauda longa para a direita)

                GraphicsPath skewCurve = new GraphicsPath();

                skewCurve.AddBezier(new PointF(2, 20), new PointF(5, 19), new PointF(6, 5), new PointF(8, 5));

                skewCurve.AddBezier(new PointF(8, 5), new PointF(11, 6), new PointF(14, 18), new PointF(22, 20));

                using (var fillBrush = new SolidBrush(Color.FromArgb(220, 240, 230)))

                {

                    GraphicsPath closed = (GraphicsPath)skewCurve.Clone();

                    closed.AddLine(22, 20, 2, 20);

                    g.FillPath(fillBrush, closed);

                }

                using (var curvePen = new Pen(Color.FromArgb(39, 174, 96), 1.5f))

                {

                    g.DrawPath(curvePen, skewCurve);

                }

                // Seta indicando a assimetria / cauda

                using (var arrowPen = new Pen(Color.FromArgb(230, 126, 34), 1.2f))

                {

                    g.DrawLine(arrowPen, 13, 13, 20, 13);

                    g.DrawLine(arrowPen, 18, 11, 20, 13);

                    g.DrawLine(arrowPen, 18, 15, 20, 13);

                }

                // Símbolo Skew 'γ'

                using (var font = new Font("Georgia", 6.5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(30, 100, 60)))

                {

                    g.DrawString("γ₁", font, textBrush, 1f, 1f);

                }

            }

            return bmp;

        }

        // 12. Frequency Table: Gráfico de barras de histograma

        private static Bitmap DrawFrequencyTable()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Linha de base

                using (var basePen = new Pen(Color.FromArgb(150, 165, 180), 1.2f))

                {

                    g.DrawLine(basePen, 2, 21, 22, 21);

                }

                // Barras do Histograma

                using (var barBrush1 = new SolidBrush(Color.FromArgb(149, 165, 166)))

                using (var barBrush2 = new SolidBrush(Color.FromArgb(52, 152, 219)))

                using (var barBrush3 = new SolidBrush(Color.FromArgb(231, 76, 60))) // Barra modal (Pico)

                using (var barBrush4 = new SolidBrush(Color.FromArgb(46, 204, 113)))

                using (var barBorder = new Pen(Color.FromArgb(60, 70, 80), 0.8f))

                {

                    // Barra 1 (Baixa)

                    g.FillRectangle(barBrush1, 3, 14, 4, 7);

                    g.DrawRectangle(barBorder, 3, 14, 4, 7);

                    // Barra 2 (Média)

                    g.FillRectangle(barBrush2, 8, 9, 4, 12);

                    g.DrawRectangle(barBorder, 8, 9, 4, 12);

                    // Barra 3 (Pico / Moda)

                    g.FillRectangle(barBrush3, 13, 4, 4, 17);

                    g.DrawRectangle(barBorder, 13, 4, 4, 17);

                    // Barra 4 (Média)

                    g.FillRectangle(barBrush4, 18, 11, 4, 10);

                    g.DrawRectangle(barBorder, 18, 11, 4, 10);

                }

                // Letra 'f'

                using (var font = new Font("Georgia", 6.5f, FontStyle.Bold | FontStyle.Italic))

                using (var textBrush = new SolidBrush(Color.FromArgb(40, 50, 70)))

                {

                    g.DrawString("f", font, textBrush, 1f, 1f);

                }

            }

            return bmp;

        }

        // 13. Trimmed & Winsorized Mean: Curva normal com extremos cortados/hachurados

        private static Bitmap DrawTrimmedWinsorizedMean()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Áreas de corte nas caudas (Sombreado vermelho claro)

                using (var cutBrush = new SolidBrush(Color.FromArgb(250, 219, 216)))

                {

                    g.FillRectangle(cutBrush, 2, 4, 4, 16);

                    g.FillRectangle(cutBrush, 18, 4, 4, 16);

                }

                // Linhas de corte verticais (Linhas pontilhadas vermelhas)

                using (var cutPen = new Pen(Color.FromArgb(231, 76, 60), 1.2f) { DashStyle = DashStyle.Dash })

                {

                    g.DrawLine(cutPen, 6, 3, 6, 21);

                    g.DrawLine(cutPen, 18, 3, 18, 21);

                }

                // Curva Normal central (Azul)

                using (var curvePen = new Pen(Color.FromArgb(41, 128, 185), 1.5f))

                {

                    GraphicsPath bell = new GraphicsPath();

                    bell.AddBezier(new PointF(2, 19), new PointF(6, 18), new PointF(9, 6), new PointF(12, 5));

                    bell.AddBezier(new PointF(12, 5), new PointF(15, 6), new PointF(18, 18), new PointF(22, 19));

                    g.DrawPath(curvePen, bell);

                }

                // Linha de média truncada central

                using (var meanPen = new Pen(Color.FromArgb(243, 156, 18), 1.5f))

                {

                    g.DrawLine(meanPen, 12, 5, 12, 20);

                }

                // Letra "x̄t"

                using (var font = new Font("Arial", 5.5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(140, 60, 20)))

                {

                    g.DrawString("x̄t", font, textBrush, 13f, 1f);

                }

            }

            return bmp;

        }

        // 14. Model Evaluation (RMSE, MAE, R²): Curva real vs predita com checkmark

        private static Bitmap DrawModelEvaluation()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Curva Real / Medida (Linha sólida azul)

                using (var truePen = new Pen(Color.FromArgb(41, 128, 185), 1.6f))

                {

                    GraphicsPath truePath = new GraphicsPath();

                    truePath.AddBezier(new PointF(2, 18), new PointF(7, 6), new PointF(14, 19), new PointF(22, 7));

                    g.DrawPath(truePen, truePath);

                }

                // Curva Simulada / Estimada (Linha tracejada laranja)

                using (var predPen = new Pen(Color.FromArgb(230, 126, 34), 1.4f) { DashStyle = DashStyle.Dash })

                {

                    GraphicsPath predPath = new GraphicsPath();

                    predPath.AddBezier(new PointF(2, 16), new PointF(7, 8), new PointF(14, 17), new PointF(22, 9));

                    g.DrawPath(predPen, predPath);

                }

                // Rótulo R²

                using (var font = new Font("Arial", 6.5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(39, 174, 96)))

                {

                    g.DrawString("R²", font, textBrush, 1f, 1f);

                }

                // Mini selo de validação

                using (var checkPen = new Pen(Color.FromArgb(39, 174, 96), 1.5f))

                {

                    g.DrawLine(checkPen, 15, 18, 18, 21);

                    g.DrawLine(checkPen, 18, 21, 22, 15);

                }

            }

            return bmp;

        }

        // 15. Tree Filter: Árvore com ramos e funil de filtro mantendo nós

        private static Bitmap DrawTreeFilter()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Ramos de árvore

                using (var treePen = new Pen(Color.FromArgb(52, 152, 219), 1.4f))

                {

                    g.DrawLine(treePen, 3, 12, 8, 12);

                    g.DrawLine(treePen, 8, 12, 14, 6);

                    g.DrawLine(treePen, 8, 12, 14, 18);

                    g.DrawLine(treePen, 14, 6, 21, 6);

                    g.DrawLine(treePen, 14, 18, 21, 18);

                }

                // Nós da árvore

                using (var nodeBrush = new SolidBrush(Color.FromArgb(41, 128, 185)))

                {

                    g.FillEllipse(nodeBrush, 1.5f, 10.5f, 3f, 3f);

                    g.FillEllipse(nodeBrush, 6.5f, 10.5f, 3f, 3f);

                    g.FillEllipse(nodeBrush, 12.5f, 4.5f, 3f, 3f);

                    g.FillEllipse(nodeBrush, 12.5f, 16.5f, 3f, 3f);

                }

                // Funil / Filtro central em Verde

                using (var filterBrush = new SolidBrush(Color.FromArgb(46, 204, 113)))

                using (var filterPen = new Pen(Color.FromArgb(30, 130, 70), 1f))

                {

                    PointF[] funnel = new PointF[]

                    {

                        new PointF(14, 8),

                        new PointF(22, 8),

                        new PointF(19, 13),

                        new PointF(19, 16),

                        new PointF(17, 16),

                        new PointF(17, 13)

                    };

                    g.FillPolygon(filterBrush, funnel);

                    g.DrawPolygon(filterPen, funnel);

                }

            }

            return bmp;

        }

        // 16. Deep Path Replace / RegEx: Path {0;1;2} com lupa/regex e seta

        private static Bitmap DrawDeepPathReplace()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Chaves de Path { }

                using (var font = new Font("Consolas", 6.5f, FontStyle.Bold))

                using (var pathBrush = new SolidBrush(Color.FromArgb(142, 68, 173)))

                {

                    g.DrawString("{A;B}", font, pathBrush, 1f, 2f);

                }

                // Seta de substituição

                using (var arrowPen = new Pen(Color.FromArgb(41, 128, 185), 1.5f))

                {

                    g.DrawLine(arrowPen, 4, 13, 15, 13);

                    g.DrawLine(arrowPen, 12, 10, 15, 13);

                    g.DrawLine(arrowPen, 12, 16, 15, 13);

                }

                // Novo Path substituído

                using (var font = new Font("Consolas", 6.5f, FontStyle.Bold))

                using (var pathBrush = new SolidBrush(Color.FromArgb(39, 174, 96)))

                {

                    g.DrawString("{B;A}", font, pathBrush, 3f, 15f);

                }

                // Símbolo .* de Regex

                using (var regexFont = new Font("Arial", 5.5f, FontStyle.Bold))

                using (var regexBrush = new SolidBrush(Color.FromArgb(230, 126, 34)))

                {

                    g.DrawString(".*", regexFont, regexBrush, 16.5f, 9f);

                }

            }

            return bmp;

        }

        // 17. Tree Partition por Comprimentos Variados: Bloco particionado [2|5|3]

        private static Bitmap DrawTreePartitionVariable()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Bloco 1 (tamanho 2 - Laranja)

                using (var b1 = new SolidBrush(Color.FromArgb(230, 126, 34)))

                using (var border = new Pen(Color.FromArgb(60, 70, 80), 0.8f))

                {

                    g.FillRectangle(b1, 2, 4, 5, 16);

                    g.DrawRectangle(border, 2, 4, 5, 16);

                }

                // Bloco 2 (tamanho 5 - Azul)

                using (var b2 = new SolidBrush(Color.FromArgb(52, 152, 219)))

                using (var border = new Pen(Color.FromArgb(60, 70, 80), 0.8f))

                {

                    g.FillRectangle(b2, 8, 4, 9, 16);

                    g.DrawRectangle(border, 8, 4, 9, 16);

                }

                // Bloco 3 (tamanho 3 - Verde)

                using (var b3 = new SolidBrush(Color.FromArgb(46, 204, 113)))

                using (var border = new Pen(Color.FromArgb(60, 70, 80), 0.8f))

                {

                    g.FillRectangle(b3, 18, 4, 4, 16);

                    g.DrawRectangle(border, 18, 4, 4, 16);

                }

                // Linha de particionador no topo

                using (var font = new Font("Arial", 5.5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(20, 30, 40)))

                {

                    g.DrawString("L1", font, textBrush, 2f, 7f);

                    g.DrawString("L2", font, textBrush, 9.5f, 7f);

                }

            }

            return bmp;

        }

        // Tree Path Item: Diagrama de ramificação com seleção/fatia {i} em destaque
        private static Bitmap DrawTreePathItem()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Árvore esquemática (cinza)
                using (var penTree = new Pen(Color.FromArgb(149, 165, 166), 1.3f))
                {
                    g.DrawLine(penTree, 3, 12, 8, 12);
                    g.DrawLine(penTree, 8, 12, 14, 5);
                    g.DrawLine(penTree, 8, 12, 14, 12);
                    g.DrawLine(penTree, 8, 12, 14, 19);
                }

                // Nós da árvore comuns (azul suave)
                using (var nodeBrush = new SolidBrush(Color.FromArgb(52, 152, 219)))
                {
                    g.FillEllipse(nodeBrush, 2, 10.5f, 3, 3);
                    g.FillEllipse(nodeBrush, 13, 10.5f, 3, 3);
                    g.FillEllipse(nodeBrush, 13, 17.5f, 3, 3);
                }

                // Ramo fatiado selecionado (laranja vibrante)
                using (var selPen = new Pen(Color.FromArgb(230, 126, 34), 1.8f))
                {
                    g.DrawLine(selPen, 8, 12, 14, 5);
                }
                using (var selNode = new SolidBrush(Color.FromArgb(230, 126, 34)))
                {
                    g.FillEllipse(selNode, 13, 3.5f, 4, 4);
                }

                // Indicador de fatia {i}
                using (var font = new Font("Consolas", 6.2f, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.FromArgb(44, 62, 80)))
                {
                    g.DrawString("{i}", font, textBrush, 13f, 12.5f);
                }
            }
            return bmp;
        }

        // Tree Search: Lupa estilizada sobre lista/árvore de dados
        private static Bitmap DrawTreeSearch()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Linhas de lista/árvore ao fundo
                using (var linePen = new Pen(Color.FromArgb(189, 195, 199), 1.4f))
                {
                    g.DrawLine(linePen, 3, 5, 12, 5);
                    g.DrawLine(linePen, 3, 9, 9, 9);
                    g.DrawLine(linePen, 3, 14, 8, 14);
                    g.DrawLine(linePen, 3, 19, 13, 19);
                }

                // Itens destacados (pontos)
                using (var ptBrush = new SolidBrush(Color.FromArgb(52, 152, 219)))
                {
                    g.FillEllipse(ptBrush, 1.5f, 4f, 2, 2);
                    g.FillEllipse(ptBrush, 1.5f, 8f, 2, 2);
                    g.FillEllipse(ptBrush, 1.5f, 13f, 2, 2);
                    g.FillEllipse(ptBrush, 1.5f, 18f, 2, 2);
                }

                // Lente da lupa (círculo com preenchimento translúcido e borda azul)
                using (var lensBg = new SolidBrush(Color.FromArgb(120, 26, 188, 156)))
                {
                    g.FillEllipse(lensBg, 8, 4, 11, 11);
                }
                using (var lensBorder = new Pen(Color.FromArgb(41, 128, 185), 2.0f))
                {
                    g.DrawEllipse(lensBorder, 8, 4, 11, 11);
                }

                // Brilho na lente
                using (var shinePen = new Pen(Color.FromArgb(200, 255, 255, 255), 1.2f))
                {
                    g.DrawArc(shinePen, 9.5f, 5.5f, 8, 8, 200, 60);
                }

                // Cabo da lupa (laranja/bronze)
                using (var handlePen = new Pen(Color.FromArgb(230, 126, 34), 2.8f))
                {
                    handlePen.StartCap = LineCap.Round;
                    handlePen.EndCap = LineCap.Round;
                    g.DrawLine(handlePen, 16.5f, 15.5f, 21.5f, 20.5f);
                }
            }
            return bmp;
        }

        // 18. Tree Align / Match Topology: Duas árvores alinhadas com ponte pontilhada

        private static Bitmap DrawTreeAlignTopology()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Árvore A (Azul - esquerda)

                using (var penA = new Pen(Color.FromArgb(41, 128, 185), 1.4f))

                using (var brushA = new SolidBrush(Color.FromArgb(41, 128, 185)))

                {

                    g.DrawLine(penA, 2, 6, 8, 6);

                    g.DrawLine(penA, 2, 18, 8, 18);

                    g.FillEllipse(brushA, 7f, 4.5f, 3f, 3f);

                    g.FillEllipse(brushA, 7f, 16.5f, 3f, 3f);

                }

                // Árvore B (Laranja - direita)

                using (var penB = new Pen(Color.FromArgb(230, 126, 34), 1.4f))

                using (var brushB = new SolidBrush(Color.FromArgb(230, 126, 34)))

                {

                    g.DrawLine(penB, 16, 6, 22, 6);

                    g.DrawLine(penB, 16, 12, 22, 12);

                    g.DrawLine(penB, 16, 18, 22, 18);

                    g.FillEllipse(brushB, 14.5f, 4.5f, 3f, 3f);

                    g.FillEllipse(brushB, 14.5f, 10.5f, 3f, 3f);

                    g.FillEllipse(brushB, 14.5f, 16.5f, 3f, 3f);

                }

                // Pontes de alinhamento / preenchimento (Verde pontilhado)

                using (var alignPen = new Pen(Color.FromArgb(46, 204, 113), 1.2f) { DashStyle = DashStyle.Dot })

                {

                    g.DrawLine(alignPen, 8, 6, 16, 6);

                    g.DrawLine(alignPen, 8, 12, 16, 12);

                    g.DrawLine(alignPen, 8, 18, 16, 18);

                }

                // Ramo preenchido com null/default (círculo oco)

                using (var nullPen = new Pen(Color.FromArgb(149, 165, 166), 1f) { DashStyle = DashStyle.Dash })

                {

                    g.DrawEllipse(nullPen, 7f, 10.5f, 3f, 3f);

                }

            }

            return bmp;

        }

        // 19. Batch Distinct / Unique: Pontos duplicados mesclados em únicos com índices

        private static Bitmap DrawBatchDistinct()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Lista de entrada com duplicatas (lado esquerdo)

                using (var bRed = new SolidBrush(Color.FromArgb(231, 76, 60)))

                using (var bBlue = new SolidBrush(Color.FromArgb(52, 152, 219)))

                {

                    g.FillEllipse(bRed, 3, 3, 4, 4);

                    g.FillEllipse(bBlue, 3, 9, 4, 4);

                    g.FillEllipse(bRed, 3, 15, 4, 4); // Duplicata

                }

                // Seta de filtro / Distinct

                using (var arrowPen = new Pen(Color.FromArgb(120, 130, 145), 1.2f))

                {

                    g.DrawLine(arrowPen, 9, 11, 14, 11);

                    g.DrawLine(arrowPen, 12, 9, 14, 11);

                    g.DrawLine(arrowPen, 12, 13, 14, 11);

                }

                // Saída Única (lado direito)

                using (var bRed = new SolidBrush(Color.FromArgb(231, 76, 60)))

                using (var bBlue = new SolidBrush(Color.FromArgb(52, 152, 219)))

                {

                    g.FillEllipse(bRed, 17, 5, 5, 5);

                    g.FillEllipse(bBlue, 17, 13, 5, 5);

                }

                // Checkmark / Badge de Único

                using (var font = new Font("Arial", 5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(39, 174, 96)))

                {

                    g.DrawString("1x", font, textBrush, 15f, 1f);

                }

            }

            return bmp;

        }

        // 20. Tree Conditional Pruner / Dispatch: Ramo dividido em True e False com matriz intacta

        private static Bitmap DrawTreeConditionalPrunerDispatch()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Tronco comum

                using (var trunkPen = new Pen(Color.FromArgb(100, 110, 125), 1.5f))

                {

                    g.DrawLine(trunkPen, 2, 12, 7, 12);

                }

                // Ramo True (Verde - acima)

                using (var truePen = new Pen(Color.FromArgb(46, 204, 113), 1.5f))

                using (var trueBrush = new SolidBrush(Color.FromArgb(46, 204, 113)))

                {

                    g.DrawLine(truePen, 7, 12, 13, 6);

                    g.DrawLine(truePen, 13, 6, 22, 6);

                    g.FillEllipse(trueBrush, 12f, 4.5f, 3f, 3f);

                    g.FillEllipse(trueBrush, 20f, 4.5f, 3f, 3f);

                }

                // Ramo False (Laranja/Vermelho - abaixo)

                using (var falsePen = new Pen(Color.FromArgb(231, 76, 60), 1.5f))

                using (var falseBrush = new SolidBrush(Color.FromArgb(231, 76, 60)))

                {

                    g.DrawLine(falsePen, 7, 12, 13, 18);

                    g.DrawLine(falsePen, 13, 18, 22, 18);

                    g.FillEllipse(falseBrush, 12f, 16.5f, 3f, 3f);

                    g.FillEllipse(falseBrush, 20f, 16.5f, 3f, 3f);

                }

                // Rótulos T e F

                using (var font = new Font("Arial", 5.5f, FontStyle.Bold))

                {

                    g.DrawString("T", font, new SolidBrush(Color.FromArgb(30, 130, 70)), 15f, 1f);

                    g.DrawString("F", font, new SolidBrush(Color.FromArgb(160, 40, 30)), 15f, 13f);

                }

            }

            return bmp;

        }

        // 21. Tree GroupBy / Bucket by Key: Itens misturados caindo em baldes/ramos por chave

        private static Bitmap DrawTreeGroupBy()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Balde 1 {0} (Azul)

                using (var b1 = new SolidBrush(Color.FromArgb(52, 152, 219)))

                using (var pen = new Pen(Color.FromArgb(30, 80, 130), 1f))

                {

                    g.FillRectangle(b1, 2, 13, 9, 9);

                    g.DrawRectangle(pen, 2, 13, 9, 9);

                }

                // Balde 2 {1} (Laranja)

                using (var b2 = new SolidBrush(Color.FromArgb(230, 126, 34)))

                using (var pen = new Pen(Color.FromArgb(140, 60, 15), 1f))

                {

                    g.FillRectangle(b2, 13, 13, 9, 9);

                    g.DrawRectangle(pen, 13, 13, 9, 9);

                }

                // Setas direcionando itens para os baldes

                using (var arrowPen1 = new Pen(Color.FromArgb(52, 152, 219), 1.2f))

                using (var arrowPen2 = new Pen(Color.FromArgb(230, 126, 34), 1.2f))

                {

                    g.DrawLine(arrowPen1, 9, 3, 6, 12);

                    g.DrawLine(arrowPen2, 15, 3, 18, 12);

                }

                // Itens no topo

                using (var dot1 = new SolidBrush(Color.FromArgb(52, 152, 219)))

                using (var dot2 = new SolidBrush(Color.FromArgb(230, 126, 34)))

                {

                    g.FillEllipse(dot1, 7.5f, 1.5f, 3.5f, 3.5f);

                    g.FillEllipse(dot2, 13.5f, 1.5f, 3.5f, 3.5f);

                }

                // Rótulo {K}

                using (var font = new Font("Arial", 5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(255, 255, 255)))

                {

                    g.DrawString("K1", font, textBrush, 3f, 14f);

                    g.DrawString("K2", font, textBrush, 14f, 14f);

                }

            }

            return bmp;

        }

        // 22. Dynamic Tree Weaver: Ramos entrelaçados dinamicamente {0;0}, {0;1}

        private static Bitmap DrawDynamicTreeWeaver()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Fio A (Azul - ondulado)

                using (var penA = new Pen(Color.FromArgb(41, 128, 185), 1.8f))

                {

                    GraphicsPath waveA = new GraphicsPath();

                    waveA.AddBezier(new PointF(2, 6), new PointF(8, 6), new PointF(10, 18), new PointF(16, 18));

                    waveA.AddBezier(new PointF(16, 18), new PointF(18, 18), new PointF(20, 6), new PointF(22, 6));

                    g.DrawPath(penA, waveA);

                }

                // Fio B (Laranja - ondulado em oposição de fase)

                using (var penB = new Pen(Color.FromArgb(230, 126, 34), 1.8f))

                {

                    GraphicsPath waveB = new GraphicsPath();

                    waveB.AddBezier(new PointF(2, 18), new PointF(8, 18), new PointF(10, 6), new PointF(16, 6));

                    waveB.AddBezier(new PointF(16, 6), new PointF(18, 6), new PointF(20, 18), new PointF(22, 18));

                    g.DrawPath(penB, waveB);

                }

                // Pontos de interseção tecida

                using (var nodeBrush = new SolidBrush(Color.FromArgb(142, 68, 173)))

                {

                    g.FillEllipse(nodeBrush, 8.5f, 10.5f, 3f, 3f);

                    g.FillEllipse(nodeBrush, 16.5f, 10.5f, 3f, 3f);

                }

                // Símbolo Weave '~'

                using (var font = new Font("Arial", 6f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(40, 50, 70)))

                {

                    g.DrawString("W", font, textBrush, 1f, 1f);

                }

            }

            return bmp;

        }

        // 23. Tree Structural Diff: Comparador de árvores com A-only, B-only e delta

        private static Bitmap DrawTreeStructuralDiff()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Círculo Venn A (Azul transparente)

                using (var brushA = new SolidBrush(Color.FromArgb(150, 52, 152, 219)))

                using (var penA = new Pen(Color.FromArgb(41, 128, 185), 1.2f))

                {

                    g.FillEllipse(brushA, 2, 4, 13, 13);

                    g.DrawEllipse(penA, 2, 4, 13, 13);

                }

                // Círculo Venn B (Laranja transparente)

                using (var brushB = new SolidBrush(Color.FromArgb(150, 230, 126, 34)))

                using (var penB = new Pen(Color.FromArgb(211, 84, 0), 1.2f))

                {

                    g.FillEllipse(brushB, 9, 4, 13, 13);

                    g.DrawEllipse(penB, 9, 4, 13, 13);

                }

                // Símbolo Delta Δ de diferença

                using (var font = new Font("Arial", 6.5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(142, 68, 173)))

                {

                    g.DrawString("Δ", font, textBrush, 9f, 6.5f);

                }

                // Letras A e B

                using (var font = new Font("Arial", 5f, FontStyle.Bold))

                {

                    g.DrawString("A", font, new SolidBrush(Color.FromArgb(20, 70, 120)), 3.5f, 8f);

                    g.DrawString("B", font, new SolidBrush(Color.FromArgb(120, 50, 10)), 17f, 8f);

                }

            }

            return bmp;

        }

        // 24. Path Math / Index Arithmetic: Ramo {0; i+1} com operadores matemáticos

        private static Bitmap DrawPathMath()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Chaves de Path { }

                using (var font = new Font("Consolas", 6.5f, FontStyle.Bold))

                using (var pathBrush = new SolidBrush(Color.FromArgb(41, 128, 185)))

                {

                    g.DrawString("{i+1}", font, pathBrush, 2f, 2f);

                }

                // Eixo / Operador + e -

                using (var opBrush1 = new SolidBrush(Color.FromArgb(46, 204, 113)))

                using (var opBrush2 = new SolidBrush(Color.FromArgb(231, 76, 60)))

                using (var font = new Font("Arial", 7f, FontStyle.Bold))

                {

                    g.DrawString("+", font, opBrush1, 4f, 12f);

                    g.DrawString("−", font, opBrush2, 14f, 12f);

                }

                // Linha de hierarquia / shift

                using (var shiftPen = new Pen(Color.FromArgb(142, 68, 173), 1.2f))

                {

                    g.DrawLine(shiftPen, 3, 19, 21, 19);

                    g.DrawLine(shiftPen, 18, 16, 21, 19);

                    g.DrawLine(shiftPen, 18, 22, 21, 19);

                }

            }

            return bmp;

        }

        // 25. Conditional Trigger / Debounced Watcher: Onda quadrada com borda de subida e cronômetro

        private static Bitmap DrawConditionalTrigger()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Onda digital quadrada (Sinal de clock / trigger)

                using (var wavePen = new Pen(Color.FromArgb(41, 128, 185), 1.6f))

                {

                    PointF[] wave = new PointF[]

                    {

                        new PointF(2, 17),

                        new PointF(7, 17),

                        new PointF(7, 7),

                        new PointF(13, 7),

                        new PointF(13, 17),

                        new PointF(18, 17)

                    };

                    g.DrawLines(wavePen, wave);

                }

                // Borda de subida destacada (Seta verde para cima)

                using (var edgePen = new Pen(Color.FromArgb(46, 204, 113), 1.8f))

                {

                    g.DrawLine(edgePen, 7, 17, 7, 7);

                    g.DrawLine(edgePen, 5, 10, 7, 7);

                    g.DrawLine(edgePen, 9, 10, 7, 7);

                }

                // Mini relógio / temporizador de Debounce (Laranja)

                using (var timerBrush = new SolidBrush(Color.FromArgb(254, 249, 231)))

                using (var timerPen = new Pen(Color.FromArgb(230, 126, 34), 1.2f))

                {

                    g.FillEllipse(timerBrush, 14f, 4f, 8f, 8f);

                    g.DrawEllipse(timerPen, 14f, 4f, 8f, 8f);

                    g.DrawLine(timerPen, 18, 8, 18, 5.5f);

                    g.DrawLine(timerPen, 18, 8, 20, 8);

                }

                // Símbolo ms

                using (var font = new Font("Arial", 5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(230, 126, 34)))

                {

                    g.DrawString("ms", font, textBrush, 14f, 13f);

                }

            }

            return bmp;

        }

        // 26. State Latch / Flip-Flop: Cadeado/portão que trava o último estado válido

        private static Bitmap DrawStateLatch()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Fluxo de dados (Linha azul entrando e saindo)

                using (var streamPen = new Pen(Color.FromArgb(52, 152, 219), 1.5f))

                {

                    g.DrawLine(streamPen, 2, 12, 6, 12);

                    g.DrawLine(streamPen, 18, 12, 22, 12);

                }

                // Cadeado central (Corpo dourado/amarelo de trava)

                using (var lockBodyBrush = new SolidBrush(Color.FromArgb(241, 196, 15)))

                using (var lockBodyPen = new Pen(Color.FromArgb(180, 140, 10), 1f))

                {

                    g.FillRectangle(lockBodyBrush, 6, 10, 12, 10);

                    g.DrawRectangle(lockBodyPen, 6, 10, 12, 10);

                }

                // Alça do cadeado (Arco de trava fechado)

                using (var shacklePen = new Pen(Color.FromArgb(100, 110, 125), 1.5f))

                {

                    g.DrawArc(shacklePen, 8, 4, 8, 8, 180, 180);

                    g.DrawLine(shacklePen, 8, 8, 8, 10);

                    g.DrawLine(shacklePen, 16, 8, 16, 10);

                }

                // Fechadura central

                using (var holeBrush = new SolidBrush(Color.FromArgb(50, 40, 10)))

                {

                    g.FillEllipse(holeBrush, 11f, 13f, 2f, 2f);

                    g.FillRectangle(holeBrush, 11.5f, 14.5f, 1f, 2.5f);

                }

                // Status de trava (Letra Q / L)

                using (var font = new Font("Arial", 5.5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(39, 174, 96)))

                {

                    g.DrawString("Q", font, textBrush, 1f, 1f);

                }

            }

            return bmp;

        }

        // 27. Iterative Accumulator: Buffer circular com disco de armazenamento

        private static Bitmap DrawIterativeAccumulator()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Setas circulares do buffer em anel (Ciano e Roxo)

                using (var ringPen = new Pen(Color.FromArgb(26, 188, 156), 2f))

                {

                    g.DrawArc(ringPen, 3, 3, 18, 18, 45, 270);

                }

                // Cabeça da seta circular

                using (var arrowBrush = new SolidBrush(Color.FromArgb(22, 160, 133)))

                {

                    PointF[] head = new PointF[]

                    {

                        new PointF(18, 7),

                        new PointF(21, 3),

                        new PointF(22, 8)

                    };

                    g.FillPolygon(arrowBrush, head);

                }

                // Disco / Ícone de exportação no centro

                using (var diskBrush = new SolidBrush(Color.FromArgb(52, 152, 219)))

                using (var diskPen = new Pen(Color.FromArgb(41, 128, 185), 0.8f))

                {

                    g.FillRectangle(diskBrush, 8, 8, 8, 8);

                    g.DrawRectangle(diskPen, 8, 8, 8, 8);

                }

                // Rótulo "K" de tamanho do buffer

                using (var font = new Font("Arial", 5.5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(255, 255, 255)))

                {

                    g.DrawString("K", font, textBrush, 9f, 8.5f);

                }

            }

            return bmp;

        }

        // 28. Convergence Watcher: Curva amortecida convergindo para tolerância com STOP

        private static Bitmap DrawConvergenceWatcher()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Banda de tolerância epsilon sombreada (Verde claro)

                using (var bandBrush = new SolidBrush(Color.FromArgb(215, 245, 225)))

                {

                    g.FillRectangle(bandBrush, 2, 9, 20, 6);

                }

                // Limites pontilhados de ±ε

                using (var epsPen = new Pen(Color.FromArgb(46, 204, 113), 1f) { DashStyle = DashStyle.Dot })

                {

                    g.DrawLine(epsPen, 2, 9, 22, 9);

                    g.DrawLine(epsPen, 2, 15, 22, 15);

                }

                // Linha de centro alvo (Zero)

                using (var centerPen = new Pen(Color.FromArgb(160, 180, 170), 1f))

                {

                    g.DrawLine(centerPen, 2, 12, 22, 12);

                }

                // Curva de oscilação amortecida convergindo para o centro (Azul)

                using (var oscPen = new Pen(Color.FromArgb(41, 128, 185), 1.5f))

                {

                    GraphicsPath osc = new GraphicsPath();

                    osc.AddBezier(new PointF(2, 3), new PointF(5, 3), new PointF(6, 20), new PointF(9, 20));

                    osc.AddBezier(new PointF(9, 20), new PointF(11, 20), new PointF(13, 10), new PointF(15, 10));

                    osc.AddBezier(new PointF(15, 10), new PointF(17, 10), new PointF(19, 12), new PointF(22, 12));

                    g.DrawPath(oscPen, osc);

                }

                // Sinal STOP vermelho no topo direito

                using (var stopBrush = new SolidBrush(Color.FromArgb(231, 76, 60)))

                {

                    PointF[] oct = new PointF[]

                    {

                        new PointF(16, 2), new PointF(20, 2), new PointF(22, 4), new PointF(22, 8),

                        new PointF(20, 10), new PointF(16, 10), new PointF(14, 8), new PointF(14, 4)

                    };

                    g.FillPolygon(stopBrush, oct);

                }

                using (var font = new Font("Arial", 4.5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(255, 255, 255)))

                {

                    g.DrawString("OK", font, textBrush, 15f, 3.5f);

                }

            }

            return bmp;

        }

        // 28B. Conditional Timer / Smart Watcher: Cronômetro / sentinela com pulso e limite de disparo
        private static Bitmap DrawConditionalTimer()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Botão superior do cronômetro (Pusher / Coroa)
                using (var crownBrush = new SolidBrush(Color.FromArgb(9, 132, 227)))
                using (var crownPen = new Pen(Color.FromArgb(116, 185, 255), 1f))
                {
                    g.FillRectangle(crownBrush, 9, 1, 6, 2);
                    g.DrawRectangle(crownPen, 9, 1, 6, 2);
                }

                // Botão lateral angular
                using (var sidePen = new Pen(Color.FromArgb(9, 132, 227), 1.5f))
                {
                    g.DrawLine(sidePen, 18, 4, 16, 6);
                }

                // Corpo do cronômetro (Aro externo ciano/turquesa)
                using (var ringPen = new Pen(Color.FromArgb(0, 206, 201), 1.8f))
                {
                    g.DrawEllipse(ringPen, 2.5f, 3.5f, 17f, 17f);
                }

                // Marcadores de hora (12, 3, 6, 9)
                using (var tickPen = new Pen(Color.FromArgb(45, 52, 54), 1.2f))
                {
                    g.DrawLine(tickPen, 11, 5, 11, 7);   // 12h
                    g.DrawLine(tickPen, 17, 12, 15, 12); // 3h
                    g.DrawLine(tickPen, 11, 19, 11, 17); // 6h
                    g.DrawLine(tickPen, 5, 12, 7, 12);   // 9h
                }

                // Ponteiro do relógio indicando tempo decorrido
                using (var handPen = new Pen(Color.FromArgb(231, 76, 60), 1.5f))
                {
                    g.DrawLine(handPen, 11, 12, 14, 8);
                }

                // Ponto de pivô central
                using (var centerBrush = new SolidBrush(Color.FromArgb(45, 52, 54)))
                {
                    g.FillEllipse(centerBrush, 10f, 11f, 2f, 2f);
                }

                // Badge Sentinela / Pulso de Ativação no canto inferior direito (Raio / Olho âmbar)
                using (var badgeBg = new SolidBrush(Color.FromArgb(241, 196, 15)))
                using (var badgePen = new Pen(Color.FromArgb(211, 84, 0), 0.8f))
                {
                    PointF[] bolt = new PointF[]
                    {
                        new PointF(18, 11),
                        new PointF(14, 17),
                        new PointF(17, 17),
                        new PointF(15, 23),
                        new PointF(22, 15),
                        new PointF(18, 15)
                    };
                    g.FillPolygon(badgeBg, bolt);
                    g.DrawPolygon(badgePen, bolt);
                }
            }
            return bmp;
        }

        // 28C. Data Generation Stopwatch & Benchmark: Cronômetro analítico com relógio, matriz de dados e ponteiro de velocidade
        private static Bitmap DrawDataGenerationTimer()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Botão superior do cronômetro (Pusher)
                using (var crownBrush = new SolidBrush(Color.FromArgb(225, 112, 85)))
                using (var crownPen = new Pen(Color.FromArgb(250, 177, 160), 1f))
                {
                    g.FillRectangle(crownBrush, 9, 1, 6, 2);
                    g.DrawRectangle(crownPen, 9, 1, 6, 2);
                }

                // Botão de início rápido lateral
                using (var sidePen = new Pen(Color.FromArgb(225, 112, 85), 1.5f))
                {
                    g.DrawLine(sidePen, 18, 4, 16, 6);
                }

                // Corpo do cronômetro (Aro externo esmeralda/verde performance)
                using (var ringPen = new Pen(Color.FromArgb(0, 184, 148), 1.8f))
                {
                    g.DrawEllipse(ringPen, 2.5f, 3.5f, 17f, 17f);
                }

                // Fundo translúcido sutil
                using (var bgBrush = new SolidBrush(Color.FromArgb(25, 0, 184, 148)))
                {
                    g.FillEllipse(bgBrush, 3.5f, 4.5f, 15f, 15f);
                }

                // Mini grade de dados no quadrante superior esquerdo (representando os dados gerados)
                using (var dataBrush = new SolidBrush(Color.FromArgb(9, 132, 227)))
                {
                    g.FillRectangle(dataBrush, 6.5f, 7.5f, 2.5f, 2.5f);
                    g.FillRectangle(dataBrush, 10f, 7.5f, 2.5f, 2.5f);
                    g.FillRectangle(dataBrush, 6.5f, 11f, 2.5f, 2.5f);
                }

                // Marcadores de quadrante (12, 3, 6, 9)
                using (var tickPen = new Pen(Color.FromArgb(99, 110, 114), 1.2f))
                {
                    g.DrawLine(tickPen, 11, 4.5f, 11, 6.5f); // 12h
                    g.DrawLine(tickPen, 17.5f, 12, 15.5f, 12); // 3h
                    g.DrawLine(tickPen, 11, 18.5f, 11, 16.5f); // 6h
                    g.DrawLine(tickPen, 4.5f, 12, 6.5f, 12);   // 9h
                }

                // Ponteiro de velocidade (laranja acelerado)
                using (var handPen = new Pen(Color.FromArgb(253, 121, 168), 1.6f))
                {
                    g.DrawLine(handPen, 11, 12, 15, 9);
                }

                // Centro / Pivô
                using (var centerBrush = new SolidBrush(Color.FromArgb(45, 52, 54)))
                {
                    g.FillEllipse(centerBrush, 9.8f, 10.8f, 2.4f, 2.4f);
                }
            }
            return bmp;
        }

        // Multi-Input Logic Gate: Símbolo de porta lógica consolidada com múltiplos pinos de entrada
        private static Bitmap DrawMultiLogicGate()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Pinos de entrada (esquerda)
                using (var wirePen = new Pen(Color.FromArgb(41, 128, 185), 1.5f))
                {
                    g.DrawLine(wirePen, 2, 6, 7, 6);
                    g.DrawLine(wirePen, 2, 12, 7, 12);
                    g.DrawLine(wirePen, 2, 18, 7, 18);
                }

                // Nós de entrada (LEDs verdes)
                using (var inBrush = new SolidBrush(Color.FromArgb(46, 204, 113)))
                {
                    g.FillEllipse(inBrush, 1.5f, 4.8f, 2.5f, 2.5f);
                    g.FillEllipse(inBrush, 1.5f, 10.8f, 2.5f, 2.5f);
                    g.FillEllipse(inBrush, 1.5f, 16.8f, 2.5f, 2.5f);
                }

                // Pino de saída (direita)
                using (var outPen = new Pen(Color.FromArgb(230, 126, 34), 1.8f))
                {
                    g.DrawLine(outPen, 18, 12, 22.5f, 12);
                }
                using (var outBrush = new SolidBrush(Color.FromArgb(230, 126, 34)))
                {
                    g.FillEllipse(outBrush, 21f, 10.5f, 2.8f, 2.8f);
                }

                // Corpo da Porta Lógica (estilo AND/OR D-shape)
                using (var gatePath = new GraphicsPath())
                {
                    gatePath.AddLine(7, 3, 13, 3);
                    gatePath.AddArc(7, 3, 12, 18, -90, 180);
                    gatePath.AddLine(13, 21, 7, 21);
                    gatePath.CloseFigure();

                    using (var gateBg = new SolidBrush(Color.FromArgb(44, 62, 80)))
                    using (var gateBorder = new Pen(Color.FromArgb(52, 152, 219), 1.5f))
                    {
                        g.FillPath(gateBg, gatePath);
                        g.DrawPath(gateBorder, gatePath);
                    }
                }

                // Símbolo central "&"
                using (var font = new Font("Arial", 6.5f, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.FromArgb(241, 196, 15)))
                {
                    g.DrawString("&", font, textBrush, 8.5f, 7f);
                }
            }
            return bmp;
        }

        // Threshold Voting Gate: Medidor de Quórum / Votação com arco percentual e checkmark
        private static Bitmap DrawThresholdVotingGate()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Arco de quórum / aprovação ao redor
                using (var trackPen = new Pen(Color.FromArgb(189, 195, 199), 2.0f))
                {
                    g.DrawArc(trackPen, 3, 3, 18, 18, 135, 270);
                }

                // Arco de aprovação atingida (verde esmeralda, ~75% de conformidade)
                using (var passPen = new Pen(Color.FromArgb(39, 174, 96), 2.5f))
                {
                    g.DrawArc(passPen, 3, 3, 18, 18, 135, 200);
                }

                // Marcador de quórum / threshold (tick âmbar)
                using (var tickPen = new Pen(Color.FromArgb(230, 126, 34), 2.0f))
                {
                    g.DrawLine(tickPen, 12, 3, 12, 6);
                }

                // Checkmark central de aprovação
                using (var checkPen = new Pen(Color.FromArgb(46, 204, 113), 2.2f))
                {
                    checkPen.StartCap = LineCap.Round;
                    checkPen.EndCap = LineCap.Round;
                    g.DrawLine(checkPen, 8, 12, 11, 15.5f);
                    g.DrawLine(checkPen, 11, 15.5f, 16.5f, 9);
                }

                // Símbolo de porcentagem "%" no canto inferior
                using (var font = new Font("Arial", 5.2f, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.FromArgb(41, 128, 185)))
                {
                    g.DrawString("%", font, textBrush, 9f, 15.5f);
                }
            }
            return bmp;
        }

        // 29. Shannon Entropy: Termodinâmica / Teoria da Informação H(X) com barras de probabilidade e símbolo H

        private static Bitmap DrawShannonEntropy()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Barras de distribuição de probabilidade pi (Cores gradientes roxo/azul)

                using (var b1 = new SolidBrush(Color.FromArgb(155, 89, 182)))

                using (var b2 = new SolidBrush(Color.FromArgb(142, 68, 173)))

                using (var b3 = new SolidBrush(Color.FromArgb(52, 152, 219)))

                using (var b4 = new SolidBrush(Color.FromArgb(26, 188, 156)))

                using (var border = new Pen(Color.FromArgb(50, 40, 70), 0.8f))

                {

                    g.FillRectangle(b1, 3, 14, 3.5f, 7);

                    g.DrawRectangle(border, 3, 14, 3.5f, 7);

                    g.FillRectangle(b2, 8, 8, 3.5f, 13);

                    g.DrawRectangle(border, 8, 8, 3.5f, 13);

                    g.FillRectangle(b3, 13, 11, 3.5f, 10);

                    g.DrawRectangle(border, 13, 11, 3.5f, 10);

                    g.FillRectangle(b4, 18, 16, 3.5f, 5);

                    g.DrawRectangle(border, 18, 16, 3.5f, 5);

                }

                // Linha de base

                using (var basePen = new Pen(Color.FromArgb(120, 110, 140), 1f))

                {

                    g.DrawLine(basePen, 2, 21, 22, 21);

                }

                // Símbolo H(X)

                using (var font = new Font("Georgia", 7.5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(100, 30, 140)))

                {

                    g.DrawString("H", font, textBrush, 2f, 1f);

                }

                // Mini símbolo de bits

                using (var font = new Font("Arial", 5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(52, 152, 219)))

                {

                    g.DrawString("bit", font, textBrush, 13f, 2f);

                }

            }

            return bmp;

        }

        // 30. Kullback-Leibler Divergence: Duas curvas de distribuição P(x) e Q(x) com área de divergência

        private static Bitmap DrawKullbackLeiblerDivergence()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Área de divergência sombreada entre as duas curvas

                using (var shadeBrush = new SolidBrush(Color.FromArgb(140, 241, 196, 15)))

                {

                    PointF[] poly = new PointF[]

                    {

                        new PointF(6, 19), new PointF(8, 7), new PointF(11, 7), new PointF(15, 10), new PointF(17, 19)

                    };

                    g.FillPolygon(shadeBrush, poly);

                }

                // Curva de Distribuição P (Azul - Referência)

                using (var penP = new Pen(Color.FromArgb(41, 128, 185), 1.6f))

                {

                    GraphicsPath pathP = new GraphicsPath();

                    pathP.AddBezier(new PointF(2, 19), new PointF(5, 18), new PointF(7, 6), new PointF(10, 6));

                    pathP.AddBezier(new PointF(10, 6), new PointF(13, 6), new PointF(15, 18), new PointF(18, 19));

                    g.DrawPath(penP, pathP);

                }

                // Curva de Distribuição Q (Laranja/Vermelho - Modelo aproximado deslocado)

                using (var penQ = new Pen(Color.FromArgb(231, 76, 60), 1.5f) { DashStyle = DashStyle.Dash })

                {

                    GraphicsPath pathQ = new GraphicsPath();

                    pathQ.AddBezier(new PointF(6, 19), new PointF(9, 18), new PointF(11, 9), new PointF(14, 9));

                    pathQ.AddBezier(new PointF(14, 9), new PointF(17, 9), new PointF(19, 18), new PointF(22, 19));

                    g.DrawPath(penQ, pathQ);

                }

                // Linha de base

                using (var basePen = new Pen(Color.FromArgb(140, 160, 180), 1f))

                {

                    g.DrawLine(basePen, 2, 19, 22, 19);

                }

                // Símbolo D_kl

                using (var font = new Font("Arial", 5.5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(180, 40, 20)))

                {

                    g.DrawString("D", font, textBrush, 1f, 1f);

                }

                using (var font = new Font("Arial", 4.5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(41, 128, 185)))

                {

                    g.DrawString("KL", font, textBrush, 6.5f, 3f);

                }

            }

            return bmp;

        }

        // 31. Geometric Frequency (1D Clustering / Create Set com Tolerância): Calibre/colchete agrupando pontos com tol e badge N

        private static Bitmap DrawGeometricClusterFrequency()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Faixa de tolerância sombreada (Azul suave)

                using (var tolBand = new SolidBrush(Color.FromArgb(215, 235, 252)))

                {

                    g.FillRectangle(tolBand, 3, 5, 12, 14);

                }

                // Colchete de tolerância [  ]

                using (var bracketPen = new Pen(Color.FromArgb(41, 128, 185), 1.4f))

                {

                    // Lado esquerdo [

                    g.DrawLine(bracketPen, 5, 5, 3, 5);

                    g.DrawLine(bracketPen, 3, 5, 3, 19);

                    g.DrawLine(bracketPen, 3, 19, 5, 19);

                    // Lado direito ]

                    g.DrawLine(bracketPen, 13, 5, 15, 5);

                    g.DrawLine(bracketPen, 15, 5, 15, 19);

                    g.DrawLine(bracketPen, 15, 19, 13, 19);

                }

                // Pontos agrupados dentro da faixa de tolerância

                using (var ptBrush = new SolidBrush(Color.FromArgb(230, 126, 34)))

                using (var ptCenter = new SolidBrush(Color.FromArgb(46, 204, 113)))

                {

                    g.FillEllipse(ptBrush, 4.5f, 10.5f, 3f, 3f);

                    g.FillEllipse(ptBrush, 7.5f, 10.5f, 3f, 3f);

                    g.FillEllipse(ptBrush, 10.5f, 10.5f, 3f, 3f);

                    // Ponto Canônico Central

                    g.FillEllipse(ptCenter, 7.5f, 6.5f, 3.5f, 3.5f);

                }

                // Seta apontando para a contagem

                using (var arrowPen = new Pen(Color.FromArgb(41, 128, 185), 1.2f))

                {

                    g.DrawLine(arrowPen, 15, 12, 18, 12);

                    g.DrawLine(arrowPen, 17, 10, 19, 12);

                    g.DrawLine(arrowPen, 17, 14, 19, 12);

                }

                // Badge de Quantitativo / Contagem "N"

                using (var badgeBrush = new SolidBrush(Color.FromArgb(39, 174, 96)))

                {

                    g.FillRectangle(badgeBrush, 17, 2, 6, 7);

                }

                using (var font = new Font("Arial", 5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(255, 255, 255)))

                {

                    g.DrawString("N", font, textBrush, 17.5f, 2.5f);

                }

                // Rótulo ±tol no rodapé

                using (var font = new Font("Arial", 5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(30, 60, 90)))

                {

                    g.DrawString("±tol", font, textBrush, 13.5f, 15f);

                }

            }

            return bmp;

        }

        // 32. Line Chart & Statistics (Curvas com linhas de Média, Mediana e Desvio Padrão)

        private static Bitmap DrawChartLine()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Grid suave

                using (var gridPen = new Pen(Color.FromArgb(45, 55, 75), 0.8f))

                {

                    g.DrawLine(gridPen, 3, 7, 21, 7);

                    g.DrawLine(gridPen, 3, 13, 21, 13);

                    g.DrawLine(gridPen, 9, 3, 9, 21);

                    g.DrawLine(gridPen, 15, 3, 15, 21);

                }

                // Eixos cartesianos

                using (var axisPen = new Pen(Color.FromArgb(120, 140, 170), 1.2f))

                {

                    g.DrawLine(axisPen, 3, 2, 3, 21);

                    g.DrawLine(axisPen, 3, 21, 22, 21);

                }

                // Faixa de Desvio Padrão ±1σ (Sombreado semi-transparente)

                using (var sigmaBrush = new SolidBrush(Color.FromArgb(40, 52, 152, 219)))

                {

                    PointF[] band = new PointF[]

                    {

                        new PointF(3, 8),

                        new PointF(8, 6),

                        new PointF(14, 9),

                        new PointF(21, 7),

                        new PointF(21, 15),

                        new PointF(14, 16),

                        new PointF(8, 14),

                        new PointF(3, 15)

                    };

                    g.FillPolygon(sigmaBrush, band);

                }

                // Linha estatística de Média μ (Tracejada Laranja)

                using (var meanPen = new Pen(Color.FromArgb(243, 156, 18), 1.2f) { DashStyle = DashStyle.Dash })

                {

                    g.DrawLine(meanPen, 3, 11, 21, 11);

                }

                // Curva de Dados Principal (Ciano / Azul brilhante)

                using (var curvePen = new Pen(Color.FromArgb(46, 204, 113), 1.6f))

                {

                    PointF[] pts = new PointF[]

                    {

                        new PointF(3, 17),

                        new PointF(8, 10),

                        new PointF(14, 13),

                        new PointF(21, 5)

                    };

                    g.DrawLines(curvePen, pts);

                }

                // Pontos marcadores na curva

                using (var ptBrush = new SolidBrush(Color.FromArgb(236, 240, 241)))

                {

                    g.FillEllipse(ptBrush, 7f, 9f, 2.5f, 2.5f);

                    g.FillEllipse(ptBrush, 13f, 12f, 2.5f, 2.5f);

                    g.FillEllipse(ptBrush, 20f, 4f, 2.5f, 2.5f);

                }

                // Símbolo μ no topo

                using (var font = new Font("Arial", 5.5f, FontStyle.Bold))

                using (var textBrush = new SolidBrush(Color.FromArgb(243, 156, 18)))

                {

                    g.DrawString("μ", font, textBrush, 17f, 0.5f);

                }

            }

            return bmp;

        }

        // 33. Scatter & Bubble Plot (Dispersão com Bolinhas, Centróide e Regressão)

        private static Bitmap DrawChartScatter()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Grid suave

                using (var gridPen = new Pen(Color.FromArgb(220, 228, 238), 0.8f))

                {

                    g.DrawLine(gridPen, 3, 7, 21, 7);

                    g.DrawLine(gridPen, 3, 14, 21, 14);

                    g.DrawLine(gridPen, 9, 3, 9, 21);

                    g.DrawLine(gridPen, 16, 3, 16, 21);

                }

                // Eixos cartesianos

                using (var axisPen = new Pen(Color.FromArgb(100, 115, 135), 1.2f))

                {

                    g.DrawLine(axisPen, 3, 2, 3, 21);

                    g.DrawLine(axisPen, 3, 21, 22, 21);

                }

                // Linha de Tendência / Regressão linear (Vermelha tracejada)

                using (var trendPen = new Pen(Color.FromArgb(231, 76, 60), 1.2f) { DashStyle = DashStyle.Dash })

                {

                    g.DrawLine(trendPen, 4, 18, 20, 4);

                }

                // Bolinhas / Dispersão de pontos coloridos e variados tamanhos

                using (var b1 = new SolidBrush(Color.FromArgb(180, 52, 152, 219)))

                using (var b2 = new SolidBrush(Color.FromArgb(180, 155, 89, 182)))

                using (var b3 = new SolidBrush(Color.FromArgb(180, 46, 204, 113)))

                using (var b4 = new SolidBrush(Color.FromArgb(180, 241, 196, 15)))

                {

                    g.FillEllipse(b1, 5, 15, 4, 4);

                    g.FillEllipse(b2, 9, 11, 5, 5);

                    g.FillEllipse(b4, 14, 9, 3.5f, 3.5f);

                    g.FillEllipse(b3, 18, 4, 4.5f, 4.5f);

                    g.FillEllipse(b1, 11, 16, 3, 3);

                    g.FillEllipse(b2, 16, 13, 3.5f, 3.5f);

                }

                // Centróide (x̄, ȳ) - Cruz / Alvo Laranja

                using (var centerPen = new Pen(Color.FromArgb(230, 126, 34), 1.5f))

                {

                    g.DrawLine(centerPen, 12, 9, 12, 13);

                    g.DrawLine(centerPen, 10, 11, 14, 11);

                }

            }

            return bmp;

        }

        // 34. Spatial Grid & Viewport Heatmap (Gradiente de calor espacial e Proximidade de Alvo no Viewport)

        private static Bitmap DrawSpatialHeatmap()

        {

            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);

            using (var g = InitGfx(bmp))

            {

                // Grade de cores do Heatmap (3x3 blocos de calor)

                Color[,] palette = new Color[3, 3]

                {

                    { Color.FromArgb(41, 128, 185), Color.FromArgb(26, 188, 156), Color.FromArgb(46, 204, 113) },

                    { Color.FromArgb(26, 188, 156), Color.FromArgb(241, 196, 15), Color.FromArgb(230, 126, 34) },

                    { Color.FromArgb(46, 204, 113), Color.FromArgb(230, 126, 34), Color.FromArgb(231, 76, 60) }

                };

                for (int r = 0; r < 3; r++)

                {

                    for (int c = 0; c < 3; c++)

                    {

                        using (var brush = new SolidBrush(palette[r, c]))

                        {

                            g.FillRectangle(brush, 2 + c * 6, 2 + r * 6, 6, 6);

                        }

                    }

                }

                // Linhas da malha / grid

                using (var gridPen = new Pen(Color.FromArgb(240, 255, 255, 255), 0.8f))

                {

                    g.DrawRectangle(gridPen, 2, 2, 18, 18);

                    g.DrawLine(gridPen, 8, 2, 8, 20);

                    g.DrawLine(gridPen, 14, 2, 14, 20);

                    g.DrawLine(gridPen, 2, 8, 20, 8);

                    g.DrawLine(gridPen, 2, 14, 20, 14);

                }

                // Ponto Alvo Ideal no centro (Target Bullseye)

                using (var ringPen = new Pen(Color.FromArgb(255, 255, 255), 1.2f))

                using (var dotBrush = new SolidBrush(Color.FromArgb(231, 76, 60)))

                {

                    g.DrawEllipse(ringPen, 8.5f, 8.5f, 5f, 5f);

                    g.FillEllipse(dotBrush, 10f, 10f, 2f, 2f);

                }

                // Badge de Viewport 3D no canto inferior direito

                using (var vpBrush = new SolidBrush(Color.FromArgb(44, 62, 80)))

                {

                    g.FillRectangle(vpBrush, 14, 14, 9, 9);

                }

                using (var vpPen = new Pen(Color.FromArgb(52, 152, 219), 1f))

                {

                    g.DrawRectangle(vpPen, 14, 14, 8, 8);

                    // Olho / Câmera do Viewport

                    g.DrawEllipse(vpPen, 15.5f, 16.5f, 5f, 3f);

                }

                using (var pupilBrush = new SolidBrush(Color.FromArgb(46, 204, 113)))

                {

                    g.FillEllipse(pupilBrush, 17f, 17f, 2f, 2f);

                }

            }

            return bmp;

        }

    
        // ==========================================
        // NOVOS ÍCONES: DISTRIBUIÇÕES E PILHAS MATEMÁTICAS (FUNDO 100% TRANSPARENTE)
        // ==========================================

        private static Bitmap DrawBetaDistribution()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Sem fundo (transparente nativo)

                // Eixos cartesianos
                using (var axisPen = new Pen(Color.FromArgb(100, 120, 140), 1.2f))
                {
                    g.DrawLine(axisPen, 3, 21, 21, 21);
                    g.DrawLine(axisPen, 3, 3, 3, 21);
                }

                // Área sob a curva Beta
                var pts = new PointF[]
                {
                    new PointF(3, 21),
                    new PointF(5, 18),
                    new PointF(8, 7),
                    new PointF(11, 6),
                    new PointF(14, 10),
                    new PointF(17, 16),
                    new PointF(20, 21)
                };

                using (var fillBrush = new SolidBrush(Color.FromArgb(90, 52, 152, 219)))
                {
                    g.FillPolygon(fillBrush, pts);
                }

                using (var curvePen = new Pen(Color.FromArgb(41, 128, 185), 1.8f))
                {
                    g.DrawCurve(curvePen, pts);
                }

                // Símbolo β estilizado
                using (var font = new Font("Georgia", 8.5f, FontStyle.Bold | FontStyle.Italic))
                using (var brush = new SolidBrush(Color.FromArgb(231, 76, 60)))
                {
                    g.DrawString("β", font, brush, 13, 2);
                }
            }
            return bmp;
        }

        private static Bitmap DrawBinomialDistribution()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Sem fundo (transparente nativo)

                // Eixo horizontal
                using (var axisPen = new Pen(Color.FromArgb(100, 130, 100), 1.2f))
                {
                    g.DrawLine(axisPen, 3, 21, 21, 21);
                }

                // Barras discretas do histograma binomial
                int[] heights = new int[] { 3, 7, 13, 16, 12, 6, 2 };
                int[] xs = new int[] { 4, 6, 9, 11, 14, 16, 19 };

                using (var barBrush = new SolidBrush(Color.FromArgb(46, 204, 113)))
                using (var borderPen = new Pen(Color.FromArgb(39, 174, 96), 0.8f))
                {
                    for (int i = 0; i < heights.Length; i++)
                    {
                        int h = heights[i];
                        int x = xs[i];
                        int y = 21 - h;
                        g.FillRectangle(barBrush, x, y, 2, h);
                        g.DrawRectangle(borderPen, x, y, 2, h);
                    }
                }

                // Badge B estilizado
                using (var font = new Font("Arial", 7f, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.FromArgb(39, 174, 96)))
                {
                    g.DrawString("B", font, brush, 2, 2);
                }
            }
            return bmp;
        }

        private static Bitmap DrawChiSquareDistribution()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Sem fundo (transparente nativo)

                // Eixos cartesianos
                using (var axisPen = new Pen(Color.FromArgb(130, 110, 90), 1.2f))
                {
                    g.DrawLine(axisPen, 3, 21, 21, 21);
                    g.DrawLine(axisPen, 3, 3, 3, 21);
                }

                // Curva Qui-Quadrado (assimétrica à direita com cauda longa)
                var pts = new PointF[]
                {
                    new PointF(3, 21),
                    new PointF(5, 8),
                    new PointF(7, 6),
                    new PointF(10, 10),
                    new PointF(14, 15),
                    new PointF(18, 18),
                    new PointF(21, 20)
                };

                using (var fillBrush = new SolidBrush(Color.FromArgb(90, 230, 126, 34)))
                {
                    g.FillPolygon(fillBrush, pts);
                }

                using (var curvePen = new Pen(Color.FromArgb(211, 84, 0), 1.8f))
                {
                    g.DrawCurve(curvePen, pts);
                }

                // Símbolo χ²
                using (var font = new Font("Arial", 7f, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.FromArgb(142, 68, 173)))
                {
                    g.DrawString("χ²", font, brush, 13, 2);
                }
            }
            return bmp;
        }

        private static Bitmap DrawMassMath()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Sem fundo (transparente nativo)

                // Pilha de blocos empilhados
                using (var b1 = new SolidBrush(Color.FromArgb(52, 152, 219)))
                using (var b2 = new SolidBrush(Color.FromArgb(46, 204, 113)))
                using (var b3 = new SolidBrush(Color.FromArgb(241, 196, 15)))
                using (var borderPen = new Pen(Color.FromArgb(44, 62, 80), 1f))
                {
                    g.FillRectangle(b1, 3, 16, 17, 5);
                    g.DrawRectangle(borderPen, 3, 16, 17, 5);

                    g.FillRectangle(b2, 5, 10, 13, 5);
                    g.DrawRectangle(borderPen, 5, 10, 13, 5);

                    g.FillRectangle(b3, 7, 4, 9, 5);
                    g.DrawRectangle(borderPen, 7, 4, 9, 5);
                }

                // Símbolo matemático Σ vibrante
                using (var font = new Font("Arial", 9f, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.FromArgb(231, 76, 60)))
                {
                    g.DrawString("Σ", font, textBrush, 13, 1);
                }
            }
            return bmp;
        }

        private static Bitmap DrawSumIf()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Sem fundo (transparente nativo)

                // Símbolo Σ em azul elegante
                using (var font = new Font("Arial", 12f, FontStyle.Bold))
                using (var sumBrush = new SolidBrush(Color.FromArgb(41, 128, 185)))
                {
                    g.DrawString("Σ", font, sumBrush, 1, 3);
                }

                // Badge de filtro "IF" em círculo laranja
                using (var badgeBrush = new SolidBrush(Color.FromArgb(230, 126, 34)))
                using (var badgePen = new Pen(Color.FromArgb(211, 84, 0), 1f))
                {
                    g.FillEllipse(badgeBrush, 12, 10, 11, 11);
                    g.DrawEllipse(badgePen, 12, 10, 11, 11);
                }

                using (var font = new Font("Arial", 6.5f, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.White))
                {
                    g.DrawString("IF", font, textBrush, 13f, 11.5f);
                }
            }
            return bmp;
        }

        private static Bitmap DrawDataGroupFinder()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Sem fundo (transparente nativo)

                // Grupo 1 (Azul)
                using (var g1Pen = new Pen(Color.FromArgb(52, 152, 219), 1.2f))
                using (var g1Brush = new SolidBrush(Color.FromArgb(90, 52, 152, 219)))
                {
                    g.FillEllipse(g1Brush, 2, 2, 9, 9);
                    g.DrawEllipse(g1Pen, 2, 2, 9, 9);
                }

                // Grupo 2 (Verde)
                using (var g2Pen = new Pen(Color.FromArgb(46, 204, 113), 1.2f))
                using (var g2Brush = new SolidBrush(Color.FromArgb(90, 46, 204, 113)))
                {
                    g.FillEllipse(g2Brush, 13, 2, 9, 9);
                    g.DrawEllipse(g2Pen, 13, 2, 9, 9);
                }

                // Grupo 3 (Laranja)
                using (var g3Pen = new Pen(Color.FromArgb(230, 126, 34), 1.2f))
                using (var g3Brush = new SolidBrush(Color.FromArgb(90, 230, 126, 34)))
                {
                    g.FillEllipse(g3Brush, 7, 13, 10, 9);
                    g.DrawEllipse(g3Pen, 7, 13, 10, 9);
                }

                // Pontos centrais em cinza escuro
                using (var dotBrush = new SolidBrush(Color.FromArgb(44, 62, 80)))
                {
                    g.FillEllipse(dotBrush, 5.5f, 5.5f, 2f, 2f);
                    g.FillEllipse(dotBrush, 16.5f, 5.5f, 2f, 2f);
                    g.FillEllipse(dotBrush, 11f, 16.5f, 2f, 2f);
                }
            }
            return bmp;
        }

        private static Bitmap DrawDataStack()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Sem fundo (transparente nativo)

                // Camadas empilhadas (VSTACK / HSTACK)
                using (var b1 = new SolidBrush(Color.FromArgb(155, 89, 182)))
                using (var b2 = new SolidBrush(Color.FromArgb(52, 152, 219)))
                using (var b3 = new SolidBrush(Color.FromArgb(46, 204, 113)))
                using (var borderPen = new Pen(Color.FromArgb(44, 62, 80), 1f))
                {
                    // Bloco A
                    g.FillRectangle(b1, 3, 3, 8, 8);
                    g.DrawRectangle(borderPen, 3, 3, 8, 8);

                    // Bloco B (empilhado verticalmente)
                    g.FillRectangle(b2, 3, 13, 8, 8);
                    g.DrawRectangle(borderPen, 3, 13, 8, 8);

                    // Bloco C (empilhado horizontalmente)
                    g.FillRectangle(b3, 13, 3, 8, 18);
                    g.DrawRectangle(borderPen, 13, 3, 8, 18);
                }

                // Seta de empilhamento em branco nítido
                using (var arrowPen = new Pen(Color.White, 1.2f))
                {
                    g.DrawLine(arrowPen, 7, 5, 7, 9);
                    g.DrawLine(arrowPen, 7, 15, 7, 19);
                    g.DrawLine(arrowPen, 15, 12, 19, 12);
                }
            }
            return bmp;
        }

    
        // ==========================================
        // MÉTODOS DE DESENHO VETORIAL: PILLS
        // ==========================================

        // 1. Pill Transmitter: Pílula ciano/teal com ondas wireless se propagando
        private static Bitmap DrawPillTransmitter()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Corpo da pílula horizontal
                using (var path = new GraphicsPath())
                using (var brush = new LinearGradientBrush(new RectangleF(2, 6, 20, 12), Color.FromArgb(0, 180, 216), Color.FromArgb(0, 119, 182), LinearGradientMode.Vertical))
                using (var borderPen = new Pen(Color.FromArgb(2, 62, 138), 1.2f))
                {
                    path.AddArc(2, 6, 12, 12, 90, 180);
                    path.AddArc(10, 6, 12, 12, 270, 180);
                    path.CloseFigure();
                    g.FillPath(brush, path);
                    g.DrawPath(borderPen, path);
                }

                // Terminal / nó esquerdo
                using (var dotBrush = new SolidBrush(Color.White))
                {
                    g.FillEllipse(dotBrush, 5.5f, 10f, 4f, 4f);
                }

                // Ondas wireless radiando para a direita
                using (var wavePen1 = new Pen(Color.FromArgb(240, 255, 255), 1.5f))
                using (var wavePen2 = new Pen(Color.FromArgb(200, 230, 255), 1.3f))
                {
                    g.DrawArc(wavePen1, 10f, 9f, 6f, 6f, -60, 120);
                    g.DrawArc(wavePen2, 13f, 7.5f, 8f, 9f, -60, 120);
                }
            }
            return bmp;
        }

        // 2. Pill Receiver: Pílula verde esmeralda com ondas wireless convergindo
        private static Bitmap DrawPillReceiver()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Corpo da pílula horizontal
                using (var path = new GraphicsPath())
                using (var brush = new LinearGradientBrush(new RectangleF(2, 6, 20, 12), Color.FromArgb(46, 204, 113), Color.FromArgb(39, 174, 96), LinearGradientMode.Vertical))
                using (var borderPen = new Pen(Color.FromArgb(20, 90, 50), 1.2f))
                {
                    path.AddArc(2, 6, 12, 12, 90, 180);
                    path.AddArc(10, 6, 12, 12, 270, 180);
                    path.CloseFigure();
                    g.FillPath(brush, path);
                    g.DrawPath(borderPen, path);
                }

                // Ondas wireless convergindo da esquerda
                using (var wavePen1 = new Pen(Color.FromArgb(200, 255, 220), 1.3f))
                using (var wavePen2 = new Pen(Color.FromArgb(240, 255, 245), 1.5f))
                {
                    g.DrawArc(wavePen1, 3f, 7.5f, 8f, 9f, 120, 120);
                    g.DrawArc(wavePen2, 7f, 9f, 6f, 6f, 120, 120);
                }

                // Alvo / nó receptor à direita com ponto luminoso
                using (var dotBrush = new SolidBrush(Color.White))
                using (var dotPen = new Pen(Color.FromArgb(20, 90, 50), 1f))
                {
                    g.FillEllipse(dotBrush, 14.5f, 10f, 4f, 4f);
                    g.DrawEllipse(dotPen, 14.5f, 10f, 4f, 4f);
                }
            }
            return bmp;
        }

        // 3. Pill Bundle Pack: Hub / Pacote com múltiplos canais convergindo
        private static Bitmap DrawPillBundlePack()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Caixa / Hub principal
                using (var boxBrush = new LinearGradientBrush(new RectangleF(4, 9, 16, 12), Color.FromArgb(58, 134, 255), Color.FromArgb(26, 82, 180), LinearGradientMode.Vertical))
                using (var boxBorder = new Pen(Color.FromArgb(15, 50, 120), 1.2f))
                {
                    g.FillRectangle(boxBrush, 4, 9, 16, 12);
                    g.DrawRectangle(boxBorder, 4, 9, 16, 12);
                }

                // Cinta dourada / lacre
                using (var strapBrush = new SolidBrush(Color.FromArgb(255, 209, 102)))
                {
                    g.FillRectangle(strapBrush, 10.5f, 9, 3f, 12);
                    g.FillRectangle(strapBrush, 4, 13.5f, 16, 3f);
                }

                // Três canais de entrada coloridos no topo (Cyan, Green, Orange)
                using (var p1 = new Pen(Color.FromArgb(0, 180, 216), 1.8f))
                using (var p2 = new Pen(Color.FromArgb(46, 204, 113), 1.8f))
                using (var p3 = new Pen(Color.FromArgb(235, 130, 60), 1.8f))
                {
                    g.DrawLine(p1, 6, 2, 8, 9);
                    g.DrawLine(p2, 12, 2, 12, 9);
                    g.DrawLine(p3, 18, 2, 16, 9);
                }
            }
            return bmp;
        }

        // 4. Pill Bundle Unpack: Pacote se abrindo e distribuindo canais
        private static Bitmap DrawPillBundleUnpack()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Caixa / Hub na base
                using (var boxBrush = new LinearGradientBrush(new RectangleF(4, 11, 16, 10), Color.FromArgb(58, 134, 255), Color.FromArgb(26, 82, 180), LinearGradientMode.Vertical))
                using (var boxBorder = new Pen(Color.FromArgb(15, 50, 120), 1.2f))
                {
                    g.FillRectangle(boxBrush, 4, 11, 16, 10);
                    g.DrawRectangle(boxBorder, 4, 11, 16, 10);
                }

                // Tampa aberta
                using (var lidBrush = new SolidBrush(Color.FromArgb(80, 150, 255)))
                {
                    g.FillPolygon(lidBrush, new PointF[] { new PointF(2, 9), new PointF(22, 9), new PointF(20, 11), new PointF(4, 11) });
                }

                // Três feixes saindo para o topo (Cyan, Green, Orange)
                using (var p1 = new Pen(Color.FromArgb(0, 180, 216), 1.8f))
                using (var p2 = new Pen(Color.FromArgb(46, 204, 113), 1.8f))
                using (var p3 = new Pen(Color.FromArgb(235, 130, 60), 1.8f))
                {
                    g.DrawLine(p1, 8, 9, 5, 2);
                    g.DrawLine(p2, 12, 9, 12, 2);
                    g.DrawLine(p3, 16, 9, 19, 2);
                }
            }
            return bmp;
        }

        // 5. Pill Catalog: Prancheta / Inspector de auditoria com status
        private static Bitmap DrawPillCatalog()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Prancheta
                using (var boardBrush = new SolidBrush(Color.FromArgb(240, 243, 246)))
                using (var boardBorder = new Pen(Color.FromArgb(70, 80, 95), 1.2f))
                {
                    g.FillRectangle(boardBrush, 3, 3, 18, 18);
                    g.DrawRectangle(boardBorder, 3, 3, 18, 18);
                }

                // Clipe superior
                using (var clipBrush = new SolidBrush(Color.FromArgb(108, 117, 125)))
                {
                    g.FillRectangle(clipBrush, 8, 1, 8, 4);
                }

                // Linhas de canais
                using (var linePen = new Pen(Color.FromArgb(170, 185, 200), 1f))
                {
                    g.DrawLine(linePen, 10, 8, 18, 8);
                    g.DrawLine(linePen, 10, 12, 18, 12);
                    g.DrawLine(linePen, 10, 16, 18, 16);
                }

                // Três pontinhos luminosos de status (Verde = Conectado, Laranja = Aviso, Vermelho = Órfão)
                using (var bGreen = new SolidBrush(Color.FromArgb(46, 204, 113)))
                using (var bAmber = new SolidBrush(Color.FromArgb(243, 156, 18)))
                using (var bRed = new SolidBrush(Color.FromArgb(231, 76, 60)))
                {
                    g.FillEllipse(bGreen, 5.5f, 6.5f, 3f, 3f);
                    g.FillEllipse(bAmber, 5.5f, 10.5f, 3f, 3f);
                    g.FillEllipse(bRed, 5.5f, 14.5f, 3f, 3f);
                }
            }
            return bmp;
        }

        // 6. Pill Cache: Microchip violeta de alta velocidade com relâmpago dourado
        private static Bitmap DrawPillCache()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Pinos metálicos do chip
                using (var pinPen = new Pen(Color.FromArgb(255, 209, 102), 1.2f))
                {
                    // Top e Bottom
                    g.DrawLine(pinPen, 7, 2, 7, 5);
                    g.DrawLine(pinPen, 12, 2, 12, 5);
                    g.DrawLine(pinPen, 17, 2, 17, 5);
                    g.DrawLine(pinPen, 7, 19, 7, 22);
                    g.DrawLine(pinPen, 12, 19, 12, 22);
                    g.DrawLine(pinPen, 17, 19, 17, 22);

                    // Left e Right
                    g.DrawLine(pinPen, 2, 7, 5, 7);
                    g.DrawLine(pinPen, 2, 12, 5, 12);
                    g.DrawLine(pinPen, 2, 17, 5, 17);
                    g.DrawLine(pinPen, 19, 7, 22, 7);
                    g.DrawLine(pinPen, 19, 12, 22, 12);
                    g.DrawLine(pinPen, 19, 17, 22, 17);
                }

                // Corpo do chip em violeta elétrico
                using (var chipBrush = new LinearGradientBrush(new RectangleF(5, 5, 14, 14), Color.FromArgb(157, 78, 221), Color.FromArgb(90, 24, 154), LinearGradientMode.ForwardDiagonal))
                using (var chipBorder = new Pen(Color.FromArgb(60, 9, 108), 1.2f))
                {
                    g.FillRectangle(chipBrush, 5, 5, 14, 14);
                    g.DrawRectangle(chipBorder, 5, 5, 14, 14);
                }

                // Relâmpago / Flash central
                PointF[] bolt = new PointF[]
                {
                    new PointF(13, 6.5f),
                    new PointF(9, 12f),
                    new PointF(12, 12f),
                    new PointF(11, 17.5f),
                    new PointF(15, 11.5f),
                    new PointF(12.5f, 11.5f)
                };
                using (var boltBrush = new SolidBrush(Color.FromArgb(255, 222, 89)))
                {
                    g.FillPolygon(boltBrush, bolt);
                }
            }
            return bmp;
        }

        // 7. Pill Preset Manager: Controles deslizantes / Faders com bookmark
        private static Bitmap DrawPillPresetManager()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Fundo de painel escuro
                using (var bgBrush = new SolidBrush(Color.FromArgb(33, 37, 41)))
                using (var borderPen = new Pen(Color.FromArgb(73, 80, 87), 1.2f))
                {
                    g.FillRectangle(bgBrush, 2, 2, 20, 20);
                    g.DrawRectangle(borderPen, 2, 2, 20, 20);
                }

                // Trilhos horizontais dos sliders
                using (var trackPen = new Pen(Color.FromArgb(108, 117, 125), 1.5f))
                {
                    g.DrawLine(trackPen, 5, 7, 19, 7);
                    g.DrawLine(trackPen, 5, 12, 19, 12);
                    g.DrawLine(trackPen, 5, 17, 19, 17);
                }

                // Knobs circulares em posições variadas
                using (var knobBrush1 = new SolidBrush(Color.FromArgb(0, 180, 216)))
                using (var knobBrush2 = new SolidBrush(Color.FromArgb(235, 130, 60)))
                using (var knobBrush3 = new SolidBrush(Color.FromArgb(46, 204, 113)))
                using (var knobPen = new Pen(Color.White, 0.8f))
                {
                    g.FillEllipse(knobBrush1, 7, 5, 4, 4);
                    g.DrawEllipse(knobPen, 7, 5, 4, 4);

                    g.FillEllipse(knobBrush2, 14, 10, 4, 4);
                    g.DrawEllipse(knobPen, 14, 10, 4, 4);

                    g.FillEllipse(knobBrush3, 10, 15, 4, 4);
                    g.DrawEllipse(knobPen, 10, 15, 4, 4);
                }
            }
            return bmp;
        }

        // 8. Pill Constraint Checker: Alvo de conformidade com checkmark verde
        private static Bitmap DrawPillConstraintChecker()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Escudo / Moldura em azul marinho
                using (var shieldBrush = new LinearGradientBrush(new RectangleF(3, 2, 18, 20), Color.FromArgb(26, 38, 57), Color.FromArgb(15, 23, 42), LinearGradientMode.Vertical))
                using (var shieldBorder = new Pen(Color.FromArgb(56, 189, 248), 1.2f))
                {
                    g.FillRectangle(shieldBrush, 3, 2, 18, 20);
                    g.DrawRectangle(shieldBorder, 3, 2, 18, 20);
                }

                // Linha pontilhada superior (Max Limit) e inferior (Min Limit)
                using (var limitPen = new Pen(Color.FromArgb(248, 113, 113), 1f) { DashStyle = DashStyle.Dot })
                {
                    g.DrawLine(limitPen, 5, 6, 19, 6);
                    g.DrawLine(limitPen, 5, 18, 19, 18);
                }

                // Checkmark verde brilhante de conformidade no centro
                using (var checkPen = new Pen(Color.FromArgb(34, 197, 94), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(checkPen, 6.5f, 12f, 10f, 15.5f);
                    g.DrawLine(checkPen, 10f, 15.5f, 17.5f, 8.5f);
                }
            }
            return bmp;
        }

        // 9. Pill Change Detector: Sensor de pulso e variação com símbolo Delta dourado e onda de sinal ciano
        private static Bitmap DrawPillChangeDetector()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Fundo circular em azul escuro técnico
                using (var bgBrush = new LinearGradientBrush(new RectangleF(2, 2, 20, 20), Color.FromArgb(30, 41, 59), Color.FromArgb(15, 23, 42), LinearGradientMode.Vertical))
                using (var bgPen = new Pen(Color.FromArgb(51, 65, 85), 1.2f))
                {
                    g.FillEllipse(bgBrush, 2, 2, 20, 20);
                    g.DrawEllipse(bgPen, 2, 2, 20, 20);
                }

                // Símbolo Delta (Δ) superior esquerdo em âmbar/dourado
                using (var deltaPen = new Pen(Color.FromArgb(245, 158, 11), 1.8f) { LineJoin = LineJoin.Round })
                {
                    PointF[] deltaPts = new PointF[]
                    {
                        new PointF(7.5f, 5.5f),
                        new PointF(12f, 13.5f),
                        new PointF(3f, 13.5f),
                        new PointF(7.5f, 5.5f)
                    };
                    g.DrawPolygon(deltaPen, deltaPts);
                }

                // Pulso elétrico / Eletrocardiograma / Onda de disparo em Ciano brilhante
                using (var pulsePen = new Pen(Color.FromArgb(6, 182, 212), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    PointF[] pulsePts = new PointF[]
                    {
                        new PointF(5f, 17.5f),
                        new PointF(10f, 17.5f),
                        new PointF(13f, 11f),
                        new PointF(15.5f, 20.5f),
                        new PointF(18f, 16.5f),
                        new PointF(21f, 16.5f)
                    };
                    g.DrawLines(pulsePen, pulsePts);
                }

                // Ponto de disparo luminoso (led ativo) no pico
                using (var dotBrush = new SolidBrush(Color.FromArgb(255, 255, 255)))
                {
                    g.FillEllipse(dotBrush, 12f, 10f, 2.2f, 2.2f);
                }
            }
            return bmp;
        }

        // 10. Pill Vertical Relay: Cápsula vertical com cabo entrando pelo topo e saindo pela base
        private static Bitmap DrawPillRelay()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Cabo superior entrando (Ciano)
                using (var wirePen = new Pen(Color.FromArgb(6, 182, 212), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(wirePen, 12f, 1.5f, 12f, 5f);
                    g.DrawLine(wirePen, 12f, 19f, 12f, 22.5f);
                }

                // Corpo da cápsula vertical arredondada (Dark Glass)
                RectangleF capsuleRect = new RectangleF(7f, 4.5f, 10f, 15f);
                using (var path = new GraphicsPath())
                {
                    path.AddArc(capsuleRect.X, capsuleRect.Y, capsuleRect.Width, capsuleRect.Width, 180, 180);
                    path.AddArc(capsuleRect.X, capsuleRect.Bottom - capsuleRect.Width, capsuleRect.Width, capsuleRect.Width, 0, 180);
                    path.CloseFigure();

                    using (var bgBrush = new LinearGradientBrush(capsuleRect, Color.FromArgb(40, 50, 68), Color.FromArgb(18, 24, 38), LinearGradientMode.Vertical))
                    {
                        g.FillPath(bgBrush, path);
                    }
                    using (var borderPen = new Pen(Color.FromArgb(0, 210, 255), 1.2f))
                    {
                        g.DrawPath(borderPen, path);
                    }
                }

                // Seta apontando para baixo (↓) em branco puro
                using (var arrowPen = new Pen(Color.White, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    g.DrawLine(arrowPen, 12f, 8.5f, 12f, 15f);
                    g.DrawLine(arrowPen, 9.5f, 12.5f, 12f, 15f);
                    g.DrawLine(arrowPen, 14.5f, 12.5f, 12f, 15f);
                }

                // Terminais / Grips nos polos (superior e inferior)
                using (var gripBrush = new SolidBrush(Color.FromArgb(0, 240, 255)))
                {
                    g.FillEllipse(gripBrush, 10.5f, 4f, 3f, 3f);
                    g.FillEllipse(gripBrush, 10.5f, 17f, 3f, 3f);
                }
            }
            return bmp;
        }

        // 11. Pill Tag Hook: Tag em formato de seta (estilo Cluster Input) com badge de grupo e saída
        private static Bitmap DrawPillHook()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Tag em formato de seta apontando para a direita (estilo Cluster Input)
                PointF[] tagPts = new PointF[]
                {
                    new PointF(2f, 5.5f),
                    new PointF(15f, 5.5f),
                    new PointF(21f, 12f),
                    new PointF(15f, 18.5f),
                    new PointF(2f, 18.5f)
                };

                using (var bgBrush = new LinearGradientBrush(new RectangleF(2, 5, 19, 14), Color.FromArgb(245, 158, 11), Color.FromArgb(217, 119, 6), LinearGradientMode.Vertical))
                using (var borderPen = new Pen(Color.FromArgb(180, 83, 9), 1.2f))
                {
                    g.FillPolygon(bgBrush, tagPts);
                    g.DrawPolygon(borderPen, tagPts);
                }

                // Miolo interno claro
                PointF[] innerPts = new PointF[]
                {
                    new PointF(3.8f, 7.2f),
                    new PointF(13.8f, 7.2f),
                    new PointF(18f, 12f),
                    new PointF(13.8f, 16.8f),
                    new PointF(3.8f, 16.8f)
                };
                using (var innerBrush = new SolidBrush(Color.FromArgb(254, 243, 199)))
                using (var innerPen = new Pen(Color.FromArgb(251, 191, 36), 0.8f))
                {
                    g.FillPolygon(innerBrush, innerPts);
                    g.DrawPolygon(innerPen, innerPts);
                }

                // LED indicador verde (●) no interior
                using (var ledBrush = new SolidBrush(Color.FromArgb(34, 197, 94)))
                using (var ledPen = new Pen(Color.White, 0.6f))
                {
                    g.FillEllipse(ledBrush, 5.5f, 10.2f, 3.6f, 3.6f);
                    g.DrawEllipse(ledPen, 5.5f, 10.2f, 3.6f, 3.6f);
                }

                // Terminal / Grip de saída na ponta da seta direita
                using (var gripBrush = new SolidBrush(Color.White))
                using (var gripPen = new Pen(Color.FromArgb(30, 41, 59), 1.2f))
                {
                    g.FillEllipse(gripBrush, 19.5f, 10.2f, 3.6f, 3.6f);
                    g.DrawEllipse(gripPen, 19.5f, 10.2f, 3.6f, 3.6f);
                }
            }
            return bmp;
        }

        // 12. Pill Slider Pool: Conjunto vertical de sliders com parâmetros e cores
        private static Bitmap DrawPillSliderPool()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Moldura externa da piscina de sliders
                using (var frameBrush = new SolidBrush(Color.FromArgb(248, 250, 252)))
                using (var framePen = new Pen(Color.FromArgb(139, 92, 246), 1.2f))
                {
                    g.FillRectangle(frameBrush, 2, 3, 20, 18);
                    g.DrawRectangle(framePen, 2, 3, 20, 18);
                }

                // Trilho 1 (Ciano - ACU)
                using (var t1Pen = new Pen(Color.FromArgb(226, 232, 240), 2f))
                using (var f1Pen = new Pen(Color.FromArgb(6, 182, 212), 2f))
                using (var g1Brush = new SolidBrush(Color.FromArgb(8, 145, 178)))
                {
                    g.DrawLine(t1Pen, 4, 7, 18, 7);
                    g.DrawLine(f1Pen, 4, 7, 12, 7);
                    g.FillEllipse(g1Brush, 10.5f, 5.5f, 3.2f, 3.2f);
                }

                // Trilho 2 (Verde Esmeralda - GEO)
                using (var t2Pen = new Pen(Color.FromArgb(226, 232, 240), 2f))
                using (var f2Pen = new Pen(Color.FromArgb(34, 197, 94), 2f))
                using (var g2Brush = new SolidBrush(Color.FromArgb(22, 163, 74)))
                {
                    g.DrawLine(t2Pen, 4, 12, 18, 12);
                    g.DrawLine(f2Pen, 4, 12, 15, 12);
                    g.FillEllipse(g2Brush, 13.5f, 10.5f, 3.2f, 3.2f);
                }

                // Trilho 3 (Âmbar - MAT)
                using (var t3Pen = new Pen(Color.FromArgb(226, 232, 240), 2f))
                using (var f3Pen = new Pen(Color.FromArgb(245, 158, 11), 2f))
                using (var g3Brush = new SolidBrush(Color.FromArgb(217, 119, 6)))
                {
                    g.DrawLine(t3Pen, 4, 17, 18, 17);
                    g.DrawLine(f3Pen, 4, 17, 8, 17);
                    g.FillEllipse(g3Brush, 6.5f, 15.5f, 3.2f, 3.2f);
                }

                // Terminal / Grip de saída na borda direita
                using (var gripBrush = new SolidBrush(Color.White))
                using (var gripPen = new Pen(Color.FromArgb(139, 92, 246), 1.2f))
                {
                    g.FillEllipse(gripBrush, 20f, 10.5f, 3.2f, 3.2f);
                    g.DrawEllipse(gripPen, 20f, 10.5f, 3.2f, 3.2f);
                }
            }
            return bmp;
        }

        private static Bitmap DrawPillDisabler()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Disjuntor / Chave de corte de circuito em vermelho coral
                using (var penBox = new Pen(Color.FromArgb(231, 76, 60), 1.6f))
                using (var fillBox = new SolidBrush(Color.FromArgb(40, 231, 76, 60)))
                {
                    g.FillRectangle(fillBox, 2f, 3f, 20f, 18f);
                    g.DrawRectangle(penBox, 2f, 3f, 20f, 18f);
                }

                // Chave aberta (fio cortado / switch aberto)
                using (var penLine = new Pen(Color.FromArgb(255, 200, 200), 1.8f))
                {
                    g.DrawLine(penLine, 5f, 12f, 9f, 12f);
                    g.DrawLine(penLine, 15f, 12f, 19f, 12f);
                    g.DrawLine(penLine, 9f, 12f, 14f, 6f);
                }

                // Símbolo de Cadeado / Alerta de trava
                using (var lockPen = new Pen(Color.FromArgb(241, 196, 15), 1.5f))
                {
                    g.DrawArc(lockPen, 9f, 13f, 6f, 5f, 180f, 180f);
                }
                using (var lockBody = new SolidBrush(Color.FromArgb(241, 196, 15)))
                {
                    g.FillRectangle(lockBody, 8.5f, 16f, 7f, 4f);
                }
            }
            return bmp;
        }

        private static Bitmap DrawPillPresetVault()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Cofre / Arquivo de variantes em roxo/magenta estilo Galapagos
                RectangleF vaultRect = new RectangleF(3f, 3f, 18f, 18f);
                using (var fillVault = new SolidBrush(Color.FromArgb(70, 155, 89, 182)))
                using (var penVault = new Pen(Color.FromArgb(155, 89, 182), 1.6f))
                {
                    g.FillRectangle(fillVault, vaultRect);
                    g.DrawRectangle(penVault, vaultRect.X, vaultRect.Y, vaultRect.Width, vaultRect.Height);
                }

                // Gavetas / Slots de Variantes (3 linhas horizontais)
                using (var penSlots = new Pen(Color.FromArgb(220, 255, 255, 255), 1.2f))
                {
                    g.DrawLine(penSlots, 5f, 8f, 19f, 8f);
                    g.DrawLine(penSlots, 5f, 13f, 19f, 13f);
                    g.DrawLine(penSlots, 5f, 18f, 19f, 18f);
                }

                // Puxadores em ouro
                using (var knobBrush = new SolidBrush(Color.FromArgb(241, 196, 15)))
                {
                    g.FillRectangle(knobBrush, 10f, 6.5f, 4f, 1.5f);
                    g.FillRectangle(knobBrush, 10f, 11.5f, 4f, 1.5f);
                    g.FillRectangle(knobBrush, 10f, 16.5f, 4f, 1.5f);
                }

                // Badge de estrela de snapshot
                using (var starPen = new Pen(Color.FromArgb(46, 204, 113), 1.5f))
                {
                    g.DrawLine(starPen, 18f, 2f, 21f, 5f);
                    g.DrawLine(starPen, 21f, 2f, 18f, 5f);
                }
            }
            return bmp;
        }

        private static Bitmap DrawPillPulseTimer()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Círculo base do cronômetro / metrônomo em fundo escuro translúcido com borda cyan
                RectangleF dialRect = new RectangleF(3.5f, 4.5f, 17f, 17f);
                using (var dialBrush = new SolidBrush(Color.FromArgb(40, 14, 165, 233)))
                using (var dialPen = new Pen(Color.FromArgb(14, 165, 233), 1.5f))
                {
                    g.FillEllipse(dialBrush, dialRect);
                    g.DrawEllipse(dialPen, dialRect);
                }

                // Botão superior do cronômetro em âmbar/laranja
                using (var crownPen = new Pen(Color.FromArgb(245, 158, 11), 1.8f))
                {
                    g.DrawLine(crownPen, 10f, 2.5f, 14f, 2.5f);
                    g.DrawLine(crownPen, 12f, 2.5f, 12f, 4.5f);
                }

                // Forma de onda de pulso digital quadrado: _/\_ em esmeralda neon / cyan
                PointF[] pulseWave = new PointF[]
                {
                    new PointF(5f, 14f),
                    new PointF(8.5f, 14f),
                    new PointF(8.5f, 8.5f),
                    new PointF(15.5f, 8.5f),
                    new PointF(15.5f, 14f),
                    new PointF(19f, 14f)
                };

                using (var wavePen = new Pen(Color.FromArgb(52, 211, 153), 1.7f) { LineJoin = LineJoin.Round })
                {
                    g.DrawLines(wavePen, pulseWave);
                }

                // Flash / Centelha no topo do pulso (indicador de impulso True)
                using (var sparkBrush = new SolidBrush(Color.FromArgb(254, 240, 138)))
                {
                    g.FillEllipse(sparkBrush, 10.5f, 7f, 3f, 3f);
                }
            }
            return bmp;
        }

        private static Bitmap DrawPillLayerPipeline()

        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                Color greenBase = Color.FromArgb(46, 175, 100);

                // Camada 3 (base)
                PointF[] layer3 = new PointF[] { new PointF(12f, 16f), new PointF(21f, 19.5f), new PointF(12f, 23f), new PointF(3f, 19.5f) };
                using (var brush = new SolidBrush(Color.FromArgb(120, greenBase)))
                using (var pen = new Pen(greenBase, 1.2f))
                {
                    g.FillPolygon(brush, layer3);
                    g.DrawPolygon(pen, layer3);
                }

                // Camada 2 (meio)
                PointF[] layer2 = new PointF[] { new PointF(12f, 11f), new PointF(21f, 14.5f), new PointF(12f, 18f), new PointF(3f, 14.5f) };
                using (var brush = new SolidBrush(Color.FromArgb(180, greenBase)))
                using (var pen = new Pen(Color.FromArgb(220, greenBase), 1.2f))
                {
                    g.FillPolygon(brush, layer2);
                    g.DrawPolygon(pen, layer2);
                }

                // Camada 1 (topo)
                PointF[] layer1 = new PointF[] { new PointF(12f, 6f), new PointF(21f, 9.5f), new PointF(12f, 13f), new PointF(3f, 9.5f) };
                using (var brush = new SolidBrush(Color.FromArgb(240, greenBase)))
                using (var pen = new Pen(Color.White, 1.2f))
                {
                    g.FillPolygon(brush, layer1);
                    g.DrawPolygon(pen, layer1);
                }

                // Indicador de pipeline/ordenação
                using (var pD = new Pen(Color.White, 1.5f))
                {
                    g.DrawLine(pD, 12f, 1f, 12f, 7f);
                    g.DrawLine(pD, 10f, 4f, 12f, 7f);
                    g.DrawLine(pD, 14f, 4f, 12f, 7f);
                }
            }
            return bmp;
        }

        private static Bitmap DrawPillTreePivot()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Fundo arredondado suave
                using (var bgBrush = new SolidBrush(Color.FromArgb(235, 248, 240)))
                using (var borderPen = new Pen(Color.FromArgb(46, 175, 100), 1.2f))
                {
                    g.FillRectangle(bgBrush, 1, 1, 21, 21);
                    g.DrawRectangle(borderPen, 1, 1, 21, 21);
                }

                // Árvore esquemática à esquerda
                using (var treePen = new Pen(Color.FromArgb(30, 130, 70), 1.4f))
                {
                    g.DrawLine(treePen, 3f, 12f, 7f, 12f);
                    g.DrawLine(treePen, 7f, 6f, 7f, 18f);
                    g.DrawLine(treePen, 7f, 6f, 10f, 6f);
                    g.DrawLine(treePen, 7f, 18f, 10f, 18f);
                }

                // Seta de transformação (Pivot)
                using (var arrowPen = new Pen(Color.FromArgb(243, 156, 18), 1.5f))
                {
                    g.DrawLine(arrowPen, 10f, 12f, 13f, 12f);
                    g.DrawLine(arrowPen, 12f, 10f, 13.5f, 12f);
                    g.DrawLine(arrowPen, 12f, 14f, 13.5f, 12f);
                }

                // Grade / Tabela à direita (Wide Columns)
                using (var tablePen = new Pen(Color.FromArgb(46, 175, 100), 1.0f))
                using (var hBrush = new SolidBrush(Color.FromArgb(46, 175, 100)))
                {
                    g.FillRectangle(hBrush, 14, 4, 8, 4);
                    g.DrawRectangle(tablePen, 14, 4, 8, 15);
                    g.DrawLine(tablePen, 18, 4, 18, 19);
                    g.DrawLine(tablePen, 14, 11, 22, 11);
                    g.DrawLine(tablePen, 14, 15, 22, 15);
                }
            }
            return bmp;
        }

        private static Bitmap DrawPillTreeTableExport()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Pasta de trabalho com abas no topo
                using (var tabRankBrush = new SolidBrush(Color.FromArgb(243, 156, 18))) // Laranja (Ranking)
                using (var tabGenBrush = new SolidBrush(Color.FromArgb(46, 175, 100)))  // Verde (Gerações)
                {
                    g.FillRectangle(tabRankBrush, 2, 2, 6, 3);
                    g.FillRectangle(tabGenBrush, 9, 2, 7, 3);
                }

                // Corpo da planilha
                using (var sheetBrush = new SolidBrush(Color.White))
                using (var borderPen = new Pen(Color.FromArgb(40, 140, 80), 1.2f))
                {
                    g.FillRectangle(sheetBrush, 2, 5, 20, 16);
                    g.DrawRectangle(borderPen, 2, 5, 20, 16);
                }

                // Cabeçalho da planilha verde
                using (var headerBrush = new SolidBrush(Color.FromArgb(46, 175, 100)))
                {
                    g.FillRectangle(headerBrush, 3, 6, 18, 4);
                }

                // Linhas da grade
                using (var gridPen = new Pen(Color.FromArgb(200, 225, 210), 1.0f))
                {
                    g.DrawLine(gridPen, 3, 13, 21, 13);
                    g.DrawLine(gridPen, 3, 16, 21, 16);
                    g.DrawLine(gridPen, 8, 10, 8, 20);
                    g.DrawLine(gridPen, 14, 10, 14, 20);
                }

                // Seta de streaming de saída (disco / exportação rápida)
                using (var outBrush = new SolidBrush(Color.FromArgb(231, 76, 60)))
                using (var outPen = new Pen(Color.White, 1.0f))
                {
                    PointF[] arrow = new PointF[]
                    {
                        new PointF(16f, 14f),
                        new PointF(23f, 17.5f),
                        new PointF(16f, 21f)
                    };
                    g.FillPolygon(outBrush, arrow);
                    g.DrawPolygon(outPen, arrow);
                }
            }
            return bmp;
        }

        private static Bitmap DrawPillNumberRounder()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Fundo circular em azul escuro técnico
                using (var bgBrush = new LinearGradientBrush(new RectangleF(2, 2, 20, 20), Color.FromArgb(14, 116, 144), Color.FromArgb(8, 51, 68), LinearGradientMode.Vertical))
                using (var bgPen = new Pen(Color.FromArgb(6, 182, 212), 1.2f))
                {
                    g.FillEllipse(bgBrush, 2, 2, 20, 20);
                    g.DrawEllipse(bgPen, 2, 2, 20, 20);
                }

                // Símbolo de aproximação / arredondamento "≈" em branco nítido
                using (var pen = new Pen(Color.White, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawBezier(pen, 5f, 9.5f, 7f, 7.5f, 9f, 11.5f, 11f, 9.5f);
                    g.DrawBezier(pen, 5f, 13.5f, 7f, 11.5f, 9f, 15.5f, 11f, 13.5f);
                }

                // Ponto decimal em amarelo âmbar
                using (var dotBrush = new SolidBrush(Color.FromArgb(250, 204, 21)))
                {
                    g.FillEllipse(dotBrush, 13f, 13f, 2.2f, 2.2f);
                }

                // Zero arredondado em ciano suave
                using (var penZero = new Pen(Color.FromArgb(224, 242, 254), 1.4f))
                {
                    g.DrawEllipse(penZero, 16f, 8.5f, 4.5f, 7f);
                }
            }
            return bmp;
        }

        private static Bitmap DrawPillDomainFilter()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Fundo circular em azul cerúleo / turquesa técnico NUM
                using (var bgBrush = new LinearGradientBrush(new RectangleF(2, 2, 20, 20), Color.FromArgb(14, 116, 144), Color.FromArgb(15, 23, 42), LinearGradientMode.Vertical))
                using (var bgPen = new Pen(Color.FromArgb(56, 189, 248), 1.2f))
                {
                    g.FillEllipse(bgBrush, 2, 2, 20, 20);
                    g.DrawEllipse(bgPen, 2, 2, 20, 20);
                }

                // Faixa central destacada do domínio ativo [Min..Max]
                using (var domainBrush = new SolidBrush(Color.FromArgb(45, 56, 189, 248)))
                {
                    g.FillRectangle(domainBrush, 7.5f, 6f, 9f, 12f);
                }

                // Eixo horizontal da reta real
                using (var axisPen = new Pen(Color.FromArgb(148, 163, 184), 1.1f))
                {
                    g.DrawLine(axisPen, 4.5f, 12f, 19.5f, 12f);
                }

                // Colchete de abertura "[" (Min) em ciano vívido
                using (var bracketPen = new Pen(Color.FromArgb(56, 189, 248), 1.6f))
                {
                    g.DrawLine(bracketPen, 9f, 7f, 7.5f, 7f);
                    g.DrawLine(bracketPen, 7.5f, 7f, 7.5f, 17f);
                    g.DrawLine(bracketPen, 7.5f, 17f, 9f, 17f);
                }

                // Colchete de fechamento "]" (Max) em ciano vívido
                using (var bracketPen = new Pen(Color.FromArgb(56, 189, 248), 1.6f))
                {
                    g.DrawLine(bracketPen, 15f, 7f, 16.5f, 7f);
                    g.DrawLine(bracketPen, 16.5f, 7f, 16.5f, 17f);
                    g.DrawLine(bracketPen, 16.5f, 17f, 15f, 17f);
                }

                // Ponto fora à esquerda (descartado - cinza opaco)
                using (var outBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
                {
                    g.FillEllipse(outBrush, 4.5f, 10.5f, 2.5f, 2.5f);
                }

                // Ponto DENTRO do domínio (aprovado - verde esmeralda brilhante)
                using (var inBrush = new SolidBrush(Color.FromArgb(34, 197, 94)))
                using (var inPen = new Pen(Color.White, 0.8f))
                {
                    g.FillEllipse(inBrush, 10.5f, 10.2f, 3.2f, 3.2f);
                    g.DrawEllipse(inPen, 10.5f, 10.2f, 3.2f, 3.2f);
                }

                // Ponto fora à direita (descartado - cinza opaco)
                using (var outBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
                {
                    g.FillEllipse(outBrush, 17.2f, 10.5f, 2.5f, 2.5f);
                }
            }
            return bmp;
        }

        private static Bitmap DrawPillInterpolator()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Fundo circular em azul cerúleo / turquesa técnico NUM
                using (var bgBrush = new LinearGradientBrush(new RectangleF(2, 2, 20, 20), Color.FromArgb(14, 116, 144), Color.FromArgb(15, 23, 42), LinearGradientMode.Vertical))
                using (var bgPen = new Pen(Color.FromArgb(56, 189, 248), 1.2f))
                {
                    g.FillEllipse(bgBrush, 2, 2, 20, 20);
                    g.DrawEllipse(bgPen, 2, 2, 20, 20);
                }

                // Linha pontilhada sutil de lacuna / grade
                using (var dashPen = new Pen(Color.FromArgb(100, 148, 163, 184), 1f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(dashPen, 12f, 5f, 12f, 19f);
                }

                // Curva de interpolação contínua (S-curve suave conectando os nós)
                using (var curvePen = new Pen(Color.FromArgb(224, 242, 254), 1.5f))
                {
                    g.DrawBezier(curvePen, 5.5f, 16f, 9f, 16f, 15f, 7.5f, 18.5f, 7.5f);
                }

                // Ponto conhecido 1 (Início da lista - amarelo âmbar)
                using (var ptBrush = new SolidBrush(Color.FromArgb(250, 204, 21)))
                using (var ptPen = new Pen(Color.FromArgb(15, 23, 42), 0.8f))
                {
                    g.FillEllipse(ptBrush, 4f, 14.5f, 3.2f, 3.2f);
                    g.DrawEllipse(ptPen, 4f, 14.5f, 3.2f, 3.2f);
                }

                // Ponto interpolado / lacuna preenchida (Centro - verde esmeralda com anel branco)
                using (var interpBrush = new SolidBrush(Color.FromArgb(34, 197, 94)))
                using (var ringPen = new Pen(Color.White, 0.9f))
                {
                    g.FillEllipse(interpBrush, 10.4f, 10.2f, 3.6f, 3.6f);
                    g.DrawEllipse(ringPen, 10.4f, 10.2f, 3.6f, 3.6f);
                }

                // Ponto conhecido 2 (Fim da lista - amarelo âmbar)
                using (var ptBrush = new SolidBrush(Color.FromArgb(250, 204, 21)))
                using (var ptPen = new Pen(Color.FromArgb(15, 23, 42), 0.8f))
                {
                    g.FillEllipse(ptBrush, 17f, 6f, 3.2f, 3.2f);
                    g.DrawEllipse(ptPen, 17f, 6f, 3.2f, 3.2f);
                }
            }
            return bmp;
        }

        private static Bitmap DrawMatrixConstruct()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Colchetes de matriz [ ] (fundo transparente)
                using (var pen = new Pen(Color.FromArgb(41, 128, 185), 1.3f))
                {
                    g.DrawLine(pen, 3f, 3f, 6f, 3f);
                    g.DrawLine(pen, 3f, 3f, 3f, 20f);
                    g.DrawLine(pen, 3f, 20f, 6f, 20f);

                    g.DrawLine(pen, 20f, 3f, 17f, 3f);
                    g.DrawLine(pen, 20f, 3f, 20f, 20f);
                    g.DrawLine(pen, 20f, 20f, 17f, 20f);
                }

                // Grid interno de pontos da matriz
                using (var dotBrush = new SolidBrush(Color.FromArgb(41, 128, 185)))
                {
                    g.FillEllipse(dotBrush, 7, 7, 2.5f, 2.5f);
                    g.FillEllipse(dotBrush, 11, 7, 2.5f, 2.5f);
                    g.FillEllipse(dotBrush, 15, 7, 2.5f, 2.5f);

                    g.FillEllipse(dotBrush, 7, 11, 2.5f, 2.5f);
                    g.FillEllipse(dotBrush, 11, 11, 2.5f, 2.5f);
                    g.FillEllipse(dotBrush, 15, 11, 2.5f, 2.5f);

                    g.FillEllipse(dotBrush, 7, 15, 2.5f, 2.5f);
                    g.FillEllipse(dotBrush, 11, 15, 2.5f, 2.5f);
                    g.FillEllipse(dotBrush, 15, 15, 2.5f, 2.5f);
                }
            }
            return bmp;
        }

        private static Bitmap DrawMatrixMultiply()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                using (var pen = new Pen(Color.FromArgb(46, 175, 100), 1.2f))
                {
                    // Colchete esquerdo
                    g.DrawLine(pen, 2f, 4f, 4f, 4f);
                    g.DrawLine(pen, 2f, 4f, 2f, 19f);
                    g.DrawLine(pen, 2f, 19f, 4f, 19f);

                    // Colchete direito
                    g.DrawLine(pen, 21f, 4f, 19f, 4f);
                    g.DrawLine(pen, 21f, 4f, 21f, 19f);
                    g.DrawLine(pen, 21f, 19f, 19f, 19f);
                }

                // Letra A
                using (var font = new Font("Arial", 8f, FontStyle.Bold))
                using (var brushA = new SolidBrush(Color.FromArgb(41, 128, 185)))
                using (var brushX = new SolidBrush(Color.FromArgb(243, 156, 18)))
                using (var brushB = new SolidBrush(Color.FromArgb(46, 175, 100)))
                {
                    g.DrawString("A", font, brushA, 4f, 6f);
                    g.DrawString("×", font, brushX, 10f, 6f);
                    g.DrawString("B", font, brushB, 15f, 6f);
                }
            }
            return bmp;
        }

        private static Bitmap DrawMatrixInvertDet()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Barras de determinante | A |
                using (var barPen = new Pen(Color.FromArgb(142, 68, 173), 1.5f))
                {
                    g.DrawLine(barPen, 3f, 3f, 3f, 20f);
                    g.DrawLine(barPen, 20f, 3f, 20f, 20f);
                }

                using (var fontA = new Font("Arial", 9f, FontStyle.Bold))
                using (var fontExp = new Font("Arial", 6f, FontStyle.Bold))
                using (var brushA = new SolidBrush(Color.FromArgb(142, 68, 173)))
                using (var brushExp = new SolidBrush(Color.FromArgb(231, 76, 60)))
                {
                    g.DrawString("A", fontA, brushA, 5f, 6f);
                    g.DrawString("-1", fontExp, brushExp, 13f, 4f);
                }

                // Indicador det na base
                using (var fontDet = new Font("Arial", 5.5f, FontStyle.Bold))
                using (var brushDet = new SolidBrush(Color.FromArgb(100, 100, 100)))
                {
                    g.DrawString("det", fontDet, brushDet, 7f, 15f);
                }
            }
            return bmp;
        }

        private static Bitmap DrawMatrixSolver()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Equação A·x = b
                using (var font = new Font("Arial", 7f, FontStyle.Bold))
                using (var brushA = new SolidBrush(Color.FromArgb(52, 73, 94)))
                using (var brushX = new SolidBrush(Color.FromArgb(46, 175, 100)))
                using (var brushB = new SolidBrush(Color.FromArgb(243, 156, 18)))
                {
                    g.DrawString("A", font, brushA, 1f, 3f);
                    g.DrawString("·", font, brushA, 7f, 3f);
                    g.DrawString("x", font, brushX, 10f, 3f);
                    g.DrawString("=", font, brushA, 15f, 3f);
                    g.DrawString("b", font, brushB, 18f, 3f);
                }

                // Seta de solução -> x
                using (var solPen = new Pen(Color.FromArgb(46, 175, 100), 1.5f))
                using (var solBrush = new SolidBrush(Color.FromArgb(46, 175, 100)))
                {
                    g.DrawLine(solPen, 4f, 17f, 15f, 17f);
                    PointF[] arrow = new PointF[] { new PointF(15f, 14f), new PointF(19f, 17f), new PointF(15f, 20f) };
                    g.FillPolygon(solBrush, arrow);
                }
            }
            return bmp;
        }

        private static Bitmap DrawMatrixEigen()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Letra grega Lambda estilizada
                using (var font = new Font("Georgia", 11f, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.FromArgb(230, 126, 34)))
                {
                    g.DrawString("λ", font, brush, 2f, 3f);
                }

                // Vetor próprio inclinado (Eigenvector)
                using (var vPen = new Pen(Color.FromArgb(41, 128, 185), 1.6f))
                using (var vBrush = new SolidBrush(Color.FromArgb(41, 128, 185)))
                {
                    g.DrawLine(vPen, 9f, 19f, 19f, 9f);
                    PointF[] arrow = new PointF[] { new PointF(15f, 8f), new PointF(21f, 7f), new PointF(20f, 13f) };
                    g.FillPolygon(vBrush, arrow);
                }

                // Letra v pequena
                using (var fontV = new Font("Arial", 6f, FontStyle.Bold))
                using (var brushV = new SolidBrush(Color.FromArgb(41, 128, 185)))
                {
                    g.DrawString("v", fontV, brushV, 13f, 14f);
                }
            }
            return bmp;
        }

        // Pill Disk Save: Ícone estilizado de disco/disquete com pílula e seta para baixo (Gravar no Disco)
        private static Bitmap DrawPillDiskSave()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Corpo do disquete/disco (Azul Ardósia Escuro)
                var bodyRect = new RectangleF(2.5f, 2.5f, 19f, 19f);
                using (var bodyBrush = new LinearGradientBrush(bodyRect, Color.FromArgb(44, 62, 80), Color.FromArgb(28, 40, 52), LinearGradientMode.Vertical))
                using (var borderPen = new Pen(Color.FromArgb(20, 30, 40), 1.2f))
                {
                    g.FillRectangle(bodyBrush, bodyRect);
                    g.DrawRectangle(borderPen, bodyRect.X, bodyRect.Y, bodyRect.Width, bodyRect.Height);
                }

                // Obturador metálico superior
                var shutterRect = new RectangleF(6f, 2.5f, 12f, 6.5f);
                using (var shutterBrush = new SolidBrush(Color.FromArgb(189, 195, 199)))
                using (var shutterPen = new Pen(Color.FromArgb(127, 140, 141), 0.8f))
                {
                    g.FillRectangle(shutterBrush, shutterRect);
                    g.DrawRectangle(shutterPen, shutterRect.X, shutterRect.Y, shutterRect.Width, shutterRect.Height);
                }
                // Janela do obturador
                using (var winBrush = new SolidBrush(Color.FromArgb(44, 62, 80)))
                {
                    g.FillRectangle(winBrush, 8f, 4f, 3.5f, 4f);
                }

                // Etiqueta central branca com badge de pílula
                var labelRect = new RectangleF(5f, 11f, 14f, 9.5f);
                using (var labelBrush = new SolidBrush(Color.FromArgb(245, 247, 250)))
                using (var labelPen = new Pen(Color.FromArgb(210, 215, 225), 0.8f))
                {
                    g.FillRectangle(labelBrush, labelRect);
                    g.DrawRectangle(labelPen, labelRect.X, labelRect.Y, labelRect.Width, labelRect.Height);
                }

                // Seta de gravação para baixo (Terracota / Laranja vibrante)
                using (var saveBrush = new SolidBrush(Color.FromArgb(230, 81, 0)))
                using (var savePen = new Pen(Color.FromArgb(191, 54, 12), 0.8f))
                {
                    PointF[] arrow = new PointF[]
                    {
                        new PointF(10f, 12.5f),
                        new PointF(14f, 12.5f),
                        new PointF(14f, 15.5f),
                        new PointF(16.5f, 15.5f),
                        new PointF(12f, 19.5f),
                        new PointF(7.5f, 15.5f),
                        new PointF(10f, 15.5f)
                    };
                    g.FillPolygon(saveBrush, arrow);
                    g.DrawPolygon(savePen, arrow);
                }
            }
            return bmp;
        }

        // Pill Disk Load: Ícone de leitura de dados do disco com pílula e seta para cima (Carregar do Disco)
        private static Bitmap DrawPillDiskLoad()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Corpo do disco/pasta de dados (Azul Petróleo / Slate)
                var bodyRect = new RectangleF(2.5f, 3.5f, 19f, 18f);
                using (var bodyBrush = new LinearGradientBrush(bodyRect, Color.FromArgb(55, 71, 79), Color.FromArgb(38, 50, 56), LinearGradientMode.Vertical))
                using (var borderPen = new Pen(Color.FromArgb(25, 35, 40), 1.2f))
                {
                    g.FillRectangle(bodyBrush, bodyRect);
                    g.DrawRectangle(borderPen, bodyRect.X, bodyRect.Y, bodyRect.Width, bodyRect.Height);
                }

                // Trilha/disco interno circular
                using (var platterBrush = new SolidBrush(Color.FromArgb(69, 90, 100)))
                using (var platterPen = new Pen(Color.FromArgb(0, 150, 136), 1f))
                {
                    g.FillEllipse(platterBrush, 5.5f, 6.5f, 13f, 13f);
                    g.DrawEllipse(platterPen, 5.5f, 6.5f, 13f, 13f);
                }

                // Pílula horizontal estilizada no centro
                var pillRect = new RectangleF(7f, 10.5f, 10f, 5f);
                using (var path = new GraphicsPath())
                {
                    path.AddArc(pillRect.X, pillRect.Y, pillRect.Height, pillRect.Height, 90, 180);
                    path.AddArc(pillRect.Right - pillRect.Height, pillRect.Y, pillRect.Height, pillRect.Height, 270, 180);
                    path.CloseFigure();

                    using (var pillBrush = new LinearGradientBrush(pillRect, Color.FromArgb(0, 150, 136), Color.FromArgb(38, 198, 218), LinearGradientMode.Horizontal))
                    {
                        g.FillPath(pillBrush, path);
                    }
                }

                // Seta de extração / upload para cima (Verde Esmeralda Brilhante)
                using (var loadBrush = new SolidBrush(Color.FromArgb(0, 230, 118)))
                using (var loadPen = new Pen(Color.FromArgb(0, 120, 80), 0.8f))
                {
                    PointF[] arrow = new PointF[]
                    {
                        new PointF(12f, 3f),
                        new PointF(16.5f, 7.5f),
                        new PointF(14f, 7.5f),
                        new PointF(14f, 10.5f),
                        new PointF(10f, 10.5f),
                        new PointF(10f, 7.5f),
                        new PointF(7.5f, 7.5f)
                    };
                    g.FillPolygon(loadBrush, arrow);
                    g.DrawPolygon(loadPen, arrow);
                }
            }
            return bmp;
        }

        private static Bitmap DrawPathDictionary()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Sem fundo (transparente nativo)

                // Livro / Dicionário estilizado em azul ciano moderno
                using (var bookBrush = new SolidBrush(Color.FromArgb(41, 128, 185)))
                using (var bookPen = new Pen(Color.FromArgb(31, 97, 141), 1f))
                {
                    g.FillRectangle(bookBrush, 3, 4, 18, 16);
                    g.DrawRectangle(bookPen, 3, 4, 18, 16);
                }

                // Páginas / Miolo interno claro
                using (var pageBrush = new SolidBrush(Color.FromArgb(236, 240, 241)))
                {
                    g.FillRectangle(pageBrush, 5, 6, 14, 12);
                }

                // Símbolo de chave e mapeamento { : } em laranja vibrante
                using (var font = new Font("Consolas", 6.5f, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.FromArgb(211, 84, 0)))
                {
                    g.DrawString("{:}", font, textBrush, 4f, 6.5f);
                }

                // Seta de mapeamento / tradução em verde esmeralda
                using (var arrowBrush = new SolidBrush(Color.FromArgb(46, 204, 113)))
                using (var arrowPen = new Pen(Color.FromArgb(39, 174, 96), 0.8f))
                {
                    PointF[] arrow = new PointF[]
                    {
                        new PointF(13f, 13f),
                        new PointF(17.5f, 15f),
                        new PointF(13f, 17f)
                    };
                    g.FillPolygon(arrowBrush, arrow);
                    g.DrawPolygon(arrowPen, arrow);
                }
            }
            return bmp;
        }

        private static Bitmap DrawFastPareto()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Sem fundo (transparente nativo)

                // Eixos cartesianos X e Y
                using (var axisPen = new Pen(Color.FromArgb(52, 73, 94), 1.2f))
                {
                    g.DrawLine(axisPen, 3, 3, 3, 20);
                    g.DrawLine(axisPen, 3, 20, 21, 20);
                }

                // Curva da Fronteira de Pareto (Laranja / Vermelho Coral)
                using (var curvePen = new Pen(Color.FromArgb(231, 76, 60), 1.2f))
                {
                    PointF[] curve = new PointF[]
                    {
                        new PointF(5, 5),
                        new PointF(7, 10),
                        new PointF(11, 14),
                        new PointF(18, 18)
                    };
                    g.DrawCurve(curvePen, curve, 0.5f);
                }

                // Pontos dominados (cinza / azul translúcido)
                using (var domBrush = new SolidBrush(Color.FromArgb(149, 165, 166)))
                {
                    g.FillEllipse(domBrush, 12, 8, 3, 3);
                    g.FillEllipse(domBrush, 15, 11, 3, 3);
                    g.FillEllipse(domBrush, 10, 6, 2.5f, 2.5f);
                }

                // Pontos da Fronteira de Pareto (Ouro / Dourado brilhante)
                using (var goldBrush = new SolidBrush(Color.FromArgb(241, 196, 15)))
                using (var goldPen = new Pen(Color.FromArgb(214, 137, 16), 0.8f))
                {
                    PointF[] paretoPts = new PointF[]
                    {
                        new PointF(5, 5),
                        new PointF(7, 10),
                        new PointF(11, 14),
                        new PointF(18, 18)
                    };
                    foreach (var pt in paretoPts)
                    {
                        g.FillEllipse(goldBrush, pt.X - 2, pt.Y - 2, 4.5f, 4.5f);
                        g.DrawEllipse(goldPen, pt.X - 2, pt.Y - 2, 4.5f, 4.5f);
                    }
                }
            }
            return bmp;
        }

        private static Bitmap DrawVectorSimilarity()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Sem fundo (transparente nativo)

                // Vetor de Consulta (Azul Ciano Elétrico)
                using (var qPen = new Pen(Color.FromArgb(41, 128, 185), 1.6f))
                {
                    qPen.EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor;
                    g.DrawLine(qPen, 3, 20, 18, 5);
                }

                // Vetor Mais Próximo / Similar (Verde Esmeralda)
                using (var matchPen = new Pen(Color.FromArgb(46, 204, 113), 1.6f))
                {
                    matchPen.EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor;
                    g.DrawLine(matchPen, 3, 20, 20, 11);
                }

                // Arco de similaridade angular / Cosseno θ (Ouro)
                using (var arcPen = new Pen(Color.FromArgb(243, 156, 18), 1.2f))
                {
                    g.DrawArc(arcPen, 6, 11, 14, 14, 290, 40);
                }

                // Lupa de busca estilizada no canto inferior direito
                using (var lensBrush = new SolidBrush(Color.FromArgb(231, 76, 60)))
                using (var lensPen = new Pen(Color.FromArgb(192, 57, 43), 0.8f))
                {
                    g.FillEllipse(lensBrush, 13, 13, 7, 7);
                    g.DrawEllipse(lensPen, 13, 13, 7, 7);
                }

                using (var handlePen = new Pen(Color.FromArgb(192, 57, 43), 1.5f))
                {
                    g.DrawLine(handlePen, 19, 19, 22, 22);
                }
            }
            return bmp;
        }

        private static Bitmap DrawLossFunctions()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Eixos cartesianos sutis
                using (var axisPen = new Pen(Color.FromArgb(140, 149, 165), 1.0f))
                {
                    g.DrawLine(axisPen, 2, 19, 22, 19); // Eixo X
                    g.DrawLine(axisPen, 12, 3, 12, 21); // Eixo Y
                }

                // Curva de Perda Convexa (Huber / MSE)
                using (var curvePen = new Pen(Color.FromArgb(231, 76, 60), 1.8f))
                {
                    PointF[] curvePts = new PointF[]
                    {
                        new PointF(3, 7),
                        new PointF(7, 14),
                        new PointF(12, 19),
                        new PointF(17, 14),
                        new PointF(21, 7)
                    };
                    g.DrawCurve(curvePen, curvePts, 0.5f);
                }

                // Ponto de mínimo ótimo (Verde Esmeralda no vértice (12, 19))
                using (var minBrush = new SolidBrush(Color.FromArgb(46, 204, 113)))
                using (var minPen = new Pen(Color.FromArgb(39, 174, 96), 1.0f))
                {
                    g.FillEllipse(minBrush, 10f, 17f, 4f, 4f);
                    g.DrawEllipse(minPen, 10f, 17f, 4f, 4f);
                }

                // Letra "L" estilizada (Loss) no canto superior esquerdo
                using (var font = new Font("Arial", 6.5f, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.FromArgb(52, 152, 219)))
                {
                    g.DrawString("L", font, textBrush, 2, 2);
                }
            }
            return bmp;
        }

        private static Bitmap DrawChartLoss()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Eixos cartesianos nítidos em cinza ardósia (fundo transparente)
                using (var axisPen = new Pen(Color.FromArgb(71, 85, 105), 1.1f))
                {
                    g.DrawLine(axisPen, 2, 20, 22, 20); // Eixo X
                    g.DrawLine(axisPen, 12, 2, 12, 21); // Eixo Y

                    // Setas direcionais nos eixos
                    g.DrawLine(axisPen, 20, 18, 22, 20);
                    g.DrawLine(axisPen, 20, 22, 22, 20);
                    g.DrawLine(axisPen, 10, 4, 12, 2);
                    g.DrawLine(axisPen, 14, 4, 12, 2);
                }

                // Faixa de tolerância suave sombreada (+/- delta)
                using (var zoneBrush = new SolidBrush(Color.FromArgb(32, 2, 132, 199)))
                {
                    g.FillRectangle(zoneBrush, 8, 10, 8, 10);
                }
                using (var deltaPen = new Pen(Color.FromArgb(120, 2, 132, 199), 0.8f) { DashStyle = DashStyle.Dot })
                {
                    g.DrawLine(deltaPen, 8, 8, 8, 20);
                    g.DrawLine(deltaPen, 16, 8, 16, 20);
                }

                // Curva de perda analítica contínua (Azul cobalto com alta legibilidade)
                using (var curvePen = new Pen(Color.FromArgb(2, 132, 199), 1.8f))
                {
                    PointF[] curvePts = new PointF[]
                    {
                        new PointF(3, 7),
                        new PointF(7, 13),
                        new PointF(12, 18),
                        new PointF(17, 13),
                        new PointF(21, 7)
                    };
                    g.DrawCurve(curvePen, curvePts, 0.5f);
                }

                // Scatter Dots / Pontos de dados reais sobre a curva
                using (var dotRed = new SolidBrush(Color.FromArgb(239, 68, 68)))
                using (var dotGreen = new SolidBrush(Color.FromArgb(34, 197, 94)))
                using (var borderPen = new Pen(Color.FromArgb(15, 23, 42), 0.8f))
                {
                    // Outlier esquerdo (vermelho)
                    g.FillEllipse(dotRed, 3f, 6f, 3.5f, 3.5f);
                    g.DrawEllipse(borderPen, 3f, 6f, 3.5f, 3.5f);

                    // Inliers centrais (verdes)
                    g.FillEllipse(dotGreen, 7.5f, 12.5f, 3.2f, 3.2f);
                    g.DrawEllipse(borderPen, 7.5f, 12.5f, 3.2f, 3.2f);

                    g.FillEllipse(dotGreen, 11f, 16f, 3.5f, 3.5f);
                    g.DrawEllipse(borderPen, 11f, 16f, 3.5f, 3.5f);

                    g.FillEllipse(dotGreen, 14.5f, 13.5f, 3.2f, 3.2f);
                    g.DrawEllipse(borderPen, 14.5f, 13.5f, 3.2f, 3.2f);

                    // Outlier direito (vermelho)
                    g.FillEllipse(dotRed, 18.5f, 7.5f, 3.5f, 3.5f);
                    g.DrawEllipse(borderPen, 18.5f, 7.5f, 3.5f, 3.5f);
                }
            }
            return bmp;
        }

        // Pill Geometry Filter: Funil técnico em ciano/ardósia filtrando polígono esmeralda com micro-face eliminada
        private static Bitmap DrawPillGeometryFilter()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Funil / Filtro estilizado em azul/ardósia com fundo translúcido
                var filterPts = new PointF[]
                {
                    new PointF(2.5f, 5f),
                    new PointF(21.5f, 5f),
                    new PointF(14.5f, 13f),
                    new PointF(14.5f, 19.5f),
                    new PointF(9.5f, 19.5f),
                    new PointF(9.5f, 13f)
                };

                using (var filterBrush = new LinearGradientBrush(new RectangleF(2, 5, 20, 15), Color.FromArgb(50, 56, 189, 248), Color.FromArgb(20, 15, 23, 42), LinearGradientMode.Vertical))
                using (var filterPen = new Pen(Color.FromArgb(56, 189, 248), 1.2f) { LineJoin = LineJoin.Round })
                {
                    g.FillPolygon(filterBrush, filterPts);
                    g.DrawPolygon(filterPen, filterPts);
                }

                // Linha pontilhada da grade do filtro
                using (var gridPen = new Pen(Color.FromArgb(148, 163, 184), 0.9f) { DashStyle = DashStyle.Dot })
                {
                    g.DrawLine(gridPen, 5.5f, 8.5f, 18.5f, 8.5f);
                }

                // Face geométrica sadia saindo limpa por baixo (triângulo verde esmeralda com contorno branco nítido)
                var cleanPts = new PointF[]
                {
                    new PointF(12f, 13f),
                    new PointF(18f, 21.5f),
                    new PointF(6f, 21.5f)
                };
                using (var cleanBrush = new SolidBrush(Color.FromArgb(16, 185, 129)))
                using (var cleanPen = new Pen(Color.FromArgb(255, 255, 255), 1.1f))
                {
                    g.FillPolygon(cleanBrush, cleanPts);
                    g.DrawPolygon(cleanPen, cleanPts);
                }

                // Micro-face / Sliver eliminada no canto superior direito com símbolo X vermelho
                using (var badBrush = new SolidBrush(Color.FromArgb(239, 68, 68)))
                using (var badPen = new Pen(Color.FromArgb(239, 68, 68), 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.FillPolygon(badBrush, new PointF[] { new PointF(17f, 2.5f), new PointF(22f, 3.5f), new PointF(19f, 4.5f) });
                    g.DrawLine(badPen, 18.5f, 1.5f, 22.5f, 5.5f);
                    g.DrawLine(badPen, 22.5f, 1.5f, 18.5f, 5.5f);
                }
            }
            return bmp;
        }

        // Pill Viewport 3D Capture: Câmera estilizada com visor 3D, lente ciano elétrico e micro pílula
        private static Bitmap DrawPillViewportCapture()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Corpo da câmera (Cinza grafite escuro com borda metálica)
                var bodyRect = new RectangleF(2f, 5.5f, 20f, 15f);
                using (var bodyBrush = new LinearGradientBrush(bodyRect, Color.FromArgb(45, 55, 72), Color.FromArgb(26, 32, 44), LinearGradientMode.Vertical))
                using (var borderPen = new Pen(Color.FromArgb(15, 23, 42), 1.2f))
                {
                    g.FillRectangle(bodyBrush, bodyRect);
                    g.DrawRectangle(borderPen, bodyRect.X, bodyRect.Y, bodyRect.Width, bodyRect.Height);
                }

                // Topo da câmera / Visor Pentaprisma
                PointF[] topRoof = new PointF[]
                {
                    new PointF(7f, 5.5f),
                    new PointF(9.5f, 2.5f),
                    new PointF(14.5f, 2.5f),
                    new PointF(17f, 5.5f)
                };
                using (var roofBrush = new SolidBrush(Color.FromArgb(74, 85, 104)))
                using (var roofPen = new Pen(Color.FromArgb(15, 23, 42), 1.1f))
                {
                    g.FillPolygon(roofBrush, topRoof);
                    g.DrawPolygon(roofPen, topRoof);
                }

                // Botão de Disparo / Flash no topo esquerdo
                using (var flashBrush = new SolidBrush(Color.FromArgb(239, 68, 68)))
                {
                    g.FillRectangle(flashBrush, 4f, 3.5f, 2.5f, 2f);
                }

                // Anel externo da Lente da Câmera (Ciano elétrico vibrante #0ea5e9)
                float lensX = 6.5f;
                float lensY = 7.5f;
                float lensSize = 11f;
                using (var lensRingBrush = new SolidBrush(Color.FromArgb(14, 165, 233)))
                using (var lensRingPen = new Pen(Color.FromArgb(56, 189, 248), 1f))
                {
                    g.FillEllipse(lensRingBrush, lensX, lensY, lensSize, lensSize);
                    g.DrawEllipse(lensRingPen, lensX, lensY, lensSize, lensSize);
                }

                // Interior da Lente (Vidro escuro profundo #0f172a)
                float innerSize = 8f;
                float innerX = lensX + 1.5f;
                float innerY = lensY + 1.5f;
                using (var innerLensBrush = new SolidBrush(Color.FromArgb(15, 23, 42)))
                {
                    g.FillEllipse(innerLensBrush, innerX, innerY, innerSize, innerSize);
                }

                // Micro grade 3D em perspectiva dentro da lente (simbolizando o Viewport 3D do Rhino)
                using (var gridPen = new Pen(Color.FromArgb(125, 211, 252), 0.8f))
                {
                    g.DrawLine(gridPen, innerX + 1.5f, innerY + 5.5f, innerX + 6.5f, innerY + 5.5f);
                    g.DrawLine(gridPen, innerX + 2.5f, innerY + 3.5f, innerX + 5.5f, innerY + 3.5f);
                    g.DrawLine(gridPen, innerX + 2f, innerY + 6.5f, innerX + 4f, innerY + 2f);
                    g.DrawLine(gridPen, innerX + 6f, innerY + 6.5f, innerX + 4f, innerY + 2f);
                }

                // Reflexo de luz na lente (brilho branco semicircular)
                using (var glintBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                {
                    g.FillEllipse(glintBrush, innerX + 1.5f, innerY + 1.5f, 2f, 2f);
                }

                // Micro pílula no canto inferior direito
                var pillRect = new RectangleF(15f, 15f, 6.5f, 3.5f);
                using (var pillBrush = new SolidBrush(Color.FromArgb(245, 158, 11)))
                using (var pillPen = new Pen(Color.FromArgb(255, 255, 255), 0.8f))
                {
                    g.FillRectangle(pillBrush, pillRect);
                    g.DrawRectangle(pillPen, pillRect.X, pillRect.Y, pillRect.Width, pillRect.Height);
                }
            }
            return bmp;
        }

        // Duplicate Data Inspector: Tabela/Árvore com linhas repetidas destacadas em carmim/âmbar e lupa de inspeção
        private static Bitmap DrawDuplicateInspector()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Prancheta / Tabela de fundo (azul ardósia suave)
                var tableRect = new RectangleF(2.5f, 2.5f, 15f, 19f);
                using (var bgBrush = new SolidBrush(Color.FromArgb(241, 245, 249)))
                using (var borderPen = new Pen(Color.FromArgb(148, 163, 184), 1f))
                {
                    g.FillRectangle(bgBrush, tableRect);
                    g.DrawRectangle(borderPen, tableRect.X, tableRect.Y, tableRect.Width, tableRect.Height);
                }

                // Cabeçalho da tabela (Azul petróleo #334155)
                var headerRect = new RectangleF(2.5f, 2.5f, 15f, 4.5f);
                using (var headBrush = new SolidBrush(Color.FromArgb(51, 65, 85)))
                {
                    g.FillRectangle(headBrush, headerRect);
                }

                // Divisórias de colunas no cabeçalho
                using (var colPen = new Pen(Color.FromArgb(100, 116, 139), 0.8f))
                {
                    g.DrawLine(colPen, 7.5f, 2.5f, 7.5f, 21.5f);
                    g.DrawLine(colPen, 12.5f, 2.5f, 12.5f, 21.5f);
                }

                // Linhas da grade
                using (var rowPen = new Pen(Color.FromArgb(203, 213, 225), 0.8f))
                {
                    g.DrawLine(rowPen, 2.5f, 7f, 17.5f, 7f);
                    g.DrawLine(rowPen, 2.5f, 10.5f, 17.5f, 10.5f);
                    g.DrawLine(rowPen, 2.5f, 14f, 17.5f, 14f);
                    g.DrawLine(rowPen, 2.5f, 17.5f, 17.5f, 17.5f);
                }

                // Linha 2 (REPETIDA - Destacada em Laranja Âmbar)
                var dupRow1 = new RectangleF(3f, 7.2f, 14f, 3f);
                using (var dupBrush = new SolidBrush(Color.FromArgb(254, 215, 170)))
                {
                    g.FillRectangle(dupBrush, dupRow1);
                }
                // Linha 4 (REPETIDA - Cópia idêntica destacada em Laranja Âmbar)
                var dupRow2 = new RectangleF(3f, 14.2f, 14f, 3f);
                using (var dupBrush = new SolidBrush(Color.FromArgb(254, 215, 170)))
                {
                    g.FillRectangle(dupBrush, dupRow2);
                }

                // Indicadores de igualdade nas linhas repetidas (traço âmbar escuro)
                using (var markPen = new Pen(Color.FromArgb(234, 88, 12), 1.2f))
                {
                    g.DrawLine(markPen, 4.5f, 8.7f, 6.5f, 8.7f);
                    g.DrawLine(markPen, 9.5f, 8.7f, 11.5f, 8.7f);
                    g.DrawLine(markPen, 14f, 8.7f, 16f, 8.7f);

                    g.DrawLine(markPen, 4.5f, 15.7f, 6.5f, 15.7f);
                    g.DrawLine(markPen, 9.5f, 15.7f, 11.5f, 15.7f);
                    g.DrawLine(markPen, 14f, 15.7f, 16f, 15.7f);
                }

                // Lupa de Inspeção / Detecção no canto inferior direito
                float glassX = 13.5f;
                float glassY = 12.5f;
                float glassR = 7.5f;

                // Círculo da lente com preenchimento semitransparente ciano
                using (var lensBg = new SolidBrush(Color.FromArgb(220, 240, 253, 250)))
                using (var lensRing = new Pen(Color.FromArgb(14, 165, 233), 1.6f))
                {
                    g.FillEllipse(lensBg, glassX, glassY, glassR, glassR);
                    g.DrawEllipse(lensRing, glassX, glassY, glassR, glassR);
                }

                // Símbolo de repetição "==" dentro da lupa
                using (var eqPen = new Pen(Color.FromArgb(225, 29, 72), 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(eqPen, glassX + 2.2f, glassY + 3.0f, glassX + 5.3f, glassY + 3.0f);
                    g.DrawLine(eqPen, glassX + 2.2f, glassY + 4.8f, glassX + 5.3f, glassY + 4.8f);
                }

                // Cabo da lupa
                using (var handlePen = new Pen(Color.FromArgb(30, 41, 59), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(handlePen, glassX + 6.2f, glassY + 6.2f, glassX + 9.5f, glassY + 9.5f);
                }
            }
            return bmp;
        }

        // Data Table Visualizer: Tabela com cabeçalho azul, linhas de dados e linha destacada em amarelo/ouro
        private static Bitmap DrawDataTableVisualizer()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Fundo do grid
                using (var bgBrush = new SolidBrush(Color.FromArgb(248, 250, 252)))
                {
                    g.FillRectangle(bgBrush, 1, 1, 22, 22);
                }

                // Cabeçalho da Tabela (Azul escuro)
                using (var headerBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
                {
                    g.FillRectangle(headerBrush, 1, 1, 22, 5);
                }

                // Divisores verticais do cabeçalho
                using (var headDivPen = new Pen(Color.FromArgb(71, 85, 105), 1f))
                {
                    g.DrawLine(headDivPen, 6, 1, 6, 6);
                    g.DrawLine(headDivPen, 14, 1, 14, 6);
                }

                // Linha 1 (dados comuns): y = 6 a 10
                using (var textPen = new Pen(Color.FromArgb(148, 163, 184), 1.2f))
                {
                    g.DrawLine(textPen, 8, 8, 12, 8);
                    g.DrawLine(textPen, 16, 8, 21, 8);
                }

                // Linha 2 (LINHA DESTACADA / HIGHLIGHTED ROW): y = 10 a 15
                using (var hlBrush = new SolidBrush(Color.FromArgb(254, 240, 138)))
                using (var hlBorder = new Pen(Color.FromArgb(234, 179, 8), 1.2f))
                {
                    g.FillRectangle(hlBrush, 1.5f, 10.5f, 21, 4.5f);
                    g.DrawRectangle(hlBorder, 1.5f, 10.5f, 21, 4.5f);
                }

                // Indicador de seleção ▶ e dados da linha selecionada
                using (var cursorBrush = new SolidBrush(Color.FromArgb(202, 138, 4)))
                using (var selTextPen = new Pen(Color.FromArgb(161, 98, 7), 1.4f))
                {
                    PointF[] triangle = new PointF[] { new PointF(2.5f, 11f), new PointF(5f, 12.8f), new PointF(2.5f, 14.5f) };
                    g.FillPolygon(cursorBrush, triangle);
                    g.DrawLine(selTextPen, 8, 12.8f, 13, 12.8f);
                    g.DrawLine(selTextPen, 16, 12.8f, 21, 12.8f);
                }

                // Linha 3 (dados zebra): y = 15 a 19
                using (var zebraBrush = new SolidBrush(Color.FromArgb(241, 245, 249)))
                using (var textPen = new Pen(Color.FromArgb(148, 163, 184), 1.2f))
                {
                    g.FillRectangle(zebraBrush, 1, 15, 22, 4);
                    g.DrawLine(textPen, 8, 17, 12, 17);
                    g.DrawLine(textPen, 16, 17, 20, 17);
                }

                // Linha de status inferior / paginação: y = 19 a 23
                using (var footBrush = new SolidBrush(Color.FromArgb(226, 232, 240)))
                using (var dotBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
                {
                    g.FillRectangle(footBrush, 1, 19, 22, 4);
                    g.FillEllipse(dotBrush, 8, 20.5f, 1.5f, 1.5f);
                    g.FillEllipse(dotBrush, 11, 20.5f, 1.5f, 1.5f);
                    g.FillEllipse(dotBrush, 14, 20.5f, 1.5f, 1.5f);
                }

                // Linhas de grade gerais
                using (var gridPen = new Pen(Color.FromArgb(203, 213, 225), 1f))
                {
                    g.DrawLine(gridPen, 6, 6, 6, 19);
                    g.DrawLine(gridPen, 14, 6, 14, 19);
                    g.DrawLine(gridPen, 1, 10, 23, 10);
                    g.DrawLine(gridPen, 1, 15, 23, 15);
                    g.DrawLine(gridPen, 1, 19, 23, 19);
                }

                // Borda externa
                using (var outerPen = new Pen(Color.FromArgb(51, 65, 85), 1.2f))
                {
                    g.DrawRectangle(outerPen, 1, 1, 22, 22);
                }
            }
            return bmp;
        }

        // Box Plot Distribution (Gráfico de Caixas e Bigodes com Quartis, Mediana e Outliers - 100% Fundo Transparente)
        // ── Hierarchical Cluster Graph: dendrograma (topo) + heatmap (base) ──
        private static Bitmap DrawHierarchicalCluster()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // ── Heatmap 4×4 na parte inferior (y=13..22) ─────────────────
                // Simula um mapa de calor reordenado com gradiente azul-ciano
                int[,] heat = {
                    { 90, 70, 30, 20 },
                    { 70, 95, 25, 15 },
                    { 30, 25, 88, 60 },
                    { 20, 15, 60, 92 }
                };
                float cx0 = 4f, cy0 = 13f, cw = 4f, ch = 2.2f;
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 4; c++)
                    {
                        float t = heat[r, c] / 100f;
                        // gradiente: escuro (#122E37) → médio (#1E8C91) → claro (#5AE6D7)
                        Color cell;
                        if (t <= 0.5f)
                        {
                            float f = t / 0.5f;
                            cell = Color.FromArgb(
                                (int)(18 + (30 - 18) * f),
                                (int)(46 + (140 - 46) * f),
                                (int)(55 + (145 - 55) * f));
                        }
                        else
                        {
                            float f = (t - 0.5f) / 0.5f;
                            cell = Color.FromArgb(
                                (int)(30 + (90 - 30) * f),
                                (int)(140 + (230 - 140) * f),
                                (int)(145 + (215 - 145) * f));
                        }
                        using (var br = new SolidBrush(cell))
                            g.FillRectangle(br, cx0 + c * cw, cy0 + r * ch, cw - 0.4f, ch - 0.4f);
                    }

                // Borda do heatmap
                using (var pen = new Pen(Color.FromArgb(60, 80, 90), 0.7f))
                    g.DrawRectangle(pen, cx0, cy0, 4 * cw - 0.4f, 4 * ch - 0.4f);

                // ── Dendrograma na parte superior (y=2..12) ───────────────────
                // 4 folhas alinhadas com as colunas do heatmap
                float[] leafX = { cx0 + 0.5f * cw, cx0 + 1.5f * cw, cx0 + 2.5f * cw, cx0 + 3.5f * cw };
                float baseY = 12f; // base do dendrograma (encosta no heatmap)

                using (var pen = new Pen(Color.FromArgb(160, 195, 215), 1.1f))
                {
                    // Merge 1: folhas 0+1 → nó A (altura 3)
                    float aY = baseY - 3f;
                    float aX = (leafX[0] + leafX[1]) * 0.5f;
                    g.DrawLine(pen, leafX[0], baseY, leafX[0], aY);
                    g.DrawLine(pen, leafX[1], baseY, leafX[1], aY);
                    g.DrawLine(pen, leafX[0], aY, leafX[1], aY);

                    // Merge 2: folhas 2+3 → nó B (altura 3)
                    float bY = baseY - 3f;
                    float bX = (leafX[2] + leafX[3]) * 0.5f;
                    g.DrawLine(pen, leafX[2], baseY, leafX[2], bY);
                    g.DrawLine(pen, leafX[3], baseY, leafX[3], bY);
                    g.DrawLine(pen, leafX[2], bY, leafX[3], bY);

                    // Merge raiz: nó A + nó B (altura 7)
                    float rootY = baseY - 7f;
                    g.DrawLine(pen, aX, aY, aX, rootY);
                    g.DrawLine(pen, bX, bY, bX, rootY);
                    g.DrawLine(pen, aX, rootY, bX, rootY);
                }

                // Linha de corte verde limão (entre os dois grupos)
                float cutY = baseY - 4.5f;
                using (var cutPen = new Pen(Color.FromArgb(140, 255, 80), 0.9f) { DashStyle = DashStyle.Dash })
                    g.DrawLine(cutPen, cx0 - 1f, cutY, cx0 + 4 * cw + 0.5f, cutY);

                // Contornos de cluster no heatmap (dois quadrantes diagonais em lime)
                using (var clusterPen = new Pen(Color.FromArgb(140, 255, 80), 0.9f))
                {
                    // Grupo 1 (linhas/colunas 0-1)
                    g.DrawRectangle(clusterPen, cx0, cy0, 2 * cw - 0.4f, 2 * ch - 0.4f);
                    // Grupo 2 (linhas/colunas 2-3)
                    g.DrawRectangle(clusterPen, cx0 + 2 * cw, cy0 + 2 * ch, 2 * cw - 0.4f, 2 * ch - 0.4f);
                }
            }
            return bmp;
        }

        private static Bitmap DrawChartBoxPlot()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Eixos cartesianos finos (inferior e esquerdo)
                using (var axisPen = new Pen(Color.FromArgb(90, 105, 125), 1.2f))
                {
                    g.DrawLine(axisPen, 2, 2, 2, 21);
                    g.DrawLine(axisPen, 2, 21, 22, 21);
                }

                // Grid sutil horizontal pontilhado
                using (var gridPen = new Pen(Color.FromArgb(200, 210, 225), 0.8f) { DashStyle = DashStyle.Dot })
                {
                    g.DrawLine(gridPen, 2, 7, 22, 7);
                    g.DrawLine(gridPen, 2, 14, 22, 14);
                }

                // Caixa 1: Coral suave (#EC949A - média/oitava da referência)
                float x1 = 8f;
                using (var whiskerPen = new Pen(Color.FromArgb(35, 45, 55), 1.1f))
                {
                    // Bigode inferior e cap
                    g.DrawLine(whiskerPen, x1, 19, x1, 15);
                    g.DrawLine(whiskerPen, x1 - 2.2f, 19, x1 + 2.2f, 19);
                    // Bigode superior e cap
                    g.DrawLine(whiskerPen, x1, 9, x1, 5);
                    g.DrawLine(whiskerPen, x1 - 2.2f, 5, x1 + 2.2f, 5);
                }
                using (var boxBrush = new SolidBrush(Color.FromArgb(236, 148, 154)))
                using (var boxBorder = new Pen(Color.FromArgb(35, 45, 55), 1.1f))
                using (var medianPen = new Pen(Color.FromArgb(20, 25, 35), 1.6f))
                {
                    g.FillRectangle(boxBrush, x1 - 3.2f, 9, 6.4f, 6f);
                    g.DrawRectangle(boxBorder, x1 - 3.2f, 9, 6.4f, 6f);
                    // Linha da Mediana
                    g.DrawLine(medianPen, x1 - 3.2f, 12, x1 + 3.2f, 12);
                }
                // Outlier Caixa 1 (círculo vazado)
                using (var outPen = new Pen(Color.FromArgb(35, 45, 55), 1f))
                {
                    g.DrawEllipse(outPen, x1 - 1.2f, 2.5f, 2.4f, 2.4f);
                }

                // Caixa 2: Ardósia azul (#8E9EB2 - variância da referência)
                float x2 = 16.5f;
                using (var whiskerPen = new Pen(Color.FromArgb(35, 45, 55), 1.1f))
                {
                    // Bigode inferior e cap
                    g.DrawLine(whiskerPen, x2, 20, x2, 17);
                    g.DrawLine(whiskerPen, x2 - 2f, 20, x2 + 2f, 20);
                    // Bigode superior e cap
                    g.DrawLine(whiskerPen, x2, 13, x2, 10);
                    g.DrawLine(whiskerPen, x2 - 2f, 10, x2 + 2f, 10);
                }
                using (var boxBrush = new SolidBrush(Color.FromArgb(142, 158, 178)))
                using (var boxBorder = new Pen(Color.FromArgb(35, 45, 55), 1.1f))
                using (var medianPen = new Pen(Color.FromArgb(20, 25, 35), 1.6f))
                {
                    g.FillRectangle(boxBrush, x2 - 3f, 13, 6f, 4f);
                    g.DrawRectangle(boxBorder, x2 - 3f, 13, 6f, 4f);
                    // Linha da Mediana
                    g.DrawLine(medianPen, x2 - 3f, 15, x2 + 3f, 15);
                }
                // Outliers Caixa 2 (círculos vazados)
                using (var outPen = new Pen(Color.FromArgb(35, 45, 55), 1f))
                {
                    g.DrawEllipse(outPen, x2 - 1.2f, 7f, 2.4f, 2.4f);
                    g.DrawEllipse(outPen, x2 - 1.2f, 3.5f, 2.4f, 2.4f);
                }
            }
            return bmp;
        }

        private static Bitmap DrawIsometricSurfaceGraph()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Eixos Isométricos sutis da Bounding Box (fundo e chão)
                using (var axisPen = new Pen(Color.FromArgb(140, 155, 175), 1f))
                {
                    // Origem no centro inferior esquerdo (x: 3, y: 17)
                    // Eixo X (para direita e baixo): (3, 17) -> (14, 21.5)
                    g.DrawLine(axisPen, 3f, 17f, 14f, 21.5f);
                    // Eixo Y (para direita e cima): (3, 17) -> (11f, 12f)
                    g.DrawLine(axisPen, 3f, 17f, 11f, 12f);
                    // Eixo Z (vertical): (3, 17) -> (3f, 5f)
                    g.DrawLine(axisPen, 3f, 17f, 3f, 5f);
                }

                // Malha 3D Isométrica com 4 facetas coloridas (Gradiente: Azul -> Ciano -> Amarelo -> Vermelho)
                // Faceta 1 (fundo esquerdo): Azul / Ciano
                PointF[] p1 = new PointF[] { new PointF(5f, 13f), new PointF(10f, 10f), new PointF(13f, 12f), new PointF(8f, 15f) };
                using (var b1 = new SolidBrush(Color.FromArgb(52, 152, 219)))
                using (var pen = new Pen(Color.FromArgb(30, 40, 50), 0.9f))
                {
                    g.FillPolygon(b1, p1);
                    g.DrawPolygon(pen, p1);
                }

                // Faceta 2 (fundo direito): Ciano / Verde
                PointF[] p2 = new PointF[] { new PointF(10f, 10f), new PointF(15f, 7f), new PointF(18f, 9.5f), new PointF(13f, 12f) };
                using (var b2 = new SolidBrush(Color.FromArgb(46, 204, 113)))
                using (var pen = new Pen(Color.FromArgb(30, 40, 50), 0.9f))
                {
                    g.FillPolygon(b2, p2);
                    g.DrawPolygon(pen, p2);
                }

                // Faceta 3 (frente esquerda): Amarelo / Laranja (cume elevado)
                PointF[] p3 = new PointF[] { new PointF(8f, 15f), new PointF(13f, 12f), new PointF(16f, 15f), new PointF(11f, 18.5f) };
                using (var b3 = new SolidBrush(Color.FromArgb(241, 196, 15)))
                using (var pen = new Pen(Color.FromArgb(30, 40, 50), 0.9f))
                {
                    g.FillPolygon(b3, p3);
                    g.DrawPolygon(pen, p3);
                }

                // Faceta 4 (frente direita): Vermelho / Laranja
                PointF[] p4 = new PointF[] { new PointF(13f, 12f), new PointF(18f, 9.5f), new PointF(20f, 12f), new PointF(16f, 15f) };
                using (var b4 = new SolidBrush(Color.FromArgb(231, 76, 60)))
                using (var pen = new Pen(Color.FromArgb(30, 40, 50), 0.9f))
                {
                    g.FillPolygon(b4, p4);
                    g.DrawPolygon(pen, p4);
                }

                // Micro barra de cor lateral vertical (colorbar) à direita
                using (var cbBorder = new Pen(Color.FromArgb(60, 70, 85), 0.8f))
                {
                    g.FillRectangle(new SolidBrush(Color.FromArgb(231, 76, 60)), 21.5f, 6f, 2f, 3.5f);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(241, 196, 15)), 21.5f, 9.5f, 2f, 3.5f);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(46, 204, 113)), 21.5f, 13f, 2f, 3.5f);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(52, 152, 219)), 21.5f, 16.5f, 2f, 3.5f);
                    g.DrawRectangle(cbBorder, 21.5f, 6f, 2f, 14f);
                }
            }
            return bmp;
        }

        private static Bitmap DrawPillViewGenerator()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                g.Clear(Color.Transparent);

                // 1. Cubo Isométrico / Bounding Box (Aramado em Cyan e Slate)
                PointF cBot = new PointF(12f, 19f);
                PointF cLeft = new PointF(3.5f, 14.5f);
                PointF cRight = new PointF(20.5f, 14.5f);
                PointF cCenter = new PointF(12f, 10f);
                PointF cTop = new PointF(12f, 2.5f);
                PointF cTopLeft = new PointF(3.5f, 7f);
                PointF cTopRight = new PointF(20.5f, 7f);

                // Faceta Superior do Cubo (Semitransparente Cyan Claro)
                PointF[] topFace = new PointF[] { cCenter, cTopRight, cTop, cTopLeft };
                using (var brushTop = new SolidBrush(Color.FromArgb(45, 14, 165, 233)))
                {
                    g.FillPolygon(brushTop, topFace);
                }

                // Faceta Esquerda do Cubo (Frente)
                PointF[] leftFace = new PointF[] { cBot, cLeft, cTopLeft, cCenter };
                using (var brushLeft = new SolidBrush(Color.FromArgb(30, 14, 165, 233)))
                {
                    g.FillPolygon(brushLeft, leftFace);
                }

                // Arestas da Bounding Box
                using (var boxPen = new Pen(Color.FromArgb(148, 163, 184), 1.1f))
                {
                    g.DrawPolygon(boxPen, new PointF[] { cTop, cTopRight, cRight, cBot, cLeft, cTopLeft });
                    g.DrawLine(boxPen, cCenter, cTop);
                    g.DrawLine(boxPen, cCenter, cLeft);
                    g.DrawLine(boxPen, cCenter, cBot);
                }

                // 2. Câmera / Visor em perspectiva (Posicionada no canto superior direito mirando no centro)
                using (var camBrush = new SolidBrush(Color.FromArgb(245, 158, 11)))
                using (var camPen = new Pen(Color.FromArgb(15, 23, 42), 1.0f))
                {
                    g.FillRectangle(camBrush, 17f, 3.5f, 5.5f, 4f);
                    g.DrawRectangle(camPen, 17f, 3.5f, 5.5f, 4f);

                    PointF[] lens = new PointF[] {
                        new PointF(17f, 4.5f),
                        new PointF(14.5f, 6f),
                        new PointF(17f, 6.5f)
                    };
                    using (var lensBrush = new SolidBrush(Color.FromArgb(234, 88, 12)))
                    {
                        g.FillPolygon(lensBrush, lens);
                        g.DrawPolygon(camPen, lens);
                    }
                }

                // 3. Vetor de Visão / Linha de Mira Ciano Pontilhada
                using (var sightPen = new Pen(Color.FromArgb(14, 165, 233), 1.2f) { DashStyle = DashStyle.Dot })
                {
                    g.DrawLine(sightPen, 14.5f, 6f, 12f, 10f);
                }

                // Ponto Focal / Alvo no Centro (Ponto Cyan com borda branca)
                using (var tgtBrush = new SolidBrush(Color.FromArgb(14, 165, 233)))
                using (var tgtBorder = new Pen(Color.White, 0.8f))
                {
                    g.FillEllipse(tgtBrush, 10.5f, 8.5f, 3f, 3f);
                    g.DrawEllipse(tgtBorder, 10.5f, 8.5f, 3f, 3f);
                }
            }
            return bmp;
        }

        // ==========================================
        // ÍCONE: PILL VECTOR SHEET LAYOUT (Prancha Vetorial SVG / PDF)
        // ==========================================
        private static Bitmap _pillVectorSheetLayout;
        public static Bitmap PillVectorSheetLayout => _pillVectorSheetLayout ?? (_pillVectorSheetLayout = DrawPillVectorSheetLayout());

        private static Bitmap DrawPillVectorSheetLayout()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // 1. Folha de Papel / Prancha Base (Branco/Cinza Técnico com Sombra Leve)
                using (var shadowBrush = new SolidBrush(Color.FromArgb(40, 15, 23, 42)))
                {
                    g.FillRectangle(shadowBrush, 2.5f, 2.5f, 19.5f, 19.5f);
                }

                using (var paperBrush = new SolidBrush(Color.FromArgb(248, 250, 252)))
                using (var borderPen = new Pen(Color.FromArgb(51, 65, 85), 1.0f))
                {
                    g.FillRectangle(paperBrush, 1.5f, 1.5f, 19.5f, 19.5f);
                    g.DrawRectangle(borderPen, 1.5f, 1.5f, 19.5f, 19.5f);
                }

                // 2. Margem Interna da Prancha (Linha Fina de Borda Técnica)
                using (var marginPen = new Pen(Color.FromArgb(148, 163, 184), 0.7f))
                {
                    g.DrawRectangle(marginPen, 3f, 3f, 16.5f, 16.5f);
                }

                // 3. Carimbo / Selo Técnico (Canto Inferior Direito)
                using (var titleBrush = new SolidBrush(Color.FromArgb(226, 232, 240)))
                using (var titlePen = new Pen(Color.FromArgb(71, 85, 105), 0.8f))
                {
                    g.FillRectangle(titleBrush, 12f, 14.5f, 7.5f, 5f);
                    g.DrawRectangle(titlePen, 12f, 14.5f, 7.5f, 5f);
                }
                using (var linePen = new Pen(Color.FromArgb(100, 116, 139), 0.6f))
                {
                    g.DrawLine(linePen, 13f, 16.5f, 18.5f, 16.5f);
                    g.DrawLine(linePen, 13f, 18f, 17f, 18f);
                }

                // 4. Quadrantes de Vistas (Auto-Layout 2x2 com Cor Ciano Glaux)
                using (var quadPen = new Pen(Color.FromArgb(14, 165, 233), 0.8f))
                using (var geomPen = new Pen(Color.FromArgb(30, 41, 59), 0.8f))
                {
                    // Vista 1 (Superior Esquerda: Isométrica / Cubo)
                    g.DrawRectangle(quadPen, 4f, 4f, 6.5f, 4.5f);
                    g.DrawLine(geomPen, 5.5f, 7f, 7.2f, 5f);
                    g.DrawLine(geomPen, 7.2f, 5f, 9f, 6f);
                    g.DrawLine(geomPen, 5.5f, 7f, 7.2f, 8f);

                    // Vista 2 (Superior Direita: Seta do Norte e Rosa dos Ventos)
                    g.DrawRectangle(quadPen, 12f, 4f, 6.5f, 4.5f);
                    using (var northBrush = new SolidBrush(Color.FromArgb(239, 68, 68)))
                    {
                        PointF[] northArrow = new PointF[] {
                            new PointF(15.2f, 4.8f),
                            new PointF(13.8f, 7.5f),
                            new PointF(15.2f, 6.8f),
                            new PointF(16.6f, 7.5f)
                        };
                        g.FillPolygon(northBrush, northArrow);
                    }

                    // Vista 3 (Inferior Esquerda: Planta / Curvas)
                    g.DrawRectangle(quadPen, 4f, 10f, 6.5f, 4.5f);
                    using (var wavePen = new Pen(Color.FromArgb(16, 185, 129), 0.8f))
                    {
                        g.DrawArc(wavePen, 4.8f, 10.8f, 5f, 3f, 0, 180);
                    }

                    // Vista 4 (Central Direita: Corte)
                    g.DrawRectangle(quadPen, 12f, 10f, 6.5f, 3.5f);
                    g.DrawLine(geomPen, 13f, 12.5f, 17.5f, 12.5f);
                }
            }
            return bmp;
        }
    }
}


