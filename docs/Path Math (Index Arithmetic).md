---
name: "Path Math (Index Arithmetic)"
nickname: "PathMath"
category: "Buraqueira Tools"
subcategory: "Tree"
class: ""
file: "PathMath_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, tree]
---

# 🧩 Path Math (Index Arithmetic) (`PathMath`)

**Categoria:** `Buraqueira Tools` ➔ `Tree`  
**Arquivo C#:** `PathMath_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Modifica diretamente a estrutura numérica dos caminhos GH_Path: soma offsets a níveis específicos ({0; i+1}), inverte hierarquias, rotaciona níveis ou colapsa profundidades intermediárias.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Tree** (`T`) | `Generic` | Árvore de dados a ter seus caminhos manipulados. |
| **Operation** (`Op`) | `Integer` | Operação aritmética no Path:\n0 = Somar Offset no nível L ({..., i + V, ...})\n1 = Inverter Hierarquia ({A; B; C} -> {C; B; A})\n2 = Rotacionar Níveis / Shift ({A; B; C} -> {B; C; A})\n3 = Colapsar / Remover Nível L (remove o índice do nível)\n4 = Inserir Nível (insere novo índice de valor V na posição L)\n5 = Truncar Profundidade (manter apenas os primeiros V níveis) |
| **Target Level** (`L`) | `Integer` | Profundidade / Nível alvo (0 = primeiro nível, -1 = último nível, -2 = penúltimo nível). Padrão: -1. |
| **Value / Offset** (`V`) | `Integer` | Valor numérico para soma, inserção ou contagem de níveis (padrão: 1). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Modified Tree** (`T`) | `Generic` | Árvore de dados com os novos caminhos calculados. |
| **Old Paths** (`P_old`) | `Text` | Lista dos caminhos originais da árvore. |
| **New Paths** (`P_new`) | `Text` | Lista dos novos caminhos gerados. |
| **Path Map** (`Map`) | `Text` | Mapeamento das transformações {antigo} -> {novo}. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
