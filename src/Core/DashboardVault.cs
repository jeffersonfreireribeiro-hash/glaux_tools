using System;
using System.Collections.Generic;
using System.Linq;
using Buraqueira_Tools.Dashboard;
using Buraqueira_Tools.ProjectState;
using GH_IO.Serialization;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Persistência (.gh/undo) e contratos com o Pill Preset Vault e o Project Vault para o Pill Dashboard.
    /// Separado do componente para não depender de WinForms (testável fora do Rhino).
    /// </summary>
    internal static class DashboardVault
    {
        public const string StateCountKey = "DashStateCount";
        public const string StateKey = "DashState";
        public const string LoadHashKey = "DashLoadHash";

        /// <summary>Grava só o estado de execução (inclusive órfãos); a configuração vem das entradas do componente.</summary>
        public static void Write(GH_IWriter writer, DashboardState state, string loadHash)
        {
            var lines = state.ToRawLines();
            writer.SetInt32(StateCountKey, lines.Count);
            for (int i = 0; i < lines.Count; i++) writer.SetString(StateKey, i, lines[i]);
            writer.SetString(LoadHashKey, loadHash ?? "");
        }

        /// <summary>Lê o estado gravado; devolve o hash do último Load State aplicado (null se não houver).</summary>
        public static string Read(GH_IReader reader, DashboardState state)
        {
            if (reader.ItemExists(StateCountKey))
            {
                int n = reader.GetInt32(StateCountKey);
                var lines = new List<string>(n);
                for (int i = 0; i < n; i++)
                {
                    if (reader.ItemExists(StateKey, i)) lines.Add(reader.GetString(StateKey, i));
                }
                state.LoadRaw(lines);
            }
            return reader.ItemExists(LoadHashKey) ? reader.GetString(LoadHashKey) : null;
        }

        public static string ControlId(Guid owner, string widgetId) => owner.ToString("D") + "|" + widgetId;

        public static IEnumerable<ControlState> Capture(Guid owner, string nickName, DashboardController controller)
        {
            foreach (var w in controller.Widgets)
            {
                if (!w.Spec.IsPersistent) continue;
                var value = controller.State.Effective(w.Spec);
                var state = new ControlState
                {
                    Kind = ControlKinds.Dashboard,
                    Id = ControlId(owner, w.Id),
                    Name = nickName + ": " + (w.Spec.Label.Length > 0 ? w.Spec.Label : w.Id)
                };
                switch (value.Kind)
                {
                    case WidgetValueKind.Number:
                        state.Number = value.Number;
                        state.Min = w.Spec.Min;
                        state.Max = w.Spec.Max;
                        break;
                    case WidgetValueKind.Boolean:
                        state.Boolean = value.Boolean;
                        break;
                    default:
                        state.Text = value.ToInvariantString();
                        break;
                }
                yield return state;
            }
        }

        public static IDictionary<string, List<string>> Options(Guid owner, DashboardController controller)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var w in controller.Widgets)
            {
                if (w.Kind == WidgetKind.Dropdown) result[ControlId(owner, w.Id)] = w.Spec.Options.ToList();
            }
            return result;
        }

        /// <summary>Aplica o valor salvo de um controle (a configuração não muda). Devolve true se o valor mudou.</summary>
        public static bool Apply(DashboardController controller, string widgetId, ControlState saved)
        {
            var spec = controller.Spec.Find(widgetId);
            if (spec == null || saved == null || !spec.IsPersistent) return false;
            WidgetValue value = saved.Number.HasValue ? WidgetValue.FromNumber(saved.Number.Value)
                : saved.Boolean.HasValue ? WidgetValue.FromBoolean(saved.Boolean.Value)
                : WidgetValue.FromText(saved.Text ?? "");
            return controller.State.Set(spec, value);
        }
    }
}
