// PillInterpolator_Component.cs
// Componente Pill para interpolação contínua de listas e árvores de dados (DataTree)
// com preenchimento automático de lacunas (null, NaN, vazios) e particionamento por contagem de indivíduos.
// Suporta interpolação Linear, Cosseno, Cúbica e Vizinho Mais Próximo, com emissão de máscara booleana e transmissão via PillHub.
// Glaux Tools - Categoria "Pills"

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Métodos suportados de interpolação matemática.
    /// </summary>
    public enum InterpolationMethod
    {
        Linear = 0,
        Cosine = 1,
        CubicSpline = 2,
        Nearest = 3
    }

    /// <summary>
    /// Componente Pill para interpolação de listas e árvores de dados com particionamento por indivíduos.
    /// </summary>
    public class PillInterpolator_Component : GH_Component
    {
        public int TotalGapsFilled { get; private set; } = 0;
        public int TotalItemsProduced { get; private set; } = 0;
        public string CurrentCleanKey { get; private set; } = "";
        public string CurrentUnit => "";
        public string CurrentCategory => "NUM";
        public Color CurrentCategoryColor => Color.FromArgb(56, 189, 248); // Azul cerúleo / turquesa NUM

        private string _lastPublishedKey = "";

        public PillInterpolator_Component()
            : base(
                "Data Interpolator (Gap Filler & Resampler)",
                "DataInterp",
                "Interpola valores numéricos em listas e árvores de dados (DataTree) preenchendo automaticamente lacunas (null, NaN, vazios, '?') e reamostrando tamanhos de listas.\n" +
                "- Se informado um 'Target Count' (ex: 63), expande ou contrai suavemente uma lista de 29 valores para exatamente 63 valores;\n" +
                "- Se informada uma lista de contagens [N1, N2...], particiona e interpola suavemente entre nós;\n" +
                "- Se 'Target Count' for omitido, preenche apenas as lacunas mantendo o tamanho original da lista/ramos;\n" +
                "- Suporta múltiplos métodos: Linear, Cosseno (Smooth), Cúbico (Spline) e Degrau (Nearest);\n" +
                "- Emite a lista interpolada completa, árvore particionada, máscara booleana paralela (True = original, False = interpolado) e contagens;\n" +
                "- Integrado à família Pill com badge [NUM] e transmissão sem fios no PillHub via parâmetro Key.",
                "Glaux Tools",
                "Transform")
        {
        }

        public override Guid ComponentGuid => new Guid("a1100022-e1ef-4000-8000-000000000022");

        protected override Bitmap Icon => GlauxToolsIcons.PillInterpolator;

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
            // 0: Values (Lista ou Árvore)
            pManager.AddGenericParameter(
                "Values", "V",
                "Lista ou Árvore de dados (DataTree) contendo os valores numéricos e eventuais lacunas (null, NaN, vazios, '?').",
                GH_ParamAccess.tree);

            // 1: Target Count / Slices
            pManager.AddIntegerParameter(
                "Target Count / Slices", "N",
                "Quantidade alvo de pontos desejada após interpolação (ex: informe 63 para reamostrar uma lista de 29 valores para exatamente 63 valores!), ou lista de contagens para particionamento. Se omitido, preserva o tamanho original preenchendo apenas as lacunas.",
                GH_ParamAccess.list);
            pManager[1].Optional = true;

            // 2: Method (Método de interpolação)
            pManager.AddIntegerParameter(
                "Method", "M",
                "Método matemático de interpolação:\n" +
                "0 = Linear (interpolação linear contínua padrão)\n" +
                "1 = Cosine / Smooth (curva suave em S com transição contínua)\n" +
                "2 = Cubic Spline (Catmull-Rom cúbico contínuo)\n" +
                "3 = Nearest / Step (repete o valor válido mais próximo)",
                GH_ParamAccess.item, 0);
            pManager[2].Optional = true;

            // 3: Edge Extrapolation (Tratamento de lacunas nas extremidades)
            pManager.AddIntegerParameter(
                "Extrapolate", "Ext",
                "Tratamento de lacunas nas bordas (início ou fim):\n" +
                "0 = Clamp / Hold Edge (mantém constante o primeiro/último valor válido)\n" +
                "1 = Linear Extrapolation (projeta a inclinação dos pontos vizinhos)",
                GH_ParamAccess.item, 0);
            pManager[3].Optional = true;

            // 4: Key (Transmissão PillHub)
            pManager.AddTextParameter(
                "Key", "K",
                "Nome opcional do canal Pill para transmissão sem fios automática dos valores interpolados via PillHub (categoria [NUM]).",
                GH_ParamAccess.item, "");
            pManager[4].Optional = true;

            // 5: Active (Ativação / Pass-through)
            pManager.AddBooleanParameter(
                "Active", "Active",
                "Liga ou desliga a interpolação. Se False, opera em pass-through sem preencher lacunas.",
                GH_ParamAccess.item, true);
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // 0: Values (Árvore de números interpolados)
            pManager.AddNumberParameter(
                "Values", "V",
                "Árvore com os valores numéricos contínuos totalmente interpolados e sem lacunas (tipo double / GH_Number).",
                GH_ParamAccess.tree);

            // 1: Data (Árvore particionada por indivíduos)
            pManager.AddGenericParameter(
                "Data", "D",
                "Estrutura em árvore de dados (DataTree) particionada em ramos conforme a quantidade de indivíduos de cada parte.",
                GH_ParamAccess.tree);

            // 2: Mask (Máscara booleana: True = original, False = interpolado)
            pManager.AddBooleanParameter(
                "Mask", "M",
                "Máscara booleana paralela: True = dado original válido existente; False = valor interpolado / lacuna preenchida.",
                GH_ParamAccess.tree);

            // 3: Counts (Contagem de indivíduos de cada ramo)
            pManager.AddIntegerParameter(
                "Counts", "C",
                "Lista com a contagem real de indivíduos resultante em cada ramo/parte.",
                GH_ParamAccess.list);

            // 4: Gaps Filled (Total de lacunas preenchidas)
            pManager.AddIntegerParameter(
                "Gaps Filled", "G",
                "Quantidade total de lacunas / valores ausentes preenchidos com sucesso por interpolação.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Obter Entradas
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) || inTree == null || inTree.DataCount == 0)
            {
                Message = "Sem Dados";
                return;
            }

            var inCounts = new List<int>();
            DA.GetDataList(1, inCounts);

            int methodInt = 0;
            DA.GetData(2, ref methodInt);
            var method = (InterpolationMethod)Math.Max(0, Math.Min(3, methodInt));

            int extrapInt = 0;
            DA.GetData(3, ref extrapInt);
            bool linearExtrap = (extrapInt == 1);

            string key = "";
            DA.GetData(4, ref key);
            CurrentCleanKey = (key ?? "").Trim();

            bool active = true;
            DA.GetData(5, ref active);

            // Gerenciamento PillHub
            if (!string.IsNullOrEmpty(CurrentCleanKey) && !CurrentCleanKey.Equals(_lastPublishedKey, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(_lastPublishedKey))
                {
                    PillHub.Unpublish(_lastPublishedKey, InstanceGuid);
                }
                _lastPublishedKey = CurrentCleanKey;
            }

            // Modo Pass-Through
            if (!active)
            {
                Message = "Pass-Through";
                DA.SetDataTree(0, inTree);
                DA.SetDataTree(1, inTree);
                return;
            }

            // 2. Processar Árvores e Listas
            var outValuesTree = new GH_Structure<GH_Number>();
            var outDataTree = new GH_Structure<IGH_Goo>();
            var outMaskTree = new GH_Structure<GH_Boolean>();
            var branchCounts = new List<int>();

            int totalGaps = 0;
            int totalProduced = 0;

            // Limpar contagens inválidas (apenas inteiros positivos)
            var validCounts = inCounts.Where(c => c > 0).ToList();

            // Cenário A: Se inTree tem apenas 1 ramo plano e há uma lista de 'Counts' que particiona os dados
            bool isFlatWithPartition = (inTree.Branches.Count == 1 && validCounts.Count > 1);

            if (isFlatWithPartition)
            {
                var flatBranch = inTree.Branches[0];
                var rawItems = ParseRawItems(flatBranch);

                // Verificar se rawItems são nós âncoras (ex: N nós âncoras para K partes entre eles)
                int totalCountSum = validCounts.Sum();
                bool matchesAnchorPoints = (rawItems.Count == validCounts.Count + 1);

                if (matchesAnchorPoints)
                {
                    // Interpolação entre nós âncoras sucessivos
                    for (int partIdx = 0; partIdx < validCounts.Count; partIdx++)
                    {
                        var path = new GH_Path(partIdx);
                        int count = validCounts[partIdx];
                        double? startVal = rawItems[partIdx].Value;
                        double? endVal = rawItems[partIdx + 1].Value;

                        var interpolatedSection = InterpolateSegment(startVal, endVal, count, method);

                        for (int k = 0; k < count; k++)
                        {
                            double v = interpolatedSection[k];
                            bool isAnchor = (k == 0 && rawItems[partIdx].HasValue) || (k == count - 1 && rawItems[partIdx + 1].HasValue);

                            outValuesTree.Append(new GH_Number(v), path);
                            outDataTree.Append(new GH_Number(v), path);
                            outMaskTree.Append(new GH_Boolean(isAnchor), path);

                            if (!isAnchor) totalGaps++;
                        }

                        branchCounts.Add(count);
                        totalProduced += count;
                    }
                }
                else
                {
                    // Fatiamento sequencial baseado em Counts com interpolação de lacunas internas
                    int currentOffset = 0;
                    for (int partIdx = 0; partIdx < validCounts.Count; partIdx++)
                    {
                        var path = new GH_Path(partIdx);
                        int count = validCounts[partIdx];

                        // Obter a fatia correspondente
                        var slice = new List<RawItem>();
                        for (int k = 0; k < count; k++)
                        {
                            if (currentOffset + k < rawItems.Count)
                            {
                                slice.Add(rawItems[currentOffset + k]);
                            }
                            else
                            {
                                slice.Add(new RawItem { HasValue = false, Value = null });
                            }
                        }
                        currentOffset += count;

                        // Preencher lacunas da fatia
                        var filled = FillGapsInList(slice, method, linearExtrap, out int gapsInPart);
                        totalGaps += gapsInPart;

                        for (int k = 0; k < filled.Count; k++)
                        {
                            outValuesTree.Append(new GH_Number(filled[k].Value), path);
                            outDataTree.Append(new GH_Number(filled[k].Value), path);
                            outMaskTree.Append(new GH_Boolean(filled[k].IsOriginal), path);
                        }

                        branchCounts.Add(filled.Count);
                        totalProduced += filled.Count;
                    }
                }
            }
            else
            {
                // Cenário B: Processamento por ramo da DataTree
                int branchIndex = 0;
                foreach (GH_Path originalPath in inTree.Paths)
                {
                    var branch = inTree.get_Branch(originalPath);
                    var rawItems = ParseRawItems(branch);

                    // Determinar se há uma contagem alvo específica para este ramo
                    int targetCount = -1;
                    if (validCounts.Count > 0)
                    {
                        targetCount = validCounts[branchIndex % validCounts.Count];
                    }

                    List<InterpolatedItem> filled;

                    if (targetCount > 0 && targetCount != rawItems.Count)
                    {
                        // Reamostragem / expansão / contração para o targetCount
                        filled = ResampleToCount(rawItems, targetCount, method, linearExtrap, out int gapsInBranch);
                        totalGaps += gapsInBranch;
                    }
                    else
                    {
                        // Preenchimento de lacunas preservando o tamanho do ramo
                        filled = FillGapsInList(rawItems, method, linearExtrap, out int gapsInBranch);
                        totalGaps += gapsInBranch;
                    }

                    foreach (var item in filled)
                    {
                        outValuesTree.Append(new GH_Number(item.Value), originalPath);
                        outDataTree.Append(new GH_Number(item.Value), originalPath);
                        outMaskTree.Append(new GH_Boolean(item.IsOriginal), originalPath);
                    }

                    branchCounts.Add(filled.Count);
                    totalProduced += filled.Count;
                    branchIndex++;
                }
            }

            TotalGapsFilled = totalGaps;
            TotalItemsProduced = totalProduced;

            // 3. Configurar Saídas
            DA.SetDataTree(0, outValuesTree);
            DA.SetDataTree(1, outDataTree);
            DA.SetDataTree(2, outMaskTree);
            DA.SetDataList(3, branchCounts);
            DA.SetData(4, totalGaps);

            // Mensagem de status na cápsula Pill
            if (totalGaps > 0)
            {
                Message = $"{totalGaps} interp. ({totalProduced} pts)";
            }
            else
            {
                Message = $"{totalProduced} pts";
            }

            // Publicação sem fios PillHub
            if (!string.IsNullOrEmpty(CurrentCleanKey) && !CurrentCleanKey.Equals("Sem Chave", StringComparison.OrdinalIgnoreCase))
            {
                var doc = OnPingDocument();
                Guid docGuid = doc != null ? doc.DocumentID : Guid.Empty;
                var gooTree = new GH_Structure<IGH_Goo>();
                foreach (var path in outValuesTree.Paths)
                {
                    foreach (var item in outValuesTree.get_Branch(path))
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
        }

        #region Modelos de Dados Internos

        private struct RawItem
        {
            public bool HasValue;
            public double? Value;
            public object OriginalGoo;
        }

        private struct InterpolatedItem
        {
            public double Value;
            public bool IsOriginal;
        }

        #endregion

        #region Extração e Parsing de Dados com Detecção de Lacunas

        private static List<RawItem> ParseRawItems(System.Collections.IEnumerable branch)
        {
            var list = (branch is System.Collections.ICollection coll)
                ? new List<RawItem>(coll.Count)
                : new List<RawItem>();

            foreach (var item in branch)
            {
                if (item == null)
                {
                    list.Add(new RawItem { HasValue = false, Value = null, OriginalGoo = null });
                    continue;
                }

                // Tentar converter para double nativo
                if (GH_Convert.ToDouble(item, out double val, GH_Conversion.Both))
                {
                    if (double.IsNaN(val) || double.IsInfinity(val))
                    {
                        list.Add(new RawItem { HasValue = false, Value = null, OriginalGoo = item });
                    }
                    else
                    {
                        list.Add(new RawItem { HasValue = true, Value = val, OriginalGoo = item });
                    }
                    continue;
                }

                // Tentar parse textual de números ou marcadores de lacuna
                string s = item.ToString().Trim();
                if (string.IsNullOrEmpty(s) ||
                    s.Equals("?", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("-", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("gap", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("nan", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(new RawItem { HasValue = false, Value = null, OriginalGoo = item });
                }
                else
                {
                    s = s.Replace(',', '.');
                    if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedVal))
                    {
                        list.Add(new RawItem { HasValue = true, Value = parsedVal, OriginalGoo = item });
                    }
                    else
                    {
                        // Dado não numérico considerado lacuna
                        list.Add(new RawItem { HasValue = false, Value = null, OriginalGoo = item });
                    }
                }
            }

            return list;
        }

        #endregion

        #region Algoritmos de Interpolação e Preenchimento de Lacunas

        private static List<InterpolatedItem> FillGapsInList(
            List<RawItem> items,
            InterpolationMethod method,
            bool linearExtrap,
            out int gapsFilled)
        {
            int n = items.Count;
            var result = new List<InterpolatedItem>(n);
            gapsFilled = 0;

            if (n == 0) return result;

            // Encontrar índices de todos os valores válidos
            var validIndices = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (items[i].HasValue && items[i].Value.HasValue)
                {
                    validIndices.Add(i);
                }
            }

            // Se não houver nenhum valor válido, preenche com 0.0
            if (validIndices.Count == 0)
            {
                for (int i = 0; i < n; i++)
                {
                    result.Add(new InterpolatedItem { Value = 0.0, IsOriginal = false });
                    gapsFilled++;
                }
                return result;
            }

            // Se houver apenas 1 valor válido, preenche tudo com ele
            if (validIndices.Count == 1)
            {
                double singleVal = items[validIndices[0]].Value.Value;
                for (int i = 0; i < n; i++)
                {
                    bool isOrig = (i == validIndices[0]);
                    result.Add(new InterpolatedItem { Value = singleVal, IsOriginal = isOrig });
                    if (!isOrig) gapsFilled++;
                }
                return result;
            }

            // Interpolação para cada posição i
            for (int i = 0; i < n; i++)
            {
                if (items[i].HasValue && items[i].Value.HasValue)
                {
                    result.Add(new InterpolatedItem { Value = items[i].Value.Value, IsOriginal = true });
                    continue;
                }

                gapsFilled++;

                // Caso 1: Antes do primeiro valor válido (Extrapolação à esquerda)
                if (i < validIndices[0])
                {
                    int firstIdx = validIndices[0];
                    double firstVal = items[firstIdx].Value.Value;

                    if (linearExtrap && validIndices.Count >= 2)
                    {
                        int secondIdx = validIndices[1];
                        double secondVal = items[secondIdx].Value.Value;
                        double slope = (secondVal - firstVal) / (secondIdx - firstIdx);
                        double extrapolated = firstVal + slope * (i - firstIdx);
                        result.Add(new InterpolatedItem { Value = extrapolated, IsOriginal = false });
                    }
                    else
                    {
                        result.Add(new InterpolatedItem { Value = firstVal, IsOriginal = false });
                    }
                    continue;
                }

                // Caso 2: Depois do último valor válido (Extrapolação à direita)
                if (i > validIndices[validIndices.Count - 1])
                {
                    int lastIdx = validIndices[validIndices.Count - 1];
                    double lastVal = items[lastIdx].Value.Value;

                    if (linearExtrap && validIndices.Count >= 2)
                    {
                        int prevIdx = validIndices[validIndices.Count - 2];
                        double prevVal = items[prevIdx].Value.Value;
                        double slope = (lastVal - prevVal) / (lastIdx - prevIdx);
                        double extrapolated = lastVal + slope * (i - lastIdx);
                        result.Add(new InterpolatedItem { Value = extrapolated, IsOriginal = false });
                    }
                    else
                    {
                        result.Add(new InterpolatedItem { Value = lastVal, IsOriginal = false });
                    }
                    continue;
                }

                // Caso 3: Intermediário entre dois valores válidos mais próximos
                int leftValidIdx = validIndices.Last(idx => idx < i);
                int rightValidIdx = validIndices.First(idx => idx > i);

                double v0 = items[leftValidIdx].Value.Value;
                double v1 = items[rightValidIdx].Value.Value;
                double t = (double)(i - leftValidIdx) / (rightValidIdx - leftValidIdx);

                double interpolatedVal = InterpolateScalar(v0, v1, t, method);
                result.Add(new InterpolatedItem { Value = interpolatedVal, IsOriginal = false });
            }

            return result;
        }

        private static List<double> InterpolateSegment(double? start, double? end, int count, InterpolationMethod method)
        {
            var res = new List<double>(count);
            if (count <= 0) return res;

            double v0 = start ?? end ?? 0.0;
            double v1 = end ?? start ?? 0.0;

            if (count == 1)
            {
                res.Add((v0 + v1) * 0.5);
                return res;
            }

            for (int k = 0; k < count; k++)
            {
                double t = (double)k / (count - 1);
                res.Add(InterpolateScalar(v0, v1, t, method));
            }

            return res;
        }

        private static List<InterpolatedItem> ResampleToCount(
            List<RawItem> items,
            int targetCount,
            InterpolationMethod method,
            bool linearExtrap,
            out int gapsFilled)
        {
            gapsFilled = 0;
            var res = new List<InterpolatedItem>(targetCount);
            if (targetCount <= 0) return res;

            // Primeiro preenche as lacunas na lista original
            var cleaned = FillGapsInList(items, method, linearExtrap, out int preGaps);
            gapsFilled += preGaps;

            if (cleaned.Count == 0)
            {
                for (int i = 0; i < targetCount; i++)
                    res.Add(new InterpolatedItem { Value = 0.0, IsOriginal = false });
                return res;
            }

            if (cleaned.Count == 1 || targetCount == 1)
            {
                double val = cleaned[0].Value;
                for (int i = 0; i < targetCount; i++)
                    res.Add(new InterpolatedItem { Value = val, IsOriginal = (i == 0) });
                return res;
            }

            // Reamostra continuamente de [0 .. cleaned.Count - 1] para targetCount indivíduos
            double scale = (double)(cleaned.Count - 1) / (targetCount - 1);

            for (int k = 0; k < targetCount; k++)
            {
                double srcIdx = k * scale;
                int idx0 = (int)Math.Floor(srcIdx);
                int idx1 = (int)Math.Ceiling(srcIdx);

                if (idx1 >= cleaned.Count) idx1 = cleaned.Count - 1;
                if (idx0 < 0) idx0 = 0;

                double t = srcIdx - idx0;
                double v0 = cleaned[idx0].Value;
                double v1 = cleaned[idx1].Value;

                double val = InterpolateScalar(v0, v1, t, method);
                bool isOrig = (Math.Abs(t) < 1e-9 && cleaned[idx0].IsOriginal);

                res.Add(new InterpolatedItem { Value = val, IsOriginal = isOrig });
                if (!isOrig) gapsFilled++;
            }

            return res;
        }

        private static double InterpolateScalar(double v0, double v1, double t, InterpolationMethod method)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));

            switch (method)
            {
                case InterpolationMethod.Cosine:
                    // Curva em S: t_smooth = (1 - cos(pi * t)) / 2
                    double ft = t * Math.PI;
                    double f = (1.0 - Math.Cos(ft)) * 0.5;
                    return v0 * (1.0 - f) + v1 * f;

                case InterpolationMethod.CubicSpline:
                    // Hermite suave cúbico: 3t^2 - 2t^3
                    double t2 = t * t;
                    double t3 = t2 * t;
                    double h = 3.0 * t2 - 2.0 * t3;
                    return v0 * (1.0 - h) + v1 * h;

                case InterpolationMethod.Nearest:
                    return t < 0.5 ? v0 : v1;

                case InterpolationMethod.Linear:
                default:
                    return v0 + t * (v1 - v0);
            }
        }

        #endregion
    }
}
