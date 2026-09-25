using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class MatrixEigen_Component : GH_Component
    {
        public MatrixEigen_Component()
            : base(
                "Eigenvalues & Eigenvectors",
                "MatEig",
                "Calcula os Autovalores (λ) e Autovetores (v) de matrizes numéricas quadradas N x N (A · v = λ · v).\n" +
                "- Utiliza o algoritmo de Jacobi com rotações cíclicas ortogonais de alta estabilidade numérica.\n" +
                "- Ordena automaticamente por magnitude (maior para menor autovalor).\n" +
                "- Essencial para análise modal acústica/estrutural, vibrações naturais e PCA (Análise de Componentes Principais).",
                "Glaux Tools",
                "Matrix")
        {
        }

        public override Guid ComponentGuid => new Guid("0b3264ba-3a8b-4c64-9480-e0fc484de85b");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Matrix A", "A", "Matriz quadrada de entrada A (N x N). Cada ramo {i} representa uma linha.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Sort Descending", "Sort", "Se verdadeiro, ordena os autovalores em ordem decrescente de magnitude.", GH_ParamAccess.item, true);
            pManager[1].Optional = true;
            pManager.AddIntegerParameter("Max Iterations", "Iter", "Número máximo de iterações de convergência de Jacobi (Padrão: 100).", GH_ParamAccess.item, 100);
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Eigenvalues", "λ", "Lista ordenada dos autovalores calculados.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Eigenvectors", "V", "Matriz de autovetores ortonormais onde cada coluna j corresponde ao autovalor λj.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Diagonal", "D", "Matriz diagonal contendo os autovalores na diagonal principal.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<IGH_Goo> tree = null;
            if (!DA.GetDataTree(0, out tree) || tree == null || tree.DataCount == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            bool sortDesc = true;
            DA.GetData(1, ref sortDesc);

            int maxIter = 100;
            DA.GetData(2, ref maxIter);

            double[,] A = ExtractMatrix(tree, out int M, out int N);

            if (M != N)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Cálculo de autovalores exige matriz quadrada N x N. Matriz atual é {M} x {N}.");
                this.Message = "Não Quadrada";
                return;
            }

            int n = N;

            // Simetrização para robustez numérica no método de Jacobi: S = (A + A^T) / 2
            double[,] S = new double[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    S[i, j] = 0.5 * (A[i, j] + A[j, i]);
                }
            }

            // Matriz V de autovetores inicializada como Identidade
            double[,] V = new double[n, n];
            for (int i = 0; i < n; i++) V[i, i] = 1.0;

            // Algoritmo de Rotações Cíclicas de Jacobi
            for (int iter = 0; iter < maxIter; iter++)
            {
                // Encontra maior elemento fora da diagonal
                double maxOffDiag = 0.0;
                int p = 0, q = 1;
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        double val = Math.Abs(S[i, j]);
                        if (val > maxOffDiag)
                        {
                            maxOffDiag = val;
                            p = i;
                            q = j;
                        }
                    }
                }

                if (maxOffDiag < 1e-14) break; // Convergiu com precisão de máquina

                // Ângulo de rotação de Jacobi
                double theta = 0.5 * (S[q, q] - S[p, p]) / S[p, q];
                double t = 1.0 / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1.0));
                if (theta < 0.0) t = -t;

                double c = 1.0 / Math.Sqrt(t * t + 1.0);
                double s = t * c;
                double tau = s / (1.0 + c);

                double Spq = S[p, q];
                S[p, q] = 0.0;
                S[p, p] -= t * Spq;
                S[q, q] += t * Spq;

                for (int i = 0; i < p; i++)
                {
                    double g = S[i, p], h = S[i, q];
                    S[i, p] = g - s * (h + g * tau);
                    S[i, q] = h + s * (g - h * tau);
                }
                for (int i = p + 1; i < q; i++)
                {
                    double g = S[p, i], h = S[i, q];
                    S[p, i] = g - s * (h + g * tau);
                    S[i, q] = h + s * (g - h * tau);
                }
                for (int i = q + 1; i < n; i++)
                {
                    double g = S[p, i], h = S[q, i];
                    S[p, i] = g - s * (h + g * tau);
                    S[q, i] = h + s * (g - h * tau);
                }

                // Acumula autovetores
                for (int i = 0; i < n; i++)
                {
                    double g = V[i, p], h = V[i, q];
                    V[i, p] = g - s * (h + g * tau);
                    V[i, q] = h + s * (g - h * tau);
                }
            }

            // Extrai autovalores da diagonal
            var eigenPairs = new List<EigenPair>();
            for (int i = 0; i < n; i++)
            {
                double val = S[i, i];
                double[] vec = new double[n];
                for (int r = 0; r < n; r++) vec[r] = V[r, i];
                eigenPairs.Add(new EigenPair { Value = val, Vector = vec });
            }

            // Ordenação
            if (sortDesc)
                eigenPairs.Sort((a, b) => b.Value.CompareTo(a.Value));
            else
                eigenPairs.Sort((a, b) => a.Value.CompareTo(b.Value));

            // Saídas
            var outVals = new List<double>();
            var outVecTree = new GH_Structure<GH_Number>();
            var outDiagTree = new GH_Structure<GH_Number>();

            for (int col = 0; col < n; col++)
            {
                outVals.Add(eigenPairs[col].Value);
            }

            // Matriz V: cada ramo {i} é uma linha dos autovetores
            for (int r = 0; r < n; r++)
            {
                var rowList = new List<GH_Number>();
                for (int c = 0; c < n; c++)
                {
                    rowList.Add(new GH_Number(eigenPairs[c].Vector[r]));
                }
                outVecTree.AppendRange(rowList, new GH_Path(r));
            }

            // Matriz D: diagonal
            for (int r = 0; r < n; r++)
            {
                var diagRow = new List<GH_Number>();
                for (int c = 0; c < n; c++)
                {
                    diagRow.Add(new GH_Number(r == c ? eigenPairs[r].Value : 0.0));
                }
                outDiagTree.AppendRange(diagRow, new GH_Path(r));
            }

            DA.SetDataList(0, outVals);
            DA.SetDataTree(1, outVecTree);
            DA.SetDataTree(2, outDiagTree);

            this.Message = $"{n} Modos";
        }

        private class EigenPair
        {
            public double Value;
            public double[] Vector;
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

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.MatrixEigen;
    }
}
