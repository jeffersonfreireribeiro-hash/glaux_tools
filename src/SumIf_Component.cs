using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class SumIf_Component : GH_Component
    {
        public SumIf_Component()
            : base(
                "Conditional Math (SumIf / SomaSe)",
                "SomaSe",
                "Executa operações condicionais no estilo Excel (SOMASE, SOMASES, CONT.SE, MÉDIA.SE, MULT.SE). Avalia critérios numéricos ou expressões de texto (ex.: '>50', '<=0', '=A', '!=0') e máscaras booleanas, com suporte a intervalo de soma separado.",
                "Glaux Tools",
                "Statistics")
        {
        }

        public override Guid ComponentGuid => new Guid("d5e6f7a8-b90c-1d2e-3f4a-5b6c7d8e9f0a");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Values", "V", "Valores a serem somados/agregados (intervalo de soma).", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Criteria", "C", "Critério de condição: expressão de texto (ex: '>50', '<0', '>=10', '!=5', '=A'), número de igualdade ou máscara booleana.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Evaluation Range", "R", "Faixa de avaliação opcional. Se não fornecida, avalia o critério diretamente em Values. Se fornecida, avalia o critério em R e soma os valores correspondentes em V.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Operation", "Op", "Operação: 0=Soma (SOMASE / SUMIF), 1=Média (MÉDIA.SE / AVERAGEIF), 2=Contagem (CONT.SE / COUNTIF), 3=Produto (MULT.SE / PRODUCTIF), 4=Mínimo (MÍN.SE / MINIF), 5=Máximo (MÁX.SE / MAXIF).", GH_ParamAccess.item, 0);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Result", "R", "Resultado escalar agregado para cada ramo.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Filtered Values", "F", "Árvore de dados contendo apenas os elementos que atenderam ao critério.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Mask", "M", "Máscara booleana correspondente 1:1 com os itens avaliados.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Count", "N", "Quantidade de elementos aceitos pelo critério por ramo.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> valuesTree) || valuesTree == null || valuesTree.IsEmpty)
            {
                this.Message = "Sem Valores";
                return;
            }

            if (!DA.GetDataTree(1, out GH_Structure<IGH_Goo> criteriaTree) || criteriaTree == null || criteriaTree.IsEmpty)
            {
                this.Message = "Sem Critério";
                return;
            }

            GH_Structure<IGH_Goo> rangeTree = null;
            bool hasRange = DA.GetDataTree(2, out rangeTree) && rangeTree != null && !rangeTree.IsEmpty;

            int op = 0;
            DA.GetData(3, ref op);

            var outResult = new GH_Structure<GH_Number>();
            var outFiltered = new GH_Structure<IGH_Goo>();
            var outMask = new GH_Structure<GH_Boolean>();
            var outCount = new GH_Structure<GH_Integer>();

            int totalAcceptedAll = 0;

            for (int p = 0; p < valuesTree.Paths.Count; p++)
            {
                var path = valuesTree.Paths[p];
                var vBranch = valuesTree.Branches[p];

                var rBranch = hasRange ? (p < rangeTree.Branches.Count ? rangeTree.Branches[p] : (rangeTree.Branches.Count > 0 ? rangeTree.Branches[0] : vBranch)) : vBranch;
                var cBranch = p < criteriaTree.Branches.Count ? criteriaTree.Branches[p] : (criteriaTree.Branches.Count > 0 ? criteriaTree.Branches[0] : null);

                if (vBranch == null || vBranch.Count == 0) continue;

                int count = Math.Min(vBranch.Count, rBranch.Count);
                var filteredList = new List<double>();
                var filteredGooList = new List<IGH_Goo>();
                int acceptedCount = 0;

                for (int i = 0; i < count; i++)
                {
                    var evalItem = rBranch[i];
                    var valItem = vBranch[i];

                    // Obter critério (pode ser 1 item para o ramo inteiro ou lista 1:1)
                    IGH_Goo critItem = null;
                    if (cBranch != null && cBranch.Count > 0)
                    {
                        critItem = (cBranch.Count == 1) ? cBranch[0] : (i < cBranch.Count ? cBranch[i] : cBranch[cBranch.Count - 1]);
                    }

                    bool match = EvaluateCondition(evalItem, critItem);
                    outMask.Append(new GH_Boolean(match), path);

                    if (match)
                    {
                        acceptedCount++;
                        outFiltered.Append(valItem, path);

                        if (TryGetNumber(valItem, out double numVal))
                        {
                            filteredList.Add(numVal);
                        }
                    }
                }

                // Calcular agregação
                double agg = 0.0;
                if (filteredList.Count > 0)
                {
                    switch (op)
                    {
                        case 0: // Soma
                            foreach (var x in filteredList) agg += x;
                            break;

                        case 1: // Média
                            double sum = 0.0;
                            foreach (var x in filteredList) sum += x;
                            agg = sum / filteredList.Count;
                            break;

                        case 2: // Contagem
                            agg = acceptedCount;
                            break;

                        case 3: // Produto
                            agg = 1.0;
                            foreach (var x in filteredList) agg *= x;
                            break;

                        case 4: // Mínimo
                            agg = double.MaxValue;
                            foreach (var x in filteredList) if (x < agg) agg = x;
                            break;

                        case 5: // Máximo
                            agg = double.MinValue;
                            foreach (var x in filteredList) if (x > agg) agg = x;
                            break;

                        default:
                            foreach (var x in filteredList) agg += x;
                            break;
                    }
                }
                else if (op == 2)
                {
                    agg = 0;
                }
                else
                {
                    agg = double.NaN;
                }

                outResult.Append(new GH_Number(agg), path);
                outCount.Append(new GH_Integer(acceptedCount), path);
                totalAcceptedAll += acceptedCount;
            }

            DA.SetDataTree(0, outResult);
            DA.SetDataTree(1, outFiltered);
            DA.SetDataTree(2, outMask);
            DA.SetDataTree(3, outCount);

            string[] opNames = new string[] { "SOMASE", "MÉDIA.SE", "CONT.SE", "MULT.SE", "MÍN.SE", "MÁX.SE" };
            string opStr = (op >= 0 && op < opNames.Length) ? opNames[op] : "SOMASE";
            this.Message = $"{opStr}\n{totalAcceptedAll:N0} Aceitos";
        }

        private static bool EvaluateCondition(IGH_Goo item, IGH_Goo criteria)
        {
            if (criteria == null) return true;

            // Se o critério for diretamente booleano
            if (criteria is GH_Boolean ghBool) return ghBool.Value;

            string critStr = criteria.ToString()?.Trim();
            if (string.IsNullOrEmpty(critStr)) return true;

            // Converter item para número se possível
            bool isItemNum = TryGetNumber(item, out double itemNum);
            string itemStr = item?.ToString()?.Trim() ?? "";

            // Expressões com operadores: >=, <=, !=, <>, >, <, =
            var match = Regex.Match(critStr, @"^(>=|<=|!=|<>|>|<|=)?\s*(.*)$");
            if (match.Success)
            {
                string op = match.Groups[1].Value;
                string valPart = match.Groups[2].Value.Trim();

                if (string.IsNullOrEmpty(op)) op = "=";
                if (op == "<>") op = "!=";

                bool isTargetNum = double.TryParse(valPart, NumberStyles.Any, CultureInfo.InvariantCulture, out double targetNum);
                if (!isTargetNum && valPart.Contains(","))
                {
                    isTargetNum = double.TryParse(valPart.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out targetNum);
                }

                if (isItemNum && isTargetNum)
                {
                    switch (op)
                    {
                        case ">": return itemNum > targetNum;
                        case ">=": return itemNum >= targetNum;
                        case "<": return itemNum < targetNum;
                        case "<=": return itemNum <= targetNum;
                        case "!=": return Math.Abs(itemNum - targetNum) > 1e-12;
                        case "=": return Math.Abs(itemNum - targetNum) <= 1e-12;
                    }
                }
                else
                {
                    // Comparação textual
                    switch (op)
                    {
                        case "!=": return !string.Equals(itemStr, valPart, StringComparison.OrdinalIgnoreCase);
                        case "=": return string.Equals(itemStr, valPart, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            return string.Equals(itemStr, critStr, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetNumber(IGH_Goo goo, out double val)
        {
            val = 0.0;
            if (goo == null) return false;
            if (goo is GH_Number ghNum) { val = ghNum.Value; return true; }
            if (goo is GH_Integer ghInt) { val = ghInt.Value; return true; }
            if (goo is GH_Boolean ghBool) { val = ghBool.Value ? 1.0 : 0.0; return true; }
            string s = goo.ToString()?.Trim();
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out val)) return true;
            if (s.Contains(",") && double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out val)) return true;
            return false;
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.SumIf;
    }
}
