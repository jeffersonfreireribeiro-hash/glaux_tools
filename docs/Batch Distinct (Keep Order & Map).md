---
name: "Batch Distinct (Keep Order & Map)"
nickname: "Distinct"
category: "Buraqueira Tools"
subcategory: "Tree"
class: ""
file: "BatchDistinct_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, tree]
---

# 🧩 Batch Distinct (Keep Order & Map) (`Distinct`)

**Categoria:** `Buraqueira Tools` ➔ `Tree`  
**Arquivo C#:** `BatchDistinct_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Extrai elementos únicos preservando a ordem original de aparição, retornando a contagem de frequências, índices da primeira ocorrência e o mapa de índices mapeados.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Lista ou Árvore de dados para filtrar elementos únicos. |
| **Tolerance** (`Tol`) | `Number` | Tolerância para igualdade numérica e de coordenadas (padrão: 1e-6). |
| **Per Branch** (`B`) | `Boolean` | Se True, extrai únicos individualmente por ramo. Se False, extrai sobre toda a árvore globalmente. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Unique Values** (`U`) | `Generic` | Elementos únicos preservando a ordem de primeira aparição. |
| **Counts** (`C`) | `Integer` | Frequência / quantidade de vezes que cada elemento único apareceu. |
| **First Indices** (`iFirst`) | `Integer` | Índice de primeira ocorrência de cada elemento único. |
| **Index Map** (`Map`) | `Integer` | Mapeamento para cada item original indicando o índice do seu correspondente único (0..K-1). |
| **Duplicates** (`Dup`) | `Generic` | Lista de elementos que se repetiram mais de uma vez. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
