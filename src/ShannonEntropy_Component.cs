using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class ShannonEntropy_Component : GH_Component
    {
        public ShannonEntropy_Component()
            : base(
                "Shannon Entropy",
                "Entropy",
                "Calcula a Entropia de Shannon H(X) = -Σ p(x) log p(x), a incerteza máxima H_max, a entropia normalizada (eficiência de informação) e a perplexidade para conjuntos de dados ou vetores de probabilidade.",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("5e6f7a8b-9c0d-1e2f-3a4b-5c6d7e8f9a0b");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Values / Probs", "X", "Conjunto de dados numéricos (amostra bruta, contagens ou vetor de probabilidades).", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Is Probability Vector", "IsProb", "Se True, interpreta a entrada diretamente como probabilidades p_i. Se False, calcula frequências e probabilidades a partir dos dados.", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("Log Base", "Base", "Base do logaritmo:\n0 = Base 2 (Bits / Shannons)\n1 = Base e (Nats / Log natural)\n2 = Base 10 (Hartleys / Dits)", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Bin Count", "B", "Número de classes/bins para dados numéricos contínuos (use 0 para valores únicos discretos).", GH_ParamAccess.item, 0);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Entropy H(X)", "H", "Entropia de Shannon H(X) = -Σ p_i log(p_i).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Max Entropy", "H_max", "Entropia máxima teórica para K estados equiprováveis (H_max = log(K)).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Normalized Entropy (η)", "η", "Entropia normalizada / Eficiência de informação (H / H_max, de 0.0 a 1.0).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Perplexity", "Perp", "Perplexidade base^H(X): número efetivo de estados equiprováveis.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Probabilities", "P", "Vetor de probabilidades p_i normalizado utilizado no cálculo.", GH_ParamAccess.list);
            pManager.AddTextParameter("Report", "Desc", "Diagnóstico e interpretação da incerteza e diversidade da distribuição.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rawList = new List<double>();
            if (!DA.GetDataList(0, rawList) || rawList == null || rawList.Count == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            bool isProb = false;
            DA.GetData(1, ref isProb);

            int logBase = 0;
            DA.GetData(2, ref logBase);

            int binCount = 0;
            DA.GetData(3, ref binCount);

            var validVals = new List<double>();
            for (int i = 0; i < rawList.Count; i++)
            {
                double v = rawList[i];
                if (!double.IsNaN(v) && !double.IsInfinity(v)) validVals.Add(v);
            }

            if (validVals.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum valor numérico finito válido fornecido.");
                this.Message = "N = 0";
                return;
            }

            List<double> probs = new List<double>();

            if (isProb)
            {
                // Normalizar probabilidades para somar 1.0
                double sum = 0.0;
                for (int i = 0; i < validVals.Count; i++)
                {
                    double p = Math.Max(0.0, validVals[i]);
                    sum += p;
                }

                if (sum < 1e-15)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A soma das probabilidades fornecidas é zero.");
                    return;
                }

                for (int i = 0; i < validVals.Count; i++)
                {
                    double p = Math.Max(0.0, validVals[i]) / sum;
                    if (p > 0.0) probs.Add(p);
                }
            }
            else // Calcular frequências discretas ou por Bins
            {
                int n = validVals.Count;
                if (binCount <= 0) // Discreto
                {
                    var freqMap = new Dictionary<double, int>();
                    for (int i = 0; i < n; i++)
                    {
                        double v = validVals[i];
                        freqMap.TryGetValue(v, out int count);
                        freqMap[v] = count + 1;
                    }
                    foreach (var kvp in freqMap)
                    {
                        probs.Add((double)kvp.Value / n);
                    }
                }
                else // Bins contínuos
                {
                    double min = validVals.Min();
                    double max = validVals.Max();
                    double range = max - min;
                    if (range < 1e-12) range = 1.0;

                    double binWidth = range / binCount;
                    int[] bins = new int[binCount];
                    for (int i = 0; i < n; i++)
                    {
                        int bIdx = (int)Math.Floor((validVals[i] - min) / binWidth);
                        if (bIdx >= binCount) bIdx = binCount - 1;
                        if (bIdx < 0) bIdx = 0;
                        bins[bIdx]++;
                    }
                    for (int b = 0; b < binCount; b++)
                    {
                        if (bins[b] > 0) probs.Add((double)bins[b] / n);
                    }
                }
            }

            int k = probs.Count;
            if (k == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhuma probabilidade não nula encontrada.");
                return;
            }

            // Função de log de acordo com a base
            Func<double, double> logFn = logBase switch
            {
                1 => Math.Log,                      // Natural (Nats)
                2 => Math.Log10,                    // Base 10 (Hartleys)
                _ => p => Math.Log(p, 2.0)          // Base 2 (Bits / Shannons)
            };

            double baseVal = logBase switch
            {
                1 => Math.E,
                2 => 10.0,
                _ => 2.0
            };

            string unitName = logBase switch
            {
                1 => "nats",
                2 => "dits",
                _ => "bits"
            };

            // Cálculo da Entropia H(X) = -Σ p_i log(p_i)
            double entropy = 0.0;
            for (int i = 0; i < k; i++)
            {
                double p = probs[i];
                if (p > 1e-15)
                {
                    entropy -= p * logFn(p);
                }
            }

            double maxEntropy = (k > 1) ? logFn(k) : 0.0;
            double normalizedEntropy = (maxEntropy > 1e-15) ? (entropy / maxEntropy) : (k == 1 ? 0.0 : 1.0);
            if (normalizedEntropy > 1.0) normalizedEntropy = 1.0;
            if (normalizedEntropy < 0.0) normalizedEntropy = 0.0;

            double perplexity = Math.Pow(baseVal, entropy);

            string interp;
            if (normalizedEntropy < 0.20) interp = "Altamente Determinístico / Baixa Incerteza";
            else if (normalizedEntropy < 0.70) interp = "Incerteza Moderada / Distribuição Agrupada";
            else if (normalizedEntropy < 0.95) interp = "Alta Diversidade / Distribuição Dispersa";
            else interp = "Quase Uniforme / Incerteza Máxima (Equiprovável)";

            string report = $"Shannon Entropy Analysis:\n-------------------------\nEntropia H(X): {entropy:F4} {unitName}\nEntropia Máxima H_max: {maxEntropy:F4} {unitName} (para K = {k} estados)\nEficiência / Incerteza (η): {normalizedEntropy * 100.0:F2}%\nPerplexidade: {perplexity:F2} estados equivalentes\nDiagnóstico: {interp}";

            DA.SetData(0, entropy);
            DA.SetData(1, maxEntropy);
            DA.SetData(2, normalizedEntropy);
            DA.SetData(3, perplexity);
            DA.SetDataList(4, probs);
            DA.SetData(5, report);

            this.Message = $"H: {entropy:F3} {unitName}\nη = {normalizedEntropy * 100.0:F1}%";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.ShannonEntropy;
    }
}
