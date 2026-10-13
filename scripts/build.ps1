param([string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_NOLOGO='1'
Push-Location $taskRoot
try {
 & $Dotnet restore src/StealthNotes/StealthNotes.csproj -r win-x64 --locked-mode
 if($LASTEXITCODE -ne 0){throw 'NuGet restore failed'}
 & $Dotnet build src/StealthNotes/StealthNotes.csproj -c Release -r win-x64 --no-restore
 if($LASTEXITCODE -ne 0){throw 'Release compilation failed'}
} finally {Pop-Location}
