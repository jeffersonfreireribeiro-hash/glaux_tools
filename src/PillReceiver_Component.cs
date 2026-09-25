using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class PillReceiver_Component : GH_Component
    {
        public string CurrentCleanKey { get; private set; } = "";
        public string CurrentCategory { get; private set; } = "GEN";
        public string CurrentUnit { get; private set; } = "";
        public Color CurrentCategoryColor { get; private set; } = Color.FromArgb(108, 117, 125);
        public bool IsConnected { get; private set; } = false;
        public bool HasWarning { get; private set; } = false;
        public string StatusShort { get; private set; } = "Desconectado";
        public int AssignedColorIndex { get; set; } = -1;

        private string _internalSelectedKey = "";
        private string _internalExpectedType = "";

        public PillReceiver_Component()
            : base(
                "Pill Receiver",
                "PillRx",
                "Recebe dados ou árvores (DataTree) de um canal sem fios Pill. Detecta incompatibilidade de tipo, calcula tempo decorrido e preserva a estrutura original de dados com imutabilidade absoluta.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("a1100002-e1ef-4000-8000-000000000002");
        protected override Bitmap Icon => GlauxToolsIcons.PillReceiver;

        public override void CreateAttributes()
        {
            m_attributes = new Pill_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Key", "K", "Nome do canal a escutar (pode ser conectado via cabo ou selecionado diretamente no menu de clique direito do componente).", GH_ParamAccess.item);
            pManager.AddTextParameter("ExpectedType", "T", "Tipo de dado esperado opcional para validação (ex: 'Number', 'Point', 'Mesh', 'Curve', 'String'). Emite aviso se houver divergência.", GH_ParamAccess.item, "");
            pManager.AddGenericParameter("Wire", "⚡", "Cabo físico oculto conectado ao Pill Transmitter (Wire Display: Hidden). Garante sincronização sequencial perfeita para o Wallacei/Galapagos mantendo a estética limpa.", GH_ParamAccess.tree);
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[2].WireDisplay = GH_ParamWireDisplay.hidden;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Dados recebidos do canal sem fios, preservando a árvore original e com imutabilidade garantida.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Timestamp", "TS", "Horário exato da última atualização transmitida.", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Status do canal: Online, Órfão (Transmitter ausente), Mismatch (tipo incompatível) ou Desatualizado.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string inputKey = "";
            bool hasKeyWire = DA.GetData(0, ref inputKey);
            string keyToUse = hasKeyWire && !string.IsNullOrWhiteSpace(inputKey) ? inputKey : _internalSelectedKey;

            if (string.IsNullOrWhiteSpace(keyToUse))
            {
                CurrentCleanKey = "";
                CurrentCategory = "GEN";
                CurrentUnit = "";
                CurrentCategoryColor = Color.FromArgb(108, 117, 125);
                IsConnected = false;
                HasWarning = false;
                StatusShort = "Sem Canal";
                Message = "Selecione Canal";
                DA.SetData(2, "Aguardando canal (conecte ou selecione via botão direito).");
                return;
            }

            string cleanKey = PillHub.CleanUpKey(keyToUse);
            if (!string.IsNullOrEmpty(CurrentCleanKey) && !string.Equals(CurrentCleanKey, cleanKey, StringComparison.OrdinalIgnoreCase))
            {
                PillHub.UnsubscribeReceiver(CurrentCleanKey, InstanceGuid);
            }
            CurrentCleanKey = cleanKey;
            EnsureColorIndex(OnPingDocument());

            // Inscreve este receptor no barramento central para receber eventos em tempo real
            PillHub.SubscribeReceiver(cleanKey, InstanceGuid);

            string inputExpectedType = "";
            bool hasTypeWire = DA.GetData(1, ref inputExpectedType);
            string typeToUse = hasTypeWire && !string.IsNullOrWhiteSpace(inputExpectedType) ? inputExpectedType : _internalExpectedType;

            // 1. Tenta obter os dados prioritariamente pelo cabo físico oculto (Wire Display -> Hidden)
            // (Apenas se o transmissor NÃO for um PillSliderPool multi-canal)
            GH_Structure<IGH_Goo> wireData = null;
            bool hasWireData = Params.Input.Count > 2 
                && !Params.Input[2].Sources.Any(s => s?.Attributes?.GetTopLevel?.DocObject is PillSliderPool_Component)
                && DA.GetDataTree(2, out wireData) 
                && wireData != null 
                && wireData.DataCount > 0;

            if (hasWireData)
            {
                wireData = PillHub.FilterWireDataForReceiver(Params.Input[2], wireData, cleanKey, OnPingDocument());

                PillHub.ParseKeyMetadata(keyToUse, null, out _, out string cat, out string u, out Color c);
                CurrentCategory = cat;
                CurrentUnit = u;
                CurrentCategoryColor = c;
                IsConnected = true;

                string wireTypeName = "Generic";
                var first = wireData.AllData(true).FirstOrDefault();
                if (first != null) wireTypeName = first.TypeName;

                bool typeMismatch = false;
                if (!string.IsNullOrWhiteSpace(typeToUse))
                {
                    string exp = typeToUse.Trim().ToLowerInvariant();
                    string actual = (wireTypeName ?? "").ToLowerInvariant();
                    if (!actual.Contains(exp) && !exp.Contains(actual))
                    {
                        typeMismatch = true;
                    }
                }

                HasWarning = typeMismatch;
                string statusDesc = typeMismatch ? $"Mismatch: Esperado [{typeToUse}], recebido [{wireTypeName}]" : "Online (Cabo Oculto ⚡)";
                if (typeMismatch)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, statusDesc);
                }

                StatusShort = statusDesc;
                Message = $"⚡ [{CurrentCategory}] {wireData.DataCount} it";

                DA.SetDataTree(0, wireData);
                DA.SetData(1, DateTime.Now.ToString("HH:mm:ss"));
                DA.SetData(2, statusDesc);
                return;
            }

            // 2. Fallback: obtém do barramento de memória (PillHub)
            if (PillHub.TryGetChannel(cleanKey, out PillChannel channel))
            {
                CurrentCategory = channel.Category;
                CurrentUnit = channel.Unit;
                CurrentCategoryColor = channel.CategoryColor;
                IsConnected = true;

                // Validação de tipo
                bool typeMismatch = false;
                if (!string.IsNullOrWhiteSpace(typeToUse))
                {
                    string exp = typeToUse.Trim().ToLowerInvariant();
                    string actual = (channel.DataTypeName ?? "").ToLowerInvariant();
                    if (!actual.Contains(exp) && !exp.Contains(actual))
                    {
                        typeMismatch = true;
                    }
                }

                // Verificação de tempo parado (mais de 10 minutos sem atualizar)
                bool isOutdated = (DateTime.Now - channel.LastUpdated).TotalMinutes > 10.0;

                HasWarning = typeMismatch || isOutdated;

                string statusDesc = "Online";
                if (typeMismatch)
                {
                    statusDesc = $"Mismatch: Esperado [{typeToUse}], mas recebido [{channel.DataTypeName}]";
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, statusDesc);
                }
                else if (isOutdated)
                {
                    statusDesc = $"Desatualizado: Última atualização há {(DateTime.Now - channel.LastUpdated).TotalMinutes:F0} min";
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, statusDesc);
                }

                StatusShort = statusDesc;
                Message = $"[{channel.Category}] {channel.TotalItemCount} it";

                DA.SetDataTree(0, channel.Data);
                DA.SetData(1, channel.LastUpdated.ToString("HH:mm:ss"));
                DA.SetData(2, statusDesc);
            }
            else
            {
                PillHub.ParseKeyMetadata(keyToUse, null, out _, out string cat, out string u, out Color c);
                CurrentCategory = cat;
                CurrentUnit = u;
                CurrentCategoryColor = c;
                IsConnected = false;
                HasWarning = true;
                StatusShort = "Órfão";
                Message = "Órfão (Sem Tx)";

                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Canal '{cleanKey}' não foi publicado por nenhum Transmitter no barramento.");

                DA.SetDataTree(0, new GH_Structure<IGH_Goo>());
                DA.SetData(1, "N/A");
                DA.SetData(2, $"Órfão: Canal '{cleanKey}' não publicado no barramento.");
            }
        }

        public bool AutoConnectHiddenWire(GH_Document doc = null, string targetKey = null)
        {
            if (doc == null) doc = OnPingDocument();
            if (doc == null) return false;

            string key = !string.IsNullOrWhiteSpace(targetKey) ? targetKey : CurrentCleanKey;
            if (string.IsNullOrWhiteSpace(key)) key = _internalSelectedKey;
            if (string.IsNullOrWhiteSpace(key)) return false;
            string cleanKey = PillHub.CleanUpKey(key);

            if (Params.Input.Count < 3) return false;
            var rxIn = Params.Input[2];

            // 0. Lookup O(1) instantâneo do transmissor no barramento central
            Guid txGuid = PillHub.GetTransmitterGuid(cleanKey, doc);
            if (txGuid != Guid.Empty)
            {
                var srcObj = doc.FindObject(txGuid, false) as GH_Component;
                // PillSliderPool não deve receber cabo físico para não acoplar a DAG de todos os sliders
                if (srcObj != null && !(srcObj is PillSliderPool_Component) && srcObj.Params.Output.Count > 0)
                {
                    var txOut = srcObj.Params.Output[0];
                    if (rxIn.Sources.Contains(txOut))
                    {
                        rxIn.WireDisplay = GH_ParamWireDisplay.hidden;
                        return true;
                    }

                    RecordUndoEvent("Conectar Cabo Oculto Pill");
                    rxIn.RemoveAllSources();
                    rxIn.AddSource(txOut);
                    rxIn.WireDisplay = GH_ParamWireDisplay.hidden;
                    return true;
                }
            }

            // 1. Fallback: Procura PillTransmitter_Component no canvas
            foreach (var obj in doc.Objects)
            {
                if (obj is PillTransmitter_Component tx)
                {
                    if (string.Equals(tx.CurrentCleanKey, cleanKey, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(PillHub.CleanUpKey(tx.NickName), cleanKey, StringComparison.OrdinalIgnoreCase))
                    {
                        if (tx.Params.Output.Count > 0)
                        {
                            var txOut = tx.Params.Output[0];
                            if (rxIn.Sources.Contains(txOut))
                            {
                                rxIn.WireDisplay = GH_ParamWireDisplay.hidden;
                                return true;
                            }

                            RecordUndoEvent("Conectar Cabo Oculto Pill");
                            rxIn.RemoveAllSources();
                            rxIn.AddSource(txOut);
                            rxIn.WireDisplay = GH_ParamWireDisplay.hidden;
                            return true;
                        }
                    }
                }
            }

            // 2. Procura em PillCache_Component
            foreach (var obj in doc.Objects)
            {
                if (obj is PillCache_Component cache)
                {
                    if (string.Equals(cache.CurrentCleanKey, cleanKey, StringComparison.OrdinalIgnoreCase))
                    {
                        if (cache.Params.Output.Count > 0)
                        {
                            var cacheOut = cache.Params.Output[0];
                            if (rxIn.Sources.Contains(cacheOut))
                            {
                                rxIn.WireDisplay = GH_ParamWireDisplay.hidden;
                                return true;
                            }

                            RecordUndoEvent("Conectar Cabo Oculto Pill");
                            rxIn.RemoveAllSources();
                            rxIn.AddSource(cacheOut);
                            rxIn.WireDisplay = GH_ParamWireDisplay.hidden;
                            return true;
                        }
                    }
                }
            }

            // 4. Procura em PillLayerPipeline_Component
            foreach (var obj in doc.Objects)
            {
                if (obj is PillLayerPipeline_Component pipeline)
                {
                    if (string.Equals(pipeline.CurrentCleanKey, cleanKey, StringComparison.OrdinalIgnoreCase))
                    {
                        if (pipeline.Params.Output.Count > 0)
                        {
                            var pipeOut = pipeline.Params.Output[0];
                            if (rxIn.Sources.Contains(pipeOut))
                            {
                                rxIn.WireDisplay = GH_ParamWireDisplay.hidden;
                                return true;
                            }

                            RecordUndoEvent("Conectar Cabo Oculto Pill");
                            rxIn.RemoveAllSources();
                            rxIn.AddSource(pipeOut);
                            rxIn.WireDisplay = GH_ParamWireDisplay.hidden;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public void DisconnectHiddenWire()
        {
            if (Params.Input.Count > 2)
            {
                RecordUndoEvent("Desconectar Cabo Oculto Pill");
                Params.Input[2].RemoveAllSources();
            }
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            // ==============================================================
            // 0. CABO FÍSICO OCULTO (MODO WALLACEI SÍNCRONO)
            // ==============================================================
            var wireMenu = new ToolStripMenuItem("⚡ Conexão Oculta (Modo Wallacei / Hidden Wire)");
            bool isWireConnected = Params.Input.Count > 2 && Params.Input[2].SourceCount > 0;
            var statusItem = new ToolStripMenuItem(isWireConnected ? "Status: Conectado por Cabo Oculto (Síncrono ⚡)" : "Status: Modo Sem Fio em Memória") { Enabled = false };
            wireMenu.DropDownItems.Add(statusItem);
            wireMenu.DropDownItems.Add(new ToolStripSeparator());

            var connectBtn = new ToolStripMenuItem("➔ Conectar Cabo Oculto a este Canal (Wire Display: Hidden)")
            {
                Enabled = !isWireConnected
            };
            connectBtn.Click += (s, e) =>
            {
                if (AutoConnectHiddenWire(OnPingDocument()))
                {
                    ExpireSolution(true);
                }
                else
                {
                    MessageBox.Show("Nenhum Transmitter ativo com este nome foi encontrado no Canvas para conexão.", "Pill Hidden Wire", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            wireMenu.DropDownItems.Add(connectBtn);

            var disconnectBtn = new ToolStripMenuItem("✕ Desconectar Cabo Oculto deste Receptor")
            {
                Enabled = isWireConnected
            };
            disconnectBtn.Click += (s, e) =>
            {
                DisconnectHiddenWire();
                ExpireSolution(true);
            };
            wireMenu.DropDownItems.Add(disconnectBtn);

            wireMenu.DropDownItems.Add(new ToolStripSeparator());

            var connectAllBtn = new ToolStripMenuItem("⚡ CONECTAR TODOS os Pills do Canvas com Cabos Ocultos (Recomendado para Wallacei)");
            connectAllBtn.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                int c = PillHub.ConnectAllDocumentPillsHidden(doc);
                MessageBox.Show($"Sucesso! {c} canais foram conectados com cabos físicos invisíveis (Wire Display -> Hidden).\n\nO Wallacei agora possui sincronização sequencial perfeita garantida pelo Grasshopper!", "Pill Hidden Wire", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ExpireSolution(true);
            };
            wireMenu.DropDownItems.Add(connectAllBtn);

            var disconnectAllBtn = new ToolStripMenuItem("🔌 DESCONECTAR TODOS os Cabos Ocultos do Canvas");
            disconnectAllBtn.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                int c = PillHub.DisconnectAllDocumentPillsHidden(doc);
                MessageBox.Show($"Todos os {c} cabos ocultos foram desconectados. O Canvas voltou ao modo sem fio tradicional.", "Pill Hidden Wire", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ExpireSolution(true);
            };
            wireMenu.DropDownItems.Add(disconnectAllBtn);

            menu.Items.Add(wireMenu);
            menu.Items.Add(new ToolStripSeparator());

            // ==============================================================
            // 1. SELEÇÃO DUPLA / AGRUPADA POR CATEGORIA (ACU, GEO, MAT, ETC.)
            // ==============================================================
            var catSubMenu = new ToolStripMenuItem("Selecionar Canal por Grupo / Categoria");
            var allChannels = PillHub.GetAllChannels();
            var allActiveKeys = PillHub.GetAllActiveKeys();

            if (allActiveKeys.Count == 0)
            {
                catSubMenu.DropDownItems.Add(new ToolStripMenuItem("Nenhum canal ativo no barramento") { Enabled = false });
            }
            else
            {
                // Agrupa por categoria
                var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var k in allActiveKeys)
                {
                    PillHub.ParseKeyMetadata(k, null, out string cKey, out string cat, out _, out _);
                    if (!groups.ContainsKey(cat)) groups[cat] = new List<string>();
                    groups[cat].Add(k);
                }

                foreach (var kvp in groups.OrderBy(g => g.Key))
                {
                    string catName = kvp.Key;
                    string catLabel = GetCategoryDisplayName(catName);
                    var groupMenu = new ToolStripMenuItem($"[{catName}] {catLabel} ({kvp.Value.Count})");

                    foreach (var key in kvp.Value.OrderBy(k => k))
                    {
                        bool isSelected = string.Equals(key, CurrentCleanKey, StringComparison.OrdinalIgnoreCase);
                        var item = new ToolStripMenuItem(key)
                        {
                            Checked = isSelected
                        };
                        string targetKey = key;
                        item.Click += (s, e) =>
                        {
                            RecordUndoEvent("Selecionar Canal Pill");
                            _internalSelectedKey = targetKey;
                            AutoConnectHiddenWire(OnPingDocument(), targetKey);
                            ExpireSolution(true);
                        };
                        groupMenu.DropDownItems.Add(item);
                    }
                    catSubMenu.DropDownItems.Add(groupMenu);
                }
            }
            menu.Items.Add(catSubMenu);

            // Lista plana rápida de todos os canais
            var flatSubMenu = new ToolStripMenuItem("Todos os Canais (Lista Plana)");
            if (allActiveKeys.Count == 0)
            {
                flatSubMenu.DropDownItems.Add(new ToolStripMenuItem("Nenhum canal ativo") { Enabled = false });
            }
            else
            {
                foreach (var key in allActiveKeys)
                {
                    bool isSelected = string.Equals(key, CurrentCleanKey, StringComparison.OrdinalIgnoreCase);
                    var item = new ToolStripMenuItem(key) { Checked = isSelected };
                    string targetKey = key;
                    item.Click += (s, e) =>
                    {
                        RecordUndoEvent("Selecionar Canal Pill");
                        _internalSelectedKey = targetKey;
                        AutoConnectHiddenWire(OnPingDocument(), targetKey);
                        ExpireSolution(true);
                    };
                    flatSubMenu.DropDownItems.Add(item);
                }
            }
            menu.Items.Add(flatSubMenu);

            // ==============================================================
            // 2. TIPO DE DADO ESPERADO (EXPECTED TYPE) & VALUE LIST
            // ==============================================================
            var typeSubMenu = new ToolStripMenuItem("Tipo de Dado Esperado (ExpectedType)");

            var standardTypes = new (string label, string val)[]
            {
                ("Qualquer Tipo (Generic)", ""),
                ("Number (Double / Número)", "Number"),
                ("Integer (Inteiro)", "Integer"),
                ("Point (Ponto 3D)", "Point"),
                ("Vector (Vetor 3D)", "Vector"),
                ("Curve (Curva / Polilinha)", "Curve"),
                ("Surface (Superfície)", "Surface"),
                ("Brep (Polissuperfície)", "Brep"),
                ("Mesh (Malha)", "Mesh"),
                ("Plane (Plano)", "Plane"),
                ("String (Texto)", "String"),
                ("Boolean (Verdadeiro/Falso)", "Boolean")
            };

            foreach (var st in standardTypes)
            {
                bool isSelected = string.Equals(_internalExpectedType, st.val, StringComparison.OrdinalIgnoreCase);
                var item = new ToolStripMenuItem(st.label) { Checked = isSelected };
                string targetType = st.val;
                item.Click += (s, e) =>
                {
                    RecordUndoEvent("Definir Tipo Esperado");
                    _internalExpectedType = targetType;
                    ExpireSolution(true);
                };
                typeSubMenu.DropDownItems.Add(item);
            }

            typeSubMenu.DropDownItems.Add(new ToolStripSeparator());

            // Criar ValueList automática no Canvas
            var createTypeListBtn = new ToolStripMenuItem("➔ Conectar Value List de Tipos no Canvas");
            createTypeListBtn.Click += (s, e) =>
            {
                SpawnExpectedTypeValueList();
            };
            typeSubMenu.DropDownItems.Add(createTypeListBtn);

            menu.Items.Add(typeSubMenu);

            // Opção para criar ValueList de Canais no Canvas
            var createChannelListBtn = new ToolStripMenuItem("➔ Conectar Value List de Canais no Canvas");
            createChannelListBtn.Click += (s, e) =>
            {
                SpawnChannelsValueList();
            };
            menu.Items.Add(createChannelListBtn);

            // Submenu de seleção e visualização de Cor do Jump
            int myIdx = EnsureColorIndex(OnPingDocument());
            var colorSubMenu = new ToolStripMenuItem($"🎨 Cor do Jump (Receptor #{myIdx + 1}: {PillHub.GetColorName(myIdx)})");
            for (int i = 0; i < 8; i++)
            {
                int cIdx = i;
                Color col = PillHub.GetReceiverColor(cIdx);
                string colName = PillHub.GetColorName(cIdx);
                var item = new ToolStripMenuItem($"#{cIdx + 1} - {colName}");
                item.Checked = (myIdx == cIdx);
                var bmp = new Bitmap(14, 14);
                using (var g = Graphics.FromImage(bmp))
                using (var sb = new SolidBrush(col))
                using (var p = new Pen(Color.FromArgb(60, 60, 60), 1f))
                {
                    g.FillRectangle(sb, 1, 1, 11, 11);
                    g.DrawRectangle(p, 1, 1, 11, 11);
                }
                item.Image = bmp;
                item.Click += (s, e) =>
                {
                    RecordUndoEvent("Mudar Cor do Jump");
                    AssignedColorIndex = cIdx;
                    Attributes.ExpireLayout();
                    Grasshopper.Instances.ActiveCanvas?.Refresh();
                };
                colorSubMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(colorSubMenu);

            menu.Items.Add(new ToolStripSeparator());

            // Opção para limpar canais fantasmas da memória
            var purgeGhostItem = new ToolStripMenuItem("Limpar Canais Fantasmas / Inativos da Memória");
            purgeGhostItem.Click += (s, e) =>
            {
                PillHub.PurgeOrphanChannels(OnPingDocument());
                ExpireSolution(true);
            };
            menu.Items.Add(purgeGhostItem);
        }

        private static string GetCategoryDisplayName(string cat)
        {
            switch (cat.ToUpperInvariant())
            {
                case "ACU": return "Acústica";
                case "GEO": return "Geometria";
                case "MAT": return "Materiais";
                case "SIM": return "Simulação";
                case "CFG": return "Configuração";
                case "OUT": return "Resultados";
                case "IN": return "Entradas";
                case "CACHE": return "Cache";
                case "U": return "Grupo U";
                default: return "Geral";
            }
        }

        private void SpawnExpectedTypeValueList()
        {
            var doc = OnPingDocument();
            if (doc == null) return;

            var vl = new GH_ValueList();
            vl.CreateAttributes();
            vl.NickName = "ExpectedType";
            vl.Name = "Tipos Esperados";
            vl.ListItems.Clear();

            var types = new (string key, string val)[]
            {
                ("Qualquer Tipo", ""),
                ("Number (Número)", "Number"),
                ("Integer (Inteiro)", "Integer"),
                ("Point (Ponto 3D)", "Point"),
                ("Vector (Vetor)", "Vector"),
                ("Curve (Curva)", "Curve"),
                ("Surface (Superfície)", "Surface"),
                ("Brep (Polissuperfície)", "Brep"),
                ("Mesh (Malha)", "Mesh"),
                ("Plane (Plano)", "Plane"),
                ("String (Texto)", "String"),
                ("Boolean (Booleano)", "Boolean")
            };

            foreach (var t in types)
            {
                vl.ListItems.Add(new GH_ValueListItem(t.key, "\"" + t.val + "\""));
            }

            PointF paramPivot = Params.Input[1].Attributes.Pivot;
            vl.Attributes.Pivot = new PointF(paramPivot.X - 220, paramPivot.Y - 10);

            doc.AddObject(vl, false);
            Params.Input[1].AddSource(vl);
            vl.ExpireSolution(true);
        }

        private void SpawnChannelsValueList()
        {
            var doc = OnPingDocument();
            if (doc == null) return;

            var vl = new GH_ValueList();
            vl.CreateAttributes();
            vl.NickName = "PillChannels";
            vl.Name = "Canais Ativos";
            vl.ListItems.Clear();

            var activeKeys = PillHub.GetAllActiveKeys();
            if (activeKeys.Count == 0)
            {
                vl.ListItems.Add(new GH_ValueListItem("Sem canais ativos", "\"\""));
            }
            else
            {
                foreach (var key in activeKeys)
                {
                    PillHub.ParseKeyMetadata(key, null, out string cleanKey, out string cat, out string unit, out _);
                    string label = string.IsNullOrEmpty(unit) ? $"[{cat}] {key}" : $"[{cat}] {key} [{unit}]";
                    vl.ListItems.Add(new GH_ValueListItem(label, "\"" + cleanKey + "\""));
                }
            }

            PointF paramPivot = Params.Input[0].Attributes.Pivot;
            vl.Attributes.Pivot = new PointF(paramPivot.X - 220, paramPivot.Y - 10);

            doc.AddObject(vl, false);
            Params.Input[0].AddSource(vl);
            vl.ExpireSolution(true);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetString("InternalSelectedKey", _internalSelectedKey ?? "");
            writer.SetString("InternalExpectedType", _internalExpectedType ?? "");
            writer.SetInt32("AssignedColorIndex", AssignedColorIndex);
            return base.Write(writer);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (!string.IsNullOrEmpty(_internalSelectedKey))
            {
                PillHub.SubscribeReceiver(PillHub.CleanUpKey(_internalSelectedKey), InstanceGuid);
            }
            EnsureColorIndex(document);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("InternalSelectedKey"))
            {
                _internalSelectedKey = reader.GetString("InternalSelectedKey");
                if (!string.IsNullOrEmpty(_internalSelectedKey))
                {
                    PillHub.SubscribeReceiver(PillHub.CleanUpKey(_internalSelectedKey), InstanceGuid);
                }
            }
            if (reader.ItemExists("InternalExpectedType"))
            {
                _internalExpectedType = reader.GetString("InternalExpectedType");
            }
            if (reader.ItemExists("AssignedColorIndex"))
            {
                AssignedColorIndex = reader.GetInt32("AssignedColorIndex");
            }
            return base.Read(reader);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);
            if (!string.IsNullOrEmpty(CurrentCleanKey))
            {
                PillHub.UnsubscribeReceiver(CurrentCleanKey, InstanceGuid);
            }
        }

        /// <summary>
        /// Garante que o receptor possua um AssignedColorIndex válido e exclusivo para o canal CurrentCleanKey.
        /// Se o índice estiver desatribuído (-1) ou em colisão com outro receptor do mesmo canal (ex: após copiar e colar),
        /// calcula o menor índice inteiro livre (0, 1, 2, ...), garantindo correspondência 100% determinística.
        /// </summary>
        public int EnsureColorIndex(GH_Document doc)
        {
            if (AssignedColorIndex >= 0) return AssignedColorIndex;

            string cleanKey = CurrentCleanKey;
            if (string.IsNullOrWhiteSpace(cleanKey))
            {
                AssignedColorIndex = 0;
                return 0;
            }

            int idx = PillHub.GetReceiverIndex(cleanKey, InstanceGuid);
            if (idx >= 0)
            {
                AssignedColorIndex = idx;
                return idx;
            }

            AssignedColorIndex = 0;
            return 0;
        }
    }
}
