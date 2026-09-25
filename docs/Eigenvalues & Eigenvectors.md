---
name: "Eigenvalues & Eigenvectors"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "MatrixEigen_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Eigenvalues & Eigenvectors (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `MatrixEigen_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Matrix A** (`A`) | `Generic` | Matriz quadrada de entrada A (N x N). Cada ramo {i} representa uma linha. |
| **Sort Descending** (`Sort`) | `Boolean` | Se verdadeiro, ordena os autovalores em ordem decrescente de magnitude. |
| **Max Iterations** (`Iter`) | `Integer` | Número máximo de iterações de convergência de Jacobi (Padrão: 100). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Eigenvalues** (`λ`) | `Number` | Lista ordenada dos autovalores calculados. |
| **Eigenvectors** (`V`) | `Number` | Matriz de autovetores ortonormais onde cada coluna j corresponde ao autovalor λj. |
| **Diagonal** (`D`) | `Number` | Matriz diagonal contendo os autovalores na diagonal principal. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
