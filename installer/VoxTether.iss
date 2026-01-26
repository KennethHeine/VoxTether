#define MyAppName "VoxTether"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "VoxTether"
#define MyAppURL "https://github.com/KennethHeine/VoxTether"
#define MyAppExeName "VoxTether.exe"
#define MyAppMutex "VoxTether_SingleInstance_Mutex"

[Setup]
AppId={{8F3D4B2A-1C5E-4F7D-9E8A-2B6C3D4E5F6A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
; Install to user's local app data by default (no admin required)
DefaultDirName={localappdata}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=Output
OutputBaseFilename=VoxTether-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
; Allow installation without admin privileges (user context)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; Update handling options
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
; Use Inno Setup's built-in application closure for VoxTether.exe
CloseApplications=force
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start with Windows"; GroupDescription: "Startup:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Use user-specific locations for shortcuts
Name: "{userprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{userprograms}\{#MyAppName}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startupicon

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\{#MyAppName}"

[Code]
var
  IsUpgrade: Boolean;
  PreviousVersion: String;

function GetPreviousVersion(): String;
var
  UninstallKey: String;
  DisplayVersion: String;
begin
  Result := '';
  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F3D4B2A-1C5E-4F7D-9E8A-2B6C3D4E5F6A}_is1';
  if RegQueryStringValue(HKLM, UninstallKey, 'DisplayVersion', DisplayVersion) then
    Result := DisplayVersion
  else if RegQueryStringValue(HKCU, UninstallKey, 'DisplayVersion', DisplayVersion) then
    Result := DisplayVersion;
end;

function InitializeSetup(): Boolean;
var
  mRes: Integer;
begin
  Result := True;
  
  // Check for existing installation
  PreviousVersion := GetPreviousVersion();
  IsUpgrade := (PreviousVersion <> '');
  
  if IsUpgrade then
  begin
    // Inform user about the upgrade
    mRes := MsgBox('VoxTether v' + PreviousVersion + ' is already installed.' + #13#10 + #13#10 +
                   'Do you want to upgrade to v{#MyAppVersion}?' + #13#10 + #13#10 +
                   'Your settings and user data will be preserved.' + #13#10 +
                   'If VoxTether is running, it will be closed automatically.',
                   mbConfirmation, MB_YESNO);
    if mRes = IDNO then
    begin
      Result := False;
      Exit;
    end;
  end;
  
  // Note: Running application will be closed automatically by Inno Setup
  // via CloseApplications=force setting
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if IsUpgrade then
    begin
      Log('Upgrade from ' + PreviousVersion + ' to {#MyAppVersion} completed successfully.');
    end
    else
    begin
      Log('Fresh installation of {#MyAppVersion} completed successfully.');
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  mRes: Integer;
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Try to close VoxTether if running before uninstall
    Exec('taskkill.exe', '/IM {#MyAppExeName} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(500);
  end;
  
  if CurUninstallStep = usPostUninstall then
  begin
    mRes := MsgBox('Do you want to delete user settings and logs?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2);
    if mRes = IDYES then
    begin
      DelTree(ExpandConstant('{userappdata}\{#MyAppName}'), True, True, True);
    end;
  end;
end;
