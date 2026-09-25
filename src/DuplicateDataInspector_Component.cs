using GH_IO.Serialization;

using System;

using System.Collections.Generic;

using System.Drawing;

using System.Linq;

using System.Text;

using System.Windows.Forms;

using Grasshopper.Kernel;

using Grasshopper.Kernel.Data;

using Grasshopper.Kernel.Types;

using Rhino.Geometry;



namespace Buraqueira_Tools

{

    /// <summary>

    /// Inspeciona, detecta e extrai dados e linhas repetidas em DataTrees e Tabelas.

    /// Possui o MODO TABELA que interpreta a DataTree como uma Tabela de Dados:

    /// as ramificações (branches) são tratadas como Colunas e os itens como Linhas,

    /// comparando tuplas completas de linhas para encontrar registros duplicados.

    /// Retorna os valores/linhas repetidos, total de grupos repetidos, contagens,

    /// máscara booleana 1:1 para Cull Pattern e diagnóstico detalhado.

    /// </summary>

    public class DuplicateDataInspector_Component : GH_Component

    {

        // Presets e configurações persistentes

        private int m_presetMode = 0; // 0=PerBranch, 1=Global, 2=BranchVsBranch, 3=Table

        private bool m_caseSensitive = true;

        private bool m_skipNull = true;

        private double m_presetTol = 1e-6;



        public DuplicateDataInspector_Component()

            : base(

                "Duplicate Data Inspector",

                "DupInspector",

                "Inspeciona, detecta e extrai dados e linhas repetidas em DataTrees e Tabelas.\n" +

                "- MODO TABELA: cada ramo é uma coluna e cada índice é uma linha da tabela, comparando linhas completas.\n" +

                "- Modos Por Ramo, Global e Ramo contra Ramo.\n" +

                "- Retorna valores/linhas repetidos, quantidade de grupos repetidos, frequências, máscara para Cull Pattern e relatório.",

                "Glaux Tools",

                "Tree")

        {

        }



        public override Guid ComponentGuid => new Guid("c8220014-e1ef-4000-8000-000000000015");



        protected override Bitmap Icon => GlauxToolsIcons.DuplicateInspector;



        public override GH_Exposure Exposure => GH_Exposure.secondary;



        protected override void RegisterInputParams(GH_InputParamManager pManager)

        {

            pManager.AddGenericParameter("Data", "D", "Árvore ou lista de dados a ser inspecionada para encontrar repetições.", GH_ParamAccess.tree);

            pManager.AddBooleanParameter("Table Mode", "Table", "Ativação direta do MODO TABELA (cada ramo é uma coluna e cada índice é uma linha da tabela). Se True, compara linhas inteiras da tabela.", GH_ParamAccess.item, false);

            pManager.AddGenericParameter("Mode", "M", "Modo de comparação alternativo:\n0 = Por Ramo (Per Branch)\n1 = Global (Toda a Árvore)\n2 = Ramo contra Ramo (Branch vs Branch)\n3 = Tabela (Table Mode: Ramos = Colunas, Índices = Linhas)\nAceita números (0 a 3) ou texto ('PerBranch', 'Global', 'BranchVsBranch', 'Table', 'Tabela', 'Ultra').", GH_ParamAccess.item);

            pManager.AddNumberParameter("Tolerance", "Tol", "Tolerância para igualdade numérica de decimais e coordenadas de pontos 3D (padrão: 1e-6).", GH_ParamAccess.item, 1e-6);

            pManager.AddBooleanParameter("Case Sensitive", "Case", "Diferenciar maiúsculas e minúsculas na comparação de textos (padrão: True).", GH_ParamAccess.item, true);

            pManager.AddBooleanParameter("Skip Nulls", "SkipNull", "Ignorar valores nulos ou vazios para não agrupá-los como repetições falsas (padrão: True).", GH_ParamAccess.item, true);



            pManager[1].Optional = true;

            pManager[2].Optional = true;

            pManager[3].Optional = true;

            pManager[4].Optional = true;

            pManager[5].Optional = true;

        }



        protected override void RegisterOutputParams(GH_OutputParamManager pManager)

        {

            pManager.AddGenericParameter("Repeated", "Rep", "Valores ou linhas repetidas. No Modo Tabela: cada ramo {grupo} contém os valores da linha repetida; nos demais modos: valores repetidos agrupados por ramo ou grupo.", GH_ParamAccess.tree);

            pManager.AddIntegerParameter("Group Count", "NumGroups", "Quantidade total de grupos repetidos distintos encontrados (linhas ou valores que apareceram 2 ou mais vezes).", GH_ParamAccess.item);

            pManager.AddIntegerParameter("Repetition Counts", "Counts", "Quantidade de vezes que cada grupo repetido apareceu na árvore ou tabela (frequência).", GH_ParamAccess.tree);

            pManager.AddIntegerParameter("Repeated Indices", "iRep", "Índices onde ocorrem as repetições (no Modo Tabela: índices de linha da tabela; nos outros modos: índices dos itens ou ramos).", GH_ParamAccess.tree);

            pManager.AddGenericParameter("Clean Data", "Clean", "Dados desduplicados (preserva a estrutura de ramos original da árvore, mantendo apenas 1 ocorrência de cada linha ou valor).", GH_ParamAccess.tree);

            pManager.AddBooleanParameter("Unique Pattern", "Pattern", "Máscara booleana 1:1 (True = mantido/único, False = repetição redundante) pronta para uso no componente Cull Pattern.", GH_ParamAccess.tree);

            pManager.AddTextParameter("Report", "R", "Relatório estruturado de diagnóstico com estatísticas de linhas, grupos repetidos e frequências.", GH_ParamAccess.item);

        }



        protected override void SolveInstance(IGH_DataAccess DA)

        {

            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) || inTree == null || inTree.IsEmpty)

            {

                Message = "Sem Dados";

                DA.SetData(1, 0);

                DA.SetData(6, "Nenhum dado conectado para inspeção.");

                return;

            }



            bool isTable = false;

            DA.GetData(1, ref isTable);



            object modeObj = null;

            DA.GetData(2, ref modeObj);



            int mode = m_presetMode;

            if (modeObj != null)

            {

                if (modeObj is int mInt) mode = mInt;

                else if (modeObj is double mDbl) mode = (int)mDbl;

                else if (modeObj is string mStr)

                {

                    mStr = mStr.Trim().ToLowerInvariant();

                    if (mStr == "table" || mStr == "tabela" || mStr == "ultra" || mStr == "matrix" || mStr == "rows" || mStr == "3") mode = 3;

                    else if (mStr == "global" || mStr == "all" || mStr == "geral" || mStr == "1") mode = 1;

                    else if (mStr == "branchvsbranch" || mStr == "branch" || mStr == "branches" || mStr == "vetores" || mStr == "2") mode = 2;

                    else mode = 0; // per branch

                }

                else if (modeObj is IGH_Goo goo)

                {

                    string s = goo.ToString().Trim().ToLowerInvariant();

                    if (s == "table" || s == "tabela" || s == "ultra" || s == "matrix" || s == "rows" || s == "3") mode = 3;

                    else if (s == "global" || s == "all" || s == "1") mode = 1;

                    else if (s == "branchvsbranch" || s == "2") mode = 2;

                    else mode = 0;

                }

            }



            // O toggle Table Mode tem prioridade de sobreposição imediata

            if (isTable) mode = 3;



            double tol = m_presetTol;

            DA.GetData(3, ref tol);



            bool caseSensitive = m_caseSensitive;

            DA.GetData(4, ref caseSensitive);



            bool skipNull = m_skipNull;

            DA.GetData(5, ref skipNull);



            var outRep = new GH_Structure<IGH_Goo>();

            var outCounts = new GH_Structure<GH_Integer>();

            var outiRep = new GH_Structure<GH_Integer>();

            var outClean = new GH_Structure<IGH_Goo>();

            var outPattern = new GH_Structure<GH_Boolean>();

            int numGroups = 0;

            string reportText = "";

            string msgText = "";

            switch (mode)

            {

                case 3: // MODO TABELA (Ramos = Colunas, Índices = Linhas)

                    ProcessTableMode(inTree, tol, caseSensitive, skipNull, outRep, outCounts, outiRep, outClean, outPattern, out numGroups, out reportText, out msgText);

                    break;



                case 1: // MODO GLOBAL (Toda a Árvore)

                    ProcessGlobalMode(inTree, tol, caseSensitive, skipNull, outRep, outCounts, outiRep, outClean, outPattern, out numGroups, out reportText, out msgText);

                    break;



                case 2: // MODO RAMO CONTRA RAMO (Branch vs Branch)

                    ProcessBranchVsBranchMode(inTree, tol, caseSensitive, skipNull, outRep, outCounts, outiRep, outClean, outPattern, out numGroups, out reportText, out msgText);

                    break;



                default: // MODO POR RAMO (Per Branch)

                    ProcessPerBranchMode(inTree, tol, caseSensitive, skipNull, outRep, outCounts, outiRep, outClean, outPattern, out numGroups, out reportText, out msgText);

                    break;

            }



            Message = msgText;



            DA.SetDataTree(0, outRep);

            DA.SetData(1, numGroups);

            DA.SetDataTree(2, outCounts);

            DA.SetDataTree(3, outiRep);

            DA.SetDataTree(4, outClean);

            DA.SetDataTree(5, outPattern);

            DA.SetData(6, reportText);

        }



        // ==========================================

        // MODO 3: MODO TABELA (TABLE MODE)

        // ==========================================

        public static void ProcessTableMode(

            GH_Structure<IGH_Goo> inTree,

            double tol,

            bool caseSensitive,

            bool skipNull,

            GH_Structure<IGH_Goo> outRep,

            GH_Structure<GH_Integer> outCounts,

            GH_Structure<GH_Integer> outiRep,

            GH_Structure<IGH_Goo> outClean,

            GH_Structure<GH_Boolean> outPattern,

            out int numGroups,

            out string reportText,

            out string messageText)

        {

            int numCols = inTree.Paths.Count;

            var paths = inTree.Paths.ToList();

            var branches = paths.Select(p => inTree.get_Branch(p)).ToList();



            int maxRows = 0;

            for (int c = 0; c < numCols; c++)

            {

                if (branches[c].Count > maxRows) maxRows = branches[c].Count;

            }



            if (maxRows == 0)

            {

                numGroups = 0;

                reportText = "Tabela sem linhas para inspecionar.";

                messageText = "Tabela Vazia";

                return;

            }



            // 1. Gera as chaves compostas para cada linha da tabela

            string[] rowKeys = new string[maxRows];

            var keyToRows = new Dictionary<string, List<int>>();

            var orderedKeys = new List<string>();



            for (int r = 0; r < maxRows; r++)

            {

                var rowSb = new StringBuilder();

                bool allNull = true;



                for (int c = 0; c < numCols; c++)

                {

                    var b = branches[c];

                    IGH_Goo item = (r < b.Count) ? (IGH_Goo)b[r] : null;

                    if (item != null) allNull = false;

                    rowSb.Append(GetItemFingerprint(item, tol, caseSensitive)).Append("¦");

                }



                if (skipNull && allNull)

                {

                    rowKeys[r] = null;

                    continue;

                }



                string rKey = rowSb.ToString();

                rowKeys[r] = rKey;



                if (!keyToRows.TryGetValue(rKey, out var rowIndices))

                {

                    rowIndices = new List<int>();

                    keyToRows[rKey] = rowIndices;

                    orderedKeys.Add(rKey);

                }

                rowIndices.Add(r);

            }



            // 2. Identifica os grupos repetidos (linhas com mais de 1 ocorrência)

            var repeatedKeys = orderedKeys.Where(k => keyToRows[k].Count > 1).ToList();

            numGroups = repeatedKeys.Count;

            int totalRepeatedRows = repeatedKeys.Sum(k => keyToRows[k].Count);

            int totalRedundantRows = repeatedKeys.Sum(k => keyToRows[k].Count - 1);



            // 3. Constrói saídas de repetições

            for (int g = 0; g < numGroups; g++)

            {

                string rKey = repeatedKeys[g];

                var rowIndices = keyToRows[rKey];

                int firstRow = rowIndices[0];

                var gPath = new GH_Path(g);



                // Em Rep: a tupla de valores da linha repetida

                for (int c = 0; c < numCols; c++)

                {

                    var b = branches[c];

                    IGH_Goo item = (firstRow < b.Count) ? (IGH_Goo)b[firstRow] : null;

                    outRep.Append(item ?? new GH_String("<null>"), gPath);

                }



                // Em iRep: todos os índices de linha onde essa linha se repete

                foreach (int rIdx in rowIndices)

                {

                    outiRep.Append(new GH_Integer(rIdx), gPath);

                }



                // Em Counts: quantas vezes essa linha se repetiu

                outCounts.Append(new GH_Integer(rowIndices.Count), gPath);

            }



            // 4. Máscara booleana e Tabela limpa (desduplicada)

            bool[] isRowKept = new bool[maxRows];

            foreach (var kvp in keyToRows)

            {

                // Apenas a primeira ocorrência de cada linha é mantida

                isRowKept[kvp.Value[0]] = true;

            }

            for (int r = 0; r < maxRows; r++)

            {

                if (rowKeys[r] == null) isRowKept[r] = true;

            }



            // Reconstrói a tabela limpa preservando os ramos de colunas originais

            for (int c = 0; c < numCols; c++)

            {

                GH_Path origPath = paths[c];

                var b = branches[c];

                outClean.EnsurePath(origPath);

                for (int r = 0; r < b.Count; r++)

                {

                    if (isRowKept[r])

                    {

                        outClean.Append((IGH_Goo)b[r], origPath);

                    }

                }

            }



            // Máscara 1:1 para cada linha da tabela

            var pPath = new GH_Path(0);

            for (int r = 0; r < maxRows; r++)

            {

                outPattern.Append(new GH_Boolean(isRowKept[r]), pPath);

            }



            // 5. Mensagem do Componente no Canvas

            if (numGroups == 0)

            {

                messageText = "Modo: Tabela\n100% Único\n(0 repetidos)";

            }

            else

            {

                messageText = $"Modo: Tabela\n{numGroups} grupos repetidos\n({totalRepeatedRows} linhas)";

            }



            // 6. Relatório Estruturado

            var sb = new StringBuilder();

            sb.AppendLine("=== BURAQUEIRA TOOLS: DUPLICATE DATA INSPECTOR ===");

            sb.AppendLine("Modo Ativo: MODO TABELA (Table Mode: Ramos = Colunas, Índices = Linhas)");

            sb.AppendLine($"Colunas Inspecionadas: {numCols} | Total de Linhas da Tabela: {maxRows}");

            sb.AppendLine($"Grupos de Linhas Repetidas: {numGroups}");

            sb.AppendLine($"Linhas Participantes de Repetição: {totalRepeatedRows} ({(maxRows > 0 ? (double)totalRepeatedRows / maxRows * 100.0 : 0):F1}%)");

            sb.AppendLine($"Linhas Redundantes Podadas: {totalRedundantRows} ({(maxRows > 0 ? (double)totalRedundantRows / maxRows * 100.0 : 0):F1}%)");

            sb.AppendLine($"Linhas Únicas Mantidas: {maxRows - totalRedundantRows} ({(maxRows > 0 ? (double)(maxRows - totalRedundantRows) / maxRows * 100.0 : 0):F1}%)");

            sb.AppendLine($"Tolerância Numérica: {tol:E2} | Case Sensitive: {caseSensitive} | Ignorar Nulos: {skipNull}");



            if (numGroups > 0)

            {

                sb.AppendLine("\n--- GRUPOS DE LINHAS REPETIDAS ---");

                for (int g = 0; g < Math.Min(numGroups, 50); g++)

                {

                    string rKey = repeatedKeys[g];

                    var rowIndices = keyToRows[rKey];

                    int firstRow = rowIndices[0];



                    sb.Append($"• Grupo #{g}: {rowIndices.Count}x repetições [Linhas: {string.Join(", ", rowIndices)}]\n  Valores: ");

                    var sampleVals = new List<string>();

                    for (int c = 0; c < Math.Min(numCols, 6); c++)

                    {

                        var b = branches[c];

                        IGH_Goo item = (firstRow < b.Count) ? (IGH_Goo)b[firstRow] : null;

                        sampleVals.Add($"Col[{c}]={item?.ToString() ?? "null"}");

                    }

                    if (numCols > 6) sampleVals.Add($"... (+{numCols - 6} colunas)");

                    sb.AppendLine(string.Join(", ", sampleVals));

                }

                if (numGroups > 50)

                {

                    sb.AppendLine($"... (+{numGroups - 50} grupos repetidos adicionais)");

                }

            }

            else

            {

                sb.AppendLine("\n✔ Nenhuma linha repetida encontrada! Todas as linhas da tabela são 100% exclusivas.");

            }



            reportText = sb.ToString();

        }



        [Obsolete("Use ProcessTableMode")]

        public static void ProcessUltraTableMode(

            GH_Structure<IGH_Goo> inTree,

            double tol,

            bool caseSensitive,

            bool skipNull,

            GH_Structure<IGH_Goo> outRep,

            GH_Structure<GH_Integer> outCounts,

            GH_Structure<GH_Integer> outiRep,

            GH_Structure<IGH_Goo> outClean,

            GH_Structure<GH_Boolean> outPattern,

            out int numGroups,

            out string reportText,

            out string messageText)

        {

            ProcessTableMode(inTree, tol, caseSensitive, skipNull, outRep, outCounts, outiRep, outClean, outPattern, out numGroups, out reportText, out messageText);

        }



        // ==========================================

        // MODO 0: POR RAMO (PER BRANCH)

        // ==========================================

        public static void ProcessPerBranchMode(

            GH_Structure<IGH_Goo> inTree,

            double tol,

            bool caseSensitive,

            bool skipNull,

            GH_Structure<IGH_Goo> outRep,

            GH_Structure<GH_Integer> outCounts,

            GH_Structure<GH_Integer> outiRep,

            GH_Structure<IGH_Goo> outClean,

            GH_Structure<GH_Boolean> outPattern,

            out int numGroups,

            out string reportText,

            out string messageText)

        {

            numGroups = 0;

            int totalInspected = inTree.DataCount;

            int totalRepeatedItems = 0;



            var sb = new StringBuilder();

            sb.AppendLine("=== BURAQUEIRA TOOLS: DUPLICATE DATA INSPECTOR ===");

            sb.AppendLine("Modo Ativo: POR RAMO (Per Branch - Duplicatas locais em cada ramo)");

            sb.AppendLine($"Ramos Inspecionados: {inTree.Paths.Count} | Total de Itens: {totalInspected}");



            for (int p = 0; p < inTree.Paths.Count; p++)

            {

                var path = inTree.Paths[p];

                var branch = inTree.get_Branch(path);

                if (branch == null || branch.Count == 0) continue;



                var keyToIndices = new Dictionary<string, List<int>>();

                var branchKeys = new List<string>();



                for (int i = 0; i < branch.Count; i++)

                {

                    var item = (IGH_Goo)branch[i];

                    if (skipNull && item == null) continue;



                    string key = GetItemFingerprint(item, tol, caseSensitive);

                    if (!keyToIndices.TryGetValue(key, out var idxList))

                    {

                        idxList = new List<int>();

                        keyToIndices[key] = idxList;

                        branchKeys.Add(key);

                    }

                    idxList.Add(i);

                }



                var repKeys = branchKeys.Where(k => keyToIndices[k].Count > 1).ToList();

                numGroups += repKeys.Count;



                for (int g = 0; g < repKeys.Count; g++)

                {

                    string k = repKeys[g];

                    var idxList = keyToIndices[k];

                    totalRepeatedItems += idxList.Count;



                    var repPath = path.AppendElement(g);

                    var firstItem = (IGH_Goo)branch[idxList[0]];

                    outRep.Append(firstItem, repPath);



                    foreach (int idx in idxList)

                    {

                        outiRep.Append(new GH_Integer(idx), repPath);

                    }

                    outCounts.Append(new GH_Integer(idxList.Count), repPath);

                }



                // Ramo Limpo e Máscara

                outClean.EnsurePath(path);

                outPattern.EnsurePath(path);

                var keptSet = new HashSet<int>(keyToIndices.Values.Select(v => v[0]));



                for (int i = 0; i < branch.Count; i++)

                {

                    bool isK = keptSet.Contains(i);

                    outPattern.Append(new GH_Boolean(isK), path);

                    if (isK)

                    {

                        outClean.Append((IGH_Goo)branch[i], path);

                    }

                }

            }



            if (numGroups == 0)

            {

                messageText = "Por Ramo\n100% Único\n(0 repetidos)";

                sb.AppendLine("\n✔ Nenhuma repetição encontrada dentro dos ramos!");

            }

            else

            {

                messageText = $"Por Ramo\n{numGroups} grupos repetidos\n({totalRepeatedItems} itens)";

                sb.AppendLine($"Grupos Repetidos Encontrados: {numGroups} | Itens Envolvidos: {totalRepeatedItems}");

            }



            reportText = sb.ToString();

        }



        // ==========================================

        // MODO 1: GLOBAL (TODA A ÁRVORE)

        // ==========================================

        public static void ProcessGlobalMode(

            GH_Structure<IGH_Goo> inTree,

            double tol,

            bool caseSensitive,

            bool skipNull,

            GH_Structure<IGH_Goo> outRep,

            GH_Structure<GH_Integer> outCounts,

            GH_Structure<GH_Integer> outiRep,

            GH_Structure<IGH_Goo> outClean,

            GH_Structure<GH_Boolean> outPattern,

            out int numGroups,

            out string reportText,

            out string messageText)

        {

            var allItems = new List<(IGH_Goo item, GH_Path path, int index)>();

            for (int p = 0; p < inTree.Paths.Count; p++)

            {

                var path = inTree.Paths[p];

                var branch = inTree.get_Branch(path);

                for (int i = 0; i < branch.Count; i++)

                {

                    allItems.Add(((IGH_Goo)branch[i], path, i));

                }

            }



            int totalInspected = allItems.Count;

            var keyToGlobalIndices = new Dictionary<string, List<int>>();

            var orderedGlobalKeys = new List<string>();



            for (int i = 0; i < allItems.Count; i++)

            {

                var (item, path, idx) = allItems[i];

                if (skipNull && item == null) continue;



                string key = GetItemFingerprint(item, tol, caseSensitive);

                if (!keyToGlobalIndices.TryGetValue(key, out var list))

                {

                    list = new List<int>();

                    keyToGlobalIndices[key] = list;

                    orderedGlobalKeys.Add(key);

                }

                list.Add(i);

            }



            var repGlobalKeys = orderedGlobalKeys.Where(k => keyToGlobalIndices[k].Count > 1).ToList();

            numGroups = repGlobalKeys.Count;

            int totalRepeatedItems = repGlobalKeys.Sum(k => keyToGlobalIndices[k].Count);



            for (int g = 0; g < numGroups; g++)

            {

                string k = repGlobalKeys[g];

                var gIndices = keyToGlobalIndices[k];

                var gPath = new GH_Path(g);



                outRep.Append(allItems[gIndices[0]].item, gPath);

                foreach (int idx in gIndices)

                {

                    outiRep.Append(new GH_Integer(idx), gPath);

                }

                outCounts.Append(new GH_Integer(gIndices.Count), gPath);

            }



            // Árvore limpa e Máscara 1:1

            var keptGlobalSet = new HashSet<int>(keyToGlobalIndices.Values.Select(v => v[0]));

            int cursor = 0;

            for (int p = 0; p < inTree.Paths.Count; p++)

            {

                var path = inTree.Paths[p];

                var branch = inTree.get_Branch(path);

                outClean.EnsurePath(path);

                outPattern.EnsurePath(path);



                for (int i = 0; i < branch.Count; i++)

                {

                    bool isK = keptGlobalSet.Contains(cursor);

                    outPattern.Append(new GH_Boolean(isK), path);

                    if (isK)

                    {

                        outClean.Append((IGH_Goo)branch[i], path);

                    }

                    cursor++;

                }

            }



            if (numGroups == 0)

            {

                messageText = "Global\n100% Único\n(0 repetidos)";

            }

            else

            {

                messageText = $"Global\n{numGroups} grupos repetidos\n({totalRepeatedItems} itens)";

            }



            var sb = new StringBuilder();

            sb.AppendLine("=== BURAQUEIRA TOOLS: DUPLICATE DATA INSPECTOR ===");

            sb.AppendLine("Modo Ativo: GLOBAL (Toda a Árvore)");

            sb.AppendLine($"Total de Itens Inspecionados: {totalInspected}");

            sb.AppendLine($"Grupos Repetidos Encontrados: {numGroups} | Itens Envolvidos: {totalRepeatedItems}");

            if (numGroups > 0)

            {

                sb.AppendLine("\n--- GRUPOS REPETIDOS GLOBAIS ---");

                for (int g = 0; g < Math.Min(numGroups, 50); g++)

                {

                    string k = repGlobalKeys[g];

                    var gIndices = keyToGlobalIndices[k];

                    var sample = allItems[gIndices[0]].item;

                    sb.AppendLine($"• Grupo #{g}: {gIndices.Count}x repetições [Valor: {sample?.ToString() ?? "null"}] (Índices lineares: {string.Join(", ", gIndices)})");

                }

            }

            else

            {

                sb.AppendLine("\n✔ Todos os itens da árvore são 100% exclusivos.");

            }



            reportText = sb.ToString();

        }



        // ==========================================

        // MODO 2: RAMO CONTRA RAMO (BRANCH VS BRANCH)

        // ==========================================

        public static void ProcessBranchVsBranchMode(

            GH_Structure<IGH_Goo> inTree,

            double tol,

            bool caseSensitive,

            bool skipNull,

            GH_Structure<IGH_Goo> outRep,

            GH_Structure<GH_Integer> outCounts,

            GH_Structure<GH_Integer> outiRep,

            GH_Structure<IGH_Goo> outClean,

            GH_Structure<GH_Boolean> outPattern,

            out int numGroups,

            out string reportText,

            out string messageText)

        {

            int numBranches = inTree.Paths.Count;

            var branchKeys = new string[numBranches];

            var keyToBranchIdx = new Dictionary<string, List<int>>();

            var orderedBKeys = new List<string>();



            for (int p = 0; p < numBranches; p++)

            {

                var path = inTree.Paths[p];

                var branch = inTree.get_Branch(path);

                var sbK = new StringBuilder();

                sbK.Append($"Count:{branch.Count}|");

                for (int i = 0; i < branch.Count; i++)

                {

                    sbK.Append(GetItemFingerprint((IGH_Goo)branch[i], tol, caseSensitive)).Append(",");

                }

                string bKey = sbK.ToString();

                branchKeys[p] = bKey;



                if (!keyToBranchIdx.TryGetValue(bKey, out var bList))

                {

                    bList = new List<int>();

                    keyToBranchIdx[bKey] = bList;

                    orderedBKeys.Add(bKey);

                }

                bList.Add(p);

            }



            var repBKeys = orderedBKeys.Where(k => keyToBranchIdx[k].Count > 1).ToList();

            numGroups = repBKeys.Count;

            int totalRepeatedBranches = repBKeys.Sum(k => keyToBranchIdx[k].Count);



            for (int g = 0; g < numGroups; g++)

            {

                string k = repBKeys[g];

                var bIndices = keyToBranchIdx[k];

                var gPath = new GH_Path(g);



                var firstBranch = inTree.get_Branch(inTree.Paths[bIndices[0]]);

                for (int i = 0; i < firstBranch.Count; i++)

                {

                    outRep.Append((IGH_Goo)firstBranch[i], gPath);

                }



                foreach (int bIdx in bIndices)

                {

                    outiRep.Append(new GH_Integer(bIdx), gPath);

                }

                outCounts.Append(new GH_Integer(bIndices.Count), gPath);

            }



            // Clean Data e Pattern

            var keptBSet = new HashSet<int>(keyToBranchIdx.Values.Select(v => v[0]));

            var patPath = new GH_Path(0);



            for (int p = 0; p < numBranches; p++)

            {

                bool isK = keptBSet.Contains(p);

                outPattern.Append(new GH_Boolean(isK), patPath);

                if (isK)

                {

                    var path = inTree.Paths[p];

                    var branch = inTree.get_Branch(path);

                    outClean.EnsurePath(path);

                    for (int i = 0; i < branch.Count; i++)

                    {

                        outClean.Append((IGH_Goo)branch[i], path);

                    }

                }

            }



            if (numGroups == 0)

            {

                messageText = "Ramo vs Ramo\n100% Único\n(0 ramos repetidos)";

            }

            else

            {

                messageText = $"Ramo vs Ramo\n{numGroups} grupos repetidos\n({totalRepeatedBranches} ramos)";

            }



            var sb = new StringBuilder();

            sb.AppendLine("=== BURAQUEIRA TOOLS: DUPLICATE DATA INSPECTOR ===");

            sb.AppendLine("Modo Ativo: RAMO CONTRA RAMO (Branch vs Branch - Vetores Idênticos)");

            sb.AppendLine($"Total de Ramos Inspecionados: {numBranches}");

            sb.AppendLine($"Grupos de Ramos Repetidos: {numGroups} | Ramos Envolvidos: {totalRepeatedBranches}");

            if (numGroups > 0)

            {

                sb.AppendLine("\n--- GRUPOS DE RAMOS REPETIDOS ---");

                for (int g = 0; g < Math.Min(numGroups, 50); g++)

                {

                    string k = repBKeys[g];

                    var bIndices = keyToBranchIdx[k];

                    var branchSample = inTree.get_Branch(inTree.Paths[bIndices[0]]);

                    sb.AppendLine($"• Grupo #{g}: {bIndices.Count}x repetições [Índices de Ramo: {string.Join(", ", bIndices)}] (Tamanho: {branchSample.Count} itens)");

                }

            }

            else

            {

                sb.AppendLine("\n✔ Não existem ramos duplicados. Todos os ramos possuem conjuntos distintos.");

            }



            reportText = sb.ToString();

        }



        // ==========================================

        // FINGERPRINT DETERMINÍSTICO E RESILIENTE

        // ==========================================

        public static string GetItemFingerprint(IGH_Goo goo, double tol, bool caseSensitive)

        {

            if (goo == null) return "§null§";



            if (goo is GH_Number num)

            {

                double v = num.Value;

                if (double.IsNaN(v)) return "§num_NaN§";

                if (double.IsPositiveInfinity(v)) return "§num_+Inf§";

                if (double.IsNegativeInfinity(v)) return "§num_-Inf§";

                if (tol > 0)

                {

                    double rounded = Math.Round(v / tol) * tol;

                    return $"§num_{rounded:G12}§";

                }

                return $"§num_{v:G14}§";

            }



            if (goo is GH_Integer integer)

            {

                return $"§int_{integer.Value}§";

            }



            if (goo is GH_String str)

            {

                string val = str.Value ?? "";

                return caseSensitive ? $"§str_{val}§" : $"§str_{val.ToLowerInvariant()}§";

            }



            if (goo is GH_Boolean b)

            {

                return b.Value ? "§bool_T§" : "§bool_F§";

            }



            if (goo is GH_Point pt)

            {

                Point3d p = pt.Value;

                if (tol > 0)

                {

                    double rx = Math.Round(p.X / tol) * tol;

                    double ry = Math.Round(p.Y / tol) * tol;

                    double rz = Math.Round(p.Z / tol) * tol;

                    return $"§pt_{rx:G8}_{ry:G8}_{rz:G8}§";

                }

                return $"§pt_{p.X:G8}_{p.Y:G8}_{p.Z:G8}§";

            }



            if (goo is GH_Vector vec)

            {

                Vector3d v = vec.Value;

                if (tol > 0)

                {

                    double rx = Math.Round(v.X / tol) * tol;

                    double ry = Math.Round(v.Y / tol) * tol;

                    double rz = Math.Round(v.Z / tol) * tol;

                    return $"§vec_{rx:G8}_{ry:G8}_{rz:G8}§";

                }

                return $"§vec_{v.X:G8}_{v.Y:G8}_{v.Z:G8}§";

            }



            return PillDataFingerprint.ComputeGooFingerprint(goo, tol);

        }



        // ==========================================

        // MENU DE CONTEXTO (RIGHT CLICK)

        // ==========================================

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)

        {

            Menu_AppendSeparator(menu);



            Menu_AppendItem(menu, "★ MODO TABELA (Ramos=Colunas, Linhas=Linhas)", (s, e) =>

            {

                m_presetMode = 3;

                ExpireSolution(true);

            }, true, m_presetMode == 3);



            Menu_AppendItem(menu, "Modo Por Ramo (Per Branch)", (s, e) =>

            {

                m_presetMode = 0;

                ExpireSolution(true);

            }, true, m_presetMode == 0);



            Menu_AppendItem(menu, "Modo Global (Toda a Árvore)", (s, e) =>

            {

                m_presetMode = 1;

                ExpireSolution(true);

            }, true, m_presetMode == 1);



            Menu_AppendItem(menu, "Modo Ramo contra Ramo (Branch vs Branch)", (s, e) =>

            {

                m_presetMode = 2;

                ExpireSolution(true);

            }, true, m_presetMode == 2);



            Menu_AppendSeparator(menu);



            Menu_AppendItem(menu, "Diferenciar Maiúsculas/Minúsculas (Case Sensitive)", (s, e) =>

            {

                m_caseSensitive = !m_caseSensitive;

                ExpireSolution(true);

            }, true, m_caseSensitive);



            Menu_AppendItem(menu, "Ignorar Itens Nulos/Vazios", (s, e) =>

            {

                m_skipNull = !m_skipNull;

                ExpireSolution(true);

            }, true, m_skipNull);

        }



        // ==========================================

        // SERIALIZAÇÃO

        // ==========================================

        public override bool Write(GH_IWriter writer)

        {

            writer.SetInt32("PresetMode", m_presetMode);

            writer.SetBoolean("CaseSensitive", m_caseSensitive);

            writer.SetBoolean("SkipNull", m_skipNull);

            writer.SetDouble("PresetTol", m_presetTol);

            return base.Write(writer);

        }



        public override bool Read(GH_IReader reader)

        {

            if (reader.ItemExists("PresetMode")) m_presetMode = reader.GetInt32("PresetMode");

            if (reader.ItemExists("CaseSensitive")) m_caseSensitive = reader.GetBoolean("CaseSensitive");

            if (reader.ItemExists("SkipNull")) m_skipNull = reader.GetBoolean("SkipNull");

            if (reader.ItemExists("PresetTol")) m_presetTol = reader.GetDouble("PresetTol");

            return base.Read(reader);

        }

    }

}

