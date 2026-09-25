using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class PillPresetManager_Component : GH_Component
    {
        private readonly Dictionary<string, PillBundle> _presets =
            new Dictionary<string, PillBundle>(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> _logHistory = new List<string>();
        private string _activePresetName = "";
        private string _lastAutoSavedHash = "";
        private int _autoSaveCounter = 0;

        public PillPresetManager_Component()
            : base(
                "Pill Preset & State Manager",
                "PillPresets",
                "Gerencia cenários e alternativas de projeto (ex: 'sala vazia' vs 'com plateia'). Salva e restaura snapshots de parâmetros em memória ou arquivo JSON externo com histórico (logger). Suporta salvamento automático por detecção de modificações.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("a1100007-e1ef-4000-8000-000000000007");
        protected override Bitmap Icon => GlauxToolsIcons.PillPresetManager;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Bundle", "B", "Pacote PillBundle atual a ser registrado ou comparado.", GH_ParamAccess.item);
            pManager.AddTextParameter("PresetName", "N", "Nome do cenário/preset a salvar (ex: 'Sala_Vazia', 'Plateia_100pct').", GH_ParamAccess.item, "");
            pManager.AddBooleanParameter("Save", "S", "Pulso para gravar o snapshot do bundle atual nos presets.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("LoadName", "L", "Nome do preset a ser ativado e carregado.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("FilePath", "FP", "Caminho opcional de arquivo JSON externo para salvar/carregar presets entre arquivos do Rhino.", GH_ParamAccess.item, "");
            pManager.AddBooleanParameter("AutoSave", "AS", "Se True, salva automaticamente um novo preset sempre que detectar alteração nos parâmetros do PillBundle.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("AutoPrefix", "AP", "Prefixo para os nomes dos presets gerados automaticamente (ex: 'Cenario', 'Iteracao').", GH_ParamAccess.item, "Cenario");

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("ActiveBundle", "AB", "Pacote PillBundle correspondente ao preset ativo carregado.", GH_ParamAccess.item);
            pManager.AddTextParameter("AvailablePresets", "P", "Lista com os nomes de todos os cenários disponíveis.", GH_ParamAccess.list);
            pManager.AddTextParameter("LogHistory", "LOG", "Histórico de mudanças e trocas de parâmetros com timestamp (CSV/Log).", GH_ParamAccess.list);
            pManager.AddTextParameter("ActiveJSON", "J", "String JSON do preset atualmente selecionado.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string filePath = "";
            DA.GetData(4, ref filePath);

            // Carrega do arquivo externo se fornecido e existente
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                LoadPresetsFromFile(filePath);
            }

            object bundleObj = null;
            DA.GetData(0, ref bundleObj);
            PillBundle currentBundle = null;
            if (bundleObj is GH_PillBundleGoo pbGoo && pbGoo.Value != null)
            {
                currentBundle = pbGoo.Value;
            }
            else if (bundleObj is GH_ObjectWrapper wrapper && wrapper.Value is PillBundle b)
            {
                currentBundle = b;
            }
            else if (bundleObj is PillBundle directBundle)
            {
                currentBundle = directBundle;
            }

            string presetName = "";
            DA.GetData(1, ref presetName);

            bool save = false;
            DA.GetData(2, ref save);

            string loadName = "";
            DA.GetData(3, ref loadName);

            bool autoSave = false;
            DA.GetData(5, ref autoSave);

            string autoPrefix = "Cenario";
            DA.GetData(6, ref autoPrefix);
            if (string.IsNullOrWhiteSpace(autoPrefix)) autoPrefix = "Cenario";

            // Detecção automática de modificação para AutoSave
            if (autoSave && currentBundle != null)
            {
                string bundleJson = currentBundle.ToJson();
                string currentHash = GetSimpleHash(bundleJson);

                if (!string.Equals(currentHash, _lastAutoSavedHash, StringComparison.Ordinal))
                {
                    _lastAutoSavedHash = currentHash;
                    _autoSaveCounter++;
                    string autoKey = !string.IsNullOrWhiteSpace(presetName) 
                        ? $"{presetName.Trim()}_v{_autoSaveCounter}" 
                        : $"{autoPrefix.Trim()}_{_autoSaveCounter:D2}_{DateTime.Now:HHmmss}";

                    _presets[autoKey] = currentBundle;
                    string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},AUTOSAVE,{autoKey},{currentBundle.Entries.Count} params";
                    _logHistory.Add(logEntry);

                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        SavePresetsToFile(filePath);
                    }
                }
            }

            // Salva novo Preset manual por pulso
            if (save && !string.IsNullOrWhiteSpace(presetName) && currentBundle != null)
            {
                string key = presetName.Trim();
                _presets[key] = currentBundle;
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},SAVED,{key},{currentBundle.Entries.Count} params";
                _logHistory.Add(logEntry);

                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    SavePresetsToFile(filePath);
                }
            }

            // Seleciona Preset para carregamento
            string targetToLoad = !string.IsNullOrWhiteSpace(loadName) ? loadName.Trim() : _activePresetName;

            PillBundle activeBundle = null;
            if (!string.IsNullOrWhiteSpace(targetToLoad) && _presets.TryGetValue(targetToLoad, out var loaded))
            {
                activeBundle = loaded;
                _activePresetName = targetToLoad;
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},LOADED,{targetToLoad},{loaded.Entries.Count} params";
                if (_logHistory.Count == 0 || !_logHistory.Last().Contains($",LOADED,{targetToLoad},"))
                {
                    _logHistory.Add(logEntry);
                }
                Message = $"Preset: {targetToLoad}";
            }
            else if (currentBundle != null)
            {
                activeBundle = currentBundle;
                Message = "Direto (Sem Preset)";
            }
            else
            {
                Message = "Nenhum Preset";
            }

            var availableList = _presets.Keys.OrderBy(k => k).ToList();

            DA.SetData(0, activeBundle != null ? new GH_PillBundleGoo(activeBundle) : null);
            DA.SetDataList(1, availableList);
            DA.SetDataList(2, _logHistory);
            DA.SetData(3, activeBundle != null ? activeBundle.ToJson() : "");
        }

        private void LoadPresetsFromFile(string path)
        {
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var dict = PillJson.DeserializeObject(json);
                if (dict != null)
                {
                    foreach (var kvp in dict)
                    {
                        if (kvp.Value is string s)
                            _presets[kvp.Key] = PillBundle.FromJson(s);
                        else if (kvp.Value is Dictionary<string, object> subDict)
                            _presets[kvp.Key] = PillBundle.FromJson(PillJson.Serialize(subDict));
                    }
                }
            }
            catch
            {
            }
        }

        private void SavePresetsToFile(string path)
        {
            try
            {
                var dict = new Dictionary<string, object>();
                foreach (var kvp in _presets)
                {
                    dict[kvp.Key] = kvp.Value.ToJson();
                }
                string json = PillJson.Serialize(dict, true);
                File.WriteAllText(path, json, Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static string GetSimpleHash(string text)
        {
            if (string.IsNullOrEmpty(text)) return "0";
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.AppendFormat("{0:x2}", b);
                return sb.ToString();
            }
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var presetsSubMenu = new ToolStripMenuItem("Selecionar Preset Salvo");
            if (_presets.Count == 0)
            {
                presetsSubMenu.DropDownItems.Add(new ToolStripMenuItem("Nenhum preset salvo na memória") { Enabled = false });
            }
            else
            {
                foreach (var name in _presets.Keys.OrderBy(k => k))
                {
                    var item = new ToolStripMenuItem(name)
                    {
                        Checked = string.Equals(name, _activePresetName, StringComparison.OrdinalIgnoreCase)
                    };
                    string target = name;
                    item.Click += (s, e) =>
                    {
                        RecordUndoEvent("Ativar Preset");
                        _activePresetName = target;
                        ExpireSolution(true);
                    };
                    presetsSubMenu.DropDownItems.Add(item);
                }
            }
            menu.Items.Add(presetsSubMenu);

            menu.Items.Add(new ToolStripSeparator());
            var hiddenMenu = new ToolStripMenuItem("⚡ Conexão Oculta (Modo Wallacei / Hidden Wire)")
            {
                ToolTipText = "Conecta/desconecta cabos físicos ocultos (Hidden Wire) preservando a sincronia DAG sequencial para Wallacei e Galapagos."
            };

            var connectAllItem = new ToolStripMenuItem("⚡ CONECTAR TODOS os Pills do Canvas com Cabos Ocultos (Recomendado para Wallacei)");
            connectAllItem.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc == null) return;
                int c = PillHub.ConnectAllDocumentPillsHidden(doc);
                doc.NewSolution(false);
                MessageBox.Show($"Todos os {c} receptores do Canvas foram conectados com cabos ocultos (hidden wire)!\nA sequência DAG está garantida para Wallacei e Galapagos.",
                    "Pill System - Modo Wallacei Ativado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            hiddenMenu.DropDownItems.Add(connectAllItem);

            var disconnectAllItem = new ToolStripMenuItem("Desconectar TODOS os Pills do Canvas (Voltar ao Modo 100% Sem Fio)");
            disconnectAllItem.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc == null) return;
                int c = PillHub.DisconnectAllDocumentPillsHidden(doc);
                doc.NewSolution(false);
                MessageBox.Show($"Todos os {c} receptores do Canvas voltaram ao modo 100% sem fio em memória.",
                    "Pill System - Modo Sem Fio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            hiddenMenu.DropDownItems.Add(disconnectAllItem);

            menu.Items.Add(hiddenMenu);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetString("ActivePresetName", _activePresetName ?? "");
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("ActivePresetName"))
            {
                _activePresetName = reader.GetString("ActivePresetName");
            }
            return base.Read(reader);
        }
    }
}
