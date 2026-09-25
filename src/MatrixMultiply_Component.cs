using System;
using System.Collections.Generic;
using System.Globalization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class MatrixMultiply_Component : GH_Component
    {
        public MatrixMultiply_Component()
            : base(
                "Matrix Multiply",
                "MatMul",
                "Executa o produto matricial algébrico de alta performance entre duas matrizes A e B.\n" +
                "- Modo 0: Produto Matricial Clássico (A x B) onde Linhas x Colunas: (M x K) x (K x N) = (M x N).\n" +
                "- Modo 1: Produto Elemento a Elemento / Hadamard (A ⊙ B).\n" +
                "- Modo 2: Multiplicação por Escalar (α · A).\n" +
                "- Validação automática de compatibilidade dimensional.",
                "Glaux Tools",
                "Matrix")
        {
        }

        public override Guid ComponentGuid => new Guid("17637572-30ff-4cd5-8ac5-e5bd3af9e919");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Matrix A", "A", "Primeira matriz de entrada A (M x K). Cada ramo {i} representa uma linha.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Matrix B / Escalar", "B", "Segunda matriz de entrada B (K x N) ou valor escalar α.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Mode", "M", "0 = Produto Matricial Clássico (A x B)\n1 = Hadamard / Elemento a Elemento (A ⊙ B)\n2 = Multiplicação Escalar (α · A)", GH_ParamAccess.item, 0);
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Result", "C", "Matriz resultante C.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Dimensions", "Dim", "Dimensões da matriz resultante (ex: 'M x N').", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Valid", "OK", "True se as dimensões foram compatíveis e o cálculo foi concluído com sucesso.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<IGH_Goo> treeA = null;
            if (!DA.GetDataTree(0, out treeA) || treeA == null || treeA.DataCount == 0)
            {
                this.Message = "Sem Matriz A";
                return;
            }

            GH_Structure<IGH_Goo> treeB = null;
            if (!DA.GetDataTree(1, out treeB) || treeB == null || treeB.DataCount == 0)
            {
                this.Message = "Sem Matriz B";
                return;
            }

            int mode = 0;
            DA.GetData(2, ref mode);

            double[,] matA = ExtractMatrix(treeA, out int rowsA, out int colsA);

            // Verifica se B é um escalar simples
            bool isScalarB = (treeB.DataCount == 1);
            double scalarVal = isScalarB ? ParseDouble(treeB.Branches[0][0]) : 1.0;

            if (mode == 2 || (isScalarB && mode != 1))
            {
                // Multiplicação por escalar
                var outC = new GH_Structure<GH_Number>();
                for (int r = 0; r < rowsA; r++)
                {
                    var rowList = new List<GH_Number>();
                    for (int c = 0; c < colsA; c++)
                    {
                        rowList.Add(new GH_Number(matA[r, c] * scalarVal));
                    }
                    outC.AppendRange(rowList, new GH_Path(r));
                }

                DA.SetDataTree(0, outC);
                DA.SetData(1, $"{rowsA} x {colsA}");
                DA.SetData(2, true);
                this.Message = $"{rowsA}x{colsA} (Escalar)";
                return;
            }

            double[,] matB = ExtractMatrix(treeB, out int rowsB, out int colsB);

            if (mode == 1)
            {
                // Hadamard (Elemento a Elemento)
                if (rowsA != rowsB || colsA != colsB)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Dimensões incompatíveis para produto de Hadamard: A é {rowsA}x{colsA} e B é {rowsB}x{colsB}.");
                    DA.SetData(1, "Incompatível");
                    DA.SetData(2, false);
                    this.Message = "Erro Dimensão";
                    return;
                }

                var outC = new GH_Structure<GH_Number>();
                for (int r = 0; r < rowsA; r++)
                {
                    var rowList = new List<GH_Number>();
                    for (int c = 0; c < colsA; c++)
                    {
                        rowList.Add(new GH_Number(matA[r, c] * matB[r, c]));
                    }
                    outC.AppendRange(rowList, new GH_Path(r));
                }

                DA.SetDataTree(0, outC);
                DA.SetData(1, $"{rowsA} x {colsA}");
                DA.SetData(2, true);
                this.Message = $"{rowsA}x{colsA} (⊙)";
                return;
            }

            // Modo 0: Produto Matricial Clássico: (M x K) x (K x N) -> (M x N)
            if (colsA != rowsB)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Incompatibilidade dimensional: Colunas de A ({colsA}) devem ser iguais às Linhas de B ({rowsB}). Dimensões atuais: ({rowsA}x{colsA}) e ({rowsB}x{colsB}).");
                DA.SetData(1, "Incompatível");
                DA.SetData(2, false);
                this.Message = "Incompatível";
                return;
            }

            int rowsC = rowsA;
            int colsC = colsB;
            int K = colsA;

            var outTree = new GH_Structure<GH_Number>();

            for (int i = 0; i < rowsC; i++)
            {
                var rowList = new List<GH_Number>();
                for (int j = 0; j < colsC; j++)
                {
                    double sum = 0.0;
                    for (int k = 0; k < K; k++)
                    {
                        sum += matA[i, k] * matB[k, j];
                    }
                    rowList.Add(new GH_Number(sum));
                }
                outTree.AppendRange(rowList, new GH_Path(i));
            }

            DA.SetDataTree(0, outTree);
            DA.SetData(1, $"{rowsC} x {colsC}");
            DA.SetData(2, true);

            this.Message = $"{rowsC}x{colsC}";
        }

        private static double[,] ExtractMatrix(GH_Structure<IGH_Goo> tree, out int rows, out int cols)
        {
            rows = tree.Branches.Count;
            cols = 0;
            for (int r = 0; r < rows; r++)
            {
                if (tree.Branches[r].Count > cols) cols = tree.Branches[r].Count;
            }
            if (cols == 0) cols = 1;

            var mat = new double[rows, cols];
            for (int r = 0; r < rows; r++)
            {
                var branch = tree.Branches[r];
                for (int c = 0; c < cols; c++)
                {
                    mat[r, c] = (c < branch.Count) ? ParseDouble(branch[c]) : 0.0;
                }
            }
            return mat;
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

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.MatrixMultiply;
    }
}
