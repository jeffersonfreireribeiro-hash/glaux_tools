using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
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
    /// Line Chart &amp; Statistics (e Histogram). Fluxo: entrada GH → pares X/Y validados → estatísticas (dados ORIGINAIS)
    /// → ChartScene (ChartLineModel.cs) → Canvas / PNG (ChartLineRenderer.cs) / geometria do Rhino. Nenhuma superfície recalcula dados.
    /// </summary>
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

        // Estado exibido (Canvas e exportação)
        public List<ChartSeries> DisplaySeries = new List<ChartSeries>();
        public ChartScene Scene;
        public string DisplayTitle = "Line Chart & Statistics";
        public string DisplayXLabel = "X Axis";
        public string DisplayYLabel = "Y Axis";
        public bool DisplayShowStats = true;
        public bool ShowCombinedCurve = true;
        public ChartLineKind Kind = ChartLineKind.Lines;
        public ChartLineMode LineMode = ChartLineMode.Raw;
        public int TrendWindow;
        public ChartRepeatMode RepeatMode = ChartRepeatMode.Raw;   // tratamento de X repetido (malhas espaciais)
        public bool ShowSamples = true;                              // com agregação: mostrar também as amostras originais
        public bool ProfilePlan = true;                              // perfil: distâncias em planta (XY); false = 3D
        public string RepeatNote = "";                               // texto informativo sobre X repetido / perfil (relatório e legenda)
        public bool ProfileActive;                                   // X = distância ao longo da Section Curve
        public Bitmap CachedChartBmp;

        /// <summary>Compatibilidade com o contrato antigo (Mode 1 = colunas/histograma).</summary>
        public bool IsColumnsMode { get => Kind == ChartLineKind.Distribution; set { Kind = value ? ChartLineKind.Distribution : ChartLineKind.Lines; } }

        // Metas / Target e Tolerância
        public double? TargetValue = null;
        public double TargetMin = 0;
        public double TargetMax = 0;
        public double? DeltaTarget = null;

        public HistogramData CachedHistData = null;

        public ChartLine_Component()
            : base(
                "Line Chart & Statistics",
                "ChartLine",
                "Gera gráficos 2D de alta definição: linhas X/Y (Raw = pontos reais · Smooth = PCHIP sem overshoot · Trend = média móvel), histograma estatístico (observações em Y → bins + KDE) ou barras XY (X = posição, Y = altura). Múltiplas séries (DataTree), valor alvo com tolerância e estatísticas calculadas sempre sobre os valores originais.",
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
            pManager.AddNumberParameter("X Values", "X", "Valores do eixo X (Lista ou Árvore): X[i] ↔ Y[i] por ramo. Os pares são ordenados juntos por X. Se omitido, X = índice 0, 1, 2... Um único ramo X vale para todos os ramos Y. Contagens diferentes de Y = erro. No modo Distribution, X é ignorado.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Y Values", "Y", "Valores do eixo Y (Lista ou Árvore). Cada ramo representa uma série independente. No modo Distribution, são as OBSERVAÇÕES do histograma.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Title", "T", "Título principal do gráfico.", GH_ParamAccess.item, "Line Chart & Statistics");
            pManager.AddTextParameter("X Label", "XLab", "Rótulo/Nome do eixo X.", GH_ParamAccess.item, "X Axis");
            pManager.AddTextParameter("Y Label", "YLab", "Rótulo/Nome do eixo Y.", GH_ParamAccess.item, "Y Axis");
            pManager.AddBooleanParameter("Show Stats", "Stats", "Exibir linhas de referência estatística (Média, Mediana e faixa ±1σ, calculadas sobre os valores originais). Sem conexão, vale a opção do menu.", GH_ParamAccess.item, true);
            pManager.AddIntegerParameter("Width", "W", "Largura da imagem exportada em pixels.", GH_ParamAccess.item, 900);
            pManager.AddIntegerParameter("Height", "H", "Altura da imagem exportada em pixels.", GH_ParamAccess.item, 550);

            pManager.AddGenericParameter("Chart Mode", "Mode", "Tipo/modo do gráfico. 'Lines' ou 0: linhas X/Y; 'Raw' / 'Smooth' / 'Trend': linhas com o modo escolhido (Raw = padrão, pontos reais); 'Histogram' ou 1: histograma ESTATÍSTICO (observações Y → bins + KDE); 'XY' / 'XYBars' ou 2: barras com X = posição e Y = altura. Sem conexão, vale o menu.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Combined Curve", "Combined", "Linhas com 2 ou mais séries: sobrepõe a média agregada por X (curva tracejada). Distribution: sobrepõe a curva KDE. Sem conexão, vale a opção do menu.", GH_ParamAccess.item, true);
            pManager.AddGenericParameter("Target Value", "Target", "Valor alvo opcional ou faixa ideal (ex.: 1.40, ou Interval(1.2, 1.6), ou '1.2 To 1.6'). Plota a linha do alvo ('Id'), a faixa de tolerância ('Tol') e o badge de desvio 'Δ'.", GH_ParamAccess.item);

            // Entradas adicionadas na v1.5.0 (no fim, compatíveis com arquivos antigos)
            pManager.AddNumberParameter("Group Tolerance", "GTol", "Tolerância para considerar X iguais (mesma unidade de X). 0 ou omitida = automática (separa ruído numérico da estrutura da malha e nunca funde colunas distintas). Usada quando há X repetido.", GH_ParamAccess.item, 0.0);
            pManager.AddPointParameter("Sample Points", "Pt", "Perfil espacial (opcional): posição 3D de cada amostra, mesma estrutura de Y (um ponto por valor). Com Section Curve conectada, X do gráfico = distância ao longo do perfil.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Section Curve", "Sec", "Perfil espacial (opcional): linha/curva de seção. Só as amostras a menos de Section Tolerance da curva entram, ordenadas pela distância ao longo dela.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Section Tolerance", "STol", "Distância máxima da amostra à curva de seção (unidades do documento). 0 ou omitida = automática (metade do espaçamento típico da malha).", GH_ParamAccess.item, 0.0);

            for (int i = 0; i < pManager.ParamCount; i++) if (i != 1) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Chart Image", "Img", "Imagem renderizada do gráfico (System.Drawing.Bitmap).", GH_ParamAccess.item);
            pManager.AddTextParameter("Stats Report", "Rep", "Relatório com Média, Mediana, Moda (aprox.), Desvio Padrão, Extremos, Alvo, modo de linha e diagnósticos de dados por série.", GH_ParamAccess.item);
            pManager.AddPointParameter("Series Points", "Pts", "Árvore de pontos 3D (X, Y, 0) por série, ordenados por X (pares X/Y preservados). Distribution: centros dos bins × contagem. XYBars: topo de cada barra.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Series Curves", "Crv", "Curva de cada série conforme o modo: Raw = polilinha dos pontos reais; Smooth = cadeia de Béziers PCHIP (passa pelos pontos, sem overshoot); Trend = polilinha da tendência. Distribution/XYBars: retângulos das barras.", GH_ParamAccess.tree);
            pManager.AddLineParameter("Reference Lines", "Refs", "Linhas de referência (Média, Mediana, Moda, +1σ, −1σ por série; com 2+ séries o ramo {998} traz as linhas do conjunto; Alvo no ramo {999}).", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Combined Curve", "Trend", "1 série: curva de TENDÊNCIA (média móvel ponderada nos X originais). 2+ séries: média agregada por X (se Combined). Distribution: curva KDE. Polilinhas — sem interpolação cúbica.", GH_ParamAccess.item);
            // Saídas adicionadas na v1.5.0 (no fim)
            pManager.AddPointParameter("Group Points", "GPts", "Pontos AGREGADOS por X: (X do grupo, valor do modo Repeated X — média, mediana, mín., máx.), uma árvore por série. Os pontos ORIGINAIS continuam em Pts.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Group Table", "Groups", "Tabela CSV (;) por série dos grupos de X: Series;Group;X;Count;Mean;Median;Min;Max;StdDev;OriginalIndices (rastreia as amostras originais de cada grupo).", GH_ParamAccess.tree);
        }

        // ------------------------------------------------------------------------------------ Solve
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(1, out GH_Structure<GH_Number> yTree) || yTree == null || yTree.IsEmpty)
            {
                Message = "Sem Dados Y";
                DisplaySeries = new List<ChartSeries>(); Scene = null; CachedHistData = null; CachedChartBmp = null;
                return;
            }
            DA.GetDataTree(0, out GH_Structure<GH_Number> xTree);

            string title = "Line Chart & Statistics"; DA.GetData(2, ref title); DisplayTitle = title;
            string xLabel = "X Axis"; DA.GetData(3, ref xLabel); DisplayXLabel = xLabel;
            string yLabel = "Y Axis"; DA.GetData(4, ref yLabel); DisplayYLabel = yLabel;

            // Stats / Combined: o valor conectado vence; sem conexão vale o menu (o padrão persistente do parâmetro não sobrescreve o menu)
            if (Params.Input[5].SourceCount > 0) { bool b = DisplayShowStats; if (DA.GetData(5, ref b)) DisplayShowStats = b; }
            if (Params.Input[9].SourceCount > 0) { bool b = ShowCombinedCurve; if (DA.GetData(9, ref b)) ShowCombinedCurve = b; }

            int width = 900; DA.GetData(6, ref width); width = Math.Max(300, Math.Min(4000, width));
            int height = 550; DA.GetData(7, ref height); height = Math.Max(200, Math.Min(3000, height));

            object rawMode = null;
            if (DA.GetData(8, ref rawMode) && rawMode != null)
            {
                if (TryParseMode(rawMode, out ChartLineKind? k, out ChartLineMode? lm, out ChartRepeatMode? rm)) { if (k.HasValue) Kind = k.Value; if (lm.HasValue) LineMode = lm.Value; if (rm.HasValue) RepeatMode = rm.Value; }
                else AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Chart Mode '{rawMode}' não reconhecido; mantido '{Kind}'. Use Lines/Raw/Smooth/Trend, Histogram, XY e, para X repetido, Mean/Median/Min/Max/Range/SD/Samples (combináveis: 'xy mean').");
            }

            double? targetVal = null; double tMin = 0, tMax = 0;
            object rawTarget = null;
            if (DA.GetData(10, ref rawTarget) && rawTarget != null && TryParseTarget(rawTarget, out double pt, out double pMin, out double pMax)) { targetVal = pt; tMin = pMin; tMax = pMax; }
            TargetValue = targetVal; TargetMin = tMin; TargetMax = tMax;

            double groupTol = 0; DA.GetData(11, ref groupTol); if (double.IsNaN(groupTol) || groupTol < 0) groupTol = 0;
            DA.GetDataTree(12, out GH_Structure<GH_Point> ptTree);
            Curve section = null; DA.GetData(13, ref section);
            double sectionTol = 0; DA.GetData(14, ref sectionTol); if (double.IsNaN(sectionTol) || sectionTol < 0) sectionTol = 0;
            RepeatNote = ""; ProfileActive = false;

            // 1) pares X/Y validados (ou perfil espacial), ordenados conjuntamente
            var paths = new List<GH_Path>();
            var seriesList = ReadSeries(xTree, yTree, Kind == ChartLineKind.Distribution, paths, ptTree, section, sectionTol);
            if (seriesList.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum dado numérico válido encontrado.");
                DisplaySeries = new List<ChartSeries>(); Scene = null; CachedHistData = null; CachedChartBmp = null;
                return;
            }

            // 2) estatísticas / histograma (valores originais) e cena
            var allVals = seriesList.SelectMany(s => s.Y).ToList();
            HistogramData hist = Kind == ChartLineKind.Distribution ? ChartHistogram.Compute(allVals, targetVal, tMin, tMax) : null;
            var opts = new ChartSceneOptions { Kind = Kind, Mode = LineMode, ShowStats = DisplayShowStats, Combined = ShowCombinedCurve, Target = targetVal, TargetMin = tMin, TargetMax = tMax, RepeatMode = RepeatMode, ShowSamples = ShowSamples, GroupTolerance = groupTol };
            var scene = ChartSceneBuilder.Build(seriesList, opts, hist);
            ReportRepeatedX(seriesList, scene);

            DisplaySeries = seriesList; CachedHistData = hist; Scene = scene;
            if (targetVal.HasValue) DeltaTarget = (Kind == ChartLineKind.Distribution && hist != null ? hist.Mode : scene.PooledMean) - targetVal.Value; else DeltaTarget = null;
            if (Kind == ChartLineKind.Lines && LineMode == ChartLineMode.Trend)
            {
                var g0 = seriesList[0].Groups;
                ChartCurves.MovingTrend(g0.Select(g => g.X).ToArray(), g0.Select(g => g.Value(RepeatMode)).ToArray(), opts.TrendSpan, out int win); TrendWindow = win;
            }

            // 3) PNG (mesma cena que o canvas)
            CachedChartBmp = RenderChart(scene, seriesList, hist, title, xLabel, yLabel, width, height);

            // 4) relatório + geometria do Rhino
            var rep = BuildReport(seriesList, hist, scene, title, targetVal, tMin, tMax);
            BuildRhinoOutputs(DA, seriesList, paths, hist, scene, opts, targetVal, out var outPts, out var outCrv, out var outRefs, out Curve outTrend);
            BuildGroupOutputs(seriesList, paths, out var outGPts, out var outGroups);

            string sCount = seriesList.Count == 1 ? "1 Série" : $"{seriesList.Count} Séries";
            string head = Kind == ChartLineKind.Distribution ? "Histograma" : Kind == ChartLineKind.XYBars ? "Barras XY" : $"{sCount} · {LineMode}";
            Message = targetVal.HasValue ? $"{head} | Δ: {DeltaTarget:F2}" : head;

            DA.SetData(0, CachedChartBmp);
            DA.SetData(1, rep);
            DA.SetDataTree(2, outPts);
            DA.SetDataTree(3, outCrv);
            DA.SetDataTree(4, outRefs);
            if (outTrend != null) DA.SetData(5, outTrend);
            DA.SetDataTree(6, outGPts);
            DA.SetDataTree(7, outGroups);
        }

        /// <summary>Informação (não erro) sobre X repetido: comum em malhas espaciais. Registra grupos, tolerância e amostras retidas.</summary>
        private void ReportRepeatedX(List<ChartSeries> series, ChartScene scene)
        {
            if (Kind == ChartLineKind.Distribution) return;
            var sb = new StringBuilder();
            foreach (var s in series.Where(x => x.HasRepeats))
            {
                string mode = RepeatMode == ChartRepeatMode.Raw ? "Raw (amostras preservadas; somente pontos)" : RepeatMode.ToString();
                string msg = $"Ramo {s.Key}: coordenadas X repetidas detectadas. {s.RepeatedSamples} de {s.Count} amostras compartilham X com outras (comum em malhas espaciais; não é erro). " +
                    $"Modo X repetido: {mode}. Grupos de X: {s.Groups.Count} (tolerância {s.GroupTolerance.ToString("0.#######", CultureInfo.InvariantCulture)}{(s.GroupToleranceAuto ? ", automática" : "")}). Amostras retidas: {s.Count}.";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, msg);
                sb.AppendLine(msg);
            }
            RepeatNote = sb.ToString().Trim();
        }

        private void BuildGroupOutputs(List<ChartSeries> series, List<GH_Path> paths, out GH_Structure<GH_Point> gpts, out GH_Structure<GH_String> table)
        {
            gpts = new GH_Structure<GH_Point>(); table = new GH_Structure<GH_String>();
            if (Kind == ChartLineKind.Distribution) return;
            for (int si = 0; si < series.Count; si++)
            {
                var s = series[si]; var path = paths[si];
                gpts.EnsurePath(path); table.EnsurePath(path);
                table.Append(new GH_String(ChartCsv.GroupsHeader), path);
                foreach (var g in s.Groups) gpts.Append(new GH_Point(new Point3d(g.X, g.Value(RepeatMode), 0)), path);
                foreach (var row in ChartCsv.GroupRows(s)) table.Append(new GH_String(row), path);
            }
        }

        /// <summary>Casamento X↔Y por ramo: mesmo caminho; um único ramo X vale para todos; mesma quantidade de ramos = por ordem. Contagens diferentes = erro explícito.</summary>
        private List<ChartSeries> ReadSeries(GH_Structure<GH_Number> xTree, GH_Structure<GH_Number> yTree, bool ignoreX, List<GH_Path> outPaths, GH_Structure<GH_Point> ptTree, Curve section, double sectionTol)
        {
            bool profile = section != null && ptTree != null && !ptTree.IsEmpty && Kind != ChartLineKind.Distribution;
            if (section != null && (ptTree == null || ptTree.IsEmpty) && Kind != ChartLineKind.Distribution)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Section Curve conectada sem Sample Points: o perfil espacial precisa da posição de cada amostra. Usando X/Y normalmente.");
            if (ptTree != null && !ptTree.IsEmpty && section == null)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Sample Points conectado sem Section Curve: ignorado (X do gráfico = X informado).");
            var result = new List<ChartSeries>();
            bool hasX = xTree != null && !xTree.IsEmpty;
            if (profile && hasX) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Perfil espacial ativo: X do gráfico = distância ao longo da Section Curve; a entrada X é ignorada.");
            if (profile) hasX = false;
            if (hasX && ignoreX) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Modo Distribution: os valores de X são ignorados (histograma estatístico usa só as observações Y). Para pares X/Y explícitos use o modo XY (barras) ou Lines.");
            if (ignoreX) hasX = false;

            int branchIdx = 0;
            foreach (GH_Path path in yTree.Paths)
            {
                int idx = branchIdx++;
                var yBranch = yTree.get_Branch(path);
                if (yBranch == null || yBranch.Count == 0) continue;
                var ys = ToDoubles(yBranch);
                List<double?> xs = null; List<int> srcMap = null;
                if (profile)
                {
                    System.Collections.IList pb = null;
                    if (ptTree.PathExists(path)) pb = ptTree.get_Branch(path);
                    else if (ptTree.PathCount == 1) pb = ptTree.get_Branch(ptTree.Paths[0]);
                    else if (ptTree.PathCount == yTree.PathCount) pb = ptTree.get_Branch(ptTree.Paths[idx]);
                    if (pb == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Ramo Y {path}: não há ramo de Sample Points correspondente (Pt tem {ptTree.PathCount} ramos, Y {yTree.PathCount}). Série ignorada."); continue; }
                    if (pb.Count != yBranch.Count) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Ramo {path}: {pb.Count} pontos e {yBranch.Count} valores. Um ponto por valor é obrigatório; série ignorada."); continue; }
                    var pts = new List<Point3d>(pb.Count);
                    foreach (object o in pb) pts.Add(o is GH_Point gp ? gp.Value : Point3d.Unset);
                    ChartProfile.Result pr;
                    try { pr = ChartProfile.Extract(pts, ys, section, sectionTol, ProfilePlan); }
                    catch (Exception ex) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Perfil espacial: {ex.Message}"); continue; }
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Perfil espacial, ramo {path}: {pr.Distance.Count} de {pts.Count} amostras a menos de {pr.Tolerance.ToString("0.####", CultureInfo.InvariantCulture)} da curva ({(ProfilePlan ? "distância em planta XY" : "distância 3D")}{(pr.ToleranceAuto ? ", tolerância automática" : "")}); {pr.Outside} fora do perfil (não entram no gráfico). Comprimento da seção: {pr.CurveLength.ToString("0.###", CultureInfo.InvariantCulture)}.");
                    if (pr.Distance.Count == 0) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Ramo {path}: nenhuma amostra próxima à curva de seção. Aumente Section Tolerance."); continue; }
                    xs = pr.Distance; ys = pr.Value; srcMap = pr.Source;
                }
                else if (hasX)
                {
                    System.Collections.IList xb = null;
                    if (xTree.PathExists(path)) xb = xTree.get_Branch(path);
                    else if (xTree.PathCount == 1) xb = xTree.get_Branch(xTree.Paths[0]);
                    else if (xTree.PathCount == yTree.PathCount) xb = xTree.get_Branch(xTree.Paths[idx]);
                    if (xb == null)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Ramo Y {path}: X foi informado mas não há ramo X correspondente (X tem {xTree.PathCount} ramos, Y {yTree.PathCount}). Série ignorada — o índice NÃO é usado como X quando X é fornecido.");
                        continue;
                    }
                    if (xb.Count != yBranch.Count)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Ramo {path}: X tem {xb.Count} valores e Y tem {yBranch.Count}. X[i] ↔ Y[i] exige contagens iguais; série ignorada.");
                        continue;
                    }
                    xs = ToDoubles(xb);
                }
                var s = ChartSeriesBuilder.Pair(path.ToString(), $"Série {path}", idx, xs, ys, srcMap);
                if (profile) { s.XExplicit = true; ProfileActive = true; }
                if (s.Count == 0) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Ramo {path}: nenhum par X/Y numérico válido."); continue; }
                if (s.Dropped > 0) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Ramo {path}: {s.Dropped} par(es) descartado(s) por X ou Y não numérico/não finito (o par inteiro é descartado, X e Y nunca desalinham).");
                if (s.InputWasUnsorted) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Ramo {path}: X fora de ordem na entrada — os pares (X,Y) foram ordenados juntos por X.");
                result.Add(s); outPaths.Add(path);
            }
            return result;
        }

        private static List<double?> ToDoubles(System.Collections.IList branch)
        {
            var list = new List<double?>(branch.Count);
            foreach (object o in branch)
            {
                double v;
                if (o != null && GH_Convert.ToDouble(o, out v, GH_Conversion.Both) && !double.IsNaN(v) && !double.IsInfinity(v)) list.Add(v); else list.Add(null);
            }
            return list;
        }

        // ------------------------------------------------------------------------------------ Relatório
        private string BuildReport(List<ChartSeries> series, HistogramData hist, ChartScene scene, string title, double? target, double tMin, double tMax)
        {
            var ci = CultureInfo.InvariantCulture;
            var rep = new StringBuilder();
            string modeText = Kind == ChartLineKind.Distribution ? "Histograma estatístico (bins + KDE; X ignorado)"
                : Kind == ChartLineKind.XYBars ? "Barras XY (X = posição, Y = altura; base em Y = 0)"
                : LineMode == ChartLineMode.Raw ? "Linhas — RAW (pontos reais ordenados por X, sem suavização)"
                : LineMode == ChartLineMode.Smooth ? "Linhas — SMOOTH (PCHIP monotônica: passa pelos pontos, sem overshoot)"
                : $"Linhas — TREND (média móvel ponderada tricúbica, janela de {TrendWindow} pontos; dados originais sobrepostos)";
            rep.AppendLine("=================================================");
            rep.AppendLine("       GLAUX CHART & STATISTICS REPORT");
            rep.AppendLine($"       Título: {title}");
            rep.AppendLine($"       Modo:   {modeText}");
            rep.AppendLine($"       Séries: {series.Count} ({series.Sum(s => s.Count)} pontos válidos)");
            rep.AppendLine("       Estatísticas: calculadas sobre os valores ORIGINAIS (nunca sobre pontos suavizados/interpolados).");
            if (target.HasValue)
            {
                rep.AppendLine($"       Meta Ideal (Alvo): {target.Value.ToString("F4", ci)} (Faixa Tol: [{tMin.ToString("F4", ci)} .. {tMax.ToString("F4", ci)}])");
                if (DeltaTarget.HasValue) rep.AppendLine($"       Desvio Δ:          {DeltaTarget.Value.ToString("+0.00;-0.00;0.00", ci)}");
            }
            rep.AppendLine("=================================================");

            if (Kind == ChartLineKind.Distribution && hist != null)
            {
                rep.AppendLine();
                rep.AppendLine("--- [DISTRIBUIÇÃO HISTOGRAMA & KDE] ---");
                rep.AppendLine($"  Observações (N):     {hist.TotalCount}  (soma das contagens dos bins = {hist.BinCounts.Sum()})");
                rep.AppendLine($"  Média (μ):           {hist.Mean.ToString("F4", ci)}");
                rep.AppendLine($"  Mediana (Q2):        {hist.Median.ToString("F4", ci)}");
                rep.AppendLine($"  Moda (Mo, aprox.):   {hist.Mode.ToString("F4", ci)}");
                rep.AppendLine($"  Desvio Padrão (σ):   {hist.StdDev.ToString("F4", ci)}");
                rep.AppendLine($"  Número de Bins:      {hist.BinCounts.Length}");
                rep.AppendLine($"  Largura do Bin:      {hist.BinWidth.ToString("F4", ci)}  (bin b = [e_b, e_b+1); último fechado)");
                rep.AppendLine("  Limites dos bins:    " + string.Join(" | ", hist.BinEdges.Select(e => e.ToString("0.####", ci))));
                rep.AppendLine("  Contagens:           " + string.Join(" | ", hist.BinCounts));
                return rep.ToString();
            }

            foreach (var s in series)
            {
                rep.AppendLine();
                rep.AppendLine($"--- [{s.Name}] (N = {s.Count}{(s.XExplicit ? "" : ", X = índice")}) ---");
                rep.AppendLine($"  Média (μ):           {s.Mean.ToString("F4", ci)}");
                rep.AppendLine($"  Mediana (Q2):        {s.Median.ToString("F4", ci)}");
                rep.AppendLine($"  Moda (Mo, aprox.):   {s.Mode.ToString("F4", ci)}");
                rep.AppendLine($"  Desvio Padrão (σ):   {s.StdDev.ToString("F4", ci)}");
                rep.AppendLine($"  Variância (σ²):      {s.Variance.ToString("F4", ci)}");
                rep.AppendLine($"  Mínimo:              {s.MinY.ToString("F4", ci)}");
                rep.AppendLine($"  Máximo:              {s.MaxY.ToString("F4", ci)}");
                rep.AppendLine($"  Faixa [μ-σ, μ+σ]:    [{(s.Mean - s.StdDev).ToString("F4", ci)}, {(s.Mean + s.StdDev).ToString("F4", ci)}]");
                if (s.Dropped > 0) rep.AppendLine($"  Pares descartados:   {s.Dropped}");
                if (s.InputWasUnsorted) rep.AppendLine("  X fora de ordem na entrada: pares ordenados conjuntamente.");
                if (s.Groups != null && Kind != ChartLineKind.Distribution)
                {
                    rep.AppendLine($"  Amostras originais:  {s.Count} (todas preservadas)");
                    rep.AppendLine($"  Grupos de X:         {s.Groups.Count} (tolerância {s.GroupTolerance.ToString("0.#######", ci)}{(s.GroupToleranceAuto ? ", automática" : ", definida pelo usuário")})");
                    if (s.HasRepeats)
                    {
                        var cnt = s.Groups.Select(g => g.Count).OrderBy(v => v).ToList();
                        rep.AppendLine($"  Amostras por grupo:  mín {cnt.First()} | mediana {cnt[cnt.Count / 2]} | máx {cnt.Last()}  ({s.RepeatedSamples} amostras em grupos com X repetido)");
                        rep.AppendLine($"  Modo X repetido:     {RepeatMode}{(RepeatMode == ChartRepeatMode.Raw ? " (amostras preservadas; somente pontos)" : "")}");
                        rep.AppendLine($"  Média GLOBAL (todas as amostras):   {s.Mean.ToString("F4", ci)}");
                        rep.AppendLine($"  Média das médias por X:             {s.Groups.Average(g => g.Mean).ToString("F4", ci)}  (difere da global quando os grupos têm quantidades diferentes de amostras)");
                        int maxRows = 40;
                        rep.AppendLine("  Grupos (X | N | média | mediana | mín | máx | σ):");
                        foreach (var g in s.Groups.Take(maxRows))
                            rep.AppendLine($"    {g.X.ToString("0.####", ci)} | {g.Count} | {g.Mean.ToString("F3", ci)} | {g.Median.ToString("F3", ci)} | {g.Min.ToString("F3", ci)} | {g.Max.ToString("F3", ci)} | {g.StdDev.ToString("F3", ci)}");
                        if (s.Groups.Count > maxRows) rep.AppendLine($"    … +{s.Groups.Count - maxRows} grupos (tabela completa na saída Groups)");
                    }
                }
            }
            if (series.Count > 1)
                rep.AppendLine($"\n  Conjunto (linhas de referência do gráfico): N = {scene.PooledCount}, μ = {scene.PooledMean.ToString("F4", ci)}, mediana = {scene.PooledMedian.ToString("F4", ci)}, σ = {scene.PooledStdDev.ToString("F4", ci)}");
            if (Kind == ChartLineKind.XYBars)
                rep.AppendLine($"\n  Barras: largura do espaço por X = {scene.BarSlotWidth.ToString("0.####", ci)} (0,8 × menor espaçamento entre X); várias séries lado a lado; base em Y = 0.");
            return rep.ToString();
        }

        // ------------------------------------------------------------------------------------ Saídas do Rhino
        private void BuildRhinoOutputs(IGH_DataAccess DA, List<ChartSeries> series, List<GH_Path> paths, HistogramData hist, ChartScene scene, ChartSceneOptions opts, double? target,
            out GH_Structure<GH_Point> pts, out GH_Structure<GH_Curve> crvs, out GH_Structure<GH_Line> refs, out Curve trend)
        {
            pts = new GH_Structure<GH_Point>(); crvs = new GH_Structure<GH_Curve>(); refs = new GH_Structure<GH_Line>(); trend = null;

            if (Kind == ChartLineKind.Distribution && hist != null)
            {
                var hp = new GH_Path(0); pts.EnsurePath(hp); crvs.EnsurePath(hp); refs.EnsurePath(hp);
                for (int b = 0; b < hist.BinCounts.Length; b++)
                {
                    double x0 = hist.BinEdges[b], x1 = hist.BinEdges[b + 1], by = hist.BinCounts[b];
                    pts.Append(new GH_Point(new Point3d(hist.BinCenters[b], by, 0)), hp);
                    crvs.Append(new GH_Curve(new Polyline(new[] { new Point3d(x0, 0, 0), new Point3d(x0, by, 0), new Point3d(x1, by, 0), new Point3d(x1, 0, 0), new Point3d(x0, 0, 0) }).ToNurbsCurve()), hp);
                }
                if (target.HasValue) refs.Append(new GH_Line(new Line(new Point3d(target.Value, 0, 0), new Point3d(target.Value, hist.MaxBinCount, 0))), hp);
                refs.Append(new GH_Line(new Line(new Point3d(hist.Mode, 0, 0), new Point3d(hist.Mode, hist.MaxBinCount, 0))), hp);
                if (hist.KdePoints != null && hist.KdePoints.Length > 1) trend = new Polyline(hist.KdePoints.Select(p => new Point3d(p.X, p.Y, 0))).ToNurbsCurve();
                return;
            }

            for (int si = 0; si < series.Count; si++)
            {
                var s = series[si]; var path = paths[si];
                pts.EnsurePath(path); crvs.EnsurePath(path); refs.EnsurePath(path);
                for (int i = 0; i < s.Count; i++) pts.Append(new GH_Point(new Point3d(s.X[i], s.Y[i], 0)), path);

                if (Kind == ChartLineKind.XYBars)
                {
                    foreach (var bar in scene.Bars.Where(b => b.SeriesIndex == s.Index))
                    {
                        crvs.Append(new GH_Curve(new Polyline(new[] { new Point3d(bar.X0, bar.Base, 0), new Point3d(bar.X0, bar.Value, 0), new Point3d(bar.X1, bar.Value, 0), new Point3d(bar.X1, bar.Base, 0), new Point3d(bar.X0, bar.Base, 0) }).ToNurbsCurve()), path);
                    }
                }
                else if (s.Count > 1)
                {
                    Curve c = null;
                    bool rep = s.HasRepeats;
                    double[] ux = s.Groups.Select(g => g.X).ToArray(), uy = s.Groups.Select(g => g.Value(RepeatMode)).ToArray();
                    if (rep && RepeatMode == ChartRepeatMode.Raw && LineMode == ChartLineMode.Raw) c = null;     // X repetido em Raw: somente pontos (sem zigue-zague)
                    else if (LineMode == ChartLineMode.Smooth && ux.Length >= 2) c = BezierChain(ux, uy);
                    else if (LineMode == ChartLineMode.Trend && ux.Length >= 2) c = ToPolyline(ChartCurves.MovingTrend(ux, uy, opts.TrendSpan, out int _));
                    else if (rep) c = ToPolyline(ux.Select((x, i) => new ChartPt(x, uy[i])).ToArray());
                    else c = ToPolyline(s.X.Select((x, i) => new ChartPt(x, s.Y[i])).ToArray());
                    if (c != null) crvs.Append(new GH_Curve(c), path);
                }

                if (DisplayShowStats)
                {
                    double xs = s.X.Min(), xe = s.X.Max(); if (Math.Abs(xs - xe) < 1e-6) { xs -= 1.0; xe += 1.0; }
                    foreach (double yv in new[] { s.Mean, s.Median, s.Mode, s.Mean + s.StdDev, s.Mean - s.StdDev })
                        refs.Append(new GH_Line(new Line(new Point3d(xs, yv, 0), new Point3d(xe, yv, 0))), path);
                }
            }

            if (DisplayShowStats && series.Count > 1)
            {
                var gp = new GH_Path(998); refs.EnsurePath(gp);
                double xs = scene.MinX, xe = scene.MaxX;
                foreach (double yv in new[] { scene.PooledMean, scene.PooledMedian, scene.PooledMean + scene.PooledStdDev, scene.PooledMean - scene.PooledStdDev })
                    refs.Append(new GH_Line(new Line(new Point3d(xs, yv, 0), new Point3d(xe, yv, 0))), gp);
            }
            if (target.HasValue) { var tp = new GH_Path(999); refs.EnsurePath(tp); refs.Append(new GH_Line(new Line(new Point3d(scene.MinX, target.Value, 0), new Point3d(scene.MaxX, target.Value, 0))), tp); }

            if (Kind == ChartLineKind.Lines)
            {
                if (series.Count == 1 && series[0].Count > 1)
                {
                    double[] ux = series[0].Groups.Select(g => g.X).ToArray(), uy = series[0].Groups.Select(g => g.Value(RepeatMode)).ToArray();
                    if (ux.Length >= 2) trend = ToPolyline(ChartCurves.MovingTrend(ux, uy, opts.TrendSpan, out int _));
                }
                else if (series.Count > 1 && ShowCombinedCurve)
                    trend = ToPolyline(scene.Polylines.Where(p => p.Role == ChartLineRole.Aggregate).Select(p => p.Pts).FirstOrDefault());
            }
        }

        private static Curve ToPolyline(ChartPt[] p)
        {
            if (p == null || p.Length < 2) return null;
            return new Polyline(p.Select(q => new Point3d(q.X, q.Y, 0))).ToNurbsCurve();
        }

        private static Curve BezierChain(double[] ux, double[] uy)
        {
            try
            {
                var pc = new PolyCurve();
                foreach (var seg in ChartCurves.PchipBezier(ux, uy))
                {
                    var nc = new BezierCurve(seg.Select(p => new Point3d(p.X, p.Y, 0)).ToArray()).ToNurbsCurve();
                    if (nc != null) pc.Append(nc);
                }
                if (pc.IsValid) return pc;
            }
            catch { }
            return ToPolyline(ChartCurves.PchipSample(ux, uy, 16));
        }

        // ------------------------------------------------------------------------------------ PNG (mesma cena do canvas)
        private Bitmap RenderChart(ChartScene scene, List<ChartSeries> series, HistogramData hist, string title, string xLabel, string yLabel, int w, int h)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                int padLeft = 80, padRight = 180, padTop = 60, padBottom = 60;
                var plot = new RectangleF(padLeft, padTop, w - padLeft - padRight, h - padTop - padBottom);
                FontFamily fam = GetUIFontFamily();
                using (var bg = new SolidBrush(Color.FromArgb(248, 250, 252))) g.FillRectangle(bg, 0, 0, w, h);

                ChartLineRenderer.DrawPlot(g, plot, scene, ChartTheme.Light(1f), fam);

                using (var f = new Font(fam, 14f, FontStyle.Bold)) using (var b = new SolidBrush(Color.FromArgb(15, 23, 42))) g.DrawString(title, f, b, padLeft, 20);

                // rótulos dos eixos
                using (var f = new Font(fam, 9f, FontStyle.Regular)) using (var b = new SolidBrush(Color.FromArgb(71, 85, 105)))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center })
                {
                    string xl = Kind == ChartLineKind.Distribution && xLabel == "X Axis" ? "Valor (classes)" : (ProfileActive && xLabel == "X Axis" ? "Distância ao longo do perfil" : xLabel);
                    string yl = Kind == ChartLineKind.Distribution && yLabel == "Y Axis" ? "Frequência (contagem)" : yLabel;
                    g.DrawString(xl, f, b, plot.Left + plot.Width / 2f, plot.Bottom + 26, sf);
                    var st = g.Save(); g.TranslateTransform(18, plot.Top + plot.Height / 2f); g.RotateTransform(-90); g.DrawString(yl, f, b, 0, 0, sf); g.Restore(st);
                }

                // badge de desvio
                if (TargetValue.HasValue && DeltaTarget.HasValue)
                {
                    bool ok = Math.Abs(DeltaTarget.Value) <= (TargetMax - TargetMin) * 0.5;
                    var rect = new RectangleF(plot.Right - 90, 18, 90, 26);
                    using (var bb = new SolidBrush(ok ? Color.FromArgb(20, 16, 185, 129) : Color.FromArgb(30, 234, 88, 12)))
                    using (var bp = new Pen(ok ? Color.FromArgb(16, 185, 129) : Color.FromArgb(234, 88, 12), 1.2f))
                    using (var bf = new Font(fam, 9f, FontStyle.Bold)) using (var bt = new SolidBrush(ok ? Color.FromArgb(16, 140, 95) : Color.FromArgb(234, 88, 12)))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.FillRectangle(bb, rect); g.DrawRectangle(bp, rect.X, rect.Y, rect.Width, rect.Height);
                        g.DrawString($"Δ: {DeltaTarget.Value.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)}", bf, bt, rect, sf);
                    }
                }
                DrawLegend(g, fam, (int)plot.Right + 16, (int)plot.Top + 5, scene, series, hist);
            }
            return bmp;
        }

        private void DrawLegend(Graphics g, FontFamily fam, int x, int y, ChartScene scene, List<ChartSeries> series, HistogramData hist)
        {
            var ci = CultureInfo.InvariantCulture;
            using (var fTitle = new Font(fam, 9f, FontStyle.Bold)) using (var fName = new Font(fam, 8.5f, FontStyle.Bold)) using (var fStat = new Font(fam, 7.5f, FontStyle.Regular))
            using (var tb = new SolidBrush(Color.FromArgb(51, 65, 85))) using (var sb = new SolidBrush(Color.FromArgb(100, 115, 130)))
            {
                g.DrawString("LEGENDA", fTitle, tb, x, y);
                int cy = y + 22;
                Action<Color, string, string, DashStyle?> row = (c, name, sub, dash) =>
                {
                    using (var pen = new Pen(c, 2.2f)) { if (dash.HasValue) pen.DashStyle = dash.Value; g.DrawLine(pen, x, cy + 6, x + 12, cy + 6); }
                    g.DrawString(name, fName, tb, x + 17, cy - 1); cy += 15;
                    if (!string.IsNullOrEmpty(sub)) { g.DrawString(sub, fStat, sb, x + 17, cy); cy += 14; }
                };

                if (Kind == ChartLineKind.Distribution && hist != null)
                {
                    row(Color.FromArgb(37, 99, 235), "Histograma", $"N = {hist.TotalCount} | {hist.BinCounts.Length} bins", null);
                    row(Color.FromArgb(0, 190, 235), "Curva KDE", null, null);
                    row(Color.FromArgb(37, 99, 235), $"μ = {hist.Mean.ToString("F2", ci)}", null, DashStyle.Dash);
                    row(Color.FromArgb(168, 85, 247), $"Med = {hist.Median.ToString("F2", ci)}", $"σ = {hist.StdDev.ToString("F2", ci)}", DashStyle.DashDot);
                    row(Color.FromArgb(234, 88, 12), $"Mo ≈ {hist.Mode.ToString("F2", ci)}", null, DashStyle.Dot);
                    return;
                }
                for (int i = 0; i < Math.Min(series.Count, 7); i++)
                {
                    var s = series[i];
                    row(Palette[s.Index % Palette.Length], s.Name, $"N={s.Count} μ={s.Mean.ToString("F2", ci)} σ={s.StdDev.ToString("F2", ci)}", null);
                }
                if (series.Count > 7) { g.DrawString($"… +{series.Count - 7} séries", fStat, sb, x, cy); cy += 14; }
                cy += 4;
                if (Kind == ChartLineKind.Lines)
                {
                    string ln = LineMode == ChartLineMode.Raw ? "RAW (pontos reais)" : LineMode == ChartLineMode.Smooth ? "SMOOTH (PCHIP)" : "TREND (média móvel)";
                    if (scene.AnyPointsOnly) ln = "somente pontos (X repetido, Raw)";
                    else if (scene.AnyAggregated) ln = $"{RepeatLabelShort(RepeatMode)} por X · {ln}";
                    cy += (int)DrawNote(g, "Linha: " + ln, fStat, sb, x, cy, 150f) + 2;
                }
                else cy += (int)DrawNote(g, scene.AnyAggregated ? $"Barras: {RepeatLabelShort(RepeatMode)} por X; base em Y = 0" : "Barras: base em Y = 0", fStat, sb, x, cy, 150f) + 2;
                if (scene.BandSeries.Count > 0) cy += (int)DrawNote(g, RepeatMode == ChartRepeatMode.Range ? "Faixa: mínimo–máximo" : "Faixa: média ± σ", fStat, sb, x, cy, 150f) + 2;
                if (scene.AnyAggregated && ShowSamples && scene.SamplesTotal > 0) cy += (int)DrawNote(g, $"Pontos: amostras originais ({scene.SamplesTotal})", fStat, sb, x, cy, 150f) + 2;
                else if (scene.AnyPointsOnly) cy += (int)DrawNote(g, $"Pontos: amostras ({scene.SamplesTotal})", fStat, sb, x, cy, 150f) + 2;
                cy += 4;
                if (scene.Polylines.Any(p => p.Role == ChartLineRole.Aggregate)) row(Color.FromArgb(2, 132, 199), "Média por X", "agregada das séries", DashStyle.Dash);
                if (scene.Refs.Any(r => r.Kind == ChartRefKind.Mean))
                {
                    row(Color.FromArgb(37, 99, 235), $"μ = {scene.PooledMean.ToString("F2", ci)}", null, DashStyle.Dash);
                    row(Color.FromArgb(168, 85, 247), $"Med = {scene.PooledMedian.ToString("F2", ci)}", $"σ = {scene.PooledStdDev.ToString("F2", ci)}", DashStyle.DashDot);
                }
                if (TargetValue.HasValue) row(Color.FromArgb(16, 185, 129), $"Id = {TargetValue.Value.ToString("F2", ci)}", null, DashStyle.Dash);
            }
        }

        private static float DrawNote(Graphics g, string text, Font f, Brush b, float x, float y, float w)
        {
            var rect = new RectangleF(x, y, w, 200f);
            SizeF sz = g.MeasureString(text, f, (int)w);
            g.DrawString(text, f, b, new RectangleF(x, y, w, sz.Height + 2));
            return sz.Height;
        }

        // ------------------------------------------------------------------------------------ Menu & serialização
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendSeparator(menu);
            var miMode = Menu_AppendItem(menu, "Tipo do Gráfico (Chart Mode)");
            Menu_AppendItem(miMode.DropDown, "Linhas X/Y (Lines)", (s, e) => SetKind(ChartLineKind.Lines), true, Kind == ChartLineKind.Lines);
            Menu_AppendItem(miMode.DropDown, "Histograma estatístico (Distribution): observações em Y", (s, e) => SetKind(ChartLineKind.Distribution), true, Kind == ChartLineKind.Distribution);
            Menu_AppendItem(miMode.DropDown, "Barras XY (X = posição, Y = altura)", (s, e) => SetKind(ChartLineKind.XYBars), true, Kind == ChartLineKind.XYBars);

            var miLine = Menu_AppendItem(menu, "Modo da Linha (Line Mode)");
            Menu_AppendItem(miLine.DropDown, "Raw — pontos reais, sem suavização", (s, e) => SetLineMode(ChartLineMode.Raw), true, LineMode == ChartLineMode.Raw);
            Menu_AppendItem(miLine.DropDown, "Smooth — PCHIP (passa pelos pontos, sem overshoot)", (s, e) => SetLineMode(ChartLineMode.Smooth), true, LineMode == ChartLineMode.Smooth);
            Menu_AppendItem(miLine.DropDown, "Trend — tendência (média móvel) sobre os dados", (s, e) => SetLineMode(ChartLineMode.Trend), true, LineMode == ChartLineMode.Trend);

            var miRep = Menu_AppendItem(menu, "X repetido (malhas espaciais)");
            foreach (ChartRepeatMode rm in Enum.GetValues(typeof(ChartRepeatMode)))
            {
                var mode = rm;
                Menu_AppendItem(miRep.DropDown, RepeatLabel(mode), (s, e) => { RecordUndoEvent("Repeated X Mode"); RepeatMode = mode; ExpireSolution(true); }, true, RepeatMode == mode);
            }
            Menu_AppendItem(menu, "Mostrar amostras originais junto da agregação", (s, e) => { RecordUndoEvent("Show Samples"); ShowSamples = !ShowSamples; ExpireSolution(true); }, true, ShowSamples);
            Menu_AppendItem(menu, "Perfil espacial: distância em planta (XY) — desmarcado = 3D", (s, e) => { RecordUndoEvent("Profile Plan"); ProfilePlan = !ProfilePlan; ExpireSolution(true); }, true, ProfilePlan);
            Menu_AppendItem(menu, "Exportar amostras originais e grupos (CSV)…", (s, e) => ExportCsvDialog());
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Exibir Linhas Estatísticas (μ, Mediana, ±1σ)", (s, e) => { RecordUndoEvent("Toggle Show Stats"); DisplayShowStats = !DisplayShowStats; ExpireSolution(true); }, true, DisplayShowStats);
            Menu_AppendItem(menu, "Exibir Curva Combinada (média por X / KDE)", (s, e) => { RecordUndoEvent("Toggle Combined Curve"); ShowCombinedCurve = !ShowCombinedCurve; ExpireSolution(true); }, true, ShowCombinedCurve);
        }

        public static string RepeatLabel(ChartRepeatMode m)
        {
            switch (m)
            {
                case ChartRepeatMode.Raw: return "Raw — preserva as amostras (somente pontos)";
                case ChartRepeatMode.Mean: return "Mean — média por X";
                case ChartRepeatMode.Median: return "Median — mediana por X";
                case ChartRepeatMode.Min: return "Minimum — menor valor por X";
                case ChartRepeatMode.Max: return "Maximum — maior valor por X";
                case ChartRepeatMode.Range: return "Range — média + faixa mínimo–máximo";
                default: return "Standard Deviation — média ± σ";
            }
        }

        public static string RepeatLabelShort(ChartRepeatMode m)
        {
            switch (m)
            {
                case ChartRepeatMode.Median: return "mediana";
                case ChartRepeatMode.Min: return "mínimo";
                case ChartRepeatMode.Max: return "máximo";
                case ChartRepeatMode.Range: return "média+faixa";
                case ChartRepeatMode.StdDev: return "média±σ";
                default: return "média";
            }
        }

        /// <summary>Grava duas tabelas CSV (;): amostras ORIGINAIS (com o grupo de cada uma) e grupos AGREGADOS. Retorna os dois caminhos.</summary>
        public static string[] WriteCsv(string basePath, IList<ChartSeries> series)
        {
            var enc = new UTF8Encoding(true);
            string sp = basePath + "_amostras.csv", gp = basePath + "_grupos.csv";
            File.WriteAllLines(sp, new[] { ChartCsv.SamplesHeader }.Concat(series.SelectMany(s => ChartCsv.SampleRows(s))), enc);
            File.WriteAllLines(gp, new[] { ChartCsv.GroupsHeader }.Concat(series.SelectMany(s => ChartCsv.GroupRows(s))), enc);
            return new[] { sp, gp };
        }

        public string ExportCsvDialog()
        {
            if (DisplaySeries == null || DisplaySeries.Count == 0) return null;
            try
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Title = "Exportar amostras e grupos - GLAUX Tools";
                    sfd.Filter = "CSV (*.csv)|*.csv";
                    string safe = DisplayTitle.Replace(":", "_").Replace("/", "_").Replace("\\", "_").Trim();
                    sfd.FileName = safe + ".csv";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        string b = Path.Combine(Path.GetDirectoryName(sfd.FileName), Path.GetFileNameWithoutExtension(sfd.FileName));
                        var paths = WriteCsv(b, DisplaySeries);
                        Message = "CSV salvo!";
                        return string.Join("; ", paths);
                    }
                }
            }
            catch (Exception ex) { Rhino.RhinoApp.WriteLine($"[ChartLine] Erro ao exportar CSV: {ex.Message}"); }
            return null;
        }

        private void SetKind(ChartLineKind k) { RecordUndoEvent("Chart Kind"); Kind = k; ExpireSolution(true); }
        private void SetLineMode(ChartLineMode m) { RecordUndoEvent("Line Mode"); LineMode = m; if (Kind != ChartLineKind.Lines) Kind = ChartLineKind.Lines; ExpireSolution(true); }

        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            writer.SetBoolean("IsColumnsMode", IsColumnsMode);   // contrato antigo (arquivos lidos por versões anteriores)
            writer.SetInt32("ChartKind", (int)Kind);
            writer.SetInt32("LineMode", (int)LineMode);
            writer.SetBoolean("DisplayShowStats", DisplayShowStats);
            writer.SetBoolean("ShowCombinedCurve", ShowCombinedCurve);
            writer.SetInt32("RepeatMode", (int)RepeatMode);
            writer.SetBoolean("ShowSamples", ShowSamples);
            writer.SetBoolean("ProfilePlan", ProfilePlan);
            return base.Write(writer);
        }

        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            if (reader.ItemExists("DisplayShowStats")) DisplayShowStats = reader.GetBoolean("DisplayShowStats");
            if (reader.ItemExists("ShowCombinedCurve")) ShowCombinedCurve = reader.GetBoolean("ShowCombinedCurve");
            if (reader.ItemExists("ChartKind")) Kind = (ChartLineKind)Math.Max(0, Math.Min(2, reader.GetInt32("ChartKind")));
            else if (reader.ItemExists("IsColumnsMode")) Kind = reader.GetBoolean("IsColumnsMode") ? ChartLineKind.Distribution : ChartLineKind.Lines;
            if (reader.ItemExists("LineMode")) LineMode = (ChartLineMode)Math.Max(0, Math.Min(2, reader.GetInt32("LineMode")));
            else LineMode = ShowCombinedCurve ? ChartLineMode.Trend : ChartLineMode.Raw;   // arquivo antigo: "Linhas + Tendência" → Trend; sem curva combinada → Raw
            if (reader.ItemExists("RepeatMode")) RepeatMode = (ChartRepeatMode)Math.Max(0, Math.Min(6, reader.GetInt32("RepeatMode"))); else RepeatMode = ChartRepeatMode.Raw;
            ShowSamples = !reader.ItemExists("ShowSamples") || reader.GetBoolean("ShowSamples");
            ProfilePlan = !reader.ItemExists("ProfilePlan") || reader.GetBoolean("ProfilePlan");
            return base.Read(reader);
        }

        public string SavePngDialog()
        {
            if (CachedChartBmp == null) return null;
            try
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Title = "Salvar Gráfico - GLAUX Tools";
                    sfd.Filter = "PNG Image (*.png)|*.png|All files (*.*)|*.*";
                    string safeTitle = DisplayTitle.Replace(":", "_").Replace("/", "_").Replace("\\", "_").Replace("[", "").Replace("]", "").Trim();
                    sfd.FileName = $"{safeTitle}_{(Kind == ChartLineKind.Distribution ? "Histogram" : Kind == ChartLineKind.XYBars ? "XYBars" : "LineChart")}.png";
                    if (sfd.ShowDialog() == DialogResult.OK) { CachedChartBmp.Save(sfd.FileName, ImageFormat.Png); return sfd.FileName; }
                }
            }
            catch (Exception ex) { Rhino.RhinoApp.WriteLine($"[ChartLine] Erro ao salvar imagem: {ex.Message}"); }
            return null;
        }

        public static FontFamily GetUIFontFamily() => Visual.PillVisualKit.UIFontFamily();

        // ------------------------------------------------------------------------------------ Parsing
        /// <summary>
        /// Interpreta o input Mode. bool/0/1 = contrato antigo (false/0 = linhas; true/1 = histograma); 2 ou 'xy'/'xybars'/'bars' = barras XY.
        /// Texto aceita vários termos (separados por espaço, vírgula, ; ou +): raw/smooth/trend (modo da linha), hist/xy/lines (tipo) e
        /// mean/median/min/max/range/sd/samples (X repetido). Ex.: 'xy mean', 'smooth range'.
        /// </summary>
        public static bool TryParseMode(object raw, out ChartLineKind? kind, out ChartLineMode? lineMode, out ChartRepeatMode? repeat)
        {
            kind = null; lineMode = null; repeat = null;
            if (raw == null) return false;
            if (raw is IGH_Goo goo) raw = goo.SafeScriptVariable();
            if (raw is GH_ObjectWrapper wrap) raw = wrap.Value;
            if (raw is bool b) { kind = b ? ChartLineKind.Distribution : ChartLineKind.Lines; return true; }
            if (raw is string str)
            {
                bool any = false, bad = false;
                foreach (string tok in str.Split(new[] { ' ', ',', ';', '+', '|' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string t = tok.Trim().ToLowerInvariant();
                    switch (t)
                    {
                        case "raw": lineMode = ChartLineMode.Raw; any = true; continue;
                        case "smooth": lineMode = ChartLineMode.Smooth; any = true; continue;
                        case "trend": lineMode = ChartLineMode.Trend; any = true; continue;
                        case "mean": case "media": case "média": repeat = ChartRepeatMode.Mean; any = true; continue;
                        case "median": case "mediana": repeat = ChartRepeatMode.Median; any = true; continue;
                        case "min": case "minimum": case "minimo": case "mínimo": repeat = ChartRepeatMode.Min; any = true; continue;
                        case "max": case "maximum": case "maximo": case "máximo": repeat = ChartRepeatMode.Max; any = true; continue;
                        case "range": case "faixa": repeat = ChartRepeatMode.Range; any = true; continue;
                        case "sd": case "std": case "stddev": case "desvio": repeat = ChartRepeatMode.StdDev; any = true; continue;
                        case "samples": case "rawsamples": case "amostras": repeat = ChartRepeatMode.Raw; any = true; continue;
                    }
                    if (t.Contains("xy") || t.Contains("bars")) { kind = ChartLineKind.XYBars; any = true; }
                    else if (t.Contains("hist") || t.Contains("col") || t.Contains("distrib") || t.Contains("bar")) { kind = ChartLineKind.Distribution; any = true; }
                    else if (t.Contains("line") || t.Contains("linh")) { kind = ChartLineKind.Lines; any = true; }
                    else if (double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out double dn) && ParseKindNumber(dn, out ChartLineKind kn)) { kind = kn; any = true; }
                    else bad = true;
                }
                if (bad || !any) { kind = null; lineMode = null; repeat = null; return false; }
                if (lineMode.HasValue && !kind.HasValue) kind = ChartLineKind.Lines;
                return true;
            }
            if (GH_Convert.ToDouble(raw, out double d, GH_Conversion.Both) && ParseKindNumber(d, out ChartLineKind k)) { kind = k; return true; }
            return false;
        }

        private static bool ParseKindNumber(double d, out ChartLineKind k)
        {
            k = ChartLineKind.Lines;
            int i = (int)Math.Round(d);
            if (i < 0 || i > 2 || Math.Abs(d - i) > 0.1) return false;
            k = (ChartLineKind)i; return true;
        }

        /// <summary>Compatibilidade (tipo + modo da linha).</summary>
        public static bool TryParseMode(object raw, out ChartLineKind kind, out ChartLineMode? lineMode)
        {
            bool ok = TryParseMode(raw, out ChartLineKind? k, out lineMode, out ChartRepeatMode? _);
            kind = k ?? ChartLineKind.Lines; return ok;
        }
        /// <summary>Compatibilidade: true quando o Mode representa histograma/colunas.</summary>
        public static bool IsColumnsModeInput(object raw) => TryParseMode(raw, out ChartLineKind k, out ChartLineMode? _) && k == ChartLineKind.Distribution;

        public static bool TryParseTarget(object raw, out double target, out double tMin, out double tMax)
        {
            target = 0; tMin = 0; tMax = 0;
            if (raw == null) return false;

            if (raw is IGH_Goo goo)
            {
                if (goo.CastTo(out Interval iv) && iv.IsValid)
                {
                    tMin = Math.Min(iv.Min, iv.Max); tMax = Math.Max(iv.Min, iv.Max); target = (tMin + tMax) * 0.5; return true;
                }
                raw = goo.SafeScriptVariable();
            }
            if (raw is Interval directIv && directIv.IsValid)
            {
                tMin = Math.Min(directIv.Min, directIv.Max); tMax = Math.Max(directIv.Min, directIv.Max); target = (tMin + tMax) * 0.5; return true;
            }
            if (raw is double d && !double.IsNaN(d) && !double.IsInfinity(d)) return SingleTarget(d, out target, out tMin, out tMax);
            if (raw is int iVal) return SingleTarget(iVal, out target, out tMin, out tMax);

            string s = raw.ToString();
            if (!string.IsNullOrWhiteSpace(s))
            {
                string[] parts = s.Split(new string[] { "To", "to", "TO", "..", ";", "," }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 &&
                    double.TryParse(parts[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double p0) &&
                    double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double p1))
                {
                    tMin = Math.Min(p0, p1); tMax = Math.Max(p0, p1); target = (tMin + tMax) * 0.5; return true;
                }
                if (parts.Length == 1 && double.TryParse(parts[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double sv))
                    return SingleTarget(sv, out target, out tMin, out tMax);
            }
            return false;
        }

        private static bool SingleTarget(double v, out double target, out double tMin, out double tMax)
        {
            target = v;
            double tol = Math.Abs(v) * 0.15; if (tol < 1e-4) tol = 0.5;
            tMin = v - tol; tMax = v + tol;
            return true;
        }

        // Compatibilidade com chamadas antigas (delegam ao modelo único)
        public static double CalculateMode(List<double> vals) => ChartStats.Mode(vals);
        public static double CalculateMedian(List<double> vals) => ChartStats.Median(vals);
        public static HistogramData ComputeHistogramData(List<double> allVals, double? target, double tMin, double tMax) => ChartHistogram.Compute(allVals, target, tMin, tMax);
    }

    /// <summary>Canvas do Grasshopper: usa a MESMA ChartScene e o MESMO desenho (ChartLineRenderer) do PNG, apenas com tema escuro.</summary>
    public class ChartLine_Attributes : GH_ComponentAttributes
    {
        private const int GRAPH_WIDTH = 400;
        private const int GRAPH_HEIGHT = 260;
        private RectangleF m_btnExportRect;

        public ChartLine_Attributes(ChartLine_Component owner) : base(owner) { }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && m_btnExportRect.Contains(e.CanvasLocation))
            {
                var comp = Owner as ChartLine_Component;
                if (comp != null)
                {
                    string saved = comp.SavePngDialog();
                    if (!string.IsNullOrEmpty(saved)) { comp.Message = "PNG Salvo!"; sender.Refresh(); }
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

            float deltaX = Bounds.Right - oldRight;
            if (Math.Abs(deltaX) > 0.5f && Owner.Params?.Output != null)
            {
                foreach (var p in Owner.Params.Output)
                {
                    if (p.Attributes == null) continue;
                    var pb = p.Attributes.Bounds; pb.X += deltaX; p.Attributes.Bounds = pb;
                    var piv = p.Attributes.Pivot; piv.X += deltaX; p.Attributes.Pivot = piv;
                }
            }
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            if (channel == GH_CanvasChannel.Objects)
            {
                var savedPivot = Pivot;
                Pivot = new PointF(Bounds.X + Bounds.Width / 2f, savedPivot.Y);
                base.Render(canvas, graphics, channel);
                Pivot = savedPivot;
            }
            else base.Render(canvas, graphics, channel);

            if (channel != GH_CanvasChannel.Objects) return;
            var comp = Owner as ChartLine_Component;
            if (comp == null) return;
            var ci = CultureInfo.InvariantCulture;

            RectangleF b = Bounds;
            RectangleF graphRect = new RectangleF(b.X + 12, b.Bottom - GRAPH_HEIGHT - 10, b.Width - 24, GRAPH_HEIGHT);
            RectangleF headerRect = new RectangleF(graphRect.X, graphRect.Y, graphRect.Width, 24);
            RectangleF footerRect = new RectangleF(graphRect.X, graphRect.Bottom - 22, graphRect.Width, 22);
            float plotTop = headerRect.Bottom + 12;
            float plotBottom = footerRect.Y - 22;
            RectangleF plotRect = new RectangleF(graphRect.X + 40, plotTop, graphRect.Width - 52, Math.Max(80, plotBottom - plotTop));
            FontFamily fam = ChartLine_Component.GetUIFontFamily();

            using (var bgBrush = new SolidBrush(Color.FromArgb(20, 23, 29))) graphics.FillRectangle(bgBrush, graphRect);
            using (var borderPen = new Pen(Color.FromArgb(65, 72, 85), 1.2f)) graphics.DrawRectangle(borderPen, graphRect.X, graphRect.Y, graphRect.Width, graphRect.Height);
            using (var hb = new SolidBrush(Color.FromArgb(30, 34, 43))) graphics.FillRectangle(hb, headerRect);
            using (var fb = new SolidBrush(Color.FromArgb(24, 27, 34))) graphics.FillRectangle(fb, footerRect);
            using (var lp = new Pen(Color.FromArgb(50, 56, 68), 1f))
            {
                graphics.DrawLine(lp, headerRect.X, headerRect.Bottom, headerRect.Right, headerRect.Bottom);
                graphics.DrawLine(lp, footerRect.X, footerRect.Y, footerRect.Right, footerRect.Y);
            }
            using (var tf = new Font(fam, 8f, FontStyle.Bold)) using (var tbr = new SolidBrush(Color.FromArgb(240, 245, 250)))
                graphics.DrawString($"▪ {comp.DisplayTitle}", tf, tbr, headerRect.X + 8, headerRect.Y + 4);

            float btnW = 75f, btnH = 18f;
            m_btnExportRect = new RectangleF(headerRect.Right - btnW - 6, headerRect.Y + 3, btnW, btnH);

            using (var badgeFont = new Font(fam, 7f, FontStyle.Bold))
            using (var sfFar = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                if (comp.TargetValue.HasValue && comp.DeltaTarget.HasValue)
                    using (var bb = new SolidBrush(Color.FromArgb(255, 165, 0)))
                        graphics.DrawString($"[Δ: {comp.DeltaTarget.Value.ToString("+0.00;-0.00;0.00", ci)}]", badgeFont, bb, m_btnExportRect.Left - 8, headerRect.Y + headerRect.Height * 0.5f, sfFar);
                else
                {
                    string modeText = comp.Kind == ChartLineKind.Distribution ? "[Histograma · Distribuição]"
                        : comp.Kind == ChartLineKind.XYBars ? "[Barras XY]"
                        : $"[Linhas · {comp.LineMode.ToString().ToUpperInvariant()}{(comp.RepeatMode != ChartRepeatMode.Raw ? " · X:" + ChartLine_Component.RepeatLabelShort(comp.RepeatMode) : "")} ({comp.DisplaySeries.Count}s)]";
                    using (var bb = new SolidBrush(comp.Kind == ChartLineKind.Lines ? Color.FromArgb(0, 220, 255) : Color.FromArgb(255, 175, 40)))
                        graphics.DrawString(modeText, badgeFont, bb, m_btnExportRect.Left - 8, headerRect.Y + headerRect.Height * 0.5f, sfFar);
                }
            }

            using (var btnBg = new SolidBrush(Color.FromArgb(44, 52, 64))) using (var btnBorder = new Pen(Color.FromArgb(80, 92, 110), 1f))
            using (var btnFont = new Font(fam, 6.5f, FontStyle.Bold)) using (var btnText = new SolidBrush(Color.FromArgb(220, 230, 242)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                graphics.FillRectangle(btnBg, m_btnExportRect);
                graphics.DrawRectangle(btnBorder, m_btnExportRect.X, m_btnExportRect.Y, m_btnExportRect.Width, m_btnExportRect.Height);
                graphics.DrawString("💾 Salvar PNG", btnFont, btnText, m_btnExportRect, sf);
            }

            var scene = comp.Scene;
            if (scene == null || comp.DisplaySeries.Count == 0)
            {
                using (var plotBrush = new SolidBrush(Color.FromArgb(12, 14, 18))) graphics.FillRectangle(plotBrush, plotRect);
                using (var ef = new Font(fam, 7.5f, FontStyle.Italic)) using (var eb = new SolidBrush(Color.FromArgb(130, 140, 155)))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    graphics.DrawString("Conecte os dados X e Y para visualizar.", ef, eb, plotRect, sf);
                return;
            }

            ChartLineRenderer.DrawPlot(graphics, plotRect, scene, ChartTheme.Dark(1f), fam);

            using (var ff = new Font(fam, 6.2f, FontStyle.Regular)) using (var fbr = new SolidBrush(Color.FromArgb(140, 155, 175)))
            {
                string info;
                var s0 = comp.DisplaySeries[0];
                if (comp.Kind == ChartLineKind.Distribution && comp.CachedHistData != null)
                {
                    var h = comp.CachedHistData;
                    info = $"N: {h.TotalCount} | Média: {h.Mean.ToString("F2", ci)} | Mediana: {h.Median.ToString("F2", ci)} | Moda≈ {h.Mode.ToString("F2", ci)} | σ: {h.StdDev.ToString("F2", ci)}";
                }
                else if (comp.DisplaySeries.Count == 1)
                    info = $"N: {s0.Count} | Média: {s0.Mean.ToString("F2", ci)} | Mediana: {s0.Median.ToString("F2", ci)} | σ: {s0.StdDev.ToString("F2", ci)} | Mín/Máx: {s0.MinY.ToString("G4", ci)}/{s0.MaxY.ToString("G4", ci)}";
                else info = $"{comp.DisplaySeries.Count} séries | N: {scene.PooledCount} | μ: {scene.PooledMean.ToString("F2", ci)} | σ: {scene.PooledStdDev.ToString("F2", ci)}";
                if (s0.Groups != null && s0.HasRepeats && comp.Kind != ChartLineKind.Distribution) info += $" | {s0.Groups.Count} grupos X";
                if (comp.TargetValue.HasValue) info += $" | Alvo: {comp.TargetValue.Value.ToString("F2", ci)} (Δ: {comp.DeltaTarget.Value.ToString("F2", ci)})";
                graphics.DrawString(info, ff, fbr, footerRect.X + 8, footerRect.Y + 4);
            }
        }
    }
}
