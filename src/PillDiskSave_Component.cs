using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Componente que salva qualquer tipo de dado, geometria ou árvore de dados do Grasshopper em disco (.pilldata).
    /// Preserva 100% da topologia de galhos, índices e tipos nativos do Grasshopper com serialização binária compacta.
    /// </summary>
    public class PillDiskSave_Component : GH_Component
    {
        public string CurrentCleanKey { get; private set; } = "";
        public string CurrentCategory { get; private set; } = "SAVE";
        public string CurrentUnit { get; private set; } = ".pilldata";
        public string FileExtensionDisplay => CurrentUnit;
        public Color CurrentCategoryColor { get; private set; } = Color.FromArgb(230, 81, 0);
        public bool LastSavedSuccess { get; private set; } = false;
        public string StatusSummary { get; private set; } = "Pronto";
        public string CurrentDirectory { get; private set; } = "";

        private bool _forceSave = false;
        private string _lastSavedPath = "";
        private DateTime _lastSavedTime = DateTime.MinValue;
        private long _lastFileSizeBytes = 0;

        public PillDiskSave_Component()
            : base(
                "Pill Disk Save",
                "PillSave",
                "Salva qualquer dado ou árvore de dados do Grasshopper em disco (.pilldata) com preservação total de tipos e topologia (geometrias, listas, números, matrizes, simulações). Suporta acionamento por botão/gatilho.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("288677b5-5db2-4450-8f57-77b05cecab61");

        protected override Bitmap Icon => GlauxToolsIcons.PillDiskSave;

        public override void CreateAttributes()
        {
            m_attributes = new Pill_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Qualquer dado, lista ou árvore de dados do Grasshopper a ser persistido em disco.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Key", "K", "Nome da chave / identificador do arquivo (ex: 'Geometria_Paredes' ou 'Resultados_Wallacei').", GH_ParamAccess.item);
            pManager.AddTextParameter("Directory", "DIR", "Diretório no disco. Se omitido, utiliza a subpasta 'PillVault' junto ao arquivo .gh atual.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Save", "S", "Gatilho para salvar em disco (True = efetua gravação). Conecte um Botão ou Toggle.", GH_ParamAccess.item, true);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("FilePath", "F", "Caminho completo do arquivo gravado no disco.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "OK", "True se a gravação no disco foi concluída com sucesso.", GH_ParamAccess.item);
            pManager.AddTextParameter("Summary", "SUM", "Resumo dos dados salvos (ramificações, contagem de itens, tamanho e data).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> tree))
            {
                tree = new GH_Structure<IGH_Goo>();
            }

            string rawKey = "";
            if (!DA.GetData(1, ref rawKey) || string.IsNullOrWhiteSpace(rawKey))
            {
                CurrentCleanKey = "Sem Chave";
                LastSavedSuccess = false;
                StatusSummary = "Informe uma chave";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Defina uma chave ou nome de arquivo para gravação em disco.");
                DA.SetData(0, "");
                DA.SetData(1, false);
                DA.SetData(2, "Nenhum arquivo gravado: chave ausente.");
                return;
            }

            string rawKeyClean = rawKey.Trim().Trim('"', '\'');
            if (rawKeyClean.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                try { rawKeyClean = new Uri(rawKeyClean).LocalPath; } catch { }
            }

            CurrentCleanKey = rawKeyClean;

            string rawDir = "";
            DA.GetData(2, ref rawDir);

            bool triggerSave = true;
            DA.GetData(3, ref triggerSave);

            if (_forceSave)
            {
                triggerSave = true;
                _forceSave = false;
            }

            // Resolução do diretório de destino
            string targetDir = !string.IsNullOrWhiteSpace(rawDir) ? rawDir.Trim().Trim('"', '\'') : "";
            var doc = OnPingDocument();
            if (doc != null && !string.IsNullOrEmpty(doc.FilePath))
            {
                string docDir = Path.GetDirectoryName(doc.FilePath);
                if (string.IsNullOrWhiteSpace(targetDir))
                {
                    targetDir = Path.Combine(docDir, "PillVault");
                }
                else if (targetDir == "." || targetDir == "./" || targetDir == @".\")
                {
                    targetDir = docDir;
                }
                else if (!Path.IsPathRooted(targetDir))
                {
                    targetDir = Path.Combine(docDir, targetDir);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(targetDir))
                {
                    targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Grasshopper", "PillVault");
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Documento .gh ainda não foi salvo. Arquivo gravado em '%APPDATA%\\Grasshopper\\PillVault'.");
                }
            }

            CurrentDirectory = targetDir;

            // Se o usuário passou um caminho absoluto no próprio Key
            string fullPath;
            if (Path.IsPathRooted(CurrentCleanKey))
            {
                fullPath = CurrentCleanKey;
                CurrentDirectory = Path.GetDirectoryName(fullPath);
                CurrentCleanKey = Path.GetFileNameWithoutExtension(fullPath);
            }
            else
            {
                string safeFileName = SanitizeFileName(CurrentCleanKey);
                if (!safeFileName.EndsWith(".pilldata", StringComparison.OrdinalIgnoreCase) &&
                    !safeFileName.EndsWith(".ghdata", StringComparison.OrdinalIgnoreCase))
                {
                    safeFileName += ".pilldata";
                }
                fullPath = Path.Combine(CurrentDirectory, safeFileName);
            }

            CurrentUnit = Path.GetExtension(fullPath);

            if (!triggerSave)
            {
                // Não está gravando nesta iteração
                if (File.Exists(fullPath))
                {
                    var fi = new FileInfo(fullPath);
                    _lastSavedPath = fullPath;
                    _lastSavedTime = fi.LastWriteTime;
                    _lastFileSizeBytes = fi.Length;
                    LastSavedSuccess = true;
                    StatusSummary = $"Em espera ({_lastFileSizeBytes / 1024.0:F1} KB)";
                    DA.SetData(0, fullPath);
                    DA.SetData(1, true);
                    DA.SetData(2, $"Arquivo existente em disco. Última gravação: {_lastSavedTime:yyyy-MM-dd HH:mm:ss} | {_lastFileSizeBytes / 1024.0:F1} KB (Gatilho Save = False)");
                }
                else
                {
                    LastSavedSuccess = false;
                    StatusSummary = "Aguardando gatilho";
                    DA.SetData(0, fullPath);
                    DA.SetData(1, false);
                    DA.SetData(2, "Aguardando acionamento do gatilho Save para gravar no disco.");
                }
                return;
            }

            try
            {
                if (!Directory.Exists(CurrentDirectory))
                {
                    Directory.CreateDirectory(CurrentDirectory);
                }

                var archive = new GH_IO.Serialization.GH_Archive();
                archive.AppendObject(tree, "PillTree");

                var root = archive.GetRootNode;
                var meta = root.CreateChunk("PillMeta");
                meta.SetString("Key", CurrentCleanKey);
                meta.SetDate("Timestamp", DateTime.Now);
                meta.SetInt32("PathCount", tree.PathCount);
                meta.SetInt32("DataCount", tree.DataCount);
                meta.SetString("Plugin", "Glaux Tools");

                byte[] bytes = archive.Serialize_Binary();
                File.WriteAllBytes(fullPath, bytes);

                _lastSavedPath = fullPath;
                _lastSavedTime = DateTime.Now;
                _lastFileSizeBytes = bytes.Length;
                LastSavedSuccess = true;

                string sizeText = _lastFileSizeBytes >= 1024 * 1024
                    ? $"{_lastFileSizeBytes / (1024.0 * 1024.0):F2} MB"
                    : $"{_lastFileSizeBytes / 1024.0:F1} KB";

                StatusSummary = $"{tree.DataCount} itens ({sizeText})";

                DA.SetData(0, fullPath);
                DA.SetData(1, true);
                DA.SetData(2, $"Salvo com sucesso: {tree.DataCount} itens em {tree.PathCount} ramo(s) | {sizeText} | {_lastSavedTime:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                LastSavedSuccess = false;
                StatusSummary = "Erro ao salvar";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Falha ao gravar arquivo em disco: {ex.Message}");
                DA.SetData(0, fullPath);
                DA.SetData(1, false);
                DA.SetData(2, $"Erro: {ex.Message}");
            }
        }

        public void OpenTargetFolder()
        {
            try
            {
                string dir = CurrentDirectory;
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    Process.Start("explorer.exe", dir);
                }
                else
                {
                    var doc = OnPingDocument();
                    if (doc != null && !string.IsNullOrEmpty(doc.FilePath))
                    {
                        string docDir = Path.GetDirectoryName(doc.FilePath);
                        if (Directory.Exists(docDir))
                            Process.Start("explorer.exe", docDir);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível abrir a pasta: {ex.Message}", "Glaux Tools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Salvar Agora no Disco", (s, e) =>
            {
                _forceSave = true;
                ExpireSolution(true);
            }, GlauxToolsIcons.PillDiskSave);

            Menu_AppendItem(menu, "Abrir Pasta no Windows Explorer", (s, e) =>
            {
                OpenTargetFolder();
            });
        }

        private static string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
