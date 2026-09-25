---
name: "Vector Similarity Search (K-NN & Embeddings)"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "VectorSimilarity_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Vector Similarity Search (K-NN & Embeddings) (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `VectorSimilarity_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Database Vectors** (`D`) | `Generic` | Árvore contendo os vetores da base de dados (onde cada ramo {a;b} representa um vetor numérico de características). |
| **Query Vectors** (`Q`) | `Generic` | Vetor(es) de consulta / alvo (ex: a curva acústica ideal ou indivíduo de referência). |
| **K Nearest** (`K`) | `Integer` | Quantidade de vizinhos mais semelhantes a retornar por consulta (Top-K). Padrão: 5. |
| **Metric** (`M`) | `Integer` | Métrica de comparação:\n0 = Similaridade de Cosseno (Cosine: 1.0 = idêntico em tendência/formato)\n1 = Distância Euclidiana (L2: 0 = idêntico em escala absoluta)\n2 = Distância Manhattan (L1)\n3 = Correlação de Pearson (formato da curva sem viés de média) |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Nearest Vectors** (`T`) | `Generic` | Árvore com os vetores dos K vizinhos mais próximos para cada consulta. |
| **Nearest Paths** (`Paths`) | `Generic` | Caminhos GH_Path originais na base de dados dos K vizinhos mais próximos. |
| **Scores / Distances** (`S`) | `Number` | Pontuação de similaridade (Cosseno/Pearson) ou Distância (L2/L1). |
| **Rank** (`R`) | `Integer` | Posição no ranking (1 = mais parecido, 2 = segundo mais parecido, etc.). |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
