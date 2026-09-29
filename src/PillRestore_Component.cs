using System;
using System.Collections.Generic;
using System.Drawing;
using Buraqueira_Tools.Data;
using Buraqueira_Tools.Persistence;
using Buraqueira_Tools.ProjectState;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Recupera um estado gravado: devolve parâmetros, entradas, saídas e canais, e (sob comando) reaplica os
    /// controles do canvas compatíveis, informando os que não puderam ser restaurados.
    /// </summary>
    public class PillRestore_Component : GlauxCapsuleComponent
    {
        private bool _lastApply;

        public PillRestore_Component()
            : base(
                "Pill Restore",
                "PillRestore",
                "Recupera um estado gravado pelo Pill Snapshot (ou uma execução do Experiment Logger):\n" +
                "- saídas: parâmetros (PillBundle, use Pill Bundle Unpack), árvores de entrada e saída, canais do PillHub registrados;\n" +
                "- 'Apply Controls' reaplica sliders, toggles, value lists, panels e Pill Slider Pools numa única solução, só nos controles compatíveis (existem, mesmo tipo, valor dentro da faixa atual, opção ainda existe); os demais aparecem em 'Compatibility'.\n" +
                "Referências: 'Nome' (última), 'Nome@3', 'Nome@-1'.",
                "Vault",
                "VAULT",
                ColorVault)
        {
        }

        public override Guid ComponentGuid => new Guid("f64e9ecf-3e22-4d10-ad8b-23dc2fc9c842");
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override Bitmap Icon => GlauxToolsIcons.PillRestore;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Store", "S", StoreInput.InputDescription, GH_ParamAccess.item);
            pManager.AddTextParameter("Ref", "R", "Estado a recuperar ('Nome', 'Nome@3', 'Nome@-1').", GH_ParamAccess.item);
            pManager.AddTextParameter("Kind", "T", "Tipo da entrada: snapshot (padrão) ou experiment.", GH_ParamAccess.item, StoreKinds.Snapshot);
            pManager.AddBooleanParameter("Apply Controls", "Go", "Reaplica os controles compatíveis no canvas (na borda False → True; use um botão).", GH_ParamAccess.item, false);
            pManager[0].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Parameters", "P", "Parâmetros gravados como PillBundle.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Inputs", "In", "Árvore de entradas gravada.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Outputs", "Out", "Árvore de saídas/resultados gravada.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Hub Keys", "HK", "Canais do PillHub registrados.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Hub Data", "HD", "Dados dos canais (ramo {i; caminho original} por canal).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Controls", "Ctl", "Controles registrados e seus valores.", GH_ParamAccess.list);
            pManager.AddTextParameter("Compatibility", "Cmp", "Para cada controle: OK ou o motivo de não poder ser restaurado.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Compatible", "N", "Quantidade de controles restauráveis no documento atual.", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "I", "Referência, data, notas e ambiente do estado.", GH_ParamAccess.item);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            StoreWatch.Unwatch(this);
            base.RemovedFromDocument(document);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var doc = OnPingDocument();
            if (!StoreInput.TryResolve(DA, 0, doc, out var store, out _, out string error))
            {
                SetCapsule("Store inválido", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error);
                return;
            }

            string refText = "", kind = StoreKinds.Snapshot;
            bool apply = false;
            if (!DA.GetData(1, ref refText) || string.IsNullOrWhiteSpace(refText))
            {
                SetCapsule("Sem referência", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Informe o estado a recuperar (Ref).");
                return;
            }
            DA.GetData(2, ref kind);
            if (string.IsNullOrWhiteSpace(kind)) kind = StoreKinds.Snapshot;
            DA.GetData(3, ref apply);
            bool rising = apply && !_lastApply;
            _lastApply = apply;

            if (EntryRef.TryParse(refText, out string key, out _)) StoreWatch.Watch(this, store, key);

            try
            {
                var header = store.Exists ? EntryRef.Resolve(store, kind, refText) : null;
                if (header == null)
                {
                    SetCapsule(refText, false);
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"'{refText}' ({kind}) não encontrado no store.");
                    return;
                }

                var parts = SnapshotCodec.Read(header, store.LoadTrees(header));
                var warnings = new List<string>();

                var bundle = BundleTrees.ToBundle(parts.Parameters, parts.Units, header.Key);
                var hubKeys = new List<string>();
                var hubData = new GH_Structure<IGH_Goo>();
                int h = 0;
                foreach (var kv in parts.HubChannels)
                {
                    hubKeys.Add(kv.Key);
                    var tree = TreeMapper.ToTree(kv.Value, warnings);
                    for (int b = 0; b < tree.PathCount; b++)
                    {
                        var original = tree.Paths[b];
                        var idx = new int[original.Length + 1];
                        idx[0] = h;
                        Array.Copy(original.Indices, 0, idx, 1, original.Length);
                        hubData.AppendRange(tree.Branches[b], new GH_Path(idx));
                    }
                    h++;
                }

                var controlLines = new List<string>();
                var compat = new List<string>();
                int compatibleCount = 0;
                List<ControlMatch> plan = null;
                if (parts.Controls.Count > 0 && doc != null)
                {
                    plan = ControlStateService.Plan(doc, parts.Controls);
                    foreach (var m in plan)
                    {
                        controlLines.Add(m.Saved.ToString());
                        if (m.Compatible)
                        {
                            compatibleCount++;
                            compat.Add(m.MatchedByName ? $"OK (pelo nome) — {m.Saved.Name}" : $"OK — {m.Saved.Name}");
                        }
                        else
                        {
                            compat.Add($"Não restaurável — {m.Saved.Name}: {m.Reason}");
                        }
                    }
                }

                if (rising)
                {
                    if (plan == null || compatibleCount == 0)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum controle compatível para reaplicar.");
                    }
                    else
                    {
                        ControlStateService.Apply(doc, plan);
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"{compatibleCount} controle(s) reaplicado(s); {plan.Count - compatibleCount} ignorado(s).");
                    }
                }
                else if (plan != null && compatibleCount < plan.Count)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"{plan.Count - compatibleCount} controle(s) não podem ser restaurados neste documento (ver Compatibility).");
                }

                ReportWarnings(warnings);
                SetCapsule(header.Reference, true, plan != null && compatibleCount < plan.Count);
                Message = header.Reference;

                string env = "";
                header.Metadata.TryGetValue(EnvironmentInfo.GlauxVersion, out string gv);
                header.Metadata.TryGetValue(EnvironmentInfo.RhinoVersion, out string rv);
                if (gv != null || rv != null) env = $" | Glaux {gv ?? "?"}, Rhino {rv ?? "?"}";

                DA.SetData(0, new GH_PillBundleGoo(bundle));
                DA.SetDataTree(1, parts.Inputs != null ? TreeMapper.ToTree(parts.Inputs, warnings) : new GH_Structure<IGH_Goo>());
                DA.SetDataTree(2, parts.Outputs != null ? TreeMapper.ToTree(parts.Outputs, warnings) : new GH_Structure<IGH_Goo>());
                DA.SetDataList(3, hubKeys);
                DA.SetDataTree(4, hubData);
                DA.SetDataList(5, controlLines);
                DA.SetDataList(6, compat);
                DA.SetData(7, compatibleCount);
                DA.SetData(8, $"{header.Reference} · {header.TimestampUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}" +
                              (string.IsNullOrEmpty(parts.Notes) ? "" : $" · {parts.Notes}") + env);
            }
            catch (Exception ex)
            {
                SetCapsule(refText, false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
