---
name: "Target Deviation"
nickname: "TgtDiff"
category: "Buraqueira Tools"
subcategory: "Statistics"
class: ""
file: "TargetDeviation_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, statistics]
---

# 🧩 Target Deviation (`TgtDiff`)

**Categoria:** `Buraqueira Tools` ➔ `Statistics`  
**Arquivo C#:** `TargetDeviation_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Compara um conjunto de valores com um valor alvo (Target), calculando o desvio em módulo (|x - T|), com sinal, erro percentual e localizando o valor mais próximo.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Target** (`T`) | `Number` | Valor alvo ou valor de referência para comparação. |
| **Values** (`V`) | `Number` | Conjunto de valores numéricos a serem comparados contra o alvo. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Absolute Difference** (`|Δ|`) | `Number` | Diferença em módulo (absoluta): |x_i - T|. |
| **Signed Difference** (`Δ`) | `Number` | Diferença com sinal algébrico: x_i - T. |
| **Percent Error** (`%Δ`) | `Number` | Erro relativo percentual: (|x_i - T| / |T|) * 100%. |
| **Closest Value** (`Best`) | `Number` | O valor da lista que possui a menor distância até o alvo. |
| **Closest Index** (`Idx`) | `Integer` | O índice (0-based) na lista do valor mais próximo do alvo. |
| **Min Absolute Difference** (`Min|Δ|`) | `Number` | O menor desvio absoluto encontrado na lista. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
