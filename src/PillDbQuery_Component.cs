using System;
using System.Collections.Generic;
using System.Drawing;
using Buraqueira_Tools.Data;
using Buraqueira_Tools.Persistence;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    /// <summary>Consulta estruturada ao store: filtros por entrada e por item, sem SQL montado a partir de texto.</summary>
    public class PillDbQuery_Component : GlauxCapsuleComponent
    {
        public PillDbQuery_Component()
            : base(
                "Pill DB Query",
                "PillQuery",
                "Consulta o store (.glauxdb) com filtros tipados, sem montar consultas a partir de texto:\n" +
                "- Entradas: tipo, padrão de chave (* e ?), revisões (última ou todas), metadados 'chave=valor'.\n" +
                "- Itens: máscara de caminho ('{0;*}', '{*;2}', '{1;**}', '{0..3;*}'), tipo, faixa numérica e texto.\n" +
                "Responde perguntas como 'quais revisões têm T60 acima de 2 s?' ou 'em que ramos aparece este valor?'.",
                "Data",
                "DB",
                ColorDB)
        {
        }

        public override Guid ComponentGuid => new Guid("b7cfcebc-275a-4470-bcfd-50050bac121e");
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override Bitmap Icon => GlauxToolsIcons.PillDbQuery;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Store", "S", StoreInput.InputDescription, GH_ParamAccess.item);
            pManager.AddTextParameter("Kind", "T", "Tipo das entradas (dataset, snapshot, experiment, metrics). Vazio = todos.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Key Pattern", "K", "Padrão da chave com * e ? (ex: 'ACU_*'). Vazio = todas.", GH_ParamAccess.item, "*");
            pManager.AddBooleanParameter("All Revisions", "All", "False = só a última revisão de cada chave; True = todas.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Metadata", "M", "Filtros de metadados 'chave=valor' (todos precisam bater).", GH_ParamAccess.list);
            pManager.AddTextParameter("Path Mask", "P", "Máscara de caminho dos itens (ex: '{0;*}'). Vazio = qualquer.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Type", "Ty", "Tipo dos itens (Number, Integer, Text, Point, Curve...). Vazio = qualquer.", GH_ParamAccess.item, "");
            pManager.AddIntervalParameter("Range", "Rg", "Faixa numérica dos itens (Number, Integer, Boolean).", GH_ParamAccess.item);
            pManager.AddTextParameter("Contains", "C", "Texto contido no valor (sem diferenciar maiúsculas).", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Tree", "Tr", "Árvore consultada dentro de cada entrada (padrão 'data').", GH_ParamAccess.item, StoreTreeNames.Data);
            pManager.AddIntegerParameter("Limit", "L", "Máximo de entradas retornadas.", GH_ParamAccess.item, 1000);
            for (int i = 0; i < 11; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Keys", "K", "Chave de cada entrada encontrada.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Revisions", "R", "Revisão de cada entrada encontrada.", GH_ParamAccess.list);
            pManager.AddTextParameter("Timestamps", "TS", "Data/hora local de cada entrada.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Values", "V", "Itens que passaram nos filtros (um ramo por entrada; vazio se não houver filtro de item).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Paths", "P", "Caminho original de cada item encontrado.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Indices", "i", "Índice original de cada item encontrado.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Count", "N", "Quantidade de itens encontrados.", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "I", "Entradas e itens examinados.", GH_ParamAccess.item);
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

            string kind = "", keyPattern = "*", pathMask = "", type = "", contains = "", treeName = StoreTreeNames.Data;
            bool allRevisions = false;
            var metaLines = new List<string>();
            Interval range = Interval.Unset;
            int limit = 1000;
            DA.GetData(1, ref kind);
            DA.GetData(2, ref keyPattern);
            DA.GetData(3, ref allRevisions);
            DA.GetDataList(4, metaLines);
            DA.GetData(5, ref pathMask);
            DA.GetData(6, ref type);
            bool hasRange = DA.GetData(7, ref range) && range.IsValid;
            DA.GetData(8, ref contains);
            DA.GetData(9, ref treeName);
            DA.GetData(10, ref limit);

            var query = new StoreQuery
            {
                Kind = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim(),
                KeyPattern = keyPattern,
                Revisions = allRevisions ? RevisionScope.All : RevisionScope.Latest,
                PathMask = pathMask,
                TypeName = type,
                TextContains = string.IsNullOrEmpty(contains) ? null : contains,
                TreeName = string.IsNullOrWhiteSpace(treeName) ? StoreTreeNames.Data : treeName.Trim(),
                EntryLimit = Math.Max(1, limit)
            };
            if (hasRange)
            {
                query.Min = range.Min;
                query.Max = range.Max;
            }
            foreach (var kv in ParseKeyValues(metaLines)) query.MetadataEquals[kv.Key] = kv.Value;

            StoreQueryResult result;
            try
            {
                result = StoreQueryEngine.Execute(store, query);
            }
            catch (FormatException ex)
            {
                SetCapsule("Filtro inválido", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                return;
            }
            catch (Exception ex)
            {
                SetCapsule("Erro", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Falha na consulta: {ex.Message}");
                return;
            }

            var keys = new List<string>();
            var revisions = new List<int>();
            var timestamps = new List<string>();
            var entryIndex = new Dictionary<Guid, int>();
            foreach (var e in result.Entries)
            {
                entryIndex[e.Id] = keys.Count;
                keys.Add(e.Key);
                revisions.Add((int)Math.Min(e.Revision, int.MaxValue));
                timestamps.Add(e.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            }

            var values = new GH_Structure<IGH_Goo>();
            var paths = new GH_Structure<GH_String>();
            var indices = new GH_Structure<GH_Integer>();
            var warnings = new List<string>();
            foreach (var m in result.Items)
            {
                if (!entryIndex.TryGetValue(m.Entry.Id, out int branch)) continue;
                var p = new GH_Path(branch);
                values.Append(GooCodec.Decode(m.Value, warnings), p);
                paths.Append(new GH_String(GlauxTreeTable.FormatPath(m.Path)), p);
                indices.Append(new GH_Integer(m.Index), p);
            }
            ReportWarnings(warnings);
            if (result.Truncated) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Resultado truncado pelo limite; refine os filtros ou aumente 'Limit'.");

            SetCapsule(string.IsNullOrWhiteSpace(keyPattern) ? "*" : keyPattern, result.Entries.Count > 0, result.Truncated, $"{result.Entries.Count}");
            Message = query.HasItemFilters ? $"{result.Items.Count} itens" : $"{result.Entries.Count} entradas";

            DA.SetDataList(0, keys);
            DA.SetDataList(1, revisions);
            DA.SetDataList(2, timestamps);
            DA.SetDataTree(3, values);
            DA.SetDataTree(4, paths);
            DA.SetDataTree(5, indices);
            DA.SetData(6, result.Items.Count);
            DA.SetData(7, $"{result.Entries.Count} entrada(s) | {result.Items.Count} item(ns) | examinados: {result.ScannedEntries} entrada(s), {result.ScannedItems} item(ns)");
        }
    }
}
