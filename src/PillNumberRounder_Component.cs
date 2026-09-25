using System;
using System.Drawing;
using System.Globalization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class PillNumberRounder_Component : GH_Component
    {
        public PillNumberRounder_Component()
            : base(
                "Pill Number Rounder",
                "PillRound",
                "Arredonda valores numéricos individuais, listas ou árvores completas (DataTree) com precisão configurável de casas decimais e modos flexíveis (Nearest, Floor, Ceiling, Truncate). Retorna números nativos e textos formatados.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("a1100020-e1ef-4000-8000-000000000020");

        protected override Bitmap Icon => GlauxToolsIcons.PillNumberRounder;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Numbers", "N", "Valores numéricos, listas ou árvores completas (DataTree) a serem arredondados.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Decimals", "D", "Quantidade de casas decimais desejada (padrão: 2). Se 0, arredonda para número inteiro.", GH_ParamAccess.item, 2);
            pManager.AddIntegerParameter("Mode", "M", "Modo de arredondamento:\n0 = Mais próximo (Midpoint Away From Zero: 1.25 -> 1.3)\n1 = Floor / Para baixo (1.29 -> 1.2)\n2 = Ceiling / Para cima (1.21 -> 1.3)\n3 = Truncate / Cortar casas (1.29 -> 1.2)", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Format", "F", "Formato do texto de saída:\n0 = Ponto decimal internacional (ex: 1.23)\n1 = Vírgula decimal padrão Brasil/Excel (ex: 1,23)", GH_ParamAccess.item, 0);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Numbers", "N", "Árvore com os valores numéricos arredondados (tipo double / GH_Number nativo).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Integers", "I", "Árvore com os valores arredondados para números inteiros (int / GH_Integer).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Text", "T", "Árvore com os valores formatados como texto, mantendo os zeros à direita e o separador configurado.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) || inTree == null || inTree.DataCount == 0)
            {
                Message = "Sem Dados";
                return;
            }

            int decimals = 2;
            DA.GetData(1, ref decimals);
            if (decimals < 0) decimals = 0;
            if (decimals > 15) decimals = 15;

            int mode = 0;
            DA.GetData(2, ref mode);
            if (mode < 0 || mode > 3) mode = 0;

            int format = 0;
            DA.GetData(3, ref format);

            var outNumbers = new GH_Structure<GH_Number>();
            var outIntegers = new GH_Structure<GH_Integer>();
            var outTexts = new GH_Structure<GH_String>();

            double factor = Math.Pow(10, decimals);
            CultureInfo culture = (format == 1) ? CultureInfo.GetCultureInfo("pt-BR") : CultureInfo.InvariantCulture;
            string formatPattern = (decimals == 0) ? "0" : "0." + new string('0', decimals);

            int processedCount = 0;

            foreach (GH_Path path in inTree.Paths)
            {
                var branch = inTree.get_Branch(path);
                foreach (var item in branch)
                {
                    if (item == null) continue;

                    double val;
                    if (!GH_Convert.ToDouble(item, out val, GH_Conversion.Both))
                    {
                        string str = item.ToString();
                        if (string.IsNullOrWhiteSpace(str)) continue;
                        str = str.Trim().Replace(',', '.');
                        if (!double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out val))
                        {
                            continue;
                        }
                    }

                    if (double.IsNaN(val) || double.IsInfinity(val))
                    {
                        continue;
                    }

                    double rounded;
                    switch (mode)
                    {
                        case 1: // Floor
                            rounded = Math.Floor(val * factor) / factor;
                            break;
                        case 2: // Ceiling
                            rounded = Math.Ceiling(val * factor) / factor;
                            break;
                        case 3: // Truncate
                            rounded = Math.Truncate(val * factor) / factor;
                            break;
                        default: // Nearest / AwayFromZero
                            rounded = Math.Round(val, decimals, MidpointRounding.AwayFromZero);
                            break;
                    }

                    outNumbers.Append(new GH_Number(rounded), path);
                    outIntegers.Append(new GH_Integer((int)Math.Round(rounded, MidpointRounding.AwayFromZero)), path);
                    outTexts.Append(new GH_String(rounded.ToString(formatPattern, culture)), path);

                    processedCount++;
                }
            }

            DA.SetDataTree(0, outNumbers);
            DA.SetDataTree(1, outIntegers);
            DA.SetDataTree(2, outTexts);

            string modeName = mode switch
            {
                1 => "Floor",
                2 => "Ceil",
                3 => "Trunc",
                _ => "Round"
            };
            Message = $"{modeName} ({decimals} dec)\n{processedCount} itens";
        }
    }
}
