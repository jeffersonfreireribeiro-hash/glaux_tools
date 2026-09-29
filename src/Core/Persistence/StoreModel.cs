using System;
using System.Collections.Generic;
using Buraqueira_Tools.Data;

namespace Buraqueira_Tools.Persistence
{
    /// <summary>Tipos de entrada usados pelas pilhas (o store aceita qualquer texto).</summary>
    public static class StoreKinds
    {
        public const string Dataset = "dataset";
        public const string Snapshot = "snapshot";
        public const string Experiment = "experiment";
        public const string Metrics = "metrics";
    }

    /// <summary>Nome padrão da árvore principal de uma entrada.</summary>
    public static class StoreTreeNames
    {
        public const string Data = "data";
    }

    /// <summary>Descrição (sem os dados) de uma árvore guardada numa entrada.</summary>
    public sealed class StoreTreeInfo
    {
        internal StoreTreeInfo(string name, string hash, int branchCount, int itemCount, int length)
        {
            Name = name;
            Hash = hash;
            BranchCount = branchCount;
            ItemCount = itemCount;
            Length = length;
        }

        public string Name { get; }

        /// <summary>SHA-256 de identidade dos dados (<see cref="TreeHash"/>).</summary>
        public string Hash { get; }
        public int BranchCount { get; }
        public int ItemCount { get; }

        /// <summary>Tamanho em bytes do bloco binário da árvore.</summary>
        public int Length { get; }
    }

    /// <summary>
    /// Cabeçalho indexado de uma entrada: identidade, tipo, chave, revisão, data, metadados e resumo das árvores.
    /// Ler cabeçalhos não carrega os dados das árvores.
    /// </summary>
    public sealed class StoreEntryHeader
    {
        internal StoreEntryHeader(Guid id, string kind, string key, long revision, DateTime timestampUtc,
            Dictionary<string, string> metadata, List<StoreTreeInfo> trees, long recordOffset, int headerLength)
        {
            Id = id;
            Kind = kind;
            Key = key;
            Revision = revision;
            TimestampUtc = timestampUtc;
            Metadata = metadata;
            Trees = trees;
            RecordOffset = recordOffset;
            HeaderLength = headerLength;
        }

        public Guid Id { get; }
        public string Kind { get; }
        public string Key { get; }
        public long Revision { get; }
        public DateTime TimestampUtc { get; }
        public IReadOnlyDictionary<string, string> Metadata { get; }
        public IReadOnlyList<StoreTreeInfo> Trees { get; }

        internal long RecordOffset { get; }
        internal int HeaderLength { get; }

        public string Reference => $"{Key}@{Revision}";

        public StoreTreeInfo FindTree(string name)
        {
            foreach (var t in Trees)
            {
                if (string.Equals(t.Name, name, StringComparison.Ordinal)) return t;
            }
            return null;
        }

        public int TotalItems
        {
            get
            {
                int n = 0;
                foreach (var t in Trees) n += t.ItemCount;
                return n;
            }
        }

        public override string ToString() => $"[{Kind}] {Key}@{Revision} ({TimestampUtc:yyyy-MM-dd HH:mm:ss}Z, {Trees.Count} árvore(s))";
    }

    /// <summary>Entrada a gravar: tipo, chave, metadados e árvores nomeadas.</summary>
    public sealed class StoreEntryDraft
    {
        public StoreEntryDraft(string kind, string key)
        {
            Kind = string.IsNullOrWhiteSpace(kind) ? StoreKinds.Dataset : kind.Trim();
            Key = key?.Trim() ?? "";
        }

        public string Kind { get; }
        public string Key { get; }
        public Dictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public List<KeyValuePair<string, GlauxTreeTable>> Trees { get; } = new List<KeyValuePair<string, GlauxTreeTable>>();

        /// <summary>Data da entrada (padrão: agora). Útil para testes e importação de históricos.</summary>
        public DateTime? TimestampUtc { get; set; }

        public StoreEntryDraft AddTree(string name, GlauxTreeTable table)
        {
            Trees.Add(new KeyValuePair<string, GlauxTreeTable>(name, table ?? new GlauxTreeTable()));
            return this;
        }
    }

    /// <summary>Resultado de uma gravação.</summary>
    public sealed class StoreAppendResult
    {
        internal StoreAppendResult(StoreEntryHeader header, bool written)
        {
            Header = header;
            Written = written;
        }

        public StoreEntryHeader Header { get; }

        /// <summary>False quando a gravação foi pulada porque a última revisão já tinha os mesmos dados e metadados.</summary>
        public bool Written { get; }
    }

    /// <summary>Estatísticas do arquivo do store.</summary>
    public sealed class StoreStats
    {
        public string Path { get; internal set; }
        public long FileBytes { get; internal set; }
        public int EntryCount { get; internal set; }
        public int KeyCount { get; internal set; }
        public int FormatVersion { get; internal set; }
        public bool ReadOnly { get; internal set; }

        /// <summary>Bytes no fim do arquivo descartados por uma gravação interrompida (serão removidos na próxima escrita).</summary>
        public long TornTailBytes { get; internal set; }

        /// <summary>Mensagem quando há corrupção no meio do arquivo (o store fica somente leitura).</summary>
        public string CorruptionMessage { get; internal set; }
    }

    public class GlauxStoreException : Exception
    {
        public GlauxStoreException(string message) : base(message) { }
        public GlauxStoreException(string message, Exception inner) : base(message, inner) { }
    }
}
