# Test-ChartLine.ps1
# Line Chart & Statistics / Histogram: componente de verdade num GH_Document em RhinoCore sem interface.
# Confere pareamento X/Y, ordenacao conjunta, Data Trees, erros de contagem, modos Raw/Smooth/Trend, XY Bars, Distribution,
# saidas do Rhino (Pts/Crv/Refs/Trend), PNG, serializacao e compatibilidade com o componente da versao anterior.
# NAO cobre: desenho no canvas do GH e no viewport do Rhino com janela (validar manualmente).
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tests\rhino\Test-ChartLine.ps1 [-Gha <Glaux_Tools.gha>]
#      Compatibilidade: (1) -Gha <gha da versao ANTIGA> -SaveOld <arquivo.xml>   (2) -Gha <gha novo> -OpenOld <arquivo.xml>

param([string]$Gha = '', [string]$SaveOld = '', [string]$OpenOld = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $Gha) { $Gha = Join-Path $root 'src\bin\Release\net48\Glaux_Tools.gha' }
$rhino = 'C:\Program Files\Rhino 8'
$env:PATH = "$rhino\System;" + $env:PATH
Add-Type -AssemblyName System.Drawing
@("$rhino\System\RhinoCommon.dll", "$rhino\Plug-ins\Grasshopper\GH_IO.dll", "$rhino\Plug-ins\Grasshopper\Grasshopper.dll", $Gha) | ForEach-Object { [Reflection.Assembly]::LoadFrom($_) | Out-Null }
if (-not ('RhinoBootChart' -as [type])) {
    Add-Type -ReferencedAssemblies "$rhino\System\RhinoCommon.dll" -TypeDefinition @"
public static class RhinoBootChart {
    public static Rhino.Runtime.InProcess.RhinoCore Core;
    public static void Start() { if (Core == null) Core = new Rhino.Runtime.InProcess.RhinoCore(new string[] { "/nosplash" }, Rhino.Runtime.InProcess.WindowStyle.Hidden); }
    public static void Stop() { if (Core != null) { Core.Dispose(); Core = null; } }
}
"@
}
[RhinoBootChart]::Start()
$script:failures = @(); $script:checks = 0
function Check([string]$label, $condition, [string]$detail = '') {
    $script:checks++
    if ([bool]$condition) { Write-Host ("  OK     {0} {1}" -f $label, $detail) -ForegroundColor Green }
    else { Write-Host ("  FALHOU {0} {1}" -f $label, $detail) -ForegroundColor Red; $script:failures += $label }
}
$SolMode = [Grasshopper.Kernel.GH_SolutionMode]::Silent
$ci = [Globalization.CultureInfo]::InvariantCulture
function N($v) { [Grasshopper.Kernel.Types.GH_Number]::new([double]$v) }
function Pth($i) { [Grasshopper.Kernel.Data.GH_Path]::new([int]$i) }
function Errs($r) { @($r.C.RuntimeMessages([Grasshopper.Kernel.GH_RuntimeMessageLevel]::Error)) }
function Warns($r) { @($r.C.RuntimeMessages([Grasshopper.Kernel.GH_RuntimeMessageLevel]::Warning)) }
function Rems($r) { @($r.C.RuntimeMessages([Grasshopper.Kernel.GH_RuntimeMessageLevel]::Remark)) }

# $ys / $xs: arrays de arrays (um por ramo). $mode: valor do input Mode (texto/numero) ou $null; $menu: ajustes diretos no componente
function Invoke-Chart($ys, $xs = $null, $mode = $null, [scriptblock]$menu = $null, $target = $null) {
    $doc = [Grasshopper.Kernel.GH_Document]::new(); $doc.Enabled = $true
    $c = [Buraqueira_Tools.ChartLine_Component]::new(); $c.CreateAttributes()
    if ($menu) { & $menu $c }   # antes dos dados: um Read() no menu nao apaga os dados persistentes
    for ($b = 0; $b -lt $ys.Count; $b++) { foreach ($v in $ys[$b]) { $c.Params.Input[1].PersistentData.Append((N $v), (Pth $b)) } }
    if ($xs) { for ($b = 0; $b -lt $xs.Count; $b++) { foreach ($v in $xs[$b]) { $c.Params.Input[0].PersistentData.Append((N $v), (Pth $b)) } } }
    if ($null -ne $mode) { $c.Params.Input[8].PersistentData.Append([Grasshopper.Kernel.Types.GH_ObjectWrapper]::new($mode)) }
    if ($null -ne $target) { $c.Params.Input[10].PersistentData.Append([Grasshopper.Kernel.Types.GH_ObjectWrapper]::new($target)) }
    $null = $doc.AddObject($c, $false)
    $sw = [Diagnostics.Stopwatch]::StartNew(); $doc.NewSolution($true, $SolMode); $sw.Stop()
    [pscustomobject]@{ C = $c; Doc = $doc; Ms = $sw.Elapsed.TotalMilliseconds }
}
function Pts($r, $branch) { @($r.C.Params.Output[2].VolatileData.get_Branch((Pth $branch)) | ForEach-Object { $_.Value }) }
function Crvs($r, $branch) { @($r.C.Params.Output[3].VolatileData.get_Branch((Pth $branch)) | ForEach-Object { $_.Value }) }
function Refs($r, $branch) { @($r.C.Params.Output[4].VolatileData.get_Branch((Pth $branch)) | ForEach-Object { $_.Value }) }
function Rep($r) { $r.C.Params.Output[1].VolatileData.get_Branch(0)[0].Value }

# --------------------------------------------------------------- compatibilidade com o componente ANTIGO
if ($SaveOld) {
    Write-Host "Gravando componentes da versao ANTIGA em $SaveOld" -ForegroundColor Yellow
    $out = New-Object Text.StringBuilder
    foreach ($case in @(@('cols', $true, $true), @('lines_combined', $false, $true), @('lines_plain', $false, $false))) {
        $c = [Buraqueira_Tools.ChartLine_Component]::new(); $c.CreateAttributes()
        $c.IsColumnsMode = $case[1]; $c.ShowCombinedCurve = $case[2]; $c.DisplayShowStats = $false
        $chunk = [GH_IO.Serialization.GH_LooseChunk]::new('Component')
        if (-not $c.Write($chunk)) { throw 'Write falhou' }
        $null = $out.AppendLine($case[0] + '|' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($chunk.Serialize_Xml())))
    }
    [IO.File]::WriteAllText($SaveOld, $out.ToString())
    [RhinoBootChart]::Stop(); exit 0
}
if ($OpenOld) {
    Write-Host "`n=== COMPATIBILIDADE: lendo componentes gravados pela versao ANTIGA ===" -ForegroundColor Cyan
    foreach ($line in [IO.File]::ReadAllLines($OpenOld)) {
        $name, $b64 = $line.Split('|')
        $chunk = [GH_IO.Serialization.GH_LooseChunk]::new('Component'); $chunk.Deserialize_Xml([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($b64)))
        $c = [Buraqueira_Tools.ChartLine_Component]::new(); $c.CreateAttributes()
        Check "[$name] Read aceitou o chunk antigo" ($c.Read($chunk))
        Check "[$name] mesmo GUID e 15 entradas / 8 saidas" ($c.ComponentGuid.ToString() -eq '1c2d3e4f-5a6b-7c8d-9e0f-1a2b3c4d5e6f' -and $c.Params.Input.Count -eq 15 -and $c.Params.Output.Count -eq 8)
        $expKind = if ($name -eq 'cols') { 'Distribution' } else { 'Lines' }
        $expLine = if ($name -eq 'lines_combined') { 'Trend' } else { 'Raw' }
        Check "[$name] Kind = $expKind" ($c.Kind.ToString() -eq $expKind) ("(" + $c.Kind + ")")
        if ($name -ne 'cols') { Check "[$name] LineMode = $expLine (Linhas+Tendencia antigo vira Trend; sem curva combinada vira Raw)" ($c.LineMode.ToString() -eq $expLine) ("(" + $c.LineMode + ")") }
        Check "[$name] DisplayShowStats=false preservado" (-not $c.DisplayShowStats)
    }
    [RhinoBootChart]::Stop()
    if ($script:failures.Count -gt 0) { Write-Host "`n$($script:failures.Count) de $($script:checks) verificacoes FALHARAM" -ForegroundColor Red; exit 1 }
    Write-Host "`nCOMPATIBILIDADE: TODOS OS $($script:checks) TESTES PASSARAM" -ForegroundColor Green; exit 0
}

Write-Host "=== LINE CHART & STATISTICS / HISTOGRAM ===" -ForegroundColor Cyan
$tmp = [Buraqueira_Tools.ChartLine_Component]::new(); $tmp.CreateAttributes()
Check "Nome/Nickname/GUID/Categoria preservados" ($tmp.Name -eq 'Line Chart & Statistics' -and $tmp.NickName -eq 'ChartLine' -and $tmp.ComponentGuid.ToString() -eq '1c2d3e4f-5a6b-7c8d-9e0f-1a2b3c4d5e6f' -and $tmp.Category -eq 'Glaux Tools' -and $tmp.SubCategory -eq 'Visual')
Check "15 entradas e 8 saidas (as 11/6 originais na mesma ordem + novas no fim)" (($tmp.Params.Input | ForEach-Object { $_.NickName }) -join ',' -eq 'X,Y,T,XLab,YLab,Stats,W,H,Mode,Combined,Target,GTol,Pt,Sec,STol' -and ($tmp.Params.Output | ForEach-Object { $_.NickName }) -join ',' -eq 'Img,Rep,Pts,Crv,Refs,Trend,GPts,Groups')
Check "X e Y continuam em arvore" ($tmp.Params.Input[0].Access -eq [Grasshopper.Kernel.GH_ParamAccess]::tree -and $tmp.Params.Input[1].Access -eq [Grasshopper.Kernel.GH_ParamAccess]::tree)

Write-Host "`n[T01] X irregular [0,1,2,10] / Y [0,10,5,20]" -ForegroundColor Yellow
$r = Invoke-Chart @(, @(0, 10, 5, 20)) @(, @(0, 1, 2, 10))
Check "Sem erros; padrao = Raw" ((Errs $r).Count -eq 0 -and $r.C.LineMode -eq 'Raw' -and $r.C.Kind -eq 'Lines')
$sc = $r.C.Scene; $m = [Buraqueira_Tools.ChartMapper]::new([Drawing.RectangleF]::new(100, 50, 400, 200), $sc)
Check "Distancia na tela proporcional a X real (8:1)" ([math]::Abs((($m.X(10) - $m.X(2)) / ($m.X(2) - $m.X(1))) - 8) -lt 1e-3)
$p = Pts $r 0
Check "Pts = pares (X,Y) originais" (($p | ForEach-Object { '{0},{1}' -f $_.X, $_.Y }) -join ';' -eq '0,0;1,10;2,5;10,20')
$crv = (Crvs $r 0)[0]
Check "Crv (Raw) = polilinha com exatamente 4 vertices (sem pontos artificiais)" ($crv.Degree -eq 1 -and $crv.Points.Count -eq 4) ("(grau {0}, {1} pts de controle)" -f $crv.Degree, $crv.Points.Count)
Check "PNG gerado 900x550" ($r.C.Params.Output[0].VolatileData.get_Branch(0)[0].Value.Width -eq 900 -and $r.C.CachedChartBmp.Height -eq 550)

Write-Host "`n[T02] Pico [0,10,0,10,0]: Smooth sem overshoot" -ForegroundColor Yellow
$r = Invoke-Chart @(, @(0, 10, 0, 10, 0)) $null 'smooth'
$bb = (Crvs $r 0)[0].GetBoundingBox($true)
Check "Modo Smooth via input Mode" ($r.C.LineMode -eq 'Smooth')
Check "Curva do Rhino entre Y=0 e Y=10 (nao ultrapassa os extremos)" ($bb.Min.Y -ge -1e-9 -and $bb.Max.Y -le 10 + 1e-9) ("(bbox Y $($bb.Min.Y) .. $($bb.Max.Y))")
Check "Curva passa pelos 5 pontos" (((0..4) | ForEach-Object { $x = $_; $t = $null; $cp = (Crvs $r 0)[0].ClosestPoint([Rhino.Geometry.Point3d]::new($x, @(0, 10, 0, 10, 0)[$x], 0), [ref]$t); ([Rhino.Geometry.Point3d]::new($x, @(0, 10, 0, 10, 0)[$x], 0)).DistanceTo((Crvs $r 0)[0].PointAt($t)) -lt 1e-6 }) -notcontains $false)
Check "Dominio X preservado (sem extrapolar)" ($bb.Min.X -ge -1e-9 -and $bb.Max.X -le 4 + 1e-9)

Write-Host "`n[T03] X desordenado [3,1,2] / Y [30,10,20]" -ForegroundColor Yellow
$r = Invoke-Chart @(, @(30, 10, 20)) @(, @(3, 1, 2))
Check "Pares ordenados conjuntamente: (1,10)(2,20)(3,30)" ((Pts $r 0 | ForEach-Object { '{0},{1}' -f $_.X, $_.Y }) -join ';' -eq '1,10;2,20;3,30')
Check "Aviso informativo de ordenacao" ((Rems $r).Count -ge 1 -and ((Rems $r) -join ' ') -like '*ordenados juntos*')

Write-Host "`n[T04] Constante [5,5,5,5]" -ForegroundColor Yellow
foreach ($mode in 'raw', 'smooth', 'trend') {
    $r = Invoke-Chart @(, @(5, 5, 5, 5)) $null $mode
    $bb = (Crvs $r 0)[0].GetBoundingBox($true)
    Check "[$mode] curva horizontal em Y=5" ($bb.Min.Y -ge 5 - 1e-9 -and $bb.Max.Y -le 5 + 1e-9 -and (Errs $r).Count -eq 0)
}

Write-Host "`n[T05] Estatisticas Y = [2,4,4,4,5,5,7,9] (media 5, mediana 4.5, sigma amostral 2.138)" -ForegroundColor Yellow
foreach ($mode in 'raw', 'smooth', 'trend') {
    $r = Invoke-Chart @(, @(2, 4, 4, 4, 5, 5, 7, 9)) $null $mode
    $refs = Refs $r 0
    Check "[$mode] Refs: media horizontal em 5; mediana 4.5; +-sigma" ([math]::Abs($refs[0].From.Y - 5) -lt 1e-12 -and $refs[0].From.Y -eq $refs[0].To.Y -and [math]::Abs($refs[1].From.Y - 4.5) -lt 1e-12 -and [math]::Abs($refs[3].From.Y - (5 + [math]::Sqrt(32 / 7))) -lt 1e-12 -and [math]::Abs($refs[4].From.Y - (5 - [math]::Sqrt(32 / 7))) -lt 1e-12)
    Check "[$mode] Relatorio traz mu = 5.0000 e sigma = 2.1381" ((Rep $r) -like '*Média (μ):*5.0000*' -and (Rep $r) -like '*2.1381*')
}

Write-Host "`n[T06/T07] XY Bars" -ForegroundColor Yellow
$r = Invoke-Chart @(, @(5, 12, 8)) @(, @(10, 20, 30)) 'xy'
Check "Modo XYBars via input" ($r.C.Kind -eq 'XYBars' -and (Errs $r).Count -eq 0)
Check "Pts = topo de cada barra (X real, altura Y)" ((Pts $r 0 | ForEach-Object { '{0},{1}' -f $_.X, $_.Y }) -join ';' -eq '10,5;20,12;30,8')
$cs = Crvs $r 0
Check "3 retangulos: base Y=0, alturas 5/12/8, centros 10/20/30, largura 8" ($cs.Count -eq 3 -and [math]::Abs($cs[1].GetBoundingBox($true).Max.Y - 12) -lt 1e-9 -and [math]::Abs($cs[1].GetBoundingBox($true).Min.Y) -lt 1e-9 -and [math]::Abs((($cs[2].GetBoundingBox($true).Min.X + $cs[2].GetBoundingBox($true).Max.X) / 2) - 30) -lt 1e-9 -and [math]::Abs($cs[0].GetBoundingBox($true).Diagonal.X - 8) -lt 1e-9)
Check "Nao reinterpreta X como amostras (sem bins/KDE; Trend vazio)" ($null -eq $r.C.CachedHistData -and @($r.C.Params.Output[5].VolatileData.AllData($true)).Count -eq 0)
$r = Invoke-Chart @(, @(5, 10, 15, 20)) @(, @(1, 2, 8, 20)) 'xybars'
$cs = Crvs $r 0; $cx = $cs | ForEach-Object { ($_.GetBoundingBox($true).Min.X + $_.GetBoundingBox($true).Max.X) / 2 }
Check "T07 X irregular: centros em 1,2,8,20 (nao equidistantes), sem sobreposicao" ([math]::Abs($cx[3] - 20) -lt 1e-9 -and [math]::Abs($cx[2] - 8) -lt 1e-9 -and $cs[1].GetBoundingBox($true).Max.X -le $cs[2].GetBoundingBox($true).Min.X -and $cs[0].GetBoundingBox($true).Max.X -le $cs[1].GetBoundingBox($true).Min.X)

Write-Host "`n[T08] Histograma estatistico" -ForegroundColor Yellow
$obs = @(1, 1, 2, 2, 2, 3, 4)
$r = Invoke-Chart @(, $obs) @(, @(100, 200, 300, 400, 500, 600, 700)) 'hist'
Check "Distribution: X ignorado (so aviso informativo), sem erro" ((Errs $r).Count -eq 0 -and ((Rems $r) -join ' ') -like '*X são ignorados*' -and $r.C.Kind -eq 'Distribution')
$h = $r.C.CachedHistData
Check "Soma das contagens = 7 observacoes; moda no valor 2 (3 vezes)" ((($h.BinCounts | Measure-Object -Sum).Sum) -eq 7 -and $h.MaxBinCount -eq 3 -and $h.TotalCount -eq 7)
Check "Limites: bins contiguos de largura constante; centros = ponto medio" ($h.BinEdges.Length -eq $h.BinCounts.Length + 1 -and [math]::Abs($h.BinEdges[1] - $h.BinEdges[0] - $h.BinWidth) -lt 1e-9 -and [math]::Abs($h.BinCenters[0] - ($h.BinEdges[0] + $h.BinEdges[1]) / 2) -lt 1e-9)
Check "Crv = 1 retangulo por bin; Pts (centro, contagem)" ((Crvs $r 0).Count -eq $h.BinCounts.Length -and (Pts $r 0).Count -eq $h.BinCounts.Length)
Check "Relatorio lista limites e contagens" ((Rep $r) -like '*Limites dos bins*' -and (Rep $r) -like '*Contagens:*')
$r = Invoke-Chart @(, $obs) $null 1
Check "Mode = 1 (contrato antigo) continua sendo histograma" ($r.C.Kind -eq 'Distribution')
$r = Invoke-Chart @(, $obs) $null $true
Check "Mode = true (contrato antigo) continua sendo histograma" ($r.C.Kind -eq 'Distribution')
$r = Invoke-Chart @(, $obs) $null 2
Check "Mode = 2 = XY Bars" ($r.C.Kind -eq 'XYBars')
$r = Invoke-Chart @(, $obs) $null 'banana'
Check "Mode desconhecido: aviso e mantem modo" ((Warns $r).Count -ge 1 -and $r.C.Kind -eq 'Lines')

Write-Host "`n[T09] Negativos" -ForegroundColor Yellow
$r = Invoke-Chart @(, @(-10, 20, -5, 15)) @(, @(1, 2, 3, 4)) 'xy'
$cs = Crvs $r 0
Check "Barras negativas crescem abaixo de Y=0; positivas acima" ($cs[0].GetBoundingBox($true).Min.Y -eq -10 -and $cs[0].GetBoundingBox($true).Max.Y -eq 0 -and $cs[1].GetBoundingBox($true).Min.Y -eq 0 -and $cs[1].GetBoundingBox($true).Max.Y -eq 20 -and $cs[2].GetBoundingBox($true).Min.Y -eq -5)
Check "Dominio Y do grafico inclui -10..20 e a linha de base" ($r.C.Scene.MinY -lt -10 -and $r.C.Scene.MaxY -gt 20)

Write-Host "`n[T10] Data Trees: duas series independentes" -ForegroundColor Yellow
$r = Invoke-Chart @(@(10, 20, 30), @(30, 15, 40)) @(@(1, 2, 3), @(1, 2, 3))
Check "2 series; Pts {0} = (1,10)(2,20)(3,30) e {1} = (1,30)(2,15)(3,40)" ((Pts $r 0 | ForEach-Object { '{0},{1}' -f $_.X, $_.Y }) -join ';' -eq '1,10;2,20;3,30' -and (Pts $r 1 | ForEach-Object { '{0},{1}' -f $_.X, $_.Y }) -join ';' -eq '1,30;2,15;3,40')
Check "Estatisticas por serie (medias 20 e 28.33) e curva agregada = media por X" ((Rep $r) -like '*20.0000*' -and (Rep $r) -like '*28.3333*' -and $r.C.Params.Output[5].VolatileData.get_Branch(0)[0].Value.PointAtStart.Y -eq 20)
$r = Invoke-Chart @(@(10, 20, 30), @(30, 15, 40)) @(, @(1, 2, 3))
Check "Um unico ramo X vale para todos os ramos Y" ((Errs $r).Count -eq 0 -and (Pts $r 1 | ForEach-Object { $_.X }) -join ',' -eq '1,2,3')
$r = Invoke-Chart @(@(10, 20, 30), @(30, 15)) @(@(1, 2, 3), @(1, 2, 3))
Check "Contagens diferentes: erro com ramo e contagens; serie nao desenhada; sem excecao" ((Errs $r).Count -eq 1 -and ((Errs $r) -join ' ') -like '*{1}*3 valores*Y tem 2*' -and $r.C.DisplaySeries.Count -eq 1)
$r = Invoke-Chart @(, @(10, 20, 30)) @(, @(1, 2))
Check "X com menos valores que Y: erro (nao usa o indice como X)" ((Errs $r).Count -ge 1 -and $r.C.DisplaySeries.Count -eq 0)
$r = Invoke-Chart @(@(1, 2), @(3, 4), @(5, 6)) @(@(1, 2), @(1, 2))
Check "X com 2 ramos para 3 Y: erro por ramo, sem misturar" ((Errs $r).Count -ge 1)
$r = Invoke-Chart @(, @(7, 8, 9))
Check "Sem X: X = indice 0,1,2 (e o relatorio diz isso)" ((Pts $r 0 | ForEach-Object { $_.X }) -join ',' -eq '0,1,2' -and (Rep $r) -like '*X = índice*')

Write-Host "`n[Trend e agregada]" -ForegroundColor Yellow
$rnd = [Random]::new(5); $y = 1..80 | ForEach-Object { 30 + 10 * [math]::Sin($_ / 8) + $rnd.NextDouble() * 30 }
$r = Invoke-Chart @(, $y) $null 'trend'
$trendCrv = $r.C.Params.Output[5].VolatileData.get_Branch(0)[0].Value; $bbT = $trendCrv.GetBoundingBox($true)
Check "Trend: dentro do intervalo dos dados, com vertices apenas nos X originais" ($bbT.Min.Y -ge ($y | Measure-Object -Minimum).Minimum - 1e-9 -and $bbT.Max.Y -le ($y | Measure-Object -Maximum).Maximum + 1e-9 -and $trendCrv.Points.Count -eq 80)
Check "Modo Trend: Crv = tendencia; dados reais continuam em Pts" ((Crvs $r 0)[0].Points.Count -eq 80 -and (Pts $r 0).Count -eq 80 -and $r.C.TrendWindow -ge 5)
$r = Invoke-Chart @(, $y)
Check "Raw: Trend (saida) tambem entrega a tendencia, mas a curva de dados continua sendo os pontos reais" ($r.C.Params.Output[5].VolatileDataCount -eq 1 -and (Crvs $r 0)[0].PointAtStart.Y -eq $y[0])

Write-Host "`n[Alvo / badge / PNG / menu]" -ForegroundColor Yellow
$r = Invoke-Chart @(, @(1, 2, 3, 4)) $null $null $null '2 To 3'
Check "Alvo 2 To 3: Delta = media(2.5) - 2.5 = 0 e Refs {999}" ([math]::Abs($r.C.DeltaTarget) -lt 1e-12 -and (Refs $r 999).Count -eq 1)
$r = Invoke-Chart @(, @(1, 2, 3, 4)) $null $null { param($c) $c.DisplayShowStats = $false }
Check "Menu: Show Stats desligado vale sem conexao (sem linhas de estatistica na cena nem em Refs)" (($r.C.Scene.Refs.Count -eq 0) -and (Refs $r 0).Count -eq 0)
$r = Invoke-Chart @(, @(1, 2, 3, 4)) $null $null { param($c) $c.Params.Input[6].PersistentData.Clear(); $c.Params.Input[7].PersistentData.Clear(); $c.Params.Input[6].PersistentData.Append([Grasshopper.Kernel.Types.GH_Integer]::new(1200)); $c.Params.Input[7].PersistentData.Append([Grasshopper.Kernel.Types.GH_Integer]::new(700)) }
Check "Width/Height: PNG 1200x700" ($r.C.CachedChartBmp.Width -eq 1200 -and $r.C.CachedChartBmp.Height -eq 700)

Write-Host "`n[Casos limite]" -ForegroundColor Yellow
$r = Invoke-Chart @(, @(42))
Check "1 ponto: sem excecao, 1 ponto em Pts" ((Errs $r).Count -eq 0 -and (Pts $r 0).Count -eq 1 -and $r.C.CachedChartBmp.Width -gt 0)
$r = Invoke-Chart @(, @(1, [double]::NaN, 3, [double]::PositiveInfinity, 5)) @(, @(1, 2, 3, 4, 5))
Check "NaN/Inf: par inteiro descartado com aviso (X e Y nunca desalinham)" ((Warns $r).Count -ge 1 -and (Pts $r 0 | ForEach-Object { '{0},{1}' -f $_.X, $_.Y }) -join ';' -eq '1,1;3,3;5,5')
$r = Invoke-Chart @(, @(1, 2, 2, 3)) @(, @(1, 2, 2, 3))
Check "X repetido: observacao informativa (nao Warning); amostras preservadas (4 pontos)" ((Warns $r).Count -eq 0 -and ((Rems $r) -join ' ') -like '*repetidas detectadas*' -and (Pts $r 0).Count -eq 4)
$r = Invoke-Chart @(, @(1, 2, 2, 5)) @(, @(1, 2, 2, 3)) 'smooth'
Check "X repetido em Smooth: sem erro e curva continua funcao de X" ((Errs $r).Count -eq 0 -and (Crvs $r 0).Count -eq 1)
$doc = [Grasshopper.Kernel.GH_Document]::new(); $doc.Enabled = $true; $c = [Buraqueira_Tools.ChartLine_Component]::new(); $c.CreateAttributes(); $null = $doc.AddObject($c, $false); $doc.NewSolution($true, $SolMode)
Check "Sem dados: mensagem 'Sem Dados Y' e sem excecao" ($c.Message -eq 'Sem Dados Y' -or $c.RuntimeMessages([Grasshopper.Kernel.GH_RuntimeMessageLevel]::Warning).Count -ge 0)

Write-Host "`n[Desempenho 5000 pontos]" -ForegroundColor Yellow
$rnd = [Random]::new(9); $big = 1..5000 | ForEach-Object { $rnd.NextDouble() * 100 }
foreach ($mode in 'raw', 'smooth', 'trend') {
    $r = Invoke-Chart @(, $big) $null $mode
    Check "[$mode] 5000 pontos: solve < 8 s" ($r.Ms -lt 8000 -and (Errs $r).Count -eq 0) ("({0:N0} ms)" -f $r.Ms)
}

function Rows($r, $branch) { @($r.C.Params.Output[7].VolatileData.get_Branch((Pth $branch)) | ForEach-Object { $_.Value }) }
function GPts($r, $branch) { @($r.C.Params.Output[6].VolatileData.get_Branch((Pth $branch)) | ForEach-Object { $_.Value }) }
function SetIn($c, [int]$i, $goo) { $c.Params.Input[$i].PersistentData.Clear(); $c.Params.Input[$i].PersistentData.Append($goo) }
$gridX = @(0, 0, 0, 1, 1, 1, 2, 2, 2); $gridLux = @(100, 200, 300, 350, 400, 450, 450, 500, 600)

Write-Host "`n[A] Malha 3x3 (X = Point.X, Y = lux): X repetido nao e erro" -ForegroundColor Yellow
$r = Invoke-Chart @(, $gridLux) @(, $gridX) 'mean'
Check "Mode 'mean' = agregacao por media; sem erro e sem Warning" ($r.C.RepeatMode -eq 'Mean' -and (Errs $r).Count -eq 0 -and (Warns $r).Count -eq 0)
Check "Observacao informativa: X repetidos, comum em malhas, grupos, tolerancia, amostras retidas" (((Rems $r) -join ' ') -like '*repetidas detectadas*9 de 9 amostras*comum em malhas*Grupos de X: 3*tolerância*Amostras retidas: 9*')
Check "Pts = 9 amostras ORIGINAIS preservadas" ((Pts $r 0).Count -eq 9 -and ((Pts $r 0 | ForEach-Object { $_.Y }) -join ',') -eq '100,200,300,350,400,450,450,500,600')
$g = GPts $r 0
Check "GPts: (0,200) (1,400) (2,516.67)" ($g.Count -eq 3 -and [math]::Abs($g[0].Y - 200) -lt 1e-9 -and [math]::Abs($g[1].Y - 400) -lt 1e-9 -and [math]::Abs($g[2].Y - 516.6666667) -lt 1e-6 -and $g[2].X -eq 2)
$rows = Rows $r 0
Check "Group Table: cabecalho + 3 grupos com N, media, mediana, min, max, sigma e indices originais" ($rows.Count -eq 4 -and $rows[0] -like 'Series;Group;X;Count;Mean;Median;Min;Max;StdDev;OriginalIndices' -and $rows[1] -like '*;0;0;3;200;200;100;300;100;0|1|2' -and $rows[3] -like '*;2;2;3;516.666*;500;450;600;*;6|7|8')
Check "Relatorio: amostras originais, grupos, tolerancia, media GLOBAL x media das medias, tabela" ((Rep $r) -like '*Amostras originais:  9*' -and (Rep $r) -like '*Grupos de X:         3*' -and (Rep $r) -like '*Média GLOBAL*372.2222*' -and (Rep $r) -like '*Média das médias por X:*372.2222*' -and (Rep $r) -like '*Grupos (X | N*')
Check "Crv (Mean/Raw) = polilinha das medias por X (3 vertices), nao as 9 amostras" ((Crvs $r 0).Count -eq 1 -and (Crvs $r 0)[0].Points.Count -eq 3)
foreach ($pair in @(@('median', @(200, 400, 500)), @('min', @(100, 350, 450)), @('max', @(300, 450, 600)))) {
    $rr = Invoke-Chart @(, $gridLux) @(, $gridX) $pair[0]
    Check ("Modo " + $pair[0] + ": GPts = " + ($pair[1] -join ',')) (((GPts $rr 0 | ForEach-Object { [math]::Round($_.Y, 6) }) -join ',') -eq ($pair[1] -join ','))
}
$rr = Invoke-Chart @(, $gridLux) @(, $gridX) 'range'
Check "Range: faixa min-max na cena e linha = media" ($rr.C.Scene.BandSeries.Count -eq 1 -and -not $rr.C.Scene.BandSeries[0].IsStdDev -and [math]::Abs($rr.C.Scene.BandSeries[0].Hi[2].Y - 600) -lt 1e-9)
$rr = Invoke-Chart @(, $gridLux) @(, $gridX) 'sd'
Check "StdDev: faixa media +- sigma" ($rr.C.Scene.BandSeries.Count -eq 1 -and $rr.C.Scene.BandSeries[0].IsStdDev -and [math]::Abs($rr.C.Scene.BandSeries[0].Hi[0].Y - 300) -lt 1e-9)
$rr = Invoke-Chart @(, $gridLux) @(, $gridX) 'raw'
Check "Raw (padrao): amostras preservadas so como pontos, sem linha em zigue-zague; Crv vazio" ($rr.C.RepeatMode -eq 'Raw' -and $rr.C.Scene.Polylines.Count -eq 0 -and $rr.C.Scene.AnyPointsOnly -and (Crvs $rr 0).Count -eq 0 -and (Pts $rr 0).Count -eq 9)
$rr = Invoke-Chart @(, $gridLux) @(, $gridX) 'smooth mean'
Check "Smooth + Mean: suaviza a serie AGREGADA (Bezier por 3 medias), nao as 9 amostras" ($rr.C.LineMode -eq 'Smooth' -and $rr.C.RepeatMode -eq 'Mean' -and (Crvs $rr 0)[0].SpanCount -eq 2)
$rr = Invoke-Chart @(, $gridLux) @(, $gridX) 'xy mean'
Check "xy mean: XYBars com uma barra por X (3 retangulos)" ($rr.C.Kind -eq 'XYBars' -and (Crvs $rr 0).Count -eq 3)
$rr = Invoke-Chart @(, $gridLux) @(, $gridX) 'xy samples'
Check "xy samples: barras das amostras originais (9), explicito" ((Crvs $rr 0).Count -eq 9)
$rr = Invoke-Chart @(, $gridLux) @(, $gridX) 'mean' { param($c) $c.ShowSamples = $false }
Check "Amostras originais desligaveis (menu): so linha agregada" ($rr.C.Scene.Markers.Count -eq 0 -and $rr.C.Scene.Polylines.Count -eq 1)

Write-Host "`n[B] Malha 55x55 = 3025 amostras" -ForegroundColor Yellow
$rnd = [Random]::new(21); $bx = New-Object 'System.Collections.Generic.List[double]'; $by = New-Object 'System.Collections.Generic.List[double]'
for ($i = 0; $i -lt 55; $i++) { for ($j = 0; $j -lt 55; $j++) { $bx.Add($i * 0.2); $by.Add(300 + 200 * [math]::Sin($i / 9.0) * [math]::Cos($j / 11.0) + $rnd.NextDouble() * 20) } }
foreach ($mode in 'raw', 'mean', 'range', 'smooth range', 'trend mean', 'xy mean') {
    $rb = Invoke-Chart @(, $by.ToArray()) @(, $bx.ToArray()) $mode
    Check "[$mode] 3025 amostras, sem erro, solve < 8 s" ((Errs $rb).Count -eq 0 -and $rb.Ms -lt 8000 -and $rb.C.DisplaySeries[0].Count -eq 3025 -and (Pts $rb 0).Count -eq 3025) ("({0:N0} ms)" -f $rb.Ms)
}
$rb = Invoke-Chart @(, $by.ToArray()) @(, $bx.ToArray()) 'mean'
Check "55 grupos de 55 amostras; GPts 55; 3025 amostras retidas" ($rb.C.DisplaySeries[0].Groups.Count -eq 55 -and ($rb.C.DisplaySeries[0].Groups | ForEach-Object { $_.Count } | Sort-Object -Unique) -eq 55 -and (GPts $rb 0).Count -eq 55 -and (Rows $rb 0).Count -eq 56)
$sm = (Invoke-Chart @(, $by.ToArray()) @(, $bx.ToArray()) 'smooth mean')
$means = $sm.C.DisplaySeries[0].Groups | ForEach-Object { $_.Mean }; $mm = $means | Measure-Object -Minimum -Maximum
$bbS = (Crvs $sm 0)[0].GetBoundingBox($true)
Check "Smooth sobre as medias: sem picos acima/abaixo das medias por X" ($bbS.Max.Y -le $mm.Maximum + 1e-6 -and $bbS.Min.Y -ge $mm.Minimum - 1e-6)
$fam = [Drawing.FontFamily]::new('Arial'); $plot = [Drawing.RectangleF]::new(60, 40, 640, 380)
foreach ($mode in 'raw', 'range') {
    $rb = Invoke-Chart @(, $by.ToArray()) @(, $bx.ToArray()) $mode
    $sw = [Diagnostics.Stopwatch]::StartNew(); $bmp = [Drawing.Bitmap]::new(800, 480); $gr = [Drawing.Graphics]::FromImage($bmp)
    1..5 | ForEach-Object { [Buraqueira_Tools.ChartLineRenderer]::DrawPlot($gr, $plot, $rb.C.Scene, [Buraqueira_Tools.ChartTheme]::Dark(1.0), $fam) }
    $sw.Stop(); $gr.Dispose(); $bmp.Dispose()
    Check "[$mode] Canvas responsivo: 5 quadros de desenho com 3025 amostras (proxy do repaint) < 2 s" ($sw.ElapsedMilliseconds -lt 2000) ("({0:N0} ms)" -f $sw.ElapsedMilliseconds)
}

Write-Host "`n[C] Coordenadas quase iguais" -ForegroundColor Yellow
$nx = @(); $ny = @(); foreach ($col in 1.5, 2.0, 2.5) { foreach ($d in 0, 1e-9, -1e-9, 2e-9) { $nx += ($col + $d); $ny += (100 * $col + $d * 1e9) } }
$rc = Invoke-Chart @(, $ny) @(, $nx) 'mean'
Check "Tolerancia automatica: 12 X numericamente distintos = 3 grupos; mensagem diz 'automatica'" ($rc.C.DisplaySeries[0].Groups.Count -eq 3 -and ((Rems $rc) -join ' ') -like '*automática*' -and (Pts $rc 0).Count -eq 12)
$rc = Invoke-Chart @(, $ny) @(, $nx) 'mean' { param($c) SetIn $c 11 (N 1e-12) }
Check "Group Tolerance informada pelo usuario (1e-12) respeitada: 12 grupos; relatorio diz 'definida pelo usuario'" ($rc.C.DisplaySeries[0].Groups.Count -eq 12 -and (Rep $rc) -like '*definida pelo usuário*')

Write-Host "`n[D] Amostras desordenadas" -ForegroundColor Yellow
$perm = @(5, 2, 8, 0, 7, 3, 1, 6, 4)
$rd = Invoke-Chart @(, ($perm | ForEach-Object { $gridLux[$_] })) @(, ($perm | ForEach-Object { $gridX[$_] })) 'mean'
Check "Pares reordenados juntos: mesmas medias 200/400/516.67 e indices originais rastreados" (((GPts $rd 0 | ForEach-Object { [math]::Round($_.Y, 3).ToString($ci) }) -join ',') -eq '200,400,516.667' -and (Rows $rd 0)[1] -like '*;3;200;200;100;300;100;*')

Write-Host "`n[E] Quantidades desiguais (2, 5, 10)" -ForegroundColor Yellow
$ex = @(0, 0) + (1, 1, 1, 1, 1) + (2, 2, 2, 2, 2, 2, 2, 2, 2, 2); $ey = @(100, 200) + (300, 300, 300, 300, 300) + (0, 0, 0, 0, 0, 0, 0, 0, 0, 1000)
$re = Invoke-Chart @(, $ey) @(, $ex) 'mean'
Check "Media GLOBAL (164.7059) e media das medias por X (183.3333) reportadas separadamente" ((Rep $re) -like '*Média GLOBAL*164.7059*' -and (Rep $re) -like '*Média das médias por X:*183.3333*' -and [math]::Abs($re.C.Scene.PooledMean - 2800 / 17) -lt 1e-9)

Write-Host "`n[F] Valores constantes (500 lux)" -ForegroundColor Yellow
foreach ($mode in 'mean', 'range', 'smooth mean', 'trend mean') {
    $rf = Invoke-Chart @(, @(500, 500, 500, 500, 500, 500, 500, 500, 500)) @(, $gridX) $mode
    $bb = (Crvs $rf 0)[0].GetBoundingBox($true)
    Check "[$mode] linha horizontal em 500" ([math]::Abs($bb.Min.Y - 500) -lt 1e-9 -and [math]::Abs($bb.Max.Y - 500) -lt 1e-9 -and (Errs $rf).Count -eq 0)
}

Write-Host "`n[G] Perfil espacial: amostras proximas a uma linha, ordenadas pela distancia ao longo dela" -ForegroundColor Yellow
$pts = @(); $vals = @(); for ($i = 0; $i -le 10; $i++) { for ($j = 0; $j -le 10; $j++) { $pts += [Rhino.Geometry.Point3d]::new($i, $j, 0.75); $vals += (100 * $i + $j) } }
function SetPts($c, $pts) { foreach ($p in $pts) { $c.Params.Input[12].PersistentData.Append([Grasshopper.Kernel.Types.GH_Point]::new($p), (Pth 0)) } }
$lineH = [Rhino.Geometry.LineCurve]::new([Rhino.Geometry.Point3d]::new(0, 5, 0), [Rhino.Geometry.Point3d]::new(10, 5, 0))
$rg = Invoke-Chart @(, $vals) $null $null { param($c) SetPts $c $pts; SetIn $c 13 ([Grasshopper.Kernel.Types.GH_Curve]::new($lineH)) }
$gp = Pts $rg 0
Check "Linha em Y=5: 11 amostras (as outras 110 ficam fora), X = distancia 0..10, Y = valor original (100x+5)" ($gp.Count -eq 11 -and (($gp | ForEach-Object { [math]::Round($_.X, 6) }) -join ',') -eq '0,1,2,3,4,5,6,7,8,9,10' -and (($gp | ForEach-Object { $_.Y }) -join ',') -eq '5,105,205,305,405,505,605,705,805,905,1005')
Check "Mensagem do perfil: tolerancia automatica (0.5), distancia em planta, fora do perfil" (((Rems $rg) -join ' ') -like '*Perfil espacial*11 de 121 amostras*0.5*planta*110 fora do perfil*')
Check "Rotulo do eixo X do PNG vira 'Distancia ao longo do perfil' (ProfileActive) e X dado e ignorado" ($rg.C.ProfileActive)
$lineD = [Rhino.Geometry.LineCurve]::new([Rhino.Geometry.Point3d]::new(0, 0, 0), [Rhino.Geometry.Point3d]::new(10, 10, 0))
$rg = Invoke-Chart @(, $vals) $null $null { param($c) SetPts $c $pts; SetIn $c 13 ([Grasshopper.Kernel.Types.GH_Curve]::new($lineD)) }
$gp = Pts $rg 0
Check "Diagonal: 11 amostras, distancias i*sqrt(2), valores 101*i" ($gp.Count -eq 11 -and [math]::Abs($gp[10].X - 10 * [math]::Sqrt(2)) -lt 1e-9 -and $gp[10].Y -eq 1010 -and $gp[3].Y -eq 303)
$rg = Invoke-Chart @(, $vals) $null $null { param($c) SetPts $c $pts; SetIn $c 13 ([Grasshopper.Kernel.Types.GH_Curve]::new($lineH)); SetIn $c 14 (N 1.0) }
Check "Section Tolerance = 1: 33 amostras (Y=4,5,6) com 3 por estacao; agregando por media recupera 100x+5" (($rg.C.DisplaySeries[0].Count -eq 33) -and $rg.C.DisplaySeries[0].Groups.Count -eq 11)
$rg = Invoke-Chart @(, $vals) $null 'mean' { param($c) SetPts $c $pts; SetIn $c 13 ([Grasshopper.Kernel.Types.GH_Curve]::new($lineH)); SetIn $c 14 (N 1.0) }
Check "...e a media por estacao = 100x+5" ((((GPts $rg 0) | ForEach-Object { [math]::Round($_.Y, 6) }) -join ',') -eq '5,105,205,305,405,505,605,705,805,905,1005')
$rg = Invoke-Chart @(, $vals) $null $null { param($c) $c.ProfilePlan = $false; SetPts $c $pts; SetIn $c 13 ([Grasshopper.Kernel.Types.GH_Curve]::new($lineH)) }
Check "Modo 3D: curva em z=0 e amostras em z=0.75 (> 0.5): nenhuma amostra; aviso, sem excecao" ($rg.C.DisplaySeries.Count -eq 0 -and (Warns $rg).Count -ge 1)
$rg = Invoke-Chart @(, $vals) $null $null { param($c) $c.ProfilePlan = $false; SetPts $c $pts; SetIn $c 13 ([Grasshopper.Kernel.Types.GH_Curve]::new($lineH)); SetIn $c 14 (N 1.0) }
Check "Modo 3D com tolerancia 1.0: usa distancia 3D (0.75)" ($rg.C.DisplaySeries[0].Count -ge 11)
$rg = Invoke-Chart @(, $vals) $null $null { param($c) SetIn $c 13 ([Grasshopper.Kernel.Types.GH_Curve]::new($lineH)) }
Check "Section Curve sem Sample Points: erro explicito e X/Y normal" ((Errs $rg).Count -ge 1 -and ((Errs $rg) -join ' ') -like '*sem Sample Points*')
$rg = Invoke-Chart @(, $vals) $null $null { param($c) SetPts $c $pts[0..9]; SetIn $c 13 ([Grasshopper.Kernel.Types.GH_Curve]::new($lineH)) }
Check "Pontos e valores com contagens diferentes: erro por ramo" (((Errs $rg) -join ' ') -like '*10 pontos e 121 valores*')

Write-Host "`n[H] Histograma de lux (X espacial nao participa)" -ForegroundColor Yellow
$lux = 1..400 | ForEach-Object { 300 + 80 * [math]::Sin($_ / 7.0) + ($_ % 13) * 4 }
$h1 = Invoke-Chart @(, $lux) @(, (1..400 | ForEach-Object { $_ % 20 })) 'hist'
$h2 = Invoke-Chart @(, $lux) @(, (1..400 | ForEach-Object { 400 - $_ })) 'hist'
$c1 = $h1.C.CachedHistData.BinCounts -join ','; $c2 = $h2.C.CachedHistData.BinCounts -join ','
Check "Contagens por bin identicas com X espacial diferente; soma = 400; eixo Y = frequencia, eixo X = lux (limites dos bins em lux)" ($c1 -eq $c2 -and ($h1.C.CachedHistData.BinCounts | Measure-Object -Sum).Sum -eq 400 -and $h1.C.CachedHistData.BinEdges[0] -le ($lux | Measure-Object -Minimum).Minimum -and $h1.C.CachedHistData.BinEdges[-1] -ge ($lux | Measure-Object -Maximum).Maximum)
Check "Distribution: X ignorado com observacao, sem aviso de X repetido" (((Rems $h1) -join ' ') -like '*X são ignorados*' -and ((Rems $h1) -join ' ') -notlike '*repetidas detectadas*')

Write-Host "`n[Data Trees {0;0} e {0;1}] cada ramo e independente" -ForegroundColor Yellow
$doc = [Grasshopper.Kernel.GH_Document]::new(); $doc.Enabled = $true
$c = [Buraqueira_Tools.ChartLine_Component]::new(); $c.CreateAttributes(); $c.RepeatMode = [Buraqueira_Tools.ChartRepeatMode]::Mean
$p00 = [Grasshopper.Kernel.Data.GH_Path]::new(0, 0); $p01 = [Grasshopper.Kernel.Data.GH_Path]::new(0, 1)
for ($i = 0; $i -lt 9; $i++) {
    $c.Params.Input[0].PersistentData.Append((N $gridX[$i]), $p00); $c.Params.Input[1].PersistentData.Append((N $gridLux[$i]), $p00)
    $c.Params.Input[0].PersistentData.Append((N $gridX[$i]), $p01); $c.Params.Input[1].PersistentData.Append((N ($gridLux[$i] * 2)), $p01)
}
$null = $doc.AddObject($c, $false); $doc.NewSolution($true, $SolMode)
$g0 = $c.Params.Output[6].VolatileData.get_Branch($p00) | ForEach-Object { [math]::Round($_.Value.Y, 3).ToString($ci) }; $g1 = $c.Params.Output[6].VolatileData.get_Branch($p01) | ForEach-Object { [math]::Round($_.Value.Y, 3).ToString($ci) }
Check "{0;0} medias 200,400,516.667 e {0;1} dobradas 400,800,1033.333 (sem misturar simulacoes)" (($g0 -join ',') -eq '200,400,516.667' -and ($g1 -join ',') -eq '400,800,1033.333' -and $c.Params.Output[2].VolatileData.PathCount -eq 2)

Write-Host "`n[J] Serializacao dos novos modos" -ForegroundColor Yellow
$c = [Buraqueira_Tools.ChartLine_Component]::new(); $c.CreateAttributes(); $c.RepeatMode = [Buraqueira_Tools.ChartRepeatMode]::Range; $c.ShowSamples = $false; $c.ProfilePlan = $false
$chunk = [GH_IO.Serialization.GH_LooseChunk]::new('Component'); $null = $c.Write($chunk)
$c2 = [Buraqueira_Tools.ChartLine_Component]::new(); $c2.CreateAttributes(); $null = $c2.Read($chunk)
Check "RepeatMode=Range, ShowSamples=false, ProfilePlan=false restaurados" ($c2.RepeatMode -eq 'Range' -and -not $c2.ShowSamples -and -not $c2.ProfilePlan)
$c3 = [Buraqueira_Tools.ChartLine_Component]::new(); $c3.CreateAttributes(); $null = $c3.Read([GH_IO.Serialization.GH_LooseChunk]::new('Component'))
Check "Chunk sem as chaves novas (arquivo anterior): Raw, amostras visiveis, perfil em planta" ($c3.RepeatMode -eq 'Raw' -and $c3.ShowSamples -and $c3.ProfilePlan)
$tmpCsv = Join-Path $env:TEMP ('chart_csv_' + [guid]::NewGuid().ToString('N'))
$paths = [Buraqueira_Tools.ChartLine_Component]::WriteCsv($tmpCsv, (Invoke-Chart @(, $gridLux) @(, $gridX) 'mean').C.DisplaySeries)
$sl = [IO.File]::ReadAllLines($paths[0]); $gl = [IO.File]::ReadAllLines($paths[1])
Check "CSV: amostras originais (9) e grupos agregados (3) em arquivos distintos" ($sl.Count -eq 10 -and $gl.Count -eq 4 -and $sl[0] -like '*Series;OriginalIndex;X;Y;Group;GroupX' -and $gl[1] -like '*;3;200;200;100;300;100;0|1|2')
[IO.File]::Delete($paths[0]); [IO.File]::Delete($paths[1])
Write-Host "`n[T12] Serializacao (chunk do componente = mesmo XML do .gh)" -ForegroundColor Yellow
$c = [Buraqueira_Tools.ChartLine_Component]::new(); $c.CreateAttributes()
$c.Kind = [Buraqueira_Tools.ChartLineKind]::XYBars; $c.LineMode = [Buraqueira_Tools.ChartLineMode]::Smooth; $c.DisplayShowStats = $false; $c.ShowCombinedCurve = $false
$chunk = [GH_IO.Serialization.GH_LooseChunk]::new('Component'); Check "Write" ($c.Write($chunk))
$c2 = [Buraqueira_Tools.ChartLine_Component]::new(); $c2.CreateAttributes(); Check "Read" ($c2.Read($chunk))
Check "Configuracoes restauradas (XYBars, Smooth, Stats off, Combined off)" ($c2.Kind -eq 'XYBars' -and $c2.LineMode -eq 'Smooth' -and -not $c2.DisplayShowStats -and -not $c2.ShowCombinedCurve)
$c2.Kind = [Buraqueira_Tools.ChartLineKind]::Distribution; $chunk = [GH_IO.Serialization.GH_LooseChunk]::new('Component'); $null = $c2.Write($chunk)
$xml = $chunk.Serialize_Xml(); Check "Chunk ainda grava IsColumnsMode=true (versoes anteriores leem como histograma)" ($xml -like '*IsColumnsMode*True*' -or $xml -like '*IsColumnsMode*true*')
# mesma configuracao => mesmo resultado depois de reabrir
$a = Invoke-Chart @(, @(5, 12, 8)) @(, @(10, 20, 30)) 'xy'
$ca = [Buraqueira_Tools.ChartLine_Component]::new(); $ca.CreateAttributes(); $ca.Kind = [Buraqueira_Tools.ChartLineKind]::XYBars
$ch = [GH_IO.Serialization.GH_LooseChunk]::new('Component'); $null = $ca.Write($ch)
$b = Invoke-Chart @(, @(5, 12, 8)) @(, @(10, 20, 30)) $null { param($c) $null = $c.Read($ch) }
Check "Componente reaberto produz as mesmas barras" ($b.C.Kind -eq 'XYBars' -and (Crvs $b 0).Count -eq (Crvs $a 0).Count -and (Pts $b 0)[1].Y -eq 12)

[RhinoBootChart]::Stop()
if ($script:failures.Count -gt 0) { Write-Host "`n$($script:failures.Count) de $($script:checks) verificacoes FALHARAM:" -ForegroundColor Red; $script:failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }; exit 1 }
Write-Host "`nTODOS OS $($script:checks) TESTES PASSARAM" -ForegroundColor Green
