---
name: "Loss Functions"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "LossFunctions_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Loss Functions (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `LossFunctions_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Predicted** (`Y_pred`) | `Number` | Valores preditos ou simulados pelo modelo/solver ŷ (conecte saídas de simulação ou genes). |
| **Target / True** (`Y_true`) | `Number` | Valores reais medidos em campo ou metas de projeto y (Ground Truth). Deve possuir o mesmo tamanho que Y_pred. |
| **Loss Type** (`Type`) | `Generic` | Função de perda a calcular:\n |
| **Parameter** (`Param`) | `Number` | Parâmetro adicional da função de perda:\n |
| **Weights** (`W`) | `Number` | Pesos opcionais por elemento para perda ponderada (ex: importância relativa por banda de oitava ou assento prioritário).\n |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Total Loss** (`Loss`) | `Number` | Perda média total ponderada (Mean Loss) - Conecte diretamente como Fitness/Objetivo a MINIMIZAR no Galapagos, Wallacei ou Goat. |
| **Pointwise Loss** (`L_i`) | `Number` | Lista com o valor da perda individual elemento a elemento L(y_i, ŷ_i). Ideal para mapear gradientes de erro e colorir a malha 3D. |
| **Gradients** (`Grad`) | `Number` | Lista com as derivadas parciais ∂L/∂ŷ_i para otimizadores baseados em gradiente ou análise de sensibilidade. |
| **Residuals** (`Res`) | `Number` | Resíduos brutos individuais (y_i - ŷ_i) de cada par de dados. |
| **Report** (`Rep`) | `Text` | Relatório técnico detalhado com interpretação física da perda, outliers detectados e resumo analítico. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
