using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Threshold Voting Gate (Porta de Quórum / Maioria)
    /// Avalia se um percentual ou número mínimo de condições foi satisfeito,
    /// suportando votação ponderada por pesos por critério.
    /// Muito útil quando nem todos os parâmetros precisam estar 100% em conformidade para prosseguir.
    /// </summary>
    public class ThresholdVotingGate_Component : GH_Component
    {
        private double _defaultThreshold = 0.5; // 50% maioria simples
        private bool _strictlyGreater = false; // se true: > Thresh; se false: >= Thresh

        public ThresholdVotingGate_Component()
            : base(
                "Threshold Voting Gate",
                "VoteGate",
                "Avalia se um percentual ou quórum numérico mínimo de condições booleanas foi satisfeito, com suporte a pesos ponderados por critério. Ideal para metas de conformidade parcial ou aprovação multicritério.",
                "Glaux Tools",
                "Automation")
        {
        }

        public override Guid ComponentGuid => new Guid("0c9b8a7d-6e5f-4a3b-2c1d-0e9f8a7b6c16");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Conditions", "C", "Lista ou árvore de verificações booleanas (ex: saídas do Pill Constraint Checker).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Threshold", "Thresh", "Valor numérico ou percentual mínimo de aprovação (ex: 0.80 para 80% ou 3 para pelo menos 3 critérios válidos). Padrão = 0.5 (50%).", GH_ParamAccess.item, 0.5);
            pManager.AddNumberParameter("Weights", "W", "Pesos opcionais por critério para votação ponderada. Se omitido, todos os critérios têm peso 1.0.", GH_ParamAccess.list);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Pass", "OK", "True se o quórum mínimo de conformidade for atingido, False caso contrário.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Score", "%", "Percentual exato de conformidade obtido (0.0 a 100.0%).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Weighted Score", "Score", "Pontuação absoluta obtida (soma dos pesos dos critérios aprovados).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Passed Count", "N_pass", "Quantidade de critérios aprovados.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Failed Count", "N_fail", "Quantidade de critérios reprovados.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Failed Indices", "iFail", "Índices (IDs locais) dos critérios que falharam (para diagnóstico rápido).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Summary", "Rep", "Diagnóstico textual completo dos critérios aprovados vs. reprovados.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<GH_Boolean> inTree) || inTree == null || inTree.PathCount == 0)
            {
                this.Message = "Sem Condições";
                return;
            }

            double threshold = _defaultThreshold;
            DA.GetData(1, ref threshold);

            var weights = new List<double>();
            DA.GetDataList(2, weights);

            var outPass = new GH_Structure<GH_Boolean>();
            var outScore = new GH_Structure<GH_Number>();
            var outWeightedScore = new GH_Structure<GH_Number>();
            var outPassCount = new GH_Structure<GH_Integer>();
            var outFailCount = new GH_Structure<GH_Integer>();
            var outFailIndices = new GH_Structure<GH_Integer>();
            var outSummary = new GH_Structure<GH_String>();

            bool lastPass = false;
            double lastPct = 0;

            foreach (var path in inTree.Paths)
            {
                var branch = inTree[path];
                int n = branch.Count;

                if (n == 0)
                {
                    outPass.Append(new GH_Boolean(false), path);
                    outScore.Append(new GH_Number(0), path);
                    outWeightedScore.Append(new GH_Number(0), path);
                    outPassCount.Append(new GH_Integer(0), path);
                    outFailCount.Append(new GH_Integer(0), path);
                    outSummary.Append(new GH_String("Ramo vazio."), path);
                    continue;
                }

                double totalWeight = 0;
                double achievedWeight = 0;
                int passedCount = 0;
                var failedIndices = new List<int>();

                for (int i = 0; i < n; i++)
                {
                    double w = (i < weights.Count && weights[i] > 0) ? weights[i] : 1.0;
                    totalWeight += w;

                    bool val = branch[i] != null && branch[i].Value;
                    if (val)
                    {
                        achievedWeight += w;
                        passedCount++;
                    }
                    else
                    {
                        failedIndices.Add(i);
                    }
                }

                int failedCount = n - passedCount;
                double ratio = totalWeight > 0 ? (achievedWeight / totalWeight) : 0.0;
                double percentage = ratio * 100.0;

                bool isPass;
                string criteriaDescription;

                if (threshold <= 1.0)
                {
                    // Limiar interpretado como percentual (ex: 0.8 = 80%)
                    double reqRatio = Math.Max(0.0, threshold);
                    isPass = _strictlyGreater ? (ratio > reqRatio) : (ratio >= reqRatio);
                    criteriaDescription = $"Exigido: {reqRatio * 100:F1}%";
                }
                else
                {
                    // Limiar interpretado como pontuação ou contagem absoluta mínima
                    isPass = _strictlyGreater ? (achievedWeight > threshold) : (achievedWeight >= threshold);
                    criteriaDescription = $"Exigido: {threshold:F1} pontos (de {totalWeight:F1})";
                }

                lastPass = isPass;
                lastPct = percentage;

                outPass.Append(new GH_Boolean(isPass), path);
                outScore.Append(new GH_Number(percentage), path);
                outWeightedScore.Append(new GH_Number(achievedWeight), path);
                outPassCount.Append(new GH_Integer(passedCount), path);
                outFailCount.Append(new GH_Integer(failedCount), path);

                foreach (var fi in failedIndices)
                {
                    outFailIndices.Append(new GH_Integer(fi), path);
                }

                string statusText = isPass ? "APROVADO" : "REPROVADO";
                string failStr = failedIndices.Count == 0
                    ? "100% de conformidade"
                    : $"Reprovados: [{string.Join(", ", failedIndices)}]";

                string report = $"[{statusText}] Conformidade: {percentage:F1}% ({achievedWeight:F1}/{totalWeight:F1} pts) | {criteriaDescription} | Aprovados: {passedCount}/{n} | {failStr}";

                outSummary.Append(new GH_String(report), path);
            }

            this.Message = $"{(lastPass ? "APROVADO" : "REPROVADO")}\n({lastPct:F1}%)";

            DA.SetDataTree(0, outPass);
            DA.SetDataTree(1, outScore);
            DA.SetDataTree(2, outWeightedScore);
            DA.SetDataTree(3, outPassCount);
            DA.SetDataTree(4, outFailCount);
            DA.SetDataTree(5, outFailIndices);
            DA.SetDataTree(6, outSummary);
        }

        #region Menu & Serialization

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var presetsMenu = new ToolStripMenuItem("Limiar Pré-definido (Threshold Presets)");

            void AddPreset(string title, double val)
            {
                var item = new ToolStripMenuItem(title)
                {
                    Checked = Math.Abs(_defaultThreshold - val) < 0.001
                };
                item.Click += (s, e) =>
                {
                    RecordUndoEvent("Definir Limiar Pré-definido");
                    _defaultThreshold = val;
                    ExpireSolution(true);
                };
                presetsMenu.DropDownItems.Add(item);
            }

            AddPreset("Maioria Simples (> 50%)", 0.501);
            AddPreset("Maioria Qualificada (>= 66.7%)", 0.667);
            AddPreset("Alta Conformidade (>= 80%)", 0.80);
            AddPreset("Rigor Extremo (>= 90%)", 0.90);
            AddPreset("Unanimidade Estrita (100%)", 1.0);

            menu.Items.Add(presetsMenu);

            var strictItem = new ToolStripMenuItem("Exigir Estritamente Maior ( > ao invés de >=)")
            {
                Checked = _strictlyGreater
            };
            strictItem.Click += (s, e) =>
            {
                RecordUndoEvent("Alternar Comparador Estrito");
                _strictlyGreater = !_strictlyGreater;
                ExpireSolution(true);
            };
            menu.Items.Add(strictItem);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetDouble("DefaultThreshold", _defaultThreshold);
            writer.SetBoolean("StrictlyGreater", _strictlyGreater);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("DefaultThreshold")) _defaultThreshold = reader.GetDouble("DefaultThreshold");
            if (reader.ItemExists("StrictlyGreater")) _strictlyGreater = reader.GetBoolean("StrictlyGreater");
            return base.Read(reader);
        }

        #endregion

        protected override Bitmap Icon => GlauxToolsIcons.ThresholdVotingGate;
    }
}
