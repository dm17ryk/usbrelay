Unicode true
RequestExecutionLevel user
SilentInstall silent
!include "LogicLib.nsh"
!include "x64.nsh"
!define INSTALL_REG_ROOT HKCU
!define UNINSTALL_KEY "Software\Classes\CLSID\{ECD27471-CC17-4ADC-B2F6-A44F78A21B73}"
!include "..\InstallDirectory.nsh"
Name "USB Relay installer directory regression"
OutFile "${TEST_OUTPUT}\directory-test.exe"
InstallDir "$PROGRAMFILES\usbrelay"

Function .onInit
  Call ResolveInstallDirectory
FunctionEnd

Section
  FileOpen $0 "${TEST_OUTPUT}\resolved.txt" w
  FileWriteUTF16LE /BOM $0 $INSTDIR
  FileClose $0
SectionEnd
