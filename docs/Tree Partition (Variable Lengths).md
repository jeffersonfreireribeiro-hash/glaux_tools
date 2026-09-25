---
name: "Tree Partition (Variable Lengths)"
nickname: "PartVar"
category: "Buraqueira Tools"
subcategory: "Tree"
class: ""
file: "TreePartitionVariable_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, tree]
---

# 🧩 Tree Partition (Variable Lengths) (`PartVar`)

**Categoria:** `Buraqueira Tools` ➔ `Tree`  
**Arquivo C#:** `TreePartitionVariable_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Divide listas ou ramos de uma árvore em novos ramos com comprimentos variados e personalizados por ramo (ex.: tamanhos [2, 5, 3, 1]).

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Dados de entrada (lista ou árvore a ser particionada). |
| **Sizes** (`S`) | `Integer` | Lista de tamanhos de cada partição (ex.: 2, 5, 3). |
| **Repeat Pattern** (`R`) | `Boolean` | Se True, repete ciclicamente a lista de tamanhos até esgotar os dados. Se False, coloca o restante no último ramo. |
| **Per Branch** (`B`) | `Boolean` | Se True, particiona cada ramo individualmente gerando sub-ramos {path; i}. Se False, aplana a árvore antes de particionar. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Partitioned Tree** (`T`) | `Generic` | Árvore de dados resultante com os ramos particionados. |
| **Branch Lengths** (`L`) | `Integer` | Tamanho real de cada ramo gerado. |
| **Branch Count** (`N`) | `Integer` | Total de ramos criados. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
