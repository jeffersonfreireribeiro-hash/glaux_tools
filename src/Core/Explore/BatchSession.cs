using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Buraqueira_Tools.Explore
{
    public enum BatchState
    {
        /// <summary>Nada executado para o plano atual.</summary>
        Idle,

        Running,

        /// <summary>Interrompido (Run desligado, Esc, controle alterado, arquivo fechado); retoma da próxima amostra.</summary>
        Paused,

        Completed
    }

    /// <summary>Uma execução: a amostra aplicada e os resultados lidos na solução seguinte.</summary>
    public sealed class BatchRun
    {
        public BatchRun(int index, double[] values, double[] results, double milliseconds)
        {
            Index = index;
            Values = values ?? Array.Empty<double>();
            Results = results ?? Array.Empty<double>();
            Milliseconds = milliseconds;
        }

        /// <summary>Índice da amostra no plano.</summary>
        public int Index { get; }

        /// <summary>Valores aplicados (escolhas como índice do nível).</summary>
        public double[] Values { get; }

        /// <summary>Resultados numéricos (NaN onde o item não era número).</summary>
        public double[] Results { get; }

        /// <summary>Da aplicação da amostra até o resultado (inclui a solução inteira do documento).</summary>
        public double Milliseconds { get; }
    }

    /// <summary>
    /// Estado de um lote, sem nada do Grasshopper: qual plano está sendo executado, quais amostras já têm resultado,
    /// qual é a próxima, progresso e tempo estimado. Sobrevive a salvar/reabrir o .gh (<see cref="Serialize"/>),
    /// e nunca mistura execuções de planos diferentes.
    /// </summary>
    public sealed class BatchSession
    {
        private const string Header = "glaux-batch 1";
        private readonly SortedDictionary<int, BatchRun> _runs = new SortedDictionary<int, BatchRun>();

        public string PlanHash { get; private set; }
        public int Total { get; private set; }
        public BatchState State { get; private set; } = BatchState.Idle;

        /// <summary>Por que o lote parou (vazio enquanto roda ou quando terminou normalmente).</summary>
        public string StopReason { get; private set; } = "";

        public IEnumerable<BatchRun> Runs => _runs.Values;
        public int DoneCount => _runs.Count;
        public double Progress => Total > 0 ? (double)_runs.Count / Total : 0;
        public bool HasRuns => _runs.Count > 0;

        /// <summary>Próxima amostra sem resultado, ou null se todas já têm.</summary>
        public int? NextIndex
        {
            get
            {
                for (int i = 0; i < Total; i++)
                {
                    if (!_runs.ContainsKey(i)) return i;
                }
                return null;
            }
        }

        public bool IsFor(string planHash) => string.Equals(PlanHash, planHash, StringComparison.Ordinal);

        public double AverageMilliseconds => _runs.Count == 0 ? 0 : _runs.Values.Average(r => r.Milliseconds);

        /// <summary>Tempo restante estimado pela média das execuções feitas (null sem execuções).</summary>
        public TimeSpan? Remaining => _runs.Count == 0 ? (TimeSpan?)null : TimeSpan.FromMilliseconds(AverageMilliseconds * (Total - _runs.Count));

        /// <summary>
        /// Começa ou retoma o lote de um plano. Se já há execuções de outro plano, não faz nada e explica: descartar
        /// resultados é sempre uma ação explícita (<see cref="Reset"/>).
        /// </summary>
        public bool TryStart(string planHash, int total, out string reason)
        {
            reason = null;
            if (total <= 0)
            {
                reason = "O plano não tem amostras.";
                return false;
            }
            if (_runs.Count > 0 && !IsFor(planHash))
            {
                reason = $"O plano mudou desde as {_runs.Count} execuções guardadas. Use Reset para descartá-las e começar o novo plano.";
                return false;
            }
            PlanHash = planHash;
            Total = total;
            StopReason = "";
            State = NextIndex.HasValue ? BatchState.Running : BatchState.Completed;
            return true;
        }

        public void Record(int index, double[] values, double[] results, double milliseconds)
        {
            if (index < 0 || index >= Total) throw new ArgumentOutOfRangeException(nameof(index));
            _runs[index] = new BatchRun(index, values, results, milliseconds);
            if (!NextIndex.HasValue)
            {
                State = BatchState.Completed;
                StopReason = "";
            }
        }

        public void Pause(string reason)
        {
            if (State == BatchState.Running) State = BatchState.Paused;
            StopReason = reason ?? "";
        }

        /// <summary>Descarta as execuções (o plano volta a ser definido no próximo início).</summary>
        public void Reset()
        {
            _runs.Clear();
            PlanHash = null;
            Total = 0;
            State = BatchState.Idle;
            StopReason = "";
        }

        public string StatusText()
        {
            switch (State)
            {
                case BatchState.Running:
                    return $"Rodando {DoneCount}/{Total}";
                case BatchState.Paused:
                    return $"Pausado {DoneCount}/{Total}" + (string.IsNullOrEmpty(StopReason) ? "" : $" ({StopReason})");
                case BatchState.Completed:
                    return $"Concluído {DoneCount}/{Total}";
                default:
                    return "Pronto";
            }
        }

        public static string FormatDuration(TimeSpan t)
        {
            if (t.TotalSeconds < 1) return $"{t.TotalMilliseconds:0} ms";
            if (t.TotalMinutes < 1) return $"{t.TotalSeconds:0.0} s";
            if (t.TotalHours < 1) return $"{(int)t.TotalMinutes} min {t.Seconds:00} s";
            return $"{(int)t.TotalHours} h {t.Minutes:00} min";
        }

        // ------------------------------------------------------------ persistência (.gh)

        /// <summary>Texto compacto e invariante; um lote em andamento volta como pausado.</summary>
        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');
            sb.Append("plan=").Append(PlanHash ?? "").Append('\n');
            sb.Append("total=").Append(Total.ToString(CultureInfo.InvariantCulture)).Append('\n');
            var state = State == BatchState.Running ? BatchState.Paused : State;
            sb.Append("state=").Append(state).Append('\n');
            sb.Append("reason=").Append(State == BatchState.Running ? "arquivo salvo durante a execução" : (StopReason ?? "").Replace('\n', ' ')).Append('\n');
            foreach (var r in _runs.Values)
            {
                sb.Append("run=").Append(r.Index.ToString(CultureInfo.InvariantCulture)).Append(';')
                  .Append(r.Milliseconds.ToString("R", CultureInfo.InvariantCulture)).Append(';')
                  .Append(Join(r.Values)).Append(';')
                  .Append(Join(r.Results)).Append('\n');
            }
            return sb.ToString();
        }

        public static BatchSession Deserialize(string text)
        {
            var s = new BatchSession();
            if (string.IsNullOrEmpty(text)) return s;
            var lines = text.Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != Header) return s;
            foreach (var raw in lines.Skip(1))
            {
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                string key = raw.Substring(0, eq), value = raw.Substring(eq + 1);
                switch (key)
                {
                    case "plan":
                        s.PlanHash = value.Length == 0 ? null : value;
                        break;
                    case "total":
                        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int total);
                        s.Total = Math.Max(0, total);
                        break;
                    case "state":
                        if (Enum.TryParse(value, out BatchState st)) s.State = st == BatchState.Running ? BatchState.Paused : st;
                        break;
                    case "reason":
                        s.StopReason = value;
                        break;
                    case "run":
                        var p = value.Split(';');
                        if (p.Length < 4 || !int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx) || idx < 0) continue;
                        double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double ms);
                        s._runs[idx] = new BatchRun(idx, Split(p[2]), Split(p[3]), ms);
                        break;
                }
            }
            if (s.Total < s._runs.Count) s.Total = s._runs.Keys.Max() + 1;
            return s;
        }

        private static string Join(double[] values) => string.Join(",", values.Select(v => v.ToString("R", CultureInfo.InvariantCulture)));

        private static double[] Split(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<double>();
            return text.Split(',').Select(t => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN).ToArray();
        }
    }
}
