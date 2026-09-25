using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class CentralTendency_Component : GH_Component
    {
        public CentralTendency_Component()
            : base(
                "Central Tendency",
                "CenterStat",
                "Calcula medidas de tendência central: Média Aritmética (Mean), Mediana (Median), Moda(s) (Mode), Mínimo, Máximo, Amplitude e Contagem.",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("3b4c5d6e-7f8a-9b0c-1d2e-3f4a5b6c7d8e");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Values", "V", "Conjunto de dados numéricos (lista ou árvore de números).", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Mean", "Avg", "Média aritmética dos valores (x̄ = Σx / N).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Median", "Med", "Mediana dos valores (ponto central ordenado Q2).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Mode", "Mod", "Moda(s) do conjunto de dados (valor ou valores com maior frequência).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Min", "Min", "Menor valor encontrado no conjunto.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Max", "Max", "Maior valor encontrado no conjunto.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Range", "Rng", "Amplitude total dos dados (Max - Min).", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Count", "N", "Quantidade total de elementos numéricos válidos.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rawList = new List<double>();
            if (!DA.GetDataList(0, rawList) || rawList == null || rawList.Count == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            // Filtrar NaNs e Infinities
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
            if (n == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum número válido fornecido.");
                this.Message = "N = 0";
                return;
            }

            // 1. Média
            double sum = 0.0;
            for (int i = 0; i < n; i++) sum += list[i];
            double mean = sum / n;

            // 2. Ordenação para Mediana, Mínimo e Máximo
            var sorted = new List<double>(list);
            sorted.Sort();

            double min = sorted[0];
            double max = sorted[n - 1];
            double range = max - min;

            // Mediana
            double median;
            if (n % 2 == 1)
            {
                median = sorted[n / 2];
            }
            else
            {
                median = (sorted[(n / 2) - 1] + sorted[n / 2]) * 0.5;
            }

            // 3. Moda(s)
            var freqMap = new Dictionary<double, int>();
            int maxFreq = 0;
            for (int i = 0; i < n; i++)
            {
                double val = list[i];
                freqMap.TryGetValue(val, out int count);
                count++;
                freqMap[val] = count;
                if (count > maxFreq) maxFreq = count;
            }

            var modes = new List<double>();
            // Se a frequência máxima for 1 e n > 1, todos os valores aparecem 1 vez (conjunto amodal ou cada valor é moda)
            if (maxFreq > 1 || n == 1)
            {
                foreach (var kvp in freqMap)
                {
                    if (kvp.Value == maxFreq)
                    {
                        modes.Add(kvp.Key);
                    }
                }
                modes.Sort();
            }

            DA.SetData(0, mean);
            DA.SetData(1, median);
            DA.SetDataList(2, modes);
            DA.SetData(3, min);
            DA.SetData(4, max);
            DA.SetData(5, range);
            DA.SetData(6, n);

            string modeTag = (modes.Count == 0) ? "Amodal" : (modes.Count == 1 ? $"Mo:{modes[0]:F2}" : $"Multi({modes.Count})");
            this.Message = $"x̄: {mean:F2} | Med: {median:F2}\n{modeTag} (N={n})";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.CentralTendency;
    }
}
