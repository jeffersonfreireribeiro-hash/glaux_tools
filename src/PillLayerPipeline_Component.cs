using System;
using System.Collections.Generic;
using System.Drawing;
using Font = System.Drawing.Font;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    public enum LayerPipelineSortMode
    {
        Spatial3D = 0,      // Centroide BBox: X -> Y -> Z (Imutavel a recriacao de objetos e GUIDs)
        Spatial2D = 1,      // Centroide BBox: X -> Y no plano
        NaturalName = 2,    // Nome do Objeto em ordem numerica natural (ex: Parede_1, Parede_2, Parede_10)
        NameThenGuid = 3,   // Nome do Objeto -> GUID persistente
        GuidOnly = 4        // Apenas GUID persistente
    }

    public class PillLayerPipeline_Component : GH_Component
    {
        public string CurrentCleanKey { get; private set; } = "";
        public string CurrentCategory { get; private set; } = "GEO";
        public string CurrentUnit { get; private set; } = "m";
        public Color CurrentCategoryColor { get; private set; } = Color.FromArgb(46, 175, 100); // Verde Esmeralda GEO

        // Parametros persistentes salvos no arquivo .gh
        private readonly List<string> _savedLayerPaths = new List<string>();
        public LayerPipelineSortMode SortMode { get; set; } = LayerPipelineSortMode.Spatial3D;
        public ObjectType FilterType { get; set; } = ObjectType.AnyObject;
        public bool GroupByLayer { get; set; } = false;
        public bool LiveTrackingEnabled { get; set; } = true;

        public int SavedLayerCount => _savedLayerPaths.Count;
        public int CapturedObjectCount { get; private set; } = 0;

        private string _lastPublishedKey = "";
        private bool _eventsRegistered = false;
        private DateTime _lastDocEventTime = DateTime.MinValue;

        public PillLayerPipeline_Component()
            : base(
                "Pill Invariant Geometry Pipeline",
                "PillLayers",
                "Pipeline deterministico de geometrias do Rhino imutavel a alteracoes de camadas e indices.\n" +
                "- Selecao estavel de camadas pelo FullPath hierarquico (ex: '01_ARQ::PAREDES::CONCRETO'), imune a criacao/exclusao de camadas intermediarias.\n" +
                "- Ordenacao canonica estrita (Espacial X->Y->Z, Nome Natural ou GUID) garantindo estabilidade absoluta para Wallacei e Galapagos.\n" +
                "- Publicacao automatica no barramento sem fio PillHub e sincronizacao por cabos ocultos.",
                "Glaux Tools",
                "Pills")
        {
            RegisterRhinoDocEvents();
        }

        public override Guid ComponentGuid => new Guid("a1100012-e1ef-4000-8000-000000000012");
        protected override Bitmap Icon => GlauxToolsIcons.PillLayerPipeline;

        public override void CreateAttributes()
        {
            m_attributes = new Pill_Attributes(this);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            RegisterRhinoDocEvents();
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            UnregisterRhinoDocEvents();
            if (!string.IsNullOrEmpty(_lastPublishedKey))
            {
                PillHub.Unpublish(_lastPublishedKey, InstanceGuid);
                _lastPublishedKey = "";
            }
            base.RemovedFromDocument(document);
        }

        private void RegisterRhinoDocEvents()
        {
            if (_eventsRegistered) return;
            RhinoDoc.AddRhinoObject += OnRhinoObjectModified;
            RhinoDoc.DeleteRhinoObject += OnRhinoObjectModified;
            RhinoDoc.UndeleteRhinoObject += OnRhinoObjectModified;
            RhinoDoc.ModifyObjectAttributes += OnRhinoObjectAttrsModified;
            _eventsRegistered = true;
        }

        private void UnregisterRhinoDocEvents()
        {
            if (!_eventsRegistered) return;
            RhinoDoc.AddRhinoObject -= OnRhinoObjectModified;
            RhinoDoc.DeleteRhinoObject -= OnRhinoObjectModified;
            RhinoDoc.UndeleteRhinoObject -= OnRhinoObjectModified;
            RhinoDoc.ModifyObjectAttributes -= OnRhinoObjectAttrsModified;
            _eventsRegistered = false;
        }

        private void OnRhinoObjectModified(object sender, RhinoObjectEventArgs e)
        {
            TriggerSchedule();
        }

        private void OnRhinoObjectAttrsModified(object sender, RhinoModifyObjectAttributesEventArgs e)
        {
            TriggerSchedule();
        }

        private void TriggerSchedule()
        {
            if (!LiveTrackingEnabled) return;
            var doc = OnPingDocument();
            if (doc == null || doc.SolutionState == GH_ProcessStep.Process) return;

            // Debounce de 100ms para operacoes em lote no Rhino
            if ((DateTime.Now - _lastDocEventTime).TotalMilliseconds < 100) return;
            _lastDocEventTime = DateTime.Now;

            doc.ScheduleSolution(50, _ => ExpireSolution(false));
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("LayerFilter", "LF", "Caminho hierarquico completo da camada (ex: '01_ARQ::PAREDES::CONCRETO') ou padrao com wildcard ('*PAREDE*', 'ACU::*'). Se vazio, utiliza a selecao do menu ou da janela visual.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("TypeFilter", "TF", "Filtro de tipo geometrico:\n0 = Qualquer\n1 = Brep / Superficie\n2 = Malha (Mesh)\n3 = Curva\n4 = Ponto\n5 = SubD\n6 = Texto / Anotacao\n7 = Extrusao", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("SortMode", "SM", "Criterio de ordenacao canonica:\n0 = Espacial 3D (X -> Y -> Z do Bounding Box) [Padrao Wallacei]\n1 = Espacial 2D (X -> Y no plano XY)\n2 = Nome Natural do Objeto (ex: P1, P2, P10)\n3 = Nome do Objeto -> GUID\n4 = GUID Estavel", GH_ParamAccess.item, (int)SortMode);
            pManager.AddTextParameter("PillKey", "K", "Chave opcional para publicar automaticamente no barramento sem fio PillHub (ex: '[GEO] Paredes_Auditorio').", GH_ParamAccess.item, "");
            pManager.AddNumberParameter("Tolerance", "T", "Tolerancia de comparacao espacial em coordenadas (padrao: 0.001 m).", GH_ParamAccess.item, 0.001);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Geometry", "G", "Geometrias capturadas e ordenadas deterministicamente.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Names", "N", "Nomes dos objetos no Rhino (ou nome da camada se sem nome).", GH_ParamAccess.tree);
            pManager.AddTextParameter("IDs", "ID", "GUIDs persistentes dos objetos no documento do Rhino.", GH_ParamAccess.tree);
            pManager.AddTextParameter("UserText", "TXT", "Metadados e User Strings dos objetos (chave=valor).", GH_ParamAccess.tree);
            pManager.AddBoxParameter("BBoxes", "BB", "Caixas delimitadoras (Bounding Boxes) das geometrias.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Summary", "S", "Diagnostico detalhado: camadas resolvidas, contagem e criterio de ordenacao.", GH_ParamAccess.item);
        }

        private class CandidateObject
        {
            public RhinoObject RhinoObj { get; set; }
            public GeometryBase Geometry { get; set; }
            public IGH_GeometricGoo Goo { get; set; }
            public BoundingBox BBox { get; set; }
            public Point3d Center { get; set; }
            public string Name { get; set; }
            public Guid Id { get; set; }
            public string LayerFullPath { get; set; }
            public int LayerIndex { get; set; }
            public Dictionary<string, string> UserStrings { get; set; }
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rhinoDoc = RhinoDoc.ActiveDoc;
            if (rhinoDoc == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum documento ativo do Rhino encontrado.");
                return;
            }

            // 1. Coleta filtros de camada
            var layerFilters = new List<string>();
            if (!DA.GetDataList(0, layerFilters) || layerFilters.Count == 0)
            {
                layerFilters = new List<string>(_savedLayerPaths);
            }

            // 2. Filtro de tipo
            int typeInput = 0;
            DA.GetData(1, ref typeInput);

            // 3. Modo de ordenacao
            int sortInput = (int)SortMode;
            DA.GetData(2, ref sortInput);
            if (Enum.IsDefined(typeof(LayerPipelineSortMode), sortInput))
            {
                SortMode = (LayerPipelineSortMode)sortInput;
            }

            // 4. Chave do PillHub
            string rawKey = "";
            DA.GetData(3, ref rawKey);

            // 5. Tolerancia espacial
            double tol = 0.001;
            DA.GetData(4, ref tol);
            if (tol < 0.0) tol = 0.0;

            // 6. Resolve camadas ativas pelo FullPath ou Nome Simples (inclui subcamadas)
            var matchedLayers = ResolveLayersByFullPath(rhinoDoc, layerFilters);

            var sb = new StringBuilder();
            sb.AppendLine("=== PILL INVARIANT GEOMETRY PIPELINE ===");
            sb.AppendLine($"Criterio de Ordenacao: {GetSortModeName(SortMode)}");
            sb.AppendLine($"Filtro de Tipo: {GetTypeName(typeInput)}");
            sb.AppendLine($"Camadas Resolvidas ({matchedLayers.Count}):");
            foreach (var l in matchedLayers)
            {
                int count = 0;
                try
                {
                    var o = rhinoDoc.Objects.FindByLayer(l);
                    if (o != null) count = o.Length;
                }
                catch { }
                sb.AppendLine($"  - [{l.Index}] {l.FullPath} ({count} objs no Rhino)");
            }

            if (matchedLayers.Count == 0)
            {
                CapturedObjectCount = 0;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Nenhuma camada correspondente encontrada no Rhino. Dê duplo clique no componente para selecionar as camadas.");
                SetEmptyOutputs(DA, sb.ToString());
                return;
            }

            // 7. Coleta de objetos pertencentes as camadas resolvidas
            var candidates = new List<CandidateObject>();
            var visitedObjGuids = new HashSet<Guid>();

            // Método 1: Busca direta na tabela de camadas do Rhino (rápido, inclui locked/hidden da camada)
            foreach (var layer in matchedLayers)
            {
                try
                {
                    var layerObjs = rhinoDoc.Objects.FindByLayer(layer);
                    if (layerObjs != null)
                    {
                        foreach (var rhObj in layerObjs)
                        {
                            if (rhObj == null || rhObj.IsDeleted) continue;
                            if (!visitedObjGuids.Add(rhObj.Id)) continue;
                            ProcessRhinoObject(rhObj, layer, candidates, typeInput);
                        }
                    }
                }
                catch { }
            }

            // Método 2: Varredura de fallback permissiva via ObjectEnumeratorSettings
            // (Captura objetos de referência externa, worksessions, instâncias e casos especiais)
            try
            {
                var settings = new ObjectEnumeratorSettings
                {
                    ActiveObjects = true,
                    LockedObjects = true,
                    HiddenObjects = true,
                    ReferenceObjects = true,
                    DeletedObjects = false,
                    IncludeLights = false,
                    IncludeGrips = false
                };

                var targetLayerIndices = new HashSet<int>(matchedLayers.Select(l => l.Index));
                foreach (var rhObj in rhinoDoc.Objects.GetObjectList(settings))
                {
                    if (rhObj == null || rhObj.IsDeleted) continue;
                    if (!targetLayerIndices.Contains(rhObj.Attributes.LayerIndex)) continue;
                    if (!visitedObjGuids.Add(rhObj.Id)) continue;

                    var layer = rhinoDoc.Layers.FindIndex(rhObj.Attributes.LayerIndex);
                    ProcessRhinoObject(rhObj, layer, candidates, typeInput);
                }
            }
            catch { }

            // 8. Aplicacao da Ordenacao Canonica Deterministica Estrita
            candidates = SortCandidatesDeterministically(candidates, SortMode, tol);
            CapturedObjectCount = candidates.Count;

            sb.AppendLine($"Total de Geometrias Capturadas: {candidates.Count}");
            if (candidates.Count == 0)
            {
                sb.AppendLine("Aviso: As camadas foram encontradas, mas nenhuma geometria correspondeu ao tipo de filtro selecionado ou as camadas estão vazias no Rhino.");
            }

            // 9. Preenche arvores de saida
            var geomTree = new GH_Structure<IGH_GeometricGoo>();
            var namesTree = new GH_Structure<GH_String>();
            var idsTree = new GH_Structure<GH_String>();
            var userTextTree = new GH_Structure<GH_String>();
            var bboxesTree = new GH_Structure<GH_Box>();

            if (GroupByLayer)
            {
                var groups = candidates.GroupBy(c => c.LayerFullPath).OrderBy(g => g.Key, NaturalStringComparer.Instance);
                int branchIdx = 0;
                foreach (var group in groups)
                {
                    var path = new GH_Path(branchIdx);
                    foreach (var item in group)
                    {
                        geomTree.Append(item.Goo, path);
                        namesTree.Append(new GH_String(item.Name), path);
                        idsTree.Append(new GH_String(item.Id.ToString()), path);
                        bboxesTree.Append(new GH_Box(new Box(item.BBox)), path);

                        string utSummary = item.UserStrings.Count > 0 
                            ? string.Join("; ", item.UserStrings.Select(kv => $"{kv.Key}={kv.Value}"))
                            : "";
                        userTextTree.Append(new GH_String(utSummary), path);
                    }
                    branchIdx++;
                }
            }
            else
            {
                var path = new GH_Path(0);
                foreach (var item in candidates)
                {
                    geomTree.Append(item.Goo, path);
                    namesTree.Append(new GH_String(item.Name), path);
                    idsTree.Append(new GH_String(item.Id.ToString()), path);
                    bboxesTree.Append(new GH_Box(new Box(item.BBox)), path);

                    string utSummary = item.UserStrings.Count > 0 
                        ? string.Join("; ", item.UserStrings.Select(kv => $"{kv.Key}={kv.Value}"))
                        : "";
                    userTextTree.Append(new GH_String(utSummary), path);
                }
            }

            DA.SetDataTree(0, geomTree);
            DA.SetDataTree(1, namesTree);
            DA.SetDataTree(2, idsTree);
            DA.SetDataTree(3, userTextTree);
            DA.SetDataTree(4, bboxesTree);
            DA.SetData(5, sb.ToString());

            // 10. Publicacao automatica no PillHub se a chave foi configurada
            if (!string.IsNullOrWhiteSpace(rawKey))
            {
                PillHub.ParseKeyMetadata(rawKey, null, out string cleanKey, out string category, out string unit, out Color catColor);
                CurrentCleanKey = cleanKey;
                CurrentCategory = string.IsNullOrEmpty(category) || category.Equals("GEN", StringComparison.OrdinalIgnoreCase) ? "GEO" : category;
                CurrentUnit = string.IsNullOrEmpty(unit) ? "m" : unit;
                CurrentCategoryColor = PillHub.GetCategoryColor(CurrentCategory);

                if (!string.IsNullOrEmpty(_lastPublishedKey) && !string.Equals(_lastPublishedKey, cleanKey, StringComparison.OrdinalIgnoreCase))
                {
                    PillHub.Unpublish(_lastPublishedKey, InstanceGuid);
                }
                _lastPublishedKey = cleanKey;

                // Converte para estrutura IGH_Goo imutavel para o PillHub
                var genericTree = new GH_Structure<IGH_Goo>();
                foreach (var p in geomTree.Paths)
                {
                    foreach (var g in geomTree[p]) genericTree.Append(g, p);
                }

                Guid currentDocId = OnPingDocument()?.DocumentID ?? Guid.Empty;
                PillHub.Publish(rawKey, genericTree, InstanceGuid, currentDocId, CurrentUnit);

                Message = $"[{CurrentCategory}] {candidates.Count} Geoms\n({GetSortModeName(SortMode)})";
            }
            else
            {
                if (!string.IsNullOrEmpty(_lastPublishedKey))
                {
                    PillHub.Unpublish(_lastPublishedKey, InstanceGuid);
                    _lastPublishedKey = "";
                }
                CurrentCleanKey = "";
                CurrentCategory = "GEO";
                CurrentCategoryColor = Color.FromArgb(46, 175, 100);
                Message = $"{candidates.Count} Geoms\n({GetSortModeName(SortMode)})";
            }
        }

        private void SetEmptyOutputs(IGH_DataAccess DA, string summary)
        {
            DA.SetDataTree(0, new GH_Structure<IGH_GeometricGoo>());
            DA.SetDataTree(1, new GH_Structure<GH_String>());
            DA.SetDataTree(2, new GH_Structure<GH_String>());
            DA.SetDataTree(3, new GH_Structure<GH_String>());
            DA.SetDataTree(4, new GH_Structure<GH_Box>());
            DA.SetData(5, summary);
            Message = "0 Geometrias";
        }

        private static List<Layer> ResolveLayersByFullPath(RhinoDoc doc, List<string> layerQueries)
        {
            var result = new List<Layer>();
            if (doc == null || doc.Layers == null) return result;

            if (layerQueries == null || layerQueries.Count == 0)
            {
                // Se nenhum filtro informado, retorna todas as camadas ativas
                foreach (var l in doc.Layers)
                {
                    if (!l.IsDeleted) result.Add(l);
                }
                return result;
            }

            var matchedIds = new HashSet<Guid>();

            foreach (var query in layerQueries)
            {
                if (string.IsNullOrWhiteSpace(query)) continue;
                string q = query.Trim();

                if (q.StartsWith("\"") && q.EndsWith("\"") && q.Length > 1)
                    q = q.Substring(1, q.Length - 2).Trim();

                bool isWildcard = q.Contains("*") || q.Contains("?");
                Regex regex = null;
                if (isWildcard)
                {
                    string pattern = "^" + Regex.Escape(q).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                    regex = new Regex(pattern, RegexOptions.IgnoreCase);
                }

                foreach (var l in doc.Layers)
                {
                    if (l.IsDeleted) continue;

                    string fullPath = l.FullPath ?? "";
                    string name = l.Name ?? "";

                    bool isMatch = false;

                    // 1. Comparação exata de FullPath (ignora maiúsculas/minúsculas)
                    if (string.Equals(fullPath, q, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                    }
                    // 2. Comparação exata de Nome Simples
                    else if (string.Equals(name, q, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                    }
                    // 3. Subcamadas automáticas: se q é "01_ARQ", inclui "01_ARQ::PAREDES", etc.
                    else if (fullPath.StartsWith(q + "::", StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                    }
                    // 4. Wildcard matching
                    else if (regex != null && (regex.IsMatch(fullPath) || regex.IsMatch(name)))
                    {
                        isMatch = true;
                    }

                    if (isMatch && matchedIds.Add(l.Id))
                    {
                        result.Add(l);
                    }
                }
            }

            return result;
        }

        private static void ProcessRhinoObject(
            RhinoObject rhObj, 
            Layer layer, 
            List<CandidateObject> candidates, 
            int typeInput)
        {
            if (rhObj == null || rhObj.IsDeleted) return;

            // Suporte completo a Blocos (InstanceObject)
            if (rhObj is InstanceObject instObj)
            {
                var idef = instObj.InstanceDefinition;
                if (idef != null)
                {
                    var xform = instObj.InstanceXform;
                    var subObjs = idef.GetObjects();
                    if (subObjs != null)
                    {
                        int subIdx = 0;
                        foreach (var subObj in subObjs)
                        {
                            if (subObj == null || subObj.Geometry == null) continue;
                            if (!MatchesObjectType(subObj.ObjectType, subObj.Geometry, typeInput)) continue;

                            var geomDup = subObj.Geometry.Duplicate();
                            geomDup.Transform(xform);

                            var subGoo = ConvertGeometryToGoo(geomDup);
                            if (subGoo == null) continue;

                            var bbox = geomDup.GetBoundingBox(true);
                            var center = bbox.IsValid ? bbox.Center : Point3d.Origin;
                            string name = rhObj.Attributes.Name;
                            if (string.IsNullOrWhiteSpace(name))
                                name = $"{idef.Name}_{subIdx}";

                            candidates.Add(new CandidateObject
                            {
                                RhinoObj = rhObj,
                                Geometry = geomDup,
                                Goo = subGoo,
                                BBox = bbox,
                                Center = center,
                                Name = name,
                                Id = rhObj.Id,
                                LayerFullPath = layer?.FullPath ?? "Desconhecida",
                                LayerIndex = rhObj.Attributes.LayerIndex,
                                UserStrings = ExtractUserStrings(rhObj)
                            });
                            subIdx++;
                        }
                    }
                }
                return;
            }

            var geom = rhObj.Geometry;
            if (geom == null) return;

            if (!MatchesObjectType(rhObj.ObjectType, geom, typeInput)) return;

            IGH_GeometricGoo goo = null;
            try
            {
                goo = GH_Convert.ToGeometricGoo(rhObj.Id);
            }
            catch { }

            if (goo == null || !goo.IsValid)
            {
                goo = ConvertGeometryToGoo(geom);
            }

            if (goo == null) return;

            var bboxFinal = geom.GetBoundingBox(true);
            var centerFinal = bboxFinal.IsValid ? bboxFinal.Center : Point3d.Origin;
            string objName = rhObj.Attributes.Name;
            if (string.IsNullOrWhiteSpace(objName))
            {
                objName = layer != null ? layer.Name : rhObj.Id.ToString();
            }

            candidates.Add(new CandidateObject
            {
                RhinoObj = rhObj,
                Geometry = geom,
                Goo = goo,
                BBox = bboxFinal,
                Center = centerFinal,
                Name = objName,
                Id = rhObj.Id,
                LayerFullPath = layer?.FullPath ?? "Desconhecida",
                LayerIndex = rhObj.Attributes.LayerIndex,
                UserStrings = ExtractUserStrings(rhObj)
            });
        }

        private static bool MatchesObjectType(ObjectType objType, GeometryBase geom, int typeInput)
        {
            switch (typeInput)
            {
                case 0: // Qualquer
                    return true;
                case 1: // Brep / Superficie / Extrusao
                    return (objType & (ObjectType.Brep | ObjectType.Surface | ObjectType.Extrusion)) != 0 ||
                           geom is Brep || geom is Surface || geom is Extrusion;
                case 2: // Malha (Mesh)
                    return (objType & ObjectType.Mesh) != 0 || geom is Mesh;
                case 3: // Curva
                    return (objType & ObjectType.Curve) != 0 || geom is Curve;
                case 4: // Ponto
                    return (objType & (ObjectType.Point | ObjectType.PointSet)) != 0 ||
                           geom is Rhino.Geometry.Point || geom is PointCloud;
                case 5: // SubD
                    return (objType & ObjectType.SubD) != 0 || geom is SubD;
                case 6: // Texto / Anotacao
                    return (objType & (ObjectType.Annotation | ObjectType.TextDot)) != 0 ||
                           geom is AnnotationBase || geom is TextDot;
                case 7: // Extrusao
                    return (objType & ObjectType.Extrusion) != 0 || geom is Extrusion;
                default:
                    return true;
            }
        }

        private static Dictionary<string, string> ExtractUserStrings(RhinoObject rhObj)
        {
            var dict = new Dictionary<string, string>();
            if (rhObj == null) return dict;
            var keys = rhObj.Attributes.GetUserStrings();
            if (keys != null)
            {
                for (int k = 0; k < keys.Count; k++)
                {
                    string kName = keys.GetKey(k);
                    dict[kName] = keys.Get(kName);
                }
            }
            return dict;
        }

        private static List<CandidateObject> SortCandidatesDeterministically(
            List<CandidateObject> list, 
            LayerPipelineSortMode mode, 
            double tol)
        {
            if (list == null || list.Count <= 1) return list;

            double invTol = tol > 0 ? 1.0 / tol : 1000.0;

            switch (mode)
            {
                case LayerPipelineSortMode.Spatial3D:
                    return list.OrderBy(c => Math.Round(c.Center.X * invTol))
                               .ThenBy(c => Math.Round(c.Center.Y * invTol))
                               .ThenBy(c => Math.Round(c.Center.Z * invTol))
                               .ThenBy(c => c.Name, NaturalStringComparer.Instance)
                               .ThenBy(c => c.Id)
                               .ToList();

                case LayerPipelineSortMode.Spatial2D:
                    return list.OrderBy(c => Math.Round(c.Center.X * invTol))
                               .ThenBy(c => Math.Round(c.Center.Y * invTol))
                               .ThenBy(c => Math.Round(c.Center.Z * invTol))
                               .ThenBy(c => c.Name, NaturalStringComparer.Instance)
                               .ThenBy(c => c.Id)
                               .ToList();

                case LayerPipelineSortMode.NaturalName:
                    return list.OrderBy(c => c.Name, NaturalStringComparer.Instance)
                               .ThenBy(c => Math.Round(c.Center.X * invTol))
                               .ThenBy(c => Math.Round(c.Center.Y * invTol))
                               .ThenBy(c => Math.Round(c.Center.Z * invTol))
                               .ThenBy(c => c.Id)
                               .ToList();

                case LayerPipelineSortMode.NameThenGuid:
                    return list.OrderBy(c => c.Name, NaturalStringComparer.Instance)
                               .ThenBy(c => c.Id)
                               .ToList();

                case LayerPipelineSortMode.GuidOnly:
                    return list.OrderBy(c => c.Id).ToList();

                default:
                    return list;
            }
        }

        private static IGH_GeometricGoo ConvertGeometryToGoo(GeometryBase geom)
        {
            if (geom == null) return null;
            if (geom is Brep b) return new GH_Brep(b);
            if (geom is Mesh m) return new GH_Mesh(m);
            if (geom is Extrusion ext) return new GH_Brep(ext.ToBrep());
            if (geom is Curve c) return GH_Convert.ToGeometricGoo(c);
            if (geom is Rhino.Geometry.Point p) return new GH_Point(p.Location);
            if (geom is SubD subD) return new GH_SubD(subD);
            if (geom is PointCloud pc && pc.Count > 0) return new GH_Point(pc[0].Location);
            return GH_Convert.ToGeometricGoo(geom);
        }

        private static string GetTypeName(int typeInput)
        {
            switch (typeInput)
            {
                case 1: return "1 (Brep / Superficie)";
                case 2: return "2 (Malha / Mesh)";
                case 3: return "3 (Curva)";
                case 4: return "4 (Ponto)";
                case 5: return "5 (SubD)";
                case 6: return "6 (Texto / Anotacao)";
                case 7: return "7 (Extrusao)";
                default: return "0 (Qualquer Geometria)";
            }
        }

        private static string GetSortModeName(LayerPipelineSortMode mode)
        {
            switch (mode)
            {
                case LayerPipelineSortMode.Spatial3D: return "Espacial 3D (X->Y->Z)";
                case LayerPipelineSortMode.Spatial2D: return "Espacial 2D (Plano XY)";
                case LayerPipelineSortMode.NaturalName: return "Nome Natural (P1..P10)";
                case LayerPipelineSortMode.NameThenGuid: return "Nome -> GUID";
                case LayerPipelineSortMode.GuidOnly: return "GUID Estavel";
                default: return mode.ToString();
            }
        }

        // =========================================================================
        // JANELA VISUAL DE SELEÇÃO DE CAMADAS (POPUP INTERATIVO MODAL)
        // =========================================================================
        public void OpenLayerPickerModal()
        {
            var rhinoDoc = RhinoDoc.ActiveDoc;
            if (rhinoDoc == null)
            {
                MessageBox.Show("Nenhum documento do Rhino está aberto no momento.", 
                    "Pill Layer Pipeline", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var form = new Form())
            {
                form.Text = "Seletor Visual de Camadas - Pill Layer Pipeline";
                form.Size = new Size(520, 640);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.Sizable;
                form.MinimumSize = new Size(420, 480);
                form.Font = new Font("Segoe UI", 9.5f);
                form.ShowIcon = false;

                // Estado de seleção em tempo real persistente (sobrevive a pesquisas e filtros)
                var workingSelected = new HashSet<string>(_savedLayerPaths, StringComparer.OrdinalIgnoreCase);

                // Top Search Panel
                var topPanel = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(12, 8, 12, 6) };
                var btnClearSearch = new Button 
                { 
                    Text = "✕", 
                    Width = 30, 
                    Dock = DockStyle.Right, 
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnClearSearch.FlatAppearance.BorderSize = 0;

                var txtSearch = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 10.5f)
                };
                btnClearSearch.Click += (s, e) => { txtSearch.Text = ""; txtSearch.Focus(); };

                topPanel.Controls.Add(txtSearch);
                topPanel.Controls.Add(btnClearSearch);

                // Quick Action buttons below search
                var actionPanel = new Panel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(12, 2, 12, 6) };
                var btnAll = new Button { Text = "Todas", Width = 70, Height = 28, Dock = DockStyle.Left };
                var btnNone = new Button { Text = "Nenhuma", Width = 80, Height = 28, Dock = DockStyle.Left };
                var btnExpand = new Button { Text = "Expandir", Width = 80, Height = 28, Dock = DockStyle.Left };
                var btnCollapse = new Button { Text = "Recolher", Width = 80, Height = 28, Dock = DockStyle.Left };
                actionPanel.Controls.Add(btnCollapse);
                actionPanel.Controls.Add(btnExpand);
                actionPanel.Controls.Add(btnNone);
                actionPanel.Controls.Add(btnAll);

                // TreeView
                var treeView = new TreeView
                {
                    Dock = DockStyle.Fill,
                    CheckBoxes = true,
                    ShowLines = true,
                    ShowPlusMinus = true,
                    ShowRootLines = true,
                    Font = new Font("Segoe UI", 10f)
                };

                // Bottom Panel
                var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(12, 10, 12, 10) };
                var lblStatus = new Label { Dock = DockStyle.Left, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9.5f, FontStyle.Italic) };
                var btnCancel = new Button { Text = "Cancelar", Width = 85, Height = 34, Dock = DockStyle.Right, DialogResult = DialogResult.Cancel };
                var btnOk = new Button { Text = "Salvar e Aplicar", Width = 130, Height = 34, Dock = DockStyle.Right, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(46, 175, 100), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                bottomPanel.Controls.Add(lblStatus);
                bottomPanel.Controls.Add(btnOk);
                bottomPanel.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
                bottomPanel.Controls.Add(btnCancel);

                void PopulateTree(string filter = "")
                {
                    treeView.BeginUpdate();
                    treeView.Nodes.Clear();

                    var sortedLayers = rhinoDoc.Layers.Where(l => !l.IsDeleted)
                        .OrderBy(l => l.FullPath, NaturalStringComparer.Instance).ToList();

                    var nodeMap = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);

                    foreach (var layer in sortedLayers)
                    {
                        string fullPath = layer.FullPath;
                        if (!string.IsNullOrWhiteSpace(filter))
                        {
                            if (fullPath.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                                layer.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                continue;
                            }
                        }

                        int objCount = 0;
                        try
                        {
                            var objs = rhinoDoc.Objects.FindByLayer(layer);
                            if (objs != null) objCount = objs.Length;
                        }
                        catch { }

                        string countText = objCount > 0 ? $" ({objCount} objs)" : "";

                        string[] parts = fullPath.Split(new[] { "::" }, StringSplitOptions.None);
                        TreeNode parentNode = null;
                        string accumulated = "";

                        for (int i = 0; i < parts.Length; i++)
                        {
                            accumulated = i == 0 ? parts[0] : accumulated + "::" + parts[i];

                            if (!nodeMap.TryGetValue(accumulated, out var node))
                            {
                                string label = (i == parts.Length - 1) ? parts[i] + countText : parts[i];
                                node = new TreeNode(label)
                                {
                                    Tag = accumulated,
                                    Checked = workingSelected.Contains(accumulated)
                                };
                                nodeMap[accumulated] = node;

                                if (parentNode == null)
                                    treeView.Nodes.Add(node);
                                else
                                    parentNode.Nodes.Add(node);
                            }
                            parentNode = node;
                        }
                    }

                    treeView.ExpandAll();
                    treeView.EndUpdate();
                    UpdateStatus();
                }

                void UpdateStatus()
                {
                    int checkedInView = CountCheckedNodes(treeView.Nodes);
                    if (string.IsNullOrWhiteSpace(txtSearch.Text))
                    {
                        lblStatus.Text = $"{workingSelected.Count} camada(s) selecionada(s)";
                    }
                    else
                    {
                        lblStatus.Text = $"{workingSelected.Count} total selecionada(s) ({checkedInView} no filtro)";
                    }
                }

                int CountCheckedNodes(TreeNodeCollection nodes)
                {
                    int c = 0;
                    foreach (TreeNode n in nodes)
                    {
                        if (n.Checked) c++;
                        c += CountCheckedNodes(n.Nodes);
                    }
                    return c;
                }

                void UpdateSelectionState(TreeNode node, bool isChecked)
                {
                    if (node.Tag is string path && !string.IsNullOrWhiteSpace(path))
                    {
                        if (isChecked)
                            workingSelected.Add(path);
                        else
                            workingSelected.Remove(path);
                    }

                    foreach (TreeNode child in node.Nodes)
                    {
                        child.Checked = isChecked;
                        UpdateSelectionState(child, isChecked);
                    }
                }

                bool updatingChildren = false;
                treeView.AfterCheck += (s, e) =>
                {
                    if (updatingChildren) return;
                    updatingChildren = true;
                    try
                    {
                        UpdateSelectionState(e.Node, e.Node.Checked);
                    }
                    finally
                    {
                        updatingChildren = false;
                    }
                    UpdateStatus();
                };

                txtSearch.TextChanged += (s, e) =>
                {
                    PopulateTree(txtSearch.Text.Trim());
                };

                btnAll.Click += (s, e) =>
                {
                    updatingChildren = true;
                    try
                    {
                        void CheckAndAdd(TreeNodeCollection nodes)
                        {
                            foreach (TreeNode n in nodes)
                            {
                                n.Checked = true;
                                if (n.Tag is string p && !string.IsNullOrWhiteSpace(p))
                                    workingSelected.Add(p);
                                CheckAndAdd(n.Nodes);
                            }
                        }
                        CheckAndAdd(treeView.Nodes);
                    }
                    finally
                    {
                        updatingChildren = false;
                    }
                    UpdateStatus();
                };

                btnNone.Click += (s, e) =>
                {
                    updatingChildren = true;
                    try
                    {
                        void UncheckAndRemove(TreeNodeCollection nodes)
                        {
                            foreach (TreeNode n in nodes)
                            {
                                n.Checked = false;
                                if (n.Tag is string p)
                                    workingSelected.Remove(p);
                                UncheckAndRemove(n.Nodes);
                            }
                        }
                        UncheckAndRemove(treeView.Nodes);

                        if (string.IsNullOrWhiteSpace(txtSearch.Text))
                        {
                            workingSelected.Clear();
                        }
                    }
                    finally
                    {
                        updatingChildren = false;
                    }
                    UpdateStatus();
                };

                btnExpand.Click += (s, e) => treeView.ExpandAll();
                btnCollapse.Click += (s, e) => treeView.CollapseAll();

                PopulateTree();

                form.Controls.Add(treeView);
                form.Controls.Add(actionPanel);
                form.Controls.Add(topPanel);
                form.Controls.Add(bottomPanel);
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    RecordUndoEvent("Editar Seleção de Camadas");
                    _savedLayerPaths.Clear();
                    _savedLayerPaths.AddRange(workingSelected);
                    m_attributes?.ExpireLayout();
                    ExpireSolution(true);
                }
            }
        }

        // =========================================================================
        // SERIALIZACAO ROBUSTA (SALVA NOMES DE CAMADAS E CONFIGURACAO)
        // =========================================================================
        public override bool Write(GH_IWriter writer)
        {
            writer.SetInt32("SortMode", (int)SortMode);
            writer.SetInt32("FilterType", (int)FilterType);
            writer.SetBoolean("GroupByLayer", GroupByLayer);
            writer.SetBoolean("LiveTracking", LiveTrackingEnabled);

            writer.SetInt32("LayerCount", _savedLayerPaths.Count);
            for (int i = 0; i < _savedLayerPaths.Count; i++)
            {
                writer.SetString("LayerPath", i, _savedLayerPaths[i]);
            }

            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("SortMode")) SortMode = (LayerPipelineSortMode)reader.GetInt32("SortMode");
            if (reader.ItemExists("FilterType")) FilterType = (ObjectType)reader.GetInt32("FilterType");
            if (reader.ItemExists("GroupByLayer")) GroupByLayer = reader.GetBoolean("GroupByLayer");
            if (reader.ItemExists("LiveTracking")) LiveTrackingEnabled = reader.GetBoolean("LiveTracking");

            _savedLayerPaths.Clear();
            if (reader.ItemExists("LayerCount"))
            {
                int count = reader.GetInt32("LayerCount");
                for (int i = 0; i < count; i++)
                {
                    if (reader.ItemExists("LayerPath", i))
                    {
                        _savedLayerPaths.Add(reader.GetString("LayerPath", i));
                    }
                }
            }

            return base.Read(reader);
        }

        // =========================================================================
        // CABOS FISICOS OCULTOS (SINCRONIA WALLACEI)
        // =========================================================================
        public int ConnectMyReceiversWithHiddenWires(GH_Document doc = null)
        {
            if (doc == null) doc = OnPingDocument();
            if (doc == null || string.IsNullOrWhiteSpace(CurrentCleanKey)) return 0;

            int count = 0;
            foreach (var obj in doc.Objects)
            {
                if (obj is PillReceiver_Component rx &&
                    string.Equals(rx.CurrentCleanKey, CurrentCleanKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (rx.AutoConnectHiddenWire(doc, CurrentCleanKey)) count++;
                }
                else if (obj is PillHook_Component hook &&
                    string.Equals(hook.CurrentCleanKey, CurrentCleanKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (hook.AutoConnectHiddenWire(doc, CurrentCleanKey)) count++;
                }
            }
            return count;
        }

        public int DisconnectMyReceivers(GH_Document doc = null)
        {
            if (doc == null) doc = OnPingDocument();
            if (doc == null || string.IsNullOrWhiteSpace(CurrentCleanKey)) return 0;

            int count = 0;
            foreach (var obj in doc.Objects)
            {
                if (obj is PillReceiver_Component rx &&
                    string.Equals(rx.CurrentCleanKey, CurrentCleanKey, StringComparison.OrdinalIgnoreCase))
                {
                    rx.DisconnectHiddenWire();
                    count++;
                }
                else if (obj is PillHook_Component hook &&
                    string.Equals(hook.CurrentCleanKey, CurrentCleanKey, StringComparison.OrdinalIgnoreCase))
                {
                    hook.DisconnectHiddenWire();
                    count++;
                }
            }
            return count;
        }

        // =========================================================================
        // MENU DE CONTEXTO INTERATIVO (ARVORE DE CAMADAS E MODOS)
        // =========================================================================
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            // 0. Botão Destaque: Abrir Seletor Visual de Camadas
            var openVisualPickerItem = new ToolStripMenuItem("📁 Abrir Seletor Visual de Camadas (Janela)...")
            {
                Font = new Font(menu.Font, FontStyle.Bold)
            };
            openVisualPickerItem.Click += (s, e) => OpenLayerPickerModal();
            menu.Items.Add(openVisualPickerItem);

            menu.Items.Add(new ToolStripSeparator());

            // 1. Submenu de Selecao de Camadas Rapida
            var layersMenu = new ToolStripMenuItem("Marcar Camadas do Rhino Diretamente");
            var doc = RhinoDoc.ActiveDoc;
            if (doc != null && doc.Layers != null)
            {
                var sortedLayers = doc.Layers.Where(l => !l.IsDeleted).OrderBy(l => l.FullPath, NaturalStringComparer.Instance).ToList();
                foreach (var l in sortedLayers)
                {
                    string path = l.FullPath;
                    int objCount = 0;
                    try
                    {
                        var objs = doc.Objects.FindByLayer(l);
                        if (objs != null) objCount = objs.Length;
                    }
                    catch { }

                    string title = objCount > 0 ? $"{path} ({objCount} objs)" : path;
                    var item = new ToolStripMenuItem(title)
                    {
                        Checked = _savedLayerPaths.Contains(path, StringComparer.OrdinalIgnoreCase)
                    };
                    item.Click += (s, e) =>
                    {
                        RecordUndoEvent("Toggle Camada Pipeline");
                        if (_savedLayerPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                            _savedLayerPaths.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
                        else
                            _savedLayerPaths.Add(path);

                        m_attributes?.ExpireLayout();
                        ExpireSolution(true);
                    };
                    layersMenu.DropDownItems.Add(item);
                }
            }
            else
            {
                layersMenu.DropDownItems.Add(new ToolStripMenuItem("Nenhum documento Rhino aberto") { Enabled = false });
            }
            menu.Items.Add(layersMenu);

            // 2. Submenu de Modo de Ordenacao
            var sortSubMenu = new ToolStripMenuItem("Criterio de Ordenacao Canonica");
            foreach (LayerPipelineSortMode mode in Enum.GetValues(typeof(LayerPipelineSortMode)))
            {
                var sItem = new ToolStripMenuItem(GetSortModeName(mode))
                {
                    Checked = SortMode == mode
                };
                var targetMode = mode;
                sItem.Click += (s, e) =>
                {
                    RecordUndoEvent("Mudar Ordenacao Pipeline");
                    SortMode = targetMode;
                    ExpireSolution(true);
                };
                sortSubMenu.DropDownItems.Add(sItem);
            }
            menu.Items.Add(sortSubMenu);

            // 3. Opcao de Agrupamento por Camada em Branches {i}
            var groupItem = new ToolStripMenuItem("Agrupar Geometrias por Camada em Branches {i}")
            {
                Checked = GroupByLayer
            };
            groupItem.Click += (s, e) =>
            {
                RecordUndoEvent("Toggle Agrupamento Pipeline");
                GroupByLayer = !GroupByLayer;
                ExpireSolution(true);
            };
            menu.Items.Add(groupItem);

            // 4. Live Tracking do Rhino
            var liveItem = new ToolStripMenuItem("Live Tracking (Auto-Update ao Editar no Rhino)")
            {
                Checked = LiveTrackingEnabled
            };
            liveItem.Click += (s, e) =>
            {
                RecordUndoEvent("Toggle Live Tracking Pipeline");
                LiveTrackingEnabled = !LiveTrackingEnabled;
            };
            menu.Items.Add(liveItem);

            // 5. Conexao Oculta (Modo Wallacei)
            menu.Items.Add(new ToolStripSeparator());
            var hiddenMenu = new ToolStripMenuItem("⚡ Conexao Oculta (Modo Wallacei / Hidden Wire)");

            if (!string.IsNullOrWhiteSpace(CurrentCleanKey))
            {
                var connectMyItem = new ToolStripMenuItem($"Conectar meus Receptores de '{CurrentCleanKey}' com Cabo Oculto");
                connectMyItem.Click += (s, e) =>
                {
                    var ghDoc = OnPingDocument();
                    if (ghDoc == null) return;
                    int count = ConnectMyReceiversWithHiddenWires(ghDoc);
                    ghDoc.NewSolution(false);
                    MessageBox.Show($"{count} receptor(es) conectado(s) com cabo oculto ao canal '{CurrentCleanKey}'.",
                        "Pill Layer Pipeline", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                hiddenMenu.DropDownItems.Add(connectMyItem);

                var disconnectMyItem = new ToolStripMenuItem($"Desconectar meus Receptores de '{CurrentCleanKey}'");
                disconnectMyItem.Click += (s, e) =>
                {
                    var ghDoc = OnPingDocument();
                    if (ghDoc == null) return;
                    int count = DisconnectMyReceivers(ghDoc);
                    ghDoc.NewSolution(false);
                    MessageBox.Show($"{count} receptor(es) desconectado(s) de '{CurrentCleanKey}'.",
                        "Pill Layer Pipeline", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                hiddenMenu.DropDownItems.Add(disconnectMyItem);

                hiddenMenu.DropDownItems.Add(new ToolStripSeparator());
            }

            var connectAllItem = new ToolStripMenuItem("⚡ CONECTAR TODOS os Pills do Canvas com Cabos Ocultos (Recomendado para Wallacei)");
            connectAllItem.Click += (s, e) =>
            {
                var ghDoc = OnPingDocument();
                if (ghDoc == null) return;
                int c = PillHub.ConnectAllDocumentPillsHidden(ghDoc);
                ghDoc.NewSolution(false);
                MessageBox.Show($"Todos os {c} receptores do Canvas foram conectados com cabos ocultos (hidden wire)!\nA sequencia DAG esta garantida para Wallacei e Galapagos.",
                    "Pill System - Modo Wallacei Ativado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            hiddenMenu.DropDownItems.Add(connectAllItem);

            var disconnectAllItem = new ToolStripMenuItem("Desconectar TODOS os Pills do Canvas (Voltar ao Modo 100% Sem Fio)");
            disconnectAllItem.Click += (s, e) =>
            {
                var ghDoc = OnPingDocument();
                if (ghDoc == null) return;
                int c = PillHub.DisconnectAllDocumentPillsHidden(ghDoc);
                ghDoc.NewSolution(false);
                MessageBox.Show($"Todos os {c} receptores do Canvas voltaram ao modo 100% sem fio em memoria.",
                    "Pill System - Modo Sem Fio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            hiddenMenu.DropDownItems.Add(disconnectAllItem);

            menu.Items.Add(hiddenMenu);
        }
    }

    /// <summary>
    /// Comparador alfanumerico natural para ordenar strings humanas como Parede_1, Parede_2, Parede_10.
    /// </summary>
    public class NaturalStringComparer : IComparer<string>
    {
        public static readonly NaturalStringComparer Instance = new NaturalStringComparer();

        public int Compare(string x, string y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            int ix = 0, iy = 0;
            while (ix < x.Length && iy < y.Length)
            {
                if (char.IsDigit(x[ix]) && char.IsDigit(y[iy]))
                {
                    // Extrai sequencia numerica completa
                    int startX = ix;
                    while (ix < x.Length && char.IsDigit(x[ix])) ix++;
                    long numX = long.Parse(x.Substring(startX, ix - startX));

                    int startY = iy;
                    while (iy < y.Length && char.IsDigit(y[iy])) iy++;
                    long numY = long.Parse(y.Substring(startY, iy - startY));

                    int numComp = numX.CompareTo(numY);
                    if (numComp != 0) return numComp;
                }
                else
                {
                    int charComp = char.ToUpperInvariant(x[ix]).CompareTo(char.ToUpperInvariant(y[iy]));
                    if (charComp != 0) return charComp;
                    ix++;
                    iy++;
                }
            }

            return x.Length.CompareTo(y.Length);
        }
    }
}
