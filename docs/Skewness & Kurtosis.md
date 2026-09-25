---
name: "Skewness & Kurtosis"
nickname: "SkewKurt"
category: "Buraqueira Tools"
subcategory: "Statistics"
class: ""
file: "SkewnessKurtosis_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, statistics]
---

# 🧩 Skewness & Kurtosis (`SkewKurt`)

**Categoria:** `Buraqueira Tools` ➔ `Statistics`  
**Arquivo C#:** `SkewnessKurtosis_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Calcula a Assimetria (Skewness g₁) e Curtose (Kurtosis g₂) para avaliar a forma da distribuição e proximidade de uma curva Normal.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Values** (`V`) | `Number` | Conjunto de dados numéricos (amostra). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Skewness** (`γ₁`) | `Number` | Coeficiente de assimetria amostral ajustado de Fisher-Pearson (0 = simétrico, >0 cauda à direita, <0 cauda à esquerda). |
| **Excess Kurtosis** (`γ₂`) | `Number` | Curtose em excesso amostral (Normal = 0, >0 leptocúrtica/caudas pesadas, <0 platicúrtica/achatada). |
| **Raw Kurtosis** (`β₂`) | `Number` | Curtose padrão / bruta (Distribuição Normal = 3.0). |
| **Is Normal?** (`Norm?`) | `Boolean` | Avaliação preliminar de normalidade (|γ₁| ≤ 0.5 e |γ₂| ≤ 1.0). |
| **Interpretation** (`Desc`) | `Text` | Diagnóstico descritivo detalhado da assimetria e curvatura da distribuição. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
