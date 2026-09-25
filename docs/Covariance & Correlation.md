---
name: "Covariance & Correlation"
nickname: "CovCorr"
category: "Buraqueira Tools"
subcategory: "Evaluation"
class: ""
file: "CovarianceCorrelation_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, evaluation]
---

# 🧩 Covariance & Correlation (`CovCorr`)

**Categoria:** `Buraqueira Tools` ➔ `Evaluation`  
**Arquivo C#:** `CovarianceCorrelation_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Calcula a Correlação Linear de Pearson (r), Correlação de Postos de Spearman (ρ) e a Covariância Amostral/Populacional entre dois conjuntos pareados de dados.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **List X** (`X`) | `Number` | Primeira variável / conjunto de dados numéricos (X). |
| **List Y** (`Y`) | `Number` | Segunda variável / conjunto de dados pareados (Y). Deve possuir o mesmo tamanho que X. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Pearson r** (`r`) | `Number` | Coeficiente de correlação linear de Pearson (-1.0 a +1.0). |
| **Spearman ρ** (`ρ`) | `Number` | Coeficiente de correlação monotônica de postos de Spearman (-1.0 a +1.0). |
| **Sample Cov** (`Cov`) | `Number` | Covariância amostral Cov(X,Y) com divisor (N-1). |
| **Pop Cov** (`σxy`) | `Number` | Covariância populacional Cov(X,Y) com divisor N. |
| **R-Squared** (`r²`) | `Number` | Coeficiente de determinação linear r² (0.0 a 1.0). |
| **Interpretation** (`Desc`) | `Text` | Diagnóstico qualitativo da força e direção da correlação. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
