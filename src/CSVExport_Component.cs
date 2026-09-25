using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class CSVExport_Component : GH_Component
    {
        public CSVExport_Component()
            : base(
                "Export CSV & Multi-Sheet Workbook",
                "CSV_Out",
                "Exporta listas e árvores de dados (DataTree) para arquivos tabulares (.csv, .tsv, .txt) ou Pastas de Trabalho do Excel (.xls / .xml).\n" +
                "- Suporte nativo a Multi-Sheet / Planilhas dentro de Planilhas: em ambientes em loop (timers, acumulação, otimização), grava cada ciclo como uma aba/planilha separada no mesmo arquivo Excel!\n" +
                "- Controle de delimitador, cabeçalhos, nomes de planilhas e modo de sobrescrita/anexo.",
                "Glaux Tools",
                "I/O")
        {
        }

        public override Guid ComponentGuid => new Guid("2a3b4c5d-6e7f-8a9b-0c1d-2e3f4a5b6c7d");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Árvore de dados (DataTree) ou listas contendo os dados a exportar por colunas {col} ou linhas {row}.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Headers", "H", "Lista opcional de cabeçalhos para as colunas do arquivo.", GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager.AddTextParameter("File Path", "P", "Caminho de destino do arquivo no disco (ex: C:\\Dados\\resultado.csv ou C:\\Dados\\projeto.xls). Se a extensão for .xls ou .xml, ativa automaticamente o modo Multi-Sheet do Excel!", GH_ParamAccess.item);
            pManager.AddTextParameter("Delimiter", "Delim", "Delimitador de colunas para CSV (Padrão: ','). Aceita ',', ';', '\\t', '|'. Ignorado no modo Excel.", GH_ParamAccess.item, ",");
            pManager[3].Optional = true;
            pManager.AddIntegerParameter("Mode", "M", "Modo de gravação:\n" +
                                                      "0 = CSV Overwrite (Sobrescrever arquivo CSV)\n" +
                                                      "1 = CSV Append (Anexar linhas ao final do CSV existente)\n" +
                                                      "2 = Excel Multi-Sheet Workbook (.xls / .xml) [Cada loop/ciclo adiciona uma nova planilha/aba independente]\n" +
                                                      "3 = Folder of CSVs (Salva um arquivo CSV separado para cada loop na pasta informada)\n" +
                                                      "* Se o caminho terminar com .xls ou .xml, o modo 2 é acionado automaticamente.", GH_ParamAccess.item, 0);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Write Trigger", "Write", "Gatilho booleano para executar a gravação no disco.", GH_ParamAccess.item, false);
            pManager[5].Optional = true;
            pManager.AddTextParameter("Sheet Name / Loop ID", "Sheet", "Nome opcional da planilha/aba ou identificador do loop atual (ex: 'Loop_1', 'Geracao_5', 'Teste_A'). Se omitido, nomeia automaticamente como 'Loop 1', 'Loop 2', etc.", GH_ParamAccess.item);
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "P", "Caminho do arquivo gravado no disco.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "OK", "True se os dados foram gravados com sucesso.", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "Info", "Relatório de linhas exportadas, abas criadas e bytes gravados.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<IGH_Goo> dataTree = null;
            if (!DA.GetDataTree(0, out dataTree) || dataTree == null || dataTree.DataCount == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            var rawHeaders = new List<string>();
            DA.GetDataList(1, rawHeaders);

            string filePath = null;
            if (!DA.GetData(2, ref filePath) || string.IsNullOrWhiteSpace(filePath))
            {
                this.Message = "Sem Caminho";
                return;
            }

            string delimStr = ",";
            DA.GetData(3, ref delimStr);
            string delimiter = (delimStr == "\\t") ? "\t" : (string.IsNullOrEmpty(delimStr) ? "," : delimStr);

            int mode = 0;
            DA.GetData(4, ref mode);

            bool write = false;
            DA.GetData(5, ref write);

            string userSheetName = null;
            DA.GetData(6, ref userSheetName);

            // Auto-detectar modo Excel pela extensão do arquivo (.xls ou .xml)
            string ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? "";
            bool isExcelExt = (ext == ".xls" || ext == ".xml");
            if (isExcelExt && mode != 3)
            {
                mode = 2; // Força modo Excel Multi-Sheet Workbook
            }

            if (!write)
            {
                this.Message = "Aguardando (Write=False)";
                DA.SetData(0, filePath);
                DA.SetData(1, false);
                DA.SetData(2, "Defina 'Write' como True para gravar o arquivo no disco.");
                return;
            }

            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                if (mode == 2)
                {
                    // ==============================================================
                    // MODO 2: EXCEL MULTI-SHEET WORKBOOK (.xls / .xml SpreadsheetML)
                    // CADA LOOP OU RAMO DA ÁRVORE VIRA UMA ABA SEPARADA NO EXCEL!
                    // ==============================================================
                    if (!isExcelExt && string.IsNullOrEmpty(ext))
                    {
                        filePath = Path.ChangeExtension(filePath, ".xls");
                    }

                    int totalSheets = 0;
                    int rowsWritten = 0;
                    ExportExcelMultiSheet(filePath, dataTree, rawHeaders, userSheetName, false, out totalSheets, out rowsWritten);

                    var fileInfo = new FileInfo(filePath);
                    string report = $"[PASTA DE TRABALHO EXCEL GRAVADA COM SUCESSO]\n" +
                                    $"- Arquivo: {filePath}\n" +
                                    $"- Total de Abas/Planilhas: {totalSheets}\n" +
                                    $"- Linhas gravadas nesta iteração: {rowsWritten:N0}\n" +
                                    $"- Colunas: {dataTree.Branches.Count}\n" +
                                    $"- Tamanho em Disco: {fileInfo.Length:N0} bytes ({fileInfo.Length / 1024.0:F1} KB)\n" +
                                    $"- Formato: Excel SpreadsheetML (Compatível com Excel, Google Sheets, LibreOffice)\n" +
                                    $"- Data/Hora: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                    DA.SetData(0, filePath);
                    DA.SetData(1, true);
                    DA.SetData(2, report);

                    this.Message = $"Excel: {totalSheets} Abas\n{rowsWritten:N0} Linhas ({fileInfo.Length / 1024.0:F1} KB)";
                    return;
                }
                else if (mode == 3)
                {
                    // ==============================================================
                    // MODO 3: FOLDER OF CSVS (UM ARQUIVO CSV POR LOOP)
                    // ==============================================================
                    string folderDir = Directory.Exists(filePath) || !Path.HasExtension(filePath) ? filePath : Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(folderDir)) Directory.CreateDirectory(folderDir);

                    string sName = !string.IsNullOrWhiteSpace(userSheetName) ? userSheetName : $"Loop_{DateTime.Now:yyyyMMdd_HHmmss}";
                    string loopCsvPath = Path.Combine(folderDir, $"{sName}.csv");

                    int rowsWritten = WriteSingleCsv(loopCsvPath, dataTree, rawHeaders, delimiter, false);
                    var fileInfo = new FileInfo(loopCsvPath);

                    string report = $"[CSV DE LOOP GRAVADO COM SUCESSO]\n" +
                                    $"- Arquivo: {loopCsvPath}\n" +
                                    $"- Linhas Gravadas: {rowsWritten:N0}\n" +
                                    $"- Tamanho: {fileInfo.Length:N0} bytes\n" +
                                    $"- Data/Hora: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                    DA.SetData(0, loopCsvPath);
                    DA.SetData(1, true);
                    DA.SetData(2, report);

                    this.Message = $"Loop CSV Salvo!\n{rowsWritten:N0} Linhas";
                    return;
                }
                else
                {
                    // ==============================================================
                    // MODO 0 ou 1: CSV TRADICIONAL (OVERWRITE ou APPEND)
                    // ==============================================================
                    bool append = (mode == 1) && File.Exists(filePath);
                    int rowsWritten = WriteSingleCsv(filePath, dataTree, rawHeaders, delimiter, append);
                    var fileInfo = new FileInfo(filePath);

                    string report = $"[EXPORTAÇÃO CSV CONCLUÍDA COM SUCESSO]\n" +
                                    $"- Arquivo: {filePath}\n" +
                                    $"- Linhas Exportadas: {rowsWritten:N0}\n" +
                                    $"- Colunas: {dataTree.Branches.Count}\n" +
                                    $"- Tamanho: {fileInfo.Length:N0} bytes\n" +
                                    $"- Modo: {(append ? "Append (Anexado)" : "Overwrite (Sobrescrito)")}\n" +
                                    $"- Data/Hora: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                    DA.SetData(0, filePath);
                    DA.SetData(1, true);
                    DA.SetData(2, report);

                    this.Message = $"Gravado com Sucesso!\n{rowsWritten:N0} Linhas ({fileInfo.Length / 1024.0:F1} KB)";
                }
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Erro ao exportar: {ex.Message}");
                DA.SetData(0, filePath);
                DA.SetData(1, false);
                DA.SetData(2, "Erro: " + ex.Message);
                this.Message = "Erro ao Gravar";
            }
        }

        #region Multi-Sheet Excel Engine

        private static void ExportExcelMultiSheet(string filePath, GH_Structure<IGH_Goo> dataTree, List<string> headers, string requestedSheetName, bool overwrite, out int totalSheetsCount, out int rowsWritten)
        {
            totalSheetsCount = 1;
            rowsWritten = 0;

            bool fileExists = File.Exists(filePath);
            string existingXml = (fileExists && !overwrite) ? File.ReadAllText(filePath, Encoding.UTF8) : null;

            int existingSheets = 0;
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(existingXml))
            {
                var matches = Regex.Matches(existingXml, @"<Worksheet\s+ss:Name=""([^""]+)""");
                existingSheets = matches.Count;
                foreach (Match m in matches)
                {
                    existingNames.Add(m.Groups[1].Value);
                }
            }

            // Gerar nome da aba limpo e válido para Excel
            string baseSheet = string.IsNullOrWhiteSpace(requestedSheetName) ? $"Loop {existingSheets + 1}" : requestedSheetName.Trim();
            baseSheet = CleanExcelSheetName(baseSheet);

            string finalSheetName = baseSheet;
            int counter = 1;
            while (existingNames.Contains(finalSheetName))
            {
                finalSheetName = CleanExcelSheetName($"{baseSheet}_{counter}");
                counter++;
            }

            // Construir o bloco XML da Worksheet
            var wsSb = new StringBuilder();
            wsSb.AppendLine($" <Worksheet ss:Name=\"{EscapeXml(finalSheetName)}\">");
            wsSb.AppendLine("  <Table>");

            int branchCount = dataTree.Branches.Count;

            // Escrever linha de cabeçalho com estilo negrito se fornecida
            if (headers != null && headers.Count > 0)
            {
                wsSb.AppendLine("   <Row ss:StyleID=\"Header\">");
                for (int h = 0; h < headers.Count; h++)
                {
                    wsSb.AppendLine($"    <Cell><Data ss:Type=\"String\">{EscapeXml(headers[h])}</Data></Cell>");
                }
                wsSb.AppendLine("   </Row>");
            }

            if (branchCount > 1)
            {
                // Modo por colunas: dataTree.Branches[col][row]
                int maxRows = 0;
                for (int b = 0; b < branchCount; b++)
                {
                    if (dataTree.Branches[b].Count > maxRows) maxRows = dataTree.Branches[b].Count;
                }

                for (int r = 0; r < maxRows; r++)
                {
                    wsSb.AppendLine("   <Row>");
                    for (int c = 0; c < branchCount; c++)
                    {
                        var branch = dataTree.Branches[c];
                        string valStr = (r < branch.Count && branch[r] != null) ? FormatGooValue(branch[r]) : "";
                        bool isNum = double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double numVal);

                        if (isNum && !double.IsNaN(numVal) && !double.IsInfinity(numVal))
                        {
                            wsSb.AppendLine($"    <Cell><Data ss:Type=\"Number\">{numVal.ToString(CultureInfo.InvariantCulture)}</Data></Cell>");
                        }
                        else
                        {
                            wsSb.AppendLine($"    <Cell><Data ss:Type=\"String\">{EscapeXml(valStr)}</Data></Cell>");
                        }
                    }
                    wsSb.AppendLine("   </Row>");
                    rowsWritten++;
                }
            }
            else
            {
                // Branch única: lista linha por linha
                var singleBranch = dataTree.Branches[0];
                for (int r = 0; r < singleBranch.Count; r++)
                {
                    string valStr = (singleBranch[r] != null) ? FormatGooValue(singleBranch[r]) : "";
                    bool isNum = double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double numVal);

                    wsSb.AppendLine("   <Row>");
                    if (isNum && !double.IsNaN(numVal) && !double.IsInfinity(numVal))
                    {
                        wsSb.AppendLine($"    <Cell><Data ss:Type=\"Number\">{numVal.ToString(CultureInfo.InvariantCulture)}</Data></Cell>");
                    }
                    else
                    {
                        wsSb.AppendLine($"    <Cell><Data ss:Type=\"String\">{EscapeXml(valStr)}</Data></Cell>");
                    }
                    wsSb.AppendLine("   </Row>");
                    rowsWritten++;
                }
            }

            wsSb.AppendLine("  </Table>");
            wsSb.AppendLine(" </Worksheet>");

            string newWorksheetXml = wsSb.ToString();

            if (!string.IsNullOrEmpty(existingXml) && existingXml.Contains("</Workbook>"))
            {
                // Inserir a nova aba/planilha logo antes da tag final de fechamento </Workbook>
                int closeIndex = existingXml.LastIndexOf("</Workbook>", StringComparison.OrdinalIgnoreCase);
                string updatedXml = existingXml.Substring(0, closeIndex) + newWorksheetXml + existingXml.Substring(closeIndex);
                File.WriteAllText(filePath, updatedXml, Encoding.UTF8);
                totalSheetsCount = existingSheets + 1;
            }
            else
            {
                // Criar o arquivo completo do zero com os cabeçalhos oficiais do Excel SpreadsheetML
                var fullSb = new StringBuilder();
                fullSb.AppendLine("<?xml version=\"1.0\"?>");
                fullSb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
                fullSb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
                fullSb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
                fullSb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
                fullSb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"");
                fullSb.AppendLine(" xmlns:html=\"http://www.w3.org/TR/REC-html40\">");
                fullSb.AppendLine(" <Styles>");
                fullSb.AppendLine("  <Style ss:ID=\"Default\" ss:Name=\"Normal\">");
                fullSb.AppendLine("   <Alignment ss:Vertical=\"Bottom\"/>");
                fullSb.AppendLine("  </Style>");
                fullSb.AppendLine("  <Style ss:ID=\"Header\">");
                fullSb.AppendLine("   <Font ss:Bold=\"1\" ss:Color=\"#FFFFFF\"/>");
                fullSb.AppendLine("   <Interior ss:Color=\"#2C3E50\" ss:Pattern=\"Solid\"/>");
                fullSb.AppendLine("   <Alignment ss:Horizontal=\"Center\"/>");
                fullSb.AppendLine("  </Style>");
                fullSb.AppendLine(" </Styles>");
                fullSb.Append(newWorksheetXml);
                fullSb.AppendLine("</Workbook>");

                File.WriteAllText(filePath, fullSb.ToString(), Encoding.UTF8);
                totalSheetsCount = 1;
            }
        }

        private static string CleanExcelSheetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Sheet";
            // O Excel proíbe os caracteres: : \ / ? * [ ]
            string clean = Regex.Replace(name, @"[:\\/?*\[\]]", "_");
            if (clean.Length > 31) clean = clean.Substring(0, 31);
            return clean;
        }

        private static string EscapeXml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("&", "&amp;")
                       .Replace("<", "&lt;")
                       .Replace(">", "&gt;")
                       .Replace("\"", "&quot;")
                       .Replace("'", "&apos;");
        }

        #endregion

        #region Standard CSV Engine

        private static int WriteSingleCsv(string filePath, GH_Structure<IGH_Goo> dataTree, List<string> headers, string delimiter, bool append)
        {
            var sb = new StringBuilder();
            int branchCount = dataTree.Branches.Count;
            int rowsWritten = 0;

            if (headers != null && headers.Count > 0 && !append)
            {
                for (int i = 0; i < headers.Count; i++)
                {
                    if (i > 0) sb.Append(delimiter);
                    sb.Append(EscapeCsvField(headers[i], delimiter));
                }
                sb.AppendLine();
            }

            if (branchCount > 1)
            {
                int maxRows = 0;
                for (int b = 0; b < branchCount; b++)
                {
                    if (dataTree.Branches[b].Count > maxRows) maxRows = dataTree.Branches[b].Count;
                }

                for (int r = 0; r < maxRows; r++)
                {
                    for (int c = 0; c < branchCount; c++)
                    {
                        if (c > 0) sb.Append(delimiter);
                        var branch = dataTree.Branches[c];
                        string valStr = (r < branch.Count && branch[r] != null) ? FormatGooValue(branch[r]) : "";
                        sb.Append(EscapeCsvField(valStr, delimiter));
                    }
                    sb.AppendLine();
                    rowsWritten++;
                }
            }
            else
            {
                var singleBranch = dataTree.Branches[0];
                for (int r = 0; r < singleBranch.Count; r++)
                {
                    string valStr = (singleBranch[r] != null) ? FormatGooValue(singleBranch[r]) : "";
                    sb.AppendLine(EscapeCsvField(valStr, delimiter));
                    rowsWritten++;
                }
            }

            if (append)
            {
                File.AppendAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
            else
            {
                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            }

            return rowsWritten;
        }

        private static string FormatGooValue(IGH_Goo goo)
        {
            if (goo == null) return "";
            object val = goo.SafeScriptVariable() ?? goo;

            if (val is double d)
            {
                return d.ToString("G", CultureInfo.InvariantCulture);
            }
            if (val is float f)
            {
                return f.ToString("G", CultureInfo.InvariantCulture);
            }
            if (val is int i)
            {
                return i.ToString(CultureInfo.InvariantCulture);
            }
            if (val is bool b)
            {
                return b ? "True" : "False";
            }
            return val.ToString();
        }

        private static string EscapeCsvField(string field, string delimiter)
        {
            if (string.IsNullOrEmpty(field)) return "";
            bool needsQuotes = field.Contains(delimiter) || field.Contains("\"") || field.Contains("\n") || field.Contains("\r");
            if (needsQuotes)
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }

        #endregion

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.CSVExport;
    }
}
