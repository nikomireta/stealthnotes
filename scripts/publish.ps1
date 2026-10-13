param([string]$Dotnet='dotnet',[switch]$FrameworkDependent,[string]$Version='')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if([string]::IsNullOrWhiteSpace($Version)){$Version=& (Join-Path $PSScriptRoot 'get-version.ps1')}
$taskMode=if($FrameworkDependent){'framework-dependent'}else{'portable'}
$taskFolder=if($FrameworkDependent){'win-x64-fdd'}else{'win-x64'}
$taskOutput=Join-Path $taskRoot "artifacts\publish\$taskFolder"
$taskReleases=Join-Path $taskRoot 'artifacts\releases'
$taskZip=Join-Path $taskReleases "StealthNotes-$Version-win-x64-$taskMode.zip"
if($Version -notmatch '^\d+\.\d+\.\d+([-.][A-Za-z0-9.]+)?$'){throw 'Invalid version'}
if(Test-Path -LiteralPath $taskZip){throw "Release already exists: $taskZip. Use a new Version to preserve previous artifacts."}
if(Test-Path -LiteralPath $taskOutput){throw "Publish directory already exists: $taskOutput. Move it aside before publishing another release."}
New-Item -ItemType Directory -Force -Path $taskOutput,$taskReleases|Out-Null
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_NOLOGO='1'
Push-Location $taskRoot
try {
 & $Dotnet publish src/StealthNotes/StealthNotes.csproj -c Release -r win-x64 --self-contained (-not $FrameworkDependent).ToString().ToLowerInvariant() -p:RestoreLockedMode=true -p:Version=$Version -o $taskOutput
 if($LASTEXITCODE -ne 0){throw 'Release publish failed'}
 foreach($taskName in @('README.md','LICENSE','THIRD-PARTY-NOTICES.md')){Copy-Item -LiteralPath (Join-Path $taskRoot $taskName) -Destination $taskOutput}
 Copy-Item -LiteralPath (Join-Path $taskRoot 'licenses') -Destination (Join-Path $taskOutput 'licenses') -Recurse
 Copy-Item -LiteralPath (Join-Path $taskRoot 'docs') -Destination (Join-Path $taskOutput 'docs') -Recurse
 $taskPayload=@(Get-ChildItem -LiteralPath $taskOutput -File -Recurse)
 $taskManifest=@($taskPayload|ForEach-Object {[ordered]@{file=$_.FullName.Substring($taskOutput.Length+1);bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
 $taskManifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $taskOutput 'FILES.sha256.json') -Encoding utf8
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 [IO.Compression.ZipFile]::CreateFromDirectory($taskOutput,$taskZip,[IO.Compression.CompressionLevel]::Optimal,$false)
 $taskSize=[ordered]@{version=$Version;mode=$taskMode;architecture='win-x64';createdUtc=[DateTime]::UtcNow.ToString('o');zipBytes=(Get-Item -LiteralPath $taskZip).Length;installedFileBytes=(Get-ChildItem -LiteralPath $taskOutput -File -Recurse|Measure-Object Length -Sum).Sum;zipSha256=(Get-FileHash -LiteralPath $taskZip -Algorithm SHA256).Hash.ToLowerInvariant();runtime=if($FrameworkDependent){'Requires .NET 10 Windows Desktop Runtime x64'}else{'Included .NET 10 Windows Desktop Runtime'};validation='Release compilation/publish only; no new runtime tests at user request'}
 $taskSize|ConvertTo-Json -Depth 5|Set-Content -LiteralPath ($taskZip+'.json') -Encoding utf8
 Write-Output "Ready: $taskZip"
} finally {Pop-Location}
