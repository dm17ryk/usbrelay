!include "FileFunc.nsh"
!include "${__FILEDIR__}\RunningProcesses.nsh"

; Reusable by the installer regression harness with an isolated HKCU key.
!ifndef INSTALL_REG_ROOT
  !define INSTALL_REG_ROOT HKLM
!endif

Function ResolveInstallDirectory
  System::Call 'kernel32::GetCurrentProcessId() i .r0'
  StrCpy $InstallerLogPath "$TEMP\usbrelay-installer-$0.log"
  FileOpen $0 "$InstallerLogPath" w
  FileWriteUTF16LE /BOM $0 "USB Relay installer diagnostics$\r$\n"
  FileClose $0
  ; NSIS removes /D= from $CMDLINE, but has already applied it to $INSTDIR.
  System::Call 'kernel32::GetCommandLineW() w .r0'
  ${GetOptions} $0 "/D=" $1
  ${IfNot} ${Errors}
    !insertmacro InstallerLog "[InstallDirectory] Explicit /D= selected: $INSTDIR"
    Return
  ${EndIf}

  StrCpy $ProcessScanMode "discover"
  Call ScanRunningApplications
  ${If} $RunningInstallDirectory != ""
  ${AndIf} $RunningInstallAmbiguous == 0
  ${AndIf} $ProcessStopFailed == 0
    StrCpy $INSTDIR "$RunningInstallDirectory"
    !insertmacro InstallerLog "[InstallDirectory] Running application overrides registry location: $INSTDIR"
    Return
  ${EndIf}

  ${If} ${RunningX64}
    SetRegView 64
    ReadRegStr $0 ${INSTALL_REG_ROOT} "${UNINSTALL_KEY}" "InstallLocation"
    !insertmacro InstallerLog "[InstallDirectory] 64-bit registry location: $0"
    ${If} $0 == ""
      SetRegView 32
      ReadRegStr $0 ${INSTALL_REG_ROOT} "${UNINSTALL_KEY}" "InstallLocation"
      !insertmacro InstallerLog "[InstallDirectory] 32-bit registry fallback: $0"
    ${EndIf}
    SetRegView 64
    ${If} $0 == ""
      StrCpy $0 "$PROGRAMFILES64\usbrelay"
      !insertmacro InstallerLog "[InstallDirectory] Fresh install default: $0"
    ${EndIf}
  ${Else}
    SetRegView 32
    ReadRegStr $0 ${INSTALL_REG_ROOT} "${UNINSTALL_KEY}" "InstallLocation"
    !insertmacro InstallerLog "[InstallDirectory] 32-bit registry location: $0"
    ${If} $0 == ""
      StrCpy $0 "$PROGRAMFILES\usbrelay"
      !insertmacro InstallerLog "[InstallDirectory] Fresh install default: $0"
    ${EndIf}
  ${EndIf}
  StrCpy $INSTDIR $0
  !insertmacro InstallerLog "[InstallDirectory] Resolved installation folder: $INSTDIR"
FunctionEnd
