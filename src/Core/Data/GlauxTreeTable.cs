using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Buraqueira_Tools.Data
{
    /// <summary>
    /// Ramo do modelo canônico: caminho completo (qualquer profundidade) e itens na ordem original.
    /// Ramos vazios são representados explicitamente (Items.Count == 0).
    /// </summary>
    public sealed class GlauxBranch
    {
        public GlauxBranch(int[] path, List<GlauxValue> items = null)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Items = items ?? new List<GlauxValue>();
        }

        public int[] Path { get; }
        public List<GlauxValue> Items { get; }
        public string PathText => GlauxTreeTable.FormatPath(Path);
        public override string ToString() => $"{PathText} ({Items.Count} itens)";
    }

    /// <summary>
    /// Linha da visão relacional de uma árvore: (ramo, caminho, índice, valor).
    /// Não assume "ramo = linha / item = coluna": árvores irregulares e ramos vazios são preservados.
    /// </summary>
    public readonly struct GlauxRow
    {
        public GlauxRow(int branchOrdinal, int[] path, int itemIndex, GlauxValue value)
        {
            BranchOrdinal = branchOrdinal;
            Path = path;
            ItemIndex = itemIndex;
            Value = value;
        }

        public int BranchOrdinal { get; }
        public int[] Path { get; }
        public int ItemIndex { get; }
        public GlauxValue Value { get; }
    }

    /// <summary>
    /// Modelo canônico, independente do Grasshopper em memória, de uma DataTree:
    /// ramos (caminho + itens tipados) e metadados. Base comum de serialização, persistência,
    /// hashing de identidade, snapshots e validação.
    /// </summary>
    public sealed class GlauxTreeTable
    {
        public List<GlauxBranch> Branches { get; } = new List<GlauxBranch>();

        /// <summary>Metadados livres (chave → valor). Não entram no hash de identidade dos dados.</summary>
        public Dictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public int BranchCount => Branches.Count;

        public int ItemCount
        {
            get
            {
                int total = 0;
                foreach (var b in Branches) total += b.Items.Count;
                return total;
            }
        }

        public int NullCount
        {
            get
            {
                int total = 0;
                foreach (var b in Branches)
                {
                    foreach (var v in b.Items)
                    {
                        if (v.IsNull) total++;
                    }
                }
                return total;
            }
        }

        public int MaxDepth
        {
            get
            {
                int max = 0;
                foreach (var b in Branches) max = Math.Max(max, b.Path.Length);
                return max;
            }
        }

        public int MinDepth
        {
            get
            {
                if (Branches.Count == 0) return 0;
                int min = int.MaxValue;
                foreach (var b in Branches) min = Math.Min(min, b.Path.Length);
                return min;
            }
        }

        public IEnumerable<GlauxRow> EnumerateRows()
        {
            for (int b = 0; b < Branches.Count; b++)
            {
                var branch = Branches[b];
                for (int i = 0; i < branch.Items.Count; i++)
                {
                    yield return new GlauxRow(b, branch.Path, i, branch.Items[i]);
                }
            }
        }

        /// <summary>Contagem de itens por tag de tipo (nulos incluídos como "Null").</summary>
        public Dictionary<string, int> CountByType()
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var b in Branches)
            {
                foreach (var v in b.Items)
                {
                    counts.TryGetValue(v.TypeTag, out int c);
                    counts[v.TypeTag] = c + 1;
                }
            }
            return counts;
        }

        public static string FormatPath(int[] path)
        {
            if (path == null) return "{}";
            var sb = new StringBuilder(2 + path.Length * 3);
            sb.Append('{');
            for (int i = 0; i < path.Length; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(path[i].ToString(CultureInfo.InvariantCulture));
            }
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>Aceita "{0;1;2}", "0;1;2" ou "{0,1,2}". "{}" representa o caminho sem índices.</summary>
        public static bool TryParsePath(string text, out int[] path)
        {
            path = null;
            if (text == null) return false;
            string s = text.Trim();
            if (s.StartsWith("{", StringComparison.Ordinal) && s.EndsWith("}", StringComparison.Ordinal))
            {
                s = s.Substring(1, s.Length - 2);
            }
            s = s.Trim();
            if (s.Length == 0)
            {
                path = new int[0];
                return true;
            }

            var parts = s.Split(new[] { ';', ',' });
            var result = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result[i])) return false;
            }
            path = result;
            return true;
        }

        public static int ComparePaths(int[] a, int[] b)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                int c = a[i].CompareTo(b[i]);
                if (c != 0) return c;
            }
            return a.Length.CompareTo(b.Length);
        }

        public static bool PathsEqual(int[] a, int[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}
