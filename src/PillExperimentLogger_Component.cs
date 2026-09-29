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
    /// Registra execuções de simulações/otimizações (entrada → configuração → versão → resultado → tempo) como
    /// revisões de um experimento, com gravação em lote para loops de alta frequência.
    /// </summary>
    public class PillExperimentLogger_Component : GlauxCapsuleComponent
    {
        private readonly List<StoreEntryDraft> _pending = new List<StoreEntryDraft>();
        private GlauxFileStore _pendingStore;
        private string _lastRunHash = "";
        private int _loggedThisSession;
        private long _lastRevision;
        private Dictionary<string, string> _environment;

        public PillExperimentLogger_Component()
            : base(
                "Pill Experiment Logger",
                "PillExpLog",
                "Registra cada execução de uma simulação ou otimização (Wallacei, Galapagos, estudos paramétricos) como uma revisão do experimento no store:\n" +
                "entrada (parâmetros/árvore) → configuração → versões → resultado → tempo de execução.\n" +
                "- Com 'Log' ligado, cada solução com entradas/resultados novos vira uma execução (duplicatas consecutivas são ignoradas).\n" +
                "- 'Buffer' agrupa N execuções numa gravação só (lote) para loops rápidos; o buffer é gravado também ao fechar o documento.\n" +
                "Depois, use Pill DB Query (Tree 'result') para achar as execuções com um resultado e Pill Restore/History para ver com quais parâmetros ele foi produzido.",
                "Vault",
                "VAULT",
                ColorVault)
        {
        }

        public override Guid ComponentGuid => new Guid("04035cb4-dd6f-4df7-8683-c2012511c8ee");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;
        protected override Bitmap Icon => GlauxToolsIcons.PillExperimentLogger;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Store", "S", StoreInput.InputDescription, GH_ParamAccess.item);
            pManager.AddTextParameter("Experiment", "E", "Nome do experimento (cada execução vira uma revisão desta chave).", GH_ParamAccess.item);
            pManager.AddGenericParameter("Parameters", "P", "PillBundle(s) com os parâmetros da execução.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Inputs", "In", "Árvore de entradas da execução (alternativa ou complemento aos parâmetros).", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Results", "Res", "Árvore de resultados/objetivos da execução.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Config", "Cfg", "Configuração 'chave=valor' (ex: 'solver=raytracing', 'raios=10000').", GH_ParamAccess.list);
            pManager.AddNumberParameter("Runtime", "ms", "Tempo da execução em ms.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Log", "L", "Registra quando True (toggle ligado durante a otimização).", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("Buffer", "B", "Execuções acumuladas antes de gravar em lote (1 = grava cada uma).", GH_ParamAccess.item, 1);
            pManager.AddBooleanParameter("Skip Duplicates", "SD", "Ignora uma execução idêntica (entradas e resultados) à anterior.", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Flush", "F", "Grava imediatamente o que estiver no buffer.", GH_ParamAccess.item, false);
            pManager[0].Optional = true;
            for (int i = 2; i < 11; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Run", "Run", "Número (revisão) da última execução gravada.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Logged", "N", "Execuções registradas nesta sessão.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Pending", "Pd", "Execuções no buffer aguardando gravação.", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "I", "Resumo.", GH_ParamAccess.item);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            FlushPending();
            base.RemovedFromDocument(document);
        }

        public override void DocumentContextChanged(GH_Document document, GH_DocumentContext context)
        {
            if (context == GH_DocumentContext.Close || context == GH_DocumentContext.Unloaded) FlushPending();
            base.DocumentContextChanged(document, context);
        }

        /// <summary>Grava o buffer (chamado também pelo PillHub ao fechar o documento).</summary>
        internal int FlushPending()
        {
            if (_pending.Count == 0 || _pendingStore == null) return 0;
            try
            {
                var results = _pendingStore.AppendBatch(_pending.ToArray(), false, InstanceGuid);
                if (results.Count > 0) _lastRevision = results[results.Count - 1].Header.Revision;
                int n = _pending.Count;
                _pending.Clear();
                return n;
            }
            catch
            {
                return 0;
            }
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

            string experiment = "";
            if (!DA.GetData(1, ref experiment) || string.IsNullOrWhiteSpace(experiment))
            {
                SetCapsule("Sem experimento", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Informe o nome do experimento.");
                return;
            }
            experiment = experiment.Trim();

            bool log = false, skipDuplicates = true, flush = false;
            int buffer = 1;
            DA.GetData(7, ref log);
            DA.GetData(8, ref buffer);
            DA.GetData(9, ref skipDuplicates);
            DA.GetData(10, ref flush);
            buffer = Math.Max(1, Math.Min(10000, buffer));

            // Mudou de store: grava o que era do anterior antes de trocar
            if (_pendingStore != null && !ReferenceEquals(_pendingStore, store)) FlushPending();
            _pendingStore = store;

            var warnings = new List<string>();
            string status = "Em espera";
            if (log)
            {
                if (readOnly)
                {
                    SetCapsule(experiment, false);
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Store aberto como somente leitura.");
                    return;
                }

                var parts = new SnapshotParts();
                var bundles = new List<IGH_Goo>();
                DA.GetDataList(2, bundles);
                VaultInputs.AddBundles(bundles, parts, warnings);
                if (DA.GetDataTree(3, out GH_Structure<IGH_Goo> inputs) && Params.Input[3].SourceCount > 0) parts.Inputs = TreeMapper.ToTable(inputs, warnings);
                if (DA.GetDataTree(4, out GH_Structure<IGH_Goo> results) && Params.Input[4].SourceCount > 0) parts.Outputs = TreeMapper.ToTable(results, warnings);
                var config = new List<string>();
                DA.GetDataList(5, config);
                foreach (var kv in ParseKeyValues(config)) parts.Config[kv.Key] = kv.Value;
                double runtime = 0;
                if (DA.GetData(6, ref runtime)) parts.RuntimeMs = runtime;

                if (_environment == null) _environment = EnvironmentInfo.Capture(PillSnapshot_Component.DocumentName(doc), doc?.DocumentID ?? Guid.Empty);
                foreach (var kv in _environment) parts.Environment[kv.Key] = kv.Value;

                var draft = SnapshotCodec.BuildDraft(StoreKinds.Experiment, experiment, parts, resultsAsOutputs: true);
                string runHash = draft.Metadata[VaultNames.MetaInputsHash] + "|" + draft.Metadata[VaultNames.MetaOutputsHash];

                if (draft.Trees.Count == 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nada a registrar: conecte Parameters, Inputs ou Results.");
                }
                else if (skipDuplicates && runHash == _lastRunHash)
                {
                    status = "Repetida (ignorada)";
                }
                else
                {
                    _lastRunHash = runHash;
                    _pending.Add(draft);
                    _loggedThisSession++;
                    status = "Registrada";
                }
            }

            if (_pending.Count >= buffer || (flush && _pending.Count > 0))
            {
                try
                {
                    var written = store.AppendBatch(_pending.ToArray(), false, InstanceGuid);
                    if (written.Count > 0) _lastRevision = written[written.Count - 1].Header.Revision;
                    _pending.Clear();
                }
                catch (Exception ex)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Falha ao gravar execuções: {ex.Message} ({_pending.Count} no buffer).");
                }
            }

            if (_lastRevision == 0 && store.Exists)
            {
                var latest = store.GetLatest(StoreKinds.Experiment, experiment);
                if (latest != null) _lastRevision = latest.Revision;
            }

            ReportWarnings(warnings);
            SetCapsule(experiment, true, _pending.Count > 0 || !log, $"run {_lastRevision}");
            Message = log ? $"{status} · {_pending.Count} no buffer" : "Log desligado";

            DA.SetData(0, (int)Math.Min(_lastRevision, int.MaxValue));
            DA.SetData(1, _loggedThisSession);
            DA.SetData(2, _pending.Count);
            DA.SetData(3, $"{experiment}: última execução gravada {_lastRevision}, {_loggedThisSession} nesta sessão, {_pending.Count} no buffer (lote de {buffer}).");
        }
    }
}
