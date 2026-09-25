// PillHook_Component.cs
// Componente Receptor Sem Fios ultracompacto em formato de Etiqueta / Seta (Cluster Input Tag).
// Conecta-se diretamente aos pinos de entrada no Canvas, exibe badge de categoria colorido,
// nome do parâmetro, LED de status e permite selecionar canais do PillHub com 1 clique.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class PillHook_Component : GH_Component
    {
        public string CurrentCleanKey { get; private set; } = "";
        public string CurrentCategory { get; private set; } = "GEN";
        public string CurrentUnit { get; private set; } = "";
        public Color CurrentCategoryColor { get; private set; } = Color.FromArgb(245, 158, 11);
        public bool IsConnected { get; private set; } = false;
        public bool HasWarning { get; private set; } = false;
        public string StatusShort { get; private set; } = "Desconectado";

        private string _internalSelectedKey = "";
        private string _lastSubscribedKey = "";

        public PillHook_Component()
            : base(
                "Pill Tag Receiver",
                "PillHook",
                "Receptor sem fios ultracompacto em formato de etiqueta com seta (estilo Cluster Input Tag).\n" +
                "- Conecta-se diretamente ao pino de entrada de qualquer componente sem cabos longos.\n" +
                "- Clique na etiqueta para escolher o canal a partir dos grupos e categorias do PillHub.\n" +
                "- Exibe badge colorido do grupo, nome do parâmetro e LED luminoso de status em tempo real.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("a110000e-e1ef-4000-8000-00000000000e");
        protected override Bitmap Icon => GlauxToolsIcons.PillHook;

        public override void CreateAttributes()
        {
            m_attributes = new PillHook_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Key", "K", "Nome opcional do canal Pill a escutar (se omitido, selecione o canal clicando diretamente na etiqueta).", GH_ParamAccess.item);
            pManager.AddGenericParameter("Wire", "⚡", "Cabo físico oculto conectado ao Pill Transmitter (Wire Display: Hidden). Garante sincronização sequencial perfeita para o Wallacei/Galapagos mantendo a estética limpa.", GH_ParamAccess.tree);
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[1].WireDisplay = GH_ParamWireDisplay.hidden;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Value", "V", "Valor ou árvore de dados recebida do canal sem fios Pill.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string wireKey = "";
            bool hasWire = DA.GetData(0, ref wireKey) && !string.IsNullOrWhiteSpace(wireKey);

            string activeKey = hasWire ? wireKey.Trim() : _internalSelectedKey;

            if (string.IsNullOrWhiteSpace(activeKey))
            {
                CurrentCleanKey = "Selecione Canal";
                CurrentCategory = "PILL";
                CurrentCategoryColor = Color.FromArgb(148, 163, 184);
                IsConnected = false;
                HasWarning = false;
                StatusShort = "Aguardando Seleção";
                Message = "Sem Canal";
                return;
            }

            string cleanKey = PillHub.CleanUpKey(activeKey);

            // Gerencia inscrição no canal
            if (!string.Equals(_lastSubscribedKey, cleanKey, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(_lastSubscribedKey))
                {
                    PillHub.UnsubscribeReceiver(_lastSubscribedKey, InstanceGuid);
                }
                PillHub.SubscribeReceiver(cleanKey, InstanceGuid);
                _lastSubscribedKey = cleanKey;
            }

            // 1. Tenta obter os dados prioritariamente pelo cabo físico oculto (Wire Display -> Hidden)
            // Mas IGNORA qualquer cabo vindo de PillSliderPool_Component para que os sliders operem 100% independentes via barramento de memória PillHub
            bool isFromPool = Params.Input.Count > 1 && Params.Input[1].Sources.Any(s => 
                s?.Attributes?.GetTopLevel?.DocObject is PillSliderPool_Component);

            GH_Structure<IGH_Goo> wireData = null;
            bool hasWireData = !isFromPool && Params.Input.Count > 1 && DA.GetDataTree(1, out wireData) && wireData != null && wireData.DataCount > 0;

            if (hasWireData)
            {
                wireData = PillHub.FilterWireDataForReceiver(Params.Input[1], wireData, cleanKey, OnPingDocument());

                PillHub.ParseKeyMetadata(activeKey, null, out string cKey, out string cat, out string un, out Color color);
                CurrentCleanKey = cKey;
                CurrentCategory = cat;
                CurrentUnit = un;
                CurrentCategoryColor = color;
                IsConnected = true;
                HasWarning = false;

                int itemCount = wireData.DataCount;
                StatusShort = itemCount > 1 ? $"{itemCount} it (Cabo ⚡)" : (itemCount == 1 ? "1 it (Cabo ⚡)" : "Vazio");
                Message = "";

                DA.SetDataTree(0, PillHub.CloneStructure(wireData));
                return;
            }

            // 2. Fallback: barramento de memória (PillHub)
            if (PillHub.TryGetChannel(cleanKey, out PillChannel channel))
            {
                CurrentCleanKey = channel.CleanKey;
                CurrentCategory = channel.Category;
                CurrentUnit = channel.Unit;
                CurrentCategoryColor = channel.CategoryColor;
                IsConnected = true;
                HasWarning = false;

                int itemCount = channel.TotalItemCount;
                StatusShort = itemCount > 1 ? $"{itemCount} itens" : (itemCount == 1 ? "1 item" : "Vazio");

                if (channel.Data != null)
                {
                    DA.SetDataTree(0, PillHub.CloneStructure(channel.Data));
                }
                Message = "";
            }
            else
            {
                PillHub.ParseKeyMetadata(activeKey, null, out string cKey, out string cat, out string un, out Color color);
                CurrentCleanKey = cKey;
                CurrentCategory = cat;
                CurrentUnit = un;
                CurrentCategoryColor = color;
                IsConnected = false;
                HasWarning = true;
                StatusShort = "Transmitter Ausente";
                Message = "Órfão";
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

            if (Params.Input.Count < 2) return false;
            var hookIn = Params.Input[1];

            // 0. Lookup O(1) instantâneo do transmissor no barramento central
            Guid txGuid = PillHub.GetTransmitterGuid(cleanKey, doc);
            if (txGuid != Guid.Empty)
            {
                var srcObj = doc.FindObject(txGuid, false) as GH_Component;
                if (srcObj != null && srcObj.Params.Output.Count > 0)
                {
                    // Se for PillSliderPool_Component, opera 100% sem fio via memória PillHub para garantir independência de cada slider
                    if (srcObj is PillSliderPool_Component)
                    {
                        if (hookIn.Sources.Count > 0)
                        {
                            RecordUndoEvent("Desconectar Cabo Oculto");
                            hookIn.RemoveAllSources();
                        }
                        return true;
                    }

                    var txOut = srcObj.Params.Output[0];
                    if (hookIn.Sources.Contains(txOut))
                    {
                        hookIn.WireDisplay = GH_ParamWireDisplay.hidden;
                        return true;
                    }

                    RecordUndoEvent("Conectar Cabo Oculto Pill Hook");
                    hookIn.RemoveAllSources();
                    hookIn.AddSource(txOut);
                    hookIn.WireDisplay = GH_ParamWireDisplay.hidden;
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
                            if (hookIn.Sources.Contains(txOut))
                            {
                                hookIn.WireDisplay = GH_ParamWireDisplay.hidden;
                                return true;
                            }

                            RecordUndoEvent("Conectar Cabo Oculto Pill Hook");
                            hookIn.RemoveAllSources();
                            hookIn.AddSource(txOut);
                            hookIn.WireDisplay = GH_ParamWireDisplay.hidden;
                            return true;
                        }
                    }
                }
            }

            // 2. Se for slider em PillSliderPool_Component, opera via memória PillHub sem cabos físicos na DAG
            foreach (var obj in doc.Objects)
            {
                if (obj is PillSliderPool_Component pool)
                {
                    var slider = pool.Sliders.FirstOrDefault(s => 
                        string.Equals(s.CleanKey, cleanKey, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(PillHub.CleanUpKey(s.FullKey), cleanKey, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(PillHub.CleanUpKey($"{s.Category}_{s.Name}"), cleanKey, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(PillHub.CleanUpKey(s.Name), cleanKey, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(s.FullKey, key, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(s.Name, key, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals($"{s.Category}_{s.Name}", key, StringComparison.OrdinalIgnoreCase) ||
                        cleanKey.EndsWith("_" + PillHub.CleanUpKey(s.Name), StringComparison.OrdinalIgnoreCase) ||
                        cleanKey.EndsWith("::" + PillHub.CleanUpKey(s.Name), StringComparison.OrdinalIgnoreCase));

                    if (slider != null)
                    {
                        if (hookIn.Sources.Count > 0)
                        {
                            RecordUndoEvent("Desconectar Cabo Oculto");
                            hookIn.RemoveAllSources();
                        }
                        return true;
                    }
                }
            }

            // 3. Procura em PillCache_Component
            foreach (var obj in doc.Objects)
            {
                if (obj is PillCache_Component cache)
                {
                    if (string.Equals(cache.CurrentCleanKey, cleanKey, StringComparison.OrdinalIgnoreCase))
                    {
                        if (cache.Params.Output.Count > 0)
                        {
                            var cacheOut = cache.Params.Output[0];
                            if (hookIn.Sources.Contains(cacheOut))
                            {
                                hookIn.WireDisplay = GH_ParamWireDisplay.hidden;
                                return true;
                            }

                            RecordUndoEvent("Conectar Cabo Oculto Pill Hook");
                            hookIn.RemoveAllSources();
                            hookIn.AddSource(cacheOut);
                            hookIn.WireDisplay = GH_ParamWireDisplay.hidden;
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
                            if (hookIn.Sources.Contains(pipeOut))
                            {
                                hookIn.WireDisplay = GH_ParamWireDisplay.hidden;
                                return true;
                            }

                            RecordUndoEvent("Conectar Cabo Oculto Pill Hook");
                            hookIn.RemoveAllSources();
                            hookIn.AddSource(pipeOut);
                            hookIn.WireDisplay = GH_ParamWireDisplay.hidden;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public void DisconnectHiddenWire()
        {
            if (Params.Input.Count > 1)
            {
                RecordUndoEvent("Desconectar Cabo Oculto Pill Hook");
                Params.Input[1].RemoveAllSources();
            }
        }

        public void SelectKey(string newKey)
        {
            RecordUndoEvent("Selecionar Canal Pill Hook");
            _internalSelectedKey = newKey;
            AutoConnectHiddenWire(OnPingDocument(), newKey);
            ExpireSolution(true);
        }

        public void ShowChannelPicker(Point screenPoint)
        {
            var menu = new ContextMenuStrip();
            PopulateChannelMenu(menu);
            menu.Show(screenPoint);
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Menu_AppendSeparator(menu);
            PopulateChannelMenu(menu);
        }

        private void PopulateChannelMenu(ToolStripDropDown menu)
        {
            var allActiveKeys = PillHub.GetAllActiveKeys();

            // ==============================================================
            // CABO FÍSICO OCULTO (MODO WALLACEI SÍNCRONO)
            // ==============================================================
            var wireMenu = new ToolStripMenuItem("⚡ Conexão Oculta (Modo Wallacei / Hidden Wire)");
            bool isWireConnected = Params.Input.Count > 1 && Params.Input[1].SourceCount > 0;
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

            var disconnectBtn = new ToolStripMenuItem("✕ Desconectar Cabo Oculto deste Hook")
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

            var header = new ToolStripMenuItem("Selecionar Canal por Categoria:") { Enabled = false };
            menu.Items.Add(header);

            if (allActiveKeys.Count == 0)
            {
                menu.Items.Add(new ToolStripMenuItem("Nenhum canal ativo no PillHub") { Enabled = false });
            }
            else
            {
                var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var k in allActiveKeys)
                {
                    PillHub.ParseKeyMetadata(k, null, out _, out string cat, out _, out _);
                    if (!groups.ContainsKey(cat)) groups[cat] = new List<string>();
                    groups[cat].Add(k);
                }

                foreach (var catKvp in groups.OrderBy(k => k.Key))
                {
                    string catCode = catKvp.Key;
                    string catName = GetCategoryDisplayName(catCode);
                    Color catColor = PillHub.GetCategoryColor(catCode);

                    var groupMenu = new ToolStripMenuItem($"[{catCode}] {catName}")
                    {
                        ForeColor = Color.FromArgb(20, 25, 35)
                    };

                    foreach (var key in catKvp.Value.OrderBy(k => k))
                    {
                        bool isSelected = string.Equals(key, CurrentCleanKey, StringComparison.OrdinalIgnoreCase);
                        var item = new ToolStripMenuItem(key) { Checked = isSelected };
                        string targetKey = key;
                        item.Click += (s, e) => SelectKey(targetKey);
                        groupMenu.DropDownItems.Add(item);
                    }
                    menu.Items.Add(groupMenu);
                }
            }

            var flatSubMenu = new ToolStripMenuItem("Todos os Canais (Lista Plana)");
            if (allActiveKeys.Count == 0)
            {
                flatSubMenu.DropDownItems.Add(new ToolStripMenuItem("Nenhum canal ativo") { Enabled = false });
            }
            else
            {
                foreach (var key in allActiveKeys.OrderBy(k => k))
                {
                    bool isSelected = string.Equals(key, CurrentCleanKey, StringComparison.OrdinalIgnoreCase);
                    var item = new ToolStripMenuItem(key) { Checked = isSelected };
                    string targetKey = key;
                    item.Click += (s, e) => SelectKey(targetKey);
                    flatSubMenu.DropDownItems.Add(item);
                }
            }
            menu.Items.Add(flatSubMenu);

            menu.Items.Add(new ToolStripSeparator());
            var purgeGhostItem = new ToolStripMenuItem("Limpar Canais Fantasmas / Inativos");
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
                case "SRC": return "Fontes Sonoras";
                case "REC": return "Receptores";
                case "SIM": return "Simulação";
                case "ENV": return "Ambiente";
                case "CFG": return "Configurações";
                case "RES": return "Resultados";
                case "VAR": return "Variáveis / Sliders";
                case "U": return "Parâmetros do Usuário";
                default: return cat;
            }
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetString("InternalSelectedKey", _internalSelectedKey);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("InternalSelectedKey"))
            {
                _internalSelectedKey = reader.GetString("InternalSelectedKey");
            }
            return base.Read(reader);
        }
    }

    // =========================================================================
    // ATTRIBUTES: RENDER DA ETIQUETA COM PONTA DE SETA (CLUSTER INPUT TAG)
    // =========================================================================
    public class PillHook_Attributes : GH_ComponentAttributes
    {
        private readonly PillHook_Component _hook;
        private const float TAG_HEIGHT = 24f;
        private const float ARROW_WIDTH = 11f;

        public PillHook_Attributes(PillHook_Component owner) : base(owner)
        {
            _hook = owner;
        }

        protected override void Layout()
        {
            // Medir textos para largura dinâmica
            string key = !string.IsNullOrWhiteSpace(_hook.CurrentCleanKey) ? _hook.CurrentCleanKey : "Selecione Canal";
            string cat = !string.IsNullOrWhiteSpace(_hook.CurrentCategory) ? _hook.CurrentCategory : "PILL";

            float badgeW = 30f;
            float textW = 60f;

            using (var fBadge = new Font("Segoe UI", 7.2f, FontStyle.Bold))
            using (var fText = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            {
                SizeF szBadge = GH_FontServer.MeasureString(cat, fBadge);
                badgeW = Math.Max(24f, szBadge.Width + 8f);

                SizeF szText = GH_FontServer.MeasureString(key, fText);
                textW = Math.Max(40f, szText.Width);
            }

            // Largura total: badge + margens + texto + LED + ponta da seta
            float totalWidth = badgeW + textW + 36f + ARROW_WIDTH;
            if (totalWidth < 115f) totalWidth = 115f;

            Bounds = new RectangleF(Pivot.X - totalWidth * 0.5f, Pivot.Y - TAG_HEIGHT * 0.5f, totalWidth, TAG_HEIGHT);

            // Posicionar o pino de saída exatamente na ponta da seta direita
            if (Owner.Params.Output.Count > 0)
            {
                var pOut = Owner.Params.Output[0];
                pOut.Attributes.Bounds = new RectangleF(Bounds.Right - 10f, Bounds.Y, 10f, Bounds.Height);
                pOut.Attributes.Pivot = new PointF(Bounds.Right, Bounds.Y + Bounds.Height * 0.5f);
            }

            // Posicionar pinos de entrada na borda esquerda
            if (Owner.Params.Input.Count > 0)
            {
                var pIn = Owner.Params.Input[0];
                pIn.Attributes.Bounds = new RectangleF(Bounds.Left, Bounds.Y, 10f, Bounds.Height);
                pIn.Attributes.Pivot = new PointF(Bounds.Left, Bounds.Y + Bounds.Height * 0.5f);
            }
            if (Owner.Params.Input.Count > 1)
            {
                var pIn2 = Owner.Params.Input[1];
                pIn2.Attributes.Bounds = new RectangleF(Bounds.Left, Bounds.Y, 10f, Bounds.Height);
                pIn2.Attributes.Pivot = new PointF(Bounds.Left, Bounds.Y + Bounds.Height * 0.5f);
            }
        }

        // RespondToMouseDown não intercepta o clique simples, permitindo selecionar e arrastar o componente normalmente no canvas


        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var ptScreen = sender.PointToScreen(new Point((int)e.CanvasLocation.X, (int)e.CanvasLocation.Y));
                _hook.ShowChannelPicker(ptScreen);
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseDoubleClick(sender, e);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            if (channel == GH_CanvasChannel.Wires)
            {
                base.Render(canvas, graphics, channel);
                return;
            }

            if (channel != GH_CanvasChannel.Objects)
            {
                base.Render(canvas, graphics, channel);
                return;
            }

            RectangleF b = Bounds;
            float midY = b.Y + b.Height * 0.5f;
            float shoulderX = b.Right - ARROW_WIDTH;
            float cornerRadius = 4f;

            Color accent = _hook.CurrentCategoryColor;
            if (accent.IsEmpty) accent = Color.FromArgb(245, 158, 11);

            // 1. Caminho da Etiqueta em formato de Seta (Cluster Input Style)
            using (var tagPath = new GraphicsPath())
            {
                tagPath.AddArc(b.X, b.Y, cornerRadius * 2, cornerRadius * 2, 180, 90);
                tagPath.AddLine(b.X + cornerRadius, b.Y, shoulderX, b.Y);
                tagPath.AddLine(shoulderX, b.Y, b.Right, midY);
                tagPath.AddLine(b.Right, midY, shoulderX, b.Bottom);
                tagPath.AddLine(shoulderX, b.Bottom, b.X + cornerRadius, b.Bottom);
                tagPath.AddArc(b.X, b.Bottom - cornerRadius * 2, cornerRadius * 2, cornerRadius * 2, 90, 90);
                tagPath.CloseFigure();

                // Efeito de Seleção
                if (Selected)
                {
                    using (var selPen = new Pen(Color.FromArgb(140, 0, 180, 240), 4f))
                    {
                        graphics.DrawPath(selPen, tagPath);
                    }
                }

                // Fundo Externo da Etiqueta (Borda colorida e corpo estilizado)
                using (var bgBrush = new LinearGradientBrush(b, Color.FromArgb(254, 243, 199), Color.FromArgb(253, 230, 138), LinearGradientMode.Vertical))
                using (var borderPen = new Pen(accent, Selected ? 2.2f : 1.5f))
                {
                    // Se o componente tiver cor específica de categoria, harmoniza o fundo
                    if (_hook.CurrentCategory.Equals("ACU", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var acuBg = new LinearGradientBrush(b, Color.FromArgb(224, 242, 254), Color.FromArgb(186, 230, 253), LinearGradientMode.Vertical))
                            graphics.FillPath(acuBg, tagPath);
                    }
                    else if (_hook.CurrentCategory.Equals("GEO", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var geoBg = new LinearGradientBrush(b, Color.FromArgb(220, 252, 231), Color.FromArgb(187, 247, 208), LinearGradientMode.Vertical))
                            graphics.FillPath(geoBg, tagPath);
                    }
                    else if (_hook.CurrentCategory.Equals("MAT", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var matBg = new LinearGradientBrush(b, Color.FromArgb(254, 243, 199), Color.FromArgb(253, 230, 138), LinearGradientMode.Vertical))
                            graphics.FillPath(matBg, tagPath);
                    }
                    else
                    {
                        using (var genBg = new LinearGradientBrush(b, Color.FromArgb(248, 250, 252), Color.FromArgb(226, 232, 240), LinearGradientMode.Vertical))
                            graphics.FillPath(genBg, tagPath);
                    }

                    graphics.DrawPath(borderPen, tagPath);
                }
            }

            // 2. Miolo Interno em Moldura (Inset Box estilo Cluster Input da Imagem)
            float insetMargin = 2.5f;
            RectangleF innerBox = new RectangleF(b.X + insetMargin, b.Y + insetMargin, shoulderX - b.X - insetMargin + 2f, b.Height - insetMargin * 2);
            using (var innerPath = CreateRoundedRectangle(innerBox, 2.5f))
            using (var innerBg = new SolidBrush(Color.FromArgb(240, 255, 255, 255)))
            using (var innerBorder = new Pen(Color.FromArgb(120, accent), 1f))
            {
                graphics.FillPath(innerBg, innerPath);
                graphics.DrawPath(innerBorder, innerPath);
            }

            // 3. Badge da Categoria [ACU], [GEO], [MAT]
            string catText = !string.IsNullOrWhiteSpace(_hook.CurrentCategory) ? _hook.CurrentCategory : "GEN";
            float badgeW = 28f;
            using (var fBadge = new Font("Segoe UI", 7.0f, FontStyle.Bold))
            {
                SizeF sz = graphics.MeasureString(catText, fBadge);
                badgeW = Math.Max(22f, (float)Math.Ceiling(sz.Width) + 6f);
                RectangleF badgeRect = new RectangleF(innerBox.X + 3f, innerBox.Y + 2.5f, badgeW, innerBox.Height - 5f);

                using (var badgePath = CreateRoundedRectangle(badgeRect, 2.5f))
                using (var badgeBrush = new SolidBrush(accent))
                {
                    graphics.FillPath(badgeBrush, badgePath);
                }

                using (var textBrush = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    graphics.DrawString(catText, fBadge, textBrush, badgeRect, sf);
                }
            }

            // 4. LED Luminoso de Status (●)
            float ledSize = 6.5f;
            float ledX = innerBox.Right - ledSize - 4f;
            float ledY = innerBox.Y + (innerBox.Height - ledSize) * 0.5f;
            RectangleF ledRect = new RectangleF(ledX, ledY, ledSize, ledSize);

            Color ledColor = _hook.IsConnected
                ? (_hook.HasWarning ? Color.FromArgb(245, 158, 11) : Color.FromArgb(34, 197, 94))
                : Color.FromArgb(239, 68, 68);

            using (var ledBrush = new SolidBrush(ledColor))
            using (var ledPen = new Pen(Color.White, 0.8f))
            {
                graphics.FillEllipse(ledBrush, ledRect);
                graphics.DrawEllipse(ledPen, ledRect);
            }

            // 5. Nome do Canal / Parâmetro
            string keyText = !string.IsNullOrWhiteSpace(_hook.CurrentCleanKey) ? _hook.CurrentCleanKey : "Selecione Canal";
            float textLeft = innerBox.X + badgeW + 6f;
            float textWidth = ledX - textLeft - 3f;

            if (textWidth > 15f)
            {
                RectangleF textRect = new RectangleF(textLeft, innerBox.Y, textWidth, innerBox.Height);
                using (var font = new Font("Segoe UI", 8.0f, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.FromArgb(15, 23, 42)))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    graphics.DrawString(keyText, font, brush, textRect, sf);
                }
            }

            // 6. Terminal de Conexão na Ponta da Seta (Grip Circle)
            using (var gripBrush = new SolidBrush(Color.White))
            using (var gripPen = new Pen(Color.FromArgb(30, 41, 59), 1.5f))
            {
                graphics.FillEllipse(gripBrush, b.Right - 4f, midY - 3.5f, 7f, 7f);
                graphics.DrawEllipse(gripPen, b.Right - 4f, midY - 3.5f, 7f, 7f);
            }
        }

        private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
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
    }
}
