using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class SkewnessKurtosis_Component : GH_Component
    {
        public SkewnessKurtosis_Component()
            : base(
                "Skewness & Kurtosis",
                "SkewKurt",
                "Calcula a Assimetria (Skewness g₁) e Curtose (Kurtosis g₂) para avaliar a forma da distribuição e proximidade de uma curva Normal.",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("1d2e3f4a-5b6c-7d8e-9f0a-1b2c3d4e5f6a");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Values", "V", "Conjunto de dados numéricos (amostra).", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Skewness", "γ₁", "Coeficiente de assimetria amostral ajustado de Fisher-Pearson (0 = simétrico, >0 cauda à direita, <0 cauda à esquerda).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Excess Kurtosis", "γ₂", "Curtose em excesso amostral (Normal = 0, >0 leptocúrtica/caudas pesadas, <0 platicúrtica/achatada).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Raw Kurtosis", "β₂", "Curtose padrão / bruta (Distribuição Normal = 3.0).", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Is Normal?", "Norm?", "Avaliação preliminar de normalidade (|γ₁| ≤ 0.5 e |γ₂| ≤ 1.0).", GH_ParamAccess.item);
            pManager.AddTextParameter("Interpretation", "Desc", "Diagnóstico descritivo detalhado da assimetria e curvatura da distribuição.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rawList = new List<double>();
            if (!DA.GetDataList(0, rawList) || rawList == null || rawList.Count == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            var list = new List<double>();
            for (int i = 0; i < rawList.Count; i++)
            {
                double v = rawList[i];
                if (!double.IsNaN(v) && !double.IsInfinity(v)) list.Add(v);
            }

            int n = list.Count;
            if (n < 4)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "São necessários no mínimo 4 números para cálculo de assimetria e curtose amostrais ajustadas.");
                this.Message = "N < 4";
                return;
            }

            // 1. Média
            double sum = 0.0;
            for (int i = 0; i < n; i++) sum += list[i];
            double mean = sum / n;

            // 2. Momentos centrais (m2, m3, m4)
            double m2 = 0.0, m3 = 0.0, m4 = 0.0;
            for (int i = 0; i < n; i++)
            {
                double d = list[i] - mean;
                double d2 = d * d;
                m2 += d2;
                m3 += d2 * d;
                m4 += d2 * d2;
            }
            m2 /= n;
            m3 /= n;
            m4 /= n;

            if (m2 < 1e-15)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Todos os valores são idênticos (variância zero).");
                this.Message = "Variância = 0";
                return;
            }

            // 3. Coeficiente de Assimetria Amostral Ajustado (Fisher-Pearson g1)
            // g1 = (sqrt(n*(n-1)) / (n-2)) * (m3 / m2^(3/2))
            double g1 = (Math.Sqrt(n * (n - 1.0)) / (n - 2.0)) * (m3 / Math.Pow(m2, 1.5));

            // 4. Curtose em Excesso Amostral Ajustada (Fisher-Pearson g2)
            // g2 = ((n-1) / ((n-2)*(n-3))) * ((n+1)*(m4 / m2^2) - 3*(n-1))
            double g2 = ((n - 1.0) / ((n - 2.0) * (n - 3.0))) * ((n + 1.0) * (m4 / (m2 * m2)) - 3.0 * (n - 1.0));
            double rawKurtosis = g2 + 3.0;

            // 5. Diagnóstico e Interpretação
            string skewDesc;
            if (Math.Abs(g1) < 0.20) skewDesc = "Distribuição Altamente Simétrica";
            else if (g1 >= 0.20 && g1 <= 0.80) skewDesc = "Assimetria Positiva Moderada (Cauda à Direita)";
            else if (g1 > 0.80) skewDesc = "Forte Assimetria Positiva (Cauda Longa à Direita)";
            else if (g1 <= -0.20 && g1 >= -0.80) skewDesc = "Assimetria Negativa Moderada (Cauda à Esquerda)";
            else skewDesc = "Forte Assimetria Negativa (Cauda Longa à Esquerda)";

            string kurtDesc;
            if (Math.Abs(g2) < 0.50) kurtDesc = "Mesocúrtica (Aproximadamente Normal)";
            else if (g2 > 0.50) kurtDesc = "Leptocúrtica (Pico Acentuado, Caudas Pesadas/Outliers)";
            else kurtDesc = "Platicúrtica (Achatada, Caudas Leves)";

            bool isNormalLike = (Math.Abs(g1) <= 0.50) && (Math.Abs(g2) <= 1.0);
            string normalityText = isNormalLike ? "Compatível com Distribuição Normal" : "Desvia de uma Distribuição Normal";

            double ses = Math.Sqrt(6.0 / n); // Erro padrão da assimetria
            double sek = Math.Sqrt(24.0 / n); // Erro padrão da curtose

            string report = $"Assimetria (Skewness): {g1:F3} (Erro Padrão ±{ses:F2}) -> {skewDesc}\nCurtose em Excesso: {g2:F3} (Erro Padrão ±{sek:F2}) -> {kurtDesc}\nCurtose Bruta (β₂): {rawKurtosis:F3} (Normal = 3.0)\nAvaliação: {normalityText} (N = {n})";

            DA.SetData(0, g1);
            DA.SetData(1, g2);
            DA.SetData(2, rawKurtosis);
            DA.SetData(3, isNormalLike);
            DA.SetData(4, report);

            this.Message = $"γ₁: {g1:F2} | γ₂: {g2:F2}\n{(isNormalLike ? "Normal-like" : "Não-Normal")}";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.SkewnessKurtosis;
    }
}
