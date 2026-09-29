using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Buraqueira_Tools.Visual;

namespace Buraqueira_Tools.Dashboard
{
    /// <summary>O que o ponteiro pediu a um widget. O controlador decide commit, throttle e undo; o widget só traduz geometria em intenção.</summary>
    public enum WidgetIntent
    {
        None,

        /// <summary>Arrasto contínuo (slider): valor em pré-visualização; o commit segue a política do widget.</summary>
        Drag,

        /// <summary>Mudança discreta (toggle): commit imediato.</summary>
        Set,

        /// <summary>Botão pressionado (True enquanto pressionado).</summary>
        Press,

        /// <summary>Botão solto.</summary>
        Release,

        /// <summary>Abrir a lista de opções (dropdown).</summary>
        OpenOptions,

        /// <summary>Digitar o valor (duplo clique no slider).</summary>
        Edit
    }

    public struct WidgetResponse
    {
        public WidgetIntent Intent;
        public WidgetValue Value;

        public static WidgetResponse None => default;
        public static WidgetResponse Of(WidgetIntent intent, WidgetValue value = default) => new WidgetResponse { Intent = intent, Value = value };
    }

    /// <summary>Ponteiro em coordenadas do painel, com o retângulo do widget e o valor atual.</summary>
    public struct WidgetPointer
    {
        public RectangleF Rect;
        public PointF Point;
        public WidgetValue Current;
        public DashboardMetrics Metrics;
    }

    /// <summary>
    /// Base dos widgets: configuração (<see cref="Spec"/>), medida para o layout, zona interativa, tradução do ponteiro em
    /// intenção e desenho. Um widget não guarda valor: controles leem o estado, indicadores leem os dados.
    /// </summary>
    public abstract class DashboardWidget
    {
        protected DashboardWidget(WidgetSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public WidgetSpec Spec { get; }
        public string Id => Spec.Id;
        public WidgetKind Kind => Spec.Kind;

        /// <summary>Tem zona clicável. Indicadores não têm: clicar neles arrasta o componente no canvas, como de costume.</summary>
        public virtual bool IsInteractive => false;

        public float PreferredHeight(DashboardMetrics m)
        {
            float h = DefaultHeight(m);
            return float.IsNaN(Spec.Height) ? h : Math.Max(12f, Spec.Height);
        }

        protected abstract float DefaultHeight(DashboardMetrics m);

        public virtual float MinWidth(DashboardMetrics m) => 90f;

        /// <summary>Largura própria quando alinhado (align ≠ stretch). 0 = ocupa a célula.</summary>
        public virtual float NaturalWidth(DashboardMetrics m) => 0f;

        public LayoutItem ToLayoutItem(DashboardMetrics m) => new LayoutItem
        {
            PreferredHeight = PreferredHeight(m),
            MinWidth = MinWidth(m),
            NaturalWidth = NaturalWidth(m),
            Span = Spec.Span,
            Align = Spec.Align
        };

        public virtual bool HitTest(RectangleF rect, PointF p, DashboardMetrics m) => false;
        public virtual WidgetResponse OnPointerDown(WidgetPointer p) => WidgetResponse.None;
        public virtual WidgetResponse OnPointerMove(WidgetPointer p) => WidgetResponse.None;
        public virtual WidgetResponse OnPointerUp(WidgetPointer p) => WidgetResponse.None;
        public virtual WidgetResponse OnDoubleClick(WidgetPointer p) => WidgetResponse.None;

        public abstract void Render(DashboardRenderContext ctx, RectangleF rect, WidgetVisual v);

        /// <summary>Faixa de altura preferida centrada verticalmente (quando a célula é mais alta que o widget).</summary>
        protected RectangleF Inner(RectangleF rect, DashboardMetrics m)
        {
            float h = Math.Min(rect.Height, PreferredHeight(m));
            return new RectangleF(rect.X, rect.Y + (rect.Height - h) / 2f, rect.Width, h);
        }

        // ---------- utilitários de desenho ----------

        // Classe aninhada: os StringFormat (GDI+) só são criados na primeira pintura, nunca ao criar widgets (testes sem GDI+)
        protected static class Formats
        {
            public static readonly StringFormat NearCenter = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            public static readonly StringFormat FarCenter = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            public static readonly StringFormat CenterCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        }

        protected static void Text(Graphics g, string text, Font font, Color color, RectangleF rect, StringFormat format)
        {
            if (string.IsNullOrEmpty(text) || rect.Width <= 1 || rect.Height <= 1) return;
            using (var b = new SolidBrush(color))
            {
                g.DrawString(text, font, b, rect, format);
            }
        }

        protected static Color InkFor(WidgetVisual v) => v.Enabled ? PillVisualKit.Ink : PillVisualKit.Faint;

        /// <summary>Rótulo à esquerda e texto de valor à direita numa mesma linha, dividindo a largura sem sobrepor.</summary>
        protected static void CaptionRow(DashboardRenderContext ctx, RectangleF row, string caption, string valueText, Color valueColor, WidgetVisual v)
        {
            var t = ctx.Theme;
            float valueW = string.IsNullOrEmpty(valueText) ? 0 : Math.Min(row.Width * 0.6f, ctx.Measure(valueText, t.ValueFont) + 4f);
            if (valueW > 0)
            {
                string fitted = DashboardFormat.Ellipsize(valueText, valueW, s => ctx.Measure(s, t.ValueFont));
                Text(ctx.Graphics, fitted, t.ValueFont, valueColor, new RectangleF(row.Right - valueW, row.Y, valueW, row.Height), Formats.FarCenter);
            }
            Text(ctx.Graphics, caption, t.CaptionFont, v.Enabled ? PillVisualKit.Muted : PillVisualKit.Faint, new RectangleF(row.X, row.Y, Math.Max(0, row.Width - valueW - 4f), row.Height), Formats.NearCenter);
        }

        protected static void Placeholder(DashboardRenderContext ctx, RectangleF rect, string text)
        {
            Text(ctx.Graphics, text, ctx.Theme.SmallFont, PillVisualKit.Faint, rect, Formats.CenterCenter);
        }
    }

    /// <summary>Fábrica dos widgets por tipo (ponto único de extensão).</summary>
    public static class DashboardWidgetRegistry
    {
        private static readonly Dictionary<WidgetKind, Func<WidgetSpec, DashboardWidget>> s_factories = new Dictionary<WidgetKind, Func<WidgetSpec, DashboardWidget>>
        {
            [WidgetKind.Label] = s => new LabelWidget(s),
            [WidgetKind.Number] = s => new NumberWidget(s),
            [WidgetKind.Slider] = s => new SliderWidget(s),
            [WidgetKind.Toggle] = s => new ToggleWidget(s),
            [WidgetKind.Button] = s => new ButtonWidget(s),
            [WidgetKind.Dropdown] = s => new DropdownWidget(s),
            [WidgetKind.Progress] = s => new ProgressWidget(s),
            [WidgetKind.MiniChart] = s => new MiniChartWidget(s)
        };

        public static DashboardWidget Create(WidgetSpec spec)
        {
            return s_factories.TryGetValue(spec.Kind, out var factory) ? factory(spec) : new LabelWidget(spec);
        }
    }

    // =====================================================================
    // Indicadores
    // =====================================================================

    /// <summary>Texto ou status. Sem valor: mostra o próprio rótulo (título de seção com style=title).</summary>
    public sealed class LabelWidget : DashboardWidget
    {
        public LabelWidget(WidgetSpec spec) : base(spec)
        {
        }

        private bool HasCaption => Spec.Label.Length > 0 && (Spec.HubKey != null || Spec.Source != null || !Spec.Default.IsNone || !Spec.EmbeddedValue.IsNone);
        private string Style => (Spec.Setting("style") ?? "").ToLowerInvariant();

        protected override float DefaultHeight(DashboardMetrics m)
        {
            float line = Style == "title" ? m.LineHeight + 4f : m.LineHeight;
            return (HasCaption ? m.CaptionHeight : 0f) + Spec.Lines * line + 4f;
        }

        public override float MinWidth(DashboardMetrics m) => 60f;

        public override void Render(DashboardRenderContext ctx, RectangleF rect, WidgetVisual v)
        {
            var t = ctx.Theme;
            var r = Inner(rect, t.Metrics);
            float y = r.Y + 2f;
            if (HasCaption)
            {
                Text(ctx.Graphics, Spec.Label, t.CaptionFont, PillVisualKit.Muted, new RectangleF(r.X, y, r.Width, t.Metrics.CaptionHeight), Formats.NearCenter);
                y += t.Metrics.CaptionHeight;
            }
            string text = v.Value.IsNone ? (HasCaption ? v.Status ?? DashboardFormat.Missing : Spec.Label) : ValueText(v.Value);
            Font font = Style == "title" ? t.HeadingFont : t.TextFont;
            Color color = Style == "muted" ? PillVisualKit.Muted : Style == "title" ? PillVisualKit.InkStrong : InkFor(v);
            var textRect = new RectangleF(r.X, y, r.Width, r.Bottom - y - 1f);
            if (Spec.Lines <= 1)
            {
                Text(ctx.Graphics, text, font, color, textRect, Formats.NearCenter);
            }
            else
            {
                using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisWord })
                {
                    Text(ctx.Graphics, text, font, color, textRect, sf);
                }
            }
        }

        private string ValueText(WidgetValue value)
        {
            if (value.Kind == WidgetValueKind.Number) return DashboardFormat.Number(value.Number, Spec.Decimals, Spec.Unit);
            return value.ToInvariantString();
        }
    }

    /// <summary>Valor numérico em destaque; com min/max, o LED indica se está dentro da faixa esperada.</summary>
    public sealed class NumberWidget : DashboardWidget
    {
        public NumberWidget(WidgetSpec spec) : base(spec)
        {
        }

        protected override float DefaultHeight(DashboardMetrics m) => m.NumberHeight;
        public override float MinWidth(DashboardMetrics m) => 80f;

        public override void Render(DashboardRenderContext ctx, RectangleF rect, WidgetVisual v)
        {
            var t = ctx.Theme;
            var r = Inner(rect, t.Metrics);
            var g = ctx.Graphics;
            bool hasNumber = v.Value.TryGetNumber(out double n);
            var caption = new RectangleF(r.X, r.Y + 1f, r.Width, t.Metrics.CaptionHeight);

            // LED de faixa antes do rótulo (à direita ele parecia pertencer ao widget vizinho)
            if (Spec.HasRange && hasNumber)
            {
                bool inside = n >= Spec.Min && n <= Spec.Max;
                PillVisualKit.DrawLed(g, new PointF(caption.X + 3.5f, caption.Y + caption.Height / 2f), 3.2f, inside ? PillVisualKit.LedOk : PillVisualKit.LedError);
                caption = new RectangleF(caption.X + 10f, caption.Y, caption.Width - 10f, caption.Height);
            }

            var valueRect = new RectangleF(r.X, r.Y + t.Metrics.CaptionHeight, r.Width, r.Height - t.Metrics.CaptionHeight);
            if (!hasNumber)
            {
                // Sem número: travessão grande e o motivo pequeno ao lado do rótulo (texto não numérico aparece como texto)
                bool isText = v.Value.Kind == WidgetValueKind.Text && !string.IsNullOrEmpty(v.Value.Text);
                CaptionRow(ctx, caption, Spec.Label, isText ? null : v.Status, PillVisualKit.Faint, v);
                string big = isText ? v.Value.Text : DashboardFormat.Missing;
                Text(g, DashboardFormat.Ellipsize(big, valueRect.Width, s => ctx.Measure(s, isText ? t.TextFont : t.BigValueFont)), isText ? t.TextFont : t.BigValueFont, isText ? InkFor(v) : PillVisualKit.Faint, valueRect, Formats.NearCenter);
                return;
            }
            Text(g, Spec.Label, t.CaptionFont, v.Enabled ? PillVisualKit.Muted : PillVisualKit.Faint, caption, Formats.NearCenter);
            string fitted = DashboardFormat.FitNumber(n, Spec.Decimals, Spec.Unit, valueRect.Width - 2f, s => ctx.Measure(s, t.BigValueFont));
            Text(g, fitted, t.BigValueFont, v.Enabled ? PillVisualKit.InkStrong : PillVisualKit.Faint, valueRect, Formats.NearCenter);
        }
    }

    /// <summary>Barra de progresso (valor entre min e max; padrão 0..1) com percentual.</summary>
    public sealed class ProgressWidget : DashboardWidget
    {
        public ProgressWidget(WidgetSpec spec) : base(spec)
        {
        }

        protected override float DefaultHeight(DashboardMetrics m) => m.ProgressHeight;

        public override void Render(DashboardRenderContext ctx, RectangleF rect, WidgetVisual v)
        {
            var t = ctx.Theme;
            var r = Inner(rect, t.Metrics);
            var g = ctx.Graphics;
            bool has = v.Value.TryGetNumber(out double n);
            double ratio = has ? WidgetValueRules.Ratio(Spec, n) : 0;
            string valueText = has ? DashboardFormat.Percent(ratio) : v.Status ?? DashboardFormat.Missing;
            CaptionRow(ctx, new RectangleF(r.X, r.Y + 1f, r.Width, t.Metrics.CaptionHeight), Spec.Label, valueText, InkFor(v), v);

            var bar = new RectangleF(r.X, r.Y + t.Metrics.CaptionHeight + 3f, r.Width, Math.Min(9f, Math.Max(5f, r.Height - t.Metrics.CaptionHeight - 5f)));
            using (var path = PillVisualKit.RoundedRect(bar, bar.Height / 2f))
            using (var bg = new SolidBrush(v.Enabled ? PillVisualKit.Field : PillVisualKit.Disabled(PillVisualKit.Field)))
            using (var border = new Pen(PillVisualKit.FieldBorder, 0.8f))
            {
                g.FillPath(bg, path);
                g.DrawPath(border, path);
            }
            float w = (float)(bar.Width * ratio);
            if (w >= 1f)
            {
                var fill = new RectangleF(bar.X, bar.Y, Math.Max(bar.Height, w), bar.Height);
                using (var path = PillVisualKit.RoundedRect(fill, bar.Height / 2f))
                using (var brush = new SolidBrush(v.Enabled ? v.Accent : PillVisualKit.Disabled(v.Accent)))
                {
                    g.FillPath(brush, path);
                }
            }
        }
    }

    /// <summary>Série numérica simples (sparkline) com mínimo/máximo e último valor; séries longas são reduzidas sem perder picos.</summary>
    public sealed class MiniChartWidget : DashboardWidget
    {
        public MiniChartWidget(WidgetSpec spec) : base(spec)
        {
        }

        protected override float DefaultHeight(DashboardMetrics m) => m.ChartHeight;
        public override float MinWidth(DashboardMetrics m) => 110f;

        public override void Render(DashboardRenderContext ctx, RectangleF rect, WidgetVisual v)
        {
            var t = ctx.Theme;
            var g = ctx.Graphics;
            IReadOnlyList<double> series = v.Value.Kind == WidgetValueKind.Series ? v.Value.Series
                : v.Value.TryGetNumber(out double single) ? new[] { single } : Array.Empty<double>();
            SeriesSampling.Range(series, out double min, out double max);
            bool has = !double.IsNaN(min);
            string last = null;
            for (int i = series.Count - 1; i >= 0 && last == null; i--)
            {
                if (SeriesSampling.IsFinite(series[i])) last = DashboardFormat.Number(series[i], Spec.Decimals, Spec.Unit);
            }
            CaptionRow(ctx, new RectangleF(rect.X, rect.Y + 1f, rect.Width, t.Metrics.CaptionHeight), Spec.Label, last ?? "", InkFor(v), v);

            var plot = new RectangleF(rect.X, rect.Y + t.Metrics.CaptionHeight + 3f, rect.Width, Math.Max(10f, rect.Height - t.Metrics.CaptionHeight - 4f));
            using (var path = PillVisualKit.RoundedRect(plot, 3f))
            using (var bg = new SolidBrush(Color.White))
            using (var border = new Pen(PillVisualKit.FieldBorder, 0.8f))
            {
                g.FillPath(bg, path);
                g.DrawPath(border, path);
            }
            if (!has)
            {
                Placeholder(ctx, plot, v.Status ?? "sem dados");
                return;
            }

            if (Spec.HasRange)
            {
                min = Spec.Min;
                max = Spec.Max;
            }
            if (max - min < 1e-12)
            {
                double pad = Math.Abs(max) > 1e-12 ? Math.Abs(max) * 0.1 : 1;
                min -= pad;
                max += pad;
            }

            var inner = RectangleF.Inflate(plot, -4f, -4f);
            float labelW = ctx.LowDetail ? 0 : Math.Min(inner.Width * 0.35f, Math.Max(ctx.Measure(DashboardFormat.Compact(max), t.SmallFont), ctx.Measure(DashboardFormat.Compact(min), t.SmallFont)) + 3f);
            var area = new RectangleF(inner.X + labelW, inner.Y, Math.Max(4f, inner.Width - labelW), inner.Height);

            float X(int index) => series.Count <= 1 ? area.X + area.Width / 2f : area.X + area.Width * index / (series.Count - 1);
            float Y(double value) => area.Bottom - (float)((Math.Max(min, Math.Min(max, value)) - min) / (max - min)) * area.Height;

            if (min < 0 && max > 0)
            {
                using (var zero = new Pen(PillVisualKit.Rail, 0.8f) { DashStyle = DashStyle.Dot })
                {
                    float zy = Y(0);
                    g.DrawLine(zero, area.X, zy, area.Right, zy);
                }
            }

            var points = SeriesSampling.MinMaxDecimate(series, Math.Max(2, Math.Min(Spec.MaxPoints, (int)(area.Width * ctx.Zoom))));
            var accent = v.Enabled ? v.Accent : PillVisualKit.Faint;
            if (points.Count == 1)
            {
                PillVisualKit.DrawLed(g, new PointF(X(points[0].Key), Y(points[0].Value)), 2.4f, accent);
            }
            else if (points.Count > 1)
            {
                var pts = new PointF[points.Count];
                for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(X(points[i].Key), Y(points[i].Value));
                var oldMode = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(accent, 1.4f) { LineJoin = LineJoin.Round })
                {
                    g.DrawLines(pen, pts);
                }
                var lastPt = pts[pts.Length - 1];
                PillVisualKit.DrawLed(g, lastPt, 2.2f, accent);
                g.SmoothingMode = oldMode;
            }

            if (labelW > 0)
            {
                using (var top = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
                using (var bottom = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Far })
                {
                    Text(g, DashboardFormat.Compact(max), t.SmallFont, PillVisualKit.Faint, new RectangleF(inner.X, inner.Y - 1f, labelW, inner.Height), top);
                    Text(g, DashboardFormat.Compact(min), t.SmallFont, PillVisualKit.Faint, new RectangleF(inner.X, inner.Y, labelW, inner.Height + 1f), bottom);
                }
            }
        }
    }

    // =====================================================================
    // Controles
    // =====================================================================

    /// <summary>Slider numérico: rótulo e valor em cima, trilho embaixo. Clique no trilho salta; arrasto segue a política de commit.</summary>
    public sealed class SliderWidget : DashboardWidget
    {
        public SliderWidget(WidgetSpec spec) : base(spec)
        {
        }

        public override bool IsInteractive => true;
        protected override float DefaultHeight(DashboardMetrics m) => m.SliderHeight;
        public override float MinWidth(DashboardMetrics m) => 110f;

        public RectangleF TrackRect(RectangleF rect, DashboardMetrics m)
        {
            var r = Inner(rect, m);
            float top = r.Y + m.CaptionHeight + 2f;
            return new RectangleF(r.X + m.KnobMargin, top, Math.Max(4f, r.Width - 2 * m.KnobMargin), Math.Max(12f, r.Bottom - top));
        }

        public override bool HitTest(RectangleF rect, PointF p, DashboardMetrics m)
        {
            var track = TrackRect(rect, m);
            var zone = RectangleF.FromLTRB(rect.X, track.Y - 3f, rect.Right, rect.Bottom);
            return zone.Contains(p);
        }

        public WidgetValue ValueAt(RectangleF rect, float x, DashboardMetrics m)
        {
            var track = TrackRect(rect, m);
            double ratio = track.Width > 0 ? (x - track.X) / track.Width : 0;
            return WidgetValue.FromNumber(WidgetValueRules.FromRatio(Spec, ratio));
        }

        public override WidgetResponse OnPointerDown(WidgetPointer p) => WidgetResponse.Of(WidgetIntent.Drag, ValueAt(p.Rect, p.Point.X, p.Metrics));
        public override WidgetResponse OnPointerMove(WidgetPointer p) => WidgetResponse.Of(WidgetIntent.Drag, ValueAt(p.Rect, p.Point.X, p.Metrics));
        public override WidgetResponse OnPointerUp(WidgetPointer p) => WidgetResponse.Of(WidgetIntent.Drag, ValueAt(p.Rect, p.Point.X, p.Metrics));
        public override WidgetResponse OnDoubleClick(WidgetPointer p) => WidgetResponse.Of(WidgetIntent.Edit, p.Current);

        public override void Render(DashboardRenderContext ctx, RectangleF rect, WidgetVisual v)
        {
            var t = ctx.Theme;
            var r = Inner(rect, t.Metrics);
            double n = v.Value.TryGetNumber(out double num) ? num : Spec.Min;
            string valueText = DashboardFormat.Number(n, Spec.Decimals, Spec.Unit) + (v.Pending ? " •" : "");
            Color valueColor = !v.Enabled ? PillVisualKit.Faint : v.Pending ? v.Accent : PillVisualKit.InkStrong;
            CaptionRow(ctx, new RectangleF(r.X, r.Y + 1f, r.Width, t.Metrics.CaptionHeight), Spec.Label, valueText, valueColor, v);
            if (v.Hover && v.Enabled && !v.Active)
            {
                using (var hover = new SolidBrush(Color.FromArgb(14, v.Accent)))
                using (var path = PillVisualKit.RoundedRect(RectangleF.Inflate(rect, 2f, 1f), 4f))
                {
                    ctx.Graphics.FillPath(hover, path);
                }
            }
            PillVisualKit.DrawRail(ctx.Graphics, TrackRect(rect, t.Metrics), WidgetValueRules.Ratio(Spec, n), v.Accent, v.Active, v.Enabled, v.Hover);
        }
    }

    /// <summary>Controle booleano: rótulo à esquerda, switch à direita; clique em qualquer ponto alterna.</summary>
    public sealed class ToggleWidget : DashboardWidget
    {
        public ToggleWidget(WidgetSpec spec) : base(spec)
        {
        }

        public override bool IsInteractive => true;
        protected override float DefaultHeight(DashboardMetrics m) => m.ToggleHeight;

        public override bool HitTest(RectangleF rect, PointF p, DashboardMetrics m) => rect.Contains(p);

        public override WidgetResponse OnPointerDown(WidgetPointer p)
        {
            bool current = p.Current.TryGetBoolean(out bool b) && b;
            return WidgetResponse.Of(WidgetIntent.Set, WidgetValue.FromBoolean(!current));
        }

        public override void Render(DashboardRenderContext ctx, RectangleF rect, WidgetVisual v)
        {
            var t = ctx.Theme;
            var r = Inner(rect, t.Metrics);
            bool on = v.Value.TryGetBoolean(out bool b) && b;
            var sw = new RectangleF(r.Right - 32f, r.Y + (r.Height - 14f) / 2f, 32f, 14f);
            if (v.Hover && v.Enabled)
            {
                using (var hover = new SolidBrush(Color.FromArgb(14, v.Accent)))
                using (var path = PillVisualKit.RoundedRect(RectangleF.Inflate(r, 2f, 0f), 4f))
                {
                    ctx.Graphics.FillPath(hover, path);
                }
            }
            Text(ctx.Graphics, Spec.Label, t.CaptionFont, InkFor(v), new RectangleF(r.X, r.Y, Math.Max(0, sw.X - r.X - 6f), r.Height), Formats.NearCenter);
            PillVisualKit.DrawSwitch(ctx.Graphics, sw, on, v.Enabled, v.Hover);
        }
    }

    /// <summary>Ação momentânea: True enquanto pressionado (mesma semântica do Button do Grasshopper).</summary>
    public sealed class ButtonWidget : DashboardWidget
    {
        public ButtonWidget(WidgetSpec spec) : base(spec)
        {
        }

        public override bool IsInteractive => true;
        protected override float DefaultHeight(DashboardMetrics m) => m.ButtonHeight;
        public override float MinWidth(DashboardMetrics m) => 70f;
        public override float NaturalWidth(DashboardMetrics m) => 96f;

        public override bool HitTest(RectangleF rect, PointF p, DashboardMetrics m) => Inner(rect, m).Contains(p);
        public override WidgetResponse OnPointerDown(WidgetPointer p) => WidgetResponse.Of(WidgetIntent.Press, WidgetValue.FromBoolean(true));
        public override WidgetResponse OnPointerUp(WidgetPointer p) => WidgetResponse.Of(WidgetIntent.Release, WidgetValue.FromBoolean(false));

        public override void Render(DashboardRenderContext ctx, RectangleF rect, WidgetVisual v)
        {
            var r = Inner(rect, ctx.Theme.Metrics);
            bool pressed = v.Active || (v.Value.TryGetBoolean(out bool b) && b);
            string caption = Spec.Caption ?? (Spec.Label.Length > 0 ? Spec.Label : "Executar");
            PillVisualKit.DrawButtonFace(ctx.Graphics, RectangleF.Inflate(r, -0.5f, -1f), caption, ctx.Theme.ButtonFont, pressed, v.Hover, v.Enabled, v.Accent);
        }
    }

    /// <summary>Seleção entre opções: rótulo em cima e campo com a opção atual; clique abre a lista.</summary>
    public sealed class DropdownWidget : DashboardWidget
    {
        public DropdownWidget(WidgetSpec spec) : base(spec)
        {
        }

        public override bool IsInteractive => true;
        protected override float DefaultHeight(DashboardMetrics m) => m.DropdownHeight;
        public override float MinWidth(DashboardMetrics m) => 100f;

        public RectangleF FieldRect(RectangleF rect, DashboardMetrics m)
        {
            var r = Inner(rect, m);
            float top = r.Y + m.CaptionHeight + 1f;
            return new RectangleF(r.X, top, r.Width, Math.Max(12f, Math.Min(m.FieldHeight, r.Bottom - top)));
        }

        public override bool HitTest(RectangleF rect, PointF p, DashboardMetrics m) => FieldRect(rect, m).Contains(p);
        public override WidgetResponse OnPointerDown(WidgetPointer p) => Spec.Options.Count > 0 ? WidgetResponse.Of(WidgetIntent.OpenOptions) : WidgetResponse.None;

        public override void Render(DashboardRenderContext ctx, RectangleF rect, WidgetVisual v)
        {
            var t = ctx.Theme;
            var r = Inner(rect, t.Metrics);
            Text(ctx.Graphics, Spec.Label, t.CaptionFont, v.Enabled ? PillVisualKit.Muted : PillVisualKit.Faint, new RectangleF(r.X, r.Y + 1f, r.Width, t.Metrics.CaptionHeight), Formats.NearCenter);
            var field = FieldRect(rect, t.Metrics);
            PillVisualKit.DrawField(ctx.Graphics, field, v.Hover || v.Active, v.Enabled, v.Accent);
            string text = Spec.Options.Count == 0 ? "sem opções" : v.Value.IsNone ? DashboardFormat.Missing : v.Value.ToInvariantString();
            var textRect = new RectangleF(field.X + 5f, field.Y, Math.Max(0, field.Width - 20f), field.Height);
            Text(ctx.Graphics, text, t.ValueFont, Spec.Options.Count == 0 ? PillVisualKit.Faint : InkFor(v), textRect, Formats.NearCenter);

            // Seta ▾ desenhada (não depende de glifo da fonte)
            float cx = field.Right - 9f, cy = field.Y + field.Height / 2f;
            using (var brush = new SolidBrush(v.Enabled ? v.Accent : PillVisualKit.Faint))
            {
                ctx.Graphics.FillPolygon(brush, new[] { new PointF(cx - 3.5f, cy - 1.8f), new PointF(cx + 3.5f, cy - 1.8f), new PointF(cx, cy + 2.2f) });
            }
        }
    }
}
