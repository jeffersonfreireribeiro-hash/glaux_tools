# Testes do Glaux Tools

Projeto xUnit (`tests/Glaux_Tools.Tests`, .NET 8) que compila o plugin e testa os serviços internos
das pilhas (Data Core, Persistence, Project State, Diagnostics, Dashboard) fora do Rhino.

## Como rodar

```bash
dotnet test tests/Glaux_Tools.Tests
```

- Com o Rhino 8 instalado (Windows), o plugin compila contra a instalação local, como antes.
- Sem Rhino (CI, Linux, macOS), `src/Glaux_Tools.csproj` usa os pacotes NuGet oficiais `Grasshopper`/`RhinoCommon`
  (só para compilar; nada é copiado para o `.gha`).

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

