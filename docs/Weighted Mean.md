---
name: "Weighted Mean"
nickname: "WAvg"
category: "Buraqueira Tools"
subcategory: "Statistics"
class: ""
file: "WeightedMean_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, statistics]
---

# 🧩 Weighted Mean (`WAvg`)

**Categoria:** `Buraqueira Tools` ➔ `Statistics`  
**Arquivo C#:** `WeightedMean_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Calcula a Média Ponderada (Weighted Mean), Soma Total de Pesos e Pesos Normalizados a partir de listas de valores e pesos correspondentes.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Values** (`V`) | `Number` | Conjunto de valores numéricos (x_i). |
| **Weights** (`W`) | `Number` | Pesos correspondentes a cada valor (w_i). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Weighted Mean** (`WAvg`) | `Number` | Média ponderada: x̄_w = Σ(x_i * w_i) / Σ(w_i). |
| **Total Weight** (`SumW`) | `Number` | Soma de todos os pesos válidos: Σ(w_i). |
| **Normalized Weights** (`NW`) | `Number` | Pesos normalizados proporcionais (w_i / Σw). |
| **Simple Mean** (`Avg`) | `Number` | Média aritmética simples para comparação rápida. |
| **Count** (`N`) | `Integer` | Quantidade total de pares (valor, peso) processados. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
