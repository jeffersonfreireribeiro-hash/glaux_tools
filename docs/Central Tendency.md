---
name: "Central Tendency"
nickname: "CenterStat"
category: "Buraqueira Tools"
subcategory: "Statistics"
class: ""
file: "CentralTendency_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, statistics]
---

# 🧩 Central Tendency (`CenterStat`)

**Categoria:** `Buraqueira Tools` ➔ `Statistics`  
**Arquivo C#:** `CentralTendency_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Calcula medidas de tendência central: Média Aritmética (Mean), Mediana (Median), Moda(s) (Mode), Mínimo, Máximo, Amplitude e Contagem.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Values** (`V`) | `Number` | Conjunto de dados numéricos (lista ou árvore de números). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Mean** (`Avg`) | `Number` | Média aritmética dos valores (x̄ = Σx / N). |
| **Median** (`Med`) | `Number` | Mediana dos valores (ponto central ordenado Q2). |
| **Mode** (`Mod`) | `Number` | Moda(s) do conjunto de dados (valor ou valores com maior frequência). |
| **Min** (`Min`) | `Number` | Menor valor encontrado no conjunto. |
| **Max** (`Max`) | `Number` | Maior valor encontrado no conjunto. |
| **Range** (`Rng`) | `Number` | Amplitude total dos dados (Max - Min). |
| **Count** (`N`) | `Integer` | Quantidade total de elementos numéricos válidos. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
