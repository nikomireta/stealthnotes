param([string]$Compiler='',[string]$Version='1.0.0')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if($Version -notmatch '^\d+\.\d+\.\d+([-.][A-Za-z0-9.]+)?$'){throw 'Invalid version'}
if(-not $Compiler){
 $taskCandidates=@((Join-Path $taskRoot '.tools/inno-setup/ISCC.exe'),(Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'))
 $Compiler=$taskCandidates|Where-Object {Test-Path -LiteralPath $_}|Select-Object -First 1
 if(-not $Compiler){throw 'Inno Setup not found. Run ./scripts/setup-inno.ps1 or pass -Compiler <path-to-ISCC.exe>.'}
}
$taskPayload=Join-Path $taskRoot 'artifacts/publish/win-x64'
$taskRelease=Join-Path $taskRoot 'artifacts/releases'
$taskSetup=Join-Path $taskRelease 'StealthNotes-Setup.exe'
if(Test-Path -LiteralPath $taskSetup){throw 'Installer already exists; move it aside to preserve the previous artifact.'}
if(-not(Test-Path -LiteralPath (Join-Path $taskPayload 'coreclr.dll'))){throw 'Publish the self-contained payload first using scripts/publish.ps1.'}
$taskMetadata=Get-Content -LiteralPath (Join-Path $taskRelease "StealthNotes-$Version-win-x64-portable.zip.json") -Raw|ConvertFrom-Json
if($taskMetadata.version -ne $Version -or $taskMetadata.mode -ne 'portable'){throw 'Payload metadata does not match installer version'}
& $Compiler "/DAppVersion=$Version" "/DPayloadDir=$taskPayload" "/DReleaseDir=$taskRelease" (Join-Path $taskRoot 'installer/StealthNotes.iss')
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
$taskHash=(Get-FileHash -LiteralPath $taskSetup -Algorithm SHA256).Hash.ToLowerInvariant()
"$taskHash  StealthNotes-Setup.exe"|Set-Content -LiteralPath ($taskSetup+'.sha256') -Encoding ascii
[ordered]@{version=$Version;createdUtc=[DateTime]::UtcNow.ToString('o');installerBytes=(Get-Item -LiteralPath $taskSetup).Length;sha256=$taskHash;runtime='Included .NET 10 Windows Desktop Runtime x64';minimumOS='Windows 11 x64';compiler=(Get-Item -LiteralPath $Compiler).VersionInfo.FileVersion;signature='Unsigned';validation='Installer compiled only. Install, upgrade and uninstall have NOT been tested at user request.'}|ConvertTo-Json|Set-Content -LiteralPath ($taskSetup+'.json') -Encoding utf8
Write-Output "Ready: $taskSetup"
