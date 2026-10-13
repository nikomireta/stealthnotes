param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$taskCode=0
$taskMessage='No running copy of StealthNotes was found for this Windows user.'
try {
    $taskSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $taskCopies=@()
    foreach($taskProcess in @(Get-CimInstance Win32_Process -Filter "Name='StealthNotes.exe'")){
        $taskOwner=Invoke-CimMethod -InputObject $taskProcess -MethodName GetOwnerSid
        if($taskOwner.ReturnValue -ne 0){
            if(-not(Get-Process -Id $taskProcess.ProcessId -ErrorAction SilentlyContinue)){continue}
            throw 'Could not verify the owner of a StealthNotes process. Close running copies and retry.'
        }
        if($taskOwner.Sid -ne $taskSid){continue}
        if([string]::IsNullOrWhiteSpace($taskProcess.ExecutablePath)){throw 'Could not identify the path of a running StealthNotes copy.'}
        $taskCopies+=([string]$taskProcess.ExecutablePath+' (PID '+$taskProcess.ProcessId+')')
    }
    if($taskCopies.Count -gt 0){
        $taskCode=1
        $taskMessage="StealthNotes is still running, possibly from a portable folder.`r`n`r`n"+($taskCopies -join "`r`n")+"`r`n`r`nRight-click its tray icon and choose Quit, then click Retry. Closing a note window is not enough. Setup will not force-close the app."
    }
}catch {
    $taskCode=2
    $taskMessage="Setup could not complete the running-app check. Installation has been stopped.`r`n`r`n"+$_.Exception.Message
}
try{[IO.File]::WriteAllText($OutputPath,$taskMessage,(New-Object Text.UTF8Encoding($true)))}catch{exit 2}
exit $taskCode
