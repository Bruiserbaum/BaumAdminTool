#define AppName        "BaumAdminTool"
#define AppVersion     "1.0.5"
#define AppVersionFull "1.0.5"
#define AppPublisher   "Bnuss"
#define AppExeName     "BaumAdminTool.exe"
#define PublishDir     "..\BaumAdminTool\bin\Release\net8.0-windows10.0.22621.0\win-x64\publish"

[Setup]
AppId={{B3C4D5E6-F7A8-9B0C-1D2E-3F4A5B6C7D8E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
OutputDir=output
OutputBaseFilename=BaumAdminTool-Setup-{#AppVersionFull}
SetupIconFile=..\BaumAdminTool\Resources\app.ico
UninstallDisplayIcon={app}\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
CloseApplications=yes
MinVersion=10.0.22621
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion restartreplace uninsrestartdelete
Source: "{#PublishDir}\*.dll";          DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\BaumAdminTool\Resources\app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";             Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}";   Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";       Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall
