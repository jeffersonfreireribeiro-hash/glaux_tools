using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Metadados e dados armazenados para um canal sem fios (Pill Channel).
    /// </summary>
    public class PillChannel
    {
        public string RawKey { get; set; }
        public string CleanKey { get; set; }
        public string Category { get; set; }
        public string Unit { get; set; }
        public Color CategoryColor { get; set; }
        public GH_Structure<IGH_Goo> Data { get; set; }
        public DateTime LastUpdated { get; set; }
        public Guid SourceComponentGuid { get; set; }
        public Guid DocumentGuid { get; set; }
        public string DataTypeName { get; set; }
        public int TotalItemCount { get; set; }
        public int BranchCount { get; set; }
        public string SourceNickName { get; set; } = "";

        public PillChannel()
        {
            LastUpdated = DateTime.Now;
            Data = new GH_Structure<IGH_Goo>();
        }
    }

    /// <summary>
    /// Pacote estruturado contendo múltiplos parâmetros nomeados (Global Parameter Hub / Bundle).
    /// Suporta serialização completa em JSON para intercâmbio entre arquivos ou presets.
    /// </summary>
    public class PillBundle
    {
        public string Namespace { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Entries { get; set; }
        public Dictionary<string, string> Units { get; set; }

        public PillBundle()
        {
            Namespace = "GLOBAL";
            Timestamp = DateTime.Now;
            Entries = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Units = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            int count = Entries != null ? Entries.Count : 0;
            string ns = string.IsNullOrWhiteSpace(Namespace) ? "GLOBAL" : Namespace;
            return $"PillBundle [{ns}] ({count} parâmetro{(count == 1 ? "" : "s")})";
        }

        public string ToJson()
        {
            var dict = new Dictionary<string, object>
            {
                { "namespace", Namespace },
                { "timestamp", Timestamp.ToString("o") },
                { "units", Units },
                { "entries", Entries }
            };
            return PillJson.Serialize(dict, true);
        }

        public static PillBundle FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new PillBundle();
            var dict = PillJson.DeserializeObject(json);
            var bundle = new PillBundle();

            if (dict != null)
            {
                if (dict.ContainsKey("namespace") && dict["namespace"] != null)
                    bundle.Namespace = dict["namespace"].ToString();
                if (dict.ContainsKey("timestamp") && dict["timestamp"] != null)
                {
                    if (DateTime.TryParse(dict["timestamp"].ToString(), out DateTime dt))
                        bundle.Timestamp = dt;
                }
                if (dict.ContainsKey("units") && dict["units"] is Dictionary<string, object> udict)
                {
                    foreach (var kvp in udict)
                        bundle.Units[kvp.Key] = kvp.Value != null ? kvp.Value.ToString() : "";
                }
                if (dict.ContainsKey("entries") && dict["entries"] is Dictionary<string, object> edict)
                {
                    foreach (var kvp in edict)
                        bundle.Entries[kvp.Key] = kvp.Value;
                }
            }
            return bundle;
        }

        public PillBundle Clone()
        {
            var clone = new PillBundle
            {
                Namespace = this.Namespace,
                Timestamp = this.Timestamp,
                Units = new Dictionary<string, string>(this.Units, StringComparer.OrdinalIgnoreCase),
                Entries = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            };
            foreach (var kvp in this.Entries)
            {
                if (kvp.Value is System.Collections.IList list && !(kvp.Value is string))
                {
                    var copyList = new List<object>();
                    foreach (var item in list) copyList.Add(item);
                    clone.Entries[kvp.Key] = copyList;
                }
                else
                {
                    clone.Entries[kvp.Key] = kvp.Value;
                }
            }
            return clone;
        }

        public bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            try
            {
                writer.SetString("Namespace", Namespace ?? "GLOBAL");
                writer.SetDate("Timestamp", Timestamp);

                // 1. JSON Universal de contingência (permite leitura externa e portabilidade total)
                string json = ToJson();
                if (!string.IsNullOrEmpty(json))
                {
                    writer.SetString("BundleJson", json);
                }

                // 2. Chunks nativos Grasshopper com alta fidelidade (tipos numéricos, listas, geometrias)
                int count = Entries != null ? Entries.Count : 0;
                writer.SetInt32("EntryCount", count);

                if (count > 0)
                {
                    int idx = 0;
                    foreach (var kvp in Entries)
                    {
                        var chunk = writer.CreateChunk("Entry", idx);
                        chunk.SetString("Key", kvp.Key);
                        if (Units != null && Units.TryGetValue(kvp.Key, out string u) && !string.IsNullOrEmpty(u))
                        {
                            chunk.SetString("Unit", u);
                        }
                        WriteEntryValue(chunk, kvp.Value);
                        idx++;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            try
            {
                if (reader.ItemExists("Namespace"))
                    Namespace = reader.GetString("Namespace");
                if (reader.ItemExists("Timestamp"))
                    Timestamp = reader.GetDate("Timestamp");

                int entryCount = 0;
                if (reader.ItemExists("EntryCount"))
                    entryCount = reader.GetInt32("EntryCount");

                if (entryCount > 0)
                {
                    Entries.Clear();
                    Units.Clear();
                    for (int i = 0; i < entryCount; i++)
                    {
                        var chunk = reader.FindChunk("Entry", i);
                        if (chunk == null) continue;

                        string key = chunk.ItemExists("Key") ? chunk.GetString("Key") : $"Param_{i}";
                        if (chunk.ItemExists("Unit"))
                        {
                            Units[key] = chunk.GetString("Unit");
                        }
                        object val = ReadEntryValue(chunk);
                        Entries[key] = val;
                    }
                    return true;
                }

                // Fallback: se não houver chunks nativos, restaura a partir de BundleJson
                if (reader.ItemExists("BundleJson"))
                {
                    string json = reader.GetString("BundleJson");
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var fallback = FromJson(json);
                        if (fallback != null)
                        {
                            if (string.IsNullOrWhiteSpace(Namespace) || Namespace.Equals("GLOBAL", StringComparison.OrdinalIgnoreCase))
                                Namespace = fallback.Namespace;
                            Timestamp = fallback.Timestamp;
                            foreach (var kvp in fallback.Units)
                                Units[kvp.Key] = kvp.Value;
                            foreach (var kvp in fallback.Entries)
                                Entries[kvp.Key] = kvp.Value;
                            return true;
                        }
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void WriteEntryValue(GH_IO.Serialization.GH_IWriter chunk, object val)
        {
            if (val == null)
            {
                chunk.SetBoolean("IsNull", true);
                return;
            }

            if (val is double d)
            {
                chunk.SetString("Kind", "double");
                chunk.SetDouble("NumVal", d);
                return;
            }
            if (val is float f)
            {
                chunk.SetString("Kind", "double");
                chunk.SetDouble("NumVal", f);
                return;
            }
            if (val is int i)
            {
                chunk.SetString("Kind", "int");
                chunk.SetInt64("IntVal", i);
                return;
            }
            if (val is long l)
            {
                chunk.SetString("Kind", "int");
                chunk.SetInt64("IntVal", l);
                return;
            }
            if (val is short s)
            {
                chunk.SetString("Kind", "int");
                chunk.SetInt64("IntVal", s);
                return;
            }
            if (val is byte by)
            {
                chunk.SetString("Kind", "int");
                chunk.SetInt64("IntVal", by);
                return;
            }
            if (val is bool b)
            {
                chunk.SetString("Kind", "bool");
                chunk.SetBoolean("BoolVal", b);
                return;
            }
            if (val is string str)
            {
                chunk.SetString("Kind", "string");
                chunk.SetString("StrVal", str);
                return;
            }
            if (val is DateTime dt)
            {
                chunk.SetString("Kind", "date");
                chunk.SetDate("DateVal", dt);
                return;
            }
            if (val is Guid g)
            {
                chunk.SetString("Kind", "guid");
                chunk.SetGuid("GuidVal", g);
                return;
            }
            if (val is System.Collections.IList list && !(val is string))
            {
                chunk.SetString("Kind", "list");
                chunk.SetInt32("Count", list.Count);
                for (int j = 0; j < list.Count; j++)
                {
                    var itemChunk = chunk.CreateChunk("ListItem", j);
                    WriteEntryValue(itemChunk, list[j]);
                }
                return;
            }

            // Tenta serializar como IGH_Goo (geometrias Rhino, Goo, etc)
            IGH_Goo goo = val as IGH_Goo ?? GH_Convert.ToGoo(val);
            if (goo != null)
            {
                try
                {
                    var gooChunk = chunk.CreateChunk("GooData");
                    if (goo.Write(gooChunk))
                    {
                        chunk.SetString("Kind", "goo");
                        chunk.SetString("GooType", goo.GetType().AssemblyQualifiedName);
                        return;
                    }
                }
                catch { }
            }

            // Fallback para string
            chunk.SetString("Kind", "string");
            chunk.SetString("StrVal", val.ToString());
        }

        private static object ReadEntryValue(GH_IO.Serialization.GH_IReader chunk)
        {
            if (chunk == null || (chunk.ItemExists("IsNull") && chunk.GetBoolean("IsNull")))
                return null;

            string kind = chunk.ItemExists("Kind") ? chunk.GetString("Kind") : "";

            switch (kind)
            {
                case "double":
                    return chunk.GetDouble("NumVal");
                case "int":
                    long l = chunk.GetInt64("IntVal");
                    if (l >= int.MinValue && l <= int.MaxValue) return (int)l;
                    return l;
                case "bool":
                    return chunk.GetBoolean("BoolVal");
                case "string":
                    return chunk.GetString("StrVal");
                case "date":
                    return chunk.GetDate("DateVal");
                case "guid":
                    return chunk.GetGuid("GuidVal");
                case "list":
                    int count = chunk.ItemExists("Count") ? chunk.GetInt32("Count") : 0;
                    var list = new List<object>(count);
                    for (int j = 0; j < count; j++)
                    {
                        var itemChunk = chunk.FindChunk("ListItem", j);
                        list.Add(ReadEntryValue(itemChunk));
                    }
                    return list;
                case "goo":
                    try
                    {
                        string typeStr = chunk.GetString("GooType");
                        var gooChunk = chunk.FindChunk("GooData");
                        if (!string.IsNullOrEmpty(typeStr) && gooChunk != null)
                        {
                            Type t = Type.GetType(typeStr);
                            if (t != null && typeof(IGH_Goo).IsAssignableFrom(t))
                            {
                                var goo = (IGH_Goo)Activator.CreateInstance(t);
                                goo.Read(gooChunk);
                                return goo.SafeScriptVariable() ?? goo;
                            }
                        }
                    }
                    catch { }
                    break;
            }

            if (chunk.ItemExists("NumVal")) return chunk.GetDouble("NumVal");
            if (chunk.ItemExists("IntVal")) return chunk.GetInt64("IntVal");
            if (chunk.ItemExists("BoolVal")) return chunk.GetBoolean("BoolVal");
            if (chunk.ItemExists("StrVal")) return chunk.GetString("StrVal");

            return null;
        }
    }

    /// <summary>
    /// Wrapper de primeira classe Grasshopper para o PillBundle, garantindo tipagem limpa 'PillBundle',
    /// formatação elegante de tooltips e conversão transparente para JSON ou tipos primitivos.
    /// </summary>
    public class GH_PillBundleGoo : GH_Goo<PillBundle>
    {
        public GH_PillBundleGoo()
        {
            Value = new PillBundle();
        }

        public GH_PillBundleGoo(PillBundle bundle)
        {
            Value = bundle ?? new PillBundle();
        }

        public override bool IsValid => Value != null;
        public override string TypeName => "PillBundle";
        public override string TypeDescription => "Pacote estruturado de parâmetros nomeados (Buraqueira Tools)";

        public override IGH_Goo Duplicate()
        {
            return new GH_PillBundleGoo(Value?.Clone());
        }

        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            if (Value == null)
            {
                writer.SetBoolean("IsNull", true);
                return true;
            }
            writer.SetBoolean("IsNull", false);
            return Value.Write(writer);
        }

        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            bool isNull = false;
            if (reader.ItemExists("IsNull"))
                isNull = reader.GetBoolean("IsNull");

            if (isNull)
            {
                Value = null;
                return true;
            }

            Value = new PillBundle();
            return Value.Read(reader);
        }

        public override string ToString()
        {
            if (Value == null) return "PillBundle (Vazio)";
            return Value.ToString();
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(PillBundle)))
            {
                target = (Q)(object)Value;
                return true;
            }
            if (typeof(Q) == typeof(string))
            {
                target = (Q)(object)Value?.ToJson();
                return true;
            }
            if (typeof(Q) == typeof(GH_String))
            {
                target = (Q)(object)new GH_String(Value?.ToJson());
                return true;
            }
            return base.CastTo(ref target);
        }

        public override bool CastFrom(object source)
        {
            if (source is PillBundle b)
            {
                Value = b;
                return true;
            }
            if (source is GH_PillBundleGoo goo)
            {
                Value = goo.Value;
                return true;
            }
            if (source is GH_ObjectWrapper wrapper && wrapper.Value is PillBundle wb)
            {
                Value = wb;
                return true;
            }
            if (source is string s)
            {
                Value = PillBundle.FromJson(s);
                return true;
            }
            if (source is GH_String ghStr)
            {
                Value = PillBundle.FromJson(ghStr.Value);
                return true;
            }
            return base.CastFrom(source);
        }
    }

    /// <summary>
    /// Barramento central de memória em alta velocidade para transmissão sem fios (Wireless Hub).
    /// Gerencia canais, categorização cromática automática, imutabilidade e limpeza ao fechar o documento.
    /// </summary>
    public static class PillHub
    {
        private static readonly ConcurrentDictionary<string, PillChannel> _channels = 
            new ConcurrentDictionary<string, PillChannel>(StringComparer.OrdinalIgnoreCase);

        // Registro de receptores ativos por canal para atualização em cascata e navegação (Jump)
        private static readonly ConcurrentDictionary<string, List<Guid>> _receivers = 
            new ConcurrentDictionary<string, List<Guid>>(StringComparer.OrdinalIgnoreCase);

        // Registro de transmissores ativos por canal para lookup instantâneo O(1) sem varrer doc.Objects
        private static readonly ConcurrentDictionary<string, Guid> _transmitters = 
            new ConcurrentDictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        // Registro de canais individuais por Guid de componente + Chave (garante que múltiplos transmissores e sliders nunca se sobrescrevam)
        private static readonly ConcurrentDictionary<string, PillChannel> _channelsByComponent = 
            new ConcurrentDictionary<string, PillChannel>(StringComparer.OrdinalIgnoreCase);

        // Fila agregada para agendamento de solução sem conflitos ou duplicações
        private static readonly HashSet<Guid> _pendingSolutionTargets = new HashSet<Guid>();
        private static readonly object _scheduleLock = new object();
        private static bool _solutionScheduled = false;

        private static bool _docListenerRegistered = false;

        static PillHub()
        {
            try
            {
                EnsureDocumentListener();
            }
            catch { }
        }

        public static void EnsureDocumentListener()
        {
            if (_docListenerRegistered) return;
            try
            {
                if (Instances.DocumentServer != null)
                {
                    Instances.DocumentServer.DocumentRemoved -= OnDocumentRemoved;
                    Instances.DocumentServer.DocumentRemoved += OnDocumentRemoved;
                    Instances.DocumentServer.DocumentAdded -= OnDocumentAdded;
                    Instances.DocumentServer.DocumentAdded += OnDocumentAdded;

                    foreach (var d in Instances.DocumentServer)
                    {
                        if (d is GH_Document ghDoc)
                        {
                            ghDoc.SolutionEnd -= OnDocumentSolutionEnd;
                            ghDoc.SolutionEnd += OnDocumentSolutionEnd;
                        }
                    }
                }
                _docListenerRegistered = true;
            }
            catch
            {
                // Modo headless ou inicialização antes do DocumentServer
            }
        }

        private static void OnDocumentAdded(GH_DocumentServer server, GH_Document doc)
        {
            if (doc == null) return;
            doc.SolutionEnd -= OnDocumentSolutionEnd;
            doc.SolutionEnd += OnDocumentSolutionEnd;
            FlushPendingTargets(doc);
        }

        private static void OnDocumentRemoved(GH_DocumentServer server, GH_Document doc)
        {
            if (doc == null) return;
            doc.SolutionEnd -= OnDocumentSolutionEnd;
            PurgeDocumentChannels(doc.DocumentID);
        }

        private static void OnDocumentSolutionEnd(object sender, GH_SolutionEventArgs e)
        {
            if (sender is GH_Document doc)
            {
                FlushPendingTargets(doc);
            }
        }

        /// <summary>
        /// Força a execução de quaisquer soluções pendentes assim que um documento estiver pronto.
        /// </summary>
        public static void FlushPendingTargets(GH_Document doc)
        {
            if (doc == null) return;
            lock (_scheduleLock)
            {
                if (_pendingSolutionTargets.Count == 0 || _solutionScheduled) return;
            }
            SafeScheduleExpiration(doc);
        }

        private static void SafeScheduleExpiration(GH_Document targetDoc)
        {
            if (targetDoc == null) return;

            // Se o documento já estiver calculando uma solução (ex: Wallacei rodando ou Ray Tracing ativo),
            // NÃO agendamos novas soluções nem expiramos objetos. O PillHub já atualizou os dados na memória;
            // qualquer receptor que calcular obterá o dado mais recente diretamente do canal.
            if (targetDoc.SolutionState == GH_ProcessStep.Process)
            {
                return;
            }

            lock (_scheduleLock)
            {
                if (_pendingSolutionTargets.Count == 0 || _solutionScheduled) return;
                _solutionScheduled = true;
            }

            targetDoc.ScheduleSolution(5, doc => ExecuteSafeExpiration(doc));
        }

        private static void ExecuteSafeExpiration(GH_Document doc)
        {
            if (doc == null)
            {
                lock (_scheduleLock) { _solutionScheduled = false; }
                return;
            }

            // Se o documento estiver calculando, aborta imediatamente sem chamar ExpireSolution
            if (doc.SolutionState == GH_ProcessStep.Process)
            {
                lock (_scheduleLock) { _solutionScheduled = false; }
                return;
            }

            List<Guid> targets;
            lock (_scheduleLock)
            {
                targets = _pendingSolutionTargets.ToList();
                _pendingSolutionTargets.Clear();
                _solutionScheduled = false;
            }

            try
            {
                foreach (var id in targets)
                {
                    IGH_DocumentObject obj = doc.FindObject(id, false);
                    if (obj == null && Instances.DocumentServer != null)
                    {
                        foreach (var sDoc in Instances.DocumentServer)
                        {
                            if (sDoc is GH_Document otherDoc)
                            {
                                obj = otherDoc.FindObject(id, false);
                                if (obj != null) break;
                            }
                        }
                    }

                    if (obj is IGH_ActiveObject activeObj)
                    {
                        var objDoc = (obj as IGH_DocumentObject)?.OnPingDocument();
                        if (objDoc == null || objDoc.SolutionState != GH_ProcessStep.Process)
                        {
                            activeObj.ExpireSolution(false);
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Conecta todos os Pill Receivers e Pill Hooks do documento aos respectivos Transmitters / SliderPools
        /// utilizando cabos físicos reais com WireDisplay configurado para 'Hidden'.
        /// Garante sincronização DAG sequencial perfeita para otimizadores como Wallacei e Galapagos.
        /// </summary>
        public static int ConnectAllDocumentPillsHidden(GH_Document doc)
        {
            if (doc == null) return 0;
            int count = 0;
            foreach (var obj in doc.Objects)
            {
                if (obj is PillReceiver_Component rx)
                {
                    if (rx.AutoConnectHiddenWire(doc)) count++;
                }
                else if (obj is PillHook_Component hook)
                {
                    if (hook.AutoConnectHiddenWire(doc)) count++;
                }
                else if (obj is PillBundlePack_Component pack)
                {
                    count += pack.AutoConnectHiddenWires(doc);
                }
            }
            return count;
        }

        /// <summary>
        /// Desconecta todos os cabos físicos ocultos dos Pill Receivers, Hooks e Bundles, voltando ao modo sem fio em memória.
        /// </summary>
        public static int DisconnectAllDocumentPillsHidden(GH_Document doc)
        {
            if (doc == null) return 0;
            int count = 0;
            foreach (var obj in doc.Objects)
            {
                if (obj is PillReceiver_Component rx)
                {
                    rx.DisconnectHiddenWire();
                    count++;
                }
                else if (obj is PillHook_Component hook)
                {
                    hook.DisconnectHiddenWire();
                    count++;
                }
                else if (obj is PillBundlePack_Component pack)
                {
                    pack.DisconnectHiddenWires();
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Remove canais associados a um documento fechado para garantir que a memória não vaze.
        /// </summary>
        public static void PurgeDocumentChannels(Guid docId)
        {
            var keysToRemove = _channels.Where(kvp => kvp.Value.DocumentGuid == docId).Select(kvp => kvp.Key).ToList();
            foreach (var key in keysToRemove)
            {
                _channels.TryRemove(key, out _);
            }
            var compKeysToRemove = _channelsByComponent.Where(kvp => kvp.Value.DocumentGuid == docId).Select(kvp => kvp.Key).ToList();
            foreach (var k in compKeysToRemove)
            {
                _channelsByComponent.TryRemove(k, out _);
            }
        }

        /// <summary>
        /// Remove um canal publicado quando o transmissor for desconectado, renomeado ou excluído.
        /// </summary>
        public static void Unpublish(string rawOrCleanKey, Guid sourceComponentGuid)
        {
            if (string.IsNullOrWhiteSpace(rawOrCleanKey)) return;
            string cleanKey = CleanUpKey(rawOrCleanKey);

            if (sourceComponentGuid != Guid.Empty)
            {
                string compKey = $"{sourceComponentGuid}_{cleanKey}";
                _channelsByComponent.TryRemove(compKey, out _);
            }

            _transmitters.TryRemove(cleanKey, out _);

            if (_channels.TryGetValue(cleanKey, out var ch))
            {
                // Remove se o transmissor for o proprietário do canal
                if (ch.SourceComponentGuid == sourceComponentGuid)
                {
                    _channels.TryRemove(cleanKey, out _);
                    NotifyReceivers(cleanKey, ch.Category, sourceComponentGuid, ch.DocumentGuid);
                }
            }
        }

        /// <summary>
        /// Remove canais órfãos que não possuem mais nenhum transmissor ativo no documento.
        /// </summary>
        public static void PurgeOrphanChannels(GH_Document doc)
        {
            if (doc == null) return;
            var activeTxKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var activeGuids = new HashSet<Guid>();
            foreach (var obj in doc.Objects)
            {
                activeGuids.Add(obj.InstanceGuid);
                if (obj is PillTransmitter_Component tx && !string.IsNullOrWhiteSpace(tx.CurrentCleanKey))
                {
                    activeTxKeys.Add(tx.CurrentCleanKey);
                }
                else if (obj is PillCache_Component cache && !string.IsNullOrWhiteSpace(cache.CurrentCleanKey))
                {
                    activeTxKeys.Add(cache.CurrentCleanKey);
                }
                else if (obj is PillLayerPipeline_Component pipe && !string.IsNullOrWhiteSpace(pipe.CurrentCleanKey))
                {
                    activeTxKeys.Add(pipe.CurrentCleanKey);
                }
                else if (obj is PillSliderPool_Component pool)
                {
                    foreach (var s in pool.Sliders)
                    {
                        activeTxKeys.Add(s.CleanKey);
                        activeTxKeys.Add(PillHub.CleanUpKey(s.FullKey));
                    }
                }
            }

            var orphanKeys = _channels.Keys.Where(k => !activeTxKeys.Contains(k)).ToList();
            foreach (var k in orphanKeys)
            {
                _channels.TryRemove(k, out _);
                _transmitters.TryRemove(k, out _);
            }

            var orphanCompKeys = _channelsByComponent.Where(kvp => !activeGuids.Contains(kvp.Value.SourceComponentGuid)).Select(kvp => kvp.Key).ToList();
            foreach (var k in orphanCompKeys)
            {
                _channelsByComponent.TryRemove(k, out _);
            }
        }

        /// <summary>
        /// Limpeza forçada de todos os canais.
        /// </summary>
        public static void PurgeAll()
        {
            _channels.Clear();
            _channelsByComponent.Clear();
            _receivers.Clear();
            _transmitters.Clear();
        }

        /// <summary>
        /// Registra ou atualiza um canal com dados e metadados.
        /// Retorna o objeto do canal e notifica os receptores inscritos caso os dados tenham mudado.
        /// </summary>
        public static PillChannel Publish(
            string rawKey, 
            GH_Structure<IGH_Goo> sourceData, 
            Guid sourceComponentGuid, 
            Guid docGuid, 
            string explicitUnit = null,
            bool forceNotify = false,
            string sourceNickName = "")
        {
            if (string.IsNullOrWhiteSpace(rawKey)) return null;

            rawKey = rawKey.Trim();
            ParseKeyMetadata(rawKey, explicitUnit, out string cleanKey, out string category, out string unit, out Color color);

            // Registra transmissor imediatamente para lookup O(1) instantâneo
            _transmitters[cleanKey] = sourceComponentGuid;

            // Duplica estrutura de ramos em memória rasa ultra-rápida (sem duplicar geometria unmanaged pesada)
            var clonedData = CloneStructure(sourceData);

            // Verificação rápida de igualdade para evitar recalcular receptores quando os dados não mudaram
            bool dataChanged = true;
            if (!forceNotify && _channels.TryGetValue(cleanKey, out var existing) && existing != null)
            {
                bool unitEqual = string.Equals(existing.Unit ?? "", unit ?? "", StringComparison.Ordinal);
                bool catEqual = string.Equals(existing.Category ?? "", category ?? "", StringComparison.OrdinalIgnoreCase);
                if (unitEqual && catEqual && AreStructuresEqual(existing.Data, clonedData))
                {
                    dataChanged = false;
                }
            }

            string dataTypeName = "Empty";
            int totalCount = 0;
            int branchCount = 0;

            if (clonedData != null && clonedData.DataCount > 0)
            {
                totalCount = clonedData.DataCount;
                branchCount = clonedData.Branches.Count;
                var firstItem = clonedData.AllData(true).FirstOrDefault();
                if (firstItem != null)
                {
                    dataTypeName = firstItem.TypeName;
                }
            }

            var channel = new PillChannel
            {
                RawKey = rawKey,
                CleanKey = cleanKey,
                Category = category,
                Unit = unit,
                CategoryColor = color,
                Data = clonedData,
                LastUpdated = DateTime.Now,
                SourceComponentGuid = sourceComponentGuid,
                DocumentGuid = docGuid,
                DataTypeName = dataTypeName,
                TotalItemCount = totalCount,
                BranchCount = branchCount,
                SourceNickName = sourceNickName ?? ""
            };

            if (sourceComponentGuid != Guid.Empty)
            {
                string compKey = $"{sourceComponentGuid}_{cleanKey}";
                _channelsByComponent[compKey] = channel;
            }
            _channels[cleanKey] = channel;

            // Dispara atualização para os receptores registrados APENAS se os dados mudaram ou forçado
            if (dataChanged || forceNotify)
            {
                NotifyReceivers(cleanKey, category, sourceComponentGuid, docGuid);
            }

            return channel;
        }

        // ==========================================
        // PALETA CROMÁTICA DETERMINÍSTICA DOS JUMPS
        // ==========================================
        private static readonly Color[] s_jumpColors = new Color[]
        {
            Color.FromArgb(239, 68, 68),   // 0: Vermelho Coral (Red) - 1º receptor conectado (idêntico ao desenho!)
            Color.FromArgb(59, 130, 246),  // 1: Azul Elétrico (Blue) - 2º receptor conectado (idêntico ao desenho!)
            Color.FromArgb(245, 158, 11),  // 2: Amarelo Âmbar (Yellow) - 3º receptor conectado (idêntico ao desenho!)
            Color.FromArgb(16, 185, 129),  // 3: Verde Esmeralda (Green)
            Color.FromArgb(168, 85, 247),  // 4: Roxo Violeta (Purple)
            Color.FromArgb(6, 182, 212),   // 5: Ciano Neon (Cyan)
            Color.FromArgb(244, 63, 94),   // 6: Rosa / Rose (Pink)
            Color.FromArgb(249, 115, 22),  // 7: Laranja Vivo (Orange)
            Color.FromArgb(132, 204, 22),  // 8: Lima (Lime)
            Color.FromArgb(99, 102, 241),  // 9: Índigo (Indigo)
            Color.FromArgb(20, 184, 166),  // 10: Turquesa / Teal
            Color.FromArgb(217, 70, 239),  // 11: Fúcsia / Magenta
            Color.FromArgb(14, 165, 233),  // 12: Sky Blue
            Color.FromArgb(234, 179, 8),   // 13: Ouro / Gold
            Color.FromArgb(34, 197, 94),   // 14: Verde Claro
            Color.FromArgb(124, 58, 237)   // 15: Deep Purple
        };

        /// <summary>
        /// Obtém a cor determinística atribuída a um receptor com base na sua ordem de conexão.
        /// O primeiro receptor conectado é Vermelho (0), o segundo é Azul (1), o terceiro é Amarelo (2), etc.
        /// </summary>
        public static Color GetReceiverColor(int index)
        {
            if (index < 0) index = 0;
            return s_jumpColors[index % s_jumpColors.Length];
        }

        /// <summary>
        /// Inscreve um componente receptor a um canal para receber notificações de expiração de solução e navegação (Jump).
        /// Retorna o índice de ordem de conexão (0, 1, 2...) que define a cor da bolinha de Jump.
        /// </summary>
        public static int SubscribeReceiver(string cleanKey, Guid receiverGuid)
        {
            if (string.IsNullOrWhiteSpace(cleanKey)) return -1;
            string k = CleanUpKey(cleanKey);
            var list = _receivers.GetOrAdd(k, _ => new List<Guid>());
            lock (list)
            {
                int idx = list.IndexOf(receiverGuid);
                if (idx < 0)
                {
                    list.Add(receiverGuid);
                    idx = list.Count - 1;
                }
                return idx;
            }
        }

        /// <summary>
        /// Remove a inscrição de um receptor.
        /// </summary>
        public static void UnsubscribeReceiver(string cleanKey, Guid receiverGuid)
        {
            if (string.IsNullOrWhiteSpace(cleanKey)) return;
            string k = CleanUpKey(cleanKey);
            if (_receivers.TryGetValue(k, out var list))
            {
                lock (list)
                {
                    list.Remove(receiverGuid);
                }
            }
        }

        /// <summary>
        /// Retorna a lista com os Guids de todos os receptores inscritos no canal.
        /// </summary>
        public static List<Guid> GetReceiverGuids(string cleanKey)
        {
            if (string.IsNullOrWhiteSpace(cleanKey)) return new List<Guid>();
            string k = CleanUpKey(cleanKey);
            if (_receivers.TryGetValue(k, out var list))
            {
                lock (list)
                {
                    return new List<Guid>(list);
                }
            }
            return new List<Guid>();
        }

        /// <summary>
        /// Obtém o índice numérico (0, 1, 2...) de um receptor na chave, determinando sua cor.
        /// </summary>
        public static int GetReceiverIndex(string cleanKey, Guid receiverGuid)
        {
            if (string.IsNullOrWhiteSpace(cleanKey)) return -1;
            string k = CleanUpKey(cleanKey);
            if (_receivers.TryGetValue(k, out var list))
            {
                lock (list)
                {
                    return list.IndexOf(receiverGuid);
                }
            }
            return -1;
        }

        public static string GetColorName(int index)
        {
            switch (index % s_jumpColors.Length)
            {
                case 0: return "Vermelho Coral";
                case 1: return "Azul Elétrico";
                case 2: return "Amarelo Âmbar";
                case 3: return "Verde Esmeralda";
                case 4: return "Roxo Violeta";
                case 5: return "Ciano Neon";
                case 6: return "Rosa / Magenta";
                case 7: return "Laranja Vivo";
                case 8: return "Lima";
                case 9: return "Índigo";
                case 10: return "Turquesa";
                case 11: return "Fúcsia";
                case 12: return "Azul Celeste";
                case 13: return "Ouro";
                case 14: return "Verde Claro";
                case 15: return "Deep Purple";
                default: return $"Cor #{index + 1}";
            }
        }

        /// <summary>
        /// Retorna todos os componentes PillReceiver fisicamente presentes no documento que escutam a chave,
        /// utilizando busca direta O(1) via dicionário _receivers em vez de varrer doc.Objects.
        /// </summary>
        public static List<PillReceiver_Component> GetActiveReceivers(string cleanKey, GH_Document doc)
        {
            var result = new List<PillReceiver_Component>();
            if (doc == null || string.IsNullOrWhiteSpace(cleanKey)) return result;

            cleanKey = CleanUpKey(cleanKey);

            if (_receivers.TryGetValue(cleanKey, out var list))
            {
                List<Guid> snapshot;
                lock (list) { snapshot = new List<Guid>(list); }

                for (int i = 0; i < snapshot.Count; i++)
                {
                    var rx = doc.FindObject(snapshot[i], false) as PillReceiver_Component;
                    if (rx != null)
                    {
                        rx.AssignedColorIndex = i;
                        result.Add(rx);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Retorna a quantidade de receptores registrados para a chave em O(1), sem acessar objetos.
        /// </summary>
        public static int GetReceiverCount(string cleanKey)
        {
            if (string.IsNullOrWhiteSpace(cleanKey)) return 0;
            string k = CleanUpKey(cleanKey);
            if (_receivers.TryGetValue(k, out var list))
            {
                lock (list) { return list.Count; }
            }
            return 0;
        }

        /// <summary>
        /// Retorna o Guid do componente PillTransmitter transmissor da chave via lookup O(1) instantâneo.
        /// </summary>
        public static Guid GetTransmitterGuid(string cleanKey, GH_Document doc = null)
        {
            if (string.IsNullOrWhiteSpace(cleanKey)) return Guid.Empty;
            cleanKey = CleanUpKey(cleanKey);

            if (_transmitters.TryGetValue(cleanKey, out var txGuid) && txGuid != Guid.Empty)
            {
                return txGuid;
            }

            if (_channels.TryGetValue(cleanKey, out var ch) && ch != null && ch.SourceComponentGuid != Guid.Empty)
            {
                return ch.SourceComponentGuid;
            }

            return Guid.Empty;
        }

        /// <summary>
        /// Executa o salto (Jump) suave da tela do Grasshopper para o componente alvo,
        /// centralizando a visualização e selecionando-o no canvas.
        /// </summary>
        public static void JumpToComponent(IGH_DocumentObject targetObj)
        {
            if (targetObj == null) return;
            var canvas = Instances.ActiveCanvas;
            if (canvas == null) return;

            var doc = targetObj.OnPingDocument();
            if (doc != null)
            {
                doc.DeselectAll();
                targetObj.Attributes.Selected = true;
            }

            // Centraliza o viewport no componente alvo
            RectangleF bounds = targetObj.Attributes.Bounds;
            PointF center = new PointF(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
            canvas.Viewport.MidPoint = center;

            if (canvas.Viewport.Zoom < 0.75f)
            {
                canvas.Viewport.Zoom = 1.0f;
            }

            canvas.Refresh();

            try
            {
                Rhino.RhinoApp.WriteLine($"[Pill Jump] ➔ Navegado para: {targetObj.NickName}");
            }
            catch { }
        }

        /// <summary>
        /// Notifica todos os receptores e bundles inscritos no canal ou grupo para recalcular o Canvas.
        /// Suporta inscrições por chave exata, categoria/grupo, prefixo e wildcard ('*').
        /// </summary>
        private static void NotifyReceivers(string cleanKey, string category, Guid ignoreGuid, Guid docGuid)
        {
            var matchedGuids = new HashSet<Guid>();

            void Collect(string key)
            {
                if (string.IsNullOrWhiteSpace(key)) return;
                string k = CleanUpKey(key);
                if (_receivers.TryGetValue(k, out var list))
                {
                    lock (list)
                    {
                        foreach (var id in list)
                        {
                            if (id != ignoreGuid) matchedGuids.Add(id);
                        }
                    }
                }
            }

            // 1. Chave exata e limpa
            Collect(cleanKey);

            // 2. Categoria e variações de prefixo (ex: "ACU", "ACU_", "ACU::")
            if (!string.IsNullOrWhiteSpace(category))
            {
                Collect(category);
                Collect(category + "_");
                Collect(category + "::");
                string norm = NormalizeCategory(category);
                if (!string.IsNullOrEmpty(norm) && !norm.Equals(category, StringComparison.OrdinalIgnoreCase))
                {
                    Collect(norm);
                    Collect(norm + "_");
                    Collect(norm + "::");
                }
            }

            // 3. Prefixo extraído diretamente da cleanKey (ex: "U_GRID" -> prefixo "U")
            int sepIdx = cleanKey.IndexOfAny(new char[] { '_', ':', '/' });
            if (sepIdx > 0)
            {
                string pfx = cleanKey.Substring(0, sepIdx).Trim();
                Collect(pfx);
                Collect(pfx + "_");
                Collect(pfx + "::");
            }

            // 4. Wildcards globais
            Collect("*");
            Collect("ALL");
            Collect("TODOS");

            if (matchedGuids.Count == 0) return;

            lock (_scheduleLock)
            {
                foreach (var id in matchedGuids)
                {
                    _pendingSolutionTargets.Add(id);
                }

                // Tenta obter o documento ativo com múltiplos fallbacks seguros
                GH_Document targetDoc = null;
                if (docGuid != Guid.Empty && Instances.DocumentServer != null)
                {
                    try
                    {
                        foreach (var item in Instances.DocumentServer)
                        {
                            if (item is GH_Document d && d.DocumentID == docGuid)
                            {
                                targetDoc = d;
                                break;
                            }
                        }
                    }
                    catch { }
                }

                if (targetDoc == null)
                {
                    targetDoc = Instances.ActiveCanvas?.Document;
                }

                if (targetDoc == null && Instances.DocumentServer != null && Instances.DocumentServer.DocumentCount > 0)
                {
                    targetDoc = Instances.DocumentServer[0] as GH_Document;
                }

                if (targetDoc != null)
                {
                    SafeScheduleExpiration(targetDoc);
                }
            }
        }

        /// <summary>
        /// Tenta obter os dados de um canal. Retorna uma cópia para preservar imutabilidade.
        /// </summary>
        public static bool TryGetChannel(string keyOrCleanKey, out PillChannel channel)
        {
            channel = null;
            if (string.IsNullOrWhiteSpace(keyOrCleanKey)) return false;

            string cleanKey = CleanUpKey(keyOrCleanKey);
            if (!_channels.TryGetValue(cleanKey, out var found))
            {
                // Fallback 1: chave bruta direta
                _channels.TryGetValue(keyOrCleanKey.Trim(), out found);
            }

            if (found == null)
            {
                // Fallback 2: busca por sufixo (ex: buscou "Raio", mas canal está registrado como "GEO_Raio")
                found = _channels.Values.FirstOrDefault(c => 
                    c.CleanKey.Equals(cleanKey, StringComparison.OrdinalIgnoreCase) ||
                    c.CleanKey.EndsWith("_" + cleanKey, StringComparison.OrdinalIgnoreCase) ||
                    c.CleanKey.EndsWith("::" + cleanKey, StringComparison.OrdinalIgnoreCase) ||
                    cleanKey.EndsWith("_" + c.CleanKey, StringComparison.OrdinalIgnoreCase));
            }

            if (found != null)
            {
                // Retorna cópia rasa do canal com clone da estrutura para total imutabilidade
                channel = new PillChannel
                {
                    RawKey = found.RawKey,
                    CleanKey = found.CleanKey,
                    Category = found.Category,
                    Unit = found.Unit,
                    CategoryColor = found.CategoryColor,
                    Data = CloneStructure(found.Data),
                    LastUpdated = found.LastUpdated,
                    SourceComponentGuid = found.SourceComponentGuid,
                    DocumentGuid = found.DocumentGuid,
                    DataTypeName = found.DataTypeName,
                    TotalItemCount = found.TotalItemCount,
                    BranchCount = found.BranchCount,
                    SourceNickName = found.SourceNickName
                };
                return true;
            }
            return false;
        }

        /// <summary>
        /// Lista todas as chaves ativas no barramento.
        /// </summary>
        public static List<string> GetAllActiveKeys()
        {
            return _channels.Keys.OrderBy(k => k).ToList();
        }

        /// <summary>
        /// Retorna todos os canais ativos para auditoria e catálogo.
        /// </summary>
        public static List<PillChannel> GetAllChannels()
        {
            var pool = _channelsByComponent.Values.Count > 0 ? (IEnumerable<PillChannel>)_channelsByComponent.Values : _channels.Values;
            return pool.OrderBy(c => c.Category).ThenBy(c => c.CleanKey).ToList();
        }

        /// <summary>
        /// Busca canais que correspondam a uma chave específica ou a um grupo/categoria (ex: 'ACU', 'GEO', 'MAT', 'ALL', '*').
        /// Suporta busca por categoria normalizada, prefixos ('ACU_', 'ACU::'), NickNames e chave exata.
        /// </summary>
        public static List<PillChannel> GetChannelsByGroupOrKey(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<PillChannel>();
            string q = query.Trim();

            if (q == "*" || q.Equals("ALL", StringComparison.OrdinalIgnoreCase) || q.Equals("TODOS", StringComparison.OrdinalIgnoreCase))
            {
                return GetAllChannels();
            }

            string cleanQuery = CleanUpKey(q);

            // Se a query possuir separador de parâmetro ('_' ou '::') e não for apenas um prefixo aberto (não termina com separador),
            // verifica primeiro se é uma chave individual exata de canal no barramento.
            bool hasParamSeparator = cleanQuery.Contains("_") || cleanQuery.Contains("::") || cleanQuery.Contains(":");
            if (hasParamSeparator && !cleanQuery.EndsWith("_") && !cleanQuery.EndsWith("::") && !cleanQuery.EndsWith(":"))
            {
                if (TryGetChannel(cleanQuery, out var singleCh))
                {
                    return new List<PillChannel> { singleCh };
                }
            }

            // Remove separadores finais (ex: caso o usuário digite "ACU_" ou "GEO::")
            string prefix = cleanQuery.TrimEnd('_', ':', '/');
            string normCat = NormalizeCategory(prefix);

            var pool = _channelsByComponent.Values.Count > 0 ? (IEnumerable<PillChannel>)_channelsByComponent.Values : _channels.Values;

            var matches = pool.Where(ch =>
            {
                // Chave exata ou limpa
                if (ch.CleanKey.Equals(cleanQuery, StringComparison.OrdinalIgnoreCase)) return true;
                if (ch.RawKey.Equals(q, StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrWhiteSpace(ch.SourceNickName) && ch.SourceNickName.Equals(q, StringComparison.OrdinalIgnoreCase)) return true;

                // Categoria exata (ex: "ACU", "GEO", "MAT", "SRF")
                if (ch.Category.Equals(prefix, StringComparison.OrdinalIgnoreCase)) return true;

                // Categoria normalizada se a query for um nome de categoria (ex: "Acústica" -> "ACU")
                if (!string.IsNullOrEmpty(normCat) && !normCat.Equals("GEN", StringComparison.OrdinalIgnoreCase) && ch.Category.Equals(normCat, StringComparison.OrdinalIgnoreCase)) return true;

                // Prefixo de chave (ex: query "ACU" casa com "ACU_T60", query "SALA" casa com "SALA_Largura")
                if (ch.CleanKey.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase)) return true;
                if (ch.CleanKey.StartsWith(prefix + "::", StringComparison.OrdinalIgnoreCase)) return true;
                if (ch.CleanKey.StartsWith(prefix + ":", StringComparison.OrdinalIgnoreCase)) return true;

                // Prefixo ou conteúdo no NickName do transmissor
                if (!string.IsNullOrWhiteSpace(ch.SourceNickName) && ch.SourceNickName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;

                return false;
            }).OrderBy(c => c.Category).ThenBy(c => c.CleanKey).ToList();

            return matches;
        }

        /// <summary>
        /// Extrai metadados de uma chave: chave limpa, categoria, unidade e cor cromática.
        /// </summary>
        public static void ParseKeyMetadata(
            string rawKey, 
            string explicitUnit, 
            out string cleanKey, 
            out string category, 
            out string unit, 
            out Color color)
        {
            unit = string.IsNullOrWhiteSpace(explicitUnit) ? "" : explicitUnit.Trim();

            string trimmed = (rawKey ?? "").Trim();
            string catCandidate = "GEN";

            // 1. Verifica se inicia com colchetes de categoria: [CAT] Nome
            if (trimmed.StartsWith("["))
            {
                int closeIdx = trimmed.IndexOf(']');
                if (closeIdx > 1)
                {
                    catCandidate = trimmed.Substring(1, closeIdx - 1).Trim().ToUpperInvariant();
                    trimmed = trimmed.Substring(closeIdx + 1).Trim();
                }
            }

            // 2. Detecta unidade no restante entre colchetes ex: Nome [s] ou [dB]
            var matchUnit = Regex.Match(trimmed, @"\[([^\]]+)\]");
            if (matchUnit.Success)
            {
                if (string.IsNullOrEmpty(unit))
                {
                    unit = matchUnit.Groups[1].Value.Trim();
                }
                trimmed = Regex.Replace(trimmed, @"\[[^\]]+\]", "").Trim();
            }

            // 3. Se não achou categoria no início, verifica prefixo com separador ex: ACU_RT60 ou GEO::Raio
            if (catCandidate == "GEN")
            {
                int sepIdx = trimmed.IndexOfAny(new char[] { '_', ':', '/' });
                if (sepIdx > 0)
                {
                    catCandidate = trimmed.Substring(0, sepIdx).Trim().ToUpperInvariant();
                }
            }

            category = NormalizeCategory(catCandidate);
            color = GetCategoryColor(category);

            cleanKey = trimmed;
        }

        public static string CleanUpKey(string rawKey)
        {
            if (string.IsNullOrWhiteSpace(rawKey)) return "";
            string trimmed = rawKey.Trim();
            if (trimmed == "*" || trimmed.Equals("ALL", StringComparison.OrdinalIgnoreCase)) return trimmed;

            ParseKeyMetadata(rawKey, null, out string cleanKey, out _, out _, out _);
            return cleanKey;
        }

        public static string NormalizeCategory(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return "GEN";
            string upper = candidate.ToUpperInvariant();

            if (upper.StartsWith("ACU") || upper.StartsWith("ACOUS") || upper.StartsWith("AUDIO") || upper.StartsWith("SOUND"))
                return "ACU";
            if (upper.StartsWith("GEO") || upper.StartsWith("CAD") || upper.StartsWith("SHP") || upper.StartsWith("MESH"))
                return "GEO";
            if (upper.StartsWith("MAT") || upper.StartsWith("ABS") || upper.StartsWith("SCAT"))
                return "MAT";
            if (upper.StartsWith("SIM") || upper.StartsWith("ANA") || upper.StartsWith("CALC") || upper.StartsWith("RAY"))
                return "SIM";
            if (upper.StartsWith("CFG") || upper.StartsWith("SET") || upper.StartsWith("PAR") || upper.StartsWith("VAR"))
                return "CFG";
            if (upper.StartsWith("OUT") || upper.StartsWith("RES") || upper.StartsWith("EXP"))
                return "OUT";
            if (upper.StartsWith("IN") || upper.StartsWith("SRC") || upper.StartsWith("DAT"))
                return "IN";

            return upper.Length > 5 ? upper.Substring(0, 5) : upper;
        }

        public static Color GetCategoryColor(string category)
        {
            switch (category.ToUpperInvariant())
            {
                case "ACU": // Acústica: Teal / Ciano Acústico
                    return Color.FromArgb(0, 180, 216);
                case "GEO": // Geometria: Verde Esmeralda
                    return Color.FromArgb(46, 175, 100);
                case "MAT": // Material: Âmbar / Laranja
                    return Color.FromArgb(235, 130, 60);
                case "SIM": // Simulação / Análise: Roxo / Magenta
                    return Color.FromArgb(170, 70, 210);
                case "CFG": // Configuração / Global: Azul Cobalto
                    return Color.FromArgb(60, 120, 240);
                case "OUT": // Saída / Resultados: Coral / Carmesim
                    return Color.FromArgb(230, 70, 80);
                case "IN":  // Entrada / Fonte: Verde Lima / Oliva
                    return Color.FromArgb(112, 193, 179);
                default:    // Padrão: Grafite Ardósia
                    return Color.FromArgb(108, 117, 125);
            }
        }

        /// <summary>
        /// Duplica a estrutura da árvore (ramos e caminhos) instantaneamente de forma isolada,
        /// preservando os itens de forma leve sem forçar deep Duplicate de geometrias pesadas.
        /// </summary>
        public static GH_Structure<IGH_Goo> CloneStructure(GH_Structure<IGH_Goo> source)
        {
            if (source == null) return new GH_Structure<IGH_Goo>();
            return source.ShallowDuplicate();
        }

        /// <summary>
        /// Compara profundamente dois objetos IGH_Goo para igualdade de valor.
        /// </summary>
        public static bool AreGooEqual(IGH_Goo a, IGH_Goo b)
        {
            return PillDataFingerprint.AreGooEqual(a, b);
        }

        /// <summary>
        /// Compara árvores de dados (GH_Structure de IGH_Goo) de forma ultra-rápida.
        /// Valida topologia, contagens e amostragem de dados para não travar a UI/Engine com árvores gigantes.
        /// </summary>
        public static bool AreStructuresEqual(GH_Structure<IGH_Goo> a, GH_Structure<IGH_Goo> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.DataCount != b.DataCount) return false;
            if (a.Paths.Count != b.Paths.Count) return false;
            if (a.DataCount == 0 && b.DataCount == 0) return true;

            for (int p = 0; p < a.Paths.Count; p++)
            {
                var path = a.Paths[p];
                if (!b.PathExists(path)) return false;

                var branchA = a[path];
                var branchB = b[path];
                if (branchA == null && branchB == null) continue;
                if (branchA == null || branchB == null) return false;
                if (branchA.Count != branchB.Count) return false;

                int count = branchA.Count;
                if (count == 0) continue;

                if (count <= 20)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (!AreGooEqual(branchA[i], branchB[i])) return false;
                    }
                }
                else
                {
                    // Amostragem nos limites e pontos-chave para validação instantânea sem lag
                    int[] samples = new int[] { 0, 1, count / 4, count / 2, (3 * count) / 4, count - 2, count - 1 };
                    foreach (int idx in samples)
                    {
                        if (idx >= 0 && idx < count && !AreGooEqual(branchA[idx], branchB[idx]))
                            return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Se o cabo físico oculto (wire) estiver conectado a um PillSliderPool, filtra e extrai
        /// exclusivamente o ramo/valor correspondente ao slider da chave especificada (targetCleanKey).
        /// Se estiver conectado a outro componente (PillTransmitter, PillCache, etc.), mantém os dados intactos.
        /// </summary>
        public static GH_Structure<IGH_Goo> FilterWireDataForReceiver(
            IGH_Param wireParam, 
            GH_Structure<IGH_Goo> rawWireData, 
            string targetCleanKey,
            GH_Document doc = null)
        {
            if (wireParam == null || rawWireData == null || rawWireData.DataCount == 0 || string.IsNullOrWhiteSpace(targetCleanKey))
            {
                return rawWireData;
            }

            if (wireParam.Sources.Count == 0)
            {
                return rawWireData;
            }

            if (doc == null)
            {
                doc = wireParam.Attributes?.GetTopLevel?.DocObject?.OnPingDocument();
            }

            // Localizar se alguma fonte vem de um PillSliderPool_Component
            PillSliderPool_Component pool = null;

            foreach (var src in wireParam.Sources)
            {
                pool = src.Attributes?.GetTopLevel?.DocObject as PillSliderPool_Component;
                if (pool == null && doc != null)
                {
                    pool = doc.Objects.OfType<PillSliderPool_Component>().FirstOrDefault(p => p.Params.Output.Contains(src));
                }

                if (pool != null)
                {
                    break;
                }
            }

            if (pool == null || pool.Sliders.Count == 0)
            {
                return rawWireData;
            }

            string rawQuery = (targetCleanKey ?? "").Trim();
            string cleanKey = CleanUpKey(rawQuery);

            // Remove namespace de escopo se presente (ex: "MODELO::Raio" -> "Raio")
            int nsIdx = cleanKey.IndexOf("::", StringComparison.Ordinal);
            if (nsIdx >= 0 && nsIdx + 2 < cleanKey.Length)
            {
                cleanKey = cleanKey.Substring(nsIdx + 2).Trim();
            }

            // Encontrar o índice do slider no pool com múltiplas estratégias de matching
            int sliderIdx = -1;
            for (int i = 0; i < pool.Sliders.Count; i++)
            {
                var s = pool.Sliders[i];
                string sName = s.Name ?? "";
                string sCleanName = CleanUpKey(sName);
                string sCat = s.Category ?? "";
                string sFullKey = s.FullKey ?? "";
                string sCleanFullKey = CleanUpKey(sFullKey);
                string sSimpleKey = $"{sCat}_{sName}";
                string sCleanSimple = CleanUpKey(sSimpleKey);

                if (string.Equals(s.CleanKey, cleanKey, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sCleanName, cleanKey, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sCleanFullKey, cleanKey, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sCleanSimple, cleanKey, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sFullKey, rawQuery, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sName, rawQuery, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sSimpleKey, rawQuery, StringComparison.OrdinalIgnoreCase) ||
                    cleanKey.EndsWith("_" + sCleanName, StringComparison.OrdinalIgnoreCase) ||
                    cleanKey.EndsWith("::" + sCleanName, StringComparison.OrdinalIgnoreCase) ||
                    cleanKey.EndsWith(" " + sCleanName, StringComparison.OrdinalIgnoreCase))
                {
                    sliderIdx = i;
                    break;
                }

                if (cleanKey.StartsWith(sCat + "_", StringComparison.OrdinalIgnoreCase) &&
                    cleanKey.Substring(sCat.Length + 1).Equals(sCleanName, StringComparison.OrdinalIgnoreCase))
                {
                    sliderIdx = i;
                    break;
                }
                if (cleanKey.StartsWith(sCat + "::", StringComparison.OrdinalIgnoreCase) &&
                    cleanKey.Substring(sCat.Length + 2).Equals(sCleanName, StringComparison.OrdinalIgnoreCase))
                {
                    sliderIdx = i;
                    break;
                }

                string normQuery = Regex.Replace(cleanKey, @"[\s_\-:]+", "").ToLowerInvariant();
                string normSlider = Regex.Replace(sCleanName, @"[\s_\-:]+", "").ToLowerInvariant();
                if (!string.IsNullOrEmpty(normSlider) && (normQuery == normSlider || normQuery.EndsWith(normSlider)))
                {
                    sliderIdx = i;
                    break;
                }
            }

            // Se o canal já existe no barramento central com dado unitário, usa prioritariamente para garantir valor único
            if (sliderIdx < 0)
            {
                if (PillHub.TryGetChannel(cleanKey, out var ch) && ch.Data != null && ch.Data.DataCount > 0)
                {
                    return PillHub.CloneStructure(ch.Data);
                }
            }

            if (sliderIdx < 0)
            {
                // Se o cabo carrega todos os valores do pool e não casou o nome, retorna o primeiro item em vez da lista inteira
                if (rawWireData.DataCount > 1)
                {
                    var firstItem = rawWireData.AllData(true).FirstOrDefault();
                    if (firstItem != null)
                    {
                        var singleTree = new GH_Structure<IGH_Goo>();
                        singleTree.Append(firstItem, new GH_Path(0));
                        return singleTree;
                    }
                }
                return rawWireData;
            }

            // =========================================================================
            // EXTRAÇÃO CIRÚRGICA DO VALOR ESPECÍFICO DO SLIDER
            // =========================================================================

            // Caso 1: Fonte conectada é a saída Tree (Output[2]) - Árvore estruturada com ramos {0}, {1}, etc.
            if (rawWireData.Branches.Count > 1)
            {
                GH_Path targetPath = rawWireData.Paths.FirstOrDefault(p => p.Length > 0 && p[p.Length - 1] == sliderIdx)
                                  ?? (rawWireData.PathExists(new GH_Path(sliderIdx)) ? new GH_Path(sliderIdx) : null);
                if (targetPath != null)
                {
                    var branch = rawWireData[targetPath];
                    var filteredTree = new GH_Structure<IGH_Goo>();
                    filteredTree.AppendRange(branch, new GH_Path(0));
                    return filteredTree;
                }
            }

            // Caso 2: Fonte conectada é a saída Values (Output[0]) ou lista com todos os sliders
            var allItems = rawWireData.AllData(true).ToList();
            if (sliderIdx >= 0 && sliderIdx < allItems.Count)
            {
                var filteredTree = new GH_Structure<IGH_Goo>();
                filteredTree.Append(allItems[sliderIdx], new GH_Path(0));
                return filteredTree;
            }

            // Caso 3: Fallback direto pelo valor do slider ativo no objeto Pool
            if (sliderIdx >= 0 && sliderIdx < pool.Sliders.Count)
            {
                var s = pool.Sliders[sliderIdx];
                var filteredTree = new GH_Structure<IGH_Goo>();
                switch (s.DataType)
                {
                    case PillDataType.Integer:
                        filteredTree.Append(new GH_Integer(s.IntValue), new GH_Path(0));
                        break;
                    case PillDataType.Boolean:
                    case PillDataType.Button:
                        filteredTree.Append(new GH_Boolean(s.BoolValue), new GH_Path(0));
                        break;
                    case PillDataType.String:
                    case PillDataType.ValueList:
                        filteredTree.Append(new GH_String(s.StringValue), new GH_Path(0));
                        break;
                    case PillDataType.Domain:
                        filteredTree.Append(new GH_Interval(new Rhino.Geometry.Interval(s.DomainStart, s.DomainEnd)), new GH_Path(0));
                        break;
                    default:
                        filteredTree.Append(new GH_Number(s.Value), new GH_Path(0));
                        break;
                }
                return filteredTree;
            }

            return rawWireData;
        }
    }
}
