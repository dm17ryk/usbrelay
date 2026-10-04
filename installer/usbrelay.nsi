Unicode true
RequestExecutionLevel admin

!include "MUI2.nsh"
!include "x64.nsh"

!ifndef VERSION
  !error "VERSION is required. Pass /DVERSION=<version>."
!endif

!ifndef APP_OUTPUT_DIR
  !error "APP_OUTPUT_DIR is required. Pass /DAPP_OUTPUT_DIR=<path>."
!endif

!ifndef OUTPUT_DIR
  !error "OUTPUT_DIR is required. Pass /DOUTPUT_DIR=<path>."
!endif

!ifndef REPO_ROOT
  !error "REPO_ROOT is required. Pass /DREPO_ROOT=<path>."
!endif

!define APP_NAME "USB-Relay Utility"
!define APP_PUBLISHER "Min Xie (minxie.dallas@gmail.com)"
!define APP_EXE "usbrelay.exe"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\usbrelay"
!include "InstallDirectory.nsh"

Name "${APP_NAME}"
OutFile "${OUTPUT_DIR}\usbrelay-setup-v${VERSION}.exe"
InstallDir "$PROGRAMFILES\usbrelay"
AllowSkipFiles off

!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!define MUI_DIRECTORYPAGE_TEXT_TOP "Setup will install ${APP_NAME} in the following folder. Running USB Relay processes in this folder will be stopped when installation starts. Click Browse to choose another folder."
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  Call ResolveInstallDirectory
FunctionEnd

Section "Install"
  Call StopInstallationProcesses
  ${If} ${Errors}
    SetErrorLevel 1
    Abort "Could not stop running USB Relay processes. Close them and try again. See $InstallerLogPath."
  ${EndIf}
  DetailPrint "[Installer] Installing ${VERSION} to $INSTDIR"
  ClearErrors
  SetOutPath "$INSTDIR"
  File "${APP_OUTPUT_DIR}\${APP_EXE}"
  ${If} ${Errors}
    DetailPrint "[Installer] Failed to replace $INSTDIR\${APP_EXE}; close all USB Relay processes."
    SetErrorLevel 1
    Abort "Cannot replace usbrelay.exe. Close all USB Relay processes and try again."
  ${EndIf}
  File /nonfatal "${APP_OUTPUT_DIR}\${APP_EXE}.config"
  File "${REPO_ROOT}\README.md"

  SetOutPath "$INSTDIR\assets"
  File /nonfatal /r "${APP_OUTPUT_DIR}\assets\*.*"

  SetOutPath "$INSTDIR\scripts"
  File "${REPO_ROOT}\scripts\usbrelay-completion.ps1"
  File "${REPO_ROOT}\scripts\Get-McpConfig.ps1"

  SetOutPath "$INSTDIR\scripts\clink"
  File /r "${REPO_ROOT}\scripts\clink\*.*"

  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\uninstall.exe"
  ${If} ${Errors}
    DetailPrint "[Installer] Failed to extract installation files or write uninstaller."
    SetErrorLevel 1
    Abort "Could not write all installation files."
  ${EndIf}

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\uninstall.exe"

  ${If} ${RunningX64}
    SetRegView 64
  ${EndIf}
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" "$\"$INSTDIR\uninstall.exe$\""
  WriteRegStr HKLM "${UNINSTALL_KEY}" "QuietUninstallString" "$\"$INSTDIR\uninstall.exe$\" /S"
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  ${If} ${RunningX64}
    SetRegView 64
  ${EndIf}

  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"

  Delete "$INSTDIR\${APP_EXE}"
  Delete "$INSTDIR\${APP_EXE}.config"
  Delete "$INSTDIR\README.md"
  RMDir /r "$INSTDIR\assets"
  RMDir /r "$INSTDIR\scripts"
  Delete "$INSTDIR\uninstall.exe"
  RMDir "$INSTDIR"

  DeleteRegKey HKLM "${UNINSTALL_KEY}"
SectionEnd
