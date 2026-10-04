Var TestDebugPrivilegeStatus
Var TestDebugPrivilegeFailed

; Elevated PowerShell/.NET can enable SeDebugPrivilege, which child processes
; inherit. Disable it in this test harness only so fixture DACLs are enforced.
; The production installer retains its normal permissions.
Function DisableTestDebugPrivilege
  StrCpy $TestDebugPrivilegeFailed 1
  System::Call 'kernel32::GetCurrentProcess() p .r0'
  System::Call 'advapi32::OpenProcessToken(p r0, i 0x20, *p .r1) i .r2 ?e'
  Pop $9
  ${If} $2 == 0
    StrCpy $TestDebugPrivilegeStatus "Cannot open harness token, Win32 error=$9"
    Return
  ${EndIf}
  ; TOKEN_PRIVILEGES with one LUID_AND_ATTRIBUTES: four DWORDs, on x86/x64.
  System::Alloc 16
  Pop $3
  ${If} $3 == 0
    StrCpy $TestDebugPrivilegeStatus "Cannot allocate harness privilege state."
  ${Else}
    System::Call '*$3(i 1, i 0, i 0, i 0)'
    IntPtrOp $4 $3 + 4
    System::Call 'advapi32::LookupPrivilegeValueW(p 0, w "SeDebugPrivilege", p r4) i .r2 ?e'
    Pop $9
    ${If} $2 == 0
      StrCpy $TestDebugPrivilegeStatus "Cannot resolve SeDebugPrivilege, Win32 error=$9"
    ${Else}
      ; Attributes=0 disables the privilege. ERROR_NOT_ALL_ASSIGNED (1300)
      ; means it was absent, which is also suitable for these permission tests.
      System::Call 'advapi32::AdjustTokenPrivileges(p r1, i 0, p r3, i 0, p 0, p 0) i .r2 ?e'
      Pop $9
      StrCpy $TestDebugPrivilegeStatus "Disable SeDebugPrivilege: result=$2, Win32 error=$9"
      ${If} $2 != 0
      ${AndIf} $9 == 0
        StrCpy $TestDebugPrivilegeFailed 0
      ${ElseIf} $2 != 0
      ${AndIf} $9 == 1300
        StrCpy $TestDebugPrivilegeFailed 0
      ${EndIf}
    ${EndIf}
    System::Free $3
  ${EndIf}
  System::Call 'kernel32::CloseHandle(p r1)'
FunctionEnd
