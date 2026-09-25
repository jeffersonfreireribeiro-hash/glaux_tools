using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;

namespace Buraqueira_Tools
{
    public class ConditionalTrigger_Component : GH_Component
    {
        private bool _lastCondition = false;
        private int _fireCount = 0;
        private GH_Structure<IGH_Goo> _lastOutput = new GH_Structure<IGH_Goo>();
        private bool _justFired = false;
        private System.Windows.Forms.Timer _debounceTimer;
        private GH_Structure<IGH_Goo> _pendingData;
        private bool _isDebouncing = false;

        public ConditionalTrigger_Component()
            : base(
                "Conditional Trigger / Debounced Watcher",
                "Trigger",
                "Dispara a recomputação a jusante apenas em borda de subida (False -> True), mudança de estado ou após um intervalo de estabilidade (Debounce) para evitar travamentos por sliders oscilantes.",
                "Glaux Tools",
                "Automation")
        {
        }

        public override Guid ComponentGuid => new Guid("1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Árvore de dados a ser liberada sob condição ou após estabilidade.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Trigger / Condition", "T", "Condição booleana de disparo.", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("Mode", "M", "Modo de disparo:\n0 = Borda de Subida (Rising Edge: False -> True)\n1 = Debounce / Estabilidade (espera ms sem oscilações antes de disparar)\n2 = Mudança de Valor (qualquer transição True/False)\n3 = Pass-Through contínuo enquanto True", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Debounce Ms", "ms", "Tempo de estabilidade em milissegundos para o modo Debounce (padrão: 250 ms).", GH_ParamAccess.item, 250);
            pManager.AddBooleanParameter("Force Fire", "F", "Força um disparo imediato avulso.", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Output", "O", "Dados liberados após a validação do gatilho ou debounce.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Has Fired", "Fired", "Pulso booleano indicando que o evento de disparo ocorreu nesta iteração.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Event Count", "N", "Total de disparos acumulados.", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Estado atual do gatilho.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) || inTree == null)
            {
                inTree = new GH_Structure<IGH_Goo>();
            }

            bool condition = false;
            DA.GetData(1, ref condition);

            int mode = 0;
            DA.GetData(2, ref mode);

            int debounceMs = 250;
            DA.GetData(3, ref debounceMs);
            if (debounceMs < 10) debounceMs = 10;

            bool force = false;
            DA.GetData(4, ref force);

            bool shouldFire = false;
            string status = "Idle";

            if (force)
            {
                shouldFire = true;
                status = "Force Fired";
            }
            else
            {
                switch (mode)
                {
                    case 0: // Rising Edge (False -> True)
                        if (condition && !_lastCondition)
                        {
                            shouldFire = true;
                            status = "Rising Edge (Fired)";
                        }
                        else
                        {
                            status = condition ? "Holding True (Waiting Low)" : "Idle (False)";
                        }
                        break;

                    case 1: // Debounce / Estabilidade
                        _pendingData = inTree.Duplicate();
                        if (!_isDebouncing)
                        {
                            _isDebouncing = true;
                            status = $"Debouncing ({debounceMs}ms)...";
                            StartDebounceTimer(debounceMs);
                        }
                        else
                        {
                            // Reiniciar contagem do timer se novos dados continuam chegando
                            RestartDebounceTimer(debounceMs);
                            status = $"Debouncing (Reset {debounceMs}ms)...";
                        }
                        break;

                    case 2: // Any Change (Toggle)
                        if (condition != _lastCondition)
                        {
                            shouldFire = true;
                            status = $"State Changed -> {condition}";
                        }
                        else
                        {
                            status = "Stable";
                        }
                        break;

                    case 3: // Continuous Pass when True
                        if (condition)
                        {
                            shouldFire = true;
                            status = "Pass-Through (Active)";
                        }
                        else
                        {
                            status = "Blocked (False)";
                        }
                        break;
                }
            }

            if (shouldFire)
            {
                _lastOutput = inTree.Duplicate();
                _fireCount++;
                _justFired = true;
            }
            else
            {
                _justFired = false;
            }

            _lastCondition = condition;

            DA.SetDataTree(0, _lastOutput);
            DA.SetData(1, _justFired);
            DA.SetData(2, _fireCount);
            DA.SetData(3, status);

            this.Message = $"{status}\nN = {_fireCount}";
        }

        private void StartDebounceTimer(int intervalMs)
        {
            if (_debounceTimer != null)
            {
                _debounceTimer.Stop();
                _debounceTimer.Dispose();
            }

            _debounceTimer = new System.Windows.Forms.Timer();
            _debounceTimer.Interval = intervalMs;
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                _isDebouncing = false;
                if (_pendingData != null)
                {
                    _lastOutput = _pendingData;
                    _fireCount++;
                    _justFired = true;
                }

                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    ExpireSolution(true);
                }));
            };
            _debounceTimer.Start();
        }

        private void RestartDebounceTimer(int intervalMs)
        {
            if (_debounceTimer != null)
            {
                _debounceTimer.Stop();
                _debounceTimer.Interval = intervalMs;
                _debounceTimer.Start();
            }
            else
            {
                StartDebounceTimer(intervalMs);
            }
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            if (_debounceTimer != null)
            {
                _debounceTimer.Stop();
                _debounceTimer.Dispose();
                _debounceTimer = null;
            }
            base.RemovedFromDocument(document);
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.ConditionalTrigger;
    }
}
