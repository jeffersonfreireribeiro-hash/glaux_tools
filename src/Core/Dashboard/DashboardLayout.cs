using System;
using System.Collections.Generic;
using System.Drawing;

namespace Buraqueira_Tools.Dashboard
{
    /// <summary>Pedido de espaço de um widget para o layout.</summary>
    public struct LayoutItem
    {
        public float PreferredHeight;
        public float MinWidth;

        /// <summary>Largura própria quando o alinhamento não é Stretch (ex: um botão compacto). 0 = ocupa a célula.</summary>
        public float NaturalWidth;

        /// <summary>Colunas ocupadas (Grid) ou peso da largura (Row).</summary>
        public int Span;

        public WidgetAlign Align;
    }

    public sealed class LayoutOptions
    {
        public LayoutKind Kind { get; set; } = LayoutKind.Stack;
        public int Columns { get; set; } = 2;
        public float Width { get; set; } = 300f;
        public float Padding { get; set; } = 8f;
        public float Spacing { get; set; } = 6f;
        public float HeaderHeight { get; set; } = 26f;

        /// <summary>Altura da área de conteúdo de um painel sem widgets (mensagem de ajuda).</summary>
        public float EmptyHeight { get; set; } = 46f;

        public static LayoutOptions From(DashboardSpec spec, float headerHeight)
        {
            return new LayoutOptions
            {
                Kind = spec.Layout,
                Columns = spec.Columns,
                Width = spec.Width,
                Padding = spec.Padding,
                Spacing = spec.Spacing,
                HeaderHeight = headerHeight
            };
        }
    }

    public sealed class LayoutResult
    {
        /// <summary>Retângulo de cada widget (coordenadas locais: 0,0 = canto superior esquerdo do painel).</summary>
        public RectangleF[] Items { get; internal set; } = Array.Empty<RectangleF>();

        /// <summary>Célula reservada para cada widget (igual ao item quando Align = Stretch).</summary>
        public RectangleF[] Cells { get; internal set; } = Array.Empty<RectangleF>();

        public SizeF Size { get; internal set; }

        /// <summary>Área de conteúdo (abaixo do cabeçalho, dentro do padding).</summary>
        public RectangleF Content { get; internal set; }

        public int Rows { get; internal set; }
    }

    /// <summary>
    /// Layout simples e previsível: Stack (coluna), Row (linha) e Grid (colunas com span). Calcula posição, tamanho,
    /// padding, espaçamento e alinhamento; a largura cresce quando os mínimos dos widgets não cabem. Coordenadas
    /// arredondadas a unidades inteiras do canvas (bordas nítidas com zoom 1) sem sobreposição nem frestas acumuladas.
    /// </summary>
    public static class DashboardLayoutEngine
    {
        public static LayoutResult Arrange(LayoutOptions options, IReadOnlyList<LayoutItem> items)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            items = items ?? Array.Empty<LayoutItem>();
            float pad = Math.Max(0, options.Padding);
            float gap = Math.Max(0, options.Spacing);
            float top = options.HeaderHeight + pad;

            if (items.Count == 0)
            {
                float w = Math.Max(120f, options.Width);
                var content = new RectangleF(pad, top, w - 2 * pad, options.EmptyHeight);
                return new LayoutResult
                {
                    Size = new SizeF(w, top + options.EmptyHeight + pad),
                    Content = content
                };
            }

            switch (options.Kind)
            {
                case LayoutKind.Row:
                    return ArrangeRow(options, items, pad, gap, top);
                case LayoutKind.Grid:
                    return ArrangeGrid(options, items, pad, gap, top);
                default:
                    return ArrangeStack(options, items, pad, gap, top);
            }
        }

        private static LayoutResult ArrangeStack(LayoutOptions o, IReadOnlyList<LayoutItem> items, float pad, float gap, float top)
        {
            float minContent = 0;
            foreach (var it in items) minContent = Math.Max(minContent, it.MinWidth);
            float width = Math.Max(Math.Max(120f, o.Width), minContent + 2 * pad);
            float contentW = width - 2 * pad;

            var cells = new RectangleF[items.Count];
            float y = top;
            for (int i = 0; i < items.Count; i++)
            {
                float h = Math.Max(1f, items[i].PreferredHeight);
                cells[i] = Snap(pad, y, pad + contentW, y + h);
                y += h + gap;
            }
            float height = y - gap + pad;
            return Finish(items, cells, width, height, new RectangleF(pad, top, contentW, height - top - pad), items.Count);
        }

        private static LayoutResult ArrangeRow(LayoutOptions o, IReadOnlyList<LayoutItem> items, float pad, float gap, float top)
        {
            int n = items.Count;
            float sumMin = 0;
            foreach (var it in items) sumMin += Math.Max(0, it.MinWidth);
            float width = Math.Max(Math.Max(120f, o.Width), sumMin + (n - 1) * gap + 2 * pad);
            float available = width - 2 * pad - (n - 1) * gap;

            var widths = DistributeByWeight(items, available);
            float rowH = 1f;
            foreach (var it in items) rowH = Math.Max(rowH, it.PreferredHeight);

            var cells = new RectangleF[n];
            float x = pad;
            for (int i = 0; i < n; i++)
            {
                cells[i] = Snap(x, top, x + widths[i], top + rowH);
                x += widths[i] + gap;
            }
            float height = top + rowH + pad;
            return Finish(items, cells, width, height, new RectangleF(pad, top, width - 2 * pad, rowH), 1);
        }

        /// <summary>Divide a largura proporcionalmente ao span, respeitando os mínimos (quem fica abaixo do mínimo é fixado nele).</summary>
        internal static float[] DistributeByWeight(IReadOnlyList<LayoutItem> items, float available)
        {
            int n = items.Count;
            var widths = new float[n];
            var fixedSet = new bool[n];
            float remaining = available;
            bool changed = true;
            while (changed)
            {
                changed = false;
                float weightSum = 0;
                for (int i = 0; i < n; i++)
                {
                    if (!fixedSet[i]) weightSum += Math.Max(1, items[i].Span);
                }
                if (weightSum <= 0) break;
                for (int i = 0; i < n; i++)
                {
                    if (fixedSet[i]) continue;
                    float w = remaining * Math.Max(1, items[i].Span) / weightSum;
                    if (w < items[i].MinWidth)
                    {
                        widths[i] = items[i].MinWidth;
                        fixedSet[i] = true;
                        remaining -= items[i].MinWidth;
                        changed = true;
                        break;
                    }
                    widths[i] = w;
                }
            }
            return widths;
        }

        private static LayoutResult ArrangeGrid(LayoutOptions o, IReadOnlyList<LayoutItem> items, float pad, float gap, float top)
        {
            int cols = Math.Max(1, o.Columns);
            float width = Math.Max(120f, o.Width);
            float cellW = (width - 2 * pad - (cols - 1) * gap) / cols;

            // Largura mínima de célula exigida pelos widgets (um widget com span s divide o mínimo por s colunas)
            float needCell = 0;
            foreach (var it in items)
            {
                int s = Math.Min(cols, Math.Max(1, it.Span));
                needCell = Math.Max(needCell, (it.MinWidth - (s - 1) * gap) / s);
            }
            if (needCell > cellW)
            {
                cellW = needCell;
                width = cols * cellW + (cols - 1) * gap + 2 * pad;
            }

            // Distribui em linhas: um widget que não cabe no resto da linha começa a próxima
            var placement = new List<(int index, int col, int span, int row)>();
            int col = 0, row = 0;
            for (int i = 0; i < items.Count; i++)
            {
                int s = Math.Min(cols, Math.Max(1, items[i].Span));
                if (col + s > cols)
                {
                    row++;
                    col = 0;
                }
                placement.Add((i, col, s, row));
                col += s;
            }

            int rows = row + 1;
            var rowHeights = new float[rows];
            foreach (var p in placement) rowHeights[p.row] = Math.Max(rowHeights[p.row], Math.Max(1f, items[p.index].PreferredHeight));
            var rowTops = new float[rows];
            float y = top;
            for (int r = 0; r < rows; r++)
            {
                rowTops[r] = y;
                y += rowHeights[r] + gap;
            }

            var cells = new RectangleF[items.Count];
            foreach (var p in placement)
            {
                float x0 = pad + p.col * (cellW + gap);
                float x1 = x0 + p.span * cellW + (p.span - 1) * gap;
                cells[p.index] = Snap(x0, rowTops[p.row], x1, rowTops[p.row] + rowHeights[p.row]);
            }
            float height = y - gap + pad;
            return Finish(items, cells, width, height, new RectangleF(pad, top, width - 2 * pad, height - top - pad), rows);
        }

        private static LayoutResult Finish(IReadOnlyList<LayoutItem> items, RectangleF[] cells, float width, float height, RectangleF content, int rows)
        {
            var rects = new RectangleF[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                var c = cells[i];
                var it = items[i];
                if (it.Align == WidgetAlign.Stretch || it.NaturalWidth <= 0 || it.NaturalWidth >= c.Width)
                {
                    rects[i] = c;
                    continue;
                }
                float w = Math.Max(it.NaturalWidth, it.MinWidth);
                if (w >= c.Width)
                {
                    rects[i] = c;
                    continue;
                }
                float x;
                switch (it.Align)
                {
                    case WidgetAlign.Center:
                        x = c.X + (c.Width - w) / 2f;
                        break;
                    case WidgetAlign.Right:
                        x = c.Right - w;
                        break;
                    default:
                        x = c.X;
                        break;
                }
                rects[i] = Snap(x, c.Y, x + w, c.Bottom);
            }
            return new LayoutResult
            {
                Items = rects,
                Cells = cells,
                Size = new SizeF((float)Math.Ceiling(width), (float)Math.Ceiling(height)),
                Content = content,
                Rows = rows
            };
        }

        /// <summary>Arredonda cada borda (não a largura): células vizinhas nunca se sobrepõem nem abrem frestas.</summary>
        private static RectangleF Snap(float left, float top, float right, float bottom)
        {
            float l = (float)Math.Round(left);
            float t = (float)Math.Round(top);
            float r = (float)Math.Round(right);
            float b = (float)Math.Round(bottom);
            return new RectangleF(l, t, Math.Max(1f, r - l), Math.Max(1f, b - t));
        }
    }
}
