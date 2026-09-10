$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sources = @((Join-Path $projectRoot 'Assets/DawnGuard/Core/Definitions.cs'))
$sources += @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets/DawnGuard/BlackwoodLTD/Core') -Filter '*.cs' -File | Select-Object -ExpandProperty FullName)
Add-Type -Path $sources
$report = [DawnGuard.BlackwoodLTD.CoastChecks]::Run()
$outputDir = Join-Path $projectRoot 'IntegrationEvidence/Coast'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$report | Set-Content -LiteralPath (Join-Path $outputDir 'core-checks.txt') -Encoding utf8
$report
