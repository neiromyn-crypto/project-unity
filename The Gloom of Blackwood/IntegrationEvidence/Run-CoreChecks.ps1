$ErrorActionPreference='Stop'
$projectPath=Split-Path $PSScriptRoot -Parent
$sources=@(Get-ChildItem -LiteralPath "$projectPath\Assets\DawnGuard\Core" -Recurse -Filter '*.cs' | Select-Object -ExpandProperty FullName)
$sources+="$projectPath\Assets\DawnGuard\BlackwoodV2\Runtime\BlackwoodBalance.cs"
Add-Type -Path $sources
$results=[System.Collections.Generic.List[string]]::new()
$results.Add([DawnGuard.Core.Diagnostics.SimulationChecks]::RunAll())
foreach($strategy in @('gun','walls')) {
    $rules=[DawnGuard.BlackwoodV2.BlackwoodBalance]::Create()
    $game=[DawnGuard.Core.GameSession]::new($rules)
    $buildError=''
    if($strategy -eq 'gun') { $ok=$game.Construction.TryBuild('gun',[DawnGuard.Core.Cell]::new(7,9),[ref]$buildError) }
    else { foreach($x in 6..8) { $ok=$game.Construction.TryBuild('wall',[DawnGuard.Core.Cell]::new($x,9),[ref]$buildError); if(!$ok) {throw $buildError} } }
    if(!$ok) {throw $buildError}
    $null=$game.StartNight()
    for($step=0; $step -lt 1600 -and $game.Phase -eq 'Night'; $step++) { $game.Tick(0.05) }
    if($game.Day -ne 2 -or $game.Phase -ne 'Day') {throw "Blackwood first night failed: $strategy"}
    $results.Add("PASS Blackwood first night $strategy; day=$($game.Day); credits=$($game.Wallet.Credits); shelterHP=$($game.Shelter.health)")
}
$results | Set-Content -LiteralPath "$PSScriptRoot\native-core-checks.txt" -Encoding utf8
$results
