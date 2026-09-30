using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Buraqueira_Tools.Data;

namespace Buraqueira_Tools.Explore
{
    public enum SamplingMethod
    {
        /// <summary>Fatorial completo: todas as combinações de L níveis por variável.</summary>
        Grid,

        /// <summary>Monte Carlo simples.</summary>
        Random,

        /// <summary>Latin Hypercube: cada variável tem exatamente uma amostra por faixa (1/N); com seleção maximin.</summary>
        LatinHypercube,

        /// <summary>Sequência de Sobol (quase aleatória, cobre o espaço de forma mais uniforme que o Monte Carlo).</summary>
        Sobol,

        /// <summary>Trajetórias de Morris (efeitos elementares, triagem barata de sensibilidade).</summary>
        Morris,

        /// <summary>Esquema de Saltelli (índices de Sobol de 1ª ordem e totais).</summary>
        Saltelli
    }

    /// <summary>Opções de amostragem (as que não se aplicam a um método são ignoradas).</summary>
    public sealed class SamplerOptions
    {
        /// <summary>Amostras (random, lhs, sobol), total aproximado (grid), trajetórias (morris) ou N base (saltelli).</summary>
        public int Count { get; set; } = 64;

        public int Seed { get; set; } = 1;

        /// <summary>Sobol/Saltelli: deslocamento digital aleatório (mantém as propriedades da sequência e evita o ponto 0).</summary>
        public bool Scramble { get; set; } = true;

        /// <summary>Grid: níveis por variável contínua; Morris: níveis da grade (par).</summary>
        public int? Levels { get; set; }

        /// <summary>LHS: quantos desenhos candidatos gerar e manter o de maior distância mínima (null = automático).</summary>
        public int? MaximinCandidates { get; set; }

        /// <summary>LHS: amostra no centro de cada faixa em vez de posição aleatória dentro dela.</summary>
        public bool Centered { get; set; }

        public static bool TryParseMethod(string text, out SamplingMethod method)
        {
            switch ((text ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", ""))
            {
                case "grid":
                case "grade":
                case "fullfactorial":
                case "factorial":
                    method = SamplingMethod.Grid;
                    return true;
                case "random":
                case "aleatorio":
                case "aleatória":
                case "aleatoria":
                case "montecarlo":
                case "mc":
                    method = SamplingMethod.Random;
                    return true;
                case "":
                case "lhs":
                case "latinhypercube":
                case "latin":
                    method = SamplingMethod.LatinHypercube;
                    return true;
                case "sobol":
                case "qmc":
                    method = SamplingMethod.Sobol;
                    return true;
                case "morris":
                case "elementaryeffects":
                    method = SamplingMethod.Morris;
                    return true;
                case "saltelli":
                case "sobolindices":
                    method = SamplingMethod.Saltelli;
                    return true;
                default:
                    method = SamplingMethod.LatinHypercube;
                    return false;
            }
        }

        public static string MethodName(SamplingMethod m)
        {
            switch (m)
            {
                case SamplingMethod.Grid: return "grid";
                case SamplingMethod.Random: return "random";
                case SamplingMethod.Sobol: return "sobol";
                case SamplingMethod.Morris: return "morris";
                case SamplingMethod.Saltelli: return "saltelli";
                default: return "lhs";
            }
        }
    }

    /// <summary>
    /// Plano de amostras: coordenadas unitárias (n × k) e os valores correspondentes de cada variável
    /// (escolhas guardadas como índice do nível). Morris e Saltelli guardam a estrutura necessária à análise.
    /// </summary>
    public sealed class SamplePlan
    {
        internal SamplePlan(DesignSpace space, SamplingMethod method, int seed, double[][] unit, int morrisLevels = 0, int baseCount = 0)
        {
            Space = space;
            Method = method;
            Seed = seed;
            Unit = unit;
            MorrisLevels = morrisLevels;
            BaseCount = baseCount;
            Values = new double[unit.Length][];
            for (int i = 0; i < unit.Length; i++)
            {
                var row = new double[space.Count];
                for (int j = 0; j < space.Count; j++) row[j] = space.Variables[j].FromUnit(unit[i][j]);
                Values[i] = row;
            }
            var sb = new StringBuilder(space.Hash).Append('|').Append(method).Append('|').Append(unit.Length);
            foreach (var row in Values)
            {
                sb.Append('\n');
                foreach (var v in row) sb.Append(v.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            }
            Hash = TreeHash.Sha256Hex(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        public DesignSpace Space { get; }
        public SamplingMethod Method { get; }
        public int Seed { get; }

        /// <summary>Coordenadas unitárias [0, 1] (n × k).</summary>
        public double[][] Unit { get; }

        /// <summary>Valores dos controles (n × k); escolhas como índice do nível.</summary>
        public double[][] Values { get; }

        /// <summary>Morris: níveis da grade (p).</summary>
        public int MorrisLevels { get; }

        /// <summary>Saltelli: N base (execuções = N × (k + 2)).</summary>
        public int BaseCount { get; }

        public int Count => Unit.Length;

        /// <summary>Morris: pontos por trajetória (k + 1).</summary>
        public int TrajectoryLength => Space.Count + 1;

        /// <summary>Identidade: espaço + método + valores (o Batch Runner não mistura execuções de planos diferentes).</summary>
        public string Hash { get; }

        public string Describe()
        {
            string m = SamplerOptions.MethodName(Method);
            switch (Method)
            {
                case SamplingMethod.Morris:
                    return $"{m}: {Count / Math.Max(1, TrajectoryLength)} trajetórias × {TrajectoryLength} pontos = {Count} amostras, {Space.Count} variáveis, {MorrisLevels} níveis";
                case SamplingMethod.Saltelli:
                    return $"{m}: N = {BaseCount} × (k + 2) = {Count} amostras, {Space.Count} variáveis";
                default:
                    return $"{m}: {Count} amostras, {Space.Count} variáveis";
            }
        }

        public override string ToString() => $"Sample Plan ({Describe()})";
    }

    /// <summary>Gerador determinístico (SplitMix64): mesma semente → mesmas amostras em qualquer máquina e versão do .NET.</summary>
    internal sealed class SplitMix64
    {
        private ulong _state;

        public SplitMix64(int seed)
        {
            _state = 0x9E3779B97F4A7C15UL ^ (ulong)(uint)seed;
        }

        public ulong NextULong()
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Uniforme em [0, 1) com 53 bits.</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        public int Next(int maxExclusive) => maxExclusive <= 1 ? 0 : (int)(NextULong() % (ulong)maxExclusive);

        public void Shuffle(int[] a)
        {
            for (int i = a.Length - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                (a[i], a[j]) = (a[j], a[i]);
            }
        }
    }

    /// <summary>
    /// Sequência de Sobol em ordem de código de Gray (Antonov–Saleev), 30 bits, até 256 dimensões, com
    /// deslocamento digital opcional. Sem deslocamento, reproduz <c>scipy.stats.qmc.Sobol(scramble=False)</c>.
    /// </summary>
    public sealed class SobolSequence
    {
        private const int Bits = 30;
        private const double Scale = 1.0 / (1 << Bits);
        private readonly uint[][] _v;
        private readonly uint[] _x;
        private readonly uint[] _shift;
        private long _index;

        public SobolSequence(int dimensions, int? scrambleSeed = null)
        {
            if (dimensions < 1 || dimensions > SobolDirectionNumbers.MaxDimensions)
            {
                throw new ArgumentException($"Sobol: de 1 a {SobolDirectionNumbers.MaxDimensions} dimensões ({dimensions} pedidas).");
            }
            Dimensions = dimensions;
            _v = new uint[dimensions][];
            _x = new uint[dimensions];
            _shift = new uint[dimensions];
            for (int j = 0; j < dimensions; j++)
            {
                var v = new uint[Bits];
                if (j == 0)
                {
                    for (int i = 0; i < Bits; i++) v[i] = 1u << (Bits - 1 - i);
                }
                else
                {
                    var row = SobolDirectionNumbers.Table[j - 1];
                    int poly = row[0];
                    int s = BitLength(poly) - 1;
                    for (int i = 0; i < s && i < Bits; i++) v[i] = (uint)row[1 + i] << (Bits - 1 - i);
                    for (int i = s; i < Bits; i++)
                    {
                        uint value = v[i - s] ^ (v[i - s] >> s);
                        for (int l = 1; l < s; l++)
                        {
                            if (((poly >> (s - l)) & 1) != 0) value ^= v[i - l];
                        }
                        v[i] = value;
                    }
                }
                _v[j] = v;
            }
            if (scrambleSeed.HasValue)
            {
                var rng = new SplitMix64(scrambleSeed.Value ^ 0x5A17);
                for (int j = 0; j < dimensions; j++) _shift[j] = (uint)(rng.NextULong() >> (64 - Bits));
            }
        }

        public int Dimensions { get; }

        /// <summary>Próximo ponto (o primeiro, sem deslocamento, é a origem).</summary>
        public double[] Next()
        {
            if (_index >= (1L << Bits)) throw new InvalidOperationException("Sobol: limite de 2^30 pontos atingido.");
            var p = new double[Dimensions];
            for (int j = 0; j < Dimensions; j++) p[j] = (_x[j] ^ _shift[j]) * Scale;
            int c = 0;
            long n = _index;
            while ((n & 1) == 1)
            {
                n >>= 1;
                c++;
            }
            for (int j = 0; j < Dimensions; j++) _x[j] ^= _v[j][c];
            _index++;
            return p;
        }

        private static int BitLength(int v)
        {
            int n = 0;
            while (v > 0)
            {
                v >>= 1;
                n++;
            }
            return n;
        }
    }

    /// <summary>Gera planos de amostras a partir de um espaço de projeto.</summary>
    public static class DesignSampler
    {
        public const int MaxSamples = 100000;

        public static SamplePlan Generate(DesignSpace space, SamplingMethod method, SamplerOptions options, ICollection<string> notes = null)
        {
            if (space == null || space.Count == 0) throw new ArgumentException("O espaço de projeto não tem variáveis.");
            options = options ?? new SamplerOptions();
            int n = options.Count;
            if (n < 1) throw new ArgumentException("A quantidade de amostras precisa ser pelo menos 1.");
            switch (method)
            {
                case SamplingMethod.Grid:
                    return Grid(space, options, notes);
                case SamplingMethod.Random:
                    CheckTotal(n);
                    return new SamplePlan(space, method, options.Seed, RandomUnit(space.Count, n, new SplitMix64(options.Seed)));
                case SamplingMethod.Sobol:
                    CheckTotal(n);
                    if (!IsPowerOfTwo(n)) notes?.Add($"Sobol é mais equilibrado com potências de 2 ({PreviousPowerOfTwo(n)} ou {PreviousPowerOfTwo(n) * 2} em vez de {n}).");
                    return new SamplePlan(space, method, options.Seed, SobolUnit(space.Count, n, options));
                case SamplingMethod.Morris:
                    return Morris(space, options, notes);
                case SamplingMethod.Saltelli:
                    return Saltelli(space, options, notes);
                default:
                    CheckTotal(n);
                    return new SamplePlan(space, SamplingMethod.LatinHypercube, options.Seed, LatinHypercube(space.Count, n, options));
            }
        }

        private static void CheckTotal(double total)
        {
            if (total > MaxSamples) throw new ArgumentException($"O plano teria {total:N0} amostras; o limite é {MaxSamples:N0}.");
        }

        private static double[][] RandomUnit(int k, int n, SplitMix64 rng)
        {
            var u = new double[n][];
            for (int i = 0; i < n; i++)
            {
                u[i] = new double[k];
                for (int j = 0; j < k; j++) u[i][j] = rng.NextDouble();
            }
            return u;
        }

        private static double[][] SobolUnit(int k, int n, SamplerOptions options)
        {
            var seq = new SobolSequence(k, options.Scramble ? options.Seed : (int?)null);
            var u = new double[n][];
            for (int i = 0; i < n; i++) u[i] = seq.Next();
            return u;
        }

        /// <summary>
        /// Latin Hypercube: por variável, uma permutação das N faixas. Com candidatos &gt; 1, fica o desenho com a maior
        /// distância mínima entre amostras (critério maximin), que evita amostras amontoadas.
        /// </summary>
        public static double[][] LatinHypercube(int k, int n, SamplerOptions options)
        {
            var rng = new SplitMix64(options.Seed);
            int candidates = options.MaximinCandidates ?? (n <= 256 ? 20 : n <= 1024 ? 5 : 1);
            candidates = Math.Max(1, Math.Min(200, candidates));
            double[][] best = null;
            double bestScore = double.NegativeInfinity;
            for (int c = 0; c < candidates; c++)
            {
                var design = new double[n][];
                for (int i = 0; i < n; i++) design[i] = new double[k];
                var perm = new int[n];
                for (int j = 0; j < k; j++)
                {
                    for (int i = 0; i < n; i++) perm[i] = i;
                    rng.Shuffle(perm);
                    for (int i = 0; i < n; i++) design[i][j] = (perm[i] + (options.Centered ? 0.5 : rng.NextDouble())) / n;
                }
                if (candidates == 1) return design;
                double score = MinSquaredDistance(design);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = design;
                }
            }
            return best;
        }

        internal static double MinSquaredDistance(double[][] design)
        {
            double min = double.PositiveInfinity;
            for (int a = 0; a < design.Length; a++)
            {
                for (int b = a + 1; b < design.Length; b++)
                {
                    double d = 0;
                    var pa = design[a];
                    var pb = design[b];
                    for (int j = 0; j < pa.Length && d < min; j++)
                    {
                        double t = pa[j] - pb[j];
                        d += t * t;
                    }
                    if (d < min) min = d;
                }
            }
            return min;
        }

        /// <summary>
        /// Fatorial completo. Contínuas: L níveis igualmente espaçados incluindo os extremos; com passo: até L dos
        /// valores possíveis; escolhas: todos os níveis. A primeira variável varia mais devagar.
        /// </summary>
        private static SamplePlan Grid(DesignSpace space, SamplerOptions options, ICollection<string> notes)
        {
            int k = space.Count;
            int levels = options.Levels ?? Math.Max(2, (int)Math.Floor(Math.Pow(options.Count, 1.0 / k) + 1e-9));
            levels = Math.Max(1, levels);
            var axes = new double[k][];
            double total = 1;
            for (int j = 0; j < k; j++)
            {
                var v = space.Variables[j];
                double[] axis;
                if (v.Type == DesignVariableType.Choice)
                {
                    axis = Enumerable.Range(0, v.Levels.Count).Select(i => (i + 0.5) / v.Levels.Count).ToArray();
                }
                else if (v.Step > 0 || v.ValueCount == 1)
                {
                    int count = v.ValueCount;
                    int use = Math.Min(levels, count);
                    axis = new double[use];
                    for (int i = 0; i < use; i++)
                    {
                        long idx = use == 1 ? 0 : (long)Math.Round(i * (count - 1.0) / (use - 1));
                        axis[i] = v.UnitOfIndex(idx);
                    }
                }
                else
                {
                    axis = levels == 1 ? new[] { 0.5 } : Enumerable.Range(0, levels).Select(i => i / (levels - 1.0)).ToArray();
                }
                axes[j] = axis;
                total *= axis.Length;
            }
            CheckTotal(total);
            if (!options.Levels.HasValue && Math.Abs(total - options.Count) > 0.5)
            {
                notes?.Add($"Grade com {total:N0} combinações (mais próximo de {options.Count} com {levels} níveis por variável contínua).");
            }

            int n = (int)total;
            var unit = new double[n][];
            var idxs = new int[k];
            for (int i = 0; i < n; i++)
            {
                var row = new double[k];
                for (int j = 0; j < k; j++) row[j] = axes[j][idxs[j]];
                unit[i] = row;
                for (int j = k - 1; j >= 0; j--)
                {
                    if (++idxs[j] < axes[j].Length) break;
                    idxs[j] = 0;
                }
            }
            return new SamplePlan(space, SamplingMethod.Grid, options.Seed, unit);
        }

        /// <summary>
        /// Trajetórias de Morris (1991): r trajetórias de k + 1 pontos numa grade de p níveis; cada passo muda uma
        /// única variável por Δ = p / (2(p − 1)), em ordem e sentido aleatórios.
        /// </summary>
        private static SamplePlan Morris(DesignSpace space, SamplerOptions options, ICollection<string> notes)
        {
            int k = space.Count;
            int r = options.Count;
            int p = options.Levels ?? 4;
            if (p < 2) throw new ArgumentException("Morris: use pelo menos 2 níveis.");
            if (p % 2 != 0)
            {
                p++;
                notes?.Add($"Morris usa um número par de níveis; usando {p}.");
            }
            CheckTotal((double)r * (k + 1));
            double delta = p / (2.0 * (p - 1));
            var rng = new SplitMix64(options.Seed);
            var unit = new List<double[]>(r * (k + 1));
            var order = new int[k];
            for (int t = 0; t < r; t++)
            {
                var x = new double[k];
                var dir = new int[k];
                for (int j = 0; j < k; j++)
                {
                    double b = rng.Next(p / 2) / (double)(p - 1);
                    dir[j] = rng.NextDouble() < 0.5 ? -1 : 1;
                    x[j] = dir[j] > 0 ? b : b + delta;
                    order[j] = j;
                }
                rng.Shuffle(order);
                unit.Add((double[])x.Clone());
                foreach (int j in order)
                {
                    x[j] = Math.Round((x[j] + dir[j] * delta) * (p - 1)) / (p - 1);
                    unit.Add((double[])x.Clone());
                }
            }
            foreach (var v in space.Variables)
            {
                if (v.IsDiscrete && v.ValueCount < p) notes?.Add($"'{v.Name}' tem {v.ValueCount} valores e a grade de Morris {p} níveis: passos podem repetir o mesmo valor (efeito 0).");
            }
            return new SamplePlan(space, SamplingMethod.Morris, options.Seed, unit.ToArray(), morrisLevels: p);
        }

        /// <summary>
        /// Esquema de Saltelli (2010) para índices de Sobol: matrizes A e B (N × k) de uma sequência de Sobol com 2k
        /// dimensões e, por linha, as execuções A, AB₁ … ABₖ, B (AB_i = A com a coluna i de B). Total N(k + 2).
        /// </summary>
        private static SamplePlan Saltelli(DesignSpace space, SamplerOptions options, ICollection<string> notes)
        {
            int k = space.Count;
            int n = options.Count;
            if (2 * k > SobolDirectionNumbers.MaxDimensions) throw new ArgumentException($"Saltelli: no máximo {SobolDirectionNumbers.MaxDimensions / 2} variáveis.");
            CheckTotal((double)n * (k + 2));
            if (!IsPowerOfTwo(n)) notes?.Add($"Saltelli é mais preciso com N potência de 2 ({PreviousPowerOfTwo(n)} ou {PreviousPowerOfTwo(n) * 2} em vez de {n}).");
            var seq = new SobolSequence(2 * k, options.Scramble ? options.Seed : (int?)null);
            if (!options.Scramble) seq.Next(); // sem deslocamento, o primeiro ponto é a origem (A = B)
            var unit = new List<double[]>(n * (k + 2));
            for (int row = 0; row < n; row++)
            {
                var p = seq.Next();
                var a = new double[k];
                var b = new double[k];
                Array.Copy(p, 0, a, 0, k);
                Array.Copy(p, k, b, 0, k);
                unit.Add(a);
                for (int i = 0; i < k; i++)
                {
                    var ab = (double[])a.Clone();
                    ab[i] = b[i];
                    unit.Add(ab);
                }
                unit.Add(b);
            }
            return new SamplePlan(space, SamplingMethod.Saltelli, options.Seed, unit.ToArray(), baseCount: n);
        }

        private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

        private static int PreviousPowerOfTwo(int n)
        {
            int p = 1;
            while (p * 2 <= n) p *= 2;
            return p;
        }
    }
}
