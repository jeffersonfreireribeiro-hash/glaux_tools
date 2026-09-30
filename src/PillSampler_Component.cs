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
    /// <summary>Gera o plano de amostras de um espaço de projeto (determinístico pela semente).</summary>
    public class PillSampler_Component : GlauxCapsuleComponent
    {
        // Último plano gerado: com o Design Space ligado por fio aos sliders, o Sampler recalcula a cada amostra
        // aplicada pelo Batch Runner; com a mesma entrada, reaproveita o plano e as árvores (nada é refeito)
        private string _cacheKey;
        private SamplePlan _cachePlan;
        private GH_Structure<IGH_Goo> _cacheSamples;
        private GH_Structure<GH_Number> _cacheUnit;
        private List<string> _cacheNotes;

        public PillSampler_Component()
            : base(
                "Pill Sampler",
                "Sampler",
                "Gera as alternativas a rodar a partir do Pill Design Space. Métodos:\n" +
                "- lhs (padrão): Latin Hypercube, boa cobertura com poucas amostras;\n" +
                "- sobol: sequência quase aleatória (use potências de 2: 64, 128, 256…);\n" +
                "- random: Monte Carlo; grid: todas as combinações de N níveis;\n" +
                "- morris: trajetórias para triagem de sensibilidade (Count = trajetórias; total = Count × (k + 1));\n" +
                "- saltelli: para índices de Sobol (Count = N base; total = N × (k + 2)).\n" +
                "Mesma semente → mesmas amostras. Nada muda no canvas: ligue o plano no Pill Batch Runner.",
                "Explore",
                "EXPLORE",
                ColorExplore)
        {
        }

        public override Guid ComponentGuid => new Guid("6a60ae71-44a5-463b-80fe-ef1bfd987a8b");
        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override Bitmap Icon => GlauxToolsIcons.PillSampler;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Space", "DS", "Espaço de projeto do Pill Design Space.", GH_ParamAccess.item);
            pManager.AddTextParameter("Method", "M", "lhs, sobol, random, grid, morris ou saltelli.", GH_ParamAccess.item, "lhs");
            pManager.AddIntegerParameter("Count", "N", "Amostras (lhs, sobol, random), total aproximado (grid), trajetórias (morris) ou N base (saltelli).", GH_ParamAccess.item, 64);
            pManager.AddIntegerParameter("Seed", "S", "Semente: a mesma semente gera as mesmas amostras.", GH_ParamAccess.item, 1);
            pManager.AddTextParameter("Options", "O", "Opcional: 'levels=5' (grid: níveis por variável; morris: níveis da grade), 'maximin=20' (lhs), 'centered' (lhs), 'scramble=false' (sobol/saltelli).", GH_ParamAccess.list);
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Plan", "P", "Plano de amostras (ligue no Pill Batch Runner).", GH_ParamAccess.item);
            pManager.AddGenericParameter("Samples", "S", "Um ramo {i} por amostra com o valor de cada variável (níveis como texto).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Unit", "U", "Mesmas amostras em coordenadas unitárias [0, 1] (úteis para gráficos).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Count", "N", "Quantidade de amostras (execuções necessárias).", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "I", "Método, tamanho e observações.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object raw = null;
            DA.GetData(0, ref raw);
            var space = (raw as GH_DesignSpaceGoo)?.Value ?? (raw as GH_SamplePlanGoo)?.Value?.Space;
            if (space == null || space.Count == 0)
            {
                SetCapsule("Sem espaço", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Ligue a saída Space do Pill Design Space.");
                return;
            }

            string methodText = "lhs";
            int count = 64, seed = 1;
            DA.GetData(1, ref methodText);
            DA.GetData(2, ref count);
            DA.GetData(3, ref seed);
            var optionLines = new List<string>();
            DA.GetDataList(4, optionLines);

            if (!SamplerOptions.TryParseMethod(methodText, out var method))
            {
                SetCapsule("Método?", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Método '{methodText}' desconhecido: use lhs, sobol, random, grid, morris ou saltelli.");
                return;
            }

            var options = new SamplerOptions { Count = count, Seed = seed };
            foreach (var kv in ParseKeyValues(optionLines))
            {
                string key = kv.Key.ToLowerInvariant();
                switch (key)
                {
                    case "levels":
                    case "niveis":
                    case "níveis":
                        if (int.TryParse(kv.Value, out int levels) && levels > 0) options.Levels = levels;
                        else AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"levels='{kv.Value}' inválido.");
                        break;
                    case "maximin":
                        if (int.TryParse(kv.Value, out int c) && c > 0) options.MaximinCandidates = c;
                        else AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"maximin='{kv.Value}' inválido.");
                        break;
                    case "centered":
                    case "centro":
                        options.Centered = kv.Value.Length == 0 || !kv.Value.Equals("false", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "scramble":
                        options.Scramble = !kv.Value.Equals("false", StringComparison.OrdinalIgnoreCase) && kv.Value != "0";
                        break;
                    default:
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Opção '{kv.Key}' desconhecida (use levels, maximin, centered ou scramble).");
                        break;
                }
            }

            string cacheKey = string.Join("|", space.Hash, method, options.Count, options.Seed, options.Levels, options.MaximinCandidates, options.Centered, options.Scramble);
            if (cacheKey != _cacheKey || _cachePlan == null)
            {
                var notesNew = new List<string>();
                SamplePlan generated;
                try
                {
                    generated = DesignSampler.Generate(space, method, options, notesNew);
                }
                catch (ArgumentException ex)
                {
                    _cacheKey = null;
                    SetCapsule("Erro", false);
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                    return;
                }
                var samplesNew = new GH_Structure<IGH_Goo>();
                var unitNew = new GH_Structure<GH_Number>();
                for (int i = 0; i < generated.Count; i++)
                {
                    var path = new GH_Path(i);
                    for (int j = 0; j < space.Count; j++)
                    {
                        samplesNew.Append(DesignControls.Display(space.Variables[j], generated.Values[i][j]), path);
                        unitNew.Append(new GH_Number(generated.Unit[i][j]), path);
                    }
                }
                _cacheKey = cacheKey;
                _cachePlan = generated;
                _cacheSamples = samplesNew;
                _cacheUnit = unitNew;
                _cacheNotes = notesNew;
            }

            var plan = _cachePlan;
            var notes = _cacheNotes;
            var samples = _cacheSamples;
            var unit = _cacheUnit;
            foreach (var n in notes) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, n);

            string m = SamplerOptions.MethodName(plan.Method);
            SetCapsule($"{m} · {plan.Count}", true, notes.Count > 0);
            Message = $"{m} · {plan.Count}";
            DA.SetData(0, new GH_SamplePlanGoo(plan));
            DA.SetDataTree(1, samples);
            DA.SetDataTree(2, unit);
            DA.SetData(3, plan.Count);
            DA.SetData(4, plan.Describe() + $" · semente {seed}" + (notes.Count > 0 ? "\n" + string.Join("\n", notes) : ""));
        }
    }
}
