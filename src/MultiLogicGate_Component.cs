using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public enum LogicGateOp
    {
        AND = 0,
        OR = 1,
        XOR = 2,
        NAND = 3,
        NOR = 4,
        XNOR = 5
    }

    /// <summary>
    /// Multi-Input Logic Gate (AND / OR / XOR / NAND / NOR / XNOR)
    /// Em vez de encadear múltiplos operadores nativos de duas entradas que poluem o canvas,
    /// esta pilha consolidada avalia N condições simultaneamente por lista ou árvore.
    /// </summary>
    public class MultiLogicGate_Component : GH_Component, IGH_VariableParameterComponent
    {
        private LogicGateOp _operation = LogicGateOp.AND;
        private bool _strictXor = false; // Se false: paridade (ímpar de Trues); se true: exclusivamente 1 True

        public MultiLogicGate_Component()
            : base(
                "Multi Logic Gate",
                "LogicGate",
                "Porta lógica consolidada de múltiplas entradas (AND, OR, XOR, NAND, NOR, XNOR). Avalia N condições simultaneamente sem poluir o canvas com múltiplos operadores encadeados. Retorna o booleano consolidado, contagens e os índices das entradas que falharam.",
                "Glaux Tools",
                "Automation")
        {
        }

        public override Guid ComponentGuid => new Guid("9b8a7c6d-5e4f-3a2b-1c0d-9e8f7a6b5c15");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Inputs", "L", "Lista ou árvore de valores booleanos a avaliar. Também aceita múltiplos fios conectados.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Operation", "Op", "Operação lógica: AND (0), OR (1), XOR (2), NAND (3), NOR (4), XNOR (5). Padrão = AND.", GH_ParamAccess.item);

            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Result", "R", "Booleano resultante consolidado da operação lógica.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("True Count", "N_true", "Quantidade de condições satisfeitas (True).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("False Count", "N_false", "Quantidade de condições não satisfeitas (False).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("False Indices", "iFalse", "Índices (IDs locais) das entradas que falharam (para diagnóstico visual imediato).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("True Indices", "iTrue", "Índices (IDs locais) das entradas que foram satisfeitas.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Diagnostic Summary", "Rep", "Resumo diagnóstico com contagem e identificação de falhas.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Determinar operação desejada
            object opObj = null;
            if (DA.GetData(1, ref opObj) && opObj != null)
            {
                _operation = ParseOperation(opObj, _operation);
            }

            // Coletar booleanos
            // Se tiver árvore na porta 0
            if (!DA.GetDataTree(0, out GH_Structure<GH_Boolean> inTree) || inTree == null || inTree.PathCount == 0)
            {
                // Verificar se há entradas adicionais ZUI (p1, p2, p3...)
                if (Params.Input.Count > 2)
                {
                    EvaluateVariablePins(DA);
                    return;
                }

                this.Message = $"{_operation}\n(Sem Entradas)";
                return;
            }

            var outResult = new GH_Structure<GH_Boolean>();
            var outTrueCount = new GH_Structure<GH_Integer>();
            var outFalseCount = new GH_Structure<GH_Integer>();
            var outFalseIndices = new GH_Structure<GH_Integer>();
            var outTrueIndices = new GH_Structure<GH_Integer>();
            var outSummary = new GH_Structure<GH_String>();

            bool lastResult = false;
            int lastTrueCount = 0;
            int lastTotal = 0;

            foreach (var path in inTree.Paths)
            {
                var branch = inTree[path];
                var boolList = branch.Select(b => b != null && b.Value).ToList();

                EvaluateBooleanList(boolList, _operation, _strictXor,
                    out bool res, out int tCount, out int fCount, out List<int> falseIdx, out List<int> trueIdx, out string rep);

                lastResult = res;
                lastTrueCount = tCount;
                lastTotal = boolList.Count;

                outResult.Append(new GH_Boolean(res), path);
                outTrueCount.Append(new GH_Integer(tCount), path);
                outFalseCount.Append(new GH_Integer(fCount), path);

                foreach (var fi in falseIdx) outFalseIndices.Append(new GH_Integer(fi), path);
                foreach (var ti in trueIdx) outTrueIndices.Append(new GH_Integer(ti), path);

                outSummary.Append(new GH_String(rep), path);
            }

            this.Message = $"{_operation}: {(lastResult ? "TRUE" : "FALSE")}\n({lastTrueCount}/{lastTotal})";

            DA.SetDataTree(0, outResult);
            DA.SetDataTree(1, outTrueCount);
            DA.SetDataTree(2, outFalseCount);
            DA.SetDataTree(3, outFalseIndices);
            DA.SetDataTree(4, outTrueIndices);
            DA.SetDataTree(5, outSummary);
        }

        private void EvaluateVariablePins(IGH_DataAccess DA)
        {
            var boolList = new List<bool>();

            for (int i = 0; i < Params.Input.Count; i++)
            {
                if (i == 1) continue; // Porta Operation

                var list = new List<GH_Boolean>();
                if (DA.GetDataList(i, list))
                {
                    foreach (var b in list)
                    {
                        if (b != null) boolList.Add(b.Value);
                    }
                }
            }

            EvaluateBooleanList(boolList, _operation, _strictXor,
                out bool res, out int tCount, out int fCount, out List<int> falseIdx, out List<int> trueIdx, out string rep);

            var path = new GH_Path(0);
            var outResult = new GH_Structure<GH_Boolean>();
            var outTrueCount = new GH_Structure<GH_Integer>();
            var outFalseCount = new GH_Structure<GH_Integer>();
            var outFalseIndices = new GH_Structure<GH_Integer>();
            var outTrueIndices = new GH_Structure<GH_Integer>();
            var outSummary = new GH_Structure<GH_String>();

            outResult.Append(new GH_Boolean(res), path);
            outTrueCount.Append(new GH_Integer(tCount), path);
            outFalseCount.Append(new GH_Integer(fCount), path);
            foreach (var fi in falseIdx) outFalseIndices.Append(new GH_Integer(fi), path);
            foreach (var ti in trueIdx) outTrueIndices.Append(new GH_Integer(ti), path);
            outSummary.Append(new GH_String(rep), path);

            this.Message = $"{_operation}: {(res ? "TRUE" : "FALSE")}\n({tCount}/{boolList.Count})";

            DA.SetDataTree(0, outResult);
            DA.SetDataTree(1, outTrueCount);
            DA.SetDataTree(2, outFalseCount);
            DA.SetDataTree(3, outFalseIndices);
            DA.SetDataTree(4, outTrueIndices);
            DA.SetDataTree(5, outSummary);
        }

        private static void EvaluateBooleanList(
            List<bool> list,
            LogicGateOp op,
            bool strictXor,
            out bool result,
            out int trueCount,
            out int falseCount,
            out List<int> falseIndices,
            out List<int> trueIndices,
            out string summary)
        {
            falseIndices = new List<int>();
            trueIndices = new List<int>();

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i]) trueIndices.Add(i);
                else falseIndices.Add(i);
            }

            trueCount = trueIndices.Count;
            falseCount = falseIndices.Count;

            if (list.Count == 0)
            {
                result = false;
                summary = "Nenhuma condição fornecida.";
                return;
            }

            switch (op)
            {
                case LogicGateOp.AND:
                    result = falseCount == 0;
                    break;
                case LogicGateOp.OR:
                    result = trueCount > 0;
                    break;
                case LogicGateOp.XOR:
                    result = strictXor ? (trueCount == 1) : (trueCount % 2 == 1);
                    break;
                case LogicGateOp.NAND:
                    result = !(falseCount == 0);
                    break;
                case LogicGateOp.NOR:
                    result = !(trueCount > 0);
                    break;
                case LogicGateOp.XNOR:
                    bool xorVal = strictXor ? (trueCount == 1) : (trueCount % 2 == 1);
                    result = !xorVal;
                    break;
                default:
                    result = falseCount == 0;
                    break;
            }

            string failStr = falseIndices.Count == 0
                ? "Nenhuma falha"
                : $"Falha nos índices: [{string.Join(", ", falseIndices)}]";

            summary = $"{op} -> {(result ? "TRUE" : "FALSE")} ({trueCount}/{list.Count} satisfeitos). {failStr}";
        }

        private static LogicGateOp ParseOperation(object raw, LogicGateOp fallback)
        {
            if (raw == null) return fallback;
            if (raw is IGH_Goo goo) raw = goo.SafeScriptVariable();

            if (raw is int i && Enum.IsDefined(typeof(LogicGateOp), i))
            {
                return (LogicGateOp)i;
            }

            string s = raw.ToString().Trim().ToUpperInvariant();
            if (Enum.TryParse<LogicGateOp>(s, true, out var parsed))
            {
                return parsed;
            }

            return fallback;
        }

        #region IGH_VariableParameterComponent Implementation

        public bool CanInsertParameter(GH_ParameterSide side, int index)
        {
            return side == GH_ParameterSide.Input && index >= 2;
        }

        public bool CanRemoveParameter(GH_ParameterSide side, int index)
        {
            return side == GH_ParameterSide.Input && index >= 2 && Params.Input.Count > 2;
        }

        public IGH_Param CreateParameter(GH_ParameterSide side, int index)
        {
            char letter = (char)('A' + (index - 2));
            return new Param_Boolean
            {
                Name = $"Input {letter}",
                NickName = letter.ToString(),
                Description = $"Condição booleana de entrada {letter}.",
                Access = GH_ParamAccess.list,
                Optional = true
            };
        }

        public bool DestroyParameter(GH_ParameterSide side, int index)
        {
            return true;
        }

        public void VariableParameterMaintenance()
        {
            for (int i = 2; i < Params.Input.Count; i++)
            {
                char letter = (char)('A' + (i - 2));
                Params.Input[i].Name = $"Input {letter}";
                Params.Input[i].NickName = letter.ToString();
                Params.Input[i].Description = $"Condição booleana de entrada {letter}.";
                Params.Input[i].Access = GH_ParamAccess.list;
                Params.Input[i].Optional = true;
            }
        }

        #endregion

        #region Menu & Serialization

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var opMenu = new ToolStripMenuItem("Operação Lógica");

            foreach (LogicGateOp op in Enum.GetValues(typeof(LogicGateOp)))
            {
                var item = new ToolStripMenuItem(op.ToString())
                {
                    Checked = (_operation == op)
                };
                LogicGateOp target = op;
                item.Click += (s, e) =>
                {
                    RecordUndoEvent("Definir Operação Lógica");
                    _operation = target;
                    ExpireSolution(true);
                };
                opMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(opMenu);

            var xorMode = new ToolStripMenuItem("XOR Estrito (Exatamente 1 True ao invés de Paridade)")
            {
                Checked = _strictXor
            };
            xorMode.Click += (s, e) =>
            {
                RecordUndoEvent("Alternar Modo XOR Estrito");
                _strictXor = !_strictXor;
                ExpireSolution(true);
            };
            menu.Items.Add(xorMode);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetInt32("LogicOperation", (int)_operation);
            writer.SetBoolean("StrictXor", _strictXor);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("LogicOperation")) _operation = (LogicGateOp)reader.GetInt32("LogicOperation");
            if (reader.ItemExists("StrictXor")) _strictXor = reader.GetBoolean("StrictXor");
            return base.Read(reader);
        }

        #endregion

        protected override Bitmap Icon => GlauxToolsIcons.MultiLogicGate;
    }
}
