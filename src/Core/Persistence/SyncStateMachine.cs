using System;

namespace Buraqueira_Tools.Persistence
{
    public enum SyncDirection
    {
        /// <summary>Grasshopper → store.</summary>
        Push = 0,
        /// <summary>Store → Grasshopper.</summary>
        Pull = 1,
        /// <summary>Nos dois sentidos, conforme quem mudou.</summary>
        TwoWay = 2
    }

    public enum ConflictPolicy
    {
        /// <summary>Não faz nada e sinaliza o conflito.</summary>
        Stop = 0,
        PreferLocal = 1,
        PreferStore = 2
    }

    public enum SyncState
    {
        /// <summary>Local e store coincidem com o último ponto sincronizado.</summary>
        Clean = 0,
        /// <summary>O dado do Grasshopper mudou desde a última sincronização.</summary>
        LocalDirty = 1,
        /// <summary>O store recebeu revisão nova desde a última sincronização.</summary>
        StoreAhead = 2,
        /// <summary>Os dois mudaram.</summary>
        Conflict = 3,
        /// <summary>Não há dados nem no Grasshopper nem no store.</summary>
        Empty = 4
    }

    public enum SyncAction
    {
        None = 0,
        Push = 1,
        Pull = 2
    }

    /// <summary>
    /// Último ponto sincronizado, persistido pelo componente no .gh:
    /// o hash do dado local visto naquele momento e a revisão/hash do store.
    /// Guardar o hash local (e não só o do store) é o que evita o laço GH → DB → GH → DB:
    /// depois de um Pull, o dado local antigo é considerado "já visto" e não é empurrado de volta.
    /// </summary>
    public struct SyncMarker
    {
        public static readonly SyncMarker None = new SyncMarker { StoreRevision = 0, StoreHash = "", LocalHash = "" };

        public long StoreRevision;
        public string StoreHash;
        public string LocalHash;

        public bool IsInitialized => StoreRevision > 0 || !string.IsNullOrEmpty(LocalHash);
    }

    /// <summary>Revisão mais recente do store para a chave sincronizada.</summary>
    public struct StoreHead
    {
        public bool Exists;
        public long Revision;
        public string Hash;
    }

    public sealed class SyncDecision
    {
        public SyncState State { get; internal set; }

        /// <summary>Ação a executar agora (já considera direção, política de conflito e gatilho manual).</summary>
        public SyncAction Action { get; internal set; }

        /// <summary>Ação que seria executada se o gatilho manual fosse acionado.</summary>
        public SyncAction Pending { get; internal set; }

        /// <summary>Marcador a gravar quando não há ação (ex.: dado local igual ao do store — eco suprimido).</summary>
        public SyncMarker? MarkerUpdate { get; internal set; }

        public bool Blocked { get; internal set; }
        public string Reason { get; internal set; }
    }

    /// <summary>
    /// Regras de sincronização Grasshopper ↔ store, puras e testáveis.
    ///
    /// - local mudou  = hash local ≠ hash local do último ponto sincronizado;
    /// - store avançou = revisão do store &gt; revisão sincronizada e hash diferente;
    /// - local igual ao store = limpo (suprime eco quando a saída volta para a entrada);
    /// - direção Push/Pull: a direção define a referência (sem conflito);
    /// - TwoWay com os dois mudados = conflito, resolvido pela política (ou bloqueado);
    /// - modo manual: a ação fica pendente até o gatilho.
    /// </summary>
    public static class SyncStateMachine
    {
        public static SyncDecision Decide(
            bool hasLocal,
            string localHash,
            StoreHead store,
            SyncMarker marker,
            SyncDirection direction,
            ConflictPolicy conflict,
            bool automatic,
            bool trigger)
        {
            var d = new SyncDecision();

            if (!hasLocal && !store.Exists)
            {
                d.State = SyncState.Empty;
                d.Reason = "Sem dados no Grasshopper nem no store.";
                return d;
            }

            // Eco / convergência: o dado local já é exatamente o do store
            if (hasLocal && store.Exists && string.Equals(localHash, store.Hash, StringComparison.Ordinal))
            {
                d.State = SyncState.Clean;
                d.Reason = "Grasshopper e store têm os mesmos dados.";
                if (marker.StoreRevision != store.Revision || !string.Equals(marker.LocalHash, localHash, StringComparison.Ordinal))
                {
                    d.MarkerUpdate = new SyncMarker { StoreRevision = store.Revision, StoreHash = store.Hash, LocalHash = localHash };
                }
                return d;
            }

            bool localChanged = hasLocal && !string.Equals(localHash, marker.LocalHash ?? "", StringComparison.Ordinal);
            bool storeChanged = store.Exists && store.Revision > marker.StoreRevision &&
                                !string.Equals(store.Hash, marker.StoreHash ?? "", StringComparison.Ordinal);

            if (localChanged && storeChanged) d.State = SyncState.Conflict;
            else if (localChanged) d.State = SyncState.LocalDirty;
            else if (storeChanged) d.State = SyncState.StoreAhead;
            else d.State = SyncState.Clean;

            // Direção única: ela define quem manda, então não há conflito a resolver
            SyncAction wanted = SyncAction.None;
            if (direction == SyncDirection.Push)
            {
                if (localChanged)
                {
                    wanted = SyncAction.Push;
                    if (storeChanged) d.Reason = $"Push sobrescreve a revisão {store.Revision} gravada por outra fonte (direção somente Push).";
                }
                else if (storeChanged)
                {
                    d.Reason = $"O store tem revisão nova ({store.Revision}), ignorada porque a direção é somente Push.";
                }
            }
            else if (direction == SyncDirection.Pull)
            {
                if (storeChanged) wanted = SyncAction.Pull;
                else if (localChanged) d.Reason = "O dado do Grasshopper mudou, mas a direção é somente Pull (o store é a referência).";
            }
            else
            {
                switch (d.State)
                {
                    case SyncState.LocalDirty:
                        wanted = SyncAction.Push;
                        break;
                    case SyncState.StoreAhead:
                        wanted = SyncAction.Pull;
                        break;
                    case SyncState.Conflict:
                        if (conflict == ConflictPolicy.PreferLocal) wanted = SyncAction.Push;
                        else if (conflict == ConflictPolicy.PreferStore) wanted = SyncAction.Pull;
                        else
                        {
                            d.Blocked = true;
                            d.Reason = $"Conflito: o Grasshopper e o store (revisão {store.Revision}) mudaram desde a última sincronização. Escolha uma política de conflito.";
                        }
                        break;
                }
            }
            if (d.State == SyncState.Clean) d.Reason = "Nada mudou desde a última sincronização.";

            d.Pending = wanted;
            d.Action = wanted != SyncAction.None && (automatic || trigger) ? wanted : SyncAction.None;
            if (wanted != SyncAction.None && d.Action == SyncAction.None)
            {
                d.Reason = wanted == SyncAction.Push ? "Push pendente: acione Sync." : "Pull pendente: acione Sync.";
            }
            else if (d.Action == SyncAction.Push && d.Reason == null)
            {
                d.Reason = d.State == SyncState.Conflict ? "Conflito resolvido a favor do Grasshopper (Push)." : "Enviando o dado do Grasshopper ao store.";
            }
            else if (d.Action == SyncAction.Pull && d.Reason == null)
            {
                d.Reason = d.State == SyncState.Conflict ? "Conflito resolvido a favor do store (Pull)." : $"Trazendo a revisão {store.Revision} do store.";
            }
            return d;
        }

        /// <summary>Marcador depois de um Push que gravou <paramref name="newRevision"/>.</summary>
        public static SyncMarker AfterPush(string localHash, long newRevision)
        {
            return new SyncMarker { StoreRevision = newRevision, StoreHash = localHash, LocalHash = localHash };
        }

        /// <summary>Marcador depois de um Pull: o dado local atual passa a ser "já visto" e não será reenviado.</summary>
        public static SyncMarker AfterPull(string currentLocalHash, StoreHead store)
        {
            return new SyncMarker { StoreRevision = store.Revision, StoreHash = store.Hash, LocalHash = currentLocalHash ?? "" };
        }
    }
}

namespace Buraqueira_Tools.Persistence
{
    public sealed class SyncOptions
    {
        public SyncDirection Direction { get; set; } = SyncDirection.TwoWay;
        public ConflictPolicy Conflict { get; set; } = ConflictPolicy.Stop;
        public bool Automatic { get; set; }
        public string Kind { get; set; } = StoreKinds.Dataset;
        public string TreeName { get; set; } = StoreTreeNames.Data;
    }

    public sealed class SyncStepResult
    {
        public SyncDecision Decision { get; internal set; }
        public SyncMarker Marker { get; internal set; }

        /// <summary>Dado efetivo a emitir no Grasshopper (local, ou o do store quando ele é a referência).</summary>
        public Buraqueira_Tools.Data.GlauxTreeTable Output { get; internal set; }
        public string LocalHash { get; internal set; }
        public long StoreRevision { get; internal set; }
        public bool Wrote { get; internal set; }
        public bool Pulled { get; internal set; }
    }

    /// <summary>
    /// Executa um passo de sincronização contra o store (decisão + Push/Pull + novo marcador).
    /// Sem dependência do Grasshopper: o componente Pill DB Sync e os testes usam a mesma lógica.
    /// </summary>
    public static class SyncEngine
    {
        public static SyncStepResult Step(
            GlauxFileStore store,
            string key,
            Buraqueira_Tools.Data.GlauxTreeTable local,
            SyncMarker marker,
            SyncOptions options,
            bool trigger,
            Guid origin = default(Guid))
        {
            options = options ?? new SyncOptions();
            string localHash = local != null ? Buraqueira_Tools.Data.TreeHash.Compute(local) : "";
            var latest = store.Exists ? store.GetLatest(options.Kind, key) : null;
            var latestTree = latest?.FindTree(options.TreeName);
            var head = new StoreHead
            {
                Exists = latestTree != null,
                Revision = latest?.Revision ?? 0,
                Hash = latestTree?.Hash ?? ""
            };

            var decision = SyncStateMachine.Decide(local != null, localHash, head, marker, options.Direction, options.Conflict, options.Automatic, trigger);
            var result = new SyncStepResult
            {
                Decision = decision,
                Marker = decision.MarkerUpdate ?? marker,
                LocalHash = localHash,
                StoreRevision = head.Revision
            };

            switch (decision.Action)
            {
                case SyncAction.Push:
                    {
                        var draft = new StoreEntryDraft(options.Kind, key).AddTree(options.TreeName, local);
                        var written = store.Append(draft, skipIfUnchanged: true, origin: origin);
                        result.Wrote = written.Written;
                        result.StoreRevision = written.Header.Revision;
                        result.Marker = SyncStateMachine.AfterPush(localHash, written.Header.Revision);
                        result.Output = local;
                        break;
                    }
                case SyncAction.Pull:
                    result.Output = store.LoadTree(latest, options.TreeName);
                    result.Pulled = true;
                    result.Marker = SyncStateMachine.AfterPull(localHash, head);
                    break;
                default:
                    result.Output = EffectiveOutput(store, key, local, localHash, result.Marker, options);
                    break;
            }
            return result;
        }

        /// <summary>
        /// Sem ação: emite o dado do store quando a última sincronização foi um Pull que ainda vale
        /// (o dado local não mudou desde então); caso contrário, o dado local.
        /// </summary>
        private static Buraqueira_Tools.Data.GlauxTreeTable EffectiveOutput(
            GlauxFileStore store, string key, Buraqueira_Tools.Data.GlauxTreeTable local, string localHash, SyncMarker marker, SyncOptions options)
        {
            bool storeIsReference = local == null ||
                (string.Equals(marker.LocalHash, localHash, StringComparison.Ordinal) &&
                 !string.Equals(marker.StoreHash, localHash, StringComparison.Ordinal) &&
                 marker.StoreRevision > 0);
            if (!storeIsReference) return local;

            var header = marker.StoreRevision > 0
                ? store.GetRevision(options.Kind, key, marker.StoreRevision)
                : store.GetLatest(options.Kind, key);
            if (header?.FindTree(options.TreeName) == null) return local;
            return store.LoadTree(header, options.TreeName);
        }
    }
}
