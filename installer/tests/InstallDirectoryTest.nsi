Unicode true
RequestExecutionLevel user
SilentInstall silent
!include "LogicLib.nsh"
!include "x64.nsh"
!ifndef APP_EXE
  !define APP_EXE "usbrelay-directory-test-child.exe"
!endif
!ifndef INSTALL_REG_ROOT
  !define INSTALL_REG_ROOT HKCU
!endif
!ifndef UNINSTALL_KEY
  !define UNINSTALL_KEY "Software\Classes\CLSID\{ECD27471-CC17-4ADC-B2F6-A44F78A21B73}"
!endif
!include "..\InstallDirectory.nsh"
!include "TestPrivileges.nsh"
Name "USB Relay installer directory regression"
OutFile "${TEST_OUTPUT}\directory-test.exe"
InstallDir "$PROGRAMFILES\usbrelay"

Function .onInit
  Call DisableTestDebugPrivilege
  Call ResolveInstallDirectory
  !insertmacro InstallerLog "[Harness] $TestDebugPrivilegeStatus"
  ${If} $TestDebugPrivilegeFailed != 0
    SetErrorLevel 2
    Quit
  ${EndIf}
FunctionEnd

Section
  ${GetParameters} $0
  ${GetOptions} $0 "/STOP" $1
  ${IfNot} ${Errors}
    Call StopInstallationProcesses
    ${If} ${Errors}
      SetErrorLevel 1
      Quit
    ${EndIf}
  ${EndIf}
  FileOpen $0 "${TEST_OUTPUT}\resolved.txt" w
  FileWriteUTF16LE /BOM $0 $INSTDIR
  FileClose $0
SectionEnd
