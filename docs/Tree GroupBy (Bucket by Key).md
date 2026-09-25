---
name: "Tree GroupBy (Bucket by Key)"
nickname: "GroupBy"
category: "Buraqueira Tools"
subcategory: "Tree"
class: ""
file: "TreeGroupBy_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, tree]
---

# 🧩 Tree GroupBy (Bucket by Key) (`GroupBy`)

**Categoria:** `Buraqueira Tools` ➔ `Tree`  
**Arquivo C#:** `TreeGroupBy_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Agrupa itens de uma lista ou árvore em novos ramos com base em uma chave (ex.: orientação solar, tipo construtivo, área, camada). Substitui todo o processo manual de sets, map e split.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Elementos a serem agrupados (geometria, números, texto, etc.). |
| **Keys** (`K`) | `Generic` | Chaves de agrupamento correspondentes 1:1 com os dados. |
| **Key Tolerance** (`Tol`) | `Number` | Tolerância para chaves numéricas contínuas (padrão: 1e-4). |
| **Per Branch** (`B`) | `Boolean` | Se True, agrupa individualmente por ramo gerando sub-ramos {path; key_idx}. Se False, agrupa globalmente. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Grouped Tree** (`T`) | `Generic` | Árvore de dados com os elementos organizados em ramos por chave. |
| **Unique Keys** (`K`) | `Generic` | Chaves únicas correspondentes a cada ramo da árvore de saída. |
| **Counts** (`C`) | `Integer` | Quantidade de elementos em cada grupo. |
| **Original Indices** (`i`) | `Integer` | Índices originais dos itens em cada grupo. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
