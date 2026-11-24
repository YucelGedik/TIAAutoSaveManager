// The Inno 6 setup engine is a 32-bit process, including x64 installation mode.
type
  TProcessIds = array of Cardinal;
  TNativeProcessEntry = record
    Size, Usage, ProcessId, HeapId, ModuleId, Threads, ParentId: Cardinal;
    BasePriority: Integer;
    Flags: Cardinal;
    ExeName: array[0..259] of Char;
  end;

function ProcessSnapshot(Flags, ProcessId: Cardinal): THandle;
  external 'CreateToolhelp32Snapshot@kernel32.dll stdcall';
function FirstProcess(Snapshot: THandle; var Entry: TNativeProcessEntry): Boolean;
  external 'Process32FirstW@kernel32.dll stdcall';
function NextProcess(Snapshot: THandle; var Entry: TNativeProcessEntry): Boolean;
  external 'Process32NextW@kernel32.dll stdcall';
function ReleaseSnapshot(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';
function TopWindow(Parent: HWND): HWND;
  external 'GetTopWindow@user32.dll stdcall';
function RelatedWindow(Window: HWND; Command: Cardinal): HWND;
  external 'GetWindow@user32.dll stdcall';
function WindowProcess(Window: HWND; var ProcessId: Cardinal): Cardinal;
  external 'GetWindowThreadProcessId@user32.dll stdcall';
function WindowTitleLength(Window: HWND): Integer;
  external 'GetWindowTextLengthW@user32.dll stdcall';
function ReadWindowTitle(Window: HWND; Buffer: String; MaxCount: Integer): Integer;
  external 'GetWindowTextW@user32.dll stdcall';
function PostWindowClose(Window: HWND; Message: Cardinal; WParam, LParam: Integer): Boolean;
  external 'PostMessageW@user32.dll stdcall';
function NativeCurrentProcessId: Cardinal;
  external 'GetCurrentProcessId@kernel32.dll stdcall';
function ProcessSession(ProcessId: Cardinal; var SessionId: Cardinal): Boolean;
  external 'ProcessIdToSessionId@kernel32.dll stdcall';

function ManagedProcessRole(const Name: String): Integer;
begin
  Result := 0;
  if CompareText(Name, 'TIAAutoSaveManager.App.exe') = 0 then Result := 1;
  if CompareText(Name, 'TIAAutoSaveManager.OpennessHost.exe') = 0 then Result := 2;
end;

procedure AddProcess(var Ids: TProcessIds; const ProcessId: Cardinal);
var Count: Integer;
begin
  Count := GetArrayLength(Ids); SetArrayLength(Ids, Count + 1); Ids[Count] := ProcessId;
end;

function ReadManagedProcesses(var Apps, Hosts: TProcessIds; var Error: String): Boolean;
var
  Snapshot: THandle;
  Entry: TNativeProcessEntry;
  Name: String;
  I, Role: Integer;
begin
  Result := False; Error := ''; SetArrayLength(Apps, 0); SetArrayLength(Hosts, 0);
  Snapshot := ProcessSnapshot(2, 0);
  if Snapshot = THandle(-1) then begin Error := 'Windows süreç listesi okunamadı: ' + SysErrorMessage(DLLGetLastError); Exit; end;
  try
    Entry.Size := SizeOf(Entry);
    if not FirstProcess(Snapshot, Entry) then begin Error := 'Windows süreç bilgisi okunamadı: ' + SysErrorMessage(DLLGetLastError); Exit; end;
    repeat
      Name := '';
      for I := 0 to 259 do begin
        if Entry.ExeName[I] = #0 then Break;
        Name := Name + Entry.ExeName[I];
      end;
      Role := ManagedProcessRole(Name);
      if Role = 1 then AddProcess(Apps, Entry.ProcessId);
      if Role = 2 then AddProcess(Hosts, Entry.ProcessId);
    until not NextProcess(Snapshot, Entry);
    if DLLGetLastError <> 18 then begin Error := 'Windows süreç listesi tamamlanamadı: ' + SysErrorMessage(DLLGetLastError); Exit; end;
    Result := True;
  finally
    ReleaseSnapshot(Snapshot);
  end;
end;

function ProcessDetails(const Apps, Hosts: TProcessIds): String;
var I: Integer;
begin
  Result := '';
  for I := 0 to GetArrayLength(Apps) - 1 do Result := Result + 'Manager PID ' + IntToStr(Apps[I]) + ' ';
  for I := 0 to GetArrayLength(Hosts) - 1 do Result := Result + 'Host PID ' + IntToStr(Hosts[I]) + ' ';
end;

function RequestNormalClose(const ProcessId: Cardinal): Boolean;
var
  Window, NextWindow: HWND;
  OwnerProcess, TargetSession, OwnSession: Cardinal;
  Title: String;
  Length: Integer;
begin
  Result := False;
  // Never send messages into another logged-in user's Windows session.
  if not ProcessSession(ProcessId, TargetSession) or not ProcessSession(NativeCurrentProcessId, OwnSession) then Exit;
  if TargetSession <> OwnSession then Exit;
  Window := TopWindow(0);
  while Window <> 0 do begin
    NextWindow := RelatedWindow(Window, 2);
    OwnerProcess := 0; WindowProcess(Window, OwnerProcess);
    if OwnerProcess = ProcessId then begin
      Length := WindowTitleLength(Window);
      if Length > 0 then begin
        SetLength(Title, Length + 1);
        Length := ReadWindowTitle(Window, Title, Length + 1);
        SetLength(Title, Length);
        // Include owned startup/duplicate-instance dialogs, not IME or message-only windows.
        if (Pos('TIA Portal Auto Save Manager', Title) = 1) or
           (Pos('Uygulama zaten açık', Title) = 1) or (Pos('Uygulama başlatılamadı', Title) = 1) or
           (Pos('Çalışan Manager', Title) = 1) then
          if PostWindowClose(Window, $0010, 0, 0) then Result := True;
      end;
    end;
    Window := NextWindow;
  end;
end;

function EnsureManagerClosed: String;
var
  Apps, Hosts: TProcessIds;
  Error: String;
  Tick, I: Integer;
begin
  Result := '';
  if not ReadManagedProcesses(Apps, Hosts, Error) then begin Result := Error; Exit; end;
  if GetArrayLength(Apps) + GetArrayLength(Hosts) = 0 then Exit;
  if SuppressibleMsgBox('Manager açık veya bağlantı süreci henüz bitmedi.' + #13#10 +
    'Manager kapatılsın mı?' + #13#10#13#10 +
    'Evet: Normal kapanma isteği gönderilir ve bağlantıların bitmesi beklenir. Devam eden kayıt/yedekleme zorla kesilmez. TIA Portal açık kalır.',
    mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) <> IDYES then begin
    Result := 'Manager kapatılmadı; kurulum için dosyalar değiştirilmedi. Hazır olduğunuzda yeniden deneyin.';
    Exit;
  end;
  for Tick := 0 to 59 do begin
    if not ReadManagedProcesses(Apps, Hosts, Error) then begin Result := Error; Exit; end;
    if GetArrayLength(Apps) + GetArrayLength(Hosts) = 0 then Exit;
    // Retry normal close every two seconds for an older Manager that declines while busy.
    if Tick mod 4 = 0 then
      for I := 0 to GetArrayLength(Apps) - 1 do RequestNormalClose(Apps[I]);
    if not IsUninstaller then begin
      WizardForm.StatusLabel.Caption := 'Manager güvenli şekilde kapatılıyor; TIA işleminin ve bağlantıların bitmesi bekleniyor...';
      WizardForm.Refresh;
    end;
    Sleep(500);
  end;
  // Hosts are never killed or sent WM_CLOSE. Wait for their IPC/native operations to finish.
  if not ReadManagedProcesses(Apps, Hosts, Error) then begin Result := Error; Exit; end;
  if GetArrayLength(Apps) + GetArrayLength(Hosts) > 0 then
    Result := 'Manager/host henüz kapanmadı. TIA işleminin bitmesini bekleyip kurulumu yeniden başlatın. Başka Windows oturumundaki Manager o oturumdan kapatılmalıdır. ' + ProcessDetails(Apps, Hosts);
end;
