using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Buraqueira_Tools.Data
{
    /// <summary>
    /// JSON tipado e legível do modelo de árvore (round-trip exato):
    /// <code>
    /// { "format": "glaux.tree", "version": 1, "metadata": { ... },
    ///   "branches": [ { "path": [0, 1], "items": [ { "t": "Number", "v": 1.5 }, null, ... ] } ] }
    /// </code>
    /// Números usam "R"/"G17" (sem perda); NaN/Infinity viram strings; datas em ISO 8601 "o";
    /// cores "#AARRGGBB"; tipos complexos { "t": "Blob", "type": ..., "text": ..., "b64": ... }.
    /// </summary>
    public static class TreeJsonCodec
    {
        public const string FormatName = "glaux.tree";
        public const int FormatVersion = 1;

        public static string Serialize(GlauxTreeTable table, bool pretty = false)
        {
            table = table ?? new GlauxTreeTable();
            var sb = new StringBuilder(64 + table.ItemCount * 24);
            string nl = pretty ? "\n" : "";
            string i1 = pretty ? "  " : "";
            string i2 = pretty ? "    " : "";
            string i3 = pretty ? "      " : "";
            string sp = pretty ? " " : "";

            sb.Append('{').Append(nl);
            sb.Append(i1).Append("\"format\":").Append(sp).Append('"').Append(FormatName).Append("\",").Append(nl);
            sb.Append(i1).Append("\"version\":").Append(sp).Append(FormatVersion).Append(',').Append(nl);

            sb.Append(i1).Append("\"metadata\":").Append(sp).Append('{');
            var keys = new List<string>(table.Metadata.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int k = 0; k < keys.Count; k++)
            {
                if (k > 0) sb.Append(',');
                PillJson.AppendEscapedString(sb, keys[k]);
                sb.Append(':').Append(sp);
                PillJson.AppendEscapedString(sb, table.Metadata[keys[k]] ?? "");
            }
            sb.Append("},").Append(nl);

            sb.Append(i1).Append("\"branches\":").Append(sp).Append('[').Append(nl);
            for (int b = 0; b < table.Branches.Count; b++)
            {
                var branch = table.Branches[b];
                sb.Append(i2).Append("{\"path\":").Append(sp).Append('[');
                for (int d = 0; d < branch.Path.Length; d++)
                {
                    if (d > 0) sb.Append(',');
                    sb.Append(branch.Path[d].ToString(CultureInfo.InvariantCulture));
                }
                sb.Append("],").Append(sp).Append("\"items\":").Append(sp).Append('[');
                for (int i = 0; i < branch.Items.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    if (pretty) sb.Append(nl).Append(i3);
                    AppendValue(sb, branch.Items[i]);
                }
                if (pretty && branch.Items.Count > 0) sb.Append(nl).Append(i2);
                sb.Append("]}");
                if (b < table.Branches.Count - 1) sb.Append(',');
                sb.Append(nl);
            }
            sb.Append(i1).Append(']').Append(nl);
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendValue(StringBuilder sb, GlauxValue v)
        {
            if (v == null || v.IsNull)
            {
                sb.Append("null");
                return;
            }

            sb.Append("{\"t\":\"").Append(v.Kind == GlauxValueKind.Blob ? "Blob" : v.TypeTag).Append("\",");
            switch (v.Kind)
            {
                case GlauxValueKind.Number:
                    sb.Append("\"v\":");
                    AppendNumber(sb, v.X);
                    break;
                case GlauxValueKind.Integer:
                    sb.Append("\"v\":").Append(v.Int.ToString(CultureInfo.InvariantCulture));
                    break;
                case GlauxValueKind.Boolean:
                    sb.Append("\"v\":").Append(v.BooleanValue ? "true" : "false");
                    break;
                case GlauxValueKind.Text:
                    sb.Append("\"v\":");
                    if (v.Text == null) sb.Append("null");
                    else PillJson.AppendEscapedString(sb, v.Text);
                    break;
                case GlauxValueKind.Point:
                case GlauxValueKind.Vector:
                    sb.Append("\"v\":[");
                    AppendNumber(sb, v.X);
                    sb.Append(',');
                    AppendNumber(sb, v.Y);
                    sb.Append(',');
                    AppendNumber(sb, v.Z);
                    sb.Append(']');
                    break;
                case GlauxValueKind.Interval:
                    sb.Append("\"v\":[");
                    AppendNumber(sb, v.X);
                    sb.Append(',');
                    AppendNumber(sb, v.Y);
                    sb.Append(']');
                    break;
                case GlauxValueKind.Colour:
                case GlauxValueKind.Time:
                case GlauxValueKind.Guid:
                    sb.Append("\"v\":");
                    PillJson.AppendEscapedString(sb, v.ToDisplayString());
                    break;
                case GlauxValueKind.Blob:
                    sb.Append("\"type\":");
                    PillJson.AppendEscapedString(sb, v.TypeTag);
                    sb.Append(",\"text\":");
                    if (v.Text == null) sb.Append("null");
                    else PillJson.AppendEscapedString(sb, v.Text);
                    sb.Append(",\"b64\":");
                    if (v.Blob == null) sb.Append("null");
                    else sb.Append('"').Append(Convert.ToBase64String(v.Blob)).Append('"');
                    break;
            }
            sb.Append('}');
        }

        private static void AppendNumber(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                sb.Append('"').Append(GlauxNumberFormat.Format(d)).Append('"');
            }
            else if (d == 0 && BitConverter.DoubleToInt64Bits(d) != 0)
            {
                // "-0" seria lido como o inteiro 0 e perderia o sinal
                sb.Append("-0.0");
            }
            else
            {
                sb.Append(GlauxNumberFormat.Format(d));
            }
        }

        public static GlauxTreeTable Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("JSON vazio.");
            object root;
            try
            {
                root = PillJson.Deserialize(json);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("JSON inválido: " + ex.Message, ex);
            }

            if (!(root is Dictionary<string, object> obj)) throw new InvalidDataException("O JSON não é um objeto de árvore Glaux.");
            if (obj.TryGetValue("format", out var fmt) && fmt != null && !string.Equals(fmt.ToString(), FormatName, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Formato '{fmt}' não é '{FormatName}'.");
            }
            if (obj.TryGetValue("version", out var ver) && ver != null && ToLong(ver, "version") > FormatVersion)
            {
                throw new InvalidDataException($"Árvore JSON versão {ver} é mais nova que a suportada ({FormatVersion}).");
            }

            var table = new GlauxTreeTable();
            if (obj.TryGetValue("metadata", out var metaObj) && metaObj is Dictionary<string, object> meta)
            {
                foreach (var kv in meta) table.Metadata[kv.Key] = kv.Value?.ToString() ?? "";
            }

            if (!obj.TryGetValue("branches", out var branchesObj) || !(branchesObj is IList branches))
            {
                throw new InvalidDataException("Campo 'branches' ausente no JSON da árvore.");
            }

            foreach (var bObj in branches)
            {
                if (!(bObj is Dictionary<string, object> b)) throw new InvalidDataException("Ramo inválido no JSON.");
                if (!b.TryGetValue("path", out var pathObj) || !(pathObj is IList pathList)) throw new InvalidDataException("Ramo sem 'path'.");
                var path = new int[pathList.Count];
                for (int d = 0; d < path.Length; d++) path[d] = checked((int)ToLong(pathList[d], "path"));

                var items = new List<GlauxValue>();
                if (b.TryGetValue("items", out var itemsObj) && itemsObj is IList itemList)
                {
                    foreach (var it in itemList) items.Add(ParseValue(it));
                }
                table.Branches.Add(new GlauxBranch(path, items));
            }
            return table;
        }

        private static GlauxValue ParseValue(object item)
        {
            if (item == null) return GlauxValue.Null;
            if (!(item is Dictionary<string, object> d)) throw new InvalidDataException("Item de árvore inválido no JSON.");
            string t = d.TryGetValue("t", out var tObj) ? tObj?.ToString() : null;
            d.TryGetValue("v", out var v);

            switch (t)
            {
                case GlauxTypeTags.Number: return GlauxValue.FromNumber(ToDouble(v));
                case GlauxTypeTags.Integer: return GlauxValue.FromInteger(ToLong(v, "Integer"));
                case GlauxTypeTags.Boolean: return GlauxValue.FromBoolean(v is bool bv ? bv : bool.Parse(v?.ToString() ?? "false"));
                case GlauxTypeTags.Text: return GlauxValue.FromText(v?.ToString());
                case GlauxTypeTags.Point:
                    {
                        var xyz = ToDoubles(v, 3);
                        return GlauxValue.FromPoint(xyz[0], xyz[1], xyz[2]);
                    }
                case GlauxTypeTags.Vector:
                    {
                        var xyz = ToDoubles(v, 3);
                        return GlauxValue.FromVector(xyz[0], xyz[1], xyz[2]);
                    }
                case GlauxTypeTags.Interval:
                    {
                        var t01 = ToDoubles(v, 2);
                        return GlauxValue.FromInterval(t01[0], t01[1]);
                    }
                case GlauxTypeTags.Colour: return GlauxValue.FromColour(ParseColour(v?.ToString()));
                case GlauxTypeTags.Time: return GlauxValue.FromTime(ParseTime(v?.ToString()));
                case GlauxTypeTags.Guid: return GlauxValue.FromGuid(Guid.Parse(v?.ToString() ?? ""));
                case "Blob":
                    {
                        string type = d.TryGetValue("type", out var ty) ? ty?.ToString() : null;
                        if (string.IsNullOrEmpty(type)) throw new InvalidDataException("Blob sem 'type' no JSON.");
                        string text = d.TryGetValue("text", out var tx) ? tx?.ToString() : null;
                        byte[] blob = d.TryGetValue("b64", out var b64) && b64 != null ? Convert.FromBase64String(b64.ToString()) : null;
                        return GlauxValue.FromBlob(type, text, blob);
                    }
                default:
                    throw new InvalidDataException($"Tipo de item desconhecido no JSON: '{t}'.");
            }
        }

        internal static int ParseColour(string text)
        {
            text = (text ?? "").Trim().TrimStart('#');
            if (int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int argb))
            {
                // "#RRGGBB" sem alfa: assume opaco
                return text.Length <= 6 ? unchecked((int)0xFF000000) | argb : argb;
            }
            throw new InvalidDataException($"Cor inválida: '{text}' (use #AARRGGBB).");
        }

        internal static DateTime ParseTime(string text)
        {
            return DateTime.ParseExact(text ?? "", "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        private static double ToDouble(object v)
        {
            switch (v)
            {
                case double d: return d;
                case long l: return l;
                case int i: return i;
                case string s when GlauxNumberFormat.TryParse(s, out double parsed): return parsed;
                default: throw new InvalidDataException($"Número inválido no JSON: '{v}'.");
            }
        }

        private static long ToLong(object v, string field)
        {
            switch (v)
            {
                case long l: return l;
                case int i: return i;
                case double d when Math.Abs(d - Math.Round(d)) < 1e-9: return (long)Math.Round(d);
                case string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed): return parsed;
                default: throw new InvalidDataException($"Inteiro inválido em '{field}': '{v}'.");
            }
        }

        private static double[] ToDoubles(object v, int count)
        {
            if (!(v is IList list) || list.Count != count) throw new InvalidDataException($"Esperados {count} números.");
            var result = new double[count];
            for (int i = 0; i < count; i++) result[i] = ToDouble(list[i]);
            return result;
        }
    }

    /// <summary>
    /// CSV "longo" (uma linha por item) que preserva caminhos, índices, tipos, nulos e ramos vazios:
    /// <code>path,index,type,value,data</code>
    /// Ramo vazio: índice -1 e tipo vazio. Metadados: linhas com path "#meta" (type = chave, value = valor).
    /// Tipos complexos: value = texto de exibição, data = bytes GH_IO em Base64.
    /// Complementa (não substitui) o CSV tabular do Import/Export CSV.
    /// </summary>
    public static class TreeCsvCodec
    {
        public const string Header = "path,index,type,value,data";
        public const string MetaMarker = "#meta";

        public static string Serialize(GlauxTreeTable table, char delimiter = ',')
        {
            table = table ?? new GlauxTreeTable();
            var sb = new StringBuilder(64 + table.ItemCount * 20);
            sb.Append(Header.Replace(',', delimiter)).Append("\r\n");

            var keys = new List<string>(table.Metadata.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var k in keys)
            {
                AppendRow(sb, delimiter, MetaMarker, "", k, table.Metadata[k] ?? "", "");
            }

            foreach (var branch in table.Branches)
            {
                string path = branch.PathText;
                if (branch.Items.Count == 0)
                {
                    AppendRow(sb, delimiter, path, "-1", "", "", "");
                    continue;
                }
                for (int i = 0; i < branch.Items.Count; i++)
                {
                    var v = branch.Items[i];
                    string idx = i.ToString(CultureInfo.InvariantCulture);
                    if (v.IsNull)
                    {
                        AppendRow(sb, delimiter, path, idx, GlauxTypeTags.Null, "", "");
                    }
                    else if (v.Kind == GlauxValueKind.Blob)
                    {
                        AppendRow(sb, delimiter, path, idx, v.TypeTag, v.Text ?? "", v.Blob == null ? "" : Convert.ToBase64String(v.Blob));
                    }
                    else if (v.Kind == GlauxValueKind.Text && v.Text == null)
                    {
                        // Texto nulo (diferente de vazio) é marcado na coluna data
                        AppendRow(sb, delimiter, path, idx, v.TypeTag, "", "null");
                    }
                    else
                    {
                        AppendRow(sb, delimiter, path, idx, v.TypeTag, v.ToDisplayString(), "");
                    }
                }
            }
            return sb.ToString();
        }

        private static void AppendRow(StringBuilder sb, char delimiter, string path, string index, string type, string value, string data)
        {
            AppendField(sb, path, delimiter);
            sb.Append(delimiter);
            AppendField(sb, index, delimiter);
            sb.Append(delimiter);
            AppendField(sb, type, delimiter);
            sb.Append(delimiter);
            AppendField(sb, value, delimiter);
            sb.Append(delimiter);
            AppendField(sb, data, delimiter);
            sb.Append("\r\n");
        }

        private static void AppendField(StringBuilder sb, string field, char delimiter)
        {
            field = field ?? "";
            bool quote = field.IndexOf(delimiter) >= 0 || field.IndexOf('"') >= 0 || field.IndexOf('\n') >= 0 || field.IndexOf('\r') >= 0 ||
                         (field.Length > 0 && (char.IsWhiteSpace(field[0]) || char.IsWhiteSpace(field[field.Length - 1])));
            if (!quote)
            {
                sb.Append(field);
                return;
            }
            sb.Append('"').Append(field.Replace("\"", "\"\"")).Append('"');
        }

        public static GlauxTreeTable Deserialize(string csv, char delimiter = ',')
        {
            var rows = ParseRows(csv ?? "", delimiter);
            if (rows.Count == 0) throw new InvalidDataException("CSV vazio.");

            var header = rows[0];
            if (header.Count < 4 || !string.Equals(header[0].Trim(), "path", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Cabeçalho esperado: '{Header}'. Para CSV tabular use o componente Import CSV.");
            }

            var table = new GlauxTreeTable();
            var paths = new List<string>();
            var indices = new List<int>();
            var values = new List<GlauxValue>();
            var branchPaths = new List<string>();

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count == 1 && row[0].Length == 0) continue;
                string path = Field(row, 0);
                if (path == MetaMarker)
                {
                    table.Metadata[Field(row, 2)] = Field(row, 3);
                    continue;
                }

                string idxText = Field(row, 1);
                if (!int.TryParse(idxText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
                {
                    throw new InvalidDataException($"Linha {r + 1}: índice inválido '{idxText}'.");
                }
                branchPaths.Add(path);
                if (idx < 0) continue;

                paths.Add(path);
                indices.Add(idx);
                try
                {
                    values.Add(ParseValue(Field(row, 2), Field(row, 3), Field(row, 4)));
                }
                catch (Exception ex) when (!(ex is InvalidDataException))
                {
                    throw new InvalidDataException($"Linha {r + 1}: {ex.Message}", ex);
                }
            }

            var warnings = new List<string>();
            var built = TreeMapper.FromColumns(paths, indices, values, branchPaths, warnings);
            if (warnings.Count > 0) throw new InvalidDataException(string.Join(" ", warnings));
            foreach (var kv in table.Metadata) built.Metadata[kv.Key] = kv.Value;
            return built;
        }

        private static string Field(List<string> row, int i) => i < row.Count ? row[i] : "";

        /// <summary>Converte (tag de tipo, texto, dados Base64) num valor; usado pelo CSV e pelo Table To Tree.</summary>
        public static GlauxValue ParseValue(string type, string value, string data)
        {
            if (!GlauxTypeTags.TryParseKind(type, out GlauxValueKind kind))
            {
                // Qualquer tag não primitiva é a identidade de um tipo Goo (blob)
                if (string.IsNullOrEmpty(type)) throw new InvalidDataException("Tipo vazio em item.");
                return GlauxValue.FromBlob(type, value, string.IsNullOrEmpty(data) ? null : Convert.FromBase64String(data));
            }

            switch (kind)
            {
                case GlauxValueKind.Null: return GlauxValue.Null;
                case GlauxValueKind.Number:
                    if (!GlauxNumberFormat.TryParse(value, out double d)) throw new InvalidDataException($"Número inválido '{value}'.");
                    return GlauxValue.FromNumber(d);
                case GlauxValueKind.Integer: return GlauxValue.FromInteger(long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
                case GlauxValueKind.Boolean: return GlauxValue.FromBoolean(bool.Parse(value));
                case GlauxValueKind.Text: return GlauxValue.FromText(data == "null" && value.Length == 0 ? null : value);
                case GlauxValueKind.Point:
                    {
                        var p = SplitNumbers(value, 3);
                        return GlauxValue.FromPoint(p[0], p[1], p[2]);
                    }
                case GlauxValueKind.Vector:
                    {
                        var p = SplitNumbers(value, 3);
                        return GlauxValue.FromVector(p[0], p[1], p[2]);
                    }
                case GlauxValueKind.Interval:
                    {
                        var p = SplitNumbers(value, 2);
                        return GlauxValue.FromInterval(p[0], p[1]);
                    }
                case GlauxValueKind.Colour: return GlauxValue.FromColour(TreeJsonCodec.ParseColour(value));
                case GlauxValueKind.Time: return GlauxValue.FromTime(TreeJsonCodec.ParseTime(value));
                case GlauxValueKind.Guid: return GlauxValue.FromGuid(Guid.Parse(value));
                default: throw new InvalidDataException($"Tipo '{type}' não suportado no CSV.");
            }
        }

        private static double[] SplitNumbers(string text, int count)
        {
            var parts = (text ?? "").Split(';');
            if (parts.Length != count) throw new InvalidDataException($"Esperados {count} números separados por ';' em '{text}'.");
            var result = new double[count];
            for (int i = 0; i < count; i++)
            {
                if (!GlauxNumberFormat.TryParse(parts[i], out result[i])) throw new InvalidDataException($"Número inválido '{parts[i]}'.");
            }
            return result;
        }

        /// <summary>Parser RFC 4180: aspas, aspas duplicadas e quebras de linha dentro de campos.</summary>
        internal static List<List<string>> ParseRows(string text, char delimiter)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            int i = 0;
            if (text.Length > 0 && text[0] == '﻿') i = 1;

            for (; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                    continue;
                }

                if (c == '"' && field.Length == 0)
                {
                    inQuotes = true;
                }
                else if (c == delimiter)
                {
                    row.Add(field.ToString());
                    field.Clear();
                }
                else if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                }
                else
                {
                    field.Append(c);
                }
            }

            if (inQuotes) throw new InvalidDataException("CSV com aspas não fechadas.");
            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }
            return rows;
        }
    }
}
