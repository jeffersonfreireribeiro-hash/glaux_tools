---
name: "Mass Math (Stack Operations)"
nickname: "MassMath"
category: "Buraqueira Tools"
subcategory: "Statistics"
class: ""
file: "MassMath_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, statistics]
---

# 🧩 Mass Math (Stack Operations) (`MassMath`)

**Categoria:** `Buraqueira Tools` ➔ `Statistics`  
**Arquivo C#:** `MassMath_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Executa operações de pilha e acumulação em massa sobre listas/árvores de números (Soma, Subtração, Multiplicação, Divisão progressiva, Média, Mín, Máx). Retorna o total escalar agregado e a lista de evolução acumulada (Running Total/Partial).

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Numbers** (`N`) | `Number` | Lista ou árvore de números a serem processados. |
| **Operation** (`Op`) | `Integer` | Operação: 0=Soma (Σ), 1=Subtração progressiva (n0 - n1...), 2=Multiplicação (Π), 3=Divisão progressiva (n0 / n1...), 4=Média acumulada, 5=Mínimo progressivo, 6=Máximo progressivo. |
| **Initial Value** (`Init`) | `Number` | Valor inicial opcional para a pilha de acumulação. Se omitido, utiliza o elemento neutro da operação. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Total** (`T`) | `Number` | Resultado escalar final agregado de cada ramo. |
| **Running** (`R`) | `Number` | Árvore de dados contendo a sequência acumulada passo a passo (Running total). |
| **Count** (`N`) | `Integer` | Quantidade total de elementos válidos processados por ramo. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
