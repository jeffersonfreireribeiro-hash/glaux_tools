---
name: "Pill Sensitivity"
nickname: "Sensitivity"
category: "Glaux Tools"
subcategory: "Explore"
class: "PillSensitivity_Component"
file: "PillSensitivity_Component.cs"
guid: "94dc089b-742e-41cd-9816-8438df246131"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, explore]
---

# 🧩 Pill Sensitivity (`Sensitivity`)

**Categoria:** `Glaux Tools` ➔ `Explore`  
**Arquivo C#:** `PillSensitivity_Component.cs`  
**Classe:** `PillSensitivity_Component`  
**Pilha:** [Pilha 7 — Exploração de Design](stacks/07_Exploracao_de_Design.md)

---

## 📝 Descrição
Mostra quais variáveis mais pesam em cada resultado, a partir do Plan e dos Results do Pill Batch Runner:

- correlation (planos lhs, sobol, random, grid): Pearson (com p-valor), Spearman e coeficientes de regressão padronizados (SRC). Importância = |SRC|; o R² diz se um modelo linear explica o resultado;
- morris (plano morris): μ* (importância), μ (sinal) e σ (não linearidade ou interação);
- sobol (plano saltelli): índices S1 (efeito sozinho) e ST (efeito total, com interações). Importância = ST.

Com Method vazio, o método segue o plano. Funciona com execuções parciais (lote pausado).

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Plan** (`P`) | `Generic` | Plano executado (saída Plan do Pill Batch Runner ou do Pill Sampler). |
| **Results** (`R`) | `Generic` | Um ramo por execução; o último índice do caminho é a amostra (saída Results do Batch Runner). |
| **Names** (`N`) | `Text` | Nome de cada resultado (colunas de Results), para o ranking. |
| **Method** (`M`) | `Text` | auto (padrão), correlation, morris ou sobol. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Variables** (`V`) | `Text` | Variáveis, na ordem das colunas de Importance. |
| **Importance** (`Imp`) | `Number` | Um ramo {r} por resultado: importância de cada variável (\|SRC\|, μ* ou ST). |
| **Ranking** (`Rk`) | `Text` | Uma linha por resultado, da variável mais para a menos importante. |
| **Details** (`D`) | `Number` | Ramo {r; variável}: todas as medidas, na ordem de Measures. |
| **Measures** (`Ms`) | `Text` | Nome das medidas em Details. |
| **Info** (`I`) | `Text` | Método, execuções usadas, ajuste (R² ou soma dos S1) e observações. |

---

## 💡 Notas de Implementação & Uso
* **Métodos, leitura dos índices e limitações:** ver [Pilha 7 — Exploração de Design](stacks/07_Exploracao_de_Design.md).
* **Como ler:** correlação/SRC supõe relação aproximadamente linear (confira o R²); Morris separa efeito médio (μ*) de não linearidade/interação (σ); Sobol diz quanto da variância cada variável explica sozinha (S1) e com interações (ST).
* **Execuções parciais:** funciona com o lote pausado; Morris usa só os passos completos e Sobol só as linhas A, AB₁…ABₖ, B completas.
* **Testes automatizados:** núcleo (espaço, amostragem, sensibilidade, lote) e adaptadores de controle cobertos em `tests/Glaux_Tools.Tests` (fora do Rhino); o laço no canvas precisa ser validado dentro do Grasshopper.
