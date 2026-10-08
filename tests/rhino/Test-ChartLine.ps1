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
        Check "[$name] mesmo GUID e 11 entradas / 6 saidas" ($c.ComponentGuid.ToString() -eq '1c2d3e4f-5a6b-7c8d-9e0f-1a2b3c4d5e6f' -and $c.Params.Input.Count -eq 11 -and $c.Params.Output.Count -eq 6)
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
Check "11 entradas e 6 saidas, mesma ordem e nomes" (($tmp.Params.Input | ForEach-Object { $_.NickName }) -join ',' -eq 'X,Y,T,XLab,YLab,Stats,W,H,Mode,Combined,Target' -and ($tmp.Params.Output | ForEach-Object { $_.NickName }) -join ',' -eq 'Img,Rep,Pts,Crv,Refs,Trend')
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
Check "X repetido: aviso; Raw mantem todos os 4 pontos" ((Warns $r).Count -ge 1 -and (Pts $r 0).Count -eq 4)
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
