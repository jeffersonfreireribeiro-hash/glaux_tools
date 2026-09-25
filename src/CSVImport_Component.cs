using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class CSVImport_Component : GH_Component
    {
        public CSVImport_Component()
            : base(
                "Import CSV / Excel XML",
                "CSV_In",
                "Lê e estrutura arquivos tabulares (.csv, .tsv, .txt) e planilhas multi-aba Excel XML / SpreadsheetML (.xml, .xls) em árvores de dados 2D indexadas por planilha e coluna.",
                "Glaux Tools",
                "I/O")
        {
        }

        public override Guid ComponentGuid => new Guid("1f2e3d4c-5b6a-7f8e-9d0c-1a2b3c4d5e6f");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "P", "Caminho absoluto do arquivo (.csv, .tsv, .txt, .xml, .xls) no disco.", GH_ParamAccess.item);
            pManager.AddTextParameter("Delimiter", "Delim", "Delimitador de colunas para arquivos de texto (ex: ',', ';', '\\t', '|'). Se vazio ou não fornecido, auto-detecta automaticamente. Ignorado para XML.", GH_ParamAccess.item, "");
            pManager[1].Optional = true;
            pManager.AddBooleanParameter("Has Headers", "Head", "Se True, a primeira linha de cada planilha é tratada como nomes de cabeçalhos de coluna.", GH_ParamAccess.item, true);
            pManager[2].Optional = true;
            pManager.AddBooleanParameter("Read Trigger", "Read", "Gatilho booleano para executar/recarregar a leitura do arquivo.", GH_ParamAccess.item, true);
            pManager[3].Optional = true;
            pManager.AddGenericParameter("Sheet Filter", "Sheet", "Filtro opcional de planilha/aba (para arquivos XML/XLS): nome da aba (ex: 'Loop 1') ou índice 0-based (ex: 0, 1). Se vazio, importa todas as abas.", GH_ParamAccess.item);
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Columns", "Cols", "Árvore de dados (DataTree) 2D onde cada ramo {sheet; col} contém a lista de valores da coluna col na planilha sheet.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Rows", "Rows", "Árvore de dados (DataTree) 2D onde cada ramo {sheet; row} contém a lista de valores da linha row na planilha sheet.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Headers", "H", "Árvore de dados (DataTree) onde cada ramo {sheet} contém os nomes dos cabeçalhos das colunas da planilha sheet.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Sheet Names", "Sheets", "Lista com os nomes de todas as planilhas/abas importadas.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Row Count", "N", "Quantidade de linhas de dados válidas lidas por planilha.", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "Info", "Relatório detalhado do arquivo importado (formato, abas, dimensões).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string filePath = null;
            if (!DA.GetData(0, ref filePath) || string.IsNullOrWhiteSpace(filePath))
            {
                this.Message = "Sem Arquivo";
                return;
            }

            string customDelim = "";
            DA.GetData(1, ref customDelim);

            bool hasHeaders = true;
            DA.GetData(2, ref hasHeaders);

            bool read = true;
            DA.GetData(3, ref read);

            if (!read)
            {
                this.Message = "Pausado (Read=False)";
                return;
            }

            if (!File.Exists(filePath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Arquivo não encontrado: {filePath}");
                this.Message = "Arquivo Não Encontrado";
                return;
            }

            // Obter filtro opcional de aba
            string sheetFilterStr = null;
            int? sheetFilterIdx = null;
            IGH_Goo sheetFilterGoo = null;
            if (DA.GetData(4, ref sheetFilterGoo) && sheetFilterGoo != null)
            {
                if (sheetFilterGoo is GH_Integer ghInt)
                {
                    sheetFilterIdx = ghInt.Value;
                }
                else if (sheetFilterGoo is GH_Number ghNum)
                {
                    sheetFilterIdx = (int)Math.Round(ghNum.Value);
                }
                else
                {
                    string rawStr = sheetFilterGoo.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(rawStr))
                    {
                        if (int.TryParse(rawStr, out int parsedIdx))
                        {
                            sheetFilterIdx = parsedIdx;
                        }
                        else
                        {
                            sheetFilterStr = rawStr;
                        }
                    }
                }
            }

            try
            {
                // Detectar se é XML / SpreadsheetML
                bool isXml = false;
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".xml" || ext == ".xls")
                {
                    isXml = true;
                }
                else
                {
                    try
                    {
                        using (var reader = new StreamReader(filePath, true))
                        {
                            char[] buffer = new char[512];
                            int readChars = reader.Read(buffer, 0, buffer.Length);
                            string headerChunk = new string(buffer, 0, readChars);
                            if (headerChunk.IndexOf("<?xml", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                headerChunk.IndexOf("<Workbook", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                headerChunk.IndexOf("<Worksheet", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                isXml = true;
                            }
                        }
                    }
                    catch
                    {
                        isXml = false;
                    }
                }

                if (isXml)
                {
                    bool xmlOk = false;
                    try
                    {
                        xmlOk = ParseExcelXmlWorkbook(
                            filePath,
                            hasHeaders,
                            sheetFilterStr,
                            sheetFilterIdx,
                            out var xmlColsTree,
                            out var xmlRowsTree,
                            out var xmlHeadersTree,
                            out var xmlSheetNames,
                            out var xmlRowCounts,
                            out string xmlReport);

                        if (xmlOk && xmlSheetNames.Count > 0)
                        {
                            DA.SetDataTree(0, xmlColsTree);
                            DA.SetDataTree(1, xmlRowsTree);
                            DA.SetDataTree(2, xmlHeadersTree);
                            DA.SetDataList(3, xmlSheetNames);
                            DA.SetDataList(4, xmlRowCounts);
                            DA.SetData(5, xmlReport);

                            int totalRows = xmlRowCounts.Sum();
                            this.Message = $"{xmlSheetNames.Count} Aba(s) XML\n{totalRows:N0} Linhas";
                            return;
                        }
                    }
                    catch (Exception xmlEx)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Aviso ao processar XML, tentando fallback CSV: {xmlEx.Message}");
                    }
                }

                // Fallback / Leitura de CSV / Delimitado padrão
                ParseCsvFile(
                    filePath,
                    customDelim,
                    hasHeaders,
                    out var csvColsTree,
                    out var csvRowsTree,
                    out var csvHeadersTree,
                    out var csvSheetNames,
                    out var csvRowCounts,
                    out string csvReport,
                    out char detectedDelim);

                DA.SetDataTree(0, csvColsTree);
                DA.SetDataTree(1, csvRowsTree);
                DA.SetDataTree(2, csvHeadersTree);
                DA.SetDataList(3, csvSheetNames);
                DA.SetDataList(4, csvRowCounts);
                DA.SetData(5, csvReport);

                int dataRowCount = csvRowCounts.Count > 0 ? csvRowCounts[0] : 0;
                int colCount = csvHeadersTree.Branches.Count > 0 ? csvHeadersTree.Branches[0].Count : 0;
                this.Message = $"{dataRowCount:N0} Linhas\n{colCount} Colunas ('{detectedDelim}')";
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Erro ao importar arquivo: {ex.Message}");
                this.Message = "Erro I/O";
            }
        }

        #region Excel XML / SpreadsheetML Engine

        private static bool ParseExcelXmlWorkbook(
            string filePath,
            bool hasHeaders,
            string sheetFilterStr,
            int? sheetFilterIdx,
            out GH_Structure<IGH_Goo> columnsTree,
            out GH_Structure<IGH_Goo> rowsTree,
            out GH_Structure<GH_String> headersTree,
            out List<string> sheetNamesList,
            out List<int> rowCountsList,
            out string report)
        {
            columnsTree = new GH_Structure<IGH_Goo>();
            rowsTree = new GH_Structure<IGH_Goo>();
            headersTree = new GH_Structure<GH_String>();
            sheetNamesList = new List<string>();
            rowCountsList = new List<int>();

            XDocument doc = XDocument.Load(filePath);
            var wsElements = doc.Descendants().Where(e => e.Name.LocalName.Equals("Worksheet", StringComparison.OrdinalIgnoreCase)).ToList();

            if (wsElements.Count == 0)
            {
                report = "Nenhuma tag <Worksheet> encontrada no arquivo XML.";
                return false;
            }

            var reportSb = new StringBuilder();
            var fileInfo = new FileInfo(filePath);
            reportSb.AppendLine("[IMPORTAÇÃO EXCEL WORKBOOK XML / SPREADSHEETML]");
            reportSb.AppendLine($"- Arquivo: {filePath}");
            reportSb.AppendLine($"- Tamanho: {fileInfo.Length:N0} bytes");
            reportSb.AppendLine($"- Total de Abas no Arquivo: {wsElements.Count}");
            reportSb.AppendLine($"- Estrutura DataTree: {{sheet; col}} para Colunas, {{sheet; row}} para Linhas, {{sheet}} para Cabeçalhos.");
            reportSb.AppendLine("--------------------------------------------------");

            int importedCount = 0;

            for (int sIdx = 0; sIdx < wsElements.Count; sIdx++)
            {
                var ws = wsElements[sIdx];

                // Extrair nome da planilha (ss:Name ou Name)
                string wsName = null;
                var nameAttr = ws.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals("Name", StringComparison.OrdinalIgnoreCase));
                if (nameAttr != null)
                {
                    wsName = nameAttr.Value;
                }
                if (string.IsNullOrWhiteSpace(wsName))
                {
                    wsName = $"Planilha_{sIdx + 1}";
                }

                // Verificar filtro se especificado
                if (sheetFilterIdx.HasValue && sheetFilterIdx.Value != sIdx)
                {
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(sheetFilterStr) && !string.Equals(wsName, sheetFilterStr, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Localizar Table
                var table = ws.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("Table", StringComparison.OrdinalIgnoreCase));
                if (table == null)
                {
                    sheetNamesList.Add(wsName);
                    rowCountsList.Add(0);
                    continue;
                }

                var rowElements = table.Elements().Where(e => e.Name.LocalName.Equals("Row", StringComparison.OrdinalIgnoreCase)).ToList();
                var rawRows = new List<List<string>>();
                int maxCols = 0;
                int currRowCounter = 0;

                foreach (var rowElem in rowElements)
                {
                    // Tratar ss:Index na tag Row
                    var rowIndexAttr = rowElem.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals("Index", StringComparison.OrdinalIgnoreCase));
                    if (rowIndexAttr != null && int.TryParse(rowIndexAttr.Value, out int explicitRowIdx))
                    {
                        int targetIdx = explicitRowIdx - 1;
                        while (currRowCounter < targetIdx)
                        {
                            rawRows.Add(new List<string>());
                            currRowCounter++;
                        }
                    }

                    var cellElements = rowElem.Elements().Where(e => e.Name.LocalName.Equals("Cell", StringComparison.OrdinalIgnoreCase)).ToList();
                    var rowValues = new List<string>();
                    int currColCounter = 0;

                    foreach (var cellElem in cellElements)
                    {
                        // Tratar ss:Index na tag Cell
                        var cellIndexAttr = cellElem.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals("Index", StringComparison.OrdinalIgnoreCase));
                        if (cellIndexAttr != null && int.TryParse(cellIndexAttr.Value, out int explicitColIdx))
                        {
                            int targetColIdx = explicitColIdx - 1;
                            while (currColCounter < targetColIdx)
                            {
                                rowValues.Add("");
                                currColCounter++;
                            }
                        }

                        // Extrair valor do nó <Data> ou do próprio <Cell>
                        var dataElem = cellElem.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("Data", StringComparison.OrdinalIgnoreCase));
                        string cellValue = "";
                        if (dataElem != null)
                        {
                            cellValue = dataElem.Value ?? "";
                        }
                        else
                        {
                            cellValue = cellElem.Value ?? "";
                        }

                        rowValues.Add(cellValue);
                        currColCounter++;
                    }

                    if (rowValues.Count > maxCols) maxCols = rowValues.Count;
                    rawRows.Add(rowValues);
                    currRowCounter++;
                }

                // Normalizar todas as linhas para que tenham o mesmo tamanho maxCols
                for (int r = 0; r < rawRows.Count; r++)
                {
                    while (rawRows[r].Count < maxCols)
                    {
                        rawRows[r].Add("");
                    }
                }

                // Processar Cabeçalhos
                int startRow = 0;
                var sheetHeaders = new List<string>();
                if (hasHeaders && rawRows.Count > 0)
                {
                    var hRow = rawRows[0];
                    for (int c = 0; c < maxCols; c++)
                    {
                        string hVal = (c < hRow.Count && !string.IsNullOrWhiteSpace(hRow[c])) ? hRow[c].Trim() : $"Col_{c}";
                        sheetHeaders.Add(hVal);
                    }
                    startRow = 1;
                }
                else
                {
                    for (int c = 0; c < maxCols; c++)
                    {
                        sheetHeaders.Add($"Col_{c}");
                    }
                }

                // Registrar cabeçalhos no ramo {sIdx}
                var sheetPath = new GH_Path(sIdx);
                foreach (var h in sheetHeaders)
                {
                    headersTree.Append(new GH_String(h), sheetPath);
                }

                // Processar Dados
                int dataRowCount = 0;
                for (int r = startRow; r < rawRows.Count; r++)
                {
                    var row = rawRows[r];

                    // Ignorar linhas 100% vazias
                    bool allEmpty = true;
                    for (int c = 0; c < maxCols; c++)
                    {
                        if (!string.IsNullOrWhiteSpace(row[c])) { allEmpty = false; break; }
                    }
                    if (allEmpty) continue;

                    var rowPath = new GH_Path(sIdx, dataRowCount);
                    for (int c = 0; c < maxCols; c++)
                    {
                        string val = row[c];
                        IGH_Goo typedGoo = ParseValueToGoo(val);

                        rowsTree.Append(typedGoo, rowPath);
                        columnsTree.Append(typedGoo, new GH_Path(sIdx, c));
                    }
                    dataRowCount++;
                }

                sheetNamesList.Add(wsName);
                rowCountsList.Add(dataRowCount);
                importedCount++;

                reportSb.AppendLine($"[Aba {sIdx}: '{wsName}']");
                reportSb.AppendLine($"  - Linhas de Dados: {dataRowCount:N0}");
                reportSb.AppendLine($"  - Colunas: {maxCols}");
                reportSb.AppendLine($"  - Cabeçalhos: {string.Join(", ", sheetHeaders)}");
            }

            report = reportSb.ToString();
            return importedCount > 0;
        }

        #endregion

        #region CSV / Delimited File Engine

        private static void ParseCsvFile(
            string filePath,
            string customDelim,
            bool hasHeaders,
            out GH_Structure<IGH_Goo> columnsTree,
            out GH_Structure<IGH_Goo> rowsTree,
            out GH_Structure<GH_String> headersTree,
            out List<string> sheetNamesList,
            out List<int> rowCountsList,
            out string report,
            out char delimiter)
        {
            columnsTree = new GH_Structure<IGH_Goo>();
            rowsTree = new GH_Structure<IGH_Goo>();
            headersTree = new GH_Structure<GH_String>();
            sheetNamesList = new List<string>();
            rowCountsList = new List<int>();

            string[] rawLines = File.ReadAllLines(filePath, DetectEncoding(filePath));
            if (rawLines == null || rawLines.Length == 0)
            {
                report = "O arquivo selecionado está vazio.";
                delimiter = ',';
                return;
            }

            delimiter = DetectDelimiter(rawLines, customDelim);

            string sheetName = Path.GetFileNameWithoutExtension(filePath);
            sheetNamesList.Add(sheetName);

            var headersList = new List<string>();
            int startLine = 0;
            int colCount = 0;

            if (hasHeaders && rawLines.Length > 0)
            {
                var headerFields = ParseCsvLine(rawLines[0], delimiter);
                colCount = headerFields.Count;
                for (int c = 0; c < headerFields.Count; c++)
                {
                    headersList.Add(headerFields[c]);
                }
                startLine = 1;
            }

            int dataRowCount = 0;

            for (int i = startLine; i < rawLines.Length; i++)
            {
                string line = rawLines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var fields = ParseCsvLine(line, delimiter);
                if (fields.Count > colCount) colCount = fields.Count;

                var rowPath = new GH_Path(0, dataRowCount);

                for (int c = 0; c < fields.Count; c++)
                {
                    string strVal = fields[c];
                    IGH_Goo typedGoo = ParseValueToGoo(strVal);

                    rowsTree.Append(typedGoo, rowPath);
                    columnsTree.Append(typedGoo, new GH_Path(0, c));
                }

                dataRowCount++;
            }

            if (!hasHeaders || headersList.Count == 0)
            {
                for (int c = 0; c < colCount; c++)
                {
                    headersList.Add($"Col_{c}");
                }
            }

            var hPath = new GH_Path(0);
            foreach (var h in headersList)
            {
                headersTree.Append(new GH_String(h), hPath);
            }

            rowCountsList.Add(dataRowCount);

            var fileInfo = new FileInfo(filePath);
            report = $"[IMPORTAÇÃO CSV/TEXTO CONCLUÍDA]\n" +
                     $"- Arquivo: {filePath}\n" +
                     $"- Formato: Texto Delimitado ('{delimiter}')\n" +
                     $"- Linhas de Dados: {dataRowCount:N0}\n" +
                     $"- Colunas: {colCount}\n" +
                     $"- Cabeçalhos: {string.Join(", ", headersList)}\n" +
                     $"- Tamanho: {fileInfo.Length:N0} bytes\n" +
                     $"- Estrutura DataTree: Ramo {{0; col}} para Colunas, {{0; row}} para Linhas, {{0}} para Cabeçalhos.";
        }

        private static Encoding DetectEncoding(string filename)
        {
            byte[] bom = new byte[4];
            using (var file = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                file.Read(bom, 0, 4);
            }
            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76) return Encoding.UTF7;
            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode;
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode;
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return Encoding.UTF32;
            return Encoding.Default;
        }

        public static char DetectDelimiter(string[] lines, string customDelim)
        {
            if (!string.IsNullOrEmpty(customDelim))
            {
                if (customDelim == "\\t" || customDelim == "\t") return '\t';
                return customDelim[0];
            }

            char[] candidates = new char[] { ';', ',', '\t', '|' };
            int[] score = new int[candidates.Length];

            int checkLines = Math.Min(lines.Length, 10);
            for (int i = 0; i < checkLines; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                for (int c = 0; c < candidates.Length; c++)
                {
                    int count = 0;
                    bool inQuotes = false;
                    for (int k = 0; k < line.Length; k++)
                    {
                        if (line[k] == '"') inQuotes = !inQuotes;
                        else if (!inQuotes && line[k] == candidates[c]) count++;
                    }
                    score[c] += count;
                }
            }

            int bestIdx = 0;
            int maxScore = -1;
            for (int c = 0; c < candidates.Length; c++)
            {
                if (score[c] > maxScore)
                {
                    maxScore = score[c];
                    bestIdx = c;
                }
            }

            return maxScore > 0 ? candidates[bestIdx] : ',';
        }

        public static List<string> ParseCsvLine(string line, char delimiter)
        {
            var fields = new List<string>();
            if (string.IsNullOrEmpty(line)) return fields;

            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++; // Pula aspas duplas escapadas ("")
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delimiter && !inQuotes)
                {
                    fields.Add(sb.ToString().Trim());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            fields.Add(sb.ToString().Trim());
            return fields;
        }

        #endregion

        #region Value Parsing Helper

        public static IGH_Goo ParseValueToGoo(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return new GH_String("");

            val = val.Trim();

            // Tentar inteiro
            if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intVal))
            {
                return new GH_Integer(intVal);
            }

            // Tentar double com ponto invariante
            if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double dblVal))
            {
                return new GH_Number(dblVal);
            }

            // Tentar double com vírgula (formato brasileiro/europeu)
            if (val.Contains(",") && double.TryParse(val.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double dblValComma))
            {
                return new GH_Number(dblValComma);
            }

            // Tentar booleano
            if (bool.TryParse(val, out bool boolVal))
            {
                return new GH_Boolean(boolVal);
            }

            return new GH_String(val);
        }

        #endregion

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.CSVImport;
    }
}
