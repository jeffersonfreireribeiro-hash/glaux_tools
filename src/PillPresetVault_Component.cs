// PillPresetVault_Component.cs
// Cofre de Variantes / Snapshots para o ecossistema de Pilulas do BURAQUEIRA Tools.
// Grava o estado completo de multiplos parametros, materiais e do Hub em slots de memoria.
// Alterna opcoes com 1 clique, dispara atualizacao no canvas e gerencia variantes por duplo clique.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    [Serializable]
    public class PillVaultVariable
    {
        public Guid ParamGuid { get; set; }
        public string ObjectName { get; set; }
        public string ObjectType { get; set; } // "Slider", "Toggle", "Panel", "ValueList", "Bundle", "Param"
        public double? NumberValue { get; set; }
        public bool? BoolValue { get; set; }
        public string TextValue { get; set; }
        public string BundleJson { get; set; } // Se for um PillBundle completo
        public string SerializedXml { get; set; } // Estado completo serializado de componentes/pilhas
        public List<string> SerializedValues { get; set; } = new List<string>();
    }

    [Serializable]
    public class PillVaultVariant
    {
        public string Name { get; set; } = "Variante";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public List<PillVaultVariable> Variables { get; set; } = new List<PillVaultVariable>();

        public string GetSummary()
        {
            var parts = new List<string>();
            foreach (var v in Variables)
            {
                if (!string.IsNullOrEmpty(v.BundleJson)) parts.Add($"{v.ObjectName} [Bundle]");
                else if (v.ObjectType == "Component") parts.Add($"{v.ObjectName} [Pilha]");
                else if (v.NumberValue.HasValue) parts.Add($"{v.ObjectName}={v.NumberValue.Value:F2}");
                else if (v.BoolValue.HasValue) parts.Add($"{v.ObjectName}={v.BoolValue.Value}");
                else if (!string.IsNullOrEmpty(v.TextValue)) parts.Add($"{v.ObjectName}={v.TextValue}");
                else parts.Add(v.ObjectName);
            }
            return string.Join("; ", parts);
        }
    }

    public class PillPresetVault_Component : GH_Component
    {
        public List<PillVaultVariant> Presets { get; set; } = new List<PillVaultVariant>();
        public int ActivePresetIndex { get; set; } = -1;
        public List<Guid> TargetGuids { get; set; } = new List<Guid>();
        public bool SuppressDefaultWires { get; set; } = true;

        public PillPresetVault_Component()
            : base(
                "Pill Preset Vault",
                "PillVault",
                "Cofre de Variantes (Preset Vault) para o ecossistema de Pilulas e Parametros do BURAQUEIRA Tools.\n" +
                "- Grava o estado atual de multiplas variaveis, materiais e do Hub em slots de memoria.\n" +
                "- Botoes: [ + Gravar Variante ] (com campo de nome) e seletor [<] [Opcao] [>] para carregar e sobrescrever o barramento.\n" +
                "- So grava quando clica para adicionar um estado novo (sem gravacao automatica indevida).\n" +
                "- Duplo clique: abre o gerenciador de log com os salvamentos; permite excluir linhas selecionadas e impede escrita manual.\n" +
                "- Cabos estilo Galapagos conectando o cofre a todos os controles monitorados com moldura [VAULT].\n" +
                "- Salva e persiste todas as variantes no proprio arquivo .gh.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("C9B2D3E4-F5A6-7B8C-9D0E-1F2A3B4C5D6E");

        protected override Bitmap Icon => GlauxToolsIcons.PillPresetVault;

        public override void CreateAttributes()
        {
            m_attributes = new PillPresetVaultAttributes(this);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            HideDefaultWires();
        }

        public void HideDefaultWires()
        {
            if (Params?.Input != null && Params.Input.Count > 0 && Params.Input[0] != null)
            {
                Params.Input[0].WireDisplay = GH_ParamWireDisplay.hidden;
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0: Parametros, Materiais ou Hub a Monitorar
            pManager.AddGenericParameter(
                "Hub / Parameters", "Hub",
                "Conecte aqui o PillBundle, ou multiplos Sliders, Toggles, Paineis ou Parametros que deseja gravar nas variantes.",
                GH_ParamAccess.list);
            pManager[0].Optional = true;

            // 1: Nome da Variante (Opcional, para salvar via fio)
            pManager.AddTextParameter(
                "Variant Name", "Name",
                "Nome para a nova variante a ser gravada (opcional, pode usar o botao [ + Gravar ] no componente).",
                GH_ParamAccess.item,
                "Opcao_A");
            pManager[1].Optional = true;

            // 2: Disparador de Gravacao (Opcional, para salvar via botao)
            pManager.AddBooleanParameter(
                "Trigger Save", "Save",
                "Pulso (True) para gravar o estado atual como uma nova variante.",
                GH_ParamAccess.item,
                false);
            pManager[2].Optional = true;

            // 3: Selecionar Variante (Opcional, indice ou nome)
            pManager.AddGenericParameter(
                "Select Variant", "Sel",
                "Indice (0, 1, 2...) ou Nome da variante para restaurar via fio.",
                GH_ParamAccess.item);
            pManager[3].Optional = true;
            HideDefaultWires();
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Active Bundle", "Bundle", "PillBundle do cenario ativo pronto para conectar em receptores.", GH_ParamAccess.item);
            pManager.AddTextParameter("Active Variant", "Active", "Nome da variante atualmente ativa no cofre.", GH_ParamAccess.item);
            pManager.AddTextParameter("All Variants", "All", "Lista com o nome de todas as variantes gravadas.", GH_ParamAccess.list);
            pManager.AddTextParameter("Summary", "Summ", "Resumo dos valores gravados na variante ativa.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Total Saved", "Count", "Quantidade total de variantes salvas no cofre.", GH_ParamAccess.item);
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Menu_AppendSeparator(menu);

                        string curName = (ActivePresetIndex >= 0 && ActivePresetIndex < Presets.Count) ? Presets[ActivePresetIndex].Name : null;
            if (!string.IsNullOrEmpty(curName))
            {
                Menu_AppendItem(menu, $"Sobrescrever / Atualizar '{curName}' com Estado Atual", (s, e) =>
                {
                    RecordUndoEvent("Atualizar Variante");
                    CaptureCurrentState(curName);
                    ExpireSolution(true);
                });
            }
            Menu_AppendItem(menu, "Gravar Nova Variante...", (s, e) =>
            {
                if (m_attributes is PillPresetVaultAttributes vaultAttr)
                {
                    vaultAttr.PromptAndSaveNewVariant();
                }
            });
            Menu_AppendSeparator(menu);

            Menu_AppendItem(menu, "Conectar Pilhas/Componentes Selecionados", (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc == null) return;
                int added = 0;
                foreach (var obj in doc.SelectedObjects())
                {
                    if (obj == this) continue;
                    if (!TargetGuids.Contains(obj.InstanceGuid))
                    {
                        TargetGuids.Add(obj.InstanceGuid);
                        added++;
                    }
                }
                if (added > 0)
                {
                    RecordUndoEvent("Conectar Pilhas");
                    ExpireSolution(true);
                }
            });

            if (TargetGuids.Count > 0)
            {
                Menu_AppendItem(menu, "âŒ Desconectar Todas as Pilhas", (s, e) =>
                {
                    RecordUndoEvent("Desconectar Pilhas");
                    TargetGuids.Clear();
                    ExpireSolution(true);
                });

                var subMenu = Menu_AppendItem(menu, $"ðŸ“¦ Pilhas Conectadas ({TargetGuids.Count})");
                var doc = OnPingDocument();
                if (doc != null)
                {
                    foreach (var id in TargetGuids.ToArray())
                    {
                        var obj = doc.FindObject(id, true);
                        string name = obj != null ? (string.IsNullOrWhiteSpace(obj.NickName) ? obj.Name : obj.NickName) : "Objeto";
                        Menu_AppendItem(subMenu.DropDown, $"Desconectar: {name}", (s, e) =>
                        {
                            TargetGuids.Remove(id);
                            ExpireSolution(true);
                        });
                    }
                }
            }
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string newName = "Opcao_Nova";
            DA.GetData(1, ref newName);

            bool triggerSave = false;
            DA.GetData(2, ref triggerSave);

            if (triggerSave)
            {
                CaptureCurrentState(newName);
            }

            // Selecao externa via entrada 3
            object rawSel = null;
            if (DA.GetData(3, ref rawSel) && rawSel != null)
            {
                int targetIdx = -1;
                if (rawSel is int i) targetIdx = i;
                else if (rawSel is double d) targetIdx = (int)d;
                else if (rawSel is string str)
                {
                    targetIdx = Presets.FindIndex(p => p.Name.Equals(str, StringComparison.OrdinalIgnoreCase));
                }
                else if (rawSel is IGH_Goo goo)
                {
                    string gs = goo.ToString();
                    targetIdx = Presets.FindIndex(p => p.Name.Equals(gs, StringComparison.OrdinalIgnoreCase));
                }

                if (targetIdx >= 0 && targetIdx < Presets.Count && targetIdx != ActivePresetIndex)
                {
                    ApplyVariant(targetIdx);
                }
            }

            // Obter bundle ativo se existir
            PillBundle activeBundle = null;
            if (ActivePresetIndex >= 0 && ActivePresetIndex < Presets.Count)
            {
                var cur = Presets[ActivePresetIndex];
                foreach (var v in cur.Variables)
                {
                    if (!string.IsNullOrEmpty(v.BundleJson))
                    {
                        activeBundle = PillBundle.FromJson(v.BundleJson);
                        break;
                    }
                }
            }

            string activeName = (ActivePresetIndex >= 0 && ActivePresetIndex < Presets.Count)
                ? Presets[ActivePresetIndex].Name
                : "(Nenhuma Ativa)";

            var allNames = new List<string>();
            foreach (var p in Presets) allNames.Add(p.Name);

            string summary = (ActivePresetIndex >= 0 && ActivePresetIndex < Presets.Count)
                ? Presets[ActivePresetIndex].GetSummary()
                : "Cofre Vazio ou Nenhuma Selecionada";

            if (activeBundle != null) DA.SetData(0, new GH_ObjectWrapper(activeBundle));
            DA.SetData(1, activeName);
            DA.SetDataList(2, allNames);
            DA.SetData(3, summary);
            DA.SetData(4, Presets.Count);

            this.Message = $"[VAULT] {Presets.Count} var";
        }

        public List<IGH_DocumentObject> GetMonitoredObjects()
        {
            var list = new List<IGH_DocumentObject>();
            var doc = OnPingDocument();

            // 1. Objetos conectados diretamente via conexao estilo Galapagos (TargetGuids)
            if (doc != null)
            {
                for (int i = TargetGuids.Count - 1; i >= 0; i--)
                {
                    var id = TargetGuids[i];
                    var obj = doc.FindObject(id, true);
                    if (obj == null)
                    {
                        TargetGuids.RemoveAt(i);
                        continue;
                    }
                    if (!list.Contains(obj) && obj != this)
                    {
                        list.Add(obj);
                        CollectUpstream(obj, list);
                    }
                }
            }

            // 2. Objetos conectados via fio tradicional no input 0
            if (Params.Input.Count > 0)
            {
                foreach (var src in Params.Input[0].Sources)
                {
                    if (src == null) continue;
                    var docObj = src.Attributes?.GetTopLevel?.DocObject;
                    if (docObj != null && !list.Contains(docObj) && docObj != this)
                    {
                        list.Add(docObj);
                        CollectUpstream(docObj, list);
                    }
                }
            }
            return list;
        }

        private void CollectUpstream(IGH_DocumentObject obj, List<IGH_DocumentObject> visited)
        {
            if (obj == null) return;

            var inputs = new List<IGH_Param>();
            if (obj is IGH_Component comp && comp.Params?.Input != null)
            {
                inputs.AddRange(comp.Params.Input);
            }
            else if (obj is IGH_Param param)
            {
                inputs.Add(param);
            }

            foreach (var inp in inputs)
            {
                if (inp?.Sources == null) continue;
                foreach (var src in inp.Sources)
                {
                    if (src == null) continue;
                    var upstreamObj = src.Attributes?.GetTopLevel?.DocObject;
                    if (upstreamObj != null && !visited.Contains(upstreamObj) && upstreamObj != this)
                    {
                        visited.Add(upstreamObj);
                        CollectUpstream(upstreamObj, visited);
                    }
                }
            }
        }

                public void OverwriteVariant(int index)
        {
            if (index < 0 || index >= Presets.Count) return;
            string targetName = Presets[index].Name;
            CaptureCurrentState(targetName);
        }

        public void CaptureCurrentState(string variantName)
        {
            if (string.IsNullOrWhiteSpace(variantName))
            {
                variantName = $"Variante_{Presets.Count + 1}";
            }

            var variant = new PillVaultVariant
            {
                Name = variantName.Trim(),
                Timestamp = DateTime.Now
            };

            var monitored = GetMonitoredObjects();
            foreach (var obj in monitored)
            {
                var pv = new PillVaultVariable
                {
                    ParamGuid = obj.InstanceGuid,
                    ObjectName = string.IsNullOrWhiteSpace(obj.NickName) ? obj.Name : obj.NickName
                };

                if (obj is GH_NumberSlider slider)
                {
                    pv.ObjectType = "Slider";
                    pv.NumberValue = (double)slider.CurrentValue;
                    pv.TextValue = slider.CurrentValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (obj is GH_BooleanToggle toggle)
                {
                    pv.ObjectType = "Toggle";
                    pv.BoolValue = toggle.Value;
                    pv.TextValue = toggle.Value.ToString();
                }
                else if (obj is GH_Panel panel)
                {
                    pv.ObjectType = "Panel";
                    pv.TextValue = panel.UserText;
                }
                else if (obj is GH_ValueList vlist)
                {
                    pv.ObjectType = "ValueList";
                    if (vlist.SelectedItems.Count > 0)
                    {
                        pv.TextValue = vlist.SelectedItems[0].Name;
                        if (double.TryParse(vlist.SelectedItems[0].Expression, out double val)) pv.NumberValue = val;
                    }
                }
                else if (obj is IGH_Component comp)
                {
                    pv.ObjectType = "Component";
                    try
                    {
                        var chunk = new GH_LooseChunk("ComponentState");
                        comp.Write(chunk);
                        pv.SerializedXml = chunk.Serialize_Xml();
                    }
                    catch { }
                }
                else if (obj is IGH_Param param)
                {
                    pv.ObjectType = "Param";
                    foreach (var data in param.VolatileData.AllData(true))
                    {
                        if (data is GH_ObjectWrapper wrapper && wrapper.Value is PillBundle bundle)
                        {
                            pv.ObjectType = "Bundle";
                            pv.BundleJson = bundle.ToJson();
                            break;
                        }
                        if (data != null) pv.SerializedValues.Add(data.ToString());
                    }
                }

                variant.Variables.Add(pv);
            }

            RecordUndoEvent("Gravar Variante");
            // Substituir se ja existir com o mesmo nome, ou adicionar nova
            int existingIdx = Presets.FindIndex(p => p.Name.Equals(variant.Name, StringComparison.OrdinalIgnoreCase));
            if (existingIdx >= 0)
            {
                Presets[existingIdx] = variant;
                ActivePresetIndex = existingIdx;
            }
            else
            {
                Presets.Add(variant);
                ActivePresetIndex = Presets.Count - 1;
            }

            var doc = OnPingDocument();
            if (doc != null)
            {
                doc.Modified();
            }

            ExpireSolution(true);
        }

        public void ApplyVariant(int index, bool triggerNewSolution = true)
        {
            if (index < 0 || index >= Presets.Count) return;

            ActivePresetIndex = index;
            var doc = OnPingDocument();
            if (doc == null) return;

            Action doApply = () =>
            {
                var variant = Presets[index];
                foreach (var v in variant.Variables)
                {
                    var obj = doc.FindObject(v.ParamGuid, false);
                    if (obj == null) continue;

                    if (obj is GH_NumberSlider slider && v.NumberValue.HasValue)
                    {
                        slider.SetSliderValue((decimal)v.NumberValue.Value);
                    }
                    else if (obj is GH_BooleanToggle toggle && v.BoolValue.HasValue)
                    {
                        toggle.Value = v.BoolValue.Value;
                    }
                    else if (obj is GH_Panel panel && v.TextValue != null)
                    {
                        panel.SetUserText(v.TextValue);
                    }
                    else if (obj is GH_ValueList vlist && !string.IsNullOrEmpty(v.TextValue))
                    {
                        for (int itemIdx = 0; itemIdx < vlist.ListItems.Count; itemIdx++)
                        {
                            if (vlist.ListItems[itemIdx].Name.Equals(v.TextValue, StringComparison.OrdinalIgnoreCase))
                            {
                                vlist.SelectItem(itemIdx);
                                break;
                            }
                        }
                    }
                    else if (obj is IGH_Component comp && !string.IsNullOrEmpty(v.SerializedXml))
                    {
                        try
                        {
                            var chunk = new GH_LooseChunk("ComponentState");
                            chunk.Deserialize_Xml(v.SerializedXml);
                            comp.Read(chunk);
                            comp.ExpireSolution(false);
                        }
                        catch { }
                    }
                }

                Grasshopper.Instances.RedrawCanvas();

                if (triggerNewSolution)
                {
                    doc.NewSolution(false);
                }
            };

            if (doc.SolutionState == GH_ProcessStep.Process)
            {
                EventHandler handler = null;
                handler = (s, e) =>
                {
                    Rhino.RhinoApp.Idle -= handler;
                    doApply();
                };
                Rhino.RhinoApp.Idle += handler;
            }
            else
            {
                doApply();
            }
        }

        public void DeleteVariant(int index)
        {
            if (index >= 0 && index < Presets.Count)
            {
                RecordUndoEvent("Excluir Variante");
                Presets.RemoveAt(index);
                if (ActivePresetIndex >= Presets.Count)
                {
                    ActivePresetIndex = Presets.Count - 1;
                }
                var doc = OnPingDocument();
                if (doc != null)
                {
                    doc.Modified();
                }
                ExpireSolution(true);
            }
        }

        // ==========================================
        // PERSISTENCIA NO ARQUIVO .GH
        // ==========================================
        public string ExportPresetsJson()
        {
            try
            {
                var list = new List<Dictionary<string, object>>();
                foreach (var p in Presets)
                {
                    var pDict = new Dictionary<string, object>
                    {
                        { "Name", p.Name ?? "Variante" },
                        { "Timestamp", p.Timestamp.ToString("o", System.Globalization.CultureInfo.InvariantCulture) }
                    };
                    var varsList = new List<Dictionary<string, object>>();
                    foreach (var v in p.Variables)
                    {
                        var vDict = new Dictionary<string, object>
                        {
                            { "Guid", v.ParamGuid.ToString() },
                            { "ObjName", v.ObjectName ?? "" },
                            { "ObjType", v.ObjectType ?? "" }
                        };
                        if (v.NumberValue.HasValue) vDict["NumVal"] = v.NumberValue.Value;
                        if (v.BoolValue.HasValue) vDict["BoolVal"] = v.BoolValue.Value;
                        if (v.TextValue != null) vDict["TxtVal"] = v.TextValue;
                        if (!string.IsNullOrEmpty(v.BundleJson)) vDict["BundleJson"] = v.BundleJson;
                        if (!string.IsNullOrEmpty(v.SerializedXml)) vDict["SerializedXml"] = v.SerializedXml;
                        if (v.SerializedValues != null && v.SerializedValues.Count > 0) vDict["Values"] = v.SerializedValues;
                        varsList.Add(vDict);
                    }
                    pDict["Vars"] = varsList;
                    list.Add(pDict);
                }
                return PillJson.Serialize(list);
            }
            catch
            {
                return "";
            }
        }

        public void ImportPresetsJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                var parsed = PillJson.Deserialize(json);
                if (parsed is List<object> list)
                {
                    Presets.Clear();
                    foreach (var item in list)
                    {
                        if (item is Dictionary<string, object> pDict)
                        {
                            var variant = new PillVaultVariant
                            {
                                Name = pDict.ContainsKey("Name") ? pDict["Name"]?.ToString() : "Variante",
                                Timestamp = pDict.ContainsKey("Timestamp") && DateTime.TryParse(pDict["Timestamp"]?.ToString(), out var dt) ? dt : DateTime.Now
                            };
                            if (pDict.ContainsKey("Vars") && pDict["Vars"] is List<object> varsList)
                            {
                                foreach (var vObj in varsList)
                                {
                                    if (vObj is Dictionary<string, object> vDict)
                                    {
                                        var pv = new PillVaultVariable
                                        {
                                            ParamGuid = vDict.ContainsKey("Guid") && Guid.TryParse(vDict["Guid"]?.ToString(), out var g) ? g : Guid.Empty,
                                            ObjectName = vDict.ContainsKey("ObjName") ? vDict["ObjName"]?.ToString() : "",
                                            ObjectType = vDict.ContainsKey("ObjType") ? vDict["ObjType"]?.ToString() : ""
                                        };
                                        if (vDict.ContainsKey("NumVal") && vDict["NumVal"] != null)
                                            pv.NumberValue = Convert.ToDouble(vDict["NumVal"], System.Globalization.CultureInfo.InvariantCulture);
                                        if (vDict.ContainsKey("BoolVal") && vDict["BoolVal"] != null)
                                            pv.BoolValue = Convert.ToBoolean(vDict["BoolVal"]);
                                        if (vDict.ContainsKey("TxtVal"))
                                            pv.TextValue = vDict["TxtVal"]?.ToString();
                                        if (vDict.ContainsKey("BundleJson"))
                                            pv.BundleJson = vDict["BundleJson"]?.ToString();
                                        if (vDict.ContainsKey("SerializedXml"))
                                            pv.SerializedXml = vDict["SerializedXml"]?.ToString();

                                        variant.Variables.Add(pv);
                                    }
                                }
                            }
                            Presets.Add(variant);
                        }
                    }
                }
            }
            catch { }
        }

        // ==========================================
        // PERSISTENCIA NO ARQUIVO .GH
        // ==========================================
        public override bool Write(GH_IWriter writer)
        {
            try
            {
                writer.SetInt32("TargetGuidCount", TargetGuids.Count);
                for (int g = 0; g < TargetGuids.Count; g++)
                {
                    writer.SetGuid("TargetGuid", g, TargetGuids[g]);
                }

                writer.SetInt32("VaultActiveIndex", ActivePresetIndex);
                writer.SetInt32("PresetCount", Presets.Count);

                // 1. JSON Payload seguro completo (Preservação à prova de falhas)
                string json = ExportPresetsJson();
                if (!string.IsNullOrEmpty(json))
                {
                    writer.SetString("VaultJson", json);
                }

                // 2. Chunks nativos do Grasshopper
                for (int i = 0; i < Presets.Count; i++)
                {
                    try
                    {
                        var pChunk = writer.CreateChunk("Preset", i);
                        var p = Presets[i];
                        pChunk.SetString("Name", p.Name ?? $"Variante_{i + 1}");
                        pChunk.SetDate("Date", p.Timestamp);
                        pChunk.SetInt32("VarCount", p.Variables.Count);
                        for (int j = 0; j < p.Variables.Count; j++)
                        {
                            try
                            {
                                var vChunk = pChunk.CreateChunk("Var", j);
                                var v = p.Variables[j];
                                vChunk.SetGuid("Guid", v.ParamGuid);
                                vChunk.SetString("ObjName", v.ObjectName ?? "");
                                vChunk.SetString("ObjType", v.ObjectType ?? "");
                                if (v.NumberValue.HasValue) vChunk.SetDouble("NumVal", v.NumberValue.Value);
                                if (v.BoolValue.HasValue) vChunk.SetBoolean("BoolVal", v.BoolValue.Value);
                                if (v.TextValue != null) vChunk.SetString("TxtVal", v.TextValue);
                                if (!string.IsNullOrEmpty(v.BundleJson)) vChunk.SetString("BundleJson", v.BundleJson);
                                if (!string.IsNullOrEmpty(v.SerializedXml)) vChunk.SetString("SerializedXml", v.SerializedXml);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            writer.SetBoolean("SuppressDefaultWires", SuppressDefaultWires);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            try
            {
                if (reader.ItemExists("SuppressDefaultWires"))
                {
                    SuppressDefaultWires = reader.GetBoolean("SuppressDefaultWires");
                }

                TargetGuids.Clear();
                if (reader.ItemExists("TargetGuidCount"))
                {
                    int gCount = reader.GetInt32("TargetGuidCount");
                    for (int g = 0; g < gCount; g++)
                    {
                        if (reader.ItemExists("TargetGuid", g))
                        {
                            TargetGuids.Add(reader.GetGuid("TargetGuid", g));
                        }
                    }
                }

                if (reader.ItemExists("VaultActiveIndex"))
                {
                    ActivePresetIndex = reader.GetInt32("VaultActiveIndex");
                }

                // Prioridade 1: Restaurar a partir do JSON seguro completo
                if (reader.ItemExists("VaultJson"))
                {
                    string json = reader.GetString("VaultJson");
                    ImportPresetsJson(json);
                }

                // Prioridade 2: Se não houver JSON ou a lista estiver vazia, ler dos Chunks
                if (Presets.Count == 0 && reader.ItemExists("PresetCount"))
                {
                    int count = reader.GetInt32("PresetCount");
                    for (int i = 0; i < count; i++)
                    {
                        try
                        {
                            var pChunk = reader.FindChunk("Preset", i);
                            if (pChunk == null) continue;
                            var p = new PillVaultVariant
                            {
                                Name = pChunk.ItemExists("Name") ? pChunk.GetString("Name") : $"Variante_{i + 1}",
                                Timestamp = pChunk.ItemExists("Date") ? pChunk.GetDate("Date") : DateTime.Now
                            };
                            if (pChunk.ItemExists("VarCount"))
                            {
                                int varCount = pChunk.GetInt32("VarCount");
                                for (int j = 0; j < varCount; j++)
                                {
                                    try
                                    {
                                        var vChunk = pChunk.FindChunk("Var", j);
                                        if (vChunk == null) continue;
                                        var v = new PillVaultVariable
                                        {
                                            ParamGuid = vChunk.ItemExists("Guid") ? vChunk.GetGuid("Guid") : Guid.Empty,
                                            ObjectName = vChunk.ItemExists("ObjName") ? vChunk.GetString("ObjName") : "",
                                            ObjectType = vChunk.ItemExists("ObjType") ? vChunk.GetString("ObjType") : ""
                                        };
                                        if (vChunk.ItemExists("NumVal")) v.NumberValue = vChunk.GetDouble("NumVal");
                                        if (vChunk.ItemExists("BoolVal")) v.BoolValue = vChunk.GetBoolean("BoolVal");
                                        if (vChunk.ItemExists("TxtVal")) v.TextValue = vChunk.GetString("TxtVal");
                                        if (vChunk.ItemExists("BundleJson")) v.BundleJson = vChunk.GetString("BundleJson");
                                        if (vChunk.ItemExists("SerializedXml")) v.SerializedXml = vChunk.GetString("SerializedXml");
                                        p.Variables.Add(v);
                                    }
                                    catch { }
                                }
                            }
                            Presets.Add(p);
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return base.Read(reader);
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var suppressItem = new ToolStripMenuItem("Ocultar Cabos Padrão do Grasshopper na Seleção")
            {
                Checked = SuppressDefaultWires,
                ToolTipText = "Quando ativado (padrão), oculta completamente os cabos verdes padrão do Grasshopper ao clicar no Pill Preset Vault, mantendo apenas os cabos customizados estilo Galapagos."
            };
            suppressItem.Click += (s, e) =>
            {
                RecordUndoEvent("Alternar Ocultação de Cabos");
                SuppressDefaultWires = !SuppressDefaultWires;
                Grasshopper.Instances.ActiveCanvas?.Invalidate();
            };
            menu.Items.Add(suppressItem);

            var wireMenu = new ToolStripMenuItem("⚡ Conexão Oculta (Modo Wallacei / Hidden Wire)");
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

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(wireMenu);
        }
    }

    public class PillVaultWireLinker : Grasshopper.GUI.Canvas.Interaction.GH_AbstractInteraction
    {
        private readonly PillPresetVaultAttributes _attr;
        private readonly PointF _startGrip;
        private PointF _currentPt;

        public PillVaultWireLinker(GH_Canvas canvas, PillPresetVaultAttributes attr, GH_CanvasMouseEvent e, PointF startGrip) : base(canvas, e, false)
        {
            _attr = attr;
            _startGrip = startGrip;
            _currentPt = startGrip;
            _attr.DraggingWirePoint = _currentPt;
        }

        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            _currentPt = e.CanvasLocation;
            _attr.DraggingWirePoint = _currentPt;
            sender.Invalidate();
            return GH_ObjectResponse.Handled;
        }

        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            _attr.DraggingWirePoint = null;

            var doc = sender.Document;
            if (doc != null)
            {
                IGH_DocumentObject hitObj = null;
                foreach (var obj in doc.Objects)
                {
                    if (obj == _attr.DocObject) continue;
                    if (obj.Attributes != null && obj.Attributes.Bounds.Contains(e.CanvasLocation))
                    {
                        hitObj = obj;
                        break;
                    }
                }

                if (hitObj != null)
                {
                    bool isCtrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                    if (isCtrl)
                    {
                        _attr.Vault.TargetGuids.Remove(hitObj.InstanceGuid);
                    }
                    else
                    {
                        if (!_attr.Vault.TargetGuids.Contains(hitObj.InstanceGuid))
                        {
                            _attr.Vault.RecordUndoEvent("Conectar Pilha");
                            _attr.Vault.TargetGuids.Add(hitObj.InstanceGuid);
                        }
                    }
                    _attr.Vault.ExpireSolution(true);
                }
            }

            sender.ActiveInteraction = null;
            sender.Invalidate();
            return GH_ObjectResponse.Handled;
        }

        public override GH_ObjectResponse RespondToKeyDown(GH_Canvas sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                _attr.DraggingWirePoint = null;
                sender.ActiveInteraction = null;
                sender.Invalidate();
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToKeyDown(sender, e);
        }
    }

        public class PillPresetVaultAttributes : GH_ComponentAttributes
    {
        public PointF? DraggingWirePoint { get; set; } = null;
        private RectangleF _btnSaveNewBounds;
        private RectangleF _btnUpdateBounds;
        private RectangleF _pillCapsuleBounds;
        private RectangleF _badgeBounds;
        private RectangleF _btnPrevBounds;
        private RectangleF _btnNextBounds;
        private RectangleF _displayBounds;
        private RectangleF _ledBounds;

        public PillPresetVaultAttributes(PillPresetVault_Component owner) : base(owner)
        {
        }

        public PillPresetVault_Component Vault => Owner as PillPresetVault_Component;

        protected override void Layout()
        {
            Vault?.HideDefaultWires();
            base.Layout();
            float oldRight = Bounds.Right;
            RectangleF b = Bounds;
            b.Width = Math.Max(b.Width, 145f);
            b.Height += 46f; // Altura para barra de acoes e capsula Pill
            Bounds = b;

            // Alinha os parâmetros de saída na borda direita expandida
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

            // Linha 1: Barra de AÃ§Ãµes RÃ¡pidas [+ NOVA] e [SALVAR]
            float y0 = b.Bottom - 44f;
            float halfW = (b.Width - 12f) / 2f;
            _btnSaveNewBounds = new RectangleF(b.X + 4f, y0, halfW, 15f);
            _btnUpdateBounds = new RectangleF(b.X + 8f + halfW, y0, halfW, 15f);

            // Linha 2: CÃ¡psula Pill Estilizada (IdÃªntica ao Pill Transmitter / Pill Receiver)
            float y1 = b.Bottom - 25f;
            _pillCapsuleBounds = new RectangleF(b.X + 4f, y1, b.Width - 8f, 21f);

            // Sub-elementos da CÃ¡psula Pill:
            // 1. Badge [VAULT]
            _badgeBounds = new RectangleF(_pillCapsuleBounds.X + 3f, _pillCapsuleBounds.Y + 3f, 40f, _pillCapsuleBounds.Height - 6f);

            // 2. LED Luminoso de Status (Ã  direita)
            float ledSize = 7f;
            _ledBounds = new RectangleF(_pillCapsuleBounds.Right - ledSize - 6f, _pillCapsuleBounds.Y + (_pillCapsuleBounds.Height - ledSize) / 2f, ledSize, ledSize);

            // 3. Controles centrais de navegaÃ§Ã£o entre o Badge e o LED
            float contentLeft = _badgeBounds.Right + 3f;
            float contentRight = _ledBounds.Left - 4f;
            float contentW = Math.Max(20f, contentRight - contentLeft);

            _btnPrevBounds = new RectangleF(contentLeft, _pillCapsuleBounds.Y + 2f, 13f, _pillCapsuleBounds.Height - 4f);
            _btnNextBounds = new RectangleF(contentRight - 13f, _pillCapsuleBounds.Y + 2f, 13f, _pillCapsuleBounds.Height - 4f);
            _displayBounds = new RectangleF(contentLeft + 14f, _pillCapsuleBounds.Y + 2f, contentW - 28f, _pillCapsuleBounds.Height - 4f);
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && Vault != null)
            {
                // 0. Clique e Arraste no Conector (Grip) de Hub / Pilhas para ligar no componente estilo Galapagos
                if (Vault.Params?.Input != null && Vault.Params.Input.Count > 0)
                {
                    var hubParam = Vault.Params.Input[0];
                    if (hubParam?.Attributes != null)
                    {
                        PointF grip = hubParam.Attributes.InputGrip;
                        float distSq = (e.CanvasLocation.X - grip.X) * (e.CanvasLocation.X - grip.X) + (e.CanvasLocation.Y - grip.Y) * (e.CanvasLocation.Y - grip.Y);
                        if (distSq <= 120f)
                        {
                            sender.ActiveInteraction = new PillVaultWireLinker(sender, this, e, grip);
                            return GH_ObjectResponse.Handled;
                        }
                    }
                }

                // 1. BotÃ£o [+ NOVA]
                if (_btnSaveNewBounds.Contains(e.CanvasLocation))
                {
                    PromptAndSaveNewVariant();
                    sender.Invalidate();
                    return GH_ObjectResponse.Handled;
                }

                // 2. BotÃ£o [SALVAR] (Sobrescrever Atual)
                if (_btnUpdateBounds.Contains(e.CanvasLocation))
                {
                    if (Vault.ActivePresetIndex >= 0 && Vault.ActivePresetIndex < Vault.Presets.Count)
                    {
                        Vault.OverwriteVariant(Vault.ActivePresetIndex);
                    }
                    else
                    {
                        PromptAndSaveNewVariant();
                    }
                    sender.Invalidate();
                    return GH_ObjectResponse.Handled;
                }

                // 3. BotÃ£o [<] Anterior
                if (_btnPrevBounds.Contains(e.CanvasLocation) && Vault.Presets.Count > 0)
                {
                    int next = Vault.ActivePresetIndex - 1;
                    if (next < 0) next = Vault.Presets.Count - 1;
                    Vault.ApplyVariant(next);
                    sender.Invalidate();
                    return GH_ObjectResponse.Handled;
                }

                // 4. BotÃ£o [>] PrÃ³ximo
                if (_btnNextBounds.Contains(e.CanvasLocation) && Vault.Presets.Count > 0)
                {
                    int next = Vault.ActivePresetIndex + 1;
                    if (next >= Vault.Presets.Count) next = 0;
                    Vault.ApplyVariant(next);
                    sender.Invalidate();
                    return GH_ObjectResponse.Handled;
                }

                // 5. Clique no Display central: se clicar no nome, abre o gerenciador de variantes
                if (_displayBounds.Contains(e.CanvasLocation))
                {
                    ShowPresetManagerDialog();
                    sender.Invalidate();
                    return GH_ObjectResponse.Handled;
                }
            }

            return base.RespondToMouseDown(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            ShowPresetManagerDialog();
            sender.Invalidate();
            return GH_ObjectResponse.Handled;
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            // 1. Canal de Fios: Desenhar os Cabos Estilo Galapagos Customizados
            if (channel == GH_CanvasChannel.Wires)
            {
                RenderGalapagosWires(canvas, graphics);

                // Se a supressao de cabos padrao estiver DESATIVADA pelo usuario, permite o comportamento normal do GH
                if (Vault != null && !Vault.SuppressDefaultWires)
                {
                    base.Render(canvas, graphics, channel);
                    return;
                }

                // Quando ativado (padrao = true):
                // NAO chama base.Render no canal Wires, impedindo que o Grasshopper desenhe o feixe de fios verdes ao selecionar o componente.
                // Repassa os fios apenas para outros inputs secundarios caso existam e nao estejam ocultos:
                if (Vault?.Params?.Input != null)
                {
                    for (int i = 0; i < Vault.Params.Input.Count; i++)
                    {
                        var param = Vault.Params.Input[i];
                        if (param == null || param.Attributes == null) continue;

                        // Hub / Parameters (i == 0) ou parametros com WireDisplay oculto sao 100% suprimidos
                        if (i == 0 || param.WireDisplay == GH_ParamWireDisplay.hidden)
                        {
                            continue;
                        }

                        param.Attributes.RenderToCanvas(canvas, GH_CanvasChannel.Wires);
                    }
                }
                return;
            }

            // 2. Renderizar Componente Base (para canais Objects, Overlay, etc.)
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

            // 3. Canal de Objetos: Molduras nos parametros monitorados e Botoes UI
            if (channel == GH_CanvasChannel.Objects)
            {
                RenderGalapagosFrames(canvas, graphics);
                RenderVaultUI(canvas, graphics);
            }
        }

        private void RenderGalapagosWires(GH_Canvas canvas, Graphics graphics)
        {
            if (Vault == null || Vault.Params?.Input == null || Vault.Params.Input.Count == 0) return;

            var hubParam = Vault.Params.Input[0];
            if (hubParam?.Attributes == null) return;

            PointF anchor = hubParam.Attributes.InputGrip;
            Color baseWireColor = Color.FromArgb(220, 155, 89, 182);
            Color selectedWireColor = Color.FromArgb(255, 185, 115, 225);

            if (DraggingWirePoint.HasValue)
            {
                PointF cur = DraggingWirePoint.Value;
                float dx = Math.Abs(cur.X - anchor.X);
                float dy = Math.Abs(cur.Y - anchor.Y);
                PointF c1 = new PointF(anchor.X - Math.Max(25f, dx * 0.4f), anchor.Y);
                PointF c2 = new PointF(cur.X, cur.Y - Math.Max(25f, dy * 0.4f));

                using (var arrowCap = new AdjustableArrowCap(4.2f, 4.8f, true))
                using (var pen = new Pen(selectedWireColor, 2.6f) { CustomEndCap = arrowCap, DashStyle = DashStyle.Dash })
                {
                    graphics.DrawBezier(pen, anchor, c1, c2, cur);
                }
            }

            var doc = Vault.OnPingDocument();
            if (doc == null) return;

            foreach (var id in Vault.TargetGuids)
            {
                var obj = doc.FindObject(id, true);
                if (obj == null || obj.Attributes == null) continue;
                bool isTargetSelected = obj.Attributes.Selected;
                Color wireColor = (Selected || isTargetSelected) ? selectedWireColor : baseWireColor;
                DrawGalapagosArrowWire(graphics, anchor, obj.Attributes.Bounds, wireColor);
            }

            if (hubParam.Sources != null)
            {
                foreach (var src in hubParam.Sources)
                {
                    if (src == null || src.Attributes == null) continue;
                    var topObj = src.Attributes.GetTopLevel?.DocObject;
                    RectangleF tb = topObj != null ? topObj.Attributes.Bounds : src.Attributes.Bounds;
                    bool isSourceSelected = (topObj != null && topObj.Attributes != null && topObj.Attributes.Selected) || src.Attributes.Selected;
                    Color wireColor = (Selected || isSourceSelected) ? selectedWireColor : baseWireColor;
                    DrawGalapagosArrowWire(graphics, anchor, tb, wireColor);
                }
            }
        }

        public static void DrawGalapagosArrowWire(Graphics graphics, PointF anchor, RectangleF targetBounds, Color color)
        {
            PointF targetPt;
            if (anchor.Y < targetBounds.Top + 10f)
            {
                targetPt = new PointF(
                    Math.Max(targetBounds.Left + 15f, Math.Min(targetBounds.Right - 15f, anchor.X)),
                    targetBounds.Top
                );
            }
            else
            {
                targetPt = GH_GraphicsUtil.BoxClosestPoint(anchor, targetBounds);
            }

            float dx = Math.Abs(targetPt.X - anchor.X);
            float dy = Math.Abs(targetPt.Y - anchor.Y);

            PointF c1;
            PointF c2;

            if (anchor.Y < targetBounds.Top + 10f)
            {
                c1 = new PointF(anchor.X - Math.Max(25f, dx * 0.4f), anchor.Y);
                c2 = new PointF(targetPt.X, targetPt.Y - Math.Max(25f, dy * 0.5f));
            }
            else
            {
                c1 = new PointF(anchor.X - Math.Max(25f, dx * 0.4f), anchor.Y);
                c2 = new PointF(targetPt.X + Math.Max(25f, dx * 0.4f), targetPt.Y);
            }

            using (var arrowCap = new AdjustableArrowCap(4.5f, 5.2f, true))
            using (var pen = new Pen(color, 2.4f) { CustomEndCap = arrowCap })
            {
                graphics.DrawBezier(pen, anchor, c1, c2, targetPt);
            }
        }

        private void RenderGalapagosFrames(GH_Canvas canvas, Graphics graphics)
        {
            if (Vault == null) return;

            var monitored = Vault.GetMonitoredObjects();
            Color frameColor = Color.FromArgb(200, 155, 89, 182);

            using (var pen = new Pen(frameColor, 2.0f) { DashStyle = DashStyle.Dash })
            using (var brushBg = new SolidBrush(Color.FromArgb(20, frameColor)))
            {
                foreach (var obj in monitored)
                {
                    if (obj == null || obj.Attributes == null) continue;
                    RectangleF tb = obj.Attributes.Bounds;
                    tb.Inflate(5, 5);

                    graphics.FillRectangle(brushBg, tb.X, tb.Y, tb.Width, tb.Height);
                    graphics.DrawRectangle(pen, tb.X, tb.Y, tb.Width, tb.Height);

                    string tag = "[VAULT]";
                    using (var font = new Font("Arial", 6.0f, FontStyle.Bold))
                    using (var tagBrush = new SolidBrush(frameColor))
                    {
                        graphics.DrawString(tag, font, tagBrush, tb.X + 2, tb.Y - 10);
                    }
                }
            }
        }

        private void RenderVaultUI(GH_Canvas canvas, Graphics graphics)
        {
            if (Vault == null) return;

            var prevSmoothing = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // -------------------------------------------------------------
            // 1. BARRA DE AÃ‡Ã•ES RÃPIDAS SUPERIOR: [+ NOVA] e [SALVAR]
            // -------------------------------------------------------------
            // A. BotÃ£o [+ NOVA]
            using (var pathNew = CreateRoundedRectangle(_btnSaveNewBounds, 3.5f))
            using (var brushNew = new SolidBrush(Color.FromArgb(37, 99, 235)))
            using (var penNew = new Pen(Color.FromArgb(96, 165, 250), 0.8f))
            {
                graphics.FillPath(brushNew, pathNew);
                graphics.DrawPath(penNew, pathNew);
            }
            using (var font = new Font("Segoe UI", 6.5f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.White))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                graphics.DrawString("+ NOVA", font, textBrush, _btnSaveNewBounds, sf);
            }

            // B. BotÃ£o [SALVAR]
            using (var pathUp = CreateRoundedRectangle(_btnUpdateBounds, 3.5f))
            using (var brushUp = new SolidBrush(Color.FromArgb(217, 119, 6)))
            using (var penUp = new Pen(Color.FromArgb(251, 191, 36), 0.8f))
            {
                graphics.FillPath(brushUp, pathUp);
                graphics.DrawPath(penUp, pathUp);
            }
            using (var font = new Font("Segoe UI", 6.5f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.White))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                graphics.DrawString("SALVAR", font, textBrush, _btnUpdateBounds, sf);
            }

            // -------------------------------------------------------------
            // 2. CÃPSULA PILL INFERIOR (IdÃªntica ao Pill Transmitter)
            // -------------------------------------------------------------
            Color themeColor = Color.FromArgb(147, 51, 234); // Roxo/Violeta do Vault

            // Fundo da cÃ¡psula com gradiente escuro e borda curva na cor tema
            using (var pathCapsule = CreateRoundedRectangle(_pillCapsuleBounds, 5.5f))
            {
                using (var bgBrush = new LinearGradientBrush(_pillCapsuleBounds, Color.FromArgb(28, 32, 38), Color.FromArgb(20, 24, 28), LinearGradientMode.Vertical))
                {
                    graphics.FillPath(bgBrush, pathCapsule);
                }
                using (var borderPen = new Pen(Color.FromArgb(180, themeColor), 1.2f))
                {
                    graphics.DrawPath(borderPen, pathCapsule);
                }
            }

            // A. Badge Colorido [VAULT]
            using (var badgePath = CreateRoundedRectangle(_badgeBounds, 3f))
            using (var badgeBrush = new SolidBrush(themeColor))
            {
                graphics.FillPath(badgeBrush, badgePath);
            }
            using (var badgeFont = new Font("Segoe UI", 6.8f, FontStyle.Bold))
            using (var badgeTextBrush = new SolidBrush(Color.White))
            {
                var sfBadge = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                graphics.DrawString("VAULT", badgeFont, badgeTextBrush, _badgeBounds, sfBadge);
            }

            // B. Botões de Navegação [<] e [>] em vetor anti-aliased nítido
            using (var arrowBrush = new SolidBrush(Color.FromArgb(180, 195, 210)))
            {
                // Seta Esquerda (Anterior)
                float cxL = _btnPrevBounds.X + _btnPrevBounds.Width / 2f;
                float cyL = _btnPrevBounds.Y + _btnPrevBounds.Height / 2f;
                PointF[] ptsLeft = new PointF[]
                {
                    new PointF(cxL + 2.5f, cyL - 3.5f),
                    new PointF(cxL - 2.5f, cyL),
                    new PointF(cxL + 2.5f, cyL + 3.5f)
                };
                graphics.FillPolygon(arrowBrush, ptsLeft);

                // Seta Direita (Próximo)
                float cxR = _btnNextBounds.X + _btnNextBounds.Width / 2f;
                float cyR = _btnNextBounds.Y + _btnNextBounds.Height / 2f;
                PointF[] ptsRight = new PointF[]
                {
                    new PointF(cxR - 2.5f, cyR - 3.5f),
                    new PointF(cxR + 2.5f, cyR),
                    new PointF(cxR - 2.5f, cyR + 3.5f)
                };
                graphics.FillPolygon(arrowBrush, ptsRight);
            }

            // C. Display do Nome da Variante Ativa
            string activeName = (Vault.ActivePresetIndex >= 0 && Vault.ActivePresetIndex < Vault.Presets.Count)
                ? Vault.Presets[Vault.ActivePresetIndex].Name
                : "(Vazio)";

            using (var nameFont = new Font("Segoe UI", 7.2f, FontStyle.Bold))
            using (var nameBrush = new SolidBrush(Color.FromArgb(254, 240, 138)))
            {
                var sfName = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                graphics.DrawString(activeName, nameFont, nameBrush, _displayBounds, sfName);
            }

            // D. LED Luminoso de Status (CÃ­rculo Verde / Ã‚mbar com Halo)
            Color ledColor = (Vault.Presets.Count > 0 && Vault.ActivePresetIndex >= 0)
                ? Color.FromArgb(34, 197, 94)  // Verde Esmeralda Online
                : Color.FromArgb(245, 158, 11); // Ã‚mbar Vazio

            using (var ledBrush = new SolidBrush(ledColor))
            using (var ledPen = new Pen(Color.FromArgb(200, 255, 255, 255), 0.8f))
            {
                graphics.FillEllipse(ledBrush, _ledBounds);
                graphics.DrawEllipse(ledPen, _ledBounds);
            }

            graphics.SmoothingMode = prevSmoothing;
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

        public void PromptAndSaveNewVariant()
        {
            if (Vault == null) return;

            using (var form = new Form())
            {
                form.Text = "Gravar / Sobrescrever Variante - Pill Preset Vault";
                form.Size = new Size(420, 210);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.BackColor = Color.FromArgb(35, 35, 40);
                form.ForeColor = Color.White;
                form.Font = new Font("Segoe UI", 9f);

                string curActiveName = (Vault.ActivePresetIndex >= 0 && Vault.ActivePresetIndex < Vault.Presets.Count)
                    ? Vault.Presets[Vault.ActivePresetIndex].Name
                    : "";

                var lbl = new Label
                {
                    Text = "Nome da variante (digite novo ou escolha para sobrescrever):",
                    Location = new Point(16, 16),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(200, 200, 200)
                };

                var cmb = new ComboBox
                {
                    Location = new Point(16, 42),
                    Width = 370,
                    BackColor = Color.FromArgb(50, 50, 55),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    DropDownStyle = ComboBoxStyle.DropDown
                };

                foreach (var p in Vault.Presets)
                {
                    cmb.Items.Add(p.Name);
                }

                if (!string.IsNullOrEmpty(curActiveName))
                {
                    cmb.Text = curActiveName;
                }
                else
                {
                    cmb.Text = $"Opcao_{Vault.Presets.Count + 1}";
                }

                var btnOverwrite = new Button
                {
                    Text = "Sobrescrever Atual",
                    Location = new Point(16, 110),
                    Width = 140,
                    Height = 32,
                    BackColor = Color.FromArgb(211, 84, 0),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnOverwrite.Enabled = !string.IsNullOrEmpty(curActiveName);
                btnOverwrite.Click += (s, e) =>
                {
                    Vault.CaptureCurrentState(curActiveName);
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                };

                var btnSaveNew = new Button
                {
                    Text = "Salvar com Nome",
                    Location = new Point(164, 110),
                    Width = 140,
                    Height = 32,
                    BackColor = Color.FromArgb(41, 128, 185),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnSaveNew.Click += (s, e) =>
                {
                    string name = cmb.Text.Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                        Vault.CaptureCurrentState(name);
                    }
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                };

                var btnCancel = new Button
                {
                    Text = "Cancelar",
                    Location = new Point(312, 110),
                    Width = 74,
                    Height = 32,
                    BackColor = Color.FromArgb(70, 70, 75),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    DialogResult = DialogResult.Cancel
                };

                form.Controls.Add(lbl);
                form.Controls.Add(cmb);
                form.Controls.Add(btnOverwrite);
                form.Controls.Add(btnSaveNew);
                form.Controls.Add(btnCancel);
                form.AcceptButton = btnSaveNew;
                form.CancelButton = btnCancel;

                form.ShowDialog();
            }
        }

        public void ShowPresetManagerDialog()
        {
            if (Vault == null) return;

            using (var form = new Form())
            {
                form.Text = "Cofre de Variantes (Pill Preset Vault) - Log e Gerenciador";
                form.Size = new Size(820, 460);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.BackColor = Color.FromArgb(30, 30, 35);
                form.ForeColor = Color.White;
                form.Font = new Font("Segoe UI", 9f);

                // Cabecalho
                var lblHeader = new Label
                {
                    Text = $"Cofre de Variantes: {Vault.Presets.Count} variantes gravadas",
                    Dock = DockStyle.Top,
                    Height = 35,
                    Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(0, 200, 255),
                    Padding = new Padding(12, 8, 0, 0)
                };

                // Grid de dados (apenas leitura e exclusao de linhas)
                var grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    BackgroundColor = Color.FromArgb(40, 40, 45),
                    ForeColor = Color.Black,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = true,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    RowHeadersVisible = false,
                    BorderStyle = BorderStyle.None
                };

                grid.Columns.Add("Name", "Nome da Variante");
                grid.Columns.Add("Date", "Data / Hora");
                grid.Columns.Add("Count", "Qtd Variaveis");
                grid.Columns.Add("Summary", "Resumo dos Valores Gravados");
                grid.Columns[0].FillWeight = 25;
                grid.Columns[1].FillWeight = 20;
                grid.Columns[2].FillWeight = 15;
                grid.Columns[3].FillWeight = 40;

                void PopulateGrid()
                {
                    grid.Rows.Clear();
                    for (int i = 0; i < Vault.Presets.Count; i++)
                    {
                        var p = Vault.Presets[i];
                        string marker = (i == Vault.ActivePresetIndex) ? " [ATIVA]" : "";
                        grid.Rows.Add(p.Name + marker, p.Timestamp.ToString("dd/MM/yyyy HH:mm:ss"), p.Variables.Count, p.GetSummary());
                    }
                }
                PopulateGrid();

                // Painel de botoes
                var pnlBottom = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 50,
                    BackColor = Color.FromArgb(25, 25, 30)
                };

                var btnDelete = new Button
                {
                    Text = "Excluir Linha Selecionada",
                    Location = new Point(12, 10),
                    Size = new Size(180, 30),
                    BackColor = Color.FromArgb(192, 57, 43),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnDelete.Click += (s, e) =>
                {
                    if (grid.SelectedRows.Count > 0)
                    {
                        int selIdx = grid.SelectedRows[0].Index;
                        if (selIdx >= 0 && selIdx < Vault.Presets.Count)
                        {
                            var pName = Vault.Presets[selIdx].Name;
                            if (MessageBox.Show($"Tem certeza que deseja excluir a variante '{pName}'?", "Confirmar Exclusao", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                            {
                                Vault.DeleteVariant(selIdx);
                                PopulateGrid();
                            }
                        }
                    }
                };

                var btnApply = new Button
                {
                    Text = "Aplicar Variante no Canvas",
                    Location = new Point(202, 10),
                    Size = new Size(160, 30),
                    BackColor = Color.FromArgb(39, 174, 96),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnApply.Click += (s, e) =>
                {
                    if (grid.SelectedRows.Count > 0)
                    {
                        int selIdx = grid.SelectedRows[0].Index;
                        if (selIdx >= 0 && selIdx < Vault.Presets.Count)
                        {
                            Vault.ApplyVariant(selIdx);
                            PopulateGrid();
                            form.Close();
                        }
                    }
                };

                var btnOverwrite = new Button
                {
                    Text = "Sobrescrever Selecionada",
                    Location = new Point(372, 10),
                    Size = new Size(175, 30),
                    BackColor = Color.FromArgb(211, 84, 0),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnOverwrite.Click += (s, e) =>
                {
                    if (grid.SelectedRows.Count > 0)
                    {
                        int selIdx = grid.SelectedRows[0].Index;
                        if (selIdx >= 0 && selIdx < Vault.Presets.Count)
                        {
                            var pName = Vault.Presets[selIdx].Name;
                            if (MessageBox.Show($"Deseja sobrescrever a variante '{pName}' com os valores atuais do Canvas?", "Confirmar Sobrescrita", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                            {
                                Vault.CaptureCurrentState(pName);
                                PopulateGrid();
                            }
                        }
                    }
                };

                var btnAdd = new Button
                {
                    Text = "+ Nova Variante",
                    Location = new Point(557, 10),
                    Size = new Size(130, 30),
                    BackColor = Color.FromArgb(41, 128, 185),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnAdd.Click += (s, e) =>
                {
                    PromptAndSaveNewVariant();
                    PopulateGrid();
                };

                var btnClose = new Button
                {
                    Text = "Fechar",
                    Location = new Point(697, 10),
                    Size = new Size(90, 30),
                    BackColor = Color.FromArgb(60, 60, 65),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    DialogResult = DialogResult.Cancel
                };

                pnlBottom.Controls.Add(btnDelete);
                pnlBottom.Controls.Add(btnApply);
                pnlBottom.Controls.Add(btnOverwrite);
                pnlBottom.Controls.Add(btnAdd);
                pnlBottom.Controls.Add(btnClose);

                form.Controls.Add(grid);
                form.Controls.Add(lblHeader);
                form.Controls.Add(pnlBottom);

                form.ShowDialog();
            }
        }    }
}