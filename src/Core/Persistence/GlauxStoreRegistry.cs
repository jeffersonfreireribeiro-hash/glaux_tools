using System;
using System.Collections.Concurrent;
using System.IO;

namespace Buraqueira_Tools.Persistence
{
    /// <summary>
    /// "Pool" de conexões dos stores: uma instância de <see cref="GlauxFileStore"/> por arquivo, compartilhada por
    /// todos os componentes e documentos. O índice fica em memória uma vez só; nenhum handle de arquivo fica aberto.
    /// </summary>
    public static class GlauxStoreRegistry
    {
        private static readonly ConcurrentDictionary<string, GlauxFileStore> s_stores =
            new ConcurrentDictionary<string, GlauxFileStore>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Disparado quando qualquer store registrado é alterado por este processo.</summary>
        public static event EventHandler<StoreChangedEventArgs> AnyStoreChanged;

        public static GlauxFileStore Get(string path)
        {
            string full = Path.GetFullPath(path);
            return s_stores.GetOrAdd(full, p =>
            {
                var store = new GlauxFileStore(p);
                store.Changed += (s, e) => AnyStoreChanged?.Invoke(s, e);
                return store;
            });
        }

        public static int OpenCount => s_stores.Count;

        /// <summary>Esquece as instâncias (os arquivos não são alterados). Usado em testes.</summary>
        public static void Clear() => s_stores.Clear();
    }
}
