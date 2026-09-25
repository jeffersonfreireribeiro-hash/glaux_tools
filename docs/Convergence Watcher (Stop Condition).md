---
name: "Convergence Watcher (Stop Condition)"
nickname: "Converge"
category: "Buraqueira Tools"
subcategory: "Automation"
class: ""
file: "ConvergenceWatcher_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, automation]
---

# 🧩 Convergence Watcher (Stop Condition) (`Converge`)

**Categoria:** `Buraqueira Tools` ➔ `Automation`  
**Arquivo C#:** `ConvergenceWatcher_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Monitora a taxa de variação (|Δx| / |x_t-1| < ε) em processos iterativos, otimizações ou solvers e emite um sinal booleano de parada (Stop) após N passos consecutivos estáveis.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Current Values** (`X`) | `Number` | Valor escalar ou lista de parâmetros da iteração atual (x_t). |
| **Epsilon (Tolerance)** (`ε`) | `Number` | Tolerância máxima de variação para considerar o passo convergido (padrão: 1e-4). |
| **Consecutive Steps** (`N`) | `Integer` | Número de iterações consecutivas abaixo de ε necessárias para declarar convergência (padrão: 3). |
| **Metric Mode** (`M`) | `Integer` | Métrica de convergência:\n0 = Variação Relativa Média (|Δx| / |x_t-1|)\n1 = Variação Absoluta Média (|Δx|)\n2 = Variação Relativa Máxima (Max |Δx_i| / |x_t-1,i|)\n3 = RMS Delta (Raiz do erro quadrático da variação) |
| **Reset** (`R`) | `Boolean` | Reseta o histórico de iterações e o contador de convergência. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Stop / Converged** (`Stop`) | `Boolean` | True quando o processo atinge a estabilidade exigida (sinal para interromper o loop). |
| **Current Delta (Δ)** (`Δ`) | `Number` | Variação calculada no passo atual em relação ao passo anterior. |
| **Stable Streak** (`Streak`) | `Integer` | Quantidade de passos consecutivos atuais abaixo da tolerância ε. |
| **Iteration Count** (`Iter`) | `Integer` | Total de ciclos/iterações processados. |
| **Delta History** (`Hist`) | `Number` | Histórico das variações Δ das últimas iterações. |
| **Report** (`Rep`) | `Text` | Diagnóstico de convergência detalhado. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
