using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GH_IO.Serialization;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools.Data
{
    /// <summary>Formatos de import/export de árvores. Os valores fazem parte das entradas dos componentes.</summary>
    public enum TreeFormat
    {
        Json = 0,
        Csv = 1,
        Binary = 2,
        PillData = 3
    }

    /// <summary>
    /// Adapters de formato sobre o mesmo modelo canônico: um único caminho de conversão para todos os formatos,
    /// em vez de um componente por formato.
    /// </summary>
    public static class TreeFormats
    {
        public static bool TryParse(string text, out TreeFormat format)
        {
            switch ((text ?? "").Trim().TrimStart('.').ToLowerInvariant())
            {
                case "0":
                case "json":
                    format = TreeFormat.Json; return true;
                case "1":
                case "csv":
                case "tsv":
                    format = TreeFormat.Csv; return true;
                case "2":
                case "bin":
                case "binary":
                case "glxt":
                case "base64":
                    format = TreeFormat.Binary; return true;
                case "3":
                case "pilldata":
                case "ghdata":
                    format = TreeFormat.PillData; return true;
                default:
                    format = TreeFormat.Json; return false;
            }
        }

        public static bool TryFromExtension(string path, out TreeFormat format)
        {
            return TryParse(Path.GetExtension(path ?? ""), out format);
        }

        public static string ExtensionFor(TreeFormat format)
        {
            switch (format)
            {
                case TreeFormat.Csv: return ".csv";
                case TreeFormat.Binary: return ".glxt";
                case TreeFormat.PillData: return ".pilldata";
                default: return ".json";
            }
        }

        public static bool IsTextual(TreeFormat format) => format == TreeFormat.Json || format == TreeFormat.Csv;

        /// <summary>Detecta o formato pelo conteúdo: JSON ({), CSV longo (cabeçalho path,...) ou Base64 de GLXT/.pilldata.</summary>
        public static TreeFormat DetectText(string text)
        {
            string t = (text ?? "").TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            if (t.StartsWith("{", StringComparison.Ordinal)) return TreeFormat.Json;
            if (t.StartsWith("path", StringComparison.OrdinalIgnoreCase)) return TreeFormat.Csv;
            // "R0xYV" = Base64 de "GLXT"
            if (t.StartsWith("R0xYV", StringComparison.Ordinal)) return TreeFormat.Binary;
            return TreeFormat.PillData;
        }

        /// <summary>Detecta o formato de um arquivo pelo conteúdo (assinatura GLXT ou texto).</summary>
        public static TreeFormat DetectBytes(byte[] bytes)
        {
            if (bytes != null && bytes.Length >= 4 && bytes[0] == 'G' && bytes[1] == 'L' && bytes[2] == 'X' && bytes[3] == 'T') return TreeFormat.Binary;
            int start = bytes != null && bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            for (int i = start; bytes != null && i < bytes.Length && i < start + 64; i++)
            {
                byte b = bytes[i];
                if (b == ' ' || b == '\t' || b == '\r' || b == '\n') continue;
                if (b == '{') return TreeFormat.Json;
                if (b == 'p' || b == 'P') return TreeFormat.Csv;
                break;
            }
            return TreeFormat.PillData;
        }

        /// <summary>Serializa para texto. Formatos binários saem em Base64 (para transportar como texto).</summary>
        public static string ToText(GH_Structure<IGH_Goo> tree, GlauxTreeTable table, TreeFormat format, bool pretty, char csvDelimiter)
        {
            switch (format)
            {
                case TreeFormat.Json: return TreeJsonCodec.Serialize(table, pretty);
                case TreeFormat.Csv: return TreeCsvCodec.Serialize(table, csvDelimiter);
                case TreeFormat.Binary: return Convert.ToBase64String(TreeBinaryCodec.Encode(table));
                case TreeFormat.PillData: return Convert.ToBase64String(PillDataCodec.Encode(tree, table.Metadata));
                default: throw new ArgumentOutOfRangeException(nameof(format));
            }
        }

        public static GlauxTreeTable FromText(string text, TreeFormat format, char csvDelimiter, ICollection<string> warnings)
        {
            switch (format)
            {
                case TreeFormat.Json: return TreeJsonCodec.Deserialize(text);
                case TreeFormat.Csv: return TreeCsvCodec.Deserialize(text, csvDelimiter);
                case TreeFormat.Binary: return TreeBinaryCodec.Decode(FromBase64(text));
                case TreeFormat.PillData: return TreeMapper.ToTable(PillDataCodec.Decode(FromBase64(text)), warnings);
                default: throw new ArgumentOutOfRangeException(nameof(format));
            }
        }

        public static byte[] ToFileBytes(GH_Structure<IGH_Goo> tree, GlauxTreeTable table, TreeFormat format, bool pretty, char csvDelimiter)
        {
            switch (format)
            {
                case TreeFormat.Binary: return TreeBinaryCodec.Encode(table);
                case TreeFormat.PillData: return PillDataCodec.Encode(tree, table.Metadata);
                default:
                    // UTF-8 com BOM: o Excel reconhece acentos no CSV
                    var utf8Bom = new UTF8Encoding(true);
                    var preamble = utf8Bom.GetPreamble();
                    var body = utf8Bom.GetBytes(ToText(tree, table, format, pretty, csvDelimiter));
                    var all = new byte[preamble.Length + body.Length];
                    Buffer.BlockCopy(preamble, 0, all, 0, preamble.Length);
                    Buffer.BlockCopy(body, 0, all, preamble.Length, body.Length);
                    return all;
            }
        }

        public static GlauxTreeTable FromFileBytes(byte[] bytes, TreeFormat format, char csvDelimiter, ICollection<string> warnings)
        {
            switch (format)
            {
                case TreeFormat.Binary: return TreeBinaryCodec.Decode(bytes);
                case TreeFormat.PillData: return TreeMapper.ToTable(PillDataCodec.Decode(bytes), warnings);
                default: return FromText(new UTF8Encoding(false).GetString(bytes).TrimStart('﻿'), format, csvDelimiter, warnings);
            }
        }

        private static byte[] FromBase64(string text)
        {
            try
            {
                return Convert.FromBase64String((text ?? "").Trim());
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException("Texto não é Base64 válido para o formato binário.", ex);
            }
        }
    }

    /// <summary>
    /// Formato .pilldata do Pill Disk Save/Load (GH_Archive com o chunk "PillTree"), para que arquivos
    /// antigos e novos sejam intercambiáveis entre os componentes.
    /// </summary>
    public static class PillDataCodec
    {
        public static byte[] Encode(GH_Structure<IGH_Goo> tree, IDictionary<string, string> metadata = null)
        {
            tree = tree ?? new GH_Structure<IGH_Goo>();
            var archive = new GH_Archive();
            archive.AppendObject(tree, "PillTree");
            var meta = archive.GetRootNode.CreateChunk("PillMeta");
            meta.SetDate("Timestamp", DateTime.Now);
            meta.SetInt32("PathCount", tree.PathCount);
            meta.SetInt32("DataCount", tree.DataCount);
            meta.SetString("Plugin", "Glaux Tools");
            if (metadata != null && metadata.Count > 0)
            {
                meta.SetInt32("GlauxMetaCount", metadata.Count);
                int i = 0;
                foreach (var kv in metadata)
                {
                    meta.SetString("GlauxMetaKey", i, kv.Key);
                    meta.SetString("GlauxMetaValue", i, kv.Value ?? "");
                    i++;
                }
            }
            return archive.Serialize_Binary();
        }

        public static GH_Structure<IGH_Goo> Decode(byte[] bytes)
        {
            var archive = new GH_Archive();
            if (!archive.Deserialize_Binary(bytes)) throw new InvalidDataException("Arquivo .pilldata inválido ou corrompido.");
            var tree = new GH_Structure<IGH_Goo>();
            foreach (string chunk in new[] { "PillTree", "Tree", "Data" })
            {
                try
                {
                    if (archive.ExtractObject(tree, chunk)) return tree;
                }
                catch
                {
                    tree = new GH_Structure<IGH_Goo>();
                }
            }

            // GH_Structure.Read recusa árvores sem ramos: o PillMeta distingue "vazia" de "corrompida"
            var meta = archive.GetRootNode?.FindChunk("PillMeta");
            if (meta != null && meta.ItemExists("PathCount") && meta.GetInt32("PathCount") == 0)
            {
                return new GH_Structure<IGH_Goo>();
            }
            throw new InvalidDataException("Nenhuma árvore encontrada no arquivo (.pilldata/.ghdata).");
        }
    }

    /// <summary>Escrita atômica: grava num temporário ao lado e substitui o destino, sem deixar arquivo pela metade.</summary>
    public static class AtomicFile
    {
        public static void WriteAllBytes(string path, byte[] bytes)
        {
            string full = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string tmp = full + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            try
            {
                if (File.Exists(full))
                {
                    File.Replace(tmp, full, null);
                }
                else
                {
                    File.Move(tmp, full);
                }
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                throw;
            }
        }
    }
}
