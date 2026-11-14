; Shooter Windows installer (T073). Authored against a validated Package-Release output.
; Compile only through Tools/Build-Installer.ps1, which supplies every /D define below.
; Required defines: AppId (GUID without braces), AppVersion (n.n.n), SourceDir, ExtraDir, OutputDir, OutputName, Publisher.

#ifndef AppId
  #error AppId define missing
#endif
#ifndef AppVersion
  #error AppVersion define missing
#endif
#ifndef SourceDir
  #error SourceDir define missing
#endif

[Setup]
AppId={{{#AppId}}
AppName=Shooter
AppVersion={#AppVersion}
AppVerName=Shooter {#AppVersion}
AppPublisher={#Publisher}
VersionInfoVersion={#AppVersion}.0
VersionInfoProductName=Shooter
DefaultDirName={localappdata}\Programs\Shooter
UsePreviousAppDir=yes
DisableProgramGroupPage=yes
DefaultGroupName=Shooter
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\Shooter.exe
UninstallDisplayName=Shooter
CloseApplications=yes
CloseApplicationsFilter=Shooter.exe
RestartApplications=no
Compression=lzma2
SolidCompression=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputName}
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: checkedonce

[Files]
; Order matters: bulk payload first, then installer metadata and the executable last.
; ignoreversion rewrites every file on a re-run of the same installer (repair).
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "Shooter.exe"; Flags: ignoreversion recursesubdirs createallsubdirs overwritereadonly
Source: "{#ExtraDir}\installer-identity.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ExtraDir}\payload-files.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\Shooter.exe"; DestDir: "{app}"; Flags: ignoreversion overwritereadonly

[Icons]
Name: "{autoprograms}\Shooter"; Filename: "{app}\Shooter.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Shooter"; Filename: "{app}\Shooter.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Shooter.exe"; Description: "Launch Shooter"; Flags: nowait postinstall skipifsilent unchecked

; User settings live in %USERPROFILE%\AppData\LocalLow\<company>\<product> (Application.persistentDataPath),
; outside {app}; neither upgrade nor uninstall touches them.

[Code]
function ParseVersionPart(var S: String): Integer;
var
  P: Integer;
  Part: String;
begin
  P := Pos('.', S);
  if P = 0 then begin
    Part := S;
    S := '';
  end else begin
    Part := Copy(S, 1, P - 1);
    S := Copy(S, P + 1, Length(S));
  end;
  Result := StrToIntDef(Part, 0);
end;

function CompareVersions(A, B: String): Integer;
var
  I, X, Y: Integer;
begin
  Result := 0;
  for I := 1 to 4 do begin
    X := ParseVersionPart(A);
    Y := ParseVersionPart(B);
    if X < Y then begin
      Result := -1;
      Exit;
    end;
    if X > Y then begin
      Result := 1;
      Exit;
    end;
  end;
end;

function GameIsRunning(): Boolean;
var
  Code: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\cmd.exe'),
    '/C tasklist /NH /FI "IMAGENAME eq Shooter.exe" | find /I "Shooter.exe" >nul',
    '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
end;

function InitializeSetup(): Boolean;
var
  Installed: String;
begin
  Result := True;
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{' + '{#AppId}' + '}_is1', 'DisplayVersion', Installed) then begin
    if (CompareVersions(Installed, '{#AppVersion}') > 0) and (ExpandConstant('{param:ALLOWDOWNGRADE|0}') <> '1') then begin
      SuppressibleMsgBox('Shooter ' + Installed + ' is already installed and is newer than this installer ({#AppVersion}). ' +
        'Downgrade is refused. Use /ALLOWDOWNGRADE=1 only with a tested compatible rollback pair.', mbError, MB_OK, IDOK);
      Result := False;
      Exit;
    end;
  end;
  { Interactive runs get the Restart Manager close prompt. Silent runs must never kill a running game. }
  if WizardSilent and GameIsRunning() then begin
    Log('Shooter.exe is running; silent setup refuses to close or replace it.');
    SuppressibleMsgBox('Shooter is running. Close the game and run setup again.', mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

procedure RemoveStale(Dir, Rel: String; Keep: TStringList);
var
  FR: TFindRec;
  Name: String;
begin
  if FindFirst(Dir + '\*', FR) then begin
    try
      repeat
        if (FR.Name <> '.') and (FR.Name <> '..') then begin
          Name := Rel + FR.Name;
          if (FR.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then begin
            RemoveStale(Dir + '\' + FR.Name, Name + '\', Keep);
            RemoveDir(Dir + '\' + FR.Name);
          end else if Keep.IndexOf(Lowercase(Name)) < 0 then begin
            Log('Removing stale payload file ' + Dir + '\' + FR.Name);
            DeleteFile(Dir + '\' + FR.Name);
          end;
        end;
      until not FindNext(FR);
    finally
      FindClose(FR);
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Keep: TStringList;
  I: Integer;
begin
  { After every new file landed: drop files of the previous payload that the new release no longer ships. }
  if CurStep = ssPostInstall then begin
    Keep := TStringList.Create;
    try
      Keep.LoadFromFile(ExpandConstant('{app}\payload-files.txt'));
      for I := 0 to Keep.Count - 1 do
        Keep[I] := Lowercase(Trim(Keep[I]));
      RemoveStale(ExpandConstant('{app}\Content'), 'content\', Keep);
      RemoveStale(ExpandConstant('{app}\Shooter_Data'), 'shooter_data\', Keep);
      RemoveStale(ExpandConstant('{app}\D3D12'), 'd3d12\', Keep);
    finally
      Keep.Free;
    end;
  end;
end;

{ Pre-flight for silent runs: before any file is touched, prove every file of the currently installed payload can be
  renamed (i.e. is not locked by a running game). A locked file aborts setup with nothing changed, instead of leaving a
  half-replaced pair. Interactive runs use the Restart Manager close prompt of CloseApplications. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  List: TStringList;
  I: Integer;
  F, Probe, ListPath: String;
begin
  Result := '';
  ListPath := ExpandConstant('{app}\payload-files.txt');
  if (not WizardSilent) or (not FileExists(ListPath)) then
    Exit;
  List := TStringList.Create;
  try
    List.LoadFromFile(ListPath);
    for I := 0 to List.Count - 1 do begin
      F := ExpandConstant('{app}\') + Trim(List[I]);
      if (Trim(List[I]) <> '') and FileExists(F) then begin
        Probe := F + '.lockprobe';
        if not RenameFile(F, Probe) then begin
          Result := 'An installed file is in use: ' + F + '. Close Shooter and run setup again. Nothing was changed.';
          Exit;
        end;
        if not RenameFile(Probe, F) then begin
          Result := 'Could not restore a probed file: ' + F + '. Run this installer again to repair.';
          Exit;
        end;
      end;
    end;
  finally
    List.Free;
  end;
end;
