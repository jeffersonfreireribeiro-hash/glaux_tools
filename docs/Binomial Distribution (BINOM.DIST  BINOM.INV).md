---
name: "Binomial Distribution (BINOM.DIST / BINOM.INV)"
nickname: "BinomDist"
category: "Buraqueira Tools"
subcategory: "Statistics"
class: ""
file: "BinomialDistribution_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, statistics]
---

# 🧩 Binomial Distribution (BINOM.DIST / BINOM.INV) (`BinomDist`)

**Categoria:** `Buraqueira Tools` ➔ `Statistics`  
**Arquivo C#:** `BinomialDistribution_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Calcula a probabilidade da distribuição binomial individual ou cumulativa (BINOM.DIST), intervalo de probabilidade (BINOM.DIST.INTERVALO / BINOM.DIST.RANGE) e o inverso (BINOM.INV), compatível com Microsoft Excel 2010/2013+.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Successes** (`K`) | `Integer` | Número de sucessos k na amostra (inteiro >= 0). |
| **Trials** (`N`) | `Integer` | Número total de tentativas independentes n (inteiro >= k). |
| **Probability** (`P`) | `Number` | Probabilidade de sucesso em cada tentativa p em [0, 1]. |
| **Cumulative** (`Cum`) | `Boolean` | Se True, calcula a probabilidade cumulativa P(X <= k). Se False, calcula a probabilidade pontual exata P(X = k). |
| **Criterion** (`Crit`) | `Number` | Critério de probabilidade alfa para BINOM.INV (padrão: 0.5). |
| **Range S2** (`S2`) | `Integer` | Limite superior opcional de sucessos para BINOM.DIST.RANGE. Se fornecido, calcula P(k <= X <= S2). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Probability** (`Prob`) | `Number` | Resultado de BINOM.DIST: probabilidade pontual P(X = k) ou cumulativa P(X <= k). |
| **Range Prob** (`RngProb`) | `Number` | Resultado de BINOM.DIST.RANGE: probabilidade de obter entre k e S2 sucessos P(k <= X <= S2). |
| **Inverse** (`Inv`) | `Integer` | Resultado de BINOM.INV: menor valor de k para o qual a distribuição cumulativa é >= Critério. |
| **Mean** (`μ`) | `Number` | Média teórica da distribuição: n * p. |
| **Variance** (`σ²`) | `Number` | Variância teórica da distribuição: n * p * (1 - p). |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
