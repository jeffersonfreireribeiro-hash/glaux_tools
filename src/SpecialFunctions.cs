using System;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Funções matemáticas especiais de alta precisão (Lanczos Gamma, Fração Contínua de Lentz para Beta e Gamma Incompletas)
    /// compatíveis com as funções estatísticas do Microsoft Excel 2010/2013+.
    /// </summary>
    public static class SpecialFunctions
    {
        private static readonly double[] LanczosCoeffs = new double[]
        {
            0.99999999999980993,
            676.5209384912358,
            -1259.1392167224028,
            771.32342877765313,
            -176.61502916214059,
            12.507343278686905,
            -0.13857109526572012,
            9.9843695780195716e-6,
            1.5056327351493116e-7
        };

        /// <summary>
        /// Logaritmo natural da função Gamma: ln(Γ(x)), aproximação de Lanczos (g=7, n=9).
        /// </summary>
        public static double LogGamma(double x)
        {
            if (double.IsNaN(x) || x <= 0)
            {
                if (x < 0.5 && x > -50.0 && Math.Abs(Math.Sin(Math.PI * x)) > 1e-15)
                {
                    // Fórmula de reflexão: Γ(1-z) Γ(z) = π / sin(π z)
                    return Math.Log(Math.PI / Math.Abs(Math.Sin(Math.PI * x))) - LogGamma(1.0 - x);
                }
                return double.NaN;
            }

            x -= 1.0;
            double a = LanczosCoeffs[0];
            double t = x + 7.5;
            for (int i = 1; i < LanczosCoeffs.Length; i++)
            {
                a += LanczosCoeffs[i] / (x + i);
            }
            return 0.5 * Math.Log(2.0 * Math.PI) + (x + 0.5) * Math.Log(t) - t + Math.Log(a);
        }

        /// <summary>
        /// Função Gamma Γ(x).
        /// </summary>
        public static double Gamma(double x)
        {
            return Math.Exp(LogGamma(x));
        }

        #region Beta Distribution & Incomplete Beta

        private static double BetaContinuedFraction(double a, double b, double x, int maxIter = 200, double eps = 1e-15)
        {
            double qab = a + b;
            double qap = a + 1.0;
            double qam = a - 1.0;
            double c = 1.0;
            double d = 1.0 - qab * x / qap;
            if (Math.Abs(d) < 1e-30) d = 1e-30;
            d = 1.0 / d;
            double h = d;

            for (int m = 1; m <= maxIter; m++)
            {
                int m2 = 2 * m;
                // Passo par
                double aa = m * (b - m) * x / ((qam + m2) * (a + m2));
                d = 1.0 + aa * d;
                if (Math.Abs(d) < 1e-30) d = 1e-30;
                c = 1.0 + aa / c;
                if (Math.Abs(c) < 1e-30) c = 1e-30;
                d = 1.0 / d;
                h *= d * c;

                // Passo ímpar
                aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
                d = 1.0 + aa * d;
                if (Math.Abs(d) < 1e-30) d = 1e-30;
                c = 1.0 + aa / c;
                if (Math.Abs(c) < 1e-30) c = 1e-30;
                d = 1.0 / d;
                double del = d * c;
                h *= del;

                if (Math.Abs(del - 1.0) < eps)
                    break;
            }
            return h;
        }

        /// <summary>
        /// Função Beta regularizada incompleta: I_x(a, b) = B(x; a, b) / B(a, b).
        /// </summary>
        public static double IncompleteBeta(double x, double a, double b)
        {
            if (double.IsNaN(x) || double.IsNaN(a) || double.IsNaN(b)) return double.NaN;
            if (x <= 0.0) return 0.0;
            if (x >= 1.0) return 1.0;
            if (a <= 0.0 || b <= 0.0) return double.NaN;

            double logBeta = LogGamma(a) + LogGamma(b) - LogGamma(a + b);
            double factor = Math.Exp(-logBeta + a * Math.Log(x) + b * Math.Log(1.0 - x));

            if (x < (a + 1.0) / (a + b + 2.0))
            {
                return factor * BetaContinuedFraction(a, b, x) / a;
            }
            else
            {
                return 1.0 - factor * BetaContinuedFraction(b, a, 1.0 - x) / b;
            }
        }

        /// <summary>
        /// Densidade de Probabilidade Beta (PDF).
        /// </summary>
        public static double BetaPdf(double x, double a, double b, double lower = 0.0, double upper = 1.0)
        {
            if (upper <= lower || x < lower || x > upper || a <= 0 || b <= 0) return 0.0;
            double scale = upper - lower;
            double z = (x - lower) / scale;
            if (z <= 0.0 || z >= 1.0)
            {
                if (z == 0.0 && a == 1.0) return b / scale;
                if (z == 1.0 && b == 1.0) return a / scale;
                return 0.0;
            }

            double logVal = -LogGamma(a) - LogGamma(b) + LogGamma(a + b) + (a - 1.0) * Math.Log(z) + (b - 1.0) * Math.Log(1.0 - z);
            return Math.Exp(logVal) / scale;
        }

        /// <summary>
        /// Distribuição Cumulativa Beta (CDF) correspondente a BETA.DIST(x, a, b, True, A, B).
        /// </summary>
        public static double BetaCdf(double x, double a, double b, double lower = 0.0, double upper = 1.0)
        {
            if (upper <= lower || a <= 0 || b <= 0) return double.NaN;
            if (x <= lower) return 0.0;
            if (x >= upper) return 1.0;
            double z = (x - lower) / (upper - lower);
            return IncompleteBeta(z, a, b);
        }

        /// <summary>
        /// Inverso da Distribuição Cumulativa Beta correspondente a BETA.INV(p, a, b, A, B).
        /// </summary>
        public static double BetaInv(double p, double a, double b, double lower = 0.0, double upper = 1.0, double tol = 1e-12)
        {
            if (upper <= lower || a <= 0 || b <= 0 || p < 0.0 || p > 1.0) return double.NaN;
            if (p == 0.0) return lower;
            if (p == 1.0) return upper;

            double scale = upper - lower;
            double low = 0.0;
            double high = 1.0;
            double z = a / (a + b); // palpite inicial pela média

            for (int iter = 0; iter < 100; iter++)
            {
                double val = IncompleteBeta(z, a, b);
                double err = val - p;
                if (Math.Abs(err) < tol)
                    return lower + z * scale;

                if (err > 0)
                    high = z;
                else
                    low = z;

                double pdf = BetaPdf(z, a, b, 0.0, 1.0);
                if (pdf > 1e-14)
                {
                    double zNew = z - err / pdf;
                    if (zNew > low && zNew < high)
                    {
                        z = zNew;
                        continue;
                    }
                }
                z = 0.5 * (low + high);
            }

            return lower + z * scale;
        }

        #endregion

        #region Binomial Distribution

        /// <summary>
        /// Probabilidade da distribuição binomial pontual (PMF) correspondente a BINOM.DIST(k, n, p, False).
        /// </summary>
        public static double BinomPmf(int k, int n, double p)
        {
            if (k < 0 || k > n || p < 0.0 || p > 1.0 || n < 0) return 0.0;
            if (p == 0.0) return k == 0 ? 1.0 : 0.0;
            if (p == 1.0) return k == n ? 1.0 : 0.0;

            double lcoeff = LogGamma(n + 1.0) - LogGamma(k + 1.0) - LogGamma(n - k + 1.0);
            return Math.Exp(lcoeff + k * Math.Log(p) + (n - k) * Math.Log(1.0 - p));
        }

        /// <summary>
        /// Probabilidade cumulativa binomial P(X <= k) correspondente a BINOM.DIST(k, n, p, True).
        /// </summary>
        public static double BinomCdf(int k, int n, double p)
        {
            if (p < 0.0 || p > 1.0 || n < 0) return double.NaN;
            if (k < 0) return 0.0;
            if (k >= n) return 1.0;
            return IncompleteBeta(1.0 - p, n - k, k + 1.0);
        }

        /// <summary>
        /// Probabilidade binomial de intervalo P(s1 <= X <= s2) correspondente a BINOM.DIST.INTERVALO / BINOM.DIST.RANGE.
        /// </summary>
        public static double BinomRange(int n, double p, int s1, int s2)
        {
            if (s1 > s2) return 0.0;
            return BinomCdf(s2, n, p) - BinomCdf(s1 - 1, n, p);
        }

        /// <summary>
        /// Menor inteiro k onde P(X <= k) >= criterion, correspondente a BINOM.INV(n, p, alpha).
        /// </summary>
        public static int BinomInv(int n, double p, double criterion)
        {
            if (n < 0 || p < 0.0 || p > 1.0 || criterion <= 0.0) return 0;
            if (criterion >= 1.0) return n;

            int low = 0;
            int high = n;
            int ans = n;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                double cdf = BinomCdf(mid, n, p);
                if (cdf >= criterion - 1e-12)
                {
                    ans = mid;
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1;
                }
            }
            return ans;
        }

        #endregion

        #region Chi-Square Distribution & Incomplete Gamma

        private static double GammaSeries(double a, double x, double eps = 1e-15)
        {
            double gln = LogGamma(a);
            double sum = 1.0 / a;
            double del = sum;
            double ap = a;
            for (int n = 1; n <= 200; n++)
            {
                ap += 1.0;
                del *= x / ap;
                sum += del;
                if (Math.Abs(del) < Math.Abs(sum) * eps)
                    break;
            }
            return sum * Math.Exp(-x + a * Math.Log(x) - gln);
        }

        private static double GammaContinuedFraction(double a, double x, double eps = 1e-15)
        {
            double gln = LogGamma(a);
            double b = x + 1.0 - a;
            double c = 1.0 / 1e-30;
            double d = 1.0 / b;
            double h = d;

            for (int i = 1; i <= 200; i++)
            {
                double an = -i * (i - a);
                b += 2.0;
                d = an * d + b;
                if (Math.Abs(d) < 1e-30) d = 1e-30;
                c = b + an / c;
                if (Math.Abs(c) < 1e-30) c = 1e-30;
                d = 1.0 / d;
                double del = d * c;
                h *= del;
                if (Math.Abs(del - 1.0) < eps)
                    break;
            }
            return Math.Exp(-x + a * Math.Log(x) - gln) * h;
        }

        /// <summary>
        /// Função Gamma regularizada incompleta P(a, x) = γ(a, x) / Γ(a).
        /// </summary>
        public static double IncompleteGamma(double a, double x)
        {
            if (double.IsNaN(a) || double.IsNaN(x) || a <= 0.0 || x < 0.0) return double.NaN;
            if (x == 0.0) return 0.0;

            if (x < a + 1.0)
            {
                return GammaSeries(a, x);
            }
            else
            {
                return 1.0 - GammaContinuedFraction(a, x);
            }
        }

        /// <summary>
        /// Densidade de probabilidade Qui-Quadrado (PDF).
        /// </summary>
        public static double ChiSqPdf(double x, double df)
        {
            if (x <= 0.0 || df <= 0.0) return 0.0;
            double halfDf = df / 2.0;
            double logVal = (halfDf - 1.0) * Math.Log(x) - x / 2.0 - halfDf * Math.Log(2.0) - LogGamma(halfDf);
            return Math.Exp(logVal);
        }

        /// <summary>
        /// Distribuição Cumulativa Qui-Quadrado (CDF) correspondente a CHISQ.DIST(x, df, True).
        /// </summary>
        public static double ChiSqCdf(double x, double df)
        {
            if (x <= 0.0 || df <= 0.0) return 0.0;
            return IncompleteGamma(df / 2.0, x / 2.0);
        }

        /// <summary>
        /// Inverso da Distribuição Qui-Quadrado correspondente a CHISQ.INV(p, df).
        /// </summary>
        public static double ChiSqInv(double p, double df, double tol = 1e-12)
        {
            if (p <= 0.0 || df <= 0.0) return 0.0;
            if (p >= 1.0) return double.PositiveInfinity;

            double low = 0.0;
            double high = Math.Max(df * 5.0, 50.0);
            while (ChiSqCdf(high, df) < p)
            {
                high *= 2.0;
            }

            for (int iter = 0; iter < 100; iter++)
            {
                double mid = 0.5 * (low + high);
                double cdf = ChiSqCdf(mid, df);
                if (Math.Abs(cdf - p) < tol)
                    return mid;

                if (cdf > p)
                    high = mid;
                else
                    low = mid;
            }
            return 0.5 * (low + high);
        }

        #endregion
    }
}
