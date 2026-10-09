#define AppVersion "1.5"

[Setup]
AppId={{28C53A2F-4969-4C4E-8C0B-8E7EF626CA70}
AppName=Echo Nano FLAC
AppVersion={#AppVersion}
AppVerName=Echo Nano FLAC {#AppVersion}
DefaultDirName={localappdata}\Programs\EchoNanoFLAC
DefaultGroupName=Echo Nano FLAC
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
SetupArchitecture=x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\dist
OutputBaseFilename=EchoNanoFLAC-Setup-1.5
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\Source\EchoNanoFLAC.ico
UninstallDisplayIcon={app}\EchoNanoFLAC.exe
UninstallDisplayName=Echo Nano FLAC
CreateUninstallRegKey=not IsTestInstall
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
LicenseFile=..\CONDICIONES.txt

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked; Check: not IsTestInstall

[Files]
Source: "payload\EchoNanoFLAC.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "payload\ffmpeg.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "payload\LEEME.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "payload\AVISOS-FFmpeg.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "payload\Source\*"; DestDir: "{app}\Source"; Flags: ignoreversion recursesubdirs

Source: "payload\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "payload\CONDICIONES.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "payload\COPYING.FFmpeg-GPLv3.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\Echo Nano FLAC"; Filename: "{app}\EchoNanoFLAC.exe"; WorkingDir: "{app}"; Check: not IsTestInstall
Name: "{userdesktop}\Echo Nano FLAC"; Filename: "{app}\EchoNanoFLAC.exe"; WorkingDir: "{app}"; Tasks: desktopicon; Check: not IsTestInstall

[Run]
Filename: "{app}\EchoNanoFLAC.exe"; Description: "Abrir Echo Nano FLAC"; Flags: nowait postinstall skipifsilent; Check: not IsTestInstall

[Code]
function IsTestInstall: Boolean;
begin
  Result := ExpandConstant('{param:TEST|0}') = '1';
end;

function InitializeSetup: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM32, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then
    MsgBox('Esta aplicación necesita .NET Framework 4.8 o posterior. Instálalo desde Microsoft y vuelve a ejecutar este asistente.', mbError, MB_OK);
end;
