using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    public class ConfidenceInterval_Component : GH_Component
    {
        public ConfidenceInterval_Component()
            : base(
                "Confidence Interval & t-Score",
                "ConfInterval",
                "Calcula o Intervalo de Confiança para a Média (com σ conhecido via Z-Score ou desconhecido via t de Student), Margem de Erro E, Erro Padrão SE, estatística de teste t/Z e p-valor bilateral para hipóteses.",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("2b36bd65-4767-4903-9539-d9bdec761fc3");

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.ConfidenceInterval;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Values", "V", "Amostra de dados numéricos (lista).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Confidence Level", "Conf", "Nível de confiança desejado (1 - α), por exemplo 0.90, 0.95 ou 0.99. Padrão: 0.95 (95%).", GH_ParamAccess.item, 0.95);
            pManager.AddNumberParameter("Known StdDev", "σ", "Desvio padrão populacional conhecido opcional σ. Se omitido ou <= 0, utiliza o desvio padrão amostral s com a distribuição t de Student.", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Null Mean μ0", "μ0", "Média sob a hipótese nula H0 (μ = μ0) para cálculo do t-score e z-score. Padrão: 0.0.", GH_ParamAccess.item, 0.0);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntervalParameter("Confidence Interval", "CI", "Intervalo de confiança [x̄ - E, x̄ + E] como um Interval do Grasshopper.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Margin of Error", "E", "Margem de erro: E = t_(α/2) * (s / √n) ou Z_(α/2) * (σ / √n).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Standard Error", "SE", "Erro padrão da média: SE = s / √n (ou σ / √n).", GH_ParamAccess.item);
            pManager.AddNumberParameter("t-Score / Z-Score", "Score", "Estatística de teste calculada para a média: t = (x̄ - μ0) / SE.", GH_ParamAccess.item);
            pManager.AddNumberParameter("p-Value", "p", "P-valor bilateral correspondente à hipótese nula H0: μ = μ0.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Critical Value", "Crit", "Valor crítico de corte t_(α/2) ou Z_(α/2).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Sample Mean", "x̄", "Média aritmética calculada da amostra.", GH_ParamAccess.item);
            pManager.AddTextParameter("Report", "Rep", "Relatório de inferência estatística formatado.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rawList = new List<double>();
            if (!DA.GetDataList(0, rawList) || rawList == null || rawList.Count == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            double conf = 0.95;
            DA.GetData(1, ref conf);
            double knownSigma = 0.0;
            DA.GetData(2, ref knownSigma);
            double mu0 = 0.0;
            DA.GetData(3, ref mu0);

            // Filtrar válidos
            var list = new List<double>();
            for (int i = 0; i < rawList.Count; i++)
            {
                double v = rawList[i];
                if (!double.IsNaN(v) && !double.IsInfinity(v))
                {
                    list.Add(v);
                }
            }

            int n = list.Count;
            if (n < 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "São necessários no mínimo 2 valores válidos para calcular o intervalo de confiança.");
                this.Message = "N < 2";
                return;
            }

            if (conf <= 0.0 || conf >= 1.0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "O nível de confiança deve estar entre 0.0 e 1.0 (ex.: 0.95).");
                this.Message = "Conf Inválido";
                return;
            }

            // Média amostral x̄
            double sum = 0.0;
            for (int i = 0; i < n; i++) sum += list[i];
            double mean = sum / n;

            // Variância amostral s²
            double sumSq = 0.0;
            for (int i = 0; i < n; i++)
            {
                double diff = list[i] - mean;
                sumSq += diff * diff;
            }
            double s2 = sumSq / (n - 1);
            double s = Math.Sqrt(s2);

            double alpha = 1.0 - conf;
            double alphaHalf = alpha / 2.0;

            bool useZ = knownSigma > 0.0;
            double se = useZ ? (knownSigma / Math.Sqrt(n)) : (s / Math.Sqrt(n));

            double criticalVal;
            double score;
            double pVal;
            string method;

            if (useZ)
            {
                method = "Normal (Z-Test com σ conhecido)";
                criticalVal = SpecialFunctions.NormInv(1.0 - alphaHalf, 0.0, 1.0);
                score = se > 1e-15 ? (mean - mu0) / se : 0.0;
                double cdf = SpecialFunctions.NormCdf(Math.Abs(score), 0.0, 1.0);
                pVal = 2.0 * (1.0 - cdf);
            }
            else
            {
                method = $"t de Student (DF = {n - 1}, s amostral)";
                criticalVal = SpecialFunctions.StudentTInv(1.0 - alphaHalf, n - 1);
                score = se > 1e-15 ? (mean - mu0) / se : 0.0;
                double cdf = SpecialFunctions.StudentTCdf(Math.Abs(score), n - 1);
                pVal = 2.0 * (1.0 - cdf);
            }

            double marginOfError = criticalVal * se;
            double lowerBound = mean - marginOfError;
            double upperBound = mean + marginOfError;

            var interval = new Interval(lowerBound, upperBound);

            DA.SetData(0, interval);
            DA.SetData(1, marginOfError);
            DA.SetData(2, se);
            DA.SetData(3, score);
            DA.SetData(4, pVal);
            DA.SetData(5, criticalVal);
            DA.SetData(6, mean);

            // Relatório
            var sb = new StringBuilder();
            sb.AppendLine("=== RELATÓRIO DE INFERÊNCIA E INTERVALO DE CONFIANÇA ===");
            sb.AppendLine($"Método Utilizado:        {method}");
            sb.AppendLine($"Tamanho da Amostra (n):  {n}");
            sb.AppendLine($"Média Amostral (x̄):      {mean:F4}");
            sb.AppendLine($"Desvio Padrão Utilizado: {(useZ ? $"{knownSigma:F4} (σ populacional)" : $"{s:F4} (s amostral)")}");
            sb.AppendLine($"Erro Padrão (SE):        {se:F4} (s / √n)");
            sb.AppendLine($"Nível de Confiança:      {conf:P1} (α = {alpha:F3})");
            sb.AppendLine($"Valor Crítico:           {criticalVal:F4}");
            sb.AppendLine($"Margem de Erro (E):      ±{marginOfError:F4}");
            sb.AppendLine($"Intervalo de Confiança:  [{lowerBound:F4} ; {upperBound:F4}]");
            sb.AppendLine("---------------------------------------------------------");
            sb.AppendLine($"Teste de Hipótese (H0: μ = {mu0:F4}):");
            sb.AppendLine($"Escore de Teste:         {(useZ ? "Z" : "t")} = {score:F4}");
            sb.AppendLine($"P-Valor Bilateral:       {pVal:E4} ({(pVal < alpha ? "REJEITA H0 - Diferença Significativa" : "NÃO REJEITA H0")})");
            sb.AppendLine("=========================================================");

            DA.SetData(7, sb.ToString());

            this.Message = $"CI {conf * 100:0.#}%: [{lowerBound:F2}, {upperBound:F2}]";
        }
    }
}
