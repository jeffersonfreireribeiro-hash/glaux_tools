using System;
using System.Collections.Generic;
using System.Linq;
using Buraqueira_Tools;
using Buraqueira_Tools.Data;
using Buraqueira_Tools.Persistence;
using Buraqueira_Tools.ProjectState;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Xunit;
using Xunit.Abstractions;

namespace Glaux_Tools.Tests
{
    public class ProjectStateTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TempDir _dir = new TempDir();

        public ProjectStateTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public void Dispose() => _dir.Dispose();

        private static PillBundle Bundle(double largura, params double[] alturas)
        {
            var b = new PillBundle { Namespace = "SALA" };
            b.Entries["Largura"] = largura;
            b.Entries["Alturas"] = alturas.Cast<object>().ToList();
            b.Entries["Nome"] = "Auditório";
            b.Entries["Ativo"] = true;
            b.Entries["Assentos"] = 120;
            b.Units["Largura"] = "m";
            return b;
        }

        private static GlauxTreeTable Tree(params double[] values)
        {
            var t = new GH_Structure<IGH_Goo>();
            for (int i = 0; i < values.Length; i++) t.Append(new GH_Number(values[i]), new GH_Path(i / 2));
            return TreeMapper.ToTable(t);
        }

        private static SnapshotParts Parts(double largura, double[] outputs, string notes = "")
        {
            var p = new SnapshotParts { Notes = notes, RuntimeMs = 12.5 };
            foreach (var kv in BundleTrees.FromBundle(Bundle(largura, 2.8, 3.2), p.Units)) p.Parameters[kv.Key] = kv.Value;
            p.Inputs = Tree(1, 2, 3);
            p.Outputs = Tree(outputs);
            p.Controls.Add(new ControlState { Kind = ControlKinds.Slider, Id = "a", Name = "Raio", Number = 4.25, Min = 0, Max = 10 });
            p.Controls.Add(new ControlState { Kind = ControlKinds.Toggle, Id = "b", Name = "Plateia", Boolean = true });
            p.Controls.Add(new ControlState { Kind = ControlKinds.ValueList, Id = "c", Name = "Material", Text = "Concreto" });
            p.HubChannels["GEO_Raio"] = Tree(4.25);
            p.Environment["glaux.version"] = "teste";
            return p;
        }

        // =====================================================================
        // SNAPSHOT → ALTERAÇÃO → RESTORE
        // =====================================================================

        [Fact]
        public void Snapshot_Alter_Restore_ReturnsExactOriginalState()
        {
            var store = new GlauxFileStore(_dir.File("vault.glauxdb"));
            var original = Parts(5.5, new[] { 1.2, 1.4, 1.1 }, "estado base");
            var r1 = store.Append(SnapshotCodec.BuildDraft(StoreKinds.Snapshot, "Sala", original)).Header;

            // Alteração: novo estado gravado por cima
            store.Append(SnapshotCodec.BuildDraft(StoreKinds.Snapshot, "Sala", Parts(7.0, new[] { 0.9, 1.0, 0.8 }, "mais larga")));

            // Restore da revisão 1 numa instância nova (como ao reabrir o projeto)
            var reopened = new GlauxFileStore(store.Path);
            var header = EntryRef.Resolve(reopened, StoreKinds.Snapshot, "Sala@1");
            Assert.Equal(r1.Id, header.Id);
            var restored = SnapshotCodec.Read(header, reopened.LoadTrees(header));

            var bundle = BundleTrees.ToBundle(restored.Parameters, restored.Units);
            Assert.Equal(5.5, bundle.Entries["Largura"]);
            Assert.Equal(new object[] { 2.8, 3.2 }, ((List<object>)bundle.Entries["Alturas"]).ToArray());
            Assert.Equal("Auditório", bundle.Entries["Nome"]);
            Assert.Equal(true, bundle.Entries["Ativo"]);
            Assert.Equal(120, bundle.Entries["Assentos"]);
            Assert.Equal("m", bundle.Units["Largura"]);

            Assert.Equal(TreeHash.Compute(original.Inputs), TreeHash.Compute(restored.Inputs));
            Assert.Equal(TreeHash.Compute(original.Outputs), TreeHash.Compute(restored.Outputs));
            Assert.Equal(TreeHash.Compute(original.HubChannels["GEO_Raio"]), TreeHash.Compute(restored.HubChannels["GEO_Raio"]));
            Assert.Equal(original.Controls.Select(c => c.ToString()), restored.Controls.Select(c => c.ToString()));
            Assert.Equal("estado base", restored.Notes);
            Assert.Equal(12.5, restored.RuntimeMs);
            Assert.Equal("teste", restored.Environment["glaux.version"]);
        }

        [Fact]
        public void CombinedHashes_SeparateInputsFromOutputs()
        {
            var a = SnapshotCodec.BuildDraft(StoreKinds.Snapshot, "S", Parts(5, new[] { 1.0 }, "a"));
            var b = SnapshotCodec.BuildDraft(StoreKinds.Snapshot, "S", Parts(5, new[] { 2.0 }, "b"));
            var c = SnapshotCodec.BuildDraft(StoreKinds.Snapshot, "S", Parts(6, new[] { 1.0 }, "a"));

            Assert.Equal(a.Metadata[VaultNames.MetaInputsHash], b.Metadata[VaultNames.MetaInputsHash]);
            Assert.NotEqual(a.Metadata[VaultNames.MetaOutputsHash], b.Metadata[VaultNames.MetaOutputsHash]);
            Assert.NotEqual(a.Metadata[VaultNames.MetaInputsHash], c.Metadata[VaultNames.MetaInputsHash]);
            Assert.Equal(a.Metadata[VaultNames.MetaOutputsHash], c.Metadata[VaultNames.MetaOutputsHash]);
        }

        [Fact]
        public void Compare_ReportsParameterChangesTreeDeltasAndEnvironment()
        {
            var store = new GlauxFileStore(_dir.File("vault.glauxdb"));
            var p1 = Parts(5.5, new[] { 1.2, 1.4, 1.1 }, "base");
            var p2 = Parts(7.0, new[] { 1.2, 1.9, 1.1 }, "variação");
            p2.HubChannels.Clear();
            p2.Environment["glaux.version"] = "teste-2";
            var a = store.Append(SnapshotCodec.BuildDraft(StoreKinds.Snapshot, "Sala", p1)).Header;
            var b = store.Append(SnapshotCodec.BuildDraft(StoreKinds.Snapshot, "Sala", p2)).Header;

            var diff = SnapshotComparer.Compare(a, b, (h, n) => store.LoadTree(h, n));
            var largura = diff.Trees.Single(t => t.Name == "param:Largura");
            Assert.Equal(TreeChange.Changed, largura.Change);
            Assert.Equal("5.5 → 7", largura.Detail);

            var outTree = diff.Trees.Single(t => t.Name == "out");
            Assert.Equal(TreeChange.Changed, outTree.Change);
            Assert.Equal(1, outTree.ChangedItems);
            Assert.Equal(0.5, outTree.MaxNumericDelta.Value, 10);
            Assert.Contains("{0}[1]", outTree.Detail);

            Assert.Equal(TreeChange.Removed, diff.Trees.Single(t => t.Name == "hub:GEO_Raio").Change);
            Assert.Equal(TreeChange.Same, diff.Trees.Single(t => t.Name == "in").Change);
            Assert.False(diff.DataEqual);
            Assert.False(diff.InputsEqual);
            Assert.False(diff.OutputsEqual);
            Assert.Contains(diff.MetadataChanges, m => m.StartsWith("glaux.version: teste → teste-2"));
            Assert.Contains(diff.MetadataChanges, m => m.StartsWith("vault.notes"));

            var self = SnapshotComparer.Compare(a, a, (h, n) => store.LoadTree(h, n));
            Assert.True(self.DataEqual && self.InputsEqual && self.OutputsEqual);
            Assert.Empty(self.MetadataChanges);
        }

        // =====================================================================
        // CONTROLES
        // =====================================================================

        [Fact]
        public void ControlState_TableRoundTrip()
        {
            var states = Parts(1, new[] { 1.0 }).Controls;
            states.Add(new ControlState { Kind = ControlKinds.PoolSlider, Id = "pool|GEO_Raio", Name = "[GEO] Raio", Number = 1, Min = 0, Max = 5, Extra = 3 });
            states.Add(new ControlState { Kind = ControlKinds.Panel, Id = "d", Name = "Notas", Text = null });
            var back = ControlState.FromTable(ControlState.ToTable(states));
            Assert.Equal(states.Select(s => s.ToString()), back.Select(s => s.ToString()));
            Assert.Equal(3, back.Single(s => s.Kind == ControlKinds.PoolSlider).Extra);
        }

        [Fact]
        public void ControlCompatibility_CoversAllCases()
        {
            var saved = new List<ControlState>
            {
                new ControlState { Kind = ControlKinds.Slider, Id = "s1", Name = "Raio", Number = 4, Min = 0, Max = 10 },
                new ControlState { Kind = ControlKinds.Slider, Id = "velho", Name = "Altura", Number = 2 },
                new ControlState { Kind = ControlKinds.Slider, Id = "s3", Name = "Largura", Number = 50 },
                new ControlState { Kind = ControlKinds.Toggle, Id = "t1", Name = "Plateia", Boolean = true },
                new ControlState { Kind = ControlKinds.ValueList, Id = "v1", Name = "Material", Text = "Madeira" },
                new ControlState { Kind = ControlKinds.Slider, Id = "x", Name = "Sumiu", Number = 1 },
                new ControlState { Kind = ControlKinds.Slider, Id = "y", Name = "Duplo", Number = 1 }
            };
            var current = new List<ControlState>
            {
                new ControlState { Kind = ControlKinds.Slider, Id = "s1", Name = "Raio", Min = 0, Max = 10 },
                new ControlState { Kind = ControlKinds.Slider, Id = "novo", Name = "Altura", Min = 0, Max = 5 },
                new ControlState { Kind = ControlKinds.Slider, Id = "s3", Name = "Largura", Min = 0, Max = 20 },
                new ControlState { Kind = ControlKinds.ValueList, Id = "t1", Name = "Plateia" },
                new ControlState { Kind = ControlKinds.ValueList, Id = "v1", Name = "Material" },
                new ControlState { Kind = ControlKinds.Slider, Id = "d1", Name = "Duplo", Min = 0, Max = 5 },
                new ControlState { Kind = ControlKinds.Slider, Id = "d2", Name = "Duplo", Min = 0, Max = 5 }
            };
            var options = new Dictionary<string, List<string>> { ["v1"] = new List<string> { "Concreto", "Vidro" } };

            var plan = ControlCompatibility.Match(saved, current, c => options.TryGetValue(c.Id, out var o) ? o : null);
            string R(string name) => plan.Single(m => m.Saved.Name == name).Reason;

            Assert.True(plan.Single(m => m.Saved.Name == "Raio").Compatible);
            var altura = plan.Single(m => m.Saved.Name == "Altura");
            Assert.True(altura.Compatible);
            Assert.True(altura.MatchedByName);
            Assert.Contains("fora da faixa", R("Largura"));
            Assert.Contains("tipo mudou", R("Plateia"));
            Assert.Contains("não existe mais na lista", R("Material"));
            Assert.Contains("não existe mais", R("Sumiu"));
            Assert.Contains("ambíguo", R("Duplo"));
        }

        [Theory]
        [InlineData("Sala", "Sala", 0)]
        [InlineData("Sala@2", "Sala", 2)]
        [InlineData("Sala@-1", "Sala", -1)]
        [InlineData("a@b@3", "a@b", 3)]
        [InlineData("e-mail@dominio", "e-mail@dominio", 0)]
        public void EntryRef_Parses(string text, string key, long revision)
        {
            Assert.True(EntryRef.TryParse(text, out string k, out long r));
            Assert.Equal(key, k);
            Assert.Equal(revision, r);
        }

        [Fact]
        public void EntryRef_ResolvesLatestSpecificAndRelative()
        {
            var store = new GlauxFileStore(_dir.File("refs.glauxdb"));
            for (int i = 0; i < 3; i++) store.Append(new StoreEntryDraft(StoreKinds.Snapshot, "S").AddTree("out", Tree(i)));
            Assert.Equal(3, EntryRef.Resolve(store, StoreKinds.Snapshot, "S").Revision);
            Assert.Equal(2, EntryRef.Resolve(store, StoreKinds.Snapshot, "S@2").Revision);
            Assert.Equal(1, EntryRef.Resolve(store, StoreKinds.Snapshot, "S@-2").Revision);
            Assert.Null(EntryRef.Resolve(store, StoreKinds.Snapshot, "S@-3"));
            Assert.Null(EntryRef.Resolve(store, StoreKinds.Snapshot, "S@9"));
            Assert.Null(EntryRef.Resolve(store, StoreKinds.Dataset, "S"));
        }

        [Fact]
        public void Environment_CaptureNeverThrowsOutsideRhino()
        {
            var env = EnvironmentInfo.Capture("projeto.gh", Guid.NewGuid());
            foreach (var k in new[] { EnvironmentInfo.GlauxVersion, EnvironmentInfo.RhinoVersion, EnvironmentInfo.GrasshopperVersion, EnvironmentInfo.OperatingSystem, EnvironmentInfo.Runtime, EnvironmentInfo.Document, EnvironmentInfo.DocumentId })
            {
                Assert.True(env.ContainsKey(k), k);
                Assert.False(string.IsNullOrEmpty(env[k]), k);
            }
            // Versão do próprio .gha (não fixa: muda a cada release)
            var asmVersion = typeof(EnvironmentInfo).Assembly.GetName().Version;
            Assert.StartsWith($"{asmVersion.Major}.{asmVersion.Minor}.{asmVersion.Build}", env[EnvironmentInfo.GlauxVersion]);
            _output.WriteLine(string.Join("\n", env.Select(kv => $"{kv.Key} = {kv.Value}")));
        }

        [Fact]
        public void BundleTrees_RoundTripMixedValues()
        {
            var b = Bundle(3.25, 1, 2, 3);
            b.Entries["Ponto"] = new Point3d(1, 2, 3);
            b.Entries["Vazio"] = null;
            var units = new Dictionary<string, string>();
            var trees = BundleTrees.FromBundle(b, units);
            var back = BundleTrees.ToBundle(trees, units);

            Assert.Equal(3.25, back.Entries["Largura"]);
            Assert.Equal(3, ((List<object>)back.Entries["Alturas"]).Count);
            Assert.Equal(new Point3d(1, 2, 3), back.Entries["Ponto"]);
            Assert.Null(back.Entries["Vazio"]);
            Assert.Equal("m", back.Units["Largura"]);
        }

        // =====================================================================
        // EXPERIMENTOS: "com quais parâmetros esse resultado foi produzido?"
        // =====================================================================

        [Fact]
        public void Experiment_FindParametersThatProducedAResult()
        {
            var store = new GlauxFileStore(_dir.File("exp.glauxdb"));
            var drafts = new List<StoreEntryDraft>();
            for (int i = 0; i < 40; i++)
            {
                double largura = 4 + i * 0.25;
                double t60 = Math.Round(0.6 + Math.Abs(largura - 8.5) * 0.3, 4); // mínimo em largura = 8.5
                var parts = new SnapshotParts { RuntimeMs = 10 + i };
                var bundle = new PillBundle();
                bundle.Entries["Largura"] = largura;
                foreach (var kv in BundleTrees.FromBundle(bundle)) parts.Parameters[kv.Key] = kv.Value;
                parts.Outputs = Tree(t60);
                parts.Config["solver"] = "raytracing";
                drafts.Add(SnapshotCodec.BuildDraft(StoreKinds.Experiment, "Otimizacao_T60", parts, resultsAsOutputs: true));
            }
            store.AppendBatch(drafts);

            // Quais execuções tiveram T60 <= 0.68 s? (8.25 e 8.75 dão 0.675; 8.5 dá 0.6)
            var q = new StoreQuery
            {
                Kind = StoreKinds.Experiment,
                KeyPattern = "Otimizacao_T60",
                Revisions = RevisionScope.All,
                TreeName = VaultNames.Results,
                Max = 0.68
            };
            var found = StoreQueryEngine.Execute(store, q);
            Assert.Equal(3, found.Entries.Count);

            // E com quais parâmetros?
            var larguras = found.Entries
                .Select(e => SnapshotCodec.Read(e, store.LoadTrees(e)))
                .Select(p => (double)BundleTrees.ToBundle(p.Parameters, p.Units).Entries["Largura"])
                .OrderBy(x => x)
                .ToArray();
            Assert.Equal(new[] { 8.25, 8.5, 8.75 }, larguras);
            Assert.All(found.Entries, e => Assert.Equal("raytracing", e.Metadata["config.solver"]));
        }

        // =====================================================================
        // CACHE / IDENTIDADE
        // =====================================================================

        [Fact]
        public void Cache_SameInputHits_ChangedInputInvalidates()
        {
            var store = new GlauxFileStore(_dir.File("cache.glauxdb"));
            var input = TestTrees.Large(20, 50);
            var first = store.Append(new StoreEntryDraft(StoreKinds.Dataset, "calc").AddTree("data", TreeMapper.ToTable(input)), skipIfUnchanged: true);
            var hit = store.Append(new StoreEntryDraft(StoreKinds.Dataset, "calc").AddTree("data", TreeMapper.ToTable(TestTrees.Large(20, 50))), skipIfUnchanged: true);
            Assert.True(first.Written);
            Assert.False(hit.Written); // mesma identidade: "cache hit", nada é regravado

            var changed = TestTrees.Large(20, 50);
            changed.Branches[7][33] = new GH_Number(-1);
            var miss = store.Append(new StoreEntryDraft(StoreKinds.Dataset, "calc").AddTree("data", TreeMapper.ToTable(changed)), skipIfUnchanged: true);
            Assert.True(miss.Written); // entrada alterada: invalidação

            // O fingerprint do Pill Compute Cache é tolerante (limiar 1e-4); a identidade é exata
            var a = new GH_Structure<IGH_Goo>(); a.Append(new GH_Number(1.0), new GH_Path(0));
            var b = new GH_Structure<IGH_Goo>(); b.Append(new GH_Number(1.00001), new GH_Path(0));
            Assert.Equal(PillDataFingerprint.ComputeTreeHash(a), PillDataFingerprint.ComputeTreeHash(b));
            Assert.NotEqual(TreeHash.Compute(a), TreeHash.Compute(b));
        }
    }
}
