#ifndef AppVersion
  #define AppVersion "1.0.0"
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

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcut:";

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\StealthNotes"; Filename: "{app}\StealthNotes.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\StealthNotes"; Filename: "{app}\StealthNotes.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\StealthNotes.exe"; Description: "Open StealthNotes"; Flags: nowait postinstall skipifsilent

; User data is outside {app}; uninstall deliberately preserves notes.sqlite3.
