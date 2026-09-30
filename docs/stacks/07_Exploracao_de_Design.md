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

## Arquitetura

| Camada | Classe | Namespace | Papel |
|---|---|---|---|
| Espaço de projeto | `DesignVariable`, `DesignSpace`, `DesignRangeOverride` | `Explore` | Variáveis ligadas aos controles pelo Id do Project Vault. Toda amostra é uma coordenada u ∈ [0, 1] convertida aqui em valor; cada valor discreto (passo ou nível) ocupa uma faixa igual de u. Identidade (hash) que não depende dos valores atuais. |
| Amostragem | `DesignSampler`, `SamplerOptions`, `SamplePlan`, `SobolSequence`, `SplitMix64` | `Explore` | Grade, aleatório, Latin Hypercube (maximin), Sobol (Joe–Kuo, 256 dimensões, deslocamento digital), Morris, Saltelli. Determinístico pela semente. |
| Sensibilidade | `SensitivityAnalysis`, `SensitivityResult` | `Explore` | Pearson (p-valor), Spearman, SRC + R²; Morris (μ*, μ, σ); Sobol (S1, ST). Aceita execuções faltando. |
| Lote | `BatchSession` | `Explore` | Qual plano, quais amostras têm resultado, próxima amostra, pausa/retomada, ETA, persistência em texto no `.gh`. Recusa misturar planos. |
| Grasshopper | `DesignControls`, `GH_DesignSpaceGoo`, `GH_SamplePlanGoo` | raiz | Descobre variáveis (sliders, toggles, value lists, Slider Pool, Dashboard), converte valor ↔ `ControlState` no formato do controle, lê o que o controle aceitou. |
| Componentes | `PillDesignSpace_Component`, `PillSampler_Component`, `PillBatchRunner_Component`, `PillSensitivity_Component` | raiz | Só leem entradas, chamam o núcleo e escrevem saídas; o Batch Runner também conduz o laço. |

O núcleo não usa nada do canvas e é todo testado fora do Rhino.

## Fluxo de uso

1. **Pill Design Space**: ligue em `Controls` os sliders, toggles e value lists a variar (ou um Pill Slider Pool / Pill Dashboard inteiro), ou escreva os nomes em `Names`. Ajuste faixas em `Ranges`, por exemplo:
   ```
   Largura | min=4 | max=12 | step=0.5
   Material | levels=Concreto;Madeira;Vidro
   Rotação | off
   ```
2. **Pill Sampler**: escolha o método e a quantidade (ex: `lhs`, `64`). A saída `Samples` mostra as alternativas antes de rodar qualquer coisa.
3. **Pill Batch Runner**: ligue o `Plan`; em `Results`, as saídas a medir (ex: RT60, custo, área). Preencha `Experiment` para gravar no store. Ligue `Run`.
4. Acompanhe `Progress` e `Status` (ou mande o `Progress` por um Transmitter para um widget `progress` do Pill Dashboard). `Esc` ou desligar `Run` pausa; ligar de novo retoma.
5. **Resultados**: `Results` do Batch Runner vai direto no **Fast Pareto** (um ramo por execução) e no **Pill Sensitivity**. Para ver uma alternativa no canvas: **Pill Restore** com `Ref = 'Experimento@N'` e `Apply Controls`.

## Componentes (painel **Explore**)

| Componente | Entradas principais | Saídas principais |
|---|---|---|
| [Pill Design Space](../Pill%20Design%20Space.md) (`DesignSpace`) | Controls (fios), Names, Ranges | Space, Names, Ranges, Count |
| [Pill Sampler](../Pill%20Sampler.md) (`Sampler`) | Space, Method, Count, Seed, Options | Plan, Samples, Unit, Count |
| [Pill Batch Runner](../Pill%20Batch%20Runner.md) (`Batch`) | Plan, Results, Run, Reset, Store, Experiment, Settle, Restore, Live | Plan, Inputs, Results, Runs, Progress, Status |
| [Pill Sensitivity](../Pill%20Sensitivity.md) (`Sensitivity`) | Plan, Results, Names, Method | Variables, Importance, Ranking, Details, Measures |

## Métodos de amostragem

| Método | Quando usar | Execuções | Observações |
|---|---|---|---|
| `lhs` (padrão) | Explorar bem o espaço com poucas execuções | N | Uma amostra por faixa 1/N em cada variável; com N ≤ 256, o melhor de 20 desenhos pelo critério maximin (amostras menos amontoadas). `centered` põe cada amostra no centro da faixa. |
| `sobol` | Cobertura uniforme, bons resultados com N crescente | N (use potência de 2) | Sequência quase aleatória de Joe–Kuo; deslocamento digital pela semente (`scramble=false` desliga). |
| `random` | Referência / Monte Carlo | N | — |
| `grid` | Poucas variáveis, todas as combinações | L^k | L = ⌊N^(1/k)⌋ níveis por variável contínua (ou `levels=L`); escolhas usam todos os níveis. |
| `morris` | Triagem: descobrir quais variáveis importam com poucas execuções | r × (k + 1) | r = Count trajetórias; `levels=p` (par, padrão 4); cada passo muda uma variável por Δ = p/(2(p−1)). |
| `saltelli` | Índices de Sobol (quanto da variância cada variável explica) | N × (k + 2) | N base = Count (potência de 2); caro, mas é a medida mais completa. |

## Sensibilidade

| Método | Plano | Importância | Outras medidas | Leitura |
|---|---|---|---|---|
| `correlation` | qualquer (lhs, sobol, random, grid) | \|SRC\| | Pearson (com p-valor), Spearman, R² | SRC = coeficiente da regressão linear com variáveis padronizadas. Só vale se o R² for alto (≥ 0,7): senão há não linearidade ou interação, e o componente avisa. |
| `morris` | morris | μ* (média de \|EE\|) | μ (sinal), σ | σ alto em relação a μ* = efeito não linear ou dependente de outras variáveis. |
| `sobol` | saltelli | ST (efeito total) | S1 (efeito sozinho), ΣS1 | ST − S1 = parte que vem de interações. ΣS1 bem abaixo de 1 = interações importantes. Estimadores de Saltelli (2010) para S1 e de Jansen (1999) para ST. |

Execuções com resultado não numérico viram `NaN` e são descartadas só onde afetam. O `Method = auto` segue o plano.

## O laço do Batch Runner

```
Run ↑ ──► captura os controles (para restaurar) ──► agenda a amostra 0
                                                        │
      ┌──────────────── ScheduleSolution(callback) ◄────┘
      ▼
callback (antes da solução): aplica a amostra i nos controles, lê o que eles aceitaram, expira o Batch Runner
      │
      ▼
solução: a definição recalcula; o Batch Runner lê Results → grava a execução i → agenda i + 1
```

- **Sem reentrância:** a amostra é aplicada no callback de `GH_Document.ScheduleSolution`, que o Grasshopper chama **antes** da solução agendada (e também antes de qualquer outra solução que se antecipe). Nenhuma solução é disparada de dentro de outra e o canvas continua respondendo entre execuções.
- **Um valor por controle, uma solução por amostra:** os controles são aplicados com `ControlStateService.ApplyNow` (mesmo código do Pill Restore), sem uma solução por slider.
- **O que o controle aceitou:** depois de aplicar, os valores são lidos de volta; se o controle limitou ou arredondou (faixa ou precisão), a execução guarda o valor real e o componente avisa.

| Situação | Comportamento |
|---|---|
| `Run` desligado / `Esc` | Pausa; ligar `Run` de novo retoma da próxima amostra sem resultado. |
| Um controle da exploração foi mexido durante a solução | Pausa sem gravar aquela execução (ela seria gravada com valores errados). |
| Plano mudou com execuções guardadas | Não mistura: `Run` explica e pede `Reset`. Durante o lote, a mudança só vale depois do Reset. |
| Controle removido do documento | Pausa com o nome do controle. |
| Arquivo salvo durante o lote | As execuções vão no `.gh`; ao reabrir, o lote aparece pausado e retoma com `Run`. |
| Arquivo fechado / componente removido | Pausa e grava no store o que estava no buffer. |
| Fim do lote ou pausa, com `Restore` ligado | Os controles voltam aos valores de antes do lote. |
| `Live` desligado | `Inputs`/`Results` só aparecem com o lote parado: componentes pesados ligados às saídas não recalculam a cada execução. |

**Store:** com `Experiment` preenchido, cada execução vira uma revisão do experimento (tipo `experiment`, o mesmo do Pill Experiment Logger), gravada em lotes de 20: parâmetros (um por variável, viram PillBundle no Pill Restore), árvore de entradas, árvore de resultados completa (qualquer tipo, não só números), **controles aplicados**, tempo da execução, método, índice da amostra, hash do plano e ambiente. O Pill Restore reaplica qualquer execução no canvas; o Pill DB Query e o Pill History consultam as execuções.

**Desempenho:** as saídas do Batch Runner crescem de forma incremental (uma execução nova = um ramo novo). Com o Design Space ligado por fio aos sliders, a cadeia Design Space → Sampler recalcula a cada amostra; o Sampler reaproveita o plano quando nada mudou.

## Correções na infraestrutura existente

A pilha passou a aplicar controles de dentro de um callback agendado, o que expôs três comportamentos do código existente. As correções também valem para o Pill Restore e o Pill Preset Vault:

| Onde | Problema | Correção |
|---|---|---|
| `ControlStateService` (value list) | `GH_ValueList.SelectItem` dispara uma solução e um registro de undo por lista, no meio da aplicação | Seleção direta dos itens + uma expiração; o documento recalcula uma vez no fim |
| `ControlStateService` (panel) | `GH_Panel.SetUserText` também dispara uma solução | Mesma estratégia |
| `PillHub` + `ControlStateService` | Publicar de dentro de um callback agendado deixava os receptores um passo atrasados (o Grasshopper copia a lista de callbacks antes de chamá-los; a expiração agendada pelo PillHub caía na solução seguinte) | `PillHub.ExpirePendingReceivers`: a aplicação de controles expira os receptores na hora |
| `PillDashboard_Component` | Valores vindos do Pill Restore/Preset Vault só eram publicados nos canais `key=` durante a solução, quando o PillHub não avisa mais os receptores | Publica ao receber o valor, como no clique |

## Testes

`tests/Glaux_Tools.Tests/ExploreTests.cs` e `ExploreGrasshopperTests.cs` (34 testes, fora do Rhino):

| Área | Verificação |
|---|---|
| Sobol | Confere ponto a ponto com `scipy.stats.qmc.Sobol(scramble=False)` (16 pontos × 12 dimensões e 64 pontos nas dimensões 37, 100, 199 e 255); os primeiros 2⁷ pontos caem um por intervalo diádico em todas as 256 dimensões, com e sem deslocamento |
| Variáveis | Cada valor discreto recebe a mesma fração de u; sem ruído de ponto flutuante (0,3 e não 0,30000000000000004); ida e volta valor ↔ u; passo que não fecha a faixa nunca passa do máximo |
| Faixas | Leitura de `min/max/step/levels/off`, curingas, vírgula decimal, erros e avisos |
| Amostragem | LHS com uma amostra por faixa em toda variável; maximin nunca pior que um desenho; grade completa com extremos e todos os níveis; trajetórias de Morris mudam uma variável por Δ; linhas A, ABᵢ, B do Saltelli; determinismo por semente em todos os métodos |
| Sensibilidade | Modelo linear → SRC = coeficiente × σx/σy e R² = 1; Pearson, Spearman com empates e p-valor conferem com o scipy; Morris exato (σ = 0) em modelo aditivo e σ > 0 com interação; **Ishigami**: S1 e ST a menos de 0,03 dos valores analíticos (0,314 / 0,442 / 0 e 0,558 / 0,442 / 0,244); execuções faltando e NaN |
| Lote | Início, retomada, conclusão, recusa de plano diferente, Reset, ETA, persistência (NaN, lote em andamento volta pausado) |
| Controles | Valor de amostra → estado no formato de cada controle (slider, níveis numéricos, toggle, value list, booleano e domínio do Slider Pool) e leitura de volta; comparação com tolerância; avisos de faixa além do controle |

**Validar no Rhino** (o laço depende do documento do Grasshopper e do canvas): lote com sliders, toggle e value list; sliders do Slider Pool com receptores; controles do Dashboard com `key=`; pausa por Esc e por mexer num slider; salvar e reabrir no meio; `Restore`; gravação no store e Pill Restore de uma execução.

## Limitações

- Resultados calculados de forma assíncrona (componentes que devolvem o valor numa solução posterior, serviços externos) não são esperados; use `Settle` para dar tempo, ou garanta que o resultado sai na mesma solução.
- O Batch Runner roda uma execução por vez, no processo do Rhino (sem paralelismo).
- Correlação/SRC em variáveis de escolha usa o índice do nível (ou o valor, se os níveis forem números): é uma leitura ordinal.
- Os índices de Sobol não trazem intervalo de confiança; com N pequeno oscilam (o componente avisa abaixo de N = 256).

## Próximos passos

- **Pill Surrogate**: modelo rápido (regressão polinomial, RBF, kriging) treinado nas execuções, para prever resultados sem rodar a definição.
- Vínculo sem fio no Design Space (menu "adicionar controles selecionados" e fios estilo Galapagos, como no Preset Vault).
- Intervalos de confiança por bootstrap nos índices de Sobol; trajetórias de Morris otimizadas (Campolongo).
- Critério de parada no lote (ex: parar quando o Pareto não muda há N execuções, reaproveitando o Convergence Watcher).

## Licença de terceiros

Os números de direção da sequência de Sobol (`src/Core/Explore/SobolDirectionNumbers.cs`) são de S. Joe e F. Y. Kuo
(arquivo `new-joe-kuo-6.21201`), distribuídos sob licença BSD:

> Copyright (c) 2008, Frances Y. Kuo and Stephen Joe. All rights reserved.
> Redistribution and use in source and binary forms, with or without modification, are permitted provided that the
> following conditions are met: Redistributions of source code must retain the above copyright notice, this list of
> conditions and the following disclaimer. Redistributions in binary form must reproduce the above copyright notice,
> this list of conditions and the following disclaimer in the documentation and/or other materials provided with the
> distribution. Neither the names of the copyright holders nor the names of the University of New South Wales and the
> University of Waikato and its contributors may be used to endorse or promote products derived from this software
> without specific prior written permission.
> THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
> LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT
> SHALL THE COPYRIGHT HOLDERS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
> DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR
> BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
> (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
> POSSIBILITY OF SUCH DAMAGE.
