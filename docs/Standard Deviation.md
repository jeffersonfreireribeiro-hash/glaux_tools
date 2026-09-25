---
name: "Standard Deviation"
nickname: "StdDev"
category: "Buraqueira Tools"
subcategory: "Statistics"
class: ""
file: "StandardDeviation_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, statistics]
---

# 🧩 Standard Deviation (`StdDev`)

**Categoria:** `Buraqueira Tools` ➔ `Statistics`  
**Arquivo C#:** `StandardDeviation_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Calcula o Desvio Padrão Amostral (s), Desvio Padrão Populacional (σ), Erro Padrão da Média (SE) e Coeficiente de Variação (CV%).

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Values** (`V`) | `Number` | Conjunto de dados numéricos (lista ou árvore de números). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Sample StdDev** (`s`) | `Number` | Desvio Padrão Amostral: s = √(Σ(x - x̄)² / (N - 1)). |
| **Population StdDev** (`σ`) | `Number` | Desvio Padrão Populacional: σ = √(Σ(x - x̄)² / N). |
| **Standard Error** (`SE`) | `Number` | Erro Padrão da Média: SE = s / √N. |
| **Coefficient of Variation** (`CV%`) | `Number` | Coeficiente de Variação percentual: CV = (s / x̄) * 100%. |
| **Mean** (`Avg`) | `Number` | Média aritmética dos valores (x̄). |
| **Count** (`N`) | `Integer` | Quantidade total de elementos numéricos válidos. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
