using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Buraqueira_Tools.Dashboard;
using Xunit;
using Xunit.Abstractions;

namespace Glaux_Tools.Tests
{
    public class DashboardSpecTests
    {
        [Fact]
        public void Parser_ReadsPanelSettingsWidgetsAndAliases()
        {
            var spec = DashboardSpecParser.Parse(new[]
            {
                "# painel de estudo",
                "title = Estudo Acústico",
                "layout = grid",
                "columns = 3",
                "width = 420",
                "slider Largura | min=2 | max=20 | step=0.5 | value=8 | unit=m | key=[GEO] Largura",
                "switch Mostrar malha | value=sim",
                "btn Recalcular",
                "select Material | options=Concreto;Madeira;Vidro | value=madeira",
                "value T60 | key=[ACU] T60 | unit=s | decimals=2 | min=0.6 | max=1.2",
                "bar Otimização | key=[OPT] Progresso",
                "sparkline Fitness | key=[OPT] Fitness | span=3 | height=80",
                "text Status | value=pronto"
            });

            Assert.Empty(spec.Warnings);
            Assert.Equal("Estudo Acústico", spec.Title);
            Assert.Equal(LayoutKind.Grid, spec.Layout);
            Assert.Equal(3, spec.Columns);
            Assert.Equal(420f, spec.Width);
            Assert.Equal(new[] { WidgetKind.Slider, WidgetKind.Toggle, WidgetKind.Button, WidgetKind.Dropdown, WidgetKind.Number, WidgetKind.Progress, WidgetKind.MiniChart, WidgetKind.Label },
                spec.Widgets.Select(w => w.Kind).ToArray());

            var slider = spec.Widgets[0];
            Assert.Equal("geo_largura", slider.Id); // id vem da chave do Hub
            Assert.Equal(2, slider.Min);
            Assert.Equal(20, slider.Max);
            Assert.Equal(1, slider.Decimals); // casas do passo 0.5
            Assert.Equal(8, slider.Default.Number);

            Assert.True(spec.Widgets[1].Default.Boolean);
            Assert.Equal("Madeira", spec.Widgets[3].Default.Text); // opção canônica
            Assert.Equal(3, spec.Widgets[6].Span);
            Assert.Equal(80f, spec.Widgets[6].Height);
            Assert.Equal("button_recalcular", spec.Widgets[2].Id);
        }

        [Fact]
        public void Parser_ProblemsBecomeWarnings_NeverExceptions()
        {
            var spec = DashboardSpecParser.Parse(new[]
            {
                "gauge Pressão",                          // tipo desconhecido
                "slider A | min=10 | max=0 | value=50",    // faixa invertida + valor fora
                "dropdown Vazio",                          // sem opções
                "slider B | step=abc | commit=turbo",      // números/enums inválidos
                "slider A | min=0 | max=1",                // id repetido
                "layout = diagonal"
            });

            Assert.Equal(4, spec.Widgets.Count);
            Assert.Contains(spec.Warnings, w => w.Contains("gauge"));
            Assert.Contains(spec.Warnings, w => w.Contains("min > max"));
            Assert.Contains(spec.Warnings, w => w.Contains("fora da faixa"));
            Assert.Contains(spec.Warnings, w => w.Contains("options"));
            Assert.Contains(spec.Warnings, w => w.Contains("step"));
            Assert.Contains(spec.Warnings, w => w.Contains("commit"));
            Assert.Contains(spec.Warnings, w => w.Contains("repetido"));
            Assert.Contains(spec.Warnings, w => w.Contains("diagonal"));

            var a = spec.Widgets[0];
            Assert.Equal(0, a.Min);
            Assert.Equal(10, a.Max);
            Assert.Equal(10, a.Default.Number); // valor limitado à faixa
            Assert.Equal("slider_a_2", spec.Widgets[3].Id);
        }

        [Fact]
        public void SpecLine_RoundTrips_IncludingQuotesAndPipes()
        {
            var original = DashboardSpecParser.Parse(new[]
            {
                "slider \"Raio | externo\" | min=0 | max=5 | value=1.25 | unit=m",
                "dropdown Modo | options=A;B;\"C|D\" | output=index",
                "label \"  espaços  \" | lines=2 | style=title",
                "chart Série | value=1;2;3.5;-4"
            });
            var lines = original.ToLines();
            var again = DashboardSpecParser.Parse(lines);

            Assert.Empty(again.Warnings);
            Assert.Equal(original.Widgets.Count, again.Widgets.Count);
            for (int i = 0; i < original.Widgets.Count; i++)
            {
                Assert.Equal(original.Widgets[i].ToSpecLine(), again.Widgets[i].ToSpecLine());
                Assert.Equal(original.Widgets[i].Label, again.Widgets[i].Label);
                Assert.Equal(original.Widgets[i].Id, again.Widgets[i].Id);
                Assert.Equal(original.Widgets[i].Default, again.Widgets[i].Default);
            }
            Assert.Equal("Raio | externo", again.Widgets[0].Label);
            Assert.Equal("espaços", again.Widgets[2].Label); // rótulos são aparados
            Assert.Equal(original.ComputeHash(), again.ComputeHash());
            Assert.Equal(new[] { 1, 2, 3.5, -4 }, again.Widgets[3].Default.Series.ToArray());
        }

        [Theory]
        [InlineData(0, 10, 0.5, -1, 3.26, 3.5)]      // passo a partir do mínimo
        [InlineData(1, 10, 2, -1, 4.1, 5)]           // passo relativo ao mínimo (1, 3, 5...)
        [InlineData(0, 1, 0, 2, 0.12345, 0.12)]      // casas decimais
        [InlineData(0, 10, 0, 2, 99, 10)]            // limita à faixa
        [InlineData(-5, 5, 0, 1, -0.04, 0)]          // sem "-0"
        [InlineData(0, 10, 3, -1, 10, 9)]            // último passo que cabe na faixa
        public void Snap_ClampsStepsAndRounds(double min, double max, double step, int decimals, double input, double expected)
        {
            var spec = WidgetSpec.Create(WidgetKind.Slider, "s", new[]
            {
                KV("min", min), KV("max", max), KV("step", step), KV("decimals", decimals)
            });
            double r = WidgetValueRules.Snap(spec, input);
            Assert.Equal(expected, r, 10);
            Assert.False(double.IsNegative(r) && r == 0);
        }

        [Fact]
        public void Coerce_FollowsWidgetRules()
        {
            var dd = WidgetSpec.Create(WidgetKind.Dropdown, "d", new[] { new KeyValuePair<string, string>("options", "Baixo, Médio, Alto") });
            Assert.Equal("Médio", WidgetValueRules.Coerce(dd, WidgetValue.FromText("médio")).Text);
            Assert.Equal("Alto", WidgetValueRules.Coerce(dd, WidgetValue.FromNumber(2)).Text);
            Assert.Equal("Baixo", WidgetValueRules.Coerce(dd, WidgetValue.FromText("0")).Text);
            Assert.True(WidgetValueRules.Coerce(dd, WidgetValue.FromText("Extremo")).IsNone);
            Assert.True(WidgetValueRules.Coerce(dd, WidgetValue.FromNumber(7)).IsNone);

            var tg = WidgetSpec.Create(WidgetKind.Toggle, "t", null);
            Assert.True(WidgetValueRules.Coerce(tg, WidgetValue.FromText("sim")).Boolean);
            Assert.False(WidgetValueRules.Coerce(tg, WidgetValue.FromText("0")).Boolean);
            Assert.True(WidgetValueRules.Coerce(tg, WidgetValue.FromText("talvez")).IsNone);

            var sl = WidgetSpec.Create(WidgetKind.Slider, "s", new[] { KV("min", 0), KV("max", 100) });
            Assert.Equal(2.5, WidgetValueRules.Coerce(sl, WidgetValue.FromText("2,5")).Number); // vírgula decimal
            Assert.True(WidgetValueRules.Coerce(sl, WidgetValue.FromText("abc")).IsNone);
            Assert.True(WidgetValueRules.Coerce(sl, WidgetValue.FromNumber(double.PositiveInfinity)).IsNone);
            Assert.Equal(0.25, WidgetValueRules.Ratio(sl, 25), 12);
            Assert.Equal(100, WidgetValueRules.FromRatio(sl, 7)); // razão limitada a 0..1
        }

        [Fact]
        public void Builder_UsesLongestListForKindAndSettings_AndValuesPerWidget()
        {
            var warnings = new List<string>();
            var widgets = DashboardBuilder.Build(
                new[] { "slider", "slider", "number", "gauge" },
                new[] { "A", "B", "T60" },
                new[] { "min=0 | max=10" },
                new[] { WidgetValue.FromNumber(4), WidgetValue.None, WidgetValue.FromNumber(1.2), WidgetValue.FromNumber(9) },
                warnings);

            Assert.Equal(3, widgets.Count); // "gauge" desconhecido
            Assert.Contains(warnings, w => w.Contains("gauge"));
            Assert.All(widgets.Take(2), w => Assert.Equal(10, w.Max)); // configuração repetida (lista mais longa)
            Assert.Equal(4, widgets[0].Default.Number); // controle: valor vira padrão
            Assert.Equal(0, widgets[1].Default.Number);
            Assert.Equal(1.2, widgets[2].EmbeddedValue.Number); // indicador: valor embutido
            Assert.Equal(10, widgets[2].Max); // number também recebe min/max (faixa esperada)

            // Rótulo não se repete: o 4º item sem rótulo ficaria com id próprio
            var more = DashboardBuilder.Build(new[] { "toggle" }, new[] { "X" }, new[] { "", "", "" }, null, null);
            Assert.Equal(new[] { "X", "", "" }, more.Select(w => w.Label).ToArray());

            var single = DashboardBuilder.ValuesPerWidget(new List<IList<WidgetValue>> { new[] { N(1), N(2), N(3) } }, b => b[0], 3);
            Assert.Equal(new[] { 1.0, 2, 3 }, single.Select(v => v.Number).ToArray()); // lista simples: um por widget
            var perBranch = DashboardBuilder.ValuesPerWidget(new List<IList<WidgetValue>> { new[] { N(1), N(2) }, new[] { N(5) } }, b => WidgetValue.FromNumber(b.Count), 2);
            Assert.Equal(new[] { 2.0, 1 }, perBranch.Select(v => v.Number).ToArray());
        }

        private static WidgetValue N(double v) => WidgetValue.FromNumber(v);

        internal static KeyValuePair<string, string> KV(string k, double v) =>
            new KeyValuePair<string, string>(k, v.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }

    public class DashboardStateTests
    {
        private static DashboardSpec Spec(params string[] lines) => DashboardSpecParser.Parse(lines);

        [Fact]
        public void State_IsSeparateFromConfiguration_AndSurvivesSpecEdits()
        {
            var spec = Spec("slider Largura | id=w | min=0 | max=10 | value=2", "toggle Ativo | id=on", "dropdown Modo | id=m | options=A;B;C", "button Ir | id=go", "number T60 | id=t60");
            var state = new DashboardState();
            Assert.Equal(2, state.Effective(spec.Find("w")).Number); // padrão da configuração

            Assert.True(state.Set(spec.Find("w"), WidgetValue.FromNumber(7.123)));
            Assert.False(state.Set(spec.Find("w"), WidgetValue.FromNumber(7.12))); // mesmo valor depois de arredondar
            Assert.True(state.Set(spec.Find("on"), WidgetValue.FromBoolean(true)));
            Assert.True(state.Set(spec.Find("m"), WidgetValue.FromText("c")));
            Assert.False(state.Set(spec.Find("t60"), WidgetValue.FromNumber(1))); // indicador não tem estado

            Assert.Equal(new[] { "w=7.12", "on=true", "m=C" }, state.ToLines(spec).ToArray()); // botão não é persistente

            // A configuração muda (faixa menor, opção removida): o estado é reinterpretado sem perder o que ainda vale
            var edited = Spec("slider Largura | id=w | min=0 | max=5", "toggle Ativo | id=on", "dropdown Modo | id=m | options=A;B");
            Assert.Equal(5, state.Effective(edited.Find("w")).Number);
            Assert.True(state.Effective(edited.Find("on")).Boolean);
            Assert.Equal("A", state.Effective(edited.Find("m")).Text); // opção sumiu → padrão

            // Widget removido e recolocado: o valor órfão volta
            var without = Spec("toggle Ativo | id=on");
            Assert.Equal(new[] { "on=true" }, state.ToLines(without).ToArray());
            Assert.Equal(7.12, state.Effective(spec.Find("w")).Number);
        }

        [Fact]
        public void State_ApplyFromPreset_ValidatesAndReportsChanges()
        {
            var spec = Spec("slider X | id=x | min=0 | max=10", "toggle T | id=t", "dropdown D | id=d | options=Um;Dois", "number N | id=n");
            var state = new DashboardState();
            var warnings = new List<string>();
            var changed = state.Apply(new[] { "x=3.5\nt=true", "d=dois", "d=Três", "n=5", "zz=1", "x=abc", "# comentário" }, spec, warnings);

            Assert.Equal(new[] { "x", "t", "d" }, changed.ToArray());
            Assert.Equal(3.5, state.Effective(spec.Find("x")).Number);
            Assert.Equal("Dois", state.Effective(spec.Find("d")).Text);
            Assert.Equal(4, warnings.Count); // Três, n (indicador), zz, abc

            // Reaplicar o mesmo preset não muda nada
            Assert.Empty(state.Apply(new[] { "x=3.5", "t=true", "d=Dois" }, spec));
        }

        [Fact]
        public void State_RawLines_RoundTrip_WithTypes()
        {
            var spec = Spec("slider X | id=x | min=-100 | max=100 | decimals=3", "toggle T | id=t", "dropdown D | id=d | options=1;2;3");
            var a = new DashboardState();
            a.Set(spec.Find("x"), WidgetValue.FromNumber(-42.125));
            a.Set(spec.Find("t"), WidgetValue.FromBoolean(true));
            a.Set(spec.Find("d"), WidgetValue.FromText("3"));

            var b = new DashboardState();
            b.LoadRaw(a.ToRawLines());
            Assert.Equal(a.ToRawLines(), b.ToRawLines());
            Assert.Equal(-42.125, b.Effective(spec.Find("x")).Number);
            Assert.True(b.Effective(spec.Find("t")).Boolean);
            Assert.Equal("3", b.Effective(spec.Find("d")).Text); // opção "3" (texto), não índice 3
        }

        [Fact]
        public void State_ResetToDefaults_OnlyTouchesCurrentControls()
        {
            var spec = Spec("slider X | id=x | value=0.5", "toggle T | id=t");
            var s = new DashboardState();
            s.Set(spec.Find("x"), WidgetValue.FromNumber(0.9));
            s.LoadRaw(s.ToRawLines().Concat(new[] { "orfao=n:4" }));
            Assert.Equal(1, s.ResetToDefaults(spec));
            Assert.Equal(0.5, s.Effective(spec.Find("x")).Number);
            Assert.True(s.TryGetRaw("orfao", out _));
        }
    }

    public class DashboardLayoutTests
    {
        private static LayoutItem Item(float h, float min = 60, int span = 1, WidgetAlign align = WidgetAlign.Stretch, float natural = 0) =>
            new LayoutItem { PreferredHeight = h, MinWidth = min, Span = span, Align = align, NaturalWidth = natural };

        private static void AssertSane(LayoutResult r, int count)
        {
            Assert.Equal(count, r.Items.Length);
            for (int i = 0; i < r.Items.Length; i++)
            {
                var a = r.Items[i];
                Assert.True(a.Left >= 0 && a.Top >= 0 && a.Right <= r.Size.Width + 0.01f && a.Bottom <= r.Size.Height + 0.01f, $"item {i} fora do painel: {a}");
                for (int j = i + 1; j < r.Items.Length; j++)
                {
                    var b = r.Items[j];
                    Assert.False(a.IntersectsWith(b) && RectangleF.Intersect(a, b).Width > 0.01f && RectangleF.Intersect(a, b).Height > 0.01f, $"itens {i} e {j} se sobrepõem");
                }
            }
        }

        [Fact]
        public void Stack_PlacesItemsTopToBottom_WithPaddingAndSpacing()
        {
            var o = new LayoutOptions { Kind = LayoutKind.Stack, Width = 300, Padding = 8, Spacing = 6, HeaderHeight = 26 };
            var r = DashboardLayoutEngine.Arrange(o, new[] { Item(20), Item(34), Item(64) });
            AssertSane(r, 3);
            Assert.Equal(new RectangleF(8, 34, 284, 20), r.Items[0]);
            Assert.Equal(34 + 20 + 6, r.Items[1].Y);
            Assert.Equal(r.Items[2].Bottom + 8, r.Size.Height);
            Assert.Equal(300, r.Size.Width);
        }

        [Fact]
        public void Row_DistributesBySpan_AndGrowsForMinimums()
        {
            var o = new LayoutOptions { Kind = LayoutKind.Row, Width = 300, Padding = 8, Spacing = 6 };
            var r = DashboardLayoutEngine.Arrange(o, new[] { Item(20, 40, 1), Item(40, 40, 2), Item(30, 40, 1) });
            AssertSane(r, 3);
            Assert.InRange(r.Items[1].Width / r.Items[0].Width, 1.9f, 2.1f);
            Assert.All(r.Items, it => Assert.Equal(40, it.Height)); // altura da linha

            // Mínimos maiores que a largura: o painel cresce em vez de espremer
            var big = DashboardLayoutEngine.Arrange(o, Enumerable.Repeat(Item(20, 120), 4).ToList());
            AssertSane(big, 4);
            Assert.True(big.Size.Width >= 4 * 120 + 3 * 6 + 16);
            Assert.All(big.Items, it => Assert.True(it.Width >= 119));

            // Um item estreito com mínimo alto fica no mínimo; os outros dividem o resto
            var mixed = DashboardLayoutEngine.Arrange(new LayoutOptions { Kind = LayoutKind.Row, Width = 400, Padding = 0, Spacing = 0 }, new[] { Item(10, 300, 1), Item(10, 10, 1), Item(10, 10, 1) });
            Assert.Equal(300, mixed.Items[0].Width, 0);
            Assert.Equal(50, mixed.Items[1].Width, 0);
        }

        [Fact]
        public void Grid_WrapsBySpan_AndUsesTallestItemPerRow()
        {
            var o = new LayoutOptions { Kind = LayoutKind.Grid, Columns = 3, Width = 320, Padding = 8, Spacing = 6 };
            var r = DashboardLayoutEngine.Arrange(o, new[] { Item(30), Item(50), Item(20, span: 2), Item(20, span: 9), Item(22) });
            AssertSane(r, 5);
            Assert.Equal(4, r.Rows);
            Assert.Equal(r.Items[0].Y, r.Items[1].Y);
            Assert.Equal(50, r.Items[0].Height); // linha tão alta quanto o maior
            Assert.True(r.Items[2].Y > r.Items[1].Y); // span 2 não cabe no resto da linha
            Assert.Equal(r.Content.Width, r.Items[3].Width, 0); // span maior que colunas → linha inteira
            Assert.True(Math.Abs(r.Items[0].Width * 2 + 6 - r.Items[2].Width) <= 1f); // bordas arredondadas: ±1
        }

        [Fact]
        public void Align_UsesNaturalWidthInsideCell_AndEmptyPanelHasPlaceholderArea()
        {
            var o = new LayoutOptions { Kind = LayoutKind.Stack, Width = 300, Padding = 10, Spacing = 4 };
            var r = DashboardLayoutEngine.Arrange(o, new[] { Item(24, 70, 1, WidgetAlign.Center, 96), Item(24, 70, 1, WidgetAlign.Right, 96), Item(24, 70, 1, WidgetAlign.Left, 96) });
            Assert.Equal(96, r.Items[0].Width, 0);
            Assert.Equal(150, r.Items[0].X + r.Items[0].Width / 2, 0);
            Assert.Equal(290, r.Items[1].Right, 0);
            Assert.Equal(10, r.Items[2].X, 0);

            var empty = DashboardLayoutEngine.Arrange(o, new LayoutItem[0]);
            Assert.Empty(empty.Items);
            Assert.True(empty.Content.Height > 0 && empty.Size.Height > o.HeaderHeight);
        }

        [Theory]
        [InlineData(0)]   // painel vazio (o caso mais baixo)
        [InlineData(1)]
        [InlineData(12)]
        public void ParamBand_KeepsEveryGripInsideThePanel_AndWidgetsBelowIt(int widgets)
        {
            var m = DashboardMetrics.Default;
            int inputs = 3, outputs = 5;
            float top = m.ContentTop(inputs, outputs);
            Assert.Equal(m.HeaderHeight + m.ParamBandHeight(inputs, outputs), top);
            Assert.True(m.ParamRowCenterY(outputs - 1) + m.ParamRowHeight / 2f <= top); // última linha dentro da faixa

            var items = Enumerable.Range(0, widgets).Select(_ => Item(20)).ToList();
            var r = DashboardLayoutEngine.Arrange(new LayoutOptions { Kind = LayoutKind.Stack, Width = 300, HeaderHeight = top }, items);
            Assert.True(r.Size.Height > top); // o painel sempre contém a faixa inteira (todos os grips)
            Assert.All(r.Items, it => Assert.True(it.Top >= top)); // widgets começam abaixo dos nomes
            Assert.Equal(0f, m.ParamBandHeight(0, 0));
        }

        [Fact]
        public void ManyWidgets_StayOrderedAndSane()
        {
            var items = Enumerable.Range(0, 250).Select(i => Item(18 + i % 5 * 8, 60 + i % 3 * 30, 1 + i % 3)).ToList();
            foreach (var kind in new[] { LayoutKind.Stack, LayoutKind.Grid })
            {
                var r = DashboardLayoutEngine.Arrange(new LayoutOptions { Kind = kind, Columns = 4, Width = 500 }, items);
                AssertSane(r, items.Count);
                for (int i = 1; i < items.Count; i++)
                {
                    Assert.True(r.Items[i].Y > r.Items[i - 1].Y || (r.Items[i].Y == r.Items[i - 1].Y && r.Items[i].X > r.Items[i - 1].X), $"ordem quebrada em {i}");
                }
            }
        }
    }

    public class DashboardFormatTests
    {
        // Medidor falso: 6 unidades por caractere (monoespaçado), "…" também conta 6
        private static float Mono(string s) => s.Length * 6f;

        [Theory]
        [InlineData(1234567.891, 2, "m", "1 234 567.89 m")]
        [InlineData(-9876.5, 1, null, "-9876.5")]
        [InlineData(-0.0001, 2, null, "0.00")]
        [InlineData(0.5, -1, "%", "0.5%")]
        [InlineData(12.3456789, -1, "s", "12.346 s")]
        [InlineData(1e18, 2, null, "1e18")]
        [InlineData(-3.2e-7, -1, null, "-3.2e-7")]
        [InlineData(double.NaN, 2, "m", "—")]
        [InlineData(double.NegativeInfinity, 0, "Pa", "-∞ Pa")]
        public void Number_HandlesLargeNegativeAndSpecialValues(double v, int decimals, string unit, string expected)
        {
            Assert.Equal(expected, DashboardFormat.Number(v, decimals, unit));
        }

        [Theory]
        [InlineData(999.0, "999")]
        [InlineData(1234.0, "1.23 k")]
        [InlineData(-45678901.0, "-45.7 M")]
        [InlineData(0.00012, "1.2e-4")]
        [InlineData(7.5e12, "7.5 T")]
        [InlineData(3.2e21, "3.2e21")]
        public void Compact_UsesSiPrefixes(double v, string expected)
        {
            Assert.Equal(expected, DashboardFormat.Compact(v));
        }

        [Fact]
        public void FitNumber_ShrinksFormatProgressively_AndNeverOverflows()
        {
            double big = -123456789.123;
            string wide = DashboardFormat.FitNumber(big, 3, "Pa", 500, Mono);
            Assert.Equal(DashboardFormat.Number(big, 3, "Pa"), wide);

            foreach (float width in new[] { 120f, 72f, 48f, 30f, 12f, 6f, 2f })
            {
                string s = DashboardFormat.FitNumber(big, 3, "Pa", width, Mono);
                Assert.True(Mono(s) <= width || s == "", $"'{s}' não cabe em {width}");
            }
            Assert.Equal("-123 M Pa", DashboardFormat.FitNumber(big, 3, "Pa", 60, Mono));
        }

        [Fact]
        public void Ellipsize_KeepsPrefixAndFits()
        {
            string longText = "Um rótulo bastante comprido para o espaço disponível";
            string r = DashboardFormat.Ellipsize(longText, 60, Mono);
            Assert.EndsWith("…", r);
            Assert.True(Mono(r) <= 60);
            Assert.StartsWith(r.TrimEnd('…'), longText);
            Assert.Equal("curto", DashboardFormat.Ellipsize("curto", 60, Mono));
            Assert.Equal("", DashboardFormat.Ellipsize("abc", 3, Mono));
        }

        [Fact]
        public void Decimate_KeepsPeaksOrderAndBound()
        {
            var rnd = new Random(5);
            var values = Enumerable.Range(0, 100000).Select(i => Math.Sin(i / 500.0) + rnd.NextDouble() * 0.01).ToList();
            values[31337] = 50;      // pico isolado
            values[77777] = -60;     // vale isolado
            values[500] = double.NaN;
            values[501] = double.PositiveInfinity;

            var pts = SeriesSampling.MinMaxDecimate(values, 120);
            Assert.True(pts.Count <= 240);
            Assert.Contains(pts, p => p.Key == 31337 && p.Value == 50);
            Assert.Contains(pts, p => p.Key == 77777 && p.Value == -60);
            Assert.DoesNotContain(pts, p => double.IsNaN(p.Value) || double.IsInfinity(p.Value));
            for (int i = 1; i < pts.Count; i++) Assert.True(pts[i].Key > pts[i - 1].Key);

            SeriesSampling.Range(values, out double min, out double max);
            Assert.Equal(-60, min);
            Assert.Equal(50, max);
            Assert.Empty(SeriesSampling.MinMaxDecimate(new double[0], 10));
            Assert.Single(SeriesSampling.MinMaxDecimate(new[] { double.NaN, 3.0 }, 10));
        }
    }

    public class DashboardInteractionTests
    {
        private readonly ITestOutputHelper _output;

        public DashboardInteractionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static WidgetValue N(double v) => WidgetValue.FromNumber(v);

        [Fact]
        public void Gate_Release_OnlyPreviewsWhileDragging_CommitsOnce()
        {
            var g = new CommitGate(CommitMode.Release, 80);
            g.Begin(N(0));
            int commits = 0;
            for (int i = 1; i <= 200; i++)
            {
                var r = g.Move(N(i), i * 5);
                Assert.NotEqual(GateDecision.Schedule, r.Decision);
                if (r.Decision == GateDecision.Commit) commits++;
            }
            Assert.Equal(0, commits);
            Assert.True(g.HasPending);
            Assert.Equal(GateDecision.Commit, g.End(N(200), 1001).Decision);
            Assert.Equal(GateDecision.None, g.End(N(200), 1002).Decision); // mesmo valor: nada
        }

        [Fact]
        public void Gate_Live_ThrottlesAndKeepsTheLastValue()
        {
            var g = new CommitGate(CommitMode.Live, 80);
            g.Begin(N(0));
            int commits = 0, schedules = 0;
            double pendingDue = -1;
            for (int i = 1; i <= 200; i++)
            {
                double now = i * 5; // 200 eventos em 1 s
                if (pendingDue >= 0 && now >= pendingDue)
                {
                    if (g.TrailingDue(pendingDue).Decision == GateDecision.Commit) commits++;
                    pendingDue = -1;
                }
                var r = g.Move(N(i), now);
                if (r.Decision == GateDecision.Commit) commits++;
                if (r.Decision == GateDecision.Schedule)
                {
                    schedules++;
                    Assert.True(pendingDue < 0, "no máximo uma entrega agendada por vez");
                    pendingDue = now + r.DelayMs;
                }
            }
            var end = g.End(N(200), 1000);
            if (end.Decision == GateDecision.Commit) commits++;
            _output.WriteLine($"Live 80 ms: 200 eventos → {commits} soluções ({schedules} agendamentos)");
            Assert.InRange(commits, 10, 14); // ~1 s / 80 ms
            Assert.Equal(N(200), g.Committed);
        }

        [Fact]
        public void Gate_UnchangedQuantizedValues_NeverCommit()
        {
            var g = new CommitGate(CommitMode.Live, 0);
            g.Begin(N(5));
            for (int i = 0; i < 100; i++) Assert.Equal(GateDecision.Preview, g.Move(N(5), i).Decision);
            Assert.Equal(GateDecision.None, g.End(N(5), 100).Decision);
            Assert.Equal(0, g.Commits);
        }

        [Fact]
        public void Gate_Auto_SwitchesToReleaseWhenSolutionsAreHeavy_AndThrottleAdapts()
        {
            var light = new CommitGate(CommitMode.Auto, 50) { LastSolutionMs = 10 };
            Assert.True(light.IsLive);
            Assert.Equal(50, light.EffectiveThrottleMs);

            var medium = new CommitGate(CommitMode.Auto, 50) { LastSolutionMs = 120 };
            Assert.True(medium.IsLive);
            Assert.Equal(240, medium.EffectiveThrottleMs); // ≥ 2 × duração da solução

            var heavy = new CommitGate(CommitMode.Auto, 50) { LastSolutionMs = 900 };
            heavy.Begin(N(0));
            Assert.False(heavy.IsLive);
            Assert.Equal(GateDecision.Preview, heavy.Move(N(1), 0).Decision);
            Assert.Equal(GateDecision.Preview, heavy.Move(N(2), 500).Decision);
            Assert.Equal(GateDecision.Commit, heavy.End(N(2), 600).Decision);
        }

        // ---------------------------------------------------------------
        // Controlador ponta a ponta (sem Grasshopper)
        // ---------------------------------------------------------------

        private sealed class FakeHub : IDashboardLiveSource
        {
            public readonly Dictionary<string, WidgetValue> Channels = new Dictionary<string, WidgetValue>(StringComparer.OrdinalIgnoreCase);

            public bool TryGet(string key, out WidgetValue value, out string status)
            {
                status = null;
                if (Channels.TryGetValue(key, out value)) return true;
                status = "canal ausente";
                return false;
            }
        }

        private static DashboardController Controller(params string[] lines)
        {
            var c = new DashboardController();
            c.SetSpec(DashboardSpecParser.Parse(lines));
            c.Arrange(26);
            return c;
        }

        private static PointF TrackPoint(DashboardController c, string id, double ratio)
        {
            int i = c.IndexOf(id);
            var w = (SliderWidget)c.Visible[i];
            var track = w.TrackRect(c.RectOf(i), c.Metrics);
            return new PointF(track.X + (float)(track.Width * ratio), track.Y + track.Height / 2f);
        }

        private static PointF Center(DashboardController c, string id)
        {
            var r = c.RectOf(c.IndexOf(id));
            return new PointF(r.X + r.Width / 2f, r.Bottom - 6f);
        }

        [Fact]
        public void Drag_Release_OneSolutionPerGesture_UndoRecordedOnce()
        {
            var c = Controller("slider Raio | id=r | min=0 | max=100 | step=1 | commit=release");
            int undo = 0;
            c.BeforeChange = _ => undo++;
            int commits = 0;

            var down = c.PointerDown(TrackPoint(c, "r", 0.1), 0);
            Assert.True(down.Has(DashboardAction.Capture) && down.Has(DashboardAction.Handled));
            if (down.Has(DashboardAction.Commit)) commits++;
            for (int i = 1; i <= 300; i++)
            {
                var e = c.PointerMove(TrackPoint(c, "r", 0.1 + i * 0.002), i * 3);
                if (e.Has(DashboardAction.Commit)) commits++;
            }
            var displayed = c.DisplayValue(c.Visible[0], out bool pending, out _);
            Assert.True(pending);
            Assert.Equal(0, c.State.Effective(c.Spec.Find("r")).Number); // ainda não entregue

            var up = c.PointerUp(TrackPoint(c, "r", 0.7), 1000);
            if (up.Has(DashboardAction.Commit)) commits++;
            Assert.True(up.Has(DashboardAction.Release));
            Assert.Equal(1, commits);
            Assert.Equal(1, undo);
            Assert.Equal(70, c.State.Effective(c.Spec.Find("r")).Number);
            Assert.Equal("r", c.LastChangedId);
            Assert.False(c.IsCapturing);
        }

        [Fact]
        public void Drag_Auto_BecomesReleaseAfterAHeavySolution()
        {
            var c = Controller("slider X | id=x | min=0 | max=1000 | step=1");
            var effects = new List<DashboardEffect>();
            effects.Add(c.PointerDown(TrackPoint(c, "x", 0.05), 0));
            Assert.True(effects[0].Has(DashboardAction.Commit)); // primeiro valor vai na hora (Live enquanto não se sabe o custo)
            c.ReportSolution(800); // o host mede: a solução levou 800 ms

            for (int i = 1; i <= 100; i++) effects.Add(c.PointerMove(TrackPoint(c, "x", 0.05 + i * 0.009), 800 + i * 10));
            effects.Add(c.PointerUp(TrackPoint(c, "x", 0.95), 2000));
            Assert.Equal(2, effects.Count(e => e.Has(DashboardAction.Commit)));
            Assert.Equal(950, c.State.Effective(c.Spec.Find("x")).Number);
        }

        [Fact]
        public void Drag_Live_SchedulesTrailingCommit_ThatDeliversTheLastValue()
        {
            var c = Controller("slider X | id=x | min=0 | max=100 | step=1 | commit=live | throttle=100");
            c.PointerDown(TrackPoint(c, "x", 0.1), 0);
            var e1 = c.PointerMove(TrackPoint(c, "x", 0.2), 10);
            Assert.True(e1.Has(DashboardAction.ScheduleTrailing));
            Assert.InRange(e1.DelayMs, 80, 100);
            var e2 = c.PointerMove(TrackPoint(c, "x", 0.3), 20);
            Assert.False(e2.Has(DashboardAction.ScheduleTrailing) || e2.Has(DashboardAction.Commit));

            var due = c.TrailingDue(100);
            Assert.True(due.Has(DashboardAction.Commit));
            Assert.Equal(30, c.State.Effective(c.Spec.Find("x")).Number);

            var up = c.PointerUp(TrackPoint(c, "x", 0.3), 150);
            Assert.False(up.Has(DashboardAction.Commit)); // valor final já entregue
            Assert.False(c.TrailingDue(300).Has(DashboardAction.Commit)); // agendamento tardio depois do fim: nada
        }

        [Fact]
        public void Toggle_Button_Dropdown_Behave()
        {
            var c = Controller("toggle Mostrar | id=t", "button Rodar | id=b", "dropdown Modo | id=m | options=A;B;C", "label Nota");
            int undo = 0;
            c.BeforeChange = _ => undo++;

            var t1 = c.PointerDown(Center(c, "t"), 0);
            Assert.True(t1.Has(DashboardAction.Commit));
            Assert.True(c.State.Effective(c.Spec.Find("t")).Boolean);
            var t2 = c.PointerDown(Center(c, "t"), 120); // segundo clique de duplo clique: ignorado
            Assert.False(t2.Has(DashboardAction.Commit));
            Assert.True(t2.Has(DashboardAction.Handled));
            var t3 = c.PointerDown(Center(c, "t"), 1000);
            Assert.True(t3.Has(DashboardAction.Commit));
            Assert.False(c.State.Effective(c.Spec.Find("t")).Boolean);

            var b1 = c.PointerDown(Center(c, "b"), 2000);
            Assert.True(b1.Has(DashboardAction.Commit) && b1.Has(DashboardAction.Capture));
            Assert.True(c.DisplayValue(c.Visible[c.IndexOf("b")], out _, out _).Boolean);
            var b2 = c.PointerUp(Center(c, "b"), 2100);
            Assert.True(b2.Has(DashboardAction.Commit) && b2.Has(DashboardAction.Release));
            Assert.False(c.State.Effective(c.Spec.Find("b")).Boolean);

            var r = c.RectOf(c.IndexOf("m"));
            var open = c.PointerDown(new PointF(r.X + 20, r.Bottom - 6), 3000);
            Assert.True(open.Has(DashboardAction.OpenOptions));
            Assert.False(open.Has(DashboardAction.Commit));
            Assert.True(c.Choose("m", "C").Has(DashboardAction.Commit));
            Assert.False(c.Choose("m", "C").Has(DashboardAction.Commit)); // mesma opção
            Assert.False(c.Choose("m", "Z").Has(DashboardAction.Commit)); // opção inexistente

            // Clique num indicador não é consumido (o componente pode ser arrastado por ali)
            var label = c.PointerDown(Center(c, "label_nota"), 4000);
            Assert.False(label.Has(DashboardAction.Handled));

            Assert.Equal(3, undo); // toggle ×2 e dropdown; botão não entra no undo
        }

        [Fact]
        public void DisabledAndHiddenWidgets_AreRespected_AndOrderIsApplied()
        {
            var c = Controller("slider A | id=a | order=3", "slider B | id=b | disabled", "slider C | id=c | hidden", "slider D | id=d | order=-1");
            Assert.Equal(new[] { "d", "b", "a" }, c.Visible.Select(w => w.Id).ToArray()); // order padrão = posição na definição
            Assert.Equal(4, c.Widgets.Count); // oculto continua produzindo valor
            Assert.Equal(DashboardEffect.Nothing, c.PointerDown(TrackPoint(c, "b", 0.5), 0));
        }

        [Fact]
        public void Indicators_ResolveHubThenDataThenBuilderThenDefault()
        {
            var hub = new FakeHub();
            var spec = DashboardSpecParser.Parse(new[] { "number T60 | key=[ACU] T60 | value=9", "number Area | value=3", "progress P | source=prog", "chart S | key=[OPT] Serie" },
                new[] { WidgetSpec.Create(WidgetKind.Number, "Embutido", null).WithEmbeddedValue(WidgetValue.FromNumber(42)) });
            var c = new DashboardController { LiveSource = hub };
            c.SetSpec(spec);

            string status;
            var t60 = c.Widgets[0];
            Assert.Equal(9, c.DisplayValue(t60, out _, out status).Number); // canal ausente → padrão
            Assert.Equal("canal ausente", status);
            hub.Channels["[ACU] T60"] = N(1.07);
            Assert.Equal(1.07, c.DisplayValue(t60, out _, out _).Number);

            c.SetNamedData(new Dictionary<string, WidgetValue> { ["area"] = N(120), ["prog"] = N(0.4) });
            Assert.Equal(120, c.DisplayValue(c.Widgets[1], out _, out _).Number); // casou pelo rótulo
            Assert.Equal(0.4, c.DisplayValue(c.Widgets[2], out _, out _).Number); // casou por source=
            Assert.Equal(42, c.DisplayValue(c.Widgets[4], out _, out _).Number); // valor do Builder
            Assert.True(c.DisplayValue(c.Widgets[3], out _, out status).IsNone);
        }

        [Fact]
        public void SpecChange_KeepsState_AndCancelsAGestureInProgress()
        {
            var c = Controller("slider X | id=x | min=0 | max=10 | commit=release");
            c.PointerDown(TrackPoint(c, "x", 0.2), 0);
            c.State.Set(c.Spec.Find("x"), N(4));
            Assert.True(c.IsCapturing);

            Assert.True(c.SetSpec(DashboardSpecParser.Parse(new[] { "slider X | id=x | min=0 | max=20 | commit=release", "toggle Novo" })));
            Assert.False(c.IsCapturing);
            Assert.Equal(4, c.State.Effective(c.Spec.Find("x")).Number);
            Assert.False(c.SetSpec(DashboardSpecParser.Parse(new[] { "slider X | id=x | min=0 | max=20 | commit=release", "toggle Novo" }))); // mesma configuração
        }

        [Fact]
        [Trait("Category", "Benchmark")]
        public void Controller_HandlesLargePanels_Fast()
        {
            var lines = Enumerable.Range(0, 500).Select(i => i % 2 == 0 ? $"slider S{i} | min=0 | max=100" : $"number N{i} | value={i * 1.5}").ToArray();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var c = Controller(lines);
            double build = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            for (int i = 0; i < 1000; i++) c.PointerMove(new PointF(150, 30 + i % 400 * 10), i);
            double hover = sw.Elapsed.TotalMilliseconds / 1000;
            _output.WriteLine($"500 widgets: parse+layout {build:F1} ms; hit-test por evento {hover * 1000:F1} µs");
            Assert.True(build < 500);
            Assert.True(hover < 1);
        }
    }
}
