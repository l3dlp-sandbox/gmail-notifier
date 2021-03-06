#define MyAppName "Gmail Notifier"
#define MyAppVersion "2.1.3.8"
#define MyAppShortVersion "2.1.8"
#define MyAppPublisher "Xavier Foucrier"
#define MyAppURL "https://github.com/xavierfoucrier/gmail-notifier"
#define MyAppExeName "Gmail notifier.exe"

[Setup]
AppId={{7E60E047-C79B-49A4-8CF6-B33D5565B2E8}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppCopyright=Copyright 2018 Gmail notifier
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppMutex=gmailnotifier-115e363ecbfefd771e55c6874680bc0a
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE.md
OutputDir=..\setup
ArchitecturesInstallIn64BitMode=x64
OutputBaseFilename={#MyAppName} {#MyAppShortVersion}
SetupIconFile=setup.ico
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
VersionInfoVersion={#MyAppVersion}
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 0,6.1
Name: "startup"; Description: "{cm:AutoStartProgram,{#MyAppName}}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkablealone;

[Files]
Source: "..\code\bin\Release 32 bits (x86)\Gmail notifier.exe"; DestDir: "{app}"; Flags: ignoreversion; Check: not Is64BitInstallMode
Source: "..\code\bin\Release 32 bits (x86)\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: not Is64BitInstallMode
Source: "..\code\bin\Release 64 bits (x64)\Gmail notifier.exe"; DestDir: "{app}"; Flags: ignoreversion; Check: Is64BitInstallMode
Source: "..\code\bin\Release 64 bits (x64)\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: Is64BitInstallMode

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:ProgramOnTheWeb,{#MyAppName}}"; Filename: "{#MyAppURL}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "install {language} {code:autorun}"; Flags: runhidden
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Gmail notifier"; ValueData: "{app}\{#MyAppExeName}"; Tasks: startup; Flags: uninsdeletevalue;

[Code]
function autorun(Value: string): string;
begin
	if RegValueExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Gmail notifier') then begin
		Result := 'auto';
	end	else begin
		Result := 'none';
	end;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\Gmail Notifier"