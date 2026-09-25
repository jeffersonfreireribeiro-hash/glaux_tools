using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class CovarianceCorrelation_Component : GH_Component
    {
        public CovarianceCorrelation_Component()
            : base(
                "Covariance & Correlation",
                "CovCorr",
                "Calcula a Correlação Linear de Pearson (r), Correlação de Postos de Spearman (ρ) e a Covariância Amostral/Populacional entre dois conjuntos pareados de dados.",
                "Glaux Tools",
                "Evaluation")
        {
        }

        public override Guid ComponentGuid => new Guid("0c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("List X", "X", "Primeira variável / conjunto de dados numéricos (X).", GH_ParamAccess.list);
            pManager.AddNumberParameter("List Y", "Y", "Segunda variável / conjunto de dados pareados (Y). Deve possuir o mesmo tamanho que X.", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Pearson r", "r", "Coeficiente de correlação linear de Pearson (-1.0 a +1.0).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Spearman ρ", "ρ", "Coeficiente de correlação monotônica de postos de Spearman (-1.0 a +1.0).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Sample Cov", "Cov", "Covariância amostral Cov(X,Y) com divisor (N-1).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Pop Cov", "σxy", "Covariância populacional Cov(X,Y) com divisor N.", GH_ParamAccess.item);
            pManager.AddNumberParameter("R-Squared", "r²", "Coeficiente de determinação linear r² (0.0 a 1.0).", GH_ParamAccess.item);
            pManager.AddTextParameter("Interpretation", "Desc", "Diagnóstico qualitativo da força e direção da correlação.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rawX = new List<double>();
            var rawY = new List<double>();

            if (!DA.GetDataList(0, rawX) || rawX == null || rawX.Count == 0)
            {
                this.Message = "Sem Dados X";
                return;
            }
            if (!DA.GetDataList(1, rawY) || rawY == null || rawY.Count == 0)
            {
                this.Message = "Sem Dados Y";
                return;
            }

            if (rawX.Count != rawY.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"As listas X e Y possuem tamanhos diferentes (X = {rawX.Count}, Y = {rawY.Count}). Elas devem ser pareadas 1:1.");
                this.Message = "Tamanhos ≠";
                return;
            }

            int total = rawX.Count;
            var cleanX = new List<double>();
            var cleanY = new List<double>();

            for (int i = 0; i < total; i++)
            {
                double vx = rawX[i];
                double vy = rawY[i];
                if (!double.IsNaN(vx) && !double.IsInfinity(vx) && !double.IsNaN(vy) && !double.IsInfinity(vy))
                {
                    cleanX.Add(vx);
                    cleanY.Add(vy);
                }
            }

            int n = cleanX.Count;
            if (n < 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "São necessários no mínimo 2 pares de números válidos para covariância/correlação.");
                this.Message = "N < 2";
                return;
            }

            // 1. Médias
            double sumX = 0.0, sumY = 0.0;
            for (int i = 0; i < n; i++)
            {
                sumX += cleanX[i];
                sumY += cleanY[i];
            }
            double meanX = sumX / n;
            double meanY = sumY / n;

            // 2. Covariância e Pearson r
            double ssX = 0.0, ssY = 0.0, ssXY = 0.0;
            for (int i = 0; i < n; i++)
            {
                double dx = cleanX[i] - meanX;
                double dy = cleanY[i] - meanY;
                ssX += dx * dx;
                ssY += dy * dy;
                ssXY += dx * dy;
            }

            double sampleCov = ssXY / (n - 1);
            double popCov = ssXY / n;

            double denom = Math.Sqrt(ssX * ssY);
            double pearsonR = (denom > 1e-15) ? (ssXY / denom) : 0.0;
            if (pearsonR > 1.0) pearsonR = 1.0;
            if (pearsonR < -1.0) pearsonR = -1.0;

            double rSquared = pearsonR * pearsonR;

            // 3. Spearman Rank Correlation (ρ)
            double spearmanRho = CalculateSpearmanRho(cleanX, cleanY, n);

            // 4. Interpretação Qualitativa
            string strengthText;
            double absR = Math.Abs(pearsonR);
            if (absR >= 0.90) strengthText = "Muito Forte";
            else if (absR >= 0.70) strengthText = "Forte";
            else if (absR >= 0.50) strengthText = "Moderada";
            else if (absR >= 0.30) strengthText = "Fraca";
            else strengthText = "Muito Fraca / Desprezível";

            string dirText = (pearsonR > 0.01) ? "Positiva" : (pearsonR < -0.01 ? "Negativa" : "Nula");
            string interp = $"Pearson: Correlação {strengthText} {dirText} (r = {pearsonR:F3}, r² = {rSquared:F3})\nSpearman: Relação Monotônica ρ = {spearmanRho:F3}\nCovariância: {sampleCov:G4} (Amostra) | N = {n}";

            DA.SetData(0, pearsonR);
            DA.SetData(1, spearmanRho);
            DA.SetData(2, sampleCov);
            DA.SetData(3, popCov);
            DA.SetData(4, rSquared);
            DA.SetData(5, interp);

            this.Message = $"r = {pearsonR:F3}\nρ = {spearmanRho:F3} (N={n})";
        }

        private static double CalculateSpearmanRho(List<double> x, List<double> y, int n)
        {
            var rankX = GetRanks(x);
            var rankY = GetRanks(y);

            double sumRx = 0.0, sumRy = 0.0;
            for (int i = 0; i < n; i++)
            {
                sumRx += rankX[i];
                sumRy += rankY[i];
            }
            double meanRx = sumRx / n;
            double meanRy = sumRy / n;

            double ssRx = 0.0, ssRy = 0.0, ssRxy = 0.0;
            for (int i = 0; i < n; i++)
            {
                double dx = rankX[i] - meanRx;
                double dy = rankY[i] - meanRy;
                ssRx += dx * dx;
                ssRy += dy * dy;
                ssRxy += dx * dy;
            }

            double denom = Math.Sqrt(ssRx * ssRy);
            if (denom < 1e-15) return 0.0;

            double rho = ssRxy / denom;
            if (rho > 1.0) rho = 1.0;
            if (rho < -1.0) rho = -1.0;
            return rho;
        }

        private static double[] GetRanks(List<double> values)
        {
            int n = values.Count;
            var indexed = new List<KeyValuePair<double, int>>(n);
            for (int i = 0; i < n; i++)
            {
                indexed.Add(new KeyValuePair<double, int>(values[i], i));
            }

            // Ordenar por valor
            indexed.Sort((a, b) => a.Key.CompareTo(b.Key));

            double[] ranks = new double[n];
            int idx = 0;
            while (idx < n)
            {
                int end = idx;
                while (end < n - 1 && Math.Abs(indexed[end + 1].Key - indexed[idx].Key) < 1e-12)
                {
                    end++;
                }

                // Posto médio para empates (Tied ranks)
                double avgRank = (idx + 1 + end + 1) * 0.5;
                for (int j = idx; j <= end; j++)
                {
                    ranks[indexed[j].Value] = avgRank;
                }
                idx = end + 1;
            }

            return ranks;
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.CovarianceCorrelation;
    }
}
