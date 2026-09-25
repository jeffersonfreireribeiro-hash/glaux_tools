---
name: "Beta Distribution (BETA.DIST / BETA.INV)"
nickname: "BetaDist"
category: "Buraqueira Tools"
subcategory: "Statistics"
class: ""
file: "BetaDistribution_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, statistics]
---

# 🧩 Beta Distribution (BETA.DIST / BETA.INV) (`BetaDist`)

**Categoria:** `Buraqueira Tools` ➔ `Statistics`  
**Arquivo C#:** `BetaDistribution_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Calcula a distribuição cumulativa Beta (BETA.DIST), a função de densidade de probabilidade (PDF) e a função inversa (BETA.INV), compatível com Microsoft Excel 2010+.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Value / Prob** (`X`) | `Number` | Valor x em [A, B] (para DIST) ou probabilidade p em [0, 1] (para INV). |
| **Alpha** (`α`) | `Number` | Parâmetro de forma Alfa (α > 0). |
| **Beta** (`β`) | `Number` | Parâmetro de forma Beta (β > 0). |
| **Cumulative** (`Cum`) | `Boolean` | Se True, calcula a probabilidade cumulativa (CDF). Se False, calcula a densidade de probabilidade (PDF). |
| **Lower Bound** (`A`) | `Number` | Limite inferior opcional do intervalo [A, B] (padrão: 0.0). |
| **Upper Bound** (`B`) | `Number` | Limite superior opcional do intervalo [A, B] (padrão: 1.0). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Distribution** (`Dist`) | `Number` | Resultado de BETA.DIST: probabilidade cumulativa (CDF) ou densidade (PDF). |
| **Inverse** (`Inv`) | `Number` | Resultado de BETA.INV: valor x correspondente à probabilidade X em [0, 1]. |
| **Mean** (`μ`) | `Number` | Média teórica da distribuição no intervalo [A, B]. |
| **Variance** (`σ²`) | `Number` | Variância teórica da distribuição. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
