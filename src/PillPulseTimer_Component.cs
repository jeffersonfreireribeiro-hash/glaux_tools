using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Rhino;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Pilha geradora de impulsos periódicos por tempo (Clock Pulse / Metrônomo).
    /// Emite um sinal booleano True a cada intervalo programado com comportamento idêntico
    /// a um botão instantâneo do Grasshopper (True momentâneo, retornando automaticamente a False).
    /// </summary>
    public class PillPulseTimer_Component : GH_Component
    {
        // ==========================================
        // PROPRIEDADES E ESTADO DO TEMPORIZADOR
        // ==========================================
        public double IntervalSec { get; set; } = 1.0;
        public bool IsActive { get; set; } = true;
        public int LimitTicks { get; set; } = 0;
        public int PulseDurationMs { get; set; } = 60;

        private int _pulseCount = 0;
        private readonly System.Diagnostics.Stopwatch _wallClock = new System.Diagnostics.Stopwatch();
        private readonly System.Diagnostics.Stopwatch _cycleWatch = new System.Diagnostics.Stopwatch();
        private Timer _timer;

        private bool _isImpulseActive = false;
        private bool _isResettingPulse = false;
        private bool _prevTrigger = false;
        private bool _isFinished = false;

        public PillPulseTimer_Component()
            : base(
                "Pill Time Impulse / Clock Pulse",
                "PillPulse",
                "Pilha geradora de impulsos temporizados estilo metrônomo / clock pulse para o ecossistema Buraqueira Tools.\n" +
                "- Emite um sinal booleano True a cada intervalo determinado de tempo (comportamento de Botão instantâneo).\n" +
                "- Retorna automaticamente a False logo após o disparo (sem travar em True).\n" +
                "- Controle de ativação (Play/Pause), contagem de ciclos, contagem regressiva e limite de disparos.\n" +
                "- Botões interativos no canvas para forçar pulso manual ou pausar/iniciar diretamente no componente.",
                "Glaux Tools",
                "Automation")
        {
        }

        public override Guid ComponentGuid => new Guid("B7110022-E1EF-4000-8000-000000000022");

        protected override Bitmap Icon => GlauxToolsIcons.PillPulseTimer;

        public override GH_Exposure Exposure => GH_Exposure.primary;


        public override void CreateAttributes()
        {
            m_attributes = new PillPulseTimer_Attributes(this);
        }

        public Color CurrentCategoryColor => Color.FromArgb(14, 165, 233); // Cyan Elétrico

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Interval", "Intv", "Intervalo de tempo entre cada impulso em segundos (ex.: 1.0, 0.5, 5.0, 60.0). Padrão: 1.0s.", GH_ParamAccess.item, 1.0);
            pManager.AddBooleanParameter("Active", "On", "Ativa ou pausa a geração periódica de impulsos (True = Ativo/Rodando, False = Pausado). Padrão: True.", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Trigger Now", "Trig", "Disparo manual avulso: envie True (ou conecte um Botão GH) para forçar um impulso imediatamente.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Reset", "Rst", "Reseta a contagem de ciclos (N=0) e reinicia a contagem de tempo.", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("Limit", "Lim", "Limite máximo de disparos (0 = Contínuo sem limite; N = Para automaticamente após N impulsos). Padrão: 0.", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Pulse Duration", "Dur", "Duração do sinal True em milissegundos antes de retornar automaticamente a False (comportamento de botão). Padrão: 60 ms.", GH_ParamAccess.item, 60);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Impulse", "Imp", "Sinal booleano em pulso: True momentâneo a cada intervalo (comportamento de botão), False em repouso.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Count", "N", "Número total de impulsos gerados desde o início ou último reset.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Elapsed Time", "Time", "Tempo total de atividade acumulado em segundos.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Countdown", "Next", "Tempo restante em segundos até o próximo impulso programado.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Running", "Run", "True se o temporizador estiver ativamente contando e gerando impulsos; False se pausado ou finalizado.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Leitura de Reset
            bool reset = false;
            DA.GetData(3, ref reset);

            if (reset)
            {
                _pulseCount = 0;
                _wallClock.Restart();
                _cycleWatch.Restart();
                _isImpulseActive = false;
                _isResettingPulse = false;
                _isFinished = false;
            }

            // 2. Leitura dos Parâmetros
            double inInterval = IntervalSec;
            if (DA.GetData(0, ref inInterval) && inInterval > 0.01)
                IntervalSec = inInterval;

            bool inActive = IsActive;
            DA.GetData(1, ref inActive);
            IsActive = inActive;

            bool inTrigger = false;
            DA.GetData(2, ref inTrigger);

            int inLimit = LimitTicks;
            if (DA.GetData(4, ref inLimit) && inLimit >= 0)
                LimitTicks = inLimit;

            int inDuration = PulseDurationMs;
            if (DA.GetData(5, ref inDuration) && inDuration >= 10)
                PulseDurationMs = inDuration;

            // 3. Detecção de Disparo Manual Externo (Entrada Trig)
            if (inTrigger && !_prevTrigger)
            {
                _isImpulseActive = true;
                _pulseCount++;
                _cycleWatch.Restart();
            }
            _prevTrigger = inTrigger;

            // 4. Verificação de Limite
            if (LimitTicks > 0 && _pulseCount >= LimitTicks)
            {
                _isFinished = true;
                StopTimer();
            }
            else if (!reset && _isFinished && _pulseCount < LimitTicks)
            {
                _isFinished = false;
            }

            // 5. Controle do Sinal de Saída (Lógica de Pulso Instantâneo / Botão)
            bool currentOutput;
            if (_isResettingPulse)
            {
                // Concluiu a fase de pulso True; agora devolve False estável
                _isResettingPulse = false;
                _isImpulseActive = false;
                currentOutput = false;
            }
            else if (_isImpulseActive)
            {
                // Emite True no ciclo atual
                currentOutput = true;

                // Agenda retorno imediato para False após a duração definida (comportamento de Botão)
                int safeDur = Math.Max(20, Math.Min(PulseDurationMs, (int)(IntervalSec * 1000 * 0.5)));
                var doc = OnPingDocument();
                if (doc != null)
                {
                    doc.ScheduleSolution(safeDur, d =>
                    {
                        _isResettingPulse = true;
                        ExpireSolution(false);
                    });
                }
            }
            else
            {
                currentOutput = false;
            }

            // 6. Gerenciamento do Temporizador Interno
            bool shouldRun = IsActive && !_isFinished;
            if (shouldRun)
            {
                if (!_wallClock.IsRunning) _wallClock.Start();
                if (!_cycleWatch.IsRunning) _cycleWatch.Start();

                int targetIntervalMs = Math.Max(30, (int)(IntervalSec * 1000.0));
                EnsureTimerRunning(targetIntervalMs);
            }
            else
            {
                if (_wallClock.IsRunning) _wallClock.Stop();
                if (_cycleWatch.IsRunning) _cycleWatch.Stop();
                StopTimer();
            }

            // 7. Cálculo de Contagem Regressiva para o Próximo Impulso
            double elapsedCycle = _cycleWatch.Elapsed.TotalSeconds;
            double countdown = Math.Max(0.0, IntervalSec - (elapsedCycle % IntervalSec));
            if (!shouldRun) countdown = IntervalSec;

            // 8. Mensagem de Diagnóstico no Componente
            if (_isImpulseActive)
            {
                Message = $"⚡ IMPULSO #{_pulseCount}\n(True)";
            }
            else if (_isFinished)
            {
                Message = $"✔ Concluído\nN = {_pulseCount}/{LimitTicks}";
            }
            else if (!IsActive)
            {
                Message = $"⏸ Pausado\nN = {_pulseCount}";
            }
            else
            {
                Message = $"⏱ {IntervalSec:0.##}s\nN = {_pulseCount}";
            }

            // 9. Definição das Saídas
            DA.SetData(0, currentOutput);
            DA.SetData(1, _pulseCount);
            DA.SetData(2, _wallClock.Elapsed.TotalSeconds);
            DA.SetData(3, countdown);
            DA.SetData(4, shouldRun);
        }

        // ==========================================
        // TEMPORIZADOR WINFORMS NA UI THREAD DO RHINO
        // ==========================================
        private void EnsureTimerRunning(int intervalMs)
        {
            if (_timer != null && _timer.Interval == intervalMs && _timer.Enabled)
                return;

            StopTimer();

            _timer = new Timer
            {
                Interval = intervalMs
            };
            _timer.Tick += (s, e) =>
            {
                if (!IsActive || (_isFinished && LimitTicks > 0 && _pulseCount >= LimitTicks))
                {
                    StopTimer();
                    return;
                }

                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    _isImpulseActive = true;
                    _pulseCount++;
                    _cycleWatch.Restart();
                    ExpireSolution(true);
                }));
            };
            _timer.Start();
        }

        public void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
        }

        // ==========================================
        // INTERAÇÃO DIRETA VIA CANVAS (BOTÕES UI)
        // ==========================================
        public void TriggerManualImpulse()
        {
            _isImpulseActive = true;
            _pulseCount++;
            _cycleWatch.Restart();
            ExpireSolution(true);
        }

        public void ToggleActive()
        {
            IsActive = !IsActive;
            if (IsActive)
            {
                _isFinished = false;
                _wallClock.Start();
                _cycleWatch.Restart();
                int targetIntervalMs = Math.Max(30, (int)(IntervalSec * 1000.0));
                EnsureTimerRunning(targetIntervalMs);
            }
            else
            {
                StopTimer();
                _wallClock.Stop();
                _cycleWatch.Stop();
            }
            ExpireSolution(true);
        }

        public void ResetCounter()
        {
            _pulseCount = 0;
            _wallClock.Restart();
            _cycleWatch.Restart();
            _isFinished = false;
            _isImpulseActive = false;
            _isResettingPulse = false;
            ExpireSolution(true);
        }

        public double GetProgressRatio()
        {
            if (!IsActive || IntervalSec <= 0) return 0.0;
            double elapsed = _cycleWatch.Elapsed.TotalSeconds;
            double ratio = (elapsed % IntervalSec) / IntervalSec;
            return Math.Max(0.0, Math.Min(1.0, ratio));
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            StopTimer();
            base.RemovedFromDocument(document);
        }


        // ==========================================
        // SERIALIZAÇÃO
        // ==========================================
        public override bool Write(GH_IWriter writer)
        {
            writer.SetDouble("IntervalSec", IntervalSec);
            writer.SetBoolean("IsActive", IsActive);
            writer.SetInt32("LimitTicks", LimitTicks);
            writer.SetInt32("PulseDurationMs", PulseDurationMs);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("IntervalSec")) IntervalSec = reader.GetDouble("IntervalSec");
            if (reader.ItemExists("IsActive")) IsActive = reader.GetBoolean("IsActive");
            if (reader.ItemExists("LimitTicks")) LimitTicks = reader.GetInt32("LimitTicks");
            if (reader.ItemExists("PulseDurationMs")) PulseDurationMs = reader.GetInt32("PulseDurationMs");
            return base.Read(reader);
        }

        // ==========================================
        // MENU DE CONTEXTO (RIGHT-CLICK)
        // ==========================================
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "⚡ Disparar Impulso Agora", (s, e) => TriggerManualImpulse(), GlauxToolsIcons.PillPulseTimer);
            Menu_AppendItem(menu, IsActive ? "⏸ Pausar Temporizador" : "▶ Iniciar Temporizador", (s, e) => ToggleActive());
            Menu_AppendItem(menu, "🔄 Resetar Contagem (N = 0)", (s, e) => ResetCounter());
            Menu_AppendSeparator(menu);

            // Submenu de Intervalos Pré-definidos
            var intMenu = Menu_AppendItem(menu, "⏱ Intervalo Pré-definido");
            AddIntervalPreset(intMenu, "50 ms (20 Hz - Ultra Rápido)", 0.05);
            AddIntervalPreset(intMenu, "100 ms (10 Hz)", 0.1);
            AddIntervalPreset(intMenu, "250 ms (4 Hz)", 0.25);
            AddIntervalPreset(intMenu, "500 ms (2 Hz / 0.5s)", 0.5);
            AddIntervalPreset(intMenu, "1.0 s (1 Hz - Padrão)", 1.0);
            AddIntervalPreset(intMenu, "2.0 s", 2.0);
            AddIntervalPreset(intMenu, "5.0 s", 5.0);
            AddIntervalPreset(intMenu, "10.0 s", 10.0);
            AddIntervalPreset(intMenu, "30.0 s", 30.0);
            AddIntervalPreset(intMenu, "60.0 s (1 minuto)", 60.0);

            // Submenu de Limite de Impulsos
            var limMenu = Menu_AppendItem(menu, "🔢 Limite de Disparos");
            AddLimitPreset(limMenu, "Infinito / Contínuo (Sem Limite)", 0);
            AddLimitPreset(limMenu, "10 Disparos", 10);
            AddLimitPreset(limMenu, "25 Disparos", 25);
            AddLimitPreset(limMenu, "50 Disparos", 50);
            AddLimitPreset(limMenu, "100 Disparos", 100);

            Menu_AppendSeparator(menu);

            var hiddenMenu = new ToolStripMenuItem("⚡ Conexão Oculta (Modo Wallacei / Hidden Wire)")
            {
                ToolTipText = "Conecta/desconecta cabos físicos ocultos (Hidden Wire) preservando a sincronia DAG sequencial para Wallacei e Galapagos."
            };

            var connectAllItem = new ToolStripMenuItem("⚡ CONECTAR TODOS os Pills do Canvas com Cabos Ocultos (Recomendado para Wallacei)");
            connectAllItem.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc == null) return;
                int c = PillHub.ConnectAllDocumentPillsHidden(doc);
                doc.NewSolution(false);
                MessageBox.Show($"Todos os {c} receptores do Canvas foram conectados com cabos ocultos (hidden wire)!\nA sequência DAG está garantida para Wallacei e Galapagos.",
                    "Pill System - Modo Wallacei Ativado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            hiddenMenu.DropDownItems.Add(connectAllItem);

            var disconnectAllItem = new ToolStripMenuItem("Desconectar TODOS os Pills do Canvas (Voltar ao Modo 100% Sem Fio)");
            disconnectAllItem.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc == null) return;
                int c = PillHub.DisconnectAllDocumentPillsHidden(doc);
                doc.NewSolution(false);
                MessageBox.Show($"Todos os {c} receptores do Canvas voltaram ao modo 100% sem fio em memória.",
                    "Pill System - Modo Sem Fio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            hiddenMenu.DropDownItems.Add(disconnectAllItem);

            menu.Items.Add(hiddenMenu);
        }


        private void AddIntervalPreset(ToolStripMenuItem parent, string text, double sec)
        {
            bool isCurrent = Math.Abs(IntervalSec - sec) < 0.001;
            Menu_AppendItem(parent.DropDown, text, (s, e) =>
            {
                IntervalSec = sec;
                ExpireSolution(true);
            }, true, isCurrent);
        }

        private void AddLimitPreset(ToolStripMenuItem parent, string text, int limit)
        {
            bool isCurrent = LimitTicks == limit;
            Menu_AppendItem(parent.DropDown, text, (s, e) =>
            {
                LimitTicks = limit;
                ExpireSolution(true);
            }, true, isCurrent);
        }
    }

    /// <summary>
    /// Atributos visuais para a Pilha de Impulso (PillPulseTimer).
    /// Adiciona um painel interativo inferior com:
    /// - LED indicador de pulso luminoso (neon glow).
    /// - Barra de progresso contínua para o próximo tick.
    /// - Botão "⚡ PULSE" para disparo avulso manual.
    /// - Botão "▶ / ⏸" para alternar ativação.
    /// - Alinhamento dos parâmetros aos extremos da pilha e ícone perfeitamente centralizado.
    /// </summary>
    public class PillPulseTimer_Attributes : GH_ComponentAttributes
    {
        private const float CARD_HEIGHT = 34f;
        private const float MIN_CARD_WIDTH = 158f;

        private RectangleF m_btnPulseRect;
        private RectangleF m_btnToggleRect;
        private RectangleF m_cardRect;

        public PillPulseTimer_Attributes(PillPulseTimer_Component owner) : base(owner)
        {
        }

        public PillPulseTimer_Component Comp => Owner as PillPulseTimer_Component;

        protected override void Layout()
        {
            base.Layout();

            float oldRight = Bounds.Right;
            RectangleF b = Bounds;
            b.Width = Math.Max(b.Width, MIN_CARD_WIDTH);
            b.Height += CARD_HEIGHT + 6f;
            Bounds = b;

            // Alinha os parâmetros de saída na borda direita expandida
            float deltaX = Bounds.Right - oldRight;
            if (Math.Abs(deltaX) > 0.5f && Owner.Params?.Output != null)
            {
                foreach (var p in Owner.Params.Output)
                {
                    if (p.Attributes != null)
                    {
                        var pb = p.Attributes.Bounds;
                        pb.X += deltaX;
                        p.Attributes.Bounds = pb;
                        var piv = p.Attributes.Pivot;
                        piv.X += deltaX;
                        p.Attributes.Pivot = piv;
                    }
                }
            }
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && Comp != null)
            {
                if (m_btnPulseRect.Contains(e.CanvasLocation))
                {
                    Comp.TriggerManualImpulse();
                    sender.Refresh();
                    return GH_ObjectResponse.Handled;
                }

                if (m_btnToggleRect.Contains(e.CanvasLocation))
                {
                    Comp.ToggleActive();
                    sender.Refresh();
                    return GH_ObjectResponse.Handled;
                }
            }
            return base.RespondToMouseDown(sender, e);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            // Centraliza o ícone do componente no Pivot real dos Bounds expandidos
            if (channel == GH_CanvasChannel.Objects)
            {
                var _savedPivot = Pivot;
                Pivot = new PointF(Bounds.X + Bounds.Width / 2f, _savedPivot.Y);
                base.Render(canvas, graphics, channel);
                Pivot = _savedPivot;
            }
            else
            {
                base.Render(canvas, graphics, channel);
            }

            if (channel == GH_CanvasChannel.Objects)
            {
                if (Comp == null) return;

                RectangleF b = Bounds;
                m_cardRect = new RectangleF(b.X + 4f, b.Bottom - CARD_HEIGHT - 3f, b.Width - 8f, CARD_HEIGHT);

                // 1. Fundo do Card Dashboard
                using (var bgBrush = new SolidBrush(Color.FromArgb(22, 26, 35)))
                using (var borderPen = new Pen(Color.FromArgb(60, 70, 85), 1.2f))
                {
                    graphics.FillRectangle(bgBrush, m_cardRect);
                    graphics.DrawRectangle(borderPen, m_cardRect.X, m_cardRect.Y, m_cardRect.Width, m_cardRect.Height);
                }

                // 2. Barra de Progresso Subjacente (Tempo até próximo tick)
                double ratio = Comp.GetProgressRatio();
                if (ratio > 0.0)
                {
                    float barW = (float)(m_cardRect.Width * ratio);
                    var barRect = new RectangleF(m_cardRect.X, m_cardRect.Bottom - 2.5f, barW, 2.5f);
                    Color barColor = Comp.IsActive ? Color.FromArgb(14, 165, 233) : Color.FromArgb(100, 110, 120);
                    using (var barBrush = new SolidBrush(barColor))
                    {
                        graphics.FillRectangle(barBrush, barRect);
                    }
                }

                // 3. LED Indicador de Pulso
                float ledX = m_cardRect.X + 8f;
                float ledY = m_cardRect.Y + (CARD_HEIGHT - 10f) / 2f;
                RectangleF ledRect = new RectangleF(ledX, ledY, 10f, 10f);

                if (Comp.Message != null && Comp.Message.Contains("IMPULSO"))
                {
                    // Flash do pulso (True) - Brilho Neon Esmeralda / Amarelo
                    using (var glowBrush = new SolidBrush(Color.FromArgb(80, 52, 211, 153)))
                    {
                        graphics.FillEllipse(glowBrush, ledRect.X - 3, ledRect.Y - 3, 16, 16);
                    }
                    using (var ledBrush = new SolidBrush(Color.FromArgb(52, 211, 153)))
                    using (var ledPen = new Pen(Color.FromArgb(254, 240, 138), 1.5f))
                    {
                        graphics.FillEllipse(ledBrush, ledRect);
                        graphics.DrawEllipse(ledPen, ledRect);
                    }
                }
                else if (Comp.IsActive)
                {
                    // Ativo / Standby - LED Cyan suave
                    using (var ledBrush = new SolidBrush(Color.FromArgb(14, 165, 233)))
                    using (var ledPen = new Pen(Color.FromArgb(56, 189, 248), 1.2f))
                    {
                        graphics.FillEllipse(ledBrush, ledRect);
                        graphics.DrawEllipse(ledPen, ledRect);
                    }
                }
                else
                {
                    // Pausado - LED Cinza/Âmbar escuro
                    using (var ledBrush = new SolidBrush(Color.FromArgb(70, 75, 85)))
                    using (var ledPen = new Pen(Color.FromArgb(90, 95, 110), 1f))
                    {
                        graphics.FillEllipse(ledBrush, ledRect);
                        graphics.DrawEllipse(ledPen, ledRect);
                    }
                }

                // 4. Botão "PULSE" (Disparo Manual)
                float btnPulseW = 54f;
                float btnH = 20f;
                float btnY = m_cardRect.Y + (CARD_HEIGHT - btnH) / 2f;
                m_btnPulseRect = new RectangleF(m_cardRect.Right - btnPulseW - 32f, btnY, btnPulseW, btnH);

                using (var btnBrush = new SolidBrush(Color.FromArgb(32, 38, 50)))
                using (var btnPen = new Pen(Color.FromArgb(14, 165, 233), 1.2f))
                {
                    graphics.FillRectangle(btnBrush, m_btnPulseRect);
                    graphics.DrawRectangle(btnPen, m_btnPulseRect.X, m_btnPulseRect.Y, m_btnPulseRect.Width, m_btnPulseRect.Height);
                }

                FontFamily fam = PillViewportCapture_Component.GetUIFontFamily();
                using (var btnFont = new Font(fam, 7.5f, FontStyle.Bold))
                using (var btnTextBrush = new SolidBrush(Color.FromArgb(224, 242, 254)))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    graphics.DrawString("⚡ PULSE", btnFont, btnTextBrush, m_btnPulseRect, sf);
                }

                // 5. Botão "▶ / ⏸" (Play/Pause: Verde quando Rodando, Vermelho quando Pausado)
                float btnToggleW = 24f;
                m_btnToggleRect = new RectangleF(m_cardRect.Right - btnToggleW - 4f, btnY, btnToggleW, btnH);

                Color toggleBg = Comp.IsActive ? Color.FromArgb(25, 52, 38) : Color.FromArgb(52, 26, 26);
                Color toggleBorder = Comp.IsActive ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68);
                Color toggleFg = Comp.IsActive ? Color.FromArgb(134, 239, 172) : Color.FromArgb(252, 165, 165);

                using (var toggleBrush = new SolidBrush(toggleBg))
                using (var togglePen = new Pen(toggleBorder, 1.2f))
                {
                    graphics.FillRectangle(toggleBrush, m_btnToggleRect);
                    graphics.DrawRectangle(togglePen, m_btnToggleRect.X, m_btnToggleRect.Y, m_btnToggleRect.Width, m_btnToggleRect.Height);
                }

                using (var toggleFont = new Font(fam, 8.5f, FontStyle.Bold))
                using (var toggleTextBrush = new SolidBrush(toggleFg))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    graphics.DrawString(Comp.IsActive ? "⏸" : "▶", toggleFont, toggleTextBrush, m_btnToggleRect, sf);
                }


                // 6. Texto de Intervalo no Centro do Card
                RectangleF labelRect = new RectangleF(ledRect.Right + 4f, m_cardRect.Y, m_btnPulseRect.X - ledRect.Right - 8f, CARD_HEIGHT);
                using (var labelFont = new Font(fam, 7.5f, FontStyle.Regular))
                using (var labelBrush = new SolidBrush(Color.FromArgb(200, 210, 225)))
                using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
                {
                    string label = $"{Comp.IntervalSec:0.##}s";
                    graphics.DrawString(label, labelFont, labelBrush, labelRect, sf);
                }
            }
        }
    }
}
