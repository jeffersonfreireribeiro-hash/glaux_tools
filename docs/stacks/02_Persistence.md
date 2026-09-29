# Pilha 2 — Data & Persistence

## Objetivo

Persistir, versionar, consultar e sincronizar DataTrees sem perda, conectando:

**Grasshopper DataTree ↔ modelo tipado (pilha 1) ↔ persistência ↔ consultas ↔ histórico**

Não é "um banco de dados genérico" plugado no Grasshopper: o store guarda **árvores tipadas com revisões**, e as consultas entendem caminhos, índices e tipos.

## Arquitetura

```
 Pill DB Write ─┐                      ┌─ Pill DB Read / Query / Schema Inspector
 Pill DB Sync ──┼─► GlauxStoreRegistry ─► GlauxFileStore (.glauxdb) ──► TreeBinaryCodec (pilha 1)
 Pill Snapshot ─┘   (1 instância por    │   log append-only + índice em memória
                     arquivo = "pool")  └─ AnyStoreChanged ─► StoreWatch ─► expira leitores na próxima solução
```

| Classe | Namespace | Papel |
|---|---|---|
| `GlauxFileStore` | `Persistence` | Store local: gravação, índice, leitura com CRC, compactação. |
| `GlauxStoreRegistry` | `Persistence` | Uma instância por arquivo, compartilhada por componentes e documentos. |
| `StoreQuery` / `StoreQueryEngine` / `PathMask` | `Persistence` | Consultas estruturadas. |
| `SyncStateMachine` / `SyncEngine` | `Persistence` | Regras e execução da sincronização. |
| `TreeValidator` | `Data` | Validação de árvores (independe do store). |
| `GH_GlauxStoreGoo`, `StoreInput`, `StoreWatch` | raiz | Camada Grasshopper: tipo da conexão, resolução da entrada Store, avisos de mudança. |

Toda a lógica testável fica sem dependência do canvas; os componentes só leem entradas e escrevem saídas.

## Componentes (painel **Data**)

| Componente | Nick | Entradas principais | Saídas principais |
|---|---|---|---|
| Pill DB Connect | `PillDB` | Path, Read Only, Compact, Keep | Store, Keys, Entries, Info |
| Pill DB Write | `PillDBWrite` | Store, Key, Data, Metadata, Kind, Write, Skip Unchanged | Revision, Hash, Written, Info |
| Pill DB Read | `PillDBRead` | Store, Key, Revision (0 = última, −1 = penúltima…), Kind, Tree | Data, Revision, Timestamp, Metadata, Hash, Revisions, Trees |
| Pill DB Query | `PillQuery` | Store, Kind, Key Pattern, All Revisions, Metadata, Path Mask, Type, Range, Contains, Tree, Limit | Keys, Revisions, Timestamps, Values, Paths, Indices, Count, Info |
| Pill Schema Inspector | `PillSchema` | Store, Key Pattern, Scan Types | Keys, Kinds, Revisions, Latest, Trees, Types, Metadata Fields, Shared Data, Info |
| Pill Data Validation | `PillValidate` | Data, Types, Allow Nulls, Unique, Range, Depth, Branch Count, Uniform Length, Required Paths, Require Items | Valid, Issues, Issue Paths, Mask, Valid Data, Summary |
| Pill DB Sync | `PillSync` | Store, Key, Local Data, Direction, Auto, Conflict, Sync, Kind | Data, State, Action, Store Revision, Local Hash, Log |

A entrada **Store** aceita a saída do DB Connect, um caminho de texto, ou fica vazia (store padrão `PillVault/glaux_project.glauxdb` ao lado do `.gh`, mesma convenção do Pill Disk Save).

## Tipos suportados

Os da pilha 1 (primitivos legíveis + qualquer Goo serializável como blob). Consultas numéricas atuam em Number, Integer e Boolean; consultas de texto no texto de exibição de qualquer item; `Type` aceita tag (Number, Text…) ou nome curto do Goo (Curve, Mesh…).

## Conceitos

- **Entrada**: (tipo, chave, revisão) + data UTC + metadados + uma ou mais **árvores nomeadas** (`data` por padrão; snapshots usam várias).
- **Revisão**: numeração 1, 2, 3… por (tipo, chave). Revisões antigas continuam legíveis até uma compactação.
- **Identidade**: cada árvore guarda o SHA-256 dos seus dados (pilha 1). `Skip Unchanged` usa esse hash para não criar revisões repetidas.
- **Relações**: não há chaves estrangeiras; relações são implícitas por hash (o Inspector lista revisões com dados idênticos) e por chave/metadados.

## Consultas (sem SQL montado a partir de texto)

Cada filtro é um valor tipado vindo de uma entrada do componente; nenhum texto do usuário vira código. O padrão de chave é escapado antes de virar expressão regular (`GEO.Raio` não casa com `GEOxRaio`).

| Filtro | Exemplo |
|---|---|
| Tipo | `snapshot` |
| Chave | `ACU_*`, `Sala??` |
| Revisões | última de cada chave ou todas |
| Metadados | `sala=A` (todos precisam bater) |
| Máscara de caminho | `{0;*}`, `{*;2}`, `{1;**}` (qualquer profundidade restante), `{0..3;*}` |
| Tipo de item | `Number`, `Curve` |
| Faixa | intervalo do Grasshopper |
| Texto | contém (sem diferenciar maiúsculas) |

## Sincronização

| Direção | Quem é a referência | Comportamento |
|---|---|---|
| 0 Push | Grasshopper | grava quando o dado local muda (sobrescreve revisões externas, com aviso) |
| 1 Pull | Store | traz revisões novas; mudanças locais são ignoradas (com aviso) |
| 2 Two-Way | quem mudou | local mudou → Push; store mudou → Pull; os dois → **conflito** |

| Estado | Significado |
|---|---|
| Clean | nada mudou desde o último ponto sincronizado (ou local = store) |
| LocalDirty | o dado do Grasshopper mudou |
| StoreAhead | o store tem revisão nova com dados diferentes |
| Conflict | os dois mudaram (só relevante em Two-Way) |
| Empty | não há dados em nenhum lado |

Política de conflito: 0 parar e avisar, 1 prevalece o Grasshopper, 2 prevalece o store. **Auto** executa sozinho; senão a ação fica pendente até **Sync**.

O marcador persistido no `.gh` guarda a revisão/hash do store **e o hash local** do último ponto sincronizado. Isso evita o laço `GH altera DB → DB altera GH → GH altera DB`:

- depois de um Pull, o dado local antigo passa a ser "já visto" e não é reenviado;
- se a saída do Sync realimentar a entrada, os dados são iguais aos do store → Clean, nenhuma revisão;
- gravar dados idênticos não cria revisão (`Skip Unchanged`).

`tests/.../PersistenceTests.Sync_TwoDocuments_TwoWayAuto_ConvergeWithoutPingPong` simula dois documentos sincronizando a mesma chave e verifica que nenhum grava em laço.

### Atualização automática dos leitores

Quando um componente grava, os leitores do mesmo arquivo (Read e Sync só se a chave deles mudou; Query e Inspector sempre) são expirados na solução seguinte. Quem gravou não é avisado. Se um leitor receber mais de 25 avisos em 5 s (laço leitura → gravação montado no canvas), a atualização automática dele pausa por 10 s e o componente mostra um aviso. Gravações feitas por **outro processo** não disparam aviso (use Reload/um timer). O DB Connect não observa o store: expirá-lo reexecutaria todos os componentes ligados à saída Store.

## Validação

Regras (todas opcionais): tipos aceitos, nulos, duplicatas por ramo ou globais (igualdade exata), faixa numérica, profundidade exata, número de ramos, comprimento uniforme, ramos obrigatórios, árvore não vazia. Saídas: válido, problemas com caminho/índice, máscara paralela e a árvore só com os itens aprovados. Ligue `Valid` ao `Write` do DB Write para gravar só dados válidos.

## Formato do arquivo `.glauxdb` (versão 1)

```
cabeçalho (32 bytes)
  "GLXDB\0\0\0" | u16 versão | u16 flags | u32 reservado | 16 bytes geração (muda a cada compactação)

registro (repetido)
  u32 "GLXR" | u8 tipo (1 = entrada) | i32 tamanho do payload
  payload
  u32 CRC-32 do payload | u32 "GLXE"

payload de entrada
  i32 tamanho do cabeçalho
  cabeçalho:
    u16 formato (1) | 16 bytes id | string tipo | string chave | i64 revisão | i64 ticks UTC
    i32 nMeta, (string chave, string valor) * nMeta          (ordem ordinal)
    i32 nÁrvores, por árvore: string nome | string hash | i32 ramos | i32 itens | i32 bytes
  blocos binários das árvores (TreeBinaryCodec, pilha 1), na ordem do cabeçalho
```

Garantias:

- **Append-only**: gravar nunca reescreve dados existentes.
- **Gravação interrompida** (queda de energia no fim do arquivo): o registro incompleto é ignorado na leitura e descartado na próxima gravação.
- **Corrupção no meio** do arquivo: entradas anteriores continuam legíveis; o store fica bloqueado para escrita (mensagem no Connect/Inspector).
- **CRC-32** verificado ao carregar dados.
- **Concorrência**: um escritor por vez (lock de arquivo, com novas tentativas), leitores simultâneos, nenhum handle aberto entre operações; outra instância ou processo vê as novas entradas na próxima operação.
- **Compactação**: reescreve em arquivo temporário e substitui; instâncias com índice antigo relocalizam entradas pelo id.

### Equivalente relacional (para o futuro provider SQLite)

```sql
CREATE TABLE entries (
  id          BLOB PRIMARY KEY,          -- 16 bytes
  kind        TEXT NOT NULL,
  key         TEXT NOT NULL,
  revision    INTEGER NOT NULL,
  ts_utc      INTEGER NOT NULL,          -- ticks
  UNIQUE (kind, key, revision)
);
CREATE TABLE entry_metadata (entry_id BLOB REFERENCES entries(id), name TEXT, value TEXT, PRIMARY KEY (entry_id, name));
CREATE TABLE trees (entry_id BLOB REFERENCES entries(id), name TEXT, hash TEXT, branch_count INTEGER, item_count INTEGER, PRIMARY KEY (entry_id, name));
CREATE TABLE branches (entry_id BLOB, tree TEXT, ordinal INTEGER, path TEXT, depth INTEGER, item_count INTEGER, PRIMARY KEY (entry_id, tree, ordinal));
CREATE TABLE items (
  entry_id BLOB, tree TEXT, branch INTEGER, idx INTEGER,
  kind INTEGER, type_tag TEXT, num REAL, int INTEGER, text TEXT, x REAL, y REAL, z REAL, blob BLOB,
  PRIMARY KEY (entry_id, tree, branch, idx)
);
CREATE INDEX items_num ON items(type_tag, num);
```

A `StoreQuery` se traduz em `WHERE` com parâmetros (`@kind`, `@min`…), nunca concatenando texto. Ramos vazios existem em `branches` com `item_count = 0`.

## Dependências externas

Nenhuma. Análise e decisão sobre SQLite/PostgreSQL em [`00_Auditoria_e_Proposta.md` §4](00_Auditoria_e_Proposta.md#4-dependências-externas-banco-de-dados): o provider SQLite fica condicionado a uma validação dentro do Rhino (carregamento da `e_sqlite3.dll` nativa com o carregamento COFF do Grasshopper).

## Limitações

- Um arquivo é um store; não há servidor nem acesso remoto (PostgreSQL fica para quando houver caso de uso concreto).
- Apagar uma chave isoladamente ainda não existe; a compactação remove revisões antigas.
- Consultas de item leem a árvore consultada de cada entrada candidata (não há índice por valor). Para stores muito grandes, filtre primeiro por chave/tipo/metadados.
- Mudanças feitas por outro processo só aparecem quando o componente recalcula.

## Desempenho (.NET 8, Linux, Release; `PersistenceTests.Benchmark_BulkInsert_And_LargeTree`)

| Operação | Resultado |
|---|---|
| Inserção em lote (1 000 entradas, um flush) | 51 ms (≈ 51 µs/entrada) |
| Gravações individuais (flush em cada) | ≈ 345 µs/entrada |
| Reindexar 1 000 entradas ao abrir | 5 ms |
| Árvore de 200 000 itens: gravar / ler | 104 ms / 21 ms |
| Consulta por faixa em 200 000 itens | 58 ms |

Em Windows/NTFS o flush por gravação custa mais; para registros em alta frequência use o Experiment Logger (lote) ou `AppendBatch`.

## Persistência

O arquivo `.glauxdb` é o artefato; fica por padrão em `PillVault` ao lado do `.gh` (versionável junto com o projeto se desejado). O Pill DB Sync guarda seu marcador de sincronização no próprio `.gh`.

## Compatibilidade

- `net48`, Rhino 8 / Grasshopper 1, sem dependências novas.
- Versão do formato no cabeçalho do arquivo e em cada entrada; leitores recusam versões futuras com mensagem clara.

## Testes

`tests/Glaux_Tools.Tests/PersistenceTests.cs`:

- round-trip **DataTree → store → DataTree** para árvores vazias, com ramo vazio, irregulares, profundas, com nulos, multi-tipo e grandes (reabrindo o arquivo numa instância nova);
- revisões por (tipo, chave), revisão específica e lista;
- deduplicação por hash e metadados;
- segunda instância enxergando gravações incrementais;
- gravação interrompida no fim (ignorada e reparada), corrupção no meio (escrita bloqueada, leitura preservada), CRC detectando bit trocado;
- compactação com instâncias de índice antigo;
- registro compartilhado e evento de mudança com origem;
- consultas por chave (com escape), revisão, metadados, máscara de caminho, tipo, faixa, texto e limite;
- máscaras de caminho (inclusive faixas e `**`);
- sincronização: primeiro Push, eco suprimido, modo manual, conflito e políticas, direções únicas e **dois documentos sem ping-pong**;
- todas as regras de validação;
- benchmark de lote, árvore grande e consulta.
