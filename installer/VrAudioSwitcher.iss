; Inno Setup script for VR Audio Switcher.
; Build:  iscc installer\VrAudioSwitcher.iss   (after publishing to .\publish)
; Or run: installer\build-installer.ps1         (publishes + compiles in one step)

#define MyAppName "VR Audio Switcher"
; Version may be overridden from the command line: iscc /DMyAppVersion=1.2.3
#ifndef MyAppVersion
  #define MyAppVersion "1.0.2"
#endif
#define MyAppPublisher "Ernest Ribeiro"
#define MyAppExeName "VrAudioSwitcher.exe"

[Setup]
; A stable AppId keeps upgrades/uninstall clean. (Generated once for this app.)
AppId={{6F2C8B1A-3D4E-4A9C-9F21-7B5E0C2D8A44}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\VR Audio Switcher
DefaultGroupName=VR Audio Switcher
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=dist
OutputBaseFilename=VrAudioSwitcher-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Per-user install — no admin prompt, matches the per-user "launch at startup" key.
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked
Name: "startupicon"; Description: "Launch automatically when Windows starts"; Flags: unchecked

[Files]
; Single-file publish output (the exe bundles the native openvr_api.dll).
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
Name: "{group}\VR Audio Switcher"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\VR Audio Switcher"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Optional launch-at-startup (per-user Run). The app also exposes this toggle in its UI.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "VrAudioSwitcher"; \
  ValueData: """{app}\{#MyAppExeName}"""; Tasks: startupicon; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,VR Audio Switcher}"; \
  Flags: nowait postinstall skipifsilent

[Code]
const
  DotNetDownloadUrl = 'https://dotnet.microsoft.com/download/dotnet/8.0/runtime';

// True if any Microsoft.WindowsDesktop.App 8.x runtime is present.
function HasDotNet8Desktop(): Boolean;
var
  BaseDir: String;
  FindRec: TFindRec;
begin
  Result := False;
  BaseDir := ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if FindFirst(BaseDir + '\8.*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          Result := True;
          Break;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function InitializeSetup(): Boolean;
var
  ErrCode: Integer;
begin
  Result := True;
  if not HasDotNet8Desktop() then
  begin
    if MsgBox('VR Audio Switcher needs the .NET 8 Desktop Runtime, which was not found.' + #13#10 +
              'Open the download page now? (Install it, then run this setup again.)',
              mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', DotNetDownloadUrl, '', '', SW_SHOW, ewNoWait, ErrCode);
    Result := False;
  end;
end;
