param([string]$Project=(Join-Path $PSScriptRoot '../src/StealthNotes/StealthNotes.csproj'))
$ErrorActionPreference='Stop'
[xml]$taskProject=Get-Content -LiteralPath $Project -Raw
$taskVersion=[string]$taskProject.Project.PropertyGroup.Version
if($taskVersion -notmatch '^\d+\.\d+\.\d+([-.][A-Za-z0-9.]+)?$'){throw 'Invalid project version'}
Write-Output $taskVersion
