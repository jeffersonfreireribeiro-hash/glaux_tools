---
name: "Tree Structural Diff"
nickname: "TreeDiff"
category: "Buraqueira Tools"
subcategory: "Tree"
class: ""
file: "TreeStructuralDiff_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, tree]
---

# 🧩 Tree Structural Diff (`TreeDiff`)

**Categoria:** `Buraqueira Tools` ➔ `Tree`  
**Arquivo C#:** `TreeStructuralDiff_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Compara duas árvores de dados (A e B) e diagnostica discrepâncias: ramos exclusivos de A, ramos exclusivos de B, contagens divergentes e equivalência topológica.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Tree A** (`A`) | `Generic` | Primeira árvore de dados (A) para comparação. |
| **Tree B** (`B`) | `Generic` | Segunda árvore de dados (B) para comparação. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Exclusive to A** (`A_only`) | `Text` | Caminhos que existem somente na Árvore A. |
| **Exclusive to B** (`B_only`) | `Text` | Caminhos que existem somente na Árvore B. |
| **Shared Paths** (`Shared`) | `Text` | Caminhos que existem em ambas as árvores. |
| **Divergent Count Paths** (`DiffCnt`) | `Text` | Caminhos compartilhados onde a quantidade de itens difere entre A e B. |
| **Count Delta (A - B)** (`ΔCount`) | `Integer` | Diferença numérica de itens (Count A - Count B) para os caminhos compartilhados. |
| **Is Identical Topology?** (`Identical?`) | `Boolean` | True se as árvores possuem exatamente os mesmos caminhos e contagens por ramo. |
| **Report** (`Rep`) | `Text` | Relatório de diagnóstico estrutural e topológico. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
