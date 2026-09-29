using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Buraqueira_Tools.Data;
using Buraqueira_Tools.Persistence;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>Grava uma DataTree como nova revisão de uma chave no store.</summary>
    public class PillDbWrite_Component : GlauxCapsuleComponent
    {
        public PillDbWrite_Component()
            : base(
                "Pill DB Write",
                "PillDBWrite",
                "Grava a DataTree como nova revisão da chave no store (.glauxdb), sem perda de estrutura ou tipos.\n" +
                "- Cada gravação cria a revisão N+1; revisões anteriores continuam consultáveis.\n" +
                "- 'Skip Unchanged' (padrão) não cria revisão quando dados e metadados são idênticos à última (identidade por SHA-256).\n" +
                "- Conecte um botão em 'Write' para gravar sob demanda, ou um toggle para gravar a cada mudança.",
                "Data",
                "DB",
                ColorDB)
        {
        }

        public override Guid ComponentGuid => new Guid("2d0dee42-91cb-41b6-88c9-4c850eeae40a");
        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override Bitmap Icon => GlauxToolsIcons.PillDbWrite;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Store", "S", StoreInput.InputDescription, GH_ParamAccess.item);
            pManager.AddTextParameter("Key", "K", "Chave do conjunto de dados (ex: 'ACU_T60', 'Resultados/Sala01').", GH_ParamAccess.item);
            pManager.AddGenericParameter("Data", "D", "Árvore a gravar.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Metadata", "M", "Metadados 'chave=valor' (ex: 'autor=Ana', 'fonte=Pachyderm').", GH_ParamAccess.list);
            pManager.AddTextParameter("Kind", "T", "Tipo da entrada (padrão 'dataset').", GH_ParamAccess.item, StoreKinds.Dataset);
            pManager.AddBooleanParameter("Write", "W", "Grava quando True.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Skip Unchanged", "SU", "Não cria revisão se os dados e metadados forem idênticos à última.", GH_ParamAccess.item, true);
            pManager[0].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Revision", "R", "Revisão gravada (ou a última existente).", GH_ParamAccess.item);
            pManager.AddTextParameter("Hash", "H", "SHA-256 de identidade dos dados.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Written", "OK", "True se uma nova revisão foi criada nesta solução.", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "I", "Resumo da gravação.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var doc = OnPingDocument();
            if (!StoreInput.TryResolve(DA, 0, doc, out var store, out bool readOnly, out string error))
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
            if (!DA.GetDataTree(2, out GH_Structure<IGH_Goo> tree)) tree = new GH_Structure<IGH_Goo>();
            var metaLines = new List<string>();
            DA.GetDataList(3, metaLines);
            string kind = StoreKinds.Dataset;
            DA.GetData(4, ref kind);
            if (string.IsNullOrWhiteSpace(kind)) kind = StoreKinds.Dataset;
            bool write = false;
            DA.GetData(5, ref write);
            bool skipUnchanged = true;
            DA.GetData(6, ref skipUnchanged);

            var warnings = new List<string>();
            var table = TreeMapper.ToTable(tree, warnings);
            string hash = TreeHash.Compute(table);
            ReportWarnings(warnings);

            try
            {
                if (!write)
                {
                    var latest = store.Exists ? store.GetLatest(kind, key) : null;
                    bool same = latest?.FindTree(StoreTreeNames.Data)?.Hash == hash;
                    SetCapsule(key, latest != null, !same, latest != null ? $"r{latest.Revision}" : "novo");
                    Message = same ? "Salvo" : "Pendente";
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, same ? "O store já tem estes dados na última revisão." : "Aguardando 'Write' = True para gravar.");
                    DA.SetData(0, latest != null ? (int)Math.Min(latest.Revision, int.MaxValue) : 0);
                    DA.SetData(1, hash);
                    DA.SetData(2, false);
                    DA.SetData(3, latest != null ? $"Última revisão: {latest.Reference} ({latest.TimestampUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss})" : "Chave ainda não gravada.");
                    return;
                }

                if (readOnly)
                {
                    SetCapsule(key, false);
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Store aberto como somente leitura.");
                    return;
                }

                var draft = new StoreEntryDraft(kind, key).AddTree(StoreTreeNames.Data, table);
                foreach (var kv in ParseKeyValues(metaLines)) draft.Metadata[kv.Key] = kv.Value;

                var sw = Stopwatch.StartNew();
                var result = store.Append(draft, skipUnchanged, InstanceGuid);
                sw.Stop();

                var h = result.Header;
                SetCapsule(key, true, false, $"r{h.Revision}");
                Message = result.Written ? $"r{h.Revision} gravada" : $"r{h.Revision} (igual)";
                DA.SetData(0, (int)Math.Min(h.Revision, int.MaxValue));
                DA.SetData(1, hash);
                DA.SetData(2, result.Written);
                DA.SetData(3, result.Written
                    ? $"Gravado {h.Reference}: {table.BranchCount} ramo(s), {table.ItemCount} item(ns) em {sw.ElapsedMilliseconds} ms."
                    : $"Sem mudança: {h.Reference} já tem estes dados e metadados.");
            }
            catch (Exception ex)
            {
                SetCapsule(key, false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Falha ao gravar: {ex.Message}");
            }
        }
    }
}
