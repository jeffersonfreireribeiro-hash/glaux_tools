using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Buraqueira_Tools;
using Buraqueira_Tools.Dashboard;
using Buraqueira_Tools.ProjectState;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Xunit;

namespace Glaux_Tools.Tests
{
    /// <summary>Camada Grasshopper do Dashboard que roda fora do Rhino: conversões, PillHub, Goo, persistência e cofres.</summary>
    public class DashboardGrasshopperTests
    {
        [Fact]
        public void Goo_ConvertsBothWays()
        {
            Assert.Equal(2.5, DashboardGoo.FromGoo(new GH_Number(2.5)).Number);
            Assert.Equal(7, DashboardGoo.FromGoo(new GH_Integer(7)).Number);
            Assert.True(DashboardGoo.FromGoo(new GH_Boolean(true)).Boolean);
            Assert.Equal("ok", DashboardGoo.FromGoo(new GH_String("ok")).Text);

            var series = DashboardGoo.FromGooList(new List<IGH_Goo> { new GH_Number(1), new GH_Integer(2), null, new GH_Boolean(true) });
            Assert.Equal(WidgetValueKind.Series, series.Kind);
            Assert.Equal(4, series.Series.Count);
            Assert.True(double.IsNaN(series.Series[2]));

            var slider = WidgetSpec.Create(WidgetKind.Slider, "i", new[] { new KeyValuePair<string, string>("step", "1"), new KeyValuePair<string, string>("max", "10") });
            Assert.IsType<GH_Integer>(DashboardGoo.ToGoo(slider, WidgetValue.FromNumber(3))); // passo inteiro → Integer
            var dd = WidgetSpec.Create(WidgetKind.Dropdown, "d", new[] { new KeyValuePair<string, string>("options", "A;B;C"), new KeyValuePair<string, string>("output", "index") });
            Assert.Equal(2, ((GH_Integer)DashboardGoo.ToGoo(dd, WidgetValue.FromText("C"))).Value);
        }

        [Fact]
        public void NamedData_ReadsBundlesAndNameValueText()
        {
            var bundle = new PillBundle { Namespace = "SALA" };
            bundle.Entries["SALA::Area"] = 120.5;
            bundle.Entries["Ativo"] = true;
            bundle.Entries["Serie"] = new List<object> { 1.0, 2.0, 3.0 };
            var warnings = new List<string>();
            var data = DashboardGoo.NamedData(new IGH_Goo[] { new GH_PillBundleGoo(bundle), new GH_String("T60 = 1,07\nEstado=ok\nsem igual"), new GH_Number(3) }, warnings);

            Assert.Equal(120.5, data["Area"].Number); // também sem o namespace
            Assert.Equal(120.5, data["sala::area"].Number);
            Assert.True(data["ativo"].Boolean);
            Assert.Equal(new[] { 1.0, 2, 3 }, data["serie"].Series.ToArray());
            Assert.Equal(1.07, data["T60"].Number, 10);
            Assert.Equal("ok", data["estado"].Text);
            Assert.Equal(2, warnings.Count); // "sem igual" e o número solto
        }

        [Fact]
        public void WidgetGoo_RoundTripsThroughGhIo_WithEmbeddedValue()
        {
            var spec = WidgetSpec.Create(WidgetKind.MiniChart, "Fitness | geração", new[] { new KeyValuePair<string, string>("key", "[OPT] Fitness") })
                .WithEmbeddedValue(WidgetValue.FromSeries(new[] { 3.0, 2.5, -1 }));
            var chunk = new GH_LooseChunk("w");
            Assert.True(new GH_DashboardWidgetGoo(spec).Write(chunk));
            var bytes = chunk.Serialize_Binary();

            var read = new GH_LooseChunk("w");
            read.Deserialize_Binary(bytes);
            var goo = new GH_DashboardWidgetGoo();
            Assert.True(goo.Read(read));
            Assert.Equal(spec.ToSpecLine(), goo.Value.ToSpecLine());
            Assert.Equal(spec.EmbeddedValue, goo.Value.EmbeddedValue);

            var cast = new GH_DashboardWidgetGoo();
            Assert.True(cast.CastFrom(new GH_String("toggle Luz | value=true")));
            Assert.True(cast.Value.Default.Boolean);
            Assert.False(cast.CastFrom(new GH_String("não é um widget")));
        }

        [Fact]
        public void Colors_ResolveHexNamesAndPillCategories()
        {
            Assert.True(DashboardColors.TryParse("#10B981", out var hex));
            Assert.Equal(Color.FromArgb(16, 185, 129).ToArgb(), hex.ToArgb());
            Assert.True(DashboardColors.TryParse("Orange", out var named));
            Assert.Equal(Color.Orange.ToArgb(), named.ToArgb());
            Assert.True(DashboardColors.TryParse("acu", out var acu));
            Assert.Equal(PillHub.GetCategoryColor("ACU").ToArgb(), acu.ToArgb());
            Assert.False(DashboardColors.TryParse("#12", out _));
            Assert.False(DashboardColors.TryParse("xyzzy", out _));

            var fallback = Color.Purple;
            var geo = WidgetSpec.Create(WidgetKind.Slider, "r", new[] { new KeyValuePair<string, string>("key", "[GEO] Raio") });
            Assert.Equal(PillHub.GetCategoryColor("GEO").ToArgb(), DashboardColors.Resolve(geo, fallback).ToArgb());
            var plain = WidgetSpec.Create(WidgetKind.Slider, "r", new[] { new KeyValuePair<string, string>("key", "Raio") });
            Assert.Equal(fallback.ToArgb(), DashboardColors.Resolve(plain, fallback).ToArgb()); // sem categoria: cor do painel
        }

        /// <summary>
        /// PillHub.Publish carrega WinForms (agenda soluções no canvas), indisponível no .NET 8 do Linux:
        /// o canal é colocado no barramento pelo mesmo dicionário que o Publish usa.
        /// </summary>
        private static void PutChannel(string rawKey, params IGH_Goo[] items)
        {
            var channels = (System.Collections.Concurrent.ConcurrentDictionary<string, PillChannel>)typeof(PillHub)
                .GetField("_channels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
            var tree = new GH_Structure<IGH_Goo>();
            foreach (var g in items) tree.Append(g);
            string clean = PillHub.CleanUpKey(rawKey);
            if (items.Length == 0) channels.TryRemove(clean, out _);
            else channels[clean] = new PillChannel { RawKey = rawKey, CleanKey = clean, Data = tree, LastUpdated = DateTime.Now.AddTicks(channels.Count + Environment.TickCount) };
        }

        [Fact]
        public void HubLiveSource_ReadsChannelsWithoutCloning_AndRefreshesOnRepublish()
        {
            string key = "[ACU] T60_dash_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            var source = new HubLiveSource();
            Assert.False(source.TryGet(key, out _, out string status));
            Assert.Equal("canal ausente", status);

            PutChannel(key, new GH_Number(1.07));
            Assert.True(source.TryGet(key, out var v1, out _));
            Assert.Equal(1.07, v1.Number);

            PutChannel(key, new GH_Number(3), new GH_Number(2), new GH_Number(1)); // republicado: outro objeto de canal
            Assert.True(source.TryGet(key, out var v2, out _));
            Assert.Equal(WidgetValueKind.Series, v2.Kind);

            PutChannel(key);
            Assert.False(source.TryGet(key, out _, out status));
            Assert.Equal("canal ausente", status);
        }

        [Fact]
        public void ControlCompatibility_HandlesDashboardControls()
        {
            string id = Guid.NewGuid().ToString("D");
            var current = new List<ControlState>
            {
                new ControlState { Kind = ControlKinds.Dashboard, Id = id + "|w", Name = "Painel: Largura", Number = 5, Min = 0, Max = 10 },
                new ControlState { Kind = ControlKinds.Dashboard, Id = id + "|m", Name = "Painel: Modo", Text = "A" },
                new ControlState { Kind = ControlKinds.Dashboard, Id = id + "|t", Name = "Painel: Ativo", Boolean = false }
            };
            var saved = new List<ControlState>
            {
                new ControlState { Kind = ControlKinds.Dashboard, Id = id + "|w", Name = "Painel: Largura", Number = 12 },
                new ControlState { Kind = ControlKinds.Dashboard, Id = id + "|m", Name = "Painel: Modo", Text = "Z" },
                new ControlState { Kind = ControlKinds.Dashboard, Id = id + "|t", Name = "Painel: Ativo", Boolean = true }
            };
            var options = new Dictionary<string, List<string>> { [id + "|m"] = new List<string> { "A", "B" } };
            var plan = ControlCompatibility.Match(saved, current, c => options.TryGetValue(c.Id, out var o) ? o : null);

            Assert.False(plan[0].Compatible); // fora da faixa atual
            Assert.Contains("fora da faixa", plan[0].Reason);
            Assert.False(plan[1].Compatible); // opção não existe mais
            Assert.Contains("'Z'", plan[1].Reason);
            Assert.True(plan[2].Compatible);
        }

        private static DashboardController NewController(params string[] lines)
        {
            var c = new DashboardController();
            c.SetSpec(DashboardSpecParser.Parse(lines));
            return c;
        }

        [Fact]
        public void Persistence_WritesOnlyRuntimeState_AndReadsItBack()
        {
            var a = NewController("slider Largura | id=w | min=0 | max=10", "toggle Ativo | id=t", "dropdown Modo | id=m | options=A;B");
            a.State.Set(a.Spec.Find("w"), WidgetValue.FromNumber(7.25));
            a.State.Set(a.Spec.Find("t"), WidgetValue.FromBoolean(true));
            a.State.Set(a.Spec.Find("m"), WidgetValue.FromText("B"));

            var chunk = new GH_LooseChunk("comp");
            DashboardVault.Write(chunk, a.State, "abc123");
            var bytes = chunk.Serialize_Binary();
            var read = new GH_LooseChunk("comp");
            read.Deserialize_Binary(bytes);

            // A configuração vem das entradas do componente, não do arquivo
            var b = NewController("slider Largura | id=w | min=0 | max=10", "toggle Ativo | id=t", "dropdown Modo | id=m | options=A;B");
            Assert.Equal("abc123", DashboardVault.Read(read, b.State));
            Assert.Equal(new[] { "w=7.25", "t=true", "m=B" }, b.State.ToLines(b.Spec).ToArray());
            Assert.Null(DashboardVault.Read(new GH_LooseChunk("vazio"), new DashboardState())); // arquivo antigo sem estado
        }

        [Fact]
        public void VaultContracts_CaptureAndApplyOnlyValues()
        {
            var owner = Guid.NewGuid();
            var c = NewController("slider Largura | id=w | min=0 | max=10", "toggle Ativo | id=t", "dropdown Modo | id=m | options=A;B", "button Ir | id=b", "number T60");

            var states = DashboardVault.Capture(owner, "Painel", c).ToList();
            Assert.Equal(3, states.Count); // botão e indicador não são estado
            Assert.All(states, s => Assert.Equal(ControlKinds.Dashboard, s.Kind));
            Assert.Equal(owner.ToString("D") + "|w", states[0].Id);
            Assert.Equal("Painel: Largura", states[0].Name);
            Assert.Equal(10, states[0].Max);
            Assert.Equal(new[] { "A", "B" }, DashboardVault.Options(owner, c)[owner.ToString("D") + "|m"].ToArray());

            Assert.True(DashboardVault.Apply(c, "w", new ControlState { Kind = ControlKinds.Dashboard, Number = 3 }));
            Assert.False(DashboardVault.Apply(c, "w", new ControlState { Kind = ControlKinds.Dashboard, Number = 3 }));
            Assert.True(DashboardVault.Apply(c, "m", new ControlState { Kind = ControlKinds.Dashboard, Text = "b" }));
            Assert.False(DashboardVault.Apply(c, "b", new ControlState { Boolean = true })); // botão: não restaurável
            Assert.False(DashboardVault.Apply(c, "nao_existe", new ControlState { Number = 1 }));
            Assert.Equal(new[] { "w=3", "t=false", "m=B" }, c.State.ToLines(c.Spec).ToArray());

            // Captura → Match → aplicação completa num painel "reaberto"
            var reopened = NewController("slider Largura | id=w | min=0 | max=10", "toggle Ativo | id=t", "dropdown Modo | id=m | options=A;B");
            var saved = DashboardVault.Capture(owner, "Painel", c).ToList();
            var plan = ControlCompatibility.Match(saved, DashboardVault.Capture(owner, "Painel", reopened).ToList(),
                cs => DashboardVault.Options(owner, reopened).TryGetValue(cs.Id, out var o) ? o : null);
            Assert.All(plan, m => Assert.True(m.Compatible, m.Reason));
            foreach (var m in plan) DashboardVault.Apply(reopened, m.Current.Id.Substring(m.Current.Id.IndexOf('|') + 1), m.Saved);
            Assert.Equal(c.State.ToLines(c.Spec), reopened.State.ToLines(reopened.Spec));
        }

        [Fact]
        public void ComponentGuids_AreUniqueAcrossThePlugin()
        {
            // Componentes do GH carregam WinForms (não instanciáveis no .NET 8 do Linux): verificação pelo código-fonte
            string dir = AppContext.BaseDirectory;
            while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir, "src", "Glaux_Tools.csproj"))) dir = System.IO.Path.GetDirectoryName(dir);
            Assert.NotNull(dir);
            var guidRegex = new System.Text.RegularExpressions.Regex(@"ComponentGuid\s*=>\s*new\s+Guid\(""([0-9a-fA-F-]{36})""\)");
            var found = new List<(string file, string guid)>();
            foreach (var file in System.IO.Directory.GetFiles(System.IO.Path.Combine(dir, "src"), "*.cs", System.IO.SearchOption.AllDirectories))
            {
                foreach (System.Text.RegularExpressions.Match m in guidRegex.Matches(System.IO.File.ReadAllText(file)))
                {
                    found.Add((System.IO.Path.GetFileName(file), m.Groups[1].Value.ToLowerInvariant()));
                }
            }
            Assert.True(found.Count > 100);
            var duplicates = found.GroupBy(f => f.guid).Where(g => g.Count() > 1).Select(g => g.Key + ": " + string.Join(", ", g.Select(x => x.file))).ToList();
            Assert.True(duplicates.Count == 0, string.Join("; ", duplicates));
            Assert.Contains(found, f => f.file == "PillDashboard_Component.cs" && f.guid == "49ee50b1-5842-466b-89ee-cd9ff6df435d");
            Assert.Contains(found, f => f.file == "PillDashboardBuilder_Component.cs" && f.guid == "d7eae36c-a08f-4120-bce0-bce93f4c80d9");
        }
    }
}
