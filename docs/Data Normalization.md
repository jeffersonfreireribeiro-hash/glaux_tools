---
name: "Data Normalization"
nickname: "Norm"
category: "Buraqueira Tools"
subcategory: "Transform"
class: ""
file: "DataNormalization_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, transform]
---

# 🧩 Data Normalization (`Norm`)

**Categoria:** `Buraqueira Tools` ➔ `Transform`  
**Arquivo C#:** `DataNormalization_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Normaliza e padroniza conjuntos de dados numéricos usando Min-Max customizável, Z-Score ((x - μ) / σ), Sigmóide Logística, Logarítmica ou Softmax.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Values** (`V`) | `Number` | Conjunto de dados numéricos de entrada. |
| **Method** (`M`) | `Integer` | Método de normalização:\n0 = Min-Max personalizado [min, max]\n1 = Z-Score ((x - μ) / σ)\n2 = Sigmóide Logística (1 / (1 + e^-z))\n3 = Logarítmica (sign(x) * ln(1 + |x|))\n4 = Softmax (probabilidades que somam 1.0) |
| **Target Min** (`min`) | `Number` | Limite inferior para o método Min-Max (padrão 0.0). |
| **Target Max** (`max`) | `Number` | Limite superior para o método Min-Max (padrão 1.0). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Normalized** (`N`) | `Number` | Valores normalizados no mesmo formato e ordem da entrada. |
| **Param 1** (`P1`) | `Number` | Primeiro parâmetro do modelo (ex.: Mínimo original ou Média μ). |
| **Param 2** (`P2`) | `Number` | Segundo parâmetro do modelo (ex.: Máximo original ou Desvio Padrão σ). |
| **Formula / Summary** (`Desc`) | `Text` | Descrição do método e parâmetros matemáticos aplicados. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
