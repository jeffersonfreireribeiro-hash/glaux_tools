---
name: "Matrix Invert & Determinant"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "MatrixInvertDet_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Matrix Invert & Determinant (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `MatrixInvertDet_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Matrix** (`A`) | `Generic` | Matriz de entrada (M x N). Cada ramo {i} representa uma linha. |
| **Tolerance** (`Tol`) | `Number` | Tolerância para detecção de singularidade (Padrão: 1e-12). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Inverse** (`Inv`) | `Number` | Matriz inversa A^-1 (válida quando quadrada e não-singular). |
| **Determinant** (`det`) | `Number` | Determinante escalar det(A). |
| **PseudoInverse** (`PInv`) | `Number` | Pseudo-Inversa de Moore-Penrose (A^+), calculada para qualquer matriz retangular ou singular. |
| **Condition** (`Cond`) | `Text` | Diagnóstico de invertibilidade e condição numérica. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
