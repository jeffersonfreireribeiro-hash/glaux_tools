# install.ps1
# Script de Instalacao Automatica do Glaux Tools para Grasshopper (Rhino 8)

$ErrorActionPreference = "Stop"

$ghLib = "$env:APPDATA\Grasshopper\Libraries"
$destGlaux = "$ghLib\Glaux"

if (-not (Test-Path $destGlaux)) {
    New-Item -ItemType Directory -Path $destGlaux -Force | Out-Null
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrEmpty($repoRoot)) {
    $repoRoot = Get-Location
}

Write-Host "=======================================================================" -ForegroundColor Cyan
Write-Host "             GLAUX TOOLS :: INSTALADOR AUTOMATICO                     " -ForegroundColor Cyan
Write-Host "                    Grasshopper (Rhino 8)                              " -ForegroundColor Cyan
Write-Host "=======================================================================" -ForegroundColor Cyan
Write-Host "`nDestino no Grasshopper:" -ForegroundColor Yellow
Write-Host "  -> $destGlaux`n" -ForegroundColor White

# 1. Localizar binario Glaux_Tools.gha pre-compilado ou compilar via dotnet build
$binCandidate = Join-Path $repoRoot "dist\Glaux_Tools.gha"
$buildCandidate = Join-Path $repoRoot "src\bin\Release\net48\Glaux_Tools.gha"
$slnPath = Join-Path $repoRoot "Glaux_Tools.sln"

$sourceGha = $null

if (Test-Path $binCandidate) {
    $sourceGha = $binCandidate
} elseif (Test-Path $buildCandidate) {
    $sourceGha = $buildCandidate
} else {
    Write-Host "[1/3] Compilando Glaux Tools via dotnet build..." -ForegroundColor Yellow
    dotnet build -c Release $slnPath
    if (Test-Path $buildCandidate) {
        $sourceGha = $buildCandidate
    }
}

if (-not $sourceGha -or -not (Test-Path $sourceGha)) {
    Write-Host "ERRO: O arquivo Glaux_Tools.gha nao foi encontrado e a compilacao falhou." -ForegroundColor Red
    Exit 1
}

Write-Host "[1/3] Binario localizado: $sourceGha" -ForegroundColor Green

# 2. Copiar para Libraries\Glaux (com suporte a swap limpo se o Rhino estiver aberto)
Write-Host "[2/3] Instalando Glaux_Tools.gha em Libraries\Glaux..." -ForegroundColor Yellow
$destFile = Join-Path $destGlaux "Glaux_Tools.gha"

try {
    Copy-Item -Path $sourceGha -Destination $destFile -Force -ErrorAction Stop
    Write-Host "  -> Copiado diretamente com sucesso!" -ForegroundColor Green
} catch {
    $oldFile = Join-Path $destGlaux ("Glaux_Tools.gha.old_" + [System.IO.Path]::GetRandomFileName())
    Rename-Item -Path $destFile -NewName ([System.IO.Path]::GetFileName($oldFile)) -Force
    Copy-Item -Path $sourceGha -Destination $destFile -Force
    Write-Host "  -> Atualizado com sucesso (swap limpo em processo ativo)!" -ForegroundColor Cyan
}

# 3. Desbloquear arquivo no Windows
Write-Host "[3/3] Desbloqueando arquivo no Windows (Unblock-File)..." -ForegroundColor Yellow
Unblock-File -Path $destFile -ErrorAction SilentlyContinue

Write-Host "`n=======================================================================" -ForegroundColor Green
Write-Host "         SUCESSO! GLAUX TOOLS FOI INSTALADO COM EXITO!                 " -ForegroundColor Green
Write-Host "  Pasta de instalacao:                                                 " -ForegroundColor Green
Write-Host "  $destGlaux                                                           " -ForegroundColor Green
Write-Host "                                                                       " -ForegroundColor Green
Write-Host "  Abra o Rhino 8 e o Grasshopper para acessar a aba 'Glaux Tools'!     " -ForegroundColor Green
Write-Host "=======================================================================" -ForegroundColor Green
