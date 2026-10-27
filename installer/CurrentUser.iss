{ Current account identity for Inno Setup 6.7.x's 32-bit setup/uninstall
  processes. DWORD-sized addresses are intentional; 64-bit install mode does
  not change the setup process ABI. No shell, WMI, elevation or files needed. }

function UserGetCurrentProcess: THandle;
  external 'GetCurrentProcess@kernel32.dll stdcall';
function UserOpenProcessToken(ProcessHandle: THandle; DesiredAccess: Cardinal;
  var TokenHandle: THandle): BOOL;
  external 'OpenProcessToken@advapi32.dll stdcall';
function UserGetTokenInformation(TokenHandle: THandle; InformationClass: Cardinal;
  Information: LongWord; InformationBytes: Cardinal; var RequiredBytes: Cardinal): BOOL;
  external 'GetTokenInformation@advapi32.dll stdcall';
function UserLocalAlloc(Flags: Cardinal; Bytes: LongWord): LongWord;
  external 'LocalAlloc@kernel32.dll stdcall';
function UserLocalFree(Memory: LongWord): LongWord;
  external 'LocalFree@kernel32.dll stdcall';
function UserCloseHandle(Handle: THandle): BOOL;
  external 'CloseHandle@kernel32.dll stdcall';
procedure UserCopyPointer(var Destination: LongWord; Source: LongWord; Bytes: LongWord);
  external 'RtlMoveMemory@kernel32.dll stdcall';
function UserConvertSidToStringSid(Sid: LongWord; var StringSid: LongWord): BOOL;
  external 'ConvertSidToStringSidW@advapi32.dll stdcall';
function UserStringLength(Value: LongWord): Integer;
  external 'lstrlenW@kernel32.dll stdcall';
function UserCopyString(Destination: String; Source: LongWord; Count: Integer): LongWord;
  external 'lstrcpynW@kernel32.dll stdcall';

function GetCurrentUserSid: String;
var
  TokenHandle: THandle;
  RequiredBytes: Cardinal;
  Buffer, Sid, StringSid: LongWord;
  StringCharacters: Integer;
begin
  Result := '';
  TokenHandle := 0;
  { TOKEN_QUERY reads only our own process token. }
  if not UserOpenProcessToken(UserGetCurrentProcess(), $0008, TokenHandle) then
    RaiseException('Could not read the current Windows account. Please try again.');
  try
    RequiredBytes := 0;
    { TokenUser = 1. The sizing call intentionally supplies a null buffer. }
    UserGetTokenInformation(TokenHandle, 1, 0, 0, RequiredBytes);
    if (RequiredBytes = 0) or (RequiredBytes > 4096) then
      RaiseException('Windows returned an invalid account token size.');
    Buffer := UserLocalAlloc(0, RequiredBytes);
    if Buffer = 0 then RaiseException('Could not allocate the Windows account token.');
    try
      if not UserGetTokenInformation(TokenHandle, 1, Buffer, RequiredBytes, RequiredBytes) then
        RaiseException('Could not read the current Windows account token.');
      Sid := 0;
      { The first member of TOKEN_USER is the 32-bit SID pointer. Its target
        remains valid inside the allocated token buffer until conversion. }
      UserCopyPointer(Sid, Buffer, 4);
      StringSid := 0;
      if not UserConvertSidToStringSid(Sid, StringSid) then
        RaiseException('Could not identify the current Windows account.');
      try
        StringCharacters := UserStringLength(StringSid);
        if (StringCharacters < 5) or (StringCharacters > 184) then
          RaiseException('Windows returned an invalid account identifier.');
        Result := StringOfChar(#0, StringCharacters + 1);
        UserCopyString(Result, StringSid, StringCharacters + 1);
        SetLength(Result, StringCharacters);
        if Pos('S-1-', Result) <> 1 then
          RaiseException('Windows returned an invalid account identifier.');
      finally
        UserLocalFree(StringSid);
      end;
    finally
      UserLocalFree(Buffer);
    end;
  finally
    UserCloseHandle(TokenHandle);
  end;
end;
