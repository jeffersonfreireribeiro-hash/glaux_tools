---
name: "Tree Path Item"
nickname: "PathItem"
category: "Buraqueira Tools"
subcategory: "Tree"
class: ""
file: "TreePathItem_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, tree]
---

# 🧩 Tree Path Item (`PathItem`)

**Categoria:** `Buraqueira Tools` ➔ `Tree`  
**Arquivo C#:** `TreePathItem_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Fatia e seleciona ramos de uma Árvore de Dados (DataTree) por dimensões de caminho {a;b;c...}, similar a um List Item multidimensional. Gera dinamicamente entradas para cada nível de ramificação ({0}, {1}, {2}...). Se uma dimensão for omitida, seleciona todos os ramos (*) filtrando pelas demais.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Tree** (`T`) | `Generic` | Árvore de dados de entrada a ser fatiada/selecionada. |
| **Index {0}** (`i0`) | `Generic` | Índice(s) desejado(s) para a 1ª dimensão do caminho {x;..}. Vazio = Todos (*). Suporta negativos (-1 = último) e listas. |
| **Index {1}** (`i1`) | `Generic` | Índice(s) desejado(s) para a 2ª dimensão do caminho {..;x;..}. Vazio = Todos (*). Suporta negativos (-1 = último) e listas. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Selected Tree** (`T`) | `Generic` | Árvore de dados contendo apenas os ramos selecionados. |
| **Selected Paths** (`P`) | `Path` | Lista dos caminhos (GH_Path) dos ramos selecionados. |
| **Remainder Tree** (`R`) | `Generic` | Árvore de dados com os ramos descartados / não selecionados (resto/inverso). |
| **Dimension Summary** (`I`) | `Text` | Resumo dos índices únicos existentes em cada dimensão da árvore de entrada. |
| **Branch Count** (`N`) | `Integer` | Quantidade de ramos selecionados. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
