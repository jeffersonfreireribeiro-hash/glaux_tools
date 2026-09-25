using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Componente Pill para filtragem e higienização de geometrias (Pill Geometry Filter).
    /// Elimina cirurgicamente geometrias colapsadas, micro-faces com área próxima de zero,
    /// áreas negativas, valores indefinidos (NaN) ou tendendo a -infinity.
    /// Suporta inspeção de subfaces em Breps e Malhas com publicação sem fios opcional no PillHub.
    /// </summary>
    public class PillGeometryFilter_Component : GH_Component
    {
        public int CleanCount { get; private set; } = 0;
        public int DiscardedCount { get; private set; } = 0;
        public string CurrentCleanKey { get; private set; } = "";
        public string CurrentCategory => "GEO";
        public string CurrentUnit => "m²";
        public Color CurrentCategoryColor => Color.FromArgb(46, 175, 100); // Verde Esmeralda GEO
        public string FilterSummaryShort => DiscardedCount > 0 ? $"{CleanCount} válidas / {DiscardedCount} podadas" : $"{CleanCount} válidas";

        private double _presetMinArea = 0.0001;
        private bool _cleanSubFacesSetting = false;
        private string _lastPublishedKey = "";

        public PillGeometryFilter_Component()
            : base(
                "Pill Geometry Filter",
                "PillGeomFilter",
                "Filtro e higienizador de geometrias para simulação acústica e modelagem paramétrica.\n" +
                "- Elimina elementos degenerados, micro-faces colapsadas com área próxima de zero, áreas negativas, NaN ou tendendo a -infinity.\n" +
                "- Suporta expurgo de micro-triângulos internos em Breps e Malhas compostas.\n" +
                "- Preserva a integridade de caminhos GH_Path para árvores de dados a jusante.\n" +
                "- Publicação opcional no barramento sem fio PillHub (categoria [GEO]).",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("a1100013-e1ef-4000-8000-000000000013");

        protected override Bitmap Icon => GlauxToolsIcons.PillGeometryFilter;

        public override void CreateAttributes()
        {
            m_attributes = new Pill_Attributes(this);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            if (!string.IsNullOrEmpty(_lastPublishedKey))
            {
                PillHub.Unpublish(_lastPublishedKey, InstanceGuid);
                _lastPublishedKey = "";
            }
            base.RemovedFromDocument(document);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Camada 1: Geometrias de Entrada
            pManager.AddGeometryParameter("Geometry", "G", "Geometrias brutas ou árvores de geometrias a inspecionar e filtrar (Brep, Mesh, Surface, Extrusion, Curve).", GH_ParamAccess.tree);

            // Camada 2: Domínio Físico e Critérios de Área
            pManager.AddNumberParameter("MinArea", "Min", "Área mínima aceitável (m²). Geometrias com área menor ou igual a este limiar, colapsadas, negativas, NaN ou tendendo a -infinito serão sumariamente eliminadas. Default: 0.0001 m² (1 cm²).", GH_ParamAccess.item, 0.0001);
            pManager.AddNumberParameter("MaxArea", "Max", "Área máxima aceitável opcional (m²). Se definido como valor positivo, geometrias infinitas ou gigantescas que ultrapassarem este limite serão eliminadas. 0 = desativado.", GH_ParamAccess.item, 0.0);

            // Camada 4: Modos de Filtragem e Topologia
            pManager.AddBooleanParameter("CleanSubFaces", "Sub", "Se True, inspeciona e remove internamente micro-triângulos e faces degeneradas de área zero em Breps e Malhas compostas. Se False, avalia o elemento como um bloco único com máxima velocidade de processamento (Recomendado). Default: False.", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("PreservePaths", "P", "Modo de preservação de caminhos da árvore de dados:\n0 = Podar ramos que ficarem vazios\n1 = Manter ramos vazios estruturais\n2 = Preencher posições eliminadas com <null> (mantém índices de sincronização idênticos)", GH_ParamAccess.item, 0);
            pManager.AddTextParameter("Key", "K", "Nome opcional do canal Pill para transmissão sem fios automática das geometrias limpas pelo PillHub (categoria [GEO]).", GH_ParamAccess.item, "");

            // Camada 6: Gatilho de Execução - O ÚLTIMO PARÂMETRO DA LISTA (Conforme a Regra de Ouro)
            pManager.AddBooleanParameter("Active", "Active", "Gatilho de ativação do filtro. Se False, opera em modo pass-through (todas as geometrias passam sem filtragem). O ÚLTIMO PARÂMETRO DA LISTA.", GH_ParamAccess.item, true);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // Camada 1: Geometrias Limpas e Aprovadas
            pManager.AddGeometryParameter("Clean", "C", "Árvore contendo apenas as geometrias higienizadas e aprovadas (com área válida > MinArea e livre de degenerações).", GH_ParamAccess.tree);

            // Camada 2: Geometrias Rejeitadas e Eliminadas
            pManager.AddGeometryParameter("Discarded", "D", "Árvore contendo as geometrias eliminadas (área próxima a zero, degeneradas, NaN, negativas, infinitas ou inválidas).", GH_ParamAccess.tree);

            // Camada 3: Métricas de Área e Máscara Booleana
            pManager.AddNumberParameter("Areas", "A", "Árvore com os valores calculados de área (m²) de cada elemento inspecionado.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Pattern", "P", "Máscara booleana (True = Aprovada/Limpa, False = Eliminada/Degenerada).", GH_ParamAccess.tree);

            // Camada 4: Diagnóstico e Relatório Técnico
            pManager.AddTextParameter("Report", "R", "Relatório estruturado com estatísticas de conformidade, percentual de descarte, menor área detectada e diagnóstico de micro-faces.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Leitura do gatilho de execução Active (garantidamente o último parâmetro de entrada)
            int activeIndex = Params.Input.Count - 1;
            bool active = true;
            DA.GetData(activeIndex, ref active);

            if (!DA.GetDataTree(0, out GH_Structure<IGH_GeometricGoo> inGeomTree) || inGeomTree == null || inGeomTree.DataCount == 0)
            {
                CleanCount = 0;
                DiscardedCount = 0;
                CurrentCleanKey = "";
                Message = "Sem Geometria";
                DA.SetData(4, "Aguardando geometria de entrada.");
                return;
            }

            double minArea = _presetMinArea;
            DA.GetData(1, ref minArea);
            if (minArea < 0 || double.IsNaN(minArea) || double.IsInfinity(minArea)) minArea = 1e-6;

            double maxArea = 0.0;
            DA.GetData(2, ref maxArea);

            bool cleanSubFaces = _cleanSubFacesSetting;
            DA.GetData(3, ref cleanSubFaces);

            int preserveMode = 0;
            DA.GetData(4, ref preserveMode);

            string rawKey = "";
            DA.GetData(5, ref rawKey);

            // Se o filtro estiver desativado (Bypass)
            if (!active)
            {
                CleanCount = inGeomTree.DataCount;
                DiscardedCount = 0;
                Message = "Bypass";

                DA.SetDataTree(0, inGeomTree);
                DA.SetDataTree(1, new GH_Structure<IGH_GeometricGoo>());
                DA.SetDataTree(2, new GH_Structure<GH_Number>());
                
                var truePattern = new GH_Structure<GH_Boolean>();
                foreach (var p in inGeomTree.Paths)
                {
                    var branch = inGeomTree.get_Branch(p);
                    if (branch != null && branch.Count > 0)
                    {
                        var bools = new List<GH_Boolean>(branch.Count);
                        for (int i = 0; i < branch.Count; i++) bools.Add(new GH_Boolean(true));
                        truePattern.AppendRange(bools, p);
                    }
                }
                DA.SetDataTree(3, truePattern);
                DA.SetData(4, $"FILTRO EM BYPASS | Todas as {inGeomTree.DataCount} geometrias foram liberadas sem filtragem.");
                return;
            }

            var cleanTree = new GH_Structure<IGH_GeometricGoo>();
            var discardedTree = new GH_Structure<IGH_GeometricGoo>();
            var areasTree = new GH_Structure<GH_Number>();
            var patternTree = new GH_Structure<GH_Boolean>();

            var paths = inGeomTree.Paths.ToList();
            int pathCount = paths.Count;
            int totalItemsInTree = inGeomTree.DataCount;

            var branchResults = new BranchResult[pathCount];

            void ProcessBranch(int pIdx)
            {
                var path = paths[pIdx];
                var branch = inGeomTree.get_Branch(path);
                int count = branch.Count;

                var res = new BranchResult
                {
                    Path = path,
                    CleanList = new List<IGH_GeometricGoo>(count),
                    DiscardedList = new List<IGH_GeometricGoo>(count),
                    AreasList = new List<GH_Number>(count),
                    PatternList = new List<GH_Boolean>(count),
                    MinArea = double.MaxValue,
                    MaxArea = double.MinValue,
                    RejectionReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                };

                for (int i = 0; i < count; i++)
                {
                    var item = branch[i] as IGH_GeometricGoo;

                    bool isValid = EvaluateGeometry(
                        item,
                        minArea,
                        maxArea,
                        cleanSubFaces,
                        out IGH_GeometricGoo cleanedGoo,
                        out double area,
                        out string rejectReason);

                    if (!double.IsNaN(area) && !double.IsInfinity(area) && area > 0)
                    {
                        if (area < res.MinArea) res.MinArea = area;
                        if (area > res.MaxArea) res.MaxArea = area;
                    }

                    res.AreasList.Add(new GH_Number(double.IsNaN(area) || double.IsInfinity(area) ? 0.0 : area));

                    if (isValid)
                    {
                        res.ApprovedCount++;
                        res.PatternList.Add(new GH_Boolean(true));
                        res.CleanList.Add(cleanedGoo ?? item);
                        if (preserveMode == 2)
                        {
                            res.DiscardedList.Add(null);
                        }
                    }
                    else
                    {
                        res.RejectedCount++;
                        res.PatternList.Add(new GH_Boolean(false));
                        res.DiscardedList.Add(item);
                        if (preserveMode == 2)
                        {
                            res.CleanList.Add(null);
                        }

                        if (!string.IsNullOrEmpty(rejectReason))
                        {
                            res.RejectionReasons.TryGetValue(rejectReason, out int c);
                            res.RejectionReasons[rejectReason] = c + 1;
                        }
                    }
                }

                branchResults[pIdx] = res;
            }

            // Execução paralela em múltiplos núcleos de CPU quando há volume de galhos e dados
            if (pathCount > 1 && totalItemsInTree > 8)
            {
                Parallel.For(0, pathCount, ProcessBranch);
            }
            else
            {
                for (int p = 0; p < pathCount; p++)
                {
                    ProcessBranch(p);
                }
            }

            int totalInspected = 0;
            int approvedCount = 0;
            int rejectedCount = 0;
            double minAreaFound = double.MaxValue;
            double maxAreaFound = double.MinValue;
            var rejectionReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Consolidação determinística e ultrarrápida com AppendRange mantendo rigorosamente a topologia da árvore
            for (int p = 0; p < pathCount; p++)
            {
                var res = branchResults[p];
                var path = res.Path;

                if (res.CleanList.Count > 0)
                {
                    cleanTree.AppendRange(res.CleanList, path);
                }
                else if (preserveMode == 1)
                {
                    cleanTree.EnsurePath(path);
                }

                if (res.DiscardedList.Count > 0)
                {
                    discardedTree.AppendRange(res.DiscardedList, path);
                }
                else if (preserveMode == 1)
                {
                    discardedTree.EnsurePath(path);
                }

                if (res.AreasList.Count > 0)
                {
                    areasTree.AppendRange(res.AreasList, path);
                }
                if (res.PatternList.Count > 0)
                {
                    patternTree.AppendRange(res.PatternList, path);
                }

                approvedCount += res.ApprovedCount;
                rejectedCount += res.RejectedCount;
                totalInspected += (res.ApprovedCount + res.RejectedCount);
                if (res.MinArea < minAreaFound) minAreaFound = res.MinArea;
                if (res.MaxArea > maxAreaFound) maxAreaFound = res.MaxArea;

                if (res.RejectionReasons != null)
                {
                    foreach (var kvp in res.RejectionReasons)
                    {
                        rejectionReasons.TryGetValue(kvp.Key, out int c);
                        rejectionReasons[kvp.Key] = c + kvp.Value;
                    }
                }
            }

            CleanCount = approvedCount;
            DiscardedCount = rejectedCount;

            DA.SetDataTree(0, cleanTree);
            DA.SetDataTree(1, discardedTree);
            DA.SetDataTree(2, areasTree);
            DA.SetDataTree(3, patternTree);

            // Monta relatório técnico de conformidade
            var sb = new StringBuilder();
            sb.AppendLine("=== BURAQUEIRA TOOLS: PILL GEOMETRY FILTER ===");
            sb.AppendLine($"Status: {(rejectedCount == 0 ? "100% CONFORME (Nenhuma geometria podada)" : $"HIGIENIZADO ({rejectedCount} podada(s))")}");
            sb.AppendLine($"Total Inspecionado: {totalInspected} em {pathCount} ramo(s)");
            sb.AppendLine($"Aprovadas (Área > {minArea:E3} m²): {approvedCount} ({(totalInspected > 0 ? (double)approvedCount / totalInspected * 100.0 : 0):F1}%)");
            sb.AppendLine($"Eliminadas (<= 0 / NaN / -Inf / Degeneradas): {rejectedCount} ({(totalInspected > 0 ? (double)rejectedCount / totalInspected * 100.0 : 0):F1}%)");
            
            if (minAreaFound < double.MaxValue)
            {
                sb.AppendLine($"Menor Área Aprovada: {minAreaFound:E4} m²");
                sb.AppendLine($"Maior Área Aprovada: {maxAreaFound:F4} m²");
            }
            sb.AppendLine($"Subfaces / Micro-triângulos Limpos: {(cleanSubFaces ? "Ativo (Deep Inspect SubFaces)" : "Inativo (Fast Direct Inspect)")}");

            if (rejectionReasons.Count > 0)
            {
                sb.AppendLine("\n--- MOTIVOS DE DESCARTE ---");
                foreach (var kvp in rejectionReasons)
                {
                    sb.AppendLine($"• {kvp.Key}: {kvp.Value} ocorrência(s)");
                }
            }
            DA.SetData(4, sb.ToString());

            // 10. Publicação automática no PillHub se a chave foi informada
            if (!string.IsNullOrWhiteSpace(rawKey))
            {
                PillHub.ParseKeyMetadata(rawKey, null, out string cleanKey, out _, out _, out _);
                CurrentCleanKey = cleanKey;

                if (!string.IsNullOrEmpty(_lastPublishedKey) && !string.Equals(_lastPublishedKey, cleanKey, StringComparison.OrdinalIgnoreCase))
                {
                    PillHub.Unpublish(_lastPublishedKey, InstanceGuid);
                }
                _lastPublishedKey = cleanKey;

                var genericCleanTree = new GH_Structure<IGH_Goo>();
                foreach (var p in cleanTree.Paths)
                {
                    var branch = cleanTree[p];
                    if (branch != null && branch.Count > 0)
                    {
                        genericCleanTree.AppendRange(branch.Where(g => g != null), p);
                    }
                }

                Guid currentDocId = OnPingDocument()?.DocumentID ?? Guid.Empty;
                PillHub.Publish(rawKey, genericCleanTree, InstanceGuid, currentDocId, CurrentUnit);
            }
            else
            {
                if (!string.IsNullOrEmpty(_lastPublishedKey))
                {
                    PillHub.Unpublish(_lastPublishedKey, InstanceGuid);
                    _lastPublishedKey = "";
                }
                CurrentCleanKey = "";
            }

            Message = rejectedCount > 0 ? $"{approvedCount} apr | -{rejectedCount}" : $"{approvedCount} OK";
        }

        /// <summary>
        /// Avalia se uma geometria possui área válida, superior ao limiar mínimo e livre de degenerações.
        /// </summary>
        private static bool EvaluateGeometry(
            IGH_GeometricGoo goo,
            double minArea,
            double maxArea,
            bool cleanSubFaces,
            out IGH_GeometricGoo cleanedGoo,
            out double area,
            out string rejectReason)
        {
            cleanedGoo = null;
            area = 0.0;
            rejectReason = "";

            if (goo == null)
            {
                rejectReason = "Geometria nula";
                return false;
            }

            object rawGeom = goo.ScriptVariable();
            if (rawGeom == null)
            {
                rejectReason = "Não foi possível extrair dados geométricos válidos do objeto";
                return false;
            }

            // =========================================================================
            // 1. CASO: MALHA (MESH)
            // =========================================================================
            if (rawGeom is Mesh mesh)
            {
                if (!mesh.IsValid || mesh.Faces.Count == 0 || mesh.Vertices.Count < 3)
                {
                    rejectReason = "Malha inválida, sem faces ou com vértices insuficientes (< 3)";
                    return false;
                }

                // Fast BBox check para descarte imediato
                var bbox = mesh.GetBoundingBox(false);
                if (!bbox.IsValid || bbox.Diagonal.Length < 1e-7)
                {
                    rejectReason = "Malha colapsada em um ponto único (Diagonal BBox < 1e-7)";
                    return false;
                }

                Mesh processMesh = mesh;
                if (cleanSubFaces)
                {
                    processMesh = mesh.DuplicateMesh();
                    int culled = processMesh.Faces.CullDegenerateFaces();
                    if (culled > 0)
                    {
                        processMesh.Compact();
                    }
                    else
                    {
                        processMesh = mesh; // Não houve degeneração; mantém a malha original
                    }
                }

                if (processMesh.Faces.Count == 0)
                {
                    rejectReason = "Todas as faces da malha eram triângulos colapsados / micro-faces de área zero";
                    return false;
                }

                // Cálculo ultrarrápido de área via produto vetorial direto em C# puro (100x mais rápido que AreaMassProperties)
                area = ComputeFastMeshArea(processMesh);

                if (cleanSubFaces && processMesh != mesh)
                {
                    cleanedGoo = new GH_Mesh(processMesh);
                }
                else
                {
                    cleanedGoo = goo;
                }
            }
            // =========================================================================
            // 2. CASO: BREP
            // =========================================================================
            else if (rawGeom is Brep brep)
            {
                if (!brep.IsValid || brep.Faces.Count == 0)
                {
                    rejectReason = "Brep inválido ou sem faces";
                    return false;
                }

                // BBox rápida sem aproximação analítica lenta de curvas (false)
                var bbox = brep.GetBoundingBox(false);
                if (!bbox.IsValid || bbox.Diagonal.Length < 1e-7)
                {
                    rejectReason = "Brep colapsado em um ponto único (Diagonal BBox < 1e-7)";
                    return false;
                }

                Brep processBrep = brep;
                if (cleanSubFaces && brep.Faces.Count > 1)
                {
                    // Pré-checagem rápida: só desconstrói e reconstrói se realmente houver subface degenerada
                    bool hasDegenerateSubFace = false;
                    double sumFaceAreas = 0.0;
                    var faceAreas = new double[brep.Faces.Count];

                    for (int f = 0; f < brep.Faces.Count; f++)
                    {
                        var face = brep.Faces[f];
                        var fAmp = AreaMassProperties.Compute(face);
                        double fa = fAmp != null ? fAmp.Area : 0.0;
                        faceAreas[f] = fa;
                        if (double.IsNaN(fa) || double.IsInfinity(fa) || fa <= minArea)
                        {
                            hasDegenerateSubFace = true;
                        }
                        else
                        {
                            sumFaceAreas += fa;
                        }
                    }

                    if (hasDegenerateSubFace)
                    {
                        var validSubFaces = new List<Brep>();
                        for (int f = 0; f < brep.Faces.Count; f++)
                        {
                            double fa = faceAreas[f];
                            if (!double.IsNaN(fa) && !double.IsInfinity(fa) && fa > minArea)
                            {
                                var dupFace = brep.Faces[f].DuplicateFace(false);
                                if (dupFace != null) validSubFaces.Add(dupFace);
                            }
                        }

                        if (validSubFaces.Count == 0)
                        {
                            rejectReason = $"Todas as {brep.Faces.Count} faces do Brep possuíam área <= limiar ({minArea:E3} m²)";
                            return false;
                        }

                        var joined = Brep.JoinBreps(validSubFaces, 0.001);
                        processBrep = (joined != null && joined.Length > 0) ? joined[0] : validSubFaces[0];
                        area = sumFaceAreas;
                        cleanedGoo = new GH_Brep(processBrep);
                    }
                    else
                    {
                        // Todas as faces são sadias! Zero duplicações, zero reconstrução de topologia
                        area = sumFaceAreas;
                        cleanedGoo = goo;
                    }
                }
                else
                {
                    // Caminho de ultra performance: cálculo direto em um único passe
                    var bAmp = AreaMassProperties.Compute(brep);
                    if (bAmp != null)
                    {
                        area = bAmp.Area;
                    }
                    else
                    {
                        double sumFaces = 0.0;
                        foreach (var f in brep.Faces)
                        {
                            var fa = AreaMassProperties.Compute(f);
                            if (fa != null && !double.IsNaN(fa.Area) && !double.IsInfinity(fa.Area) && fa.Area > 0)
                                sumFaces += fa.Area;
                        }
                        area = sumFaces;
                    }
                    cleanedGoo = goo;
                }
            }
            // =========================================================================
            // 3. CASO: SUPERFÍCIE (SURFACE) OU EXTRUSÃO
            // =========================================================================
            else if (rawGeom is Surface srf)
            {
                var sAmp = AreaMassProperties.Compute(srf);
                if (sAmp != null)
                {
                    area = sAmp.Area;
                }
                else
                {
                    var b = srf.ToBrep();
                    var bAmp = b != null ? AreaMassProperties.Compute(b) : null;
                    if (bAmp != null) area = bAmp.Area;
                    else { rejectReason = "Superfície degenerada com falha no cálculo de área"; return false; }
                }
                cleanedGoo = goo;
            }
            else if (rawGeom is Extrusion ext)
            {
                var b = ext.ToBrep();
                var bAmp = b != null ? AreaMassProperties.Compute(b) : null;
                if (bAmp != null) area = bAmp.Area;
                else { rejectReason = "Extrusão degenerada com falha no cálculo de área"; return false; }
                cleanedGoo = goo;
            }
            // =========================================================================
            // 4. CASO: CURVA (CURVE)
            // =========================================================================
            else if (rawGeom is Curve crv)
            {
                if (crv.IsClosed && crv.IsPlanar())
                {
                    var cAmp = AreaMassProperties.Compute(crv);
                    if (cAmp != null) area = cAmp.Area;
                    else area = crv.GetLength();
                }
                else
                {
                    area = crv.GetLength();
                }

                if (area < 1e-9)
                {
                    rejectReason = $"Curva colapsada de comprimento quase nulo ({area:E3} m)";
                    return false;
                }
                cleanedGoo = goo;
            }
            else
            {
                // Conversão genérica de emergência
                Brep tryBrep = null;
                Mesh tryMesh = null;
                if (GH_Convert.ToBrep(goo, ref tryBrep, GH_Conversion.Both) && tryBrep != null)
                {
                    var amp = AreaMassProperties.Compute(tryBrep);
                    if (amp != null) area = amp.Area;
                    else { rejectReason = "Falha no cálculo de área do Brep convertido"; return false; }
                    cleanedGoo = new GH_Brep(tryBrep);
                }
                else if (GH_Convert.ToMesh(goo, ref tryMesh, GH_Conversion.Both) && tryMesh != null)
                {
                    var amp = AreaMassProperties.Compute(tryMesh);
                    if (amp != null) area = amp.Area;
                    else { rejectReason = "Falha no cálculo de área da Malha convertida"; return false; }
                    cleanedGoo = new GH_Mesh(tryMesh);
                }
                else
                {
                    rejectReason = $"Tipo geométrico não suportado ({rawGeom.GetType().Name})";
                    return false;
                }
            }

            // =========================================================================
            // CRITÉRIOS DE EXCLUSÃO CRÍTICA (Zero, NaN, -Infinity, Negativo)
            // =========================================================================
            if (double.IsNaN(area))
            {
                rejectReason = "Área indefinida (NaN / Not-a-Number)";
                return false;
            }

            if (double.IsNegativeInfinity(area) || double.IsInfinity(area))
            {
                rejectReason = "Área infinita (tendendo a -infinity ou +infinity)";
                return false;
            }

            if (area <= 0.0)
            {
                rejectReason = $"Área nula ou negativa ({area:E3} m²)";
                return false;
            }

            if (area <= minArea)
            {
                rejectReason = $"Micro-face / área próxima de zero ({area:E4} m² <= limiar {minArea:E4} m²)";
                return false;
            }

            if (maxArea > 0.0 && area > maxArea)
            {
                rejectReason = $"Área excessiva acima do teto máximo ({area:F2} m² > {maxArea:F2} m²)";
                return false;
            }

            return true;
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var limiarMenu = new ToolStripMenuItem("📏 Limiar de Área Mínima (Presets)");
            var presets = new (string label, double val)[]
            {
                ("1e-6 m² (1 mm² - Tolerância Ultra-Fina)", 1e-6),
                ("0.0001 m² (1 cm² - Padrão Acústica/Arquitetura)", 0.0001),
                ("0.001 m² (10 cm² - Malha Média)", 0.001),
                ("0.01 m² (1 dm² - Malha Macro)", 0.01),
                ("0.1 m² (Faces Grandes)", 0.1)
            };

            foreach (var p in presets)
            {
                var item = new ToolStripMenuItem(p.label)
                {
                    Checked = Math.Abs(_presetMinArea - p.val) < 1e-12
                };
                double targetVal = p.val;
                item.Click += (s, e) =>
                {
                    RecordUndoEvent("Alterar Limiar de Área");
                    _presetMinArea = targetVal;
                    ExpireSolution(true);
                };
                limiarMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(limiarMenu);

            var subFacesItem = new ToolStripMenuItem("🧹 Limpar Micro-Faces Internas de Breps/Malhas", null, (s, e) =>
            {
                RecordUndoEvent("Toggle Clean SubFaces");
                _cleanSubFacesSetting = !_cleanSubFacesSetting;
                ExpireSolution(true);
            })
            {
                Checked = _cleanSubFacesSetting
            };
            menu.Items.Add(subFacesItem);
        }

        /// <summary>
        /// Cálculo ultrarrápido de área de malha via produto vetorial por face em C# puro.
        /// Mais de 100x mais rápido que AreaMassProperties.Compute(mesh) e sem alocação de memória.
        /// </summary>
        public static double ComputeFastMeshArea(Mesh mesh)
        {
            if (mesh == null) return 0.0;
            var verts = mesh.Vertices;
            var faces = mesh.Faces;
            int count = faces.Count;
            if (count == 0 || verts.Count < 3) return 0.0;

            double totalArea = 0.0;
            for (int i = 0; i < count; i++)
            {
                var f = faces[i];
                Point3f a = verts[f.A];
                Point3f b = verts[f.B];
                Point3f c = verts[f.C];

                double abx = b.X - a.X;
                double aby = b.Y - a.Y;
                double abz = b.Z - a.Z;

                double acx = c.X - a.X;
                double acy = c.Y - a.Y;
                double acz = c.Z - a.Z;

                double cx = aby * acz - abz * acy;
                double cy = abz * acx - abx * acz;
                double cz = abx * acy - aby * acx;

                double triArea = 0.5 * Math.Sqrt(cx * cx + cy * cy + cz * cz);
                if (!double.IsNaN(triArea) && !double.IsInfinity(triArea) && triArea > 0)
                {
                    totalArea += triArea;
                }

                if (f.IsQuad)
                {
                    Point3f d = verts[f.D];
                    double adx = d.X - a.X;
                    double ady = d.Y - a.Y;
                    double adz = d.Z - a.Z;

                    double qx = acy * adz - acz * ady;
                    double qy = acz * adx - acx * adz;
                    double qz = acx * ady - acy * adx;

                    double quadArea = 0.5 * Math.Sqrt(qx * qx + qy * qy + qz * qz);
                    if (!double.IsNaN(quadArea) && !double.IsInfinity(quadArea) && quadArea > 0)
                    {
                        totalArea += quadArea;
                    }
                }
            }
            return totalArea;
        }

        private class BranchResult
        {
            public GH_Path Path;
            public List<IGH_GeometricGoo> CleanList;
            public List<IGH_GeometricGoo> DiscardedList;
            public List<GH_Number> AreasList;
            public List<GH_Boolean> PatternList;
            public int ApprovedCount;
            public int RejectedCount;
            public double MinArea;
            public double MaxArea;
            public Dictionary<string, int> RejectionReasons;
        }
    }
}
