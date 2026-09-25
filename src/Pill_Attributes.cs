using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Atributos visuais para componentes de transmissão e recepção sem fios (Pill).
    /// Renderiza uma cápsula estilizada com a cor da categoria (ACU, GEO, MAT, etc.),
    /// indicador luminoso de status (Online, Órfão, Mismatch) e identificação do canal.
    /// Inclui Dock de Teletransporte (Jump Colorido) interativo com bolinhas de cores coordenadas
    /// permitindo navegar instantaneamente entre Pill Transmitter e múltiplos Pill Receivers.
    /// </summary>
    public class Pill_Attributes : GH_ComponentAttributes
    {
        private const float PILL_HEIGHT_STANDARD = 26f;
        private const float PILL_HEIGHT_JUMP = 38f;
        private const float MIN_WIDTH = 132f;

        private readonly List<RectangleF> _jumpDotRects = new List<RectangleF>();
        private readonly List<Guid> _jumpTargetGuids = new List<Guid>();
        private int _hoveredDotIndex = -1;

        public Pill_Attributes(GH_Component owner) : base(owner)
        {
        }

        private bool IsTransmitterType => Owner is PillTransmitter_Component || Owner is PillCache_Component || Owner is PillLayerPipeline_Component || (Owner is PillGeometryFilter_Component gf && !string.IsNullOrEmpty(gf.CurrentCleanKey)) || (Owner is PillDomainFilter_Component df && !string.IsNullOrEmpty(df.CurrentCleanKey));
        private bool IsReceiverType => Owner is PillReceiver_Component;
        private bool HasJumpDock => IsTransmitterType || IsReceiverType;
        private float PillExtraHeight => HasJumpDock ? PILL_HEIGHT_JUMP : PILL_HEIGHT_STANDARD;

        private string GetOwnerCleanKey()
        {
            if (Owner is PillTransmitter_Component tx) return tx.CurrentCleanKey;
            if (Owner is PillReceiver_Component rx) return rx.CurrentCleanKey;
            if (Owner is PillCache_Component cache) return cache.CurrentCleanKey;
            if (Owner is PillLayerPipeline_Component pipe) return pipe.CurrentCleanKey;
            if (Owner is PillGeometryFilter_Component gf) return gf.CurrentCleanKey;
            if (Owner is PillDomainFilter_Component df) return df.CurrentCleanKey;
            if (Owner is PillDiskSave_Component dsave) return dsave.CurrentCleanKey;
            if (Owner is PillDiskLoad_Component dload) return dload.CurrentCleanKey;
            return "";
        }

        protected override void Layout()
        {
            base.Layout();

            RectangleF b = Bounds;
            float extraWidth = 0f;

            string cleanKey = GetOwnerCleanKey();
            if (IsTransmitterType && !string.IsNullOrEmpty(cleanKey) && !cleanKey.Equals("Sem Chave", StringComparison.OrdinalIgnoreCase))
            {
                int rxCount = PillHub.GetReceiverCount(cleanKey);
                if (rxCount > 6)
                {
                    extraWidth = (rxCount - 6) * 13f;
                }
            }

            float oldRight = Bounds.Right;
            b.Width = Math.Max(b.Width, MIN_WIDTH + extraWidth);
            b.Height += PillExtraHeight;
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

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            // Renderiza o componente padrão (grips, nomes de parâmetros, conexões)
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
                var comp = Owner as GH_Component;
                if (comp == null) return;

                RectangleF b = Bounds;
                float extraH = PillExtraHeight;
                RectangleF pillHeaderRect = new RectangleF(b.X + 4, b.Bottom - extraH + 2, b.Width - 8, extraH - 4);

                string key = "";
                string category = "GEN";
                string unit = "";
                Color catColor = Color.FromArgb(108, 117, 125);
                bool isConnected = false;
                bool isWarning = false;
                string subText = "";

                if (comp is PillTransmitter_Component tx)
                {
                    key = tx.CurrentCleanKey;
                    category = tx.CurrentCategory;
                    unit = tx.CurrentUnit;
                    catColor = tx.CurrentCategoryColor;
                    isConnected = tx.HasData;
                    subText = tx.ItemCountSummary;
                }
                else if (comp is PillReceiver_Component rx)
                {
                    key = rx.CurrentCleanKey;
                    category = rx.CurrentCategory;
                    unit = rx.CurrentUnit;
                    catColor = rx.CurrentCategoryColor;
                    isConnected = rx.IsConnected;
                    isWarning = rx.HasWarning;
                    subText = rx.StatusShort;
                }
                else if (comp is PillCache_Component cache)
                {
                    key = cache.CurrentCleanKey;
                    category = cache.CurrentCategory;
                    unit = cache.CurrentUnit;
                    catColor = cache.CurrentCategoryColor;
                    isConnected = cache.IsCached;
                    isWarning = cache.IsBypassed;
                    subText = cache.CacheStatusShort;
                }
                else if (comp is PillLayerPipeline_Component pipe)
                {
                    key = string.IsNullOrEmpty(pipe.CurrentCleanKey) 
                        ? (pipe.SavedLayerCount > 0 ? $"{pipe.SavedLayerCount} Camada(s)" : "Selecione Camadas")
                        : pipe.CurrentCleanKey;
                    category = pipe.CurrentCategory;
                    unit = pipe.CurrentUnit;
                    catColor = pipe.CurrentCategoryColor;
                    isConnected = pipe.CapturedObjectCount > 0;
                    isWarning = pipe.SavedLayerCount == 0;
                    subText = $"{pipe.CapturedObjectCount} objs";
                }
                else if (comp is PillDiskSave_Component dsave)
                {
                    key = dsave.CurrentCleanKey;
                    category = dsave.CurrentCategory;
                    unit = dsave.FileExtensionDisplay;
                    catColor = dsave.CurrentCategoryColor;
                    isConnected = dsave.LastSavedSuccess;
                    isWarning = !dsave.LastSavedSuccess;
                    subText = dsave.StatusSummary;
                }
                else if (comp is PillDiskLoad_Component dload)
                {
                    key = dload.CurrentCleanKey;
                    category = dload.CurrentCategory;
                    unit = dload.FileExtensionDisplay;
                    catColor = dload.CurrentCategoryColor;
                    isConnected = dload.LastLoadSuccess;
                    isWarning = !dload.LastLoadSuccess;
                    subText = dload.StatusSummary;
                }
                else if (comp is PillGeometryFilter_Component filter)
                {
                    key = string.IsNullOrEmpty(filter.CurrentCleanKey) ? filter.FilterSummaryShort : filter.CurrentCleanKey;
                    category = filter.CurrentCategory;
                    unit = filter.CurrentUnit;
                    catColor = filter.CurrentCategoryColor;
                    isConnected = filter.CleanCount > 0;
                    isWarning = filter.DiscardedCount > 0;
                    subText = $"{filter.CleanCount} apr / {filter.DiscardedCount} desc";
                }
                else if (comp is PillDomainFilter_Component dfilter)
                {
                    key = string.IsNullOrEmpty(dfilter.CurrentCleanKey) ? dfilter.FilterSummaryShort : dfilter.CurrentCleanKey;
                    category = dfilter.CurrentCategory;
                    unit = dfilter.CurrentUnit;
                    catColor = dfilter.CurrentCategoryColor;
                    isConnected = dfilter.InsideCount > 0;
                    isWarning = dfilter.OutsideCount > 0 && dfilter.InsideCount == 0;
                    subText = $"{dfilter.InsideCount} in / {dfilter.OutsideCount} out";
                }
                else if (comp is PillInterpolator_Component interp)
                {
                    key = string.IsNullOrEmpty(interp.CurrentCleanKey) ? "Interpolação" : interp.CurrentCleanKey;
                    category = interp.CurrentCategory;
                    unit = interp.CurrentUnit;
                    catColor = interp.CurrentCategoryColor;
                    isConnected = interp.TotalItemsProduced > 0;
                    isWarning = interp.TotalGapsFilled > 0 && interp.TotalItemsProduced == 0;
                    subText = interp.TotalGapsFilled > 0 ? $"{interp.TotalGapsFilled} interp. ({interp.TotalItemsProduced} pts)" : $"{interp.TotalItemsProduced} pts";
                }

                if (string.IsNullOrEmpty(key) || key.Equals("Sem Chave", StringComparison.OrdinalIgnoreCase))
                {
                    if (comp is PillCache_Component)
                        key = "Cache (Aguardando Chave)";
                    else if (comp is PillReceiver_Component)
                        key = "Receiver (Selecione Canal)";
                    else if (comp is PillLayerPipeline_Component)
                        key = "Pipeline (Selecione Camadas)";
                    else if (comp is PillDiskSave_Component)
                        key = "Save (Informe Chave)";
                    else if (comp is PillDiskLoad_Component)
                        key = "Load (Informe Chave)";
                    else if (comp is PillGeometryFilter_Component)
                        key = "Filtro Geometria";
                    else if (comp is PillInterpolator_Component)
                        key = "Interpolador";
                    else
                        key = "Transmitter (Sem Chave)";
                }

                // Desenho da cápsula estilizada inferior
                using (var path = CreateRoundedRectangle(pillHeaderRect, 6f))
                {
                    // Fundo da cápsula com gradiente escuro moderno
                    using (var bgBrush = new LinearGradientBrush(pillHeaderRect, Color.FromArgb(28, 32, 38), Color.FromArgb(20, 24, 28), LinearGradientMode.Vertical))
                    {
                        graphics.FillPath(bgBrush, path);
                    }

                    // Borda na cor da categoria
                    using (var borderPen = new Pen(Color.FromArgb(160, catColor), 1.2f))
                    {
                        graphics.DrawPath(borderPen, path);
                    }
                }

                // Badge da Categoria com largura dinâmica [ACU], [GEO], [CACHE], [U] (sem quebrar linhas)
                float badgeW = 32f;
                using (var badgeFont = new Font("Segoe UI", 7.2f, FontStyle.Bold))
                {
                    SizeF measured = graphics.MeasureString(category, badgeFont);
                    badgeW = Math.Max(26f, (float)Math.Ceiling(measured.Width) + 8f);
                    if (badgeW > pillHeaderRect.Width * 0.45f) badgeW = pillHeaderRect.Width * 0.45f;

                    RectangleF badgeRect = new RectangleF(pillHeaderRect.X + 4, pillHeaderRect.Y + 2.5f, badgeW, 14f);
                    using (var badgePath = CreateRoundedRectangle(badgeRect, 3f))
                    using (var badgeBrush = new SolidBrush(catColor))
                    {
                        graphics.FillPath(badgeBrush, badgePath);
                    }

                    using (var badgeTextBrush = new SolidBrush(Color.White))
                    {
                        var sfBadge = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center,
                            FormatFlags = StringFormatFlags.NoWrap,
                            Trimming = StringTrimming.None
                        };
                        graphics.DrawString(category, badgeFont, badgeTextBrush, badgeRect, sfBadge);
                    }
                }

                // LED Luminoso de Status (Círculo Verde / Âmbar / Vermelho)
                float ledSize = 6.5f;
                float ledX = pillHeaderRect.Right - ledSize - 6;
                float ledY = pillHeaderRect.Y + 6.5f;
                RectangleF ledRect = new RectangleF(ledX, ledY, ledSize, ledSize);

                Color ledColor;
                if (comp is PillCache_Component cacheObj)
                {
                    if (cacheObj.IsBypassed)
                        ledColor = Color.FromArgb(243, 156, 18); // Laranja: Bypass
                    else if (cacheObj.IsCached)
                        ledColor = Color.FromArgb(46, 204, 113); // Verde: CACHE HIT
                    else
                        ledColor = Color.FromArgb(0, 180, 216);  // Ciano: Recalculado
                }
                else
                {
                    ledColor = isConnected ? (isWarning ? Color.FromArgb(243, 156, 18) : Color.FromArgb(46, 204, 113)) : Color.FromArgb(231, 76, 60);
                }
                using (var ledBrush = new SolidBrush(ledColor))
                using (var ledPen = new Pen(Color.FromArgb(200, 255, 255, 255), 0.8f))
                {
                    graphics.FillEllipse(ledBrush, ledRect);
                    graphics.DrawEllipse(ledPen, ledRect);
                }

                // Texto do Canal e Unidade
                float textLeft = pillHeaderRect.X + 4 + badgeW + 5;
                float textWidth = ledX - textLeft - 4;
                if (textWidth > 20)
                {
                    RectangleF labelRect = new RectangleF(textLeft, pillHeaderRect.Y + 1.5f, textWidth, 16f);
                    string displayLabel = key;
                    if (!string.IsNullOrEmpty(unit))
                    {
                        displayLabel += $" [{unit}]";
                    }

                    using (var labelFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                    using (var labelBrush = new SolidBrush(Color.FromArgb(235, 240, 245)))
                    {
                        var sfLabel = new StringFormat
                        {
                            Alignment = StringAlignment.Near,
                            LineAlignment = StringAlignment.Center,
                            Trimming = StringTrimming.EllipsisCharacter,
                            FormatFlags = StringFormatFlags.NoWrap
                        };
                        graphics.DrawString(displayLabel, labelFont, labelBrush, labelRect, sfLabel);
                    }
                }

                // =========================================================================
                // SE FOR TRANSMITTER OU RECEIVER: RENDERIZA O DOCK DE JUMP COLORIDO
                // =========================================================================
                if (HasJumpDock)
                {
                    _jumpDotRects.Clear();
                    _jumpTargetGuids.Clear();

                    float dividerY = pillHeaderRect.Y + 19.5f;
                    using (var dividerPen = new Pen(Color.FromArgb(48, 56, 68), 0.8f))
                    {
                        graphics.DrawLine(dividerPen, pillHeaderRect.X + 5, dividerY, pillHeaderRect.Right - 5, dividerY);
                    }

                    var doc = comp.OnPingDocument();

                    if (IsTransmitterType)
                    {
                        string txKey = GetOwnerCleanKey();
                        var activeReceivers = PillHub.GetActiveReceivers(txKey, doc);
                        float dotY = pillHeaderRect.Y + 27f;
                        float startDotX = pillHeaderRect.X + 8f;

                        if (activeReceivers == null || activeReceivers.Count == 0)
                        {
                            // Indicador discreto de 0 receptores conectados
                            using (var dimPen = new Pen(Color.FromArgb(85, 96, 110), 1.0f) { DashStyle = DashStyle.Dot })
                            using (var dimFont = new Font("Segoe UI", 6.2f, FontStyle.Regular))
                            using (var dimBrush = new SolidBrush(Color.FromArgb(110, 122, 138)))
                            {
                                graphics.DrawEllipse(dimPen, startDotX - 3.5f, dotY - 3.5f, 7f, 7f);
                                graphics.DrawString("0 rx", dimFont, dimBrush, startDotX + 7f, dotY - 5.5f);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < activeReceivers.Count; i++)
                            {
                                float dotX = startDotX + i * 13f;
                                var rxObj = activeReceivers[i];
                                Color dotColor = PillHub.GetReceiverColor(rxObj.AssignedColorIndex);

                                RectangleF dotRect = new RectangleF(dotX - 4f, dotY - 4f, 8f, 8f);
                                _jumpDotRects.Add(dotRect);
                                _jumpTargetGuids.Add(rxObj.InstanceGuid);

                                bool isHovered = (_hoveredDotIndex == i);
                                float drawRadius = isHovered ? 4.8f : 3.5f;
                                RectangleF drawRect = new RectangleF(dotX - drawRadius, dotY - drawRadius, drawRadius * 2f, drawRadius * 2f);

                                using (var dotBrush = new SolidBrush(dotColor))
                                using (var borderPen = new Pen(isHovered ? Color.White : Color.FromArgb(200, 240, 240, 240), isHovered ? 1.4f : 0.8f))
                                {
                                    graphics.FillEllipse(dotBrush, drawRect);
                                    graphics.DrawEllipse(borderPen, drawRect);
                                }

                                if (isHovered)
                                {
                                    using (var tipFont = new Font("Segoe UI", 6.5f, FontStyle.Bold))
                                    using (var tipBrush = new SolidBrush(Color.White))
                                    {
                                        float tipX = startDotX + activeReceivers.Count * 13f + 4f;
                                        if (tipX < pillHeaderRect.Right - 35f)
                                        {
                                            graphics.DrawString($"➔ Rx #{rxObj.AssignedColorIndex + 1}", tipFont, tipBrush, tipX, dotY - 5.5f);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (comp is PillReceiver_Component rxComp)
                    {
                        Guid txGuid = PillHub.GetTransmitterGuid(rxComp.CurrentCleanKey, doc);
                        int myIndex = rxComp.AssignedColorIndex >= 0 ? rxComp.AssignedColorIndex : rxComp.EnsureColorIndex(doc);
                        Color myColor = PillHub.GetReceiverColor(myIndex);
                        float dotY = pillHeaderRect.Y + 27f;
                        float dotX = pillHeaderRect.X + 8f;

                        // Rótulo inteligente dependendo do tipo da fonte de transmissão
                        string jumpLabel = "⇡ Jump Tx";
                        if (doc != null && txGuid != Guid.Empty)
                        {
                            var srcObj = doc.FindObject(txGuid, false);
                            if (srcObj is PillCache_Component) jumpLabel = "⇡ Jump Cache";
                            else if (srcObj is PillLayerPipeline_Component) jumpLabel = "⇡ Jump Layers";
                            else if (srcObj is PillGeometryFilter_Component) jumpLabel = "⇡ Jump Filter";
                        }

                        // A área clicável inclui a bolinha e o rótulo de Jump
                        RectangleF clickArea = new RectangleF(pillHeaderRect.X + 3f, pillHeaderRect.Y + 20f, 78f, 14f);
                        _jumpDotRects.Add(clickArea);
                        _jumpTargetGuids.Add(txGuid);

                        bool isHovered = (_hoveredDotIndex == 0);
                        float drawRadius = isHovered ? 4.8f : 3.5f;
                        RectangleF drawRect = new RectangleF(dotX - drawRadius, dotY - drawRadius, drawRadius * 2f, drawRadius * 2f);

                        using (var dotBrush = new SolidBrush(myColor))
                        using (var borderPen = new Pen(isHovered ? Color.White : Color.FromArgb(200, 240, 240, 240), isHovered ? 1.4f : 0.8f))
                        {
                            graphics.FillEllipse(dotBrush, drawRect);
                            graphics.DrawEllipse(borderPen, drawRect);
                        }

                        // Rótulo de salto para o transmissor / fonte
                        using (var jumpFont = new Font("Segoe UI", 6.5f, isHovered ? FontStyle.Bold : FontStyle.Regular))
                        using (var jumpBrush = new SolidBrush(isHovered ? Color.White : Color.FromArgb(148, 163, 184)))
                        {
                            graphics.DrawString(jumpLabel, jumpFont, jumpBrush, dotX + 7f, dotY - 5.5f);
                        }
                    }
                }
            }
        }

        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (HasJumpDock && _jumpDotRects.Count > 0)
            {
                int hovered = -1;
                for (int i = 0; i < _jumpDotRects.Count; i++)
                {
                    var hit = _jumpDotRects[i];
                    hit.Inflate(2f, 2f);
                    if (hit.Contains(e.CanvasLocation))
                    {
                        hovered = i;
                        break;
                    }
                }

                if (hovered != _hoveredDotIndex)
                {
                    _hoveredDotIndex = hovered;
                    sender.Cursor = (hovered >= 0) ? Cursors.Hand : Cursors.Default;
                    sender.Refresh();
                    return GH_ObjectResponse.Handled;
                }

                if (hovered >= 0)
                {
                    sender.Cursor = Cursors.Hand;
                    return GH_ObjectResponse.Handled;
                }
            }
            else
            {
                if (_hoveredDotIndex >= 0)
                {
                    _hoveredDotIndex = -1;
                    sender.Cursor = Cursors.Default;
                    sender.Refresh();
                }
            }

            return base.RespondToMouseMove(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && HasJumpDock)
            {
                for (int i = 0; i < _jumpDotRects.Count; i++)
                {
                    var hit = _jumpDotRects[i];
                    hit.Inflate(3f, 3f);
                    if (hit.Contains(e.CanvasLocation))
                    {
                        if (i < _jumpTargetGuids.Count)
                        {
                            Guid targetGuid = _jumpTargetGuids[i];
                            if (targetGuid != Guid.Empty)
                            {
                                var doc = Owner.OnPingDocument();
                                if (doc != null)
                                {
                                    var targetObj = doc.FindObject(targetGuid, false);
                                    if (targetObj != null)
                                    {
                                        PillHub.JumpToComponent(targetObj);
                                        return GH_ObjectResponse.Handled;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return base.RespondToMouseDown(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (Owner is PillLayerPipeline_Component pipe)
            {
                pipe.OpenLayerPickerModal();
                return GH_ObjectResponse.Handled;
            }
            if (Owner is PillDiskSave_Component dsave)
            {
                dsave.OpenTargetFolder();
                return GH_ObjectResponse.Handled;
            }
            if (Owner is PillDiskLoad_Component dload)
            {
                dload.PromptSelectFile();
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseDoubleClick(sender, e);
        }

        private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
