$ErrorActionPreference='Stop'
$projectPath=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectPath
$unityData='C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Data'
$responseRoot='Library/Bee/artifacts/1900b0aE.dag'
$compileDir='IntegrationEvidence/Compile'
New-Item -ItemType Directory -Path $compileDir -Force | Out-Null
foreach($assembly in @('DawnGuard.Core','Assembly-CSharp','Assembly-CSharp-Editor')) {
    $lines=Get-Content -LiteralPath "$responseRoot/$assembly.rsp"
    $lines=$lines | ForEach-Object {
        $line=$_
        foreach($localAssembly in @('DawnGuard.Core','Assembly-CSharp','Assembly-CSharp-Editor')) {
            $line=$line.Replace("$responseRoot/$localAssembly.dll","$compileDir/$localAssembly.dll").Replace("$responseRoot/$localAssembly.ref.dll","$compileDir/$localAssembly.ref.dll")
        }
        $line
    }
    if($assembly -eq 'Assembly-CSharp') { $lines+='"Assets/DawnGuard/BlackwoodV2/Runtime/BlackwoodPlayVerification.cs"' }
    if($assembly -eq 'Assembly-CSharp-Editor') { $lines+='"Assets/DawnGuard/BlackwoodV2/Editor/BlackwoodIntegrationChecks.cs"' }
    $lines | Select-Object -Unique | Set-Content -LiteralPath "$compileDir/$assembly.rsp" -Encoding utf8
    & "$unityData/NetCoreRuntime/dotnet.exe" "$unityData/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll" "@$compileDir/$assembly.rsp" 2>&1 | Tee-Object -FilePath "$compileDir/$assembly.log"
    if($LASTEXITCODE -ne 0) {throw "Unity reference compilation failed: $assembly"}
}
'PASS: Three assemblies compiled with the installed Unity compiler and project references.'
