---
name: "Data Stack (VSTACK / HSTACK)"
nickname: "DataStack"
category: "Buraqueira Tools"
subcategory: "Tree"
class: ""
file: "DataStack_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, tree]
---

# 🧩 Data Stack (VSTACK / HSTACK) (`DataStack`)

**Categoria:** `Buraqueira Tools` ➔ `Tree`  
**Arquivo C#:** `DataStack_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Empilha e combina conjuntos de dados em pilha vertical (VSTACK / concatenação de ramos) ou horizontal (HSTACK / alinhamento de colunas/matrizes lado a lado com preenchimento seguro), similar às novas funções de matriz do Excel e NumPy.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data A** (`A`) | `Generic` | Primeiro conjunto de dados / árvore. |
| **Data B** (`B`) | `Generic` | Segundo conjunto de dados / árvore. |
| **Data C** (`C`) | `Generic` | Terceiro conjunto opcional de dados / árvore. |
| **Data D** (`D`) | `Generic` | Quarto conjunto opcional de dados / árvore. |
| **Mode** (`M`) | `Integer` | Modo: 0=Vertical Stack (VSTACK: empilha listas/ramos sequencialmente), 1=Horizontal Stack (HSTACK: alinha colunas lado a lado por linha {row; col}). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Stacked Tree** (`T`) | `Generic` | Árvore de dados resultante com a estrutura empilhada. |
| **Total Branches** (`B`) | `Integer` | Quantidade total de ramos na árvore resultante. |
| **Total Items** (`N`) | `Integer` | Quantidade total de elementos empilhados. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
