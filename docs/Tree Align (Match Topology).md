---
name: "Tree Align (Match Topology)"
nickname: "TreeAlign"
category: "Buraqueira Tools"
subcategory: "Tree"
class: ""
file: "TreeAlignTopology_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, tree]
---

# 🧩 Tree Align (Match Topology) (`TreeAlign`)

**Categoria:** `Buraqueira Tools` ➔ `Tree`  
**Arquivo C#:** `TreeAlignTopology_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Emparelha e alinha duas Árvores de Dados com topologias assimétricas, preenchendo ramos faltantes com <null> ou valor padrão para evitar quebras em operações 1:1.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Tree A** (`A`) | `Generic` | Primeira árvore de dados (A). |
| **Tree B** (`B`) | `Generic` | Segunda árvore de dados (B). |
| **Mode** (`M`) | `Integer` | Modo de emparelhamento:\n0 = União (ambas recebem todos os ramos de A e B)\n1 = Alinhar B para A (seguir caminhos de A)\n2 = Alinhar A para B (seguir caminhos de B)\n3 = Interseção (manter apenas caminhos existentes em ambas) |
| **Default Fill A** (`fillA`) | `Generic` | Valor opcional para preencher ramos inexistentes em A (padrão <null>). |
| **Default Fill B** (`fillB`) | `Generic` | Valor opcional para preencher ramos inexistentes em B (padrão <null>). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Aligned A** (`A`) | `Generic` | Árvore A com topologia alinhada e sincronizada. |
| **Aligned B** (`B`) | `Generic` | Árvore B com topologia alinhada e sincronizada. |
| **Shared Paths** (`P`) | `Text` | Lista dos caminhos unificados na topologia final. |
| **Report** (`Rep`) | `Text` | Relatório de ramos alinhados e preenchidos. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
