; =============================================
; ArkDuckBot Desktop Installer (Production)
; Fixes uninstall + supports upgrades
; =============================================

#define MyAppName      "ArkDuckBot Desktop"
#define MyAppVersion   "1.0.0"
#define MyAppPublisher "Franz Ferdinand"
#define MyAppURL       "https://github.com/Franzferdinan51/Ark-DuckBot-Desktop"
#define MyAppExeName   "ArkDuckBot.exe"
; New App ID for ARK DuckBot
#define MyAppId        "{{D9F1D5D2-3F3G-5E3E-AC8F-4C20G1D2BCDE}}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}

UsePreviousAppDir=yes
UsePreviousGroup=yes
CreateUninstallRegKey=yes
OutputDir=..\bin\Installer
OutputBaseFilename=ArkDuckBot-Setup
Compression=lzma2/max
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
DisableProgramGroupPage=yes

SetupIconFile=..\Assets\arkduckbot-icon.ico
WizardImageFile=..\Assets\Images\installer.png
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=admin

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 1. Main files (DLLs, EXE, cash.wav, etc.) from Release folder
Source: "..\bin\Installer\publish\*"; DestDir: "{app}"; Flags: ignoreversion

; 2. Subdirectories from Release folder
Source: "..\bin\Installer\publish\Assets\*";    DestDir: "{app}\Assets";    Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\bin\Installer\publish\runtime\*";   DestDir: "{app}\runtime";   Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\runtimes"
Type: filesandordirs; Name: "{app}\runtime"
Type: filesandordirs; Name: "{app}\Assets"

[Code]
procedure DeleteOldBrokenUninstallers;
var Key: string;
begin
  Key := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\ArkDuckBot Desktop';
  RegDeleteKeyIncludingSubkeys(HKLM, Key);
  RegDeleteKeyIncludingSubkeys(HKCU, Key);
  Key := 'Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\ArkDuckBot Desktop';
  RegDeleteKeyIncludingSubkeys(HKLM, Key);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then DeleteOldBrokenUninstallers;
end;