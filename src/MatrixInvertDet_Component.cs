using System;
using System.Collections.Generic;
using System.Globalization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class MatrixInvertDet_Component : GH_Component
    {
        public MatrixInvertDet_Component()
            : base(
                "Matrix Invert & Determinant",
                "MatInv",
                "Calcula a Inversa (A^-1), Determinante det(A) e Pseudo-Inversa (Moore-Penrose A^+) de qualquer matriz numérica.\n" +
                "- Para matrizes quadradas não-singulares, calcula a inversa exata via Gauss-Jordan.\n" +
                "- Para matrizes retangulares ou singulares, calcula a Pseudo-Inversa de Moore-Penrose estável.\n" +
                "- Fornece diagnóstico de condição e estabilidade numérica.",
                "Glaux Tools",
                "Matrix")
        {
        }

        public override Guid ComponentGuid => new Guid("92d2fc0b-0a1d-45aa-b4d7-ba7e54175465");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Matrix", "A", "Matriz de entrada (M x N). Cada ramo {i} representa uma linha.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Tolerance", "Tol", "Tolerância para detecção de singularidade (Padrão: 1e-12).", GH_ParamAccess.item, 1e-12);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Inverse", "Inv", "Matriz inversa A^-1 (válida quando quadrada e não-singular).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Determinant", "det", "Determinante escalar det(A).", GH_ParamAccess.item);
            pManager.AddNumberParameter("PseudoInverse", "PInv", "Pseudo-Inversa de Moore-Penrose (A^+), calculada para qualquer matriz retangular ou singular.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Condition", "Cond", "Diagnóstico de invertibilidade e condição numérica.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<IGH_Goo> tree = null;
            if (!DA.GetDataTree(0, out tree) || tree == null || tree.DataCount == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            double tol = 1e-12;
            DA.GetData(1, ref tol);

            double[,] A = ExtractMatrix(tree, out int M, out int N);

            bool isSquare = (M == N);
            double det = 0.0;
            double[,] inv = null;
            string condition = "Indeterminado";

            if (isSquare)
            {
                inv = InvertSquare(A, M, tol, out det, out bool success);
                if (success)
                {
                    condition = (Math.Abs(det) > 1e-4) ? "Invertível (Bem Condicionada)" : "Quase Singular (Mal Condicionada)";
                }
                else
                {
                    condition = "Singular (det = 0, Sem Inversa Clássica)";
                    inv = null;
                }
                DA.SetData(1, det);
            }
            else
            {
                condition = $"Retangular ({M}x{N}, Sem Determinante)";
            }

            // Pseudo-Inversa de Moore-Penrose (A^+)
            double[,] pinv = ComputePseudoInverse(A, M, N, tol);

            // Saídas
            if (inv != null)
            {
                DA.SetDataTree(0, MatrixToTree(inv, M, M));
            }
            if (pinv != null)
            {
                DA.SetDataTree(2, MatrixToTree(pinv, N, M));
            }

            DA.SetData(3, condition);
            this.Message = isSquare ? $"det={det:G3}" : $"{M}x{N}";
        }

        private static double[,] InvertSquare(double[,] A, int n, double tol, out double det, out bool success)
        {
            det = 1.0;
            success = false;

            double[,] aug = new double[n, 2 * n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) aug[i, j] = A[i, j];
                aug[i, n + i] = 1.0;
            }

            for (int col = 0; col < n; col++)
            {
                // Pivoteamento parcial
                int maxRow = col;
                double maxVal = Math.Abs(aug[col, col]);
                for (int r = col + 1; r < n; r++)
                {
                    double v = Math.Abs(aug[r, col]);
                    if (v > maxVal) { maxVal = v; maxRow = r; }
                }

                if (maxVal < tol)
                {
                    det = 0.0;
                    return null;
                }

                if (maxRow != col)
                {
                    for (int c = 0; c < 2 * n; c++)
                    {
                        double tmp = aug[col, c];
                        aug[col, c] = aug[maxRow, c];
                        aug[maxRow, c] = tmp;
                    }
                    det = -det;
                }

                double pivot = aug[col, col];
                det *= pivot;

                for (int c = 0; c < 2 * n; c++) aug[col, c] /= pivot;

                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    double factor = aug[r, col];
                    if (Math.Abs(factor) > 0.0)
                    {
                        for (int c = 0; c < 2 * n; c++) aug[r, c] -= factor * aug[col, c];
                    }
                }
            }

            var inv = new double[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) inv[i, j] = aug[i, n + j];
            }

            success = true;
            return inv;
        }

        private static double[,] ComputePseudoInverse(double[,] A, int m, int n, double tol)
        {
            // Pseudo-Inversa:
            // Se M == N e invertível: A^-1
            // Se M > N: (A^T * A)^-1 * A^T
            // Se M < N: A^T * (A * A^T)^-1
            // Com regularização de Tikhonov caso ATA seja singular
            double[,] AT = Transpose(A, m, n);

            if (m >= n)
            {
                double[,] ATA = Multiply(AT, n, m, A, m, n);
                // Regularização Tikhonov se necessário
                for (int i = 0; i < n; i++) ATA[i, i] += 1e-10;
                double[,] ATA_inv = InvertSquare(ATA, n, tol, out double _, out bool ok);
                if (ok) return Multiply(ATA_inv, n, n, AT, n, m);
            }
            else
            {
                double[,] AAT = Multiply(A, m, n, AT, n, m);
                for (int i = 0; i < m; i++) AAT[i, i] += 1e-10;
                double[,] AAT_inv = InvertSquare(AAT, m, tol, out double _, out bool ok);
                if (ok) return Multiply(AT, n, m, AAT_inv, m, m);
            }

            return null;
        }

        private static double[,] Transpose(double[,] A, int m, int n)
        {
            var T = new double[n, m];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < n; j++) T[j, i] = A[i, j];
            return T;
        }

        private static double[,] Multiply(double[,] A, int rA, int cA, double[,] B, int rB, int cB)
        {
            var C = new double[rA, cB];
            for (int i = 0; i < rA; i++)
            {
                for (int j = 0; j < cB; j++)
                {
                    double sum = 0.0;
                    for (int k = 0; k < cA; k++) sum += A[i, k] * B[k, j];
                    C[i, j] = sum;
                }
            }
            return C;
        }

        private static GH_Structure<GH_Number> MatrixToTree(double[,] mat, int rows, int cols)
        {
            var tree = new GH_Structure<GH_Number>();
            for (int r = 0; r < rows; r++)
            {
                var list = new List<GH_Number>();
                for (int c = 0; c < cols; c++) list.Add(new GH_Number(mat[r, c]));
                tree.AppendRange(list, new GH_Path(r));
            }
            return tree;
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

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.MatrixInvertDet;
    }
}
