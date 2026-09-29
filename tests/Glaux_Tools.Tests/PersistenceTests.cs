using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Buraqueira_Tools.Data;
using Buraqueira_Tools.Persistence;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Xunit;
using Xunit.Abstractions;

namespace Glaux_Tools.Tests
{
    public sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "glaux_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }

    public class PersistenceTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TempDir _dir = new TempDir();

        public PersistenceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public void Dispose() => _dir.Dispose();

        private GlauxFileStore NewStore(string name = "teste.glauxdb") => new GlauxFileStore(_dir.File(name));

        private static StoreEntryDraft Draft(string key, GH_Structure<IGH_Goo> tree, string kind = StoreKinds.Dataset, params (string, string)[] meta)
        {
            var d = new StoreEntryDraft(kind, key).AddTree(StoreTreeNames.Data, TreeMapper.ToTable(tree));
            foreach (var (k, v) in meta) d.Metadata[k] = v;
            return d;
        }

        private static GH_Structure<IGH_Goo> Numbers(params double[] values)
        {
            var t = new GH_Structure<IGH_Goo>();
            foreach (var v in values) t.Append(new GH_Number(v), new GH_Path(0));
            return t;
        }

        public static IEnumerable<object[]> Shapes() => TestTrees.AllShapes();

        // =====================================================================
        // ROUND-TRIP DataTree → store → DataTree
        // =====================================================================

        [Theory]
        [MemberData(nameof(Shapes))]
        public void Store_RoundTrip_PreservesTree(string name, GH_Structure<IGH_Goo> tree)
        {
            var store = NewStore();
            store.Append(Draft("k", tree, meta: ("forma", name)));

            var reopened = NewStore();
            var header = reopened.GetLatest(StoreKinds.Dataset, "k");
            Assert.NotNull(header);
            Assert.Equal(name, header.Metadata["forma"]);
            Assert.Equal(TreeHash.Compute(tree), header.FindTree(StoreTreeNames.Data).Hash);

            var back = TreeMapper.ToTree(reopened.LoadTree(header, StoreTreeNames.Data));
            TestTrees.AssertStructurallyEqual(tree, back);
        }

        [Fact]
        public void Revisions_IncrementPerKindAndKey()
        {
            var store = NewStore();
            var r1 = store.Append(Draft("A", Numbers(1))).Header;
            var r2 = store.Append(Draft("A", Numbers(2))).Header;
            var b1 = store.Append(Draft("B", Numbers(1))).Header;
            var s1 = store.Append(Draft("A", Numbers(1), StoreKinds.Snapshot)).Header;

            Assert.Equal(new long[] { 1, 2, 1, 1 }, new[] { r1.Revision, r2.Revision, b1.Revision, s1.Revision });
            Assert.Equal(2, store.GetLatest(StoreKinds.Dataset, "A").Revision);
            Assert.Equal(1, store.GetRevision(StoreKinds.Dataset, "A", 1).Revision);
            Assert.Equal(new long[] { 1, 2 }, store.GetRevisions(StoreKinds.Dataset, "A").Select(h => h.Revision).ToArray());
            Assert.Equal(1, store.LoadTree(store.GetRevision(StoreKinds.Dataset, "A", 1), "data").Branches[0].Items[0].X);
        }

        [Fact]
        public void SkipIfUnchanged_DeduplicatesByHashAndMetadata()
        {
            var store = NewStore();
            var first = store.Append(Draft("A", Numbers(1, 2), meta: ("m", "1")), skipIfUnchanged: true);
            var same = store.Append(Draft("A", Numbers(1, 2), meta: ("m", "1")), skipIfUnchanged: true);
            var newMeta = store.Append(Draft("A", Numbers(1, 2), meta: ("m", "2")), skipIfUnchanged: true);
            var newData = store.Append(Draft("A", Numbers(1, 3), meta: ("m", "2")), skipIfUnchanged: true);

            Assert.True(first.Written);
            Assert.False(same.Written);
            Assert.Equal(first.Header.Id, same.Header.Id);
            Assert.True(newMeta.Written);
            Assert.True(newData.Written);
            Assert.Equal(3, store.ListEntries().Count);
        }

        [Fact]
        public void SecondInstance_SeesAppendsIncrementally()
        {
            var a = NewStore();
            var b = NewStore();
            a.Append(Draft("A", Numbers(1)));
            Assert.Single(b.ListEntries());
            a.Append(Draft("A", Numbers(2)));
            b.Append(Draft("A", Numbers(3)));
            Assert.Equal(new long[] { 1, 2, 3 }, a.ListEntries().Select(e => e.Revision).ToArray());
            Assert.Equal(3, b.GetLatest(StoreKinds.Dataset, "A").Revision);
        }

        [Fact]
        public void TornWrite_AtEnd_IsIgnoredAndRepairedOnNextWrite()
        {
            var store = NewStore();
            store.Append(Draft("A", Numbers(1)));
            store.Append(Draft("A", Numbers(2, 3, 4)));
            long full = new FileInfo(store.Path).Length;

            using (var fs = new FileStream(store.Path, FileMode.Open))
            {
                fs.SetLength(full - 10); // simula queda de energia no meio da 2ª gravação
            }

            var reopened = NewStore();
            Assert.Single(reopened.ListEntries());
            Assert.True(reopened.GetStats().TornTailBytes > 0);
            Assert.Null(reopened.GetStats().CorruptionMessage);

            var r = reopened.Append(Draft("A", Numbers(9)));
            Assert.Equal(2, r.Header.Revision);
            var again = NewStore();
            Assert.Equal(2, again.ListEntries().Count);
            Assert.Equal(0, again.GetStats().TornTailBytes);
            Assert.Equal(9, again.LoadTree(again.GetLatest(StoreKinds.Dataset, "A"), "data").Branches[0].Items[0].X);
        }

        [Fact]
        public void Corruption_InTheMiddle_BlocksWritesButKeepsEarlierEntries()
        {
            var store = NewStore();
            var first = store.Append(Draft("A", Numbers(1))).Header;
            var second = store.Append(Draft("A", Numbers(2))).Header;
            store.Append(Draft("A", Numbers(3)));

            // Estraga a assinatura do 2º registro
            using (var fs = new FileStream(store.Path, FileMode.Open))
            {
                fs.Position = GetOffset(second);
                fs.WriteByte(0x00);
            }

            var reopened = NewStore();
            Assert.Single(reopened.ListEntries());
            Assert.NotNull(reopened.GetStats().CorruptionMessage);
            Assert.Equal(1, reopened.LoadTree(reopened.GetById(first.Id), "data").Branches[0].Items[0].X);
            Assert.Throws<GlauxStoreException>(() => reopened.Append(Draft("A", Numbers(4))));
        }

        [Fact]
        public void Crc_DetectsBitRotInsideData()
        {
            var store = NewStore();
            var h = store.Append(Draft("A", Numbers(1, 2, 3))).Header;
            long offset = GetOffset(h);
            long length = new FileInfo(store.Path).Length;

            // Inverte um byte dentro dos dados da árvore (antes do CRC/marcador final)
            using (var fs = new FileStream(store.Path, FileMode.Open))
            {
                fs.Position = length - 12;
                int b = fs.ReadByte();
                fs.Position = length - 12;
                fs.WriteByte((byte)(b ^ 0xFF));
            }

            var reopened = NewStore();
            var header = Assert.Single(reopened.ListEntries());
            var ex = Assert.Throws<GlauxStoreException>(() => reopened.LoadTree(header, "data"));
            Assert.Contains("CRC", ex.Message);
            _output.WriteLine($"offset {offset}: {ex.Message}");
        }

        [Fact]
        public void Compact_KeepsLastRevisions_AndStaleInstancesRelocate()
        {
            var store = NewStore();
            for (int i = 1; i <= 5; i++) store.Append(Draft("A", Numbers(i)));
            store.Append(Draft("B", Numbers(100)));
            var stale = NewStore();
            var staleHeaders = stale.ListEntries();
            long before = new FileInfo(store.Path).Length;

            int removed = store.Compact(2);

            Assert.Equal(3, removed);
            Assert.True(new FileInfo(store.Path).Length < before);
            Assert.Equal(new long[] { 4, 5 }, store.GetRevisions(StoreKinds.Dataset, "A").Select(h => h.Revision).ToArray());
            Assert.Equal(1, store.GetLatest(StoreKinds.Dataset, "B").Revision);

            // Instância com índice antigo: a revisão 5 é relocalizada pelo id; a 1 não existe mais
            var rev5 = staleHeaders.Single(h => h.Key == "A" && h.Revision == 5);
            Assert.Equal(5, stale.LoadTree(rev5, "data").Branches[0].Items[0].X);
            var rev1 = staleHeaders.Single(h => h.Key == "A" && h.Revision == 1);
            Assert.Throws<GlauxStoreException>(() => stale.LoadTree(rev1, "data"));

            // Nova revisão continua a numeração
            Assert.Equal(6, store.Append(Draft("A", Numbers(6))).Header.Revision);
        }

        [Fact]
        public void NotAStore_FailsWithClearMessage()
        {
            string path = _dir.File("outro.glauxdb");
            System.IO.File.WriteAllText(path, "isto não é um store, é um texto qualquer com tamanho suficiente");
            var ex = Assert.Throws<GlauxStoreException>(() => new GlauxFileStore(path).ListEntries());
            Assert.Contains("não é um store Glaux", ex.Message);
        }

        [Fact]
        public void Registry_SharesOneInstancePerFile_AndForwardsChanges()
        {
            GlauxStoreRegistry.Clear();
            string path = _dir.File("pool.glauxdb");
            var a = GlauxStoreRegistry.Get(path);
            var b = GlauxStoreRegistry.Get(Path.Combine(_dir.Path, ".", "pool.glauxdb"));
            Assert.Same(a, b);

            StoreChangedEventArgs seen = null;
            EventHandler<StoreChangedEventArgs> handler = (s, e) => seen = e;
            GlauxStoreRegistry.AnyStoreChanged += handler;
            try
            {
                var origin = Guid.NewGuid();
                a.Append(Draft("A", Numbers(1)), origin: origin);
                Assert.NotNull(seen);
                Assert.Equal(origin, seen.Origin);
                Assert.Single(seen.Added);
            }
            finally
            {
                GlauxStoreRegistry.AnyStoreChanged -= handler;
            }
        }

        // =====================================================================
        // CONSULTAS
        // =====================================================================

        private GlauxFileStore QueryFixture()
        {
            var store = NewStore("consulta.glauxdb");
            var tree = new GH_Structure<IGH_Goo>();
            tree.AppendRange(new IGH_Goo[] { new GH_Number(0.5), new GH_Number(2.5), new GH_String("sala A") }, new GH_Path(0, 0));
            tree.AppendRange(new IGH_Goo[] { new GH_Number(7), new GH_Integer(3) }, new GH_Path(0, 1));
            tree.AppendRange(new IGH_Goo[] { new GH_Number(1.5) }, new GH_Path(1, 2));
            store.Append(Draft("ACU_T60", tree, meta: ("sala", "A")));
            store.Append(Draft("ACU_T60", Numbers(9, 10), meta: ("sala", "A")));
            store.Append(Draft("ACU_C80", Numbers(1, 2), meta: ("sala", "B")));
            store.Append(Draft("GEO.Raio", Numbers(4), meta: ("sala", "B")));
            store.Append(Draft("GEOxRaio", Numbers(4)));
            return store;
        }

        [Fact]
        public void Query_HeaderFilters()
        {
            var store = QueryFixture();
            var latest = StoreQueryEngine.Execute(store, new StoreQuery { KeyPattern = "ACU_*" });
            Assert.Equal(new[] { "ACU_C80@1", "ACU_T60@2" }, latest.Entries.Select(e => e.Reference).ToArray());

            var all = StoreQueryEngine.Execute(store, new StoreQuery { KeyPattern = "ACU_T60", Revisions = RevisionScope.All });
            Assert.Equal(2, all.Entries.Count);

            var byMeta = new StoreQuery();
            byMeta.MetadataEquals["sala"] = "B";
            Assert.Equal(2, StoreQueryEngine.Execute(store, byMeta).Entries.Count);

            // O ponto do padrão é literal (a entrada do usuário é escapada)
            var dot = StoreQueryEngine.Execute(store, new StoreQuery { KeyPattern = "GEO.Raio" });
            Assert.Equal("GEO.Raio", Assert.Single(dot.Entries).Key);

            var rev1 = StoreQueryEngine.Execute(store, new StoreQuery { KeyPattern = "ACU_T60", Revision = 1 });
            Assert.Equal(1, Assert.Single(rev1.Entries).Revision);
        }

        [Fact]
        public void Query_ItemFilters()
        {
            var store = QueryFixture();
            var q = new StoreQuery { KeyPattern = "ACU_T60", Revision = 1, Min = 1, Max = 5 };
            var r = StoreQueryEngine.Execute(store, q);
            Assert.Equal(new[] { 2.5, 3, 1.5 }, r.Items.Select(i => i.Value.NumericValue.Value).ToArray());

            q = new StoreQuery { KeyPattern = "ACU_T60", Revision = 1, PathMask = "{0;*}", TypeName = "Number" };
            Assert.Equal(new[] { 0.5, 2.5, 7 }, StoreQueryEngine.Execute(store, q).Items.Select(i => i.Value.X).ToArray());

            q = new StoreQuery { KeyPattern = "ACU_T60", Revision = 1, TextContains = "SALA" };
            var text = Assert.Single(StoreQueryEngine.Execute(store, q).Items);
            Assert.Equal(new[] { 0, 0 }, text.Path);
            Assert.Equal(2, text.Index);

            q = new StoreQuery { KeyPattern = "*", Min = 8 };
            var high = StoreQueryEngine.Execute(store, q);
            Assert.Equal(new[] { "ACU_T60@2" }, high.Entries.Select(e => e.Reference).ToArray());

            q = new StoreQuery { KeyPattern = "ACU_T60", Revisions = RevisionScope.All, Min = 0, ItemLimit = 2 };
            var limited = StoreQueryEngine.Execute(store, q);
            Assert.Equal(2, limited.Items.Count);
            Assert.True(limited.Truncated);
        }

        [Theory]
        [InlineData("{0;*}", "0;5", true)]
        [InlineData("{0;*}", "1;5", false)]
        [InlineData("{0;*}", "0", false)]
        [InlineData("{*;2}", "7;2", true)]
        [InlineData("{1;**}", "1;2;3;4", true)]
        [InlineData("{1;**}", "1", true)]
        [InlineData("{1;**}", "2;2", false)]
        [InlineData("{0..3;*}", "3;9", true)]
        [InlineData("{0..3;*}", "4;9", false)]
        [InlineData("{-2..-1}", "-1", true)]
        public void PathMask_Matches(string mask, string path, bool expected)
        {
            GlauxTreeTable.TryParsePath(path, out int[] p);
            Assert.Equal(expected, PathMask.Parse(mask).Matches(p));
        }

        [Fact]
        public void PathMask_RejectsInvalid()
        {
            Assert.Throws<FormatException>(() => PathMask.Parse("{a;1}"));
            Assert.Throws<FormatException>(() => PathMask.Parse("{**;1}"));
            Assert.Null(PathMask.Parse(""));
        }

        // =====================================================================
        // SINCRONIZAÇÃO
        // =====================================================================

        private static GlauxTreeTable T(params double[] values) => TreeMapper.ToTable(Numbers(values));

        [Fact]
        public void Sync_FirstPush_ThenClean_ThenEchoSuppressed()
        {
            var store = NewStore("sync.glauxdb");
            var opt = new SyncOptions { Direction = SyncDirection.TwoWay, Automatic = true };

            var s1 = SyncEngine.Step(store, "K", T(1), SyncMarker.None, opt, trigger: false);
            Assert.Equal(SyncAction.Push, s1.Decision.Action);
            Assert.True(s1.Wrote);

            var s2 = SyncEngine.Step(store, "K", T(1), s1.Marker, opt, false);
            Assert.Equal(SyncState.Clean, s2.Decision.State);
            Assert.Equal(SyncAction.None, s2.Decision.Action);

            // A saída realimenta a entrada: mesmo dado que o store → limpo, nada é gravado
            var s3 = SyncEngine.Step(store, "K", s2.Output, s2.Marker, opt, false);
            Assert.Equal(SyncState.Clean, s3.Decision.State);
            Assert.Single(store.ListEntries());
        }

        [Fact]
        public void Sync_Manual_WaitsForTrigger()
        {
            var store = NewStore("sync.glauxdb");
            var opt = new SyncOptions { Direction = SyncDirection.Push, Automatic = false };
            var pending = SyncEngine.Step(store, "K", T(1), SyncMarker.None, opt, trigger: false);
            Assert.Equal(SyncAction.None, pending.Decision.Action);
            Assert.Equal(SyncAction.Push, pending.Decision.Pending);
            Assert.False(store.Exists && store.ListEntries().Count > 0);

            var done = SyncEngine.Step(store, "K", T(1), pending.Marker, opt, trigger: true);
            Assert.True(done.Wrote);
        }

        [Fact]
        public void Sync_TwoDocuments_TwoWayAuto_ConvergeWithoutPingPong()
        {
            string path = _dir.File("compartilhado.glauxdb");
            var storeA = new GlauxFileStore(path);
            var storeB = new GlauxFileStore(path);
            var opt = new SyncOptions { Direction = SyncDirection.TwoWay, Automatic = true };

            var localA = T(1);
            var localB = T(2);
            var markerA = SyncMarker.None;
            var markerB = SyncMarker.None;

            // A publica primeiro
            var a1 = SyncEngine.Step(storeA, "K", localA, markerA, opt, false); markerA = a1.Marker;
            Assert.Equal(SyncAction.Push, a1.Decision.Action);

            // B tem outro dado desde o início: conflito na primeira sincronização (política Stop)
            var b1 = SyncEngine.Step(storeB, "K", localB, markerB, opt, false);
            Assert.Equal(SyncState.Conflict, b1.Decision.State);
            Assert.True(b1.Decision.Blocked);

            // B decide adotar o store
            var optStore = new SyncOptions { Direction = SyncDirection.TwoWay, Automatic = true, Conflict = ConflictPolicy.PreferStore };
            var b2 = SyncEngine.Step(storeB, "K", localB, markerB, optStore, false); markerB = b2.Marker;
            Assert.True(b2.Pulled);
            Assert.Equal(1, b2.Output.Branches[0].Items[0].X);

            // Várias soluções seguidas sem mudança local: ninguém grava nada (sem laço GH → DB → GH)
            for (int i = 0; i < 5; i++)
            {
                var sa = SyncEngine.Step(storeA, "K", localA, markerA, opt, false); markerA = sa.Marker;
                var sb = SyncEngine.Step(storeB, "K", localB, markerB, opt, false); markerB = sb.Marker;
                Assert.Equal(SyncAction.None, sa.Decision.Action);
                Assert.Equal(SyncAction.None, sb.Decision.Action);
                Assert.Equal(1, sb.Output.Branches[0].Items[0].X); // B continua emitindo o dado do store
            }
            Assert.Single(storeA.ListEntries());

            // A muda → push; B recebe por pull e não reenvia
            localA = T(3);
            var a2 = SyncEngine.Step(storeA, "K", localA, markerA, opt, false); markerA = a2.Marker;
            Assert.Equal(SyncAction.Push, a2.Decision.Action);
            var b3 = SyncEngine.Step(storeB, "K", localB, markerB, opt, false); markerB = b3.Marker;
            Assert.Equal(SyncAction.Pull, b3.Decision.Action);
            Assert.Equal(3, b3.Output.Branches[0].Items[0].X);
            var b4 = SyncEngine.Step(storeB, "K", localB, markerB, opt, false);
            Assert.Equal(SyncAction.None, b4.Decision.Action);
            Assert.Equal(2, storeA.ListEntries().Count);

            // B muda de verdade → push; A recebe
            localB = T(4);
            var b5 = SyncEngine.Step(storeB, "K", localB, markerB, opt, false); markerB = b5.Marker;
            Assert.Equal(SyncAction.Push, b5.Decision.Action);
            var a3 = SyncEngine.Step(storeA, "K", localA, markerA, opt, false);
            Assert.Equal(SyncAction.Pull, a3.Decision.Action);
            Assert.Equal(4, a3.Output.Branches[0].Items[0].X);
        }

        [Fact]
        public void Sync_Conflict_Policies_And_OneWayDirections()
        {
            var store = NewStore("sync.glauxdb");
            var twoWay = new SyncOptions { Direction = SyncDirection.TwoWay, Automatic = true };
            var marker = SyncEngine.Step(store, "K", T(1), SyncMarker.None, twoWay, false).Marker;

            // Outro escritor muda o store e o local também muda
            new GlauxFileStore(store.Path).Append(new StoreEntryDraft(StoreKinds.Dataset, "K").AddTree("data", T(50)));

            var stop = SyncEngine.Step(store, "K", T(2), marker, twoWay, false);
            Assert.Equal(SyncState.Conflict, stop.Decision.State);
            Assert.True(stop.Decision.Blocked);

            var local = SyncEngine.Step(store, "K", T(2), marker, new SyncOptions { Automatic = true, Conflict = ConflictPolicy.PreferLocal }, false);
            Assert.Equal(SyncAction.Push, local.Decision.Action);

            var pushOnly = new SyncOptions { Direction = SyncDirection.Push, Automatic = true };
            var pushNoLocalChange = SyncEngine.Step(store, "K", T(2), local.Marker, pushOnly, false);
            Assert.Equal(SyncAction.None, pushNoLocalChange.Decision.Action);

            var pullOnly = new SyncOptions { Direction = SyncDirection.Pull, Automatic = true };
            var pullLocalChanged = SyncEngine.Step(store, "K", T(99), local.Marker, pullOnly, false);
            Assert.Equal(SyncAction.None, pullLocalChanged.Decision.Action);
            Assert.Contains("somente Pull", pullLocalChanged.Decision.Reason);
        }

        // =====================================================================
        // VALIDAÇÃO
        // =====================================================================

        [Fact]
        public void Validation_AllRules()
        {
            var tree = new GH_Structure<IGH_Goo>();
            tree.AppendRange(new IGH_Goo[] { new GH_Number(1), new GH_Number(1), null, new GH_String("x"), new GH_Number(99) }, new GH_Path(0));
            tree.AppendRange(new IGH_Goo[] { new GH_Number(2) }, new GH_Path(1, 0));
            var table = TreeMapper.ToTable(tree);

            var ok = TreeValidator.Validate(table, new ValidationRules());
            Assert.True(ok.IsValid);

            var rules = new ValidationRules
            {
                AllowNulls = false,
                Unique = UniqueScope.PerBranch,
                Min = 0,
                Max = 10,
                ExpectedDepth = 1,
                ExpectedBranchCount = 3,
                UniformBranchLength = true
            };
            rules.AllowedTypes.Add("Number");
            rules.RequiredPaths.Add("{2}");
            var r = TreeValidator.Validate(table, rules);

            string Rules() => string.Join(",", r.Issues.Select(i => i.Rule).Distinct().OrderBy(x => x));
            Assert.False(r.IsValid);
            Assert.Equal("Comprimento,Duplicado,Estrutura,Faixa,Nulo,Profundidade,Tipo", Rules());
            Assert.Equal(new[] { true, false, false, false, false }, r.ItemMask[0]);
            Assert.Equal(new[] { true }, r.ItemMask[1]);

            var global = new ValidationRules { Unique = UniqueScope.Global };
            var dup = new GH_Structure<IGH_Goo>();
            dup.Append(new GH_Number(5), new GH_Path(0));
            dup.Append(new GH_Number(5), new GH_Path(1));
            Assert.Single(TreeValidator.Validate(TreeMapper.ToTable(dup), global).Issues);
            Assert.True(TreeValidator.Validate(TreeMapper.ToTable(dup), new ValidationRules { Unique = UniqueScope.PerBranch }).IsValid);
        }

        // =====================================================================
        // DESEMPENHO
        // =====================================================================

        [Fact]
        [Trait("Category", "Benchmark")]
        public void Benchmark_BulkInsert_And_LargeTree()
        {
            var store = NewStore("bench.glauxdb");
            const int n = 1000;
            var drafts = Enumerable.Range(0, n).Select(i => Draft("run_" + (i % 50), Numbers(i, i * 2, i * 3), StoreKinds.Experiment)).ToList();

            var sw = Stopwatch.StartNew();
            store.AppendBatch(drafts);
            long batchMs = sw.ElapsedMilliseconds;

            var single = NewStore("bench_single.glauxdb");
            sw.Restart();
            foreach (var d in drafts.Take(200)) single.Append(d);
            long singleMs = sw.ElapsedMilliseconds;

            sw.Restart();
            var reopened = NewStore("bench.glauxdb");
            int count = reopened.ListEntries().Count;
            long indexMs = sw.ElapsedMilliseconds;
            Assert.Equal(n, count);

            var big = TestTrees.Large(1000, 200);
            sw.Restart();
            var h = store.Append(Draft("grande", big)).Header;
            long bigWriteMs = sw.ElapsedMilliseconds;
            sw.Restart();
            var back = store.LoadTree(h, "data");
            long bigReadMs = sw.ElapsedMilliseconds;
            Assert.Equal(big.DataCount, back.ItemCount);

            sw.Restart();
            var q = StoreQueryEngine.Execute(store, new StoreQuery { KeyPattern = "grande", Min = 999.5 });
            long queryMs = sw.ElapsedMilliseconds;

            _output.WriteLine($"Lote de {n} entradas: {batchMs} ms ({batchMs * 1000.0 / n:F0} µs/entrada)");
            _output.WriteLine($"200 gravações individuais: {singleMs} ms ({singleMs * 1000.0 / 200:F0} µs/entrada, com flush cada)");
            _output.WriteLine($"Reindexar {n} entradas do disco: {indexMs} ms");
            _output.WriteLine($"Árvore de 200 000 itens: gravação {bigWriteMs} ms, leitura {bigReadMs} ms, consulta {queryMs} ms ({q.Items.Count} itens > 999.5)");
            _output.WriteLine($"Arquivo: {new FileInfo(store.Path).Length / 1024.0 / 1024.0:F2} MB");

            Assert.True(batchMs < 10000, $"lote lento: {batchMs} ms");
        }

        private static long GetOffset(StoreEntryHeader h)
        {
            var prop = typeof(StoreEntryHeader).GetProperty("RecordOffset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (long)prop.GetValue(h);
        }
    }
}
