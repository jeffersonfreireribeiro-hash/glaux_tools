using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class ModelEvaluation_Component : GH_Component
    {
        public ModelEvaluation_Component()
            : base(
                "Model Metrics (RMSE, MAE, R²)",
                "Metrics",
                "Avalia a precisão e erro entre dados simulados/preditos e dados reais medidos em campo: RMSE, MAE, R² (Coeficiente de Determinação), MAPE %, Viés (Bias) e Erro Máximo.",
                "Glaux Tools",
                "Evaluation")
        {
        }

        public override Guid ComponentGuid => new Guid("4a5b6c7d-8e9f-0a1b-2c3d-4e5f6a7b8c9d");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Simulated / Pred", "Y_sim", "Valores simulados, preditos ou estimados pelo modelo.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Measured / True", "Y_true", "Valores reais medidos em campo ou de referência (Ground Truth). Deve possuir o mesmo tamanho que Y_sim.", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("RMSE", "RMSE", "Root Mean Square Error: raiz do erro quadrático médio (penaliza grandes erros).", GH_ParamAccess.item);
            pManager.AddNumberParameter("MAE", "MAE", "Mean Absolute Error: erro médio absoluto direto.", GH_ParamAccess.item);
            pManager.AddNumberParameter("R-Squared (R²)", "R²", "Coeficiente de Determinação R² (1 - SS_res / SS_tot). Indica a proporção da variância explicada pelo modelo.", GH_ParamAccess.item);
            pManager.AddNumberParameter("MAPE %", "MAPE", "Mean Absolute Percentage Error: erro percentual absoluto médio (%).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Bias / ME", "Bias", "Viés médio (Mean Error = Σ(Y_sim - Y_true) / N). Positivo = superestimação, Negativo = subestimação.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Max Error", "MaxErr", "Maior erro absoluto pontual encontrado no conjunto.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Count", "N", "Número de pares comparados válidos.", GH_ParamAccess.item);
            pManager.AddTextParameter("Report", "Rep", "Relatório de desempenho e qualidade de ajuste do modelo.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rawSim = new List<double>();
            var rawTrue = new List<double>();

            if (!DA.GetDataList(0, rawSim) || rawSim == null || rawSim.Count == 0)
            {
                this.Message = "Sem Dados Sim";
                return;
            }
            if (!DA.GetDataList(1, rawTrue) || rawTrue == null || rawTrue.Count == 0)
            {
                this.Message = "Sem Dados True";
                return;
            }

            if (rawSim.Count != rawTrue.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Tamanhos incompatíveis: Y_sim ({rawSim.Count}) != Y_true ({rawTrue.Count}).");
                this.Message = "Tamanhos ≠";
                return;
            }

            int total = rawSim.Count;
            var sim = new List<double>();
            var target = new List<double>();

            for (int i = 0; i < total; i++)
            {
                double s = rawSim[i];
                double t = rawTrue[i];
                if (!double.IsNaN(s) && !double.IsInfinity(s) && !double.IsNaN(t) && !double.IsInfinity(t))
                {
                    sim.Add(s);
                    target.Add(t);
                }
            }

            int n = sim.Count;
            if (n < 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "São necessários no mínimo 2 pares de valores válidos para calcular as métricas de aderência.");
                this.Message = "N < 2";
                return;
            }

            // 1. Média do Ground Truth (Y_true)
            double sumTrue = 0.0;
            for (int i = 0; i < n; i++) sumTrue += target[i];
            double meanTrue = sumTrue / n;

            // 2. Acumuladores de Erro
            double ssRes = 0.0;
            double ssTot = 0.0;
            double sumAbsErr = 0.0;
            double sumSignedErr = 0.0;
            double sumPercentErr = 0.0;
            int mapeValidCount = 0;
            double maxAbsErr = 0.0;

            for (int i = 0; i < n; i++)
            {
                double ySim = sim[i];
                double yTrue = target[i];

                double diff = ySim - yTrue;
                double absDiff = Math.Abs(diff);
                double dTot = yTrue - meanTrue;

                ssRes += diff * diff;
                ssTot += dTot * dTot;
                sumAbsErr += absDiff;
                sumSignedErr += diff;

                if (absDiff > maxAbsErr) maxAbsErr = absDiff;

                if (Math.Abs(yTrue) > 1e-12)
                {
                    sumPercentErr += (absDiff / Math.Abs(yTrue)) * 100.0;
                    mapeValidCount++;
                }
            }

            // 3. Métricas
            double mse = ssRes / n;
            double rmse = Math.Sqrt(mse);
            double mae = sumAbsErr / n;
            double bias = sumSignedErr / n;
            double mape = (mapeValidCount > 0) ? (sumPercentErr / mapeValidCount) : double.NaN;

            // R² = 1 - (SS_res / SS_tot)
            double rSquared;
            if (ssTot < 1e-15)
            {
                rSquared = (ssRes < 1e-15) ? 1.0 : 0.0;
            }
            else
            {
                rSquared = 1.0 - (ssRes / ssTot);
            }

            // 4. Diagnóstico de Qualidade do Ajuste
            string fitQuality;
            if (rSquared >= 0.95) fitQuality = "Ajuste Excelente (R² ≥ 0.95)";
            else if (rSquared >= 0.80) fitQuality = "Bom Ajuste (R² ≥ 0.80)";
            else if (rSquared >= 0.60) fitQuality = "Ajuste Moderado (R² ≥ 0.60)";
            else if (rSquared >= 0.0) fitQuality = "Ajuste Pobre (R² < 0.60)";
            else fitQuality = "Modelo Pior que a Média Simples (R² < 0)";

            string biasDesc = (Math.Abs(bias) < 1e-6) ? "Neutro / Sem viés" : (bias > 0 ? "Superestimação sistemática" : "Subestimação sistemática");

            string report = $"Model Evaluation Summary (N = {n}):\n----------------------------------------\nR² (Coef. Determinação): {rSquared:F4} -> {fitQuality}\nRMSE (Erro Quadrático Médio): {rmse:G4}\nMAE (Erro Absoluto Médio): {mae:G4}\nMAPE: {(double.IsNaN(mape) ? "N/A" : $"{mape:F2}%")}\nViés Médio (Bias/ME): {bias:G4} ({biasDesc})\nErro Máximo Pontual: {maxAbsErr:G4}";

            DA.SetData(0, rmse);
            DA.SetData(1, mae);
            DA.SetData(2, rSquared);
            DA.SetData(3, mape);
            DA.SetData(4, bias);
            DA.SetData(5, maxAbsErr);
            DA.SetData(6, n);
            DA.SetData(7, report);

            this.Message = $"R²: {rSquared:F3}\nRMSE: {rmse:F2} | MAE: {mae:F2}";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.ModelEvaluation;
    }
}
