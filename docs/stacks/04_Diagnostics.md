# Pilha 4 — Performance & Diagnostics

## Objetivo

Encontrar gargalos de uma definição (e do próprio Glaux) sem que a medição altere o que está sendo medido:

- quanto tempo cada componente gasta, com média, mediana, percentis e frequência;
- quanto da solução é trabalho dos componentes e quanto é custo interno do Grasshopper;
- memória, acertos/falhas de cache, volume de dados;
- quanto o próprio profiler custa;
- séries históricas no store para comparar versões e configurações.

## Arquitetura

**Sem instrumentação**: o Grasshopper já mede o tempo de cada componente (`ProcessorTime`, cronômetro em `GH_Component.ComputeData`, zerado ao expirar). O profiler só **lê** esses tempos no fim da solução.

```
GH_Document.SolutionStart ──► ProfilerHost: marca quem está expirado; contadores do cache
GH_Document.SolutionEnd ────► ProfilerHost: lê ProcessorTime + itens de saída de cada objeto
                                  │   (custo desta leitura é medido e reportado)
                                  ▼
                             SolutionProfiler (puro, testável)
                               - quem calculou nesta solução
                               - RollingStats por componente e por solução
                               - soluções só do profiler são ignoradas (sem laço)
                                  ▼
                        Pill Runtime Profiler (componente) ──► Store (tipo "metrics", opcional)
```

| Classe | Namespace | Papel |
|---|---|---|
| `RollingStats` | `Diagnostics` | Janela móvel: média, mediana, p90/p95/p99 (interpolação linear), mín, máx, desvio. |
| `SolutionProfiler` | `Diagnostics` | Agregação por componente/solução, ranking, frequência. |
| `ProfilerHost` | raiz | Eventos do documento; existe só enquanto houver um profiler ativo (custo zero sem profiler). |

### Como decide "quem calculou nesta solução"

Um objeto conta se estava **expirado no início** da solução ou se o **tempo medido mudou**, e se o tempo é maior que zero (tempo zero = expirado mas não calculou: bloqueado, desativado, entrada obrigatória vazia). A primeira leitura de um objeto é só linha de base.

### Componente × solução

- **Tempo dos componentes** = soma dos `ProcessorTime` de quem calculou (inclui os parâmetros de entrada de cada componente).
- **Tempo da solução** = duração reportada pelo Grasshopper (`GH_SolutionEventArgs.Duration`).
- **Diferença** = custo interno do Grasshopper (coleta de dados, conversões, preview, eventos).

### Atualização "Live" sem laço

Depois de uma solução registrada, o profiler agenda uma solução curta que recalcula só ele (e o que depende dele). Nessa solução, tudo que calculou está no "fecho" do profiler → a solução é ignorada → nenhum novo agendamento. Consequência: o profiler mostra a **solução anterior** (ele calcula dentro da solução que mede), e o tempo dele e dos componentes ligados à saída dele não entra no ranking.

### Custo do próprio profiler

Medido a cada solução (marcação no início + leitura no fim) e reportado em ms e em % do tempo da solução. A agregação em si custa ≈ 0,03 ms (100 objetos), 0,28 ms (1 000) e 0,55 ms (5 000) por solução (`DiagnosticsTests.Profiler_RecordCost_ScalesWithDocumentSize`, .NET 8, Linux). A leitura dos objetos no Grasshopper soma a isso e aparece em "Custo do profiler" no Report. Desligado (`Enabled = False`), nenhum evento fica inscrito.

## Componente (painel **Diagnostics**)

### Pill Runtime Profiler (`PillProfiler`)

| Entrada | Descrição |
|---|---|
| Enabled | liga/desliga (desligado = sem eventos) |
| Live | atualiza após cada solução |
| Window | soluções na janela das estatísticas (padrão 50) |
| Top | quantos componentes listar |
| Sort | 0 total na janela, 1 média, 2 último, 3 p95, 4 execuções |
| Filter | texto no nome/categoria |
| Reset | zera (borda) |
| Store, Log, Log Every | grava a série no store |

| Saída | Descrição |
|---|---|
| Components, Last ms, Mean ms, Median ms, P95 ms, Runs, Share %, Ids | ranking |
| Solution ms, Overhead ms, Memory MB | números para gráficos |
| Report | solução × componentes × Grasshopper, janela, frequência, memória, Pill Compute Cache, custo do profiler |

**Por que um componente só:** "Performance Monitor", "Runtime Profiler" e "Metrics Logger" seriam três componentes quase iguais lendo os mesmos dados. O monitor de um componente específico é o ranking com `Filter`; o logger é a entrada `Store`/`Log`. O **Data Generation Stopwatch** existente continua para medir o trecho a montante de um fio.

### Série de métricas no store

Entrada tipo `metrics`, chave `profile:<documento>`:

| Árvore | Colunas (um ramo por linha) |
|---|---|
| `components` | name, last_ms, mean_ms, median_ms, p95_ms, runs, output_items |
| `solution` | solution_ms, components_ms, grasshopper_ms, profiler_ms, gc_mb, working_set_mb, cache_hits, cache_misses |

Metadados: nomes das colunas, janela e ambiente (versões do Glaux/Rhino/Grasshopper). Leia com **Pill History** (Kind `metrics`, Tree `solution`) → Chart Line, ou compare execuções com **Pill Compare**. Útil para acompanhar a simulação acústica e, no futuro, GPU.

## Tipos suportados

Qualquer objeto ativo do documento (componentes e parâmetros soltos). Tempos em ms (double).

## Limitações

- O Grasshopper arredonda a resolução do cronômetro ao tick do sistema; componentes muito rápidos podem aparecer com 0 ms e não contam como execução.
- Componentes dentro de clusters aparecem como o cluster (tempo total).
- `ProcessorTime` não inclui o preview no viewport (fica no tempo do Grasshopper).
- Memória do processo (`Environment.WorkingSet`) inclui o Rhino inteiro.

## Persistência

Estatísticas ficam em memória enquanto o documento está aberto; a série histórica vai para o store se `Log` estiver ligado.

## Compatibilidade

Grasshopper 1 / Rhino 8; usa apenas API pública (`SolutionStart/End`, `ProcessorTime`, `Phase`).

## Testes

`tests/Glaux_Tools.Tests/DiagnosticsTests.cs`:

- `RollingStats` contra implementação de referência (média, mediana, percentis, desvio, janela, NaN) e casos-limite de percentil;
- detecção de quem calculou (linha de base, tempo mudou, expirado, tempo zero), tempo atribuído × não atribuído, solução só do profiler ignorada, ranking/filtro/top, frequência, reset;
- **medição instrumentada × referência**: cargas reais cronometradas de forma independente alimentam o profiler; média, mediana, p95 e último batem com a referência; custo do profiler ≈ 1 % do trabalho medido (limite do teste: 5 %);
- custo de agregação para 100, 1 000 e 5 000 objetos.
