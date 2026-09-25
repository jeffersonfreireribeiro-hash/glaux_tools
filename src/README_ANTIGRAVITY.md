# 🐘 Guia Técnico do Buraqueira Tools para o Antigravity

Este arquivo serve como contexto completo para qualquer agente **Antigravity** (ou desenvolvedor) que abra este repositório em outro computador.


10. **Pilhas Matemáticas e Agregações Condicionais (`Statistics` / `Math`):**
    - **Mass Math (`MassMath`):** Operações em massa acumuladas (Soma $\Sigma$, Subtração em cascata, Multiplicação $\Pi$, Divisão em cascata, Média acumulada, Mínimo e Máximo progressivos) com saída de total escalar e lista evolutiva (*running totals*).
    - **Conditional Math (`SomaSe`):** Agregações estilo Excel (`SOMASE`, `SOMASES`, `MÉDIA.SE`, `CONT.SE`, `MULT.SE`, `MÍN.SE`, `MÁX.SE`) com suporte a operadores textuais (`>50`, `<=0`, `=A`, `!=0`), máscaras booleanas e intervalo de soma separado.
11. **Segmentação e Empilhamento Avançado de Dados (`Tree`):**
    - **Data Group Finder (`GroupFinder`):** Detecção automática de padrões e agrupamento por ilhas de repetição consecutivas (*runs*), clusters por proximidade/tolerância contínua, faixas de limiares (*thresholds*) e quantis, retornando árvore por grupo, métricas e IDs 1:1.
    - **Data Stack (`DataStack`):** Empilhamento vertical (`VSTACK`) e horizontal (`HSTACK`) de listas e DataTrees com alinhamento matricial e preenchimento seguro.
12. **Distribuições Estatísticas (Excel 2010/2013+ Compatível):**
    - **Beta Distribution (`BetaDist`):** `BETA.DIST` (CDF e PDF), `BETA.INV`, média e variância teóricas com frações contínuas de alta precisão ($10^{-15}$).
    - **Binomial Distribution (`BinomDist`):** `BINOM.DIST` (pontual PMF e cumulativa CDF), `BINOM.DIST.INTERVALO / BINOM.DIST.RANGE` e `BINOM.INV` (quantil critério).
    - **Chi-Square Distribution (`ChiSqDist`):** `CHISQ.DIST` (CDF e PDF), `CHISQ.INV`, média e variância teóricas.

---

## 📌 1. O que é o Buraqueira Tools?
O **Buraqueira Tools** é um plugin para o Grasshopper (Rhino 7 / 8) em **C# puro (.NET Framework 4.8)** focado em análise de dados, estatística, automação, manipulação de árvores e visualização gráfica interativa/viewport:

1. **Visualização Gráfica & Viewport Heatmap (`Visual`):**
   - **Line Chart & Statistics (`ChartLine`):** Gráficos 2D de linhas de alta definição a partir de DataTrees (múltiplas curvas), com sobreposição de Média ($\mu$), Mediana ($Q_2$), Moda ($Mo$) e faixa de Desvio Padrão ($\pm 1\sigma$).
   - **Scatter & Bubble Plot (`ChartScatter`):** Dispersão 2D com bolinhas (raios e cores dinâmicas), centróide médio $(\bar{x}, \bar{y})$, elipse de dispersão ($\pm 1\sigma$) e linha de tendência linear ($R^2$, Pearson $r$).
   - **Spatial Grid & Viewport Heatmap (`SpatialHeatmap`):** Espalhamento/interpolação espacial (IDW) em malha com **gradiente totalmente customizável (lista de cores do Grasshopper, 15 presets e inversão)**, **avaliação por proximidade de valor ideal ($|v - Target|$)** e **renderização direta no Viewport do Rhino** em tempo real (`IGH_PreviewObject`).
2. **Frequência com Tolerância Geométrica (Clustering 1D / Create Set com Tolerância):** agrupa dimensões com ruído de precisão flutuante (ex.: tolerância 5mm), retornando valores canônicos médios, contagem/quantitativo, porcentagens e mapa de índices para rotulagem 1:1.
3. **Teoria da Informação e Avaliação (`Statistics` & `Evaluation`):** Entropia de Shannon $H(X)$, Divergência de Kullback-Leibler $D_{KL}(P \parallel Q)$, Divergência de Jensen-Shannon (JSD), Distância JS e Entropia Cruzada.
4. **Automação, Estado e Controle de Fluxo Iterativo (`Automation`):** gatilhos condicionais com debounce para evitar travamentos de sliders, travas de estado (latch/flip-flop) contra dados nulos, acumulador iterativo com buffer circular e exportação JSON/CSV, e monitor de convergência com parada automática.
5. **Manipulação Avançada de Árvores de Dados (`Tree`):** filtros com preservação de caminhos, RegEx em caminhos, partição variável, emparelhamento topológico assimétrico, distinct com mapeamento de índices, GroupBy por chaves, Weave dinâmico, Diff estrutural e aritmética de índices no path.
6. **Estatística Descritiva e Robusta (`Statistics`):** tendência central, variância, desvio padrão, média ponderada, assimetria (skewness), curtose (kurtosis), tabela de frequências (multimodal/bins) e média truncada/winsorizada.
7. **Transformação e Limpeza de Dados (`Transform`):** normalização (Min-Max, Z-Score, Sigmóide, Log, Softmax) e detecção de outliers (IQR, Z-Score, MAD).
8. **Validação e Avaliação de Modelos (`Evaluation`):** covariância/correlação (Pearson e Spearman) e métricas de calibração (RMSE, MAE, R², MAPE, Viés/Bias).
9. **Entrada e Saída (`I/O`):** importação e exportação de CSV/TSV/TXT e planilhas multi-aba Excel XML / SpreadsheetML (.xml, .xls) com suporte a matriz 2D em DataTree (`{sheet; col}` e `{sheet; row}`), detecção inteligente de cabeçalhos e nomes de abas.

---

## 📁 2. Estrutura do Projeto
- **Código-fonte:** `g:\Meu Drive\PROJETOS\Teatro escola\pachyderm\Buraqueira_Tools\`
- **Arquivo de Projeto:** `Buraqueira_Tools.csproj` (SDK-style MSBuild, Target: `net48`)
- **Pasta de Distribuição Pronta:** `g:\Meu Drive\PROJETOS\Teatro escola\pachyderm\Buraqueira_Tools_Dist\`
- **Pasta de Instalação Ativa do Grasshopper:** `%appdata%\Grasshopper\Libraries\Buraqueira_Tools.gha`

---

## 🛠️ 3. Como Compilar
Execute no terminal:
```powershell
dotnet build "g:\Meu Drive\PROJETOS\Teatro escola\pachyderm\Buraqueira_Tools\Buraqueira_Tools.csproj" -c Release
```
*O evento de Post-Build já copia o `.gha` diretamente para `%appdata%\Grasshopper\Libraries\` e para a pasta `Buraqueira_Tools_Dist\`.*

---

## 🧩 4. Catálogo dos 43 Componentes Registrados

| Subcategoria | Componente | Nickname | GUID |
| :--- | :--- | :--- | :--- |
| **Visual** | Line Chart & Statistics | `ChartLine` | `1c2d3e4f-5a6b-7c8d-9e0f-1a2b3c4d5e6f` |
| **Visual** | Scatter & Bubble Plot | `ChartScatter` | `8c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f` |
| **Visual** | Spatial Grid & Viewport Heatmap | `SpatialHeatmap` | `7d2e3f4a-5b6c-7d8e-9f0a-1b2c3d4e5f6a` |
| **Statistics** | Geometric Frequency (1D Clustering) | `GeoCluster` | `7a8b9c0d-1e2f-3a4b-5c6d-7e8f9a0b1c2d` |
| **Statistics** | Shannon Entropy | `Entropy` | `5e6f7a8b-9c0d-1e2f-3a4b-5c6d7e8f9a0b` |
| **Evaluation** | KL Divergence (Relative Entropy) | `KLDivergence` | `6f7a8b9c-0d1e-2f3a-4b5c-6d7e8f9a0b1c` |
| **Automation** | Conditional Trigger / Debounced Watcher | `Trigger` | `1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d` |
| **Automation** | State Latch / Flip-Flop (Gating) | `StateLatch` | `2b3c4d5e-6f7a-8b9c-0d1e-2f3a4b5c6d7e` |
| **Automation** | Iterative Accumulator (Data Logger) | `Accumulator` | `3c4d5e6f-7a8b-9c0d-1e2f-3a4b5c6d7e8f` |
| **Automation** | Convergence Watcher (Stop Condition) | `Converge` | `4d5e6f7a-8b9c-0d1e-2f3a-4b5c6d7e8f9a` |
| **Automation** | Conditional Timer / Smart Watcher | `SmartTimer` | `9e8f7a6b-5c4d-3e2f-1a0b-9c8d7e6f5a4b` |
| **Automation** | Data Generation Stopwatch & Benchmark | `DataTimer` | `660d217d-3903-4935-b00e-e9d5dd92b8be` |
| **Tree** | Tree Filter (Preserve Paths) | `TreeFilter` | `5b6c7d8e-9f0a-1b2c-3d4e-5f6a7b8c9d0e` |
| **Tree** | Deep Path Replace (RegEx) | `PathRegEx` | `6c7d8e9f-0a1b-2c3d-4e5f-6a7b8c9d0e1f` |
| **Tree** | Tree Partition (Variable Lengths) | `PartVar` | `7d8e9f0a-1b2c-3d4e-5f6a-7b8c9d0e1f2a` |
| **Tree** | Tree Align (Match Topology) | `TreeAlign` | `8e9f0a1b-2c3d-4e5f-6a7b-8c9d0e1f2a3b` |
| **Tree** | Batch Distinct (Keep Order & Map) | `Distinct` | `9f0a1b2c-3d4e-5f6a-7b8c-9d0e1f2a3b4c` |
| **Tree** | Tree Dispatch (Preserve Paths) | `TreeDispatch` | `0a1b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d` |
| **Tree** | Tree GroupBy (Bucket by Key) | `GroupBy` | `1b2c3d4e-5f6a-7b8c-9d0e-1f2a3b4c5d6e` |
| **Tree** | Dynamic Tree Weaver | `TreeWeave` | `2c3d4e5f-6a7b-8c9d-0e1f-2a3b4c5d6e7f` |
| **Tree** | Tree Structural Diff | `TreeDiff` | `3d4e5f6a-7b8c-9d0e-1f2a-3b4c5d6e7f8a` |
| **Tree** | Path Math (Index Arithmetic) | `PathMath` | `4e5f6a7b-8c9d-0e1f-2a3b-4c5d6e7f8a9b` |
| **Statistics** | Central Tendency | `CenterStat` | `3b4c5d6e-7f8a-9b0c-1d2e-3f4a5b6c7d8e` |
| **Statistics** | Variance | `Var` | `4c5d6e7f-8a9b-0c1d-2e3f-4a5b6c7d8e9f` |
| **Statistics** | Standard Deviation | `StdDev` | `5d6e7f8a-9b0c-1d2e-3f4a-5b6c7d8e9f0a` |
| **Statistics** | Weighted Mean | `WAvg` | `6e7f8a9b-0c1d-2e3f-4a5b-6c7d8e9f0a1b` |
| **Statistics** | Skewness & Kurtosis | `SkewKurt` | `1d2e3f4a-5b6c-7d8e-9f0a-1b2c3d4e5f6a` |
| **Statistics** | Frequency Table | `FreqTab` | `2e3f4a5b-6c7d-8e9f-0a1b-2c3d4e5f6a7b` |
| **Statistics** | Trimmed & Winsorized Mean | `TrimWin` | `3f4a5b6c-7d8e-9f0a-1b2c-3d4e5f6a7b8c` |
| **Statistics** | Target Deviation | `TgtDiff` | `7f8a9b0c-1d2e-3f4a-5b6c-7d8e9f0a1b2c` |
| **Transform** | Data Normalization | `Norm` | `8a9b0c1d-2e3f-4a5b-6c7d-8e9f0a1b2c3d` |
| **Transform** | Outlier Filter | `Outliers` | `9b0c1d2e-3f4a-5b6c-7d8e-9f0a1b2c3d4e` |
| **Evaluation** | Covariance & Correlation | `CovCorr` | `0c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f` |
| **Evaluation** | Model Metrics (RMSE, MAE, R²) | `Metrics` | `4a5b6c7d-8e9f-0a1b-2c3d-4e5f6a7b8c9d` |
| **I/O** | Import CSV / Excel XML | `CSV_In` | `1f2e3d4c-5b6a-7f8e-9d0c-1a2b3c4d5e6f` |
| **I/O** | Export CSV | `CSV_Out` | `2a3b4c5d-6e7f-8a9b-0c1d-2e3f4a5b6c7d` |
| **Statistics** | Beta Distribution (BETA.DIST / BETA.INV) | `BetaDist` | `f1a2b3c4-d5e6-7f8a-9b0c-1d2e3f4a5b6c` |
| **Statistics** | Binomial Distribution (BINOM.DIST / BINOM.INV) | `BinomDist` | `a2b3c4d5-e6f7-8a9b-0c1d-2e3f4a5b6c7d` |
| **Statistics** | Chi-Square Distribution (CHISQ.DIST / CHISQ.INV) | `ChiSqDist` | `b3c4d5e6-f7a8-9b0c-1d2e-3f4a5b6c7d8e` |
| **Statistics** | Mass Math (Stack Operations) | `MassMath` | `c4d5e6f7-a8b9-0c1d-2e3f-4a5b6c7d8e9f` |
| **Statistics** | Conditional Math (SumIf / SomaSe) | `SomaSe` | `d5e6f7a8-b90c-1d2e-3f4a-5b6c7d8e9f0a` |
| **Tree** | Data Group Finder (Cluster & Runs) | `GroupFinder` | `e6f7a8b9-0c1d-2e3f-4a5b-6c7d8e9f0a1b` |
| **Tree** | Data Stack (VSTACK / HSTACK) | `DataStack` | `f7a8b90c-1d2e-3f4a-5b6c-7d8e9f0a1b2c` |
| **Pills** | Pill Transmitter (Wireless Broadcast) | `PillTx` | `a1100001-e1ef-4000-8000-000000000001` |
| **Pills** | Pill Receiver (Wireless Receiver) | `PillRx` | `a1100002-e1ef-4000-8000-000000000002` |
| **Pills** | Pill Bundle Pack | `PillPack` | `a1100003-e1ef-4000-8000-000000000003` |
| **Pills** | Pill Bundle Unpack | `PillUnpack` | `a1100004-e1ef-4000-8000-000000000004` |
| **Pills** | Pill Catalog | `PillCatalog` | `a1100005-e1ef-4000-8000-000000000005` |
| **Pills** | Pill Cache | `PillCache` | `a1100006-e1ef-4000-8000-000000000006` |
| **Pills** | Pill Preset Manager | `PillPreset` | `a1100007-e1ef-4000-8000-000000000007` |
| **Pills** | Pill Constraint Checker | `PillGuard` | `a1100008-e1ef-4000-8000-000000000008` |
| **Pills** | Pill Change Detector | `PillDiff` | `a1100009-e1ef-4000-8000-000000000009` |
| **Pills** | Pill Relay | `PillRelay` | `a110000a-e1ef-4000-8000-00000000000a` |
| **Pills** | Pill Tag Receiver (Cluster Arrow Tag) | `PillHook` | `a110000e-e1ef-4000-8000-00000000000e` |
| **Pills** | Pill Slider Pool (Multi-Parameter Stack) | `SliderPool` | `a110000f-e1ef-4000-8000-00000000000f` |
| **Pills** | Pill Geometry Filter | `GeomFilter` | `a1100011-e1ef-4000-8000-000000000011` |
| **Pills** | Pill Viewport 3D Capture | `PillCapture` | `b7110014-e1ef-4000-8000-000000000014` |
| **Tree** | Duplicate Data Inspector | `DupInspector` | `c8220014-e1ef-4000-8000-000000000015` |

---

## 🎨 5. Sistema de Ícones
- A classe `BuraqueiraToolsIcons.cs` contém o ícone da aba (Pixel art do BURAQUEIRA com prancheta em Base64) e os métodos vetoriais GDI+ (24x24 px) de todos os 43 componentes.
- A classe `BuraqueiraToolsPriority.cs` registra a aba `Buraqueira Tools` no carregamento do Grasshopper.
