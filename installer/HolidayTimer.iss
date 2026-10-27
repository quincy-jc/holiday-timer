; One-file, per-user Windows 11 installer. Keep this AppId stable across releases.
#ifndef PayloadDir
  #error PayloadDir must name the verified application build directory.
#endif
#ifndef SetupOutputDir
  #define SetupOutputDir "..\dist\setup"
#endif
#define AppVersion "1.7.1"
#ifdef IsolationTest
  #ifdef PreviousVersion
    #undef AppVersion
    #define AppVersion PreviousVersion
  #endif
  #ifndef TestRoot
    #error IsolationTest requires an explicit TestRoot.
  #endif
  #define InstallerId "HolidayTimer-Installer-Test"
  #define InstallerFile "HolidayTimerSetup-Test"
  #define InstallDirectory TestRoot + "\installed"
  #define PreferencesDirectory TestRoot + "\preferences"
  #define RunKey "Software\HolidayTimer\InstallerTests"
  #define ShortcutPath TestRoot + "\Holiday Timer"
#else
  #define InstallerId "{{90E548BE-4C79-4499-8A77-838B7B0E947C}"
  #define InstallerFile "HolidayTimerSetup"
  #define InstallDirectory "{localappdata}\Programs\HolidayTimer"
  #define PreferencesDirectory "{localappdata}\HolidayTimer"
  #define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"
  #define ShortcutPath "{userprograms}\Holiday Timer"
#endif

[Setup]
AppId={#InstallerId}
AppName=Holiday Timer
AppVersion={#AppVersion}
AppVerName=Holiday Timer
VersionInfoVersion={#AppVersion}
VersionInfoDescription=Holiday Timer Setup
DefaultDirName={#InstallDirectory}
PrivilegesRequired=lowest
UsePreviousAppDir=no
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableWelcomePage=yes
DisableReadyPage=no
DisableFinishedPage=yes
WizardStyle=modern dynamic windows11 hidebevels
WizardImageFile=
WizardSmallImageFile=
MinVersion=10.0.22000
AppMutex={code:GetAppMutex}
SetupMutex=Local\HolidayTimer.Setup
CloseApplications=no
RestartApplications=no
RestartIfNeededByRun=no
AlwaysRestart=no
Uninstallable=yes
CreateUninstallRegKey=yes
UninstallDisplayName=Holiday Timer
UninstallDisplayIcon={app}\HolidayTimer.exe
OutputDir={#SetupOutputDir}
OutputBaseFilename={#InstallerFile}
Compression=lzma2/max
SolidCompression=yes
DiskSpanning=no
TimeStampsInUTC=yes
TouchDate=none

[Files]
Source: "{#PayloadDir}\HolidayTimer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\HolidayTimer.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\Inter-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{#ShortcutPath}"; Filename: "{app}\HolidayTimer.exe"; WorkingDir: "{app}"; AppUserModelID: "HolidayTimer.Desktop"

[Registry]
; The app owns later startup changes. Uninstall removes this value only if it
; still points to this installation; never attach uninsdeletevalue to it.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "HolidayTimer"; ValueData: """{app}\HolidayTimer.exe"" --startup"; Check: IsFreshCountdown

#ifndef IsolationTest
[Run]
Filename: "{app}\HolidayTimer.exe"; WorkingDir: "{app}"; Flags: nowait skipifsilent
Filename: "{app}\HolidayTimer.exe"; WorkingDir: "{app}"; Flags: nowait skipifnotsilent; Check: IsAutoUpdate
#endif

[Messages]
ReadyLabel1=Your countdown, ready for your desktop.
ReadyLabel2a=Click Install to set up Holiday Timer for your Windows account.
ReadyLabel2b=Click Install to set up Holiday Timer for your Windows account.
SetupAppRunningError=Holiday Timer is still running.%n%nSave any settings you want to keep, then right-click its tray icon and choose Exit Holiday Timer.%n%nClick OK when it has closed, or Cancel to leave Setup.
UninstallAppRunningError=Holiday Timer is still running.%n%nSave any settings you want to keep, then right-click its tray icon and choose Exit Holiday Timer.%n%nClick OK when it has closed, or Cancel to leave Uninstall.
ConfirmUninstall=Remove Holiday Timer and its sign-in entry?%n%nYour saved countdown and appearance preferences will be kept.

[Code]
#include "CurrentUser.iss"

function IsAutoUpdate(): Boolean;
var I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), '/AUTOUPDATE') = 0 then Result := True;
end;

function ActualAppMutex(): String;
begin
#ifdef IsolationTest
  Result := 'Local\HolidayTimer.SetupTest.Instance';
#else
  Result := 'Local\HolidayTimer.Instance.' + GetCurrentUserSid();
#endif
end;

function GetAppMutex(Param: String): String;
begin
  if IsAutoUpdate() and not IsUninstaller() then Result := ''
  else Result := ActualAppMutex();
end;

function IsFreshCountdown(): Boolean;
var
  Preferences: String;
begin
  Preferences := ExpandConstant('{#PreferencesDirectory}');
  Result := not FileExists(Preferences + '\settings.json') and
    not FileExists(Preferences + '\settings.json.bak');
end;

function InitializeSetup(): Boolean;
begin
  Result := IsDotNetInstalled(net48, 0);
  if not Result then
    SuppressibleMsgBox('Holiday Timer needs .NET Framework 4.8 or newer. Install it through Windows Update, then open Setup again.',
      mbCriticalError, MB_OK, IDOK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Attempts: Integer;
begin
  Result := '';
  if IsAutoUpdate() then begin
    Attempts := 0;
    while CheckForMutexes(ActualAppMutex()) and (Attempts < 120) do begin
      Sleep(250);
      Attempts := Attempts + 1;
    end;
  end;
  if CheckForMutexes(ActualAppMutex()) then
    Result := 'Holiday Timer opened while Setup was running. Save your settings, choose Exit Holiday Timer from its tray menu, then try again.';
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo,
  MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := 'An animated orb. A live countdown. Right on your desktop.' + NewLine + NewLine +
    'Holiday Timer opens as soon as installation finishes.' + NewLine +
    'Choose your holiday, then save your countdown.' + NewLine + NewLine;
  if IsFreshCountdown() then
    Result := Result + 'Launches when you sign in. You can change this in the app.' + NewLine
  else
    Result := Result + 'Your existing countdown and sign-in preference are kept.' + NewLine;
  Result := Result + 'Remove it any time in Windows Settings > Apps > Installed apps.' + NewLine + NewLine +
    'Install location:' + NewLine + WizardDirValue();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command, OwnedCommand: String;
begin
  if CurUninstallStep = usUninstall then begin
    OwnedCommand := '"' + ExpandConstant('{app}\HolidayTimer.exe') + '" --startup';
    if RegQueryStringValue(HKCU, '{#RunKey}', 'HolidayTimer', Command) and
      (CompareText(Trim(Command), OwnedCommand) = 0) then begin
      if not RegDeleteValue(HKCU, '{#RunKey}', 'HolidayTimer') then
        Log('The sign-in entry could not be removed.');
    end;
  end;
end;
