using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class VectorSimilarity_Component : GH_Component
    {
        public VectorSimilarity_Component()
            : base(
                "Vector Similarity Search (K-NN & Embeddings)",
                "VecSearch",
                "Busca vetorial ultra-rápida (estilo FAISS) para encontrar as configurações, curvas acústicas ou geometrias mais semelhantes a um alvo.\n" +
                "- Execução multithread paralela em CPU sobre milhares de vetores em milissegundos.\n" +
                "- Métricas: Similaridade de Cosseno (Cosine), Distância Euclidiana (L2), Manhattan (L1) e Correlação de Pearson.\n" +
                "- Retorna os Top-K vizinhos mais próximos, caminhos originais e índices de similaridade.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("9801bdab-4849-47ad-8493-84437a1541d2");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Database Vectors", "D", "Árvore contendo os vetores da base de dados (onde cada ramo {a;b} representa um vetor numérico de características).", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Query Vectors", "Q", "Vetor(es) de consulta / alvo (ex: a curva acústica ideal ou indivíduo de referência).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("K Nearest", "K", "Quantidade de vizinhos mais semelhantes a retornar por consulta (Top-K). Padrão: 5.", GH_ParamAccess.item, 5);
            pManager[2].Optional = true;
            pManager.AddIntegerParameter("Metric", "M", "Métrica de comparação:\n0 = Similaridade de Cosseno (Cosine: 1.0 = idêntico em tendência/formato)\n1 = Distância Euclidiana (L2: 0 = idêntico em escala absoluta)\n2 = Distância Manhattan (L1)\n3 = Correlação de Pearson (formato da curva sem viés de média)", GH_ParamAccess.item, 0);
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Nearest Vectors", "T", "Árvore com os vetores dos K vizinhos mais próximos para cada consulta.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Nearest Paths", "Paths", "Caminhos GH_Path originais na base de dados dos K vizinhos mais próximos.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Scores / Distances", "S", "Pontuação de similaridade (Cosseno/Pearson) ou Distância (L2/L1).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Rank", "R", "Posição no ranking (1 = mais parecido, 2 = segundo mais parecido, etc.).", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> dbTree) || dbTree == null || dbTree.PathCount == 0)
            {
                this.Message = "Sem Base";
                return;
            }

            if (!DA.GetDataTree(1, out GH_Structure<IGH_Goo> queryTree) || queryTree == null || queryTree.PathCount == 0)
            {
                this.Message = "Sem Query";
                return;
            }

            int topK = 5;
            DA.GetData(2, ref topK);
            if (topK < 1) topK = 1;

            int metric = 0;
            DA.GetData(3, ref metric);

            // 1. Extrair Base de Dados para Arrays contíguos de memória (Alta performance)
            var dbPaths = new List<GH_Path>();
            var dbVectorsList = new List<double[]>();

            foreach (GH_Path p in dbTree.Paths)
            {
                var branch = dbTree.get_Branch(p);
                if (branch == null || branch.Count == 0) continue;

                var vec = new double[branch.Count];
                bool valid = false;
                for (int i = 0; i < branch.Count; i++)
                {
                    if (GH_Convert.ToDouble(branch[i], out double val, GH_Conversion.Both))
                    {
                        vec[i] = val;
                        valid = true;
                    }
                }

                if (valid)
                {
                    dbPaths.Add(p);
                    dbVectorsList.Add(vec);
                }
            }

            int N = dbVectorsList.Count;
            if (N == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum vetor numérico válido encontrado na base.");
                return;
            }

            double[][] db = dbVectorsList.ToArray();

            // 2. Extrair Query Vectors
            var queryPaths = new List<GH_Path>();
            var queryVectorsList = new List<double[]>();

            foreach (GH_Path p in queryTree.Paths)
            {
                var branch = queryTree.get_Branch(p);
                if (branch == null || branch.Count == 0) continue;

                var vec = new double[branch.Count];
                bool valid = false;
                for (int i = 0; i < branch.Count; i++)
                {
                    if (GH_Convert.ToDouble(branch[i], out double val, GH_Conversion.Both))
                    {
                        vec[i] = val;
                        valid = true;
                    }
                }

                if (valid)
                {
                    queryPaths.Add(p);
                    queryVectorsList.Add(vec);
                }
            }

            int Q = queryVectorsList.Count;
            if (Q == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum vetor numérico válido encontrado na Query.");
                return;
            }

            // 3. Estruturas de saída
            var outNearestVectors = new GH_Structure<IGH_Goo>();
            var outNearestPaths = new GH_Structure<IGH_Goo>();
            var outScores = new GH_Structure<GH_Number>();
            var outRanks = new GH_Structure<GH_Integer>();

            // 4. Execução de Busca Vetorial Paralela por Query
            bool higherIsBetter = (metric == 0 || metric == 3); // Cosine e Pearson: maior é melhor

            for (int qIdx = 0; qIdx < Q; qIdx++)
            {
                double[] qVec = queryVectorsList[qIdx];
                GH_Path qPath = queryPaths[qIdx];

                // Array para armazenar scores de todos os N indivíduos
                double[] scores = new double[N];

                // Loop multithread paralelo ultra-rápido sobre todos os vetores da base
                Parallel.For(0, N, i =>
                {
                    double[] target = db[i];
                    scores[i] = ComputeSimilarity(qVec, target, metric);
                });

                // Ordenar e pegar Top-K
                var rankedIndices = Enumerable.Range(0, N).ToList();
                if (higherIsBetter)
                {
                    rankedIndices.Sort((a, b) => scores[b].CompareTo(scores[a]));
                }
                else
                {
                    rankedIndices.Sort((a, b) => scores[a].CompareTo(scores[b]));
                }

                int actualK = Math.Min(topK, N);

                for (int rank = 0; rank < actualK; rank++)
                {
                    int matchIdx = rankedIndices[rank];
                    GH_Path matchedDbPath = dbPaths[matchIdx];
                    double score = scores[matchIdx];

                    // Caminho de saída: {query_path; rank}
                    GH_Path resPath = qPath.AppendElement(rank);

                    // Adicionar vetor original
                    var origBranch = dbTree.get_Branch(matchedDbPath);
                    outNearestVectors.EnsurePath(resPath);
                    for (int b = 0; b < origBranch.Count; b++)
                    {
                        outNearestVectors.Append((IGH_Goo)origBranch[b], resPath);
                    }

                    // Adicionar caminho e score
                    outNearestPaths.Append(new GH_String(matchedDbPath.ToString()), resPath);
                    outScores.Append(new GH_Number(score), resPath);
                    outRanks.Append(new GH_Integer(rank + 1), resPath);
                }
            }

            DA.SetDataTree(0, outNearestVectors);
            DA.SetDataTree(1, outNearestPaths);
            DA.SetDataTree(2, outScores);
            DA.SetDataTree(3, outRanks);

            string metricName = metric switch
            {
                0 => "Cosine",
                1 => "L2 (Euclid)",
                2 => "L1 (Manh)",
                3 => "Pearson",
                _ => "Sim"
            };

            this.Message = $"VecSearch\n{metricName} | K={topK}";
        }

        private static double ComputeSimilarity(double[] u, double[] v, int metric)
        {
            int len = Math.Min(u.Length, v.Length);
            if (len == 0) return (metric == 0 || metric == 3) ? -1.0 : double.MaxValue;

            switch (metric)
            {
                case 0: // Cosine Similarity: (u . v) / (||u|| * ||v||)
                    {
                        double dot = 0.0;
                        double normU = 0.0;
                        double normV = 0.0;
                        for (int i = 0; i < len; i++)
                        {
                            dot += u[i] * v[i];
                            normU += u[i] * u[i];
                            normV += v[i] * v[i];
                        }
                        double denom = Math.Sqrt(normU) * Math.Sqrt(normV);
                        if (denom < 1e-12) return 0.0;
                        return dot / denom;
                    }

                case 1: // Euclidean Distance L2: sqrt(sum((u - v)^2))
                    {
                        double sumSq = 0.0;
                        for (int i = 0; i < len; i++)
                        {
                            double diff = u[i] - v[i];
                            sumSq += diff * diff;
                        }
                        return Math.Sqrt(sumSq);
                    }

                case 2: // Manhattan Distance L1: sum(|u - v|)
                    {
                        double sum = 0.0;
                        for (int i = 0; i < len; i++)
                        {
                            sum += Math.Abs(u[i] - v[i]);
                        }
                        return sum;
                    }

                case 3: // Pearson Correlation
                    {
                        double sumU = 0.0, sumV = 0.0;
                        for (int i = 0; i < len; i++)
                        {
                            sumU += u[i];
                            sumV += v[i];
                        }
                        double meanU = sumU / len;
                        double meanV = sumV / len;

                        double num = 0.0, denU = 0.0, denV = 0.0;
                        for (int i = 0; i < len; i++)
                        {
                            double du = u[i] - meanU;
                            double dv = v[i] - meanV;
                            num += du * dv;
                            denU += du * du;
                            denV += dv * dv;
                        }
                        double den = Math.Sqrt(denU) * Math.Sqrt(denV);
                        if (den < 1e-12) return 0.0;
                        return num / den;
                    }

                default:
                    return 0.0;
            }
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.VectorSimilarity;
    }
}
