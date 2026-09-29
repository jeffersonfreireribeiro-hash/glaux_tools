using System;
using System.Collections.Generic;
using System.Linq;

namespace Buraqueira_Tools.Dashboard
{
    /// <summary>
    /// Estado de execução dos controles (valor do slider, toggle, opção escolhida), separado da configuração.
    /// É o que vai para o arquivo .gh, para o undo, para o Pill Preset Vault e para os snapshots do Project Vault.
    /// Valores de indicadores (número, progresso, série) vêm dos dados e nunca são gravados aqui.
    /// Entradas de widgets que sumiram da configuração são mantidas (órfãs) para voltar se o widget reaparecer.
    /// </summary>
    public sealed class DashboardState
    {
        public const int MaxOrphans = 200;

        private readonly Dictionary<string, WidgetValue> _values = new Dictionary<string, WidgetValue>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _insertion = new List<string>();

        public int Count => _values.Count;

        public bool TryGetRaw(string id, out WidgetValue value) => _values.TryGetValue(id ?? "", out value);

        /// <summary>Valor efetivo: o estado (se válido para a configuração atual) ou o padrão da configuração.</summary>
        public WidgetValue Effective(WidgetSpec spec)
        {
            if (spec == null) return WidgetValue.None;
            if (spec.IsControl && _values.TryGetValue(spec.Id, out var raw))
            {
                var coerced = WidgetValueRules.Coerce(spec, raw);
                if (!coerced.IsNone) return coerced;
            }
            return spec.Default;
        }

        /// <summary>Grava o valor (convertido para as regras do widget). Devolve true se o valor efetivo mudou.</summary>
        public bool Set(WidgetSpec spec, WidgetValue value)
        {
            if (spec == null || !spec.IsControl) return false;
            var coerced = WidgetValueRules.Coerce(spec, value);
            if (coerced.IsNone) return false;
            var before = Effective(spec);
            Store(spec.Id, coerced);
            return before != coerced;
        }

        public void Remove(string id)
        {
            if (id != null && _values.Remove(id)) _insertion.RemoveAll(k => string.Equals(k, id, StringComparison.OrdinalIgnoreCase));
        }

        public void Clear()
        {
            _values.Clear();
            _insertion.Clear();
        }

        /// <summary>Volta os controles da configuração ao padrão (mantém órfãos).</summary>
        public int ResetToDefaults(DashboardSpec spec)
        {
            int n = 0;
            foreach (var w in spec.Widgets)
            {
                if (!w.IsControl || !_values.ContainsKey(w.Id)) continue;
                if (Effective(w) != w.Default) n++;
                Remove(w.Id);
            }
            return n;
        }

        /// <summary>Linhas <c>id=valor</c> dos controles persistentes da configuração (saída State e presets).</summary>
        public List<string> ToLines(DashboardSpec spec)
        {
            var lines = new List<string>();
            foreach (var w in spec.Widgets)
            {
                if (!w.IsPersistent) continue;
                lines.Add(w.Id + "=" + Effective(w).ToInvariantString());
            }
            return lines;
        }

        /// <summary>Tudo o que está guardado (inclusive órfãos), para gravar no .gh.</summary>
        public List<string> ToRawLines()
        {
            var lines = new List<string>(_insertion.Count);
            foreach (var id in _insertion)
            {
                if (_values.TryGetValue(id, out var v)) lines.Add(id + "=" + TypeTag(v) + ":" + v.ToInvariantString());
            }
            return lines;
        }

        /// <summary>Lê o que <see cref="ToRawLines"/> gravou.</summary>
        public void LoadRaw(IEnumerable<string> lines)
        {
            Clear();
            if (lines == null) return;
            foreach (var line in lines)
            {
                if (!SplitLine(line, out string id, out string text)) continue;
                WidgetValue v;
                if (text.Length > 2 && text[1] == ':')
                {
                    string payload = text.Substring(2);
                    switch (text[0])
                    {
                        case 'n':
                            v = DashboardText.TryParseNumber(payload, out double d) ? WidgetValue.FromNumber(d) : WidgetValue.FromText(payload);
                            break;
                        case 'b':
                            v = DashboardText.TryParseBoolean(payload, out bool b) ? WidgetValue.FromBoolean(b) : WidgetValue.FromText(payload);
                            break;
                        default:
                            v = WidgetValue.FromText(payload);
                            break;
                    }
                }
                else
                {
                    v = WidgetValue.FromText(text);
                }
                Store(id, v);
            }
        }

        /// <summary>
        /// Aplica linhas <c>id=valor</c> vindas de fora (entrada Load State, Preset Vault, Pill Restore).
        /// Só altera controles persistentes que existem na configuração; devolve os ids que mudaram.
        /// Valores inválidos (fora das opções, texto em slider) viram avisos.
        /// </summary>
        public List<string> Apply(IEnumerable<string> lines, DashboardSpec spec, ICollection<string> warnings = null)
        {
            var changed = new List<string>();
            if (lines == null || spec == null) return changed;
            foreach (var block in lines)
            {
                if (block == null) continue;
                foreach (var line in block.Replace("\r\n", "\n").Split('\n'))
                {
                    if (!SplitLine(line, out string id, out string text)) continue;
                    var w = spec.Find(id);
                    if (w == null)
                    {
                        warnings?.Add($"estado '{id}': widget não existe neste painel.");
                        continue;
                    }
                    if (!w.IsPersistent)
                    {
                        warnings?.Add($"estado '{id}': {WidgetKinds.Name(w.Kind)} não guarda estado.");
                        continue;
                    }
                    var raw = WidgetValue.FromText(text);
                    if (WidgetValueRules.Coerce(w, raw).IsNone)
                    {
                        warnings?.Add($"estado '{id}': valor '{text}' inválido para {WidgetKinds.Name(w.Kind)}.");
                        continue;
                    }
                    if (Set(w, raw)) changed.Add(w.Id);
                }
            }
            return changed;
        }

        private void Store(string id, WidgetValue value)
        {
            // Mais recente no fim: o corte de órfãos (Trim) descarta primeiro o que não é tocado há mais tempo
            if (_values.ContainsKey(id)) _insertion.RemoveAll(k => string.Equals(k, id, StringComparison.OrdinalIgnoreCase));
            _insertion.Add(id);
            _values[id] = value;
            if (_insertion.Count > MaxOrphans * 2) Trim();
        }

        private void Trim()
        {
            // Mantém as entradas mais recentes; o limite só é atingido com centenas de widgets renomeados
            int drop = _insertion.Count - MaxOrphans;
            for (int i = 0; i < drop; i++) _values.Remove(_insertion[i]);
            _insertion.RemoveRange(0, drop);
        }

        private static string TypeTag(WidgetValue v)
        {
            switch (v.Kind)
            {
                case WidgetValueKind.Number: return "n";
                case WidgetValueKind.Boolean: return "b";
                default: return "t";
            }
        }

        private static bool SplitLine(string line, out string id, out string value)
        {
            id = null;
            value = null;
            if (string.IsNullOrWhiteSpace(line)) return false;
            string t = line.Trim();
            if (t.StartsWith("#", StringComparison.Ordinal)) return false;
            int eq = t.IndexOf('=');
            if (eq <= 0) return false;
            id = t.Substring(0, eq).Trim();
            value = t.Substring(eq + 1).Trim();
            return id.Length > 0;
        }

        public IEnumerable<string> Ids => _insertion.ToList();
    }
}
