---
name: "Matrix Multiply"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "MatrixMultiply_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Matrix Multiply (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `MatrixMultiply_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Matrix A** (`A`) | `Generic` | Primeira matriz de entrada A (M x K). Cada ramo {i} representa uma linha. |
| **Matrix B / Escalar** (`B`) | `Generic` | Segunda matriz de entrada B (K x N) ou valor escalar α. |
| **Mode** (`M`) | `Integer` | 0 = Produto Matricial Clássico (A x B)\n1 = Hadamard / Elemento a Elemento (A ⊙ B)\n2 = Multiplicação Escalar (α · A) |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Result** (`C`) | `Number` | Matriz resultante C. |
| **Dimensions** (`Dim`) | `Text` | Dimensões da matriz resultante (ex: 'M x N'). |
| **Valid** (`OK`) | `Boolean` | True se as dimensões foram compatíveis e o cálculo foi concluído com sucesso. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
