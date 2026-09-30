using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Buraqueira_Tools.Explore;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>Mostra quais variáveis mais pesam em cada resultado de um lote.</summary>
    public class PillSensitivity_Component : GlauxCapsuleComponent
    {
        public PillSensitivity_Component()
            : base(
                "Pill Sensitivity",
                "Sensitivity",
                "Mostra quais variáveis mais pesam em cada resultado, a partir do Plan e dos Results do Pill Batch Runner:\n" +
                "- correlation (planos lhs, sobol, random, grid): Pearson (com p-valor), Spearman e coeficientes de regressão padronizados (SRC). " +
                "Importância = |SRC|; o R² diz se um modelo linear explica o resultado;\n" +
                "- morris (plano morris): μ* (importância), μ (sinal) e σ (não linearidade ou interação);\n" +
                "- sobol (plano saltelli): índices S1 (efeito sozinho) e ST (efeito total, com interações). Importância = ST.\n" +
                "Com Method vazio, o método segue o plano. Funciona com execuções parciais (lote pausado).",
                "Explore",
                "EXPLORE",
                ColorExplore)
        {
        }

        public override Guid ComponentGuid => new Guid("94dc089b-742e-41cd-9816-8438df246131");
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override Bitmap Icon => GlauxToolsIcons.PillSensitivity;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Plan", "P", "Plano executado (saída Plan do Pill Batch Runner ou do Pill Sampler).", GH_ParamAccess.item);
            pManager.AddGenericParameter("Results", "R", "Um ramo por execução; o último índice do caminho é a amostra (saída Results do Batch Runner).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Names", "N", "Nome de cada resultado (colunas de Results), para o ranking.", GH_ParamAccess.list);
            pManager.AddTextParameter("Method", "M", "auto (padrão), correlation, morris ou sobol.", GH_ParamAccess.item, "auto");
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Variables", "V", "Variáveis, na ordem das colunas de Importance.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Importance", "Imp", "Um ramo {r} por resultado: importância de cada variável (|SRC|, μ* ou ST).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Ranking", "Rk", "Uma linha por resultado, da variável mais para a menos importante.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Details", "D", "Ramo {r; variável}: todas as medidas, na ordem de Measures.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Measures", "Ms", "Nome das medidas em Details.", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "Método, execuções usadas, ajuste (R² ou soma dos S1) e observações.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object rawPlan = null;
            DA.GetData(0, ref rawPlan);
            var plan = (rawPlan as GH_SamplePlanGoo)?.Value;
            if (plan == null)
            {
                SetCapsule("Sem plano", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Ligue o Plan do Pill Batch Runner (ou do Pill Sampler).");
                return;
            }
            if (!DA.GetDataTree(1, out GH_Structure<IGH_Goo> tree) || tree == null || tree.PathCount == 0)
            {
                SetCapsule("Sem resultados", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Ligue os Results do Pill Batch Runner.");
                return;
            }
            var names = new List<string>();
            DA.GetDataList(2, names);
            string methodText = "auto";
            DA.GetData(3, ref methodText);
            if (!TryParseMethod(methodText, out var method))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Método '{methodText}' desconhecido: use auto, correlation, morris ou sobol.");
                return;
            }

            // Uma linha por ramo; a amostra é o último índice do caminho
            var rows = new List<int>();
            var y = new List<double[]>();
            var seen = new HashSet<int>();
            for (int b = 0; b < tree.PathCount; b++)
            {
                var path = tree.Paths[b];
                int sample = path.Length > 0 ? path[path.Length - 1] : b;
                if (!seen.Add(sample))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Dois ramos para a amostra {sample}: use um ramo {{i}} por execução.");
                    return;
                }
                rows.Add(sample);
                y.Add(tree.Branches[b].Select(DesignControls.ToNumber).ToArray());
            }

            SensitivityResult result;
            try
            {
                result = SensitivityAnalysis.Analyze(plan, null, rows, y.ToArray(), method, names);
            }
            catch (ArgumentException ex)
            {
                SetCapsule("Erro", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                return;
            }

            var importance = new GH_Structure<GH_Number>();
            var details = new GH_Structure<GH_Number>();
            var info = new List<string> { $"{MethodLabel(result.Method)} · {rows.Count} execuções de {plan.Count} · importância = {result.ImportanceMeasure}" };
            var notes = new List<string>();
            for (int r = 0; r < result.Results.Count; r++)
            {
                var rs = result.Results[r];
                var path = new GH_Path(r);
                importance.EnsurePath(path);
                foreach (var v in rs.Importance) importance.Append(new GH_Number(v), path);
                for (int j = 0; j < rs.Details.Length; j++)
                {
                    var dp = new GH_Path(r, j);
                    details.EnsurePath(dp);
                    foreach (var d in rs.Details[j]) details.Append(new GH_Number(d), dp);
                }
                string fit = rs.Fit.HasValue ? (result.Method == SensitivityMethod.Sobol ? $", ΣS1 = {SensitivityResult.Fmt(rs.Fit.Value)}" : $", R² = {SensitivityResult.Fmt(rs.Fit.Value)}") : "";
                info.Add($"{rs.Name}: {rs.Used} usadas{fit}");
                foreach (var n in rs.Notes) notes.Add($"{rs.Name}: {n}");
            }
            foreach (var n in notes.Distinct().Take(4)) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, n);

            SetCapsule($"{MethodLabel(result.Method)} · {result.Results.Count} resultado(s)", true, notes.Count > 0);
            Message = MethodLabel(result.Method);
            DA.SetDataList(0, result.Variables);
            DA.SetDataTree(1, importance);
            DA.SetDataList(2, result.RankingLines());
            DA.SetDataTree(3, details);
            DA.SetDataList(4, result.Measures);
            DA.SetData(5, string.Join("\n", info.Concat(notes)));
        }

        private static bool TryParseMethod(string text, out SensitivityMethod method)
        {
            switch ((text ?? "").Trim().ToLowerInvariant())
            {
                case "":
                case "auto":
                    method = SensitivityMethod.Auto;
                    return true;
                case "correlation":
                case "correlação":
                case "correlacao":
                case "src":
                    method = SensitivityMethod.Correlation;
                    return true;
                case "morris":
                    method = SensitivityMethod.Morris;
                    return true;
                case "sobol":
                    method = SensitivityMethod.Sobol;
                    return true;
                default:
                    method = SensitivityMethod.Auto;
                    return false;
            }
        }

        private static string MethodLabel(SensitivityMethod m)
        {
            switch (m)
            {
                case SensitivityMethod.Morris: return "morris";
                case SensitivityMethod.Sobol: return "sobol";
                default: return "correlation";
            }
        }
    }
}
