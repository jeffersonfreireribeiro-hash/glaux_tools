using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Componente Pill para corte e filtragem de listas e árvores numéricas por um domínio escolhido [Min..Max].
    /// Retorna diretamente os valores numéricos reais aprovados dentro do intervalo (não apenas os IDs/índices),
    /// os valores descartados fora do domínio, máscara booleana e índices originais.
    /// </summary>
    public class PillDomainFilter_Component : GH_Component
    {
        public int InsideCount { get; private set; } = 0;
        public int OutsideCount { get; private set; } = 0;
        public double EffectiveMin { get; private set; } = double.NaN;
        public double EffectiveMax { get; private set; } = double.NaN;
        public string CurrentCleanKey { get; private set; } = "";
        public string CurrentCategory => "NUM";
        public string CurrentUnit => "";
        public Color CurrentCategoryColor => Color.FromArgb(52, 152, 219); // Azul celeste / turquesa NUM
        public string FilterSummaryShort => (!double.IsNaN(EffectiveMin) && !double.IsNaN(EffectiveMax))
            ? $"[{EffectiveMin:F1}..{EffectiveMax:F1}]"
            : (InsideCount > 0 ? $"{InsideCount} in" : "Sem Domínio");

        private string _lastPublishedKey = "";

        public PillDomainFilter_Component()
            : base(
                "Pill Domain Filter",
                "PillClip",
                "Filtra e recorta cirurgicamente valores numéricos, listas ou árvores completas (DataTree) com base em um domínio ou intervalo [Min To Max].\n" +
                "- Retorna diretamente os valores numéricos reais aprovados dentro do intervalo (não apenas o ID);\n" +
                "- Retorna também os valores descartados (fora do domínio), máscara booleana e índices;\n" +
                "- Aceita Interval nativo (ex: '100 To 250') ou limites numéricos individuais (Min / Max);\n" +
                "- Modos: 0 = Compactar (remove fora), 1 = Preservar Ramos, 2 = Alinhar com Null (listas paralelas), 3 = Clamp (grampear);\n" +
                "- Cápsula Pill integrada com badge [NUM], contadores dinâmicos e transmissão sem fio opcional no PillHub.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("a1100021-e1ef-4000-8000-000000000021");

        protected override Bitmap Icon => GlauxToolsIcons.PillDomainFilter;

        public override void CreateAttributes()
        {
            m_attributes = new Pill_Attributes(this);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            if (!string.IsNullOrEmpty(_lastPublishedKey))
            {
                PillHub.Unpublish(_lastPublishedKey, InstanceGuid);
                _lastPublishedKey = "";
            }
            base.RemovedFromDocument(document);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0: Values (Árvore ou Lista de números)
            pManager.AddGenericParameter("Values", "V", "Valores numéricos, listas ou árvore completa (DataTree) a inspecionar e filtrar.", GH_ParamAccess.tree);

            // 1: Domain (Intervalo de corte: ex: 100 To 250)
            pManager.AddGenericParameter("Domain", "D", "Domínio ou intervalo numérico [Min To Max] (ex: 100 To 250). Opcional se Min e Max forem fornecidos.", GH_ParamAccess.item);
            pManager[1].Optional = true;

            // 2: Min (Limite inferior numérico individual)
            pManager.AddNumberParameter("Min", "Min", "Limite inferior aceitável (opcional, sobrescreve ou define o início do domínio).", GH_ParamAccess.item, double.NaN);
            pManager[2].Optional = true;

            // 3: Max (Limite superior numérico individual)
            pManager.AddNumberParameter("Max", "Max", "Limite superior aceitável (opcional, sobrescreve ou define o fim do domínio).", GH_ParamAccess.item, double.NaN);
            pManager[3].Optional = true;

            // 4: Inclusive (Limites inclusivos ou exclusivos)
            pManager.AddBooleanParameter("Inclusive", "Inc", "Se True (padrão), inclui os limites borda: Min <= V <= Max. Se False, estritamente aberto: Min < V < Max.", GH_ParamAccess.item, true);
            pManager[4].Optional = true;

            // 5: Mode
            pManager.AddIntegerParameter("Mode", "M", "Modo de saída:\n0 = Compactar (remove valores fora e descarta ramos vazios)\n1 = Preservar Ramos (mantém ramos vazios estruturais)\n2 = Alinhar com Null (substitui descartados por <null> para sincronizar listas paralelas)\n3 = Clamp / Grampear (em vez de descartar, ajusta números fora para Min ou Max)", GH_ParamAccess.item, 0);
            pManager[5].Optional = true;

            // 6: Key (Transmissão sem fios no PillHub)
            pManager.AddTextParameter("Key", "K", "Nome opcional do canal Pill para transmissão sem fios automática dos valores filtrados pelo PillHub (categoria [NUM]).", GH_ParamAccess.item, "");
            pManager[6].Optional = true;

            // 7: Active (Ativação / Pass-through)
            pManager.AddBooleanParameter("Active", "Active", "Gatilho de ativação do filtro. Se False, opera em pass-through (todos os valores passam em Inside sem cortes).", GH_ParamAccess.item, true);
            pManager[7].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Inside", "In", "Árvore ou lista contendo os valores numéricos reais que estão DENTRO do domínio escolhido [Min..Max].", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Outside", "Out", "Árvore ou lista contendo os valores numéricos descartados (que ficaram FORA do domínio).", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Mask", "M", "Máscara booleana correspondente aos dados originais (True = Dentro, False = Fora).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Indices", "I", "Índices originais dos itens que foram aprovados dentro do domínio.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Count", "C", "Quantidade total de valores aprovados dentro do domínio.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Obter árvore de valores
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) || inTree == null || inTree.DataCount == 0)
            {
                InsideCount = 0;
                OutsideCount = 0;
                Message = "Sem Dados";
                return;
            }

            // 2. Resolver o Domínio [minVal, maxVal]
            double minVal = double.NaN;
            double maxVal = double.NaN;

            // Verificar se usuário passou um Domain (Interval)
            IGH_Goo rawDomain = null;
            if (DA.GetData(1, ref rawDomain) && rawDomain != null)
            {
                Interval parsedInterval;
                if (ExtractInterval(rawDomain, out parsedInterval))
                {
                    minVal = parsedInterval.Min;
                    maxVal = parsedInterval.Max;
                }
            }

            // Verificar se Min individual foi fornecido
            double userMin = double.NaN;
            if (DA.GetData(2, ref userMin) && !double.IsNaN(userMin))
            {
                minVal = userMin;
            }

            // Verificar se Max individual foi fornecido
            double userMax = double.NaN;
            if (DA.GetData(3, ref userMax) && !double.IsNaN(userMax))
            {
                maxVal = userMax;
            }

            // Se ainda não tiver domínio definido, aviso
            if (double.IsNaN(minVal) && double.IsNaN(maxVal))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Defina o domínio de corte através da entrada 'Domain' (ex: 100 To 250) ou dos parâmetros 'Min' e 'Max'.");
                Message = "Defina Domínio";
                return;
            }
            if (double.IsNaN(minVal)) minVal = double.NegativeInfinity;
            if (double.IsNaN(maxVal)) maxVal = double.PositiveInfinity;

            // Garantir que minVal <= maxVal
            if (minVal > maxVal)
            {
                double temp = minVal;
                minVal = maxVal;
                maxVal = temp;
            }

            EffectiveMin = minVal;
            EffectiveMax = maxVal;

            // 3. Parâmetros de controle
            bool inclusive = true;
            DA.GetData(4, ref inclusive);

            int mode = 0;
            DA.GetData(5, ref mode);
            if (mode < 0 || mode > 3) mode = 0;

            string key = "";
            DA.GetData(6, ref key);
            CurrentCleanKey = (key ?? "").Trim();

            bool active = true;
            DA.GetData(7, ref active);

            // 4. Estruturas de Saída
            var outInside = new GH_Structure<GH_Number>();
            var outOutside = new GH_Structure<GH_Number>();
            var outMask = new GH_Structure<GH_Boolean>();
            var outIndices = new GH_Structure<GH_Integer>();

            int inCounter = 0;
            int outCounter = 0;

            foreach (GH_Path path in inTree.Paths)
            {
                var branch = inTree.get_Branch(path);
                if (branch == null) continue;

                var insideList = new List<GH_Number>();
                var outsideList = new List<GH_Number>();
                var maskList = new List<GH_Boolean>();
                var indexList = new List<GH_Integer>();

                for (int i = 0; i < branch.Count; i++)
                {
                    var item = branch[i];
                    if (item == null)
                    {
                        if (mode == 2) // AlignWithNull
                        {
                            insideList.Add(null);
                            outsideList.Add(null);
                            maskList.Add(new GH_Boolean(false));
                        }
                        continue;
                    }

                    double val;
                    if (!GH_Convert.ToDouble(item, out val, GH_Conversion.Both))
                    {
                        string str = item.ToString();
                        if (string.IsNullOrWhiteSpace(str)) continue;
                        str = str.Trim().Replace(',', '.');
                        if (!double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out val))
                        {
                            if (mode == 2)
                            {
                                insideList.Add(null);
                                outsideList.Add(null);
                                maskList.Add(new GH_Boolean(false));
                            }
                            continue;
                        }
                    }

                    if (double.IsNaN(val) || double.IsInfinity(val))
                    {
                        outsideList.Add(new GH_Number(val));
                        maskList.Add(new GH_Boolean(false));
                        outCounter++;
                        if (mode == 2) insideList.Add(null);
                        continue;
                    }

                    // Se Bypass (Active == false)
                    if (!active)
                    {
                        insideList.Add(new GH_Number(val));
                        maskList.Add(new GH_Boolean(true));
                        indexList.Add(new GH_Integer(i));
                        inCounter++;
                        continue;
                    }

                    // Verificação do Domínio
                    bool isInside;
                    if (inclusive)
                    {
                        isInside = (val >= minVal && val <= maxVal);
                    }
                    else
                    {
                        isInside = (val > minVal && val < maxVal);
                    }

                    if (isInside)
                    {
                        insideList.Add(new GH_Number(val));
                        maskList.Add(new GH_Boolean(true));
                        indexList.Add(new GH_Integer(i));
                        inCounter++;
                    }
                    else
                    {
                        if (mode == 3) // Clamp
                        {
                            double clamped = Math.Max(minVal, Math.Min(maxVal, val));
                            insideList.Add(new GH_Number(clamped));
                            outsideList.Add(new GH_Number(val));
                            maskList.Add(new GH_Boolean(false));
                            indexList.Add(new GH_Integer(i));
                            inCounter++;
                            outCounter++;
                        }
                        else
                        {
                            outsideList.Add(new GH_Number(val));
                            maskList.Add(new GH_Boolean(false));
                            outCounter++;
                            if (mode == 2) // AlignWithNull
                            {
                                insideList.Add(null);
                            }
                        }
                    }
                }

                // Adicionar listas aos ramos de saída conforme o modo
                if (insideList.Count > 0 || mode == 1 || mode == 2)
                {
                    outInside.AppendRange(insideList, path);
                }
                if (outsideList.Count > 0 || mode == 1 || mode == 2)
                {
                    outOutside.AppendRange(outsideList, path);
                }
                if (maskList.Count > 0 || mode == 1 || mode == 2)
                {
                    outMask.AppendRange(maskList, path);
                }
                if (indexList.Count > 0 || mode == 1)
                {
                    outIndices.AppendRange(indexList, path);
                }
            }

            InsideCount = inCounter;
            OutsideCount = outCounter;

            // 5. Atribuir Saídas
            DA.SetDataTree(0, outInside);
            DA.SetDataTree(1, outOutside);
            DA.SetDataTree(2, outMask);
            DA.SetDataTree(3, outIndices);
            DA.SetData(4, inCounter);

            // 6. Publicação sem fio no PillHub se chave fornecida
            if (!string.IsNullOrEmpty(CurrentCleanKey) && !CurrentCleanKey.Equals("Sem Chave", StringComparison.OrdinalIgnoreCase))
            {
                var doc = OnPingDocument();
                Guid docGuid = doc != null ? doc.DocumentID : Guid.Empty;
                var gooTree = new GH_Structure<IGH_Goo>();
                foreach (var path in outInside.Paths)
                {
                    foreach (var item in outInside.get_Branch(path))
                    {
                        if (item is IGH_Goo goo)
                            gooTree.Append(goo, path);
                    }
                }

                PillHub.Publish(
                    CurrentCleanKey,
                    gooTree,
                    InstanceGuid,
                    docGuid,
                    CurrentUnit,
                    true,
                    NickName);
                _lastPublishedKey = CurrentCleanKey;
            }
            else if (!string.IsNullOrEmpty(_lastPublishedKey))
            {
                PillHub.Unpublish(_lastPublishedKey, InstanceGuid);
                _lastPublishedKey = "";
            }

            // Mensagem de Status na cápsula
            string minStr = double.IsNegativeInfinity(minVal) ? "-∞" : minVal.ToString("G4", CultureInfo.InvariantCulture);
            string maxStr = double.IsPositiveInfinity(maxVal) ? "+∞" : maxVal.ToString("G4", CultureInfo.InvariantCulture);

            if (!active)
            {
                Message = $"Bypass\n{inCounter} itens";
            }
            else
            {
                Message = $"[{minStr}..{maxStr}]\n{inCounter} in / {outCounter} out";
            }
        }

        private static bool ExtractInterval(IGH_Goo goo, out Interval interval)
        {
            interval = Interval.Unset;
            if (goo == null) return false;

            if (goo is GH_Interval ghInt)
            {
                interval = ghInt.Value;
                return true;
            }

            object script = goo.ScriptVariable();
            if (script is Interval iv)
            {
                interval = iv;
                return true;
            }

            if (goo.CastTo(out interval))
            {
                return true;
            }

            string str = goo.ToString();
            if (string.IsNullOrWhiteSpace(str)) return false;

            // Tentar interpretar formatos: "100 To 250", "100..250", "100;250", "[100, 250]"
            str = str.Trim().TrimStart('[', '(').TrimEnd(']', ')');
            string[] parts = null;
            if (str.IndexOf("To", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                parts = System.Text.RegularExpressions.Regex.Split(str, @"\s+To\s+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            else if (str.Contains(".."))
            {
                parts = str.Split(new[] { ".." }, StringSplitOptions.RemoveEmptyEntries);
            }
            else if (str.Contains(";"))
            {
                parts = str.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            }
            else if (str.Contains(","))
            {
                parts = str.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }

            if (parts != null && parts.Length >= 2)
            {
                double v0, v1;
                if (double.TryParse(parts[0].Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out v0) &&
                    double.TryParse(parts[1].Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out v1))
                {
                    interval = new Interval(v0, v1);
                    return true;
                }
            }

            return false;
        }
    }
}
