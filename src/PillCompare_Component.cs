using System;
using System.Collections.Generic;
using System.Drawing;
using Buraqueira_Tools.Persistence;
using Buraqueira_Tools.ProjectState;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    /// <summary>Compara dois estados gravados: árvores, parâmetros, valores e ambiente.</summary>
    public class PillCompare_Component : GlauxCapsuleComponent
    {
        public PillCompare_Component()
            : base(
                "Pill Compare",
                "PillCompare",
                "Compara duas revisões gravadas (snapshots, experimentos ou datasets):\n" +
                "- quais árvores/parâmetros mudaram, surgiram ou sumiram (identidade por hash, sem carregar o que é igual);\n" +
                "- para parâmetros, 'valor antigo → novo'; para árvores, itens diferentes, ramos novos/removidos e maior variação numérica;\n" +
                "- mudanças de ambiente (versões do Glaux/Rhino/Grasshopper, documento) e se entradas/saídas são idênticas.\n" +
                "Referências: 'Nome' (última), 'Nome@3' (revisão 3), 'Nome@-1' (penúltima). Sem B, compara A com a revisão anterior.",
                "Vault",
                "VAULT",
                ColorVault)
        {
        }

        public override Guid ComponentGuid => new Guid("162fb1d0-b437-4854-b48c-8d93caa87b48");
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override Bitmap Icon => GlauxToolsIcons.PillCompare;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Store", "S", StoreInput.InputDescription, GH_ParamAccess.item);
            pManager.AddTextParameter("A", "A", "Referência do primeiro estado ('Nome', 'Nome@3', 'Nome@-1').", GH_ParamAccess.item);
            pManager.AddTextParameter("B", "B", "Referência do segundo estado. Vazio = revisão anterior à de A.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Kind", "T", "Tipo das entradas (padrão 'snapshot').", GH_ParamAccess.item, StoreKinds.Snapshot);
            pManager[0].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Same Data", "=", "True se todas as árvores são idênticas.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Same Inputs", "=In", "True se as entradas (parâmetros, entradas, controles, canais) são idênticas.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Same Outputs", "=Out", "True se as saídas são idênticas.", GH_ParamAccess.item);
            pManager.AddTextParameter("Changed", "Ch", "Árvores alteradas.", GH_ParamAccess.list);
            pManager.AddTextParameter("Added", "Ad", "Árvores só em B.", GH_ParamAccess.list);
            pManager.AddTextParameter("Removed", "Rm", "Árvores só em A.", GH_ParamAccess.list);
            pManager.AddTextParameter("Report", "Rp", "Uma linha por árvore alterada, com o detalhe da diferença.", GH_ParamAccess.list);
            pManager.AddTextParameter("Metadata Changes", "MC", "Mudanças de ambiente/metadados.", GH_ParamAccess.list);
            pManager.AddTextParameter("Pair", "AB", "Referências efetivamente comparadas.", GH_ParamAccess.item);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            StoreWatch.Unwatch(this);
            base.RemovedFromDocument(document);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!StoreInput.TryResolve(DA, 0, OnPingDocument(), out var store, out _, out string error))
            {
                SetCapsule("Store inválido", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error);
                return;
            }
            StoreWatch.Watch(this, store);

            string refA = "", refB = "", kind = StoreKinds.Snapshot;
            if (!DA.GetData(1, ref refA) || string.IsNullOrWhiteSpace(refA))
            {
                SetCapsule("Sem A", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Informe a referência A.");
                return;
            }
            DA.GetData(2, ref refB);
            DA.GetData(3, ref kind);
            if (string.IsNullOrWhiteSpace(kind)) kind = StoreKinds.Snapshot;

            try
            {
                var a = store.Exists ? EntryRef.Resolve(store, kind, refA) : null;
                if (a == null)
                {
                    SetCapsule(refA, false);
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"'{refA}' ({kind}) não encontrado no store.");
                    return;
                }

                StoreEntryHeader b;
                if (string.IsNullOrWhiteSpace(refB))
                {
                    // Sem B: A contra a revisão imediatamente anterior da mesma chave
                    b = a;
                    a = null;
                    foreach (var r in store.GetRevisions(kind, b.Key))
                    {
                        if (r.Revision < b.Revision) a = r;
                    }
                    if (a == null)
                    {
                        SetCapsule(b.Reference, false);
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{b.Reference} não tem revisão anterior para comparar.");
                        return;
                    }
                }
                else
                {
                    b = EntryRef.Resolve(store, kind, refB);
                    if (b == null)
                    {
                        SetCapsule(refB, false);
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"'{refB}' ({kind}) não encontrado no store.");
                        return;
                    }
                }

                var diff = SnapshotComparer.Compare(a, b, (h, name) => store.LoadTree(h, name));

                var changed = new List<string>();
                var added = new List<string>();
                var removed = new List<string>();
                var report = new List<string>();
                foreach (var t in diff.Trees)
                {
                    switch (t.Change)
                    {
                        case TreeChange.Changed:
                            changed.Add(t.Name);
                            report.Add($"{t.Name}: {t.Detail}");
                            break;
                        case TreeChange.Added:
                            added.Add(t.Name);
                            report.Add($"{t.Name}: nova ({t.Detail})");
                            break;
                        case TreeChange.Removed:
                            removed.Add(t.Name);
                            report.Add($"{t.Name}: removida ({t.Detail})");
                            break;
                    }
                }

                string pair = $"{a.Reference} → {b.Reference}";
                SetCapsule(pair, true, !diff.DataEqual);
                Message = diff.DataEqual ? "Idênticos" : $"{changed.Count + added.Count + removed.Count} diferença(s)";

                DA.SetData(0, diff.DataEqual);
                DA.SetData(1, diff.InputsEqual);
                DA.SetData(2, diff.OutputsEqual);
                DA.SetDataList(3, changed);
                DA.SetDataList(4, added);
                DA.SetDataList(5, removed);
                DA.SetDataList(6, report);
                DA.SetDataList(7, diff.MetadataChanges);
                DA.SetData(8, pair);
            }
            catch (Exception ex)
            {
                SetCapsule("Erro", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
