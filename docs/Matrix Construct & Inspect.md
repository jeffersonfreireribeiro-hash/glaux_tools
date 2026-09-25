---
name: "Matrix Construct & Inspect"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "MatrixConstruct_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Matrix Construct & Inspect (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `MatrixConstruct_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data / Tree** (`D`) | `Generic` | Árvore de dados (onde cada ramo {i} é uma linha) ou lista plana de valores numéricos. |
| **Rows** (`M`) | `Integer` | Número de linhas (opcional se fornecido via árvore de dados). |
| **Cols** (`N`) | `Integer` | Número de colunas (opcional se fornecido via árvore de dados). |
| **Identity** (`I`) | `Boolean` | Se verdadeiro, gera uma Matriz Identidade I_N com dimensões N x N. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Matrix** (`M`) | `Number` | Matriz construída como DataTree onde cada ramo {i} é uma linha. |
| **Transpose** (`MT`) | `Number` | Matriz transposta (A^T). |
| **Dimensions** (`Dim`) | `Text` | Texto informativo das dimensões (ex: '4 x 4'). |
| **Trace** (`tr`) | `Number` | Traço da matriz (soma da diagonal principal A_ii). Válido para matrizes quadradas. |
| **Rows** (`R`) | `Integer` | Quantidade de linhas M. |
| **Cols** (`C`) | `Integer` | Quantidade de colunas N. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
