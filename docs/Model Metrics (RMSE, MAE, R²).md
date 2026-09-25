---
name: "Model Metrics (RMSE, MAE, R²)"
nickname: "Metrics"
category: "Buraqueira Tools"
subcategory: "Evaluation"
class: ""
file: "ModelEvaluation_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, evaluation]
---

# 🧩 Model Metrics (RMSE, MAE, R²) (`Metrics`)

**Categoria:** `Buraqueira Tools` ➔ `Evaluation`  
**Arquivo C#:** `ModelEvaluation_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Avalia a precisão e erro entre dados simulados/preditos e dados reais medidos em campo: RMSE, MAE, R² (Coeficiente de Determinação), MAPE %, Viés (Bias) e Erro Máximo.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Simulated / Pred** (`Y_sim`) | `Number` | Valores simulados, preditos ou estimados pelo modelo. |
| **Measured / True** (`Y_true`) | `Number` | Valores reais medidos em campo ou de referência (Ground Truth). Deve possuir o mesmo tamanho que Y_sim. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **RMSE** (`RMSE`) | `Number` | Root Mean Square Error: raiz do erro quadrático médio (penaliza grandes erros). |
| **MAE** (`MAE`) | `Number` | Mean Absolute Error: erro médio absoluto direto. |
| **R-Squared (R²)** (`R²`) | `Number` | Coeficiente de Determinação R² (1 - SS_res / SS_tot). Indica a proporção da variância explicada pelo modelo. |
| **MAPE %** (`MAPE`) | `Number` | Mean Absolute Percentage Error: erro percentual absoluto médio (%). |
| **Bias / ME** (`Bias`) | `Number` | Viés médio (Mean Error = Σ(Y_sim - Y_true) / N). Positivo = superestimação, Negativo = subestimação. |
| **Max Error** (`MaxErr`) | `Number` | Maior erro absoluto pontual encontrado no conjunto. |
| **Count** (`N`) | `Integer` | Número de pares comparados válidos. |
| **Report** (`Rep`) | `Text` | Relatório de desempenho e qualidade de ajuste do modelo. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
