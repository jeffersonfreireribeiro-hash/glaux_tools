using System.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System.Windows.Forms;

namespace Buraqueira_Tools
{
    public class PillBundlePack_Component : GH_Component
    {
        private readonly HashSet<string> _subscribedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public PillBundlePack_Component()
            : base(
                "Pill Bundle Pack (Parameter Hub)",
                "PillPack",
                "Empacota múltiplos parâmetros, listas ou árvores nomeadas em um único pacote estruturado (PillBundle / Hub Central). Permite puxar automaticamente canais por nome de grupo/categoria (ex: 'ACU', 'GEO', 'ALL') ou empacotar valores manuais. Reduz a fiação complexa a uma única linha de transmissão e suporta exportação JSON.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("a1100003-e1ef-4000-8000-000000000003");
        protected override Bitmap Icon => GlauxToolsIcons.PillBundlePack;

        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);
            foreach (var k in _subscribedKeys)
            {
                PillHub.UnsubscribeReceiver(k, InstanceGuid);
            }
            _subscribedKeys.Clear();
        }

        public int AutoConnectHiddenWires(GH_Document doc = null)
        {
            if (doc == null) doc = OnPingDocument();
            if (doc == null || Params.Input.Count < 4) return 0;

            var inWires = Params.Input[3];
            int connectedCount = 0;

            var keys = new List<string>();
            if (Params.Input[0].SourceCount > 0)
            {
                foreach (var src in Params.Input[0].Sources)
                {
                    foreach (var item in src.VolatileData.AllData(true))
                    {
                        if (item != null) keys.Add(item.ToString());
                    }
                }
            }

            if (keys.Count == 0) return 0;

            foreach (var key in keys)
            {
                var channels = PillHub.GetChannelsByGroupOrKey(key);
                foreach (var ch in channels)
                {
                    string cKey = ch.CleanKey;
                    foreach (var obj in doc.Objects)
                    {
                        if (obj is PillTransmitter_Component tx &&
                            (obj.InstanceGuid == ch.SourceComponentGuid ||
                             string.Equals(tx.CurrentCleanKey, cKey, StringComparison.OrdinalIgnoreCase) ||
                             (!string.IsNullOrEmpty(ch.SourceNickName) && string.Equals(tx.NickName, ch.SourceNickName, StringComparison.OrdinalIgnoreCase)) ||
                             string.Equals(tx.CurrentCategory, key, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (tx.Params.Output.Count > 0 && !inWires.Sources.Contains(tx.Params.Output[0]))
                            {
                                inWires.AddSource(tx.Params.Output[0]);
                                inWires.WireDisplay = GH_ParamWireDisplay.hidden;
                                connectedCount++;
                            }
                        }
                    }
                }
            }

            return connectedCount;
        }

        public void DisconnectHiddenWires()
        {
            if (Params.Input.Count > 3)
            {
                RecordUndoEvent("Desconectar Cabos Ocultos Bundle");
                Params.Input[3].RemoveAllSources();
            }
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var wireMenu = new ToolStripMenuItem("⚡ Conexão Oculta (Modo Wallacei / Hidden Wire)");
            bool isWireConnected = Params.Input.Count > 3 && Params.Input[3].SourceCount > 0;
            var statusItem = new ToolStripMenuItem(isWireConnected ? $"Status: Conectado ({Params.Input[3].SourceCount} cabos ocultos ⚡)" : "Status: Modo Sem Fio em Memória") { Enabled = false };
            wireMenu.DropDownItems.Add(statusItem);
            wireMenu.DropDownItems.Add(new ToolStripSeparator());

            var connectBtn = new ToolStripMenuItem("➔ Conectar Cabos Ocultos aos Transmitters deste Pacote")
            {
                Enabled = !isWireConnected
            };
            connectBtn.Click += (s, e) =>
            {
                int c = AutoConnectHiddenWires(OnPingDocument());
                MessageBox.Show($"Conectados {c} cabos físicos invisíveis (Wire Display -> Hidden) aos Transmitters deste pacote.", "Pill Hidden Wire", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ExpireSolution(true);
            };
            wireMenu.DropDownItems.Add(connectBtn);

            var disconnectBtn = new ToolStripMenuItem("✕ Desconectar Cabos Ocultos deste Pacote")
            {
                Enabled = isWireConnected
            };
            disconnectBtn.Click += (s, e) =>
            {
                DisconnectHiddenWires();
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

            var refreshItem = new ToolStripMenuItem("Recarregar do PillHub / Sincronizar Pacote");
            refreshItem.Click += (s, e) =>
            {
                ExpireSolution(true);
            };
            menu.Items.Add(refreshItem);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Keys", "K", "Lista de chaves de parâmetros (ex: 'ACU_T60_Alvo [s]') OU nomes de grupos/categorias (ex: 'ACU', 'GEO', 'MAT', 'ALL'). Se for um grupo, todas as variáveis ativas dessa categoria são automaticamente colhidas do barramento PillHub.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Values", "V", "Valores manuais OPCIONAIS. DEIXE DESCONECTADO para colher automaticamente dos Transmitters do Canvas (via Wires ⚡ e PillHub). Conecte aqui apenas se quiser forçar valores manuais sem transmissores.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Namespace", "NS", "Namespace ou prefixo de escopo opcional (ex: 'SALA_01' ou 'CONFIG').", GH_ParamAccess.item, "GLOBAL");
            pManager.AddGenericParameter("Wires", "⚡", "Cabos físicos ocultos automáticos (Wire Display: Hidden) dos Transmitters correspondentes para sincronização no Wallacei.", GH_ParamAccess.tree);
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[3].WireDisplay = GH_ParamWireDisplay.hidden;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Bundle", "B", "Objeto PillBundle estruturado contendo todos os parâmetros empacotados.", GH_ParamAccess.item);
            pManager.AddTextParameter("JSON", "J", "String JSON serializada pronta para exportar para arquivo ou transmitir entre definições.", GH_ParamAccess.item);
            pManager.AddTextParameter("Summary", "S", "Resumo detalhado dos parâmetros empacotados.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            try
            {
                var keys = new List<string>();
                DA.GetDataList(0, keys);

                GH_Structure<IGH_Goo> valuesTree = null;
                bool hasValues = DA.GetDataTree(1, out valuesTree) && valuesTree != null && valuesTree.DataCount > 0;

                string ns = "GLOBAL";
                DA.GetData(2, ref ns);
                if (string.IsNullOrWhiteSpace(ns)) ns = "GLOBAL";

                var inWires = Params.Input.Count > 3 ? Params.Input[3] : null;
                bool hasWires = inWires != null && inWires.SourceCount > 0;

                if (keys.Count == 0 && !hasWires)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Forneça pelo menos uma chave ou conecte cabos na entrada Wires ⚡.");
                    return;
                }

                // Limpa inscrições anteriores para renovar com os canais atuais
                foreach (var k in _subscribedKeys)
                {
                    PillHub.UnsubscribeReceiver(k, InstanceGuid);
                }
                _subscribedKeys.Clear();

                var bundle = new PillBundle
                {
                    Namespace = ns.Trim(),
                    Timestamp = DateTime.Now
                };

                var sbSummary = new StringBuilder();
                sbSummary.AppendLine($"=== PILL BUNDLE [{bundle.Namespace}] ===");
                sbSummary.AppendLine($"Data/Hora: {bundle.Timestamp:yyyy-MM-dd HH:mm:ss}");

                int pulledFromHubCount = 0;
                int explicitPackedCount = 0;
                int wiresPackedCount = 0;

                var packedKeysSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // =========================================================================
                // 1. PRIORIDADE FÍSICA: EMPACOTAMENTO DIRETO DE TODAS AS FONTES EM WIRES ⚡
                // =========================================================================
                if (hasWires)
                {
                    for (int k = 0; k < inWires.Sources.Count; k++)
                    {
                        var src = inWires.Sources[k];
                        if (src == null) continue;

                        var docObj = src.Attributes?.GetTopLevel?.DocObject;
                        PillTransmitter_Component tx = docObj as PillTransmitter_Component;

                        string itemKey = "";
                        string unit = "";
                        string category = "";

                        if (tx != null)
                        {
                            unit = tx.CurrentUnit ?? "";
                            category = tx.CurrentCategory ?? "";
                            if (!string.IsNullOrWhiteSpace(tx.CurrentCleanKey) &&
                                !tx.CurrentCleanKey.Equals("GEN", StringComparison.OrdinalIgnoreCase) &&
                                !tx.CurrentCleanKey.Equals("Pass", StringComparison.OrdinalIgnoreCase) &&
                                !tx.CurrentCleanKey.Equals("D", StringComparison.OrdinalIgnoreCase))
                            {
                                itemKey = tx.CurrentCleanKey;
                            }
                        }

                        // Verifica se o componente tem NickName descritivo atribuído pelo usuário
                        if (docObj != null && !string.IsNullOrWhiteSpace(docObj.NickName) &&
                            !docObj.NickName.Equals("PillTx", StringComparison.OrdinalIgnoreCase) &&
                            !docObj.NickName.Equals("Pill Transmitter", StringComparison.OrdinalIgnoreCase) &&
                            !docObj.NickName.Equals("Transmitter", StringComparison.OrdinalIgnoreCase) &&
                            !docObj.NickName.Equals("Tx", StringComparison.OrdinalIgnoreCase))
                        {
                            // Se a chave for vazia, genérica ou apenas o nome de categoria (ex: "SRF"), usa o NickName
                            if (string.IsNullOrWhiteSpace(itemKey) || 
                                itemKey.Equals(category, StringComparison.OrdinalIgnoreCase) ||
                                (keys.Count > 0 && itemKey.Equals(keys[0].Trim(), StringComparison.OrdinalIgnoreCase)))
                            {
                                itemKey = docObj.NickName;
                            }
                            else if (!itemKey.Contains(docObj.NickName))
                            {
                                itemKey = $"{itemKey}_{docObj.NickName}";
                            }
                        }

                        // Se ainda não temos uma chave descritiva, tenta o NickName do parâmetro de saída
                        if (string.IsNullOrWhiteSpace(itemKey) || itemKey.Equals("Pass", StringComparison.OrdinalIgnoreCase) || itemKey.Equals("D", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrWhiteSpace(src.NickName) && 
                                !src.NickName.Equals("Pass", StringComparison.OrdinalIgnoreCase) && 
                                !src.NickName.Equals("D", StringComparison.OrdinalIgnoreCase) &&
                                !src.NickName.Equals("⚡", StringComparison.OrdinalIgnoreCase))
                            {
                                itemKey = src.NickName;
                            }
                            else if (docObj != null && !string.IsNullOrWhiteSpace(docObj.NickName))
                            {
                                itemKey = docObj.NickName;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(itemKey))
                        {
                            itemKey = $"Param_{k + 1}";
                        }

                        itemKey = PillHub.CleanUpKey(itemKey);

                        // Extrai os dados do cabo físico com segurança
                        object valObj = null;
                        var vData = src.VolatileData;
                        if (vData != null && vData.DataCount > 0)
                        {
                            if (vData.DataCount == 1)
                            {
                                var first = vData.AllData(true).FirstOrDefault();
                                valObj = first != null ? first.SafeScriptVariable() : null;
                            }
                            else
                            {
                                valObj = vData.AllData(true).Select(g => g?.SafeScriptVariable()).ToList();
                            }
                        }
                        else if (tx != null && !string.IsNullOrEmpty(tx.CurrentCleanKey) && PillHub.TryGetChannel(tx.CurrentCleanKey, out var ch))
                        {
                            if (ch.Data != null && ch.Data.DataCount > 0)
                            {
                                if (ch.Data.DataCount == 1)
                                    valObj = ch.Data.AllData(true).FirstOrDefault()?.SafeScriptVariable();
                                else
                                    valObj = ch.Data.AllData(true).Select(g => g?.SafeScriptVariable()).ToList();
                            }
                        }

                        // Escopo com namespace
                        string scopedKey = string.IsNullOrEmpty(bundle.Namespace) || bundle.Namespace.Equals("GLOBAL", StringComparison.OrdinalIgnoreCase)
                            ? itemKey
                            : $"{bundle.Namespace}::{itemKey}";

                        // Desempate de chaves duplicadas para garantir que TODAS as 17 fontes sejam preservadas
                        string uniqueScopedKey = scopedKey;
                        int suffix = 2;
                        while (bundle.Entries.ContainsKey(uniqueScopedKey))
                        {
                            uniqueScopedKey = $"{scopedKey}_{suffix++}";
                        }
                        scopedKey = uniqueScopedKey;

                        bundle.Entries[scopedKey] = valObj;
                        packedKeysSet.Add(itemKey);
                        packedKeysSet.Add(scopedKey);

                        if (!string.IsNullOrEmpty(unit))
                        {
                            bundle.Units[scopedKey] = unit;
                        }

                        if (tx != null && !string.IsNullOrEmpty(tx.CurrentCleanKey))
                        {
                            PillHub.SubscribeReceiver(tx.CurrentCleanKey, InstanceGuid);
                            _subscribedKeys.Add(tx.CurrentCleanKey);
                        }

                        wiresPackedCount++;
                        string unitStr = string.IsNullOrEmpty(unit) ? "" : $" [{unit}]";
                        string valStr = valObj is System.Collections.IList lst ? $"{lst.Count} itens" : (valObj?.ToString() ?? "null");
                        sbSummary.AppendLine($"• {scopedKey}{unitStr} (via Wires ⚡ #{k + 1}) = {valStr}");
                    }
                }

                // =========================================================================
                // 2. EMPACOTAMENTO VIA KEYS / VALUES OU BUSCA NO HUB SEM FIO
                // =========================================================================
                for (int i = 0; i < keys.Count; i++)
                {
                    string rawKey = keys[i];
                    if (string.IsNullOrWhiteSpace(rawKey)) continue;

                    string trimmedKey = rawKey.Trim();
                    string cleanK = PillHub.CleanUpKey(trimmedKey);
                    bool hasParamSep = cleanK.Contains("_") || cleanK.Contains("::") || cleanK.Contains(":");
                    bool isWildcard = trimmedKey == "*" || trimmedKey.Equals("ALL", StringComparison.OrdinalIgnoreCase) || trimmedKey.Equals("TODOS", StringComparison.OrdinalIgnoreCase);
                    bool isGroupOrWildcard = isWildcard || !hasParamSep || cleanK.EndsWith("_") || cleanK.EndsWith("::");

                    // Inscrição preventiva
                    if (isGroupOrWildcard)
                    {
                        string groupName = cleanK.TrimEnd('_', ':', '/');
                        if (string.IsNullOrEmpty(groupName)) groupName = "*";

                        PillHub.SubscribeReceiver(groupName, InstanceGuid);
                        _subscribedKeys.Add(groupName);

                        string normCat = PillHub.NormalizeCategory(groupName);
                        if (!string.IsNullOrEmpty(normCat) && !normCat.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                        {
                            PillHub.SubscribeReceiver(normCat, InstanceGuid);
                            _subscribedKeys.Add(normCat);
                        }
                    }
                    else
                    {
                        PillHub.SubscribeReceiver(cleanK, InstanceGuid);
                        _subscribedKeys.Add(cleanK);
                    }

                    // Se há valores explícitos conectados em Values (V)
                    bool hasExplicitValue = false;
                    List<object> explicitValList = null;

                    if (hasValues)
                    {
                        var targetPath = new GH_Path(i);
                        if (valuesTree.PathExists(targetPath))
                        {
                            var branch = valuesTree[targetPath];
                            if (branch != null && branch.Count > 0)
                            {
                                explicitValList = branch.Where(g => g != null).Select(g => g.SafeScriptVariable()).ToList();
                                hasExplicitValue = true;
                            }
                        }
                        else if (i < valuesTree.Branches.Count)
                        {
                            var branch = valuesTree.Branches[i];
                            if (branch != null && branch.Count > 0)
                            {
                                explicitValList = branch.Where(g => g != null).Select(g => g.SafeScriptVariable()).ToList();
                                hasExplicitValue = true;
                            }
                        }
                        else if (keys.Count == 1 && valuesTree.Branches.Count == 1)
                        {
                            var branch = valuesTree.Branches[0];
                            if (branch != null && branch.Count > 0)
                            {
                                explicitValList = branch.Where(g => g != null).Select(g => g.SafeScriptVariable()).ToList();
                                hasExplicitValue = true;
                            }
                        }
                        else
                        {
                            var allItems = valuesTree.AllData(true).ToList();
                            if (i < allItems.Count && allItems[i] != null)
                            {
                                explicitValList = new List<object> { allItems[i].SafeScriptVariable() };
                                hasExplicitValue = true;
                            }
                        }
                    }

                    if (hasExplicitValue)
                    {
                        PillHub.ParseKeyMetadata(rawKey, null, out string cleanKey, out _, out string unit, out _);
                        string scopedKey = string.IsNullOrEmpty(bundle.Namespace) || bundle.Namespace.Equals("GLOBAL", StringComparison.OrdinalIgnoreCase)
                            ? cleanKey
                            : $"{bundle.Namespace}::{cleanKey}";

                        if (!string.IsNullOrEmpty(unit))
                        {
                            bundle.Units[scopedKey] = unit;
                        }

                        if (explicitValList.Count == 1)
                            bundle.Entries[scopedKey] = explicitValList[0];
                        else
                            bundle.Entries[scopedKey] = explicitValList;

                        packedKeysSet.Add(cleanKey);
                        packedKeysSet.Add(scopedKey);
                        explicitPackedCount++;

                        string unitStr = string.IsNullOrEmpty(unit) ? "" : $" [{unit}]";
                        string valStr = explicitValList.Count == 1 ? (explicitValList[0]?.ToString() ?? "null") : $"{explicitValList.Count} itens";
                        sbSummary.AppendLine($"• {scopedKey}{unitStr} (Manual) = {valStr}");
                        continue;
                    }

                    // Se não há valores explícitos, busca no barramento PillHub
                    var matchingChannels = PillHub.GetChannelsByGroupOrKey(rawKey);

                    if (matchingChannels.Count > 0)
                    {
                        foreach (var ch in matchingChannels)
                        {
                            PillHub.SubscribeReceiver(ch.CleanKey, InstanceGuid);
                            _subscribedKeys.Add(ch.CleanKey);

                            string itemKey = ch.CleanKey;
                            if (!string.IsNullOrWhiteSpace(ch.SourceNickName) &&
                                !ch.SourceNickName.Equals("PillTx", StringComparison.OrdinalIgnoreCase) &&
                                !ch.SourceNickName.Equals("Transmitter", StringComparison.OrdinalIgnoreCase) &&
                                !ch.SourceNickName.Equals(itemKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (itemKey.Equals("GEN", StringComparison.OrdinalIgnoreCase) || itemKey.Equals(ch.Category, StringComparison.OrdinalIgnoreCase))
                                {
                                    itemKey = ch.SourceNickName;
                                }
                                else if (!itemKey.Contains(ch.SourceNickName))
                                {
                                    itemKey = $"{itemKey}_{ch.SourceNickName}";
                                }
                            }

                            string scopedKey = string.IsNullOrEmpty(bundle.Namespace) || bundle.Namespace.Equals("GLOBAL", StringComparison.OrdinalIgnoreCase)
                                ? itemKey
                                : $"{bundle.Namespace}::{itemKey}";

                            // Se já foi empacotado pelos Wires, não duplica
                            if (packedKeysSet.Contains(itemKey) || packedKeysSet.Contains(scopedKey) || bundle.Entries.ContainsKey(scopedKey))
                            {
                                continue;
                            }

                            // Desempate
                            string uniqueKey = scopedKey;
                            int suffix = 2;
                            while (bundle.Entries.ContainsKey(uniqueKey))
                            {
                                uniqueKey = $"{scopedKey}_{suffix++}";
                            }
                            scopedKey = uniqueKey;

                            // Extração de dados
                            object valObj = null;
                            if (ch.Data != null && ch.Data.DataCount > 0)
                            {
                                if (ch.Data.DataCount == 1)
                                    valObj = ch.Data.AllData(true).FirstOrDefault()?.SafeScriptVariable();
                                else
                                    valObj = ch.Data.AllData(true).Select(g => g?.SafeScriptVariable()).ToList();
                            }

                            bundle.Entries[scopedKey] = valObj;
                            packedKeysSet.Add(itemKey);
                            packedKeysSet.Add(scopedKey);

                            if (!string.IsNullOrEmpty(ch.Unit))
                            {
                                bundle.Units[scopedKey] = ch.Unit;
                            }

                            pulledFromHubCount++;
                            string unitStr = string.IsNullOrEmpty(ch.Unit) ? "" : $" [{ch.Unit}]";
                            string valStr = valObj is System.Collections.IList lst ? $"{lst.Count} itens" : (valObj?.ToString() ?? "null");
                            sbSummary.AppendLine($"• {scopedKey}{unitStr} (Hub: {ch.Category}) = {valStr}");
                        }
                    }
                    else if (!hasWires)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Nenhum canal ativo encontrado no PillHub para a chave/grupo '{rawKey}'.");
                        sbSummary.AppendLine($"• [{rawKey}]: Nenhum canal ativo encontrado no PillHub.");
                    }
                }

                // =========================================================================
                // 3. CÁLCULO DE TOTAIS E EXIBIÇÃO CLARA NO COMPONENTE
                // =========================================================================
                int totalItemsCount = 0;
                foreach (var kvp in bundle.Entries)
                {
                    if (kvp.Value is System.Collections.IList list)
                        totalItemsCount += list.Count;
                    else if (kvp.Value != null)
                        totalItemsCount += 1;
                }

                sbSummary.AppendLine("-----------------------------------------");
                sbSummary.AppendLine($"Total no Pacote: {bundle.Entries.Count} parâmetros ({totalItemsCount} itens no total)");
                if (wiresPackedCount > 0) sbSummary.AppendLine($"  - Coletados via Wires ⚡: {wiresPackedCount}");
                if (pulledFromHubCount > 0) sbSummary.AppendLine($"  - Coletados do PillHub: {pulledFromHubCount}");
                if (explicitPackedCount > 0) sbSummary.AppendLine($"  - Empacotados manualmente: {explicitPackedCount}");

                string json = bundle.ToJson();

                DA.SetData(0, new GH_PillBundleGoo(bundle));
                DA.SetData(1, json);
                DA.SetData(2, sbSummary.ToString());

                Message = $"{bundle.Entries.Count} Params ({totalItemsCount} it)";
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Erro no Pill Bundle Pack: {ex.Message}");
            }
        }
    }
}
