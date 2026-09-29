using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Buraqueira_Tools.Persistence;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>Referência a um store (.glauxdb) trafegando entre componentes.</summary>
    public sealed class GlauxStoreRef
    {
        public GlauxStoreRef(string path, bool readOnly)
        {
            Path = path ?? "";
            ReadOnly = readOnly;
        }

        public string Path { get; }
        public bool ReadOnly { get; }

        public override string ToString() => $"GlauxStore [{System.IO.Path.GetFileName(Path)}]{(ReadOnly ? " (somente leitura)" : "")}";
    }

    public class GH_GlauxStoreGoo : GH_Goo<GlauxStoreRef>
    {
        public GH_GlauxStoreGoo()
        {
            Value = new GlauxStoreRef("", false);
        }

        public GH_GlauxStoreGoo(GlauxStoreRef value)
        {
            Value = value ?? new GlauxStoreRef("", false);
        }

        public override bool IsValid => Value != null && !string.IsNullOrWhiteSpace(Value.Path);
        public override string TypeName => "GlauxStore";
        public override string TypeDescription => "Conexão com um store local de dados do Glaux Tools (.glauxdb)";
        public override IGH_Goo Duplicate() => new GH_GlauxStoreGoo(Value);
        public override string ToString() => Value?.ToString() ?? "GlauxStore (vazio)";

        public override bool CastFrom(object source)
        {
            switch (source)
            {
                case GlauxStoreRef r:
                    Value = r;
                    return true;
                case GH_GlauxStoreGoo g:
                    Value = g.Value;
                    return true;
                case string s:
                    Value = new GlauxStoreRef(s, false);
                    return true;
                case GH_String gs:
                    Value = new GlauxStoreRef(gs.Value, false);
                    return true;
            }
            return false;
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(GlauxStoreRef)))
            {
                target = (Q)(object)Value;
                return true;
            }
            if (typeof(Q) == typeof(GH_String))
            {
                target = (Q)(object)new GH_String(Value?.Path);
                return true;
            }
            return false;
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetString("Path", Value?.Path ?? "");
            writer.SetBoolean("ReadOnly", Value?.ReadOnly ?? false);
            return true;
        }

        public override bool Read(GH_IReader reader)
        {
            string path = reader.ItemExists("Path") ? reader.GetString("Path") : "";
            bool ro = reader.ItemExists("ReadOnly") && reader.GetBoolean("ReadOnly");
            Value = new GlauxStoreRef(path, ro);
            return true;
        }
    }

    /// <summary>Resolve a entrada "Store" (conexão, caminho em texto ou vazio = store padrão do projeto).</summary>
    internal static class StoreInput
    {
        public const string DefaultFileName = "glaux_project" + GlauxFileStore.DefaultExtension;

        public const string InputDescription =
            "Conexão do Pill DB Connect, ou caminho de um arquivo .glauxdb. Vazio = store padrão do projeto (pasta PillVault do .gh).";

        public static bool TryResolve(IGH_DataAccess DA, int index, GH_Document doc, out GlauxFileStore store, out bool readOnly, out string error)
        {
            object raw = null;
            DA.GetData(index, ref raw);
            return TryResolve(raw, doc, out store, out readOnly, out error);
        }

        public static bool TryResolve(object raw, GH_Document doc, out GlauxFileStore store, out bool readOnly, out string error)
        {
            store = null;
            readOnly = false;
            error = null;
            string path = "";

            switch (raw)
            {
                case GH_GlauxStoreGoo goo when goo.Value != null:
                    path = goo.Value.Path;
                    readOnly = goo.Value.ReadOnly;
                    break;
                case GlauxStoreRef r:
                    path = r.Path;
                    readOnly = r.ReadOnly;
                    break;
                case GH_String s:
                    path = s.Value;
                    break;
                case string s:
                    path = s;
                    break;
                case GH_ObjectWrapper w when w.Value is GlauxStoreRef wr:
                    path = wr.Path;
                    readOnly = wr.ReadOnly;
                    break;
                case null:
                    break;
                default:
                    error = $"Entrada Store inválida ({raw.GetType().Name}). Use o Pill DB Connect ou um caminho .glauxdb.";
                    return false;
            }

            try
            {
                string full = GlauxPaths.Resolve(doc, path, DefaultFileName, GlauxFileStore.DefaultExtension);
                store = GlauxStoreRegistry.Get(full);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Caminho de store inválido: {ex.Message}";
                return false;
            }
        }
    }

    /// <summary>
    /// Mantém atualizados os componentes que leem um store: quando outro componente grava nele, os leitores
    /// são expirados na próxima solução. Quem gravou não é avisado (Origin), e um limitador suspende
    /// temporariamente a atualização automática de um componente que entre em laço (lê → grava → lê...).
    /// </summary>
    internal static class StoreWatch
    {
        private const int LoopWindowMs = 5000;
        private const int LoopMaxNotifications = 25;
        private const int LoopPauseMs = 10000;

        private sealed class Watcher
        {
            public string Path;
            public string Key;
            public WeakReference<GH_Component> Component;
            public readonly Queue<DateTime> Recent = new Queue<DateTime>();
            public DateTime PausedUntil;
        }

        private static readonly ConcurrentDictionary<Guid, Watcher> s_watchers = new ConcurrentDictionary<Guid, Watcher>();

        static StoreWatch()
        {
            GlauxStoreRegistry.AnyStoreChanged += OnStoreChanged;
        }

        /// <param name="key">Só avisa quando esta chave recebe revisão nova (nulo = qualquer gravação no arquivo).</param>
        public static void Watch(GH_Component component, GlauxFileStore store, string key = null)
        {
            if (component == null || store == null) return;
            s_watchers.AddOrUpdate(component.InstanceGuid,
                _ => new Watcher { Path = store.Path, Key = key, Component = new WeakReference<GH_Component>(component) },
                (_, w) =>
                {
                    w.Path = store.Path;
                    w.Key = key;
                    return w;
                });
        }

        public static void Unwatch(GH_Component component)
        {
            if (component != null) s_watchers.TryRemove(component.InstanceGuid, out _);
        }

        /// <summary>True se a atualização automática deste componente foi suspensa por suspeita de laço.</summary>
        public static bool IsPaused(GH_Component component)
        {
            return component != null && s_watchers.TryGetValue(component.InstanceGuid, out var w) && w.PausedUntil > DateTime.UtcNow;
        }

        private static bool TouchesKey(StoreChangedEventArgs e, string key)
        {
            foreach (var h in e.Added)
            {
                if (string.Equals(h.Key, key, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static void OnStoreChanged(object sender, StoreChangedEventArgs e)
        {
            var now = DateTime.UtcNow;
            var byDoc = new Dictionary<GH_Document, List<GH_Component>>();
            foreach (var kv in s_watchers)
            {
                var w = kv.Value;
                if (!w.Component.TryGetTarget(out var comp))
                {
                    s_watchers.TryRemove(kv.Key, out _);
                    continue;
                }
                if (kv.Key == e.Origin) continue;
                if (!string.Equals(w.Path, e.Path, StringComparison.OrdinalIgnoreCase)) continue;
                if (w.Key != null && !e.Compacted && !TouchesKey(e, w.Key)) continue;
                if (w.PausedUntil > now) continue;

                lock (w.Recent)
                {
                    w.Recent.Enqueue(now);
                    while (w.Recent.Count > 0 && (now - w.Recent.Peek()).TotalMilliseconds > LoopWindowMs) w.Recent.Dequeue();
                    if (w.Recent.Count > LoopMaxNotifications)
                    {
                        w.PausedUntil = now.AddMilliseconds(LoopPauseMs);
                        w.Recent.Clear();
                    }
                }

                var doc = comp.OnPingDocument();
                if (doc == null) continue;
                if (!byDoc.TryGetValue(doc, out var list)) byDoc[doc] = list = new List<GH_Component>();
                list.Add(comp);
            }

            foreach (var kv in byDoc)
            {
                var doc = kv.Key;
                // Documento fechado: o timer de agendamento dele já foi descartado
                if (doc.Context == GH_DocumentContext.Close) continue;
                var targets = kv.Value;
                try
                {
                    // Chamado durante uma solução: o Grasshopper inicia o agendamento quando ela terminar
                    doc.ScheduleSolution(20, d =>
                    {
                        foreach (var c in targets) c.ExpireSolution(false);
                    });
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
    }
}
