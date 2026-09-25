// HierarchicalClusterGraph_Component.cs
// Componente para visualização de Agrupamento Hierárquico com Paridade de Risco/Peso (HRP-style)
// Exibe: Dendrograma (linkage tree) no topo, Mapa de Calor de Correlação reordenado no centro,
//        Barras de Peso por grupo na direita e legenda de cor na base.
// Ideal para análise acústica: agrupa receptores por similaridade de RT60 / T60.
// Renderizado diretamente no Canvas do Grasshopper.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    // ─────────────────────────────────────────────────────────────────────────
    // Estruturas de dados internas
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Nó de linkage para o dendrograma (single / average / complete linkage).</summary>
    internal class LinkageNode
    {
        public int Left;    // índice do cluster esquerdo (< n → leaf; ≥ n → merge node)
        public int Right;   // índice do cluster direito
        public double Distance; // distância de fusão
        public int Size;    // total de folhas neste cluster
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Componente principal
    // ─────────────────────────────────────────────────────────────────────────

    public class HierarchicalClusterGraph_Component : GH_Component
    {
        // ── Paleta / Cores ─────────────────────────────────────────────────
        private static readonly Color BG           = Color.FromArgb(18, 22, 26);
        private static readonly Color PanelBG      = Color.FromArgb(26, 32, 38);
        private static readonly Color LimeGreen    = Color.FromArgb(140, 255, 80);
        private static readonly Color ClusterOutline = Color.FromArgb(120, 255, 70);
        private static readonly Color TextMain     = Color.FromArgb(220, 230, 235);
        private static readonly Color TextDim      = Color.FromArgb(130, 145, 160);
        private static readonly Color CyanAccent   = Color.FromArgb(0, 220, 255);
        private static readonly Color HeatLow      = Color.FromArgb(18, 48, 55);
        private static readonly Color HeatMid      = Color.FromArgb(30, 140, 145);
        private static readonly Color HeatHigh     = Color.FromArgb(90, 230, 215);

        // ── Cache para renderização ─────────────────────────────────────────
        internal double[,] DisplayMatrix;      // correlação reordenada [n×n]
        internal string[]  DisplayLabels;      // nomes dos nós (reordenados)
        internal List<LinkageNode> DisplayTree; // árvore de linkage
        internal int[]     DisplayOrder;       // permutação das folhas
        internal List<int[]> DisplayClusters;  // grupos: lista de índices originais
        internal double[]  DisplayWeights;     // pesos normalizados por receptor
        internal string    DisplayTitle  = "Hierarchical Cluster Graph";
        internal string    DisplaySubtitle = "SINGLE LINKAGE · CORRELATION MATRIX";
        internal string    DisplayCaption = "";
        internal double    CutHeight     = double.NaN; // altura do corte (NaN → auto)
        internal bool      ShowWeights   = true;
        internal Bitmap    CachedBmp;

        public HierarchicalClusterGraph_Component()
            : base(
                "Hierarchical Cluster Graph",
                "HCluster",
                "Visualiza agrupamento hierárquico (dendrograma + mapa de calor reordenado + barras de peso). " +
                "Ideal para agrupar receptores acústicos por similaridade de TR / T60.",
                "Glaux Tools",
                "Visual")
        { }

        public override Guid ComponentGuid =>
            new Guid("b3e74c21-9a1f-4d60-ae85-5c7f23d01b88");

        protected override System.Drawing.Bitmap Icon =>
            GlauxToolsIcons.HierarchicalCluster;

        // ── Entradas / Saídas ───────────────────────────────────────────────
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0 – Matriz de correlação/similaridade (lista plana NxN, row-major)
            pManager.AddNumberParameter("Correlation Matrix", "Matrix",
                "Matriz de correlação ou similaridade NxN fornecida como lista plana (row-major). " +
                "Valores de 0 a 1. Pode ser fornecida como DataTree com N branches de N valores cada.",
                GH_ParamAccess.list);
            pManager[0].Optional = false;

            // 1 – Número de colunas N (tamanho da matriz)
            pManager.AddIntegerParameter("N (Size)", "N",
                "Tamanho N da matriz quadrada NxN. Informe N quando a matriz chegar como lista plana.",
                GH_ParamAccess.item, -1);
            pManager[1].Optional = true;

            // 2 – Rótulos dos nós
            pManager.AddTextParameter("Labels", "Labels",
                "Nomes dos nós/receptores (lista de N strings). Opcional.",
                GH_ParamAccess.list);
            pManager[2].Optional = true;

            // 3 – Altura de corte para agrupamento
            pManager.AddNumberParameter("Cut Height", "Cut",
                "Altura de corte no dendrograma para definir os grupos. " +
                "Se não conectado, usa 60% da altura máxima.",
                GH_ParamAccess.item, double.NaN);
            pManager[3].Optional = true;

            // 4 – Método de linkage: 0=single, 1=average, 2=complete
            pManager.AddIntegerParameter("Linkage Method", "Method",
                "Método de linkage: 0=Single (default), 1=Average, 2=Complete.",
                GH_ParamAccess.item, 0);
            pManager[4].Optional = true;

            // 5 – Título do gráfico
            pManager.AddTextParameter("Title", "Title",
                "Título exibido no topo do gráfico.",
                GH_ParamAccess.item, "Hierarchical Cluster Graph");
            pManager[5].Optional = true;

            // 6 – Subtítulo
            pManager.AddTextParameter("Subtitle", "Sub",
                "Subtítulo (ex.: nome do método, frequência analisada).",
                GH_ParamAccess.item, "SINGLE LINKAGE · CORRELATION MATRIX");
            pManager[6].Optional = true;

            // 7 – Legenda / caption inferior
            pManager.AddTextParameter("Caption", "Caption",
                "Texto de legenda exibido na base do gráfico.",
                GH_ParamAccess.item, "");
            pManager[7].Optional = true;

            // 8 – Mostrar barras de peso?
            pManager.AddBooleanParameter("Show Weights", "Weights",
                "Se verdadeiro, exibe as barras de peso por receptor à direita.",
                GH_ParamAccess.item, true);
            pManager[8].Optional = true;

            // 9 – Caminho para exportar PNG
            pManager.AddTextParameter("Export Path", "PNG",
                "Caminho para exportar o gráfico como PNG. Deixe vazio para não exportar.",
                GH_ParamAccess.item, "");
            pManager[9].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Cluster Assignments", "Groups",
                "Índice de grupo para cada receptor (na ordem original de entrada).",
                GH_ParamAccess.list);

            pManager.AddNumberParameter("Weights", "W",
                "Peso normalizado por receptor (HRP-style: risco dividido igualmente entre grupos, " +
                "depois igualmente dentro de cada grupo).",
                GH_ParamAccess.list);

            pManager.AddIntegerParameter("Order", "Order",
                "Permutação das folhas após reordenamento hierárquico.",
                GH_ParamAccess.list);

            pManager.AddIntegerParameter("Group Count", "nGroups",
                "Número de grupos formados pelo corte.",
                GH_ParamAccess.item);
        }

        // ── SolveInstance ───────────────────────────────────────────────────
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Leitura de inputs
            var matList  = new List<double>();
            int nSize    = -1;
            var labels   = new List<string>();
            double cutH  = double.NaN;
            int method   = 0;
            string title = "Hierarchical Cluster Graph";
            string sub   = "SINGLE LINKAGE · CORRELATION MATRIX";
            string cap   = "";
            bool showW   = true;
            string exportPath = "";

            if (!DA.GetDataList(0, matList)) return;
            DA.GetData(1, ref nSize);
            DA.GetDataList(2, labels);
            DA.GetData(3, ref cutH);
            DA.GetData(4, ref method);
            DA.GetData(5, ref title);
            DA.GetData(6, ref sub);
            DA.GetData(7, ref cap);
            DA.GetData(8, ref showW);
            DA.GetData(9, ref exportPath);

            // ── Detectar N ────────────────────────────────────────────────
            int n;
            if (nSize > 0)
            {
                n = nSize;
            }
            else
            {
                n = (int)Math.Round(Math.Sqrt(matList.Count));
                if (n * n != matList.Count)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        $"A lista tem {matList.Count} valores, que não é um quadrado perfeito. " +
                        "Conecte N manualmente.");
                    return;
                }
            }

            if (matList.Count < n * n)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Esperados {n * n} valores para a matriz {n}×{n}, recebidos {matList.Count}.");
                return;
            }

            // ── Construir matriz double[n,n] ──────────────────────────────
            double[,] corr = new double[n, n];
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                    corr[r, c] = Math.Max(0, Math.Min(1, matList[r * n + c]));

            // ── Rótulos padrão ────────────────────────────────────────────
            if (labels.Count < n)
            {
                labels.Clear();
                for (int i = 0; i < n; i++) labels.Add($"R{i + 1:00}");
            }

            // ── Hierarquia (linkage) ──────────────────────────────────────
            // Converte correlação → distância: d = 1 − corr
            double[,] dist = new double[n, n];
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                    dist[r, c] = 1.0 - corr[r, c];

            List<LinkageNode> tree = ComputeLinkage(dist, n, method);

            // ── Ordem das folhas (reordenamento DFS) ──────────────────────
            int[] order = GetLeafOrder(tree, n);

            // ── Corte → grupos ────────────────────────────────────────────
            double maxDist = tree.Count > 0 ? tree[tree.Count - 1].Distance : 0.6;
            if (double.IsNaN(cutH) || cutH <= 0)
                cutH = 0.6 * maxDist;

            List<int[]> clusters = CutTree(tree, n, cutH);

            // ── Pesos HRP-style ───────────────────────────────────────────
            // Risco equalizado entre grupos; dentro de cada grupo igualmente.
            double[] weights = new double[n];
            int gc = clusters.Count;
            for (int g = 0; g < gc; g++)
            {
                double w = 1.0 / gc / clusters[g].Length;
                foreach (int idx in clusters[g])
                    weights[idx] = w;
            }

            // ── Reordenar matriz de correlação pela permutação ────────────
            double[,] corrReordered = new double[n, n];
            for (int ri = 0; ri < n; ri++)
                for (int ci = 0; ci < n; ci++)
                    corrReordered[ri, ci] = corr[order[ri], order[ci]];

            // ── Pesos reordenados (para exibir na mesma ordem do heatmap) ─
            double[] weightsReordered = new double[n];
            string[] labelsReordered  = new string[n];
            for (int i = 0; i < n; i++)
            {
                weightsReordered[i] = weights[order[i]];
                labelsReordered[i]  = labels[order[i]];
            }

            // ── Cluster assignments na ordem original ─────────────────────
            int[] assignments = new int[n];
            for (int g = 0; g < gc; g++)
                foreach (int idx in clusters[g])
                    assignments[idx] = g + 1;

            // ── Salvar para renderização ──────────────────────────────────
            DisplayMatrix   = corrReordered;
            DisplayLabels   = labelsReordered;
            DisplayTree     = tree;
            DisplayOrder    = order;
            DisplayClusters = clusters;
            DisplayWeights  = weightsReordered;
            DisplayTitle    = title;
            DisplaySubtitle = sub;
            DisplayCaption  = cap;
            CutHeight       = cutH;
            ShowWeights     = showW;
            CachedBmp       = null; // forçar re-render

            Grasshopper.Instances.InvalidateCanvas();

            // ── Outputs ───────────────────────────────────────────────────
            DA.SetDataList(0, assignments.Select(x => (object)(GH_Integer)new GH_Integer(x)));
            DA.SetDataList(1, weights.Select(x => (object)(GH_Number)new GH_Number(x)));
            DA.SetDataList(2, order.Select(x => (object)(GH_Integer)new GH_Integer(x)));
            DA.SetData(3, gc);

            // ── Exportar PNG ──────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(exportPath))
            {
                try
                {
                    Bitmap bmp = RenderToBitmap(1200, 1050);
                    bmp.Save(exportPath, ImageFormat.Png);
                    bmp.Dispose();
                }
                catch (Exception ex)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Falha ao exportar PNG: {ex.Message}");
                }
            }
        }

        // ── Attributes (custom canvas render) ──────────────────────────────
        public override void CreateAttributes()
        {
            m_attributes = new HierarchicalClusterGraph_Attributes(this);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Algoritmos de clustering
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Computa linkage hierárquico (SLINK / UPGMA / CLINK).</summary>
        private static List<LinkageNode> ComputeLinkage(double[,] dist, int n, int method)
        {
            // dist matrix mutável entre clusters ativos
            // Algoritmo O(n²) naive - suficiente para n ≤ 200
            var clusterMembers = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++) clusterMembers[i] = new List<int> { i };

            // d[a,b] = distância entre cluster a e cluster b
            // Usamos dicionário de pares para evitar resize
            var d = new Dictionary<(int, int), double>();
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    d[(i, j)] = dist[i, j];

            var nodes = new List<LinkageNode>();
            var active = new HashSet<int>(Enumerable.Range(0, n));
            int nextId = n;

            while (active.Count > 1)
            {
                // Encontrar par com distância mínima
                double minD = double.MaxValue;
                int bestA = -1, bestB = -1;
                var aList = active.ToArray();
                for (int ai = 0; ai < aList.Length; ai++)
                    for (int bi = ai + 1; bi < aList.Length; bi++)
                    {
                        int a = aList[ai], b = aList[bi];
                        int ka = Math.Min(a, b), kb = Math.Max(a, b);
                        if (d.TryGetValue((ka, kb), out double dd) && dd < minD)
                        {
                            minD = dd; bestA = a; bestB = b;
                        }
                    }

                // Criar novo nó
                var membersA = clusterMembers[bestA];
                var membersB = clusterMembers[bestB];
                var merged   = membersA.Concat(membersB).ToList();

                nodes.Add(new LinkageNode
                {
                    Left     = bestA,
                    Right    = bestB,
                    Distance = minD,
                    Size     = merged.Count
                });

                // Atualizar distâncias para o novo cluster
                int newId = nextId++;
                active.Remove(bestA);
                active.Remove(bestB);
                active.Add(newId);
                clusterMembers[newId] = merged;

                foreach (int other in active)
                {
                    if (other == newId) continue;
                    int ka = Math.Min(bestA, other), kb = Math.Max(bestA, other);
                    int kc = Math.Min(bestB, other), kd = Math.Max(bestB, other);
                    double dA = d.TryGetValue((ka, kb), out double da) ? da : double.MaxValue;
                    double dB = d.TryGetValue((kc, kd), out double db) ? db : double.MaxValue;

                    double newDist;
                    switch (method)
                    {
                        case 2: newDist = Math.Max(dA, dB); break; // complete
                        case 1: // average (UPGMA)
                            newDist = (dA * membersA.Count + dB * membersB.Count) / merged.Count;
                            break;
                        default: newDist = Math.Min(dA, dB); break; // single
                    }

                    int k1 = Math.Min(newId, other), k2 = Math.Max(newId, other);
                    d[(k1, k2)] = newDist;
                }
            }

            return nodes;
        }

        /// <summary>Percorre a árvore em DFS para obter a ordem das folhas.</summary>
        private static int[] GetLeafOrder(List<LinkageNode> tree, int n)
        {
            if (tree.Count == 0)
                return Enumerable.Range(0, n).ToArray();

            var result = new List<int>();
            void DFS(int id)
            {
                if (id < n) { result.Add(id); return; }
                var node = tree[id - n];
                DFS(node.Left);
                DFS(node.Right);
            }
            DFS(n + tree.Count - 1);
            return result.ToArray();
        }

        /// <summary>Corta a árvore na altura dada e retorna grupos de índices de folhas originais.</summary>
        private static List<int[]> CutTree(List<LinkageNode> tree, int n, double cutHeight)
        {
            if (tree.Count == 0)
                return Enumerable.Range(0, n).Select(i => new[] { i }).ToList();

            var groups = new List<int[]>();

            void Collect(int id)
            {
                if (id < n) { groups.Add(new[] { id }); return; }
                var node = tree[id - n];
                if (node.Distance >= cutHeight)
                {
                    // Cortar aqui: descer nos filhos
                    Collect(node.Left);
                    Collect(node.Right);
                }
                else
                {
                    // Este cluster inteiro ficou abaixo do corte → coleta todas as folhas
                    var leaves = new List<int>();
                    CollectLeaves(id, leaves, tree, n);
                    groups.Add(leaves.ToArray());
                }
            }

            Collect(n + tree.Count - 1);
            return groups;
        }

        private static void CollectLeaves(int id, List<int> leaves, List<LinkageNode> tree, int n)
        {
            if (id < n) { leaves.Add(id); return; }
            var node = tree[id - n];
            CollectLeaves(node.Left,  leaves, tree, n);
            CollectLeaves(node.Right, leaves, tree, n);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Renderização PNG (exportação)
        // ══════════════════════════════════════════════════════════════════

        internal Bitmap RenderToBitmap(int width, int height)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode     = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                var bounds = new RectangleF(0, 0, width, height);
                DrawChart(g, bounds, 1f);
            }
            return bmp;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Rotina de desenho central (usada tanto no Canvas quanto no PNG)
        // ══════════════════════════════════════════════════════════════════

        internal void DrawChart(Graphics g, RectangleF bounds, float scale)
        {
            if (DisplayMatrix == null) return;

            int n = DisplayMatrix.GetLength(0);
            float W = bounds.Width;
            float H = bounds.Height;
            float X = bounds.X;
            float Y = bounds.Y;

            // ── Fundo ─────────────────────────────────────────────────────
            using (var bgBrush = new SolidBrush(BG))
                g.FillRectangle(bgBrush, bounds);

            // ── Layout (proporcional) ─────────────────────────────────────
            float pad        = 12f * scale;
            float titleH     = 52f * scale;
            float statsH     = 22f * scale;
            float dendroH    = (H - titleH - statsH - 60f * scale) * 0.30f;
            float heatH      = (H - titleH - statsH - 60f * scale) * 0.58f;
            float legendH    = 28f * scale;
            float capH       = (DisplayCaption.Length > 0) ? 36f * scale : 0f;

            float labelW     = 38f * scale;
            float weightBarW = ShowWeights ? 80f * scale : 0f;
            float rightPad   = 8f * scale;

            float plotW = W - labelW - weightBarW - rightPad - 2 * pad;

            float topY       = Y + pad;
            float titleY     = topY;
            float statsY     = titleY + titleH;
            float dendroY    = statsY + statsH;
            float heatY      = dendroY + dendroH;
            float legendY    = heatY + heatH + 6f * scale;
            float capY       = legendY + legendH + 4f * scale;

            float plotX = X + pad + labelW;

            // ── Título ────────────────────────────────────────────────────
            DrawTitle(g, new RectangleF(X + pad, titleY, W - 2 * pad, titleH), scale);

            // ── Estatísticas topo ─────────────────────────────────────────
            DrawStats(g, new RectangleF(X + pad, statsY, W - 2 * pad, statsH), scale);

            // ── Dendrograma ───────────────────────────────────────────────
            var dendroRect = new RectangleF(plotX, dendroY + 4f * scale, plotW, dendroH - 4f * scale);
            DrawDendrogram(g, dendroRect, scale);

            // ── Heatmap ───────────────────────────────────────────────────
            var heatRect = new RectangleF(plotX, heatY, plotW, heatH);
            DrawHeatmap(g, heatRect, labelW, scale, plotX, plotW, heatY, heatH, weightBarW, rightPad);

            // ── Legenda de cor ────────────────────────────────────────────
            float legendX = plotX + plotW * 0.1f;
            float legendW2 = plotW * 0.5f;
            DrawColorLegend(g, new RectangleF(legendX, legendY, legendW2, legendH), scale);

            // ── Caption ───────────────────────────────────────────────────
            if (DisplayCaption.Length > 0)
            {
                using (var f = new Font("Courier New", 8f * scale, FontStyle.Regular))
                using (var brush = new SolidBrush(TextDim))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(DisplayCaption, f, brush, new RectangleF(X + pad, capY, W - 2 * pad, capH), sf);
            }
        }

        private void DrawTitle(Graphics g, RectangleF rect, float scale)
        {
            float midY = rect.Y + rect.Height * 0.38f;
            float subY = rect.Y + rect.Height * 0.70f;

            using (var f = new Font("Arial", 14f * scale, FontStyle.Bold))
            using (var b = new SolidBrush(TextMain))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(DisplayTitle, f, b, new RectangleF(rect.X, rect.Y, rect.Width, rect.Height * 0.55f), sf);

            using (var f = new Font("Courier New", 7f * scale, FontStyle.Regular))
            using (var b = new SolidBrush(TextDim))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(DisplaySubtitle, f, b, new RectangleF(rect.X, subY - 4f * scale, rect.Width, rect.Height * 0.35f), sf);
        }

        private void DrawStats(Graphics g, RectangleF rect, float scale)
        {
            if (DisplayClusters == null || DisplayClusters.Count == 0) return;
            int n  = DisplayMatrix.GetLength(0);
            int gc = DisplayClusters.Count;
            int biggest = DisplayClusters.Max(c => c.Length);
            int pct = (int)Math.Round(100.0 * biggest / n);

            // Esquerda: "XX% IN THE BIGGEST GROUP"
            string leftTxt = $"{pct}%  IN THE BIGGEST GROUP";
            using (var fl = new Font("Courier New", 7f * scale, FontStyle.Bold))
            using (var bl = new SolidBrush(LimeGreen))
                g.DrawString(leftTxt, fl, bl, rect.X, rect.Y + 2f * scale);

            // Direita: "HRP" ou subtítulo breve
            string rightTxt = $"{gc} GROUPS";
            using (var fr = new Font("Courier New", 7f * scale, FontStyle.Bold))
            using (var br = new SolidBrush(LimeGreen))
            using (var sf = new StringFormat { Alignment = StringAlignment.Far })
                g.DrawString(rightTxt, fr, br, rect, sf);
        }

        // ── Dendrograma ──────────────────────────────────────────────────────

        private void DrawDendrogram(Graphics g, RectangleF rect, float scale)
        {
            if (DisplayTree == null || DisplayTree.Count == 0 || DisplayOrder == null) return;

            int n = DisplayOrder.Length;

            // Altura máxima no dendrograma
            double maxH = DisplayTree.Max(nd => nd.Distance);
            if (maxH <= 0) maxH = 1;

            // Posição X de cada folha (em ordem de DisplayOrder)
            // A célula i do heatmap corresponde ao i-ésimo elemento do DisplayOrder.
            float cellW = rect.Width / n;

            // Mapeamento: id de nó → posição X central e Y de topo
            // Folhas: posição X = (índice na permutação + 0.5) * cellW
            // Nós internos: posição X = média dos filhos
            var posX = new Dictionary<int, float>();
            var posY = new Dictionary<int, float>();

            // Índice de cada folha original na permutação
            var leafPos = new Dictionary<int, int>();
            for (int i = 0; i < n; i++)
                leafPos[DisplayOrder[i]] = i;

            for (int i = 0; i < n; i++)
            {
                int leafId = DisplayOrder[i];
                posX[leafId] = rect.X + (i + 0.5f) * cellW;
                posY[leafId] = rect.Bottom;
            }

            // Calcular posições dos nós internos (bottom-up)
            for (int ni = 0; ni < DisplayTree.Count; ni++)
            {
                int nodeId = n + ni;
                var node = DisplayTree[ni];
                float xL = posX[node.Left];
                float xR = posX[node.Right];
                float cx = (xL + xR) * 0.5f;
                float cy = rect.Bottom - (float)(node.Distance / maxH) * rect.Height;
                posX[nodeId] = cx;
                posY[nodeId] = cy;
            }

            // Linha de corte
            float cutY = rect.Bottom - (float)(CutHeight / maxH) * rect.Height;

            // Desenhar arestas
            using (var pen = new Pen(Color.FromArgb(180, 200, 215), 1.2f * scale))
            using (var penCut = new Pen(LimeGreen, 1.0f * scale) { DashStyle = DashStyle.Dash })
            {
                // Linhas de fusão
                for (int ni = 0; ni < DisplayTree.Count; ni++)
                {
                    int nodeId = n + ni;
                    var node = DisplayTree[ni];
                    float xL = posX[node.Left];  float yL = posY[node.Left];
                    float xR = posX[node.Right]; float yR = posY[node.Right];
                    float cx = posX[nodeId];     float cy = posY[nodeId];

                    // Linha vertical esquerda
                    g.DrawLine(pen, xL, yL, xL, cy);
                    // Linha vertical direita
                    g.DrawLine(pen, xR, yR, xR, cy);
                    // Linha horizontal
                    g.DrawLine(pen, xL, cy, xR, cy);
                }

                // Linha de corte horizontal
                g.DrawLine(penCut, rect.X, cutY, rect.Right, cutY);
                // Label "CUT"
                using (var fc = new Font("Courier New", 6f * scale, FontStyle.Bold))
                using (var bc = new SolidBrush(LimeGreen))
                    g.DrawString("CUT", fc, bc, rect.Right + 2f * scale, cutY - 6f * scale);
            }

            // Eixo Y com escala de distância
            using (var fy = new Font("Arial", 5.5f * scale, FontStyle.Regular))
            using (var by = new SolidBrush(TextDim))
            using (var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                int ticks = 4;
                for (int t = 0; t <= ticks; t++)
                {
                    float frac = t / (float)ticks;
                    float ty   = rect.Bottom - frac * rect.Height;
                    string lbl = (maxH * frac).ToString("0.00");
                    g.DrawString(lbl, fy, by, new RectangleF(rect.X - 30f * scale, ty - 6f * scale, 28f * scale, 12f * scale), sf);
                }
            }

            // Label "LINKAGE DISTANCE" na esquerda
            using (var fl = new Font("Courier New", 5.5f * scale, FontStyle.Regular))
            using (var bl = new SolidBrush(TextDim))
            {
                var m = g.MeasureString("LINKAGE DISTANCE", fl);
                var state = g.Save();
                g.TranslateTransform(rect.X - 36f * scale, rect.Y + rect.Height * 0.5f);
                g.RotateTransform(-90);
                g.DrawString("LINKAGE DISTANCE", fl, bl, -m.Width / 2f, -m.Height / 2f);
                g.Restore(state);
            }
        }

        // ── Heatmap ──────────────────────────────────────────────────────────

        private void DrawHeatmap(Graphics g, RectangleF rect,
            float labelW, float scale,
            float plotX, float plotW, float heatY, float heatH,
            float weightBarW, float rightPad)
        {
            if (DisplayMatrix == null) return;
            int n = DisplayMatrix.GetLength(0);
            float cellW = plotW / n;
            float cellH = heatH / n;

            // ── Células do heatmap ───────────────────────────────────────
            for (int row = 0; row < n; row++)
            {
                for (int col = 0; col < n; col++)
                {
                    double v   = DisplayMatrix[row, col];
                    Color cell = LerpColor(HeatLow, HeatMid, HeatHigh, v);
                    float cx = plotX + col * cellW;
                    float cy = heatY + row * cellH;
                    using (var br = new SolidBrush(cell))
                        g.FillRectangle(br, cx, cy, cellW, cellH);
                }
            }

            // ── Contornos de cluster ─────────────────────────────────────
            if (DisplayClusters != null)
            {
                // Mapear folha original → posição na ordem reordenada
                var orderMap = new Dictionary<int, int>();
                for (int i = 0; i < DisplayOrder.Length; i++)
                    orderMap[DisplayOrder[i]] = i;

                using (var pen = new Pen(ClusterOutline, 2f * scale))
                {
                    foreach (var clust in DisplayClusters)
                    {
                        if (clust.Length == 0) continue;
                        // Pegar índices reordenados
                        int[] reordered = clust.Select(idx => orderMap.TryGetValue(idx, out int pos) ? pos : idx).OrderBy(x => x).ToArray();
                        int rMin = reordered.Min();
                        int rMax = reordered.Max();

                        float rx = plotX + rMin * cellW;
                        float ry = heatY + rMin * cellH;
                        float rw = (rMax - rMin + 1) * cellW;
                        float rh = (rMax - rMin + 1) * cellH;
                        g.DrawRectangle(pen, rx, ry, rw, rh);
                    }
                }
            }

            // ── Rótulos Y (esquerda) ─────────────────────────────────────
            using (var fl = new Font("Courier New", 5.5f * scale, FontStyle.Regular))
            using (var bl = new SolidBrush(TextMain))
            using (var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                for (int row = 0; row < n; row++)
                {
                    float cy = heatY + (row + 0.5f) * cellH;
                    g.DrawString(DisplayLabels[row], fl, bl,
                        new RectangleF(plotX - labelW, cy - cellH * 0.5f, labelW - 2f * scale, cellH), sf);
                }
            }

            // ── Rótulos X (embaixo do heatmap) ───────────────────────────
            using (var fx = new Font("Courier New", 5.5f * scale, FontStyle.Regular))
            using (var bx = new SolidBrush(TextDim))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
            {
                for (int col = 0; col < n; col++)
                {
                    float cx = plotX + (col + 0.5f) * cellW;
                    float textY = heatY + heatH + 2f * scale;
                    // Vertical text para labels longas
                    var m = g.MeasureString(DisplayLabels[col], fx);
                    var state = g.Save();
                    g.TranslateTransform(cx, textY + m.Width * 0.5f);
                    g.RotateTransform(-90);
                    g.DrawString(DisplayLabels[col], fx, bx, -m.Width * 0.5f, -m.Height * 0.5f);
                    g.Restore(state);
                }
            }

            // ── Barras de peso (direita) ─────────────────────────────────
            if (ShowWeights && DisplayWeights != null && weightBarW > 0)
            {
                double maxW = DisplayWeights.Max();
                float barAreaX = plotX + plotW + 4f * scale;
                float barMaxW  = weightBarW - 10f * scale;

                // "WEIGHT" label no topo
                using (var fw = new Font("Courier New", 5.5f * scale, FontStyle.Regular))
                using (var bw = new SolidBrush(TextDim))
                using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Far })
                    g.DrawString("WEIGHT", fw, bw, barAreaX, heatY - 12f * scale);

                for (int row = 0; row < n; row++)
                {
                    double w   = maxW > 0 ? DisplayWeights[row] / maxW : 0;
                    float bw2  = (float)(w * barMaxW);
                    float cy   = heatY + row * cellH + cellH * 0.15f;
                    float bh   = cellH * 0.70f;

                    using (var brBar = new SolidBrush(LimeGreen))
                        g.FillRectangle(brBar, barAreaX, cy, Math.Max(1f, bw2), bh);
                }
            }
        }

        private void DrawColorLegend(Graphics g, RectangleF rect, float scale)
        {
            // Gradiente de cor
            int steps = 100;
            float sw = rect.Width / steps;
            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)(steps - 1);
                Color c = LerpColor(HeatLow, HeatMid, HeatHigh, t);
                using (var br = new SolidBrush(c))
                    g.FillRectangle(br, rect.X + s * sw, rect.Y + 6f * scale, sw + 1, rect.Height - 18f * scale);
            }

            // Contorno
            using (var pen = new Pen(TextDim, 0.5f * scale))
                g.DrawRectangle(pen, rect.X, rect.Y + 6f * scale, rect.Width, rect.Height - 18f * scale);

            // Rótulos
            using (var fl = new Font("Courier New", 5.5f * scale, FontStyle.Regular))
            using (var bl = new SolidBrush(TextDim))
            {
                g.DrawString("CORRELATION", fl, bl, rect.X - 52f * scale, rect.Y + rect.Height * 0.2f);
                g.DrawString("0",           fl, bl, rect.X,                rect.Bottom - 14f * scale);
                g.DrawString("+1",          fl, bl, rect.Right - 12f * scale, rect.Bottom - 14f * scale);
            }
        }

        // ── Utilitário de cor ────────────────────────────────────────────────
        private static Color LerpColor(Color a, Color b, Color c, double t)
        {
            // t in [0,1]: a→b for t in [0,0.5], b→c for t in [0.5,1]
            t = Math.Max(0, Math.Min(1, t));
            Color from, to;
            float f;
            if (t <= 0.5)
            {
                from = a; to = b; f = (float)(t / 0.5);
            }
            else
            {
                from = b; to = c; f = (float)((t - 0.5) / 0.5);
            }
            int r = (int)(from.R + (to.R - from.R) * f);
            int gv = (int)(from.G + (to.G - from.G) * f);
            int bv = (int)(from.B + (to.B - from.B) * f);
            return Color.FromArgb(
                Math.Max(0, Math.Min(255, r)),
                Math.Max(0, Math.Min(255, gv)),
                Math.Max(0, Math.Min(255, bv)));
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Atributos de Canvas (renderização interativa no GH)
    // ══════════════════════════════════════════════════════════════════════════

    public class HierarchicalClusterGraph_Attributes : GH_ComponentAttributes
    {
        private const float GRAPH_WIDTH = 540f;
        private const float GRAPH_HEIGHT = 460f;

        public HierarchicalClusterGraph_Attributes(HierarchicalClusterGraph_Component owner)
            : base(owner) { }

        protected override void Layout()
        {
            base.Layout();
            float oldRight = Bounds.Right;
            RectangleF b = Bounds;
            b.Width = Math.Max(b.Width, GRAPH_WIDTH + 24f);
            b.Height += GRAPH_HEIGHT + 18f;
            Bounds = b;

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
                var _savedPivot = Pivot;
                Pivot = new PointF(Bounds.X + Bounds.Width / 2f, _savedPivot.Y);
                base.Render(canvas, graphics, channel);
                Pivot = _savedPivot;

                var comp = Owner as HierarchicalClusterGraph_Component;
                if (comp == null) return;

                RectangleF b = Bounds;
                RectangleF graphRect = new RectangleF(b.X + 12f, b.Bottom - GRAPH_HEIGHT - 10f, b.Width - 24f, GRAPH_HEIGHT);

                // Painel escuro do gráfico
                using (var bgBrush = new SolidBrush(Color.FromArgb(18, 22, 26)))
                {
                    graphics.FillRectangle(bgBrush, graphRect);
                }
                using (var borderPen = new Pen(Color.FromArgb(60, 80, 90), 1f))
                {
                    graphics.DrawRectangle(borderPen, graphRect.X, graphRect.Y, graphRect.Width, graphRect.Height);
                }

                // Placeholder quando não há matriz de correlação conectada
                if (comp.DisplayMatrix == null)
                {
                    string msg1 = "Hierarchical Cluster Graph";
                    string msg2 = "Conecte a Correlation Matrix para visualizar.";
                    using (var f1 = new Font("Arial", 11f, FontStyle.Bold))
                    using (var f2 = new Font("Courier New", 7.5f, FontStyle.Regular))
                    using (var b1 = new SolidBrush(Color.FromArgb(180, 195, 210)))
                    using (var b2 = new SolidBrush(Color.FromArgb(100, 120, 135)))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        var r1 = new RectangleF(graphRect.X, graphRect.Y + graphRect.Height * 0.40f, graphRect.Width, 22f);
                        var r2 = new RectangleF(graphRect.X, graphRect.Y + graphRect.Height * 0.55f, graphRect.Width, 18f);
                        graphics.DrawString(msg1, f1, b1, r1, sf);
                        graphics.DrawString(msg2, f2, b2, r2, sf);
                    }
                    return;
                }

                float zoom = canvas?.Viewport?.Zoom ?? 1f;
                float scale = Math.Max(0.5f, Math.Min(2f, zoom));

                try
                {
                    comp.DrawChart(graphics, graphRect, scale);
                }
                catch (Exception ex)
                {
                    using (var f = new Font("Courier New", 7f, FontStyle.Regular))
                    using (var bPen = new SolidBrush(Color.FromArgb(255, 100, 80)))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        graphics.DrawString($"Erro ao renderizar:\n{ex.Message}", f, bPen, graphRect, sf);
                }
            }
            else
            {
                base.Render(canvas, graphics, channel);
            }
        }
    }
}
