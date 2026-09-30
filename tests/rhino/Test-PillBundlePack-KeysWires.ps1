# Pill Bundle Pack (Parameter Hub) com Keys E Wires ⚡ apontando para os mesmos transmissores.
# Defeito até a v1.2.0: o caminho por fio (Wires ⚡) trata os apelidos "Pill Transmitter" e "Tx" como genéricos e nomeia a
# entrada pela chave (SRF_A1); o caminho pelo barramento (Keys) não os trata, monta "SRF_A1_Pill Transmitter", passa pelo
# teste de "já empacotado", que compara só o texto, e cada transmissor entra DUAS vezes no pacote, sem aviso.
# Achado no Teatro Escola (Glaux Acoustics, §5 #21/#32): 17 grupos de superfícies viravam 34 entradas e 157 superfícies, 314.
# Correção (v1.2.1): o canal de um transmissor já ligado em Wires ⚡ é reconhecido pela instância (SourceComponentGuid), não
# pelo nome; e os dois caminhos usam a mesma lista de apelidos genéricos, para o nome da entrada não mudar com os fios.
# Exige o Rhino 8 instalado (RhinoCore em processo, sem interface); o CI não roda este teste. Uso:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests\rhino\Test-PillBundlePack-KeysWires.ps1 [-Gha <Glaux_Tools.gha>]
#       [-OldGh <.gh gravado pela v1.2.0 ou anterior> -OldGhHub <índice do Pill Bundle Pack no arquivo>]   (parte E)
param([string]$Gha = '', [string]$OldGh = '', [int]$OldGhHub = 837)
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

$script:failures = @(); $script:checks = 0
function Check([string]$label, [bool]$ok, [string]$detail = '') {
    $script:checks++
    if ($ok) { Write-Host ("  OK     {0} {1}" -f $label, $detail) } else { Write-Host ("  FALHOU {0} {1}" -f $label, $detail); $script:failures += $label }
}

$HubType = [Buraqueira_Tools.PillHub]
$Mode = [Grasshopper.Kernel.GH_SolutionMode]::Silent

# Monta um documento com transmissores (2 números cada) e um Pill Bundle Pack; devolve entradas e itens do pacote.
# $txSpecs: lista de @{ Key; Nick; Wire (liga em Wires ⚡); Values }
function Invoke-Pack($txSpecs, [string[]]$packKeys, [string]$ns = 'GLOBAL') {
    $HubType::PurgeAll()
    $doc = [Grasshopper.Kernel.GH_Document]::new(); $doc.Enabled = $true
    $txs = @()
    foreach ($s in $txSpecs) {
        $tx = [Buraqueira_Tools.PillTransmitter_Component]::new(); $tx.CreateAttributes()
        if ($s.Nick) { $tx.NickName = $s.Nick }
        $tx.Params.Input[0].PersistentData.Append([Grasshopper.Kernel.Types.GH_String]::new($s.Key))
        foreach ($v in $s.Values) { $tx.Params.Input[1].PersistentData.Append([Grasshopper.Kernel.Types.GH_Number]::new([double]$v)) }
        $null = $doc.AddObject($tx, $false); $txs += $tx
    }
    $pack = [Buraqueira_Tools.PillBundlePack_Component]::new(); $pack.CreateAttributes()
    foreach ($k in $packKeys) { $pack.Params.Input[0].PersistentData.Append([Grasshopper.Kernel.Types.GH_String]::new($k)) }
    $pack.Params.Input[2].PersistentData.Clear(); $pack.Params.Input[2].PersistentData.Append([Grasshopper.Kernel.Types.GH_String]::new($ns))
    for ($i = 0; $i -lt $txSpecs.Count; $i++) { if ($txSpecs[$i].Wire) { $pack.Params.Input[3].AddSource($txs[$i].Params.Output[0]) } }
    $null = $doc.AddObject($pack, $false)
    $unpack = [Buraqueira_Tools.PillBundleUnpack_Component]::new(); $unpack.CreateAttributes()
    $unpack.Params.Input[0].AddSource($pack.Params.Output[0])
    $null = $doc.AddObject($unpack, $false)
    $doc.NewSolution($true, $Mode)

    $goo = @($pack.Params.Output[0].VolatileData.AllData($true))[0]
    $r = [pscustomobject]@{ Entries = -1; Keys = @(); Items = -1; UnpackItems = -1; Messages = '' }
    $msgs = @($pack.RuntimeMessages([Grasshopper.Kernel.GH_RuntimeMessageLevel]::Error)) + @($pack.RuntimeMessages([Grasshopper.Kernel.GH_RuntimeMessageLevel]::Warning))
    $r.Messages = ($msgs -join ' | ')
    if ($goo -ne $null -and $goo.Value -ne $null) {
        $entries = $goo.Value.Entries
        $r.Entries = $entries.Count
        $r.Keys = @($entries.get_Keys() | Sort-Object)
        $n = 0
        foreach ($kv in $entries.GetEnumerator()) { if ($kv.Value -is [System.Collections.IList]) { $n += $kv.Value.Count } elseif ($kv.Value -ne $null) { $n += 1 } }
        $r.Items = $n
        $r.UnpackItems = $unpack.Params.Output[1].VolatileData.DataCount
    }
    $HubType::PurgeAll()
    return $r
}

function Specs([string]$nick, [bool[]]$wires, [string]$prefix = 'SRF_A') {
    $list = @()
    for ($i = 0; $i -lt $wires.Count; $i++) { $list += @{ Key = "$prefix$($i + 1)"; Nick = $nick; Wire = $wires[$i]; Values = @((10 * ($i + 1) + 1), (10 * ($i + 1) + 2)) } }
    return , $list
}
function Show($r) { "(entradas {0}, itens {1}, Unpack {2}; chaves: {3}{4})" -f $r.Entries, $r.Items, $r.UnpackItems, ($r.Keys -join ', '), $(if ($r.Messages) { "; mensagens: $($r.Messages)" } else { '' }) }

Write-Host ("Glaux_Tools: {0} ({1})" -f [Diagnostics.FileVersionInfo]::GetVersionInfo($Gha).ProductVersion, $Gha)

Write-Host "`nA) Keys = 'SRF' + Wires ⚡ nos mesmos 3 transmissores (o caso do Teatro Escola): 3 entradas, 6 itens"
foreach ($nick in @('Pill Transmitter', 'Tx', 'PillTx', 'Transmitter', 'Paredes')) {
    $r = Invoke-Pack (Specs $nick @($true, $true, $true)) @('SRF')
    Check "A apelido '$nick'" ($r.Entries -eq 3 -and $r.Items -eq 6 -and $r.UnpackItems -eq 6) (Show $r)
}

Write-Host "`nB) O nome da entrada não depende dos fios: só Keys, só Wires e Keys + Wires dão as mesmas chaves"
foreach ($nick in @('Pill Transmitter', 'Tx', 'PillTx')) {
    $rk = Invoke-Pack (Specs $nick @($false, $false, $false)) @('SRF')
    $rw = Invoke-Pack (Specs $nick @($true, $true, $true)) @()
    $rb = Invoke-Pack (Specs $nick @($true, $true, $true)) @('SRF')
    $expected = 'SRF_A1, SRF_A2, SRF_A3'
    Check "B só Keys, '$nick'" ($rk.Entries -eq 3 -and ($rk.Keys -join ', ') -eq $expected) (Show $rk)
    Check "B só Wires, '$nick'" ($rw.Entries -eq 3 -and ($rw.Keys -join ', ') -eq $expected) (Show $rw)
    Check "B Keys + Wires, '$nick'" ($rb.Entries -eq 3 -and ($rb.Keys -join ', ') -eq $expected) (Show $rb)
}

Write-Host "`nC) Casos mistos: nada some e nada duplica"
$r = Invoke-Pack (Specs 'Pill Transmitter' @($true, $false, $false)) @('SRF')
Check 'C fio só no 1º transmissor; os outros 2 vêm do barramento' ($r.Entries -eq 3 -and $r.Items -eq 6) (Show $r)
$r = Invoke-Pack (Specs 'Pill Transmitter' @($true, $true, $true, $false)) @('SRF')
Check 'C 4º transmissor do grupo sem fio entra pelo barramento' ($r.Entries -eq 4 -and $r.Items -eq 8) (Show $r)
$r = Invoke-Pack (Specs 'Pill Transmitter' @($true, $false, $false)) @('SRF_A1')
Check 'C chave exata SRF_A1 + fio no mesmo transmissor: 1 entrada' ($r.Entries -eq 1 -and $r.Items -eq 2) (Show $r)
$same = Specs 'Pill Transmitter' @($true, $true)
$same[1].Values = $same[0].Values
$r = Invoke-Pack $same @('SRF')
Check 'C dois transmissores com os MESMOS dados continuam 2 entradas' ($r.Entries -eq 2 -and $r.Items -eq 4) (Show $r)
$r = Invoke-Pack (Specs 'Pill Transmitter' @($true, $true, $true)) @('SRF') 'SALA_01'
Check 'C namespace SALA_01: 3 entradas com escopo' ($r.Entries -eq 3 -and $r.Items -eq 6 -and ($r.Keys -join ', ') -eq 'SALA_01::SRF_A1, SALA_01::SRF_A2, SALA_01::SRF_A3') (Show $r)
$mixed = (Specs 'Pill Transmitter' @($true, $true)) + (Specs 'Pill Transmitter' @($true) 'GEO_B')
$r = Invoke-Pack $mixed @('SRF')
Check 'C transmissor de outro grupo ligado por fio também entra (1 vez)' ($r.Entries -eq 3 -and $r.Items -eq 6) (Show $r)

Write-Host "`nD) Hub #837 do Teatro Escola: as 17 chaves reais, apelido 'Pill Transmitter', Keys = 'SRF' + 17 Wires ⚡, 157 itens"
$teatroKeys = @('SRF_LigacaoEntrePaineis', 'SRF_AbsorvedoresPaineis', 'SRF_Balcao', 'SRF_Carpete', 'SRF_CarpeteParede', 'SRF_EspelhosParedes',
    'SRF_ForroAbsorvedor', 'SRF_ForroReflexivo', 'SRF_Palco', 'SRF_Paredes', 'SRF_Poltronas', 'SRF_Portas', 'SRF_Urdimento', 'SRF_Vidro',
    'SRF_Coxias', 'SRF_ParedeFundoPalco', 'SRF_ForroAbsorverdorCurvo')
$counts = @(12) + @(9) * 15 + @(10)   # 157 no total
$teatro = @()
for ($i = 0; $i -lt 17; $i++) { $teatro += @{ Key = $teatroKeys[$i]; Nick = 'Pill Transmitter'; Wire = $true; Values = @(1..$counts[$i]) } }
$r = Invoke-Pack $teatro @('SRF')
Check 'D 17 entradas e 157 itens (v1.2.0: 34 e 314)' ($r.Entries -eq 17 -and $r.Items -eq 157 -and $r.UnpackItems -eq 157) ("(entradas {0}, itens {1}, Unpack {2})" -f $r.Entries, $r.Items, $r.UnpackItems)
$wiresOnly = @(); foreach ($s in $teatro) { $wiresOnly += $s.Clone() }
$r = Invoke-Pack $wiresOnly @()
Check 'D mesmo hub só com os Wires (sem Keys): 17 entradas e 157 itens' ($r.Entries -eq 17 -and $r.Items -eq 157) ("(entradas {0}, itens {1}{2})" -f $r.Entries, $r.Items, $(if ($r.Messages) { "; mensagens: $($r.Messages)" } else { '' }))

Write-Host "`nE) Arquivo antigo: a entrada Keys vem gravada como obrigatória (Optional = False) e passa a opcional ao abrir"
if ($OldGh -and (Test-Path -LiteralPath $OldGh)) {
    $ar = [GH_IO.Serialization.GH_Archive]::new()
    if (-not $ar.ReadFromFile($OldGh)) { throw "leitura falhou: $OldGh" }
    $container = $ar.GetRootNode.FindChunk('Definition').FindChunk('DefinitionObjects').FindChunk('Object', $OldGhHub).FindChunk('Container')
    $fresh = [Buraqueira_Tools.PillBundlePack_Component]::new(); $fresh.CreateAttributes()
    $okRead = $fresh.Read($container)
    $paramChunks = @($container.Chunks); $pd = $container.FindChunk('ParameterData'); if ($null -ne $pd) { $paramChunks += @($pd.Chunks) }
    $fileOptional = $null
    foreach ($pc in $paramChunks) { if ($pc.Name -in 'param_input', 'InputParam' -and $pc.GetString('Name') -eq 'Keys') { $fileOptional = $pc.GetBoolean('Optional') } }
    Check 'E Pill Bundle Pack lido do .gh' ($okRead -and $fresh.Params.Input.Count -eq 4) ("(instância {0}; Keys no arquivo: Optional = {1})" -f $fresh.InstanceGuid, $fileOptional)
    Check 'E Keys opcional depois de abrir' ($fresh.Params.Input[0].Optional) ("(Optional = {0})" -f $fresh.Params.Input[0].Optional)
} else { Write-Host "  (sem -OldGh: E não executado)" }

Write-Host ("`n{0} verificacoes, {1} falhas" -f $script:checks, $script:failures.Count)
$code = [int]($script:failures.Count -gt 0)
[RhinoBootTools]::Stop()
[Environment]::Exit($code)
