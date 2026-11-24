#ifndef AppVersion
  #error AppVersion must be supplied by build-installer.ps1
#endif
#ifndef IncludePrerequisites
  #define IncludePrerequisites 1
#endif
#ifndef PackageKind
  #define PackageKind "Setup"
#endif

[Setup]
AppId={{2E9AC8D0-4D0F-4BCA-A507-35B8DD131C52}
AppName=TIA Portal Auto Save Manager
AppVersion={#AppVersion}
AppVerName=TIA Portal Auto Save Manager {#AppVersion}
DefaultDirName={autopf}\TIA Auto Save Manager
DefaultGroupName=TIA Auto Save Manager
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDirectory}
OutputBaseFilename=TIAAutoSaveManager-v{#AppVersion}-{#PackageKind}-win-x64
SetupIconFile=..\src\TIAAutoSaveManager.App\Assets\App.ico
UninstallDisplayIcon={app}\TIAAutoSaveManager.App.exe
WizardStyle=modern dark polar
WizardSmallImageFile=..\src\TIAAutoSaveManager.App\Assets\Logo.png
WizardImageFile=..\src\TIAAutoSaveManager.App\Assets\Logo.png
WizardImageStretch=yes
#if Int(IncludePrerequisites)
InfoBeforeFile=SetupInfo.txt
#else
InfoBeforeFile=UpdateInfo.txt
#endif
Compression=lzma2
SolidCompression=yes
CloseApplications=no
RestartApplications=no
UninstallDisplayName=TIA Portal Auto Save Manager
SetupLogging=yes

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaüstü kısayolu oluştur"; GroupDescription: "Kısayollar:"

[Files]
Source: "{#PayloadDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
#if Int(IncludePrerequisites)
Source: "{#CacheDirectory}\windowsdesktop-runtime-{#DesktopVersion}-win-x64.exe"; Flags: dontcopy noencryption solidbreak
Source: "{#CacheDirectory}\ndp48-x86-x64-allos-enu.exe"; Flags: dontcopy noencryption solidbreak
#endif

[Icons]
Name: "{autoprograms}\TIA Auto Save Manager"; Filename: "{app}\TIAAutoSaveManager.App.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\TIA Auto Save Manager"; Filename: "{app}\TIAAutoSaveManager.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\TIAAutoSaveManager.App.exe"; Description: "TIA Auto Save Manager'ı aç"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
#include "PrerequisiteRules.iss"
#include "PrerequisiteDetection.iss"

procedure InitializeWizard;
begin
  // Keep the supplied square logo square in the tall wizard image areas.
  WizardForm.WizardBitmapImage.Height := WizardForm.WizardBitmapImage.Width;
  WizardForm.WizardBitmapImage.Top := (WizardForm.WelcomePage.Height - WizardForm.WizardBitmapImage.Height) div 2;
  WizardForm.WizardBitmapImage2.Height := WizardForm.WizardBitmapImage2.Width;
  WizardForm.WizardBitmapImage2.Top := (WizardForm.FinishedPage.Height - WizardForm.WizardBitmapImage2.Height) div 2;
end;

function InstallPrerequisite(const FileName, DisplayName, ExpectedHash: String;
  const Desktop: Boolean; var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
  Installed: Boolean;
  InstallerPath: String;
begin
  Result := '';
  WizardForm.StatusLabel.Caption := DisplayName + ' kuruluyor. Lütfen bekleyin...';
  ExtractTemporaryFile(FileName);
  InstallerPath := ExpandConstant('{tmp}\') + FileName;
  if CompareText(GetSHA256OfFile(InstallerPath), ExpectedHash) <> 0 then begin
    Result := DisplayName + ' dosyası doğrulanamadı. Kurulum durduruldu.';
    Exit;
  end;
  if not Exec(InstallerPath, '/quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then begin
    Result := DisplayName + ' kurulumu başlatılamadı: ' + SysErrorMessage(ExitCode);
    Exit;
  end;
  Log(DisplayName + ' exit code: ' + IntToStr(ExitCode));
  if IsRestartExitCode(ExitCode) then begin
    NeedsRestart := True;
    Result := DisplayName + ' için Windows yeniden başlatılmalı. Yeniden başlattıktan sonra Setup dosyasını tekrar çalıştırın.';
    Exit;
  end;
  if Desktop then Installed := HasDesktopRuntime else Installed := HasFramework;
  if not PrerequisiteSucceeded(ExitCode, Installed) then
    Result := DisplayName + ' kurulumu doğrulanamadı (kod: ' + IntToStr(ExitCode) + '). Windows kurulum günlüğünü kontrol edip tekrar deneyin.';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := EnsureManagerClosed;
  if Result <> '' then Exit;
#if Int(IncludePrerequisites)
  if not HasFramework then begin
    Result := InstallPrerequisite('ndp48-x86-x64-allos-enu.exe', '.NET Framework 4.8', '{#FrameworkSHA256}', False, NeedsRestart);
    if Result <> '' then Exit;
  end;
  if not HasDesktopRuntime then
    Result := InstallPrerequisite('windowsdesktop-runtime-{#DesktopVersion}-win-x64.exe', '.NET 8 Windows Desktop Runtime x64', '{#DesktopSHA256}', True, NeedsRestart);
#else
  if not HasFramework or not HasDesktopRuntime then
    Result := 'Bu küçük güncelleme paketi için .NET Framework 4.8 ve .NET 8 Desktop Runtime x64 ({#DesktopVersion} veya daha yeni 8.0 yaması) kurulu olmalıdır. Eksik bileşenlerin otomatik kurulması için tam Setup paketini çalıştırın.';
#endif
end;

function InitializeUninstall: Boolean;
var Error: String;
begin
  Error := EnsureManagerClosed;
  Result := Error = '';
  if not Result then SuppressibleMsgBox(Error, mbError, MB_OK, IDOK);
end;
