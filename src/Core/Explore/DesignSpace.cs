using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Buraqueira_Tools.Data;

namespace Buraqueira_Tools.Explore
{
    /// <summary>Contínua (faixa, com passo opcional) ou escolha entre níveis (toggle, value list, dropdown, lista de valores).</summary>
    public enum DesignVariableType
    {
        Continuous,
        Choice
    }

    /// <summary>
    /// Uma variável do espaço de projeto, ligada a um controle do canvas pelo mesmo Id do Project Vault
    /// (<c>ControlState.Id</c>). Toda amostra nasce como coordenada unitária u ∈ [0, 1] e é convertida aqui para o
    /// valor do controle; para variáveis discretas (passo ou níveis) cada valor possível ocupa uma faixa igual de u,
    /// então amostragens uniformes continuam uniformes depois da conversão.
    /// </summary>
    public sealed class DesignVariable
    {
        private DesignVariable(string id, string kind, string name, DesignVariableType type, double min, double max, double step, IList<string> levels)
        {
            Id = id ?? "";
            Kind = kind ?? "";
            Name = string.IsNullOrWhiteSpace(name) ? Id : name.Trim();
            Type = type;
            Min = min;
            Max = max;
            Step = step;
            Levels = levels == null ? Array.Empty<string>() : levels.ToArray();
            Decimals = step > 0 ? Math.Max(DecimalsOf(step), DecimalsOf(min)) : -1;
            NumericLevels = type == DesignVariableType.Choice && Levels.Count > 0 && Levels.All(l => TryParseNumber(l, out _));
        }

        /// <summary>Id do controle (<c>ControlState.Id</c>): GUID do objeto ou "guidDoDono|chave".</summary>
        public string Id { get; }

        /// <summary>Tipo do controle (<c>ControlKinds</c>).</summary>
        public string Kind { get; }

        public string Name { get; }
        public DesignVariableType Type { get; }
        public double Min { get; }
        public double Max { get; }

        /// <summary>Passo dos valores (0 = contínuo). Valores possíveis: Min, Min + Step, ... até Max.</summary>
        public double Step { get; }

        public IReadOnlyList<string> Levels { get; }

        /// <summary>Casas decimais usadas para arredondar os valores com passo (-1 = sem arredondamento).</summary>
        public int Decimals { get; }

        /// <summary>Todos os níveis são números (ex: "levels=2;4;8" num slider).</summary>
        public bool NumericLevels { get; }

        public bool IsDiscrete => Type == DesignVariableType.Choice || Step > 0;

        /// <summary>Quantidade de valores possíveis (<see cref="int.MaxValue"/> para contínua sem passo).</summary>
        public int ValueCount
        {
            get
            {
                if (Type == DesignVariableType.Choice) return Levels.Count;
                if (Step <= 0) return Max > Min ? int.MaxValue : 1;
                double n = Math.Floor((Max - Min) / Step + 1e-9) + 1;
                return n >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)n);
            }
        }

        public static DesignVariable Continuous(string id, string kind, string name, double min, double max, double step = 0)
        {
            if (double.IsNaN(min) || double.IsNaN(max) || double.IsInfinity(min) || double.IsInfinity(max)) throw new ArgumentException($"Faixa inválida em '{name}'.");
            if (max < min) (min, max) = (max, min);
            if (double.IsNaN(step) || step < 0) step = 0;
            return new DesignVariable(id, kind, name, DesignVariableType.Continuous, min, max, step, null);
        }

        public static DesignVariable Choice(string id, string kind, string name, IList<string> levels)
        {
            if (levels == null || levels.Count == 0) throw new ArgumentException($"'{name}' não tem níveis.");
            return new DesignVariable(id, kind, name, DesignVariableType.Choice, 0, levels.Count - 1, 0, levels);
        }

        /// <summary>Valor do controle para a coordenada unitária <paramref name="u"/> (escolha → índice do nível).</summary>
        public double FromUnit(double u)
        {
            if (double.IsNaN(u)) u = 0;
            u = u < 0 ? 0 : u > 1 ? 1 : u;
            if (Type == DesignVariableType.Choice)
            {
                return Math.Min((int)Math.Floor(u * Levels.Count), Levels.Count - 1);
            }
            if (Step > 0)
            {
                int count = ValueCount;
                long k = Math.Min((long)Math.Floor(u * count), count - 1L);
                return Snap(Min + k * Step);
            }
            return Min + u * (Max - Min);
        }

        /// <summary>Coordenada unitária de um valor (centro da faixa do valor, para variáveis discretas).</summary>
        public double ToUnit(double value)
        {
            if (Type == DesignVariableType.Choice)
            {
                int idx = (int)Math.Round(value);
                idx = Math.Max(0, Math.Min(Levels.Count - 1, idx));
                return (idx + 0.5) / Levels.Count;
            }
            if (Step > 0)
            {
                int count = ValueCount;
                long k = (long)Math.Round((value - Min) / Step);
                k = Math.Max(0, Math.Min(count - 1L, k));
                return (k + 0.5) / count;
            }
            return Max > Min ? Math.Max(0, Math.Min(1, (value - Min) / (Max - Min))) : 0.5;
        }

        /// <summary>Unitária do k-ésimo valor discreto (0 ≤ k &lt; <see cref="ValueCount"/>).</summary>
        public double UnitOfIndex(long k)
        {
            int count = ValueCount;
            return (Math.Max(0, Math.Min(count - 1L, k)) + 0.5) / count;
        }

        /// <summary>Número usado nas análises: o próprio valor; nível numérico; ou o índice do nível.</summary>
        public double NumericValue(double value)
        {
            if (Type != DesignVariableType.Choice) return value;
            int idx = Math.Max(0, Math.Min(Levels.Count - 1, (int)Math.Round(value)));
            return NumericLevels && TryParseNumber(Levels[idx], out double v) ? v : idx;
        }

        /// <summary>Texto do nível (escolha) ou número invariante.</summary>
        public string Format(double value)
        {
            if (Type == DesignVariableType.Choice)
            {
                int idx = Math.Max(0, Math.Min(Levels.Count - 1, (int)Math.Round(value)));
                return Levels[idx];
            }
            return FormatNumber(value);
        }

        public string Describe()
        {
            if (Type == DesignVariableType.Choice)
            {
                return $"{Name} {{{string.Join(", ", Levels)}}}";
            }
            string range = $"{Name} [{FormatNumber(Min)} … {FormatNumber(Max)}]";
            return Step > 0 ? $"{range} passo {FormatNumber(Step)} ({ValueCount} valores)" : range;
        }

        /// <summary>Aplica uma sobreposição de faixa (min/max/passo/níveis) e devolve a variável resultante.</summary>
        public DesignVariable With(DesignRangeOverride o, ICollection<string> warnings = null)
        {
            if (o == null) return this;
            if (o.Levels != null && o.Levels.Count > 0)
            {
                return Choice(Id, Kind, Name, o.Levels);
            }
            if (Type == DesignVariableType.Choice)
            {
                if (o.Min.HasValue || o.Max.HasValue || o.Step.HasValue) warnings?.Add($"'{Name}' é uma escolha entre níveis: min/max/step ignorados (use levels=).");
                return this;
            }
            double min = o.Min ?? Min, max = o.Max ?? Max, step = o.Step ?? Step;
            if (max < min)
            {
                warnings?.Add($"'{Name}': min > max; os valores foram trocados.");
            }
            return Continuous(Id, Kind, Name, min, max, step);
        }

        internal string Canonical()
        {
            var sb = new StringBuilder();
            sb.Append(Id).Append('\u0001').Append(Kind).Append('\u0001').Append(Name).Append('\u0001').Append(Type);
            sb.Append('\u0001').Append(Min.ToString("R", CultureInfo.InvariantCulture));
            sb.Append('\u0001').Append(Max.ToString("R", CultureInfo.InvariantCulture));
            sb.Append('\u0001').Append(Step.ToString("R", CultureInfo.InvariantCulture));
            foreach (var l in Levels) sb.Append('\u0001').Append(l);
            return sb.ToString();
        }

        private double Snap(double v)
        {
            if (v > Max) v = Max;
            return Decimals >= 0 ? Math.Round(v, Decimals) : v;
        }

        public static string FormatNumber(double v) => v.ToString("G10", CultureInfo.InvariantCulture);

        internal static bool TryParseNumber(string text, out double value)
        {
            text = (text ?? "").Trim();
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
            // "0,5" (vírgula decimal) quando não há ponto
            return text.IndexOf('.') < 0 && double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static int DecimalsOf(double v)
        {
            v = Math.Abs(v);
            for (int d = 0; d <= 10; d++)
            {
                double scaled = v * Math.Pow(10, d);
                if (Math.Abs(scaled - Math.Round(scaled)) < 1e-7 * Math.Max(1, scaled)) return d;
            }
            return 10;
        }

        public override string ToString() => Describe();
    }

    /// <summary>
    /// Sobreposição de faixa escrita pelo usuário: <c>Nome | min=2 | max=8 | step=0.5</c>, <c>Nome | levels=A;B;C</c>
    /// ou <c>Nome | off</c> (tira a variável do espaço). O nome aceita curingas (<c>*</c>, <c>?</c>).
    /// </summary>
    public sealed class DesignRangeOverride
    {
        public string Pattern { get; private set; }
        public double? Min { get; private set; }
        public double? Max { get; private set; }
        public double? Step { get; private set; }
        public List<string> Levels { get; private set; }
        public bool Exclude { get; private set; }

        public static bool TryParse(string line, out DesignRangeOverride result, out string error)
        {
            result = null;
            error = null;
            if (string.IsNullOrWhiteSpace(line)) return false;
            var parts = line.Split('|');
            string pattern = parts[0].Trim();
            if (pattern.Length == 0)
            {
                error = $"'{line.Trim()}': falta o nome da variável antes do '|'.";
                return false;
            }
            var o = new DesignRangeOverride { Pattern = pattern };
            for (int i = 1; i < parts.Length; i++)
            {
                string token = parts[i].Trim();
                if (token.Length == 0) continue;
                int eq = token.IndexOf('=');
                string key = (eq >= 0 ? token.Substring(0, eq) : token).Trim().ToLowerInvariant();
                string value = eq >= 0 ? token.Substring(eq + 1).Trim() : "";
                switch (key)
                {
                    case "off":
                    case "exclude":
                    case "fixo":
                    case "fixed":
                        o.Exclude = true;
                        break;
                    case "min":
                    case "max":
                    case "step":
                    case "passo":
                        if (!DesignVariable.TryParseNumber(value, out double number) || double.IsNaN(number) || double.IsInfinity(number))
                        {
                            error = $"'{line.Trim()}': '{value}' não é um número em {key}=.";
                            return false;
                        }
                        if (key == "min") o.Min = number;
                        else if (key == "max") o.Max = number;
                        else if (number < 0)
                        {
                            error = $"'{line.Trim()}': o passo não pode ser negativo.";
                            return false;
                        }
                        else o.Step = number;
                        break;
                    case "levels":
                    case "niveis":
                    case "níveis":
                    case "values":
                        o.Levels = value.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                        if (o.Levels.Count == 0)
                        {
                            error = $"'{line.Trim()}': levels= vazio (separe os níveis com ';').";
                            return false;
                        }
                        break;
                    default:
                        error = $"'{line.Trim()}': opção desconhecida '{key}' (use min, max, step, levels ou off).";
                        return false;
                }
            }
            result = o;
            return true;
        }

        public bool Matches(DesignVariable v) => WildcardMatch(Pattern, v.Name) || string.Equals(Pattern, v.Id, StringComparison.OrdinalIgnoreCase);

        /// <summary>Curingas <c>*</c> e <c>?</c>, sem diferenciar maiúsculas.</summary>
        public static bool WildcardMatch(string pattern, string text)
        {
            pattern = pattern ?? "";
            text = text ?? "";
            int p = 0, t = 0, star = -1, mark = 0;
            while (t < text.Length)
            {
                if (p < pattern.Length && (pattern[p] == '?' || char.ToLowerInvariant(pattern[p]) == char.ToLowerInvariant(text[t])))
                {
                    p++;
                    t++;
                }
                else if (p < pattern.Length && pattern[p] == '*')
                {
                    star = p++;
                    mark = t;
                }
                else if (star >= 0)
                {
                    p = star + 1;
                    t = ++mark;
                }
                else
                {
                    return false;
                }
            }
            while (p < pattern.Length && pattern[p] == '*') p++;
            return p == pattern.Length;
        }
    }

    /// <summary>Espaço de projeto: variáveis em ordem, sem duplicatas (por Id), com identidade estável.</summary>
    public sealed class DesignSpace
    {
        public const int MaxVariables = 128;

        public DesignSpace(IEnumerable<DesignVariable> variables)
        {
            var list = new List<DesignVariable>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in variables ?? Enumerable.Empty<DesignVariable>())
            {
                if (v != null && seen.Add(v.Id)) list.Add(v);
            }
            if (list.Count > MaxVariables) throw new ArgumentException($"No máximo {MaxVariables} variáveis por espaço de projeto ({list.Count} informadas).");
            Variables = list;
            Hash = TreeHash.Sha256Hex(Encoding.UTF8.GetBytes(string.Join("\n", list.Select(v => v.Canonical()))));
        }

        public IReadOnlyList<DesignVariable> Variables { get; }
        public int Count => Variables.Count;

        /// <summary>Identidade: muda quando uma variável, faixa, passo ou nível muda (não depende dos valores atuais).</summary>
        public string Hash { get; }

        public int IndexOf(string nameOrId)
        {
            for (int i = 0; i < Variables.Count; i++)
            {
                if (string.Equals(Variables[i].Name, nameOrId, StringComparison.OrdinalIgnoreCase) || string.Equals(Variables[i].Id, nameOrId, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        /// <summary>Aplica as sobreposições na ordem; a última que casar com uma variável vence. Devolve o novo espaço.</summary>
        public DesignSpace WithOverrides(IEnumerable<DesignRangeOverride> overrides, ICollection<string> warnings)
        {
            var rules = (overrides ?? Enumerable.Empty<DesignRangeOverride>()).ToList();
            if (rules.Count == 0) return this;
            var used = new bool[rules.Count];
            var result = new List<DesignVariable>();
            foreach (var v in Variables)
            {
                var current = v;
                bool excluded = false;
                for (int i = 0; i < rules.Count; i++)
                {
                    if (!rules[i].Matches(v)) continue;
                    used[i] = true;
                    if (rules[i].Exclude) excluded = true;
                    else current = current.With(rules[i], warnings);
                }
                if (!excluded) result.Add(current);
            }
            for (int i = 0; i < rules.Count; i++)
            {
                if (!used[i]) warnings?.Add($"Faixa '{rules[i].Pattern}': nenhuma variável com esse nome.");
            }
            return new DesignSpace(result);
        }

        /// <summary>Tamanho do espaço discreto (produto dos valores possíveis), ou null se alguma variável for contínua.</summary>
        public double? DiscreteSize()
        {
            double size = 1;
            foreach (var v in Variables)
            {
                int n = v.ValueCount;
                if (n == int.MaxValue) return null;
                size *= n;
            }
            return size;
        }

        public List<string> Describe() => Variables.Select(v => v.Describe()).ToList();

        public override string ToString() => $"Design Space ({Count} variáveis)";
    }
}
