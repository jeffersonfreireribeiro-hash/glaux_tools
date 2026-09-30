using System;
using Buraqueira_Tools;
using Buraqueira_Tools.Explore;
using Buraqueira_Tools.ProjectState;
using Grasshopper.Kernel.Types;
using Xunit;

namespace Glaux_Tools.Tests
{
    /// <summary>
    /// Adaptadores da Exploração de Design que não dependem do canvas: valor de amostra ↔ estado de cada tipo de
    /// controle (o mesmo formato que o Project Vault aplica), leitura do que o controle aceitou e conversões de dados.
    /// </summary>
    public class ExploreGrasshopperTests
    {
        private static ControlState Current(string kind, double? number = null, bool? boolean = null, string text = null, double? extra = null) =>
            new ControlState { Kind = kind, Id = Guid.NewGuid().ToString("D"), Name = "c", Number = number, Boolean = boolean, Text = text, Extra = extra, Min = number.HasValue ? 0 : (double?)null, Max = number.HasValue ? 100 : (double?)null };

        [Fact]
        public void ToSaved_UsesTheRepresentationOfTheCurrentControl()
        {
            var cont = DesignVariable.Continuous("s", ControlKinds.Slider, "Largura", 0, 10, 0.5);
            var slider = Current(ControlKinds.Slider, number: 3);
            var s1 = DesignControls.ToSaved(cont, 7.5, slider);
            Assert.Equal(7.5, s1.Number);
            Assert.Null(s1.Boolean);
            Assert.Equal(slider.Id, s1.Id);
            Assert.Equal(ControlKinds.Slider, s1.Kind);

            // Níveis numéricos num slider: aplica o número do nível
            var levels = DesignVariable.Choice("s", ControlKinds.Slider, "Espessura", new[] { "2", "4.5", "8" });
            Assert.Equal(4.5, DesignControls.ToSaved(levels, 1, slider).Number);

            // Toggle: booleano
            var toggleVar = DesignVariable.Choice("t", ControlKinds.Toggle, "Ativo", DesignControls.BooleanLevels);
            var toggle = Current(ControlKinds.Toggle, boolean: false);
            Assert.True(DesignControls.ToSaved(toggleVar, 1, toggle).Boolean);
            Assert.False(DesignControls.ToSaved(toggleVar, 0, toggle).Boolean);

            // Value list / dropdown: texto do nível
            var listVar = DesignVariable.Choice("v", ControlKinds.ValueList, "Material", new[] { "Concreto", "Madeira" });
            var list = Current(ControlKinds.ValueList, text: "Concreto");
            Assert.Equal("Madeira", DesignControls.ToSaved(listVar, 1, list).Text);

            // Slider booleano do Slider Pool guarda 0/1 como número
            var poolBool = Current(ControlKinds.PoolSlider, number: 0);
            Assert.Equal(1, DesignControls.ToSaved(toggleVar, 1, poolBool).Number);

            // Domínio do Slider Pool: o fim do domínio é preservado
            var domain = Current(ControlKinds.PoolSlider, number: 2, extra: 9);
            var saved = DesignControls.ToSaved(cont, 4, domain);
            Assert.Equal(4, saved.Number);
            Assert.Equal(9, saved.Extra);
        }

        [Fact]
        public void ActualValue_ReadsBackWhatTheControlAccepted()
        {
            var cont = DesignVariable.Continuous("s", ControlKinds.Slider, "Largura", 0, 10);
            Assert.Equal(3.25, DesignControls.ActualValue(cont, Current(ControlKinds.Slider, number: 3.25), 3.2));
            Assert.Equal(3.2, DesignControls.ActualValue(cont, null, 3.2));

            var toggleVar = DesignVariable.Choice("t", ControlKinds.Toggle, "Ativo", DesignControls.BooleanLevels);
            Assert.Equal(1, DesignControls.ActualValue(toggleVar, Current(ControlKinds.Toggle, boolean: true), 0));
            Assert.Equal(0, DesignControls.ActualValue(toggleVar, Current(ControlKinds.PoolSlider, number: 0), 1));

            var listVar = DesignVariable.Choice("v", ControlKinds.ValueList, "Material", new[] { "Concreto", "Madeira", "Vidro" });
            Assert.Equal(2, DesignControls.ActualValue(listVar, Current(ControlKinds.ValueList, text: "vidro"), 0));
            // Nível que o controle não tem: fica o pedido
            Assert.Equal(1, DesignControls.ActualValue(listVar, Current(ControlKinds.ValueList, text: "Aço"), 1));

            var levels = DesignVariable.Choice("s", ControlKinds.Slider, "Espessura", new[] { "2", "4.5", "8" });
            Assert.Equal(2, DesignControls.ActualValue(levels, Current(ControlKinds.Slider, number: 8), 0));
        }

        [Fact]
        public void SameValue_ComparesNumbersWithRelativeTolerance()
        {
            Assert.True(DesignControls.SameValue(Current(ControlKinds.Slider, number: 1e6), Current(ControlKinds.Slider, number: 1e6 + 1e-6)));
            Assert.False(DesignControls.SameValue(Current(ControlKinds.Slider, number: 1), Current(ControlKinds.Slider, number: 1.001)));
            Assert.True(DesignControls.SameValue(Current(ControlKinds.Toggle, boolean: true), Current(ControlKinds.Toggle, boolean: true)));
            Assert.False(DesignControls.SameValue(Current(ControlKinds.Toggle, boolean: true), Current(ControlKinds.Toggle, boolean: false)));
            Assert.False(DesignControls.SameValue(Current(ControlKinds.ValueList, text: "A"), Current(ControlKinds.ValueList, text: "a")));
            Assert.False(DesignControls.SameValue(Current(ControlKinds.PoolSlider, number: 1, extra: 2), Current(ControlKinds.PoolSlider, number: 1, extra: 3)));
            Assert.False(DesignControls.SameValue(Current(ControlKinds.Slider, number: 1), null));
        }

        [Fact]
        public void Display_And_ToNumber_ConvertGrasshopperData()
        {
            Assert.IsType<GH_Integer>(DesignControls.Display(DesignVariable.Continuous("i", ControlKinds.Slider, "Pavimentos", 1, 10, 1), 4));
            Assert.IsType<GH_Number>(DesignControls.Display(DesignVariable.Continuous("f", ControlKinds.Slider, "Recuo", 0, 1, 0.1), 0.3));
            Assert.IsType<GH_Number>(DesignControls.Display(DesignVariable.Continuous("h", ControlKinds.Slider, "Meio passo", 0.5, 5, 1), 1.5));
            var b = Assert.IsType<GH_Boolean>(DesignControls.Display(DesignVariable.Choice("t", ControlKinds.Toggle, "Ativo", DesignControls.BooleanLevels), 1));
            Assert.True(b.Value);
            var s = Assert.IsType<GH_String>(DesignControls.Display(DesignVariable.Choice("v", ControlKinds.ValueList, "Material", new[] { "Concreto", "Madeira" }), 1));
            Assert.Equal("Madeira", s.Value);
            var n = Assert.IsType<GH_Number>(DesignControls.Display(DesignVariable.Choice("s", ControlKinds.Slider, "Espessura", new[] { "2", "4.5" }), 1));
            Assert.Equal(4.5, n.Value);
            Assert.IsType<GH_Number>(DesignControls.Display(null, 2.5));

            Assert.Equal(2.5, DesignControls.ToNumber(new GH_Number(2.5)));
            Assert.Equal(3, DesignControls.ToNumber(new GH_Integer(3)));
            Assert.Equal(1, DesignControls.ToNumber(new GH_Boolean(true)));
            Assert.Equal(0.75, DesignControls.ToNumber(new GH_String("0,75")));
            Assert.True(double.IsNaN(DesignControls.ToNumber(new GH_String("alto"))));
            Assert.True(double.IsNaN(DesignControls.ToNumber(null)));
        }

        [Fact]
        public void CheckAgainstControl_WarnsWhenOverridesLeaveTheControlRange()
        {
            var original = DesignVariable.Continuous("s", ControlKinds.Slider, "Largura", 0, 10, 0.1);
            var warnings = new System.Collections.Generic.List<string>();
            DesignControls.CheckAgainstControl(original, DesignVariable.Continuous("s", ControlKinds.Slider, "Largura", 2, 8, 0.5), warnings);
            Assert.Empty(warnings);
            DesignControls.CheckAgainstControl(original, DesignVariable.Continuous("s", ControlKinds.Slider, "Largura", -5, 20, 0.5), warnings);
            Assert.Contains(warnings, w => w.Contains("limites"));
            DesignControls.CheckAgainstControl(original, DesignVariable.Continuous("s", ControlKinds.Slider, "Largura", 0, 10, 0.01), warnings);
            Assert.Contains(warnings, w => w.Contains("precisão"));
            DesignControls.CheckAgainstControl(original, DesignVariable.Choice("s", ControlKinds.Slider, "Largura", new[] { "2", "40" }), warnings);
            Assert.Equal(2, warnings.FindAll(w => w.Contains("limites")).Count);

            var list = DesignVariable.Choice("v", ControlKinds.ValueList, "Material", new[] { "Concreto", "Madeira" });
            DesignControls.CheckAgainstControl(list, DesignVariable.Choice("v", ControlKinds.ValueList, "Material", new[] { "Madeira", "Aço" }), warnings);
            Assert.Contains(warnings, w => w.Contains("Aço"));
        }

        [Fact]
        public void PlanGoo_CarriesTheSpace()
        {
            var space = new DesignSpace(new[] { DesignVariable.Continuous("a", ControlKinds.Slider, "A", 0, 1) });
            var plan = DesignSampler.Generate(space, SamplingMethod.Random, new SamplerOptions { Count = 3 });
            var goo = new GH_SamplePlanGoo(plan);
            Assert.True(goo.IsValid);
            Assert.Contains("random", goo.ToString());
            var spaceGoo = new GH_DesignSpaceGoo();
            Assert.True(spaceGoo.CastFrom(goo));
            Assert.Same(space, spaceGoo.Value);
            Assert.False(new GH_SamplePlanGoo().IsValid);
        }
    }
}
