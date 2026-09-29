# Pilha 1 — Data Core & Serialização (DataTree Mapper, Import/Export)

## Objetivo

Dar ao Glaux uma representação **canônica e sem perda** de qualquer DataTree, independente do Grasshopper em memória, que sirva de base para:

- import/export em formatos abertos (JSON, CSV) e compactos (binário);
- persistência e consultas (pilha 2);
- snapshots e proveniência (pilha 3);
- hash de **identidade exata** dos dados.

Round-trip garantido: `DataTree → modelo → formato → modelo → DataTree` devolve a mesma árvore (caminhos, ordem, ramos vazios, nulos, tipos e valores bit a bit).

## Arquitetura

```
GH_Structure<IGH_Goo> ──TreeMapper──► GlauxTreeTable ──┬─ TreeBinaryCodec (.glxt)
        ▲                               (ramos + itens  ├─ TreeJsonCodec   (.json)
        │                                tipados +      ├─ TreeCsvCodec    (.csv longo)
        └──────────── TreeMapper ◄────── metadados)     └─ TreeHash (SHA-256 do binário sem metadados)
                                                        PillDataCodec (.pilldata, formato do Disk Save)
```

| Classe (`Buraqueira_Tools.Data`) | Papel |
|---|---|
| `GlauxValue` | Valor imutável de um item: `Kind` + tag de tipo + campos (X/Y/Z, Int, Text, Blob). Igualdade exata. |
| `GooCodec` | `IGH_Goo` ↔ `GlauxValue`. Primitivos viram valores legíveis; qualquer outro Goo usa o próprio `Write/Read` do GH_IO (blob). |
| `GH_GlauxOpaqueGoo` | Guarda um item cujo tipo não está carregado (plugin ausente) sem perder os bytes. |
| `GlauxTreeTable` / `GlauxBranch` / `GlauxRow` | Modelo canônico e visão relacional (uma linha por item). |
| `TreeMapper` | GH ↔ modelo, e montagem a partir de colunas (`FromColumns`). |
| `TreeBinaryCodec`, `TreeJsonCodec`, `TreeCsvCodec`, `PillDataCodec` | Adapters de formato. |
| `TreeFormats` | Fachada: escolha/detecção de formato, texto × arquivo. |
| `TreeHash` | Identidade exata (SHA-256). |
| `AtomicFile` | Escrita em arquivo temporário + substituição (nunca deixa arquivo pela metade). |

### Por que não "ramo = linha, item = coluna"

Árvores do Grasshopper são irregulares: ramos com tamanhos diferentes, ramos vazios, profundidades distintas (`{0}` e `{0;3;1}` na mesma árvore) e tipos mistos. O modelo guarda **cada ramo com seu caminho completo** e **cada item com seu índice e tipo**, que é a forma relacional que não perde nada:

| path | index | type | value |
|---|---|---|---|
| `{0}` | 0 | Number | 1.5 |
| `{0}` | 1 | Null | |
| `{1}` | -1 | *(ramo vazio)* | |
| `{2;1}` | 0 | Point | 1;2;3 |

## Componentes (painel **I/O**)

### Pill Tree Export (`PillExport`)

| Entrada | Tipo | Descrição |
|---|---|---|
| Data (D) | árvore | Dados a exportar. |
| Format (F) | texto | `json` (padrão), `csv`, `binary`, `pilldata`. Vazio + arquivo = pela extensão. |
| File Path (Path) | texto | Destino opcional; relativo = pasta do `.gh`. |
| Write (W) | bool | Grava o arquivo quando True. |
| Metadata (M) | lista | `chave=valor`. Não altera o hash. |
| Pretty (P) | bool | JSON indentado. |
| Delimiter (Del) | texto | `,` `;` ou `tab` (CSV). |

| Saída | Descrição |
|---|---|
| Text (T) | Serialização (só sem arquivo; binários em Base64). |
| Hash (H) | SHA-256 de identidade exata. |
| File Path | Arquivo gravado nesta solução. |
| Info (I) | Ramos, itens, nulos, profundidade, tipos, tamanho, tempo. |

### Pill Tree Import (`PillImport`)

Entradas: Text (T), File Path, Format (vazio = detectar pelo conteúdo), Delimiter, Reload.
Saídas: Data (árvore), Hash, Metadata, Info.
O arquivo só é relido quando tamanho ou data de modificação mudam (ou com Reload).

### Pill Tree Table (`PillTable`)

Árvore → tabela longa. Saídas: Paths, Indices, Types, Values (Goo original), Text Values (texto invariante), Branch Paths (todos os ramos, inclusive vazios), Branch Counts.

### Pill Table To Tree (`PillToTree`)

Colunas → árvore. Entradas: Paths (um por valor ou um só), Indices (opcional; lacunas viram nulos; negativo declara ramo), Values, Types (opcional; converte textos), Branch Paths (ramos vazios).

## Tipos suportados

| Tipo GH | Representação | Consultável |
|---|---|---|
| Number | `f64` exato (NaN, ±∞, −0 preservados) | sim |
| Integer | `i64` (GH usa 32 bits; fora da faixa vira Number com aviso) | sim |
| Boolean | 0/1 | sim |
| Text | UTF-8; `null` ≠ `""` | sim |
| Point, Vector | x, y, z | sim |
| Interval (Domain) | t0, t1 | sim |
| Colour | ARGB (nome da cor não é preservado — o próprio GH também não preserva) | sim |
| Time | `DateTime.ToBinary` (ticks + Kind) | sim |
| Guid | texto `D` | sim |
| Qualquer outro Goo (Curve, Mesh, Brep, Plane, Matrix, PillBundle, tipos de plugins…) | blob GH_IO do próprio objeto + tipo `Namespace.Tipo, Assembly` | só pelo tipo/texto |
| Goo sem serialização (ex.: `GH_ObjectWrapper` genérico) | só o texto; marcado como **lossy** com aviso | — |

Subclasses de tipos primitivos (ex.: um "número" de plugin) seguem pelo caminho genérico para não perder o tipo real.

## Exemplos

- **Transportar uma árvore entre arquivos .gh:** Export (json, sem arquivo) → Pill Transmitter → em outro documento, Import (T).
- **Planilha editável sem perder estrutura:** Export `csv` + `;` → editar no Excel → Import.
- **Arquivo compacto para simulações grandes:** Export `binary` com File Path `resultados.glxt`.
- **Filtrar itens de uma árvore irregular:** Tree Table → filtros nativos do GH nas colunas → Table To Tree com Branch Paths.
- **Detectar se um dataset mudou de verdade:** comparar a saída Hash entre execuções.

## Formatos

### Binário `.glxt` (versão 1)

```
"GLXT" | u16 versão | u16 flags (bit0 = metadados)
[metadados: i32 n, (string, string) * n, em ordem ordinal]
i32 nTipos, string * nTipos             (tabela de tipos de blob, ordem de aparição)
i32 nRamos
  por ramo: i32 profundidade, i32 * profundidade, i32 nItens
    por item: u8 kind + payload
      0 Null | 1 Number f64 | 2 Integer i64 | 3 Boolean u8 | 4 Text u8+string
      5 Point / 6 Vector f64×3 | 7 Interval f64×2 | 8 Colour i32 | 9 Time i64
      10 Guid 16 bytes | 11 Blob i32 tipo, u8+string texto, i32 tamanho (-1 = sem bytes) + bytes
```
Strings: UTF-8 com prefixo de tamanho (`BinaryWriter`). A leitura valida assinatura, versão e contagens (dados corrompidos geram erro, nunca alocação gigante).

### JSON (`glaux.tree` v1)

```json
{ "format": "glaux.tree", "version": 1,
  "metadata": { "origem": "teste" },
  "branches": [
    { "path": [0, 1], "items": [ { "t": "Number", "v": 0.1 }, null, { "t": "Point", "v": [1, 2, 3] },
                                 { "t": "Colour", "v": "#80FF0000" }, { "t": "Time", "v": "2026-09-29T10:11:12.1234567Z" },
                                 { "t": "Blob", "type": "Grasshopper.Kernel.Types.GH_Curve, Grasshopper", "text": "Curve", "b64": "..." } ] } ] }
```

### CSV longo

`path,index,type,value,data` — ramo vazio: índice `-1`; nulo: tipo `Null`; texto nulo: `data = null`; metadados: linhas `#meta,,chave,valor,`. Salvo em UTF-8 com BOM (acentos corretos no Excel). Não substitui o **Import/Export CSV** tabular existente.

### `.pilldata`

Formato do Pill Disk Save (GH_Archive com o chunk `PillTree`); Import e Disk Load leem os arquivos um do outro.

## Limitações

- O leitor nativo do `.pilldata` (GH) só encontra tipos registrados no `ComponentServer` do Grasshopper e recusa árvores sem ramos; o `PillDataCodec` trata a árvore vazia pelo `PillMeta`. Para dados que vão sair do Grasshopper, prefira `.glxt`/JSON (resolvem tipos em qualquer assembly carregado).
- Blobs de geometria são o formato interno do Rhino: legíveis por Grasshopper/Rhino, não por outras ferramentas.
- O caminho `{}` (sem índices) é aceito pelo modelo, mas o Grasshopper normalmente não o produz.
- JSON/CSV de árvores muito grandes geram textos grandes; para centenas de milhares de itens use o binário.

## Desempenho (200 000 itens, 1 000 ramos; .NET 8, Linux, Release)

| Operação | Tempo | Tamanho |
|---|---|---|
| GH → modelo (`TreeMapper.ToTable`) | 139 ms | — |
| Hash de identidade | 67 ms | — |
| Binário escrita / leitura | 43 / 163 ms | 1,73 MB |
| JSON escrita / leitura | 496 / 1 359 ms | 6,86 MB |
| CSV escrita / leitura | 512 / 1 280 ms | 6,90 MB |
| Modelo → GH (`TreeMapper.ToTree`) | 15 ms | — |

(`tests/.../DataCoreTests.Benchmark_LargeTree_AllFormats`.)

## Persistência

Esta pilha não guarda estado próprio: o arquivo exportado é o artefato. O modelo e o codec binário são o formato de armazenamento da pilha 2.

## Compatibilidade

- Rhino 8 / Grasshopper 1, `net48`; nenhuma dependência nova.
- Arquivos `.glxt`/JSON/CSV carregam `version`; leitores recusam versões futuras com mensagem clara.
- `.pilldata` compatível com Pill Disk Save/Load.

## Testes

`tests/Glaux_Tools.Tests/DataCoreTests.cs` (55 testes):

- round-trip GH ↔ modelo e por formato (texto e arquivo) para árvores **vazias, com um ramo vazio, irregulares, profundas (12 níveis), com nulos, multi-tipo e grandes**;
- binário bit a bit estável (codificar → decodificar → codificar);
- hash: igual para dados iguais, diferente para mudança em item do meio, tipo, caminho, nulo × texto vazio, ramo vazio × árvore vazia; metadados não afetam;
- valores extremos de ponto flutuante (NaN, ±∞, −0, subnormais, `MaxValue`);
- tipos desconhecidos preservados como opacos e reexportados sem perda;
- CSV com aspas/quebras de linha; rejeição de CSV tabular, JSON de outro formato/versão e binário corrompido/truncado;
- montagem por colunas com lacunas, duplicatas e ramos vazios;
- benchmark.
