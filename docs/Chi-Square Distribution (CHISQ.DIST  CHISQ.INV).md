---
name: "Chi-Square Distribution (CHISQ.DIST / CHISQ.INV)"
nickname: "ChiSqDist"
category: "Buraqueira Tools"
subcategory: "Statistics"
class: ""
file: "ChiSquareDistribution_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, statistics]
---

# 🧩 Chi-Square Distribution (CHISQ.DIST / CHISQ.INV) (`ChiSqDist`)

**Categoria:** `Buraqueira Tools` ➔ `Statistics`  
**Arquivo C#:** `ChiSquareDistribution_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Calcula a distribuição Qui-Quadrado cumulativa (CHISQ.DIST), densidade de probabilidade (PDF) e a função inversa (CHISQ.INV), compatível com Microsoft Excel 2010+.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Value / Prob** (`X`) | `Number` | Valor x >= 0 (para DIST) ou probabilidade p em [0, 1] (para INV). |
| **Degrees of Freedom** (`DF`) | `Number` | Graus de liberdade da distribuição (DF >= 1). |
| **Cumulative** (`Cum`) | `Boolean` | Se True, calcula a distribuição cumulativa (CDF). Se False, calcula a densidade de probabilidade (PDF). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Distribution** (`Dist`) | `Number` | Resultado de CHISQ.DIST: probabilidade cumulativa (CDF) ou densidade (PDF). |
| **Inverse** (`Inv`) | `Number` | Resultado de CHISQ.INV: quantil x correspondente à probabilidade X em [0, 1]. |
| **Mean** (`μ`) | `Number` | Média teórica da distribuição Qui-Quadrado: DF. |
| **Variance** (`σ²`) | `Number` | Variância teórica da distribuição Qui-Quadrado: 2 * DF. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
