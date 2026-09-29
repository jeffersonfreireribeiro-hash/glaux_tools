using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Buraqueira_Tools.Data;
using Buraqueira_Tools.Persistence;
using Buraqueira_Tools.ProjectState;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Captura um estado reproduzível do projeto (parâmetros, entradas, saídas, controles do canvas, canais do
    /// PillHub, ambiente e hashes) como nova revisão no store.
    /// </summary>
    public class PillSnapshot_Component : GlauxCapsuleComponent
    {
        public PillSnapshot_Component()
            : base(
                "Pill Snapshot",
                "PillSnap",
                "Grava um estado reproduzível do projeto como nova revisão no store (.glauxdb):\n" +
                "- parâmetros nomeados (PillBundle), árvores de entrada e de saída;\n" +
                "- opcionalmente os controles do canvas (sliders, toggles, value lists, panels de entrada, Pill Slider Pool) e canais do PillHub;\n" +
                "- versões do Glaux, Rhino e Grasshopper, sistema, documento, data, notas, tags e tempo de execução;\n" +
                "- hashes de identidade das entradas e das saídas (mesmo hash de entradas = mesmos parâmetros).\n" +
                "Evolui o Pill Preset Vault: o histórico fica fora do .gh, consultável, comparável e restaurável.",
                "Vault",
                "VAULT",
                ColorVault)
        {
        }

        public override Guid ComponentGuid => new Guid("e3ec4408-12ab-4a13-97c0-a530572893d3");
        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override Bitmap Icon => GlauxToolsIcons.PillSnapshot;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Store", "S", StoreInput.InputDescription, GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "Nome do estado (chave do snapshot; cada captura cria uma revisão).", GH_ParamAccess.item);
            pManager.AddGenericParameter("Parameters", "P", "PillBundle(s) com os parâmetros nomeados (Pill Bundle Pack).", GH_ParamAccess.list);
            pManager.AddGenericParameter("Inputs", "In", "Árvore de entradas relevantes.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Outputs", "Out", "Árvore de saídas/resultados relevantes.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Controls", "Ctl", "Registra sliders, toggles, value lists, panels de entrada e Pill Slider Pools do documento.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Hub", "Hub", "Canais do PillHub a registrar: chave, grupo (ex: 'GEO') ou 'ALL'.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Notes", "Nt", "Anotações livres.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Tags", "Tg", "Etiquetas (ex: 'aprovado', 'cliente').", GH_ParamAccess.list);
            pManager.AddNumberParameter("Runtime", "ms", "Tempo de execução em ms (ex: do Pill Runtime Profiler ou Data Timer).", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Capture", "C", "Grava o snapshot quando True (conecte um botão).", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Skip Unchanged", "SU", "Não cria revisão se tudo for idêntico à última.", GH_ParamAccess.item, true);
            pManager[0].Optional = true;
            for (int i = 2; i < 12; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Ref", "R", "Referência do snapshot gravado ('Nome@revisão').", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Revision", "Rev", "Revisão gravada (ou a última existente).", GH_ParamAccess.item);
            pManager.AddTextParameter("Inputs Hash", "IH", "Identidade combinada das entradas (parâmetros, entradas, controles, canais).", GH_ParamAccess.item);
            pManager.AddTextParameter("Outputs Hash", "OH", "Identidade das saídas.", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "I", "Resumo do que foi registrado.", GH_ParamAccess.item);
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

            string name = "";
            if (!DA.GetData(1, ref name) || string.IsNullOrWhiteSpace(name))
            {
                SetCapsule("Sem nome", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Informe o nome do estado (Name).");
                return;
            }
            name = name.Trim();

            var warnings = new List<string>();
            var parts = new SnapshotParts();
            var bundleItems = new List<IGH_Goo>();
            DA.GetDataList(2, bundleItems);
            VaultInputs.AddBundles(bundleItems, parts, warnings);

            if (DA.GetDataTree(3, out GH_Structure<IGH_Goo> inputs) && Params.Input[3].SourceCount > 0) parts.Inputs = TreeMapper.ToTable(inputs, warnings);
            if (DA.GetDataTree(4, out GH_Structure<IGH_Goo> outputs) && Params.Input[4].SourceCount > 0) parts.Outputs = TreeMapper.ToTable(outputs, warnings);

            bool controls = false;
            DA.GetData(5, ref controls);
            if (controls) parts.Controls.AddRange(ControlStateService.Capture(doc));

            string hub = "";
            DA.GetData(6, ref hub);
            foreach (var kv in HubCapture.Capture(hub, warnings)) parts.HubChannels[kv.Key] = kv.Value;

            string notes = "";
            DA.GetData(7, ref notes);
            parts.Notes = notes;
            var tags = new List<string>();
            DA.GetDataList(8, tags);
            foreach (var t in tags)
            {
                if (!string.IsNullOrWhiteSpace(t)) parts.Tags.Add(t.Trim());
            }
            double runtime = 0;
            if (DA.GetData(9, ref runtime)) parts.RuntimeMs = runtime;
            bool capture = false;
            DA.GetData(10, ref capture);
            bool skip = true;
            DA.GetData(11, ref skip);

            foreach (var kv in EnvironmentInfo.Capture(DocumentName(doc), doc?.DocumentID ?? Guid.Empty)) parts.Environment[kv.Key] = kv.Value;

            var draft = SnapshotCodec.BuildDraft(StoreKinds.Snapshot, name, parts);
            string inHash = draft.Metadata[VaultNames.MetaInputsHash];
            string outHash = draft.Metadata[VaultNames.MetaOutputsHash];
            ReportWarnings(warnings);

            string summary = $"{parts.Parameters.Count} parâmetro(s), {(parts.Inputs != null ? parts.Inputs.ItemCount : 0)} entrada(s), " +
                             $"{(parts.Outputs != null ? parts.Outputs.ItemCount : 0)} saída(s), {parts.Controls.Count} controle(s), {parts.HubChannels.Count} canal(is)";

            if (draft.Trees.Count == 0)
            {
                SetCapsule(name, false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nada a registrar: conecte Parameters, Inputs, Outputs, ative Controls ou informe Hub.");
                return;
            }

            try
            {
                if (!capture)
                {
                    var latest = store.Exists ? store.GetLatest(StoreKinds.Snapshot, name) : null;
                    SetCapsule(name, latest != null, true, latest != null ? $"r{latest.Revision}" : "novo");
                    Message = "Pronto";
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Aguardando 'Capture' = True.");
                    DA.SetData(0, latest?.Reference ?? "");
                    DA.SetData(1, latest != null ? (int)Math.Min(latest.Revision, int.MaxValue) : 0);
                    DA.SetData(2, inHash);
                    DA.SetData(3, outHash);
                    DA.SetData(4, "Será registrado: " + summary);
                    return;
                }

                if (readOnly)
                {
                    SetCapsule(name, false);
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Store aberto como somente leitura.");
                    return;
                }

                var result = store.Append(draft, skip, InstanceGuid);
                var h = result.Header;
                SetCapsule(name, true, false, $"r{h.Revision}");
                Message = result.Written ? $"r{h.Revision} gravado" : $"r{h.Revision} (igual)";
                DA.SetData(0, h.Reference);
                DA.SetData(1, (int)Math.Min(h.Revision, int.MaxValue));
                DA.SetData(2, inHash);
                DA.SetData(3, outHash);
                DA.SetData(4, (result.Written ? $"Snapshot {h.Reference}: " : $"Sem mudança desde {h.Reference}: ") + summary);
            }
            catch (Exception ex)
            {
                SetCapsule(name, false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Falha ao gravar snapshot: {ex.Message}");
            }
        }

        internal static string DocumentName(GH_Document doc)
        {
            if (doc == null) return "";
            return !string.IsNullOrEmpty(doc.FilePath) ? Path.GetFileName(doc.FilePath) : doc.DisplayName;
        }
    }
}
