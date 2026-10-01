# Test-Chart3DColumn.ps1
# Testes automatizados do componente Chart3DColumn_Component (3D Column & Grid Chart) no Rhino 8.
# Executa testes em processo com RhinoCore (headless) para validar geometria, Data Trees, escalas e performance.
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tests\rhino\Test-Chart3DColumn.ps1

param([string]$Gha = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $Gha) { $Gha = Join-Path $root 'src\bin\Release\net48\Glaux_Tools.gha' }
$rhino = 'C:\Program Files\Rhino 8'
$env:PATH = "$rhino\System;" + $env:PATH

@("$rhino\System\RhinoCommon.dll", "$rhino\Plug-ins\Grasshopper\GH_IO.dll", "$rhino\Plug-ins\Grasshopper\Grasshopper.dll", $Gha) |
    ForEach-Object { [Reflection.Assembly]::LoadFrom($_) | Out-Null }

if (-not ('RhinoBootTools' -as [type])) {
    Add-Type -ReferencedAssemblies "$rhino\System\RhinoCommon.dll" -TypeDefinition @"
using System;
public static class RhinoBootTools {
    public static Rhino.Runtime.InProcess.RhinoCore Core;
    public static void Start() { if (Core == null) Core = new Rhino.Runtime.InProcess.RhinoCore(new string[] { "/nosplash" }, Rhino.Runtime.InProcess.WindowStyle.Hidden); }
    public static void Stop() { if (Core != null) { Core.Dispose(); Core = null; } }
}
"@
}
[RhinoBootTools]::Start()

$script:failures = @()
$script:checks = 0

function Check([string]$label, $condition, [string]$detail = '') {
    $script:checks++
    $ok = [bool]$condition
    if ($ok) {
        Write-Host ("  OK     {0} {1}" -f $label, $detail) -ForegroundColor Green
    } else {
        Write-Host ("  FALHOU {0} {1}" -f $label, $detail) -ForegroundColor Red
        $script:failures += $label
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "GLAUX TOOLS - TESTES RHINOCORE: CHART 3D" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# ----------------------------------------------------
# 1. Metadados e Contrato do Componente
# ----------------------------------------------------
Write-Host "`n[1] Verificando Metadados do Componente..." -ForegroundColor Yellow
$comp = [Buraqueira_Tools.Chart3DColumn_Component]::new()
$comp.CreateAttributes()

$expectedName = "3D Column & Grid Chart"
Check "Nome Oficial" ($comp.Name -eq $expectedName) ("({0})" -f $comp.Name)
Check "NickName" ($comp.NickName -eq "Chart3DCol") ("({0})" -f $comp.NickName)
Check "Categoria" ($comp.Category -eq "Glaux Tools") ("({0})" -f $comp.Category)
Check "SubCategoria" ($comp.SubCategory -eq "Visual") ("({0})" -f $comp.SubCategory)
Check "GUID Unico" ($comp.ComponentGuid.ToString() -eq "8e2b1c4a-9d3f-4a7b-b5c6-1e0f2a3b4c5d")
Check "Total de Inputs" ($comp.Params.Input.Count -eq 8) ("({0} inputs)" -f $comp.Params.Input.Count)
Check "Total de Outputs" ($comp.Params.Output.Count -eq 6) ("({0} outputs)" -f $comp.Params.Output.Count)

$Mode = [Grasshopper.Kernel.GH_SolutionMode]::Silent

# ----------------------------------------------------
# 2. Teste 1D: Lista Linear de Valores
# ----------------------------------------------------
Write-Host "`n[2] Testando Modo 1D (Lista Linear: 10, 20, 30, 40)..." -ForegroundColor Yellow
$doc = [Grasshopper.Kernel.GH_Document]::new()
$doc.Enabled = $true
$comp1D = [Buraqueira_Tools.Chart3DColumn_Component]::new()
$comp1D.CreateAttributes()

@(10, 20, 30, 40) | ForEach-Object {
    $comp1D.Params.Input[0].PersistentData.Append([Grasshopper.Kernel.Types.GH_Number]::new([double]$_))
}
$null = $doc.AddObject($comp1D, $false)
$doc.NewSolution($true, $Mode)

$mesh1D = $comp1D.Params.Output[0].VolatileData.get_Branch(0)[0].Value
$isMesh1DOk = ($mesh1D -ne $null -and $mesh1D.IsValid)
Check "Mesh 1D Valida" $isMesh1DOk ("(Vertices: {0}, Faces: {1})" -f $mesh1D.Vertices.Count, $mesh1D.Faces.Count)

$count1D = $comp1D.Params.Output[1].VolatileData.DataCount
Check "Contagem de Caixas 1D" ($count1D -eq 4) ("({0} caixas)" -f $count1D)

$rep1D = $comp1D.Params.Output[5].VolatileData.get_Branch(0)[0].Value
Check "Relatorio 1D indica Modo Linear" ($rep1D -like "*1D Linear*")
Check "Relatorio 1D contem Minimo e Maximo" ($rep1D -like "*Min*10*" -and $rep1D -like "*Max*40*")

# ----------------------------------------------------
# 3. Teste 2D: Grade Matricial 3x3 (Data Tree)
# ----------------------------------------------------
Write-Host "`n[3] Testando Modo 2D Grid (Data Tree 3x3 = 9 colunas)..." -ForegroundColor Yellow
$comp2D = [Buraqueira_Tools.Chart3DColumn_Component]::new()
$comp2D.CreateAttributes()

$treeData = @(
    @{ Path = 0; Vals = @(10, 20, 30) },
    @{ Path = 1; Vals = @(15, 25, 35) },
    @{ Path = 2; Vals = @(12, 18, 28) }
)

foreach ($branch in $treeData) {
    $ghPath = [Grasshopper.Kernel.Data.GH_Path]::new($branch.Path)
    foreach ($val in $branch.Vals) {
        $comp2D.Params.Input[0].PersistentData.Append([Grasshopper.Kernel.Types.GH_Number]::new([double]$val), $ghPath)
    }
}
$null = $doc.AddObject($comp2D, $false)
$doc.NewSolution($true, $Mode)

$mesh2D = $comp2D.Params.Output[0].VolatileData.get_Branch(0)[0].Value
$isMesh2DOk = ($mesh2D -ne $null -and $mesh2D.IsValid)
Check "Mesh 2D Valida" $isMesh2DOk ("(Vertices: {0}, Faces: {1})" -f $mesh2D.Vertices.Count, $mesh2D.Faces.Count)

$count2D = $comp2D.Params.Output[1].VolatileData.DataCount
Check "Contagem de Caixas 2D (9 colunas)" ($count2D -eq 9) ("({0} caixas)" -f $count2D)

$rep2D = $comp2D.Params.Output[5].VolatileData.get_Branch(0)[0].Value
Check "Relatorio 2D indica Grade 3x3" ($rep2D -like "*2D Grade Matricial*" -and $rep2D -like "*X=3*Y=3*")

# ----------------------------------------------------
# 4. Teste de Valores Negativos e Zero (-10, 0, 10)
# ----------------------------------------------------
Write-Host "`n[4] Testando Valores Negativos e Zero (-10, 0, 10)..." -ForegroundColor Yellow
$compNeg = [Buraqueira_Tools.Chart3DColumn_Component]::new()
$compNeg.CreateAttributes()

@(-10, 0, 10) | ForEach-Object {
    $compNeg.Params.Input[0].PersistentData.Append([Grasshopper.Kernel.Types.GH_Number]::new([double]$_))
}
$null = $doc.AddObject($compNeg, $false)
$doc.NewSolution($true, $Mode)

$meshNeg = $compNeg.Params.Output[0].VolatileData.get_Branch(0)[0].Value
$isMeshNegOk = ($meshNeg -ne $null -and $meshNeg.IsValid)
Check "Mesh com Negativos e Zero Valida" $isMeshNegOk

$bNeg0 = $compNeg.Params.Output[1].VolatileData.get_Branch(0)[0].Value
$bZero = $compNeg.Params.Output[1].VolatileData.get_Branch(0)[1].Value
$bPos2 = $compNeg.Params.Output[1].VolatileData.get_Branch(0)[2].Value

$negZOk = ($bNeg0.Z.Min -eq -10.0 -and $bNeg0.Z.Max -eq 0.0)
Check "Coluna Negativa cresce para baixo (-Z)" $negZOk ("(Z: {0} a {1})" -f $bNeg0.Z.Min, $bNeg0.Z.Max)

$zeroZOk = ($bZero.Z.Min -eq 0.0 -and $bZero.Z.Max -eq 0.0)
Check "Coluna Zero no plano base Z=0" $zeroZOk

$posZOk = ($bPos2.Z.Min -eq 0.0 -and $bPos2.Z.Max -eq 10.0)
Check "Coluna Positiva cresce para cima (+Z)" $posZOk ("(Z: {0} a {1})" -f $bPos2.Z.Min, $bPos2.Z.Max)

# ----------------------------------------------------
# 5. Teste de Escala (Scale = 0.01)
# ----------------------------------------------------
Write-Host "`n[5] Testando Escala de Exibicao (1000, 2000, 3000 com Scale=0.01)..." -ForegroundColor Yellow
$compScale = [Buraqueira_Tools.Chart3DColumn_Component]::new()
$compScale.CreateAttributes()

@(1000, 2000, 3000) | ForEach-Object {
    $compScale.Params.Input[0].PersistentData.Append([Grasshopper.Kernel.Types.GH_Number]::new([double]$_))
}
$compScale.Params.Input[2].PersistentData.Clear()
$compScale.Params.Input[2].PersistentData.Append([Grasshopper.Kernel.Types.GH_Number]::new(0.01))

$null = $doc.AddObject($compScale, $false)
$doc.NewSolution($true, $Mode)

$bScale = $compScale.Params.Output[1].VolatileData.get_Branch(0)[0].Value
$scaleH = [Math]::Abs($bScale.Z.Max - 10.0)
Check "Altura Escalada (1000 * 0.01 = 10)" ($scaleH -lt 1e-4) ("(Z.Max: {0})" -f $bScale.Z.Max)

$repScale = $compScale.Params.Output[5].VolatileData.get_Branch(0)[0].Value
Check "Relatorio preserva Valor Real (1000 a 3000)" ($repScale -like "*1000*" -and $repScale -like "*3000*")

# ----------------------------------------------------
# 6. Teste de Arvore Irregular (Sparse Grid)
# ----------------------------------------------------
Write-Host "`n[6] Testando Arvore Irregular ({0}: 3 itens, {1}: 2 itens, {2}: 4 itens)..." -ForegroundColor Yellow
$compIrr = [Buraqueira_Tools.Chart3DColumn_Component]::new()
$compIrr.CreateAttributes()

$p0 = [Grasshopper.Kernel.Data.GH_Path]::new(0)
@(10, 20, 30) | ForEach-Object { $compIrr.Params.Input[0].PersistentData.Append([Grasshopper.Kernel.Types.GH_Number]::new([double]$_), $p0) }

$p1 = [Grasshopper.Kernel.Data.GH_Path]::new(1)
@(40, 50) | ForEach-Object { $compIrr.Params.Input[0].PersistentData.Append([Grasshopper.Kernel.Types.GH_Number]::new([double]$_), $p1) }

$p2 = [Grasshopper.Kernel.Data.GH_Path]::new(2)
@(60, 70, 80, 90) | ForEach-Object { $compIrr.Params.Input[0].PersistentData.Append([Grasshopper.Kernel.Types.GH_Number]::new([double]$_), $p2) }

$null = $doc.AddObject($compIrr, $false)
$doc.NewSolution($true, $Mode)

$meshIrr = $compIrr.Params.Output[0].VolatileData.get_Branch(0)[0].Value
$countIrr = $compIrr.Params.Output[1].VolatileData.DataCount
Check "Arvore Irregular executada sem crash" ($countIrr -eq 9) ("({0} caixas geradas)" -f $countIrr)
Check "Mesh Irregular Valida" ($meshIrr -ne $null -and $meshIrr.IsValid)

# ----------------------------------------------------
# 7. Teste de Performance (1.000 Colunas)
# ----------------------------------------------------
Write-Host "`n[7] Testando Performance com 1.000 Colunas..." -ForegroundColor Yellow
$compPerf = [Buraqueira_Tools.Chart3DColumn_Component]::new()
$compPerf.CreateAttributes()

for ($r = 0; $r -lt 20; $r++) {
    $p = [Grasshopper.Kernel.Data.GH_Path]::new($r)
    for ($c = 0; $c -lt 50; $c++) {
        $val = [double](($r + 1) * ($c + 1) % 100)
        $compPerf.Params.Input[0].PersistentData.Append([Grasshopper.Kernel.Types.GH_Number]::new($val), $p)
    }
}
$null = $doc.AddObject($compPerf, $false)

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$doc.NewSolution($true, $Mode)
$sw.Stop()

$countPerf = $compPerf.Params.Output[1].VolatileData.DataCount
$meshPerf = $compPerf.Params.Output[0].VolatileData.get_Branch(0)[0].Value
Check "1.000 Colunas Geradas" ($countPerf -eq 1000) ("({0} colunas)" -f $countPerf)
Check "Tempo de Geracao < 500 ms" ($sw.ElapsedMilliseconds -lt 500) ("({0} ms)" -f $sw.ElapsedMilliseconds)
Check "Malha de 1.000 Colunas Valida" ($meshPerf -ne $null -and $meshPerf.IsValid) ("(Vertices: {0}, Faces: {1})" -f $meshPerf.Vertices.Count, $meshPerf.Faces.Count)

# ----------------------------------------------------
# 8. Teste de Serializacao (Write & Read)
# ----------------------------------------------------
Write-Host "`n[8] Testando Serializacao (Write & Read de Configuracoes)..." -ForegroundColor Yellow
$compSer = [Buraqueira_Tools.Chart3DColumn_Component]::new()
$compSer.ShowBaseGrid = $false
$compSer.ShowAxes = $false
$compSer.ShowCategoryLabels = $false
$compSer.ShowValueLabels = $true
$compSer.ShowWires = $false
$compSer.ColorMode = [Buraqueira_Tools.Chart3DColorMode]::ValueGradient

$chunk = [GH_IO.Serialization.GH_LooseChunk]::new("TestChunk")
$writeOk = $compSer.Write($chunk)
Check "Write executado com sucesso" $writeOk

$compRestored = [Buraqueira_Tools.Chart3DColumn_Component]::new()
$readOk = $compRestored.Read($chunk)
Check "Read executado com sucesso" $readOk
Check "ShowBaseGrid restaurado (False)" ($compRestored.ShowBaseGrid -eq $false)
Check "ShowAxes restaurado (False)" ($compRestored.ShowAxes -eq $false)
Check "ShowCategoryLabels restaurado (False)" ($compRestored.ShowCategoryLabels -eq $false)
Check "ShowValueLabels restaurado (True)" ($compRestored.ShowValueLabels -eq $true)
Check "ShowWires restaurado (False)" ($compRestored.ShowWires -eq $false)
Check "ColorMode restaurado (ValueGradient)" ($compRestored.ColorMode -eq [Buraqueira_Tools.Chart3DColorMode]::ValueGradient)

# ----------------------------------------------------
# Resumo Final
# ----------------------------------------------------
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "RESUMO DOS TESTES RHINOCORE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ("Total de Verificacoes : {0}" -f $script:checks)
if ($script:failures.Count -eq 0) {
    Write-Host "STATUS GERAL          : TODOS PASSARAM (100% OK)" -ForegroundColor Green
} else {
    Write-Host ("STATUS GERAL          : {0} FALHAS ENCONTRADAS" -f $script:failures.Count) -ForegroundColor Red
    foreach ($f in $script:failures) {
        Write-Host ("  - {0}" -f $f) -ForegroundColor Red
    }
}
Write-Host "========================================`n" -ForegroundColor Cyan

$code = [int]($script:failures.Count -gt 0)
[RhinoBootTools]::Stop()
[Environment]::Exit($code)
