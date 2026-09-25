using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class MassMath_Component : GH_Component
    {
        public MassMath_Component()
            : base(
                "Mass Math (Stack Operations)",
                "MassMath",
                "Executa operações de pilha e acumulação em massa sobre listas/árvores de números (Soma, Subtração, Multiplicação, Divisão progressiva, Média, Mín, Máx). Retorna o total escalar agregado e a lista de evolução acumulada (Running Total/Partial).",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("c4d5e6f7-a8b9-0c1d-2e3f-4a5b6c7d8e9f");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Numbers", "N", "Lista ou árvore de números a serem processados.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Operation", "Op", "Operação: 0=Soma (Σ), 1=Subtração progressiva (n0 - n1...), 2=Multiplicação (Π), 3=Divisão progressiva (n0 / n1...), 4=Média acumulada, 5=Mínimo progressivo, 6=Máximo progressivo.", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Initial Value", "Init", "Valor inicial opcional para a pilha de acumulação. Se omitido, utiliza o elemento neutro da operação.", GH_ParamAccess.item);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Total", "T", "Resultado escalar final agregado de cada ramo.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Running", "R", "Árvore de dados contendo a sequência acumulada passo a passo (Running total).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Count", "N", "Quantidade total de elementos válidos processados por ramo.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<GH_Number> numTree) || numTree == null || numTree.IsEmpty)
            {
                this.Message = "Sem Dados";
                return;
            }

            int op = 0;
            DA.GetData(1, ref op);

            double customInit = 0.0;
            bool hasCustomInit = DA.GetData(2, ref customInit);

            var outTotal = new GH_Structure<GH_Number>();
            var outRunning = new GH_Structure<GH_Number>();
            var outCount = new GH_Structure<GH_Integer>();

            int totalBranches = numTree.Paths.Count;

            for (int p = 0; p < totalBranches; p++)
            {
                var path = numTree.Paths[p];
                var branch = numTree.Branches[p];

                if (branch == null || branch.Count == 0)
                {
                    outTotal.Append(new GH_Number(0.0), path);
                    outCount.Append(new GH_Integer(0), path);
                    continue;
                }

                double runningVal = 0.0;
                int startIdx = 0;

                // Definir valor neutro inicial de acordo com a operação
                if (hasCustomInit)
                {
                    runningVal = customInit;
                    startIdx = 0;
                }
                else
                {
                    // Sem custom init: primeiro elemento é a base
                    runningVal = branch[0].Value;
                    startIdx = 1;
                }

                var runningList = new List<GH_Number>();
                if (!hasCustomInit)
                {
                    runningList.Add(new GH_Number(runningVal));
                }

                double runningSumForAvg = hasCustomInit ? customInit : branch[0].Value;

                for (int i = startIdx; i < branch.Count; i++)
                {
                    double val = branch[i].Value;

                    switch (op)
                    {
                        case 0: // Soma
                            runningVal += val;
                            break;

                        case 1: // Subtração
                            runningVal -= val;
                            break;

                        case 2: // Multiplicação
                            runningVal *= val;
                            break;

                        case 3: // Divisão
                            if (Math.Abs(val) > 1e-15)
                                runningVal /= val;
                            else
                                runningVal = double.NaN;
                            break;

                        case 4: // Média acumulada
                            runningSumForAvg += val;
                            runningVal = runningSumForAvg / (hasCustomInit ? (i + 1) : (i + 1));
                            break;

                        case 5: // Mínimo
                            if (val < runningVal) runningVal = val;
                            break;

                        case 6: // Máximo
                            if (val > runningVal) runningVal = val;
                            break;

                        default:
                            runningVal += val;
                            break;
                    }

                    runningList.Add(new GH_Number(runningVal));
                }

                outTotal.Append(new GH_Number(runningVal), path);
                outCount.Append(new GH_Integer(branch.Count), path);

                foreach (var r in runningList)
                {
                    outRunning.Append(r, path);
                }
            }

            DA.SetDataTree(0, outTotal);
            DA.SetDataTree(1, outRunning);
            DA.SetDataTree(2, outCount);

            string[] opNames = new string[] { "Soma (Σ)", "Subtração (-)", "Produto (Π)", "Divisão (/)", "Média", "Mínimo", "Máximo" };
            string currentOp = (op >= 0 && op < opNames.Length) ? opNames[op] : "Op Desconhecida";
            this.Message = $"{currentOp}\n{totalBranches} Ramos";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.MassMath;
    }
}
