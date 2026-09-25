using System;
using System.Diagnostics;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;

namespace Buraqueira_Tools
{
    public class ConditionalTimer_Component : GH_Component
    {
        private int _currentTicks = 0;
        private bool _isExecuting = false;
        private bool _isFinished = false;
        private bool _prevCondition = false;
        private bool _justTicked = false;
        private GH_Structure<IGH_Goo> _lastOutput = new GH_Structure<IGH_Goo>();
        private System.Windows.Forms.Timer _timer;

        public ConditionalTimer_Component()
            : base(
                "Conditional Timer / Smart Watcher",
                "SmartTimer",
                "Temporizador inteligente e sentinela condicional (Watcher). Executa ciclos ou gravações apenas quando critérios forem atingidos, com controle estrito de disparos máximos (One-Shot, Burst ou Polling) para prevenir loops infinitos e travamentos.",
                "Glaux Tools",
                "Automation")
        {
        }

        public override Guid ComponentGuid => new Guid("9e8f7a6b-5c4d-3e2f-1a0b-9c8d7e6f5a4b");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Árvore de dados opcional a ser liberada durante os pulsos de execução.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Arm / Enable", "E", "Habilita ou desarma o temporizador/watcher (True = Armado/Pronto, False = Desarmado/Pausa).", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Condition / Trigger", "C", "Critério booleano de ativação (quando True, aciona a contagem ou execução).", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("Interval Ms", "ms", "Intervalo entre pulsos ou checagens em milissegundos (padrão: 100 ms, mínimo: 10 ms).", GH_ParamAccess.item, 100);
            pManager.AddIntegerParameter("Max Ticks", "Max", "Limite máximo de pulsos por ativação (1 = One-Shot / Disparo Único; N = Rajada de N passos; 0 = Sem limite estrito enquanto a condição for True). Previne loops infinitos.", GH_ParamAccess.item, 1);
            pManager.AddIntegerParameter("Mode", "M", "Modo de Operação:\n0 = One-Shot (Dispara 1 vez na transição False->True e desarma)\n1 = Burst Mode (Dispara N passos com intervalo 'ms' enquanto True)\n2 = Polling Watcher (Checa a cada 'ms'; no momento em que a condição for True, dispara e desarma)\n3 = Gated Stream (Timer contínuo enquanto True, com trava de segurança em Max)", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Reset / Re-Arm", "R", "Reseta o contador de iterações, cancela agendamentos pendentes e rearma o temporizador.", GH_ParamAccess.item, false);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Output Data", "O", "Dados liberados durante os pulsos de execução ativos.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Tick Pulse", "Tick", "Pulso booleano emitido (True) no ciclo/disparo atual (ideal para acionar gravadores e solvers).", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Tick Count", "Count", "Contagem de pulsos executados no ciclo atual.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Is Active", "Active", "True se o temporizador estiver ativamente executando ciclos.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Finished / Done", "Done", "True quando o ciclo de disparos for concluído e o componente desarmar com sucesso.", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "Status", "Diagnóstico e estado em tempo real do temporizador.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool reset = false;
            DA.GetData(6, ref reset);

            if (reset)
            {
                StopTimer();
                _currentTicks = 0;
                _isExecuting = false;
                _isFinished = false;
                _prevCondition = false;
                _justTicked = false;
                _lastOutput = new GH_Structure<IGH_Goo>();
            }

            bool hasData = DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) && inTree != null;
            if (!hasData) inTree = new GH_Structure<IGH_Goo>();

            bool armed = true;
            DA.GetData(1, ref armed);

            bool condition = false;
            DA.GetData(2, ref condition);

            int intervalMs = 100;
            DA.GetData(3, ref intervalMs);
            if (intervalMs < 10) intervalMs = 10;

            int maxTicks = 1;
            DA.GetData(4, ref maxTicks);
            if (maxTicks < 0) maxTicks = 0;

            int mode = 0;
            DA.GetData(5, ref mode);

            _justTicked = false;
            string status = "Desarmado";

            if (!armed)
            {
                StopTimer();
                _isExecuting = false;
                status = "Desarmado (Arm = False)";
            }
            else if (_isFinished && !reset)
            {
                status = $"Finalizado ({_currentTicks}/{maxTicks} disparos) [Aguardando Reset]";
            }
            else
            {
                switch (mode)
                {
                    case 0: // One-Shot / Disparo Único
                        if (condition && !_prevCondition)
                        {
                            // Borda de subida detectada
                            _justTicked = true;
                            _currentTicks = 1;
                            _lastOutput = inTree.Duplicate();
                            _isFinished = true;
                            _isExecuting = false;
                            status = "Disparo Único Executado (Desarmado)";
                        }
                        else if (_isFinished)
                        {
                            status = "Disparo Único Concluído (Aguardando Reset ou Nova Borda)";
                        }
                        else
                        {
                            status = condition ? "Condição Satisfeita (Já Disparado)" : "Aguardando Condição (Armado)...";
                        }
                        break;

                    case 1: // Burst Mode (N passos temporizados)
                        if (condition)
                        {
                            if (!_isExecuting && !_isFinished)
                            {
                                _isExecuting = true;
                                _currentTicks = 0;
                            }

                            if (_isExecuting)
                            {
                                _currentTicks++;
                                _justTicked = true;
                                _lastOutput = inTree.Duplicate();

                                if (maxTicks > 0 && _currentTicks >= maxTicks)
                                {
                                    _isExecuting = false;
                                    _isFinished = true;
                                    StopTimer();
                                    status = $"Rajada Concluída: {_currentTicks}/{maxTicks} passos (Parada Segura)";
                                }
                                else
                                {
                                    status = $"Executando Rajada: Passo {_currentTicks}/{(maxTicks > 0 ? maxTicks.ToString() : "∞")} ({intervalMs}ms)";
                                    ScheduleNextTick(intervalMs);
                                }
                            }
                        }
                        else
                        {
                            if (_isExecuting)
                            {
                                _isExecuting = false;
                                StopTimer();
                                status = $"Pausado na iteração {_currentTicks} (Condição tornou-se False)";
                            }
                            else
                            {
                                status = "Aguardando Condição True para iniciar rajada...";
                            }
                        }
                        break;

                    case 2: // Polling Watcher
                        if (condition)
                        {
                            // Critério atingido durante a observação
                            StopTimer();
                            _justTicked = true;
                            _currentTicks++;
                            _lastOutput = inTree.Duplicate();
                            _isFinished = true;
                            _isExecuting = false;
                            status = $"Critério Atingido! Disparo efetuado no passo {_currentTicks} (Desarmado)";
                        }
                        else
                        {
                            // Critério não satisfeito: continua observando no intervalo especificado
                            _isExecuting = true;
                            _currentTicks++;
                            status = $"Sentinela Observando... Checagem #{_currentTicks} (a cada {intervalMs}ms)";
                            ScheduleNextTick(intervalMs);
                        }
                        break;

                    case 3: // Gated Stream com limite de segurança
                        if (condition)
                        {
                            _isExecuting = true;
                            _currentTicks++;
                            _justTicked = true;
                            _lastOutput = inTree.Duplicate();

                            if (maxTicks > 0 && _currentTicks >= maxTicks)
                            {
                                _isExecuting = false;
                                _isFinished = true;
                                StopTimer();
                                status = $"Limite Máximo de Segurança Atingido: {_currentTicks} iterações (Parada Automática)";
                            }
                            else
                            {
                                status = $"Fluxo Temporizado Ativo: Iteração {_currentTicks} ({intervalMs}ms)";
                                ScheduleNextTick(intervalMs);
                            }
                        }
                        else
                        {
                            _isExecuting = false;
                            StopTimer();
                            status = "Stream Pausado (Condição = False)";
                        }
                        break;
                }
            }

            _prevCondition = condition;

            DA.SetDataTree(0, _lastOutput);
            DA.SetData(1, _justTicked);
            DA.SetData(2, _currentTicks);
            DA.SetData(3, _isExecuting);
            DA.SetData(4, _isFinished);
            DA.SetData(5, status);

            this.Message = $"{(_isExecuting ? "⏱ Rodando" : (_isFinished ? "✔ Concluído" : "⏸ Standby"))}\nN = {_currentTicks}";
        }

        private void ScheduleNextTick(int intervalMs)
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
            }

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = Math.Max(10, intervalMs);
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    ExpireSolution(true);
                }));
            };
            _timer.Start();
        }

        private void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            StopTimer();
            base.RemovedFromDocument(document);
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.ConditionalTimer;
    }
}
