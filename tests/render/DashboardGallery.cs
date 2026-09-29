// Galeria de renderização do Pill Dashboard (teste visual, separado dos testes de dados).
// Desenha painéis reais com o mesmo renderizador do canvas: todos os widgets, textos longos, números grandes e
// negativos, dado ausente, painel vazio, layouts stack/row/grid, zoom baixo (sem texto), zoom 3x (DPI alto) e
// componente desativado; também mede o tempo de pintura.
//
// Linux (mono + libgdiplus), a partir de uma pasta com Glaux_Tools.gha copiado como Glaux_Tools.dll e Grasshopper.dll:
//   mcs -r:System.Drawing.dll -r:Glaux_Tools.dll -r:Grasshopper.dll DashboardGallery.cs && mono DashboardGallery.exe
// Windows (.NET Framework): csc /r:System.Drawing.dll /r:Glaux_Tools.dll /r:Grasshopper.dll DashboardGallery.cs
// Saída: gallery.png e zoom3.png na pasta atual.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Buraqueira_Tools.Dashboard;

class FakeHub : IDashboardLiveSource
{
    public Dictionary<string, WidgetValue> C = new Dictionary<string, WidgetValue>(StringComparer.OrdinalIgnoreCase);
    public bool TryGet(string key, out WidgetValue v, out string status)
    {
        status = null;
        if (C.TryGetValue(key, out v)) return true;
        status = "canal ausente";
        return false;
    }
}

class DashboardGallery
{
    static DashboardController Make(FakeHub hub, params string[] lines)
    {
        var c = new DashboardController { LiveSource = hub };
        c.SetSpec(DashboardSpecParser.Parse(lines));
        c.Arrange(DashboardMetrics.Default.ContentTop(3, 5)); // cabeçalho + faixa de parâmetros (3 entradas, 5 saídas)
        return c;
    }

    static void Draw(Graphics g, DashboardController c, float x, float y, float zoom, string title, bool locked = false, int msg = 0, bool selected = false)
    {
        var state = g.Save();
        g.TranslateTransform(x, y);
        g.ScaleTransform(zoom, zoom);
        var ctx = new DashboardRenderContext(g, DashboardTheme.Default, zoom);
        var chrome = new DashboardChrome { Title = title, Badge = c.Widgets.Count + " widgets", Locked = locked, MessageLevel = msg, Selected = selected,
            InputLabels = new[] { "Widgets", "Data", "Load State" },
            OutputLabels = new[] { "Values", "Names", "State", "Changed", "Info" },
            AccentOf = s => s.HubKey != null && s.HubKey.StartsWith("[ACU]") ? Color.FromArgb(0, 180, 216) : s.HubKey != null && s.HubKey.StartsWith("[GEO]") ? Color.FromArgb(46, 175, 100) : Color.Empty };
        DashboardRenderer.Render(ctx, c, new RectangleF(0, 0, c.Layout.Size.Width, c.Layout.Size.Height), chrome);
        g.Restore(state);
    }

    static void Main(string[] args)
    {
        var hub = new FakeHub();
        hub.C["[ACU] T60"] = WidgetValue.FromNumber(1.0734);
        hub.C["[OPT] Progresso"] = WidgetValue.FromNumber(0.62);
        var series = new List<double>();
        var rnd = new Random(2);
        for (int i = 0; i < 5000; i++) series.Add(Math.Exp(-i / 1500.0) * 3 + rnd.NextDouble() * 0.2 + (i == 3000 ? 2 : 0));
        hub.C["[OPT] Fitness"] = WidgetValue.FromSeries(series);

        var full = Make(hub,
            "title = Estudo Acústico — Sala 2",
            "layout = grid", "columns = 2", "width = 380",
            "slider Largura | min=4 | max=20 | step=0.5 | value=8 | unit=m | key=[GEO] Largura",
            "slider Absorção média | min=0.02 | max=0.95 | value=0.25",
            "toggle Mostrar raios | value=true",
            "toggle Modo rápido",
            "dropdown Material do forro | options=Gesso;Madeira ripada;Lã mineral | value=Madeira ripada | span=2",
            "number T60 | key=[ACU] T60 | unit=s | decimals=2 | min=0.6 | max=1.2",
            "number Custo estimado | value=-1234567.891 | unit=R$ | decimals=2",
            "progress Otimização | key=[OPT] Progresso",
            "button Recalcular | align=center",
            "chart Fitness por geração | key=[OPT] Fitness | span=2",
            "label Status | value=Aguardando simulação acústica com 10 000 raios e 3 ordens de reflexão | span=2");
        // Estados visuais: arrasto em andamento com valor pendente, hover
        var track = ((SliderWidget)full.Visible[0]).TrackRect(full.RectOf(0), full.Metrics);
        full.PointerDown(new PointF(track.X + track.Width * 0.2f, track.Y + 6), 0);
        full.ReportSolution(900);
        full.PointerMove(new PointF(track.X + track.Width * 0.75f, track.Y + 6), 50);

        var stack = Make(hub,
            "title = Um título de painel extremamente longo que não cabe no cabeçalho de jeito nenhum",
            "width = 230",
            "label \"Seção: Geometria\" | style=title",
            "slider Um rótulo muito longo para um slider estreito | min=-1000000 | max=1000000 | value=-987654.3 | decimals=1",
            "number Número gigante | value=1.23e18 | unit=Pa",
            "number Minúsculo | value=-0.000000321",
            "number Sem dado | key=[ACU] Inexistente",
            "dropdown Sem opções",
            "toggle Desativado | disabled | value=true",
            "chart Série vazia | key=[X] Nada",
            "chart Constante | value=5;5;5;5",
            "progress Negativo | value=-3 | min=-10 | max=10",
            "label Nota | value=Texto longo em várias linhas para testar a quebra por palavra e as reticências no final do bloco | lines=2");

        var row = Make(hub, "title = Linha", "layout = row", "width = 300", "button A", "button B | span=2", "toggle C", "number D | value=42");
        var empty = Make(hub, "title = Painel novo");

        // Posições calculadas pelas alturas reais (o painel cresce com a faixa de parâmetros e os widgets)
        const float S = 1.5f, gap = 30f;
        float rowY = 20 + Math.Max(full.Layout.Size.Height, stack.Layout.Size.Height) * S + gap;
        float emptyY = rowY + row.Layout.Size.Height * S + gap;
        int height = (int)(emptyY + Math.Max(empty.Layout.Size.Height * S, full.Layout.Size.Height * 0.4f) + 30);
        using (var bmp = new Bitmap(1300, height))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(212, 208, 200));
            Draw(g, full, 20, 20, S, "Estudo Acústico — Sala 2", selected: true);
            Draw(g, stack, 620, 20, S, stack.Spec.Title, msg: 1);
            Draw(g, row, 20, rowY, S, "Linha");
            Draw(g, row, 620, rowY, S, "Desativado", locked: true);
            Draw(g, empty, 20, emptyY, S, "Painel novo");
            Draw(g, full, 520, emptyY, 0.4f, "zoom baixo");
            bmp.Save("gallery.png", ImageFormat.Png);
        }
        using (var bmp = new Bitmap(1200, 1250))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(212, 208, 200));
            Draw(g, full, 10, 10, 3f, "Estudo Acústico — Sala 2");
            bmp.Save("zoom3.png", ImageFormat.Png);
        }
        using (var bmp = new Bitmap(800, 600))
        using (var g = Graphics.FromImage(bmp))
        {
            Draw(g, full, 0, 0, 1f, "aquecimento");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100; i++) Draw(g, full, 0, 0, 1f, "bench");
            double full1 = sw.Elapsed.TotalMilliseconds / 100;
            sw.Restart();
            for (int i = 0; i < 100; i++) Draw(g, full, 0, 0, 0.4f, "bench");
            double low = sw.Elapsed.TotalMilliseconds / 100;
            Console.WriteLine($"pintura do painel de 11 widgets: {full1:F2} ms (zoom 1), {low:F2} ms (zoom 0.4, sem texto)");
        }
        Console.WriteLine("ok " + full.Layout.Size + " " + stack.Layout.Size);
    }
}
