using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Visualizador interativo de tabelas de dados diretamente no Canvas do Grasshopper.
    /// Permite escolher o intervalo/fatia de linhas visíveis, buscar/selecionar linhas com destaque visual (highlight),
    /// navegar entre páginas com botões interativos e exportar os dados visíveis.
    /// </summary>
    public class DataTableVisualizer_Component : GH_Component
    {
        // Configurações e Estado da Tabela
        public List<string> ColumnHeaders = new List<string>();
        public List<List<string>> AllRows = new List<List<string>>();
        public int TotalRowCount = 0;
        public int TotalColumnCount = 0;

        // Intervalo e Paginação
        public int StartIndex = 0;
        public int EndIndex = 19;
        public int PageSize = 20;
        public int CurrentPage = 0;

        // Seleção e Destaque (Highlight)
        public int HighlightedIndex = -1;
        public List<int> MatchingIndices = new List<int>();
        public string ActiveSearchQuery = "";

        // Customização Visual
        public string DisplayTitle = "Data Table Visualizer";
        public int VisualWidth = 520;
        public int RowHeight = 22;
        public int TableMode = 0; // 0 = Branches as Columns, 1 = Branches as Rows, 2 = Flat List

        public DataTableVisualizer_Component()
            : base(
                "Data Table Visualizer",
                "TableViz",
                "Visualizador interativo de tabelas de dados no Canvas do Grasshopper:\n" +
                "  - Renderiza o grid de dados com numeração de linhas, cabeçalho e zebra\n" +
                "  - Escolha de intervalo/fatia de linhas visíveis (Interval/Domain ou paginação)\n" +
                "  - Busca textual ou seleção por índice com linha destacada em ouro brilhante\n" +
                "  - Clique interativo nas linhas do canvas para selecionar e emitir nos outputs\n" +
                "  - Botões de navegação e exportação rápida (CSV e PNG).",
                "Glaux Tools",
                "Visual")
        {
        }

        public override Guid ComponentGuid => new Guid("a3f1b2c4-8e9d-4000-a000-000000000088");

        protected override Bitmap Icon => GlauxToolsIcons.DataTableVisualizer;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override void CreateAttributes()
        {
            m_attributes = new DataTableVisualizer_Attributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0: Data
            pManager.AddGenericParameter(
                "Data", "D",
                "Árvore ou lista de dados a ser exibida na tabela.\n" +
                "Padrão: cada ramo (branch) é tratado como uma coluna e os itens como linhas.",
                GH_ParamAccess.tree);

            // 1: Headers
            pManager.AddTextParameter(
                "Headers", "H",
                "Nomes das colunas da tabela. Se omitido, gera automaticamente 'Col 0, Col 1...' ou usa os caminhos dos ramos.",
                GH_ParamAccess.list);
            pManager[1].Optional = true;

            // 2: Row Range / Intervalo
            pManager.AddGenericParameter(
                "Row Range", "Range",
                "Intervalo de linhas a serem visualizadas.\n" +
                "Aceita Domínio/Intervalo do Grasshopper (ex: '0 To 25', '10 To 40'),\n" +
                "texto formatado ('10..30', '0-50') ou número inteiro inicial.\n" +
                "Se omitido, utiliza a paginação pelo tamanho de página (Page Size).",
                GH_ParamAccess.item);
            pManager[2].Optional = true;

            // 3: Search / Select
            pManager.AddGenericParameter(
                "Search / Select", "Sel",
                "Linha para buscar e destacar na tabela:\n" +
                "  - Número inteiro (ex: 5): seleciona e destaca a linha do índice correspondente.\n" +
                "  - Texto/palavra-chave (ex: 'Auditório', '0.75'): busca nas colunas e destaca todas as linhas encontradas.",
                GH_ParamAccess.item);
            pManager[3].Optional = true;

            // 4: Page Size
            pManager.AddIntegerParameter(
                "Page Size", "N",
                "Quantidade de linhas visíveis por página quando não for fornecido um intervalo explícito (padrão: 20).",
                GH_ParamAccess.item, 20);
            pManager[4].Optional = true;

            // 5: Table Mode
            pManager.AddIntegerParameter(
                "Table Mode", "Mode",
                "Interpretação da estrutura da árvore:\n" +
                "0: Auto / Ramos como Colunas (padrão)\n" +
                "1: Ramos como Linhas\n" +
                "2: Lista Plana (coluna única)",
                GH_ParamAccess.item, 0);
            pManager[5].Optional = true;

            // 6: Title
            pManager.AddTextParameter(
                "Title", "T",
                "Título exibido no cabeçalho superior do visualizador no canvas.",
                GH_ParamAccess.item, "Data Table Visualizer");
            pManager[6].Optional = true;

            // 7: Width
            pManager.AddIntegerParameter(
                "Width", "W",
                "Largura em pixels da tabela no canvas (mínimo: 320 px, padrão: 520 px).",
                GH_ParamAccess.item, 520);
            pManager[7].Optional = true;

            // 8: Row Height
            pManager.AddIntegerParameter(
                "Row Height", "RowH",
                "Altura em pixels de cada linha da tabela (padrão: 22 px).",
                GH_ParamAccess.item, 22);
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // 0: Visible Data
            pManager.AddGenericParameter(
                "Visible Data", "Vis",
                "Fatia de dados que está atualmente visível no intervalo filtrado (DataTree onde cada ramo é uma coluna).",
                GH_ParamAccess.tree);

            // 1: Selected Row
            pManager.AddTextParameter(
                "Selected Row", "Row",
                "Valores de todas as colunas da linha atualmente destacada/selecionada.",
                GH_ParamAccess.list);

            // 2: Selected Index
            pManager.AddIntegerParameter(
                "Selected Index", "iSel",
                "Índice global (0-indexed) da linha destacada (-1 se nenhuma linha estiver selecionada).",
                GH_ParamAccess.item);

            // 3: Match Indices
            pManager.AddIntegerParameter(
                "Match Indices", "iMatch",
                "Lista dos índices globais de todas as linhas que atenderam ao critério de busca textual.",
                GH_ParamAccess.list);

            // 4: Total Rows
            pManager.AddIntegerParameter(
                "Total Rows", "Rows",
                "Quantidade total de linhas da tabela completa.",
                GH_ParamAccess.item);

            // 5: Total Columns
            pManager.AddIntegerParameter(
                "Total Columns", "Cols",
                "Quantidade de colunas da tabela.",
                GH_ParamAccess.item);

            // 6: Summary
            pManager.AddTextParameter(
                "Summary", "Info",
                "Relatório resumido estruturado da tabela, intervalo exibido e linha destacada.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Obter Árvore de Dados
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) || inTree == null || inTree.IsEmpty)
            {
                Message = "Sem Dados";
                ClearTable();
                DA.SetData(2, -1);
                DA.SetData(4, 0);
                DA.SetData(5, 0);
                DA.SetData(6, "Nenhum dado conectado à tabela.");
                return;
            }

            // 2. Modo da Tabela
            int mode = TableMode;
            DA.GetData(5, ref mode);
            TableMode = Math.Max(0, Math.Min(2, mode));

            // 3. Título e Dimensões Visuais
            string title = "Data Table Visualizer";
            DA.GetData(6, ref title);
            DisplayTitle = string.IsNullOrWhiteSpace(title) ? "Data Table Visualizer" : title;

            int width = 520;
            DA.GetData(7, ref width);
            VisualWidth = Math.Max(320, Math.Min(1400, width));

            int rowH = 22;
            DA.GetData(8, ref rowH);
            RowHeight = Math.Max(18, Math.Min(40, rowH));

            int pageSize = 20;
            DA.GetData(4, ref pageSize);
            PageSize = Math.Max(1, Math.Min(100, pageSize));

            // 4. Headers customizados
            var userHeaders = new List<string>();
            DA.GetDataList(1, userHeaders);

            // 5. Converter DataTree em Matriz 2D de Linhas e Colunas
            BuildMatrixFromTree(inTree, TableMode, userHeaders);

            if (TotalRowCount == 0)
            {
                Message = "Tabela Vazia";
                ClearTable();
                DA.SetData(2, -1);
                DA.SetData(4, 0);
                DA.SetData(5, TotalColumnCount);
                DA.SetData(6, "Tabela não contém linhas com dados válidos.");
                return;
            }

            // 6. Processar Busca e Seleção (Search / Select)
            object searchObj = null;
            DA.GetData(3, ref searchObj);
            ProcessSearchAndSelection(searchObj);

            // 7. Processar Intervalo de Linhas (Row Range)
            object rangeObj = null;
            DA.GetData(2, ref rangeObj);
            ProcessRowRange(rangeObj);

            // 8. Se uma linha foi destacada pela busca ou clique, garantir visibilidade se desejado
            if (HighlightedIndex >= 0 && rangeObj == null)
            {
                // Se não há range fixo travado pelo usuário, posicionar a página onde o item está
                if (HighlightedIndex < StartIndex || HighlightedIndex > EndIndex)
                {
                    CurrentPage = HighlightedIndex / PageSize;
                    StartIndex = CurrentPage * PageSize;
                    EndIndex = Math.Min(TotalRowCount - 1, StartIndex + PageSize - 1);
                }
            }

            // 9. Construir Subconjunto Visível (Output 0: Visible Data)
            var visTree = new GH_Structure<GH_String>();
            int visCount = Math.Max(0, EndIndex - StartIndex + 1);

            for (int col = 0; col < TotalColumnCount; col++)
            {
                var branchPath = new GH_Path(col);
                for (int r = StartIndex; r <= EndIndex && r < TotalRowCount; r++)
                {
                    string val = (col < AllRows[r].Count) ? AllRows[r][col] : "";
                    visTree.Append(new GH_String(val), branchPath);
                }
            }
            DA.SetDataTree(0, visTree);

            // 10. Output 1: Selected Row
            var selectedRowValues = new List<string>();
            if (HighlightedIndex >= 0 && HighlightedIndex < TotalRowCount)
            {
                selectedRowValues = new List<string>(AllRows[HighlightedIndex]);
            }
            DA.SetDataList(1, selectedRowValues);

            // 11. Output 2: Selected Index
            DA.SetData(2, HighlightedIndex);

            // 12. Output 3: Match Indices
            DA.SetDataList(3, MatchingIndices);

            // 13. Output 4 e 5: Total Rows e Total Cols
            DA.SetData(4, TotalRowCount);
            DA.SetData(5, TotalColumnCount);

            // 14. Output 6: Summary
            var sb = new StringBuilder();
            sb.AppendLine($"=== {DisplayTitle} ===");
            sb.AppendLine($"Total de Linhas: {TotalRowCount} | Colunas: {TotalColumnCount}");
            sb.AppendLine($"Intervalo Exibido: [{StartIndex} .. {EndIndex}] ({visCount} linhas visíveis)");
            if (HighlightedIndex >= 0)
            {
                sb.AppendLine($"Linha Destacada: #{HighlightedIndex} -> [{string.Join(" | ", selectedRowValues)}]");
            }
            else
            {
                sb.AppendLine("Nenhuma linha destacada no momento.");
            }

            if (!string.IsNullOrEmpty(ActiveSearchQuery))
            {
                sb.AppendLine($"Busca por: '{ActiveSearchQuery}' ({MatchingIndices.Count} correspondência(s) encontrada(s))");
            }
            DA.SetData(6, sb.ToString().TrimEnd());

            Message = $"{TotalRowCount} L × {TotalColumnCount} C\n[{StartIndex}..{EndIndex}]";
        }

        #region Processamento de Matriz e Dados

        private void ClearTable()
        {
            ColumnHeaders.Clear();
            AllRows.Clear();
            TotalRowCount = 0;
            TotalColumnCount = 0;
            StartIndex = 0;
            EndIndex = 0;
            MatchingIndices.Clear();
        }

        private void BuildMatrixFromTree(GH_Structure<IGH_Goo> tree, int mode, List<string> customHeaders)
        {
            AllRows.Clear();
            ColumnHeaders.Clear();

            if (mode == 1)
            {
                // Modo 1: Ramos são Linhas
                TotalRowCount = tree.PathCount;
                int maxCols = 0;
                foreach (var branch in tree.Branches)
                {
                    if (branch.Count > maxCols) maxCols = branch.Count;
                }
                TotalColumnCount = maxCols;

                for (int r = 0; r < TotalRowCount; r++)
                {
                    var branch = tree.Branches[r];
                    var row = new List<string>(TotalColumnCount);
                    for (int c = 0; c < TotalColumnCount; c++)
                    {
                        string s = (c < branch.Count) ? FormatGoo(branch[c]) : "";
                        row.Add(s);
                    }
                    AllRows.Add(row);
                }

                // Headers
                for (int c = 0; c < TotalColumnCount; c++)
                {
                    string h = (c < customHeaders.Count && !string.IsNullOrWhiteSpace(customHeaders[c]))
                        ? customHeaders[c]
                        : $"Col {c}";
                    ColumnHeaders.Add(h);
                }
            }
            else if (mode == 2)
            {
                // Modo 2: Lista Plana (Coluna Única)
                TotalColumnCount = 1;
                string colName = (customHeaders.Count > 0 && !string.IsNullOrWhiteSpace(customHeaders[0]))
                    ? customHeaders[0]
                    : "Valores";
                ColumnHeaders.Add(colName);

                foreach (var goo in tree.AllData(true))
                {
                    AllRows.Add(new List<string> { FormatGoo(goo) });
                }
                TotalRowCount = AllRows.Count;
            }
            else
            {
                // Modo 0: Ramos são Colunas (padrão)
                TotalColumnCount = tree.PathCount;
                int maxRows = 0;
                foreach (var branch in tree.Branches)
                {
                    if (branch.Count > maxRows) maxRows = branch.Count;
                }
                TotalRowCount = maxRows;

                // Headers das Colunas
                for (int c = 0; c < TotalColumnCount; c++)
                {
                    string h;
                    if (c < customHeaders.Count && !string.IsNullOrWhiteSpace(customHeaders[c]))
                    {
                        h = customHeaders[c];
                    }
                    else
                    {
                        var path = tree.Paths[c];
                        h = (path.Indices.Length == 1) ? $"Col {path.Indices[0]}" : $"{{{path}}}";
                    }
                    ColumnHeaders.Add(h);
                }

                // Preencher Linhas
                for (int r = 0; r < TotalRowCount; r++)
                {
                    var row = new List<string>(TotalColumnCount);
                    for (int c = 0; c < TotalColumnCount; c++)
                    {
                        var branch = tree.Branches[c];
                        string val = (r < branch.Count) ? FormatGoo(branch[r]) : "";
                        row.Add(val);
                    }
                    AllRows.Add(row);
                }
            }
        }

        private static string FormatGoo(IGH_Goo goo)
        {
            if (goo == null) return "-";
            object raw = goo.ScriptVariable();
            if (raw == null) return "-";

            if (raw is double d)
            {
                if (double.IsNaN(d)) return "NaN";
                if (double.IsInfinity(d)) return "∞";
                return Math.Abs(d) >= 10000 || (Math.Abs(d) > 0 && Math.Abs(d) < 0.001)
                    ? d.ToString("0.###e+0", CultureInfo.InvariantCulture)
                    : d.ToString("0.####", CultureInfo.InvariantCulture);
            }
            if (raw is float f)
            {
                return f.ToString("0.####", CultureInfo.InvariantCulture);
            }
            if (raw is int || raw is long || raw is short || raw is byte)
            {
                return raw.ToString();
            }
            if (raw is bool b)
            {
                return b ? "True" : "False";
            }
            if (raw is Point3d pt)
            {
                return $"({pt.X:0.##}, {pt.Y:0.##}, {pt.Z:0.##})";
            }
            if (raw is Vector3d vec)
            {
                return $"[{vec.X:0.##}, {vec.Y:0.##}, {vec.Z:0.##}]";
            }

            return goo.ToString();
        }

        private void ProcessSearchAndSelection(object searchObj)
        {
            MatchingIndices.Clear();
            ActiveSearchQuery = "";

            if (searchObj == null) return;

            // Pode ser número inteiro direto
            if (searchObj is int idx)
            {
                HighlightedIndex = Math.Max(-1, Math.Min(TotalRowCount - 1, idx));
                if (HighlightedIndex >= 0) MatchingIndices.Add(HighlightedIndex);
                return;
            }
            if (searchObj is double dIdx && !double.IsNaN(dIdx))
            {
                HighlightedIndex = Math.Max(-1, Math.Min(TotalRowCount - 1, (int)Math.Round(dIdx)));
                if (HighlightedIndex >= 0) MatchingIndices.Add(HighlightedIndex);
                return;
            }

            // Pode ser IGH_Goo
            if (searchObj is IGH_Goo goo)
            {
                object scriptVar = goo.ScriptVariable();
                if (scriptVar is int gInt)
                {
                    HighlightedIndex = Math.Max(-1, Math.Min(TotalRowCount - 1, gInt));
                    if (HighlightedIndex >= 0) MatchingIndices.Add(HighlightedIndex);
                    return;
                }
                if (scriptVar is double gDbl && !double.IsNaN(gDbl))
                {
                    HighlightedIndex = Math.Max(-1, Math.Min(TotalRowCount - 1, (int)Math.Round(gDbl)));
                    if (HighlightedIndex >= 0) MatchingIndices.Add(HighlightedIndex);
                    return;
                }
                searchObj = goo.ToString();
            }

            // Busca Textual
            string query = searchObj?.ToString()?.Trim();
            if (string.IsNullOrEmpty(query)) return;

            ActiveSearchQuery = query;

            // Tentar interpretar se o texto é um número simples
            if (int.TryParse(query, out int parsedIndex) && parsedIndex >= 0 && parsedIndex < TotalRowCount)
            {
                HighlightedIndex = parsedIndex;
                MatchingIndices.Add(parsedIndex);
            }

            // Realizar busca case-insensitive por substring em todas as colunas
            string qLower = query.ToLowerInvariant();
            for (int r = 0; r < TotalRowCount; r++)
            {
                var row = AllRows[r];
                bool matched = false;
                for (int c = 0; c < row.Count; c++)
                {
                    if (row[c] != null && row[c].ToLowerInvariant().Contains(qLower))
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched && !MatchingIndices.Contains(r))
                {
                    MatchingIndices.Add(r);
                }
            }

            if (MatchingIndices.Count > 0)
            {
                HighlightedIndex = MatchingIndices[0];
            }
        }

        private void ProcessRowRange(object rangeObj)
        {
            if (rangeObj != null)
            {
                // 1. Tentar intervalo/domínio do Rhino
                if (rangeObj is Interval interval)
                {
                    int start = (int)Math.Floor(interval.Min);
                    int end = (int)Math.Floor(interval.Max);
                    SetClampedRange(start, end);
                    return;
                }
                if (rangeObj is GH_Interval ghInterval)
                {
                    int start = (int)Math.Floor(ghInterval.Value.Min);
                    int end = (int)Math.Floor(ghInterval.Value.Max);
                    SetClampedRange(start, end);
                    return;
                }

                // 2. Inteiro único (Start Index)
                if (rangeObj is int sInt)
                {
                    SetClampedRange(sInt, sInt + PageSize - 1);
                    return;
                }
                if (rangeObj is double sDbl && !double.IsNaN(sDbl))
                {
                    int s = (int)sDbl;
                    SetClampedRange(s, s + PageSize - 1);
                    return;
                }

                // 3. String de intervalo: "10..30", "10 To 30", "10-30"
                string str = rangeObj.ToString()?.Trim();
                if (!string.IsNullOrEmpty(str))
                {
                    var parts = str.Split(new string[] { "..", " To ", " to ", "-", "," }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[0].Trim(), out int p0) && int.TryParse(parts[1].Trim(), out int p1))
                    {
                        SetClampedRange(p0, p1);
                        return;
                    }
                    if (parts.Length == 1 && int.TryParse(parts[0].Trim(), out int single))
                    {
                        SetClampedRange(single, single + PageSize - 1);
                        return;
                    }
                }
            }

            // Paginação padrão por página
            int maxPages = Math.Max(1, (int)Math.Ceiling(TotalRowCount / (double)PageSize));
            CurrentPage = Math.Max(0, Math.Min(maxPages - 1, CurrentPage));

            int pStart = CurrentPage * PageSize;
            int pEnd = Math.Min(TotalRowCount - 1, pStart + PageSize - 1);
            SetClampedRange(pStart, pEnd);
        }

        private void SetClampedRange(int start, int end)
        {
            if (start > end)
            {
                int tmp = start;
                start = end;
                end = tmp;
            }

            StartIndex = Math.Max(0, Math.Min(TotalRowCount - 1, start));
            EndIndex = Math.Max(StartIndex, Math.Min(TotalRowCount - 1, end));
        }

        #endregion

        #region Ações de Navegação e Exportação

        public void NavigatePreviousPage()
        {
            int span = Math.Max(1, EndIndex - StartIndex + 1);
            int newStart = Math.Max(0, StartIndex - span);
            int newEnd = newStart + span - 1;
            SetClampedRange(newStart, newEnd);
            CurrentPage = Math.Max(0, CurrentPage - 1);
            ExpireSolution(true);
        }

        public void NavigateNextPage()
        {
            int span = Math.Max(1, EndIndex - StartIndex + 1);
            int newStart = Math.Min(TotalRowCount - 1, StartIndex + span);
            int newEnd = Math.Min(TotalRowCount - 1, newStart + span - 1);
            SetClampedRange(newStart, newEnd);
            CurrentPage++;
            ExpireSolution(true);
        }

        public void SelectRow(int globalIndex)
        {
            if (globalIndex >= 0 && globalIndex < TotalRowCount)
            {
                RecordUndoEvent("Selecionar Linha da Tabela");
                HighlightedIndex = globalIndex;
                ExpireSolution(true);
            }
        }

        public void CopyVisibleCsvToClipboard()
        {
            try
            {
                var sb = new StringBuilder();
                // Headers
                sb.AppendLine(string.Join(";", ColumnHeaders));
                // Rows
                for (int r = StartIndex; r <= EndIndex && r < TotalRowCount; r++)
                {
                    sb.AppendLine(string.Join(";", AllRows[r]));
                }
                Clipboard.SetText(sb.ToString());
            }
            catch { }
        }

        public void SaveCsvDialog()
        {
            try
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "CSV Delimitado (*.csv)|*.csv|Arquivo de Texto (*.txt)|*.txt";
                    sfd.FileName = "Tabela_Dados.csv";
                    sfd.Title = "Salvar Tabela Completa como CSV";

                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine(string.Join(";", ColumnHeaders));
                        for (int r = 0; r < TotalRowCount; r++)
                        {
                            sb.AppendLine(string.Join(";", AllRows[r]));
                        }
                        File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                        Message = "CSV Salvo!";
                    }
                }
            }
            catch { }
        }

        public void SavePngDialog()
        {
            try
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "PNG Image (*.png)|*.png";
                    sfd.FileName = "Tabela_Visualizacao.png";
                    sfd.Title = "Salvar Imagem da Tabela (PNG)";

                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        var bmp = RenderTableBitmap(1000);
                        bmp.Save(sfd.FileName, ImageFormat.Png);
                        Message = "PNG Salvo!";
                    }
                }
            }
            catch { }
        }

        public Bitmap RenderTableBitmap(int targetWidth)
        {
            int visCount = Math.Max(1, EndIndex - StartIndex + 1);
            int rowH = 26;
            int headerH = 34;
            int footerH = 30;
            int totalH = headerH + 28 + (visCount * rowH) + footerH;

            var bmp = new Bitmap(targetWidth, totalH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                var rect = new RectangleF(0, 0, targetWidth, totalH);
                RenderTableGraphics(g, rect, true);
            }
            return bmp;
        }

        #endregion

        #region Renderização Gráfica da Tabela

        public void RenderTableGraphics(Graphics g, RectangleF tableRect, bool hiRes)
        {
            if (tableRect.Width < 50 || tableRect.Height < 50) return;

            // 1. Fundo Geral do Painel com Cantos Arredondados
            using (var bgBrush = new SolidBrush(Color.FromArgb(248, 250, 252)))
            using (var borderPen = new Pen(Color.FromArgb(203, 213, 225), 1.2f))
            {
                g.FillRectangle(bgBrush, tableRect);
                g.DrawRectangle(borderPen, tableRect.X, tableRect.Y, tableRect.Width, tableRect.Height);
            }

            // 2. Barra Superior (Header Card)
            float cardHeaderH = hiRes ? 34 : 26;
            var headerRect = new RectangleF(tableRect.X, tableRect.Y, tableRect.Width, cardHeaderH);
            using (var headBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
            {
                g.FillRectangle(headBrush, headerRect);
            }

            // Título e Badges
            using (var titleFont = new Font("Segoe UI", hiRes ? 9.5f : 8f, FontStyle.Bold))
            using (var badgeFont = new Font("Segoe UI", hiRes ? 8f : 6.8f, FontStyle.Regular))
            using (var titleBrush = new SolidBrush(Color.White))
            using (var badgeBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
            {
                g.DrawString(DisplayTitle, titleFont, titleBrush, headerRect.X + 10, headerRect.Y + (cardHeaderH - titleFont.Height) / 2f);

                string badgeText = $"{TotalRowCount} Linhas × {TotalColumnCount} Colunas  |  Exibindo [{StartIndex}..{EndIndex}]";
                var badgeSize = g.MeasureString(badgeText, badgeFont);
                g.DrawString(badgeText, badgeFont, badgeBrush, headerRect.Right - badgeSize.Width - 10, headerRect.Y + (cardHeaderH - badgeSize.Height) / 2f);
            }

            // 3. Cabeçalho das Colunas
            float colHeaderH = hiRes ? 26 : 22;
            var colHeaderRect = new RectangleF(tableRect.X, headerRect.Bottom, tableRect.Width, colHeaderH);
            using (var colHeadBrush = new SolidBrush(Color.FromArgb(51, 65, 85)))
            using (var colHeadLine = new Pen(Color.FromArgb(71, 85, 105), 1f))
            {
                g.FillRectangle(colHeadBrush, colHeaderRect);
                g.DrawLine(colHeadLine, colHeaderRect.X, colHeaderRect.Bottom, colHeaderRect.Right, colHeaderRect.Bottom);
            }

            // Dimensões das Colunas
            float rowNumColW = hiRes ? 48 : 38;
            float availableDataW = Math.Max(50, tableRect.Width - rowNumColW);
            float colW = (TotalColumnCount > 0) ? availableDataW / TotalColumnCount : availableDataW;

            // Rótulo da Coluna de Número (#)
            using (var numHeadFont = new Font("Segoe UI", hiRes ? 8f : 7f, FontStyle.Bold))
            using (var numHeadBrush = new SolidBrush(Color.FromArgb(203, 213, 225)))
            {
                var sfNum = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("#", numHeadFont, numHeadBrush, new RectangleF(colHeaderRect.X, colHeaderRect.Y, rowNumColW, colHeaderH), sfNum);
            }

            // Rótulos das Colunas de Dados
            using (var colFont = new Font("Segoe UI", hiRes ? 8f : 7f, FontStyle.Bold))
            using (var colTextBrush = new SolidBrush(Color.White))
            using (var divPen = new Pen(Color.FromArgb(71, 85, 105), 1f))
            {
                var sfCol = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };

                for (int c = 0; c < TotalColumnCount; c++)
                {
                    float cx = colHeaderRect.X + rowNumColW + (c * colW);
                    var cRect = new RectangleF(cx + 6, colHeaderRect.Y, colW - 12, colHeaderH);
                    string hText = (c < ColumnHeaders.Count) ? ColumnHeaders[c] : $"Col {c}";
                    g.DrawString(hText, colFont, colTextBrush, cRect, sfCol);

                    // Divisor vertical
                    g.DrawLine(divPen, cx, colHeaderRect.Y, cx, colHeaderRect.Bottom);
                }
            }

            // 4. Linhas de Dados
            float curY = colHeaderRect.Bottom;
            float rowH = hiRes ? 26 : RowHeight;
            int visCount = Math.Max(0, EndIndex - StartIndex + 1);

            using (var regularFont = new Font("Segoe UI", hiRes ? 8.5f : 7.2f, FontStyle.Regular))
            using (var boldFont = new Font("Segoe UI", hiRes ? 8.5f : 7.2f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
            using (var mutedBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
            using (var zebraBrush = new SolidBrush(Color.FromArgb(241, 245, 249)))
            using (var whiteBrush = new SolidBrush(Color.White))
            using (var hlBgBrush = new SolidBrush(Color.FromArgb(254, 240, 138)))
            using (var hlBorderPen = new Pen(Color.FromArgb(234, 179, 8), 1.5f))
            using (var hlTextBrush = new SolidBrush(Color.FromArgb(69, 26, 3)))
            using (var markerBrush = new SolidBrush(Color.FromArgb(202, 138, 4)))
            using (var gridLinePen = new Pen(Color.FromArgb(226, 232, 240), 1f))
            using (var divLinePen = new Pen(Color.FromArgb(226, 232, 240), 1f))
            {
                var sfData = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };

                var sfIndex = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                for (int i = 0; i < visCount; i++)
                {
                    int globalR = StartIndex + i;
                    if (globalR >= TotalRowCount) break;

                    var rRect = new RectangleF(tableRect.X, curY, tableRect.Width, rowH);
                    bool isHighlighted = (globalR == HighlightedIndex);
                    bool isMatched = MatchingIndices.Contains(globalR);

                    // Fundo da linha
                    if (isHighlighted)
                    {
                        g.FillRectangle(hlBgBrush, rRect);
                        g.DrawRectangle(hlBorderPen, rRect.X + 0.5f, rRect.Y + 0.5f, rRect.Width - 1f, rRect.Height - 1f);
                    }
                    else if (isMatched)
                    {
                        using (var matchBrush = new SolidBrush(Color.FromArgb(254, 249, 195)))
                        {
                            g.FillRectangle(matchBrush, rRect);
                        }
                    }
                    else if (i % 2 == 1)
                    {
                        g.FillRectangle(zebraBrush, rRect);
                    }
                    else
                    {
                        g.FillRectangle(whiteBrush, rRect);
                    }

                    // Linha inferior de grade
                    g.DrawLine(gridLinePen, rRect.X, rRect.Bottom, rRect.Right, rRect.Bottom);

                    // Coluna de Número (#)
                    var numRect = new RectangleF(rRect.X, rRect.Y, rowNumColW, rowH);
                    if (isHighlighted)
                    {
                        // Marcador ▶
                        PointF[] arrow = new PointF[]
                        {
                            new PointF(numRect.X + 4, numRect.Y + (rowH / 2f) - 4),
                            new PointF(numRect.X + 9, numRect.Y + (rowH / 2f)),
                            new PointF(numRect.X + 4, numRect.Y + (rowH / 2f) + 4)
                        };
                        g.FillPolygon(markerBrush, arrow);
                        g.DrawString(globalR.ToString(), boldFont, hlTextBrush, numRect, sfIndex);
                    }
                    else
                    {
                        g.DrawString(globalR.ToString(), regularFont, mutedBrush, numRect, sfIndex);
                    }

                    // Colunas de Dados da Linha
                    var rowData = AllRows[globalR];
                    for (int c = 0; c < TotalColumnCount; c++)
                    {
                        float cx = rRect.X + rowNumColW + (c * colW);
                        var cellRect = new RectangleF(cx + 6, rRect.Y, colW - 12, rowH);

                        string cellVal = (c < rowData.Count) ? rowData[c] : "";
                        var curBrush = isHighlighted ? hlTextBrush : textBrush;
                        var curFont = isHighlighted ? boldFont : regularFont;

                        g.DrawString(cellVal, curFont, curBrush, cellRect, sfData);

                        // Divisor vertical
                        g.DrawLine(divLinePen, cx, rRect.Y, cx, rRect.Bottom);
                    }

                    curY += rowH;
                }
            }

            // 5. Rodapé Informativo e Barra de Navegação
            float footerH = hiRes ? 30 : 24;
            var footerRect = new RectangleF(tableRect.X, tableRect.Bottom - footerH, tableRect.Width, footerH);
            using (var footBrush = new SolidBrush(Color.FromArgb(241, 245, 249)))
            using (var footLine = new Pen(Color.FromArgb(203, 213, 225), 1f))
            {
                g.FillRectangle(footBrush, footerRect);
                g.DrawLine(footLine, footerRect.X, footerRect.Y, footerRect.Right, footerRect.Y);
            }

            // Texto de Status do Rodapé
            using (var footFont = new Font("Segoe UI", hiRes ? 8f : 6.8f, FontStyle.Regular))
            using (var footTextBrush = new SolidBrush(Color.FromArgb(71, 85, 105)))
            {
                int maxPages = Math.Max(1, (int)Math.Ceiling(TotalRowCount / (double)PageSize));
                string pageInfo = $"Página {CurrentPage + 1} de {maxPages}  ({TotalRowCount} linhas totais)";
                g.DrawString(pageInfo, footFont, footTextBrush, footerRect.X + 10, footerRect.Y + (footerH - footFont.Height) / 2f);
            }
        }

        #endregion

        #region Menu de Contexto e Serialização

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var copyItem = new ToolStripMenuItem("Copiar Tabela Visível (CSV para Área de Transferência)");
            copyItem.Click += (s, e) =>
            {
                CopyVisibleCsvToClipboard();
                Message = "Copiado!";
            };
            menu.Items.Add(copyItem);

            var saveCsvItem = new ToolStripMenuItem("Salvar Tabela Completa como CSV...");
            saveCsvItem.Click += (s, e) => SaveCsvDialog();
            menu.Items.Add(saveCsvItem);

            var savePngItem = new ToolStripMenuItem("Salvar Imagem da Tabela (PNG Alta Resolução)...");
            savePngItem.Click += (s, e) => SavePngDialog();
            menu.Items.Add(savePngItem);

            menu.Items.Add(new ToolStripSeparator());

            var modeMenu = new ToolStripMenuItem("Modo de Interpretação dos Ramos (Tree Mode):");
            var modes = new (int mode, string label)[]
            {
                (0, "0: Ramos como Colunas (Columns Mode - Padrão)"),
                (1, "1: Ramos como Linhas (Rows Mode)"),
                (2, "2: Lista Plana (Single Column)")
            };

            foreach (var m in modes)
            {
                var item = new ToolStripMenuItem(m.label)
                {
                    Checked = (TableMode == m.mode)
                };
                int target = m.mode;
                item.Click += (s, e) =>
                {
                    RecordUndoEvent("Mudar Modo da Tabela");
                    TableMode = target;
                    ExpireSolution(true);
                };
                modeMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(modeMenu);

            if (HighlightedIndex >= 0)
            {
                var clearSelItem = new ToolStripMenuItem("Limpar Linha Selecionada");
                clearSelItem.Click += (s, e) =>
                {
                    RecordUndoEvent("Limpar Seleção");
                    HighlightedIndex = -1;
                    ExpireSolution(true);
                };
                menu.Items.Add(clearSelItem);
            }
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetInt32("HighlightedIndex", HighlightedIndex);
            writer.SetInt32("CurrentPage", CurrentPage);
            writer.SetInt32("PageSize", PageSize);
            writer.SetInt32("TableMode", TableMode);
            writer.SetString("DisplayTitle", DisplayTitle ?? "");
            writer.SetInt32("VisualWidth", VisualWidth);
            writer.SetInt32("RowHeight", RowHeight);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("HighlightedIndex")) HighlightedIndex = reader.GetInt32("HighlightedIndex");
            if (reader.ItemExists("CurrentPage")) CurrentPage = reader.GetInt32("CurrentPage");
            if (reader.ItemExists("PageSize")) PageSize = reader.GetInt32("PageSize");
            if (reader.ItemExists("TableMode")) TableMode = reader.GetInt32("TableMode");
            if (reader.ItemExists("DisplayTitle")) DisplayTitle = reader.GetString("DisplayTitle");
            if (reader.ItemExists("VisualWidth")) VisualWidth = reader.GetInt32("VisualWidth");
            if (reader.ItemExists("RowHeight")) RowHeight = reader.GetInt32("RowHeight");
            return base.Read(reader);
        }

        #endregion
    }

    /// <summary>
    /// Atributos gráficos no Canvas para o DataTableVisualizer_Component.
    /// Gerencia a expansão de layout, clique direto nas linhas para seleção e botões de navegação.
    /// </summary>
    public class DataTableVisualizer_Attributes : GH_ComponentAttributes
    {
        private RectangleF m_tableRect;
        private RectangleF m_btnPrevRect;
        private RectangleF m_btnNextRect;
        private RectangleF m_btnCopyRect;
        private RectangleF m_btnExportRect;

        public DataTableVisualizer_Attributes(DataTableVisualizer_Component owner) : base(owner)
        {
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            var comp = Owner as DataTableVisualizer_Component;
            if (comp == null) return base.RespondToMouseDown(sender, e);

            if (e.Button == MouseButtons.Left)
            {
                // 1. Botão Página Anterior [◀ Ant]
                if (m_btnPrevRect.Contains(e.CanvasLocation))
                {
                    comp.NavigatePreviousPage();
                    sender.Refresh();
                    return GH_ObjectResponse.Handled;
                }

                // 2. Botão Próxima Página [Próx ▶]
                if (m_btnNextRect.Contains(e.CanvasLocation))
                {
                    comp.NavigateNextPage();
                    sender.Refresh();
                    return GH_ObjectResponse.Handled;
                }

                // 3. Botão Copiar CSV [📋 CSV]
                if (m_btnCopyRect.Contains(e.CanvasLocation))
                {
                    comp.CopyVisibleCsvToClipboard();
                    comp.Message = "Copiado!";
                    sender.Refresh();
                    return GH_ObjectResponse.Handled;
                }

                // 4. Botão Salvar PNG [💾 PNG]
                if (m_btnExportRect.Contains(e.CanvasLocation))
                {
                    comp.SavePngDialog();
                    sender.Refresh();
                    return GH_ObjectResponse.Handled;
                }

                // 5. Clique em uma linha da tabela para selecionar/destacar
                float cardHeaderH = 26;
                float colHeaderH = 22;
                float rowH = comp.RowHeight;
                float rowsStartY = m_tableRect.Y + cardHeaderH + colHeaderH;
                float footerY = m_tableRect.Bottom - 24;

                if (e.CanvasLocation.X >= m_tableRect.X && e.CanvasLocation.X <= m_tableRect.Right &&
                    e.CanvasLocation.Y >= rowsStartY && e.CanvasLocation.Y < footerY)
                {
                    float relativeY = e.CanvasLocation.Y - rowsStartY;
                    int rowIndexOnScreen = (int)(relativeY / rowH);
                    int globalIndex = comp.StartIndex + rowIndexOnScreen;

                    if (globalIndex >= 0 && globalIndex < comp.TotalRowCount && globalIndex <= comp.EndIndex)
                    {
                        comp.SelectRow(globalIndex);
                        sender.Refresh();
                        return GH_ObjectResponse.Handled;
                    }
                }
            }

            return base.RespondToMouseDown(sender, e);
        }

        protected override void Layout()
        {
            base.Layout();

            var comp = Owner as DataTableVisualizer_Component;
            int desiredW = (comp != null) ? comp.VisualWidth : 520;
            int rowH = (comp != null) ? comp.RowHeight : 22;
            int visCount = (comp != null) ? Math.Max(1, Math.Min(30, comp.EndIndex - comp.StartIndex + 1)) : 10;

            // Alturas das seções
            int cardHeaderH = 26;
            int colHeaderH = 22;
            int rowsH = visCount * rowH;
            int footerH = 24;
            int tableTotalH = cardHeaderH + colHeaderH + rowsH + footerH;

            float oldRight = Bounds.Right;
            RectangleF b = Bounds;
            b.Width = Math.Max(b.Width, desiredW + 24);
            b.Height += tableTotalH + 16;
            Bounds = b;

            // Alinhar outputs na borda direita expandida
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
            if (channel == GH_CanvasChannel.Objects)
            {
                var savedPivot = Pivot;
                Pivot = new PointF(Bounds.X + Bounds.Width / 2f, savedPivot.Y);
                base.Render(canvas, graphics, channel);
                Pivot = savedPivot;
            }
            else
            {
                base.Render(canvas, graphics, channel);
            }

            if (channel == GH_CanvasChannel.Objects)
            {
                var comp = Owner as DataTableVisualizer_Component;
                if (comp == null) return;

                int rowH = comp.RowHeight;
                int visCount = Math.Max(1, Math.Min(30, comp.EndIndex - comp.StartIndex + 1));
                int cardHeaderH = 26;
                int colHeaderH = 22;
                int rowsH = visCount * rowH;
                int footerH = 24;
                int tableTotalH = cardHeaderH + colHeaderH + rowsH + footerH;

                RectangleF b = Bounds;
                m_tableRect = new RectangleF(b.X + 12, b.Bottom - tableTotalH - 8, b.Width - 24, tableTotalH);

                // Renderiza a tabela completa
                comp.RenderTableGraphics(graphics, m_tableRect, false);

                // Renderiza os botões de ação e paginação no rodapé
                float btnW = 58;
                float btnSmallW = 46;
                float btnH = 17;
                float btnY = m_tableRect.Bottom - btnH - 3.5f;

                // Botão Salvar PNG [💾 PNG]
                m_btnExportRect = new RectangleF(m_tableRect.Right - btnSmallW - 6, btnY, btnSmallW, btnH);
                // Botão Copiar CSV [📋 CSV]
                m_btnCopyRect = new RectangleF(m_btnExportRect.X - btnSmallW - 4, btnY, btnSmallW, btnH);
                // Botão Próximo [Próx ▶]
                m_btnNextRect = new RectangleF(m_btnCopyRect.X - btnW - 6, btnY, btnW, btnH);
                // Botão Anterior [◀ Ant]
                m_btnPrevRect = new RectangleF(m_btnNextRect.X - btnW - 4, btnY, btnW, btnH);

                RenderButton(graphics, m_btnPrevRect, "◀ Ant", Color.FromArgb(51, 65, 85), Color.FromArgb(71, 85, 105));
                RenderButton(graphics, m_btnNextRect, "Próx ▶", Color.FromArgb(51, 65, 85), Color.FromArgb(71, 85, 105));
                RenderButton(graphics, m_btnCopyRect, "📋 CSV", Color.FromArgb(14, 116, 144), Color.FromArgb(8, 145, 178));
                RenderButton(graphics, m_btnExportRect, "💾 PNG", Color.FromArgb(2, 132, 199), Color.FromArgb(3, 105, 161));
            }
        }

        private static void RenderButton(Graphics g, RectangleF rect, string text, Color bgColor, Color borderColor)
        {
            using (var btnBg = new SolidBrush(bgColor))
            using (var border = new Pen(borderColor, 1f))
            using (var font = new Font("Segoe UI", 6.8f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.White))
            {
                g.FillRectangle(btnBg, rect);
                g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(text, font, textBrush, rect, sf);
            }
        }
    }
}
