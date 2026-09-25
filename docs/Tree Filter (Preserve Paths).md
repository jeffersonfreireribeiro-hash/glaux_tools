---
name: "Tree Filter (Preserve Paths)"
nickname: "TreeFilter"
category: "Buraqueira Tools"
subcategory: "Tree"
class: ""
file: "TreeFilter_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, tree]
---

# 🧩 Tree Filter (Preserve Paths) (`TreeFilter`)

**Categoria:** `Buraqueira Tools` ➔ `Tree`  
**Arquivo C#:** `TreeFilter_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Filtra itens em uma Árvore de Dados (DataTree) com base em uma máscara booleana ou condição, sem perder, colapsar ou alterar os caminhos (GH_Path) originais.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Tree** (`T`) | `Generic` | Árvore de dados de entrada a ser filtrada. |
| **Mask** (`M`) | `Boolean` | Máscara booleana (DataTree ou lista com valores True para manter e False para filtrar). |
| **Preserve Empty Branches** (`E`) | `Boolean` | Se True, mantém os ramos onde todos os itens foram filtrados na árvore (ramos vazios). Se False, remove o ramo. |
| **Replace With Null** (`N`) | `Boolean` | Se True, substitui os itens filtrados por <null> mantendo o índice e contagem exatos por ramo. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Filtered Tree** (`T`) | `Generic` | Árvore de dados resultante com os mesmos caminhos GH_Path da original. |
| **Retained Indices** (`i`) | `Integer` | Índices originais dos itens mantidos em cada ramo. |
| **Count** (`N`) | `Integer` | Quantidade total de itens retidos na árvore. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
