using System;
using System.Drawing;
using Buraqueira_Tools.Visual;

namespace Buraqueira_Tools.Dashboard
{
    /// <summary>Medidas do painel em unidades do canvas (independentes de fonte e de GDI+; usadas pelo layout e pelos testes).</summary>
    public sealed class DashboardMetrics
    {
        public float HeaderHeight { get; set; } = 26f;
        public float CaptionHeight { get; set; } = 13f;
        public float LineHeight { get; set; } = 13f;
        public float SliderHeight { get; set; } = 34f;
        public float ToggleHeight { get; set; } = 22f;
        public float ButtonHeight { get; set; } = 24f;
        public float DropdownHeight { get; set; } = 34f;
        public float NumberHeight { get; set; } = 38f;
        public float ProgressHeight { get; set; } = 28f;
        public float ChartHeight { get; set; } = 64f;
        public float FieldHeight { get; set; } = 18f;
        public float KnobMargin { get; set; } = 7f;

        /// <summary>Altura de cada linha da faixa de parâmetros (nomes das entradas/saídas alinhados aos grips).</summary>
        public float ParamRowHeight { get; set; } = 14f;

        public float ParamBandPadding { get; set; } = 3f;

        /// <summary>Altura da faixa de parâmetros abaixo do cabeçalho (0 sem parâmetros).</summary>
        public float ParamBandHeight(int inputs, int outputs)
        {
            int rows = Math.Max(inputs, outputs);
            return rows <= 0 ? 0f : 2 * ParamBandPadding + rows * ParamRowHeight;
        }

        /// <summary>Centro vertical (coordenada local do painel) da linha <paramref name="index"/> da faixa de parâmetros.</summary>
        public float ParamRowCenterY(int index) => HeaderHeight + ParamBandPadding + (index + 0.5f) * ParamRowHeight;

        /// <summary>Topo da área de widgets: cabeçalho + faixa de parâmetros.</summary>
        public float ContentTop(int inputs, int outputs) => HeaderHeight + ParamBandHeight(inputs, outputs);

        public static DashboardMetrics Default { get; } = new DashboardMetrics();
    }

    /// <summary>Aparência do painel: medidas, cores (do kit visual das Pills) e fontes em cache.</summary>
    public sealed class DashboardTheme
    {
        public DashboardMetrics Metrics { get; set; } = DashboardMetrics.Default;
        public Color Accent { get; set; } = PillVisualKit.Accent;

        public Font TitleFont => PillVisualKit.Font(8.5f, FontStyle.Bold);
        public Font CaptionFont => PillVisualKit.Font(7.2f, FontStyle.Bold);
        public Font TextFont => PillVisualKit.Font(7.4f);
        public Font HeadingFont => PillVisualKit.Font(9f, FontStyle.Bold);
        public Font ValueFont => PillVisualKit.Font(7.2f, FontStyle.Bold);
        public Font BigValueFont => PillVisualKit.Font(12.5f, FontStyle.Bold);
        public Font SmallFont => PillVisualKit.Font(6.3f);
        public Font ButtonFont => PillVisualKit.Font(7.2f, FontStyle.Bold);

        public static DashboardTheme Default { get; } = new DashboardTheme();
    }

    /// <summary>Tudo o que um widget precisa para se desenhar.</summary>
    public sealed class DashboardRenderContext
    {
        private static readonly StringFormat s_measureFormat = CreateMeasureFormat();

        public DashboardRenderContext(Graphics graphics, DashboardTheme theme, float zoom)
        {
            Graphics = graphics;
            Theme = theme ?? DashboardTheme.Default;
            Zoom = zoom <= 0 ? 1f : zoom;
        }

        public Graphics Graphics { get; }
        public DashboardTheme Theme { get; }

        /// <summary>Zoom do canvas: abaixo de ~0,45 o texto fica ilegível e o painel desenha só blocos (mais rápido).</summary>
        public float Zoom { get; }

        public bool LowDetail => Zoom < 0.45f;

        public float Measure(string text, Font font)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            return Graphics.MeasureString(text, font, PointF.Empty, s_measureFormat).Width;
        }

        private static StringFormat CreateMeasureFormat()
        {
            var sf = (StringFormat)StringFormat.GenericTypographic.Clone();
            sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
            return sf;
        }
    }

    /// <summary>Estado visual de um widget numa pintura.</summary>
    public struct WidgetVisual
    {
        /// <summary>Valor exibido (para controles durante o arrasto, o valor em pré-visualização).</summary>
        public WidgetValue Value;

        public bool Hover;
        public bool Active;

        /// <summary>O valor exibido ainda não foi entregue ao Grasshopper (arrasto em modo Release ou aguardando o throttle).</summary>
        public bool Pending;

        public bool Enabled;
        public Color Accent;

        /// <summary>Mensagem curta quando não há dado (ex: canal do Hub ausente).</summary>
        public string Status;
    }
}
