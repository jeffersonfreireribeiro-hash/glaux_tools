# Glaux Tools — Auditoria da arquitetura e proposta das novas pilhas

> Status: proposta consolidada a partir da auditoria do código (branch `claude/glaux-functional-stacks`).
> As pilhas 1–4 têm MVP implementado neste ciclo; as pilhas 6–8 estão definidas aqui e ficam para os próximos ciclos.

---

## 1. Estado atual do repositório

| Aspecto | Situação encontrada |
|---|---|
| Projeto | Um único assembly `Glaux_Tools.gha` (`src/Glaux_Tools.csproj`, SDK-style, `net48`, WinForms). |
| Namespace | `Buraqueira_Tools` para tudo, arquivos planos em `src/`. |
| Componentes | 97 `GH_Component`, sem GUIDs duplicados, em 9 painéis da aba **Glaux Tools** (Pills, Tree, Statistics, Visual, Automation, Evaluation, Matrix, Transform, I/O). |
| Barramento | `PillHub` (estático): canais por chave, inscrições de receptores, cabos ocultos, fila de recálculo. |
| Persistência | `Pill Disk Save/Load`: um arquivo binário `.pilldata` por chave (`GH_Archive` com o `GH_Structure` inteiro). Presets gravados dentro do `.gh` (`Pill Preset Vault`) ou em JSON (`Pill Preset Manager`). Não há histórico consultável nem banco. |
| Serialização | `GH_IO` binário (Disk Save), `PillJson` (JSON próprio, usado por `PillBundle`), CSV/Excel XML (`CSV_In`/`CSV_Out`). |
| Hashing | `PillDataFingerprint`: SHA-256 sobre um fingerprint **tolerante** (números arredondados por limiar, geometria amostrada por bounding box/vértices). Serve para detecção de mudança e cache; **não** é uma identidade exata do dado. |
| Cache | `Pill Compute Cache`: dicionário estático em memória, chave = fingerprint das entradas. Sem persistência. |
| Timers | `Pill Pulse Timer` (WinForms `Timer`), `Conditional Timer`, `Conditional Trigger`, `Convergence Watcher`. |
| Sliders | `Pill Slider Pool` (painel de sliders/toggles/botões/value lists desenhado em atributos customizados). |
| Visualização | Canvas: GDI+ em `GH_ComponentAttributes` customizados (gráficos, tabela, surface graph). Viewport: `DisplayPipeline` (Spatial Heatmap, Sheet Layout). Ícones 24×24 desenhados em GDI+ (`GlauxToolsIcons`). |
| Profiling | `Data Generation Stopwatch`: tempo de CPU só das fontes diretas de um fio + `SolutionSpan`; média/mín/máx/desvio. Sem ranking do documento, sem percentis, sem memória. |
| Atributos Pill | `Pill_Attributes` desenha a cápsula (badge de categoria, LED, chave) com um `if (comp is X)` por tipo — cada componente novo exigia editar o renderizador. |
| Testes | Nenhum projeto de testes. O build exigia o Rhino instalado (referências por `HintPath`). |
| Documentação | `docs/*.md` gerados de `components_catalog.json` (`extract_catalog.py`). |

### Fatos verificados no Grasshopper 8 (decompilado)

- `GH_Document.FindObject` é O(1) (dicionário interno).
- `GH_Component.ProcessorTime` é medido pelo próprio Grasshopper (cronômetro em `ComputeData`) e zerado em `ClearData`. Um profiler pode **ler** esses tempos no `SolutionEnd` sem instrumentar nada.
- `DA.SetDataTree` copia os ramos para o parâmetro de saída.
- `GH_Structure` mantém os caminhos ordenados (`SortedList`); ramos vazios e itens `null` são representáveis.

---

## 2. Mapa: proposta × o que já existe

| Proposta | Já existe? | Parcial? | Infraestrutura reutilizável | Criar? |
|---|---|---|---|---|
| **DataTree Mapper** (path/index/type/value/metadata, round-trip) | Não | Sim: Disk Save faz round-trip binário, mas opaco (não consultável) | `GH_IO` (blobs de tipos complexos), `GH_Structure` | **Sim** — núcleo de tudo abaixo |
| **DataTree Serializer** | Parcial | Binário `.pilldata` (Disk Save); JSON só para `PillBundle` | Disk Save (formato lido como adapter), `PillJson` | **Sim**, como adapters do Mapper (JSON, CSV longo, binário) |
| **CSV / Table Import-Export** | **Sim** | — | `CSV_In` / `CSV_Out` (tabelas 2D, multi-aba) | Não duplicar. O CSV *longo* (path,index,type,value) entra como adapter do Serializer |
| **JSON Import/Export** | Parcial | Bundle JSON, Accumulator JSON | `PillJson` | Adapter JSON do Serializer (árvores tipadas) |
| **Pill DB Connect** | Não | — | Pasta padrão `PillVault` do Disk Save | **Sim** (provider local sem dependências; ver §4) |
| **Pill DB Read / Write** | Não | Disk Save/Load (1 arquivo por chave, sem revisões) | Mapper + codec binário | **Sim** |
| **Pill Query** | Não | Tree Search / Tree Filter atuam só na árvore viva | — | **Sim**, filtros estruturados (nunca SQL concatenado) |
| **Pill DB Sync** | Não | Change Detector detecta mudança, não sincroniza | Hash de identidade | **Sim**, com máquina de estados testável |
| **Pill Schema Inspector** | Não | Pill Catalog inspeciona o barramento, não arquivos | — | **Sim** |
| **Pill Data Validation** | Parcial | Constraint Checker (faixas), Duplicate Inspector (duplicatas), Tree Diff (estrutura) | Regras sobre a tabela do Mapper | **Sim**, validação unificada pré-persistência |
| **Pill Project Vault** | Parcial | Preset Vault (variantes dentro do `.gh`) | O arquivo `.glauxdb` do DB Connect **é** o vault | **Não** como componente separado: vault = store |
| **Pill Snapshot** | Parcial | Preset Vault captura sliders/toggles/panels | Lógica de captura do Preset Vault, `PillBundle`, `PillHub` | **Sim** (acrescenta hashes, saídas, versões, ambiente) |
| **Pill History** | Parcial | Log de texto do Preset Manager | Store | **Sim** |
| **Pill Compare** | Parcial | Tree Structural Diff (só estrutura) | Mapper + hash | **Sim** (estrutura + valores + parâmetros) |
| **Pill Restore** | Parcial | Preset Vault `ApplyVariant` | Mesma estratégia de aplicação no `RhinoApp.Idle` | **Sim** (com checagem de compatibilidade) |
| **Pill Experiment Logger** | Parcial | Iterative Accumulator (buffer em memória) | Store (append barato) | **Sim** |
| **Hashing de identidade** | Parcial | Fingerprint tolerante | `SHA256` | **Serviço interno** (hash exato do codec canônico). Fingerprint tolerante continua para cache/mudança |
| **Pill Performance Monitor** | Parcial | Data Timer (estatística básica) | `ProcessorTime` nativo | Fundido no **Runtime Profiler** (evita dois componentes quase iguais) |
| **Pill Runtime Profiler** | Não | — | `ProcessorTime`, `SolutionStart/End` | **Sim** |
| **Pill Metrics Logger** | Não | — | Store | Fundido no Profiler (entrada `Store` + `Log`) — séries lidas por History/Query |
| **Pill Dashboard / Control Panel** | Parcial | Slider Pool já é um painel de controles | Renderização do Slider Pool, `Pill_Attributes` | Próximo ciclo (§6) |
| **Pill Status / Gauge / Mini Chart** | Parcial | LED das cápsulas; Chart Line (gráfico completo) | Kit de renderização comum | Próximo ciclo |
| **Pill Heatmap 3D** (Mesh/Surface/Brep) | Parcial | Spatial Heatmap (IDW em grade + viewport) | Paletas e IDW do Spatial Heatmap | Próximo ciclo, separando interpolação × cor |
| **Pill Advanced Plot** | Parcial | Chart Line, Scatter, Box Plot, Loss, Iso Surface | Atributos de gráfico existentes | Estender (eixos múltiplos, polar, regiões, anotações), não recriar |
| **Pill Network Graph** | Parcial | Hierarchical Cluster Graph (dendrograma) | — | Próximo ciclo |
| **Pill Viewport Annotation** | Parcial | Sheet Layout + Pen Style (2D); Rhino 8 GH já tem Text Tag/cotas nativas | Pen Style | Próximo ciclo, só o que o nativo não cobre |
| **Timeline / Keyframe / Playback** | Não | Pulse Timer (fonte de tempo) | Pulse Timer, Slider Pool, Snapshot | Próximo ciclo |
| **"Pill Interpolator"** (animação) | **Nome já usado** | `Data Interpolator` (reamostragem de listas) | — | Renomear a proposta para **Pill Keyframe Tween** |
| **Animation Export** | Parcial | Viewport Capture (captura única) | Viewport Capture | Próximo ciclo |

---

## 3. Arquitetura compartilhada

Sem novos assemblies de produção: namespaces internos no mesmo `.gha`, pastas em `src/Core/`.

| Serviço | Namespace | Conteúdo | Usado por |
|---|---|---|---|
| Data Core | `Buraqueira_Tools.Data` | `GlauxTreeTable` (modelo canônico), `GooCodec` (valor tipado ↔ `IGH_Goo`), `TreeMapper`, `TreeHash` (identidade exata), codecs Binário/JSON/CSV | Serializer, Store, Snapshot, Validation, Compare |
| Persistence | `Buraqueira_Tools.Persistence` | `GlauxFileStore` (log append-only), `GlauxStoreRegistry` (pool de conexões por arquivo), `StoreQuery`, `SyncStateMachine`, `TreeValidator` | DB *, Vault, Profiler |
| Project State | `Buraqueira_Tools.ProjectState` | `EnvironmentInfo`, `SnapshotBuilder`, `SnapshotComparer`, captura/aplicação de controles | Snapshot, Restore, Compare, Experiment Logger |
| Diagnostics | `Buraqueira_Tools.Diagnostics` | `SolutionProfiler` (por documento), `RollingStats` (média, mediana, percentis) | Runtime Profiler |
| Visual (existente) | `Buraqueira_Tools` | `IPillCapsule`: contrato para a cápsula do `Pill_Attributes` sem editar o renderizador a cada componente | Todos os componentes novos |

Regras seguidas:

- Componentes finos: toda lógica testável fica nos serviços (sem WinForms/canvas), o componente só lê entradas e escreve saídas.
- Hash **de identidade** (exato, `TreeHash`) ≠ fingerprint **de mudança** (tolerante, `PillDataFingerprint`) ≠ **versionamento** (revisões do store). O Git continua versionando o código.

---

## 4. Dependências externas (banco de dados)

Pacotes restaurados para `net48`/`win-x64` para medir o que teria de ser distribuído junto ao `.gha`:

| Opção | O que precisa ir junto ao `.gha` | Riscos no Rhino |
|---|---|---|
| `Microsoft.Data.Sqlite` 8 | 8 DLLs gerenciadas (inclui `System.Memory`, `System.Buffers`, `System.Numerics.Vectors`, `Unsafe`) + `e_sqlite3.dll` nativa — 2,3 MB | O provider `dynamic_cdecl` procura a nativa pela localização do assembly; com o carregamento de `.gha` por *COFF byte array* do Grasshopper, `Assembly.Location` fica vazio e a nativa não é encontrada. Polyfills podem conflitar com as cópias do Rhino no modo .NET Framework. |
| `System.Data.SQLite.Core` 1.0.119 | 1 DLL + `x64/SQLite.Interop.dll` e `x86/…` — 3,9 MB | Mesmo problema de localização da interop nativa. |
| `Npgsql` 7 (PostgreSQL) | 16 DLLs (inclui `System.Text.Json`, `System.Collections.Immutable`, `DiagnosticSource`) — 2,8 MB, sem nativa | Alto risco de conflito de versão com assemblies que o próprio Rhino 8 carrega; exige servidor PostgreSQL. |
| EF Core / Dapper | EF Core não suporta `net48` nas versões atuais; Dapper é pequeno mas não resolve o provider | Não justificam a manutenção para o volume de consultas do plugin. |

**Decisão deste ciclo:** o store padrão é um **provider local sem dependências** (`.glauxdb`, log append-only com CRC). Ele entrega persistência, revisões, consultas e histórico agora, sem risco de instalação.

A interface (`IGlauxStore`), o modelo de linhas e as consultas estruturadas foram desenhados para o SQLite: o schema SQL equivalente está em [`02_Persistence.md`](02_Persistence.md). O provider SQLite entra quando houver uma validação no Rhino (Windows) cobrindo:

1. Verificar se o Rhino 8 já distribui `e_sqlite3.dll` / `Microsoft.Data.Sqlite.dll` em `C:\Program Files\Rhino 8\System` (se sim, referenciar com `Private=False` e não distribuir nada).
2. Caso contrário, empacotar o provider num assembly opcional (`Glaux_Tools.Sqlite.dll`) carregado por reflexão, com `SQLitePCL.raw.SetProvider` apontando para a nativa por caminho absoluto (independe de `Assembly.Location`).
3. Testar com o carregamento COFF ligado e desligado, em Rhino .NET Framework e .NET Core.
4. Atualizar `install.ps1` para copiar a nativa.

PostgreSQL fica fora até existir um caso de uso remoto concreto.

---

## 5. Tecnologia gráfica (para Dashboard / Visualização)

| Alvo | Tecnologia disponível | Observação |
|---|---|---|
| Canvas do Grasshopper 1 | GDI+ (`System.Drawing.Graphics` em `GH_ComponentAttributes`) | É a única API do canvas do GH1; o zoom do canvas já cuida da escala. Centralizar fontes/pincéis em cache evita o custo de criar `Font` a cada pintura (padrão atual). |
| Viewport do Rhino | `DisplayPipeline` (OpenGL) | Para heatmaps/anotações 3D. |
| Janelas flutuantes | Eto.Forms (vem com o Rhino 8, multiplataforma) | Para dashboards grandes fora do canvas; sem dependência nova. |

---

## 6. Proposta consolidada

| Pilha | Componentes | Já existe parcialmente? | Dependências | Complexidade | Prioridade |
|---|---|---|---|---|---|
| 1. Data Core & Serialização (inclui Import/Export) | Pill Tree Export, Pill Tree Import, Pill Tree Table, Pill Table To Tree | Disk Save (binário), CSV, Bundle JSON | `GH_IO` | Média | **1 — base de tudo** |
| 2. Persistence | Pill DB Connect, DB Write, DB Read, DB Query, Schema Inspector, Data Validation, DB Sync | Disk Save/Load | Pilha 1 | Alta | **2** |
| 3. Project Vault & Provenance | Pill Snapshot, History, Compare, Restore, Experiment Logger | Preset Vault, Preset Manager, Accumulator, Tree Diff | Pilhas 1–2 | Média-alta | **3** |
| 4. Performance & Diagnostics | Pill Runtime Profiler (+ log de métricas no store) | Data Timer | Pilha 2 (só para o log) | Média | **4** (independente; pode subir) |
| 5. Import / Export | Coberta pelos adapters da pilha 1 (JSON, CSV longo, `.pilldata`) | CSV_In/Out | Pilha 1 | Baixa | Entregue junto com a 1 |
| 6. Dashboard & Controls | Pill Dashboard (container), Pill Gauge, Pill Status, Pill Mini Chart | Slider Pool, LEDs, Chart Line | Kit visual comum | Alta (UI) | 5 |
| 7. Advanced Visualization | Pill Mesh Heatmap, extensões do Chart Line (eixos, polar, regiões), Pill Network Graph, Pill Viewport Annotation | Spatial Heatmap, gráficos, dendrograma | Kit visual, Pilha 1 | Alta | 6 |
| 8. Animation & Timeline | Pill Timeline, Pill Keyframe, Pill Keyframe Tween, Pill Playback, Pill Frame Export | Pulse Timer, Viewport Capture | Pilhas 1 e 3 (estados), Pulse Timer | Alta | 7 |

### MVP de cada pilha

| Pilha | MVP |
|---|---|
| 1 | Modelo canônico + round-trip sem perda (vazio, irregular, profundo, ramos vazios, multi-tipo, grande) + adapters JSON/CSV/binário + hash exato. |
| 2 | Store local com revisões, leitura por revisão, deduplicação por hash, consultas estruturadas, inspeção, validação e sincronização com estado *dirty*/conflito. |
| 3 | Snapshot com ambiente e hashes → histórico → comparação → restauração compatível; logger de experimentos append-only. |
| 4 | Ranking de componentes por solução (sem instrumentação), estatísticas com percentis, memória, cache hits/misses, custo do próprio profiler, log opcional no store. |
| 6 | Kit de renderização compartilhado + um container de dashboard que agrupa Gauge/Status/Mini Chart ligados a canais do PillHub. |
| 7 | Mesh Heatmap: interpolação (IDW/nearest em vértices, com raio máximo) separada do mapeamento de cor; máscara onde não há dado. |
| 8 | Timeline (tempo) → Keyframes (estados = snapshots/bundles) → Tween (interpolação) → Playback (Pulse Timer) → Frame Export (Viewport Capture). |

### Ordem e justificativa

A ordem sugerida foi mantida, com dois ajustes:

1. **Import/Export foi absorvida pela pilha 1**: os formatos são adapters do mesmo modelo canônico; criar componentes separados por formato duplicaria lógica.
2. **Diagnostics não depende de nada** (usa `ProcessorTime` nativo). Fica em 4.º para poder gravar métricas no store, mas pode ser usada desde já para medir as pilhas seguintes.

Dashboard → Visualização → Animação ficam depois porque dependem de um kit visual comum (evitar que cada componente recrie fontes, paletas e layout) e, no caso da animação, dos estados da pilha 3.

---

## 7. Padrões para os componentes novos

- GUIDs aleatórios (uuid4), verificados contra todo `src/` antes do uso.
- Categoria `Glaux Tools`; painéis novos **Data** (persistência + vault) e **Diagnostics**; serialização no painel existente **I/O**.
- Nomes `Pill …` e nicknames `Pill…`, mensagens de runtime em português, erros como `Warning`/`Error` (nunca exceção para o canvas).
- Cápsula visual via `IPillCapsule` (categoria, LED, chave, status).
- Ícones desenhados em `GlauxToolsIcons` (24×24 GDI+), no mesmo estilo.
- Documentação por pilha em `docs/stacks/` e por componente em `docs/` (gerada do catálogo).

## 8. Testes

Projeto `tests/Glaux_Tools.Tests` (xUnit, .NET 8) que carrega o `.gha` compilado.

- Rodam em qualquer SO: tipos primitivos do GH, pontos, vetores, intervalos, cores, datas, GUIDs, complexos e transformações; toda a lógica de store, consulta, validação, sincronização, snapshot e estatística.
- Exigem o Rhino (não cobertos no CI Linux): planos, curvas, malhas, Breps (dependem de `rhcommon_c`) e qualquer coisa que toque o canvas/WinForms. O codec os trata pelo mesmo caminho genérico (blob `GH_IO`) que é testado com complexos e transformações.
- Benchmarks marcados com a categoria `Benchmark` (tempo impresso; limites generosos só para detectar regressões grosseiras).

Como rodar: ver [`tests/README.md`](../../tests/README.md).
