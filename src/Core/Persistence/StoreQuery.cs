using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Buraqueira_Tools.Data;

namespace Buraqueira_Tools.Persistence
{
    public enum RevisionScope
    {
        Latest = 0,
        All = 1
    }

    /// <summary>
    /// Consulta estruturada ao store. Cada filtro é um valor tipado vindo de uma entrada do componente —
    /// nada é concatenado numa string de consulta (sem risco de injeção). Filtros de item são avaliados
    /// sobre a árvore <see cref="TreeName"/> de cada entrada candidata.
    /// </summary>
    public sealed class StoreQuery
    {
        public string Kind { get; set; }

        /// <summary>Padrão da chave com * e ? (vazio = todas).</summary>
        public string KeyPattern { get; set; }

        public RevisionScope Revisions { get; set; } = RevisionScope.Latest;
        public long? Revision { get; set; }
        public DateTime? SinceUtc { get; set; }
        public DateTime? UntilUtc { get; set; }
        public Dictionary<string, string> MetadataEquals { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public string TreeName { get; set; } = StoreTreeNames.Data;

        /// <summary>Máscara de caminho: "{0;*}", "{*;2}", "{1;**}", "{0..3;*}" (vazio = qualquer).</summary>
        public string PathMask { get; set; }

        /// <summary>Tag de tipo (Number, Text...) ou nome curto do tipo Goo (Curve, Mesh...).</summary>
        public string TypeName { get; set; }

        public double? Min { get; set; }
        public double? Max { get; set; }
        public string TextEquals { get; set; }
        public string TextContains { get; set; }
        public int EntryLimit { get; set; } = 1000;
        public int ItemLimit { get; set; } = 100000;

        public bool HasItemFilters =>
            !string.IsNullOrWhiteSpace(PathMask) || !string.IsNullOrWhiteSpace(TypeName) || Min.HasValue || Max.HasValue ||
            TextEquals != null || !string.IsNullOrEmpty(TextContains);
    }

    public sealed class StoreItemMatch
    {
        internal StoreItemMatch(StoreEntryHeader entry, int[] path, int index, GlauxValue value)
        {
            Entry = entry;
            Path = path;
            Index = index;
            Value = value;
        }

        public StoreEntryHeader Entry { get; }
        public int[] Path { get; }
        public int Index { get; }
        public GlauxValue Value { get; }
    }

    public sealed class StoreQueryResult
    {
        public List<StoreEntryHeader> Entries { get; } = new List<StoreEntryHeader>();
        public List<StoreItemMatch> Items { get; } = new List<StoreItemMatch>();
        public int ScannedEntries { get; internal set; }
        public int ScannedItems { get; internal set; }
        public bool Truncated { get; internal set; }
    }

    public static class StoreQueryEngine
    {
        public static StoreQueryResult Execute(GlauxFileStore store, StoreQuery query)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            query = query ?? new StoreQuery();
            var result = new StoreQueryResult();
            if (!store.Exists) return result;

            var keyRegex = GlobToRegex(query.KeyPattern);
            var mask = PathMask.Parse(query.PathMask);

            // 1. Filtros de cabeçalho (sem ler dados)
            var all = store.ListEntries();
            var candidates = new List<StoreEntryHeader>();
            var latestByKey = new Dictionary<string, StoreEntryHeader>(StringComparer.Ordinal);
            foreach (var e in all)
            {
                if (!string.IsNullOrEmpty(query.Kind) && !string.Equals(e.Kind, query.Kind, StringComparison.Ordinal)) continue;
                if (keyRegex != null && !keyRegex.IsMatch(e.Key)) continue;
                if (query.Revision.HasValue && e.Revision != query.Revision.Value) continue;
                if (query.SinceUtc.HasValue && e.TimestampUtc < query.SinceUtc.Value) continue;
                if (query.UntilUtc.HasValue && e.TimestampUtc > query.UntilUtc.Value) continue;
                if (!MetadataMatches(e, query.MetadataEquals)) continue;

                if (query.Revisions == RevisionScope.Latest && !query.Revision.HasValue)
                {
                    string k = e.Kind + "\u0001" + e.Key;
                    if (!latestByKey.TryGetValue(k, out var prev) || e.Revision > prev.Revision) latestByKey[k] = e;
                }
                else
                {
                    candidates.Add(e);
                }
            }
            if (query.Revisions == RevisionScope.Latest && !query.Revision.HasValue) candidates.AddRange(latestByKey.Values);
            candidates.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.Key, b.Key);
                return c != 0 ? c : a.Revision.CompareTo(b.Revision);
            });

            // 2. Filtros de item (carrega só a árvore consultada de cada candidata)
            foreach (var e in candidates)
            {
                if (result.Entries.Count >= query.EntryLimit)
                {
                    result.Truncated = true;
                    break;
                }
                result.ScannedEntries++;

                if (!query.HasItemFilters)
                {
                    result.Entries.Add(e);
                    continue;
                }

                if (e.FindTree(query.TreeName) == null) continue;
                var table = store.LoadTree(e, query.TreeName);
                bool any = false;
                foreach (var branch in table.Branches)
                {
                    if (mask != null && !mask.Matches(branch.Path)) continue;
                    for (int i = 0; i < branch.Items.Count; i++)
                    {
                        result.ScannedItems++;
                        var v = branch.Items[i];
                        if (!ItemMatches(v, query)) continue;
                        any = true;
                        if (result.Items.Count >= query.ItemLimit)
                        {
                            result.Truncated = true;
                            continue;
                        }
                        result.Items.Add(new StoreItemMatch(e, branch.Path, i, v));
                    }
                }
                if (any) result.Entries.Add(e);
            }
            return result;
        }

        public static bool ItemMatches(GlauxValue v, StoreQuery q)
        {
            if (!string.IsNullOrWhiteSpace(q.TypeName))
            {
                string t = q.TypeName.Trim();
                if (!string.Equals(v.TypeTag, t, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(v.DisplayType, t, StringComparison.OrdinalIgnoreCase)) return false;
            }
            if (q.Min.HasValue || q.Max.HasValue)
            {
                double? n = v.NumericValue;
                if (!n.HasValue || double.IsNaN(n.Value)) return false;
                if (q.Min.HasValue && n.Value < q.Min.Value) return false;
                if (q.Max.HasValue && n.Value > q.Max.Value) return false;
            }
            if (q.TextEquals != null || !string.IsNullOrEmpty(q.TextContains))
            {
                if (v.IsNull) return false;
                string text = v.ToDisplayString();
                if (q.TextEquals != null && !string.Equals(text, q.TextEquals, StringComparison.Ordinal)) return false;
                if (!string.IsNullOrEmpty(q.TextContains) && text.IndexOf(q.TextContains, StringComparison.OrdinalIgnoreCase) < 0) return false;
            }
            return true;
        }

        private static bool MetadataMatches(StoreEntryHeader e, Dictionary<string, string> filters)
        {
            foreach (var kv in filters)
            {
                if (!e.Metadata.TryGetValue(kv.Key, out var v) || !string.Equals(v, kv.Value, StringComparison.Ordinal)) return false;
            }
            return true;
        }

        /// <summary>Converte um padrão com * e ? numa Regex (o texto do usuário é escapado; nulo = sem filtro).</summary>
        public static Regex GlobToRegex(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern) || pattern.Trim() == "*") return null;
            var sb = new StringBuilder("^");
            foreach (char c in pattern.Trim())
            {
                if (c == '*') sb.Append(".*");
                else if (c == '?') sb.Append('.');
                else sb.Append(Regex.Escape(c.ToString()));
            }
            sb.Append('$');
            return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }

    /// <summary>
    /// Máscara de caminho: cada posição aceita um inteiro, "*" (qualquer índice), "a..b" (faixa inclusiva);
    /// "**" no fim aceita qualquer quantidade de níveis restantes.
    /// </summary>
    public sealed class PathMask
    {
        private readonly List<Func<int, bool>> _parts;
        private readonly bool _openTail;

        private PathMask(List<Func<int, bool>> parts, bool openTail)
        {
            _parts = parts;
            _openTail = openTail;
        }

        public static PathMask Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string s = text.Trim();
            if (s.StartsWith("{", StringComparison.Ordinal) && s.EndsWith("}", StringComparison.Ordinal)) s = s.Substring(1, s.Length - 2);
            var tokens = s.Split(';');
            var parts = new List<Func<int, bool>>();
            bool openTail = false;
            for (int i = 0; i < tokens.Length; i++)
            {
                string t = tokens[i].Trim();
                if (t == "**")
                {
                    if (i != tokens.Length - 1) throw new FormatException("'**' só pode aparecer no fim da máscara.");
                    openTail = true;
                    break;
                }
                if (t == "*")
                {
                    parts.Add(_ => true);
                    continue;
                }
                int dots = t.IndexOf("..", StringComparison.Ordinal);
                if (dots > 0)
                {
                    int a = int.Parse(t.Substring(0, dots), CultureInfo.InvariantCulture);
                    int b = int.Parse(t.Substring(dots + 2), CultureInfo.InvariantCulture);
                    parts.Add(v => v >= Math.Min(a, b) && v <= Math.Max(a, b));
                    continue;
                }
                if (!int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int exact))
                {
                    throw new FormatException($"Parte inválida '{t}' na máscara de caminho (use inteiro, *, a..b ou ** no fim).");
                }
                parts.Add(v => v == exact);
            }
            return new PathMask(parts, openTail);
        }

        public bool Matches(int[] path)
        {
            if (_openTail ? path.Length < _parts.Count : path.Length != _parts.Count) return false;
            for (int i = 0; i < _parts.Count; i++)
            {
                if (!_parts[i](path[i])) return false;
            }
            return true;
        }
    }
}
