using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class TargetDeviation_Component : GH_Component
    {
        public TargetDeviation_Component()
            : base(
                "Target Deviation",
                "TgtDiff",
                "Compara um conjunto de valores com um valor alvo (Target), calculando o desvio em módulo (|x - T|), com sinal, erro percentual e localizando o valor mais próximo.",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("7f8a9b0c-1d2e-3f4a-5b6c-7d8e9f0a1b2c");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Target", "T", "Valor alvo ou valor de referência para comparação.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Values", "V", "Conjunto de valores numéricos a serem comparados contra o alvo.", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Absolute Difference", "|Δ|", "Diferença em módulo (absoluta): |x_i - T|.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Signed Difference", "Δ", "Diferença com sinal algébrico: x_i - T.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Percent Error", "%Δ", "Erro relativo percentual: (|x_i - T| / |T|) * 100%.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Closest Value", "Best", "O valor da lista que possui a menor distância até o alvo.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Closest Index", "Idx", "O índice (0-based) na lista do valor mais próximo do alvo.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Min Absolute Difference", "Min|Δ|", "O menor desvio absoluto encontrado na lista.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double target = 0.0;
            if (!DA.GetData(0, ref target) || double.IsNaN(target) || double.IsInfinity(target))
            {
                this.Message = "Sem Alvo";
                return;
            }

            var rawValues = new List<double>();
            if (!DA.GetDataList(1, rawValues) || rawValues == null || rawValues.Count == 0)
            {
                this.Message = "Sem Valores";
                return;
            }

            int count = rawValues.Count;
            var absDiffs = new List<double>(count);
            var signedDiffs = new List<double>(count);
            var percentErrors = new List<double>(count);

            double minAbsDiff = double.MaxValue;
            double closestValue = target;
            int closestIndex = -1;

            double targetMag = Math.Abs(target);
            bool targetIsZero = targetMag < 1e-12;

            for (int i = 0; i < count; i++)
            {
                double v = rawValues[i];
                if (double.IsNaN(v) || double.IsInfinity(v))
                {
                    absDiffs.Add(double.NaN);
                    signedDiffs.Add(double.NaN);
                    percentErrors.Add(double.NaN);
                    continue;
                }

                double signed = v - target;
                double abs = Math.Abs(signed);
                double pct = targetIsZero ? (abs * 100.0) : ((abs / targetMag) * 100.0);

                absDiffs.Add(abs);
                signedDiffs.Add(signed);
                percentErrors.Add(pct);

                if (abs < minAbsDiff)
                {
                    minAbsDiff = abs;
                    closestValue = v;
                    closestIndex = i;
                }
            }

            if (closestIndex == -1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum valor válido encontrado para comparação.");
                this.Message = "Nenhum Válido";
                return;
            }

            DA.SetDataList(0, absDiffs);
            DA.SetDataList(1, signedDiffs);
            DA.SetDataList(2, percentErrors);
            DA.SetData(3, closestValue);
            DA.SetData(4, closestIndex);
            DA.SetData(5, minAbsDiff);

            this.Message = $"Alvo: {target:G4}\nMais Próximo: {closestValue:G4} (Δ={minAbsDiff:G4})";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.TargetDeviation;
    }
}
