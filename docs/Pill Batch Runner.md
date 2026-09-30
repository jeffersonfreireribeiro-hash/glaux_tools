---
name: "Pill Batch Runner"
nickname: "Batch"
category: "Glaux Tools"
subcategory: "Explore"
class: "PillBatchRunner_Component"
file: "PillBatchRunner_Component.cs"
guid: "e2bd43c7-9b2d-40df-a17f-41e0607ebdb5"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, explore]
---

# 🧩 Pill Batch Runner (`Batch`)

**Categoria:** `Glaux Tools` ➔ `Explore`  
**Arquivo C#:** `PillBatchRunner_Component.cs`  
**Classe:** `PillBatchRunner_Component`  
**Pilha:** [Pilha 7 — Exploração de Design](stacks/07_Exploracao_de_Design.md)

---

## 📝 Descrição
Roda cada amostra do plano na definição e guarda os resultados:

1. ligue o Plan (Pill Sampler) e, em Results, os resultados a medir (qualquer saída que dependa dos controles);
2. ligue Run (toggle): cada amostra é aplicada nos controles, a definição recalcula e os resultados são lidos;
3. desligue Run (ou Esc) para pausar; ligar de novo retoma da próxima amostra. Reset descarta as execuções.

Com Experiment preenchido, cada execução é gravada no store (parâmetros, resultados, controles e tempo): o Pill Restore reaplica qualquer uma no canvas.
As execuções ficam salvas no .gh. Ao terminar ou pausar, os controles voltam aos valores de antes (Restore).

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Plan** (`P`) | `Generic` | Plano de amostras do Pill Sampler. |
| **Results** (`R`) | `Generic` | Resultados a registrar em cada execução (números viram colunas; todos os itens, em ordem de ramo). |
| **Run** (`Run`) | `Boolean` | Liga para rodar (ou retomar); desliga para pausar. Use um toggle. |
| **Reset** (`Rst`) | `Boolean` | Descarta as execuções guardadas (na borda False → True; use um botão). |
| **Store** (`S`) | `Generic` | Conexão do Pill DB Connect, ou caminho de um arquivo .glauxdb. Vazio = store padrão do projeto (pasta PillVault do .gh). |
| **Experiment** (`E`) | `Text` | Nome do experimento no store (vazio = não grava; as execuções continuam no .gh). |
| **Settle** (`ms`) | `Integer` | Espera entre execuções, em ms (para definições que precisam de um tempo extra). |
| **Restore** (`Rs`) | `Boolean` | Ao terminar ou pausar, volta os controles aos valores de antes do lote. |
| **Live** (`Lv`) | `Boolean` | Atualiza Inputs/Results a cada execução. Desligado, elas só aparecem com o lote parado: componentes pesados ligados às saídas (ex: Fast Pareto com milhares de linhas) não recalculam a cada passo. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Plan** (`P`) | `Generic` | Plano das execuções (ligue no Pill Sensitivity). |
| **Inputs** (`In`) | `Generic` | Um ramo {i} por execução com os valores aplicados (i = índice da amostra). |
| **Results** (`Res`) | `Number` | Um ramo {i} por execução com os resultados numéricos (NaN onde o item não era número). Pronto para Fast Pareto e Pill Sensitivity. |
| **Runs** (`N`) | `Integer` | Execuções concluídas. |
| **Progress** (`%`) | `Number` | Progresso de 0 a 1 (ligue num Transmitter para um widget progress do Pill Dashboard). |
| **Status** (`St`) | `Text` | Rodando, pausado (e por quê) ou concluído. |
| **Info** (`I`) | `Text` | Tempo por execução, tempo restante estimado e gravação no store. |

---

## 💡 Notas de Implementação & Uso
* **Fluxo completo, laço sem reentrância, retomada e integração com o store:** ver [Pilha 7 — Exploração de Design](stacks/07_Exploracao_de_Design.md).
* **Como o laço funciona:** cada amostra é aplicada num callback de `GH_Document.ScheduleSolution`, que roda antes da solução agendada; o componente lê os resultados na solução seguinte e agenda a próxima. O canvas continua respondendo e nenhuma solução é disparada de dentro de outra.
* **Segurança:** se um controle da exploração for mexido durante o lote, o lote pausa (a execução não é gravada com valores errados). Mudar o plano com execuções guardadas exige `Reset`.
* **Pill Restore:** com `Experiment` preenchido, cada execução guarda os controles aplicados; `Pill Restore` com `Ref = 'Experimento@N'` e `Apply Controls` reaplica aquela alternativa no canvas.
* **Lotes grandes:** desligue `Live` para que componentes pesados ligados às saídas (ex: Fast Pareto com milhares de linhas) só recalculem quando o lote parar.
* **Testes automatizados:** núcleo (espaço, amostragem, sensibilidade, lote) e adaptadores de controle cobertos em `tests/Glaux_Tools.Tests` (fora do Rhino); o laço no canvas precisa ser validado dentro do Grasshopper.
