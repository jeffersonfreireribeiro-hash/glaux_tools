---
name: "Loss Curve Visualizer"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "ChartLoss_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Loss Curve Visualizer (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `ChartLoss_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Predicted** (`Y_pred`) | `Number` | Valores preditos ou simulados ŷ (conecte saídas do modelo ou genes de otimização). |
| **Target / True** (`Y_true`) | `Number` | Valores reais medidos em campo ou metas de projeto y (Ground Truth). |
| **Loss Type** (`Type`) | `Generic` | 0: Huber Loss (Smooth MAE)\n1: Quantile Loss (Pinball)\n2: MAE (L1)\n3: MSE (L2)\n4: Hinge Loss (SVM)\n5: Binary Cross-Entropy\nPadrão: 0 (Huber Loss). Alternável também via clique direito. |
| **Parameter** (`Param`) | `Number` | Parâmetro adicional: Delta δ para Huber (padrão: 1.0) ou Quantil q/τ para Quantile Loss (padrão: 0.50). |
| **Title** (`T`) | `Text` | Título principal exibido no cabeçalho do gráfico. |
| **Width** (`W`) | `Integer` | Largura em pixels da imagem bitmap exportada (padrão: 900 px). |
| **Height** (`H`) | `Integer` | Altura em pixels da imagem bitmap exportada (padrão: 550 px). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Total Loss** (`Loss`) | `Number` | Perda média calculada (Mean Loss). |
| **Chart Image** (`Img`) | `Generic` | Bitmap do gráfico renderizado em alta definição (System.Drawing.Bitmap). |
| **Pointwise Loss** (`L_i`) | `Number` | Lista com o valor da perda de cada amostra individual. |
| **Residuals** (`Res`) | `Number` | Resíduos brutos individuais (y_i - ŷ_i). |
| **Gradients** (`Grad`) | `Number` | Lista de gradientes / derivadas parciais ∂L/∂ŷ_i. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
