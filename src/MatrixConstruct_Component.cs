using System;
using System.Collections.Generic;
using System.Globalization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class MatrixConstruct_Component : GH_Component
    {
        public MatrixConstruct_Component()
            : base(
                "Matrix Construct & Inspect",
                "MatBuild",
                "Constrói, formata e inspeciona matrizes numéricas M x N.\n" +
                "- Converte DataTrees arbitrárias em matrizes bidimensionais (onde cada ramo {i} é uma linha).\n" +
                "- Suporta criar matrizes a partir de listas planas definindo M e N.\n" +
                "- Gera matriz identidade I_N automaticamente.\n" +
                "- Calcula a matriz transposta (A^T), traço tr(A) e contagem de dimensões.",
                "Glaux Tools",
                "Matrix")
        {
        }

        public override Guid ComponentGuid => new Guid("383bc044-e90b-4d08-ab91-80a5cc8e67b2");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data / Tree", "D", "Árvore de dados (onde cada ramo {i} é uma linha) ou lista plana de valores numéricos.", GH_ParamAccess.tree);
            pManager[0].Optional = true;
            pManager.AddIntegerParameter("Rows", "M", "Número de linhas (opcional se fornecido via árvore de dados).", GH_ParamAccess.item, 0);
            pManager[1].Optional = true;
            pManager.AddIntegerParameter("Cols", "N", "Número de colunas (opcional se fornecido via árvore de dados).", GH_ParamAccess.item, 0);
            pManager[2].Optional = true;
            pManager.AddBooleanParameter("Identity", "I", "Se verdadeiro, gera uma Matriz Identidade I_N com dimensões N x N.", GH_ParamAccess.item, false);
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Matrix", "M", "Matriz construída como DataTree onde cada ramo {i} é uma linha.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Transpose", "MT", "Matriz transposta (A^T).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Dimensions", "Dim", "Texto informativo das dimensões (ex: '4 x 4').", GH_ParamAccess.item);
            pManager.AddNumberParameter("Trace", "tr", "Traço da matriz (soma da diagonal principal A_ii). Válido para matrizes quadradas.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Rows", "R", "Quantidade de linhas M.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Cols", "C", "Quantidade de colunas N.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<IGH_Goo> dataTree = null;
            DA.GetDataTree(0, out dataTree);

            int reqRows = 0;
            DA.GetData(1, ref reqRows);

            int reqCols = 0;
            DA.GetData(2, ref reqCols);

            bool identity = false;
            DA.GetData(3, ref identity);

            double[,] mat = null;
            int rows = 0;
            int cols = 0;

            if (identity)
            {
                int n = (reqCols > 0) ? reqCols : (reqRows > 0 ? reqRows : 3);
                rows = n;
                cols = n;
                mat = new double[rows, cols];
                for (int i = 0; i < n; i++) mat[i, i] = 1.0;
            }
            else if (dataTree != null && dataTree.DataCount > 0)
            {
                // Se a árvore tem múltiplos ramos, cada ramo é uma linha
                if (dataTree.Branches.Count > 1)
                {
                    rows = dataTree.Branches.Count;
                    cols = 0;
                    for (int r = 0; r < rows; r++)
                    {
                        if (dataTree.Branches[r].Count > cols) cols = dataTree.Branches[r].Count;
                    }
                    if (cols == 0) cols = 1;

                    mat = new double[rows, cols];
                    for (int r = 0; r < rows; r++)
                    {
                        var branch = dataTree.Branches[r];
                        for (int c = 0; c < cols; c++)
                        {
                            mat[r, c] = (c < branch.Count) ? ParseDouble(branch[c]) : 0.0;
                        }
                    }
                }
                else
                {
                    // Ramo único (lista plana): usa reqRows e reqCols se fornecidos
                    var list = dataTree.Branches[0];
                    int total = list.Count;

                    if (reqRows > 0 && reqCols > 0 && reqRows * reqCols == total)
                    {
                        rows = reqRows;
                        cols = reqCols;
                    }
                    else if (reqCols > 0)
                    {
                        cols = reqCols;
                        rows = (int)Math.Ceiling((double)total / cols);
                    }
                    else if (reqRows > 0)
                    {
                        rows = reqRows;
                        cols = (int)Math.Ceiling((double)total / rows);
                    }
                    else
                    {
                        // Vetor linha 1 x N
                        rows = 1;
                        cols = total;
                    }

                    mat = new double[rows, cols];
                    for (int idx = 0; idx < total; idx++)
                    {
                        int r = idx / cols;
                        int c = idx % cols;
                        if (r < rows) mat[r, c] = ParseDouble(list[idx]);
                    }
                }
            }
            else
            {
                this.Message = "Sem Dados";
                return;
            }

            // Constrói DataTree da Matriz e da Transposta
            var outMat = new GH_Structure<GH_Number>();
            var outTrans = new GH_Structure<GH_Number>();

            double trace = 0.0;
            bool isSquare = (rows == cols);

            for (int r = 0; r < rows; r++)
            {
                var rowList = new List<GH_Number>();
                for (int c = 0; c < cols; c++)
                {
                    double v = mat[r, c];
                    rowList.Add(new GH_Number(v));
                    if (isSquare && r == c) trace += v;
                }
                outMat.AppendRange(rowList, new GH_Path(r));
            }

            for (int c = 0; c < cols; c++)
            {
                var colList = new List<GH_Number>();
                for (int r = 0; r < rows; r++)
                {
                    colList.Add(new GH_Number(mat[r, c]));
                }
                outTrans.AppendRange(colList, new GH_Path(c));
            }

            DA.SetDataTree(0, outMat);
            DA.SetDataTree(1, outTrans);
            DA.SetData(2, $"{rows} x {cols}");
            if (isSquare) DA.SetData(3, trace);
            DA.SetData(4, rows);
            DA.SetData(5, cols);

            this.Message = $"{rows}x{cols}";
        }

        private static double ParseDouble(object item)
        {
            if (item == null) return 0.0;
            if (item is IGH_Goo goo) item = goo.SafeScriptVariable() ?? goo;
            if (item is double d) return d;
            if (item is float f) return f;
            if (item is int i) return i;
            string s = item.ToString().Replace(",", ".");
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double res)) return res;
            return 0.0;
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.MatrixConstruct;
    }
}
