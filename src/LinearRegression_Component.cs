using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class LinearRegression_Component : GH_Component
    {
        public LinearRegression_Component()
            : base(
                "Linear Regression (OLS)",
                "LinReg",
                "Executa Regressão Linear Simples por Mínimos Quadrados Ordinários (OLS):\n" +
                "- Equação: y = β0 + β1 * x\n" +
                "- Coeficiente Angular (Slope β1) = Σ(x - x̄)(y - ȳ) / Σ(x - x̄)²\n" +
                "- Coeficiente Linear (Intercept β0) = ȳ - β1 * x̄\n" +
                "- Coeficiente de Determinação R² = 1 - SS_res / SS_tot\n" +
                "- Predições ŷ, Resíduos pontuais, Erro Padrão do Slope e Teste t de Significância.",
                "Glaux Tools",
                "Evaluation")
        {
        }

        public override Guid ComponentGuid => new Guid("9d4a9e1f-2270-4656-a41d-a4f02aa12a85");

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.LinearRegression;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("X Values", "X", "Variável independente / preditora (eixo X).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Y Values", "Y", "Variável dependente / resposta (eixo Y). Deve possuir o mesmo tamanho que X.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Eval X", "X_eval", "Valores opcionais de X para avaliação/predição de novos pontos ŷ. Se omitido, avalia sobre o próprio X de entrada.", GH_ParamAccess.list);

            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Slope (β1)", "β1", "Coeficiente angular / inclinação da reta de regressão (taxa de variação de Y por unidade de X).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Intercept (β0)", "β0", "Coeficiente linear / intercepto no eixo Y (valor estimado de Y quando X = 0).", GH_ParamAccess.item);
            pManager.AddNumberParameter("R-Squared (R²)", "R²", "Coeficiente de Determinação R² (proporção da variância de Y explicada por X).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Predicted (Ŷ)", "Ŷ", "Valores preditos ou ajustados pelo modelo linear ŷ = β0 + β1 * x.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Residuals (e)", "e", "Resíduos do ajuste: e_i = y_i - ŷ_i.", GH_ParamAccess.list);
            pManager.AddTextParameter("Equation", "Eq", "Equação matemática textual da reta ajustada: 'y = a * x + b'.", GH_ParamAccess.item);
            pManager.AddNumberParameter("p-Value", "p", "P-valor do teste t bicaudal para a inclinação β1 (H0: β1 = 0, sem relação linear).", GH_ParamAccess.item);
            pManager.AddTextParameter("Report", "Rep", "Relatório de diagnóstico detalhado da regressão.", GH_ParamAccess.item);
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
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Tamanhos incompatíveis: X ({rawX.Count}) != Y ({rawY.Count}).");
                this.Message = "Tamanhos ≠";
                return;
            }

            // Filtragem de pares válidos
            var xList = new List<double>();
            var yList = new List<double>();

            for (int i = 0; i < rawX.Count; i++)
            {
                double x = rawX[i];
                double y = rawY[i];
                if (!double.IsNaN(x) && !double.IsInfinity(x) && !double.IsNaN(y) && !double.IsInfinity(y))
                {
                    xList.Add(x);
                    yList.Add(y);
                }
            }

            int n = xList.Count;
            if (n < 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "São necessários no mínimo 2 pares de coordenadas válidos para ajustar uma reta.");
                this.Message = "N < 2";
                return;
            }

            // Médias de X e Y
            double sumX = 0.0;
            double sumY = 0.0;
            for (int i = 0; i < n; i++)
            {
                sumX += xList[i];
                sumY += yList[i];
            }
            double meanX = sumX / n;
            double meanY = sumY / n;

            // Covariância e Variância de X
            double sumCross = 0.0;
            double sumSqX = 0.0;
            double sumSqY = 0.0;

            for (int i = 0; i < n; i++)
            {
                double dx = xList[i] - meanX;
                double dy = yList[i] - meanY;
                sumCross += dx * dy;
                sumSqX += dx * dx;
                sumSqY += dy * dy;
            }

            if (sumSqX < 1e-15)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Todos os valores de X são idênticos. A reta é vertical (inclinação indefinida).");
                this.Message = "X Constante";
                return;
            }

            // Slope (β1) e Intercept (β0)
            double beta1 = sumCross / sumSqX;
            double beta0 = meanY - beta1 * meanX;

            // Avaliar Predições e Resíduos nos dados de treino
            var residuals = new List<double>(n);
            double ssRes = 0.0;

            for (int i = 0; i < n; i++)
            {
                double yHat = beta0 + beta1 * xList[i];
                double res = yList[i] - yHat;
                residuals.Add(res);
                ssRes += res * res;
            }

            // R-Squared (R²)
            double ssTot = sumSqY;
            double rSquared = ssTot > 1e-15 ? Math.Max(0.0, 1.0 - (ssRes / ssTot)) : 1.0;
            double pearsonR = (Math.Sqrt(sumSqX) * Math.Sqrt(sumSqY)) > 1e-15 ? sumCross / (Math.Sqrt(sumSqX) * Math.Sqrt(sumSqY)) : 0.0;

            // Erro Padrão do Slope e Teste t
            double df = n - 2;
            double sResidual = df > 0 ? Math.Sqrt(ssRes / df) : 0.0;
            double seBeta1 = sumSqX > 1e-15 ? sResidual / Math.Sqrt(sumSqX) : 0.0;

            double tStat = seBeta1 > 1e-15 ? beta1 / seBeta1 : 0.0;
            double pVal = df > 0 ? 2.0 * (1.0 - SpecialFunctions.StudentTCdf(Math.Abs(tStat), df)) : double.NaN;

            // Predição para novos pontos se fornecidos
            var rawEval = new List<double>();
            var predicted = new List<double>();

            if (DA.GetDataList(2, rawEval) && rawEval != null && rawEval.Count > 0)
            {
                foreach (var xEval in rawEval)
                {
                    if (!double.IsNaN(xEval) && !double.IsInfinity(xEval))
                    {
                        predicted.Add(beta0 + beta1 * xEval);
                    }
                    else
                    {
                        predicted.Add(double.NaN);
                    }
                }
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    predicted.Add(beta0 + beta1 * xList[i]);
                }
            }

            string eqSign = beta0 >= 0 ? "+" : "-";
            string equation = $"y = {beta1:F4} * x {eqSign} {Math.Abs(beta0):F4}";

            DA.SetData(0, beta1);
            DA.SetData(1, beta0);
            DA.SetData(2, rSquared);
            DA.SetDataList(3, predicted);
            DA.SetDataList(4, residuals);
            DA.SetData(5, equation);
            if (!double.IsNaN(pVal)) DA.SetData(6, pVal);

            // Relatório
            var sb = new StringBuilder();
            sb.AppendLine("=== RELATÓRIO DE REGRESSÃO LINEAR SIMPLES (OLS) ===");
            sb.AppendLine($"Equação do Modelo:        {equation}");
            sb.AppendLine($"Coeficiente Angular (β1): {beta1:F6}");
            sb.AppendLine($"Coeficiente Linear (β0):  {beta0:F6}");
            sb.AppendLine($"R² (Determinação):        {rSquared:P2} ({rSquared:F4})");
            sb.AppendLine($"Pearson r:                {pearsonR:F4}");
            sb.AppendLine($"N (Pares de Dados):       {n}");
            sb.AppendLine($"Erro Padrão Residual (s): {sResidual:F4}");
            sb.AppendLine($"Erro Padrão de β1:        {seBeta1:F4}");
            sb.AppendLine($"Estatística t do Slope:   t = {tStat:F4} (DF = {df})");
            sb.AppendLine($"P-Valor do Slope:         {(double.IsNaN(pVal) ? "N/A" : $"{pVal:E4}")} ({(pVal < 0.05 ? "Significativo p < 0.05" : "Não significativo")})");
            sb.AppendLine("===================================================");

            DA.SetData(7, sb.ToString());

            this.Message = $"R²={rSquared:F3} | β1={beta1:F2}";
        }
    }
}
