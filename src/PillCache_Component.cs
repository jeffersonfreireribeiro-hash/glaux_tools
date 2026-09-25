using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class PillCache_Component : GH_Component
    {
        public string CurrentCleanKey { get; private set; } = "";
        public string CurrentCategory { get; private set; } = "CACHE";
        public string CurrentUnit { get; private set; } = "";
        public Color CurrentCategoryColor { get; private set; } = Color.FromArgb(157, 78, 221);
        public bool IsCached { get; private set; } = false;
        public bool IsBypassed { get; private set; } = false;
        public string CacheStatusShort { get; private set; } = "Novo";

        private class CachePayload
        {
            public string InputHash { get; set; }
            public GH_Structure<IGH_Goo> Data { get; set; }
            public DateTime ComputeTime { get; set; }
            public int HitCount { get; set; }
        }

        private static readonly ConcurrentDictionary<string, CachePayload> _globalCache =
            new ConcurrentDictionary<string, CachePayload>(StringComparer.OrdinalIgnoreCase);

        private string _lastPublishedKey = "";

        public override void RemovedFromDocument(GH_Document document)
        {
            if (!string.IsNullOrEmpty(_lastPublishedKey))
            {
                PillHub.Unpublish(_lastPublishedKey, InstanceGuid);
                _lastPublishedKey = "";
            }
            base.RemovedFromDocument(document);
        }

        public PillCache_Component()
            : base(
                "Pill Compute Cache",
                "PillCache",
                "Bypass de recomputacao pesada. Armazena o resultado de operacoes caras (acustica, raytracing, malhas) em memoria associado ao hash dos parametros de entrada. Se as entradas nao mudarem, devolve o cache instantaneamente sem recomputar.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("a1100006-e1ef-4000-8000-000000000006");
        protected override Bitmap Icon => GlauxToolsIcons.PillCache;

        public override void CreateAttributes()
        {
            m_attributes = new Pill_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Key", "K", "Identificador unico da tarefa/calculo em cache. O prefixo define o grupo automaticamente.", GH_ParamAccess.item);
            pManager.AddGenericParameter("InputParams", "P", "Parametros ou geometrias de entrada. Se qualquer valor mudar, o cache e invalidado automaticamente.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("ComputedData", "D", "Resultado pesado da computacao a ser memorizado.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("ForceRecalc", "F", "Forca nova computacao e limpa o cache atual desta chave.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Bypass", "BY", "Se True, desativa o cache e sempre passa o dado direto.", GH_ParamAccess.item, false);
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Dados resgatados do cache ou recem-computados (imutabilidade garantida).", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("IsCached", "C", "True se o resultado foi retornado da memoria cache sem gasto de processamento.", GH_ParamAccess.item);
            pManager.AddTextParameter("ComputeTime", "TS", "Horario em que o calculo original foi computado.", GH_ParamAccess.item);
            pManager.AddTextParameter("Stats", "S", "Estatisticas do cache: contagem de hits, hash e diagnostico.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string rawKey = "";
            if (!DA.GetData(0, ref rawKey) || string.IsNullOrWhiteSpace(rawKey))
            {
                if (!string.IsNullOrEmpty(_lastPublishedKey))
                {
                    PillHub.Unpublish(_lastPublishedKey, InstanceGuid);
                    _lastPublishedKey = "";
                }

                CurrentCleanKey = "Sem Chave";
                CurrentCategory = "CACHE";
                CurrentUnit = "";
                CurrentCategoryColor = Color.FromArgb(157, 78, 221);
                IsCached = false;
                IsBypassed = false;
                CacheStatusShort = "Sem Chave";
                Message = "Informe Chave";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Informe uma chave de identificacao para o cache.");
                return;
            }

            rawKey = rawKey.Trim();
            PillHub.ParseKeyMetadata(rawKey, null, out string cleanKey, out string category, out string unit, out Color catColor);
            
            CurrentCleanKey = cleanKey;
            CurrentCategory = string.IsNullOrEmpty(category) || category.Equals("GEN", StringComparison.OrdinalIgnoreCase) ? "CACHE" : category;
            CurrentUnit = unit;
            CurrentCategoryColor = CurrentCategory == "CACHE" ? Color.FromArgb(157, 78, 221) : catColor;

            if (!string.IsNullOrEmpty(_lastPublishedKey) && !string.Equals(_lastPublishedKey, cleanKey, StringComparison.OrdinalIgnoreCase))
            {
                PillHub.Unpublish(_lastPublishedKey, InstanceGuid);
            }
            _lastPublishedKey = cleanKey;
            string key = cleanKey;

            if (!DA.GetDataTree(1, out GH_Structure<IGH_Goo> inputParamsTree))
            {
                inputParamsTree = new GH_Structure<IGH_Goo>();
            }

            if (!DA.GetDataTree(2, out GH_Structure<IGH_Goo> computedDataTree))
            {
                computedDataTree = new GH_Structure<IGH_Goo>();
            }

            bool forceRecalc = false;
            DA.GetData(3, ref forceRecalc);

            bool bypass = false;
            DA.GetData(4, ref bypass);
            IsBypassed = bypass;

            if (bypass)
            {
                IsCached = false;
                CacheStatusShort = "Bypass";
                var cloneBypass = PillHub.CloneStructure(computedDataTree);
                DA.SetDataTree(0, cloneBypass);
                DA.SetData(1, false);
                DA.SetData(2, DateTime.Now.ToString("HH:mm:ss"));
                DA.SetData(3, "Bypass Ativo (Cache ignorado)");
                Message = $"[{CurrentCategory}] Bypass";

                Guid docId = OnPingDocument()?.DocumentID ?? Guid.Empty;
                PillHub.Publish(rawKey, cloneBypass, InstanceGuid, docId, CurrentUnit);
                return;
            }

            string currentHash = ComputeTreeHash(inputParamsTree);
            GH_Structure<IGH_Goo> outTree = null;

            if (!forceRecalc && _globalCache.TryGetValue(key, out var payload) && payload.InputHash == currentHash)
            {
                payload.HitCount++;
                IsCached = true;
                CacheStatusShort = $"HIT ({payload.HitCount}x)";

                outTree = PillHub.CloneStructure(payload.Data);
                DA.SetDataTree(0, outTree);
                DA.SetData(1, true);
                DA.SetData(2, payload.ComputeTime.ToString("HH:mm:ss"));
                string inSummary = inputParamsTree.DataCount > 0 ? PillDataFingerprint.GetReadableSummary(inputParamsTree.get_FirstItem(true)) : "Vazio";
                DA.SetData(3, $"CACHE HIT ({payload.HitCount}x) | Hash: {payload.InputHash.Substring(0, 8)}... | Entrada: {inSummary} | Computado em: {payload.ComputeTime:HH:mm:ss}");

                Message = $"[{CurrentCategory}] HIT ({payload.HitCount}x)";
            }
            else
            {
                var newPayload = new CachePayload
                {
                    InputHash = currentHash,
                    Data = PillHub.CloneStructure(computedDataTree),
                    ComputeTime = DateTime.Now,
                    HitCount = 0
                };
                _globalCache[key] = newPayload;

                IsCached = false;
                CacheStatusShort = "Recalculado";

                outTree = PillHub.CloneStructure(newPayload.Data);
                DA.SetDataTree(0, outTree);
                DA.SetData(1, false);
                DA.SetData(2, newPayload.ComputeTime.ToString("HH:mm:ss"));
                string inSummary = inputParamsTree.DataCount > 0 ? PillDataFingerprint.GetReadableSummary(inputParamsTree.get_FirstItem(true)) : "Vazio";
                DA.SetData(3, $"CACHE RECALCULADO | Hash: {currentHash.Substring(0, 8)}... | Entrada: {inSummary} | Itens: {newPayload.Data.DataCount}");

                Message = $"[{CurrentCategory}] Recalculado";
            }

            Guid currentDocId = OnPingDocument()?.DocumentID ?? Guid.Empty;
            PillHub.Publish(rawKey, outTree, InstanceGuid, currentDocId, CurrentUnit);
        }

        private static string ComputeTreeHash(GH_Structure<IGH_Goo> tree)
        {
            return PillDataFingerprint.ComputeTreeHash(tree);
        }

        public int ConnectMyReceiversWithHiddenWires(GH_Document doc = null)
        {
            if (doc == null) doc = OnPingDocument();
            if (doc == null || string.IsNullOrWhiteSpace(CurrentCleanKey)) return 0;

            int connectedCount = 0;
            foreach (var obj in doc.Objects)
            {
                if (obj is PillReceiver_Component rx &&
                    string.Equals(rx.CurrentCleanKey, CurrentCleanKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (rx.AutoConnectHiddenWire(doc, CurrentCleanKey)) connectedCount++;
                }
                else if (obj is PillHook_Component hook &&
                    string.Equals(hook.CurrentCleanKey, CurrentCleanKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (hook.AutoConnectHiddenWire(doc, CurrentCleanKey)) connectedCount++;
                }
            }
            return connectedCount;
        }

        public int DisconnectMyReceivers(GH_Document doc = null)
        {
            if (doc == null) doc = OnPingDocument();
            if (doc == null || string.IsNullOrWhiteSpace(CurrentCleanKey)) return 0;

            int disconnectedCount = 0;
            foreach (var obj in doc.Objects)
            {
                if (obj is PillReceiver_Component rx &&
                    string.Equals(rx.CurrentCleanKey, CurrentCleanKey, StringComparison.OrdinalIgnoreCase))
                {
                    rx.DisconnectHiddenWire();
                    disconnectedCount++;
                }
                else if (obj is PillHook_Component hook &&
                    string.Equals(hook.CurrentCleanKey, CurrentCleanKey, StringComparison.OrdinalIgnoreCase))
                {
                    hook.DisconnectHiddenWire();
                    disconnectedCount++;
                }
            }
            return disconnectedCount;
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var clearItem = new ToolStripMenuItem("Limpar Cache Global de Memoria");
            clearItem.Click += (s, e) =>
            {
                _globalCache.Clear();
                ExpireSolution(true);
            };
            menu.Items.Add(clearItem);

            menu.Items.Add(new ToolStripSeparator());
            var hiddenMenu = new ToolStripMenuItem("⚡ Conexao Oculta (Modo Wallacei / Hidden Wire)")
            {
                ToolTipText = "Conecta/desconecta cabos fisicos ocultos (Hidden Wire) preservando a sincronia DAG sequencial para Wallacei e Galapagos."
            };

            var connectMyItem = new ToolStripMenuItem($"Conectar meus Receptores de '{CurrentCleanKey}' com Cabo Oculto");
            connectMyItem.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc == null) return;
                int count = ConnectMyReceiversWithHiddenWires(doc);
                doc.NewSolution(false);
                MessageBox.Show($"{count} receptor(es) conectado(s) com cabo oculto (hidden wire) ao canal '{CurrentCleanKey}'.",
                    "Pill Cache - Conexao Oculta", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            hiddenMenu.DropDownItems.Add(connectMyItem);

            var disconnectMyItem = new ToolStripMenuItem($"Desconectar meus Receptores de '{CurrentCleanKey}'");
            disconnectMyItem.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc == null) return;
                int count = DisconnectMyReceivers(doc);
                doc.NewSolution(false);
                MessageBox.Show($"{count} receptor(es) desconectado(s) de '{CurrentCleanKey}'.",
                    "Pill Cache - Modo Sem Fio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            hiddenMenu.DropDownItems.Add(disconnectMyItem);

            hiddenMenu.DropDownItems.Add(new ToolStripSeparator());

            var connectAllItem = new ToolStripMenuItem("⚡ CONECTAR TODOS os Pills do Canvas com Cabos Ocultos (Recomendado para Wallacei)");
            connectAllItem.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc == null) return;
                int c = PillHub.ConnectAllDocumentPillsHidden(doc);
                doc.NewSolution(false);
                MessageBox.Show($"Todos os {c} receptores do Canvas foram conectados com cabos ocultos (hidden wire)!\nA sequencia DAG esta garantida para Wallacei e Galapagos.",
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
                MessageBox.Show($"Todos os {c} receptores do Canvas voltaram ao modo 100% sem fio em memoria.",
                    "Pill System - Modo Sem Fio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            hiddenMenu.DropDownItems.Add(disconnectAllItem);

            menu.Items.Add(hiddenMenu);
        }
    }
}
