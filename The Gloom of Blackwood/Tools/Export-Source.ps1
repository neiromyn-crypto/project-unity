param([string]$OutputPath, [switch]$ListOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$files = @(foreach ($name in @('Assets','Packages','ProjectSettings','Design','Tools')) {
    Get-ChildItem -LiteralPath (Join-Path $projectRoot $name) -File -Recurse -Force
})
$files += @(Get-ChildItem -LiteralPath $projectRoot -File -Force | Where-Object {$_.Name -like 'README*.md' -or $_.Name -in @('.gitignore','.gitattributes')})
$files = @($files | Where-Object {$_.Extension -notin @('.blend1','.blend2','.pyc') -and $_.FullName -notmatch '[\\/]__pycache__[\\/]'} | Sort-Object FullName)
foreach ($required in @('Assets/DawnGuard/BlackwoodLTD/Scenes/BlackwoodCoast.unity','Packages/manifest.json','Packages/packages-lock.json','ProjectSettings/ProjectVersion.txt')) {
    if (!(Test-Path -LiteralPath (Join-Path $projectRoot $required))) { throw "Required source file missing: $required" }
}
if ($ListOnly) {
    [pscustomobject]@{Files=$files.Count;UncompressedBytes=($files | Measure-Object Length -Sum).Sum;Included='Assets (+ .meta), Packages, ProjectSettings, Design, Tools, README';Excluded='Library, Temp, Logs, UserSettings, .vs, Archive, IntegrationEvidence'} | ConvertTo-Json
    return
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) { throw 'Specify -OutputPath or use -ListOnly.' }
$destination = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $destination) { throw 'Destination already exists; choose a new archive name.' }
foreach ($name in @('Assets','Packages','ProjectSettings','Design','Tools')) {
    if ($destination.StartsWith((Join-Path $projectRoot $name)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Place the archive outside source directories.' }
}
New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
$archive = [IO.Compression.ZipFile]::Open($destination, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        $entry = $file.FullName.Substring($projectRoot.Length+1).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$entry,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
$readback = [IO.Compression.ZipFile]::OpenRead($destination)
try { if ($readback.Entries.Count -ne $files.Count) { throw 'Archive entry count mismatch' } } finally { $readback.Dispose() }
Write-Output "Source archive created: $destination"
