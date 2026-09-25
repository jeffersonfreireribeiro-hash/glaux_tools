using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Serializador e parser JSON puro, ultraleve e sem dependências externas.
    /// Funciona perfeitamente em qualquer versão do Rhino (Rhino 7, Rhino 8 .NET 7/8 Core e .NET Framework 4.8).
    /// </summary>
    public static class PillJson
    {
        public static string Serialize(object obj, bool pretty = false, int maxItems = -1)
        {
            var sb = new StringBuilder();
            SerializeValue(obj, sb, pretty, 0, maxItems);
            return sb.ToString();
        }

        private static void SerializeValue(object obj, StringBuilder sb, bool pretty, int indent, int maxItems = -1)
        {
            if (obj == null)
            {
                sb.Append("null");
                return;
            }

            if (obj is bool b)
            {
                sb.Append(b ? "true" : "false");
                return;
            }

            if (obj is string s)
            {
                sb.Append("\"");
                foreach (char c in s)
                {
                    switch (c)
                    {
                        case '\"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 32)
                                sb.AppendFormat("\\u{0:x4}", (int)c);
                            else
                                sb.Append(c);
                            break;
                    }
                }
                sb.Append("\"");
                return;
            }

            if (obj is double || obj is float || obj is decimal)
            {
                double d = Convert.ToDouble(obj, CultureInfo.InvariantCulture);
                if (double.IsNaN(d) || double.IsInfinity(d))
                    sb.Append("null");
                else
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (obj is int || obj is long || obj is short || obj is byte || obj is sbyte || obj is uint || obj is ulong || obj is ushort)
            {
                sb.Append(Convert.ToInt64(obj, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (obj is DateTime dt)
            {
                sb.Append("\"").Append(dt.ToString("o", CultureInfo.InvariantCulture)).Append("\"");
                return;
            }

            if (obj is IDictionary dict)
            {
                sb.Append("{");
                bool first = true;
                foreach (DictionaryEntry entry in dict)
                {
                    if (!first) sb.Append(",");
                    if (pretty) { sb.AppendLine(); sb.Append(new string(' ', (indent + 1) * 2)); }
                    sb.Append("\"").Append(entry.Key?.ToString()).Append("\":");
                    if (pretty) sb.Append(" ");
                    SerializeValue(entry.Value, sb, pretty, indent + 1, maxItems);
                    first = false;
                }
                if (pretty && !first) { sb.AppendLine(); sb.Append(new string(' ', indent * 2)); }
                sb.Append("}");
                return;
            }

            if (obj is IEnumerable list && !(obj is string))
            {
                sb.Append("[");
                bool first = true;
                int count = 0;
                foreach (var item in list)
                {
                    count++;
                    if (maxItems > 0 && count > maxItems)
                    {
                        if (!first) sb.Append(",");
                        if (pretty) { sb.AppendLine(); sb.Append(new string(' ', (indent + 1) * 2)); }
                        sb.Append($"\"... (+{count} itens truncados no JSON para preservação de memória e performance)\"");
                        break;
                    }
                    if (!first) sb.Append(",");
                    if (pretty) { sb.AppendLine(); sb.Append(new string(' ', (indent + 1) * 2)); }
                    SerializeValue(item, sb, pretty, indent + 1, maxItems);
                    first = false;
                }
                if (pretty && !first) { sb.AppendLine(); sb.Append(new string(' ', indent * 2)); }
                sb.Append("]");
                return;
            }

            // Fallback seguro para string com try-catch (proteção para objetos terceiros tipo Wallacei)
            string strVal;
            try
            {
                strVal = obj?.ToString() ?? "null";
            }
            catch
            {
                strVal = obj?.GetType()?.Name ?? "item";
            }
            SerializeValue(strVal, sb, pretty, indent, maxItems);
        }

        public static object Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            int index = 0;
            return ParseValue(json, ref index);
        }

        public static Dictionary<string, object> DeserializeObject(string json)
        {
            var res = Deserialize(json);
            return res as Dictionary<string, object>;
        }

        private static void SkipWhitespace(string s, ref int index)
        {
            while (index < s.Length && char.IsWhiteSpace(s[index]))
                index++;
        }

        private static object ParseValue(string s, ref int index)
        {
            SkipWhitespace(s, ref index);
            if (index >= s.Length) return null;

            char c = s[index];
            if (c == '{') return ParseObject(s, ref index);
            if (c == '[') return ParseArray(s, ref index);
            if (c == '\"') return ParseString(s, ref index);
            if (c == 't' || c == 'f') return ParseBool(s, ref index);
            if (c == 'n') return ParseNull(s, ref index);
            if (c == '-' || char.IsDigit(c)) return ParseNumber(s, ref index);

            throw new FormatException($"Caractere inesperado no JSON '{c}' na posição {index}");
        }

        private static Dictionary<string, object> ParseObject(string s, ref int index)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            index++; // pula '{'

            while (index < s.Length)
            {
                SkipWhitespace(s, ref index);
                if (index >= s.Length) break;
                if (s[index] == '}') { index++; return dict; }

                if (s[index] == ',') { index++; continue; }

                string key = ParseString(s, ref index);
                SkipWhitespace(s, ref index);
                if (index < s.Length && s[index] == ':') index++; // pula ':'

                object val = ParseValue(s, ref index);
                dict[key] = val;

                SkipWhitespace(s, ref index);
                if (index < s.Length && s[index] == ',') index++;
                else if (index < s.Length && s[index] == '}') { index++; return dict; }
            }
            return dict;
        }

        private static List<object> ParseArray(string s, ref int index)
        {
            var list = new List<object>();
            index++; // pula '['

            while (index < s.Length)
            {
                SkipWhitespace(s, ref index);
                if (index >= s.Length) break;
                if (s[index] == ']') { index++; return list; }

                if (s[index] == ',') { index++; continue; }

                object item = ParseValue(s, ref index);
                list.Add(item);

                SkipWhitespace(s, ref index);
                if (index < s.Length && s[index] == ',') index++;
                else if (index < s.Length && s[index] == ']') { index++; return list; }
            }
            return list;
        }

        private static string ParseString(string s, ref int index)
        {
            index++; // pula '\"'
            var sb = new StringBuilder();
            while (index < s.Length)
            {
                char c = s[index++];
                if (c == '\"') return sb.ToString();
                if (c == '\\' && index < s.Length)
                {
                    char esc = s[index++];
                    switch (esc)
                    {
                        case '\"': sb.Append('\"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (index + 4 <= s.Length)
                            {
                                string hex = s.Substring(index, 4);
                                sb.Append((char)Convert.ToInt32(hex, 16));
                                index += 4;
                            }
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static bool ParseBool(string s, ref int index)
        {
            if (s.Substring(index).StartsWith("true", StringComparison.OrdinalIgnoreCase))
            {
                index += 4;
                return true;
            }
            if (s.Substring(index).StartsWith("false", StringComparison.OrdinalIgnoreCase))
            {
                index += 5;
                return false;
            }
            throw new FormatException($"Valor booleano inválido na posição {index}");
        }

        private static object ParseNull(string s, ref int index)
        {
            if (s.Substring(index).StartsWith("null", StringComparison.OrdinalIgnoreCase))
            {
                index += 4;
                return null;
            }
            throw new FormatException($"Valor null inválido na posição {index}");
        }

        private static object ParseNumber(string s, ref int index)
        {
            int start = index;
            bool isFloating = false;
            if (s[index] == '-') index++;
            while (index < s.Length && (char.IsDigit(s[index]) || s[index] == '.' || s[index] == 'e' || s[index] == 'E' || s[index] == '+' || s[index] == '-'))
            {
                if (s[index] == '.' || s[index] == 'e' || s[index] == 'E') isFloating = true;
                index++;
            }

            string numStr = s.Substring(start, index - start);
            if (isFloating && double.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal))
            {
                return dVal;
            }
            if (long.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out long lVal))
            {
                if (lVal >= int.MinValue && lVal <= int.MaxValue) return (int)lVal;
                return lVal;
            }
            if (double.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double dFallback))
            {
                return dFallback;
            }
            return 0;
        }
    }
}
