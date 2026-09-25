using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class TrimmedWinsorizedMean_Component : GH_Component
    {
        public TrimmedWinsorizedMean_Component()
            : base(
                "Trimmed & Winsorized Mean",
                "TrimWin",
                "Calcula medidas robustas de tendência central (Média Truncada / Trimmed Mean e Winsorização) para mitigar o impacto de valores extremos e caudas pesadas.",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("3f4a5b6c-7d8e-9f0a-1b2c-3d4e5f6a7b8c");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Values", "V", "Conjunto de dados numéricos.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Cutoff %", "p", "Porcentagem de corte em cada cauda (ex.: 0.10 ou 10 para 10% na cauda inferior e 10% na superior). Padrão: 0.10 (10%).", GH_ParamAccess.item, 0.10);

            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Trimmed Mean", "x̄_trim", "Média truncada (calculada após descartar p% dos menores e maiores valores).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Winsorized Mean", "x̄_win", "Média winsorizada (calculada substituindo p% dos extremos pelos valores de corte).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Trimmed Data", "D_trim", "Lista de valores restantes após a remoção dos extremos.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Winsorized Data", "D_win", "Lista de dados com os extremos substituídos pelos limites de corte.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Lower Cutoff", "L", "Valor do limite inferior de corte.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Upper Cutoff", "U", "Valor do limite superior de corte.", GH_ParamAccess.item);
            pManager.AddTextParameter("Summary", "Desc", "Resumo das estatísticas robustas calculadas.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rawList = new List<double>();
            if (!DA.GetDataList(0, rawList) || rawList == null || rawList.Count == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            double p = 0.10;
            DA.GetData(1, ref p);

            // Converter se fornecido em percentual inteiro (ex.: 10 -> 0.10)
            if (p > 1.0) p /= 100.0;
            if (p < 0.0) p = 0.0;
            if (p >= 0.50)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A porcentagem de corte em cada cauda deve ser menor que 50% (0.50).");
                return;
            }

            var list = new List<double>();
            for (int i = 0; i < rawList.Count; i++)
            {
                double v = rawList[i];
                if (!double.IsNaN(v) && !double.IsInfinity(v)) list.Add(v);
            }

            int n = list.Count;
            if (n < 3)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "São necessários no mínimo 3 valores para truncamento e winsorização.");
                this.Message = "N < 3";
                return;
            }

            // Ordenar para extrair percentis e limites
            var sorted = new List<double>(list);
            sorted.Sort();

            int k = (int)Math.Floor(n * p);
            if (k * 2 >= n) k = (n - 1) / 2;

            double lowerCutoff = sorted[k];
            double upperCutoff = sorted[n - 1 - k];

            // 1. Trimmed Mean (Remover k menores e k maiores)
            var trimmedData = new List<double>();
            double sumTrim = 0.0;
            for (int i = k; i < n - k; i++)
            {
                double val = sorted[i];
                trimmedData.Add(val);
                sumTrim += val;
            }
            double trimmedMean = (trimmedData.Count > 0) ? (sumTrim / trimmedData.Count) : sorted[k];

            // 2. Winsorized Mean (Substituir k menores por lowerCutoff e k maiores por upperCutoff)
            var winsorizedData = new List<double>(n);
            double sumWin = 0.0;
            for (int i = 0; i < n; i++)
            {
                double val = list[i];
                if (val < lowerCutoff) val = lowerCutoff;
                else if (val > upperCutoff) val = upperCutoff;

                winsorizedData.Add(val);
                sumWin += val;
            }
            double winsorizedMean = sumWin / n;

            string summary = $"Robust Estimation (p = {p * 100.0:F1}%):\nTrimmed Mean: {trimmedMean:G4} (descartados {k * 2} pontos)\nWinsorized Mean: {winsorizedMean:G4}\nCorte Inferior: {lowerCutoff:G4} | Corte Superior: {upperCutoff:G4}\nN Total: {n} (N Truncado: {trimmedData.Count})";

            DA.SetData(0, trimmedMean);
            DA.SetData(1, winsorizedMean);
            DA.SetDataList(2, trimmedData);
            DA.SetDataList(3, winsorizedData);
            DA.SetData(4, lowerCutoff);
            DA.SetData(5, upperCutoff);
            DA.SetData(6, summary);

            this.Message = $"Trim({p * 100.0:G2}%): {trimmedMean:F2}\nWin: {winsorizedMean:F2}";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.TrimmedWinsorizedMean;
    }
}
