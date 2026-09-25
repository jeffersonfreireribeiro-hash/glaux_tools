---
name: "Linear System Solver"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "MatrixSolver_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Linear System Solver (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `MatrixSolver_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Matrix A** (`A`) | `Generic` | Matriz de coeficientes A (M x N). |
| **Vector b** (`b`) | `Generic` | Vetor ou coluna de termos independentes b (M valores). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Solution x** (`x`) | `Number` | Vetor solução x tal que A · x ≈ b. |
| **Residual** (`r`) | `Number` | Resíduo euclidiano ||A·x - b||2 (erro da solução). |
| **Solved** (`OK`) | `Boolean` | True se o sistema foi resolvido com sucesso. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
