param([string]$Destination=(Join-Path $PSScriptRoot '../.tools/inno-setup'))
$ErrorActionPreference='Stop'
$taskDestination=[IO.Path]::GetFullPath($Destination)
$taskCompiler=Join-Path $taskDestination 'ISCC.exe'
if(Test-Path -LiteralPath $taskCompiler){Write-Output $taskCompiler;return}
$taskVersion='6.7.3'
$taskUrl="https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-$taskVersion.exe"
New-Item -ItemType Directory -Force -Path $taskDestination|Out-Null
$taskDownload=Join-Path $taskDestination "innosetup-$taskVersion.exe"
Invoke-WebRequest -Uri $taskUrl -OutFile $taskDownload
$taskSignature=Get-AuthenticodeSignature -LiteralPath $taskDownload
if($taskSignature.Status -ne 'Valid' -or $taskSignature.SignerCertificate.Subject -notmatch 'CN=Pyrsys B\.V\.,'){
 throw 'Inno Setup download has an invalid signature or unexpected publisher; installation stopped.'
}
$taskArguments=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/NOICONS','/CURRENTUSER',('/DIR="'+$taskDestination+'"'))
$taskProcess=Start-Process -FilePath $taskDownload -ArgumentList $taskArguments -WindowStyle Hidden -Wait -PassThru
if($taskProcess.ExitCode -ne 0 -or -not(Test-Path -LiteralPath $taskCompiler)){throw 'Inno Setup compiler installation failed'}
[ordered]@{version=$taskVersion;url=$taskUrl;sha256=(Get-FileHash -LiteralPath $taskDownload -Algorithm SHA256).Hash;publisher=$taskSignature.SignerCertificate.Subject}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskDestination 'download.json') -Encoding utf8
Write-Output $taskCompiler
