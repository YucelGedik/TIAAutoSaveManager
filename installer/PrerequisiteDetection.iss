function HasDesktopRuntimeInView(const RootKey: Integer): Boolean;
var
  Versions: TArrayOfString;
  I: Integer;
begin
  Result := False;
  // Architecture is x64 in both views; x86 runtimes never satisfy this check.
  if RegGetValueNames(RootKey, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Versions) then
    for I := 0 to GetArrayLength(Versions) - 1 do
      if DesktopVersionSatisfies(Versions[I]) then begin
        Result := True;
        Exit;
      end;
end;

function HasDesktopRuntime: Boolean;
begin
  Result := HasDesktopRuntimeInView(HKLM32) or HasDesktopRuntimeInView(HKLM64);
end;

function HasFramework: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release);
  if Result then Result := FrameworkReleaseSatisfies(Release);
end;

#include "InstallerProcesses.iss"
