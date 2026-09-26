using GH_IO.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Display;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Componente no estilo "Pill" que captura o Viewport 3D do Rhino em alta resolução
    /// com preview visual em tempo real no Canvas do Grasshopper (estilo Spatial Heatmap),
    /// modos de exibição (Rendered, Shaded, Raytraced, Ghosted), fundo transparente
    /// e salvamento automático ou sob demanda no disco (PNG, JPG, BMP).
    /// </summary>
    public class PillViewportCapture_Component : GH_Component
    {
        // ==========================================
        // PROPRIEDADES E PRESETS
        // ==========================================
        public int PresetWidth = 1920;
        public int PresetHeight = 1080;
        public string PresetDisplayMode = "Keep";
        public bool TransparentBackground = false;
        public bool DrawGrid = false;
        public bool UseEditorialLayout = true;

        // Estado e Cache da Captura
        public Bitmap CapturedBitmap { get; private set; }
        public Bitmap CanvasThumbnailBmp { get; private set; }
        public string LastSavedFilePath { get; private set; } = "";
        public long LastSavedFileSize { get; private set; } = 0;
        public DateTime LastSavedTime { get; private set; } = DateTime.MinValue;
        public bool LastSaveSuccess { get; private set; } = false;

        // Metadados Atuais
        public string CurrentViewName { get; private set; } = "Perspective";
        public string CurrentDisplayModeName { get; private set; } = "Keep";
        public int CurrentWidth { get; private set; } = 1920;
        public int CurrentHeight { get; private set; } = 1080;
        public string CurrentDirectory { get; private set; } = "";
        public string CurrentFileName { get; private set; } = "Viewport_3D";
        public string CurrentCleanKey { get; private set; } = "Viewport_3D";
        public Color CurrentCategoryColor => Color.FromArgb(14, 165, 233); // Cyan elétrico

        // Controle de Triggers
        private bool _forceCapture = false;
        private bool _forceSave = false;

        // Anotações sobrepostas na imagem capturada
        public string AnnotationTitle = "";
        public string AnnotationFontName = "Segoe UI";
        public int AnnotationFontSize = 36;
        public Color AnnotationTitleColor = Color.White;
        public string AnnotationUnit = "";
        public string LegendReportText = "";
        public List<Color> LegendColorStops = null;

        public PillViewportCapture_Component()
            : base(
                "Pill Viewport 3D Capture",
                "PillCapture",
                "Captura o Viewport 3D do Rhino em alta resolução com preview visual no Canvas do Grasshopper, suporte a modos de exibição (Rendered, Shaded, Raytraced, Ghosted), fundo transparente e salvamento automático ou sob demanda no disco (PNG, JPG, BMP).",
                "Glaux Tools",
                "Visual")
        {
        }

        public override Guid ComponentGuid => new Guid("b7110014-e1ef-4000-8000-000000000014");

        protected override Bitmap Icon => GlauxToolsIcons.PillViewportCapture;

        public override GH_Exposure Exposure => GH_Exposure.primary;


        public override void CreateAttributes()
        {
            m_attributes = new PillViewportCapture_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("View", "V", "Nome do Viewport do Rhino a capturar (ex.: 'Perspective', 'Top', 'Front', 'Right'). Se omitido ou vazio, captura o Viewport Ativo.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Width", "W", "Largura da imagem em pixels (ex.: 1920, 2560, 3840). 0 adota a largura da janela do viewport.", GH_ParamAccess.item, 1920);
            pManager.AddIntegerParameter("Height", "H", "Altura da imagem em pixels (ex.: 1080, 1440, 2160). 0 adota a altura da janela do viewport.", GH_ParamAccess.item, 1080);
            pManager.AddTextParameter("FileName", "Name", "Nome do arquivo da imagem a salvar (ex.: 'Fachada_Principal' ou 'Estudo_01.png').", GH_ParamAccess.item, "Viewport_3D");
            pManager.AddTextParameter("Directory", "Dir", "Pasta de destino no disco. Se omitido, adota 'PillVault/Captures' junto ao arquivo .gh atual.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Display Mode", "Mode", "Modo de exibição do Rhino a ser renderizado na captura:\n- 'Keep' / 'Active' (mantém o atual)\n- 'Rendered' (Renderizado)\n- 'Shaded' (Sombreado)\n- 'Ghosted' (Fantasma)\n- 'Raytraced' (Traçado de Raios)\n- 'Technical' (Técnico)\n- 'Artistic' (Artístico)\n- 'Pen' (Caneta)\n- 'Wireframe' (Aramado)", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Transparent", "Trans", "Fundo transparente (True = PNG com canal alfa transparente).", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Draw Grid", "Grid", "Desenhar a grade e eixos do plano de construção do viewport.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Save to Disk", "Save", "Gatilho para salvar a imagem no disco (True = salva arquivo). Conecte um Botão ou Toggle.", GH_ParamAccess.item, true);

            // Anotações sobrepostas na imagem capturada
            pManager.AddTextParameter("Title", "Ttl", "Título editorial a sobrepor sobre a imagem capturada (ex.: 'Vista Principal — Fachada Norte'). Desenhado com sombra protetora no canto superior esquerdo.", GH_ParamAccess.item);
            pManager.AddTextParameter("Font Name", "Font", "Família tipográfica do título (ex.: 'Segoe UI', 'Arial', 'Helvetica'). Padrão: 'Segoe UI'.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Font Size", "Sz", "Tamanho da fonte do título em pixels. Padrão: 36.", GH_ParamAccess.item, 36);
            pManager.AddColourParameter("Title Color", "TCol", "Cor do texto do título. Padrão: Branco.", GH_ParamAccess.item, Color.White);
            pManager.AddGenericParameter("Legend", "Leg", "Legenda de heatmap a sobrepor sobre a imagem. Aceita:\n• Saída 'Heatmap Report' (Rep) do Spatial Grid & Viewport Heatmap — parseia min/max/unidade/gradiente automaticamente.\n• Lista de cores (Palette Stops) — usa como barra de gradiente sem escala numérica.\nExibe barra de gradiente vertical colorida com ticks de escala no canto inferior esquerdo da imagem.", GH_ParamAccess.item);
            pManager.AddTextParameter("Unit", "U", "Unidade de medida a exibir junto ao título, na barra de escala e no rodapé (ex.: 'dB', 's', 'm', '°C'). Se omitido, extrai automaticamente do Heatmap Report conectado em 'Legend'.", GH_ParamAccess.item);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
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
        }


        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("FilePath", "Path", "Caminho absoluto completo do arquivo salvo no disco.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Image", "Img", "Bitmap em memória (System.Drawing.Bitmap) da captura do viewport.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "OK", "True se a captura e/ou salvamento em disco foram concluídos com êxito.", GH_ParamAccess.item);
            pManager.AddTextParameter("Summary", "Sum", "Relatório de metadados da captura (viewport, modo, resolução, tamanho, data).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Obtenção das Entradas
            string viewName = "";
            DA.GetData(0, ref viewName);

            int inWidth = PresetWidth;
            if (!DA.GetData(1, ref inWidth)) inWidth = PresetWidth;

            int inHeight = PresetHeight;
            if (!DA.GetData(2, ref inHeight)) inHeight = PresetHeight;

            string inFileName = "Viewport_3D";
            if (!DA.GetData(3, ref inFileName) || string.IsNullOrWhiteSpace(inFileName)) inFileName = "Viewport_3D";

            string inDir = "";
            DA.GetData(4, ref inDir);

            object inModeObj = null;
            DA.GetData(5, ref inModeObj);

            bool inTransparent = TransparentBackground;
            if (!DA.GetData(6, ref inTransparent)) inTransparent = TransparentBackground;

            bool inGrid = DrawGrid;
            if (!DA.GetData(7, ref inGrid)) inGrid = DrawGrid;

            bool inSave = true;
            if (!DA.GetData(8, ref inSave)) inSave = true;

            if (_forceSave)
            {
                inSave = true;
                _forceSave = false;
            }

            // 1b. Entradas de anotação sobre a imagem (índices 9-14)
            string inTitle = "";
            DA.GetData(9, ref inTitle);

            string inFontName = "Segoe UI";
            if (!DA.GetData(10, ref inFontName) || string.IsNullOrWhiteSpace(inFontName))
                inFontName = "Segoe UI";

            int inFontSize = 36;
            if (!DA.GetData(11, ref inFontSize) || inFontSize < 1) inFontSize = 36;
            inFontSize = Math.Max(6, Math.Min(500, inFontSize));

            Color inTitleColor = Color.White;
            DA.GetData(12, ref inTitleColor);

            object inLegendObj = null;
            DA.GetData(13, ref inLegendObj);

            string inUnit = "";
            DA.GetData(14, ref inUnit);

            // Atualiza estado de anotações
            AnnotationTitle = inTitle?.Trim() ?? "";
            AnnotationFontName = inFontName;
            AnnotationFontSize = inFontSize;
            AnnotationTitleColor = inTitleColor;
            AnnotationUnit = inUnit?.Trim() ?? "";

            // Resolve Legend: aceita HeatmapReport (texto) ou lista de cores
            if (inLegendObj != null)
            {
                string legendStr = null;
                if (inLegendObj is IGH_Goo legendGoo)
                    legendGoo.CastTo(out legendStr);
                if (legendStr == null && inLegendObj is string ds)
                    legendStr = ds;
                LegendReportText = legendStr ?? "";

                var cols = new List<Color>();
                if (inLegendObj is System.Collections.IEnumerable enumerable && !(inLegendObj is string))
                {
                    foreach (var item in enumerable)
                    {
                        if (item != null && GH_Convert.ToColor(item, out Color c, GH_Conversion.Both))
                            cols.Add(c);
                    }
                }
                else if (GH_Convert.ToColor(inLegendObj, out Color singleCol, GH_Conversion.Both))
                {
                    cols.Add(singleCol);
                }
                LegendColorStops = cols.Count >= 2 ? cols : null;
            }
            else
            {
                LegendReportText = "";
                LegendColorStops = null;
            }


            string modeStr = PresetDisplayMode;
            if (inModeObj != null)
            {
                if (inModeObj is string s && !string.IsNullOrWhiteSpace(s))
                    modeStr = s.Trim();
                else if (inModeObj is int idx)
                {
                    switch (idx)
                    {
                        case 1: modeStr = "Rendered"; break;
                        case 2: modeStr = "Shaded"; break;
                        case 3: modeStr = "Ghosted"; break;
                        case 4: modeStr = "Raytraced"; break;
                        case 5: modeStr = "Wireframe"; break;
                        case 6: modeStr = "Technical"; break;
                        case 7: modeStr = "Artistic"; break;
                        case 8: modeStr = "Pen"; break;
                        default: modeStr = "Keep"; break;
                    }
                }
                else if (inModeObj is IGH_Goo goo)
                {
                    modeStr = goo.ToString();
                }
            }

            // 3. Localização do RhinoView
            RhinoView view = null;
            var doc = RhinoDoc.ActiveDoc;
            if (doc != null && doc.Views != null)
            {
                if (!string.IsNullOrWhiteSpace(viewName))
                {
                    view = doc.Views.Find(viewName, false);
                    if (view == null && doc.NamedViews != null)
                    {
                        int namedIdx = doc.NamedViews.FindByName(viewName);
                        if (namedIdx >= 0)
                            view = doc.Views.ActiveView;
                            if (view == null)
                            {
                                foreach (var v in doc.Views) { if (v != null) { view = v; break; } }
                            }
                            if (view != null && view.ActiveViewport != null)
                            {
                                doc.NamedViews.Restore(namedIdx, view.ActiveViewport);
                                view.Redraw();
                            }
                    }
                }
                if (view == null)
                {
                    view = doc.Views.ActiveView;
                }
                if (view == null)
                {
                    foreach (var v in doc.Views)
                    {
                        if (v != null) { view = v; break; }
                    }
                }
            }

            if (view == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum viewport ativo do Rhino encontrado para captura.");
                DA.SetData(0, "");
                DA.SetData(1, null);
                DA.SetData(2, false);
                DA.SetData(3, "Falha: Nenhum viewport do Rhino encontrado.");
                return;
            }

            // 4. Determinação das Dimensões
            int targetW = inWidth > 0 ? inWidth : (view.ActiveViewport != null ? view.ActiveViewport.Size.Width : 1920);
            int targetH = inHeight > 0 ? inHeight : (view.ActiveViewport != null ? view.ActiveViewport.Size.Height : 1080);
            if (targetW < 16) targetW = 1920;
            if (targetH < 16) targetH = 1080;

            CurrentWidth = targetW;
            CurrentHeight = targetH;
            CurrentViewName = view.MainViewport != null ? (view.MainViewport.Name ?? "Viewport") : "Viewport";

            // 5. Configuração do Modo de Exibição
            DisplayModeDescription originalMode = view.ActiveViewport?.DisplayMode;
            DisplayModeDescription requestedMode = null;

            if (!string.IsNullOrWhiteSpace(modeStr) &&
                !modeStr.Equals("Keep", StringComparison.OrdinalIgnoreCase) &&
                !modeStr.Equals("KeepCurrent", StringComparison.OrdinalIgnoreCase) &&
                !modeStr.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                var modes = DisplayModeDescription.GetDisplayModes();
                requestedMode = modes?.FirstOrDefault(m =>
                    m.EnglishName.Equals(modeStr, StringComparison.OrdinalIgnoreCase) ||
                    m.LocalName.Equals(modeStr, StringComparison.OrdinalIgnoreCase));
            }

            CurrentDisplayModeName = requestedMode != null ? requestedMode.EnglishName : (originalMode != null ? originalMode.EnglishName : "Active");

            // 6. Execução da Captura via RhinoCommon
            bool swapped = false;
            Bitmap fullBmp = null;
            try
            {
                if (requestedMode != null && originalMode != null && requestedMode.Id != originalMode.Id && view.ActiveViewport != null)
                {
                    view.ActiveViewport.DisplayMode = requestedMode;
                    swapped = true;
                }

                var capture = new ViewCapture
                {
                    Width = targetW,
                    Height = targetH,
                    ScaleScreenItems = false,
                    DrawGrid = inGrid,
                    DrawAxes = inGrid,
                    DrawGridAxes = inGrid,
                    TransparentBackground = inTransparent
                };

                fullBmp = capture.CaptureToBitmap(view);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Erro ao capturar viewport do Rhino: {ex.Message}");
            }
            finally
            {
                if (swapped && originalMode != null && view.ActiveViewport != null)
                {
                    view.ActiveViewport.DisplayMode = originalMode;
                }
            }

            // 7. Atualização do Bitmap e do Thumbnail do Canvas
            if (fullBmp != null)
            {
                UpdateCapturedImage(fullBmp);
            }

            // 8. Gravação em Disco (quando Save == true)
            if (inSave && CapturedBitmap != null)
            {
                try
                {
                    string targetDir = inDir;
                    if (string.IsNullOrWhiteSpace(targetDir))
                    {
                        var ghDoc = OnPingDocument();
                        if (ghDoc != null && !string.IsNullOrEmpty(ghDoc.FilePath))
                        {
                            targetDir = Path.Combine(Path.GetDirectoryName(ghDoc.FilePath), "PillVault", "Captures");
                        }
                        else
                        {
                            string myPictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                            targetDir = Path.Combine(myPictures, "RhinoCaptures");
                        }
                    }

                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    CurrentDirectory = targetDir;
                    CurrentFileName = inFileName.Trim();

                    string safeName = SanitizeFileName(CurrentFileName);
                    string ext = Path.GetExtension(safeName);
                    if (string.IsNullOrEmpty(ext))
                    {
                        ext = ".png";
                        safeName += ext;
                    }

                    string fullPath = Path.Combine(targetDir, safeName);

                    ImageFormat fmt = ImageFormat.Png;
                    if (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                        fmt = ImageFormat.Jpeg;
                    else if (ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
                        fmt = ImageFormat.Bmp;

                    CapturedBitmap.Save(fullPath, fmt);

                    var fi = new FileInfo(fullPath);
                    LastSavedFilePath = fullPath;
                    LastSavedFileSize = fi.Length;
                    LastSavedTime = DateTime.Now;
                    LastSaveSuccess = true;

                    string cleanKey = Path.GetFileNameWithoutExtension(safeName);
                    CurrentCleanKey = cleanKey;
                }
                catch (Exception ex)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Erro ao salvar imagem no disco: {ex.Message}");
                    LastSaveSuccess = false;
                }
            }
            else if (!inSave)
            {
                LastSaveSuccess = false;
            }

            if (_forceCapture)
            {
                _forceCapture = false;
            }

            // 9. Saídas do Componente
            DA.SetData(0, LastSavedFilePath ?? "");
            DA.SetData(1, CapturedBitmap != null ? new GH_ObjectWrapper(CapturedBitmap) : null);
            DA.SetData(2, CapturedBitmap != null && (inSave ? LastSaveSuccess : true));

            string sizeStr = LastSavedFileSize > 0 ? $" | Tamanho: {LastSavedFileSize / 1024.0:F1} KB" : "";
            string statusDisk = inSave ? (LastSaveSuccess ? $" | Arquivo: {LastSavedFilePath}" : " | Falha na gravação") : " | Somente preview em memória";
            string summary = $"Viewport: {CurrentViewName} | Modo: {CurrentDisplayModeName} | Resolução: {CurrentWidth} × {CurrentHeight} px{sizeStr}{statusDisk}";
            DA.SetData(3, summary);
            Message = "";
        }

        // ==========================================
        // ATUALIZAÇÃO E CACHE DE IMAGEM
        // ==========================================
        private void UpdateCapturedImage(Bitmap fullBmp)
        {
            var oldThumb = CanvasThumbnailBmp;
            var oldCaptured = CapturedBitmap;

            Bitmap finalBmp = fullBmp;

            if (fullBmp != null)
            {
                if (UseEditorialLayout && !TransparentBackground)
                {
                    try
                    {
                        finalBmp = BuildEditorialCaptureImage(fullBmp, CurrentWidth, CurrentHeight);
                    }
                    catch
                    {
                        finalBmp = fullBmp;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(AnnotationTitle) || !string.IsNullOrWhiteSpace(LegendReportText) || LegendColorStops != null)
                {
                    try
                    {
                        finalBmp = new Bitmap(fullBmp);
                        using (var g = Graphics.FromImage(finalBmp))
                        {
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            TryParseHeatmapReport(LegendReportText, out var overlayLegendData);
                            DrawTitleOverlay(g, finalBmp.Width, finalBmp.Height, overlayLegendData);
                            if (!string.IsNullOrWhiteSpace(LegendReportText) || LegendColorStops != null)
                                DrawLegendOverlay(g, finalBmp.Width, finalBmp.Height);
                        }
                    }
                    catch
                    {
                        finalBmp = fullBmp; // fallback sem anotação
                    }
                }
            }

            CapturedBitmap = finalBmp;

            if (finalBmp != null)
            {
                // Gera thumbnail otimizado (~460px de largura) preservando aspect ratio para o Canvas
                int thumbW = 460;
                int thumbH = (int)Math.Max(60, thumbW * (double)finalBmp.Height / Math.Max(1, finalBmp.Width));
                var thumb = new Bitmap(thumbW, thumbH, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(thumb))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(finalBmp, 0, 0, thumbW, thumbH);
                }
                CanvasThumbnailBmp = thumb;
            }
            else
            {
                CanvasThumbnailBmp = null;
            }

            if (oldThumb != null)
                try { oldThumb.Dispose(); } catch { }

            // Libera cópia antiga se não for fullBmp e não for finalBmp
            if (oldCaptured != null && oldCaptured != fullBmp && oldCaptured != finalBmp)
                try { oldCaptured.Dispose(); } catch { }
        }


        // ==========================================
        // AÇÕES INTERATIVAS DO USUÁRIO
        // ==========================================
        public void TriggerCapture()
        {
            _forceCapture = true;
            ExpireSolution(true);
        }

        public string SaveImageDialog()
        {
            if (CapturedBitmap == null)
            {
                TriggerCapture();
            }
            if (CapturedBitmap == null) return null;

            using (var sfd = new SaveFileDialog())
            {
                sfd.Title = "Salvar Captura 3D - BURAQUEIRA Tools";
                sfd.Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|Bitmap Image (*.bmp)|*.bmp|All Files (*.*)|*.*";
                string safeName = SanitizeFileName(CurrentFileName);
                if (string.IsNullOrEmpty(safeName)) safeName = "Viewport_3D";
                sfd.FileName = safeName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? safeName : safeName + ".png";

                if (!string.IsNullOrEmpty(CurrentDirectory) && Directory.Exists(CurrentDirectory))
                {
                    sfd.InitialDirectory = CurrentDirectory;
                }

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    string ext = Path.GetExtension(sfd.FileName).ToLowerInvariant();
                    ImageFormat fmt = ImageFormat.Png;
                    if (ext == ".jpg" || ext == ".jpeg") fmt = ImageFormat.Jpeg;
                    else if (ext == ".bmp") fmt = ImageFormat.Bmp;

                    CapturedBitmap.Save(sfd.FileName, fmt);
                    LastSavedFilePath = sfd.FileName;
                    FileInfo fi = new FileInfo(sfd.FileName);
                    LastSavedFileSize = fi.Length;
                    LastSavedTime = DateTime.Now;
                    LastSaveSuccess = true;
                    Message = "Salvo!";
                    return sfd.FileName;
                }
            }
            return null;
        }

        public void OpenDestinationFolder()
        {
            try
            {
                string dir = CurrentDirectory;
                if (!string.IsNullOrEmpty(LastSavedFilePath) && File.Exists(LastSavedFilePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{LastSavedFilePath}\"");
                    return;
                }
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    Process.Start("explorer.exe", $"\"{dir}\"");
                    return;
                }
                string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                Process.Start("explorer.exe", $"\"{pictures}\"");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[PillCapture] Erro ao abrir pasta: {ex.Message}");
            }
        }

        // ==========================================
        // MENU DE CONTEXTO (RIGHT-CLICK)
        // ==========================================
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "📸 Capturar Agora", (s, e) => TriggerCapture(), GlauxToolsIcons.PillViewportCapture);
            Menu_AppendItem(menu, "💾 Salvar Imagem Como...", (s, e) => SaveImageDialog());
            Menu_AppendItem(menu, "📂 Abrir Pasta no Explorer", (s, e) => OpenDestinationFolder());
            Menu_AppendSeparator(menu);

            // Submenu de Resolução Preset
            var resMenu = Menu_AppendItem(menu, "📐 Resolução Pré-definida");
            AddResPreset(resMenu, "1080p Full HD (1920 × 1080)", 1920, 1080);
            AddResPreset(resMenu, "720p HD (1280 × 720)", 1280, 720);
            AddResPreset(resMenu, "2K QHD (2560 × 1440)", 2560, 1440);
            AddResPreset(resMenu, "4K UHD (3840 × 2160)", 3840, 2160);
            AddResPreset(resMenu, "Quadrado 1:1 (1080 × 1080)", 1080, 1080);
            AddResPreset(resMenu, "Instagram Story 9:16 (1080 × 1920)", 1080, 1920);
            AddResPreset(resMenu, "Resolução Atual do Viewport", 0, 0);

            // Submenu de Modos de Exibição
            var modeMenu = Menu_AppendItem(menu, "🎨 Modo de Exibição (Display Mode)");
            AddModePreset(modeMenu, "Manter Ativo (Keep Current)", "Keep");
            AddModePreset(modeMenu, "Rendered (Renderizado)", "Rendered");
            AddModePreset(modeMenu, "Shaded (Sombreado)", "Shaded");
            AddModePreset(modeMenu, "Ghosted (Semitransparente)", "Ghosted");
            AddModePreset(modeMenu, "Raytraced (Traçado de Raios)", "Raytraced");
            AddModePreset(modeMenu, "Technical (Técnico)", "Technical");
            AddModePreset(modeMenu, "Artistic (Artístico)", "Artistic");
            AddModePreset(modeMenu, "Pen (Caneta)", "Pen");
            AddModePreset(modeMenu, "Wireframe (Aramado)", "Wireframe");

            Menu_AppendSeparator(menu);

            // Toggles
            Menu_AppendItem(menu, "📐 Layout Editorial (Estilo Heatmap)", (s, e) =>
            {
                RecordUndoEvent("Toggle Editorial Layout");
                UseEditorialLayout = !UseEditorialLayout;
                ExpireSolution(true);
            }, true, UseEditorialLayout);

            Menu_AppendItem(menu, "Fundo Transparente", (s, e) =>
            {
                TransparentBackground = !TransparentBackground;
                ExpireSolution(true);
            }, true, TransparentBackground);

            Menu_AppendItem(menu, "Desenhar Grade & Eixos", (s, e) =>
            {
                DrawGrid = !DrawGrid;
                ExpireSolution(true);
            }, true, DrawGrid);
        }

        private void AddResPreset(ToolStripMenuItem parent, string name, int w, int h)
        {
            bool isChecked = PresetWidth == w && PresetHeight == h;
            Menu_AppendItem(parent.DropDown, name, (s, e) =>
            {
                PresetWidth = w;
                PresetHeight = h;
                ExpireSolution(true);
            }, true, isChecked);
        }

        private void AddModePreset(ToolStripMenuItem parent, string name, string modeKey)
        {
            bool isChecked = string.Equals(PresetDisplayMode, modeKey, StringComparison.OrdinalIgnoreCase);
            Menu_AppendItem(parent.DropDown, name, (s, e) =>
            {
                PresetDisplayMode = modeKey;
                ExpireSolution(true);
            }, true, isChecked);
        }

        // ==========================================
        // SERIALIZAÇÃO / GH_ISERIALIZABLE
        // ==========================================
        public override bool Write(GH_IWriter writer)
        {
            writer.SetInt32("PresetWidth", PresetWidth);
            writer.SetInt32("PresetHeight", PresetHeight);
            writer.SetString("PresetDisplayMode", PresetDisplayMode ?? "Keep");
            writer.SetBoolean("TransparentBackground", TransparentBackground);
            writer.SetBoolean("DrawGrid", DrawGrid);
            writer.SetBoolean("UseEditorialLayout", UseEditorialLayout);
            // Anotações
            writer.SetString("AnnotationTitle", AnnotationTitle ?? "");
            writer.SetString("AnnotationFontName", AnnotationFontName ?? "Segoe UI");
            writer.SetInt32("AnnotationFontSize", AnnotationFontSize);
            writer.SetInt32("AnnotationTitleColorARGB", AnnotationTitleColor.ToArgb());
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("PresetWidth")) PresetWidth = reader.GetInt32("PresetWidth");
            if (reader.ItemExists("PresetHeight")) PresetHeight = reader.GetInt32("PresetHeight");
            if (reader.ItemExists("PresetDisplayMode")) PresetDisplayMode = reader.GetString("PresetDisplayMode");
            if (reader.ItemExists("TransparentBackground")) TransparentBackground = reader.GetBoolean("TransparentBackground");
            if (reader.ItemExists("DrawGrid")) DrawGrid = reader.GetBoolean("DrawGrid");
            if (reader.ItemExists("UseEditorialLayout")) UseEditorialLayout = reader.GetBoolean("UseEditorialLayout");
            // Anotações
            if (reader.ItemExists("AnnotationTitle")) AnnotationTitle = reader.GetString("AnnotationTitle");
            if (reader.ItemExists("AnnotationFontName")) AnnotationFontName = reader.GetString("AnnotationFontName");
            if (reader.ItemExists("AnnotationFontSize")) AnnotationFontSize = reader.GetInt32("AnnotationFontSize");
            if (reader.ItemExists("AnnotationTitleColorARGB")) AnnotationTitleColor = Color.FromArgb(reader.GetInt32("AnnotationTitleColorARGB"));
            return base.Read(reader);
        }


        // ==========================================
        // UTILITÁRIOS E AUXILIARES
        // ==========================================
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

        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Viewport_3D";
            string invalid = new string(Path.GetInvalidFileNameChars());
            string clean = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
            return string.IsNullOrEmpty(clean) ? "Viewport_3D" : clean;
        }

        public string GetAspectRatioString()
        {
            if (CurrentWidth <= 0 || CurrentHeight <= 0) return "16:9";
            double ratio = (double)CurrentWidth / CurrentHeight;
            if (Math.Abs(ratio - (16.0 / 9.0)) < 0.05) return "16:9";
            if (Math.Abs(ratio - (4.0 / 3.0)) < 0.05) return "4:3";
            if (Math.Abs(ratio - 1.0) < 0.05) return "1:1";
            if (Math.Abs(ratio - (9.0 / 16.0)) < 0.05) return "9:16";
            if (Math.Abs(ratio - (21.0 / 9.0)) < 0.05) return "21:9";
            return $"{ratio:F2}:1";
        }

        // ==========================================
        // ANOTAÇÕES — SOBREPOSIÇÃO NA IMAGEM
        // ==========================================

        public string GetEffectiveUnit(HeatmapLegendData legendData)
        {
            if (!string.IsNullOrWhiteSpace(AnnotationUnit))
                return AnnotationUnit.Trim();
            if (!string.IsNullOrWhiteSpace(legendData.Unit))
                return legendData.Unit.Trim();
            return "";
        }

        public string GetEffectiveTitle(HeatmapLegendData legendData, bool includeUnit = true)
        {
            string title = !string.IsNullOrWhiteSpace(AnnotationTitle)
                ? AnnotationTitle.Trim()
                : (!string.IsNullOrWhiteSpace(legendData.Title)
                    ? legendData.Title.Trim()
                    : (!string.IsNullOrWhiteSpace(CurrentViewName) ? CurrentViewName : "Visualização 3D"));

            string unit = GetEffectiveUnit(legendData);
            if (includeUnit && !string.IsNullOrWhiteSpace(unit))
            {
                string formattedUnit = unit.StartsWith("[") && unit.EndsWith("]") ? unit : $"[{unit}]";
                if (!title.Contains(formattedUnit) && !title.Contains(unit))
                {
                    title = $"{title} {formattedUnit}";
                }
            }
            return title;
        }

        /// <summary>
        /// Desenha o título editorial com sombra protetora no canto superior esquerdo da imagem.
        /// </summary>
        private void DrawTitleOverlay(Graphics g, int imgW, int imgH, HeatmapLegendData legendData)
        {
            string titleToDraw = GetEffectiveTitle(legendData, true);
            if (string.IsNullOrWhiteSpace(titleToDraw)) return;

            float fontSize = Math.Max(6f, Math.Min(500f, AnnotationFontSize));
            FontFamily ff;
            try { ff = new FontFamily(AnnotationFontName ?? "Segoe UI"); }
            catch { ff = FontFamily.GenericSansSerif; }

            using (var font = new Font(ff, fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                float margin = Math.Max(16f, fontSize * 0.5f);
                float x = margin;
                float y = margin;
                float shadow = Math.Max(1f, fontSize * 0.07f);

                SizeF textSize = g.MeasureString(titleToDraw, font);

                // Sombra para legibilidade sobre qualquer fundo
                using (var shadowBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
                {
                    g.DrawString(titleToDraw, font, shadowBrush, x + shadow, y + shadow);
                    g.DrawString(titleToDraw, font, shadowBrush, x - shadow, y + shadow);
                }

                // Texto principal
                using (var textBrush = new SolidBrush(AnnotationTitleColor))
                {
                    g.DrawString(titleToDraw, font, textBrush, x, y);
                }

                // Subtítulo automático (vista | modo | data)
                float subFontSize = Math.Max(6f, fontSize * 0.36f);
                using (var subFont = new Font(ff, subFontSize, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    string subtitle = $"{DateTime.Now:yyyy-MM-dd HH:mm}  ·  {CurrentViewName}  ·  {CurrentDisplayModeName}";
                    float subY = y + textSize.Height + 3f;

                    using (var shadowBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
                        g.DrawString(subtitle, subFont, shadowBrush, x + 1, subY + 1);

                    using (var subBrush = new SolidBrush(Color.FromArgb(200, AnnotationTitleColor)))
                        g.DrawString(subtitle, subFont, subBrush, x, subY);
                }
            }
        }

        /// <summary>
        /// Parseia o HeatmapReport e desenha barra de gradiente com escala de valores.
        /// </summary>
        private void DrawLegendOverlay(Graphics g, int imgW, int imgH)
        {
            if (!TryParseHeatmapReport(LegendReportText, out var legendData)) return;
            var stops = legendData.ColorStops;
            if (stops == null || stops.Count < 2) return;

            string effectiveUnit = GetEffectiveUnit(legendData);
            string formattedUnit = !string.IsNullOrWhiteSpace(effectiveUnit) ? (effectiveUnit.StartsWith("[") && effectiveUnit.EndsWith("]") ? effectiveUnit : $"[{effectiveUnit}]") : "";

            FontFamily ff;
            try { ff = new FontFamily(AnnotationFontName ?? "Segoe UI"); }
            catch { ff = FontFamily.GenericSansSerif; }

            int barW = Math.Max(20, imgW / 60);
            int barH = Math.Min(Math.Max(120, imgH / 4), 400);
            int margin = Math.Max(16, imgW / 80);
            float labelSz = Math.Max(8f, AnnotationFontSize * 0.30f);
            float titleSz = Math.Max(9f, AnnotationFontSize * 0.36f);

            // Posição: canto inferior esquerdo
            int barX = margin;
            int barY = imgH - margin - barH - (int)(labelSz + 8);

            // Largura máxima do label de valor
            float labelMaxW = 0f;
            using (var lf = new Font(ff, labelSz, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                string sample = legendData.MaxVal.ToString("F2");
                labelMaxW = g.MeasureString(sample, lf).Width + 10f;
            }

            int panelPad = 8;
            float legendTitleH = string.IsNullOrWhiteSpace(formattedUnit) ? 0f : titleSz + 6f;
            var panelRect = new RectangleF(
                barX - panelPad,
                barY - panelPad - legendTitleH,
                barW + labelMaxW + panelPad * 3 + 6,
                barH + (int)(labelSz + 10) + panelPad * 2 + legendTitleH);

            // Fundo do painel da legenda
            using (var panelBrush = new SolidBrush(Color.FromArgb(185, 16, 19, 26)))
                g.FillRectangle(panelBrush, panelRect);
            using (var panelPen = new Pen(Color.FromArgb(110, 80, 100, 130), 1f))
                g.DrawRectangle(panelPen, panelRect.X, panelRect.Y, panelRect.Width, panelRect.Height);

            // Unidade de medida acima da legenda (ao invés do nome da escala)
            if (!string.IsNullOrWhiteSpace(formattedUnit))
            {
                using (var titleFont = new Font(ff, titleSz, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var titleBrush = new SolidBrush(Color.FromArgb(235, 245, 255)))
                {
                    g.DrawString(formattedUnit, titleFont, titleBrush, barX, barY - legendTitleH - panelPad + 3);
                }
            }

            // Barra de gradiente (topo = Max, base = Min)
            for (int py = 0; py < barH; py++)
            {
                double t = 1.0 - (double)py / Math.Max(1, barH - 1);
                Color c = CaptureGradientSample(stops, t);
                using (var pen = new Pen(c, 1f))
                    g.DrawLine(pen, barX, barY + py, barX + barW - 1, barY + py);
            }
            using (var borderPen = new Pen(Color.FromArgb(150, 120, 140, 170), 1f))
                g.DrawRectangle(borderPen, barX, barY, barW - 1, barH - 1);

            // Ticks e labels de escala (5 divisões)
            int numTicks = 5;
            string unitSuffix = string.IsNullOrWhiteSpace(effectiveUnit) ? "" : $" {effectiveUnit}";
            using (var labelFont = new Font(ff, labelSz, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var labelBrush = new SolidBrush(Color.FromArgb(230, 240, 252)))
            using (var shadowBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
            using (var tickPen = new Pen(Color.FromArgb(190, 190, 200, 220), 1f))
            {
                for (int i = 0; i <= numTicks; i++)
                {
                    double fraction = 1.0 - (double)i / numTicks;
                    double val = legendData.MinVal + fraction * (legendData.MaxVal - legendData.MinVal);
                    int tickY = barY + (int)(i * (barH - 1.0) / numTicks);

                    g.DrawLine(tickPen, barX + barW, tickY, barX + barW + 5, tickY);

                    double absV = Math.Abs(val);
                    string valStr = absV >= 10000 ? $"{val:F0}" : absV >= 1000 ? $"{val:F1}" : absV >= 100 ? $"{val:F1}" : $"{val:F2}";
                    valStr += unitSuffix;

                    float lx = barX + barW + 7;
                    float ly = tickY - labelSz / 2f;
                    g.DrawString(valStr, labelFont, shadowBrush, lx + 1, ly + 1);
                    g.DrawString(valStr, labelFont, labelBrush, lx, ly);
                }
            }
        }

        /// <summary>
        /// Gera a imagem 3D enquadrada no layout editorial idêntico ao do Heatmap:
        /// - Fundo branco puro com moldura sutil
        /// - Cabeçalho com título em negrito, subtítulo editorial (pontos, grade, IDW, slope, alvo) e divisor horizontal
        /// - Viewport 3D enquadrado no centro com borda limpa
        /// - Escala de gradiente vertical generosa e ampliada na lateral direita com ticks, valores e indicador de Alvo
        /// - Rodapé editorial com estatísticas mín/média/máx/alvo e carimbo da simulação
        /// </summary>
        private Bitmap BuildEditorialCaptureImage(Bitmap vpBmp, int targetW, int targetH)
        {
            if (vpBmp == null) return null;

            int w = targetW > 0 ? targetW : vpBmp.Width;
            int h = targetH > 0 ? targetH : vpBmp.Height;
            if (w < 800) w = 1400;
            if (h < 500) h = 880;

            float scale = Math.Max(0.6f, (float)w / 1400f);
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // Fundo Branco Puro
                g.Clear(Color.White);

                // Moldura externa sutil
                using (var borderPen = new Pen(Color.FromArgb(226, 232, 240), 1.5f * scale))
                {
                    g.DrawRectangle(borderPen, 1, 1, w - 3, h - 3);
                }

                float marginL = 60f * scale;
                float marginR = 60f * scale;
                float headerY = 28f * scale;
                float headerH = 54f * scale;
                float footerH = 46f * scale;
                float footerY = h - footerH - 22f * scale;

                float plotTop = headerY + headerH + 20f * scale;
                float plotBottom = footerY - 20f * scale;
                float plotLeft = marginL;
                float plotRight = w - marginR;
                float plotWidth = plotRight - plotLeft;
                float plotHeight = plotBottom - plotTop;

                FontFamily fam = GetUIFontFamily();

                bool hasLegend = TryParseHeatmapReport(LegendReportText, out var legendData);
                if (!hasLegend && LegendColorStops != null && LegendColorStops.Count >= 2)
                {
                    legendData = new HeatmapLegendData
                    {
                        ColorStops = LegendColorStops,
                        GradientName = "Personalizado",
                        MinVal = 0,
                        MaxVal = 1,
                        SamplePointsCount = 0,
                        HasStats = false
                    };
                    hasLegend = true;
                }

                string effectiveUnit = GetEffectiveUnit(legendData);
                string formattedUnit = !string.IsNullOrWhiteSpace(effectiveUnit) ? (effectiveUnit.StartsWith("[") && effectiveUnit.EndsWith("]") ? effectiveUnit : $"[{effectiveUnit}]") : "";

                // 1. Cabeçalho Editorial
                float titleFontSize = (AnnotationFontSize > 0 && AnnotationFontSize != 36)
                    ? (AnnotationFontSize * 0.5f * scale)
                    : (17f * scale);
                titleFontSize = Math.Max(11f, Math.Min(48f, titleFontSize));
                float subFontSize = Math.Max(8f, 9.5f * scale);

                Color titleColor = (AnnotationTitleColor == Color.White || AnnotationTitleColor.IsEmpty)
                    ? Color.FromArgb(15, 23, 42)
                    : AnnotationTitleColor;

                using (var titleFont = new Font(fam, titleFontSize, FontStyle.Bold))
                using (var subFont = new Font(fam, subFontSize, FontStyle.Regular))
                using (var titleBrush = new SolidBrush(titleColor))
                using (var subBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
                using (var headerLinePen = new Pen(Color.FromArgb(226, 232, 240), 1.5f * scale))
                {
                    string fullTitle = GetEffectiveTitle(legendData, true);
                    g.DrawString(fullTitle, titleFont, titleBrush, plotLeft, headerY);

                    string subDesc;
                    if (hasLegend && legendData.SamplePointsCount > 0)
                    {
                        subDesc = $"{legendData.SamplePointsCount} pontos amostrados · Grade {legendData.GridResX}×{legendData.GridResY} · Interpolação IDW (P={legendData.IdwPower:F1})" +
                                  (legendData.Has3DSlope ? " · [3D Slope]" : "");
                        if (legendData.IdealTargetVal.HasValue)
                        {
                            subDesc += $" · Alvo Ideal: {legendData.IdealTargetVal.Value:F2} {formattedUnit}".Trim();
                        }
                    }
                    else
                    {
                        subDesc = $"Viewport: {CurrentViewName} · Modo: {CurrentDisplayModeName} · Resolução {vpBmp.Width}×{vpBmp.Height} px · Perspectiva";
                    }

                    g.DrawString(subDesc, subFont, subBrush, plotLeft, headerY + titleFontSize * 1.45f + 4f);
                    g.DrawLine(headerLinePen, plotLeft, headerY + headerH, plotRight, headerY + headerH);
                }

                // 2. Área Central: Viewport 3D + Escala Lateral
                bool showScale = hasLegend && legendData.ColorStops != null && legendData.ColorStops.Count >= 2;
                float scaleAreaWidth = showScale ? Math.Max(200f * scale, 170f) : 0f;
                float vpAreaWidth = plotWidth - scaleAreaWidth;
                float vpAreaHeight = plotHeight;

                float vpAspect = (float)vpBmp.Width / Math.Max(1, vpBmp.Height);
                float renderW = vpAreaWidth;
                float renderH = renderW / vpAspect;
                if (renderH > vpAreaHeight)
                {
                    renderH = vpAreaHeight;
                    renderW = renderH * vpAspect;
                }

                float vpX = plotLeft + (vpAreaWidth - renderW) * 0.5f;
                float vpY = plotTop + (vpAreaHeight - renderH) * 0.5f;
                RectangleF vpRect = new RectangleF(vpX, vpY, renderW, renderH);

                // Render do Viewport 3D
                using (var vpBgBrush = new SolidBrush(Color.FromArgb(248, 250, 252)))
                using (var vpBorderPen = new Pen(Color.FromArgb(203, 213, 225), 1.2f * scale))
                {
                    g.FillRectangle(vpBgBrush, vpRect);
                    g.DrawImage(vpBmp, vpRect);
                    g.DrawRectangle(vpBorderPen, vpRect.X, vpRect.Y, vpRect.Width, vpRect.Height);
                }

                // 3. Escala Lateral Ampliada (quando existir)
                if (showScale)
                {
                    float scaleX = vpRect.Right + 32f * scale;
                    float barW = Math.Max(28f * scale, 24f);
                    float barH = Math.Max(vpRect.Height * 0.75f, 220f * scale);
                    float barY = vpRect.Y + (vpRect.Height - barH) * 0.5f;
                    RectangleF barRect = new RectangleF(scaleX, barY, barW, barH);

                    var stops = legendData.ColorStops;

                    // Unidade de medida acima da barra de escala (ao invés do nome da escala)
                    if (!string.IsNullOrWhiteSpace(formattedUnit))
                    {
                        using (var scaleTitleFont = new Font(fam, 9f * scale, FontStyle.Bold))
                        using (var scaleTitleBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
                        {
                            g.DrawString(formattedUnit, scaleTitleFont, scaleTitleBrush, barRect.X - 2f, barRect.Y - 24f * scale);
                        }
                    }

                    // Barra de gradiente vertical (topo = Max, base = Min)
                    int pySteps = (int)barRect.Height;
                    for (int py = 0; py < pySteps; py++)
                    {
                        double t = 1.0 - (double)py / Math.Max(1, pySteps - 1);
                        Color c = CaptureGradientSample(stops, t);
                        using (var pen = new Pen(c, 1f))
                        {
                            g.DrawLine(pen, barRect.X, barRect.Y + py, barRect.Right, barRect.Y + py);
                        }
                    }
                    using (var barBorderPen = new Pen(Color.FromArgb(148, 163, 184), 1.2f * scale))
                    {
                        g.DrawRectangle(barBorderPen, barRect.X, barRect.Y, barRect.Width, barRect.Height);
                    }

                    // Ticks e valores numéricos
                    int numTicks = 5;
                    using (var tickFont = new Font(fam, 8.5f * scale, FontStyle.Bold))
                    using (var tickBrush = new SolidBrush(Color.FromArgb(71, 85, 105)))
                    using (var tickPen = new Pen(Color.FromArgb(148, 163, 184), 1.2f * scale))
                    {
                        for (int i = 0; i <= numTicks; i++)
                        {
                            double fraction = 1.0 - (double)i / numTicks;
                            double val = legendData.MinVal + fraction * (legendData.MaxVal - legendData.MinVal);
                            float ty = barRect.Y + (float)(i * barRect.Height / numTicks);

                            g.DrawLine(tickPen, barRect.Right, ty, barRect.Right + 6f * scale, ty);

                            double absV = Math.Abs(val);
                            string valStr = absV >= 1000 ? $"{val:F0}" : $"{val:F2}";
                            g.DrawString(valStr, tickFont, tickBrush, barRect.Right + 9f * scale, ty - 6f * scale);
                        }
                    }

                    // Marcador de Alvo Ideal se presente
                    if (legendData.IdealTargetVal.HasValue && legendData.MaxVal > legendData.MinVal)
                    {
                        double targetVal = legendData.IdealTargetVal.Value;
                        if (targetVal >= legendData.MinVal && targetVal <= legendData.MaxVal)
                        {
                            float targetNorm = (float)((targetVal - legendData.MinVal) / (legendData.MaxVal - legendData.MinVal));
                            float targetY = barRect.Bottom - targetNorm * barRect.Height;

                            using (var targetPen = new Pen(Color.FromArgb(16, 185, 129), 2.2f * scale))
                            using (var targetBrush = new SolidBrush(Color.FromArgb(5, 150, 105)))
                            using (var targetFont = new Font(fam, 8.5f * scale, FontStyle.Bold))
                            {
                                g.DrawLine(targetPen, barRect.X - 5f * scale, targetY, barRect.Right + 12f * scale, targetY);
                                PointF[] arrow = new PointF[]
                                {
                                    new PointF(barRect.X - 4f * scale, targetY),
                                    new PointF(barRect.X - 11f * scale, targetY - 4f * scale),
                                    new PointF(barRect.X - 11f * scale, targetY + 4f * scale)
                                };
                                g.FillPolygon(targetBrush, arrow);
                                g.DrawString($"Alvo ({targetVal:F2})", targetFont, targetBrush, barRect.Right + 16f * scale, targetY - 6f * scale);
                            }
                        }
                    }
                }

                // 4. Rodapé Editorial (Footer)
                var footerRect = new RectangleF(plotLeft, footerY, plotWidth, footerH);
                using (var footerBgBrush = new SolidBrush(Color.FromArgb(248, 250, 252)))
                using (var footerTopPen = new Pen(Color.FromArgb(226, 232, 240), 1.2f * scale))
                using (var footFont = new Font(fam, 9.5f * scale, FontStyle.Bold))
                using (var footTextBrush = new SolidBrush(Color.FromArgb(51, 65, 85)))
                using (var waterFont = new Font(fam, 8.5f * scale, FontStyle.Regular))
                using (var waterBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                {
                    g.FillRectangle(footerBgBrush, footerRect);
                    g.DrawLine(footerTopPen, footerRect.Left, footerRect.Top, footerRect.Right, footerRect.Top);

                    string unitLabel = string.IsNullOrWhiteSpace(formattedUnit) ? "" : $" {formattedUnit}";
                    string footText;
                    if (hasLegend && legendData.HasStats)
                    {
                        footText = $"Mín = {legendData.DataMinVal:F2}{unitLabel}  |  Média = {legendData.DataAvgVal:F2}{unitLabel}  |  Máx = {legendData.DataMaxVal:F2}{unitLabel}";
                        if (legendData.IdealTargetVal.HasValue)
                        {
                            footText += $"  |  Alvo = {legendData.IdealTargetVal.Value:F2}{unitLabel}";
                        }
                    }
                    else
                    {
                        footText = $"Viewport: {CurrentViewName}  |  Modo: {CurrentDisplayModeName}  |  Câmera: Perspectiva";
                    }

                    g.DrawString(footText, footFont, footTextBrush, footerRect.Left + 14f * scale, footerRect.Top + 14f * scale);

                    var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    string ds = (hasLegend && !string.IsNullOrWhiteSpace(legendData.DataSource)) ? legendData.DataSource : "Pachyderm Simulation";
                    string stamp = $"{ds} · {DateTime.Now:yyyy-MM-dd HH:mm}";
                    g.DrawString(stamp, waterFont, waterBrush, footerRect.Right - 14f * scale, footerRect.Top + footerRect.Height * 0.5f, sfRight);
                }
            }

            return bmp;
        }

        public struct HeatmapLegendData
        {
            public string Title;
            public string DataSource;
            public string GradientName;
            public int SamplePointsCount;
            public int GridResX;
            public int GridResY;
            public double IdwPower;
            public bool Has3DSlope;
            public double? IdealTargetVal;
            public double DataMinVal;
            public double DataMaxVal;
            public double DataAvgVal;
            public bool HasStats;
            public double MinVal;
            public double MaxVal;
            public string Unit;
            public List<Color> ColorStops;
        }

        public static bool TryParseHeatmapReport(string report, out HeatmapLegendData data)
        {
            data = new HeatmapLegendData
            {
                ColorStops = new List<Color>(),
                SamplePointsCount = 0,
                GridResX = 0,
                GridResY = 0,
                IdwPower = 2.0,
                Has3DSlope = false,
                HasStats = false
            };
            if (string.IsNullOrWhiteSpace(report)) return false;

            // Título (suporta acentuação, encoding ou inglês)
            var m = Regex.Match(report, @"(?:Título|Titulo|T[ií\u00ED\uFFFD]tulo|Title):\s+(.+?)(?:\r|\n|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (m.Success) data.Title = m.Groups[1].Value.Trim();

            // Fonte dos Dados
            m = Regex.Match(report, @"(?:Fonte dos Dados|Data Source):\s+(.+?)(?:\r|\n|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (m.Success) data.DataSource = m.Groups[1].Value.Trim();

            // Unidade de Medida explícita no relatório
            m = Regex.Match(report, @"(?:Unidade|Unit):\s+(.+?)(?:\r|\n|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (m.Success)
            {
                string u = m.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(u))
                    data.Unit = u;
            }

            // Gradiente
            m = Regex.Match(report, @"(?:Gradiente Ativo|Active Gradient):\s+([\w\s\-\/]+?)(?:\s*\(|\r|\n|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (m.Success) data.GradientName = m.Groups[1].Value.Trim();

            // Pontos de Amostra
            m = Regex.Match(report, @"(?:Pontos de Amostra|Sample Points):\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int pts)) data.SamplePointsCount = pts;

            // Resolução
            m = Regex.Match(report, @"(?:Resolu[çc\u00E7\uFFFD][aã\u00E3\uFFFD]o|Resolution):\s+(\d+)\s*x\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (m.Success)
            {
                int.TryParse(m.Groups[1].Value, out data.GridResX);
                int.TryParse(m.Groups[2].Value, out data.GridResY);
            }

            // IDW Power
            m = Regex.Match(report, @"IDW Power:\s+([\d\.\,]+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (m.Success)
                double.TryParse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out data.IdwPower);

            // Ajuste de Inclinação / 3D Slope
            data.Has3DSlope = report.Contains("Plano Inclinado") || report.Contains("Superfície Inclinada") || report.Contains("[3D Slope]");

            // Limites Ativos
            m = Regex.Match(report, @"(?:Limites Ativos|Active Limits):\s+\[([\-\d\.\,]+)\s+\.\.\s+([\-\d\.\,]+)\]\s*(.*)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (m.Success)
            {
                double.TryParse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out data.MinVal);
                double.TryParse(m.Groups[2].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out data.MaxVal);

                if (string.IsNullOrWhiteSpace(data.Unit))
                {
                    string rest = m.Groups[3].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(rest))
                    {
                        var uMatch = Regex.Match(rest, @"^(\[?[a-zA-Z0-9_\-\/°µ%]+\]?)");
                        if (uMatch.Success)
                        {
                            string uCandidate = uMatch.Groups[1].Value.Trim();
                            if (!uCandidate.Equals("[Automático]", StringComparison.OrdinalIgnoreCase) &&
                                !uCandidate.Equals("[Definido", StringComparison.OrdinalIgnoreCase))
                            {
                                data.Unit = uCandidate;
                            }
                        }
                    }
                }
            }

            // Faixa Real (Dados)
            m = Regex.Match(report, @"(?:Faixa Real|Data Range)[^\:]*:\s+\[([\-\d\.\,]+)\s+\.\.\s+([\-\d\.\,]+)\]\s*(.*)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (m.Success)
            {
                double.TryParse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out data.DataMinVal);
                double.TryParse(m.Groups[2].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out data.DataMaxVal);
                data.HasStats = true;

                if (string.IsNullOrWhiteSpace(data.Unit))
                {
                    string rest = m.Groups[3].Value.Trim();
                    var uMatch = Regex.Match(rest, @"^(\[?[a-zA-Z0-9_\-\/°µ%]+\]?)");
                    if (uMatch.Success)
                    {
                        string uCandidate = uMatch.Groups[1].Value.Trim();
                        if (!uCandidate.StartsWith("(") && !uCandidate.StartsWith("[Definido"))
                        {
                            data.Unit = uCandidate;
                        }
                    }
                }
            }

            // Média Amostral
            m = Regex.Match(report, @"(?:M[eé\u00E9\uFFFD]dia|Mean|Average)\s*(?:Amostral)?:\s+([\-\d\.\,]+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (m.Success)
            {
                double.TryParse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out data.DataAvgVal);
                data.HasStats = true;
            }

            // Valor Alvo Ideal
            m = Regex.Match(report, @"(?:Valor Alvo Ideal|Ideal Target Value):\s+([\-\d\.\,]+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (m.Success && double.TryParse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double target))
            {
                data.IdealTargetVal = target;
            }

            data.ColorStops = GetCaptureGradientByName(data.GradientName ?? "Turbo");
            return data.ColorStops.Count >= 2;
        }

        private static List<Color> GetCaptureGradientByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return CaptureGradientTurbo();
            string lower = name.ToLowerInvariant();
            if (lower.Contains("turbo")) return CaptureGradientTurbo();
            if (lower.Contains("virid")) return CaptureGradientViridis();
            if (lower.Contains("therm") || lower.Contains("jet")) return CaptureGradientThermal();
            if (lower.Contains("plasm")) return CaptureGradientPlasma();
            if (lower.Contains("magm")) return CaptureGradientMagma();
            if (lower.Contains("cool") || lower.Contains("warm")) return CaptureGradientCoolWarm();
            if (lower.Contains("spectr")) return CaptureGradientSpectral();
            if (lower.Contains("grey") || lower.Contains("gray") || lower.Contains("cinza")) return CaptureGradientGreyscale();
            if (lower.Contains("target") || lower.Contains("proxim")) return CaptureGradientTargetProximity();
            if (lower.Contains("diverg")) return CaptureGradientTargetDiverging();
            if (lower.Contains("sunset") || lower.Contains("ylorrd")) return CaptureGradientSunset();
            if (lower.Contains("ocean") || lower.Contains("blue")) return CaptureGradientOcean();
            if (lower.Contains("forest") || lower.Contains("green")) return CaptureGradientForest();
            if (lower.Contains("infern")) return CaptureGradientInferno();
            if (lower.Contains("civid")) return CaptureGradientCividis();
            return CaptureGradientTurbo();
        }

        private static Color CaptureGradientSample(List<Color> stops, double t)
        {
            if (stops == null || stops.Count == 0) return Color.Gray;
            if (stops.Count == 1) return stops[0];
            t = Math.Max(0.0, Math.Min(1.0, t));
            double seg = t * (stops.Count - 1);
            int idx = (int)seg;
            if (idx >= stops.Count - 1) return stops[stops.Count - 1];
            double lt = seg - idx;
            var c0 = stops[idx]; var c1 = stops[idx + 1];
            return Color.FromArgb(
                (int)(c0.A + (c1.A - c0.A) * lt),
                (int)(c0.R + (c1.R - c0.R) * lt),
                (int)(c0.G + (c1.G - c0.G) * lt),
                (int)(c0.B + (c1.B - c0.B) * lt));
        }

        // Gradientes embutidos (espelham os presets do SpatialHeatmap)
        private static List<Color> CaptureGradientTurbo() => new List<Color> {
            Color.FromArgb(48,18,59), Color.FromArgb(70,134,251), Color.FromArgb(27,229,181),
            Color.FromArgb(164,252,60), Color.FromArgb(251,185,56), Color.FromArgb(227,68,10), Color.FromArgb(122,4,3) };
        private static List<Color> CaptureGradientViridis() => new List<Color> {
            Color.FromArgb(68,1,84), Color.FromArgb(71,44,122), Color.FromArgb(59,81,139),
            Color.FromArgb(44,113,142), Color.FromArgb(33,144,141), Color.FromArgb(94,201,98), Color.FromArgb(253,231,37) };
        private static List<Color> CaptureGradientThermal() => new List<Color> {
            Color.FromArgb(0,0,130), Color.FromArgb(0,0,255), Color.FromArgb(0,255,255),
            Color.FromArgb(0,255,0), Color.FromArgb(255,255,0), Color.FromArgb(255,128,0), Color.FromArgb(255,0,0), Color.FromArgb(128,0,0) };
        private static List<Color> CaptureGradientPlasma() => new List<Color> {
            Color.FromArgb(13,8,135), Color.FromArgb(84,2,163), Color.FromArgb(139,10,165),
            Color.FromArgb(185,50,137), Color.FromArgb(219,92,104), Color.FromArgb(243,148,69), Color.FromArgb(240,249,33) };
        private static List<Color> CaptureGradientMagma() => new List<Color> {
            Color.FromArgb(0,0,4), Color.FromArgb(58,15,112), Color.FromArgb(139,41,129),
            Color.FromArgb(205,84,104), Color.FromArgb(244,148,96), Color.FromArgb(252,214,142), Color.FromArgb(252,253,191) };
        private static List<Color> CaptureGradientInferno() => new List<Color> {
            Color.FromArgb(0,0,4), Color.FromArgb(54,14,101), Color.FromArgb(133,33,107),
            Color.FromArgb(202,63,77), Color.FromArgb(243,120,46), Color.FromArgb(252,198,86), Color.FromArgb(252,255,164) };
        private static List<Color> CaptureGradientCoolWarm() => new List<Color> {
            Color.FromArgb(59,76,192), Color.FromArgb(120,157,232), Color.FromArgb(190,210,240),
            Color.FromArgb(235,235,235), Color.FromArgb(240,190,155), Color.FromArgb(220,110,90), Color.FromArgb(180,4,38) };
        private static List<Color> CaptureGradientCividis() => new List<Color> {
            Color.FromArgb(0,32,76), Color.FromArgb(0,69,115), Color.FromArgb(49,108,142),
            Color.FromArgb(100,148,159), Color.FromArgb(155,187,167), Color.FromArgb(213,224,153), Color.FromArgb(253,255,65) };
        private static List<Color> CaptureGradientSpectral() => new List<Color> {
            Color.FromArgb(158,1,66), Color.FromArgb(213,62,79), Color.FromArgb(244,109,67),
            Color.FromArgb(253,174,97), Color.FromArgb(255,255,191), Color.FromArgb(171,221,164), Color.FromArgb(50,136,189) };
        private static List<Color> CaptureGradientSunset() => new List<Color> {
            Color.FromArgb(128,0,38), Color.FromArgb(189,0,38), Color.FromArgb(227,26,28),
            Color.FromArgb(252,78,42), Color.FromArgb(253,141,60), Color.FromArgb(254,204,92), Color.FromArgb(255,255,178) };
        private static List<Color> CaptureGradientOcean() => new List<Color> {
            Color.FromArgb(8,48,107), Color.FromArgb(8,81,156), Color.FromArgb(33,113,181),
            Color.FromArgb(66,146,198), Color.FromArgb(107,174,214), Color.FromArgb(158,202,225), Color.FromArgb(198,219,239) };
        private static List<Color> CaptureGradientForest() => new List<Color> {
            Color.FromArgb(0,68,27), Color.FromArgb(0,109,44), Color.FromArgb(35,139,69),
            Color.FromArgb(65,171,93), Color.FromArgb(116,196,118), Color.FromArgb(161,217,155), Color.FromArgb(199,233,192) };
        private static List<Color> CaptureGradientTargetProximity() => new List<Color> {
            Color.FromArgb(26,188,156), Color.FromArgb(52,231,166), Color.FromArgb(255,234,0), Color.FromArgb(255,80,0), Color.FromArgb(210,0,30) };
        private static List<Color> CaptureGradientTargetDiverging() => new List<Color> {
            Color.FromArgb(30,100,200), Color.FromArgb(100,170,240), Color.FromArgb(230,240,255),
            Color.FromArgb(255,240,200), Color.FromArgb(240,120,30), Color.FromArgb(200,0,20) };
        private static List<Color> CaptureGradientGreyscale() => new List<Color> {
            Color.FromArgb(20,20,20), Color.FromArgb(255,255,255) };
    }


    /// <summary>
    /// Atributos visuais customizados para o Canvas do Grasshopper.
    /// Renderiza uma prancheta escura editorial com preview da imagem capturada,
    /// botões interativos de captura/salvamento e métricas de resolução no rodapé.
    /// </summary>
    public class PillViewportCapture_Attributes : GH_ComponentAttributes
    {
        private const int CARD_WIDTH = 420;
        private const int CARD_HEIGHT = 260;

        private RectangleF m_btnCaptureRect;
        private RectangleF m_btnSaveRect;
        private RectangleF m_btnOpenRect;

        public PillViewportCapture_Attributes(PillViewportCapture_Component owner) : base(owner)
        {
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var comp = Owner as PillViewportCapture_Component;
                if (comp != null)
                {
                    if (m_btnCaptureRect.Contains(e.CanvasLocation))
                    {
                        comp.TriggerCapture();
                        return GH_ObjectResponse.Handled;
                    }
                    if (m_btnSaveRect.Contains(e.CanvasLocation))
                    {
                        string saved = comp.SaveImageDialog();
                        if (!string.IsNullOrEmpty(saved))
                        {
                            comp.Message = "Salvo!";
                            sender.Refresh();
                        }
                        return GH_ObjectResponse.Handled;
                    }
                    if (m_btnOpenRect.Contains(e.CanvasLocation))
                    {
                        comp.OpenDestinationFolder();
                        return GH_ObjectResponse.Handled;
                    }
                }
            }
            return base.RespondToMouseDown(sender, e);
        }

        protected override void Layout()
        {
            // 1. Calcula o layout natural dos parâmetros e ícone do componente
            base.Layout();

            // 2. Determina a posição Y mais baixa entre todos os parâmetros de entrada e saída
            float maxParamBottom = Bounds.Y + 40f;
            if (Owner.Params != null)
            {
                if (Owner.Params.Input != null)
                {
                    foreach (var p in Owner.Params.Input)
                    {
                        if (p.Attributes != null && p.Attributes.Bounds.Bottom > maxParamBottom)
                            maxParamBottom = p.Attributes.Bounds.Bottom;
                    }
                }
                if (Owner.Params.Output != null)
                {
                    foreach (var p in Owner.Params.Output)
                    {
                        if (p.Attributes != null && p.Attributes.Bounds.Bottom > maxParamBottom)
                            maxParamBottom = p.Attributes.Bounds.Bottom;
                    }
                }
            }

            // 3. Define a largura e altura totais para que o card fique estritamente abaixo dos parâmetros
            float oldRight = Bounds.Right;
            float totalWidth = Math.Max(Bounds.Width, CARD_WIDTH + 24f);
            float cardTop = maxParamBottom + 12f;
            float totalHeight = (cardTop - Bounds.Y) + CARD_HEIGHT + 14f;

            Bounds = new RectangleF(Bounds.X, Bounds.Y, totalWidth, totalHeight);

            // 4. Realinha os parâmetros de saída na borda direita da cápsula expandida
            float deltaX = Bounds.Right - oldRight;
            if (Math.Abs(deltaX) > 0.5f && Owner.Params != null && Owner.Params.Output != null)
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
                var comp = Owner as PillViewportCapture_Component;
                if (comp == null) return;


                // 1. Localiza a base dos parâmetros para posicionar o card com respiro
                float maxParamBottom = Bounds.Y + 40f;
                if (Owner.Params != null)
                {
                    if (Owner.Params.Input != null)
                    {
                        foreach (var p in Owner.Params.Input)
                        {
                            if (p.Attributes != null && p.Attributes.Bounds.Bottom > maxParamBottom)
                                maxParamBottom = p.Attributes.Bounds.Bottom;
                        }
                    }
                    if (Owner.Params.Output != null)
                    {
                        foreach (var p in Owner.Params.Output)
                        {
                            if (p.Attributes != null && p.Attributes.Bounds.Bottom > maxParamBottom)
                                maxParamBottom = p.Attributes.Bounds.Bottom;
                        }
                    }
                }

                float cardY = maxParamBottom + 10f;
                RectangleF cardRect = new RectangleF(Bounds.X + 12, cardY, Bounds.Width - 24, CARD_HEIGHT);
                RectangleF headerRect = new RectangleF(cardRect.X, cardRect.Y, cardRect.Width, 28);
                RectangleF footerRect = new RectangleF(cardRect.X, cardRect.Bottom - 24, cardRect.Width, 24);

                float plotTop = headerRect.Bottom + 6;
                float plotBottom = footerRect.Y - 6;
                float plotHeight = Math.Max(90, plotBottom - plotTop);

                RectangleF plotRect = new RectangleF(
                    cardRect.X + 10,
                    plotTop,
                    cardRect.Width - 20,
                    plotHeight);

                FontFamily fam = PillViewportCapture_Component.GetUIFontFamily();

                // 2. Painel Principal Escuro
                using (var bgBrush = new SolidBrush(Color.FromArgb(20, 23, 29)))
                {
                    graphics.FillRectangle(bgBrush, cardRect);
                }
                using (var borderPen = new Pen(Color.FromArgb(65, 72, 85), 1.2f))
                {
                    graphics.DrawRectangle(borderPen, cardRect.X, cardRect.Y, cardRect.Width, cardRect.Height);
                }

                // Header e Footer
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

                // Título no Header com marcador cyan elétrico
                using (var dotBrush = new SolidBrush(comp.CurrentCategoryColor))
                {
                    graphics.FillEllipse(dotBrush, headerRect.X + 8, headerRect.Y + 10, 8, 8);
                }
                using (var titleFont = new Font(fam, 8f, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(Color.FromArgb(240, 245, 250)))
                {
                    string cardTitle = !string.IsNullOrWhiteSpace(comp.AnnotationTitle)
                        ? comp.AnnotationTitle
                        : (!string.IsNullOrWhiteSpace(comp.LegendReportText) && PillViewportCapture_Component.TryParseHeatmapReport(comp.LegendReportText, out var ld) && !string.IsNullOrWhiteSpace(ld.Title)
                            ? ld.Title
                            : (!string.IsNullOrWhiteSpace(comp.CurrentViewName) ? $"Viewport: {comp.CurrentViewName}" : "3D Viewport Snapshot"));

                    float maxTitleW = Math.Max(50f, (m_btnCaptureRect.X - 10f) - (headerRect.X + 22));
                    var headerTitleRect = new RectangleF(headerRect.X + 20, headerRect.Y + 6, maxTitleW, 18f);
                    var sfCard = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    graphics.DrawString(cardTitle, titleFont, titleBrush, headerTitleRect, sfCard);
                }

                // Botões Interativos no Header (da direita para a esquerda)
                float btnH = 20f;
                float curX = headerRect.Right - 6;

                // Botão "↗ Abrir"
                float openW = 54f;
                curX -= openW;
                m_btnOpenRect = new RectangleF(curX, headerRect.Y + 4, openW, btnH);

                // Botão "▼ Salvar"
                float saveW = 60f;
                curX -= (saveW + 4);
                m_btnSaveRect = new RectangleF(curX, headerRect.Y + 4, saveW, btnH);

                // Botão "● Capturar"
                float capW = 74f;
                curX -= (capW + 4);
                m_btnCaptureRect = new RectangleF(curX, headerRect.Y + 4, capW, btnH);

                // Badge de Viewport e Resolução (centralizada, sem sobrepor o título)
                float titleRight = headerRect.X + 155f;
                float badgeAvailW = (curX - 8f) - titleRight;
                if (badgeAvailW > 45f)
                {
                    RectangleF badgeRect = new RectangleF(titleRight, headerRect.Y + 4, badgeAvailW, 20f);
                    using (var badgeFont = new Font(fam, 6.8f, FontStyle.Bold))
                    using (var badgeBrush = new SolidBrush(Color.FromArgb(0, 220, 255)))
                    {
                        var sfBadge = new StringFormat
                        {
                            Alignment = StringAlignment.Far,
                            LineAlignment = StringAlignment.Center,
                            Trimming = StringTrimming.EllipsisCharacter,
                            FormatFlags = StringFormatFlags.NoWrap
                        };
                        string viewDisplay = string.IsNullOrEmpty(comp.CurrentViewName) ? "Ativo" : comp.CurrentViewName;
                        graphics.DrawString($"[{viewDisplay}]", badgeFont, badgeBrush, badgeRect, sfBadge);
                    }
                }

                // Renderiza Botões com tipografia limpa sem emojis quebrados
                DrawHeaderButton(graphics, fam, m_btnCaptureRect, "● Capturar", Color.FromArgb(14, 116, 144), Color.FromArgb(6, 182, 212), Color.White);
                DrawHeaderButton(graphics, fam, m_btnSaveRect, "▼ Salvar", Color.FromArgb(44, 52, 64), Color.FromArgb(80, 92, 110), Color.FromArgb(220, 230, 242));
                DrawHeaderButton(graphics, fam, m_btnOpenRect, "↗ Abrir", Color.FromArgb(44, 52, 64), Color.FromArgb(80, 92, 110), Color.FromArgb(220, 230, 242));

                // 3. Área do Preview 3D (Letterboxed / Aspect Ratio preservado)
                using (var plotBrush = new SolidBrush(Color.FromArgb(12, 14, 18)))
                {
                    graphics.FillRectangle(plotBrush, plotRect);
                }

                if (comp.CanvasThumbnailBmp != null)
                {
                    float bmpW = comp.CanvasThumbnailBmp.Width;
                    float bmpH = comp.CanvasThumbnailBmp.Height;
                    float scale = Math.Min(plotRect.Width / bmpW, plotRect.Height / bmpH);
                    float drawW = bmpW * scale;
                    float drawH = bmpH * scale;
                    float drawX = plotRect.X + (plotRect.Width - drawW) * 0.5f;
                    float drawY = plotRect.Y + (plotRect.Height - drawH) * 0.5f;
                    var destRect = new RectangleF(drawX, drawY, drawW, drawH);

                    // Imagem capturada com alta qualidade
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(comp.CanvasThumbnailBmp, destRect);

                    // Moldura da imagem
                    using (var framePen = new Pen(Color.FromArgb(80, 95, 120), 1f))
                    {
                        graphics.DrawRectangle(framePen, destRect.X, destRect.Y, destRect.Width, destRect.Height);
                    }

                    // Borda da área do viewport
                    using (var border = new Pen(Color.FromArgb(60, 70, 85), 1.2f))
                    {
                        graphics.DrawRectangle(border, plotRect.X, plotRect.Y, plotRect.Width, plotRect.Height);
                    }
                }
                else
                {
                    using (var border = new Pen(Color.FromArgb(50, 60, 75), 1f))
                    {
                        graphics.DrawRectangle(border, plotRect.X, plotRect.Y, plotRect.Width, plotRect.Height);
                    }
                    using (var emptyFont = new Font(fam, 7.5f, FontStyle.Italic))
                    using (var emptyBrush = new SolidBrush(Color.FromArgb(130, 140, 155)))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        graphics.DrawString("Conecte ou clique em '● Capturar' para registrar o Viewport 3D do Rhino.", emptyFont, emptyBrush, plotRect, sf);
                    }
                }

                // 4. Rodapé Editorial (sem emojis com glifos ausentes)
                using (var footerFont = new Font(fam, 6.5f, FontStyle.Regular))
                using (var footerBrush = new SolidBrush(Color.FromArgb(160, 175, 195)))
                {
                    string ext = Path.GetExtension(comp.LastSavedFilePath);
                    if (string.IsNullOrEmpty(ext)) ext = ".PNG";
                    else ext = ext.ToUpperInvariant();

                    string sizeInfo = comp.LastSavedFileSize > 0 ? $"{comp.LastSavedFileSize / 1024.0:F1} KB" : "Em memória";
                    string statInfo = $"⛶ {comp.CurrentWidth} × {comp.CurrentHeight} ({comp.GetAspectRatioString()}) | {ext} | {sizeInfo} | {comp.CurrentDisplayModeName}";
                    graphics.DrawString(statInfo, footerFont, footerBrush, footerRect.X + 8, footerRect.Y + 5);

                    // Lado direito do rodapé: Status de gravação
                    var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    if (comp.LastSaveSuccess)
                    {
                        using (var okBrush = new SolidBrush(Color.FromArgb(52, 211, 153)))
                        {
                            string timeStr = comp.LastSavedTime != DateTime.MinValue ? $"{comp.LastSavedTime:HH:mm:ss}" : "";
                            graphics.DrawString($"✔ Gravado em Disco [{timeStr}]", footerFont, okBrush, footerRect.Right - 8, footerRect.Y + footerRect.Height * 0.5f, sfRight);
                        }
                    }
                    else if (comp.CapturedBitmap != null)
                    {
                        using (var prevBrush = new SolidBrush(Color.FromArgb(56, 189, 248)))
                        {
                            graphics.DrawString("● Preview Ativo", footerFont, prevBrush, footerRect.Right - 8, footerRect.Y + footerRect.Height * 0.5f, sfRight);
                        }
                    }
                    else
                    {
                        graphics.DrawString("Aguardando", footerFont, footerBrush, footerRect.Right - 8, footerRect.Y + footerRect.Height * 0.5f, sfRight);
                    }
                }
            }
        }

        private void DrawHeaderButton(Graphics g, FontFamily fam, RectangleF rect, string text, Color bgColor, Color borderColor, Color textColor)
        {
            using (var btnBg = new SolidBrush(bgColor))
            using (var btnBorder = new Pen(borderColor, 1f))
            using (var btnFont = new Font(fam, 6.5f, FontStyle.Bold))
            using (var btnTextBrush = new SolidBrush(textColor))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillRectangle(btnBg, rect);
                g.DrawRectangle(btnBorder, rect.X, rect.Y, rect.Width, rect.Height);
                g.DrawString(text, btnFont, btnTextBrush, rect, sf);
            }
        }
    }
}
