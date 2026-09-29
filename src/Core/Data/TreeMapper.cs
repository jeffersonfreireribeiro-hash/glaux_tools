using System;
using System.Collections.Generic;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools.Data
{
    /// <summary>
    /// Mapeia DataTrees do Grasshopper para o modelo canônico e de volta, sem perda estrutural:
    /// caminhos de qualquer profundidade, ramos vazios, itens nulos e tipos mistos no mesmo ramo.
    /// </summary>
    public static class TreeMapper
    {
        public static GlauxTreeTable ToTable(GH_Structure<IGH_Goo> tree, ICollection<string> warnings = null)
        {
            var table = new GlauxTreeTable();
            if (tree == null) return table;

            for (int b = 0; b < tree.PathCount; b++)
            {
                GH_Path path = tree.Paths[b];
                List<IGH_Goo> list = tree.Branches[b];
                var items = new List<GlauxValue>(list?.Count ?? 0);
                if (list != null)
                {
                    foreach (var goo in list) items.Add(GooCodec.Encode(goo, warnings));
                }
                table.Branches.Add(new GlauxBranch((int[])path.Indices.Clone(), items));
            }
            return table;
        }

        public static GH_Structure<IGH_Goo> ToTree(GlauxTreeTable table, ICollection<string> warnings = null)
        {
            var tree = new GH_Structure<IGH_Goo>();
            if (table == null) return tree;

            foreach (var branch in table.Branches)
            {
                var path = new GH_Path(branch.Path);
                var items = new List<IGH_Goo>(branch.Items.Count);
                foreach (var v in branch.Items) items.Add(GooCodec.Decode(v, warnings));
                // AppendRange também cria o caminho quando a lista está vazia (preserva ramos vazios)
                tree.AppendRange(items, path);
            }
            return tree;
        }

        /// <summary>
        /// Monta uma árvore a partir de colunas (visão relacional). Os itens de cada caminho são posicionados
        /// pelo índice quando informado (lacunas viram nulos); sem índice, seguem a ordem das linhas.
        /// <paramref name="branchPaths"/> recria ramos que não têm nenhum item (ramos vazios).
        /// </summary>
        public static GlauxTreeTable FromColumns(
            IList<string> paths,
            IList<int> indices,
            IList<GlauxValue> values,
            IList<string> branchPaths,
            ICollection<string> warnings)
        {
            var table = new GlauxTreeTable();
            var byPath = new Dictionary<string, SortedDictionary<int, GlauxValue>>(StringComparer.Ordinal);
            var sequential = new Dictionary<string, List<GlauxValue>>(StringComparer.Ordinal);
            var parsedPaths = new Dictionary<string, int[]>(StringComparer.Ordinal);
            var order = new List<string>();

            bool TryRegister(string rawPath, out string key)
            {
                key = null;
                if (!GlauxTreeTable.TryParsePath(rawPath, out int[] parsed))
                {
                    warnings?.Add($"Caminho inválido ignorado: '{rawPath}'.");
                    return false;
                }
                key = GlauxTreeTable.FormatPath(parsed);
                if (!parsedPaths.ContainsKey(key))
                {
                    parsedPaths[key] = parsed;
                    order.Add(key);
                }
                return true;
            }

            if (branchPaths != null)
            {
                foreach (var bp in branchPaths)
                {
                    TryRegister(bp, out _);
                }
            }

            int rowCount = values?.Count ?? 0;
            if (paths != null && paths.Count != rowCount && paths.Count != 1)
            {
                warnings?.Add($"Quantidade de caminhos ({paths.Count}) diferente da quantidade de valores ({rowCount}); usando o menor.");
                rowCount = Math.Min(rowCount, paths.Count);
            }
            bool useIndices = indices != null && indices.Count > 0;
            if (useIndices && indices.Count != rowCount)
            {
                warnings?.Add($"Quantidade de índices ({indices.Count}) diferente da quantidade de valores ({rowCount}); índices ignorados.");
                useIndices = false;
            }

            for (int r = 0; r < rowCount; r++)
            {
                string rawPath = paths == null || paths.Count == 0 ? "{0}" : (paths.Count == 1 ? paths[0] : paths[r]);
                if (!TryRegister(rawPath, out string key)) continue;
                var value = values[r] ?? GlauxValue.Null;

                if (useIndices)
                {
                    int idx = indices[r];
                    if (idx < 0)
                    {
                        // Índice negativo marca apenas a existência do ramo (linha de ramo vazio)
                        continue;
                    }
                    if (!byPath.TryGetValue(key, out var slots))
                    {
                        slots = new SortedDictionary<int, GlauxValue>();
                        byPath[key] = slots;
                    }
                    if (slots.ContainsKey(idx))
                    {
                        warnings?.Add($"Índice {idx} repetido em {key}; mantido o último valor.");
                    }
                    slots[idx] = value;
                }
                else
                {
                    if (!sequential.TryGetValue(key, out var list))
                    {
                        list = new List<GlauxValue>();
                        sequential[key] = list;
                    }
                    list.Add(value);
                }
            }

            foreach (var key in order)
            {
                var items = new List<GlauxValue>();
                if (useIndices && byPath.TryGetValue(key, out var slots) && slots.Count > 0)
                {
                    int last = -1;
                    foreach (var kv in slots) last = kv.Key;
                    for (int i = 0; i <= last; i++)
                    {
                        items.Add(slots.TryGetValue(i, out var v) ? v : GlauxValue.Null);
                    }
                }
                else if (sequential.TryGetValue(key, out var list))
                {
                    items.AddRange(list);
                }
                table.Branches.Add(new GlauxBranch(parsedPaths[key], items));
            }
            return table;
        }
    }
}
