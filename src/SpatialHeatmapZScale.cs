using System;
using System.Collections.Generic;
using System.Globalization;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Escala lateral de valores do Spatial Grid &amp; Viewport Heatmap.
    ///
    /// A altura desenhada de cada nó da malha é uma função LINEAR do valor interpolado:
    ///     altura(v) = baseZ(nó) + OffsetA + OffsetB · (v − ValueRef)
    /// com ValueRef = menor valor interpolado na grade (zNormMin do componente),
    ///   • Z normalizado ('0.2 To 2.5'):  OffsetA = zBase,  OffsetB = zSpan / (maxGrade − minGrade)
    ///   • Z por fator (ZScale = k):       OffsetA = 0,      OffsetB = k
    /// A escala inverte essa relação: cada rótulo mostra o VALOR DE DADOS (não a cota Z do viewport), posicionado
    /// exatamente na altura que a malha usa para aquele valor.
    /// </summary>
    public sealed class ZScaleDefinition
    {
        public double ValueMin, ValueMax;      // domínio numérico representado pela escala
        public double ValueRef;                // valor que corresponde a OffsetA
        public double OffsetA, OffsetB;        // parâmetros da transformação valor → altura
        public double BaseZ;                   // cota da superfície-base no canto de referência da grade
        public double AxisX, AxisY;            // posição (plano XY) do eixo da escala
        public double TickLength;              // comprimento dos traços (unidades do modelo)
        public double LineEndX;                // fim (em X) das linhas de referência horizontais
        public int Decimals;
        public bool IsDegenerate;              // Min == Max: um único traço
        public List<double> Ticks = new List<double>();
        public List<string> Labels = new List<string>();
        public string Title = "";
        public BoundingBox Bounds = BoundingBox.Empty;

        public double HeightOf(double value) => BaseZ + OffsetA + OffsetB * (value - ValueRef);
        public Point3d AxisPoint(double value) => new Point3d(AxisX, AxisY, HeightOf(value));
    }

    public static class ZScaleBuilder
    {
        /// <summary>
        /// Monta a escala. Devolve null quando ela não faz sentido: sem deformação em Z (OffsetB == 0) ou domínio inválido.
        /// </summary>
        public static ZScaleDefinition Build(
            double valueMin, double valueMax, int divisions, string unit,
            double valueRef, double offsetA, double offsetB, double baseZ,
            double gridMinX, double gridMaxX, double gridMinY, double gridMaxY)
        {
            if (Math.Abs(offsetB) < 1e-12) return null;
            if (double.IsNaN(valueMin) || double.IsNaN(valueMax) || double.IsInfinity(valueMin) || double.IsInfinity(valueMax)) return null;
            if (valueMax < valueMin) { double t = valueMin; valueMin = valueMax; valueMax = t; }

            double span = Math.Max(gridMaxX - gridMinX, gridMaxY - gridMinY);
            if (span < 1e-9) span = 1.0;

            var def = new ZScaleDefinition
            {
                ValueMin = valueMin, ValueMax = valueMax, ValueRef = valueRef,
                OffsetA = offsetA, OffsetB = offsetB, BaseZ = baseZ,
                AxisX = gridMinX - 0.06 * span,      // ao lado da grade (borda mínima em X), nunca atravessando-a
                AxisY = gridMinY,
                TickLength = 0.015 * span,
                LineEndX = gridMaxX
            };

            double step;
            def.IsDegenerate = !(valueMax - valueMin > 1e-12 * Math.Max(1.0, Math.Abs(valueMax)));
            if (def.IsDegenerate)
            {
                def.Ticks.Add(valueMin);
                def.Decimals = DecimalsForValue(valueMin);
            }
            else
            {
                def.Ticks = NiceTicks(valueMin, valueMax, divisions, out step, out int decimals);
                def.Decimals = decimals;
            }

            foreach (double tv in def.Ticks) def.Labels.Add(FormatValue(tv, def.Decimals, unit));

            // Caixa: eixo, traços, linhas de referência e uma folga à esquerda para os rótulos (texto 2D em pixels)
            var bb = BoundingBox.Empty;
            double zLo = def.HeightOf(valueMin), zHi = def.HeightOf(valueMax);
            bb.Union(new Point3d(def.AxisX, def.AxisY, Math.Min(zLo, zHi)));
            bb.Union(new Point3d(def.AxisX, def.AxisY, Math.Max(zLo, zHi)));
            bb.Union(new Point3d(def.LineEndX, def.AxisY, Math.Max(zLo, zHi)));
            bb.Union(new Point3d(def.AxisX - 0.10 * span, def.AxisY, Math.Max(zLo, zHi) + 0.03 * span));
            def.Bounds = bb;
            return def;
        }

        /// <summary>
        /// Valores "bonitos" (passos 1, 2, 2.5, 5 × 10^k) dentro de [lo, hi], próximos de <paramref name="count"/> traços.
        /// 0 aparece sempre que está no intervalo. Se não couberem 2 traços, usa os extremos.
        /// </summary>
        public static List<double> NiceTicks(double lo, double hi, int count, out double step, out int decimals)
        {
            var ticks = new List<double>();
            int n = Math.Max(2, Math.Min(12, count));
            double range = hi - lo;
            double raw = range / (n - 1);
            double mag = Math.Pow(10.0, Math.Floor(Math.Log10(raw)));
            double r = raw / mag;
            double nice = r <= 1.0 + 1e-9 ? 1.0 : r <= 2.0 + 1e-9 ? 2.0 : r <= 2.5 + 1e-9 ? 2.5 : r <= 5.0 + 1e-9 ? 5.0 : 10.0;
            step = nice * mag;

            long first = (long)Math.Ceiling(lo / step - 1e-9);
            long last = (long)Math.Floor(hi / step + 1e-9);
            for (long k = first; k <= last; k++)
            {
                double v = k * step;
                if (Math.Abs(v) < step * 1e-9) v = 0.0;
                ticks.Add(v);
            }

            if (ticks.Count < 2)
            {
                ticks.Clear();
                ticks.Add(lo);
                ticks.Add(hi);
                step = range;
                decimals = Math.Max(DecimalsForValue(lo), DecimalsForValue(hi));
                decimals = Math.Max(decimals, Math.Min(8, Math.Max(0, 2 - (int)Math.Floor(Math.Log10(range)))));
                return ticks;
            }

            decimals = DecimalsForStep(step);
            return ticks;
        }

        /// <summary>Casas decimais mínimas para escrever o passo sem perder informação (2.5 → 1, 0.25 → 2, 20 → 0).</summary>
        public static int DecimalsForStep(double step)
        {
            step = Math.Abs(step);
            for (int d = 0; d <= 8; d++)
            {
                double s = step * Math.Pow(10, d);
                if (Math.Abs(s - Math.Round(s)) < 1e-7 * Math.Max(1.0, s)) return d;
            }
            return 8;
        }

        private static int DecimalsForValue(double v)
        {
            v = Math.Abs(v);
            if (v < 1e-12) return 0;
            for (int d = 0; d <= 4; d++)
            {
                double s = v * Math.Pow(10, d);
                if (Math.Abs(s - Math.Round(s)) < 1e-7 * Math.Max(1.0, s)) return d;
            }
            return 2;
        }

        /// <summary>Formata com a cultura do usuário, sem precisão absurda e sem "-0".</summary>
        public static string FormatValue(double value, int decimals, string unit)
        {
            double rounded = Math.Round(value, Math.Max(0, Math.Min(8, decimals)));
            if (rounded == 0.0) rounded = 0.0; // elimina -0
            string text = rounded.ToString("F" + Math.Max(0, Math.Min(8, decimals)), CultureInfo.CurrentCulture);
            return string.IsNullOrWhiteSpace(unit) ? text : text + " " + unit.Trim();
        }
    }
}
