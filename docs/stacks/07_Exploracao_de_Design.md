# Pilha 7 — Exploração de Design

## Objetivo

Gerar alternativas de projeto de forma sistemática, rodar cada uma na definição, guardar entradas e resultados e
mostrar quais parâmetros mais pesam em cada resultado. A pilha junta num fluxo único o que as pilhas 1 a 6 já
oferecem separadamente (controles, banco local, histórico, profiler, Pareto e painel).

```
Design Space ──► Sampler ──► Batch Runner ──► Sensitivity
 (variáveis)     (amostras)   (roda e grava)    (o que pesa)
                                  │
                                  ├──► Fast Pareto (melhores)
                                  ├──► Pill DB Query / Pill History (store)
                                  └──► Pill Restore (reaplica uma execução no canvas)
```

## Infraestrutura: existente → reutilizada → nova

| Existente | Reutilizado | Novo (e por quê) |
|---|---|---|
| **`ControlStateService` / `ControlState` / `ControlCompatibility`** (Project Vault): captura e aplica sliders, toggles, value lists, sliders do Slider Pool e controles do Pill Dashboard numa única solução | Identidade dos controles (`Id`, `Kind`, faixa), leitura dos valores atuais e escrita sem uma solução por controle | `ApplyNow`: a aplicação existente sempre dispara `NewSolution`; o Batch Runner precisa aplicar **dentro** de um callback agendado, antes da solução que o próprio Grasshopper vai rodar |
| **Pill Slider Pool** (com modo Wallacei) | Sliders do pool como variáveis (`PublishSingleSlider` publica no PillHub) | — |
| **Pill Dashboard** (`IPillControlStateProvider`) | Sliders, toggles e dropdowns do painel como variáveis; passo e opções do `WidgetSpec` | — |
| **Pill Experiment Logger** / store `.glauxdb` | `SnapshotCodec.BuildDraft` (tipo `experiment`), gravação em lote (`AppendBatch`), ambiente | O Batch Runner grava cada execução com os **controles aplicados**: assim o Pill Restore reaplica qualquer execução no canvas |
| **Pill Restore / History / DB Query** | Sem mudança: leem as execuções gravadas pelo Batch Runner | — |
| **Runtime Profiler** / Data Timer | — | Tempo de cada execução medido pelo próprio Batch Runner (da aplicação da amostra até o resultado), usado para ETA e gravado como `runtime_ms` |
| **Fast Pareto** | Aceita a saída do Batch Runner direto (um ramo `{i}` por execução) | — |
| **Covariance & Correlation**, **Linear Regression**, **Matrix Solver** | Mesmas fórmulas (Pearson, Spearman com empates, mínimos quadrados); `SpecialFunctions.StudentTCdf` para o p-valor | Rotinas vetorizadas por variável × resultado (os componentes atuais tratam um par X/Y por vez, com as rotinas privadas) |
| **Convergence Watcher**, **Pulse Timer**, **Iterative Accumulator** | Não necessários: o laço do Batch Runner usa `GH_Document.ScheduleSolution` com callback (roda antes da próxima solução, mesmo que outra solução se antecipe) | Máquina de estados de lote (`BatchSession`): retomada, pausa, conclusão, ETA, persistência no `.gh` |
| **Pill Preset Vault** (fios estilo Galapagos) | — | Fora do MVP: vínculo sem fio por menu (ver Próximos passos) |
| Amostragem, sensibilidade | **Não existiam** (busca por Latin Hypercube, Sobol, Morris, Saltelli, surrogate: nada no código) | Núcleo `Buraqueira_Tools.Explore` |
