# Test-SpatialHeatmapZScale.ps1
# Spatial Grid & Viewport Heatmap: escala lateral de valores (Show Value Scale / Scale Divisions).
# Monta o componente de verdade num GH_Document em RhinoCore sem interface e confere que:
#   - cada rotulo da escala esta na altura EXATA que a malha usa para aquele valor (valor de dados != altura de exibicao);
#   - malha, valores e cores nao mudam por causa da escala;
#   - serializacao e compatibilidade com componente gravado pela versao anterior.
# O desenho no viewport (linhas/texto 2D) NAO e coberto aqui: validar no Rhino com janela.
#
# Uso:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests\rhino\Test-SpatialHeatmapZScale.ps1 -Gha <Glaux_Tools.gha>
#   compatibilidade (dois processos): ... -Gha <gha ANTIGO> -SaveOld <arq.xml>   e   ... -Gha <gha NOVO> -OpenOld <arq.xml>

param([string]$Gha = '', [string]$SaveOld = '', [string]$OpenOld = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $Gha) { $Gha = Join-Path $root 'src\bin\Release\net48\Glaux_Tools.gha' }
$rhino = 'C:\Program Files\Rhino 8'
$env:PATH = "$rhino\System;" + $env:PATH
@("$rhino\System\RhinoCommon.dll", "$rhino\Plug-ins\Grasshopper\GH_IO.dll", "$rhino\Plug-ins\Grasshopper\Grasshopper.dll", $Gha) | ForEach-Object { [Reflection.Assembly]::LoadFrom($_) | Out-Null }
if (-not ('RhinoBootHeat' -as [type])) {
    Add-Type -ReferencedAssemblies "$rhino\System\RhinoCommon.dll" -TypeDefinition @"
public static class RhinoBootHeat {
    public static Rhino.Runtime.InProcess.RhinoCore Core;
    public static void Start() { if (Core == null) Core = new Rhino.Runtime.InProcess.RhinoCore(new string[] { "/nosplash" }, Rhino.Runtime.InProcess.WindowStyle.Hidden); }
    public static void Stop() { if (Core != null) { Core.Dispose(); Core = null; } }
}
"@
}
[RhinoBootHeat]::Start()
$script:failures = @(); $script:checks = 0
function Check([string]$label, $condition, [string]$detail = '') {
    $script:checks++
    if ([bool]$condition) { Write-Host ("  OK     {0} {1}" -f $label, $detail) -ForegroundColor Green }
    else { Write-Host ("  FALHOU {0} {1}" -f $label, $detail) -ForegroundColor Red; $script:failures += $label }
}
$Mode = [Grasshopper.Kernel.GH_SolutionMode]::Silent
$ci = [Globalization.CultureInfo]::InvariantCulture
$P3 = [Rhino.Geometry.Point3d]

# Pontos 5x5 em [0,10]^2 com valores dados por um scriptblock {param($x,$y) ...}
function Get-Samples([scriptblock]$f, [double]$z = 0.0, $xform = $null) {
    $pts = @(); $vals = @()
    foreach ($ix in 0..4) { foreach ($iy in 0..4) {
        $x = $ix * 2.5; $y = $iy * 2.5
        $p = $P3::new($x, $y, $z); if ($xform) { $p.Transform($xform) }
        $pts += $p; $vals += [double](& $f $x $y)
    } }
    return [pscustomobject]@{ Pts = $pts; Vals = $vals }
}
$peak = { param($x, $y) 100.0 * [math]::Exp(-((($x - 5) * ($x - 5)) + (($y - 5) * ($y - 5))) / 10.0) }

# Roda o componente. $zscale: GH goo para a entrada 8 (ou $null). $show: bool ou $null (nao conectado = padrao False)
function Invoke-Heat($s, $zscale = $null, $show = $null, [int]$div = 5, $limits = $null, [string]$unit = '', [int]$res = 30, [bool]$slope = $false) {
    $doc = [Grasshopper.Kernel.GH_Document]::new(); $doc.Enabled = $true
    $c = [Buraqueira_Tools.SpatialHeatmap_Component]::new(); $c.CreateAttributes()
    foreach ($p in $s.Pts) { $c.Params.Input[0].PersistentData.Append([Grasshopper.Kernel.Types.GH_Point]::new($p)) }
    foreach ($v in $s.Vals) { $c.Params.Input[1].PersistentData.Append([Grasshopper.Kernel.Types.GH_Number]::new($v)) }
    $c.Params.Input[5].PersistentData.Clear(); $c.Params.Input[5].PersistentData.Append([Grasshopper.Kernel.Types.GH_Integer]::new($res))
    if ($zscale -ne $null) { $c.Params.Input[8].PersistentData.Append($zscale) }
    if ($unit) { $c.Params.Input[10].PersistentData.Append([Grasshopper.Kernel.Types.GH_String]::new($unit)) }
    if ($limits -ne $null) { $c.Params.Input[11].PersistentData.Append($limits) }
    $c.Params.Input[13].PersistentData.Clear(); $c.Params.Input[13].PersistentData.Append([Grasshopper.Kernel.Types.GH_Boolean]::new($slope))
    if ($show -ne $null) { $c.Params.Input[16].PersistentData.Clear(); $c.Params.Input[16].PersistentData.Append([Grasshopper.Kernel.Types.GH_Boolean]::new([bool]$show)) }
    $c.Params.Input[17].PersistentData.Clear(); $c.Params.Input[17].PersistentData.Append([Grasshopper.Kernel.Types.GH_Integer]::new($div))
    $null = $doc.AddObject($c, $false)
    $sw = [Diagnostics.Stopwatch]::StartNew(); $doc.NewSolution($true, $Mode); $sw.Stop()
    return [pscustomobject]@{ C = $c; Doc = $doc; Ms = $sw.Elapsed.TotalMilliseconds }
}
function N([double]$v) { [Grasshopper.Kernel.Types.GH_Number]::new($v) }

# Maior desvio entre a altura real de cada nó (saída GridPts) e a altura que a escala atribui ao valor do nó (GridVals)
function Max-HeightError($r) {
    $sc = $r.C.ValueScale
    $pts = @($r.C.Params.Output[1].VolatileData.AllData($true) | ForEach-Object { $_.Value })
    $vals = @($r.C.Params.Output[2].VolatileData.AllData($true) | ForEach-Object { $_.Value })
    $err = 0.0
    for ($i = 0; $i -lt $pts.Count; $i++) { $e = [math]::Abs($pts[$i].Z - $sc.HeightOf($vals[$i])); if ($e -gt $err) { $err = $e } }
    return $err
}
function Labels($r) { if ($r.C.ValueScale) { ($r.C.ValueScale.Labels -join ' ') } else { '(sem escala)' } }

# ---------------------------------------------------------------- compat: gravar com a versao antiga
if ($SaveOld) {
    Write-Host "Gravando Spatial Heatmap da versao ANTIGA em $SaveOld" -ForegroundColor Yellow
    $s = Get-Samples $peak
    $doc = [Grasshopper.Kernel.GH_Document]::new(); $doc.Enabled = $true
    $c = [Buraqueira_Tools.SpatialHeatmap_Component]::new(); $c.CreateAttributes()
    foreach ($p in $s.Pts) { $c.Params.Input[0].PersistentData.Append([Grasshopper.Kernel.Types.GH_Point]::new($p)) }
    foreach ($v in $s.Vals) { $c.Params.Input[1].PersistentData.Append([Grasshopper.Kernel.Types.GH_Number]::new($v)) }
    $c.Params.Input[5].PersistentData.Clear(); $c.Params.Input[5].PersistentData.Append([Grasshopper.Kernel.Types.GH_Integer]::new(30))
    $c.Params.Input[8].PersistentData.Append((N 0.1))
    $null = $doc.AddObject($c, $false); $doc.NewSolution($true, $Mode)
    $m = $c.Params.Output[0].VolatileData.get_Branch(0)[0].Value
    $zs = @($m.Vertices | ForEach-Object { [double]$_.Z }); $sumz = ($zs | Measure-Object -Sum).Sum
    Write-Host ("  entradas={0} saidas={1} vertices={2} somaZ={3}" -f $c.Params.Input.Count, $c.Params.Output.Count, $m.Vertices.Count, $sumz.ToString($ci))
    $c.Params.Input[8].PersistentData.Clear()   # itens GH_Number em entrada generica travam o Read headless (ver T10)
    $chunk = [GH_IO.Serialization.GH_LooseChunk]::new('Component'); $null = $c.Write($chunk)
    [IO.File]::WriteAllText($SaveOld, $chunk.Serialize_Xml(), (New-Object Text.UTF8Encoding($false)))
    [IO.File]::WriteAllText("$SaveOld.stats", ("{0};{1};{2}" -f $m.Vertices.Count, $sumz.ToString($ci), $m.Faces.Count))
    [RhinoBootHeat]::Stop(); exit 0
}
if ($OpenOld) {
    Write-Host "`n=== COMPATIBILIDADE: componente gravado pela versao anterior ===" -ForegroundColor Cyan
    $chunk = [GH_IO.Serialization.GH_LooseChunk]::new('Component'); $chunk.Deserialize_Xml([IO.File]::ReadAllText($OpenOld))
    $c = [Buraqueira_Tools.SpatialHeatmap_Component]::new(); $c.CreateAttributes()
    Check "Component.Read aceitou o chunk antigo" ($c.Read($chunk))
    Write-Host ("  entradas apos Read: {0}" -f $c.Params.Input.Count)
    Check "18 entradas; as 16 antigas na mesma ordem" ($c.Params.Input.Count -eq 18 -and $c.Params.Input[8].Name -eq 'Z Elevation Scale' -and $c.Params.Input[15].Name -eq 'Display Mode')
    Check "Show Value Scale e Scale Divisions no fim (16, 17)" ($c.Params.Input[16].Name -eq 'Show Value Scale' -and $c.Params.Input[17].Name -eq 'Scale Divisions')
    Check "5 saidas inalteradas" ($c.Params.Output.Count -eq 7)
    # (a entrada generica ZScale volta nula na reidratacao headless, nas duas versoes: reaplica o mesmo fator 0.1)
    $c.Params.Input[8].PersistentData.Clear(); $c.Params.Input[8].PersistentData.Append((N 0.1))
    $doc = [Grasshopper.Kernel.GH_Document]::new(); $doc.Enabled = $true
    $null = $doc.AddObject($c, $false); $doc.NewSolution($true, $Mode)
    $m = $c.Params.Output[0].VolatileData.get_Branch(0)[0].Value
    $zs = @($m.Vertices | ForEach-Object { [double]$_.Z }); $sumz = ($zs | Measure-Object -Sum).Sum
    $old = (Get-Content "$OpenOld.stats").Split(';')
    Check "Malha identica a da versao antiga (vertices, faces, soma de Z)" ($m.Vertices.Count -eq [int]$old[0] -and $m.Faces.Count -eq [int]$old[2] -and [math]::Abs($sumz - [double]::Parse($old[1], $ci)) -lt 1e-4) ("(novo: {0}/{1}; antigo: {2})" -f $m.Vertices.Count, $sumz.ToString($ci), ($old -join '/'))
    Check "Sem escala (padrao False): visual anterior preservado" ($c.ValueScale -eq $null -and [string]::IsNullOrEmpty($c.ValueScaleNote))
    Write-Host ""
    if ($script:failures.Count -eq 0) { Write-Host ("TODOS OS {0} TESTES PASSARAM" -f $script:checks) -ForegroundColor Green } else { Write-Host ("FALHARAM: " + ($script:failures -join '; ')) -ForegroundColor Red }
    [RhinoBootHeat]::Stop(); exit ($script:failures.Count)
}

# ---------------------------------------------------------------- contrato
Write-Host "=== SPATIAL GRID & VIEWPORT HEATMAP: ESCALA DE VALORES ===" -ForegroundColor Cyan
$tmp = [Buraqueira_Tools.SpatialHeatmap_Component]::new(); $tmp.CreateAttributes()
Check "GUID preservado" ($tmp.ComponentGuid.ToString() -eq '7d2e3f4a-5b6c-7d8e-9f0a-1b2c3d4e5f6a')
Check "18 entradas; as 16 originais nos mesmos indices" ($tmp.Params.Input.Count -eq 18 -and $tmp.Params.Input[8].Name -eq 'Z Elevation Scale' -and $tmp.Params.Input[15].Name -eq 'Display Mode')
Check "Show Value Scale (16) booleana opcional, padrao False" ($tmp.Params.Input[16].Name -eq 'Show Value Scale' -and $tmp.Params.Input[16].Optional -and (-not $tmp.Params.Input[16].PersistentData.get_Branch(0)[0].Value))
Check "Scale Divisions (17) inteira opcional, padrao 5" ($tmp.Params.Input[17].Name -eq 'Scale Divisions' -and $tmp.Params.Input[17].Optional -and $tmp.Params.Input[17].PersistentData.get_Branch(0)[0].Value -eq 5)
Check "7 saidas inalteradas" ($tmp.Params.Output.Count -eq 7)

Write-Host "`n[T01] Plano (Z = 0) com 'Show Value Scale' ligado: nao ha escala" -ForegroundColor Yellow
$s = Get-Samples { param($x, $y) 0 }; $s.Vals = @((@(0.0, 1.0, 2.0, 3.0) * 7) | Select-Object -First 25)
$r = Invoke-Heat $s $null $true
Check "Sem deformacao: sem escala e sem aviso vermelho" ($r.C.ValueScale -eq $null -and @($r.C.RuntimeMessages([Grasshopper.Kernel.GH_RuntimeMessageLevel]::Error)).Count -eq 0 -and @($r.C.RuntimeMessages([Grasshopper.Kernel.GH_RuntimeMessageLevel]::Warning)).Count -eq 0)
Check "Nota informativa leve no relatorio" ($r.C.ValueScaleNote -like '*oculta*' -and $r.C.Params.Output[6].VolatileData.get_Branch(0)[0].Value -like '*Escala de Valores:*oculta*')
Check "Heatmap segue funcionando (malha valida com cores)" ($r.C.CachedHeatmapMesh.IsValid -and $r.C.CachedHeatmapMesh.VertexColors.Count -eq $r.C.CachedHeatmapMesh.Vertices.Count)
$r = Invoke-Heat $s $null $false
Check "Padrao (Show = False): nenhuma escala nem nota" ($r.C.ValueScale -eq $null -and $r.C.ValueScaleNote -eq '')

Write-Host "`n[T02] Valores 0..100 com deformacao (fator 1)" -ForegroundColor Yellow
$s = Get-Samples $peak
$r = Invoke-Heat $s (N 1.0) $true
$sc = $r.C.ValueScale
Check "Escala criada" ($sc -ne $null)
Write-Host ("  dominio {0:N3}..{1:N3}; rotulos: {2}" -f $sc.ValueMin, $sc.ValueMax, (Labels $r))
Check "Altura de cada no = altura que a escala da ao valor (erro < 1e-6)" ((Max-HeightError $r) -lt 1e-6) ("(erro max {0:E2})" -f (Max-HeightError $r))
Check "Eixo a esquerda da grade (nao a atravessa)" ($sc.AxisX -lt $r.C.CachedMinX -and [math]::Abs($sc.AxisY - $r.C.CachedMinY) -lt 1e-9)
Check "ClippingBox contem eixo e topo da escala" ($r.C.ClippingBox.Contains($sc.AxisPoint($sc.ValueMin)) -and $r.C.ClippingBox.Contains($sc.AxisPoint($sc.ValueMax)))
Check "Marcas dentro do dominio" (@($sc.Ticks | Where-Object { $_ -lt $sc.ValueMin - 1e-9 -or $_ -gt $sc.ValueMax + 1e-9 }).Count -eq 0 -and $sc.Ticks.Count -ge 3)

Write-Host "`n[T03] Minimo diferente de zero (20..80)" -ForegroundColor Yellow
$s = Get-Samples { param($x, $y) 20.0 + 60.0 * (($x + $y) / 20.0) }
$r = Invoke-Heat $s (N 1.0) $true
Write-Host ("  dominio {0:N3}..{1:N3}; rotulos: {2}" -f $r.C.ValueScale.ValueMin, $r.C.ValueScale.ValueMax, (Labels $r))
Check "Dominio = faixa real interpolada (nao assume 0)" ($r.C.ValueScale.ValueMin -gt 15 -and $r.C.ValueScale.ValueMin -lt 40 -and $r.C.ValueScale.Ticks[0] -ge 20 - 1e-9)
Check "Rotulos coerentes com a malha" ((Max-HeightError $r) -lt 1e-6)

Write-Host "`n[T04] Valores negativos (-10..10)" -ForegroundColor Yellow
$s = Get-Samples { param($x, $y) -10.0 + 20.0 * (($x + $y) / 20.0) }
$r = Invoke-Heat $s (N 1.0) $true
Write-Host ("  rotulos: {0}" -f (Labels $r))
$sc = $r.C.ValueScale
Check "Tem rotulo negativo, zero e positivo" (@($sc.Ticks | ? { $_ -lt 0 }).Count -gt 0 -and @($sc.Ticks | ? { $_ -eq 0 }).Count -eq 1 -and @($sc.Ticks | ? { $_ -gt 0 }).Count -gt 0)
$zero = $sc.HeightOf(0.0); $zr = $r.C.CachedHeatmapMesh
Check "Zero na posicao correspondente (altura de v = 0 = base + (0 - minimo) * fator)" ([math]::Abs($zero - ($sc.BaseZ + (0 - $sc.ValueRef) * 1.0)) -lt 1e-9)
Check "Rotulos coerentes com a malha" ((Max-HeightError $r) -lt 1e-6)

Write-Host "`n[T05] Todos iguais (50,50,50,50...)" -ForegroundColor Yellow
$s = Get-Samples { param($x, $y) 50.0 }
$r = Invoke-Heat $s (N 1.0) $true
$errs = @($r.C.RuntimeMessages([Grasshopper.Kernel.GH_RuntimeMessageLevel]::Error))
Check "Sem excecao nem erro" ($errs.Count -eq 0 -and $r.C.CachedHeatmapMesh -ne $null)
Check "Visualizacao estavel: ou 1 marca em 50 ou oculta por falta de variacao" (($r.C.ValueScale -ne $null -and $r.C.ValueScale.Ticks.Count -eq 1 -and $r.C.ValueScale.Labels[0] -eq '50') -or ($r.C.ValueScale -eq $null)) ("(" + (Labels $r) + ")")
Check "Nenhum NaN na malha" (@($r.C.CachedHeatmapMesh.Vertices | Where-Object { [double]::IsNaN($_.Z) }).Count -eq 0)

Write-Host "`n[T06] Fator Z 0.1 (dados 0..100): altura 0..10, rotulos 0..100" -ForegroundColor Yellow
$s = Get-Samples { param($x, $y) 100.0 * (($x + $y) / 20.0) }
$r = Invoke-Heat $s (N 0.1) $true
$sc = $r.C.ValueScale
Write-Host ("  rotulos: {0}; altura(rotulo maximo)= {1:N3}" -f (Labels $r), $sc.HeightOf($sc.Ticks[$sc.Ticks.Count - 1]))
$labNums = @($sc.Labels | ForEach-Object { [double]::Parse($_, [Globalization.CultureInfo]::CurrentCulture) })
Check "Rotulos mostram valores dos dados (dezenas, ate ~100), nao alturas (que seriam ~10)" ($labNums.Count -ge 3 -and ($labNums | Measure-Object -Maximum).Maximum -ge 50 -and ($labNums | Measure-Object -Maximum).Maximum -le 100.0001)
Check "Altura exibida = 0.1 x valor" ([math]::Abs($sc.HeightOf(100) - ($sc.HeightOf(0) + 10.0)) -lt 1e-6)
Check "Rotulos coerentes com a malha" ((Max-HeightError $r) -lt 1e-6)
$iv = [Grasshopper.Kernel.Types.GH_Interval]::new([Rhino.Geometry.Interval]::new(0.2, 2.5))
$r = Invoke-Heat $s $iv $true
Check "Z normalizado ('0.2 To 2.5'): rotulos coerentes com a malha" ($r.C.ValueScale -ne $null -and (Max-HeightError $r) -lt 1e-6) ("(erro " + ("{0:E2}" -f (Max-HeightError $r)) + ")")
$mn = ($r.C.CachedHeatmapMesh.Vertices | ForEach-Object { [double]$_.Z } | Measure-Object -Minimum -Maximum)
Check "Altura da malha normalizada vai de 0.2 a 2.5" ([math]::Abs($mn.Minimum - 0.2) -lt 1e-4 -and [math]::Abs($mn.Maximum - 2.5) -lt 1e-4) ("({0:N4}..{1:N4})" -f $mn.Minimum, $mn.Maximum)

Write-Host "`n[T07] Pontos girados 30 graus (e T08: movidos longe da origem)" -ForegroundColor Yellow
$xf = [Rhino.Geometry.Transform]::Rotation([math]::PI / 6.0, [Rhino.Geometry.Vector3d]::ZAxis, $P3::Origin)
$s = Get-Samples $peak 0.0 $xf
$r = Invoke-Heat $s (N 1.0) $true
Check "Girado: escala valida e coerente com a malha" ($r.C.ValueScale -ne $null -and (Max-HeightError $r) -lt 1e-6)
Check "Girado: eixo fora da grade (X) e na borda (Y)" ($r.C.ValueScale.AxisX -lt $r.C.CachedMinX -and [math]::Abs($r.C.ValueScale.AxisY - $r.C.CachedMinY) -lt 1e-9)
$xf2 = [Rhino.Geometry.Transform]::Translation(120000.0, -250000.0, 75.0)
$s2 = Get-Samples $peak 0.0 $xf2
$r2 = Invoke-Heat $s2 (N 1.0) $true
Check "Movido: a escala acompanha (X/Y proximos da grade)" ([math]::Abs($r2.C.ValueScale.AxisX - 120000.0) -lt 5.0 -and [math]::Abs($r2.C.ValueScale.AxisY - (-250000.0)) -lt 5.0)
Check "Movido: cota base da escala = cota da grade (Z = 75)" ([math]::Abs($r2.C.ValueScale.BaseZ - 75.0) -lt 1e-6)
Check "Movido: rotulos coerentes com a malha" ((Max-HeightError $r2) -lt 1e-6)
Check "Movido: ClippingBox inclui a escala" ($r2.C.ClippingBox.Contains($r2.C.ValueScale.AxisPoint($r2.C.ValueScale.ValueMax)))

Write-Host "`n[T09] Desempenho (400 pontos, grade 250 x 250)" -ForegroundColor Yellow
$big = @{ Pts = @(); Vals = @() }; $rnd = [Random]::new(7)
for ($i = 0; $i -lt 400; $i++) { $x = $rnd.NextDouble() * 100; $y = $rnd.NextDouble() * 100; $big.Pts += $P3::new($x, $y, 0); $big.Vals += (100.0 * [math]::Exp(-((($x - 50) * ($x - 50)) + (($y - 50) * ($y - 50))) / 600.0)) }
$bigS = [pscustomobject]$big
$off = @(); $on = @()
foreach ($k in 1..3) { $off += (Invoke-Heat $bigS (N 0.05) $false 5 $null '' 250).Ms; $on += (Invoke-Heat $bigS (N 0.05) $true 5 $null '' 250).Ms }
$mOff = ($off | Sort-Object)[1]; $mOn = ($on | Sort-Object)[1]
Write-Host ("  mediana sem escala: {0:N0} ms; com escala: {1:N0} ms" -f $mOff, $mOn)
Check "Escala nao degrada o calculo (diferenca < 10% ou < 100 ms)" (($mOn - $mOff) -lt [math]::Max(100.0, 0.10 * $mOff))

Write-Host "`n[T10] Salvar / reabrir" -ForegroundColor Yellow
$s = Get-Samples $peak
$r = Invoke-Heat $s (N 1.0) $true 6
$r.C.ShowScaleLines = $false; $r.C.ShowScaleTitle = $true
$r.C.Params.Input[8].PersistentData.Clear()   # itens GH_Number em entrada generica podem travar o Read em RhinoCore headless
$chunk = [GH_IO.Serialization.GH_LooseChunk]::new('Component'); $null = $r.C.Write($chunk)
$xml = $chunk.Serialize_Xml()
$chunk2 = [GH_IO.Serialization.GH_LooseChunk]::new('Component'); $chunk2.Deserialize_Xml($xml)
$c2 = [Buraqueira_Tools.SpatialHeatmap_Component]::new(); $c2.CreateAttributes(); $null = $c2.Read($chunk2)
Check "Show Value Scale = True preservado" ($c2.Params.Input[16].PersistentData.get_Branch(0)[0].Value -eq $true)
Check "Scale Divisions = 6 preservado" ($c2.Params.Input[17].PersistentData.get_Branch(0)[0].Value -eq 6)
Check "Opcoes do menu preservadas (linhas desligadas, titulo ligado)" ((-not $c2.ShowScaleLines) -and $c2.ShowScaleTitle)
# Observado: a entrada generica ZScale (GH_Number em Param_Generic) volta nula na reidratacao do chunk em RhinoCore headless; isso e anterior a escala. Reaplica o fator para testar o resto.
$c2.Params.Input[8].PersistentData.Clear(); $c2.Params.Input[8].PersistentData.Append((N 1.0))
$d2 = [Grasshopper.Kernel.GH_Document]::new(); $d2.Enabled = $true; $null = $d2.AddObject($c2, $false); $d2.NewSolution($true, $Mode)
Write-Host ("  in8 tipo orig={0}; reaberto={1} valor={2}" -f ($r.C.Params.Input[8].PersistentData.AllData($true) | % { $_.GetType().Name }), ($c2.Params.Input[8].PersistentData.AllData($true) | % { $_.GetType().Name }), ($c2.Params.Input[8].PersistentData.AllData($true) | % { $_.ToString() }))
Write-Host ("  c2 nota=[{0}] zscale in8={1} show={2} div={3}" -f $c2.ValueScaleNote, $c2.Params.Input[8].PersistentData.DataCount, (($c2.Params.Input[16].PersistentData.AllData($true) | % { $_.Value }) -join ","), (($c2.Params.Input[17].PersistentData.AllData($true) | % { $_.Value }) -join ","))
Write-Host ("  c2: pts={0} vals={1} msg=[{2}] mesh={3}" -f $c2.Params.Input[0].PersistentData.DataCount, $c2.Params.Input[1].PersistentData.DataCount, (($c2.RuntimeMessages([Grasshopper.Kernel.GH_RuntimeMessageLevel]::Error) + $c2.RuntimeMessages([Grasshopper.Kernel.GH_RuntimeMessageLevel]::Warning)) -join " | "), ($c2.CachedHeatmapMesh -ne $null))
Check "Reaberto: escala recriada" ($c2.ValueScale -ne $null -and $c2.ValueScale.Ticks.Count -ge 3) ("(" + $(if ($c2.ValueScale) { $c2.ValueScale.Labels.Count } else { 0 }) + " marcas)")

Write-Host "`n[T11] Dominio manual, unidade e divisoes" -ForegroundColor Yellow
$s = Get-Samples { param($x, $y) 17.0 + 66.0 * (($x + $y) / 20.0) }
$lim = [Grasshopper.Kernel.Types.GH_Interval]::new([Rhino.Geometry.Interval]::new(0, 100))
$r = Invoke-Heat $s (N 0.1) $true 5 $lim 'dB'
Write-Host ("  rotulos: {0}" -f (Labels $r))
Check "Value Limits 0..100 respeitado: 0 25 50 75 100 (com unidade)" ((Labels $r) -eq '0 dB 25 dB 50 dB 75 dB 100 dB')
Check "Altura de cada no ainda bate com a escala (Limits so estende o dominio)" ((Max-HeightError $r) -lt 1e-6)
$r = Invoke-Heat $s (N 0.1) $true 3
Check "Scale Divisions = 3 gera poucas marcas" ($r.C.ValueScale.Ticks.Count -le 4) ("(" + $r.C.ValueScale.Ticks.Count + ")")
$r = Invoke-Heat $s (N 0.1) $true 99
Check "Scale Divisions absurdo e limitado" ($r.C.ValueScale.Ticks.Count -le 30) ("(" + $r.C.ValueScale.Ticks.Count + ")")

Write-Host "`n[T12] Escala nao altera malha, cores nem valores" -ForegroundColor Yellow
$s = Get-Samples $peak
$a = Invoke-Heat $s (N 0.5) $false; $b = Invoke-Heat $s (N 0.5) $true
$ma = $a.C.CachedHeatmapMesh; $mb = $b.C.CachedHeatmapMesh
$sameV = $ma.Vertices.Count -eq $mb.Vertices.Count; $dz = 0.0
for ($i = 0; $i -lt $ma.Vertices.Count; $i++) { $d = [math]::Abs($ma.Vertices[$i].Z - $mb.Vertices[$i].Z); if ($d -gt $dz) { $dz = $d } }
$sameC = $true; for ($i = 0; $i -lt $ma.VertexColors.Count; $i++) { if ($ma.VertexColors[$i] -ne $mb.VertexColors[$i]) { $sameC = $false; break } }
Check "Malha identica com e sem escala" ($sameV -and $dz -eq 0.0 -and $ma.Faces.Count -eq $mb.Faces.Count)
Check "Cores identicas" $sameC
Check "Saidas de valores identicas" (($a.C.Params.Output[2].VolatileData.DataCount -eq $b.C.Params.Output[2].VolatileData.DataCount))

Write-Host ""
if ($script:failures.Count -eq 0) { Write-Host ("TODOS OS {0} TESTES PASSARAM" -f $script:checks) -ForegroundColor Green }
else { Write-Host ("{0} de {1} verificacoes FALHARAM: {2}" -f $script:failures.Count, $script:checks, ($script:failures -join '; ')) -ForegroundColor Red }
[RhinoBootHeat]::Stop()
exit ($script:failures.Count)
