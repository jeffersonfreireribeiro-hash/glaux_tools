using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    public class ClusterValidation_Component : GH_Component
    {
        public ClusterValidation_Component()
            : base(
                "Cluster Validation (Mahalanobis & Silhouette)",
                "ClusterEval",
                "Avalia a qualidade de agrupamentos e distâncias multivariadas:\n" +
                "- Coeficiente de Silhueta (Silhouette Score) s = (b - a) / max(a, b) pontual e global\n" +
                "- Distância de Mahalanobis D² = (x - μ)ᵀ Σ⁻¹ (x - μ) sensível à covariância multivariada\n" +
                "- Diagnóstico de coesão intra-cluster, separabilidade inter-cluster e detecção de anomalias.",
                "Glaux Tools",
                "Evaluation")
        {
        }

        public override Guid ComponentGuid => new Guid("9cb48456-4a74-4e74-ae4f-1d8844bd469e");

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.ClusterValidation;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Points / Vectors", "D", "Pontos ou vetores multidimensionais. Aceita lista de Point3d ou Árvore onde cada ramo {i} é um vetor de atributos numéricos.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Cluster Labels", "Clusters", "Lista de rótulos de cluster para cada ponto (0, 1, ..., K-1).", GH_ParamAccess.list);
            pManager.AddGenericParameter("Center / Mean μ", "μ", "Ponto ou vetor médio de referência opcional para a distância de Mahalanobis. Se omitido, calcula o centroide empírico dos dados.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Covariance Matrix", "Cov", "Matriz de covariância opcional Σ (d x d). Se omitida, calcula a covariância empírica dos dados automaticamente.", GH_ParamAccess.tree);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Silhouette Scores", "s", "Coeficiente de Silhueta individual de cada ponto s_i em [-1, 1]. (> 0.5 bom agrupamento; < 0 mal classificado).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Mean Silhouette", "Avg_s", "Silhueta média global do modelo de clustering.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Cluster Silhouettes", "Cluster_s", "Silhueta média por cada cluster individual (K valores).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Mahalanobis D²", "D²", "Distância quadrática de Mahalanobis D² de cada ponto em relação ao centro ponderada pela covariância.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Mahalanobis Dist", "D", "Distância de Mahalanobis métrica D = √(D²).", GH_ParamAccess.list);
            pManager.AddTextParameter("Report", "Rep", "Diagnóstico de validação de agrupamento e separabilidade.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> dataTree) || dataTree == null || dataTree.DataCount == 0)
            {
                this.Message = "Sem Dados";
                return;
            }

            // 1. Extrair pontos/vetores em matriz N x D
            List<double[]> points = ExtractVectors(dataTree, out int dim);
            int n = points.Count;

            if (n < 2 || dim < 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "São necessários no mínimo 2 pontos multidimensionais válidos.");
                this.Message = "N < 2";
                return;
            }

            // 2. Extrair rótulos de cluster
            var clusterLabels = new List<int>();
            bool hasClusters = DA.GetDataList(1, clusterLabels) && clusterLabels != null && clusterLabels.Count == n;

            // 3. Média empírica μ
            double[] mean = new double[dim];
            for (int i = 0; i < n; i++)
            {
                for (int d = 0; d < dim; d++) mean[d] += points[i][d];
            }
            for (int d = 0; d < dim; d++) mean[d] /= n;

            // Centroide customizado se fornecido
            IGH_Goo centerGoo = null;
            if (DA.GetData(2, ref centerGoo) && centerGoo != null)
            {
                if (centerGoo.CastTo(out Point3d ptCenter))
                {
                    mean[0] = ptCenter.X;
                    if (dim > 1) mean[1] = ptCenter.Y;
                    if (dim > 2) mean[2] = ptCenter.Z;
                }
                else if (centerGoo.CastTo(out double dCenter) && dim == 1)
                {
                    mean[0] = dCenter;
                }
            }

            // 4. Matriz de Covariância Σ (dim x dim)
            double[,] cov = new double[dim, dim];
            for (int i = 0; i < n; i++)
            {
                for (int r = 0; r < dim; r++)
                {
                    double diffR = points[i][r] - mean[r];
                    for (int c = 0; c < dim; c++)
                    {
                        double diffC = points[i][c] - mean[c];
                        cov[r, c] += diffR * diffC;
                    }
                }
            }

            double covDiv = n > 1 ? (n - 1) : 1.0;
            for (int r = 0; r < dim; r++)
            {
                for (int c = 0; c < dim; c++) cov[r, c] /= covDiv;
                cov[r, r] += 1e-6; // Regularização Tikhonov para estabilidade numérica
            }

            // Inverter matriz de covariância via Gauss-Jordan
            double[,] invCov = InvertSquareMatrix(cov, dim);

            // 5. Calcular Distância de Mahalanobis D² = (x - μ)ᵀ Σ⁻¹ (x - μ)
            var mahalanobisSq = new List<double>(n);
            var mahalanobisDist = new List<double>(n);

            for (int i = 0; i < n; i++)
            {
                double[] diff = new double[dim];
                for (int d = 0; d < dim; d++) diff[d] = points[i][d] - mean[d];

                // y = Σ⁻¹ * diff
                double[] y = new double[dim];
                for (int r = 0; r < dim; r++)
                {
                    double s = 0.0;
                    for (int c = 0; c < dim; c++) s += invCov[r, c] * diff[c];
                    y[r] = s;
                }

                // D² = diffᵀ * y
                double d2 = 0.0;
                for (int d = 0; d < dim; d++) d2 += diff[d] * y[d];
                d2 = Math.Max(0.0, d2);

                mahalanobisSq.Add(d2);
                mahalanobisDist.Add(Math.Sqrt(d2));
            }

            // 6. Calcular Coeficiente de Silhueta (Silhouette Score) se houver clusters
            var silhouetteScores = new List<double>();
            double globalSilhouette = double.NaN;
            var clusterSilhouettes = new List<double>();

            if (hasClusters)
            {
                // Mapear clusters únicos
                var uniqueClusters = new HashSet<int>(clusterLabels);
                int kClusters = uniqueClusters.Count;

                if (kClusters >= 2 && kClusters < n)
                {
                    // Separar pontos por cluster
                    var clusterIndices = new Dictionary<int, List<int>>();
                    foreach (var cId in uniqueClusters) clusterIndices[cId] = new List<int>();
                    for (int i = 0; i < n; i++) clusterIndices[clusterLabels[i]].Add(i);

                    // Pré-calcular distâncias euclidianas entre todos os pares
                    double[,] distMatrix = new double[n, n];
                    for (int i = 0; i < n; i++)
                    {
                        for (int j = i + 1; j < n; j++)
                        {
                            double dist = EuclideanDist(points[i], points[j], dim);
                            distMatrix[i, j] = dist;
                            distMatrix[j, i] = dist;
                        }
                    }

                    double totalSil = 0.0;
                    var clusterSumSil = new Dictionary<int, double>();
                    foreach (var cId in uniqueClusters) clusterSumSil[cId] = 0.0;

                    for (int i = 0; i < n; i++)
                    {
                        int ownCluster = clusterLabels[i];
                        var ownPoints = clusterIndices[ownCluster];

                        // a(i) = média da distância para os outros pontos do mesmo cluster
                        double a_i = 0.0;
                        if (ownPoints.Count > 1)
                        {
                            double sumA = 0.0;
                            foreach (int idx in ownPoints)
                            {
                                if (idx != i) sumA += distMatrix[i, idx];
                            }
                            a_i = sumA / (ownPoints.Count - 1);
                        }

                        // b(i) = menor distância média para pontos de qualquer outro cluster
                        double b_i = double.MaxValue;
                        foreach (var otherKvp in clusterIndices)
                        {
                            if (otherKvp.Key == ownCluster) continue;
                            var otherPoints = otherKvp.Value;
                            if (otherPoints.Count == 0) continue;

                            double sumB = 0.0;
                            foreach (int idx in otherPoints)
                            {
                                sumB += distMatrix[i, idx];
                            }
                            double avgB = sumB / otherPoints.Count;
                            if (avgB < b_i) b_i = avgB;
                        }

                        // s(i) = (b - a) / max(a, b)
                        double s_i = 0.0;
                        if (ownPoints.Count > 1)
                        {
                            double maxAB = Math.Max(a_i, b_i);
                            if (maxAB > 1e-15)
                            {
                                s_i = (b_i - a_i) / maxAB;
                            }
                        }

                        silhouetteScores.Add(s_i);
                        totalSil += s_i;
                        clusterSumSil[ownCluster] += s_i;
                    }

                    globalSilhouette = totalSil / n;
                    foreach (var cId in uniqueClusters)
                    {
                        clusterSilhouettes.Add(clusterSumSil[cId] / clusterIndices[cId].Count);
                    }
                }
            }

            if (silhouetteScores.Count > 0)
            {
                DA.SetDataList(0, silhouetteScores);
                DA.SetData(1, globalSilhouette);
                DA.SetDataList(2, clusterSilhouettes);
            }
            DA.SetDataList(3, mahalanobisSq);
            DA.SetDataList(4, mahalanobisDist);

            // Relatório
            var sb = new StringBuilder();
            sb.AppendLine("=== RELATÓRIO DE VALIDAÇÃO DE CLUSTER E DISTÂNCIA DE MAHALANOBIS ===");
            sb.AppendLine($"Total de Amostras (n):        {n}");
            sb.AppendLine($"Dimensionalidade dos Vetores: {dim}D");
            if (hasClusters && !double.IsNaN(globalSilhouette))
            {
                sb.AppendLine("------------------------------------------------------------------");
                sb.AppendLine($"Silhueta Média Global:        {globalSilhouette:F4} (de -1.0 a +1.0)");
                string qual = globalSilhouette > 0.7 ? "Excelente estrutura de clusters" :
                              globalSilhouette > 0.5 ? "Boa separação razoável" :
                              globalSilhouette > 0.25 ? "Estrutura fraca / sobreposição" : "Sem clusters perceptíveis ou mal rotulados";
                sb.AppendLine($"Interpretação da Silhueta:    {qual}");
                for (int k = 0; k < clusterSilhouettes.Count; k++)
                {
                    sb.AppendLine($"  - Cluster {k}: Silhueta Média = {clusterSilhouettes[k]:F4}");
                }
            }
            sb.AppendLine("------------------------------------------------------------------");
            sb.AppendLine("Distância de Mahalanobis D² = (x - μ)ᵀ Σ⁻¹ (x - μ):");
            double avgMahal = 0.0;
            double maxMahal = 0.0;
            foreach (var d2 in mahalanobisSq)
            {
                avgMahal += d2;
                if (d2 > maxMahal) maxMahal = d2;
            }
            avgMahal /= n;
            sb.AppendLine($"  - Média D²:                  {avgMahal:F4} (esperado teórico ≈ {dim} para normais)");
            sb.AppendLine($"  - Maior D² (Outlier Extremo): {maxMahal:F4}");
            sb.AppendLine("==================================================================");

            DA.SetData(5, sb.ToString());

            this.Message = !double.IsNaN(globalSilhouette) ? $"Sil={globalSilhouette:F3}" : $"Mahal {dim}D";
        }

        private static List<double[]> ExtractVectors(GH_Structure<IGH_Goo> tree, out int dim)
        {
            dim = 0;
            var list = new List<double[]>();

            // Se for lista plana de Point3d
            if (tree.PathCount == 1)
            {
                var branch = tree.get_Branch(0);
                bool allPoints = true;
                foreach (var item in branch)
                {
                    if (item is IGH_Goo goo && goo.CastTo(out Point3d pt))
                    {
                        list.Add(new double[] { pt.X, pt.Y, pt.Z });
                    }
                    else
                    {
                        allPoints = false;
                        break;
                    }
                }
                if (allPoints && list.Count > 0)
                {
                    dim = 3;
                    return list;
                }
                list.Clear();
            }

            // Cada ramo é um vetor
            foreach (var path in tree.Paths)
            {
                var branch = tree.get_Branch(path);
                var vec = new List<double>();
                foreach (var item in branch)
                {
                    if (item is IGH_Goo goo)
                    {
                        if (goo.CastTo(out double val) && !double.IsNaN(val) && !double.IsInfinity(val))
                        {
                            vec.Add(val);
                        }
                        else if (goo.CastTo(out Point3d pt))
                        {
                            vec.Add(pt.X);
                            vec.Add(pt.Y);
                            vec.Add(pt.Z);
                        }
                    }
                }
                if (vec.Count > 0)
                {
                    if (dim == 0) dim = vec.Count;
                    if (vec.Count == dim)
                    {
                        list.Add(vec.ToArray());
                    }
                }
            }

            return list;
        }

        private static double EuclideanDist(double[] a, double[] b, int dim)
        {
            double sumSq = 0.0;
            for (int d = 0; d < dim; d++)
            {
                double diff = a[d] - b[d];
                sumSq += diff * diff;
            }
            return Math.Sqrt(sumSq);
        }

        private static double[,] InvertSquareMatrix(double[,] A, int n)
        {
            double[,] aug = new double[n, 2 * n];
            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++) aug[r, c] = A[r, c];
                aug[r, n + r] = 1.0;
            }

            for (int i = 0; i < n; i++)
            {
                int maxRow = i;
                double maxVal = Math.Abs(aug[i, i]);
                for (int r = i + 1; r < n; r++)
                {
                    if (Math.Abs(aug[r, i]) > maxVal)
                    {
                        maxVal = Math.Abs(aug[r, i]);
                        maxRow = r;
                    }
                }

                if (maxRow != i)
                {
                    for (int c = 0; c < 2 * n; c++)
                    {
                        double tmp = aug[i, c];
                        aug[i, c] = aug[maxRow, c];
                        aug[maxRow, c] = tmp;
                    }
                }

                double pivot = aug[i, i];
                if (Math.Abs(pivot) < 1e-15) pivot = 1e-15;

                for (int c = 0; c < 2 * n; c++) aug[i, c] /= pivot;

                for (int r = 0; r < n; r++)
                {
                    if (r != i)
                    {
                        double factor = aug[r, i];
                        for (int c = 0; c < 2 * n; c++) aug[r, c] -= factor * aug[i, c];
                    }
                }
            }

            double[,] inv = new double[n, n];
            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++) inv[r, c] = aug[r, n + c];
            }
            return inv;
        }
    }
}
