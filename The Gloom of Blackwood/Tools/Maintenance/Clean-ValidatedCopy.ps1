$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$target = [IO.Path]::GetFullPath((Join-Path $projectRoot 'IntegrationEvidence/UnityValidationProject'))
$archiveRoot = Join-Path $projectRoot 'Archive/Maintenance-2026-09-10'
$manifest = Get-Content -LiteralPath (Join-Path $archiveRoot 'validation-copy-manifest.json') -Raw | ConvertFrom-Json
if (!(Test-Path -LiteralPath $target)) { Write-Output 'Validation copy already removed.'; exit }
$resolved = (Resolve-Path -LiteralPath $target).Path
if ($resolved -ne $target -or !$resolved.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected deletion target' }
if ($manifest.source -ne $target) { throw 'Manifest is for another directory' }
if ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing a linked directory' }
foreach ($record in $manifest.files) {
    $base = if ($record.action -eq 'preserved_in_archive') { Join-Path $archiveRoot 'ValidationProjectDelta' } else { $projectRoot }
    $copy = [IO.Path]::GetFullPath((Join-Path $base $record.path))
    if (!$copy.StartsWith([IO.Path]::GetFullPath($base)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe manifest path' }
    if (!(Test-Path -LiteralPath $copy) -or (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $record.sha256) { throw "Preserved file changed or missing: $($record.path)" }
}
# The editor process paths were checked separately before this maintenance operation.
$lockPath = Join-Path $target 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $lockPath) { $lock = [IO.File]::Open($lockPath,'Open','Read','None'); $lock.Dispose() }
Remove-Item -LiteralPath $resolved -Recurse -Force
@{Removed=$resolved;Utc=[DateTime]::UtcNow.ToString('o');VerifiedSourceFiles=$manifest.files.Count;PreservedFiles=$manifest.preservedFiles;FreedBytes=$manifest.generatedBytes+$manifest.identicalSourceBytes+$manifest.preservedBytes} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $archiveRoot 'cleanup-result.json')
Write-Output "Removed validated copy; preserved $($manifest.preservedFiles) unique files."
