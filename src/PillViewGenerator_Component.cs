using GH_IO.Serialization;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Componente no estilo "Pill" que gera e orienta vistas e câmeras 3D no Rhino
    /// a partir da Bounding Box do modelo e de um vetor de orientação da frente.
    /// Gera vistas isométricas (NE, NW, SE, SW), ortogonais (Front, Back, Left, Right, Top, Bottom)
    /// e perspectiva, configurando o Viewport em tempo real, gravando Named Views no Rhino
    /// e exportando nomes e títulos formatados para alimentar diretamente o Pill Viewport 3D Capture.
    /// </summary>
    public class PillViewGenerator_Component : GH_Component
    {
        // ==========================================
        // PROPRIEDADES E CONFIGURAÇÕES
        // ==========================================
        public int ActiveViewIndex = 0;
        public string PresetPrefix = "Model";
        public double PresetPadding = 1.15;
        public bool AutoApplyToViewport = true;
        public bool AutoSaveNamedViews = false;
        public string TargetViewportName = "Perspective";
        public string ProjectionMode = "Auto"; // "Auto", "Parallel", "Perspective"

        // Cache das Vistas Calculadas
        public List<ViewDefinition> GeneratedViews { get; private set; } = new List<ViewDefinition>();
        public ViewDefinition ActiveViewDef { get; private set; } = null;
        public BoundingBox CurrentBBox { get; private set; } = BoundingBox.Unset;
        public Vector3d CurrentModelDirection { get; private set; } = Vector3d.YAxis;
        public Color CategoryColor => Color.FromArgb(14, 165, 233); // Cyan Elétrico Glaux Visual

        // Triggers de Ação Manual
        private bool _forceApply = false;
        private bool _forceSaveNamedViews = false;

        public PillViewGenerator_Component()
            : base(
                "Pill View Generator",
                "PillViewGen",
                "Configura câmeras e gera vistas no Rhino a partir de um Bounding Box e vetor de orientação do modelo. Suporta vistas isométricas (NE, NW, SE, SW), ortogonais (Front, Back, Left, Right, Top, Bottom) e perspectiva, com enquadramento automático, criação de Named Views e exportação de metadados prontos para o Pill Viewport 3D Capture.",
                "Glaux Tools",
                "Visual")
        {
        }

        public override Guid ComponentGuid => new Guid("b7110015-e1ef-4000-8000-000000000015");

        protected override Bitmap Icon => GlauxToolsIcons.PillViewGenerator;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override void CreateAttributes()
        {
            m_attributes = new PillViewGenerator_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter(
                "Bounding Box / Geometry", "BBox",
                "Bounding Box ou geometria(s) do modelo a enquadrar (Brep, Mesh, Curve, SubD, etc.). Se múltiplas geometrias forem fornecidas, calcula a união das caixas delimitadoras automaticamente.",
                GH_ParamAccess.list);

            pManager.AddVectorParameter(
                "Direction", "Dir",
                "Vetor de orientação do modelo que define a 'Frente' ou eixo principal (ex.: Y=(0,1,0) para Norte/Frontal arquitetônico padrão). Se omitido, adota Y=(0,1,0).",
                GH_ParamAccess.item,
                Vector3d.YAxis);

            pManager.AddTextParameter(
                "Views", "Views",
                "Vistas a gerar. Aceita nomes individuais ('Isometric NE', 'Front', 'Top', etc.), grupos ('All', 'All Isometric', 'All Orthogonal') ou lista de vistas. Se omitido, gera 'All' (11 vistas).",
                GH_ParamAccess.list);

            pManager.AddIntegerParameter(
                "Active Index", "Idx",
                "Índice da vista ativa a aplicar ao Viewport e emitir em ActiveName/ActiveTitle (0 = primeira vista). Conecte um Slider ou Pill Pulse Timer para alternar entre vistas automaticamente.",
                GH_ParamAccess.item,
                0);

            pManager.AddTextParameter(
                "Prefix", "Name",
                "Prefixo ou nome base para nomenclatura de arquivos e títulos editoriais (ex.: 'Auditorio', 'Edificio_A'). Padrão: 'Model'.",
                GH_ParamAccess.item,
                "Model");

            pManager.AddNumberParameter(
                "Padding", "Pad",
                "Fator de margem/espaçamento ao redor do Bounding Box para enquadramento perfeito sem cortes (ex.: 1.15 = 15% de margem extra). Padrão: 1.15.",
                GH_ParamAccess.item,
                1.15);

            pManager.AddBooleanParameter(
                "Apply to Viewport", "Apply",
                "Gatilho para aplicar a câmera da vista ativa diretamente ao Viewport do Rhino (True = reorienta o viewport em tempo real). Padrão: True.",
                GH_ParamAccess.item,
                true);

            pManager.AddBooleanParameter(
                "Save Named Views", "Save",
                "Salvar/atualizar todas as vistas geradas na tabela de Named Views do documento ativo do Rhino (True = grava vistas nomeadas). Padrão: False.",
                GH_ParamAccess.item,
                false);

            pManager.AddTextParameter(
                "Target Viewport", "Vp",
                "Nome do Viewport do Rhino a ser modificado (ex.: 'Perspective', 'Top', 'Front'). Se vazio ou omitido, adota 'Perspective' ou o Viewport ativo.",
                GH_ParamAccess.item,
                "Perspective");

            pManager.AddTextParameter(
                "Projection", "Proj",
                "Modo de projeção da câmera:\n- 'Auto' (Paralela para Ortogonais e Isométricas, Perspectiva para Perspectiva)\n- 'Parallel' (Axonométrica pura)\n- 'Perspective' (Perspectiva com ponto de fuga)",
                GH_ParamAccess.item,
                "Auto");

            pManager[0].Optional = false;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
            pManager[8].Optional = true;
            pManager[9].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter(
                "View", "V",
                "Nome do Viewport do Rhino configurado (ex.: 'Perspective'). Conecte diretamente à entrada 'View (V)' do Pill Viewport 3D Capture.",
                GH_ParamAccess.item);

            pManager.AddTextParameter(
                "Active Name", "AName",
                "Nome de arquivo padronizado da vista ativa selecionada (ex.: 'Model_Isometric_NE'). Conecte diretamente à entrada 'FileName (Name)' do Pill Viewport 3D Capture.",
                GH_ParamAccess.item);

            pManager.AddTextParameter(
                "Active Title", "ATtl",
                "Título editorial formatado da vista ativa selecionada (ex.: 'Vista Isométrica NE — Model'). Conecte diretamente à entrada 'Title (Ttl)' do Pill Viewport 3D Capture.",
                GH_ParamAccess.item);

            pManager.AddTextParameter(
                "File Names", "Names",
                "Lista completa de nomes de arquivos padronizados para todas as vistas geradas.",
                GH_ParamAccess.list);

            pManager.AddTextParameter(
                "Titles", "Titles",
                "Lista completa de títulos editoriais padronizados para todas as vistas geradas.",
                GH_ParamAccess.list);

            pManager.AddTextParameter(
                "View Names", "Views",
                "Lista de identificadores/rótulos das vistas geradas (ex.: 'Isometric_NE', 'Front', 'Top').",
                GH_ParamAccess.list);

            pManager.AddPointParameter(
                "Cameras", "Cam",
                "Pontos de posição 3D da câmera no espaço para cada vista.",
                GH_ParamAccess.list);

            pManager.AddPointParameter(
                "Target", "Tgt",
                "Ponto de mira/alvo central da câmera no espaço (centro geométrico do Bounding Box).",
                GH_ParamAccess.item);

            pManager.AddPlaneParameter(
                "Planes", "Pl",
                "Planos de visualização para cada vista (Origem no Target, Z apontando para a Câmera, Y no vetor Up da Câmera).",
                GH_ParamAccess.list);

            pManager.AddTextParameter(
                "Data", "D",
                "Relatório analítico em texto com a tabela de todas as vistas geradas, coordenadas de câmera, vetores de mira e ângulos.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Extração do Bounding Box (suporta múltiplos objetos e geometrias)
            var geomList = new List<object>();
            if (!DA.GetDataList(0, geomList) || geomList == null || geomList.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Conecte um Bounding Box ou geometria(s) na entrada 'BBox'.");
                ClearOutputs(DA);
                return;
            }

            BoundingBox totalBbox = BoundingBox.Unset;
            foreach (var item in geomList)
            {
                if (item == null) continue;
                if (item is BoundingBox bb && bb.IsValid)
                {
                    totalBbox.Union(bb);
                }
                else if (item is GH_Box ghBox && ghBox.Value.IsValid)
                {
                    totalBbox.Union(ghBox.Value.BoundingBox);
                }
                else if (item is IGH_GeometricGoo goo)
                {
                    BoundingBox b = BoundingBox.Unset;
                    if (goo is GH_Brep gb && gb.Value != null) b = gb.Value.GetBoundingBox(true);
                    else if (goo is GH_Mesh gm && gm.Value != null) b = gm.Value.GetBoundingBox(true);
                    else if (goo is GH_Curve gc && gc.Value != null) b = gc.Value.GetBoundingBox(true);
                    else if (goo is GH_Surface gs && gs.Value != null) b = gs.Value.GetBoundingBox(true);
                    else if (goo is GH_Point gp) b = new BoundingBox(gp.Value, gp.Value);
                    else
                    {
                        try
                        {
                            dynamic dyn = goo;
                            if (dyn.Value != null) b = dyn.Value.GetBoundingBox(true);
                        }
                        catch { }
                    }
                    if (b.IsValid) totalBbox.Union(b);
                }
                else if (item is GeometryBase geom)
                {
                    var b = geom.GetBoundingBox(true);
                    if (b.IsValid) totalBbox.Union(b);
                }
            }

            if (!totalBbox.IsValid)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Não foi possível calcular uma Bounding Box válida a partir das geometrias fornecidas.");
                ClearOutputs(DA);
                return;
            }
            CurrentBBox = totalBbox;

            // 2. Vetor de Orientação do Modelo (Frente)
            Vector3d inDir = Vector3d.YAxis;
            DA.GetData(1, ref inDir);
            if (!inDir.IsValid || inDir.Length < 1e-6) inDir = Vector3d.YAxis;
            CurrentModelDirection = inDir;

            // 3. Lista de Seleção de Vistas
            var inViews = new List<string>();
            DA.GetDataList(2, inViews);

            // 4. Índice Ativo
            int inIndex = ActiveViewIndex;
            if (DA.GetData(3, ref inIndex))
            {
                ActiveViewIndex = inIndex;
            }

            // 5. Prefixo de Arquivos / Título
            string inPrefix = PresetPrefix;
            if (DA.GetData(4, ref inPrefix) && !string.IsNullOrWhiteSpace(inPrefix))
            {
                PresetPrefix = inPrefix.Trim();
            }
            string cleanPrefix = SanitizeFileName(PresetPrefix);

            // 6. Padding / Margem
            double inPadding = PresetPadding;
            if (DA.GetData(5, ref inPadding) && inPadding >= 1.0)
            {
                PresetPadding = inPadding;
            }

            // 7. Gatilho de Aplicação no Viewport
            bool inApply = AutoApplyToViewport;
            DA.GetData(6, ref inApply);
            if (_forceApply)
            {
                inApply = true;
                _forceApply = false;
            }

            // 8. Gatilho de Salvamento de Named Views
            bool inSaveNamed = AutoSaveNamedViews;
            DA.GetData(7, ref inSaveNamed);
            if (_forceSaveNamedViews)
            {
                inSaveNamed = true;
                _forceSaveNamedViews = false;
            }

            // 9. Viewport Alvo
            string inVpName = TargetViewportName;
            if (DA.GetData(8, ref inVpName) && !string.IsNullOrWhiteSpace(inVpName))
            {
                TargetViewportName = inVpName.Trim();
            }

            // 10. Projeção
            string inProj = ProjectionMode;
            if (DA.GetData(9, ref inProj) && !string.IsNullOrWhiteSpace(inProj))
            {
                ProjectionMode = inProj.Trim();
            }

            // ==========================================
            // CÁLCULO GEOMÉTRICO DAS VISTAS
            // ==========================================
            var viewDefs = BuildViewDefinitions(totalBbox, inDir, inViews, cleanPrefix, PresetPadding, ProjectionMode);
            if (viewDefs.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhuma vista válida foi gerada com os critérios fornecidos.");
                ClearOutputs(DA);
                return;
            }
            GeneratedViews = viewDefs;

            // Ajusta índice ativo com wrap circular
            int safeIdx = ((ActiveViewIndex % viewDefs.Count) + viewDefs.Count) % viewDefs.Count;
            ActiveViewDef = viewDefs[safeIdx];

            // Padded Bounding Box
            var paddedBbox = totalBbox;
            if (PresetPadding > 1.0)
            {
                double diag = totalBbox.Diagonal.Length;
                double padDelta = (PresetPadding - 1.0) * diag * 0.5;
                paddedBbox.Inflate(padDelta);
            }

            // ==========================================
            // APLICAÇÃO NO VIEWPORT DO RHINO
            // ==========================================
            var doc = RhinoDoc.ActiveDoc;
            RhinoView targetView = null;
            if (doc != null && doc.Views != null)
            {
                if (!string.IsNullOrWhiteSpace(TargetViewportName))
                {
                    targetView = doc.Views.Find(TargetViewportName, false);
                }
                if (targetView == null)
                {
                    targetView = doc.Views.ActiveView;
                    if (targetView == null)
                    {
                        foreach (var v in doc.Views) { if (v != null) { targetView = v; break; } }
                    }
                }
            }

            if (inApply && targetView != null && targetView.ActiveViewport != null)
            {
                ApplyViewToViewport(targetView.ActiveViewport, ActiveViewDef, paddedBbox, ProjectionMode);
                targetView.Redraw();
            }

            // ==========================================
            // SALVAMENTO DE NAMED VIEWS NO RHINO
            // ==========================================
            if (inSaveNamed && doc != null && targetView != null && targetView.ActiveViewport != null)
            {
                SaveAllNamedViews(doc, targetView, viewDefs, paddedBbox, ProjectionMode, ActiveViewDef);
            }

            // ==========================================
            // SAÍDAS DO COMPONENTE
            // ==========================================
            string effectiveViewName = targetView != null ? targetView.MainViewport.Name : TargetViewportName;
            if (string.IsNullOrWhiteSpace(effectiveViewName)) effectiveViewName = "Perspective";

            DA.SetData(0, effectiveViewName);
            DA.SetData(1, ActiveViewDef.FileName);
            DA.SetData(2, ActiveViewDef.EditorialTitle);
            DA.SetDataList(3, viewDefs.Select(v => v.FileName).ToList());
            DA.SetDataList(4, viewDefs.Select(v => v.EditorialTitle).ToList());
            DA.SetDataList(5, viewDefs.Select(v => v.Id).ToList());
            DA.SetDataList(6, viewDefs.Select(v => v.CameraLocation).ToList());
            DA.SetData(7, totalBbox.Center);
            DA.SetDataList(8, viewDefs.Select(v => v.ViewPlane).ToList());
            DA.SetData(9, GenerateDataReport(viewDefs, ActiveViewDef, totalBbox, inDir, effectiveViewName));
        }

        private void ClearOutputs(IGH_DataAccess DA)
        {
            DA.SetData(0, "");
            DA.SetData(1, "");
            DA.SetData(2, "");
            DA.SetDataList(3, new List<string>());
            DA.SetDataList(4, new List<string>());
            DA.SetDataList(5, new List<string>());
            DA.SetDataList(6, new List<Point3d>());
            DA.SetData(7, Point3d.Unset);
            DA.SetDataList(8, new List<Plane>());
            DA.SetData(9, "Sem dados.");
        }

        // ==========================================
        // MOTOR MATEMÁTICO DE CÁLCULO DE CÂMERAS
        // ==========================================
        private List<ViewDefinition> BuildViewDefinitions(
            BoundingBox bbox,
            Vector3d modelDir,
            List<string> requestedViews,
            string prefix,
            double padding,
            string projMode)
        {
            Point3d target = bbox.Center;
            double diag = bbox.Diagonal.Length;
            if (diag < 1e-4) diag = 1.0;
            double camDist = diag * 2.5 * Math.Max(1.0, padding);

            // Sistema de Coordenadas do Modelo (Plano XY)
            Vector3d dirXY = new Vector3d(modelDir.X, modelDir.Y, 0.0);
            if (dirXY.Length < 1e-6) dirXY = Vector3d.YAxis;
            dirXY.Unitize();

            Vector3d uF = dirXY;                          // Frente
            Vector3d uZ = Vector3d.ZAxis;                 // Vertical Z
            Vector3d uR = Vector3d.CrossProduct(uF, uZ);  // Direita
            uR.Unitize();
            Vector3d uB = -uF;                            // Fundo / Posterior
            Vector3d uL = -uR;                            // Esquerda

            // Vetores Isométricos Canônicos (Azimutes 45° a partir das faces, Elevação 35.264° = arcsin(1/√3))
            // cos(35.264°) = √(2/3), sin(35.264°) = 1/√3.
            // Direção NE = (uR + uF + uZ) / √3
            Vector3d vIsoNE = (uR + uF + uZ); vIsoNE.Unitize();
            Vector3d vIsoNW = (uL + uF + uZ); vIsoNW.Unitize();
            Vector3d vIsoSE = (uR + uB + uZ); vIsoSE.Unitize();
            Vector3d vIsoSW = (uL + uB + uZ); vIsoSW.Unitize();

            // Biblioteca Canônica de Vistas (11 Vistas)
            var catalog = new List<ViewDefinition>
            {
                // 1. Isométricas
                CreateDef("Isometric_NE", "Isometric NE", $"{prefix}_Isometric_NE", $"Vista Isométrica NE — {prefix}", target, vIsoNE, uZ, camDist, true, 45.0, 35.264),
                CreateDef("Isometric_NW", "Isometric NW", $"{prefix}_Isometric_NW", $"Vista Isométrica NW — {prefix}", target, vIsoNW, uZ, camDist, true, 135.0, 35.264),
                CreateDef("Isometric_SE", "Isometric SE", $"{prefix}_Isometric_SE", $"Vista Isométrica SE — {prefix}", target, vIsoSE, uZ, camDist, true, 315.0, 35.264),
                CreateDef("Isometric_SW", "Isometric SW", $"{prefix}_Isometric_SW", $"Vista Isométrica SW — {prefix}", target, vIsoSW, uZ, camDist, true, 225.0, 35.264),

                // 2. Ortogonais
                // Frente: câmera fica em frente ao modelo (+uF) olhando para o centro (-uF)
                CreateDef("Front", "Front", $"{prefix}_Front", $"Fachada Frontal — {prefix}", target, uF, uZ, camDist, true, 90.0, 0.0),
                // Fundo: câmera fica atrás (+uB) olhando para o centro
                CreateDef("Back", "Back", $"{prefix}_Back", $"Fachada Posterior — {prefix}", target, uB, uZ, camDist, true, 270.0, 0.0),
                // Esquerda: câmera fica à esquerda (+uL) olhando para o centro
                CreateDef("Left", "Left", $"{prefix}_Left", $"Elevação Lateral Esquerda — {prefix}", target, uL, uZ, camDist, true, 180.0, 0.0),
                // Direita: câmera fica à direita (+uR) olhando para o centro
                CreateDef("Right", "Right", $"{prefix}_Right", $"Elevação Lateral Direita — {prefix}", target, uR, uZ, camDist, true, 0.0, 0.0),
                // Topo: câmera fica acima (+uZ), Up é a frente (+uF)
                CreateDef("Top", "Top", $"{prefix}_Top", $"Vista Superior (Planta) — {prefix}", target, uZ, uF, camDist, true, 0.0, 90.0),
                // Fundo Inferior: câmera fica abaixo (-uZ), Up é -uF
                CreateDef("Bottom", "Bottom", $"{prefix}_Bottom", $"Vista Inferior — {prefix}", target, -uZ, -uF, camDist, true, 0.0, -90.0),

                // 3. Perspectiva 3D
                CreateDef("Perspective", "Perspective 3D", $"{prefix}_Perspective_3D", $"Perspectiva 3D — {prefix}", target, (uR * 0.85 + uF * 0.85 + uZ * 0.65), uZ, camDist, false, 45.0, 30.0)
            };

            // Filtragem baseada nas entradas do usuário
            if (requestedViews == null || requestedViews.Count == 0 || requestedViews.Any(s => string.IsNullOrWhiteSpace(s) || s.Trim().Equals("All", StringComparison.OrdinalIgnoreCase) || s.Trim().Equals("Todas", StringComparison.OrdinalIgnoreCase)))
            {
                return catalog;
            }

            var selected = new List<ViewDefinition>();
            foreach (var req in requestedViews)
            {
                if (string.IsNullOrWhiteSpace(req)) continue;
                string cleanReq = req.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");

                if (cleanReq.Contains("alliso") || cleanReq == "isometric" || cleanReq == "isometrics" || cleanReq == "axonometric")
                {
                    selected.AddRange(catalog.Where(c => c.Id.StartsWith("Isometric")));
                }
                else if (cleanReq.Contains("allortho") || cleanReq == "orthogonal" || cleanReq == "elevations" || cleanReq == "fachadas")
                {
                    selected.AddRange(catalog.Where(c => c.Id == "Front" || c.Id == "Back" || c.Id == "Left" || c.Id == "Right" || c.Id == "Top" || c.Id == "Bottom"));
                }
                else if (cleanReq.Contains("isone") || cleanReq.Contains("nordeste") || cleanReq == "ne")
                {
                    selected.Add(catalog.First(c => c.Id == "Isometric_NE"));
                }
                else if (cleanReq.Contains("isonw") || cleanReq.Contains("noroeste") || cleanReq == "nw")
                {
                    selected.Add(catalog.First(c => c.Id == "Isometric_NW"));
                }
                else if (cleanReq.Contains("isose") || cleanReq.Contains("sudeste") || cleanReq == "se")
                {
                    selected.Add(catalog.First(c => c.Id == "Isometric_SE"));
                }
                else if (cleanReq.Contains("isosw") || cleanReq.Contains("sudoeste") || cleanReq == "sw")
                {
                    selected.Add(catalog.First(c => c.Id == "Isometric_SW"));
                }
                else if (cleanReq.Contains("front") || cleanReq.Contains("frente") || cleanReq.Contains("frontal") || cleanReq.Contains("fachada"))
                {
                    selected.Add(catalog.First(c => c.Id == "Front"));
                }
                else if (cleanReq.Contains("back") || cleanReq.Contains("posterior") || cleanReq.Contains("fundo") || cleanReq.Contains("costas") || cleanReq.Contains("traseira"))
                {
                    selected.Add(catalog.First(c => c.Id == "Back"));
                }
                else if (cleanReq.Contains("left") || cleanReq.Contains("esquerda"))
                {
                    selected.Add(catalog.First(c => c.Id == "Left"));
                }
                else if (cleanReq.Contains("right") || cleanReq.Contains("direita"))
                {
                    selected.Add(catalog.First(c => c.Id == "Right"));
                }
                else if (cleanReq.Contains("top") || cleanReq.Contains("topo") || cleanReq.Contains("planta") || cleanReq.Contains("superior"))
                {
                    selected.Add(catalog.First(c => c.Id == "Top"));
                }
                else if (cleanReq.Contains("bottom") || cleanReq.Contains("inferior"))
                {
                    selected.Add(catalog.First(c => c.Id == "Bottom"));
                }
                else if (cleanReq.Contains("persp") || cleanReq.Contains("perspectiva"))
                {
                    selected.Add(catalog.First(c => c.Id == "Perspective"));
                }
            }

            // Elimina duplicatas preservando a ordem
            var result = new List<ViewDefinition>();
            foreach (var item in selected)
            {
                if (!result.Any(r => r.Id == item.Id))
                {
                    result.Add(item);
                }
            }

            return result.Count > 0 ? result : catalog;
        }

        private ViewDefinition CreateDef(
            string id,
            string displayName,
            string fileName,
            string editorialTitle,
            Point3d target,
            Vector3d dirToCam,
            Vector3d up,
            double dist,
            bool isParallel,
            double azDeg,
            double elDeg)
        {
            dirToCam.Unitize();
            up.Unitize();

            Point3d camPos = target + (dirToCam * dist);

            // Plane: Origem no target, normal Z apontando para a câmera, Y no up
            Vector3d planeX = Vector3d.CrossProduct(up, dirToCam);
            planeX.Unitize();
            Vector3d planeY = Vector3d.CrossProduct(dirToCam, planeX);
            planeY.Unitize();
            Plane plane = new Plane(target, planeX, planeY);

            return new ViewDefinition
            {
                Id = id,
                DisplayName = displayName,
                FileName = fileName,
                EditorialTitle = editorialTitle,
                Target = target,
                CameraLocation = camPos,
                CameraUp = up,
                DirectionToCamera = dirToCam,
                IsParallel = isParallel,
                ViewPlane = plane,
                AzimuthDeg = azDeg,
                ElevationDeg = elDeg
            };
        }

        private void ApplyViewToViewport(
            RhinoViewport vp,
            ViewDefinition def,
            BoundingBox paddedBbox,
            string projMode)
        {
            if (vp == null || def == null) return;

            bool isParallel = def.IsParallel;
            if (projMode.Equals("Parallel", StringComparison.OrdinalIgnoreCase)) isParallel = true;
            else if (projMode.Equals("Perspective", StringComparison.OrdinalIgnoreCase)) isParallel = false;

            vp.ChangeToParallelProjection(isParallel);
            vp.SetCameraLocations(def.Target, def.CameraLocation);
            vp.CameraUp = def.CameraUp;
            vp.ZoomBoundingBox(paddedBbox);
        }

        private void SaveAllNamedViews(
            RhinoDoc doc,
            RhinoView targetView,
            List<ViewDefinition> defs,
            BoundingBox paddedBbox,
            string projMode,
            ViewDefinition activeDef)
        {
            if (doc == null || targetView == null || defs == null) return;
            var vp = targetView.ActiveViewport;
            if (vp == null) return;

            foreach (var def in defs)
            {
                ApplyViewToViewport(vp, def, paddedBbox, projMode);

                var vi = new ViewInfo(vp)
                {
                    Name = def.FileName
                };

                int existingIdx = doc.NamedViews.FindByName(def.FileName);
                if (existingIdx >= 0)
                {
                    doc.NamedViews.Delete(existingIdx);
                }
                doc.NamedViews.Add(vi);
            }

            // Restaura a vista ativa selecionada no viewport
            ApplyViewToViewport(vp, activeDef, paddedBbox, projMode);
            targetView.Redraw();
        }

        private string GenerateDataReport(
            List<ViewDefinition> views,
            ViewDefinition active,
            BoundingBox bbox,
            Vector3d dir,
            string vpName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                GLAUX TOOLS — PILL VIEW GENERATOR REPORT                  ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine($"• Viewport Alvo: {vpName}");
            sb.AppendLine($"• Vista Ativa:   {active.DisplayName} ({active.Id})");
            sb.AppendLine($"• Arquivo Ativo: {active.FileName}.png");
            sb.AppendLine($"• Título Ativo:  {active.EditorialTitle}");
            sb.AppendLine($"• Bounding Box:  Min({bbox.Min.X:F1}, {bbox.Min.Y:F1}, {bbox.Min.Z:F1}) -> Max({bbox.Max.X:F1}, {bbox.Max.Y:F1}, {bbox.Max.Z:F1})");
            sb.AppendLine($"• Diagonal:      {bbox.Diagonal.Length:F2} m  |  Centro: ({bbox.Center.X:F2}, {bbox.Center.Y:F2}, {bbox.Center.Z:F2})");
            sb.AppendLine($"• Orientação:    ({dir.X:F2}, {dir.Y:F2}, {dir.Z:F2})");
            sb.AppendLine($"• Total Vistas:  {views.Count}");
            sb.AppendLine();
            sb.AppendLine("────────────────────────────────────────────────────────────────────────────");
            sb.AppendLine(string.Format("{0,-4} | {1,-14} | {2,-6} | {3,-7} | {4,-24}", "#", "Vista", "Azim.", "Elev.", "Nome do Arquivo"));
            sb.AppendLine("────────────────────────────────────────────────────────────────────────────");

            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                string marker = (v.Id == active.Id) ? "►" : " ";
                sb.AppendLine(string.Format("{0} {1,-2} | {2,-14} | {3,5:F0}° | {4,5:F1}° | {5,-24}",
                    marker, i + 1, v.DisplayName, v.AzimuthDeg, v.ElevationDeg, v.FileName));
            }
            sb.AppendLine("────────────────────────────────────────────────────────────────────────────");
            return sb.ToString();
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Model";
            string clean = name.Trim();
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                clean = clean.Replace(c, '_');
            }
            return clean.Replace(" ", "_");
        }

        // ==========================================
        // COMANDOS INTERATIVOS
        // ==========================================
        public void StepView(int delta)
        {
            if (GeneratedViews == null || GeneratedViews.Count == 0) return;
            ActiveViewIndex = ((ActiveViewIndex + delta % GeneratedViews.Count) + GeneratedViews.Count) % GeneratedViews.Count;
            _forceApply = true;
            ExpireSolution(true);
        }

        public void TriggerApply()
        {
            _forceApply = true;
            ExpireSolution(true);
        }

        public void TriggerSaveNamedViews()
        {
            _forceSaveNamedViews = true;
            ExpireSolution(true);
        }

        // ==========================================
        // SERIALIZAÇÃO (PERSISTÊNCIA .GH)
        // ==========================================
        public override bool Write(GH_IWriter writer)
        {
            writer.SetInt32("ActiveViewIndex", ActiveViewIndex);
            writer.SetString("PresetPrefix", PresetPrefix);
            writer.SetDouble("PresetPadding", PresetPadding);
            writer.SetBoolean("AutoApplyToViewport", AutoApplyToViewport);
            writer.SetBoolean("AutoSaveNamedViews", AutoSaveNamedViews);
            writer.SetString("TargetViewportName", TargetViewportName);
            writer.SetString("ProjectionMode", ProjectionMode);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("ActiveViewIndex")) ActiveViewIndex = reader.GetInt32("ActiveViewIndex");
            if (reader.ItemExists("PresetPrefix")) PresetPrefix = reader.GetString("PresetPrefix");
            if (reader.ItemExists("PresetPadding")) PresetPadding = reader.GetDouble("PresetPadding");
            if (reader.ItemExists("AutoApplyToViewport")) AutoApplyToViewport = reader.GetBoolean("AutoApplyToViewport");
            if (reader.ItemExists("AutoSaveNamedViews")) AutoSaveNamedViews = reader.GetBoolean("AutoSaveNamedViews");
            if (reader.ItemExists("TargetViewportName")) TargetViewportName = reader.GetString("TargetViewportName");
            if (reader.ItemExists("ProjectionMode")) ProjectionMode = reader.GetString("ProjectionMode");
            return base.Read(reader);
        }

        // ==========================================
        // MENU DE CONTEXTO (RIGHT-CLICK)
        // ==========================================
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendSeparator(menu);

            // Ações Manuais
            Menu_AppendItem(menu, "🎯 Aplicar Vista Ativa ao Viewport", (s, e) => TriggerApply(), GlauxToolsIcons.PillViewGenerator);
            Menu_AppendItem(menu, "📷 Salvar Todas as Vistas como Named Views", (s, e) => TriggerSaveNamedViews(), GlauxToolsIcons.PillViewGenerator);
            Menu_AppendSeparator(menu);

            // Toggles
            var mApply = Menu_AppendItem(menu, "Auto-Aplicar no Viewport", (s, e) =>
            {
                AutoApplyToViewport = !AutoApplyToViewport;
                ExpireSolution(true);
            });
            mApply.Checked = AutoApplyToViewport;

            var mSave = Menu_AppendItem(menu, "Auto-Salvar Named Views no Rhino", (s, e) =>
            {
                AutoSaveNamedViews = !AutoSaveNamedViews;
                ExpireSolution(true);
            });
            mSave.Checked = AutoSaveNamedViews;

            Menu_AppendSeparator(menu);

            // Seletor de Vistas Rápidas
            if (GeneratedViews != null && GeneratedViews.Count > 0)
            {
                var viewMenu = Menu_AppendItem(menu, $"Selecionar Vista ({GeneratedViews.Count} Vistas)");
                for (int i = 0; i < GeneratedViews.Count; i++)
                {
                    int idx = i;
                    var def = GeneratedViews[i];
                    var item = Menu_AppendItem(viewMenu.DropDown, $"#{i + 1} - {def.DisplayName}", (s, e) =>
                    {
                        ActiveViewIndex = idx;
                        _forceApply = true;
                        ExpireSolution(true);
                    });
                    item.Checked = (i == ActiveViewIndex);
                }
            }
        }
    }

    /// <summary>
    /// Modelo de dados estruturado para cada definição de vista gerada.
    /// </summary>
    public class ViewDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string FileName { get; set; }
        public string EditorialTitle { get; set; }
        public Point3d Target { get; set; }
        public Point3d CameraLocation { get; set; }
        public Vector3d CameraUp { get; set; }
        public Vector3d DirectionToCamera { get; set; }
        public bool IsParallel { get; set; }
        public Plane ViewPlane { get; set; }
        public double AzimuthDeg { get; set; }
        public double ElevationDeg { get; set; }
    }

    /// <summary>
    /// Atributos visuais customizados para o Pill View Generator.
    /// Renderiza uma cápsula de controle na base do componente com botões interativos
    /// para navegação de vistas [◄] [►], aplicação [Apply] e gravação [Save Views].
    /// </summary>
    public class PillViewGenerator_Attributes : GH_ComponentAttributes
    {
        private const float CONTROL_BAR_HEIGHT = 44f;
        private const float MIN_WIDTH = 180f;

        private RectangleF m_btnPrevRect;
        private RectangleF m_btnNextRect;
        private RectangleF m_btnApplyRect;
        private RectangleF m_btnSaveRect;

        public PillViewGenerator_Attributes(PillViewGenerator_Component owner) : base(owner)
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

            // Alinha saídas caso a largura tenha expandido
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

            // Geometria dos Botões de Controle na Barra Inferior
            float barY = Bounds.Bottom - CONTROL_BAR_HEIGHT + 3f;
            float barW = Bounds.Width - 10f;
            float barX = Bounds.X + 5f;

            // Linha Superior da Barra: [ ◄ ] [ Nome da Vista ] [ ► ]
            float row1Y = barY + 2f;
            float btnW = 24f;
            float btnH = 17f;

            m_btnPrevRect = new RectangleF(barX + 2f, row1Y, btnW, btnH);
            m_btnNextRect = new RectangleF(barX + barW - btnW - 2f, row1Y, btnW, btnH);

            // Linha Inferior da Barra: [ Apply View ] [ 📷 Save Views ]
            float row2Y = barY + 21f;
            float halfBtnW = (barW - 6f) / 2f;
            m_btnApplyRect = new RectangleF(barX + 2f, row2Y, halfBtnW, 18f);
            m_btnSaveRect = new RectangleF(barX + 4f + halfBtnW, row2Y, halfBtnW, 18f);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            if (channel == GH_CanvasChannel.Objects)
            {
                var savedPivot = Pivot;
                Pivot = new PointF(Bounds.X + Bounds.Width / 2f, savedPivot.Y);
                base.Render(canvas, graphics, channel);
                Pivot = savedPivot;

                var comp = Owner as PillViewGenerator_Component;
                if (comp == null) return;

                RectangleF b = Bounds;
                RectangleF barRect = new RectangleF(b.X + 4f, b.Bottom - CONTROL_BAR_HEIGHT + 2f, b.Width - 8f, CONTROL_BAR_HEIGHT - 4f);

                // 1. Fundo da Cápsula Inferior (Dark Slate com Gradiente Suave)
                using (var path = CreateRoundedRectangle(barRect, 5f))
                using (var bgBrush = new LinearGradientBrush(barRect, Color.FromArgb(30, 41, 59), Color.FromArgb(15, 23, 42), LinearGradientMode.Vertical))
                using (var borderPen = new Pen(Color.FromArgb(51, 65, 85), 1.0f))
                {
                    graphics.FillPath(bgBrush, path);
                    graphics.DrawPath(borderPen, path);
                }

                // 2. Linha 1: Botão [ ◄ ], Badge da Vista Ativa, Botão [ ► ]
                DrawMiniButton(graphics, m_btnPrevRect, "◄", Color.FromArgb(71, 85, 105), Color.White);
                DrawMiniButton(graphics, m_btnNextRect, "►", Color.FromArgb(71, 85, 105), Color.White);

                // Badge Central: Nome da Vista Ativa
                float badgeX = m_btnPrevRect.Right + 3f;
                float badgeW = m_btnNextRect.Left - badgeX - 3f;
                RectangleF badgeRect = new RectangleF(badgeX, m_btnPrevRect.Y, badgeW, m_btnPrevRect.Height);

                string viewText = comp.ActiveViewDef != null ? comp.ActiveViewDef.DisplayName : "Sem Vistas";
                int totalCount = comp.GeneratedViews?.Count ?? 0;
                string viewSummary = $"{viewText} ({comp.ActiveViewIndex + 1}/{totalCount})";

                using (var badgePath = CreateRoundedRectangle(badgeRect, 3f))
                using (var badgeBrush = new SolidBrush(Color.FromArgb(14, 165, 233))) // Cyan
                using (var textFont = new System.Drawing.Font("Segoe UI", 6.8f, FontStyle.Bold))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                {
                    graphics.FillPath(badgeBrush, badgePath);
                    graphics.DrawString(viewSummary, textFont, Brushes.White, badgeRect, sf);
                }

                // 3. Linha 2: Botão [ Apply View ] e Botão [ 📷 Save Views ]
                DrawActionPill(graphics, m_btnApplyRect, "🎯 Aplicar Viewport", Color.FromArgb(16, 185, 129)); // Esmeralda
                DrawActionPill(graphics, m_btnSaveRect, "📷 Salvar Named", Color.FromArgb(99, 102, 241)); // Indigo
            }
            else
            {
                base.Render(canvas, graphics, channel);
            }
        }

        private void DrawMiniButton(Graphics g, RectangleF rect, string text, Color bg, Color fg)
        {
            using (var path = CreateRoundedRectangle(rect, 3f))
            using (var brush = new SolidBrush(bg))
            using (var pen = new Pen(Color.FromArgb(100, 116, 139), 0.8f))
            using (var font = new System.Drawing.Font("Segoe UI", 6.5f, FontStyle.Bold))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
                using (var fgBrush = new SolidBrush(fg))
                {
                    g.DrawString(text, font, fgBrush, rect, sf);
                }
            }
        }

        private void DrawActionPill(Graphics g, RectangleF rect, string text, Color accent)
        {
            using (var path = CreateRoundedRectangle(rect, 3.5f))
            using (var bgBrush = new SolidBrush(Color.FromArgb(35, 45, 60)))
            using (var borderPen = new Pen(accent, 0.9f))
            using (var font = new System.Drawing.Font("Segoe UI", 6.5f, FontStyle.Regular))
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
                var comp = Owner as PillViewGenerator_Component;
                if (comp != null)
                {
                    if (m_btnPrevRect.Contains(e.CanvasLocation))
                    {
                        comp.StepView(-1);
                        return GH_ObjectResponse.Handled;
                    }
                    if (m_btnNextRect.Contains(e.CanvasLocation))
                    {
                        comp.StepView(1);
                        return GH_ObjectResponse.Handled;
                    }
                    if (m_btnApplyRect.Contains(e.CanvasLocation))
                    {
                        comp.TriggerApply();
                        return GH_ObjectResponse.Handled;
                    }
                    if (m_btnSaveRect.Contains(e.CanvasLocation))
                    {
                        comp.TriggerSaveNamedViews();
                        return GH_ObjectResponse.Handled;
                    }
                }
            }
            return base.RespondToMouseDown(sender, e);
        }
    }
}
