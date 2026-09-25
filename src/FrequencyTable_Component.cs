using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class FrequencyTable_Component : GH_Component
    {
        public FrequencyTable_Component()
            : base(
                "Frequency Table",
                "FreqTab",
                "Gera a tabela completa de distribuição de frequências (absoluta, relativa %, acumulada) e identifica modas simples e multimodais.",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("2e3f4a5b-6c7d-8e9f-0a1b-2c3d4e5f6a7b");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Values", "V", "Conjunto de dados numéricos de entrada.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Bin Count", "B", "Quantidade de classes/faixas para dados contínuos (use 0 ou deixe em branco para valores únicos discretos exatos).", GH_ParamAccess.item, 0);

            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Modes", "Mo", "Moda(s) do conjunto (detecta distribuições unimodais, bimodais e multimodais).", GH_ParamAccess.list);
            pManager.AddTextParameter("Bins / Values", "Val", "Rótulos das classes/faixas ou valores únicos analisados.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Frequency", "f", "Frequência absoluta (contagem de ocorrências em cada classe).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Relative Freq %", "f%", "Frequência relativa percentual (f / N * 100%).", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Cumulative Freq", "F", "Frequência acumulada (soma progressiva das contagens).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Cumulative %", "F%", "Frequência relativa acumulada percentual (0 a 100%).", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rawList = new List<double>();
            if (!DA.GetDataList(0, rawList) || rawList == null || rawList.Count == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            int binCount = 0;
            DA.GetData(1, ref binCount);

            var list = new List<double>();
            for (int i = 0; i < rawList.Count; i++)
            {
                double v = rawList[i];
                if (!double.IsNaN(v) && !double.IsInfinity(v)) list.Add(v);
            }

            int n = list.Count;
            if (n == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum número válido fornecido.");
                this.Message = "N = 0";
                return;
            }

            var modes = new List<double>();
            var labels = new List<string>();
            var freqs = new List<int>();
            var relFreqs = new List<double>();
            var cumFreqs = new List<int>();
            var cumPercents = new List<double>();

            if (binCount <= 0) // 1. Valores Discretos Exatos
            {
                var freqMap = new Dictionary<double, int>();
                for (int i = 0; i < n; i++)
                {
                    double v = list[i];
                    freqMap.TryGetValue(v, out int count);
                    freqMap[v] = count + 1;
                }

                var sortedKeys = freqMap.Keys.ToList();
                sortedKeys.Sort();

                int maxF = 0;
                foreach (var k in sortedKeys)
                {
                    int f = freqMap[k];
                    if (f > maxF) maxF = f;
                }

                // Identificar moda(s)
                if (maxF > 1 || n == 1)
                {
                    foreach (var k in sortedKeys)
                    {
                        if (freqMap[k] == maxF) modes.Add(k);
                    }
                }

                int runningSum = 0;
                foreach (var k in sortedKeys)
                {
                    int f = freqMap[k];
                    runningSum += f;
                    double rel = (double)f / n * 100.0;
                    double cumRel = (double)runningSum / n * 100.0;

                    labels.Add(k.ToString("G6"));
                    freqs.Add(f);
                    relFreqs.Add(rel);
                    cumFreqs.Add(runningSum);
                    cumPercents.Add(cumRel);
                }
            }
            else // 2. Agrupamento em Bins / Intervalos Contínuos
            {
                double min = list.Min();
                double max = list.Max();
                double range = max - min;
                if (range < 1e-12) range = 1.0;

                double binWidth = range / binCount;
                int[] binFreqs = new int[binCount];
                double[] binCenters = new double[binCount];

                for (int b = 0; b < binCount; b++)
                {
                    binCenters[b] = min + (b + 0.5) * binWidth;
                }

                for (int i = 0; i < n; i++)
                {
                    double v = list[i];
                    int bIdx = (int)Math.Floor((v - min) / binWidth);
                    if (bIdx >= binCount) bIdx = binCount - 1;
                    if (bIdx < 0) bIdx = 0;
                    binFreqs[bIdx]++;
                }

                int maxF = binFreqs.Max();
                if (maxF > 0)
                {
                    for (int b = 0; b < binCount; b++)
                    {
                        if (binFreqs[b] == maxF) modes.Add(binCenters[b]);
                    }
                }

                int runningSum = 0;
                for (int b = 0; b < binCount; b++)
                {
                    double lower = min + b * binWidth;
                    double upper = min + (b + 1) * binWidth;
                    int f = binFreqs[b];
                    runningSum += f;
                    double rel = (double)f / n * 100.0;
                    double cumRel = (double)runningSum / n * 100.0;

                    labels.Add($"[{lower:G4} ; {upper:G4})");
                    freqs.Add(f);
                    relFreqs.Add(rel);
                    cumFreqs.Add(runningSum);
                    cumPercents.Add(cumRel);
                }
            }

            DA.SetDataList(0, modes);
            DA.SetDataList(1, labels);
            DA.SetDataList(2, freqs);
            DA.SetDataList(3, relFreqs);
            DA.SetDataList(4, cumFreqs);
            DA.SetDataList(5, cumPercents);

            string modeSummary;
            if (modes.Count == 0) modeSummary = "Amodal";
            else if (modes.Count == 1) modeSummary = $"Mo: {modes[0]:F2}";
            else if (modes.Count == 2) modeSummary = $"Bimodal ({modes[0]:F1}, {modes[1]:F1})";
            else modeSummary = $"Multimodal ({modes.Count} modas)";

            this.Message = $"{modeSummary}\n{labels.Count} classes (N={n})";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.FrequencyTable;
    }
}
