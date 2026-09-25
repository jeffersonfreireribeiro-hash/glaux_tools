---
name: "Data Group Finder (Cluster & Runs)"
nickname: "GroupFinder"
category: "Buraqueira Tools"
subcategory: "Tree"
class: ""
file: "DataGroupFinder_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, tree]
---

# 🧩 Data Group Finder (Cluster & Runs) (`GroupFinder`)

**Categoria:** `Buraqueira Tools` ➔ `Tree`  
**Arquivo C#:** `DataGroupFinder_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Encontra e segmenta automaticamente grupos de dados através de múltiplos métodos: ilhas de repetição consecutivas (runs), clusters de proximidade contínua, faixas de limiares (thresholds) ou quantis. Retorna grupos em DataTree com métricas e IDs 1:1.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Lista ou árvore de dados a serem agrupados. |
| **Mode** (`M`) | `Integer` | Modo de detecção: 0=Consecutive Runs (ilhas idênticas/estáveis contíguas), 1=Proximity Clusters (tolerância contínua entre vizinhos), 2=Threshold Bins (faixas por limiares), 3=Equal Quantiles (K grupos balanceados). |
| **Tolerance / Step** (`Tol`) | `Number` | Tolerância para runs e clusters por proximidade (padrão: 1.0). |
| **Thresholds / K** (`Thresh`) | `Number` | Lista de valores limiares para o Modo 2 ou quantidade K de grupos para o Modo 3. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Groups** (`G`) | `Generic` | Árvore de dados estruturada em {path; group_id} contendo os elementos de cada grupo. |
| **Group Indices** (`i`) | `Integer` | Árvore com os índices originais dos elementos em cada grupo. |
| **Group Means** (`Avg`) | `Number` | Média numérica dos elementos de cada grupo. |
| **Group Counts** (`N`) | `Integer` | Quantidade de elementos em cada grupo. |
| **Item Group IDs** (`ID`) | `Integer` | Lista ou árvore 1:1 com o ID numérico do grupo atribuído a cada item original. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
