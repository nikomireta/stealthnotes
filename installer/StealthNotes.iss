#ifndef AppVersion
  #error AppVersion is required; build using scripts/installer.ps1
#endif
#ifndef PayloadDir
  #define PayloadDir "..\artifacts\publish\win-x64"
#endif
#ifndef ReleaseDir
  #define ReleaseDir "..\artifacts\releases"
#endif

[Setup]
AppId={{D28B97C9-B989-4D62-AE44-5E38E17CC367}
AppName=StealthNotes
AppVersion={#AppVersion}
AppPublisher=StealthNotes contributors
DefaultDirName={localappdata}\Programs\StealthNotes
DefaultGroupName=StealthNotes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible and not arm64
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
AppMutex=Local\StealthNotes.InstallerGuard
CloseApplications=no
RestartApplications=no
OutputDir={#ReleaseDir}
OutputBaseFilename=StealthNotes-Setup
SetupIconFile=..\src\StealthNotes\Assets\StealthNotes.ico
UninstallDisplayIcon={app}\StealthNotes.exe
LicenseFile=..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=110
SetupLogging=yes

[Messages]
SetupAppRunningError=%1 is still running.%n%nRight-click its tray icon and choose Quit. Closing note windows or the Notes list is not enough.%n%nThen click OK to continue, or Cancel to exit. Setup will not force-close the app.
UninstallAppRunningError=%1 is still running.%n%nRight-click its tray icon and choose Quit, then click OK to continue, or Cancel to exit.

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcut:";

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "check-running.ps1"; Flags: dontcopy

[Icons]
Name: "{group}\StealthNotes"; Filename: "{app}\StealthNotes.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\StealthNotes"; Filename: "{app}\StealthNotes.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\StealthNotes.exe"; Description: "Open StealthNotes"; Flags: nowait postinstall skipifsilent

; User data is outside {app}; uninstall deliberately preserves notes.sqlite3.

[Code]
function CheckRunningCopies(var Detail: String): Boolean;
var
  ExitCode, I: Integer;
  Lines: TArrayOfString;
  ScriptPath, ResultPath, Arguments: String;
begin
  Result := False;
  Detail := 'Setup could not verify that StealthNotes is closed. Installation has been stopped.';
  try
    ExtractTemporaryFile('check-running.ps1');
    ScriptPath := ExpandConstant('{tmp}\check-running.ps1');
    ResultPath := ExpandConstant('{tmp}\running-copies.txt');
    DeleteFile(ResultPath);
    Arguments := '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ScriptPath + '" -OutputPath "' + ResultPath + '"';
    if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Arguments, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      Exit;
    if not LoadStringsFromFile(ResultPath, Lines) then
      Exit;
    if GetArrayLength(Lines) = 0 then
      Exit;
    begin
      Detail := '';
      for I := 0 to GetArrayLength(Lines) - 1 do
        Detail := Detail + Lines[I] + #13#10;
    end;
    Result := ExitCode = 0;
  except
    Detail := Detail + #13#10 + GetExceptionMessage;
  end;
  Log('Running-app preflight: ' + Detail);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Detail: String;
begin
  Result := '';
  while not CheckRunningCopies(Detail) do
  begin
    if WizardSilent then
    begin
      Result := Detail;
      Exit;
    end;
    if SuppressibleMsgBox(Detail, mbError, MB_RETRYCANCEL, IDCANCEL) <> IDRETRY then
    begin
      Result := Detail;
      Exit;
    end;
  end;
end;
