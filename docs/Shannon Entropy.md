---
name: "Shannon Entropy"
nickname: "Entropy"
category: "Buraqueira Tools"
subcategory: "Statistics"
class: ""
file: "ShannonEntropy_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, statistics]
---

# 🧩 Shannon Entropy (`Entropy`)

**Categoria:** `Buraqueira Tools` ➔ `Statistics`  
**Arquivo C#:** `ShannonEntropy_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Calcula a Entropia de Shannon H(X) = -Σ p(x) log p(x), a incerteza máxima H_max, a entropia normalizada (eficiência de informação) e a perplexidade para conjuntos de dados ou vetores de probabilidade.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Values / Probs** (`X`) | `Number` | Conjunto de dados numéricos (amostra bruta, contagens ou vetor de probabilidades). |
| **Is Probability Vector** (`IsProb`) | `Boolean` | Se True, interpreta a entrada diretamente como probabilidades p_i. Se False, calcula frequências e probabilidades a partir dos dados. |
| **Log Base** (`Base`) | `Integer` | Base do logaritmo:\n0 = Base 2 (Bits / Shannons)\n1 = Base e (Nats / Log natural)\n2 = Base 10 (Hartleys / Dits) |
| **Bin Count** (`B`) | `Integer` | Número de classes/bins para dados numéricos contínuos (use 0 para valores únicos discretos). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Entropy H(X)** (`H`) | `Number` | Entropia de Shannon H(X) = -Σ p_i log(p_i). |
| **Max Entropy** (`H_max`) | `Number` | Entropia máxima teórica para K estados equiprováveis (H_max = log(K)). |
| **Normalized Entropy (η)** (`η`) | `Number` | Entropia normalizada / Eficiência de informação (H / H_max, de 0.0 a 1.0). |
| **Perplexity** (`Perp`) | `Number` | Perplexidade base^H(X): número efetivo de estados equiprováveis. |
| **Probabilities** (`P`) | `Number` | Vetor de probabilidades p_i normalizado utilizado no cálculo. |
| **Report** (`Desc`) | `Text` | Diagnóstico e interpretação da incerteza e diversidade da distribuição. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
