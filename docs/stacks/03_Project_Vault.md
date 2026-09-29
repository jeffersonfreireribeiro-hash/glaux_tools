# Pilha 3 — Project Vault & Provenance

## Objetivo

Registrar **estado, histórico e proveniência** do projeto paramétrico, para poder responder:

- "Como estava o projeto na versão que o cliente aprovou?"
- "O que mudou entre estas duas versões?"
- "**Com quais parâmetros este resultado foi produzido?**"

Evolui o Pill Preset Vault (variantes dentro do `.gh`) para um histórico fora do arquivo, consultável, comparável e restaurável, com versões de software e hashes.

## Arquitetura

O "Project Vault" **é o store** da pilha 2 (`.glauxdb`): não há componente de vault separado. Snapshots e experimentos são entradas com tipo próprio.

```
Pill Snapshot ─┐                         ┌─ Pill History   (lista revisões, extrai árvores)
Pill Experiment├─► SnapshotCodec ─► store ┼─ Pill Compare   (SnapshotComparer)
Logger         ┘   (partes → árvores       ├─ Pill Restore   (SnapshotCodec.Read + ControlStateService)
                    nomeadas + metadados)  └─ Pill DB Query  (achar execuções por resultado)
```

| Classe (`Buraqueira_Tools.ProjectState`) | Papel |
|---|---|
| `SnapshotParts` / `SnapshotCodec` | Monta a entrada (árvores nomeadas + metadados + hashes combinados) e a lê de volta. |
| `EnvironmentInfo` | Versões do Glaux, Rhino, Grasshopper, SO, runtime e documento. Nunca lança exceção fora do Rhino. |
| `ControlState` / `ControlCompatibility` | Estado de controles do canvas e decisão de compatibilidade (testável). |
| `BundleTrees` | `PillBundle` ↔ árvores `param:*`. |
| `SnapshotComparer` | Diferenças entre duas entradas. |
| `EntryRef` | Referências `Nome`, `Nome@3`, `Nome@-1`. |
| `ControlStateService`, `HubCapture` (raiz) | Leitura/escrita dos controles e canais no Grasshopper. |

### Conteúdo de uma entrada de snapshot (tipo `snapshot`, chave = nome, revisão = captura)

| Árvore | Conteúdo |
|---|---|
| `param:<nome>` | cada parâmetro do(s) PillBundle(s) |
| `in` | árvore de entradas |
| `out` | árvore de saídas (em experimentos: `result`) |
| `controls` | um ramo por controle: `[tipo, id, nome, valor, mín, máx, extra]` |
| `hub:<canal>` | dados de canais do PillHub |

| Metadado | Conteúdo |
|---|---|
| `glaux.version`, `rhino.version`, `grasshopper.version`, `os`, `runtime`, `document`, `document.id` | ambiente |
| `vault.notes`, `vault.tags` | anotações |
| `runtime.ms` | tempo informado |
| `config.<chave>` | configuração (experimentos) |
| `unit.<parâmetro>` | unidades do PillBundle |
| `hash.inputs` | SHA-256 combinado de `param:*`, `in`, `controls`, `hub:*` |
| `hash.outputs` | SHA-256 combinado de `out`/`result` |
| `vault.format` | versão do layout (1) |

**Hash ≠ versionamento**: o hash identifica dados (dois snapshots com o mesmo `hash.inputs` foram produzidos exatamente com os mesmos parâmetros); o versionamento é a sequência de revisões do store; o Git continua versionando o código e o `.gh`.

## Componentes (painel **Vault**)

### Pill Snapshot (`PillSnap`)
Entradas: Store, Name, Parameters (PillBundle), Inputs, Outputs, Controls (bool), Hub (chave/grupo/ALL), Notes, Tags, Runtime (ms), Capture, Skip Unchanged.
Saídas: Ref (`Nome@rev`), Revision, Inputs Hash, Outputs Hash, Info.

### Pill History (`PillHistory`)
Entradas: Store, Kind (snapshot/experiment/metrics/dataset), Key Pattern, Tree (ex.: `out`, `result`, `param:Largura`), Limit, Newest First.
Saídas: Refs, Keys, Revisions, Timestamps, Notes, Inputs Hash, Outputs Hash, Metadata (ramo por revisão), Tree Data (`{i; caminho original}` — pronto para Chart Line/tabelas), Info.

### Pill Compare (`PillCompare`)
Entradas: Store, A, B (vazio = revisão anterior de A), Kind.
Saídas: Same Data, Same Inputs, Same Outputs, Changed, Added, Removed, Report (ex.: `param:Largura: 5.5 → 7`, `out: 1 item(ns) diferente(s); Δmáx 0.5; primeira: {0}[1]: 1.4 → 1.9`), Metadata Changes (ex.: `rhino.version: 8.10 → 8.12`), Pair.
Árvores com o mesmo hash não são carregadas.

### Pill Restore (`PillRestore`)
Entradas: Store, Ref, Kind, Apply Controls (borda False → True).
Saídas: Parameters (PillBundle → Pill Bundle Unpack), Inputs, Outputs, Hub Keys, Hub Data, Controls, Compatibility, Compatible, Info.
Restauração **compatível**: um controle só é reaplicado se existir (pelo id; senão por tipo + nome único), for do mesmo tipo, o valor couber na faixa atual do slider e a opção ainda existir na value list. Os demais são listados com o motivo. Sliders são ajustados sem disparar uma solução cada (uma única solução no fim).

### Pill Experiment Logger (`PillExpLog`)
Entradas: Store, Experiment, Parameters, Inputs, Results, Config (`chave=valor`), Runtime, Log, Buffer, Skip Duplicates, Flush.
Saídas: Run, Logged, Pending, Info.
Cada execução nova vira uma revisão do experimento (tipo `experiment`): `entrada → configuração → versão → resultado → tempo`. Com Buffer > 1, grava em lote (uma gravação para N execuções); o buffer também é gravado ao remover o componente e ao fechar o documento.

## Exemplos

- **Aprovação de projeto**: Snapshot `Proposta` com Parameters (Bundle Pack), Outputs (áreas/T60) e Controls; após ajustes, Compare `Proposta@1` × `Proposta` mostra o que mudou; Restore `Proposta@1` + Apply Controls volta os sliders.
- **Otimização (Wallacei/Galapagos)**: Experiment Logger com Log ligado, Buffer 50; ao final, Pill DB Query (Kind `experiment`, Tree `result`, Range) encontra as execuções boas, e Pill Restore/History mostra com quais parâmetros foram obtidas.
- **Linha do tempo de um resultado**: History com Tree `out` → Chart Line.

## Limitações

- Restaurar não recria controles apagados nem muda faixas de sliders (o valor fora da faixa atual é reportado, não forçado).
- Controles de plugins de terceiros (além de sliders, toggles, value lists, panels e Pill Slider Pool) não são capturados; use Inputs/Parameters para eles.
- Canais do PillHub são restaurados como dados (saídas), não republicados no barramento — evita dois publicadores da mesma chave.
- Panels só são capturados quando são de entrada (sem fio de origem).
- O buffer do Experiment Logger fica em memória até ser gravado: um travamento do Rhino perde as execuções ainda não gravadas (use Buffer 1 quando cada execução for cara).

## Desempenho

Snapshot e experimentos usam o store da pilha 2 (lote de 1 000 entradas ≈ 51 ms; gravação individual ≈ 0,35 ms mais o flush). A comparação só carrega árvores com hash diferente. Capturar controles percorre os objetos do documento uma vez.

## Persistência

Tudo no `.glauxdb` (pilha 2). O Pill Restore guarda só a borda do gatilho; nenhum estado de vault fica dentro do `.gh`.

## Compatibilidade

- Layout de snapshot versionado (`vault.format = 1`).
- Referências por id com fallback por nome permitem restaurar em cópias do `.gh` (ids diferentes) quando os nomes são únicos.
- Convive com Pill Preset Vault/Manager (variantes rápidas dentro do `.gh`); o Vault é o histórico durável.

## Testes

`tests/Glaux_Tools.Tests/ProjectStateTests.cs`:

- **snapshot → alteração → restore** reabrindo o store: parâmetros (com unidades), entradas, saídas, canais, controles, notas, tempo e ambiente idênticos ao original;
- hashes combinados separando entradas de saídas;
- comparação: parâmetro `5.5 → 7`, variação numérica, primeira diferença, árvore removida, mudança de versão;
- controles: round-trip da tabela e compatibilidade (id, nome, ambíguo, fora da faixa, tipo mudou, opção removida, inexistente);
- referências `Nome`, `Nome@N`, `Nome@-N`;
- ambiente capturado sem exceção fora do Rhino;
- PillBundle com tipos mistos;
- experimento: 40 execuções em lote → consulta por resultado → parâmetros que o produziram;
- cache: mesma entrada = acerto (sem nova revisão), entrada alterada = invalidação; fingerprint tolerante do Pill Cache × identidade exata.
