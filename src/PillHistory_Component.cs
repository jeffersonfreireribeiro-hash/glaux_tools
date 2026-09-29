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
    /// <summary>Lista o histórico de snapshots/experimentos (ou qualquer tipo de entrada) de um store.</summary>
    public class PillHistory_Component : GlauxCapsuleComponent
    {
        public PillHistory_Component()
            : base(
                "Pill History",
                "PillHistory",
                "Lista o histórico de estados gravados no store: snapshots, experimentos, métricas ou datasets.\n" +
                "- Uma linha por revisão: referência, data, notas, hashes de entradas e saídas.\n" +
                "- 'Tree' extrai uma árvore de cada revisão (ex: 'out', 'result', 'param:Largura') numa só árvore {revisão; caminho original}, pronta para gráficos (Chart Line) e tabelas.",
                "Vault",
                "VAULT",
                ColorVault)
        {
        }

        public override Guid ComponentGuid => new Guid("13e0a590-d4ff-46a1-845a-f0c5eb4d205c");
        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override Bitmap Icon => GlauxToolsIcons.PillHistory;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Store", "S", StoreInput.InputDescription, GH_ParamAccess.item);
            pManager.AddTextParameter("Kind", "T", "Tipo das entradas: snapshot (padrão), experiment, metrics, dataset.", GH_ParamAccess.item, StoreKinds.Snapshot);
            pManager.AddTextParameter("Key Pattern", "K", "Padrão de chave com * e ?.", GH_ParamAccess.item, "*");
            pManager.AddTextParameter("Tree", "Tr", "Árvore a extrair de cada revisão (opcional).", GH_ParamAccess.item, "");
            pManager.AddIntegerParameter("Limit", "L", "Máximo de revisões listadas.", GH_ParamAccess.item, 100);
            pManager.AddBooleanParameter("Newest First", "NF", "Mais recentes primeiro.", GH_ParamAccess.item, true);
            for (int i = 0; i < 6; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Refs", "R", "Referências 'chave@revisão' (entrada do Compare/Restore).", GH_ParamAccess.list);
            pManager.AddTextParameter("Keys", "K", "Chave de cada revisão.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Revisions", "Rev", "Número de cada revisão.", GH_ParamAccess.list);
            pManager.AddTextParameter("Timestamps", "TS", "Data/hora local.", GH_ParamAccess.list);
            pManager.AddTextParameter("Notes", "Nt", "Notas e tags.", GH_ParamAccess.list);
            pManager.AddTextParameter("Inputs Hash", "IH", "Hash (curto) das entradas de cada revisão.", GH_ParamAccess.list);
            pManager.AddTextParameter("Outputs Hash", "OH", "Hash (curto) das saídas de cada revisão.", GH_ParamAccess.list);
            pManager.AddTextParameter("Metadata", "M", "Metadados 'chave=valor' (um ramo por revisão).", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Tree Data", "D", "Árvore extraída de cada revisão, com caminho {i; caminho original}.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Info", "I", "Resumo.", GH_ParamAccess.item);
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

            string kind = StoreKinds.Snapshot, pattern = "*", treeName = "";
            int limit = 100;
            bool newestFirst = true;
            DA.GetData(1, ref kind);
            DA.GetData(2, ref pattern);
            DA.GetData(3, ref treeName);
            DA.GetData(4, ref limit);
            DA.GetData(5, ref newestFirst);

            if (!store.Exists)
            {
                SetCapsule("Sem store", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Store ainda não existe: {store.Path}");
                return;
            }

            try
            {
                var regex = StoreQueryEngine.GlobToRegex(pattern);
                var entries = new List<StoreEntryHeader>();
                foreach (var e in store.ListEntries())
                {
                    if (!string.IsNullOrWhiteSpace(kind) && !string.Equals(e.Kind, kind.Trim(), StringComparison.Ordinal)) continue;
                    if (regex != null && !regex.IsMatch(e.Key)) continue;
                    entries.Add(e);
                }
                entries.Sort((a, b) =>
                {
                    int c = a.TimestampUtc.CompareTo(b.TimestampUtc);
                    if (c == 0) c = a.Revision.CompareTo(b.Revision);
                    return newestFirst ? -c : c;
                });
                bool truncated = entries.Count > Math.Max(1, limit);
                if (truncated) entries.RemoveRange(Math.Max(1, limit), entries.Count - Math.Max(1, limit));

                var refs = new List<string>();
                var keys = new List<string>();
                var revs = new List<int>();
                var stamps = new List<string>();
                var notes = new List<string>();
                var inHashes = new List<string>();
                var outHashes = new List<string>();
                var meta = new GH_Structure<GH_String>();
                var data = new GH_Structure<IGH_Goo>();
                var warnings = new List<string>();

                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    refs.Add(e.Reference);
                    keys.Add(e.Key);
                    revs.Add((int)Math.Min(e.Revision, int.MaxValue));
                    stamps.Add(e.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                    e.Metadata.TryGetValue(VaultNames.MetaNotes, out string note);
                    e.Metadata.TryGetValue(VaultNames.MetaTags, out string tag);
                    notes.Add(string.IsNullOrEmpty(tag) ? note ?? "" : $"{note} [{tag}]".Trim());
                    e.Metadata.TryGetValue(VaultNames.MetaInputsHash, out string ih);
                    e.Metadata.TryGetValue(VaultNames.MetaOutputsHash, out string oh);
                    inHashes.Add(TreeHash.Short(ih));
                    outHashes.Add(TreeHash.Short(oh));

                    var mp = new GH_Path(i);
                    meta.EnsurePath(mp);
                    foreach (var line in FormatKeyValues(PillDbRead_Component.ToDictionary(e.Metadata))) meta.Append(new GH_String(line), mp);

                    if (!string.IsNullOrWhiteSpace(treeName) && e.FindTree(treeName.Trim()) != null)
                    {
                        var table = store.LoadTree(e, treeName.Trim());
                        var tree = TreeMapper.ToTree(table, warnings);
                        for (int b = 0; b < tree.PathCount; b++)
                        {
                            var original = tree.Paths[b];
                            var indices = new int[original.Length + 1];
                            indices[0] = i;
                            Array.Copy(original.Indices, 0, indices, 1, original.Length);
                            data.AppendRange(tree.Branches[b], new GH_Path(indices));
                        }
                    }
                }

                ReportWarnings(warnings);
                if (truncated) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Mostrando as {entries.Count} revisões limitadas por 'Limit'.");
                SetCapsule(string.IsNullOrWhiteSpace(pattern) ? "*" : pattern, entries.Count > 0, false, kind);
                Message = $"{entries.Count} revisões";

                DA.SetDataList(0, refs);
                DA.SetDataList(1, keys);
                DA.SetDataList(2, revs);
                DA.SetDataList(3, stamps);
                DA.SetDataList(4, notes);
                DA.SetDataList(5, inHashes);
                DA.SetDataList(6, outHashes);
                DA.SetDataTree(7, meta);
                DA.SetDataTree(8, data);
                DA.SetData(9, $"{entries.Count} revisão(ões) de '{kind}'" + (string.IsNullOrWhiteSpace(treeName) ? "" : $", árvore '{treeName}'"));
            }
            catch (Exception ex)
            {
                SetCapsule("Erro", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
