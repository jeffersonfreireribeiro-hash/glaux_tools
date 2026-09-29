using System;
using System.Collections.Generic;
using Buraqueira_Tools.Data;
using Buraqueira_Tools.ProjectState;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Captura e restaura o estado dos controles do canvas (sliders, toggles, value lists, panels de entrada,
    /// sliders do Pill Slider Pool e controles de componentes <see cref="IPillControlStateProvider"/>, como o
    /// Pill Dashboard). A decisão de compatibilidade fica em <see cref="ControlCompatibility"/> (testável);
    /// aqui só há a leitura e a escrita nos objetos do Grasshopper.
    /// </summary>
    internal static class ControlStateService
    {
        public static List<ControlState> Capture(GH_Document doc, bool includePanels = true)
        {
            var list = new List<ControlState>();
            if (doc == null) return list;
            foreach (var obj in doc.Objects)
            {
                switch (obj)
                {
                    case GH_NumberSlider s:
                        list.Add(new ControlState
                        {
                            Kind = ControlKinds.Slider,
                            Id = s.InstanceGuid.ToString("D"),
                            Name = Label(s),
                            Number = (double)s.CurrentValue,
                            Min = (double)s.Slider.Minimum,
                            Max = (double)s.Slider.Maximum
                        });
                        break;
                    case GH_BooleanToggle t:
                        list.Add(new ControlState { Kind = ControlKinds.Toggle, Id = t.InstanceGuid.ToString("D"), Name = Label(t), Boolean = t.Value });
                        break;
                    case GH_ValueList v:
                        list.Add(new ControlState
                        {
                            Kind = ControlKinds.ValueList,
                            Id = v.InstanceGuid.ToString("D"),
                            Name = Label(v),
                            Text = v.SelectedItems.Count > 0 ? v.SelectedItems[0].Name : ""
                        });
                        break;
                    case GH_Panel p when includePanels && p.SourceCount == 0:
                        list.Add(new ControlState { Kind = ControlKinds.Panel, Id = p.InstanceGuid.ToString("D"), Name = Label(p), Text = p.UserText });
                        break;
                    case PillSliderPool_Component pool:
                        foreach (var s in pool.Sliders)
                        {
                            var state = new ControlState
                            {
                                Kind = ControlKinds.PoolSlider,
                                Id = pool.InstanceGuid.ToString("D") + "|" + s.CleanKey,
                                Name = s.FullKey,
                                Min = s.Min,
                                Max = s.Max
                            };
                            if (s.DataType == PillDataType.String || s.DataType == PillDataType.ValueList)
                            {
                                state.Text = s.StringValue;
                                state.Min = null;
                                state.Max = null;
                            }
                            else
                            {
                                state.Number = s.Value;
                                if (s.DataType == PillDataType.Domain) state.Extra = s.DomainEnd;
                            }
                            list.Add(state);
                        }
                        break;
                    case IPillControlStateProvider provider:
                        list.AddRange(provider.CaptureControlStates());
                        break;
                }
            }
            return list;
        }

        /// <summary>Compara o estado salvo com o documento atual (nada é alterado).</summary>
        public static List<ControlMatch> Plan(GH_Document doc, IList<ControlState> saved)
        {
            var current = Capture(doc);
            var options = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in doc.Objects)
            {
                if (obj is GH_ValueList v)
                {
                    var names = new List<string>();
                    foreach (var item in v.ListItems) names.Add(item.Name);
                    options[v.InstanceGuid.ToString("D")] = names;
                }
                else if (obj is IPillControlStateProvider provider)
                {
                    foreach (var kv in provider.ControlOptions()) options[kv.Key] = kv.Value;
                }
            }
            return ControlCompatibility.Match(saved, current, c => options.TryGetValue(c.Id ?? "", out var n) ? n : null);
        }

        /// <summary>
        /// Aplica os controles compatíveis numa única solução (fora da solução em andamento, via RhinoApp.Idle).
        /// </summary>
        public static void Apply(GH_Document doc, IList<ControlMatch> plan)
        {
            if (doc == null) return;
            var compatible = new List<ControlMatch>();
            foreach (var m in plan)
            {
                if (m.Compatible) compatible.Add(m);
            }
            if (compatible.Count == 0) return;

            Action apply = () =>
            {
                var touchedPools = new HashSet<PillSliderPool_Component>();
                var touchedProviders = new HashSet<IGH_ActiveObject>();
                foreach (var m in compatible)
                {
                    try
                    {
                        ApplyOne(doc, m, touchedPools, touchedProviders);
                    }
                    catch
                    {
                        // Um controle que falhar não impede os demais
                    }
                }
                foreach (var pool in touchedPools) pool.ExpireSolution(false);
                foreach (var provider in touchedProviders) provider.ExpireSolution(false);
                Grasshopper.Instances.RedrawCanvas();
                doc.NewSolution(false);
            };

            if (doc.SolutionState == GH_ProcessStep.Process)
            {
                EventHandler handler = null;
                handler = (s, e) =>
                {
                    Rhino.RhinoApp.Idle -= handler;
                    apply();
                };
                Rhino.RhinoApp.Idle += handler;
            }
            else
            {
                apply();
            }
        }

        private static void ApplyOne(GH_Document doc, ControlMatch m, HashSet<PillSliderPool_Component> touchedPools, HashSet<IGH_ActiveObject> touchedProviders)
        {
            var saved = m.Saved;
            var current = m.Current;
            if (current.Kind == ControlKinds.Dashboard)
            {
                int sep = current.Id.IndexOf('|');
                if (sep < 0 || !Guid.TryParse(current.Id.Substring(0, sep), out Guid ownerId)) return;
                // Aplica só o valor: configuração e posição do painel não mudam
                if (doc.FindObject(ownerId, true) is IPillControlStateProvider provider && provider.ApplyControlState(current.Id.Substring(sep + 1), saved) && provider is IGH_ActiveObject active)
                {
                    touchedProviders.Add(active);
                }
                return;
            }
            if (current.Kind == ControlKinds.PoolSlider)
            {
                int bar = current.Id.IndexOf('|');
                if (bar < 0 || !Guid.TryParse(current.Id.Substring(0, bar), out Guid poolId)) return;
                string key = current.Id.Substring(bar + 1);
                if (!(doc.FindObject(poolId, true) is PillSliderPool_Component pool)) return;
                foreach (var s in pool.Sliders)
                {
                    if (!string.Equals(s.CleanKey, key, StringComparison.OrdinalIgnoreCase)) continue;
                    if (saved.Text != null && (s.DataType == PillDataType.String || s.DataType == PillDataType.ValueList))
                    {
                        s.StringValue = saved.Text;
                        int idx = s.StringOptions.FindIndex(o => string.Equals(o, saved.Text, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0) s.Value = idx;
                    }
                    else if (saved.Number.HasValue)
                    {
                        s.Value = saved.Number.Value;
                        if (saved.Extra.HasValue) s.DomainEnd = saved.Extra.Value;
                    }
                    pool.PublishSingleSlider(s);
                    touchedPools.Add(pool);
                    break;
                }
                return;
            }

            if (!Guid.TryParse(current.Id, out Guid id)) return;
            var obj = doc.FindObject(id, true);
            switch (obj)
            {
                case GH_NumberSlider slider when saved.Number.HasValue:
                    // Sem eventos: evita uma solução por slider; o documento recalcula uma vez no fim
                    bool raise = slider.Slider.RaiseEvents;
                    slider.Slider.RaiseEvents = false;
                    try
                    {
                        slider.TrySetSliderValue((decimal)saved.Number.Value);
                    }
                    finally
                    {
                        slider.Slider.RaiseEvents = raise;
                    }
                    slider.ExpireSolution(false);
                    break;
                case GH_BooleanToggle toggle when saved.Boolean.HasValue:
                    toggle.Value = saved.Boolean.Value;
                    toggle.ExpireSolution(false);
                    break;
                case GH_ValueList list when saved.Text != null:
                    for (int i = 0; i < list.ListItems.Count; i++)
                    {
                        if (string.Equals(list.ListItems[i].Name, saved.Text, StringComparison.OrdinalIgnoreCase))
                        {
                            list.SelectItem(i);
                            break;
                        }
                    }
                    break;
                case GH_Panel panel when saved.Text != null:
                    panel.SetUserText(saved.Text);
                    break;
            }
        }

        private static string Label(IGH_DocumentObject obj)
        {
            return string.IsNullOrWhiteSpace(obj.NickName) ? obj.Name : obj.NickName;
        }
    }

    /// <summary>Captura dos canais do PillHub para snapshots.</summary>
    internal static class HubCapture
    {
        public static Dictionary<string, GlauxTreeTable> Capture(string groupQuery, ICollection<string> warnings)
        {
            var result = new Dictionary<string, GlauxTreeTable>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(groupQuery)) return result;
            foreach (var ch in PillHub.GetChannelsByGroupOrKey(groupQuery))
            {
                if (ch?.Data == null || result.ContainsKey(ch.CleanKey)) continue;
                result[ch.CleanKey] = TreeMapper.ToTable(ch.Data, warnings);
            }
            return result;
        }
    }
}
