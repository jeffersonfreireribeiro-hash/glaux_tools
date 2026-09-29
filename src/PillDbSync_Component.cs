using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Buraqueira_Tools.Data;
using Buraqueira_Tools.Persistence;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Sincronização controlada Grasshopper ↔ store, com direção explícita, revisão, estado dirty,
    /// política de conflito e proteção contra laços (ver <see cref="SyncStateMachine"/>).
    /// </summary>
    public class PillDbSync_Component : GlauxCapsuleComponent
    {
        private SyncMarker _marker = SyncMarker.None;
        private string _markerScope = "";

        public PillDbSync_Component()
            : base(
                "Pill DB Sync",
                "PillSync",
                "Sincroniza uma DataTree com uma chave do store (.glauxdb) de forma controlada:\n" +
                "- Direction: 0 = Push (Grasshopper → store), 1 = Pull (store → Grasshopper), 2 = Two-Way.\n" +
                "- Estado: Clean, LocalDirty (o dado do GH mudou), StoreAhead (o store tem revisão nova), Conflict (os dois mudaram).\n" +
                "- Conflito (só em Two-Way): 0 = parar e avisar, 1 = prevalece o Grasshopper, 2 = prevalece o store.\n" +
                "- Auto = sincroniza sozinho; senão espera o gatilho 'Sync'.\n" +
                "Sem laços: depois de um Pull o dado local antigo não é reenviado, e dados iguais aos do store não geram revisão.",
                "Data",
                "DB",
                ColorDB)
        {
        }

        public override Guid ComponentGuid => new Guid("6585b5d8-f0a8-47ef-8c42-d7c5b665626c");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;
        protected override Bitmap Icon => GlauxToolsIcons.PillDbSync;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Store", "S", StoreInput.InputDescription, GH_ParamAccess.item);
            pManager.AddTextParameter("Key", "K", "Chave sincronizada.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Local Data", "D", "Dado do Grasshopper (vazio = só receber do store).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Direction", "Dir", "0 = Push, 1 = Pull, 2 = Two-Way.", GH_ParamAccess.item, 2);
            pManager.AddBooleanParameter("Auto", "A", "Sincroniza automaticamente a cada mudança (sem gatilho).", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("Conflict", "C", "Conflito em Two-Way: 0 = parar, 1 = Grasshopper prevalece, 2 = store prevalece.", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Sync", "Go", "Executa a ação pendente (modo manual).", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Kind", "T", "Tipo da entrada (padrão 'dataset').", GH_ParamAccess.item, StoreKinds.Dataset);
            pManager[0].Optional = true;
            for (int i = 2; i < 8; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Dado efetivo: o local, ou o do store quando ele é a referência (após Pull).", GH_ParamAccess.tree);
            pManager.AddTextParameter("State", "St", "Clean, LocalDirty, StoreAhead, Conflict ou Empty.", GH_ParamAccess.item);
            pManager.AddTextParameter("Action", "Ac", "Ação executada nesta solução (Push, Pull, None) ou pendente.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Store Revision", "R", "Revisão do store após a sincronização.", GH_ParamAccess.item);
            pManager.AddTextParameter("Local Hash", "H", "SHA-256 do dado local.", GH_ParamAccess.item);
            pManager.AddTextParameter("Log", "L", "Motivo da decisão.", GH_ParamAccess.item);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            StoreWatch.Unwatch(this);
            base.RemovedFromDocument(document);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!StoreInput.TryResolve(DA, 0, OnPingDocument(), out var store, out bool readOnly, out string error))
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

            GlauxTreeTable local = null;
            var warnings = new List<string>();
            if (DA.GetDataTree(2, out GH_Structure<IGH_Goo> tree) && tree != null && Params.Input[2].SourceCount > 0)
            {
                local = TreeMapper.ToTable(tree, warnings);
            }

            int direction = 2, conflict = 0;
            bool auto = false, trigger = false;
            string kind = StoreKinds.Dataset;
            DA.GetData(3, ref direction);
            DA.GetData(4, ref auto);
            DA.GetData(5, ref conflict);
            DA.GetData(6, ref trigger);
            DA.GetData(7, ref kind);
            if (string.IsNullOrWhiteSpace(kind)) kind = StoreKinds.Dataset;

            var options = new SyncOptions
            {
                Direction = (SyncDirection)Math.Max(0, Math.Min(2, direction)),
                Conflict = (ConflictPolicy)Math.Max(0, Math.Min(2, conflict)),
                Automatic = auto,
                Kind = kind.Trim()
            };
            if (readOnly && options.Direction != SyncDirection.Pull)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Store somente leitura: usando direção Pull.");
                options.Direction = SyncDirection.Pull;
            }

            // O marcador só vale para o mesmo store/tipo/chave
            string scope = $"{store.Path}|{options.Kind}|{key}";
            if (!string.Equals(scope, _markerScope, StringComparison.OrdinalIgnoreCase))
            {
                _marker = SyncMarker.None;
                _markerScope = scope;
            }

            SyncStepResult step;
            try
            {
                step = SyncEngine.Step(store, key, local, _marker, options, trigger, InstanceGuid);
            }
            catch (Exception ex)
            {
                SetCapsule(key, false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Falha na sincronização: {ex.Message}");
                return;
            }
            _marker = step.Marker;

            var decision = step.Decision;
            ReportWarnings(warnings);
            if (decision.Blocked) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, decision.Reason);

            string action = decision.Action != SyncAction.None
                ? decision.Action.ToString()
                : (decision.Pending != SyncAction.None ? $"{decision.Pending} (pendente)" : "None");
            bool clean = decision.State == SyncState.Clean;
            SetCapsule(key, !decision.Blocked, !clean, decision.State.ToString());
            Message = decision.Action != SyncAction.None ? decision.Action.ToString() : decision.State.ToString();

            DA.SetDataTree(0, step.Output != null ? TreeMapper.ToTree(step.Output) : new GH_Structure<IGH_Goo>());
            DA.SetData(1, decision.State.ToString());
            DA.SetData(2, action);
            DA.SetData(3, (int)Math.Min(step.StoreRevision, int.MaxValue));
            DA.SetData(4, step.LocalHash);
            DA.SetData(5, decision.Reason ?? "");
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Esquecer estado de sincronização", (s, e) =>
            {
                RecordUndoEvent("Reiniciar Pill DB Sync");
                _marker = SyncMarker.None;
                ExpireSolution(true);
            });
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetString("SyncScope", _markerScope ?? "");
            writer.SetInt64("SyncStoreRevision", _marker.StoreRevision);
            writer.SetString("SyncStoreHash", _marker.StoreHash ?? "");
            writer.SetString("SyncLocalHash", _marker.LocalHash ?? "");
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("SyncScope"))
            {
                _markerScope = reader.GetString("SyncScope");
                _marker = new SyncMarker
                {
                    StoreRevision = reader.GetInt64("SyncStoreRevision"),
                    StoreHash = reader.GetString("SyncStoreHash"),
                    LocalHash = reader.GetString("SyncLocalHash")
                };
            }
            return base.Read(reader);
        }
    }
}
