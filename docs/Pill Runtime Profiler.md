---
name: "Pill Runtime Profiler"
nickname: "PillProfiler"
category: "Glaux Tools"
subcategory: "Diagnostics"
class: "PillRuntimeProfiler_Component"
file: "PillRuntimeProfiler_Component.cs"
guid: "7da3dffc-951c-461d-92d5-5558ecb80f04"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, diagnostics]
---

# 🧩 Pill Runtime Profiler (`PillProfiler`)

**Categoria:** `Glaux Tools` ➔ `Diagnostics`  
**Arquivo C#:** `PillRuntimeProfiler_Component.cs`  
**Classe:** `PillRuntimeProfiler_Component`  
**Pilha:** [Pilha 4 — Performance & Diagnostics](stacks/04_Diagnostics.md)

---

## 📝 Descrição
Identifica gargalos da definição inteira sem instrumentar componentes: lê, no fim de cada solução, o tempo que o próprio Grasshopper mediu para cada componente.
- Ranking com último, média, mediana, p95, execuções e participação no tempo.
- Separa tempo dos componentes do tempo total da solução (a diferença é custo interno do Grasshopper: coleta de dados, conversões, preview).
- Soluções/minuto, memória (GC e processo), acertos/falhas do Pill Compute Cache e o custo medido do próprio profiler.
- 'Live' atualiza após cada solução (sem laço); 'Store' + 'Log' gravam a série no store (tipo 'metrics'). Mostra a solução anterior: o profiler calcula dentro da solução que está medindo.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Enabled** (`On`) | `Boolean` | Liga o profiler (desligado = nenhum evento inscrito, custo zero). |
| **Live** (`Lv`) | `Boolean` | Atualiza este componente após cada solução. |
| **Window** (`W`) | `Integer` | Quantidade de soluções na janela das estatísticas. |
| **Top** (`N`) | `Integer` | Quantos componentes listar. |
| **Sort** (`So`) | `Integer` | Ordenação: 0 = tempo total na janela, 1 = média, 2 = último, 3 = p95, 4 = execuções. |
| **Filter** (`F`) | `Text` | Só componentes cujo nome/categoria contém este texto. |
| **Reset** (`R`) | `Boolean` | Zera as estatísticas (borda False → True). |
| **Store** (`S`) | `Generic` | Store para gravar a série de métricas (opcional; ver Log). |
| **Log** (`L`) | `Boolean` | Grava as métricas no store a cada 'Log Every' soluções registradas. |
| **Log Every** (`LE`) | `Integer` | Intervalo de soluções entre gravações. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Components** (`C`) | `Text` | Componentes no ranking ('Apelido (Nome)'). |
| **Last ms** (`Last`) | `Number` | Tempo do último cálculo de cada componente (ms). |
| **Mean ms** (`Mean`) | `Number` | Média na janela (ms). |
| **Median ms** (`Med`) | `Number` | Mediana na janela (ms). |
| **P95 ms** (`P95`) | `Number` | Percentil 95 na janela (ms). |
| **Runs** (`Runs`) | `Integer` | Execuções registradas desde o início/reset. |
| **Share %** (`%`) | `Number` | Participação no tempo total dos componentes na janela. |
| **Ids** (`Id`) | `Text` | InstanceGuid de cada componente (para localizar no canvas). |
| **Solution ms** (`Sol`) | `Number` | Tempo total da última solução registrada (ms). |
| **Overhead ms** (`Ovh`) | `Number` | Custo do profiler na última solução (ms). |
| **Memory MB** (`Mem`) | `Number` | Memória gerenciada (GC) após a última solução (MB). |
| **Report** (`Rp`) | `Text` | Resumo legível: solução, componentes, GH, frequência, memória, cache e custo do profiler. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 4 — Performance & Diagnostics](stacks/04_Diagnostics.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
