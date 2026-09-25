using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public enum LossType
    {
        Huber = 0,               // Smooth Mean Absolute Error
        Quantile = 1,            // Pinball Loss / Incerteza
        MAE = 2,                 // Mean Absolute Error / L1
        MSE = 3,                 // Mean Squared Error / L2
        Hinge = 4,               // SVM / Margem Máxima
        BinaryCrossEntropy = 5   // Log Loss / Probabilidades
    }

    /// <summary>
    /// Componente de Funções de Perda (Loss Functions) do Buraqueira Tools.
    /// Calcula Huber Loss, Quantile Loss, MAE, MSE, Hinge Loss e Binary Cross-Entropy
    /// com suporte a pesos por elemento, perdas pontuais, resíduos e gradientes.
    /// Ideal como Função Objetivo (Fitness / Cost Function) para Galapagos, Wallacei, Goat e calibração de modelos.
    /// </summary>
    public class LossFunctions_Component : GH_Component
    {
        private LossType _selectedLossType = LossType.Huber;
        private double _defaultParam = 1.0; // Delta para Huber (1.0) ou Quantil q para Quantile (0.5)

        public LossFunctions_Component()
            : base(
                "Loss Functions",
                "Loss",
                "Calcula funções de perda (Loss Functions) para otimização, calibração e machine learning:\n" +
                "  - Huber Loss (Smooth MAE - Robusta a Outliers)\n" +
                "  - Quantile Loss (Pinball Loss - Modelagem de Incerteza/Percentis)\n" +
                "  - Mean Absolute Error (MAE / L1 Loss)\n" +
                "  - Mean Squared Error (MSE / L2 Loss)\n" +
                "  - Hinge Loss (Margem Máxima / SVM / Conformidade)\n" +
                "  - Binary Cross-Entropy (Log Loss / Classificação Probabilística)\n" +
                "Conecte a saída 'Loss' diretamente como Fitness no Galapagos, Wallacei ou Goat.",
                "Glaux Tools",
                "Evaluation")
        {
        }

        public override Guid ComponentGuid => new Guid("c7e2b145-8f6a-4d2b-9e3c-7a1b5c8d9e0f");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0: Valores Preditos / Simulados
            pManager.AddNumberParameter(
                "Predicted", "Y_pred",
                "Valores preditos ou simulados pelo modelo/solver ŷ (conecte saídas de simulação ou genes).",
                GH_ParamAccess.list);

            // 1: Valores Reais / Alvos
            pManager.AddNumberParameter(
                "Target / True", "Y_true",
                "Valores reais medidos em campo ou metas de projeto y (Ground Truth). Deve possuir o mesmo tamanho que Y_pred.",
                GH_ParamAccess.list);

            // 2: Tipo de Função de Perda (Opcional)
            pManager.AddGenericParameter(
                "Loss Type", "Type",
                "Função de perda a calcular:\n" +
                "  0: Huber Loss (Smooth MAE - Robusta a Outliers)\n" +
                "  1: Quantile Loss (Pinball Loss - Modelagem de Incerteza)\n" +
                "  2: MAE (L1 Loss - Erro Absoluto Médio)\n" +
                "  3: MSE (L2 Loss - Erro Quadrático Médio)\n" +
                "  4: Hinge Loss (SVM / Margem de Conformidade)\n" +
                "  5: Binary Cross-Entropy (Log Loss)\n" +
                "Aceita número (0 a 5), texto ou seleção no menu de clique direito.\nPadrão: Huber Loss (0).",
                GH_ParamAccess.item);
            pManager[2].Optional = true;

            // 3: Parâmetro Adicional (Delta δ ou Quantil q/τ)
            pManager.AddNumberParameter(
                "Parameter", "Param",
                "Parâmetro adicional da função de perda:\n" +
                "  - Huber Loss: Delta (δ / limiar de transição entre MSE e MAE). Padrão: 1.0\n" +
                "  - Quantile Loss: Quantil q / τ (entre 0.0 e 1.0). Ex: 0.50 (mediana), 0.90 (pior caso 90%), 0.10 (melhor caso). Padrão: 0.50\n" +
                "  - MAE / MSE / Hinge / BCE: Ignorado.",
                GH_ParamAccess.item, 1.0);
            pManager[3].Optional = true;

            // 4: Pesos Opcionais por Elemento
            pManager.AddNumberParameter(
                "Weights", "W",
                "Pesos opcionais por elemento para perda ponderada (ex: importância relativa por banda de oitava ou assento prioritário).\n" +
                "Se omitido, todos os pontos possuem peso 1.0.",
                GH_ParamAccess.list);
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // 0: Perda Média Total (Escalar para Fitness do Galapagos/Wallacei)
            pManager.AddNumberParameter(
                "Total Loss", "Loss",
                "Perda média total ponderada (Mean Loss) - Conecte diretamente como Fitness/Objetivo a MINIMIZAR no Galapagos, Wallacei ou Goat.",
                GH_ParamAccess.item);

            // 1: Perda Pontual Individual
            pManager.AddNumberParameter(
                "Pointwise Loss", "L_i",
                "Lista com o valor da perda individual elemento a elemento L(y_i, ŷ_i). Ideal para mapear gradientes de erro e colorir a malha 3D.",
                GH_ParamAccess.list);

            // 2: Gradientes / Derivadas
            pManager.AddNumberParameter(
                "Gradients", "Grad",
                "Lista com as derivadas parciais ∂L/∂ŷ_i para otimizadores baseados em gradiente ou análise de sensibilidade.",
                GH_ParamAccess.list);

            // 3: Resíduos Individuais (y - ŷ)
            pManager.AddNumberParameter(
                "Residuals", "Res",
                "Resíduos brutos individuais (y_i - ŷ_i) de cada par de dados.",
                GH_ParamAccess.list);

            // 4: Relatório Analítico
            pManager.AddTextParameter(
                "Report", "Rep",
                "Relatório técnico detalhado com interpretação física da perda, outliers detectados e resumo analítico.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rawPred = new List<double>();
            var rawTrue = new List<double>();

            if (!DA.GetDataList(0, rawPred) || rawPred == null || rawPred.Count == 0)
            {
                this.Message = "Sem Y_pred";
                return;
            }
            if (!DA.GetDataList(1, rawTrue) || rawTrue == null || rawTrue.Count == 0)
            {
                this.Message = "Sem Y_true";
                return;
            }

            if (rawPred.Count != rawTrue.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Incompatibilidade de tamanho: Y_pred ({rawPred.Count}) != Y_true ({rawTrue.Count}).");
                this.Message = "Tamanhos ≠";
                return;
            }

            // 1. Determinar o Tipo de Perda
            LossType lossType = _selectedLossType;
            object rawType = null;
            if (DA.GetData(2, ref rawType) && rawType != null)
            {
                lossType = ParseLossType(rawType, _selectedLossType);
            }

            // 2. Parâmetro (Delta ou Quantil)
            double userParam = (lossType == LossType.Quantile) ? 0.5 : 1.0;
            DA.GetData(3, ref userParam);

            // Validação de limites para Delta e Quantil
            if (lossType == LossType.Huber && userParam <= 0.0)
            {
                userParam = 1.0;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Delta para Huber Loss deve ser > 0. Ajustado para 1.0.");
            }
            else if (lossType == LossType.Quantile)
            {
                if (userParam <= 0.0 || userParam >= 1.0)
                {
                    userParam = Math.Max(0.01, Math.Min(0.99, userParam));
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Quantil ajustado para intervalo (0, 1): {userParam:F2}.");
                }
            }

            // 3. Pesos Opcionais
            var rawWeights = new List<double>();
            DA.GetDataList(4, rawWeights);
            bool hasWeights = rawWeights != null && rawWeights.Count > 0;

            // 4. Filtragem de dados válidos (remover NaN e Infinity)
            int total = rawPred.Count;
            var cleanPred = new List<double>();
            var cleanTrue = new List<double>();
            var cleanWeights = new List<double>();

            for (int i = 0; i < total; i++)
            {
                double p = rawPred[i];
                double t = rawTrue[i];
                if (!double.IsNaN(p) && !double.IsInfinity(p) && !double.IsNaN(t) && !double.IsInfinity(t))
                {
                    cleanPred.Add(p);
                    cleanTrue.Add(t);

                    double w = 1.0;
                    if (hasWeights)
                    {
                        if (i < rawWeights.Count)
                        {
                            double rw = rawWeights[i];
                            w = (double.IsNaN(rw) || rw < 0.0) ? 0.0 : rw;
                        }
                        else
                        {
                            w = rawWeights[rawWeights.Count - 1];
                        }
                    }
                    cleanWeights.Add(w);
                }
            }

            int n = cleanPred.Count;
            if (n == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum par de valores numéricos válidos encontrado.");
                this.Message = "Sem Dados Válidos";
                return;
            }

            // 5. Cálculo das Funções de Perda
            var pointwiseLoss = new List<double>(n);
            var gradients = new List<double>(n);
            var residuals = new List<double>(n);

            double weightedLossSum = 0.0;
            double totalWeight = 0.0;
            int outlierCount = 0; // Para Huber Loss (|e| > delta)
            double sumAbsRes = 0.0;
            double sumSqRes = 0.0;

            for (int i = 0; i < n; i++)
            {
                double yHat = cleanPred[i];
                double y = cleanTrue[i];
                double e = y - yHat; // Resíduo: y - ŷ
                double absE = Math.Abs(e);
                double w = cleanWeights[i];

                residuals.Add(e);
                sumAbsRes += absE;
                sumSqRes += e * e;

                double l_i = 0.0;
                double grad_i = 0.0;

                switch (lossType)
                {
                    case LossType.Huber:
                        {
                            // Huber Loss: L_δ = 0.5 * e² se |e| <= δ, senão δ*(|e| - 0.5*δ)
                            double delta = userParam;
                            if (absE <= delta)
                            {
                                l_i = 0.5 * e * e;
                                grad_i = yHat - y; // ∂L/∂ŷ = -(y - ŷ)
                            }
                            else
                            {
                                l_i = delta * (absE - 0.5 * delta);
                                grad_i = delta * (e > 0 ? -1.0 : 1.0); // -δ * sgn(y - ŷ)
                                outlierCount++;
                            }
                            break;
                        }

                    case LossType.Quantile:
                        {
                            // Quantile Loss (Pinball Loss): max(q*e, (q - 1)*e)
                            double q = userParam;
                            if (e >= 0.0)
                            {
                                l_i = q * e;
                                grad_i = -q;
                            }
                            else
                            {
                                l_i = (1.0 - q) * (-e);
                                grad_i = 1.0 - q;
                            }
                            break;
                        }

                    case LossType.MAE:
                        {
                            // Mean Absolute Error (L1 Loss): |y - ŷ|
                            l_i = absE;
                            grad_i = (e > 0) ? -1.0 : (e < 0 ? 1.0 : 0.0);
                            break;
                        }

                    case LossType.MSE:
                        {
                            // Mean Squared Error (L2 Loss): (y - ŷ)²
                            l_i = e * e;
                            grad_i = 2.0 * (yHat - y);
                            break;
                        }

                    case LossType.Hinge:
                        {
                            // Hinge Loss (SVM): max(0, 1 - y * ŷ)
                            // Se y estiver no padrão {0, 1}, mapeia 0 -> -1
                            double label = (y <= 0.0) ? -1.0 : 1.0;
                            double margin = label * yHat;
                            if (margin < 1.0)
                            {
                                l_i = 1.0 - margin;
                                grad_i = -label;
                            }
                            else
                            {
                                l_i = 0.0;
                                grad_i = 0.0;
                            }
                            break;
                        }

                    case LossType.BinaryCrossEntropy:
                        {
                            // Binary Cross-Entropy: -[y*ln(ŷ) + (1-y)*ln(1-ŷ)]
                            // Clamping para estabilidade numérica e evitar log(0)
                            double eps = 1e-15;
                            double yHatClamped = Math.Max(eps, Math.Min(1.0 - eps, yHat));
                            double label = Math.Max(0.0, Math.Min(1.0, y));

                            l_i = -(label * Math.Log(yHatClamped) + (1.0 - label) * Math.Log(1.0 - yHatClamped));
                            grad_i = (yHatClamped - label) / (yHatClamped * (1.0 - yHatClamped));
                            break;
                        }
                }

                pointwiseLoss.Add(l_i);
                gradients.Add(grad_i);

                weightedLossSum += w * l_i;
                totalWeight += w;
            }

            // 6. Perda Média Total (Ponderada)
            double meanLoss = (totalWeight > 1e-15) ? (weightedLossSum / totalWeight) : (weightedLossSum / n);
            double globalMae = sumAbsRes / n;
            double globalRmse = Math.Sqrt(sumSqRes / n);

            // 7. Relatório Analítico Detalhado
            string typeName = GetLossTypeName(lossType);
            string paramInfo = "";
            if (lossType == LossType.Huber)
                paramInfo = $"\nDelta de Transição (δ): {userParam:F3}\nOutliers Amortecidos (|e| > δ): {outlierCount} de {n} ({(double)outlierCount / n * 100.0:F1}%)";
            else if (lossType == LossType.Quantile)
                paramInfo = $"\nQuantil Alvo (q / τ): {userParam:P1} (Percentil {(userParam * 100.0):F0}%)\nInterpretação: Penaliza subestimação com peso {userParam:F2} e superestimação com peso {(1.0 - userParam):F2}";

            string report =
                $"=== Loss Functions Report (Buraqueira Tools) ===\n" +
                $"Função Aplicada: {typeName}\n" +
                $"Pares de Dados Avaliados (N): {n}\n" +
                $"Perda Média Ponderada (Total Loss): {meanLoss:G6}\n" +
                $"------------------------------------------------\n" +
                $"Métricas de Referência Geral:\n" +
                $"  - MAE (Erro Médio Absoluto): {globalMae:G5}\n" +
                $"  - RMSE (Raiz do Erro Quadrático): {globalRmse:G5}" +
                paramInfo + "\n" +
                $"------------------------------------------------\n" +
                $"Uso Recomendado: Conecte 'Loss' como Fitness a MINIMIZAR no Galapagos/Wallacei.";

            // 8. Atribuição de Saídas
            DA.SetData(0, meanLoss);
            DA.SetDataList(1, pointwiseLoss);
            DA.SetDataList(2, gradients);
            DA.SetDataList(3, residuals);
            DA.SetData(4, report);

            // Mensagem no Bloco (Badge)
            this.Message = $"{GetShortName(lossType)}: {meanLoss:G4}";
        }

        #region Helpers & Parsing

        public static LossType ParseLossType(object raw, LossType fallback)
        {
            if (raw == null) return fallback;
            if (raw is IGH_Goo goo) raw = goo.ScriptVariable();

            if (raw is int iVal && iVal >= 0 && iVal <= 5) return (LossType)iVal;
            if (raw is double dVal)
            {
                int r = (int)Math.Round(dVal);
                if (r >= 0 && r <= 5) return (LossType)r;
            }

            string s = raw.ToString().Trim().ToLowerInvariant();
            if (s.Contains("huber") || s.Contains("smooth")) return LossType.Huber;
            if (s.Contains("quant") || s.Contains("pinball") || s.Contains("percent")) return LossType.Quantile;
            if (s == "mae" || s.Contains("l1") || s.Contains("absolute")) return LossType.MAE;
            if (s == "mse" || s.Contains("l2") || s.Contains("squared") || s.Contains("quadrat")) return LossType.MSE;
            if (s.Contains("hinge") || s.Contains("svm") || s.Contains("margin")) return LossType.Hinge;
            if (s.Contains("cross") || s.Contains("entropy") || s.Contains("bce") || s.Contains("log loss") || s.Contains("logloss")) return LossType.BinaryCrossEntropy;

            if (int.TryParse(s, out int parsedInt) && parsedInt >= 0 && parsedInt <= 5)
                return (LossType)parsedInt;

            return fallback;
        }

        public static string GetLossTypeName(LossType type)
        {
            switch (type)
            {
                case LossType.Huber: return "Huber Loss (Smooth Mean Absolute Error)";
                case LossType.Quantile: return "Quantile Loss (Pinball Loss)";
                case LossType.MAE: return "Mean Absolute Error (MAE / L1 Loss)";
                case LossType.MSE: return "Mean Squared Error (MSE / L2 Loss)";
                case LossType.Hinge: return "Hinge Loss (SVM / Margem Máxima)";
                case LossType.BinaryCrossEntropy: return "Binary Cross-Entropy (Log Loss)";
                default: return type.ToString();
            }
        }

        public static string GetShortName(LossType type)
        {
            switch (type)
            {
                case LossType.Huber: return "Huber";
                case LossType.Quantile: return "Quantile";
                case LossType.MAE: return "MAE";
                case LossType.MSE: return "MSE";
                case LossType.Hinge: return "Hinge";
                case LossType.BinaryCrossEntropy: return "BCE";
                default: return type.ToString();
            }
        }

        #endregion

        #region Menu de Contexto (Right-Click) & Serialização

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var lossMenu = new ToolStripMenuItem("Selecionar Função de Perda (Loss):");

            var items = new (LossType type, string label)[]
            {
                (LossType.Huber, "0: Huber Loss (Smooth MAE - Robusta a Outliers)"),
                (LossType.Quantile, "1: Quantile Loss (Pinball Loss - Incerteza/Percentis)"),
                (LossType.MAE, "2: Mean Absolute Error (MAE / L1)"),
                (LossType.MSE, "3: Mean Squared Error (MSE / L2)"),
                (LossType.Hinge, "4: Hinge Loss (SVM / Margem de Conformidade)"),
                (LossType.BinaryCrossEntropy, "5: Binary Cross-Entropy (Log Loss)")
            };

            foreach (var item in items)
            {
                var menuItem = new ToolStripMenuItem(item.label)
                {
                    Checked = (_selectedLossType == item.type)
                };
                var targetType = item.type;
                menuItem.Click += (s, e) =>
                {
                    RecordUndoEvent("Alterar Função de Perda");
                    _selectedLossType = targetType;
                    ExpireSolution(true);
                };
                lossMenu.DropDownItems.Add(menuItem);
            }

            menu.Items.Add(lossMenu);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetInt32("SelectedLossType", (int)_selectedLossType);
            writer.SetDouble("DefaultParam", _defaultParam);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("SelectedLossType"))
            {
                _selectedLossType = (LossType)reader.GetInt32("SelectedLossType");
            }
            if (reader.ItemExists("DefaultParam"))
            {
                _defaultParam = reader.GetDouble("DefaultParam");
            }
            return base.Read(reader);
        }

        #endregion

        protected override Bitmap Icon => GlauxToolsIcons.LossFunctions;
    }
}
