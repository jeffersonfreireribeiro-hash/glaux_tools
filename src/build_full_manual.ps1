# build_full_manual.ps1
param(
    [string]$OutputDir = $PSScriptRoot
)

$catalogPath = Join-Path $OutputDir "components_catalog.json"
if (-not (Test-Path $catalogPath)) {
    Write-Error "components_catalog.json not found."
    exit 1
}

$catalog = Get-Content $catalogPath -Raw | ConvertFrom-Json

# Normalizar subcategorias vazias
foreach ($item in $catalog) {
    if ([string]::IsNullOrWhiteSpace($item.SubCategory)) {
        if ($item.Name -like "Pill*" -or $item.File -like "Pill*") {
            $item.SubCategory = "Pills"
        } elseif ($item.File -like "*CSV*" -or $item.Name -like "*CSV*") {
            $item.SubCategory = "I/O"
        } elseif ($item.File -like "*Timer*" -or $item.Name -like "*Stopwatch*") {
            $item.SubCategory = "Automation"
        } else {
            $item.SubCategory = "Utilitários"
        }
    }
}

$groupsOrder = @("Pills", "Tree", "Statistics", "Automation", "Evaluation", "Visual", "Transform", "I/O", "Utilitários")

$groupTitles = @{
    "Pills" = @{ Title = "1. Ecossistema Pills (Barramento Sem Fios & Variantes)"; Desc = "Componentes de transmissão sem fios (PillHub), concentradores verticais de parâmetros, cofre de variantes com conectores estilo Galapagos e caches inteligentes para otimização acústica e paramétrica." }
    "Tree" = @{ Title = "2. Manipulação Avançada de Árvores de Dados (Data Trees)"; Desc = "Conjunto profissional de ferramentas para reestruturação topológica, filtragem por caminhos, alinhamento de árvores, particionamento e entrelaçamento dinâmico de dados." }
    "Statistics" = @{ Title = "3. Estatística, Probabilidade e Distribuições"; Desc = "Módulos de estatística descritiva, detecção de outliers (IQR/MAD), correlações, entropia de informação e distribuições contínuas/discretas (Beta, Binomial, Qui-Quadrado, Weibull, etc.)." }
    "Automation" = @{ Title = "4. Automação, Estados e Processos Iterativos"; Desc = "Ferramentas para criação de loops, acumuladores circulares (com exportação JSON), travas lógicas (State Latch) e monitoramento de convergência." }
    "Evaluation" = @{ Title = "5. Avaliação de Modelos e Metas de Projeto"; Desc = "Métricas de validação de erro (R², RMSE, MAE), cálculo de desvios relativos de metas de projeto e análise de dispersão geométrica." }
    "Visual" = @{ Title = "6. Visualização Gráfica no Canvas"; Desc = "Renderizadores de gráficos analíticos 2D diretamente no Canvas do Grasshopper (linhas, dispersão X/Y e mapas de calor espaciais)." }
    "Transform" = @{ Title = "7. Matemática em Massa e Agrupamento"; Desc = "Operações matemáticas de alto desempenho vetorizadas em árvores, somas condicionais (SumIf) e agrupamento rápido por similaridade." }
    "I/O" = @{ Title = "8. Importação e Exportação de Dados (I/O)"; Desc = "Leitores e exportadores de dados tabulares (CSV, texto delimitado e planilhas multiactivas) com tipagem automática e alta velocidade." }
    "Utilitários" = @{ Title = "9. Utilitários Gerais"; Desc = "Funções auxiliares e de suporte operacional ao Canvas do Grasshopper." }
}

$sb = New-Object System.Text.StringBuilder

$sb.AppendLine(@"
<!DOCTYPE html>
<html lang="pt-BR">
<head>
<meta charset="UTF-8">
<title>Buraqueira Tools - Manual Completo & Dicionário de Componentes</title>
<style>
  @page {
    size: A4;
    margin: 16mm 14mm 16mm 14mm;
    @bottom-right {
      content: counter(page);
    }
  }

  body {
    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
    color: #1e293b;
    line-height: 1.5;
    font-size: 10pt;
    margin: 0;
    padding: 0;
    background: #fff;
  }

  /* Capa */
  .cover {
    page-break-after: always;
    text-align: center;
    padding-top: 70px;
    padding-bottom: 50px;
  }
  .cover-badge {
    display: inline-block;
    background: #4338ca;
    color: white;
    font-weight: 800;
    font-size: 10.5pt;
    letter-spacing: 2px;
    padding: 6px 20px;
    border-radius: 20px;
    margin-bottom: 25px;
    text-transform: uppercase;
  }
  .cover h1 {
    font-size: 27pt;
    font-weight: 900;
    color: #0f172a;
    margin: 0 0 10px 0;
    letter-spacing: -0.5px;
  }
  .cover h2 {
    font-size: 14pt;
    font-weight: 600;
    color: #64748b;
    margin: 0 0 35px 0;
  }
  .cover-box {
    background: #f8fafc;
    border: 1px solid #e2e8f0;
    border-radius: 12px;
    padding: 22px;
    max-width: 520px;
    margin: 0 auto 40px auto;
    text-align: left;
    font-size: 9.5pt;
    color: #334155;
  }
  .cover-box ul {
    margin: 8px 0 0 18px;
    padding: 0;
  }
  .cover-box li {
    margin-bottom: 5px;
  }
  .cover-meta {
    font-size: 8.5pt;
    color: #94a3b8;
  }

  /* Sumário */
  .toc {
    page-break-after: always;
    padding-top: 15px;
  }
  .toc h2 {
    font-size: 17pt;
    border-bottom: 2px solid #e2e8f0;
    padding-bottom: 8px;
    color: #0f172a;
    margin-bottom: 20px;
  }
  .toc-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 15px;
    margin-bottom: 25px;
  }
  .toc-card {
    background: #f8fafc;
    border: 1px solid #e2e8f0;
    border-radius: 8px;
    padding: 12px 14px;
  }
  .toc-card h3 {
    margin: 0 0 6px 0;
    font-size: 10.5pt;
    color: #4338ca;
  }
  .toc-card p {
    margin: 0;
    font-size: 8.5pt;
    color: #64748b;
  }

  /* Seções de Capítulos */
  .chapter-header {
    page-break-before: always;
    margin-top: 25px;
    margin-bottom: 20px;
    border-bottom: 2.5px solid #4338ca;
    padding-bottom: 8px;
  }
  .chapter-header h1 {
    font-size: 16pt;
    color: #0f172a;
    margin: 0 0 6px 0;
  }
  .chapter-header p {
    font-size: 9.5pt;
    color: #64748b;
    margin: 0;
  }

  /* Cartão de Componente */
  .component-card {
    background: #ffffff;
    border: 1px solid #cbd5e1;
    border-radius: 8px;
    padding: 14px 16px;
    margin-bottom: 16px;
    page-break-inside: avoid;
  }
  .comp-title-bar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    border-bottom: 1px solid #f1f5f9;
    padding-bottom: 8px;
    margin-bottom: 10px;
  }
  .comp-name {
    font-size: 11.5pt;
    font-weight: 800;
    color: #1e293b;
  }
  .comp-nick {
    font-size: 8.5pt;
    font-family: monospace;
    background: #f1f5f9;
    padding: 2px 6px;
    border-radius: 4px;
    color: #475569;
  }
  .comp-desc {
    font-size: 9.2pt;
    color: #334155;
    margin-bottom: 12px;
    line-height: 1.45;
  }

  /* Tabelas de Parâmetros */
  table.params-table {
    width: 100%;
    border-collapse: collapse;
    font-size: 8.4pt;
    margin-top: 6px;
  }
  table.params-table th, table.params-table td {
    padding: 5px 8px;
    border: 1px solid #e2e8f0;
    text-align: left;
  }
  table.params-table th {
    background: #f8fafc;
    font-weight: 700;
    color: #475569;
  }
  .io-tag {
    font-weight: 800;
    font-size: 7.5pt;
    padding: 1px 5px;
    border-radius: 3px;
    display: inline-block;
  }
  .io-in { background: #e0e7ff; color: #3730a3; }
  .io-out { background: #dcfce7; color: #166534; }

  .param-name {
    font-weight: 700;
    color: #0f172a;
  }
  .param-type {
    font-family: monospace;
    font-size: 7.8pt;
    color: #64748b;
  }

  /* Callout */
  .callout {
    border-left: 3.5px solid #4338ca;
    background: #f5f3ff;
    padding: 8px 12px;
    border-radius: 0 6px 6px 0;
    margin: 10px 0;
    font-size: 8.8pt;
  }

  .badge-cat {
    display: inline-block;
    background: #e2e8f0;
    color: #334155;
    font-weight: 700;
    font-size: 7.5pt;
    padding: 2px 6px;
    border-radius: 4px;
    margin-left: 6px;
  }

  .footer-bar {
    text-align: center;
    font-size: 8pt;
    color: #94a3b8;
    margin-top: 25px;
    padding-top: 10px;
    border-top: 1px solid #e2e8f0;
  }
</style>
</head>
<body>

<!-- CAPA -->
<div class="cover">
  <div class="cover-badge">Buraqueira Tools • Documentação Técnica Completa</div>
  <h1>ENCICLOPÉDIA DE COMPONENTES & PILHAS</h1>
  <h2>Manual Oficial de Todos os Módulos do Buraqueira Tools para Grasshopper</h2>

  <div class="cover-box">
    <strong>Visão Geral do Conjunto Completo:</strong>
    <ul>
      <li><strong>57 Componentes Especializados:</strong> Catálogo técnico integral com parâmetros de entrada (Inputs), saídas (Outputs) e descrições de funcionamento.</li>
      <li><strong>Ecossistema Pills:</strong> Transmissão de dados sem fios (PillHub), concentradores verticais de parâmetros, cofre de variantes com cabos estilo Galapagos e caches inteligentes.</li>
      <li><strong>Data Trees Avançadas:</strong> Álgebra e topologia de árvores, alinhamento, filtragem condicional e entrelaçamento de ramos.</li>
      <li><strong>Estatística & Probabilidade:</strong> Média, variância, correlação, assimetria, curtose, tabelas de frequência e 7 distribuições probabilísticas.</li>
      <li><strong>Automação & Gráficos:</strong> Acumuladores iterativos de dados, temporizadores e gráficos direto no Canvas.</li>
    </ul>
  </div>

  <div class="cover-meta">
    Buraqueira Tools • Desenvolvido para Acústica Arquitetônica & Modelagem Paramétrica • Rhino 7 e Rhino 8
  </div>
</div>

<!-- SUMÁRIO -->
<div class="toc">
  <h2>Estrutura dos Módulos</h2>
  <div class="toc-grid">
"@)

foreach ($g in $groupsOrder) {
    if ($groupTitles.ContainsKey($g)) {
        $info = $groupTitles[$g]
        $count = ($catalog | Where-Object { $_.SubCategory -eq $g }).Count
        if ($count -gt 0) {
            $sb.AppendLine(@"
    <div class="toc-card">
      <h3>$($info.Title) <span class="badge-cat">$count Pilhas</span></h3>
      <p>$($info.Desc)</p>
    </div>
"@)
        }
    }
}

$sb.AppendLine(@"
  </div>
</div>
"@)

# GERAR CADA SEÇÃO
foreach ($g in $groupsOrder) {
    $items = $catalog | Where-Object { $_.SubCategory -eq $g } | Sort-Object Name
    if ($items.Count -eq 0) { continue }

    $info = $groupTitles[$g]
    $title = if ($info) { $info.Title } else { $g }
    $desc = if ($info) { $info.Desc } else { "" }

    $sb.AppendLine(@"
<div class="chapter-header">
  <h1>$title</h1>
  <p>$desc (Total: $($items.Count) componentes)</p>
</div>
"@)

    foreach ($c in $items) {
        $sb.AppendLine(@"
<div class="component-card">
  <div class="comp-title-bar">
    <div>
      <span class="comp-name">$($c.Name)</span>
      <span class="badge-cat">$($c.SubCategory)</span>
    </div>
    <span class="comp-nick">$($c.NickName)</span>
  </div>
  <div class="comp-desc">$([System.Net.WebUtility]::HtmlEncode($c.Description))</div>
"@)

        if (($c.Inputs.Count -gt 0) -or ($c.Outputs.Count -gt 0)) {
            $sb.AppendLine(@"
  <table class="params-table">
    <thead>
      <tr>
        <th style="width: 10%;">Direção</th>
        <th style="width: 25%;">Nome (Sigla)</th>
        <th style="width: 20%;">Tipo</th>
        <th>Descrição</th>
      </tr>
    </thead>
    <tbody>
"@)

            foreach ($inp in $c.Inputs) {
                $sb.AppendLine(@"
      <tr>
        <td><span class="io-tag io-in">INPUT</span></td>
        <td><span class="param-name">$($inp.Name)</span> <span class="param-type">($($inp.Nick))</span></td>
        <td><span class="param-type">$($inp.Type)</span></td>
        <td>$([System.Net.WebUtility]::HtmlEncode($inp.Desc))</td>
      </tr>
"@)
            }

            foreach ($out in $c.Outputs) {
                $sb.AppendLine(@"
      <tr>
        <td><span class="io-tag io-out">OUTPUT</span></td>
        <td><span class="param-name">$($out.Name)</span> <span class="param-type">($($out.Nick))</span></td>
        <td><span class="param-type">$($out.Type)</span></td>
        <td>$([System.Net.WebUtility]::HtmlEncode($out.Desc))</td>
      </tr>
"@)
            }

            $sb.AppendLine(@"
    </tbody>
  </table>
"@)
        }

        $sb.AppendLine("</div>")
    }
}

$sb.AppendLine(@"
<div class="footer-bar">
  Buraqueira Tools • Manual Completo Gerado Automaticamente • Buraqueiras/src/Tools
</div>
</body>
</html>
"@)

$fullHtmlPath = Join-Path $OutputDir "Buraqueira_Tools_Manual_Completo.html"
[System.IO.File]::WriteAllText($fullHtmlPath, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Host "HTML gerado: $fullHtmlPath"

# Gerar PDF via Edge/Chrome
$browserCandidates = @(
    "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    "C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    "C:\Program Files\Google\Chrome\Application\chrome.exe"
)

$browser = $null
foreach ($b in $browserCandidates) {
    if (Test-Path $b) { $browser = $b; break }
}

if ($browser) {
    $fullPdfPath = Join-Path $OutputDir "Buraqueira_Tools_Manual_Completo.pdf"
    $htmlUri = "file:///" + ($fullHtmlPath -replace '\\', '/')
    $args = "--headless --disable-gpu --no-pdf-header-footer --run-all-compositor-stages-before-draw --virtual-time-budget=4000 --print-to-pdf=`"$fullPdfPath`" `"$htmlUri`""
    Write-Host "Compilando PDF com $browser..."
    $p = Start-Process -FilePath $browser -ArgumentList $args -Wait -PassThru -NoNewWindow
    if (Test-Path $fullPdfPath) {
        $sizeKb = [math]::Round(((Get-Item $fullPdfPath).Length / 1KB), 1)
        Write-Host "Sucesso! Manual completo em PDF gerado: $fullPdfPath ($sizeKb KB)" -ForegroundColor Green
    }
} else {
    Write-Warning "Nenhum navegador encontrado para compilar o PDF."
}


