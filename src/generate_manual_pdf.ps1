# generate_manual_pdf.ps1
# Automação para gerar e atualizar os Manuais em PDF do Buraqueira Tools.
# 1. Manual Específico das Pilhas (Pills Ecosystem)
# 2. Enciclopédia Completa de Todos os 57 Componentes da Biblioteca

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = $PSScriptRoot
}

# 1. Localizar navegador disponível
$browserCandidates = @(
    "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    "C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    "C:\Program Files\Google\Chrome\Application\chrome.exe",
    "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
)

$browserPath = $null
foreach ($candidate in $browserCandidates) {
    if (Test-Path $candidate) {
        $browserPath = $candidate
        break
    }
}

if (-not $browserPath) {
    Write-Error "Nenhum navegador (Edge ou Chrome) encontrado para renderização de PDFs."
    exit 1
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Buraqueira Tools - GERADOR AUTOMATIZADO DE MANUAIS PDF " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Navegador Utilizado: $browserPath`n"

# ---------------------------------------------------------
# A. MANUAL 1: ECOSSISTEMA PILLS (PillHub, Vault, SliderPool)
# ---------------------------------------------------------
$pillsHtml = Join-Path $scriptDir "Buraqueira_Tools_Manual_Pills.html"
$pillsPdf = Join-Path $scriptDir "Buraqueira_Tools_Manual_Pills.pdf"

if (Test-Path $pillsHtml) {
    Write-Host "[1/2] Compilando Manual do Ecossistema Pills..." -ForegroundColor Yellow
    $htmlUri = "file:///" + ($pillsHtml -replace '\\', '/')
    $args1 = "--headless --disable-gpu --no-pdf-header-footer --run-all-compositor-stages-before-draw --virtual-time-budget=2500 --print-to-pdf=`"$pillsPdf`" `"$htmlUri`""
    $null = Start-Process -FilePath $browserPath -ArgumentList $args1 -Wait -PassThru -NoNewWindow
    if (Test-Path $pillsPdf) {
        $sizeKb = [math]::Round(((Get-Item $pillsPdf).Length / 1KB), 1)
        Write-Host "  -> Sucesso: $pillsPdf ($sizeKb KB)" -ForegroundColor Green
    }
} else {
    Write-Warning "Arquivo fonte $pillsHtml não encontrado."
}

# ---------------------------------------------------------
# B. MANUAL 2: ENCICLOPÉDIA COMPLETA DE TODOS OS COMPONENTES
# ---------------------------------------------------------
$extractScriptPy = Join-Path $scriptDir "extract_catalog.py"
if (Test-Path $extractScriptPy) {
    python $extractScriptPy
}

$buildScriptPy = Join-Path $scriptDir "build_full_manual.py"
$fullPdf = Join-Path $scriptDir "Buraqueira_Tools_Manual_Completo.pdf"

if (Test-Path $buildScriptPy) {
    Write-Host "`n[2/2] Compilando Enciclopédia Completa de Componentes..." -ForegroundColor Yellow
    python $buildScriptPy
    if (Test-Path $fullPdf) {
        $sizeKb = [math]::Round(((Get-Item $fullPdf).Length / 1KB), 1)
        Write-Host "  -> Sucesso: $fullPdf ($sizeKb KB)" -ForegroundColor Green
    }
} else {
    Write-Warning "Script $buildScriptPy não encontrado."
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host " PROCESSAMENTO CONCLUÍDO COM ÊXITO! " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

