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
    /// <summary>Inspeciona o conteúdo de um store: chaves, revisões, árvores, tipos, metadados e dados compartilhados.</summary>
    public class PillSchemaInspector_Component : GlauxCapsuleComponent
    {
        public PillSchemaInspector_Component()
            : base(
                "Pill Schema Inspector",
                "PillSchema",
                "Mostra o que há dentro de um store (.glauxdb): chaves por tipo, número de revisões, última gravação, árvores de cada entrada, tipos de dados, profundidade, campos de metadados e revisões que compartilham exatamente os mesmos dados (mesmo hash).\n" +
                "Não há tabelas SQL: cada chave guarda árvores tipadas; o 'schema' é inferido do conteúdo.",
                "Data",
                "DB",
                ColorDB)
        {
        }

        public override Guid ComponentGuid => new Guid("e81fefb2-a9fe-4c83-bfc2-2654e4cfed57");
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override Bitmap Icon => GlauxToolsIcons.PillSchemaInspector;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Store", "S", StoreInput.InputDescription, GH_ParamAccess.item);
            pManager.AddTextParameter("Key Pattern", "K", "Padrão de chave com * e ? (vazio = todas).", GH_ParamAccess.item, "*");
            pManager.AddBooleanParameter("Scan Types", "ST", "Lê a última revisão de cada chave para listar tipos e profundidade (mais lento em stores grandes).", GH_ParamAccess.item, true);
            for (int i = 0; i < 3; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Keys", "K", "Chaves (uma por tipo+chave).", GH_ParamAccess.list);
            pManager.AddTextParameter("Kinds", "T", "Tipo de cada chave.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Revisions", "R", "Quantidade de revisões de cada chave.", GH_ParamAccess.list);
            pManager.AddTextParameter("Latest", "L", "Última revisão e data de cada chave.", GH_ParamAccess.list);
            pManager.AddTextParameter("Trees", "Tr", "Árvores da última revisão (ramo por chave): nome, ramos, itens.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Types", "Ty", "Tipos da última revisão (ramo por chave), com contagem e profundidade.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Metadata Fields", "MF", "Nomes de metadados usados no store e em quantas entradas.", GH_ParamAccess.list);
            pManager.AddTextParameter("Shared Data", "SD", "Revisões com dados idênticos (mesmo hash): relação implícita entre entradas.", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "Arquivo, tamanho, versão e integridade.", GH_ParamAccess.item);
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

            string pattern = "*";
            DA.GetData(1, ref pattern);
            bool scanTypes = true;
            DA.GetData(2, ref scanTypes);

            if (!store.Exists)
            {
                SetCapsule(System.IO.Path.GetFileName(store.Path), false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Store ainda não existe: {store.Path}");
                return;
            }

            try
            {
                var regex = StoreQueryEngine.GlobToRegex(pattern);
                var groups = new SortedDictionary<string, List<StoreEntryHeader>>(StringComparer.Ordinal);
                var metaFields = new SortedDictionary<string, int>(StringComparer.Ordinal);
                var byHash = new Dictionary<string, List<string>>(StringComparer.Ordinal);

                foreach (var e in store.ListEntries())
                {
                    if (regex != null && !regex.IsMatch(e.Key)) continue;
                    string k = e.Kind + ":" + e.Key;
                    if (!groups.TryGetValue(k, out var list)) groups[k] = list = new List<StoreEntryHeader>();
                    list.Add(e);
                    foreach (var mk in e.Metadata.Keys)
                    {
                        metaFields.TryGetValue(mk, out int c);
                        metaFields[mk] = c + 1;
                    }
                    foreach (var t in e.Trees)
                    {
                        if (!byHash.TryGetValue(t.Hash, out var refs)) byHash[t.Hash] = refs = new List<string>();
                        refs.Add($"{e.Kind}:{e.Reference}/{t.Name}");
                    }
                }

                var keys = new List<string>();
                var kinds = new List<string>();
                var revCounts = new List<int>();
                var latest = new List<string>();
                var trees = new GH_Structure<GH_String>();
                var types = new GH_Structure<GH_String>();
                int b = 0;
                foreach (var kv in groups)
                {
                    var list = kv.Value;
                    StoreEntryHeader last = list[0];
                    foreach (var e in list) if (e.Revision > last.Revision) last = e;

                    keys.Add(last.Key);
                    kinds.Add(last.Kind);
                    revCounts.Add(list.Count);
                    latest.Add($"r{last.Revision} · {last.TimestampUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");

                    var path = new GH_Path(b);
                    trees.EnsurePath(path);
                    types.EnsurePath(path);
                    foreach (var t in last.Trees)
                    {
                        trees.Append(new GH_String($"{t.Name}: {t.BranchCount} ramo(s), {t.ItemCount} item(ns), {FormatBytes(t.Length)}"), path);
                    }

                    if (scanTypes)
                    {
                        foreach (var kvTree in store.LoadTrees(last))
                        {
                            var table = kvTree.Value;
                            types.Append(new GH_String($"{kvTree.Key}: {PillTreeExport_Component.DescribeTypes(table)} | profundidade {table.MinDepth}–{table.MaxDepth}"), path);
                        }
                    }
                    b++;
                }

                var fields = new List<string>();
                foreach (var kv in metaFields) fields.Add($"{kv.Key} ({kv.Value})");

                var shared = new List<string>();
                foreach (var kv in byHash)
                {
                    if (kv.Value.Count > 1) shared.Add($"{TreeHash.Short(kv.Key)}: {string.Join(", ", kv.Value)}");
                }
                shared.Sort(StringComparer.Ordinal);

                var stats = store.GetStats();
                SetCapsule(System.IO.Path.GetFileName(store.Path), stats.CorruptionMessage == null, stats.TornTailBytes > 0);
                Message = $"{groups.Count} chaves";
                if (stats.CorruptionMessage != null) AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Corrupção: " + stats.CorruptionMessage);

                DA.SetDataList(0, keys);
                DA.SetDataList(1, kinds);
                DA.SetDataList(2, revCounts);
                DA.SetDataList(3, latest);
                DA.SetDataTree(4, trees);
                DA.SetDataTree(5, types);
                DA.SetDataList(6, fields);
                DA.SetDataList(7, shared);
                DA.SetData(8, $"{store.Path} | {stats.KeyCount} chave(s), {stats.EntryCount} revisão(ões) | {FormatBytes(stats.FileBytes)} | formato v{stats.FormatVersion}" +
                              (stats.TornTailBytes > 0 ? $" | {stats.TornTailBytes} B de gravação interrompida" : ""));
            }
            catch (Exception ex)
            {
                SetCapsule("Erro", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
