using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Buraqueira_Tools.Dashboard
{
    public enum GateDecision
    {
        /// <summary>Nada a fazer (valor igual ao já entregue).</summary>
        None,

        /// <summary>Só redesenhar o valor em pré-visualização.</summary>
        Preview,

        /// <summary>Entregar o valor ao Grasshopper agora.</summary>
        Commit,

        /// <summary>Agendar uma entrega atrasada (fim do intervalo de throttle), para o último valor não se perder.</summary>
        Schedule
    }

    public struct GateResult
    {
        public GateDecision Decision;
        public WidgetValue Value;
        public int DelayMs;
    }

    /// <summary>
    /// Decide quando um arrasto vira solução do Grasshopper, evitando MouseMove → ExpireSolution → MouseMove...
    /// <list type="bullet">
    /// <item>valor igual ao último entregue (depois do passo/decimais) nunca gera solução;</item>
    /// <item>Live: no máximo uma solução por intervalo de throttle, com uma entrega final agendada para o último valor;</item>
    /// <item>Release: só pré-visualização durante o arrasto; entrega ao soltar;</item>
    /// <item>Auto: Live enquanto a última solução medida for leve; vira Release quando ela passa de <see cref="HeavyMs"/>;</item>
    /// <item>o throttle efetivo cresce com o custo da solução (≥ 2 × duração), mantendo a interface responsiva;</item>
    /// <item>ao soltar, o valor final é sempre entregue se diferente do último entregue.</item>
    /// </list>
    /// Tempos em milissegundos de um relógio monotônico fornecido pelo chamador (testável).
    /// </summary>
    public sealed class CommitGate
    {
        public const double DefaultHeavyMs = 150;

        private double _lastCommitAt = double.NegativeInfinity;

        public CommitGate(CommitMode mode, int throttleMs, double heavyMs = DefaultHeavyMs)
        {
            Mode = mode;
            ThrottleMs = Math.Max(0, throttleMs);
            HeavyMs = heavyMs;
        }

        public CommitMode Mode { get; }
        public int ThrottleMs { get; }
        public double HeavyMs { get; }

        /// <summary>Duração da última solução disparada por um commit (medida pelo host).</summary>
        public double LastSolutionMs { get; set; }

        public WidgetValue Committed { get; private set; }
        public WidgetValue Pending { get; private set; }
        public bool TrailingScheduled { get; private set; }
        public int Commits { get; private set; }

        public bool IsLive => Mode == CommitMode.Live || (Mode == CommitMode.Auto && LastSolutionMs <= HeavyMs);

        public int EffectiveThrottleMs => (int)Math.Max(ThrottleMs, Math.Min(2000, LastSolutionMs * 2));

        public bool HasPending => !Pending.IsNone && Pending != Committed;

        public void Begin(WidgetValue committed)
        {
            Committed = committed;
            Pending = committed;
            TrailingScheduled = false;
            Commits = 0;
            _lastCommitAt = double.NegativeInfinity;
        }

        public GateResult Move(WidgetValue value, double nowMs)
        {
            Pending = value;
            if (value == Committed) return new GateResult { Decision = GateDecision.Preview, Value = value };
            if (!IsLive) return new GateResult { Decision = GateDecision.Preview, Value = value };

            double elapsed = nowMs - _lastCommitAt;
            int throttle = EffectiveThrottleMs;
            if (elapsed >= throttle) return DoCommit(value, nowMs);

            if (!TrailingScheduled)
            {
                TrailingScheduled = true;
                return new GateResult { Decision = GateDecision.Schedule, Value = value, DelayMs = (int)Math.Ceiling(Math.Max(1, throttle - elapsed)) };
            }
            return new GateResult { Decision = GateDecision.Preview, Value = value };
        }

        /// <summary>Chamado quando a entrega agendada vence: entrega o último valor pendente, se ainda for diferente.</summary>
        public GateResult TrailingDue(double nowMs)
        {
            TrailingScheduled = false;
            if (!HasPending || !IsLive) return new GateResult { Decision = GateDecision.None, Value = Pending };
            return DoCommit(Pending, nowMs);
        }

        public GateResult End(WidgetValue value, double nowMs)
        {
            Pending = value;
            TrailingScheduled = false;
            if (value.IsNone || value == Committed) return new GateResult { Decision = GateDecision.None, Value = value };
            return DoCommit(value, nowMs);
        }

        private GateResult DoCommit(WidgetValue value, double nowMs)
        {
            Committed = value;
            _lastCommitAt = nowMs;
            Commits++;
            return new GateResult { Decision = GateDecision.Commit, Value = value };
        }
    }

    [Flags]
    public enum DashboardAction
    {
        None = 0,

        /// <summary>Redesenhar o canvas.</summary>
        Repaint = 1,

        /// <summary>O estado mudou: publicar no Hub e recalcular (o controlador já gravou o estado).</summary>
        Commit = 2,

        /// <summary>Agendar <see cref="DashboardController.TrailingDue"/> depois de <see cref="DashboardEffect.DelayMs"/>.</summary>
        ScheduleTrailing = 4,

        OpenOptions = 8,
        EditValue = 16,

        /// <summary>Capturar o mouse (arrasto em andamento).</summary>
        Capture = 32,

        /// <summary>Soltar a captura do mouse.</summary>
        Release = 64,

        /// <summary>O evento foi consumido pelo painel (o componente não deve ser arrastado).</summary>
        Handled = 128
    }

    public sealed class DashboardEffect
    {
        public static readonly DashboardEffect Nothing = new DashboardEffect();

        public DashboardAction Actions { get; internal set; }
        public string WidgetId { get; internal set; }
        public WidgetValue Value { get; internal set; }
        public int DelayMs { get; internal set; }

        /// <summary>Retângulo local do alvo (ex: campo do dropdown, para posicionar o menu).</summary>
        public RectangleF Rect { get; internal set; }

        public bool Has(DashboardAction a) => (Actions & a) == a;
    }

    /// <summary>Fonte de dados ao vivo para indicadores com <c>key=</c> (no Grasshopper: canal do PillHub lido na pintura).</summary>
    public interface IDashboardLiveSource
    {
        bool TryGet(string key, out WidgetValue value, out string status);
    }

    /// <summary>
    /// Controlador do painel, sem dependência do Grasshopper: mantém configuração, estado, widgets e layout, e traduz
    /// eventos do ponteiro em efeitos (commit, redesenho, abrir opções, editar valor) segundo a política de commit.
    /// O host (atributos do componente) executa os efeitos: publicar no PillHub, expirar a solução, mostrar menus.
    /// </summary>
    public sealed class DashboardController
    {
        public const double ToggleDebounceMs = 300;

        private readonly List<DashboardWidget> _widgets = new List<DashboardWidget>();
        private readonly List<DashboardWidget> _visible = new List<DashboardWidget>();
        private readonly Dictionary<string, double> _lastSetAt = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, WidgetValue> _namedData = new Dictionary<string, WidgetValue>(StringComparer.OrdinalIgnoreCase);

        private int _active = -1;
        private int _hover = -1;
        private CommitGate _gate;
        private bool _gestureChanged;

        public DashboardMetrics Metrics { get; set; } = DashboardMetrics.Default;
        public DashboardSpec Spec { get; private set; } = new DashboardSpec();
        public string SpecHash { get; private set; } = "";
        public DashboardState State { get; } = new DashboardState();
        public LayoutResult Layout { get; private set; }
        public IDashboardLiveSource LiveSource { get; set; }

        /// <summary>Chamado uma vez por gesto, antes da primeira mudança de estado (no Grasshopper: RecordUndoEvent).</summary>
        public Action<string> BeforeChange { get; set; }

        public IReadOnlyList<DashboardWidget> Widgets => _widgets;
        public IReadOnlyList<DashboardWidget> Visible => _visible;

        public string LastChangedId { get; set; }
        public double LastSolutionMs { get; private set; }
        public long CommitCount { get; private set; }
        public int ActiveIndex => _active;
        public int HoverIndex => _hover;
        public bool IsCapturing => _active >= 0;

        /// <summary>Troca a configuração; widgets só são recriados quando o hash muda. O estado é preservado por id.</summary>
        public bool SetSpec(DashboardSpec spec)
        {
            spec = spec ?? new DashboardSpec();
            string hash = spec.ComputeHash();
            if (hash == SpecHash && _widgets.Count == spec.Widgets.Count)
            {
                Spec = spec;
                return false;
            }

            string activeId = _active >= 0 && _active < _visible.Count ? _visible[_active].Id : null;
            Spec = spec;
            SpecHash = hash;
            _widgets.Clear();
            _visible.Clear();
            foreach (var w in spec.Widgets) _widgets.Add(DashboardWidgetRegistry.Create(w));

            var order = new List<(DashboardWidget w, int index)>();
            for (int i = 0; i < _widgets.Count; i++)
            {
                if (_widgets[i].Spec.Visible) order.Add((_widgets[i], i));
            }
            order.Sort((a, b) =>
            {
                int c = a.w.Spec.Order.CompareTo(b.w.Spec.Order);
                return c != 0 ? c : a.index.CompareTo(b.index);
            });
            foreach (var o in order) _visible.Add(o.w);

            // Configuração mudou no meio de um arrasto: encerra o gesto sem entregar nada
            if (activeId != null) CancelGesture();
            _hover = -1;
            Layout = null;
            return true;
        }

        /// <summary>Dados nomeados da entrada Data (casados por 'source=', id ou rótulo).</summary>
        public void SetNamedData(IDictionary<string, WidgetValue> data)
        {
            _namedData = data == null
                ? new Dictionary<string, WidgetValue>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, WidgetValue>(data, StringComparer.OrdinalIgnoreCase);
        }

        public LayoutResult Arrange(float headerHeight)
        {
            var items = new List<LayoutItem>(_visible.Count);
            foreach (var w in _visible) items.Add(w.ToLayoutItem(Metrics));
            Layout = DashboardLayoutEngine.Arrange(LayoutOptions.From(Spec, headerHeight), items);
            return Layout;
        }

        public RectangleF RectOf(int visibleIndex)
        {
            if (Layout == null || visibleIndex < 0 || visibleIndex >= Layout.Items.Length) return RectangleF.Empty;
            return Layout.Items[visibleIndex];
        }

        public int IndexOf(string id)
        {
            for (int i = 0; i < _visible.Count; i++)
            {
                if (string.Equals(_visible[i].Id, id, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        /// <summary>Widget interativo e habilitado sob o ponteiro (coordenadas locais do painel), ou -1.</summary>
        public int HitTest(PointF p)
        {
            if (Layout == null) return -1;
            for (int i = 0; i < _visible.Count && i < Layout.Items.Length; i++)
            {
                var w = _visible[i];
                if (w.IsInteractive && w.Spec.Enabled && w.HitTest(Layout.Items[i], p, Metrics)) return i;
            }
            return -1;
        }

        /// <summary>Valor a exibir: controles mostram a pré-visualização do arrasto; indicadores leem Hub → Data → Builder → padrão.</summary>
        public WidgetValue DisplayValue(DashboardWidget w, out bool pending, out string status)
        {
            pending = false;
            status = null;
            var spec = w.Spec;
            if (spec.IsControl)
            {
                int idx = _active;
                if (idx >= 0 && idx < _visible.Count && ReferenceEquals(_visible[idx], w))
                {
                    if (spec.Kind == WidgetKind.Button) return WidgetValue.FromBoolean(true);
                    if (_gate != null && !_gate.Pending.IsNone)
                    {
                        pending = _gate.HasPending;
                        return _gate.Pending;
                    }
                }
                return State.Effective(spec);
            }
            return IndicatorValue(spec, out status);
        }

        public WidgetValue IndicatorValue(WidgetSpec spec, out string status)
        {
            status = null;
            if (spec.HubKey != null)
            {
                if (LiveSource != null && LiveSource.TryGet(spec.HubKey, out var live, out status) && !live.IsNone) return live;
                if (LiveSource == null) status = "sem PillHub";
            }
            if (TryNamed(spec, out var named)) return named;
            if (!spec.EmbeddedValue.IsNone) return spec.EmbeddedValue;
            return spec.Default;
        }

        private bool TryNamed(WidgetSpec spec, out WidgetValue value)
        {
            value = WidgetValue.None;
            if (_namedData.Count == 0) return false;
            if (spec.Source != null) return _namedData.TryGetValue(spec.Source, out value);
            return _namedData.TryGetValue(spec.Id, out value) || (spec.Label.Length > 0 && _namedData.TryGetValue(spec.Label, out value));
        }

        // =================================================================
        // Ponteiro
        // =================================================================

        public DashboardEffect PointerDown(PointF p, double nowMs)
        {
            if (_active >= 0) CancelGesture();
            int idx = HitTest(p);
            if (idx < 0) return DashboardEffect.Nothing;
            var w = _visible[idx];
            var rect = RectOf(idx);
            var current = State.Effective(w.Spec);
            var response = w.OnPointerDown(Pointer(rect, p, current));

            switch (response.Intent)
            {
                case WidgetIntent.Drag:
                {
                    _active = idx;
                    _gestureChanged = false;
                    _gate = new CommitGate(w.Spec.Commit, w.Spec.ThrottleMs) { LastSolutionMs = LastSolutionMs };
                    _gate.Begin(current);
                    var effect = FromGate(w, _gate.Move(response.Value, nowMs));
                    effect.Actions |= DashboardAction.Capture | DashboardAction.Handled | DashboardAction.Repaint;
                    return effect;
                }
                case WidgetIntent.Set:
                {
                    if (_lastSetAt.TryGetValue(w.Id, out double last) && nowMs - last < ToggleDebounceMs)
                    {
                        // Segundo clique de um duplo clique: não desfaz o primeiro
                        return new DashboardEffect { Actions = DashboardAction.Handled, WidgetId = w.Id };
                    }
                    _lastSetAt[w.Id] = nowMs;
                    return Commit(w, response.Value, DashboardAction.Handled | DashboardAction.Repaint);
                }
                case WidgetIntent.Press:
                {
                    _active = idx;
                    _gate = null;
                    // Botão não entra no undo: é uma ação, não um estado
                    return CommitNoUndo(w, response.Value, DashboardAction.Capture | DashboardAction.Handled | DashboardAction.Repaint);
                }
                case WidgetIntent.OpenOptions:
                    return new DashboardEffect
                    {
                        Actions = DashboardAction.OpenOptions | DashboardAction.Handled,
                        WidgetId = w.Id,
                        Value = current,
                        Rect = w is DropdownWidget dd ? dd.FieldRect(rect, Metrics) : rect
                    };
                default:
                    return DashboardEffect.Nothing;
            }
        }

        public DashboardEffect PointerMove(PointF p, double nowMs)
        {
            if (_active >= 0 && _active < _visible.Count)
            {
                var w = _visible[_active];
                if (_gate == null) return new DashboardEffect { Actions = DashboardAction.Handled, WidgetId = w.Id };
                var response = w.OnPointerMove(Pointer(RectOf(_active), p, _gate.Committed));
                if (response.Intent != WidgetIntent.Drag) return new DashboardEffect { Actions = DashboardAction.Handled, WidgetId = w.Id };
                bool changedPreview = response.Value != _gate.Pending;
                var effect = FromGate(w, _gate.Move(response.Value, nowMs));
                effect.Actions |= DashboardAction.Handled;
                if (changedPreview) effect.Actions |= DashboardAction.Repaint;
                return effect;
            }

            int hover = HitTest(p);
            if (hover != _hover)
            {
                _hover = hover;
                return new DashboardEffect { Actions = DashboardAction.Repaint };
            }
            return DashboardEffect.Nothing;
        }

        public DashboardEffect PointerUp(PointF p, double nowMs)
        {
            if (_active < 0 || _active >= _visible.Count)
            {
                _active = -1;
                return DashboardEffect.Nothing;
            }
            var w = _visible[_active];
            DashboardEffect effect;
            if (w.Kind == WidgetKind.Button)
            {
                effect = CommitNoUndo(w, WidgetValue.FromBoolean(false), DashboardAction.Release | DashboardAction.Handled | DashboardAction.Repaint);
            }
            else if (_gate != null)
            {
                var response = w.OnPointerUp(Pointer(RectOf(_active), p, _gate.Committed));
                var final = response.Intent == WidgetIntent.Drag ? response.Value : _gate.Pending;
                effect = FromGate(w, _gate.End(final, nowMs));
                effect.Actions |= DashboardAction.Release | DashboardAction.Handled | DashboardAction.Repaint;
            }
            else
            {
                effect = new DashboardEffect { Actions = DashboardAction.Release | DashboardAction.Handled | DashboardAction.Repaint, WidgetId = w.Id };
            }
            _active = -1;
            _gate = null;
            return effect;
        }

        public DashboardEffect DoubleClick(PointF p, double nowMs)
        {
            int idx = HitTest(p);
            if (idx < 0) return DashboardEffect.Nothing;
            var w = _visible[idx];
            // O segundo clique já iniciou um arrasto: encerra entregando o valor (normalmente o mesmo do primeiro clique)
            DashboardAction carried = DashboardAction.None;
            if (_active >= 0) carried = PointerUp(p, nowMs).Actions & (DashboardAction.Commit | DashboardAction.Release | DashboardAction.Repaint);
            var response = w.OnDoubleClick(Pointer(RectOf(idx), p, State.Effective(w.Spec)));
            if (response.Intent != WidgetIntent.Edit) return new DashboardEffect { Actions = DashboardAction.Handled | carried, WidgetId = w.Id };
            return new DashboardEffect { Actions = DashboardAction.EditValue | DashboardAction.Handled | carried, WidgetId = w.Id, Value = State.Effective(w.Spec), Rect = RectOf(idx) };
        }

        /// <summary>Entrega atrasada agendada pelo throttle.</summary>
        public DashboardEffect TrailingDue(double nowMs)
        {
            if (_gate == null || _active < 0 || _active >= _visible.Count) return DashboardEffect.Nothing;
            return FromGate(_visible[_active], _gate.TrailingDue(nowMs));
        }

        /// <summary>Escolha no menu do dropdown.</summary>
        public DashboardEffect Choose(string widgetId, string option)
        {
            var w = _widgets.FirstOrDefault(x => string.Equals(x.Id, widgetId, StringComparison.OrdinalIgnoreCase));
            if (w == null || w.Kind != WidgetKind.Dropdown) return DashboardEffect.Nothing;
            return Commit(w, WidgetValue.FromText(option), DashboardAction.Repaint);
        }

        /// <summary>Valor digitado (duplo clique) ou definido por código.</summary>
        public DashboardEffect SetValue(string widgetId, WidgetValue value)
        {
            var w = _widgets.FirstOrDefault(x => string.Equals(x.Id, widgetId, StringComparison.OrdinalIgnoreCase));
            if (w == null || !w.Spec.IsControl) return DashboardEffect.Nothing;
            return Commit(w, value, DashboardAction.Repaint);
        }

        /// <summary>O host informa quanto durou a solução disparada por um commit (alimenta o modo Auto e o throttle).</summary>
        public void ReportSolution(double milliseconds)
        {
            LastSolutionMs = Math.Max(0, milliseconds);
            if (_gate != null) _gate.LastSolutionMs = LastSolutionMs;
        }

        /// <summary>O ponteiro saiu do painel (o canvas não avisa a saída; o host verifica na pintura).</summary>
        public bool ClearHover()
        {
            if (_hover < 0) return false;
            _hover = -1;
            return true;
        }

        public void CancelGesture()
        {
            _active = -1;
            _gate = null;
            _gestureChanged = false;
        }

        // =================================================================

        private WidgetPointer Pointer(RectangleF rect, PointF p, WidgetValue current) => new WidgetPointer { Rect = rect, Point = p, Current = current, Metrics = Metrics };

        private DashboardEffect FromGate(DashboardWidget w, GateResult r)
        {
            switch (r.Decision)
            {
                case GateDecision.Commit:
                    return Commit(w, r.Value, DashboardAction.Repaint, gesture: true);
                case GateDecision.Schedule:
                    return new DashboardEffect { Actions = DashboardAction.ScheduleTrailing | DashboardAction.Repaint, WidgetId = w.Id, Value = r.Value, DelayMs = r.DelayMs };
                case GateDecision.Preview:
                    return new DashboardEffect { Actions = DashboardAction.Repaint, WidgetId = w.Id, Value = r.Value };
                default:
                    return new DashboardEffect { WidgetId = w.Id, Value = r.Value };
            }
        }

        private DashboardEffect Commit(DashboardWidget w, WidgetValue value, DashboardAction extra, bool gesture = false)
        {
            var coerced = WidgetValueRules.Coerce(w.Spec, value);
            if (coerced.IsNone || coerced == State.Effective(w.Spec)) return new DashboardEffect { Actions = extra, WidgetId = w.Id, Value = coerced };
            if (!gesture || !_gestureChanged) BeforeChange?.Invoke(w.Spec.Label.Length > 0 ? w.Spec.Label : w.Id);
            if (gesture) _gestureChanged = true;
            return CommitNoUndo(w, coerced, extra);
        }

        private DashboardEffect CommitNoUndo(DashboardWidget w, WidgetValue value, DashboardAction extra)
        {
            if (!State.Set(w.Spec, value)) return new DashboardEffect { Actions = extra, WidgetId = w.Id, Value = value };
            LastChangedId = w.Id;
            CommitCount++;
            return new DashboardEffect { Actions = DashboardAction.Commit | extra, WidgetId = w.Id, Value = State.Effective(w.Spec) };
        }
    }
}
