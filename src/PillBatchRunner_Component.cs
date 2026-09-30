using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Buraqueira_Tools.Data;
using Buraqueira_Tools.Explore;
using Buraqueira_Tools.Persistence;
using Buraqueira_Tools.ProjectState;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Roda um plano de amostras na definição: aplica cada amostra nos controles, espera a solução, lê os resultados
    /// e agenda a próxima. O laço usa <c>GH_Document.ScheduleSolution</c> com callback (a amostra é aplicada antes da
    /// solução agendada), então o canvas continua respondendo e nenhuma solução é disparada de dentro de outra.
    /// </summary>
    public class PillBatchRunner_Component : GlauxCapsuleComponent
    {
        private const string SessionKey = "GlauxBatchSession";
        private const int StoreBatchSize = 20;

        private BatchSession _session = new BatchSession();
        private SamplePlan _plan;
        private int _awaiting = -1;
        private bool _scheduled;
        private int _generation;
        private double[] _appliedValues;
        private Dictionary<string, ControlState> _applied;
        private List<ControlMatch> _initial;
        private readonly Stopwatch _clock = new Stopwatch();
        private bool _lastRun, _lastReset;

        private readonly List<StoreEntryDraft> _pending = new List<StoreEntryDraft>();
        private GlauxFileStore _store;
        private string _experiment;
        private int _storeWritten;
        private string _storeError;
        private Dictionary<string, string> _environment;
        private string _adjusted;

        // Saídas montadas de forma incremental (uma execução nova = um ramo novo, sem refazer tudo)
        private GH_Structure<IGH_Goo> _inputsTree;
        private GH_Structure<GH_Number> _resultsTree;
        private int _treeRuns = -1;
        private int _treeLastIndex = -1;
        private string _treeOwner;

        public PillBatchRunner_Component()
            : base(
                "Pill Batch Runner",
                "Batch",
                "Roda cada amostra do plano na definição e guarda os resultados:\n" +
                "1. ligue o Plan (Pill Sampler) e, em Results, os resultados a medir (qualquer saída que dependa dos controles);\n" +
                "2. ligue Run (toggle): cada amostra é aplicada nos controles, a definição recalcula e os resultados são lidos;\n" +
                "3. desligue Run (ou Esc) para pausar; ligar de novo retoma da próxima amostra. Reset descarta as execuções.\n" +
                "Com Experiment preenchido, cada execução é gravada no store (parâmetros, resultados, controles e tempo): o Pill Restore reaplica qualquer uma no canvas.\n" +
                "As execuções ficam salvas no .gh. Ao terminar ou pausar, os controles voltam aos valores de antes (Restore).",
                "Explore",
                "EXPLORE",
                ColorExplore)
        {
        }

        public override Guid ComponentGuid => new Guid("e2bd43c7-9b2d-40df-a17f-41e0607ebdb5");
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override Bitmap Icon => GlauxToolsIcons.PillBatchRunner;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Plan", "P", "Plano de amostras do Pill Sampler.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Results", "R", "Resultados a registrar em cada execução (números viram colunas; todos os itens, em ordem de ramo).", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Run", "Run", "Liga para rodar (ou retomar); desliga para pausar. Use um toggle.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Reset", "Rst", "Descarta as execuções guardadas (na borda False → True; use um botão).", GH_ParamAccess.item, false);
            pManager.AddGenericParameter("Store", "S", StoreInput.InputDescription, GH_ParamAccess.item);
            pManager.AddTextParameter("Experiment", "E", "Nome do experimento no store (vazio = não grava; as execuções continuam no .gh).", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Settle", "ms", "Espera entre execuções, em ms (para definições que precisam de um tempo extra).", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Restore", "Rs", "Ao terminar ou pausar, volta os controles aos valores de antes do lote.", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Live", "Lv", "Atualiza Inputs/Results a cada execução. Desligado, elas só aparecem com o lote parado: componentes pesados ligados às saídas (ex: Fast Pareto com milhares de linhas) não recalculam a cada passo.", GH_ParamAccess.item, true);
            pManager[1].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Plan", "P", "Plano das execuções (ligue no Pill Sensitivity).", GH_ParamAccess.item);
            pManager.AddGenericParameter("Inputs", "In", "Um ramo {i} por execução com os valores aplicados (i = índice da amostra).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Results", "Res", "Um ramo {i} por execução com os resultados numéricos (NaN onde o item não era número). Pronto para Fast Pareto e Pill Sensitivity.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Runs", "N", "Execuções concluídas.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Progress", "%", "Progresso de 0 a 1 (ligue num Transmitter para um widget progress do Pill Dashboard).", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "St", "Rodando, pausado (e por quê) ou concluído.", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "I", "Tempo por execução, tempo restante estimado e gravação no store.", GH_ParamAccess.item);
        }

        // ------------------------------------------------------------------ ciclo de vida

        public override bool Write(GH_IWriter writer)
        {
            writer.SetString(SessionKey, _session.Serialize());
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists(SessionKey)) _session = BatchSession.Deserialize(reader.GetString(SessionKey));
            _awaiting = -1;
            _plan = null;
            _generation++;
            _treeRuns = -1;
            return base.Read(reader);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            Interrupt("componente removido");
            base.RemovedFromDocument(document);
        }

        public override void DocumentContextChanged(GH_Document document, GH_DocumentContext context)
        {
            if (context == GH_DocumentContext.Close || context == GH_DocumentContext.Unloaded) Interrupt("arquivo fechado");
            base.DocumentContextChanged(document, context);
        }

        private void Interrupt(string reason)
        {
            if (_session.State == BatchState.Running) _session.Pause(reason);
            _awaiting = -1;
            _generation++;
            FlushStore();
        }

        // ------------------------------------------------------------------ solução

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var doc = OnPingDocument();
            object rawPlan = null;
            DA.GetData(0, ref rawPlan);
            var plan = (rawPlan as GH_SamplePlanGoo)?.Value;
            DA.GetDataTree(1, out GH_Structure<IGH_Goo> results);
            bool run = false, reset = false, restore = true, live = true;
            int settle = 0;
            string experiment = "";
            DA.GetData(2, ref run);
            DA.GetData(3, ref reset);
            DA.GetData(5, ref experiment);
            DA.GetData(6, ref settle);
            DA.GetData(7, ref restore);
            DA.GetData(8, ref live);
            settle = Math.Max(1, Math.Min(60000, settle));

            bool runRising = run && !_lastRun;
            bool resetRising = reset && !_lastReset;
            _lastRun = run;
            _lastReset = reset;
            var warnings = new List<string>();

            if (resetRising)
            {
                Stop(doc, "Reset", restore);
                _session.Reset();
                _plan = null;
                _storeWritten = 0;
                _adjusted = null;
                _treeRuns = -1;
            }

            // 1. Resultado da amostra aplicada antes desta solução
            bool wasRunning = _session.State == BatchState.Running;
            if (wasRunning && _awaiting >= 0)
            {
                if (doc != null && doc.AbortRequested)
                {
                    Stop(doc, "Esc", restore);
                }
                else if (!ControlsUnchanged(doc, out string who))
                {
                    Stop(doc, $"'{who}' foi alterado durante o lote", restore);
                }
                else
                {
                    var numbers = Flatten(results);
                    if (numbers.Length == 0) warnings.Add("Results está vazio: as execuções não terão resultados. Ligue as saídas a medir em Results.");
                    int index = _awaiting;
                    _awaiting = -1;
                    _session.Record(index, _appliedValues, numbers, _clock.Elapsed.TotalMilliseconds);
                    QueueStore(index, results, doc, warnings);
                    if (_session.State == BatchState.Completed)
                    {
                        FlushStore();
                        if (restore) ScheduleRestore(doc);
                    }
                }
            }

            // 2. Continua, pausa ou começa
            if (_session.State == BatchState.Running)
            {
                if (!run) Stop(doc, "Run desligado", restore);
                else if (GH_Document.IsEscapeKeyDown()) Stop(doc, "Esc", restore);
                else if (_awaiting < 0 && !_scheduled) ScheduleNext(doc, settle);
            }
            else if (runRising)
            {
                Start(DA, doc, plan, experiment, settle);
            }

            WriteOutputs(DA, plan, warnings, live || _session.State != BatchState.Running);
        }

        private void Start(IGH_DataAccess DA, GH_Document doc, SamplePlan plan, string experiment, int settle)
        {
            if (plan == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Ligue o Plan do Pill Sampler antes de rodar.");
                return;
            }
            if (!_session.TryStart(plan.Hash, plan.Count, out string reason))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, reason);
                return;
            }
            if (_session.State == BatchState.Completed)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Este plano já foi todo executado. Use Reset para rodar de novo.");
                return;
            }

            var current = DesignControls.CaptureById(doc);
            var missing = plan.Space.Variables.Where(v => !current.ContainsKey(v.Id)).Select(v => v.Name).ToList();
            if (missing.Count > 0)
            {
                _session.Pause("controle ausente");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Controle(s) não encontrado(s) neste documento: {string.Join(", ", missing)}.");
                return;
            }

            _store = null;
            _storeError = null;
            _experiment = string.IsNullOrWhiteSpace(experiment) ? null : experiment.Trim();
            if (_experiment != null)
            {
                if (!StoreInput.TryResolve(DA, 4, doc, out var store, out bool readOnly, out string error) || readOnly)
                {
                    _session.Pause("store indisponível");
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, readOnly ? "Store aberto como somente leitura." : error);
                    return;
                }
                _store = store;
            }

            _plan = plan;
            _generation++;
            _initial = plan.Space.Variables.Select(v => new ControlMatch { Saved = current[v.Id], Current = current[v.Id], Compatible = true }).ToList();
            ScheduleNext(doc, settle);
        }

        private void Stop(GH_Document doc, string reason, bool restore)
        {
            if (_session.State == BatchState.Running) _session.Pause(reason);
            _awaiting = -1;
            _generation++;
            FlushStore();
            if (restore) ScheduleRestore(doc);
        }

        private void ScheduleNext(GH_Document doc, int settle)
        {
            var next = _session.NextIndex;
            if (doc == null || !next.HasValue) return;
            int index = next.Value, generation = _generation;
            _scheduled = true;
            doc.ScheduleSolution(settle, d => ApplySample(d, index, generation));
        }

        /// <summary>Roda antes da solução agendada: aplica a amostra, registra o que os controles aceitaram e expira o componente.</summary>
        private void ApplySample(GH_Document doc, int index, int generation)
        {
            // Callback de um lote já parado/reiniciado (ex: Reset com outro plano): não aplica nada
            if (generation != _generation) return;
            _scheduled = false;
            if (_session.State != BatchState.Running || _plan == null || Locked || OnPingDocument() != doc) return;
            if (index < 0 || index >= _plan.Count || _session.NextIndex != index) return;
            var space = _plan.Space;
            var requested = _plan.Values[index];
            var current = DesignControls.CaptureById(doc);
            var matches = new List<ControlMatch>(space.Count);
            for (int j = 0; j < space.Count; j++)
            {
                var v = space.Variables[j];
                if (!current.TryGetValue(v.Id, out var cur))
                {
                    _session.Pause($"'{v.Name}' não existe mais");
                    ExpireSolution(false);
                    return;
                }
                matches.Add(new ControlMatch { Current = cur, Saved = DesignControls.ToSaved(v, requested[j], cur), Compatible = true });
            }

            ControlStateService.ApplyNow(doc, matches);

            var after = DesignControls.CaptureById(doc);
            _applied = new Dictionary<string, ControlState>(StringComparer.OrdinalIgnoreCase);
            var actual = new double[space.Count];
            for (int j = 0; j < space.Count; j++)
            {
                var v = space.Variables[j];
                after.TryGetValue(v.Id, out var a);
                _applied[v.Id] = a;
                actual[j] = DesignControls.ActualValue(v, a, requested[j]);
                if (Math.Abs(actual[j] - requested[j]) > 1e-9 * Math.Max(1, Math.Abs(requested[j])))
                {
                    _adjusted = $"'{v.Name}': o controle ajustou {v.Format(requested[j])} para {v.Format(actual[j])} (faixa ou precisão do controle). As execuções guardam o valor aplicado.";
                }
            }
            _appliedValues = actual;
            _awaiting = index;
            _clock.Restart();
            ExpireSolution(false);
        }

        /// <summary>Os controles continuam com os valores aplicados (ninguém mexeu durante a solução).</summary>
        private bool ControlsUnchanged(GH_Document doc, out string who)
        {
            who = null;
            if (_applied == null) return true;
            var now = DesignControls.CaptureById(doc);
            foreach (var kv in _applied)
            {
                if (kv.Value == null) continue;
                if (!now.TryGetValue(kv.Key, out var s) || !DesignControls.SameValue(kv.Value, s))
                {
                    who = kv.Value.Name;
                    return false;
                }
            }
            return true;
        }

        private void ScheduleRestore(GH_Document doc)
        {
            var initial = _initial;
            _initial = null;
            if (initial == null || doc == null) return;
            doc.ScheduleSolution(1, d =>
            {
                var current = DesignControls.CaptureById(d);
                var matches = new List<ControlMatch>();
                foreach (var m in initial)
                {
                    if (current.TryGetValue(m.Current.Id, out var cur)) matches.Add(new ControlMatch { Current = cur, Saved = m.Saved, Compatible = true });
                }
                ControlStateService.ApplyNow(d, matches);
            });
        }

        private static double[] Flatten(GH_Structure<IGH_Goo> tree)
        {
            if (tree == null) return Array.Empty<double>();
            var list = new List<double>();
            foreach (var branch in tree.Branches)
            {
                foreach (var goo in branch) list.Add(DesignControls.ToNumber(goo));
            }
            return list.ToArray();
        }

        // ------------------------------------------------------------------ store

        private void QueueStore(int index, GH_Structure<IGH_Goo> results, GH_Document doc, List<string> warnings)
        {
            if (_store == null || _experiment == null || _plan == null) return;
            var parts = new SnapshotParts();
            var space = _plan.Space;
            var items = new List<GlauxValue>(space.Count);
            for (int j = 0; j < space.Count; j++)
            {
                var v = space.Variables[j];
                double value = _appliedValues[j];
                var gv = v.Type == DesignVariableType.Choice && !v.NumericLevels ? GlauxValue.FromText(v.Format(value)) : GlauxValue.FromNumber(v.NumericValue(value));
                items.Add(gv);
                var single = new GlauxTreeTable();
                single.Branches.Add(new GlauxBranch(new[] { 0 }, new List<GlauxValue> { gv }));
                parts.Parameters[v.Name] = single;
            }
            var inputs = new GlauxTreeTable();
            inputs.Branches.Add(new GlauxBranch(new[] { 0 }, items));
            parts.Inputs = inputs;
            if (results != null && results.DataCount > 0) parts.Outputs = TreeMapper.ToTable(results, warnings);
            if (_applied != null)
            {
                foreach (var s in _applied.Values)
                {
                    if (s != null) parts.Controls.Add(s);
                }
            }
            parts.Config["method"] = SamplerOptions.MethodName(_plan.Method);
            parts.Config["sample"] = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            parts.Config["plan"] = _plan.Hash.Substring(0, Math.Min(12, _plan.Hash.Length));
            parts.Config["samples"] = _plan.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            parts.RuntimeMs = _clock.Elapsed.TotalMilliseconds;
            parts.Tags.Add("batch");
            if (_environment == null) _environment = EnvironmentInfo.Capture(PillSnapshot_Component.DocumentName(doc), doc?.DocumentID ?? Guid.Empty);
            foreach (var kv in _environment) parts.Environment[kv.Key] = kv.Value;
            _pending.Add(SnapshotCodec.BuildDraft(StoreKinds.Experiment, _experiment, parts, resultsAsOutputs: true));
            if (_pending.Count >= StoreBatchSize) FlushStore();
        }

        private void FlushStore()
        {
            if (_pending.Count == 0 || _store == null) return;
            try
            {
                _store.AppendBatch(_pending.ToArray(), false, InstanceGuid);
                _storeWritten += _pending.Count;
                _pending.Clear();
                _storeError = null;
            }
            catch (Exception ex)
            {
                _storeError = $"Falha ao gravar no store: {ex.Message} ({_pending.Count} execução(ões) aguardando).";
            }
        }

        // ------------------------------------------------------------------ saídas

        /// <summary>
        /// Mantém as árvores de saída em dia: acrescenta só as execuções novas (índices crescentes, como o lote grava);
        /// qualquer outra mudança (Reset, arquivo reaberto, outro plano, execução fora de ordem) refaz tudo.
        /// </summary>
        private void UpdateTrees(SamplePlan owner)
        {
            string ownerHash = owner?.Hash ?? "";
            var runs = _session.Runs.ToList();
            bool rebuild = _inputsTree == null || _treeRuns < 0 || _treeRuns > runs.Count || _treeOwner != ownerHash;
            // As execuções já nas árvores precisam continuar sendo as primeiras (nenhuma entrou antes delas)
            if (!rebuild && _treeRuns > 0 && runs[_treeRuns - 1].Index != _treeLastIndex) rebuild = true;
            if (rebuild)
            {
                _inputsTree = new GH_Structure<IGH_Goo>();
                _resultsTree = new GH_Structure<GH_Number>();
                _treeRuns = 0;
                _treeOwner = ownerHash;
            }
            var space = owner?.Space;
            for (int k = _treeRuns; k < runs.Count; k++)
            {
                var r = runs[k];
                var path = new GH_Path(r.Index);
                for (int j = 0; j < r.Values.Length; j++)
                {
                    var v = space != null && j < space.Count ? space.Variables[j] : null;
                    _inputsTree.Append(DesignControls.Display(v, r.Values[j]), path);
                }
                _resultsTree.EnsurePath(path);
                foreach (var y in r.Results) _resultsTree.Append(new GH_Number(y), path);
            }
            _treeRuns = runs.Count;
            _treeLastIndex = runs.Count > 0 ? runs[runs.Count - 1].Index : -1;
        }

        private void WriteOutputs(IGH_DataAccess DA, SamplePlan input, List<string> warnings, bool publishRuns)
        {
            // Plano a que as execuções pertencem (para formatar e para a sensibilidade)
            SamplePlan owner = _plan;
            if (owner == null && input != null && (_session.IsFor(input.Hash) || !_session.HasRuns)) owner = input;
            bool stale = input != null && _session.HasRuns && !_session.IsFor(input.Hash);
            if (stale)
            {
                warnings.Add(_session.State == BatchState.Running
                    ? "O plano mudou durante o lote; a mudança vale depois do Reset."
                    : $"O plano ligado não é o das {_session.DoneCount} execuções guardadas. Use Reset para descartá-las ou religue o plano original.");
            }
            if (_storeError != null) AddRuntimeMessage(GH_RuntimeMessageLevel.Error, _storeError);
            ReportWarnings(warnings);
            if (_adjusted != null) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, _adjusted);

            UpdateTrees(owner);

            string status = _session.StatusText();
            var info = new List<string>();
            if (owner != null) info.Add(owner.Describe());
            if (_session.HasRuns)
            {
                double totalMs = _session.Runs.Sum(r => r.Milliseconds);
                string line = $"{_session.DoneCount}/{_session.Total} execuções · média {BatchSession.FormatDuration(TimeSpan.FromMilliseconds(_session.AverageMilliseconds))} · total {BatchSession.FormatDuration(TimeSpan.FromMilliseconds(totalMs))}";
                if (_session.State != BatchState.Completed && _session.Remaining.HasValue) line += $" · faltam ~{BatchSession.FormatDuration(_session.Remaining.Value)}";
                info.Add(line);
            }
            if (_experiment != null && _store != null) info.Add($"Store: experimento '{_experiment}', {_storeWritten} gravada(s)" + (_pending.Count > 0 ? $", {_pending.Count} no buffer" : ""));
            else if (_experiment == null && _session.HasRuns) info.Add("Sem Experiment: as execuções ficam só no .gh.");

            if (!publishRuns) info.Add("Live desligado: Inputs/Results aparecem quando o lote parar.");
            SetCapsule(status, _session.State != BatchState.Idle || owner != null, _session.State == BatchState.Paused || stale);
            Message = status;

            DA.SetData(0, owner != null ? new GH_SamplePlanGoo(owner) : null);
            if (publishRuns)
            {
                DA.SetDataTree(1, _inputsTree);
                DA.SetDataTree(2, _resultsTree);
            }
            DA.SetData(3, _session.DoneCount);
            DA.SetData(4, _session.Progress);
            DA.SetData(5, status);
            DA.SetData(6, string.Join("\n", info));
        }
    }
}
