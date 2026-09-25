---
name: "Variance"
nickname: "Var"
category: "Buraqueira Tools"
subcategory: "Statistics"
class: ""
file: "Variance_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, statistics]
---

# 🧩 Variance (`Var`)

**Categoria:** `Buraqueira Tools` ➔ `Statistics`  
**Arquivo C#:** `Variance_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Calcula a Variância Amostral (s² com divisor N-1), Variância Populacional (σ² com divisor N) e Soma dos Quadrados dos Desvios (SS).

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Values** (`V`) | `Number` | Conjunto de dados numéricos (lista ou árvore de números). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Sample Variance** (`s²`) | `Number` | Variância amostral não-viesada: s² = Σ(x - x̄)² / (N - 1). |
| **Population Variance** (`σ²`) | `Number` | Variância populacional exata: σ² = Σ(x - x̄)² / N. |
| **Sum of Squares** (`SS`) | `Number` | Soma dos quadrados dos desvios: SS = Σ(x - x̄)². |
| **Count** (`N`) | `Integer` | Quantidade total de elementos numéricos válidos. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
