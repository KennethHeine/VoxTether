#define MyAppName "VoxTether"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "VoxTether"
#define MyAppURL "https://github.com/KennethHeine/VoxTether"
#define MyAppExeName "VoxTether.exe"

[Setup]
AppId={{8F3D4B2A-1C5E-4F7D-9E8A-2B6C3D4E5F6A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
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
PrivilegesRequired=admin
; Update handling options
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start with Windows"; GroupDescription: "Startup:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
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

function IsVoxTetherRunning(): Boolean;
var
  ResultCode: Integer;
begin
  // Use tasklist to check if VoxTether is running
  Result := False;
  if Exec('cmd.exe', '/c tasklist /FI "IMAGENAME eq {#MyAppExeName}" 2>NUL | find /I "{#MyAppExeName}" >NUL', 
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := (ResultCode = 0);
  end;
end;

function CloseVoxTether(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  // Try to gracefully close VoxTether using taskkill
  if Exec('taskkill.exe', '/IM {#MyAppExeName} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    // Wait a moment for the process to fully close
    Sleep(1000);
    Result := not IsVoxTetherRunning();
  end;
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
                   'Your settings and user data will be preserved.',
                   mbConfirmation, MB_YESNO);
    if mRes = IDNO then
    begin
      Result := False;
      Exit;
    end;
  end;
  
  // Check if VoxTether is currently running
  if IsVoxTetherRunning() then
  begin
    mRes := MsgBox('VoxTether is currently running.' + #13#10 + #13#10 +
                   'The installer needs to close it before continuing.' + #13#10 +
                   'Do you want to close VoxTether now?',
                   mbConfirmation, MB_YESNO);
    if mRes = IDYES then
    begin
      if not CloseVoxTether() then
      begin
        MsgBox('Failed to close VoxTether. Please close it manually and run the installer again.',
               mbError, MB_OK);
        Result := False;
        Exit;
      end;
    end
    else
    begin
      MsgBox('Please close VoxTether manually and run the installer again.',
             mbInformation, MB_OK);
      Result := False;
      Exit;
    end;
  end;
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
begin
  if CurUninstallStep = usUninstall then
  begin
    // Close VoxTether if running before uninstall
    if IsVoxTetherRunning() then
    begin
      CloseVoxTether();
    end;
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
