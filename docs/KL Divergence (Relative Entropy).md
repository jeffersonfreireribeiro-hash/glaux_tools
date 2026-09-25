---
name: "KL Divergence (Relative Entropy)"
nickname: "KLDivergence"
category: "Buraqueira Tools"
subcategory: "Evaluation"
class: ""
file: "KullbackLeiblerDivergence_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, evaluation]
---

# 🧩 KL Divergence (Relative Entropy) (`KLDivergence`)

**Categoria:** `Buraqueira Tools` ➔ `Evaluation`  
**Arquivo C#:** `KullbackLeiblerDivergence_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Calcula a Divergência de Kullback-Leibler D_KL(P || Q) = Σ P(x) log(P(x)/Q(x)), a Divergência Simétrica de Jensen-Shannon (JSD), a Distância JS e a Entropia Cruzada entre duas distribuições de probabilidade.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Distribution P** (`P`) | `Number` | Distribuição de referência / probabilidade real P(x). |
| **Distribution Q** (`Q`) | `Number` | Distribuição aproximada / modelo simulado Q(x). Deve possuir o mesmo tamanho que P. |
| **Log Base** (`Base`) | `Integer` | Base do logaritmo:\n0 = Base 2 (Bits)\n1 = Base e (Nats)\n2 = Base 10 (Hartleys) |
| **Epsilon Smoothing** (`ε`) | `Number` | Suavização para evitar divisões por zero ou log(0) em probabilidades nulas (padrão: 1e-12). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **KL Divergence D_KL(P||Q)** (`D_KL`) | `Number` | Divergência de Kullback-Leibler direta D_KL(P || Q) em relação ao modelo Q. |
| **Reverse KL D_KL(Q||P)** (`D_KL_QP`) | `Number` | Divergência reversa / assimétrica D_KL(Q || P). |
| **Jensen-Shannon Div (JSD)** (`JSD`) | `Number` | Divergência simétrica e limitada de Jensen-Shannon (0.0 a 1.0 em bits). |
| **JS Distance** (`JSDist`) | `Number` | Métrica formal de distância entre distribuições (raiz quadrada da JSD). |
| **Cross Entropy H(P,Q)** (`H_PQ`) | `Number` | Entropia Cruzada H(P, Q) = H(P) + D_KL(P || Q). |
| **Report** (`Desc`) | `Text` | Diagnóstico comparativo e aderência entre as distribuições. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
