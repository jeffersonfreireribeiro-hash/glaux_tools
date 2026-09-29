using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools.Data
{
    /// <summary>
    /// Formato binário canônico e compacto do modelo de árvore (usado pelo store e pelo hash de identidade).
    ///
    /// Layout (little-endian, strings UTF-8 com prefixo de tamanho do BinaryWriter):
    ///   "GLXT" | u16 versão | u16 flags (bit0 = contém metadados)
    ///   [metadados: i32 n, (string chave, string valor) * n]           (se bit0)
    ///   i32 nTipos, string * nTipos                                     (tabela de tags de blob)
    ///   i32 nRamos, por ramo: i32 profundidade, i32 * profundidade, i32 nItens,
    ///     por item: u8 kind + payload
    ///       Number: f64 | Integer: i64 | Boolean: u8 | Text: u8 presente + string
    ///       Point/Vector: f64 x3 | Interval: f64 x2 | Colour: i32 ARGB | Time: i64 (DateTime.ToBinary)
    ///       Guid: 16 bytes | Blob: i32 índice do tipo, u8 texto presente + string, i32 tamanho (-1 = sem bytes) + bytes
    /// </summary>
    public static class TreeBinaryCodec
    {
        public const ushort FormatVersion = 1;
        private const ushort FlagMetadata = 1;
        private static readonly byte[] Magic = { (byte)'G', (byte)'L', (byte)'X', (byte)'T' };
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static byte[] Encode(GlauxTreeTable table, bool includeMetadata = true)
        {
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, Utf8, true))
                {
                    Write(w, table, includeMetadata);
                }
                return ms.ToArray();
            }
        }

        public static GlauxTreeTable Decode(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using (var ms = new MemoryStream(data, false))
            using (var r = new BinaryReader(ms, Utf8, true))
            {
                return Read(r, data.Length);
            }
        }

        public static void Write(BinaryWriter w, GlauxTreeTable table, bool includeMetadata)
        {
            table = table ?? new GlauxTreeTable();
            bool withMeta = includeMetadata && table.Metadata.Count > 0;

            w.Write(Magic);
            w.Write(FormatVersion);
            w.Write(withMeta ? FlagMetadata : (ushort)0);

            if (withMeta)
            {
                // Ordem estável para que o mesmo conteúdo gere sempre os mesmos bytes
                var keys = new List<string>(table.Metadata.Keys);
                keys.Sort(StringComparer.Ordinal);
                w.Write(keys.Count);
                foreach (var k in keys)
                {
                    w.Write(k);
                    w.Write(table.Metadata[k] ?? "");
                }
            }

            var typeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            var types = new List<string>();
            foreach (var branch in table.Branches)
            {
                foreach (var v in branch.Items)
                {
                    if (v.Kind == GlauxValueKind.Blob && !typeIndex.ContainsKey(v.TypeTag))
                    {
                        typeIndex[v.TypeTag] = types.Count;
                        types.Add(v.TypeTag);
                    }
                }
            }
            w.Write(types.Count);
            foreach (var t in types) w.Write(t);

            w.Write(table.Branches.Count);
            foreach (var branch in table.Branches)
            {
                w.Write(branch.Path.Length);
                foreach (int idx in branch.Path) w.Write(idx);
                w.Write(branch.Items.Count);
                foreach (var v in branch.Items) WriteValue(w, v, typeIndex);
            }
        }

        private static void WriteValue(BinaryWriter w, GlauxValue v, Dictionary<string, int> typeIndex)
        {
            w.Write((byte)v.Kind);
            switch (v.Kind)
            {
                case GlauxValueKind.Null:
                    break;
                case GlauxValueKind.Number:
                    w.Write(v.X);
                    break;
                case GlauxValueKind.Integer:
                case GlauxValueKind.Time:
                    w.Write(v.Int);
                    break;
                case GlauxValueKind.Boolean:
                    w.Write((byte)(v.BooleanValue ? 1 : 0));
                    break;
                case GlauxValueKind.Text:
                    WriteOptionalString(w, v.Text);
                    break;
                case GlauxValueKind.Point:
                case GlauxValueKind.Vector:
                    w.Write(v.X);
                    w.Write(v.Y);
                    w.Write(v.Z);
                    break;
                case GlauxValueKind.Interval:
                    w.Write(v.X);
                    w.Write(v.Y);
                    break;
                case GlauxValueKind.Colour:
                    w.Write((int)v.Int);
                    break;
                case GlauxValueKind.Guid:
                    w.Write(v.GuidValue.ToByteArray());
                    break;
                case GlauxValueKind.Blob:
                    w.Write(typeIndex[v.TypeTag]);
                    WriteOptionalString(w, v.Text);
                    if (v.Blob == null)
                    {
                        w.Write(-1);
                    }
                    else
                    {
                        w.Write(v.Blob.Length);
                        w.Write(v.Blob);
                    }
                    break;
                default:
                    throw new InvalidDataException($"Tipo de valor desconhecido: {v.Kind}");
            }
        }

        public static GlauxTreeTable Read(BinaryReader r, long availableBytes)
        {
            byte[] magic = r.ReadBytes(4);
            if (magic.Length != 4 || magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3])
            {
                throw new InvalidDataException("Dados não são uma árvore Glaux (assinatura GLXT ausente).");
            }
            ushort version = r.ReadUInt16();
            if (version > FormatVersion)
            {
                throw new InvalidDataException($"Árvore Glaux versão {version} é mais nova que a suportada ({FormatVersion}). Atualize o Glaux Tools.");
            }
            ushort flags = r.ReadUInt16();

            var table = new GlauxTreeTable();
            if ((flags & FlagMetadata) != 0)
            {
                int metaCount = ReadCount(r, availableBytes, "metadados");
                for (int i = 0; i < metaCount; i++)
                {
                    string k = r.ReadString();
                    table.Metadata[k] = r.ReadString();
                }
            }

            int typeCount = ReadCount(r, availableBytes, "tipos");
            var types = new string[typeCount];
            for (int i = 0; i < typeCount; i++) types[i] = r.ReadString();

            int branchCount = ReadCount(r, availableBytes, "ramos");
            table.Branches.Capacity = branchCount;
            for (int b = 0; b < branchCount; b++)
            {
                int depth = ReadCount(r, availableBytes, "profundidade");
                var path = new int[depth];
                for (int d = 0; d < depth; d++) path[d] = r.ReadInt32();
                int itemCount = ReadCount(r, availableBytes, "itens");
                var items = new List<GlauxValue>(itemCount);
                for (int i = 0; i < itemCount; i++) items.Add(ReadValue(r, types, availableBytes));
                table.Branches.Add(new GlauxBranch(path, items));
            }
            return table;
        }

        private static GlauxValue ReadValue(BinaryReader r, string[] types, long availableBytes)
        {
            var kind = (GlauxValueKind)r.ReadByte();
            switch (kind)
            {
                case GlauxValueKind.Null: return GlauxValue.Null;
                case GlauxValueKind.Number: return GlauxValue.FromNumber(r.ReadDouble());
                case GlauxValueKind.Integer: return GlauxValue.FromInteger(r.ReadInt64());
                case GlauxValueKind.Time: return GlauxValue.FromRaw(GlauxValueKind.Time, null, 0, 0, 0, r.ReadInt64(), null, null);
                case GlauxValueKind.Boolean: return GlauxValue.FromBoolean(r.ReadByte() != 0);
                case GlauxValueKind.Text: return GlauxValue.FromText(ReadOptionalString(r));
                case GlauxValueKind.Point: return GlauxValue.FromPoint(r.ReadDouble(), r.ReadDouble(), r.ReadDouble());
                case GlauxValueKind.Vector: return GlauxValue.FromVector(r.ReadDouble(), r.ReadDouble(), r.ReadDouble());
                case GlauxValueKind.Interval: return GlauxValue.FromInterval(r.ReadDouble(), r.ReadDouble());
                case GlauxValueKind.Colour: return GlauxValue.FromColour(r.ReadInt32());
                case GlauxValueKind.Guid: return GlauxValue.FromGuid(new Guid(r.ReadBytes(16)));
                case GlauxValueKind.Blob:
                    {
                        int typeIdx = r.ReadInt32();
                        if (typeIdx < 0 || typeIdx >= types.Length) throw new InvalidDataException("Índice de tipo inválido na árvore Glaux.");
                        string text = ReadOptionalString(r);
                        int len = r.ReadInt32();
                        byte[] blob = null;
                        if (len >= 0)
                        {
                            if (len > availableBytes) throw new InvalidDataException("Tamanho de blob maior que os dados disponíveis.");
                            blob = r.ReadBytes(len);
                            if (blob.Length != len) throw new EndOfStreamException("Blob truncado na árvore Glaux.");
                        }
                        return GlauxValue.FromBlob(types[typeIdx], text, blob);
                    }
                default:
                    throw new InvalidDataException($"Tipo de valor desconhecido na árvore Glaux: {(int)kind}.");
            }
        }

        private static int ReadCount(BinaryReader r, long availableBytes, string what)
        {
            int n = r.ReadInt32();
            if (n < 0 || n > availableBytes) throw new InvalidDataException($"Contagem de {what} inválida ({n}) na árvore Glaux.");
            return n;
        }

        private static void WriteOptionalString(BinaryWriter w, string s)
        {
            w.Write((byte)(s != null ? 1 : 0));
            if (s != null) w.Write(s);
        }

        private static string ReadOptionalString(BinaryReader r)
        {
            return r.ReadByte() != 0 ? r.ReadString() : null;
        }
    }

    /// <summary>
    /// Hash de identidade exato (SHA-256) dos dados de uma árvore, sobre o formato binário canônico sem metadados.
    /// Diferente do fingerprint tolerante de <see cref="PillDataFingerprint"/> (detecção de mudança/cache):
    /// aqui qualquer diferença de bit, tipo, caminho, ordem ou nulo produz outro hash.
    /// </summary>
    public static class TreeHash
    {
        public static string Compute(GlauxTreeTable table)
        {
            return Sha256Hex(TreeBinaryCodec.Encode(table, includeMetadata: false));
        }

        public static string Compute(GH_Structure<IGH_Goo> tree)
        {
            return Compute(TreeMapper.ToTable(tree));
        }

        public static string Sha256Hex(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(data);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static string Short(string hash) => string.IsNullOrEmpty(hash) ? "" : (hash.Length > 12 ? hash.Substring(0, 12) : hash);
    }
}
