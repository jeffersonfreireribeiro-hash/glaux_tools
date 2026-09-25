using System;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class StateLatch_Component : GH_Component
    {
        private GH_Structure<IGH_Goo> _latchedState = new GH_Structure<IGH_Goo>();
        private DateTime _lastUpdateTime = DateTime.MinValue;
        private bool _hasValidState = false;

        public StateLatch_Component()
            : base(
                "State Latch / Flip-Flop (Gating)",
                "StateLatch",
                "Portão de retenção de dados que 'trava' o último estado válido. Quando o portão fecha ou erros/geometrias nulas chegam, a saída congela no valor anterior, impedindo que falhas propaguem.",
                "Glaux Tools",
                "Automation")
        {
        }

        public override Guid ComponentGuid => new Guid("2b3c4d5e-6f7a-8b9c-0d1e-2f3a4b5c6d7e");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Fluxo de dados de entrada.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Gate / Pass", "G", "Portão de dados (True = Aberto/Atualiza estado ao vivo, False = Fechado/Trava no último valor válido).", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Protect Nulls/Errors", "Err", "Se True, trava automaticamente o último estado válido caso os dados de entrada cheguem vazios ou nulos.", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Reset", "R", "Reseta o estado travado em memória.", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Latched Data", "D", "Dados transmitidos ou travados.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Is Latched", "L", "True se a saída estiver congelada em um estado anterior.", GH_ParamAccess.item);
            pManager.AddTextParameter("Last Update", "T", "Horário da última atualização válida do estado.", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Diagnóstico do estado de retenção.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool reset = false;
            DA.GetData(3, ref reset);
            if (reset)
            {
                _latchedState = new GH_Structure<IGH_Goo>();
                _hasValidState = false;
                _lastUpdateTime = DateTime.MinValue;
            }

            bool gateOpen = true;
            DA.GetData(1, ref gateOpen);

            bool protectNulls = true;
            DA.GetData(2, ref protectNulls);

            bool hasIncoming = DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) && inTree != null && inTree.DataCount > 0;

            bool isNullOrEmpty = !hasIncoming || inTree.DataCount == 0;

            bool isLatched = false;
            string status;

            if (gateOpen)
            {
                if (protectNulls && isNullOrEmpty)
                {
                    // Proteção ativa: dado nulo não substitui o bom
                    isLatched = true;
                    status = _hasValidState ? "Protected (Frozen on Null)" : "Empty / No Valid State";
                }
                else
                {
                    // Atualiza estado normalmente
                    _latchedState = inTree != null ? inTree.Duplicate() : new GH_Structure<IGH_Goo>();
                    _hasValidState = _latchedState.DataCount > 0;
                    _lastUpdateTime = DateTime.Now;
                    isLatched = false;
                    status = "Live (Pass-Through)";
                }
            }
            else
            {
                // Portão fechado: segurar estado anterior
                isLatched = true;
                status = _hasValidState ? "Latched (Gate Closed)" : "Gate Closed (Empty)";
            }

            string timeStr = (_lastUpdateTime == DateTime.MinValue) ? "Nunca" : _lastUpdateTime.ToString("HH:mm:ss.fff");

            DA.SetDataTree(0, _latchedState);
            DA.SetData(1, isLatched);
            DA.SetData(2, timeStr);
            DA.SetData(3, status);

            this.Message = $"{status}\n{(isLatched ? "🔒 Trava" : "🟢 Aberto")}";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.StateLatch;
    }
}
