using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class PillConstraintChecker_Component : GH_Component
    {
        public PillConstraintChecker_Component()
            : base(
                "Pill Constraint Checker",
                "PillCheck",
                "Validador e guardião de restrições de projeto. Compara parâmetros acústicos e geométricos contra faixas-alvo (ex: RT60 dentro da ISO 3382). Emite status visual no canvas e relatórios de conformidade.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("a1100008-e1ef-4000-8000-000000000008");
        protected override Bitmap Icon => GlauxToolsIcons.PillConstraintChecker;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ParamName", "N", "Nome do parâmetro sob verificação (ex: 'RT60 1000Hz [s]').", GH_ParamAccess.item, "Parâmetro");
            pManager.AddNumberParameter("Values", "V", "Valores medidos ou calculados a verificar.", GH_ParamAccess.list);
            pManager.AddNumberParameter("MinTarget", "Min", "Limite mínimo aceitável.", GH_ParamAccess.item);
            pManager.AddNumberParameter("MaxTarget", "Max", "Limite máximo aceitável.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Tolerance", "Tol", "Margem de tolerância opcional (expandindo a faixa aceitável em ±Tol).", GH_ParamAccess.item, 0.0);
            pManager[0].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Pass", "OK", "True se 100% dos valores cumprem os limites de conformidade; False se houver violação.", GH_ParamAccess.item);
            pManager.AddNumberParameter("ComplianceRate", "CR", "Taxa percentual de conformidade [0% a 100%].", GH_ParamAccess.item);
            pManager.AddTextParameter("Violations", "VIO", "Lista detalhada dos valores em desacordo com as metas.", GH_ParamAccess.list);
            pManager.AddNumberParameter("ConformingValues", "CV", "Subconjunto com os valores aprovados.", GH_ParamAccess.list);
            pManager.AddTextParameter("Report", "RPT", "Relatório estruturado de conformidade com média e diagnóstico.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string paramName = "Parâmetro";
            DA.GetData(0, ref paramName);

            var values = new List<double>();
            if (!DA.GetDataList(1, values) || values.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Forneça pelo menos um valor para validação de restrição.");
                return;
            }

            double min = 0;
            double max = 0;
            if (!DA.GetData(2, ref min) || !DA.GetData(3, ref max)) return;

            if (min > max)
            {
                double tmp = min; min = max; max = tmp;
            }

            double tol = 0;
            DA.GetData(4, ref tol);
            if (tol < 0) tol = Math.Abs(tol);

            double effMin = min - tol;
            double effMax = max + tol;

            var violations = new List<string>();
            var conforming = new List<double>();

            double sum = 0;
            for (int i = 0; i < values.Count; i++)
            {
                double v = values[i];
                sum += v;

                if (v >= effMin && v <= effMax)
                {
                    conforming.Add(v);
                }
                else
                {
                    double diff = v < effMin ? (v - effMin) : (v - effMax);
                    string side = v < effMin ? "Abaixo do Mínimo" : "Acima do Máximo";
                    violations.Add($"Item [{i}] = {v:F3} ({side} por {Math.Abs(diff):F3}) | Alvo: [{effMin:F2} a {effMax:F2}]");
                }
            }

            int total = values.Count;
            int passCount = conforming.Count;
            double rate = (double)passCount / total * 100.0;
            bool pass = violations.Count == 0;
            double mean = sum / total;

            var sb = new StringBuilder();
            sb.AppendLine("==========================================================");
            sb.AppendLine($"     RELATÓRIO DE CONFORMIDADE: {paramName.ToUpper()}     ");
            sb.AppendLine("==========================================================");
            sb.AppendLine($"Faixa Aceitável: [{effMin:F3} a {effMax:F3}] (Tol: ±{tol:F3})");
            sb.AppendLine($"Total de Amostras: {total} | Aprovadas: {passCount} | Violações: {violations.Count}");
            sb.AppendLine($"Taxa de Conformidade: {rate:F1}%");
            sb.AppendLine($"Média das Amostras: {mean:F3}");
            sb.AppendLine($"Veredito: {(pass ? "CONFORME (APROVADO)" : "VIOLAÇÃO IDENTIFICADA")}");
            sb.AppendLine("----------------------------------------------------------");

            if (violations.Count > 0)
            {
                sb.AppendLine("Itens em Desacordo:");
                foreach (var v in violations)
                {
                    sb.AppendLine($" • {v}");
                }
            }
            else
            {
                sb.AppendLine("✓ Todos os valores situam-se rigorosamente dentro dos limites.");
            }

            sb.AppendLine("==========================================================");

            if (!pass)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{violations.Count} de {total} valores violaram os limites normativos.");
                Message = $"FAIL ({violations.Count}/{total})";
            }
            else
            {
                Message = "PASS (100%)";
            }

            DA.SetData(0, pass);
            DA.SetData(1, rate);
            DA.SetDataList(2, violations);
            DA.SetDataList(3, conforming);
            DA.SetData(4, sb.ToString());
        }
    }
}
