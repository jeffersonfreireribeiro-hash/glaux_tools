# Test-IsoSurfaceUnits.ps1
# Pill Isometric Surface Graph: unidades de medida (Unit / Axis Unit), proporcao real X:Y e painel de estatisticas, em RhinoCore sem interface.
# NAO cobre: desenho no canvas do GH com janela.
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tests\rhino\Test-IsoSurfaceUnits.ps1 [-Gha <Glaux_Tools.gha>] [-Out <pasta para PNGs>]

param([string]$Gha = '', [string]$Out = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $Gha) { $Gha = Join-Path $root 'src\bin\Release\net48\Glaux_Tools.gha' }
$rhino = 'C:\Program Files\Rhino 8'
$env:PATH = "$rhino\System;" + $env:PATH
Add-Type -AssemblyName System.Drawing
@("$rhino\System\RhinoCommon.dll", "$rhino\Plug-ins\Grasshopper\GH_IO.dll", "$rhino\Plug-ins\Grasshopper\Grasshopper.dll", $Gha) | ForEach-Object { [Reflection.Assembly]::LoadFrom($_) | Out-Null }
if (-not ('RhinoBootIso' -as [type])) {
    Add-Type -ReferencedAssemblies "$rhino\System\RhinoCommon.dll" -TypeDefinition @"
public static class RhinoBootIso {
    public static Rhino.Runtime.InProcess.RhinoCore Core;
    public static void Start() { if (Core == null) Core = new Rhino.Runtime.InProcess.RhinoCore(new string[] { "/nosplash" }, Rhino.Runtime.InProcess.WindowStyle.Hidden); }
    public static void Stop() { if (Core != null) { Core.Dispose(); Core = null; } }
}
"@
}
[RhinoBootIso]::Start()
$script:failures = @(); $script:checks = 0
function Check([string]$label, $condition, [string]$detail = '') {
    $script:checks++
    if ([bool]$condition) { Write-Host ("  OK     {0} {1}" -f $label, $detail) -ForegroundColor Green }
    else { Write-Host ("  FALHOU {0} {1}" -f $label, $detail) -ForegroundColor Red; $script:failures += $label }
}
$SolMode = [Grasshopper.Kernel.GH_SolutionMode]::Silent
function N($v) { [Grasshopper.Kernel.Types.GH_Number]::new([double]$v) }
function S($v) { [Grasshopper.Kernel.Types.GH_String]::new([string]$v) }
function Errs($c) { @($c.RuntimeMessages([Grasshopper.Kernel.GH_RuntimeMessageLevel]::Error)) }

function Invoke-Iso([int]$nx, [int]$ny, [double]$xmax, [double]$ymax, $unit = $null, $axisUnit = $null, $zLabel = $null, $xLabel = $null) {
    $doc = [Grasshopper.Kernel.GH_Document]::new(); $doc.Enabled = $true
    $c = [Buraqueira_Tools.PillIsometricSurfaceGraph_Component]::new(); $c.CreateAttributes()
    $rnd = [Random]::new(4)
    for ($i = 0; $i -lt $nx; $i++) { $c.Params.Input[0].PersistentData.Append((N (0.125 + $i * ($xmax - 0.125) / ($nx - 1)))) }
    for ($j = 0; $j -lt $ny; $j++) { $c.Params.Input[1].PersistentData.Append((N (0.125 + $j * ($ymax - 0.125) / ($ny - 1)))) }
    for ($j = 0; $j -lt $ny; $j++) { for ($i = 0; $i -lt $nx; $i++) { $c.Params.Input[2].PersistentData.Append((N (30 + 12 * [math]::Sin($i / 6.0) * [math]::Cos($j / 5.0) + $rnd.NextDouble() * 6)), [Grasshopper.Kernel.Data.GH_Path]::new($j)) } }
    if ($null -ne $unit) { $c.Params.Input[14].PersistentData.Clear(); $c.Params.Input[14].PersistentData.Append((S $unit)) }
    if ($null -ne $axisUnit) { $c.Params.Input[15].PersistentData.Clear(); $c.Params.Input[15].PersistentData.Append((S $axisUnit)) }
    if ($null -ne $zLabel) { $c.Params.Input[9].PersistentData.Clear(); $c.Params.Input[9].PersistentData.Append((S $zLabel)) }
    if ($null -ne $xLabel) { $c.Params.Input[7].PersistentData.Clear(); $c.Params.Input[7].PersistentData.Append((S $xLabel)) }
    $null = $doc.AddObject($c, $false); $doc.NewSolution($true, $SolMode)
    [pscustomobject]@{ C = $c }
}
function Save($r, $name) { if ($Out) { $r.C.Params.Output[0].VolatileData.get_Branch(0)[0].Value.Save((Join-Path $Out "$name.png"), [Drawing.Imaging.ImageFormat]::Png) } }

Write-Host "=== PILL ISOMETRIC SURFACE GRAPH ===" -ForegroundColor Cyan
$tmp = [Buraqueira_Tools.PillIsometricSurfaceGraph_Component]::new(); $tmp.CreateAttributes()
Check "Entradas antigas 0-13 intactas; novas Unit (U) e Axis Unit (AU) no fim" ($tmp.Params.Input.Count -eq 16 -and $tmp.Params.Input[13].NickName -eq 'Save' -and $tmp.Params.Input[14].NickName -eq 'U' -and $tmp.Params.Input[15].NickName -eq 'AU' -and $tmp.Params.Output.Count -eq 5)

Write-Host "`n[U1] Sem unidade: comportamento anterior" -ForegroundColor Yellow
$r = Invoke-Iso 20 15 2.875 1.5
Check "Rotulos sem unidade (x1, x2, D1), sem erros" ($r.C.XLabel -eq 'x1' -and $r.C.YLabel -eq 'x2' -and $r.C.ZLabel -eq 'D1' -and $r.C.UnitText -eq '' -and (Errs $r.C).Count -eq 0)
Save $r 'iso_nounit'

Write-Host "`n[U2] Unit = dB, Axis Unit = m" -ForegroundColor Yellow
$r = Invoke-Iso 20 15 2.875 1.5 'dB' 'm'
Check "Rotulos x1 [m], x2 [m], D1 [dB]" ($r.C.XLabel -eq 'x1 [m]' -and $r.C.YLabel -eq 'x2 [m]' -and $r.C.ZLabel -eq 'D1 [dB]' -and $r.C.UnitText -eq 'dB' -and $r.C.AxisUnitText -eq 'm')
$rep = $r.C.Params.Output[3].VolatileData.get_Branch(0)[0].Value
Check "Resumo traz as unidades e os valores com a unidade" ($rep -like '*valores Z = dB; eixos X/Y = m*' -and $rep -like '*Média de Z*dB*' -and $rep -like '*Valores válidos (N): 300*')
Check "ValueWithUnit formata '32.5 dB'" ($r.C.ValueWithUnit(32.5) -eq '32.5 dB')
Save $r 'iso_units'

Write-Host "`n[U3] Unidade informada entre colchetes e sem duplicar" -ForegroundColor Yellow
$r = Invoke-Iso 10 8 2.875 1.5 '[lux]' '(mm)'
Check "'[lux]' e '(mm)' normalizados: D1 [lux], x1 [mm]" ($r.C.UnitText -eq 'lux' -and $r.C.AxisUnitText -eq 'mm' -and $r.C.ZLabel -eq 'D1 [lux]' -and $r.C.XLabel -eq 'x1 [mm]')

Write-Host "`n[U4] Unidade extraida do proprio rotulo" -ForegroundColor Yellow
$r = Invoke-Iso 10 8 2.875 1.5 $null $null 'EDT [s]' 'x1 (m)'
Check "Z Label 'EDT [s]' => unidade s sem duplicar; X Label 'x1 (m)' => eixos em m" ($r.C.UnitText -eq 's' -and $r.C.ZLabel -eq 'EDT [s]' -and $r.C.AxisUnitText -eq 'm' -and $r.C.XLabel -eq 'x1 (m)' -and $r.C.YLabel -eq 'x2 [m]')
$r = Invoke-Iso 10 8 2.875 1.5 'dB' $null 'EDT [s]'
Check "Unit conectada vence a do rotulo (dB), rotulo ja com unidade nao e duplicado" ($r.C.UnitText -eq 'dB' -and $r.C.ZLabel -eq 'EDT [s]')

Write-Host "`n[P] Proporcao real X:Y" -ForegroundColor Yellow
$r = Invoke-Iso 40 14 2.875 1.0 'dB' 'm'
$c = $r.C; $c.ApplyAspect()
$rx = $c.MaxX - $c.MinX; $ry = $c.MaxY - $c.MinY
Check "Caixa: razao entre os eixos = razao entre os intervalos" ($rx / $ry -gt 3.1 -and $rx / $ry -lt 3.3)
$flags = [Reflection.BindingFlags]'NonPublic,Static,Public'
$m = [Buraqueira_Tools.PillIsometricSurfaceGraph_Component].GetMethod('ProjectIsometricPoint', $flags)
function Proj([double]$x, [double]$y, [double]$z) {
    $a = [object[]]@($x, $y, $z, (45.0 * [math]::PI / 180), 0.5, 0.866, [single]0, [single]0, [single]100, [single]100, [Drawing.PointF]::new(0, 0), [double]0)
    $null = $m.Invoke($null, $a); $a[10]
}
function Dist($p, $q) { [math]::Sqrt(([double]$q.X - $p.X) * ([double]$q.X - $p.X) + ([double]$q.Y - $p.Y) * ([double]$q.Y - $p.Y)) }
$c.ApplyAspect()
$lenX = Dist (Proj -1 0 -1) (Proj 1 0 -1)
$lenY = Dist (Proj 0 -1 -1) (Proj 0 1 -1)
Check "Comprimento projetado do eixo X / eixo Y = razao dos intervalos (2.75/0.875 = 3.14)" ([math]::Abs($lenX / $lenY - $rx / $ry) -lt 1e-3) ("(" + [math]::Round($lenX / $lenY, 4) + " vs " + [math]::Round($rx / $ry, 4) + ")")
$rq = Invoke-Iso 20 20 2.875 2.875; $rq.C.ApplyAspect()
$lx = Dist (Proj -1 0 -1) (Proj 1 0 -1); $ly = Dist (Proj 0 -1 -1) (Proj 0 1 -1)
Check "Intervalos iguais: eixos X e Y com o mesmo comprimento (caixa quadrada)" ([math]::Abs($lx - $ly) -lt 1e-3)Save $r 'iso_rect_units'

[RhinoBootIso]::Stop()
if ($script:failures.Count -gt 0) { Write-Host "`n$($script:failures.Count) de $($script:checks) verificacoes FALHARAM:" -ForegroundColor Red; $script:failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }; exit 1 }
Write-Host "`nTODOS OS $($script:checks) TESTES PASSARAM" -ForegroundColor Green
