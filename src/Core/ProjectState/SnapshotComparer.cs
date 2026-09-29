using System;
using System.Collections.Generic;
using System.Globalization;
using Buraqueira_Tools.Data;
using Buraqueira_Tools.Persistence;

namespace Buraqueira_Tools.ProjectState
{
    public enum TreeChange
    {
        Same = 0,
        Changed = 1,
        Added = 2,
        Removed = 3
    }

    public sealed class TreeDiff
    {
        public string Name { get; internal set; }
        public TreeChange Change { get; internal set; }
        public string Detail { get; internal set; }

        /// <summary>Maior diferença absoluta entre itens numéricos nas mesmas posições (nulo se não se aplica).</summary>
        public double? MaxNumericDelta { get; internal set; }

        public int ChangedItems { get; internal set; }

        public override string ToString() => $"{Name}: {Change}{(string.IsNullOrEmpty(Detail) ? "" : " — " + Detail)}";
    }

    public sealed class SnapshotDiff
    {
        public List<TreeDiff> Trees { get; } = new List<TreeDiff>();
        public List<string> MetadataChanges { get; } = new List<string>();

        public bool DataEqual
        {
            get
            {
                foreach (var t in Trees)
                {
                    if (t.Change != TreeChange.Same) return false;
                }
                return true;
            }
        }

        public bool InputsEqual { get; internal set; }
        public bool OutputsEqual { get; internal set; }
    }

    /// <summary>
    /// Compara duas entradas (snapshots, experimentos ou datasets): árvores adicionadas/removidas/alteradas
    /// (usando o hash de identidade para pular as iguais sem carregar dados), detalhe da primeira diferença,
    /// maior variação numérica e mudanças de metadados (ex.: versões do Rhino/Glaux).
    /// </summary>
    public static class SnapshotComparer
    {
        private static readonly HashSet<string> IgnoredMetadata = new HashSet<string>(StringComparer.Ordinal)
        {
            VaultNames.MetaInputsHash, VaultNames.MetaOutputsHash
        };

        public static SnapshotDiff Compare(StoreEntryHeader a, StoreEntryHeader b, Func<StoreEntryHeader, string, GlauxTreeTable> loadTree)
        {
            var diff = new SnapshotDiff();
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var t in a.Trees) names.Add(t.Name);
            foreach (var t in b.Trees) names.Add(t.Name);

            foreach (var name in names)
            {
                var ta = a.FindTree(name);
                var tb = b.FindTree(name);
                var d = new TreeDiff { Name = name };
                if (ta == null)
                {
                    d.Change = TreeChange.Added;
                    d.Detail = $"{tb.ItemCount} item(ns)";
                }
                else if (tb == null)
                {
                    d.Change = TreeChange.Removed;
                    d.Detail = $"{ta.ItemCount} item(ns)";
                }
                else if (string.Equals(ta.Hash, tb.Hash, StringComparison.Ordinal))
                {
                    d.Change = TreeChange.Same;
                }
                else
                {
                    d.Change = TreeChange.Changed;
                    DescribeChange(d, loadTree(a, name), loadTree(b, name), name);
                }
                diff.Trees.Add(d);
            }

            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var k in a.Metadata.Keys) keys.Add(k);
            foreach (var k in b.Metadata.Keys) keys.Add(k);
            foreach (var k in keys)
            {
                if (IgnoredMetadata.Contains(k)) continue;
                a.Metadata.TryGetValue(k, out string va);
                b.Metadata.TryGetValue(k, out string vb);
                if (!string.Equals(va, vb, StringComparison.Ordinal))
                {
                    diff.MetadataChanges.Add($"{k}: {va ?? "∅"} → {vb ?? "∅"}");
                }
            }

            a.Metadata.TryGetValue(VaultNames.MetaInputsHash, out string ia);
            b.Metadata.TryGetValue(VaultNames.MetaInputsHash, out string ib);
            a.Metadata.TryGetValue(VaultNames.MetaOutputsHash, out string oa);
            b.Metadata.TryGetValue(VaultNames.MetaOutputsHash, out string ob);
            diff.InputsEqual = ia != null && ib != null ? ia == ib : AllSame(diff, VaultNames.IsInputTree);
            diff.OutputsEqual = oa != null && ob != null ? oa == ob : AllSame(diff, VaultNames.IsOutputTree);
            return diff;
        }

        private static bool AllSame(SnapshotDiff diff, Func<string, bool> filter)
        {
            foreach (var t in diff.Trees)
            {
                if (filter(t.Name) && t.Change != TreeChange.Same) return false;
            }
            return true;
        }

        /// <summary>Descreve a diferença entre duas árvores já carregadas.</summary>
        public static void DescribeChange(TreeDiff d, GlauxTreeTable a, GlauxTreeTable b, string name)
        {
            // Parâmetro escalar: "a → b"
            if (a.ItemCount == 1 && b.ItemCount == 1 && name.StartsWith(VaultNames.ParamPrefix, StringComparison.Ordinal))
            {
                var va = FirstItem(a);
                var vb = FirstItem(b);
                d.Detail = $"{va.ToDisplayString()} → {vb.ToDisplayString()}";
                d.ChangedItems = 1;
                if (va.NumericValue.HasValue && vb.NumericValue.HasValue) d.MaxNumericDelta = Math.Abs(vb.NumericValue.Value - va.NumericValue.Value);
                return;
            }

            var byPathA = new Dictionary<string, GlauxBranch>(StringComparer.Ordinal);
            foreach (var br in a.Branches) byPathA[br.PathText] = br;
            var byPathB = new Dictionary<string, GlauxBranch>(StringComparer.Ordinal);
            foreach (var br in b.Branches) byPathB[br.PathText] = br;

            int onlyA = 0, onlyB = 0, lengthChanged = 0, changedItems = 0;
            double? maxDelta = null;
            string first = null;
            foreach (var kv in byPathA)
            {
                if (!byPathB.TryGetValue(kv.Key, out var bb))
                {
                    onlyA++;
                    if (first == null) first = $"ramo {kv.Key} só na primeira";
                    continue;
                }
                var ba = kv.Value;
                if (ba.Items.Count != bb.Items.Count)
                {
                    lengthChanged++;
                    if (first == null) first = $"{kv.Key}: {ba.Items.Count} → {bb.Items.Count} itens";
                }
                int n = Math.Min(ba.Items.Count, bb.Items.Count);
                for (int i = 0; i < n; i++)
                {
                    var x = ba.Items[i];
                    var y = bb.Items[i];
                    if (x.Equals(y)) continue;
                    changedItems++;
                    if (first == null) first = $"{kv.Key}[{i}]: {Short(x)} → {Short(y)}";
                    if (x.NumericValue.HasValue && y.NumericValue.HasValue)
                    {
                        double delta = Math.Abs(y.NumericValue.Value - x.NumericValue.Value);
                        if (!double.IsNaN(delta)) maxDelta = maxDelta.HasValue ? Math.Max(maxDelta.Value, delta) : delta;
                    }
                }
            }
            foreach (var key in byPathB.Keys)
            {
                if (!byPathA.ContainsKey(key))
                {
                    onlyB++;
                    if (first == null) first = $"ramo {key} só na segunda";
                }
            }

            var parts = new List<string>();
            if (changedItems > 0) parts.Add($"{changedItems} item(ns) diferente(s)");
            if (lengthChanged > 0) parts.Add($"{lengthChanged} ramo(s) com tamanho diferente");
            if (onlyA > 0) parts.Add($"{onlyA} ramo(s) removido(s)");
            if (onlyB > 0) parts.Add($"{onlyB} ramo(s) novo(s)");
            if (maxDelta.HasValue) parts.Add("Δmáx " + maxDelta.Value.ToString("G6", CultureInfo.InvariantCulture));
            if (parts.Count == 0) parts.Add("mesmos valores, diferença de tipo ou metadados da árvore");
            if (first != null) parts.Add("primeira: " + first);
            d.Detail = string.Join("; ", parts);
            d.ChangedItems = changedItems;
            d.MaxNumericDelta = maxDelta;
        }

        private static GlauxValue FirstItem(GlauxTreeTable t)
        {
            foreach (var b in t.Branches)
            {
                if (b.Items.Count > 0) return b.Items[0];
            }
            return GlauxValue.Null;
        }

        private static string Short(GlauxValue v)
        {
            string s = v.IsNull ? "null" : v.ToDisplayString();
            return s.Length > 40 ? s.Substring(0, 40) + "…" : s;
        }
    }
}
