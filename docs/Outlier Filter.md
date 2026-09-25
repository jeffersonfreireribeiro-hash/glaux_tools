---
name: "Outlier Filter"
nickname: "Outliers"
category: "Buraqueira Tools"
subcategory: "Transform"
class: ""
file: "OutlierDetection_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, transform]
---

# 🧩 Outlier Filter (`Outliers`)

**Categoria:** `Buraqueira Tools` ➔ `Transform`  
**Arquivo C#:** `OutlierDetection_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Detecta, separa e filtra valores aberrantes (outliers) utilizando Intervalo Interquartil (IQR / Tukey), Z-Score (|z| > limite) ou Z-Score Modificado por MAD.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Values** (`V`) | `Number` | Conjunto de dados numéricos a inspecionar. |
| **Method** (`M`) | `Integer` | Método de detecção de Outliers:\n0 = IQR (Tukey's Fences: [Q1 - k*IQR, Q3 + k*IQR])\n1 = Z-Score (|z| > Limite)\n2 = Z-Score Modificado (Baseado na Mediana / MAD) |
| **Threshold** (`T`) | `Number` | Multiplicador / Limite de sensibilidade (padrão: 1.5 para IQR, 3.0 para Z-Score, 3.5 para MAD). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Clean Data** (`C`) | `Number` | Valores inliers (limpos, sem os outliers) mantendo a ordem original. |
| **Outliers** (`O`) | `Number` | Lista com apenas os valores aberrantes detectados. |
| **Clean Mask** (`M`) | `Boolean` | Máscara booleana do tamanho da lista original (True = Inlier, False = Outlier). |
| **Clean Indices** (`iC`) | `Integer` | Índices originais dos valores limpos. |
| **Outlier Indices** (`iO`) | `Integer` | Índices originais dos valores aberrantes. |
| **Lower Bound** (`L`) | `Number` | Limite inferior de corte aceitável. |
| **Upper Bound** (`U`) | `Number` | Limite superior de corte aceitável. |
| **Outlier Count** (`N_out`) | `Integer` | Total de outliers identificados. |
| **Report** (`Rep`) | `Text` | Resumo detalhado com percentis, quartis e taxa de anomalia. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
