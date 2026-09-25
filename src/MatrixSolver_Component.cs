using System;
using System.Collections.Generic;
using System.Globalization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class MatrixSolver_Component : GH_Component
    {
        public MatrixSolver_Component()
            : base(
                "Linear System Solver",
                "LinSolve",
                "Resolve sistemas de equações lineares exatos ou sobredeterminados da forma A · x = b.\n" +
                "- Para matrizes quadradas N x N: resolução direta via Decomposição LU com pivoteamento parcial.\n" +
                "- Para sistemas sobredeterminados M x N (M > N): solução de Mínimos Quadrados (Least Squares).\n" +
                "- Calcula o resíduo euclidiano ||A·x - b|| para avaliação rigorosa da precisão da solução.",
                "Glaux Tools",
                "Matrix")
        {
        }

        public override Guid ComponentGuid => new Guid("dfb8ca71-3a5c-41f8-b291-cfa534c09fba");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Matrix A", "A", "Matriz de coeficientes A (M x N).", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Vector b", "b", "Vetor ou coluna de termos independentes b (M valores).", GH_ParamAccess.tree);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Solution x", "x", "Vetor solução x tal que A · x ≈ b.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Residual", "r", "Resíduo euclidiano ||A·x - b||2 (erro da solução).", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Solved", "OK", "True se o sistema foi resolvido com sucesso.", GH_ParamAccess.item);
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
                this.Message = "Sem Vetor b";
                return;
            }

            double[,] A = ExtractMatrix(treeA, out int M, out int N);
            double[] b = ExtractVector(treeB, M);

            if (b.Length != M)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Incompatibilidade: O vetor b tem {b.Length} elementos, mas a matriz A tem {M} linhas.");
                DA.SetData(2, false);
                this.Message = "Erro Dimensão";
                return;
            }

            double[] x = null;
            bool ok = false;

            if (M == N)
            {
                // Sistema quadrado: LU com pivoteamento
                x = SolveSquareLU(A, b, N, out ok);
            }
            else if (M > N)
            {
                // Sobredeterminado: Mínimos Quadrados (A^T * A) * x = A^T * b
                x = SolveLeastSquares(A, b, M, N, out ok);
            }
            else
            {
                // Subdeterminado (M < N): Solução de norma mínima x = A^T * (A * A^T)^-1 * b
                x = SolveMinimumNorm(A, b, M, N, out ok);
            }

            if (!ok || x == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "O sistema linear é singular ou indeterminado.");
                DA.SetData(2, false);
                this.Message = "Singular";
                return;
            }

            // Calcula resíduo ||A*x - b||
            double residualSq = 0.0;
            for (int i = 0; i < M; i++)
            {
                double ax = 0.0;
                for (int j = 0; j < N; j++) ax += A[i, j] * x[j];
                double diff = ax - b[i];
                residualSq += diff * diff;
            }
            double residual = Math.Sqrt(residualSq);

            var xList = new List<double>(x);

            DA.SetDataList(0, xList);
            DA.SetData(1, residual);
            DA.SetData(2, true);

            this.Message = $"r = {residual:G2}";
        }

        private static double[] SolveSquareLU(double[,] A, double[] b, int n, out bool ok)
        {
            ok = false;
            double[,] LU = (double[,])A.Clone();
            int[] piv = new int[n];
            for (int i = 0; i < n; i++) piv[i] = i;

            for (int j = 0; j < n; j++)
            {
                int p = j;
                double maxVal = Math.Abs(LU[j, j]);
                for (int i = j + 1; i < n; i++)
                {
                    if (Math.Abs(LU[i, j]) > maxVal)
                    {
                        maxVal = Math.Abs(LU[i, j]);
                        p = i;
                    }
                }

                if (maxVal < 1e-14) return null; // Singular

                if (p != j)
                {
                    for (int k = 0; k < n; k++)
                    {
                        double tmp = LU[p, k];
                        LU[p, k] = LU[j, k];
                        LU[j, k] = tmp;
                    }
                    int tPiv = piv[p]; piv[p] = piv[j]; piv[j] = tPiv;
                }

                for (int i = j + 1; i < n; i++)
                {
                    LU[i, j] /= LU[j, j];
                    for (int k = j + 1; k < n; k++)
                    {
                        LU[i, k] -= LU[i, j] * LU[j, k];
                    }
                }
            }

            // Forward substitution L * y = P * b
            double[] y = new double[n];
            for (int i = 0; i < n; i++)
            {
                double sum = b[piv[i]];
                for (int k = 0; k < i; k++) sum -= LU[i, k] * y[k];
                y[i] = sum;
            }

            // Backward substitution U * x = y
            double[] x = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                double sum = y[i];
                for (int k = i + 1; k < n; k++) sum -= LU[i, k] * x[k];
                x[i] = sum / LU[i, i];
            }

            ok = true;
            return x;
        }

        private static double[] SolveLeastSquares(double[,] A, double[] b, int m, int n, out bool ok)
        {
            // (A^T * A) * x = A^T * b
            double[,] ATA = new double[n, n];
            double[] ATb = new double[n];

            for (int i = 0; i < n; i++)
            {
                double sumB = 0.0;
                for (int k = 0; k < m; k++) sumB += A[k, i] * b[k];
                ATb[i] = sumB;

                for (int j = 0; j < n; j++)
                {
                    double sum = 0.0;
                    for (int k = 0; k < m; k++) sum += A[k, i] * A[k, j];
                    ATA[i, j] = sum;
                }
                ATA[i, i] += 1e-12; // Tikhonov mínima
            }

            return SolveSquareLU(ATA, ATb, n, out ok);
        }

        private static double[] SolveMinimumNorm(double[,] A, double[] b, int m, int n, out bool ok)
        {
            // x = A^T * (A * A^T)^-1 * b
            double[,] AAT = new double[m, m];
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    double sum = 0.0;
                    for (int k = 0; k < n; k++) sum += A[i, k] * A[j, k];
                    AAT[i, j] = sum;
                }
                AAT[i, i] += 1e-12;
            }

            double[] y = SolveSquareLU(AAT, b, m, out ok);
            if (!ok || y == null) return null;

            double[] x = new double[n];
            for (int j = 0; j < n; j++)
            {
                double sum = 0.0;
                for (int i = 0; i < m; i++) sum += A[i, j] * y[i];
                x[j] = sum;
            }
            return x;
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

        private static double[] ExtractVector(GH_Structure<IGH_Goo> tree, int expectedLength)
        {
            var list = new List<double>();
            for (int b = 0; b < tree.Branches.Count; b++)
            {
                var branch = tree.Branches[b];
                for (int i = 0; i < branch.Count; i++)
                {
                    list.Add(ParseDouble(branch[i]));
                }
            }
            return list.ToArray();
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

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.MatrixSolver;
    }
}
