!include "FileFunc.nsh"

; Reusable by the installer regression harness with an isolated HKCU key.
!ifndef INSTALL_REG_ROOT
  !define INSTALL_REG_ROOT HKLM
!endif

Function ResolveInstallDirectory
  ; NSIS removes /D= from $CMDLINE, but has already applied it to $INSTDIR.
  System::Call 'kernel32::GetCommandLineW() w .r0'
  ${GetOptions} $0 "/D=" $1
  ${IfNot} ${Errors}
    DetailPrint "[InstallDirectory] Explicit /D= selected: $INSTDIR"
    Return
  ${EndIf}

  ${If} ${RunningX64}
    SetRegView 64
    ReadRegStr $0 ${INSTALL_REG_ROOT} "${UNINSTALL_KEY}" "InstallLocation"
    DetailPrint "[InstallDirectory] 64-bit registry location: $0"
    ${If} $0 == ""
      SetRegView 32
      ReadRegStr $0 ${INSTALL_REG_ROOT} "${UNINSTALL_KEY}" "InstallLocation"
      DetailPrint "[InstallDirectory] 32-bit registry fallback: $0"
    ${EndIf}
    SetRegView 64
    ${If} $0 == ""
      StrCpy $0 "$PROGRAMFILES64\usbrelay"
      DetailPrint "[InstallDirectory] Fresh install default: $0"
    ${EndIf}
  ${Else}
    SetRegView 32
    ReadRegStr $0 ${INSTALL_REG_ROOT} "${UNINSTALL_KEY}" "InstallLocation"
    DetailPrint "[InstallDirectory] 32-bit registry location: $0"
    ${If} $0 == ""
      StrCpy $0 "$PROGRAMFILES\usbrelay"
      DetailPrint "[InstallDirectory] Fresh install default: $0"
    ${EndIf}
  ${EndIf}
  StrCpy $INSTDIR $0
  DetailPrint "[InstallDirectory] Resolved installation folder: $INSTDIR"
FunctionEnd
