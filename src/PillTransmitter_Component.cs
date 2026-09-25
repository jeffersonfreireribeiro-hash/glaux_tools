using System;
using System.Drawing;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class PillTransmitter_Component : GH_Component
    {
        public string CurrentCleanKey { get; private set; } = "";
        public string CurrentCategory { get; private set; } = "GEN";
        public string CurrentUnit { get; private set; } = "";
        public Color CurrentCategoryColor { get; private set; } = Color.FromArgb(108, 117, 125);
        public bool HasData { get; private set; } = false;
        public string ItemCountSummary { get; private set; } = "0 it";

        private string _lastPublishedKey = "";

        public PillTransmitter_Component()
            : base(
                "Pill Transmitter",
                "PillTx",
                "Transmite dados ou árvores (DataTree) sem fiação pelo canvas, com categoria cromática automática por prefixo (ACU_, GEO_, MAT_), unidade embutida, timestamp e validação de integridade.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("a1100001-e1ef-4000-8000-000000000001");
        protected override Bitmap Icon => GlauxToolsIcons.PillTransmitter;

        public override void CreateAttributes()
        {
            m_attributes = new Pill_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Key", "K", "Identificador do canal (ex: 'ACU_T60 [s]', 'GEO::Malha', 'MAT_Absorcao'). O prefixo define a categoria e a cor do pill automaticamente.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Data", "D", "Dados ou árvore (DataTree) a serem publicados no canal.", GH_ParamAccess.tree);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Unit", "U", "Unidade opcional (ex: 's', 'dB', 'Hz', 'm³'). Se não informada, será detectada automaticamente de colchetes na chave [unidade].", GH_ParamAccess.item, "");
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Pass", "D", "Pass-through opcional dos dados (para inspecionar ou encadear sem fio adicional).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Info", "I", "Diagnóstico do canal: chave limpa, categoria, unidade, contagem e timestamp.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string rawKey = "";
            if (!DA.GetData(0, ref rawKey) || string.IsNullOrWhiteSpace(rawKey))
            {
                // Se a chave foi desconectada ou apagada, despublica a chave antiga imediatamente
                if (!string.IsNullOrEmpty(_lastPublishedKey))
                {
                    PillHub.Unpublish(_lastPublishedKey, InstanceGuid);
                    _lastPublishedKey = "";
                }

                CurrentCleanKey = "";
                CurrentCategory = "GEN";
                CurrentUnit = "";
                CurrentCategoryColor = Color.FromArgb(108, 117, 125);
                HasData = false;
                ItemCountSummary = "Sem chave";
                Message = "Sem Chave";
                return;
            }

            string cleanKey = PillHub.CleanUpKey(rawKey);

            // Se o usuário renomeou a chave, despublica a chave anterior
            if (!string.IsNullOrEmpty(_lastPublishedKey) && !string.Equals(_lastPublishedKey, cleanKey, StringComparison.OrdinalIgnoreCase))
            {
                PillHub.Unpublish(_lastPublishedKey, InstanceGuid);
            }
            _lastPublishedKey = cleanKey;

            string explicitUnit = "";
            DA.GetData(2, ref explicitUnit);

            if (!DA.GetDataTree(1, out GH_Structure<IGH_Goo> dataTree))
            {
                dataTree = new GH_Structure<IGH_Goo>();
            }

            Guid docId = OnPingDocument()?.DocumentID ?? Guid.Empty;
            var channel = PillHub.Publish(rawKey, dataTree, InstanceGuid, docId, explicitUnit, forceNotify: false, sourceNickName: NickName);

            if (channel != null)
            {
                CurrentCleanKey = channel.CleanKey;
                CurrentCategory = channel.Category;
                CurrentUnit = channel.Unit;
                CurrentCategoryColor = channel.CategoryColor;
                HasData = channel.TotalItemCount > 0;
                ItemCountSummary = $"{channel.TotalItemCount} it";

                string info = $"Canal: {channel.CleanKey} | Cat: [{channel.Category}] | Unidade: {(string.IsNullOrEmpty(channel.Unit) ? "N/A" : channel.Unit)} | Itens: {channel.TotalItemCount} ({channel.BranchCount} ramos) | Tipo: {channel.DataTypeName} | Atualizado: {channel.LastUpdated:HH:mm:ss}";

                DA.SetDataTree(0, dataTree);
                DA.SetData(1, info);

                Message = $"[{channel.Category}] {channel.TotalItemCount} it";
            }
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);
            // Remove o canal publicado do barramento assim que o componente for excluído do Canvas
            if (!string.IsNullOrEmpty(_lastPublishedKey))
            {
                PillHub.Unpublish(_lastPublishedKey, InstanceGuid);
                _lastPublishedKey = "";
            }
        }

        public int ConnectMyReceiversWithHiddenWires(GH_Document doc = null)
        {
            if (doc == null) doc = OnPingDocument();
            if (doc == null || string.IsNullOrWhiteSpace(CurrentCleanKey)) return 0;

            int connectedCount = 0;
            string cleanKey = CurrentCleanKey;

            var rxs = PillHub.GetActiveReceivers(cleanKey, doc);
            foreach (var rx in rxs)
            {
                if (rx.AutoConnectHiddenWire(doc, cleanKey)) connectedCount++;
            }
            return connectedCount;
        }

        public int DisconnectMyReceivers(GH_Document doc = null)
        {
            if (doc == null) doc = OnPingDocument();
            if (doc == null || string.IsNullOrWhiteSpace(CurrentCleanKey)) return 0;

            int disconnectedCount = 0;
            string cleanKey = CurrentCleanKey;

            var rxs = PillHub.GetActiveReceivers(cleanKey, doc);
            foreach (var rx in rxs)
            {
                rx.DisconnectHiddenWire();
                disconnectedCount++;
            }
            return disconnectedCount;
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            // ==============================================================
            // CABO FÍSICO OCULTO (MODO WALLACEI SÍNCRONO)
            // ==============================================================
            var wireMenu = new ToolStripMenuItem("⚡ Conexão Oculta (Modo Wallacei / Hidden Wire)");

            var connectMyBtn = new ToolStripMenuItem("➔ Conectar Todos os Receptores deste Canal com Cabos Ocultos");
            connectMyBtn.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                int c = ConnectMyReceiversWithHiddenWires(doc);
                MessageBox.Show($"Conectados {c} receptores ao canal '{CurrentCleanKey}' com cabos físicos invisíveis (Wire Display -> Hidden).", "Pill Hidden Wire", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ExpireSolution(true);
            };
            wireMenu.DropDownItems.Add(connectMyBtn);

            var disconnectMyBtn = new ToolStripMenuItem("✕ Desconectar Receptores deste Canal");
            disconnectMyBtn.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                int c = DisconnectMyReceivers(doc);
                MessageBox.Show($"Desconectados {c} receptores do canal '{CurrentCleanKey}'.", "Pill Hidden Wire", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ExpireSolution(true);
            };
            wireMenu.DropDownItems.Add(disconnectMyBtn);

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

            var purgeItem = new ToolStripMenuItem("Remover Este Canal da Memória");
            purgeItem.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_lastPublishedKey))
                {
                    PillHub.Unpublish(_lastPublishedKey, InstanceGuid);
                    _lastPublishedKey = "";
                    ExpireSolution(true);
                }
            };
            menu.Items.Add(purgeItem);
        }
    }
}
