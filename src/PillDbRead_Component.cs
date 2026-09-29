using System;
using System.Collections.Generic;
using System.Drawing;
using Buraqueira_Tools.Data;
using Buraqueira_Tools.Persistence;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>Lê uma revisão de uma chave do store e reconstrói a DataTree.</summary>
    public class PillDbRead_Component : GlauxCapsuleComponent
    {
        private Guid _cachedEntry = Guid.Empty;
        private string _cachedTree = "";
        private GH_Structure<IGH_Goo> _cachedData;
        private List<string> _cachedWarnings = new List<string>();

        public PillDbRead_Component()
            : base(
                "Pill DB Read",
                "PillDBRead",
                "Lê uma revisão de uma chave do store (.glauxdb) e reconstrói a DataTree original (caminhos, ramos vazios, nulos e tipos).\n" +
                "- Revision 0 = última; N = revisão N; -1 = penúltima, -2 = antepenúltima...\n" +
                "- Atualiza sozinho quando outro componente grava no mesmo store.",
                "Data",
                "DB",
                ColorDB)
        {
        }

        public override Guid ComponentGuid => new Guid("09ddcfcb-9325-403d-8a80-02db78a40596");
        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override Bitmap Icon => GlauxToolsIcons.PillDbRead;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Store", "S", StoreInput.InputDescription, GH_ParamAccess.item);
            pManager.AddTextParameter("Key", "K", "Chave a ler.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Revision", "R", "0 = última; N > 0 = revisão N; N < 0 = N revisões antes da última.", GH_ParamAccess.item, 0);
            pManager.AddTextParameter("Kind", "T", "Tipo da entrada (padrão 'dataset').", GH_ParamAccess.item, StoreKinds.Dataset);
            pManager.AddTextParameter("Tree", "Tr", "Nome da árvore dentro da entrada (padrão 'data'; snapshots têm várias).", GH_ParamAccess.item, StoreTreeNames.Data);
            pManager[0].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Árvore reconstruída.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Revision", "R", "Revisão lida.", GH_ParamAccess.item);
            pManager.AddTextParameter("Timestamp", "TS", "Data/hora local da gravação.", GH_ParamAccess.item);
            pManager.AddTextParameter("Metadata", "M", "Metadados 'chave=valor'.", GH_ParamAccess.list);
            pManager.AddTextParameter("Hash", "H", "SHA-256 de identidade dos dados.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Revisions", "Rs", "Todas as revisões disponíveis da chave.", GH_ParamAccess.list);
            pManager.AddTextParameter("Trees", "Tn", "Nomes das árvores da entrada.", GH_ParamAccess.list);
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
            string key = "";
            if (!DA.GetData(1, ref key) || string.IsNullOrWhiteSpace(key))
            {
                SetCapsule("Sem chave", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Informe a chave (Key).");
                return;
            }
            key = key.Trim();
            StoreWatch.Watch(this, store, key);
            int revision = 0;
            DA.GetData(2, ref revision);
            string kind = StoreKinds.Dataset;
            DA.GetData(3, ref kind);
            if (string.IsNullOrWhiteSpace(kind)) kind = StoreKinds.Dataset;
            string treeName = StoreTreeNames.Data;
            DA.GetData(4, ref treeName);
            if (string.IsNullOrWhiteSpace(treeName)) treeName = StoreTreeNames.Data;

            try
            {
                if (!store.Exists)
                {
                    SetCapsule(key, false);
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Store ainda não existe: {store.Path}");
                    return;
                }

                var revisions = store.GetRevisions(kind, key);
                if (revisions.Count == 0)
                {
                    SetCapsule(key, false);
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Chave '{key}' ({kind}) não existe no store.");
                    return;
                }

                StoreEntryHeader header = ResolveRevision(revisions, revision);
                if (header == null)
                {
                    SetCapsule(key, false);
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Revisão {revision} não existe (disponíveis: 1..{revisions[revisions.Count - 1].Revision}).");
                    return;
                }

                var treeInfo = header.FindTree(treeName);
                if (treeInfo == null)
                {
                    SetCapsule(header.Reference, false);
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"A entrada {header.Reference} não tem a árvore '{treeName}'.");
                    return;
                }

                if (header.Id != _cachedEntry || treeName != _cachedTree || _cachedData == null)
                {
                    var warnings = new List<string>();
                    _cachedData = TreeMapper.ToTree(store.LoadTree(header, treeName), warnings);
                    _cachedWarnings = warnings;
                    _cachedEntry = header.Id;
                    _cachedTree = treeName;
                }
                ReportWarnings(_cachedWarnings);

                bool isLatest = header.Revision == revisions[revisions.Count - 1].Revision;
                SetCapsule(key, true, !isLatest, $"r{header.Revision}");
                Message = isLatest ? $"r{header.Revision} (última)" : $"r{header.Revision}";
                if (StoreWatch.IsPaused(this))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Atualização automática pausada: gravações demais em sequência (possível laço leitura → gravação).");
                }

                var revList = new List<int>(revisions.Count);
                foreach (var r in revisions) revList.Add((int)Math.Min(r.Revision, int.MaxValue));
                var trees = new List<string>();
                foreach (var t in header.Trees) trees.Add(t.Name);

                DA.SetDataTree(0, _cachedData);
                DA.SetData(1, (int)Math.Min(header.Revision, int.MaxValue));
                DA.SetData(2, header.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                DA.SetDataList(3, FormatKeyValues(new Dictionary<string, string>(ToDictionary(header.Metadata))));
                DA.SetData(4, treeInfo.Hash);
                DA.SetDataList(5, revList);
                DA.SetDataList(6, trees);
            }
            catch (Exception ex)
            {
                SetCapsule(key, false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Falha ao ler: {ex.Message}");
            }
        }

        internal static StoreEntryHeader ResolveRevision(IReadOnlyList<StoreEntryHeader> revisions, long revision)
        {
            if (revisions.Count == 0) return null;
            if (revision == 0) return revisions[revisions.Count - 1];
            if (revision < 0)
            {
                int idx = revisions.Count - 1 + (int)Math.Max(revision, -revisions.Count);
                return idx >= 0 ? revisions[idx] : null;
            }
            foreach (var r in revisions)
            {
                if (r.Revision == revision) return r;
            }
            return null;
        }

        internal static IDictionary<string, string> ToDictionary(IReadOnlyDictionary<string, string> source)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in source) d[kv.Key] = kv.Value;
            return d;
        }
    }
}
