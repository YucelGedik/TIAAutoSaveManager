
[Setup]
AppName=Installer rule tests
AppVersion=1
DefaultDirName={tmp}\Unused-Installer-Tests
PrivilegesRequired=lowest
Uninstallable=no
CreateAppDir=no
OutputDir={#OutputDirectory}
OutputBaseFilename=InstallerRuleTests

[Code]
#include "PrerequisiteRules.iss"
#include "PrerequisiteDetection.iss"
var
  TestCount: Integer;

procedure Check(const Condition: Boolean; const Name: String);
begin
  if not Condition then RaiseException('FAILED: ' + Name);
  TestCount := TestCount + 1;
end;

function InitializeSetup: Boolean;
var
  Report: String;
  Passed: Boolean;
  Apps, Hosts: TProcessIds;
  InventoryError: String;
  TargetPid: Cardinal;
  Tick: Integer;
  Entry: TNativeProcessEntry;
begin
  Result := False; // The test executable never installs anything.
  Passed := False;
  try
    Check(DesktopVersionSatisfies('8.0.{#DesktopMinPatch}'), 'bundled runtime accepted');
    Check(DesktopVersionSatisfies('8.0.999'), 'newer patch accepted');
    Check(not DesktopVersionSatisfies('8.0.0'), 'older patch rejected');
    Check(not DesktopVersionSatisfies('9.0.0'), 'only .NET 9 is insufficient');
    Check(not DesktopVersionSatisfies('10.0.0'), 'only .NET 10 is insufficient');
    Check(not DesktopVersionSatisfies('8.0.99-preview.1'), 'preview rejected');
    Check(not DesktopVersionSatisfies('8.0.bad'), 'invalid patch rejected');
    Check(not DesktopVersionSatisfies('8.0.-1'), 'negative patch rejected');
    Check(not DesktopVersionSatisfies(''), 'missing runtime rejected');
    Check(not FrameworkReleaseSatisfies(528039), 'older framework rejected');
    Check(FrameworkReleaseSatisfies(528040), 'framework 4.8 accepted');
    Check(FrameworkReleaseSatisfies(533320), 'framework 4.8.1 accepted');
    Check(IsRestartExitCode(3010), 'reboot required handled');
    Check(IsRestartExitCode(1641), 'reboot initiated handled');
    Check(not IsRestartExitCode(1603), 'failure not reboot');
    Check(PrerequisiteSucceeded(0, True), 'verified install succeeds');
    Check(not PrerequisiteSucceeded(0, False), 'success exit without detection fails');
    Check(PrerequisiteSucceeded(1638, True), 'already installed needs detection');
    Check(not PrerequisiteSucceeded(1638, False), 'unconfirmed already installed fails');
    Check(not PrerequisiteSucceeded(1603, True), 'failure code rejected');
    Check(not PrerequisiteSucceeded(3010, True), 'restart cannot be treated as completed');
    Check(SizeOf(Entry) = 556, '32-bit native process record size');
    Check(ReadManagedProcesses(Apps, Hosts, InventoryError), 'native process inventory works: ' + InventoryError);
    Check(ManagedProcessRole('TIAAutoSaveManager.App.exe') = 1, 'Manager identified');
    Check(ManagedProcessRole('tiaautosavemanager.opennesshost.exe') = 2, 'host identified case-insensitively');
    Check(ManagedProcessRole('Siemens.Automation.Portal.exe') = 0, 'TIA Portal is never a close target');
    Check(ManagedProcessRole('unrelated.exe') = 0, 'unrelated processes ignored');
    Check(HasFramework = IsDotNetInstalled(net48, 0), 'framework detection agrees with built-in detection');
    Check(HasDesktopRuntime = (HasDesktopRuntimeInView(HKLM32) or HasDesktopRuntimeInView(HKLM64)), 'both runtime registry views are checked');
    TargetPid := StrToIntDef(ExpandConstant('{param:CloseTargetPid|0}'), 0);
    if TargetPid = 0 then RaiseException('Missing isolated close-test target.');
    Check(RequestNormalClose(TargetPid), 'normal close reaches an invisible test window');
    for Tick := 0 to 19 do begin
      if FileExists(ExpandConstant('{param:CloseMarker}')) then Break;
      if Tick mod 4 = 0 then RequestNormalClose(TargetPid);
      Sleep(250);
    end;
    Check(FileExists(ExpandConstant('{param:CloseMarker}')), 'busy test target finishes and exits through normal close');
    Passed := True;
    Report := 'PASSED: ' + IntToStr(TestCount) + ' checks';
  except
    Report := GetExceptionMessage;
  end;
  SaveStringToFile(ExpandConstant('{param:Report|installer-rule-tests.txt}'), Report, False);
  if not Passed then Log(Report);
end;
