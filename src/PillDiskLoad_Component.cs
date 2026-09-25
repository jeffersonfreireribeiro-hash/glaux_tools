using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Componente que lê e carrega qualquer tipo de dado, geometria ou árvore de dados previamente salva em disco (.pilldata).
    /// Restaura 100% da topologia original de ramos, caminhos e tipos de dados do Grasshopper.
    /// Inclui Resolução Inteligente de Caminhos Multi-Máquina (portabilidade automática entre computadores).
    /// </summary>
    public class PillDiskLoad_Component : GH_Component
    {
        public string CurrentCleanKey { get; private set; } = "";
        public string CurrentCategory { get; private set; } = "LOAD";
        public string CurrentUnit { get; private set; } = ".pilldata";
        public string FileExtensionDisplay => CurrentUnit;
        public Color CurrentCategoryColor { get; private set; } = Color.FromArgb(0, 137, 123);
        public bool LastLoadSuccess { get; private set; } = false;
        public string StatusSummary { get; private set; } = "Aguardando";
        public string CurrentDirectory { get; private set; } = "";

        private bool _forceReload = false;
        private string _customFilePathOverride = "";
        private string _cachedFilePath = "";
        private DateTime _cachedFileWriteTime = DateTime.MinValue;
        private GH_Structure<IGH_Goo> _cachedTree = null;
        private string _cachedTimestamp = "";
        private string _cachedSummary = "";

        public PillDiskLoad_Component()
            : base(
                "Pill Disk Load",
                "PillLoad",
                "Carrega e restaura com fidelidade total qualquer árvore de dados ou geometria gravada em disco pelo Pill Disk Save (.pilldata ou .ghdata).\n" +
                "- Possui busca inteligente de arquivos portátil entre diferentes computadores (resolve automaticamente caminhos de outro PC);\n" +
                "- Procura na mesma pasta do arquivo .gh, na subpasta 'PillVault' ou diretório especificado;\n" +
                "- Dê duplo-clique no componente para selecionar o arquivo diretamente no disco.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("51311372-75dd-4c70-889e-fd41a5348507");

        protected override Bitmap Icon => GlauxToolsIcons.PillDiskLoad;

        public override void CreateAttributes()
        {
            m_attributes = new Pill_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter(
                "Key", "K",
                "Nome da chave a carregar, nome do arquivo ou caminho completo.\n" +
                "Se contiver o caminho de outro computador, o componente localizará o arquivo automaticamente na pasta do seu projeto local.",
                GH_ParamAccess.item);

            pManager.AddTextParameter(
                "Directory", "DIR",
                "Diretório onde procurar o arquivo. Se omitido, procura automaticamente na mesma pasta do arquivo .gh e na subpasta 'PillVault'.",
                GH_ParamAccess.item);

            pManager.AddBooleanParameter(
                "Reload", "R",
                "Gatilho para forçar a releitura do arquivo do disco (Botão ou Timer).",
                GH_ParamAccess.item, false);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Árvore de dados restaurada com tipos e estrutura originais intactos.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Timestamp", "TS", "Data e hora em que o arquivo foi gravado no disco.", GH_ParamAccess.item);
            pManager.AddTextParameter("Summary", "SUM", "Informações de diagnóstico do arquivo (itens, galhos, tamanho e caminho em disco).", GH_ParamAccess.item);
        }

        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            bool rc = base.Write(writer);
            if (!string.IsNullOrEmpty(_customFilePathOverride))
            {
                writer.SetString("CustomFilePathOverride", _customFilePathOverride);
            }
            return rc;
        }

        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            bool rc = base.Read(reader);
            if (reader.ItemExists("CustomFilePathOverride"))
            {
                _customFilePathOverride = reader.GetString("CustomFilePathOverride");
            }
            return rc;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string rawKey = "";
            DA.GetData(0, ref rawKey);

            string rawDir = "";
            DA.GetData(1, ref rawDir);

            bool triggerReload = false;
            DA.GetData(2, ref triggerReload);

            if (_forceReload)
            {
                triggerReload = true;
                _forceReload = false;
            }

            // Resolução Inteligente do Arquivo
            string resolvedPath = ResolvePillFilePath(rawKey, rawDir, out List<string> searchedLocations, out string searchLog);

            if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
            {
                LastLoadSuccess = false;
                StatusSummary = "Não encontrado";

                string targetName = !string.IsNullOrWhiteSpace(rawKey) ? Path.GetFileName(rawKey.Trim().Trim('"', '\'')) : "Sem Chave";
                CurrentCleanKey = Path.GetFileNameWithoutExtension(targetName);

                var sbErr = new StringBuilder();
                sbErr.AppendLine($"Arquivo '{targetName}' não encontrado.");
                sbErr.AppendLine("Locais verificados:");
                for (int i = 0; i < searchedLocations.Count; i++)
                {
                    sbErr.AppendLine($"  [{i + 1}] {searchedLocations[i]}");
                }
                sbErr.AppendLine("Dica: coloque o arquivo .pilldata na mesma pasta do seu arquivo .gh ou dê duplo clique no componente para selecioná-lo.");

                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, sbErr.ToString());
                DA.SetDataTree(0, new GH_Structure<IGH_Goo>());
                DA.SetData(1, "");
                DA.SetData(2, sbErr.ToString());
                return;
            }

            CurrentDirectory = Path.GetDirectoryName(resolvedPath);
            CurrentCleanKey = Path.GetFileNameWithoutExtension(resolvedPath);
            CurrentUnit = Path.GetExtension(resolvedPath);

            var fi = new FileInfo(resolvedPath);
            DateTime currentWriteTime = fi.LastWriteTimeUtc;

            // Reutiliza cache em memória se o arquivo não foi alterado
            if (!triggerReload &&
                _cachedTree != null &&
                string.Equals(_cachedFilePath, resolvedPath, StringComparison.OrdinalIgnoreCase) &&
                _cachedFileWriteTime == currentWriteTime)
            {
                LastLoadSuccess = true;
                DA.SetDataTree(0, _cachedTree);
                DA.SetData(1, _cachedTimestamp);
                DA.SetData(2, _cachedSummary + " [Cache Memória]");
                return;
            }

            // Deserialização resiliente
            try
            {
                byte[] bytes = File.ReadAllBytes(resolvedPath);
                var archive = new GH_IO.Serialization.GH_Archive();
                if (!archive.Deserialize_Binary(bytes))
                {
                    throw new InvalidDataException("Formato de arquivo binário inválido ou corrompido.");
                }

                var tree = new GH_Structure<IGH_Goo>();
                bool extracted = false;

                // 1. Tentar extrair pelo chunk padrão "PillTree"
                try
                {
                    extracted = archive.ExtractObject(tree, "PillTree");
                }
                catch { }

                // 2. Tentar extrair por chunks padrão do Grasshopper (.ghdata)
                if (!extracted)
                {
                    try
                    {
                        extracted = archive.ExtractObject(tree, "Tree") ||
                                    archive.ExtractObject(tree, "Data") ||
                                    archive.ExtractObject(tree, "GH_Structure");
                    }
                    catch { }
                }

                // 3. Tentar varredura em todos os chunks da raiz
                if (!extracted)
                {
                    var root = archive.GetRootNode;
                    if (root != null)
                    {
                        for (int i = 0; i < root.ChunkCount; i++)
                        {
                            var ch = root.Chunks[i];
                            if (ch != null && !string.Equals(ch.Name, "PillMeta", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    if (archive.ExtractObject(tree, ch.Name))
                                    {
                                        extracted = true;
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }

                if (!extracted)
                {
                    throw new InvalidDataException("Estrutura de dados não encontrada dentro do arquivo.");
                }

                string timeText = "";
                try
                {
                    var root = archive.GetRootNode;
                    if (root != null && root.FindChunk("PillMeta") is GH_IO.Serialization.GH_Chunk meta)
                    {
                        if (meta.ItemExists("Timestamp"))
                        {
                            DateTime dt = meta.GetDate("Timestamp");
                            timeText = dt.ToString("yyyy-MM-dd HH:mm:ss");
                        }
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(timeText))
                {
                    timeText = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                }

                string sizeText = fi.Length >= 1024 * 1024
                    ? $"{fi.Length / (1024.0 * 1024.0):F2} MB"
                    : $"{fi.Length / 1024.0:F1} KB";

                string summary = $"{tree.DataCount} itens em {tree.PathCount} ramo(s) | {sizeText} | {timeText} | Arquivo: {resolvedPath}";

                _cachedTree = tree;
                _cachedFilePath = resolvedPath;
                _cachedFileWriteTime = currentWriteTime;
                _cachedTimestamp = timeText;
                _cachedSummary = summary;
                LastLoadSuccess = true;
                StatusSummary = $"{tree.DataCount} itens ({sizeText})";

                DA.SetDataTree(0, tree);
                DA.SetData(1, timeText);
                DA.SetData(2, summary);
            }
            catch (Exception ex)
            {
                LastLoadSuccess = false;
                StatusSummary = "Erro ao carregar";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Falha ao carregar arquivo '{resolvedPath}': {ex.Message}");
                DA.SetDataTree(0, new GH_Structure<IGH_Goo>());
                DA.SetData(1, "");
                DA.SetData(2, $"Erro: {ex.Message}");
            }
        }

        /// <summary>
        /// Localizador inteligente de arquivos com suporte a caminhos de outros computadores,
        /// pastas relativas, busca na pasta do arquivo .gh e subpastas 'PillVault'.
        /// </summary>
        private string ResolvePillFilePath(string rawKey, string rawDir, out List<string> searchedLocations, out string searchLog)
        {
            searchedLocations = new List<string>();
            searchLog = "";

            // 1. Override manual definido pelo usuário (via janela de seleção)
            if (!string.IsNullOrEmpty(_customFilePathOverride))
            {
                if (File.Exists(_customFilePathOverride))
                {
                    searchLog = $"Arquivo via seleção manual: '{_customFilePathOverride}'";
                    return _customFilePathOverride;
                }
                else
                {
                    // Se o override gravado era de outro PC, adota o nome do arquivo dele para busca local
                    string ovFileName = Path.GetFileName(_customFilePathOverride);
                    if (!string.IsNullOrEmpty(ovFileName))
                    {
                        rawKey = ovFileName;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(rawKey)) return null;

            string cleanInput = rawKey.Trim().Trim('"', '\'');
            if (cleanInput.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                try { cleanInput = new Uri(cleanInput).LocalPath; } catch { }
            }

            // 2. Se for um caminho absoluto válido e existente diretamente nesta máquina
            if (Path.IsPathRooted(cleanInput) && File.Exists(cleanInput))
            {
                searchLog = $"Caminho direto existente: '{cleanInput}'";
                return cleanInput;
            }

            // 3. Extrair variações de nome do arquivo
            string justFileName = Path.GetFileName(cleanInput);
            string baseName = Path.GetFileNameWithoutExtension(cleanInput);
            string parentFolderName = "";
            try
            {
                string dirName = Path.GetDirectoryName(cleanInput);
                if (!string.IsNullOrEmpty(dirName))
                    parentFolderName = Path.GetFileName(dirName);
            }
            catch { }

            var nameVariations = new List<string>();
            if (!string.IsNullOrEmpty(justFileName)) nameVariations.Add(justFileName);
            if (!string.IsNullOrEmpty(baseName))
            {
                string pExt = baseName + ".pilldata";
                if (!nameVariations.Contains(pExt, StringComparer.OrdinalIgnoreCase)) nameVariations.Add(pExt);

                string gExt = baseName + ".ghdata";
                if (!nameVariations.Contains(gExt, StringComparer.OrdinalIgnoreCase)) nameVariations.Add(gExt);

                if (!nameVariations.Contains(baseName, StringComparer.OrdinalIgnoreCase)) nameVariations.Add(baseName);
            }

            // 4. Coleta de diretórios candidatos para busca
            var candidateDirs = new List<string>();

            void AddCandidate(string d)
            {
                if (string.IsNullOrWhiteSpace(d)) return;
                try
                {
                    string full = Path.GetFullPath(d);
                    if (!candidateDirs.Contains(full, StringComparer.OrdinalIgnoreCase))
                    {
                        candidateDirs.Add(full);
                    }
                }
                catch { }
            }

            // Diretório do documento .gh atual
            string docDir = null;
            var doc = OnPingDocument();
            if (doc != null && !string.IsNullOrEmpty(doc.FilePath))
            {
                try { docDir = Path.GetDirectoryName(doc.FilePath); } catch { }
            }

            // A. Diretório informado pelo usuário no pino DIR
            if (!string.IsNullOrWhiteSpace(rawDir))
            {
                string cleanDir = rawDir.Trim().Trim('"', '\'');
                if (Path.IsPathRooted(cleanDir))
                {
                    AddCandidate(cleanDir);
                    AddCandidate(Path.Combine(cleanDir, "PillVault"));
                    AddCandidate(Path.Combine(cleanDir, "pillvault"));
                }
                else if (!string.IsNullOrEmpty(docDir))
                {
                    AddCandidate(Path.Combine(docDir, cleanDir));
                    AddCandidate(Path.Combine(docDir, cleanDir, "PillVault"));
                }
                else
                {
                    AddCandidate(cleanDir);
                }
            }

            // B. Pasta do arquivo .gh atual (Crucial para portabilidade!)
            if (!string.IsNullOrEmpty(docDir))
            {
                AddCandidate(docDir); // Mesma pasta do script .gh
                AddCandidate(Path.Combine(docDir, "PillVault"));
                AddCandidate(Path.Combine(docDir, "pillvault"));
                AddCandidate(Path.Combine(docDir, "Data"));
                AddCandidate(Path.Combine(docDir, "data"));

                if (!string.IsNullOrEmpty(parentFolderName) &&
                    !parentFolderName.Equals("PillVault", StringComparison.OrdinalIgnoreCase) &&
                    !parentFolderName.Equals("data", StringComparison.OrdinalIgnoreCase))
                {
                    AddCandidate(Path.Combine(docDir, parentFolderName));
                }
            }

            // C. Pasta do modelo Rhino (.3dm) atual
            try
            {
                var rhinoDoc = Rhino.RhinoDoc.ActiveDoc;
                if (rhinoDoc != null && !string.IsNullOrEmpty(rhinoDoc.Path))
                {
                    string rDir = Path.GetDirectoryName(rhinoDoc.Path);
                    AddCandidate(rDir);
                    AddCandidate(Path.Combine(rDir, "PillVault"));
                }
            }
            catch { }

            // D. Pastas locais do usuário (Desktop, Documentos, Downloads)
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            AddCandidate(desktop);
            AddCandidate(Path.Combine(desktop, "PillVault"));

            string personal = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            AddCandidate(personal);
            AddCandidate(Path.Combine(personal, "PillVault"));

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                string downloads = Path.Combine(userProfile, "Downloads");
                AddCandidate(downloads);
                AddCandidate(Path.Combine(downloads, "PillVault"));
            }

            // E. Pasta Global Grasshopper PillVault no AppData
            string appDataPill = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Grasshopper", "PillVault");
            AddCandidate(appDataPill);

            // 5. Executar a busca por prioridade
            foreach (string dir in candidateDirs)
            {
                if (!Directory.Exists(dir))
                {
                    searchedLocations.Add($"{dir} (não existe)");
                    continue;
                }

                searchedLocations.Add(dir);

                // A. Testar correspondência direta
                foreach (string nameVar in nameVariations)
                {
                    string testPath = Path.Combine(dir, nameVar);
                    if (File.Exists(testPath))
                    {
                        searchLog = $"Encontrado em: '{testPath}'";
                        return testPath;
                    }
                }

                // B. Testar correspondência case-insensitive no diretório
                try
                {
                    string[] dirFiles = Directory.GetFiles(dir);
                    foreach (string f in dirFiles)
                    {
                        string fName = Path.GetFileName(f);
                        string fBase = Path.GetFileNameWithoutExtension(f);
                        string fExt = Path.GetExtension(f);

                        if (string.Equals(fExt, ".pilldata", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(fExt, ".ghdata", StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.Equals(fName, justFileName, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(fBase, baseName, StringComparison.OrdinalIgnoreCase))
                            {
                                searchLog = $"Encontrado por varredura em: '{f}'";
                                return f;
                            }
                        }
                    }
                }
                catch { }
            }

            // 6. Se ainda não achou, e houver um único arquivo .pilldata na pasta do .gh, adotar como sugestão
            if (!string.IsNullOrEmpty(docDir) && Directory.Exists(docDir))
            {
                try
                {
                    string[] docPillFiles = Directory.GetFiles(docDir, "*.pilldata");
                    if (docPillFiles.Length == 1)
                    {
                        searchLog = $"Único arquivo .pilldata na pasta do .gh: '{docPillFiles[0]}'";
                        return docPillFiles[0];
                    }
                }
                catch { }
            }

            return null;
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

        public void PromptSelectFile()
        {
            try
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Filter = "Arquivos Pill Data (*.pilldata;*.ghdata)|*.pilldata;*.ghdata|Todos os Arquivos (*.*)|*.*";
                    ofd.Title = "Selecionar Arquivo Pill Data para Carregar";

                    string initDir = CurrentDirectory;
                    if (string.IsNullOrEmpty(initDir) || !Directory.Exists(initDir))
                    {
                        var doc = OnPingDocument();
                        if (doc != null && !string.IsNullOrEmpty(doc.FilePath))
                        {
                            initDir = Path.GetDirectoryName(doc.FilePath);
                        }
                    }

                    if (!string.IsNullOrEmpty(initDir) && Directory.Exists(initDir))
                    {
                        ofd.InitialDirectory = initDir;
                    }

                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        _customFilePathOverride = ofd.FileName;
                        _forceReload = true;
                        ExpireSolution(true);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir seletor de arquivos: {ex.Message}", "Glaux Tools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendSeparator(menu);

            Menu_AppendItem(menu, "Recarregar Arquivo do Disco", (s, e) =>
            {
                _forceReload = true;
                ExpireSolution(true);
            }, GlauxToolsIcons.PillDiskLoad);

            Menu_AppendItem(menu, "Selecionar Arquivo no Disco...", (s, e) =>
            {
                PromptSelectFile();
            });

            if (!string.IsNullOrEmpty(_customFilePathOverride))
            {
                Menu_AppendItem(menu, "Limpar Seleção Manual (Voltar para Chave)", (s, e) =>
                {
                    _customFilePathOverride = "";
                    _forceReload = true;
                    ExpireSolution(true);
                });
            }

            Menu_AppendItem(menu, "Abrir Pasta no Windows Explorer", (s, e) =>
            {
                OpenTargetFolder();
            });
        }
    }
}
