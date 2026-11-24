// Pure rules shared by the setup and the executable test harness.
function DesktopVersionSatisfies(const Version: String): Boolean;
var
  Parts: TArrayOfString;
  I, Number: Integer;
begin
  Result := False;
  Parts := StringSplit(Version, ['.'], stAll);
  if GetArrayLength(Parts) <> 3 then Exit;
  for I := 0 to 2 do begin
    Number := StrToIntDef(Parts[I], -1);
    if (Number < 0) or (IntToStr(Number) <> Parts[I]) then Exit;
  end;
  Result := (Parts[0] = '8') and (Parts[1] = '0') and
    (StrToIntDef(Parts[2], -1) >= {#DesktopMinPatch});
end;

function FrameworkReleaseSatisfies(const Release: Cardinal): Boolean;
begin
  Result := Release >= 528040;
end;

function IsRestartExitCode(const ExitCode: Integer): Boolean;
begin
  Result := (ExitCode = 3010) or (ExitCode = 1641);
end;

function PrerequisiteSucceeded(const ExitCode: Integer; const DetectedAfter: Boolean): Boolean;
begin
  // 1638 (another version is installed) is accepted ONLY if detection confirms it.
  Result := ((ExitCode = 0) or (ExitCode = 1638)) and DetectedAfter;
end;
