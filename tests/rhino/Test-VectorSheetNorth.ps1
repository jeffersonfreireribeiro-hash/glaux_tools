# Test-VectorSheetNorth.ps1
# Pill Vector Sheet Layout: "North Direction" (direcao) + "Show North" (liga/desliga) com item unico ou lista.
# Monta um GH_Document com o componente de verdade num RhinoCore sem interface e le o SVG gerado.
#
# Uso:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests\rhino\Test-VectorSheetNorth.ps1 [-Gha caminho\Glaux_Tools.gha]
#   Compatibilidade com .gh antigo (duas etapas, dois processos, pois as duas versoes tem o mesmo GUID):
#     ... -Gha <gha ANTIGO> -SaveOldGh C:\temp\old.gh
#     ... -Gha <gha NOVO>   -OpenOldGh C:\temp\old.gh

param([string]$Gha = '', [string]$SaveOldGh = '', [string]$OpenOldGh = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $Gha) { $Gha = Join-Path $root 'src\bin\Release\net48\Glaux_Tools.gha' }
$rhino = 'C:\Program Files\Rhino 8'
$env:PATH = "$rhino\System;" + $env:PATH

@("$rhino\System\RhinoCommon.dll", "$rhino\Plug-ins\Grasshopper\GH_IO.dll", "$rhino\Plug-ins\Grasshopper\Grasshopper.dll", $Gha) |
    ForEach-Object { [Reflection.Assembly]::LoadFrom($_) | Out-Null }

if (-not ('RhinoBootNorth' -as [type])) {
    Add-Type -ReferencedAssemblies "$rhino\System\RhinoCommon.dll" -TypeDefinition @"
using System;
public static class RhinoBootNorth {
    public static Rhino.Runtime.InProcess.RhinoCore Core;
    public static void Start() { if (Core == null) Core = new Rhino.Runtime.InProcess.RhinoCore(new string[] { "/nosplash" }, Rhino.Runtime.InProcess.WindowStyle.Hidden); }
    public static void Stop() { if (Core != null) { Core.Dispose(); Core = null; } }
}
"@
}
[RhinoBootNorth]::Start()

$script:failures = @(); $script:checks = 0
function Check([string]$label, $condition, [string]$detail = '') {
    $script:checks++
    if ([bool]$condition) { Write-Host ("  OK     {0} {1}" -f $label, $detail) -ForegroundColor Green }
    else { Write-Host ("  FALHOU {0} {1}" -f $label, $detail) -ForegroundColor Red; $script:failures += $label }
}
$Mode = [Grasshopper.Kernel.GH_SolutionMode]::Silent
$ci = [Globalization.CultureInfo]::InvariantCulture

function New-Sheet([int]$views = 4) {
    $doc = [Grasshopper.Kernel.GH_Document]::new(); $doc.Enabled = $true
    $c = [Buraqueira_Tools.PillVectorSheetLayout_Component]::new(); $c.CreateAttributes()
    for ($i = 0; $i -lt $views; $i++) {
        $ln = [Rhino.Geometry.LineCurve]::new([Rhino.Geometry.Point3d]::new(0, 0, 0), [Rhino.Geometry.Point3d]::new(10 + $i, 5, 0))
        $c.Params.Input[0].PersistentData.Append([Grasshopper.Kernel.Types.GH_Curve]::new($ln), [Grasshopper.Kernel.Data.GH_Path]::new($i))
    }
    $null = $doc.AddObject($c, $false)
    return [pscustomobject]@{ Doc = $doc; C = $c }
}

# Roda e devolve as setas: @{ View = indice do quadro; Rot = rotacao SVG } ordenadas por quadro
function Invoke-North($dirs = @(), $shows = $null, [bool]$cycle = $false, [int]$views = 4) {
    $s = New-Sheet $views
    if ($cycle) { $s.C.ListMatch = [Buraqueira_Tools.GlauxListMatchMode]::Cycle }
    foreach ($d in $dirs) { $s.C.Params.Input[5].PersistentData.Append($d) }
    if ($null -ne $shows) { foreach ($b in $shows) { $s.C.Params.Input[10].PersistentData.Append([Grasshopper.Kernel.Types.GH_Boolean]::new([bool]$b)) } }
    $s.Doc.NewSolution($true, $Mode)
    $svg = ''
    if ($s.C.Params.Output[2].VolatileData.DataCount -gt 0) { $svg = $s.C.Params.Output[2].VolatileData.get_Branch(0)[0].ScriptVariable() }
    $vps = $s.C.ComputedViewports
    $arrows = @()
    foreach ($m in [regex]::Matches($svg, 'id="north_arrow" transform="translate\(([-\d.]+), ([-\d.]+)\) rotate\(([-\d.]+)\)"')) {
        $x = [double]::Parse($m.Groups[1].Value, $ci); $y = [double]::Parse($m.Groups[2].Value, $ci); $rot = [double]::Parse($m.Groups[3].Value, $ci)
        $view = -1
        for ($i = 0; $i -lt $vps.Count; $i++) { if ([math]::Abs($x - ($vps[$i].Right - 8.0)) -lt 0.02 -and $y -ge $vps[$i].Top -and $y -le $vps[$i].Bottom) { $view = $i } }
        $arrows += [pscustomobject]@{ View = $view; Rot = $rot }
    }
    $arrows = @($arrows | Sort-Object View)
    $warn = @($s.C.RuntimeMessages([Grasshopper.Kernel.GH_RuntimeMessageLevel]::Warning))
    $log = ''
    if ($s.C.Params.Output[4].VolatileData.DataCount -gt 0) { $log = $s.C.Params.Output[4].VolatileData.get_Branch(0)[0].ScriptVariable() }
    return [pscustomobject]@{ Arrows = $arrows; Warnings = $warn; Log = $log; Sheet = $s; Svg = $svg }
}
function Fmt($a) { ($a | ForEach-Object { "v{0}:{1}" -f $_.View, $_.Rot.ToString("0.#", $ci) }) -join ' ' }
function N([double]$v) { [Grasshopper.Kernel.Types.GH_Number]::new($v) }
function Same($r, [string]$expected) { (Fmt $r.Arrows) -eq $expected }
$tmp = [Buraqueira_Tools.PillVectorSheetLayout_Component]::new(); $tmp.CreateAttributes()

# --------------------------------------------------------------- etapa de compatibilidade
if ($SaveOldGh) {
    # Serializa SO o componente (GH_LooseChunk = o mesmo XML que o Grasshopper grava no .gh), com dados persistentes em N
    Write-Host "Gravando o componente da versao ANTIGA em $SaveOldGh" -ForegroundColor Yellow
    $s = New-Sheet 3
    $s.C.Params.Input[5].PersistentData.Append((N 0)); $s.C.Params.Input[5].PersistentData.Append((N 180))
    $s.Doc.NewSolution($true, $Mode)
    $oldArrows = ([regex]::Matches($s.C.Params.Output[2].VolatileData.get_Branch(0)[0].ScriptVariable(), 'id="north_arrow"')).Count
    Write-Host ("  Inputs do componente antigo: {0}; setas no SVG: {1}" -f $s.C.Params.Input.Count, $oldArrows)
    $s.C.Params.Input[0].PersistentData.Clear(); $s.C.Params.Input[5].PersistentData.Clear()   # dados persistentes de entradas genericas/geometria travam o Read em RhinoCore headless; itens GH_Number em entrada generica travam o Read em RhinoCore headless
    $chunk = [GH_IO.Serialization.GH_LooseChunk]::new('Component')
    Check "Serializou o componente" ($s.C.Write($chunk))
    [IO.File]::WriteAllText($SaveOldGh, $chunk.Serialize_Xml(), (New-Object Text.UTF8Encoding($false)))
    [RhinoBootNorth]::Stop(); exit 0
}
if ($OpenOldGh) {
    Write-Host "`n=== COMPATIBILIDADE: lendo componente gravado pela versao antiga ===" -ForegroundColor Cyan
    $chunk = [GH_IO.Serialization.GH_LooseChunk]::new('Component')
    $chunk.Deserialize_Xml([IO.File]::ReadAllText($OpenOldGh))
    Check "Leu o XML antigo (chunk com itens)" ($chunk.ItemCount -gt 0 -or $chunk.ChunkCount -gt 0)
    $doc = [Grasshopper.Kernel.GH_Document]::new(); $doc.Enabled = $true
    $c = [Buraqueira_Tools.PillVectorSheetLayout_Component]::new(); $c.CreateAttributes()
    Check "Component.Read aceitou o chunk antigo" ($c.Read($chunk))
    Check "Mesmo GUID" ($c.ComponentGuid.ToString() -eq 'b7110015-e1ef-4000-8000-000000000016')
    Write-Host ("  entradas apos Read: {0} -> {1}" -f $c.Params.Input.Count, (($c.Params.Input | ForEach-Object { $_.Name }) -join ' | '))
    Check "Entradas 0-9 continuam as mesmas, na mesma ordem" ($c.Params.Input[5].Name -eq 'North Direction' -and $c.Params.Input[8].Name -eq 'Export' -and $c.Params.Input[9].Name -eq 'File Path')
    Check "Show North existe no fim (indice 10), sem dados (= True)" ($c.Params.Input.Count -eq 11 -and $c.Params.Input[10].Name -eq 'Show North' -and $c.Params.Input[10].SourceCount -eq 0 -and $c.Params.Input[10].VolatileDataCount -eq 0)
    $c.Params.Input[5].PersistentData.Append((N 0)); $c.Params.Input[5].PersistentData.Append((N 180))   # reaplica as direcoes [0,180] (ver nota acima)
    Check "Modo de lista = RepeatLast (chunk antigo nao tem a chave)" ($c.ListMatch -eq [Buraqueira_Tools.GlauxListMatchMode]::RepeatLast)
    for ($i = 0; $i -lt 3; $i++) {
        $ln = [Rhino.Geometry.LineCurve]::new([Rhino.Geometry.Point3d]::new(0, 0, 0), [Rhino.Geometry.Point3d]::new(10 + $i, 5, 0))
        $c.Params.Input[0].PersistentData.Append([Grasshopper.Kernel.Types.GH_Curve]::new($ln), [Grasshopper.Kernel.Data.GH_Path]::new($i))
    }
    $null = $doc.AddObject($c, $false); $doc.NewSolution($true, $Mode)
    $svg = $c.Params.Output[2].VolatileData.get_Branch(0)[0].ScriptVariable()
    $rots = @([regex]::Matches($svg, 'id="north_arrow" transform="translate\([-\d.]+, [-\d.]+\) rotate\(([-\d.]+)\)"') | ForEach-Object { [double]::Parse($_.Groups[1].Value, $ci) })
    Write-Host ("  rotacoes: " + ($rots -join ' '))
    Check "3 vistas, 3 setas (Norte aparece, como na versao antiga)" ($rots.Count -eq 3)
    Check "Direcoes herdadas [0,180] -> 90, -90, -90 (repete o ultimo)" (($rots -join ' ') -eq '90 -90 -90')
    Write-Host ""
    if ($script:failures.Count -eq 0) { Write-Host ("TODOS OS {0} TESTES PASSARAM" -f $script:checks) -ForegroundColor Green } else { Write-Host ("FALHARAM: " + ($script:failures -join '; ')) -ForegroundColor Red }
    [RhinoBootNorth]::Stop(); exit ($script:failures.Count)
}
# --------------------------------------------------------------- contrato
Write-Host "=== PILL VECTOR SHEET LAYOUT: NORTH DIRECTION + SHOW NORTH ===" -ForegroundColor Cyan
Check "GUID preservado" ($tmp.ComponentGuid.ToString() -eq 'b7110015-e1ef-4000-8000-000000000016')
Check "11 entradas; as 10 originais nos mesmos indices" ($tmp.Params.Input.Count -eq 11 -and $tmp.Params.Input[5].Name -eq 'North Direction' -and $tmp.Params.Input[8].Name -eq 'Export' -and $tmp.Params.Input[9].Name -eq 'File Path')
Check "Show North = ultima entrada (10), booleana, lista, opcional" ($tmp.Params.Input[10].Name -eq 'Show North' -and $tmp.Params.Input[10].Access -eq [Grasshopper.Kernel.GH_ParamAccess]::list -and $tmp.Params.Input[10].Optional)
Check "5 saidas inalteradas" ($tmp.Params.Output.Count -eq 5)
Check "Padrao de lista: repete o ultimo" ($tmp.ListMatch -eq [Buraqueira_Tools.GlauxListMatchMode]::RepeatLast)

Write-Host "`n[1] Padrao (nada conectado) = Norte em todas, apontando para cima" -ForegroundColor Yellow
$r = Invoke-North
Check "4 setas, rotate(0) (90 graus = Norte para cima)" (Same $r 'v0:0 v1:0 v2:0 v3:0') ("(" + (Fmt $r.Arrows) + ")")

Write-Host "`n[2] 1 North Direction + 1 Show North" -ForegroundColor Yellow
$r = Invoke-North @((N 30)) @($true)
Check "1 dir (30) + Show True: 4 setas iguais (rot 60)" (Same $r 'v0:60 v1:60 v2:60 v3:60') ("(" + (Fmt $r.Arrows) + ")")
$r = Invoke-North @((N 30)) @($false)
Check "1 dir + Show False: nenhuma seta" ($r.Arrows.Count -eq 0)

Write-Host "`n[3] N North Directions + 1 Show North" -ForegroundColor Yellow
$r = Invoke-North @((N 0), (N 90), (N 180), (N 270)) @($true)
Check "4 dirs + Show True: cada vista com a sua (90, 0, -90, -180)" (Same $r 'v0:90 v1:0 v2:-90 v3:-180') ("(" + (Fmt $r.Arrows) + ")")
$r = Invoke-North @((N 0), (N 90), (N 180), (N 270)) @($false)
Check "4 dirs + Show False: nenhuma seta" ($r.Arrows.Count -eq 0)

Write-Host "`n[4] 1 North Direction + N Show North" -ForegroundColor Yellow
$r = Invoke-North @((N 30)) @($true, $false, $true, $false)
Check "[T,F,T,F]: setas so nas vistas 0 e 2, ambas com a mesma direcao" (Same $r 'v0:60 v2:60') ("(" + (Fmt $r.Arrows) + ")")

Write-Host "`n[5] N North Directions + N Show North" -ForegroundColor Yellow
$r = Invoke-North @((N 0), (N 90), (N 180), (N 270)) @($true, $false, $true, $false)
Check "[T,F,T,F]: vista 0 (90) e vista 2 (-90)" (Same $r 'v0:90 v2:-90') ("(" + (Fmt $r.Arrows) + ")")
$r = Invoke-North @((N 0), (N 90), (N 180), (N 270)) @($false, $false, $false, $false)
Check "Todos False, 4 direcoes validas: nenhuma seta (Show manda)" ($r.Arrows.Count -eq 0)
$r = Invoke-North @((N 0), (N 90), (N 180), (N 270)) @($false, $true, $true, $true)
Check "Show False na vista 0 e direcao valida nela: vista 0 sem Norte; demais com a propria direcao" (Same $r 'v1:0 v2:-90 v3:-180') ("(" + (Fmt $r.Arrows) + ")")

Write-Host "`n[6] Listas MENORES que o numero de vistas (4)" -ForegroundColor Yellow
$r = Invoke-North @((N 0), (N 90)) @($true)
Check "Padrao: repete o ultimo (v2 e v3 = dir 90 -> rot 0)" (Same $r 'v0:90 v1:0 v2:0 v3:0') ("(" + (Fmt $r.Arrows) + ")")
$r = Invoke-North @((N 0), (N 90)) @($true) $true
Check "Ciclico (opt-in): v2 = dir 0, v3 = dir 90" (Same $r 'v0:90 v1:0 v2:90 v3:0') ("(" + (Fmt $r.Arrows) + ")")
$r = Invoke-North @() @($true, $false)
Check "Show [T,F]: padrao repete o ultimo (F): so a vista 0" (Same $r 'v0:0') ("(" + (Fmt $r.Arrows) + ")")
$r = Invoke-North @() @($true, $false) $true
Check "Show [T,F] ciclico: vistas 0 e 2" (Same $r 'v0:0 v2:0') ("(" + (Fmt $r.Arrows) + ")")
Check "Log descreve a regra aplicada" ($r.Log -like '*Show North: 2 valores para 4*ciclicamente*')

Write-Host "`n[7] Listas MAIORES que o numero de vistas (2 vistas, 5 itens)" -ForegroundColor Yellow
$r = Invoke-North @((N 0), (N 90), (N 180), (N 270), (N 45)) @($true, $true, $false, $false, $false) $false 2
Check "Sem excecao; so usa os 2 primeiros" (Same $r 'v0:90 v1:0') ("(" + (Fmt $r.Arrows) + ")")
Check "Log avisa os excedentes" ($r.Log -like '*excedente(s) ignorado(s)*')

Write-Host "`n[8] Tipos aceitos em North Direction" -ForegroundColor Yellow
$r = Invoke-North @([Grasshopper.Kernel.Types.GH_Vector]::new([Rhino.Geometry.Vector3d]::new(1, 0, 0)), [Grasshopper.Kernel.Types.GH_Vector]::new([Rhino.Geometry.Vector3d]::new(0, 1, 0)), [Grasshopper.Kernel.Types.GH_Vector]::new([Rhino.Geometry.Vector3d]::new(-1, 0, 0)), [Grasshopper.Kernel.Types.GH_Vector]::new([Rhino.Geometry.Vector3d]::new(0, -1, 0)))
Check "Vetores X, Y, -X, -Y -> 90, 0, -90, -180" (Same $r 'v0:90 v1:0 v2:-90 v3:-180') ("(" + (Fmt $r.Arrows) + ")")
$r = Invoke-North @([Grasshopper.Kernel.Types.GH_Integer]::new(180), [Grasshopper.Kernel.Types.GH_String]::new('90'), [Grasshopper.Kernel.Types.GH_String]::new('45,5'))
Check "Inteiro, texto e texto pt-BR '45,5' (repete o ultimo)" (Same $r 'v0:-90 v1:0 v2:44.5 v3:44.5') ("(" + (Fmt $r.Arrows) + ")")

Write-Host "`n[9] Nulo / NaN / invalido / vazio" -ForegroundColor Yellow
$r = Invoke-North @((N ([double]::NaN)))
Check "NaN: 90 (Norte para cima) + aviso, sem excecao" ((Same $r 'v0:0 v1:0 v2:0 v3:0') -and $r.Warnings.Count -ge 1) ("(" + (Fmt $r.Arrows) + " | " + ($r.Warnings -join ' ') + ")")
$r = Invoke-North @([Grasshopper.Kernel.Types.GH_String]::new('abc'), (N 90))
Check "Texto invalido: v0 = 90 + aviso; v1.. seguem" ((Same $r 'v0:0 v1:0 v2:0 v3:0') -and $r.Warnings.Count -ge 1)
$r = Invoke-North @([Grasshopper.Kernel.Types.GH_Vector]::new([Rhino.Geometry.Vector3d]::new(0, 0, 0)))
Check "Vetor nulo: 90 + aviso" ((Same $r 'v0:0 v1:0 v2:0 v3:0') -and $r.Warnings.Count -ge 1)
$r = Invoke-North @() @()
Check "Listas vazias: Norte em todas (padrao historico)" (Same $r 'v0:0 v1:0 v2:0 v3:0')

Write-Host "`n[10] Uso legado: booleano em North Direction (versao de 06/10 nao publicada)" -ForegroundColor Yellow
$r = Invoke-North @([Grasshopper.Kernel.Types.GH_Boolean]::new($false))
Check "False unico em N: sem Norte" ($r.Arrows.Count -eq 0)
$r = Invoke-North @([Grasshopper.Kernel.Types.GH_Boolean]::new($true), [Grasshopper.Kernel.Types.GH_Boolean]::new($false), [Grasshopper.Kernel.Types.GH_Boolean]::new($true), [Grasshopper.Kernel.Types.GH_Boolean]::new($false))
Check "[T,F,T,F] em N: vistas 0 e 2" (Same $r 'v0:0 v2:0') ("(" + (Fmt $r.Arrows) + ")")
$r = Invoke-North @([Grasshopper.Kernel.Types.GH_Boolean]::new($true)) @($false)
Check "True legado + Show False: Show manda (sem Norte)" ($r.Arrows.Count -eq 0)

Write-Host "`n[11] Outras quantidades de vistas" -ForegroundColor Yellow
$r = Invoke-North @((N 0)) @($true) $false 1
Check "1 vista" (Same $r 'v0:90') ("(" + (Fmt $r.Arrows) + ")")
$r = Invoke-North @((N 0), (N 90), (N 180), (N 270), (N 0), (N 90)) @($true) $false 6
Check "6 vistas, 6 direcoes" ($r.Arrows.Count -eq 6) ("(" + (Fmt $r.Arrows) + ")")

Write-Host "`n[12] Persistencia do modo de lista (Write/Read)" -ForegroundColor Yellow
$c2 = [Buraqueira_Tools.PillVectorSheetLayout_Component]::new(); $c2.CreateAttributes(); $c2.ListMatch = [Buraqueira_Tools.GlauxListMatchMode]::Cycle
$chunk = [GH_IO.Serialization.GH_LooseChunk]::new('c'); $null = $c2.Write($chunk)
$c3 = [Buraqueira_Tools.PillVectorSheetLayout_Component]::new(); $c3.CreateAttributes(); $null = $c3.Read($chunk)
Check "Cycle volta do arquivo" ($c3.ListMatch -eq [Buraqueira_Tools.GlauxListMatchMode]::Cycle)
$c4 = [Buraqueira_Tools.PillVectorSheetLayout_Component]::new(); $c4.CreateAttributes(); $null = $c4.Read([GH_IO.Serialization.GH_LooseChunk]::new('c'))
Check ".gh sem a chave abre como RepeatLast" ($c4.ListMatch -eq [Buraqueira_Tools.GlauxListMatchMode]::RepeatLast)

Write-Host ""
if ($script:failures.Count -eq 0) { Write-Host ("TODOS OS {0} TESTES PASSARAM" -f $script:checks) -ForegroundColor Green }
else { Write-Host ("{0} de {1} verificacoes FALHARAM: {2}" -f $script:failures.Count, $script:checks, ($script:failures -join '; ')) -ForegroundColor Red }
[RhinoBootNorth]::Stop()
exit ($script:failures.Count)
