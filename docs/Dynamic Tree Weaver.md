---
name: "Dynamic Tree Weaver"
nickname: "TreeWeave"
category: "Buraqueira Tools"
subcategory: "Tree"
class: ""
file: "DynamicTreeWeaver_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, tree]
---

# 🧩 Dynamic Tree Weaver (`TreeWeave`)

**Categoria:** `Buraqueira Tools` ➔ `Tree`  
**Arquivo C#:** `DynamicTreeWeaver_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Intercala dinamicamente fluxos e ramos de árvores assimétricas respeitando padrões e chaves personalizadas sem a rigidez do Weave nativo.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Stream 0** (`S0`) | `Generic` | Primeiro fluxo de dados / árvore (Stream 0). |
| **Stream 1** (`S1`) | `Generic` | Segundo fluxo de dados / árvore (Stream 1). |
| **Stream 2** (`S2`) | `Generic` | Terceiro fluxo opcional de dados / árvore (Stream 2). |
| **Pattern** (`P`) | `Integer` | Padrão de intercalação (ex.: 0, 1, 0, 2). |
| **Mode** (`M`) | `Integer` | Modo de intercalação:\n0 = Por item dentro de cada caminho coincidente\n1 = Por ramo completo\n2 = Aplanar fluxos e tecer globalmente |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Weaved Tree** (`T`) | `Generic` | Árvore de dados tecida e combinada dinamicamente. |
| **Paths** (`P`) | `Text` | Lista dos caminhos gerados. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
