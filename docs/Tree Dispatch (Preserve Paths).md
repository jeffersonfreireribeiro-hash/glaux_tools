---
name: "Tree Dispatch (Preserve Paths)"
nickname: "TreeDispatch"
category: "Buraqueira Tools"
subcategory: "Tree"
class: ""
file: "TreeConditionalPrunerDispatch_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, tree]
---

# 🧩 Tree Dispatch (Preserve Paths) (`TreeDispatch`)

**Categoria:** `Buraqueira Tools` ➔ `Tree`  
**Arquivo C#:** `TreeConditionalPrunerDispatch_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Divide uma Árvore de Dados em duas saídas (True / False) preservando a matriz original de caminhos GH_Path (com ramos vazios ou preenchimento com null) para não quebrar árvores a jusante.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Tree** (`T`) | `Generic` | Árvore de dados de entrada a ser despachada / dividida. |
| **Pattern** (`P`) | `Boolean` | Padrão ou máscara booleana (True = Saída A, False = Saída B). |
| **Preserve Mode** (`M`) | `Integer` | Modo de preservação de caminhos:\n0 = Manter ramos vazios (caminho existe, sem itens)\n1 = Preencher slots excluídos com <null> (mantém índices e comprimentos idênticos)\n2 = Podar ramos vazios (comportamento nativo) |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Tree True (A)** (`A`) | `Generic` | Árvore contendo os elementos avaliados como True. |
| **Tree False (B)** (`B`) | `Generic` | Árvore contendo os elementos avaliados como False. |
| **Paths** (`P`) | `Text` | Lista dos caminhos processados. |
| **Count A** (`NA`) | `Integer` | Total de elementos despachados para A. |
| **Count B** (`NB`) | `Integer` | Total de elementos despachados para B. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
