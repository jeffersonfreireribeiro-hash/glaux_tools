# Test-Marimekko.ps1
# Marimekko Chart: componente de verdade num GH_Document em RhinoCore sem interface.
# Confere o modelo (invariantes, areas), as saidas, o mesmo layout no viewport/PNG/SVG/CSV/PDF, planos, desempenho e serializacao.
# NAO cobre: desenho no canvas do GH e no viewport do Rhino com janela (validar manualmente).
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tests\rhino\Test-Marimekko.ps1 -Gha <Glaux_Tools.gha>

param([string]$Gha = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $Gha) { $Gha = Join-Path $root 'src\bin\Release\net48\Glaux_Tools.gha' }
$rhino = 'C:\Program Files\Rhino 8'
$env:PATH = "$rhino\System;" + $env:PATH
Add-Type -AssemblyName System.Drawing
@("$rhino\System\RhinoCommon.dll", "$rhino\Plug-ins\Grasshopper\GH_IO.dll", "$rhino\Plug-ins\Grasshopper\Grasshopper.dll", $Gha) | ForEach-Object { [Reflection.Assembly]::LoadFrom($_) | Out-Null }
if (-not ('RhinoBootMek' -as [type])) {
    Add-Type -ReferencedAssemblies "$rhino\System\RhinoCommon.dll" -TypeDefinition @"
public static class RhinoBootMek {
    public static Rhino.Runtime.InProcess.RhinoCore Core;
    public static void Start() { if (Core == null) Core = new Rhino.Runtime.InProcess.RhinoCore(new string[] { "/nosplash" }, Rhino.Runtime.InProcess.WindowStyle.Hidden); }
    public static void Stop() { if (Core != null) { Core.Dispose(); Core = null; } }
}
"@
}
[RhinoBootMek]::Start()
$script:failures = @(); $script:checks = 0
function Check([string]$label, $condition, [string]$detail = '') {
    $script:checks++
    if ([bool]$condition) { Write-Host ("  OK     {0} {1}" -f $label, $detail) -ForegroundColor Green }
    else { Write-Host ("  FALHOU {0} {1}" -f $label, $detail) -ForegroundColor Red; $script:failures += $label }
}
$Mode = [Grasshopper.Kernel.GH_SolutionMode]::Silent
$ci = [Globalization.CultureInfo]::InvariantCulture
$tmpDir = Join-Path $env:TEMP ('mek_test_' + [guid]::NewGuid().ToString('N')); New-Item -ItemType Directory -Path $tmpDir | Out-Null
function N($v) { [Grasshopper.Kernel.Types.GH_Number]::new([double]$v) }
function S($v) { [Grasshopper.Kernel.Types.GH_String]::new([string]$v) }

# $cats: array de arrays (valores por categoria)
function Invoke-Mek($cats, $widths = @(), $catLabels = @(), $segLabels = @(), $plane = $null, $size = $null, [bool]$export = $false) {
    $doc = [Grasshopper.Kernel.GH_Document]::new(); $doc.Enabled = $true
    $c = [Buraqueira_Tools.Marimekko_Component]::new(); $c.CreateAttributes()
    for ($i = 0; $i -lt $cats.Count; $i++) { foreach ($v in $cats[$i]) { $c.Params.Input[0].PersistentData.Append((N $v), [Grasshopper.Kernel.Data.GH_Path]::new($i)) } }
    foreach ($w in $widths) { $c.Params.Input[1].PersistentData.Append((N $w)) }
    foreach ($l in $catLabels) { $c.Params.Input[2].PersistentData.Append((S $l)) }
    foreach ($l in $segLabels) { $c.Params.Input[3].PersistentData.Append((S $l)) }
    if ($plane) { $c.Params.Input[4].PersistentData.Clear(); $c.Params.Input[4].PersistentData.Append([Grasshopper.Kernel.Types.GH_Plane]::new($plane)) }
    if ($size) { $c.Params.Input[5].PersistentData.Append($size) }
    $c.Params.Input[8].PersistentData.Clear(); $c.Params.Input[8].PersistentData.Append((S $tmpDir))
    if ($export) { $c.Params.Input[9].PersistentData.Clear(); $c.Params.Input[9].PersistentData.Append([Grasshopper.Kernel.Types.GH_Boolean]::new($true)) }
    $null = $doc.AddObject($c, $false)
    $sw = [Diagnostics.Stopwatch]::StartNew(); $doc.NewSolution($true, $Mode); $sw.Stop()
    return [pscustomobject]@{ C = $c; Doc = $doc; Ms = $sw.Elapsed.TotalMilliseconds }
}
function Errs($r) { @($r.C.RuntimeMessages([Grasshopper.Kernel.GH_RuntimeMessageLevel]::Error)) }
function Warns($r) { @($r.C.RuntimeMessages([Grasshopper.Kernel.GH_RuntimeMessageLevel]::Warning)) }
$data = @(@(20, 30, 50), @(60, 20, 20), @(10, 40, 50))

Write-Host "=== MARIMEKKO CHART ===" -ForegroundColor Cyan
$tmp = [Buraqueira_Tools.Marimekko_Component]::new(); $tmp.CreateAttributes()
Check "Nome/Nickname/Categoria" ($tmp.Name -eq 'Marimekko Chart' -and $tmp.NickName -eq 'Marimekko' -and $tmp.Category -eq 'Glaux Tools' -and $tmp.SubCategory -eq 'Visual')
Check "Entradas: Values em arvore, Export por ultimo" ($tmp.Params.Input.Count -eq 10 -and $tmp.Params.Input[0].Access -eq [Grasshopper.Kernel.GH_ParamAccess]::tree -and $tmp.Params.Input[9].Name -eq 'Export')
Check "7 saidas; Cells/Segment Share/Cell Area Share em arvore" ($tmp.Params.Output.Count -eq 7 -and $tmp.Params.Output[0].Name -eq 'Cells' -and $tmp.Params.Output[3].Name -eq 'Segment Share' -and $tmp.Params.Output[4].Name -eq 'Cell Area Share')
Check "Icone 24x24" ([Buraqueira_Tools.GlauxToolsIcons]::Marimekko.Width -eq 24)

Write-Host "`n[T01] Simples: larguras 20/50/30" -ForegroundColor Yellow
$r = Invoke-Mek $data @(20, 50, 30) @('A', 'B', 'C') @('X', 'Y', 'Z')
$lay = $r.C.Layout
Check "Sem erros" ((Errs $r).Count -eq 0) ("(" + ((Errs $r) -join '|') + ")")
Check "9 celulas, arvore {cat}[seg] preservada" ($r.C.Params.Output[0].VolatileData.PathCount -eq 3 -and $r.C.Params.Output[0].VolatileData.DataCount -eq 9)
Check "Largura total = 100%" ([math]::Abs((($r.C.Params.Output[2].VolatileData.AllData($true) | ForEach-Object { $_.Value } | Measure-Object -Sum).Sum) - 1.0) -lt 1e-12)
Check "Cada categoria: alturas = 100%" ($lay.MaxInvariantError() -lt 1e-12)
$areas = @($r.C.Params.Output[4].VolatileData.AllData($true) | ForEach-Object { $_.Value })
Check "Areas somam 100%" ([math]::Abs(($areas | Measure-Object -Sum).Sum - 1.0) -lt 1e-12)
Check "Cell Area Share de B/X = 0,5 x 0,6 = 0,30 (nao 60%)" ([math]::Abs($r.C.Params.Output[4].VolatileData.get_Branch(1)[0].Value - 0.30) -lt 1e-12 -and [math]::Abs($r.C.Params.Output[3].VolatileData.get_Branch(1)[0].Value - 0.60) -lt 1e-12)

Write-Host "`n[T02] Larguras nao normalizadas (200/500/300) = mesma geometria" -ForegroundColor Yellow
$r2 = Invoke-Mek $data @(200, 500, 300)
$same = $true; for ($i = 0; $i -lt $lay.Cells.Count; $i++) { $a = $lay.Cells[$i]; $b = $r2.C.Layout.Cells[$i]; if ([math]::Abs($a.X0 - $b.X0) + [math]::Abs($a.X1 - $b.X1) + [math]::Abs($a.Y0 - $b.Y0) + [math]::Abs($a.Y1 - $b.Y1) -gt 1e-12) { $same = $false } }
Check "Geometria identica" $same

Write-Host "`n[T03] Segmentos nao normalizados (200/300/500) = 20/30/50 %" -ForegroundColor Yellow
$r3 = Invoke-Mek @(, @(200, 300, 500)) @(1)
Check "Alturas 0.2 / 0.3 / 0.5" ((@($r3.C.Layout.Cells | ForEach-Object { [math]::Round($_.NormalizedHeight, 12).ToString($ci) }) -join ' ') -eq '0.2 0.3 0.5')

Write-Host "`n[T04] Categoria de largura 0 / [T05] total 0 / [T06] negativos" -ForegroundColor Yellow
$r = Invoke-Mek $data @(20, 0, 30)
Check "Largura 0: sem erro, categoria sem celulas, aviso" ((Errs $r).Count -eq 0 -and $r.C.Layout.Categories.Count -eq 2 -and (Warns $r).Count -ge 1 -and $r.C.Params.Output[0].VolatileData.DataCount -eq 6)
$r = Invoke-Mek $data @(0, 0, 0)
Check "Total 0: entrada invalida, erro claro, sem geometria nem excecao" ((Errs $r).Count -ge 1 -and $r.C.Params.Output[0].VolatileData.DataCount -eq 0 -and $r.C.Message -like '*invál*') ("(" + ((Errs $r) -join '|') + ")")
$r = Invoke-Mek $data @(20, -10, 90)
Check "Largura negativa: erro, sem Abs, sem celulas" ((Errs $r).Count -ge 1 -and $r.C.Params.Output[0].VolatileData.DataCount -eq 0 -and ((Errs $r) -join ' ') -like '*negativa*')
$r = Invoke-Mek @(@(20, -5, 85)) @(10)
Check "Segmento negativo: erro, sem celulas" ((Errs $r).Count -ge 1 -and $r.C.Params.Output[0].VolatileData.DataCount -eq 0 -and ((Errs $r) -join ' ') -like '*negativo*')
$r = Invoke-Mek $data @(10, 20)
Check "Quantidade intermediaria de larguras (2 para 3 categorias): erro explicito" ((Errs $r).Count -ge 1 -and ((Errs $r) -join ' ') -like '*Category Widths*')
$r = Invoke-Mek $data @(5)
Check "1 largura vale para todas (3 colunas iguais)" ((Errs $r).Count -eq 0 -and ($r.C.Layout.Categories | ForEach-Object { [math]::Round($_.NormalizedWidth, 9) } | Select-Object -Unique).Count -eq 1)
$r = Invoke-Mek $data @()
Check "Sem larguras: usa o total de cada categoria (100/100/100 = iguais)" ((Errs $r).Count -eq 0 -and [math]::Abs($r.C.Layout.Categories[0].NormalizedWidth - 1.0 / 3.0) -lt 1e-12)

Write-Host "`n[T07] Arvore irregular" -ForegroundColor Yellow
$r = Invoke-Mek @(@(1, 2, 3), @(4, 5), @(1, 1, 1, 1)) @()
Check "Nao quebra; 3/2/4 celulas; aviso explicito" ((Errs $r).Count -eq 0 -and ($r.C.Layout.Categories | ForEach-Object { $_.Cells.Count }) -join ',' -eq '3,2,4' -and ((Warns $r) -join ' ') -like '*irregular*')
Check "Arvore de saida preservada ({0}[3] {1}[2] {2}[4])" ($r.C.Params.Output[0].VolatileData.get_Branch(1).Count -eq 2 -and $r.C.Params.Output[0].VolatileData.get_Branch(2).Count -eq 4)

Write-Host "`n[T08] 100 categorias x 20 segmentos" -ForegroundColor Yellow
$rnd = [Random]::new(5); $big = @(); for ($i = 0; $i -lt 100; $i++) { $big += , @(1..20 | ForEach-Object { 1 + $rnd.NextDouble() * 50 }) }
$rb = Invoke-Mek $big @()
Check "2000 celulas validas" ((Errs $rb).Count -eq 0 -and $rb.C.Layout.Cells.Count -eq 2000 -and $rb.C.Layout.MaxInvariantError() -lt 1e-12)
Write-Host ("  solve completo (inclui montagem do GH_Document): {0:N0} ms" -f $rb.Ms)
$sw = [Diagnostics.Stopwatch]::StartNew(); $png = [Buraqueira_Tools.MarimekkoGdi]::RenderPng($rb.C.Layout, $rb.C.Style, 1200, 720); $sw.Stop(); $pngMs = $sw.Elapsed.TotalMilliseconds; $png.Dispose()
$sw.Restart(); $svg = [Buraqueira_Tools.MarimekkoExport]::BuildSvg($rb.C.Layout, $rb.C.Style); $sw.Stop(); $svgMs = $sw.Elapsed.TotalMilliseconds
Write-Host ("  render GDI+/PNG 1200x720: {0:N0} ms; SVG: {1:N0} ms ({2:N0} KB)" -f $pngMs, $svgMs, ($svg.Length / 1024))
Check "Render/export em tempo razoavel (< 3 s cada)" ($pngMs -lt 3000 -and $svgMs -lt 3000)
Check "SVG tem 1 <rect> por celula (+ limite, legenda, fundo)" ([regex]::Matches($svg, 'data-segment-share=').Count -eq 2000)
foreach ($sz in @(@(3, 3), @(10, 5), @(50, 10))) {
    $cc = @(); $rr = [Random]::new(9); for ($i = 0; $i -lt $sz[0]; $i++) { $cc += , @(1..$sz[1] | ForEach-Object { 1 + $rr.NextDouble() * 9 }) }
    $x = Invoke-Mek $cc @()
    $sw.Restart(); $b2 = [Buraqueira_Tools.MarimekkoGdi]::RenderPng($x.C.Layout, $x.C.Style, 460, 300); $sw.Stop(); $b2.Dispose()
    Write-Host ("  {0}x{1}: solve {2:N0} ms; redraw GDI+ (canvas 460x300) {3:N0} ms" -f $sz[0], $sz[1], $x.Ms, $sw.Elapsed.TotalMilliseconds)
}

Write-Host "`n[T09/T10] Mesmo grafico no canvas (GDI+), viewport (retangulos), SVG, CSV" -ForegroundColor Yellow
$r = Invoke-Mek $data @(20, 50, 30) @('A', 'B', 'C') @('X', 'Y', 'Z') $null ([Grasshopper.Kernel.Types.GH_String]::new('10, 6'))
$lay = $r.C.Layout; $style = $r.C.Style
# viewport / saidas: retangulos / tamanho do grafico == coordenadas normalizadas do layout
$maxVp = 0.0; $k = 0
foreach ($cat in 0..2) { $br = $r.C.Params.Output[0].VolatileData.get_Branch($cat); for ($j = 0; $j -lt $br.Count; $j++) { $rect = $br[$j].Value; $cell = $lay.Cells[$k]; $k++
    $d = [math]::Abs($rect.Corner(0).X / 10.0 - $cell.X0) + [math]::Abs($rect.Corner(2).X / 10.0 - $cell.X1) + [math]::Abs($rect.Corner(0).Y / 6.0 - $cell.Y0) + [math]::Abs($rect.Corner(2).Y / 6.0 - $cell.Y1); if ($d -gt $maxVp) { $maxVp = $d } } }
Check "Viewport/saidas == layout normalizado (erro max)" ($maxVp -lt 1e-9) ("({0:E2})" -f $maxVp)
$boundary = $r.C.Params.Output[1].VolatileData.get_Branch(0)[0].Value
Check "Chart Boundary = 10 x 6" ([math]::Abs($boundary.Width - 10) -lt 1e-9 -and [math]::Abs($boundary.Height - 6) -lt 1e-9)
# area geometrica das celulas / area total == RelativeArea
$maxArea = 0.0; $k = 0; foreach ($cat in 0..2) { $br = $r.C.Params.Output[0].VolatileData.get_Branch($cat); for ($j = 0; $j -lt $br.Count; $j++) { $a = $br[$j].Value.Area / 60.0; $e = $lay.Cells[$k].RelativeArea; $k++; if ([math]::Abs($a - $e) -gt $maxArea) { $maxArea = [math]::Abs($a - $e) } } }
Check "Area gerada / area total == largura x altura normalizadas (erro max)" ($maxArea -lt 1e-12) ("({0:E2})" -f $maxArea)
# canvas = MarimekkoGdi.Draw (a mesma funcao que o Attributes chama): amostra a cor no canto de cada celula
$bmp = [System.Drawing.Bitmap]::new(920, 600); $g = [System.Drawing.Graphics]::FromImage($bmp); $g.Clear([System.Drawing.Color]::Black)
[Buraqueira_Tools.MarimekkoGdi]::Draw($g, [System.Drawing.RectangleF]::new(0, 0, 920, 600), $lay, $style, $true); $g.Dispose()
$frame = [Buraqueira_Tools.MarimekkoFrame]::Compute(920, 600, $style, $lay)
$bad = 0; foreach ($cell in $lay.Cells) { $rc = $frame.CellRect($cell); $px = $bmp.GetPixel([int]($rc.X + 3), [int]($rc.Y + 3)); $exp = [Buraqueira_Tools.MarimekkoText]::SegmentColor($cell.SegmentIndex, $style.Palette); if ([math]::Abs($px.R - $exp.R) + [math]::Abs($px.G - $exp.G) + [math]::Abs($px.B - $exp.B) -gt 6) { $bad++ } }
Check "Canvas: cor de cada celula = cor do segmento (mesmo segmento, mesma cor)" ($bad -eq 0) ("($bad divergentes)")
Check "Canvas: proporcao dos retangulos de pixel == layout (largura/altura)" ($lay.Cells | ForEach-Object { $rc = $frame.CellRect($_); [math]::Abs(($rc.Width / $frame.Plot.Width) - ($_.X1 - $_.X0)) + [math]::Abs(($rc.Height / $frame.Plot.Height) - ($_.Y1 - $_.Y0)) } | Where-Object { $_ -gt 1e-4 } | Measure-Object | ForEach-Object { $_.Count -eq 0 })
$bmp.Dispose()
# SVG
$svg = [Buraqueira_Tools.MarimekkoExport]::BuildSvg($lay, $style, 1200, 720)
$fr2 = [Buraqueira_Tools.MarimekkoFrame]::Compute(1200, 720, $style, $lay)
$svgBad = 0
foreach ($m in [regex]::Matches($svg, '<g id="cell_[^"]*"[^>]*data-segment-share="([^"]+)"[^>]*>\s*<title>[^<]*</title>\s*<rect x="([^"]+)" y="([^"]+)" width="([^"]+)" height="([^"]+)"')) {
    $wN = [double]::Parse($m.Groups[4].Value, $ci) / $fr2.Plot.Width; $hN = [double]::Parse($m.Groups[5].Value, $ci) / $fr2.Plot.Height
    if ([math]::Abs($hN - [double]::Parse($m.Groups[1].Value, $ci)) -gt 2e-3) { $svgBad++ }
}
Check "SVG: 9 celulas vetoriais com altura == participacao do segmento" ([regex]::Matches($svg, '<rect x=').Count -ge 9 -and $svgBad -eq 0 -and [regex]::Matches($svg, 'data-area-share=').Count -eq 9) ("($svgBad divergentes)")
Check "SVG: legenda, rotulos de categoria, limite e tooltips (title)" ($svg -match 'marimekko_legend' -and $svg -match 'marimekko_category_labels' -and $svg -match 'marimekko_boundary' -and ([regex]::Matches($svg, '<title>').Count -eq 9))
Check "SVG valido (XML)" (([xml]$svg) -ne $null)
$csv = [Buraqueira_Tools.MarimekkoExport]::BuildCsv($lay)
Check "CSV: 9 linhas de dados, cabecalho semantico" (($csv -split "`r?`n" | Where-Object { $_ }).Count -eq 10 -and $csv -like 'CategoryID,CategoryLabel,CategoryWidthValue,CategoryWidthPercent,SegmentID*')

Write-Host "`n[T13] Plano girado e movido" -ForegroundColor Yellow
$xf = [Rhino.Geometry.Transform]::Rotation([math]::PI / 5, [Rhino.Geometry.Vector3d]::ZAxis, [Rhino.Geometry.Point3d]::Origin) * [Rhino.Geometry.Transform]::Translation(5000.0, -3000.0, 40.0)
$pl = [Rhino.Geometry.Plane]::WorldXY; $null = $pl.Transform($xf)
$r = Invoke-Mek $data @(20, 50, 30) @() @() $pl
$rect0 = $r.C.Params.Output[0].VolatileData.get_Branch(0)[0].Value
Check "Celula 0 comeca na origem do plano" ($rect0.Corner(0).DistanceTo($pl.Origin) -lt 1e-9)
$maxd = 0.0; $k = 0; foreach ($cat in 0..2) { $br = $r.C.Params.Output[0].VolatileData.get_Branch($cat); for ($j = 0; $j -lt $br.Count; $j++) { $cell = $r.C.Layout.Cells[$k]; $k++; $exp = $pl.PointAt($cell.X0 * 10.0, $cell.Y0 * 6.0); $d = $br[$j].Value.Corner(0).DistanceTo($exp); if ($d -gt $maxd) { $maxd = $d } } }
Check "Todas as celulas seguem o plano (erro max)" ($maxd -lt 1e-6) ("({0:E2})" -f $maxd)
Check "ClippingBox inclui o grafico no plano movido" ($r.C.ClippingBox.Contains($pl.PointAt(10, 6)) -and $r.C.ClippingBox.Contains($pl.Origin))

Write-Host "`n[T11/T10] Exportacao so quando pedida; PNG/SVG/CSV/PDF em disco" -ForegroundColor Yellow
$before = @(Get-ChildItem $tmpDir -File).Count
$r = Invoke-Mek $data @(20, 50, 30) @('Residencial', 'Comercial', 'Institucional') @('Tipo A', 'Tipo B', 'Tipo C')
$r.Doc.NewSolution($true, $Mode)
Check "Sem gatilho: nenhum arquivo e escrito (em varias solucoes)" (@(Get-ChildItem $tmpDir -File).Count -eq $before)
$re = Invoke-Mek $data @(20, 50, 30) @('Residencial', 'Comercial', 'Institucional') @('Tipo A', 'Tipo B', 'Tipo C') $null $null $true
$files = @($re.C.Params.Output[6].VolatileData.AllData($true) | ForEach-Object { $_.Value })
Check "Gatilho: PNG + SVG + CSV exportados" ($files.Count -eq 3 -and @($files | Where-Object { Test-Path $_ }).Count -eq 3) ("(" + (($files | ForEach-Object { Split-Path $_ -Leaf }) -join ', ') + ")")
$re.Doc.NewSolution($true, $Mode)
Check "Gatilho continua True: nao reexporta a cada solucao" (@(Get-ChildItem $tmpDir -File).Count -eq ($before + 3))
$png = $files | Where-Object { $_ -like '*.png' }; $img = [System.Drawing.Image]::FromFile($png)
Check "PNG em alta resolucao (2400 x 1440)" ($img.Width -eq 2400 -and $img.Height -eq 1440); $img.Dispose()
$csvFile = $files | Where-Object { $_ -like '*.csv' }
$rows = @(Import-Csv $csvFile)
Check "CSV exportado: 9 linhas; soma das larguras (por categoria unica) = 100" ($rows.Count -eq 9 -and [math]::Abs((($rows | Group-Object CategoryID | ForEach-Object { [double]::Parse($_.Group[0].CategoryWidthPercent, $ci) } | Measure-Object -Sum).Sum) - 100.0) -lt 1e-6)
Check "CSV: por categoria, soma dos segmentos = 100" (@($rows | Group-Object CategoryID | Where-Object { [math]::Abs((($_.Group | ForEach-Object { [double]::Parse($_.SegmentPercent, $ci) } | Measure-Object -Sum).Sum) - 100.0) -gt 1e-6 }).Count -eq 0)
Check "CSV: semantica preservada (rotulos, valores originais)" ($rows[0].CategoryLabel -eq 'Residencial' -and $rows[0].SegmentLabel -eq 'Tipo A' -and [double]::Parse($rows[0].SegmentValue, $ci) -eq 20 -and [double]::Parse($rows[0].CategoryWidthValue, $ci) -eq 20)
$svgFile = $files | Where-Object { $_ -like '*.svg' }; $svgTxt = [IO.File]::ReadAllText($svgFile)
Check "SVG exportado e identico ao BuildSvg do mesmo modelo" ($svgTxt -eq [Buraqueira_Tools.MarimekkoExport]::BuildSvg($re.C.Layout, $re.C.Style))
$pdfErr = $null; $pdf = $re.C.ExportPdf('mek_pdf_test', [ref]$pdfErr)
if ($pdf) { $bytes = [IO.File]::ReadAllBytes($pdf); Check "PDF vetorial gerado (Edge/Chrome headless)" ($bytes.Length -gt 1000 -and [Text.Encoding]::ASCII.GetString($bytes, 0, 4) -eq '%PDF') ("(" + $bytes.Length + " bytes)") }
else { Write-Host ("  PDF: NAO GERADO ({0}) - sem navegador Chromium instalado?" -f $pdfErr) -ForegroundColor DarkYellow }

Write-Host "`n[T12] Salvar / reabrir (Write/Read do componente)" -ForegroundColor Yellow
$cfg = Invoke-Mek $data @(20, 50, 30) @('A', 'B', 'C') @('X', 'Y', 'Z')
$cfg.C.LabelMode = [Buraqueira_Tools.MarimekkoLabelMode]::Value; $cfg.C.ShowLegend = $false; $cfg.C.ShowCategoryLabels = $false; $cfg.C.ShowCategoryShare = $false; $cfg.C.ShowCanvasChart = $false; $cfg.C.ShowViewportPreview = $false; $cfg.C.AlsoExportPdf = $true
# Observado em RhinoCore headless: a reidratacao de itens GH_Number de uma entrada generica (persistent data) pode devolver nulos ou travar o Read.
# Por isso o teste serializa sem os dados de Values e reaplica-os depois; os dados reais chegam por fios no GH.
$cfg.C.Params.Input[0].PersistentData.Clear()
# Observado em RhinoCore headless: a reidratacao de itens GH_Number de entrada generica (persistent data) devolve nulos ou trava o Read.
# O teste serializa sem os dados de Values e os reaplica depois; no GH real os dados chegam por fios.
$cfg.C.Params.Input[0].PersistentData.Clear(); $cfg.C.Params.Input[1].PersistentData.Clear()
$chunk = [GH_IO.Serialization.GH_LooseChunk]::new('Component'); $null = $cfg.C.Write($chunk)
$xml = $chunk.Serialize_Xml(); $chunk2 = [GH_IO.Serialization.GH_LooseChunk]::new('Component'); $chunk2.Deserialize_Xml($xml)
$c2 = [Buraqueira_Tools.Marimekko_Component]::new(); $c2.CreateAttributes(); $null = $c2.Read($chunk2)
Check "Configuracoes de menu preservadas" ($c2.LabelMode -eq [Buraqueira_Tools.MarimekkoLabelMode]::Value -and (-not $c2.ShowLegend) -and (-not $c2.ShowCategoryLabels) -and (-not $c2.ShowCategoryShare) -and (-not $c2.ShowCanvasChart) -and (-not $c2.ShowViewportPreview) -and $c2.AlsoExportPdf)
Check "Entradas e saidas reabertas intactas (10 / 7)" ($c2.Params.Input.Count -eq 10 -and $c2.Params.Output.Count -eq 7)
# Observado (tambem em SpatialHeatmap): itens GH_Number de entradas genericas voltam nulos na reidratacao headless do chunk; reaplica os valores para testar o restante.
$c2.Params.Input[0].PersistentData.Clear(); for ($i = 0; $i -lt 3; $i++) { foreach ($v in $data[$i]) { $c2.Params.Input[0].PersistentData.Append((N $v), [Grasshopper.Kernel.Data.GH_Path]::new($i)) } }
$d2 = [Grasshopper.Kernel.GH_Document]::new(); $d2.Enabled = $true; $null = $d2.AddObject($c2, $false); $d2.NewSolution($true, $Mode)
Check "Reaberto: mesmo layout (9 celulas validas)" ($c2.Layout -ne $null -and $c2.Layout.IsValid -and $c2.Layout.Cells.Count -eq 9)

Write-Host "`n[VISUAL] Dataset com leitura obvia" -ForegroundColor Yellow
$rv = Invoke-Mek @(@(30, 10, 10), @(40, 40, 120), @(10, 10, 10)) @(25, 60, 15) @('Pequena', 'GRANDE', 'Media') @('A', 'B', 'C')
$lv = $rv.C.Layout
$wMax = ($lv.Categories | Sort-Object NormalizedWidth -Descending | Select-Object -First 1).Label
$segMax = ($lv.Cells | Where-Object { $_.CategoryLabel -eq 'GRANDE' } | Sort-Object NormalizedHeight -Descending | Select-Object -First 1).SegmentLabel
Check "Categoria B (GRANDE) e a mais larga (60% da largura)" ($wMax -eq 'GRANDE' -and [math]::Abs($lv.Categories[1].NormalizedWidth - 0.6) -lt 1e-12)
Check "Segmento C domina a categoria B (60% da altura)" ($segMax -eq 'C' -and [math]::Abs(($lv.Cells | Where-Object { $_.CategoryLabel -eq 'GRANDE' -and $_.SegmentLabel -eq 'C' }).NormalizedHeight - 0.6) -lt 1e-12)
$prev = Join-Path $env:TEMP 'marimekko_preview.png'; $pb = [Buraqueira_Tools.MarimekkoGdi]::RenderPng($lv, $rv.C.Style, 1200, 720); $pb.Save($prev, [System.Drawing.Imaging.ImageFormat]::Png); $pb.Dispose()
Write-Host "  imagem: $prev"

try { Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue } catch { }
Write-Host ""
if ($script:failures.Count -eq 0) { Write-Host ("TODOS OS {0} TESTES PASSARAM" -f $script:checks) -ForegroundColor Green }
else { Write-Host ("{0} de {1} verificacoes FALHARAM: {2}" -f $script:failures.Count, $script:checks, ($script:failures -join '; ')) -ForegroundColor Red }
[RhinoBootMek]::Stop()
exit ($script:failures.Count)
