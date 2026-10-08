using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Perfil espacial: seleciona as amostras (pontos + valores) próximas a uma linha/curva de seção e as ordena pela
    /// DISTÂNCIA AO LONGO dela (X do gráfico = distância no perfil; Y = valor). Não interpola nem inventa valores intermediários.
    /// Diferente de agrupar por X: a posição de cada amostra é projetada na curva (ponto mais próximo).
    /// </summary>
    public static class ChartProfile
    {
        public sealed class Result
        {
            public List<double?> Distance = new List<double?>();   // X do gráfico
            public List<double?> Value = new List<double?>();      // Y (valor original da amostra)
            public List<int> Source = new List<int>();             // posição original da amostra na entrada
            public int Outside;                                     // amostras além da tolerância (descartadas do perfil, não do conjunto original)
            public double Tolerance;                                // distância máxima à curva usada
            public bool ToleranceAuto;
            public double CurveLength;
        }

        /// <summary>Tolerância automática: metade da mediana da distância ao vizinho mais próximo (espaçamento típico da malha).</summary>
        public static double AutoTolerance(IList<Point3d> pts, bool planXY)
        {
            int n = pts.Count;
            if (n < 2) return 0.5;
            var p = pts.Select(q => planXY ? new Point3d(q.X, q.Y, 0) : q).ToArray();
            int probes = Math.Min(n, 1500); int step = Math.Max(1, n / probes);
            var nn = new List<double>();
            for (int i = 0; i < n; i += step)
            {
                double best = double.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;
                    double d = p[i].DistanceToSquared(p[j]);
                    if (d > 0 && d < best) best = d;      // ignora pontos coincidentes
                }
                if (best < double.MaxValue) nn.Add(Math.Sqrt(best));
            }
            if (nn.Count == 0) return 0.5;
            nn.Sort();
            return 0.5 * nn[nn.Count / 2];
        }

        /// <param name="tol">Distância máxima da amostra à curva (≤ 0: automática)</param>
        /// <param name="planXY">true: distâncias medidas em planta (projeta pontos e curva no plano XY); false: 3D</param>
        public static Result Extract(IList<Point3d> pts, IList<double?> vals, Curve curve, double tol, bool planXY)
        {
            if (curve == null || !curve.IsValid) throw new ArgumentException("Curva de seção inválida.");
            if (pts.Count != vals.Count) throw new ArgumentException("Pontos e valores devem ter a mesma quantidade.");
            var res = new Result();
            Curve c = curve;
            if (planXY) { var pc = Curve.ProjectToPlane(curve, Plane.WorldXY); if (pc != null) c = pc; }
            res.CurveLength = c.GetLength();
            res.ToleranceAuto = !(tol > 0);
            res.Tolerance = res.ToleranceAuto ? AutoTolerance(pts, planXY) : tol;
            for (int i = 0; i < pts.Count; i++)
            {
                var p = planXY ? new Point3d(pts[i].X, pts[i].Y, 0) : pts[i];
                if (!p.IsValid || !c.ClosestPoint(p, out double t)) { res.Outside++; continue; }
                if (p.DistanceTo(c.PointAt(t)) > res.Tolerance) { res.Outside++; continue; }
                double along = t <= c.Domain.Min ? 0.0 : c.GetLength(new Interval(c.Domain.Min, t));
                res.Distance.Add(along); res.Value.Add(vals[i]); res.Source.Add(i);
            }
            return res;
        }
    }
}
