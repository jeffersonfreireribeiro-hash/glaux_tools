using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Buraqueira_Tools
{
    // =====================================================================================
    //  MARIMEKKO — MODELO DE DADOS + NORMALIZAÇÃO + LAYOUT (sem Grasshopper, sem Rhino, sem GDI+)
    //
    //  Um único modelo e um único layout alimentam TODAS as representações:
    //      Canvas do GH, viewport do Rhino, PNG, SVG, PDF, CSV e as saídas geométricas.
    //
    //  Largura da categoria  = valor de largura (CategoryWidthValue)         → NormalizedWidth_i  = W_i / ΣW
    //  Altura do segmento    = valor / total da categoria                    → NormalizedHeight_ij = V_ij / Σ_j V_ij
    //  Área da célula        = NormalizedWidth_i × NormalizedHeight_ij       → RelativeArea (fração do gráfico)
    //  Estes três valores (CategoryShare, SegmentShare, AreaShare) são SEMPRE mantidos separados.
    //  Coordenadas normalizadas: X0..X1 e Y0..Y1 em [0,1]; Y0 = 0 é a base do gráfico (1º segmento embaixo).
    // =====================================================================================

    public enum MarimekkoSeverity { Info = 0, Warning = 1, Error = 2 }

    public sealed class MarimekkoDiagnostic
    {
        public MarimekkoSeverity Severity;
        public string Message;
        public MarimekkoDiagnostic(MarimekkoSeverity severity, string message) { Severity = severity; Message = message; }
        public override string ToString() => $"[{Severity}] {Message}";
    }

    /// <summary>Entrada bruta de uma categoria (um ramo da árvore). Valores NaN/∞ são preservados para serem diagnosticados.</summary>
    public sealed class MarimekkoCategoryInput
    {
        public string Id = "";                 // estável: caminho do ramo ("{0}", "{1}", ...)
        public string Label = "";
        public double? WidthValue;             // null = não informado (usa o total da categoria)
        public List<double> SegmentValues = new List<double>();
        public List<string> SegmentLabels;     // opcional (por categoria); se null usa a lista global
    }

    public sealed class MarimekkoSegmentInfo
    {
        public int Index;
        public string Id = "";                 // estável: "S1", "S2", ...
        public string Label = "";
    }

    public sealed class MarimekkoCell
    {
        public string CategoryId, CategoryLabel, SegmentId, SegmentLabel;
        public int CategoryIndex, SegmentIndex;       // posição no layout (índice da categoria incluída / do segmento)
        public double WidthValue;                     // valor de largura da categoria
        public double SegmentValue;
        public double CategoryTotal;
        public double NormalizedWidth;                // fração da largura total do gráfico (0..1)
        public double NormalizedHeight;               // fração da altura da categoria (0..1)
        public double X0, X1, Y0, Y1;                 // coordenadas normalizadas (0..1)
        public double RelativeArea => NormalizedWidth * NormalizedHeight;   // fração da área total do gráfico
        /// <summary>Identidade estável da célula (não depende de [i][j]).</summary>
        public string StableKey => CategoryId + "|" + SegmentId;
    }

    public sealed class MarimekkoCategoryLayout
    {
        public string Id = "", Label = "";
        public int Index;
        public double WidthValue, CategoryTotal, NormalizedWidth, X0, X1;
        public List<MarimekkoCell> Cells = new List<MarimekkoCell>();
    }

    public sealed class MarimekkoLayout
    {
        public readonly List<MarimekkoCategoryLayout> Categories = new List<MarimekkoCategoryLayout>();
        public readonly List<MarimekkoCell> Cells = new List<MarimekkoCell>();
        public readonly List<MarimekkoSegmentInfo> Segments = new List<MarimekkoSegmentInfo>();
        public readonly List<MarimekkoDiagnostic> Diagnostics = new List<MarimekkoDiagnostic>();
        public double TotalWidthValue;

        public bool HasErrors => Diagnostics.Any(d => d.Severity == MarimekkoSeverity.Error);
        public bool IsValid => !HasErrors && Cells.Count > 0;

        private void Add(MarimekkoSeverity s, string m) => Diagnostics.Add(new MarimekkoDiagnostic(s, m));

        /// <summary>
        /// Constrói o layout. Regras (nada é corrigido em silêncio, nunca se usa Abs()):
        ///  • largura/segmento negativo, NaN ou ∞ → ERRO: o gráfico inteiro é inválido (sem geometria);
        ///  • largura 0 → categoria sem largura (nenhuma célula), aviso;
        ///  • Σ larguras = 0 → ERRO; total da categoria = 0 → categoria sem composição, omitida, aviso;
        ///  • larguras não informadas → cada categoria usa o seu próprio total (Marimekko clássico);
        ///  • a ordem fornecida é sempre preservada.
        /// </summary>
        public static MarimekkoLayout Build(IList<MarimekkoCategoryInput> inputs, IList<string> globalSegmentLabels = null)
        {
            var lay = new MarimekkoLayout();
            if (inputs == null || inputs.Count == 0)
            {
                lay.Add(MarimekkoSeverity.Error, "Nenhuma categoria (ramo) em Values.");
                return lay;
            }

            // 1) Validação numérica dura
            for (int i = 0; i < inputs.Count; i++)
            {
                var c = inputs[i];
                string name = DisplayName(c, i);
                if (c.WidthValue.HasValue)
                {
                    double w = c.WidthValue.Value;
                    if (double.IsNaN(w) || double.IsInfinity(w))
                        lay.Add(MarimekkoSeverity.Error, $"Largura da categoria {name} inválida (NaN/Infinito).");
                    else if (w < 0)
                        lay.Add(MarimekkoSeverity.Error, $"Largura da categoria {name} negativa ({Fmt(w)}): valores negativos não são válidos em um Marimekko composicional (não é usado valor absoluto).");
                }
                for (int j = 0; j < c.SegmentValues.Count; j++)
                {
                    double v = c.SegmentValues[j];
                    if (double.IsNaN(v) || double.IsInfinity(v))
                        lay.Add(MarimekkoSeverity.Error, $"Segmento {j + 1} da categoria {name} inválido (NaN/Infinito/nulo).");
                    else if (v < 0)
                        lay.Add(MarimekkoSeverity.Error, $"Segmento {j + 1} da categoria {name} negativo ({Fmt(v)}): não é usado valor absoluto nem células invertidas.");
                }
            }
            if (lay.HasErrors) return lay;

            // 2) Segmentos (identidade posicional estável S1..Sn; rótulos da lista global ou da categoria)
            int maxSeg = inputs.Max(c => c.SegmentValues.Count);
            for (int j = 0; j < maxSeg; j++)
            {
                string label = null;
                if (globalSegmentLabels != null && j < globalSegmentLabels.Count && !string.IsNullOrWhiteSpace(globalSegmentLabels[j]))
                    label = globalSegmentLabels[j].Trim();
                if (label == null)
                {
                    var withLabel = inputs.FirstOrDefault(c => c.SegmentLabels != null && j < c.SegmentLabels.Count && !string.IsNullOrWhiteSpace(c.SegmentLabels[j]));
                    if (withLabel != null) label = withLabel.SegmentLabels[j].Trim();
                }
                lay.Segments.Add(new MarimekkoSegmentInfo { Index = j, Id = "S" + (j + 1), Label = label ?? ("Segmento " + (j + 1)) });
            }
            if (globalSegmentLabels != null && globalSegmentLabels.Count > maxSeg && maxSeg > 0)
                lay.Add(MarimekkoSeverity.Info, $"Segment Labels tem {globalSegmentLabels.Count} itens para {maxSeg} segmento(s): excedentes ignorados.");
            if (inputs.Select(c => c.SegmentValues.Count).Distinct().Count() > 1)
                lay.Add(MarimekkoSeverity.Warning, "Árvore irregular: categorias com quantidades diferentes de segmentos. Segmentos são identificados pela POSIÇÃO no ramo (S1, S2, ...); segmentos ausentes NÃO são assumidos como 0 (a categoria só usa os que tem).");

            // 3) Larguras efetivas e totais
            var included = new List<(MarimekkoCategoryInput input, int srcIndex, double width, double total)>();
            for (int i = 0; i < inputs.Count; i++)
            {
                var c = inputs[i];
                string name = DisplayName(c, i);
                double total = c.SegmentValues.Sum();
                if (c.SegmentValues.Count == 0)
                {
                    lay.Add(MarimekkoSeverity.Warning, $"Categoria {name} sem segmentos (ramo vazio): omitida.");
                    continue;
                }
                double width = c.WidthValue ?? total;
                if (width <= 0)
                {
                    lay.Add(MarimekkoSeverity.Warning, $"Categoria {name} com largura 0: sem largura, nenhuma célula é gerada.");
                    continue;
                }
                if (total <= 0)
                {
                    lay.Add(MarimekkoSeverity.Warning, $"Categoria {name} sem composição válida (soma dos segmentos = 0): omitida.");
                    continue;
                }
                included.Add((c, i, width, total));
            }

            double sumW = included.Sum(x => x.width);
            if (included.Count == 0 || sumW <= 0)
            {
                lay.Add(MarimekkoSeverity.Error, "Entrada inválida: a soma das larguras das categorias válidas é 0.");
                return lay;
            }
            lay.TotalWidthValue = sumW;

            // 4) Normalização e layout (ordem preservada)
            double x = 0.0;
            for (int k = 0; k < included.Count; k++)
            {
                var (c, src, width, total) = included[k];
                var cat = new MarimekkoCategoryLayout
                {
                    Id = c.Id, Label = string.IsNullOrWhiteSpace(c.Label) ? DisplayName(c, src) : c.Label.Trim(),
                    Index = k, WidthValue = width, CategoryTotal = total,
                    NormalizedWidth = width / sumW, X0 = x, X1 = (k == included.Count - 1) ? 1.0 : x + width / sumW
                };
                x = cat.X1;

                double y = 0.0;
                for (int j = 0; j < c.SegmentValues.Count; j++)
                {
                    double v = c.SegmentValues[j];
                    double h = v / total;
                    var seg = lay.Segments[j];
                    string segLabel = (c.SegmentLabels != null && j < c.SegmentLabels.Count && !string.IsNullOrWhiteSpace(c.SegmentLabels[j])) ? c.SegmentLabels[j].Trim() : seg.Label;
                    var cell = new MarimekkoCell
                    {
                        CategoryId = cat.Id, CategoryLabel = cat.Label, SegmentId = seg.Id, SegmentLabel = segLabel,
                        CategoryIndex = k, SegmentIndex = j, WidthValue = width, SegmentValue = v, CategoryTotal = total,
                        NormalizedWidth = cat.NormalizedWidth, NormalizedHeight = h,
                        X0 = cat.X0, X1 = cat.X1, Y0 = y, Y1 = (j == c.SegmentValues.Count - 1) ? 1.0 : y + h
                    };
                    y = cell.Y1;
                    cat.Cells.Add(cell);
                    lay.Cells.Add(cell);
                }
                lay.Categories.Add(cat);
            }
            return lay;
        }

        /// <summary>Maior desvio de: Σ larguras normalizadas = 1 e, por categoria, Σ alturas normalizadas = 1.</summary>
        public double MaxInvariantError()
        {
            if (Categories.Count == 0) return double.NaN;
            double err = Math.Abs(Categories.Sum(c => c.NormalizedWidth) - 1.0);
            foreach (var c in Categories) err = Math.Max(err, Math.Abs(c.Cells.Sum(x => x.NormalizedHeight) - 1.0));
            return err;
        }

        /// <summary>Maior desvio entre a área geométrica (X1−X0)(Y1−Y0) e NormalizedWidth × NormalizedHeight.</summary>
        public double MaxAreaError()
        {
            double err = 0;
            foreach (var c in Cells) err = Math.Max(err, Math.Abs((c.X1 - c.X0) * (c.Y1 - c.Y0) - c.RelativeArea));
            return err;
        }

        private static string DisplayName(MarimekkoCategoryInput c, int index)
            => string.IsNullOrWhiteSpace(c.Label) ? (string.IsNullOrWhiteSpace(c.Id) ? $"#{index + 1}" : c.Id) : $"'{c.Label.Trim()}'";

        private static string Fmt(double v) => v.ToString("G6", CultureInfo.InvariantCulture);
    }
}
