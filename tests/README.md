# Testes do Glaux Tools

Projeto xUnit (`tests/Glaux_Tools.Tests`, .NET 8) que compila o plugin e testa os serviços internos
das pilhas (Data Core, Persistence, Project State, Diagnostics, Dashboard, Exploração de Design) fora do Rhino.

## Como rodar

```bash
dotnet test tests/Glaux_Tools.Tests
```

- Com o Rhino 8 instalado (Windows), o plugin compila contra a instalação local, como antes.
- Sem Rhino (CI, Linux, macOS), `src/Glaux_Tools.csproj` usa os pacotes NuGet oficiais `Grasshopper`/`RhinoCommon`
  (só para compilar; nada é copiado para o `.gha`).
- No GitHub Actions, [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) roda estes testes em Linux e Windows em cada pull request
  e em cada push na `main`.

Só os benchmarks:

```bash
dotnet test tests/Glaux_Tools.Tests --filter Category=Benchmark --logger "console;verbosity=detailed"
```

## O que é coberto aqui e o que exige o Rhino

| Coberto nos testes | Exige o Rhino (validar manualmente no Grasshopper) |
|---|---|
| Números, inteiros, booleanos, textos, pontos, vetores, intervalos, cores, datas, GUIDs, complexos, transformações | Planos, linhas, círculos, curvas, malhas, Breps e outras geometrias que usam a biblioteca nativa `rhcommon_c` |
| Caminho genérico de serialização (`GH_IO`) usado por qualquer tipo complexo | Renderização no canvas (WinForms/GDI+), viewport |
| Store, revisões, consultas, validação, sincronização, snapshots, estatísticas do profiler | Leitura dos tempos reais dos componentes (`ProcessorTime`) num documento aberto |
| Dashboard: definição (ida e volta), estado, layout, política de commit do arrasto, formatação, adaptadores do PillHub e dos cofres | Mouse, captura, menus e tooltip no canvas; componentes (carregam WinForms, indisponível no .NET 8 do Linux) |
| Exploração de Design: variáveis e faixas, amostragem (Sobol conferido com o scipy, LHS, grade, Morris, Saltelli), sensibilidade (correlação/SRC, Morris, índices de Sobol da função de Ishigami), estado do lote, conversão valor ↔ estado de cada tipo de controle | O laço do Pill Batch Runner (aplicar → solução → ler resultados → próxima) num documento do Grasshopper; Slider Pool e Dashboard com receptores do PillHub durante o lote |

As geometrias passam pelo mesmo caminho genérico (blob `GH_IO`) que é exercitado com complexos e transformações.

## Como os testes carregam o plugin

O plugin é gerado como `Glaux_Tools.gha`. O projeto de testes referencia o `.gha` apenas para compilação,
copia o arquivo para a pasta de saída e `PluginAssemblyLoader` o carrega sob demanda
(o host do .NET não aceita a extensão `.gha` no `deps.json`).

## Renderização (separada dos testes de dados)

`tests/render/DashboardGallery.cs` desenha painéis reais do Pill Dashboard com o mesmo renderizador do canvas
(todos os widgets, textos longos, números grandes e negativos, painel vazio, layouts, zoom baixo e 3×, desativado)
e mede o tempo de pintura. As instruções de compilação estão no cabeçalho do arquivo (mono no Linux, `csc` no Windows).
As imagens de referência ficam em `docs/stacks/img/`.

## Documento do Grasshopper no Rhino 8 (fora do CI)

`tests/rhino/` tem testes que montam um `GH_Document` com componentes de verdade e calculam a solução num RhinoCore sem interface.
Eles exigem o Rhino 8 instalado no Windows, por isso o CI não os roda:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests\rhino\Test-PillBundlePack-KeysWires.ps1
```

- `Test-PillBundlePack-KeysWires.ps1`: Pill Bundle Pack com `Keys`, com `Wires ⚡` e com os dois apontando para os mesmos transmissores.
  Cobre apelidos genéricos e descritivos, casos mistos, namespace e a escala de um projeto real (17 transmissores, 157 itens).
  Com `-OldGh <arquivo .gh da v1.2.0 ou anterior>`, confere também que a entrada `Keys`, gravada como obrigatória, abre como opcional.
  `-Gha` escolhe o binário: a v1.2.0 falha em 18 de 24 verificações; a v1.2.1 passa em todas.

**Neste Windows com Rhino:** o projeto de testes xUnit usa os pacotes NuGet da McNeel (Grasshopper 8.0). Para compilar o plugin com
as mesmas referências, como no CI, rode `dotnet test tests/Glaux_Tools.Tests -p:GlauxUseRhinoInstall=false`. Sem isso, o plugin compila
contra o Rhino instalado (8.x) e o projeto de testes falha com CS1705 (versão de referência mais nova).
