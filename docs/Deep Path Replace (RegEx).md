---
name: "Deep Path Replace (RegEx)"
nickname: "PathRegEx"
category: "Buraqueira Tools"
subcategory: "Tree"
class: ""
file: "DeepPathReplace_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, tree]
---

# 🧩 Deep Path Replace (RegEx) (`PathRegEx`)

**Categoria:** `Buraqueira Tools` ➔ `Tree`  
**Arquivo C#:** `DeepPathReplace_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Renomeia, reorganiza e modifica os caminhos GH_Path de uma árvore utilizando Expressões Regulares (RegEx), grupos de captura ($1, $2) e aritmética de índices.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Tree** (`T`) | `Generic` | Árvore de dados cujos caminhos serão renomeados ou reorganizados. |
| **Pattern** (`P`) | `Text` | Padrão de busca (RegEx ou template de caminho, ex.: \\{(\\d+);(\\d+)\\} ou {A;B;C}). |
| **Replacement** (`R`) | `Text` | Formato de substituição (ex.: {$2;$1}, {$1;$2+1}, {0;$1;$2}). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Tree** (`T`) | `Generic` | Árvore de dados resultante com a nova estrutura de caminhos. |
| **Path Map** (`Map`) | `Text` | Mapeamento dos caminhos originais para os novos caminhos. |
| **New Paths** (`P`) | `Text` | Lista dos novos caminhos gerados. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
