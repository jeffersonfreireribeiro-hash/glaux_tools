using System;
using System.Collections.Generic;
using System.Globalization;

namespace Buraqueira_Tools.Data
{
    public enum UniqueScope
    {
        None = 0,
        PerBranch = 1,
        Global = 2
    }

    /// <summary>Regras de validação de uma árvore antes de persistir (todas opcionais).</summary>
    public sealed class ValidationRules
    {
        /// <summary>Tipos aceitos (tag ou nome curto: Number, Integer, Text, Point, Curve...). Vazio = qualquer.</summary>
        public List<string> AllowedTypes { get; } = new List<string>();

        public bool AllowNulls { get; set; } = true;
        public bool RequireItems { get; set; }
        public UniqueScope Unique { get; set; } = UniqueScope.None;
        public double? Min { get; set; }
        public double? Max { get; set; }
        public int? ExpectedDepth { get; set; }
        public int? ExpectedBranchCount { get; set; }
        public bool UniformBranchLength { get; set; }
        public List<string> RequiredPaths { get; } = new List<string>();
    }

    public sealed class ValidationIssue
    {
        public ValidationIssue(string rule, string pathText, int index, string message)
        {
            Rule = rule;
            PathText = pathText;
            Index = index;
            Message = message;
        }

        public string Rule { get; }

        /// <summary>Caminho do ramo afetado (vazio para regras da árvore inteira).</summary>
        public string PathText { get; }

        /// <summary>Índice do item (-1 para regras de ramo ou de árvore).</summary>
        public int Index { get; }

        public string Message { get; }

        public override string ToString() => Index >= 0 ? $"[{Rule}] {PathText}[{Index}]: {Message}" : $"[{Rule}] {PathText} {Message}".Replace("  ", " ");
    }

    public sealed class ValidationResult
    {
        public bool IsValid => IssueCount == 0;
        public int IssueCount { get; internal set; }
        public List<ValidationIssue> Issues { get; } = new List<ValidationIssue>();

        /// <summary>Máscara paralela à árvore: true = item aprovado nas regras de item.</summary>
        public List<bool[]> ItemMask { get; } = new List<bool[]>();

        public bool Truncated { get; internal set; }
    }

    /// <summary>Valida o modelo canônico de uma árvore contra <see cref="ValidationRules"/>.</summary>
    public static class TreeValidator
    {
        public static ValidationResult Validate(GlauxTreeTable table, ValidationRules rules, int maxIssues = 500)
        {
            table = table ?? new GlauxTreeTable();
            rules = rules ?? new ValidationRules();
            var result = new ValidationResult();

            void Add(string rule, string path, int index, string message)
            {
                result.IssueCount++;
                if (result.Issues.Count < maxIssues) result.Issues.Add(new ValidationIssue(rule, path, index, message));
                else result.Truncated = true;
            }

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in rules.AllowedTypes)
            {
                if (!string.IsNullOrWhiteSpace(t)) allowed.Add(NormalizeTypeName(t.Trim()));
            }

            // Estrutura da árvore
            if (rules.RequireItems && table.ItemCount == 0) Add("Estrutura", "", -1, "a árvore não tem itens.");
            if (rules.ExpectedBranchCount.HasValue && table.BranchCount != rules.ExpectedBranchCount.Value)
            {
                Add("Estrutura", "", -1, $"esperados {rules.ExpectedBranchCount.Value} ramo(s), encontrados {table.BranchCount}.");
            }
            foreach (var required in rules.RequiredPaths)
            {
                if (!GlauxTreeTable.TryParsePath(required, out int[] rp))
                {
                    Add("Estrutura", required, -1, "caminho obrigatório inválido.");
                    continue;
                }
                bool found = false;
                foreach (var b in table.Branches)
                {
                    if (GlauxTreeTable.PathsEqual(b.Path, rp))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) Add("Estrutura", GlauxTreeTable.FormatPath(rp), -1, "ramo obrigatório ausente.");
            }

            int firstLength = table.BranchCount > 0 ? table.Branches[0].Items.Count : 0;
            var globalSeen = new HashSet<GlauxValue>();

            foreach (var branch in table.Branches)
            {
                string pathText = branch.PathText;
                var mask = new bool[branch.Items.Count];
                result.ItemMask.Add(mask);

                if (rules.ExpectedDepth.HasValue && branch.Path.Length != rules.ExpectedDepth.Value)
                {
                    Add("Profundidade", pathText, -1, $"profundidade {branch.Path.Length}, esperada {rules.ExpectedDepth.Value}.");
                }
                if (rules.UniformBranchLength && branch.Items.Count != firstLength)
                {
                    Add("Comprimento", pathText, -1, $"{branch.Items.Count} item(ns), o primeiro ramo tem {firstLength}.");
                }

                var branchSeen = new HashSet<GlauxValue>();
                for (int i = 0; i < branch.Items.Count; i++)
                {
                    var v = branch.Items[i];
                    bool ok = true;

                    if (v.IsNull)
                    {
                        if (!rules.AllowNulls)
                        {
                            Add("Nulo", pathText, i, "item nulo não permitido.");
                            ok = false;
                        }
                        mask[i] = ok;
                        continue;
                    }

                    if (allowed.Count > 0 && !allowed.Contains(v.TypeTag) && !allowed.Contains(v.DisplayType) && !allowed.Contains(NormalizeTypeName(v.DisplayType)))
                    {
                        Add("Tipo", pathText, i, $"tipo {v.DisplayType} fora dos tipos aceitos.");
                        ok = false;
                    }

                    if (rules.Min.HasValue || rules.Max.HasValue)
                    {
                        double? n = v.NumericValue;
                        if (!n.HasValue)
                        {
                            Add("Faixa", pathText, i, $"{v.DisplayType} não é numérico.");
                            ok = false;
                        }
                        else if (double.IsNaN(n.Value) || (rules.Min.HasValue && n.Value < rules.Min.Value) || (rules.Max.HasValue && n.Value > rules.Max.Value))
                        {
                            Add("Faixa", pathText, i, $"{Format(n.Value)} fora de [{Format(rules.Min)}, {Format(rules.Max)}].");
                            ok = false;
                        }
                    }

                    if (rules.Unique == UniqueScope.PerBranch && !branchSeen.Add(v))
                    {
                        Add("Duplicado", pathText, i, $"valor repetido no ramo ({v.ToDisplayString()}).");
                        ok = false;
                    }
                    else if (rules.Unique == UniqueScope.Global && !globalSeen.Add(v))
                    {
                        Add("Duplicado", pathText, i, $"valor repetido na árvore ({v.ToDisplayString()}).");
                        ok = false;
                    }

                    mask[i] = ok;
                }
            }
            return result;
        }

        /// <summary>Aceita nomes comuns do Grasshopper como sinônimos das tags ("Domain" = Interval, "String" = Text...).</summary>
        private static string NormalizeTypeName(string t)
        {
            switch (t.ToLowerInvariant())
            {
                case "domain": return GlauxTypeTags.Interval;
                case "string": return GlauxTypeTags.Text;
                case "int": return GlauxTypeTags.Integer;
                case "bool": return GlauxTypeTags.Boolean;
                case "double":
                case "float": return GlauxTypeTags.Number;
                case "color": return GlauxTypeTags.Colour;
                case "datetime": return GlauxTypeTags.Time;
                default: return t;
            }
        }

        private static string Format(double? v) => v.HasValue ? v.Value.ToString("G6", CultureInfo.InvariantCulture) : "−∞/+∞";
        private static string Format(double v) => v.ToString("G6", CultureInfo.InvariantCulture);
    }
}
