// PillDisabler_Component.cs
// Pilula Desativadora / Kill Switch para o ecossistema BURAQUEIRA Tools.
// Desativa de verdade (Ctrl+E / Locked) componentes e pilhas inteiras no canvas do Grasshopper.
// Corta completamente a execucao e consumo de CPU/GPU, com cabos e molduras estilo Galapagos.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;
using GH_IO.Serialization;
using Rhino;

namespace Buraqueira_Tools
{
    public class PillDisabler_Component : GH_Component
    {
        public bool DisableTargetStack { get; set; } = true;
        public bool AlternateABMode { get; set; } = false;
        public bool ActiveIsA { get; set; } = true; // No modo A/B: true = A ativa (B desativada), false = B ativa (A desativada)
        public bool IncludeDownstream { get; set; } = true;
        public bool SuppressDefaultWires { get; set; } = true;

        public List<Guid> StackAGuids { get; set; } = new List<Guid>();
        public List<Guid> StackBGuids { get; set; } = new List<Guid>();

        private bool _idleScheduled = false;

        public PillDisabler_Component()
            : base(
                "Pill Disabler / Kill Switch",
                "PillDisabler",
                "Desativa de verdade (Ctrl+E / Locked) componentes, pilhas ou Pilulas no canvas do Grasshopper.\n" +
                "- Zera 100% o consumo de CPU e GPU dos componentes desligados.\n" +
                "- Suporta corte direto (Mute) ou alternancia mutua entre Pilha A e Pilha B.\n" +
                "- Conexao estilo Galapagos: arraste dos grips ou clique com botao direito para conectar pilhas inteiras.\n" +
                "- Cabos com setas e molduras visuais no canvas indicando os componentes controlados.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("B8A1C2D3-E4F5-6A7B-8C9D-0E1F2A3B4C5D");

        protected override Bitmap Icon => GlauxToolsIcons.PillDisabler;

        public override void CreateAttributes()
        {
            m_attributes = new PillDisablerAttributes(this);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            HideDefaultWires();
        }

        public void HideDefaultWires()
        {
            if (!SuppressDefaultWires) return;
            if (Params?.Input != null)
            {
                if (Params.Input.Count > 1 && Params.Input[1] != null)
                    Params.Input[1].WireDisplay = GH_ParamWireDisplay.hidden;
                if (Params.Input.Count > 2 && Params.Input[2] != null)
                    Params.Input[2].WireDisplay = GH_ParamWireDisplay.hidden;
            }
        }

        public override bool Write(GH_IWriter writer)
        {
            try
            {
                writer.SetInt32("StackACount", StackAGuids.Count);
                for (int i = 0; i < StackAGuids.Count; i++) writer.SetGuid("StackAGuid", i, StackAGuids[i]);

                writer.SetInt32("StackBCount", StackBGuids.Count);
                for (int i = 0; i < StackBGuids.Count; i++) writer.SetGuid("StackBGuid", i, StackBGuids[i]);

                writer.SetBoolean("SuppressDefaultWires", SuppressDefaultWires);
            }
            catch { }
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            try
            {
                StackAGuids.Clear();
                if (reader.ItemExists("StackACount"))
                {
                    int cA = reader.GetInt32("StackACount");
                    for (int i = 0; i < cA; i++)
                    {
                        if (reader.ItemExists("StackAGuid", i)) StackAGuids.Add(reader.GetGuid("StackAGuid", i));
                    }
                }

                StackBGuids.Clear();
                if (reader.ItemExists("StackBCount"))
                {
                    int cB = reader.GetInt32("StackBCount");
                    for (int i = 0; i < cB; i++)
                    {
                        if (reader.ItemExists("StackBGuid", i)) StackBGuids.Add(reader.GetGuid("StackBGuid", i));
                    }
                }

                if (reader.ItemExists("SuppressDefaultWires"))
                {
                    SuppressDefaultWires = reader.GetBoolean("SuppressDefaultWires");
                }
            }
            catch { }
            return base.Read(reader);
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Menu_AppendSeparator(menu);

            Menu_AppendItem(menu, "🔗 Conectar Selecionados na Pilha A", (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc == null) return;
                int added = 0;
                foreach (var obj in doc.SelectedObjects())
                {
                    if (obj == this) continue;
                    if (!StackAGuids.Contains(obj.InstanceGuid))
                    {
                        StackAGuids.Add(obj.InstanceGuid);
                        added++;
                    }
                }
                if (added > 0)
                {
                    RecordUndoEvent("Conectar Pilha A");
                    ExpireSolution(true);
                }
            });

            Menu_AppendItem(menu, "🔗 Conectar Selecionados na Pilha B", (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc == null) return;
                int added = 0;
                foreach (var obj in doc.SelectedObjects())
                {
                    if (obj == this) continue;
                    if (!StackBGuids.Contains(obj.InstanceGuid))
                    {
                        StackBGuids.Add(obj.InstanceGuid);
                        added++;
                    }
                }
                if (added > 0)
                {
                    RecordUndoEvent("Conectar Pilha B");
                    ExpireSolution(true);
                }
            });

            if (StackAGuids.Count > 0 || StackBGuids.Count > 0)
            {
                Menu_AppendItem(menu, "❌ Desconectar Todas as Pilhas", (s, e) =>
                {
                    RecordUndoEvent("Desconectar Pilhas");
                    StackAGuids.Clear();
                    StackBGuids.Clear();
                    ExpireSolution(true);
                });
            }

            Menu_AppendSeparator(menu);
            var suppressItem = new ToolStripMenuItem("Ocultar Cabos Padrão do Grasshopper na Seleção")
            {
                Checked = SuppressDefaultWires,
                ToolTipText = "Quando ativado (padrão), oculta completamente os cabos verdes padrão do Grasshopper ao clicar no Pill Disabler, mantendo apenas os cabos customizados estilo Galapagos."
            };
            suppressItem.Click += (s, e) =>
            {
                RecordUndoEvent("Alternar Ocultação de Cabos");
                SuppressDefaultWires = !SuppressDefaultWires;
                Grasshopper.Instances.ActiveCanvas?.Invalidate();
            };
            menu.Items.Add(suppressItem);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0: Mute / Disable ou Seletor
            pManager.AddBooleanParameter(
                "Mute / Disable", "Mute",
                "Se True (padrao), desativa a pilha de verdade (Locked = true). Se False, mantem ativada.\n" +
                "No modo A/B: True = Pilha A ativa (B desativa), False = Pilha B ativa (A desativa).",
                GH_ParamAccess.item,
                true);
            pManager[0].Optional = true;

            // 1: Pilha Principal / Pilha A
            pManager.AddGenericParameter(
                "Target Stack (A)", "StackA",
                "Componente(s) ou Pilulas da pilha a ser controlada. Puxe conexoes dos componentes ou arraste o conector.",
                GH_ParamAccess.list);
            pManager[1].Optional = true;

            // 2: Pilha Secundaria / Pilha B (Opcional - Modo Alternador Mutuo)
            pManager.AddGenericParameter(
                "Stack B (Optional)", "StackB",
                "Segunda pilha para alternancia mutua. Se conectada, quando A estiver ativa, B sera desativada, e vice-versa.",
                GH_ParamAccess.list);
            pManager[2].Optional = true;

            // 3: Desativar a Jusante (Downstream)
            pManager.AddBooleanParameter(
                "Downstream", "Down",
                "Se True (padrao), tambem desativa todos os componentes conectados a jusante das pilhas controladas.",
                GH_ParamAccess.item,
                true);
            pManager[3].Optional = true;

            HideDefaultWires();
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "Status", "Status atual da execucao das pilhas.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Disabled Count", "Count", "Quantidade de componentes atualmente desativados de verdade no canvas.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            HideDefaultWires();

            bool muteInput = true;
            DA.GetData(0, ref muteInput);

            bool downstream = true;
            DA.GetData(3, ref downstream);
            IncludeDownstream = downstream;

            // Verificar se ha conexao na Pilha B (seja por fio ou por StackBGuids)
            var sourcesB = (Params.Input.Count > 2) ? Params.Input[2].Sources : null;
            AlternateABMode = (sourcesB != null && sourcesB.Count > 0) || (StackBGuids.Count > 0);

            if (AlternateABMode)
            {
                ActiveIsA = muteInput; // True = A ativa, False = B ativa
            }
            else
            {
                DisableTargetStack = muteInput;
            }

            // Agendar aplicacao segura fora da solucao corrente (via RhinoApp.Idle)
            if (NeedsLockUpdate())
            {
                RequestLockUpdate();
            }

            // Status para saida
            int disabledCount = 0;
            string statusMsg;

            if (AlternateABMode)
            {
                statusMsg = ActiveIsA
                    ? "[MODO A/B] Pilha A ATIVA | Pilha B DESATIVADA (Locked)"
                    : "[MODO A/B] Pilha B ATIVA | Pilha A DESATIVADA (Locked)";
            }
            else
            {
                statusMsg = DisableTargetStack
                    ? "[STATUS] Pilha Alvo DESATIVADA de verdade (Locked = True / 0% CPU/GPU)"
                    : "[STATUS] Pilha Alvo ATIVA no canvas (Locked = False)";
            }

            var allDisabled = GetDisabledObjects();
            disabledCount = allDisabled.Count;

            DA.SetData(0, statusMsg);
            DA.SetData(1, disabledCount);

            this.Message = AlternateABMode
                ? (ActiveIsA ? "Pilha A Ativa" : "Pilha B Ativa")
                : (DisableTargetStack ? $"{disabledCount} Desativados" : "Tudo Ativo");
        }

        public List<IGH_DocumentObject> GetObjectsInStackA()
        {
            var list = new List<IGH_DocumentObject>();
            var doc = OnPingDocument();

            // 1. Conexoes diretas estilo Galapagos
            if (doc != null)
            {
                for (int i = StackAGuids.Count - 1; i >= 0; i--)
                {
                    var obj = doc.FindObject(StackAGuids[i], true);
                    if (obj == null) { StackAGuids.RemoveAt(i); continue; }
                    if (!list.Contains(obj) && obj != this)
                    {
                        list.Add(obj);
                        if (IncludeDownstream) CollectDownstream(obj, list);
                    }
                }
            }

            // 2. Conexoes tradicionais por fio no input 1
            if (Params.Input.Count > 1)
            {
                foreach (var src in Params.Input[1].Sources)
                {
                    if (src == null) continue;
                    var docObj = src.Attributes?.GetTopLevel?.DocObject;
                    if (docObj != null && !list.Contains(docObj) && docObj != this)
                    {
                        list.Add(docObj);
                        if (IncludeDownstream) CollectDownstream(docObj, list);
                    }
                }
            }
            return list;
        }

        public List<IGH_DocumentObject> GetObjectsInStackB()
        {
            var list = new List<IGH_DocumentObject>();
            var doc = OnPingDocument();

            // 1. Conexoes diretas estilo Galapagos
            if (doc != null)
            {
                for (int i = StackBGuids.Count - 1; i >= 0; i--)
                {
                    var obj = doc.FindObject(StackBGuids[i], true);
                    if (obj == null) { StackBGuids.RemoveAt(i); continue; }
                    if (!list.Contains(obj) && obj != this)
                    {
                        list.Add(obj);
                        if (IncludeDownstream) CollectDownstream(obj, list);
                    }
                }
            }

            // 2. Conexoes tradicionais por fio no input 2
            if (Params.Input.Count > 2)
            {
                foreach (var src in Params.Input[2].Sources)
                {
                    if (src == null) continue;
                    var docObj = src.Attributes?.GetTopLevel?.DocObject;
                    if (docObj != null && !list.Contains(docObj) && docObj != this)
                    {
                        list.Add(docObj);
                        if (IncludeDownstream) CollectDownstream(docObj, list);
                    }
                }
            }
            return list;
        }

        public List<IGH_DocumentObject> GetDisabledObjects()
        {
            var list = new List<IGH_DocumentObject>();
            if (AlternateABMode)
            {
                var targetList = ActiveIsA ? GetObjectsInStackB() : GetObjectsInStackA();
                foreach (var obj in targetList)
                {
                    if (obj is IGH_ActiveObject act && act.Locked) list.Add(obj);
                }
            }
            else
            {
                if (DisableTargetStack)
                {
                    foreach (var obj in GetObjectsInStackA())
                    {
                        if (obj is IGH_ActiveObject act && act.Locked) list.Add(obj);
                    }
                }
            }
            return list;
        }

        private void CollectDownstream(IGH_DocumentObject obj, List<IGH_DocumentObject> visited)
        {
            if (obj == null) return;

            var outputs = new List<IGH_Param>();
            if (obj is IGH_Component comp && comp.Params?.Output != null)
            {
                outputs.AddRange(comp.Params.Output);
            }
            else if (obj is IGH_Param param)
            {
                outputs.Add(param);
            }

            foreach (var outp in outputs)
            {
                if (outp?.Recipients == null) continue;
                foreach (var rec in outp.Recipients)
                {
                    if (rec == null) continue;
                    var nextObj = rec.Attributes?.GetTopLevel?.DocObject;
                    if (nextObj != null && !visited.Contains(nextObj) && nextObj != this)
                    {
                        visited.Add(nextObj);
                        CollectDownstream(nextObj, visited);
                    }
                }
            }
        }

        private bool NeedsLockUpdate()
        {
            if (AlternateABMode)
            {
                bool lockA = !ActiveIsA;
                bool lockB = ActiveIsA;
                foreach (var obj in GetObjectsInStackA())
                {
                    if (obj is IGH_ActiveObject act && act.Locked != lockA) return true;
                }
                foreach (var obj in GetObjectsInStackB())
                {
                    if (obj is IGH_ActiveObject act && act.Locked != lockB) return true;
                }
            }
            else
            {
                foreach (var obj in GetObjectsInStackA())
                {
                    if (obj is IGH_ActiveObject act && act.Locked != DisableTargetStack) return true;
                }
            }
            return false;
        }

        public void RequestLockUpdate()
        {
            if (_idleScheduled) return;
            _idleScheduled = true;
            RhinoApp.Idle += OnRhinoIdle;
        }

        private void OnRhinoIdle(object sender, EventArgs e)
        {
            RhinoApp.Idle -= OnRhinoIdle;
            _idleScheduled = false;

            var doc = OnPingDocument();
            if (doc == null) return;
            if (doc.SolutionState == GH_ProcessStep.Process)
            {
                RequestLockUpdate();
                return;
            }

            bool anyUnlocked = ApplyLocksToCanvasInternal(doc);

            Instances.RedrawCanvas();

            if (anyUnlocked)
            {
                doc.NewSolution(false);
            }
        }

        private bool ApplyLocksToCanvasInternal(GH_Document doc)
        {
            if (doc == null) return false;

            bool anyUnlocked = false;

            if (AlternateABMode)
            {
                var listA = GetObjectsInStackA();
                var listB = GetObjectsInStackB();

                bool lockA = !ActiveIsA;
                bool lockB = ActiveIsA;

                foreach (var obj in listA)
                {
                    if (SetObjectLockedSafe(obj, lockA)) anyUnlocked = true;
                }
                foreach (var obj in listB)
                {
                    if (SetObjectLockedSafe(obj, lockB)) anyUnlocked = true;
                }
            }
            else
            {
                var listA = GetObjectsInStackA();
                foreach (var obj in listA)
                {
                    if (SetObjectLockedSafe(obj, DisableTargetStack)) anyUnlocked = true;
                }
            }

            return anyUnlocked;
        }

        private bool SetObjectLockedSafe(IGH_DocumentObject obj, bool locked)
        {
            if (obj is IGH_ActiveObject act)
            {
                if (act.Locked != locked)
                {
                    act.RecordUndoEvent("Lock / Unlock");
                    act.Locked = locked;
                    return !locked; // true se reativado (precisa recalcular)
                }
            }
            return false;
        }

        public void ToggleManualState()
        {
            if (AlternateABMode)
            {
                ActiveIsA = !ActiveIsA;
            }
            else
            {
                DisableTargetStack = !DisableTargetStack;
            }

            RequestLockUpdate();
            ExpireSolution(true);
        }
    }

    public class PillDisablerWireLinker : Grasshopper.GUI.Canvas.Interaction.GH_AbstractInteraction
    {
        private readonly PillDisablerAttributes _attr;
        private readonly bool _isStackB;
        private PointF _currentPt;

        public PillDisablerWireLinker(GH_Canvas canvas, PillDisablerAttributes attr, GH_CanvasMouseEvent e, PointF startGrip, bool isStackB) : base(canvas, e, false)
        {
            _attr = attr;
            _isStackB = isStackB;
            _currentPt = startGrip;
            _attr.DraggingWirePoint = _currentPt;
            _attr.DraggingIsStackB = isStackB;
        }

        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            _currentPt = e.CanvasLocation;
            _attr.DraggingWirePoint = _currentPt;
            sender.Invalidate();
            return GH_ObjectResponse.Handled;
        }

        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            _attr.DraggingWirePoint = null;

            var doc = sender.Document;
            if (doc != null)
            {
                IGH_DocumentObject hitObj = null;
                foreach (var obj in doc.Objects)
                {
                    if (obj == _attr.DocObject) continue;
                    if (obj.Attributes != null && obj.Attributes.Bounds.Contains(e.CanvasLocation))
                    {
                        hitObj = obj;
                        break;
                    }
                }

                if (hitObj != null)
                {
                    bool isCtrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                    var targetList = _isStackB ? _attr.Disabler.StackBGuids : _attr.Disabler.StackAGuids;

                    if (isCtrl)
                    {
                        targetList.Remove(hitObj.InstanceGuid);
                    }
                    else
                    {
                        if (!targetList.Contains(hitObj.InstanceGuid))
                        {
                            _attr.Disabler.RecordUndoEvent(_isStackB ? "Conectar Pilha B" : "Conectar Pilha A");
                            targetList.Add(hitObj.InstanceGuid);
                        }
                    }
                    _attr.Disabler.ExpireSolution(true);
                }
            }

            sender.ActiveInteraction = null;
            sender.Invalidate();
            return GH_ObjectResponse.Handled;
        }

        public override GH_ObjectResponse RespondToKeyDown(GH_Canvas sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                _attr.DraggingWirePoint = null;
                sender.ActiveInteraction = null;
                sender.Invalidate();
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToKeyDown(sender, e);
        }
    }

    public class PillDisablerAttributes : GH_ComponentAttributes
    {
        public PointF? DraggingWirePoint { get; set; } = null;
        public bool DraggingIsStackB { get; set; } = false;

        private RectangleF _btnBounds;

        public PillDisablerAttributes(PillDisabler_Component owner) : base(owner)
        {
        }

        public PillDisabler_Component Disabler => Owner as PillDisabler_Component;

        protected override void Layout()
        {
            Disabler?.HideDefaultWires();
            base.Layout();
            RectangleF b = Bounds;
            b.Height += 24; // Espaco para o botao interativo
            Bounds = b;
            _btnBounds = new RectangleF(b.X + 4, b.Bottom - 22, b.Width - 8, 18);
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && Disabler?.Params?.Input != null)
            {
                // Clique e arraste no conector de Stack A (Input 1)
                if (Disabler.Params.Input.Count > 1 && Disabler.Params.Input[1]?.Attributes != null)
                {
                    PointF gripA = Disabler.Params.Input[1].Attributes.InputGrip;
                    float d2A = (e.CanvasLocation.X - gripA.X) * (e.CanvasLocation.X - gripA.X) + (e.CanvasLocation.Y - gripA.Y) * (e.CanvasLocation.Y - gripA.Y);
                    if (d2A <= 120f)
                    {
                        sender.ActiveInteraction = new PillDisablerWireLinker(sender, this, e, gripA, false);
                        return GH_ObjectResponse.Handled;
                    }
                }

                // Clique e arraste no conector de Stack B (Input 2)
                if (Disabler.Params.Input.Count > 2 && Disabler.Params.Input[2]?.Attributes != null)
                {
                    PointF gripB = Disabler.Params.Input[2].Attributes.InputGrip;
                    float d2B = (e.CanvasLocation.X - gripB.X) * (e.CanvasLocation.X - gripB.X) + (e.CanvasLocation.Y - gripB.Y) * (e.CanvasLocation.Y - gripB.Y);
                    if (d2B <= 120f)
                    {
                        sender.ActiveInteraction = new PillDisablerWireLinker(sender, this, e, gripB, true);
                        return GH_ObjectResponse.Handled;
                    }
                }

                // Botao interativo
                if (_btnBounds.Contains(e.CanvasLocation))
                {
                    Disabler?.ToggleManualState();
                    sender.Invalidate();
                    return GH_ObjectResponse.Handled;
                }
            }
            return base.RespondToMouseDown(sender, e);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            // 1. Canal de Fios: Desenhar os Cabos Estilo Galapagos com setas
            if (channel == GH_CanvasChannel.Wires)
            {
                RenderGalapagosWires(canvas, graphics);

                // Se a supressao de cabos padrao estiver DESATIVADA pelo usuario, permite o comportamento normal do GH
                if (Disabler != null && !Disabler.SuppressDefaultWires)
                {
                    base.Render(canvas, graphics, channel);
                    return;
                }

                // Quando ativado (padrao = true):
                // NAO chama base.Render no canal Wires, impedindo que o Grasshopper desenhe o feixe de fios verdes ao selecionar o componente.
                // Repassa os fios apenas para outros inputs caso existam e nao estejam ocultos:
                if (Disabler?.Params?.Input != null)
                {
                    for (int i = 0; i < Disabler.Params.Input.Count; i++)
                    {
                        var param = Disabler.Params.Input[i];
                        if (param == null || param.Attributes == null) continue;

                        // Pilha A (i == 1) e Pilha B (i == 2) ou parametros com WireDisplay oculto sao 100% suprimidos
                        if (i == 1 || i == 2 || param.WireDisplay == GH_ParamWireDisplay.hidden)
                        {
                            continue;
                        }

                        param.Attributes.RenderToCanvas(canvas, GH_CanvasChannel.Wires);
                    }
                }
                return;
            }

            // 2. Renderizar Componente Base
            base.Render(canvas, graphics, channel);

            // 3. Canal de Objetos: Molduras de Alerta nos Componentes Controlados e Botao UI
            if (channel == GH_CanvasChannel.Objects)
            {
                RenderGalapagosFrames(canvas, graphics);
                RenderDisablerButton(canvas, graphics);
            }
        }

        private void RenderGalapagosWires(GH_Canvas canvas, Graphics graphics)
        {
            if (Disabler == null || Disabler.Params?.Input == null) return;

            var doc = Disabler.OnPingDocument();
            if (doc == null) return;

            // Fios para Pilha A (Input 1)
            if (Disabler.Params.Input.Count > 1 && Disabler.Params.Input[1]?.Attributes != null)
            {
                var paramA = Disabler.Params.Input[1];
                PointF anchorA = paramA.Attributes.InputGrip;
                Color colorA = (!Disabler.AlternateABMode && Disabler.DisableTargetStack) || (Disabler.AlternateABMode && !Disabler.ActiveIsA)
                    ? Color.FromArgb(230, 231, 76, 60)   // Vermelho / Desativado
                    : Color.FromArgb(230, 46, 204, 113);  // Verde / Ativo

                // Fio de arrasto da Pilha A
                if (DraggingWirePoint.HasValue && !DraggingIsStackB)
                {
                    DrawDraggingWire(graphics, anchorA, DraggingWirePoint.Value, colorA);
                }

                // Alvos diretos em StackAGuids
                foreach (var id in Disabler.StackAGuids)
                {
                    var obj = doc.FindObject(id, true);
                    if (obj == null || obj.Attributes == null) continue;
                    PillPresetVaultAttributes.DrawGalapagosArrowWire(graphics, anchorA, obj.Attributes.Bounds, colorA);
                }

                // Alvos tradicionais conectados no input 1
                if (paramA.Sources != null)
                {
                    foreach (var src in paramA.Sources)
                    {
                        if (src == null || src.Attributes == null) continue;
                        var topObj = src.Attributes.GetTopLevel?.DocObject;
                        RectangleF tb = topObj != null ? topObj.Attributes.Bounds : src.Attributes.Bounds;
                        PillPresetVaultAttributes.DrawGalapagosArrowWire(graphics, anchorA, tb, colorA);
                    }
                }
            }

            // Fios para Pilha B (Input 2)
            if ((Disabler.AlternateABMode || Disabler.StackBGuids.Count > 0) && Disabler.Params.Input.Count > 2 && Disabler.Params.Input[2]?.Attributes != null)
            {
                var paramB = Disabler.Params.Input[2];
                PointF anchorB = paramB.Attributes.InputGrip;
                Color colorB = Disabler.ActiveIsA
                    ? Color.FromArgb(230, 231, 76, 60)   // Vermelho / Desativado
                    : Color.FromArgb(230, 46, 204, 113);  // Verde / Ativo

                // Fio de arrasto da Pilha B
                if (DraggingWirePoint.HasValue && DraggingIsStackB)
                {
                    DrawDraggingWire(graphics, anchorB, DraggingWirePoint.Value, colorB);
                }

                // Alvos diretos em StackBGuids
                foreach (var id in Disabler.StackBGuids)
                {
                    var obj = doc.FindObject(id, true);
                    if (obj == null || obj.Attributes == null) continue;
                    PillPresetVaultAttributes.DrawGalapagosArrowWire(graphics, anchorB, obj.Attributes.Bounds, colorB);
                }

                // Alvos tradicionais conectados no input 2
                if (paramB.Sources != null)
                {
                    foreach (var src in paramB.Sources)
                    {
                        if (src == null || src.Attributes == null) continue;
                        var topObj = src.Attributes.GetTopLevel?.DocObject;
                        RectangleF tb = topObj != null ? topObj.Attributes.Bounds : src.Attributes.Bounds;
                        PillPresetVaultAttributes.DrawGalapagosArrowWire(graphics, anchorB, tb, colorB);
                    }
                }
            }
        }

        private void DrawDraggingWire(Graphics graphics, PointF anchor, PointF cur, Color color)
        {
            float dx = Math.Abs(cur.X - anchor.X);
            float dy = Math.Abs(cur.Y - anchor.Y);
            PointF c1 = new PointF(anchor.X - Math.Max(25f, dx * 0.4f), anchor.Y);
            PointF c2 = new PointF(cur.X, cur.Y - Math.Max(25f, dy * 0.4f));

            using (var arrowCap = new AdjustableArrowCap(4.2f, 4.8f, true))
            using (var pen = new Pen(color, 2.4f) { CustomEndCap = arrowCap, DashStyle = DashStyle.Dash })
            {
                graphics.DrawBezier(pen, anchor, c1, c2, cur);
            }
        }

        private void RenderGalapagosFrames(GH_Canvas canvas, Graphics graphics)
        {
            if (Disabler == null) return;

            var stackA = Disabler.GetObjectsInStackA();
            var stackB = Disabler.GetObjectsInStackB();

            RenderFrames(graphics, stackA, (!Disabler.AlternateABMode && Disabler.DisableTargetStack) || (Disabler.AlternateABMode && !Disabler.ActiveIsA));
            if (Disabler.AlternateABMode || Disabler.StackBGuids.Count > 0)
            {
                RenderFrames(graphics, stackB, Disabler.ActiveIsA);
            }
        }

        private void RenderFrames(Graphics graphics, List<IGH_DocumentObject> objects, bool isLocked)
        {
            Color frameColor = isLocked ? Color.FromArgb(220, 231, 76, 60) : Color.FromArgb(200, 46, 204, 113);
            using (var pen = new Pen(frameColor, 2.0f) { DashStyle = isLocked ? DashStyle.DashDot : DashStyle.Solid })
            using (var brushBg = new SolidBrush(Color.FromArgb(isLocked ? 35 : 20, frameColor)))
            {
                foreach (var obj in objects)
                {
                    if (obj == null || obj.Attributes == null) continue;
                    RectangleF tb = obj.Attributes.Bounds;
                    tb.Inflate(5, 5);

                    graphics.FillRectangle(brushBg, tb.X, tb.Y, tb.Width, tb.Height);
                    graphics.DrawRectangle(pen, tb.X, tb.Y, tb.Width, tb.Height);

                    // Se estiver travado, desenha o badge [LOCKED]
                    if (isLocked)
                    {
                        string tag = "[LOCKED]";
                        using (var font = new Font("Arial", 6.5f, FontStyle.Bold))
                        using (var tagBrush = new SolidBrush(Color.FromArgb(231, 76, 60)))
                        {
                            graphics.DrawString(tag, font, tagBrush, tb.X + 2, tb.Y - 10);
                        }
                    }
                }
            }
        }

        private void RenderDisablerButton(GH_Canvas canvas, Graphics graphics)
        {
            if (Disabler == null) return;

            bool isMuted = Disabler.AlternateABMode ? !Disabler.ActiveIsA : Disabler.DisableTargetStack;
            Color btnColor = Disabler.AlternateABMode
                ? (Disabler.ActiveIsA ? Color.FromArgb(41, 128, 185) : Color.FromArgb(142, 68, 173))
                : (isMuted ? Color.FromArgb(192, 57, 43) : Color.FromArgb(39, 174, 96));

            using (var fillBrush = new SolidBrush(btnColor))
            {
                graphics.FillRectangle(fillBrush, _btnBounds);
            }
            graphics.DrawRectangle(Pens.White, _btnBounds.X, _btnBounds.Y, _btnBounds.Width, _btnBounds.Height);

            string btnText;
            if (Disabler.AlternateABMode)
            {
                btnText = Disabler.ActiveIsA ? "Pilha A (Ativa) -> B" : "Pilha B (Ativa) -> A";
            }
            else
            {
                btnText = isMuted ? "DESATIVADO (Locked)" : "ATIVADO (Running)";
            }

            using (var font = new Font("Arial", 7.0f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.White))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                graphics.DrawString(btnText, font, textBrush, _btnBounds, sf);
            }
        }
    }
}