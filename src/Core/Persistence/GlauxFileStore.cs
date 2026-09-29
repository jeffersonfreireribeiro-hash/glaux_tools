using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Buraqueira_Tools.Data;

namespace Buraqueira_Tools.Persistence
{
    /// <summary>Evento de alteração do store (entradas acrescentadas ou arquivo compactado).</summary>
    public sealed class StoreChangedEventArgs : EventArgs
    {
        internal StoreChangedEventArgs(string path, IReadOnlyList<StoreEntryHeader> added, bool compacted)
        {
            Path = path;
            Added = added;
            Compacted = compacted;
        }

        public string Path { get; }
        public IReadOnlyList<StoreEntryHeader> Added { get; }
        public bool Compacted { get; }

        /// <summary>Componente que originou a gravação (para não notificar a si mesmo). Pode ser vazio.</summary>
        public Guid Origin { get; internal set; }
    }

    /// <summary>
    /// Store local sem dependências externas (.glauxdb): log append-only de entradas versionadas.
    ///
    /// Arquivo:
    ///   cabeçalho 32 bytes: "GLXDB\0\0\0" | u16 versão | u16 flags | u32 reservado | 16 bytes geração (muda a cada compactação)
    ///   registros: u32 "GLXR" | u8 tipo (1 = entrada) | i32 tamanho | payload | u32 CRC-32(payload) | u32 "GLXE"
    ///   payload de entrada: i32 tamanhoCabeçalho | cabeçalho | blocos binários das árvores (TreeBinaryCodec), em sequência
    ///   cabeçalho: u16 formato | 16 bytes id | kind | key | i64 revisão | i64 ticks UTC | metadados | descritores das árvores
    ///
    /// Garantias:
    /// - gravação interrompida no fim do arquivo é ignorada na leitura e descartada na próxima escrita;
    /// - corrupção no meio do arquivo deixa o store somente leitura (as entradas anteriores continuam legíveis);
    /// - CRC verificado ao carregar dados; um único escritor por vez (lock de arquivo), leitores concorrentes;
    /// - nenhum handle fica aberto entre operações (vários documentos/processos podem usar o mesmo arquivo).
    /// </summary>
    public sealed class GlauxFileStore
    {
        public const ushort FormatVersion = 1;
        public const string DefaultExtension = ".glauxdb";

        private static readonly byte[] FileMagic = { (byte)'G', (byte)'L', (byte)'X', (byte)'D', (byte)'B', 0, 0, 0 };
        private const int FileHeaderLength = 32;
        private const uint RecordMarker = 0x52584C47; // "GLXR"
        private const uint EndMarker = 0x45584C47;    // "GLXE"
        private const byte RecordTypeEntry = 1;
        private const int RecordPrefixLength = 4 + 1 + 4;
        private const int RecordSuffixLength = 4 + 4;
        private const ushort EntryFormat = 1;
        private const int WriteRetries = 20;
        private const int WriteRetryDelayMs = 50;

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        private readonly object _sync = new object();
        private readonly List<StoreEntryHeader> _entries = new List<StoreEntryHeader>();
        private Guid _generation = Guid.Empty;
        private long _indexedLength;
        private long _tornTail;
        private DateTime _observedWriteUtc;
        private string _corruption;

        public GlauxFileStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Caminho do store vazio.", nameof(path));
            Path = System.IO.Path.GetFullPath(path);
        }

        public string Path { get; }

        public bool Exists => File.Exists(Path);

        /// <summary>Disparado depois de gravações e compactações feitas por esta instância.</summary>
        public event EventHandler<StoreChangedEventArgs> Changed;

        // =====================================================================
        // LEITURA
        // =====================================================================

        public IReadOnlyList<StoreEntryHeader> ListEntries()
        {
            lock (_sync)
            {
                Refresh();
                return _entries.ToArray();
            }
        }

        public StoreEntryHeader GetLatest(string kind, string key)
        {
            lock (_sync)
            {
                Refresh();
                return FindLatest(kind, key);
            }
        }

        public StoreEntryHeader GetRevision(string kind, string key, long revision)
        {
            lock (_sync)
            {
                Refresh();
                foreach (var e in _entries)
                {
                    if (e.Revision == revision && Matches(e, kind, key)) return e;
                }
                return null;
            }
        }

        public IReadOnlyList<StoreEntryHeader> GetRevisions(string kind, string key)
        {
            lock (_sync)
            {
                Refresh();
                var list = new List<StoreEntryHeader>();
                foreach (var e in _entries)
                {
                    if (Matches(e, kind, key)) list.Add(e);
                }
                list.Sort((a, b) => a.Revision.CompareTo(b.Revision));
                return list;
            }
        }

        public StoreEntryHeader GetById(Guid id)
        {
            lock (_sync)
            {
                Refresh();
                foreach (var e in _entries)
                {
                    if (e.Id == id) return e;
                }
                return null;
            }
        }

        /// <summary>Carrega uma árvore da entrada (verifica o CRC do registro). Nulo se a árvore não existir.</summary>
        public GlauxTreeTable LoadTree(StoreEntryHeader header, string treeName)
        {
            var all = LoadTrees(header, treeName);
            return all.TryGetValue(treeName, out var t) ? t : null;
        }

        /// <summary>Carrega todas as árvores da entrada (ou só <paramref name="onlyTree"/>), verificando o CRC.</summary>
        public Dictionary<string, GlauxTreeTable> LoadTrees(StoreEntryHeader header, string onlyTree = null)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            byte[] payload;
            lock (_sync)
            {
                Refresh();
                // O arquivo pode ter sido compactado desde que o cabeçalho foi obtido: relocaliza pelo id
                StoreEntryHeader current = null;
                foreach (var e in _entries)
                {
                    if (e.Id == header.Id)
                    {
                        current = e;
                        break;
                    }
                }
                if (current == null) throw new GlauxStoreException($"A entrada {header.Reference} não existe mais no store (compactado?).");
                header = current;
                using (var fs = OpenRead())
                {
                    payload = ReadPayloadVerified(fs, header);
                }
            }

            var result = new Dictionary<string, GlauxTreeTable>(StringComparer.Ordinal);
            int offset = 4 + header.HeaderLength;
            foreach (var t in header.Trees)
            {
                if (onlyTree == null || string.Equals(onlyTree, t.Name, StringComparison.Ordinal))
                {
                    var slice = new byte[t.Length];
                    Buffer.BlockCopy(payload, offset, slice, 0, t.Length);
                    result[t.Name] = TreeBinaryCodec.Decode(slice);
                }
                offset += t.Length;
            }
            return result;
        }

        public StoreStats GetStats()
        {
            lock (_sync)
            {
                Refresh();
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var e in _entries) keys.Add(e.Kind + "\u0001" + e.Key);
                return new StoreStats
                {
                    Path = Path,
                    FileBytes = File.Exists(Path) ? new FileInfo(Path).Length : 0,
                    EntryCount = _entries.Count,
                    KeyCount = keys.Count,
                    FormatVersion = FormatVersion,
                    TornTailBytes = _tornTail,
                    CorruptionMessage = _corruption
                };
            }
        }

        // =====================================================================
        // ESCRITA
        // =====================================================================

        /// <summary>Cria o arquivo vazio (só cabeçalho) se ainda não existir.</summary>
        public void EnsureCreated()
        {
            lock (_sync)
            {
                using (var fs = OpenWrite())
                {
                    if (fs.Length == 0) WriteFileHeader(fs, Guid.NewGuid());
                    RefreshFrom(fs);
                }
                ObserveFile();
            }
        }

        /// <summary>
        /// Acrescenta uma nova revisão para (kind, key). Com <paramref name="skipIfUnchanged"/>, não grava quando a última
        /// revisão já tem as mesmas árvores (mesmos hashes) e os mesmos metadados.
        /// </summary>
        public StoreAppendResult Append(StoreEntryDraft draft, bool skipIfUnchanged = false, Guid origin = default(Guid))
        {
            return AppendBatch(new[] { draft }, skipIfUnchanged, origin)[0];
        }

        /// <summary>Grava várias entradas com um único acesso ao arquivo e um único flush (inserção em lote).</summary>
        public IReadOnlyList<StoreAppendResult> AppendBatch(IEnumerable<StoreEntryDraft> drafts, bool skipIfUnchanged = false, Guid origin = default(Guid))
        {
            if (drafts == null) throw new ArgumentNullException(nameof(drafts));

            // Codifica fora do lock de arquivo: é a parte cara
            var prepared = new List<PreparedEntry>();
            foreach (var d in drafts) prepared.Add(Prepare(d));
            if (prepared.Count == 0) return new StoreAppendResult[0];

            var results = new List<StoreAppendResult>(prepared.Count);
            var added = new List<StoreEntryHeader>();
            lock (_sync)
            {
                using (var fs = OpenWrite())
                {
                    if (fs.Length == 0) WriteFileHeader(fs, Guid.NewGuid());
                    RefreshFrom(fs);
                    if (_corruption != null) throw new GlauxStoreException("Store corrompido; gravação bloqueada. " + _corruption);

                    if (_tornTail > 0)
                    {
                        fs.SetLength(_indexedLength);
                        _tornTail = 0;
                    }
                    fs.Position = _indexedLength;

                    foreach (var p in prepared)
                    {
                        var latest = FindLatest(p.Draft.Kind, p.Draft.Key);
                        if (skipIfUnchanged && latest != null && SameContent(latest, p))
                        {
                            results.Add(new StoreAppendResult(latest, false));
                            continue;
                        }

                        long revision = latest == null ? 1 : latest.Revision + 1;
                        var header = WriteRecord(fs, p, revision);
                        _entries.Add(header);
                        added.Add(header);
                        results.Add(new StoreAppendResult(header, true));
                    }

                    _indexedLength = fs.Position;
                    fs.Flush(true);
                }
                ObserveFile();
            }

            if (added.Count > 0)
            {
                Changed?.Invoke(this, new StoreChangedEventArgs(Path, added, false) { Origin = origin });
            }
            return results;
        }

        /// <summary>
        /// Reescreve o arquivo mantendo só as últimas <paramref name="keepRevisionsPerKey"/> revisões de cada (kind, key)
        /// (0 = mantém todas e só elimina lixo de gravações interrompidas). Retorna quantas entradas foram removidas.
        /// </summary>
        public int Compact(int keepRevisionsPerKey)
        {
            int removed;
            lock (_sync)
            {
                string tmp = Path + ".compact.tmp";
                using (var fs = OpenWrite())
                {
                    if (fs.Length == 0) return 0;
                    RefreshFrom(fs);
                    if (_corruption != null) throw new GlauxStoreException("Store corrompido; compactação bloqueada. " + _corruption);

                    var keep = new HashSet<Guid>();
                    var groups = new Dictionary<string, List<StoreEntryHeader>>(StringComparer.Ordinal);
                    foreach (var e in _entries)
                    {
                        string k = e.Kind + "\u0001" + e.Key;
                        if (!groups.TryGetValue(k, out var list)) groups[k] = list = new List<StoreEntryHeader>();
                        list.Add(e);
                    }
                    foreach (var list in groups.Values)
                    {
                        list.Sort((a, b) => b.Revision.CompareTo(a.Revision));
                        int n = keepRevisionsPerKey <= 0 ? list.Count : Math.Min(keepRevisionsPerKey, list.Count);
                        for (int i = 0; i < n; i++) keep.Add(list[i].Id);
                    }

                    removed = _entries.Count - keep.Count;
                    using (var outFs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        WriteFileHeader(outFs, Guid.NewGuid());
                        foreach (var e in _entries)
                        {
                            if (!keep.Contains(e.Id)) continue;
                            long length = RecordLength(fs, e);
                            fs.Position = e.RecordOffset;
                            CopyBytes(fs, outFs, length);
                        }
                        outFs.Flush(true);
                    }
                }

                File.Replace(tmp, Path, null);
                ResetIndex();
                using (var fs = OpenRead()) RefreshFrom(fs);
                ObserveFile();
            }

            Changed?.Invoke(this, new StoreChangedEventArgs(Path, new StoreEntryHeader[0], true));
            return removed;
        }

        // =====================================================================
        // INDEXAÇÃO
        // =====================================================================

        private void Refresh()
        {
            var fi = new FileInfo(Path);
            if (!fi.Exists)
            {
                ResetIndex();
                return;
            }
            if (fi.Length == _indexedLength + _tornTail && fi.LastWriteTimeUtc == _observedWriteUtc && _indexedLength > 0) return;

            using (var fs = OpenRead())
            {
                RefreshFrom(fs);
            }
            _observedWriteUtc = fi.LastWriteTimeUtc;
        }

        private void RefreshFrom(FileStream fs)
        {
            long length = fs.Length;
            if (length == 0)
            {
                ResetIndex();
                return;
            }
            if (length < FileHeaderLength) throw new GlauxStoreException($"Arquivo '{Path}' não é um store Glaux (tamanho {length} bytes).");

            fs.Position = 0;
            var r = new BinaryReader(fs, Utf8, true);
            byte[] magic = r.ReadBytes(8);
            for (int i = 0; i < FileMagic.Length; i++)
            {
                if (magic[i] != FileMagic[i]) throw new GlauxStoreException($"Arquivo '{Path}' não é um store Glaux (assinatura inválida).");
            }
            ushort version = r.ReadUInt16();
            if (version > FormatVersion) throw new GlauxStoreException($"Store versão {version} é mais nova que a suportada ({FormatVersion}). Atualize o Glaux Tools.");
            r.ReadUInt16();
            r.ReadUInt32();
            var generation = new Guid(r.ReadBytes(16));

            // Arquivo substituído (compactação) ou encolhido: reindexa do zero
            if (generation != _generation || length < _indexedLength + _tornTail)
            {
                ResetIndex();
                _generation = generation;
            }

            long pos = Math.Max(_indexedLength, FileHeaderLength);
            _tornTail = 0;
            while (pos < length)
            {
                if (length - pos < RecordPrefixLength)
                {
                    _tornTail = length - pos;
                    break;
                }

                fs.Position = pos;
                uint marker = r.ReadUInt32();
                byte type = r.ReadByte();
                int payloadLength = r.ReadInt32();
                if (marker != RecordMarker || type != RecordTypeEntry || payloadLength < 4)
                {
                    _corruption = $"registro inválido na posição {pos}.";
                    break;
                }

                long end = pos + RecordPrefixLength + payloadLength + RecordSuffixLength;
                if (end > length)
                {
                    // Gravação interrompida: o registro não chegou ao fim
                    _tornTail = length - pos;
                    break;
                }

                fs.Position = end - 4;
                if (r.ReadUInt32() != EndMarker)
                {
                    _corruption = $"fim de registro ausente na posição {end - 4}.";
                    break;
                }

                fs.Position = pos + RecordPrefixLength;
                int headerLength = r.ReadInt32();
                if (headerLength < 0 || headerLength > payloadLength - 4)
                {
                    _corruption = $"cabeçalho de entrada inválido na posição {pos}.";
                    break;
                }

                StoreEntryHeader header;
                try
                {
                    header = ParseHeader(r.ReadBytes(headerLength), pos, headerLength, payloadLength);
                }
                catch (Exception ex)
                {
                    _corruption = $"cabeçalho ilegível na posição {pos}: {ex.Message}";
                    break;
                }

                _entries.Add(header);
                pos = end;
            }

            _indexedLength = pos;
        }

        private void ResetIndex()
        {
            _entries.Clear();
            _indexedLength = 0;
            _tornTail = 0;
            _corruption = null;
            _generation = Guid.Empty;
        }

        private void ObserveFile()
        {
            var fi = new FileInfo(Path);
            _observedWriteUtc = fi.Exists ? fi.LastWriteTimeUtc : default(DateTime);
        }

        // =====================================================================
        // FORMATO
        // =====================================================================

        private sealed class PreparedEntry
        {
            public StoreEntryDraft Draft;
            public List<string> Names = new List<string>();
            public List<string> Hashes = new List<string>();
            public List<byte[]> Blobs = new List<byte[]>();
            public List<int> BranchCounts = new List<int>();
            public List<int> ItemCounts = new List<int>();
        }

        private static PreparedEntry Prepare(StoreEntryDraft draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (string.IsNullOrWhiteSpace(draft.Key)) throw new ArgumentException("A chave da entrada é obrigatória.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            var p = new PreparedEntry { Draft = draft };
            foreach (var kv in draft.Trees)
            {
                if (string.IsNullOrEmpty(kv.Key)) throw new ArgumentException("Árvore sem nome na entrada " + draft.Key);
                if (!names.Add(kv.Key)) throw new ArgumentException($"Árvore '{kv.Key}' repetida na entrada {draft.Key}.");
                p.Names.Add(kv.Key);
                p.Hashes.Add(TreeHash.Compute(kv.Value));
                p.Blobs.Add(TreeBinaryCodec.Encode(kv.Value));
                p.BranchCounts.Add(kv.Value.BranchCount);
                p.ItemCounts.Add(kv.Value.ItemCount);
            }
            return p;
        }

        private static bool SameContent(StoreEntryHeader latest, PreparedEntry p)
        {
            if (latest.Trees.Count != p.Names.Count) return false;
            for (int i = 0; i < p.Names.Count; i++)
            {
                if (!string.Equals(latest.Trees[i].Name, p.Names[i], StringComparison.Ordinal)) return false;
                if (!string.Equals(latest.Trees[i].Hash, p.Hashes[i], StringComparison.Ordinal)) return false;
            }
            if (latest.Metadata.Count != p.Draft.Metadata.Count) return false;
            foreach (var kv in p.Draft.Metadata)
            {
                if (!latest.Metadata.TryGetValue(kv.Key, out var v) || !string.Equals(v, kv.Value ?? "", StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private StoreEntryHeader WriteRecord(FileStream fs, PreparedEntry p, long revision)
        {
            var id = Guid.NewGuid();
            DateTime ts = (p.Draft.TimestampUtc ?? DateTime.UtcNow).ToUniversalTime();
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in p.Draft.Metadata) metadata[kv.Key] = kv.Value ?? "";

            byte[] headerBytes;
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, Utf8, true))
                {
                    w.Write(EntryFormat);
                    w.Write(id.ToByteArray());
                    w.Write(p.Draft.Kind);
                    w.Write(p.Draft.Key);
                    w.Write(revision);
                    w.Write(ts.Ticks);
                    var keys = new List<string>(metadata.Keys);
                    keys.Sort(StringComparer.Ordinal);
                    w.Write(keys.Count);
                    foreach (var k in keys)
                    {
                        w.Write(k);
                        w.Write(metadata[k]);
                    }
                    w.Write(p.Names.Count);
                    for (int i = 0; i < p.Names.Count; i++)
                    {
                        w.Write(p.Names[i]);
                        w.Write(p.Hashes[i]);
                        w.Write(p.BranchCounts[i]);
                        w.Write(p.ItemCounts[i]);
                        w.Write(p.Blobs[i].Length);
                    }
                }
                headerBytes = ms.ToArray();
            }

            int payloadLength = 4 + headerBytes.Length;
            foreach (var b in p.Blobs) payloadLength += b.Length;

            long recordOffset = fs.Position;
            var crc = new Crc32();
            var w2 = new BinaryWriter(fs, Utf8, true);
            w2.Write(RecordMarker);
            w2.Write(RecordTypeEntry);
            w2.Write(payloadLength);
            byte[] headerLen = BitConverter.GetBytes(headerBytes.Length);
            w2.Write(headerLen);
            crc.Update(headerLen);
            w2.Write(headerBytes);
            crc.Update(headerBytes);
            foreach (var b in p.Blobs)
            {
                w2.Write(b);
                crc.Update(b);
            }
            w2.Write(crc.Value);
            w2.Write(EndMarker);
            w2.Flush();

            var trees = new List<StoreTreeInfo>(p.Names.Count);
            for (int i = 0; i < p.Names.Count; i++)
            {
                trees.Add(new StoreTreeInfo(p.Names[i], p.Hashes[i], p.BranchCounts[i], p.ItemCounts[i], p.Blobs[i].Length));
            }
            return new StoreEntryHeader(id, p.Draft.Kind, p.Draft.Key, revision, DateTime.SpecifyKind(ts, DateTimeKind.Utc),
                metadata, trees, recordOffset, headerBytes.Length);
        }

        private static StoreEntryHeader ParseHeader(byte[] bytes, long recordOffset, int headerLength, int payloadLength)
        {
            using (var ms = new MemoryStream(bytes, false))
            using (var r = new BinaryReader(ms, Utf8, true))
            {
                ushort format = r.ReadUInt16();
                if (format > EntryFormat) throw new InvalidDataException($"formato de entrada {format} desconhecido");
                var id = new Guid(r.ReadBytes(16));
                string kind = r.ReadString();
                string key = r.ReadString();
                long revision = r.ReadInt64();
                var ts = new DateTime(r.ReadInt64(), DateTimeKind.Utc);
                int metaCount = r.ReadInt32();
                if (metaCount < 0 || metaCount > bytes.Length) throw new InvalidDataException("contagem de metadados inválida");
                var meta = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 0; i < metaCount; i++)
                {
                    string k = r.ReadString();
                    meta[k] = r.ReadString();
                }
                int treeCount = r.ReadInt32();
                if (treeCount < 0 || treeCount > bytes.Length) throw new InvalidDataException("contagem de árvores inválida");
                var trees = new List<StoreTreeInfo>(treeCount);
                long total = 4 + headerLength;
                for (int i = 0; i < treeCount; i++)
                {
                    string name = r.ReadString();
                    string hash = r.ReadString();
                    int branches = r.ReadInt32();
                    int items = r.ReadInt32();
                    int length = r.ReadInt32();
                    if (length < 0) throw new InvalidDataException("tamanho de árvore inválido");
                    total += length;
                    trees.Add(new StoreTreeInfo(name, hash, branches, items, length));
                }
                if (total != payloadLength) throw new InvalidDataException("tamanhos das árvores não batem com o registro");
                return new StoreEntryHeader(id, kind, key, revision, ts, meta, trees, recordOffset, headerLength);
            }
        }

        private byte[] ReadPayloadVerified(FileStream fs, StoreEntryHeader header)
        {
            var r = new BinaryReader(fs, Utf8, true);
            fs.Position = header.RecordOffset;
            if (r.ReadUInt32() != RecordMarker) throw new GlauxStoreException($"Registro de {header.Reference} não encontrado (arquivo alterado?).");
            r.ReadByte();
            int payloadLength = r.ReadInt32();
            byte[] payload = r.ReadBytes(payloadLength);
            if (payload.Length != payloadLength) throw new GlauxStoreException($"Registro de {header.Reference} truncado.");
            uint stored = r.ReadUInt32();
            if (Crc32.Compute(payload) != stored) throw new GlauxStoreException($"CRC inválido em {header.Reference}: dados corrompidos no disco.");
            return payload;
        }

        private static long RecordLength(FileStream fs, StoreEntryHeader header)
        {
            fs.Position = header.RecordOffset + 5;
            var buf = new byte[4];
            if (fs.Read(buf, 0, 4) != 4) throw new EndOfStreamException();
            return RecordPrefixLength + BitConverter.ToInt32(buf, 0) + RecordSuffixLength;
        }

        private static void WriteFileHeader(Stream s, Guid generation)
        {
            s.Position = 0;
            var w = new BinaryWriter(s, Utf8, true);
            w.Write(FileMagic);
            w.Write(FormatVersion);
            w.Write((ushort)0);
            w.Write(0u);
            w.Write(generation.ToByteArray());
            w.Flush();
        }

        private static void CopyBytes(Stream from, Stream to, long count)
        {
            var buffer = new byte[81920];
            while (count > 0)
            {
                int n = from.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
                if (n <= 0) throw new EndOfStreamException();
                to.Write(buffer, 0, n);
                count -= n;
            }
        }

        private StoreEntryHeader FindLatest(string kind, string key)
        {
            StoreEntryHeader best = null;
            foreach (var e in _entries)
            {
                if (Matches(e, kind, key) && (best == null || e.Revision > best.Revision)) best = e;
            }
            return best;
        }

        private static bool Matches(StoreEntryHeader e, string kind, string key)
        {
            return string.Equals(e.Key, key?.Trim() ?? "", StringComparison.Ordinal) &&
                   (string.IsNullOrEmpty(kind) || string.Equals(e.Kind, kind, StringComparison.Ordinal));
        }

        private FileStream OpenRead()
        {
            return new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }

        private FileStream OpenWrite()
        {
            string dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    // FileShare.Read: um único escritor por vez; leitores continuam funcionando
                    return new FileStream(Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                }
                catch (IOException) when (attempt < WriteRetries)
                {
                    Thread.Sleep(WriteRetryDelayMs);
                }
            }
        }
    }

    /// <summary>CRC-32 (IEEE 802.3, polinômio 0xEDB88320).</summary>
    internal sealed class Crc32
    {
        private static readonly uint[] Table = BuildTable();
        private uint _crc = 0xFFFFFFFF;

        public uint Value => ~_crc;

        public void Update(byte[] data)
        {
            uint c = _crc;
            for (int i = 0; i < data.Length; i++) c = Table[(c ^ data[i]) & 0xFF] ^ (c >> 8);
            _crc = c;
        }

        public static uint Compute(byte[] data)
        {
            var crc = new Crc32();
            crc.Update(data);
            return crc.Value;
        }

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }
    }
}
