!include "FileFunc.nsh"

Var InstallerLogPath
Var RunningInstallDirectory
Var RunningInstallAmbiguous
Var ProcessScanMode
Var ProcessStopFailed

; Keep diagnostics available even when .onInit runs before the details page.
!macro InstallerLog TEXT
  DetailPrint "${TEXT}"
  Push $R8
  Push $R9
  ClearErrors
  FileOpen $R8 "$InstallerLogPath" a
  ${IfNot} ${Errors}
    FileSeek $R8 0 END
    FileWriteUTF16LE $R8 "${TEXT}$\r$\n"
    FileClose $R8
  ${EndIf}
  Pop $R9
  Pop $R8
!macroend

; PROCESSENTRY32W has a pointer-sized heap field and native alignment.
!if ${NSIS_PTR_SIZE} = 8
  !define PROCESS_ENTRY_SIZE 568
  !define PROCESS_ENTRY_NAME_OFFSET 44
!else
  !define PROCESS_ENTRY_SIZE 556
  !define PROCESS_ENTRY_NAME_OFFSET 36
!endif

Function ScanRunningApplications
  StrCpy $RunningInstallDirectory ""
  StrCpy $RunningInstallAmbiguous 0
  StrCpy $ProcessStopFailed 0
  !insertmacro InstallerLog "[Processes] Scan mode=$ProcessScanMode, target=$INSTDIR"
  System::Call 'kernel32::CreateToolhelp32Snapshot(i 2, i 0) p .r0 ?e'
  Pop $9
  ${If} $0 == -1
    !insertmacro InstallerLog "[Processes] Snapshot failed, Win32 error=$9"
    StrCpy $ProcessStopFailed 1
    Return
  ${EndIf}
  System::Alloc ${PROCESS_ENTRY_SIZE}
  Pop $1
  ${If} $1 == 0
    !insertmacro InstallerLog "[Processes] Could not allocate process entry."
    System::Call 'kernel32::CloseHandle(p r0)'
    StrCpy $ProcessStopFailed 1
    Return
  ${EndIf}
  System::Call '*$1(i ${PROCESS_ENTRY_SIZE})'
  System::Call 'kernel32::Process32FirstW(p r0, p r1) i .r2 ?e'
  Pop $9
  ${DoWhile} $2 != 0
    IntOp $3 $1 + 8
    System::Call '*$3(i .r3)'
    IntOp $4 $1 + ${PROCESS_ENTRY_NAME_OFFSET}
    System::Call '*$4(&w260 .r4)'
    ${If} $4 == "${APP_EXE}"
      ; Identify the folder before requesting permission to terminate a process.
      System::Call 'kernel32::OpenProcess(i 0x101000, i 0, i r3) p .r5 ?e'
      Pop $9
      ${If} $5 == 0
        !insertmacro InstallerLog "[Processes] Cannot open PID=$3, Win32 error=$9"
        ; ERROR_INVALID_PARAMETER means a process disappeared after the snapshot.
        ${If} $9 != 87
          StrCpy $ProcessStopFailed 1
        ${EndIf}
      ${Else}
        System::Call 'kernel32::QueryFullProcessImageNameW(p r5, i 0, w .r6, *i ${NSIS_MAX_STRLEN}) i .r2 ?e'
        Pop $9
        ${If} $2 == 0
          System::Call 'kernel32::WaitForSingleObject(p r5, i 0) i .r7'
          !insertmacro InstallerLog "[Processes] Cannot resolve PID=$3, Win32 error=$9, wait=$7"
          ${If} $7 != 0
            StrCpy $ProcessStopFailed 1
          ${EndIf}
        ${Else}
          ${GetFileName} "$6" $8
          ${If} $8 != "${APP_EXE}"
            !insertmacro InstallerLog "[Processes] PID=$3 now belongs to another executable: $6"
          ${Else}
          ${GetParent} "$6" $8
          GetFullPathName $8 "$8"
          !insertmacro InstallerLog "[Processes] PID=$3, executable=$6, directory=$8"
          ${If} $ProcessScanMode == "discover"
            ${If} $RunningInstallDirectory == ""
              StrCpy $RunningInstallDirectory "$8"
            ${ElseIf} $RunningInstallDirectory != $8
              StrCpy $RunningInstallAmbiguous 1
              !insertmacro InstallerLog "[Processes] Multiple running installation folders; retain registry/manual selection."
            ${Else}
              !insertmacro InstallerLog "[Processes] Additional process in the same installation folder."
            ${EndIf}
          ${ElseIf} $8 != $INSTDIR
            !insertmacro InstallerLog "[Processes] Leaving PID=$3 running in another installation folder."
          ${ElseIf} $ProcessScanMode == "verify"
            System::Call 'kernel32::WaitForSingleObject(p r5, i 0) i .r7'
            ${If} $7 != 0
              StrCpy $ProcessStopFailed 1
              !insertmacro InstallerLog "[Processes] PID=$3 still locks the selected installation."
            ${EndIf}
          ${Else}
            Call StopResolvedProcess
          ${EndIf}
          ${EndIf}
        ${EndIf}
        System::Call 'kernel32::CloseHandle(p r5)'
      ${EndIf}
    ${EndIf}
    System::Call 'kernel32::Process32NextW(p r0, p r1) i .r2 ?e'
    Pop $9
  ${Loop}
  ${If} $9 != 18
    !insertmacro InstallerLog "[Processes] Enumeration ended unexpectedly, Win32 error=$9"
    StrCpy $ProcessStopFailed 1
  ${EndIf}
  System::Free $1
  System::Call 'kernel32::CloseHandle(p r0)'
  !insertmacro InstallerLog "[Processes] Scan complete; folder=$RunningInstallDirectory, ambiguous=$RunningInstallAmbiguous, failed=$ProcessStopFailed"
FunctionEnd

Function StopResolvedProcess
  ; Keep the query handle alive, and validate the image on the stronger handle
  ; too, so PID reuse cannot terminate a process in another installation.
  System::Call 'kernel32::OpenProcess(i 0x101001, i 0, i r3) p .r7 ?e'
  Pop $9
  ${If} $7 == 0
    System::Call 'kernel32::WaitForSingleObject(p r5, i 0) i .r2'
    ${If} $2 != 0
      StrCpy $ProcessStopFailed 1
      !insertmacro InstallerLog "[Processes] Cannot stop selected PID=$3, Win32 error=$9"
    ${Else}
      !insertmacro InstallerLog "[Processes] Selected PID=$3 already exited."
    ${EndIf}
    Return
  ${EndIf}
  System::Call 'kernel32::QueryFullProcessImageNameW(p r7, i 0, w .R0, *i ${NSIS_MAX_STRLEN}) i .r2 ?e'
  Pop $9
  ${If} $2 == 0
    System::Call 'kernel32::WaitForSingleObject(p r7, i 0) i .r2'
    ${If} $2 != 0
      StrCpy $ProcessStopFailed 1
      !insertmacro InstallerLog "[Processes] Cannot revalidate selected PID=$3, Win32 error=$9"
    ${EndIf}
  ${ElseIf} $R0 != $6
    !insertmacro InstallerLog "[Processes] PID=$3 changed image to $R0; leaving it running."
  ${Else}
    !insertmacro InstallerLog "[Processes] Terminating PID=$3 before replacing files."
    System::Call 'kernel32::TerminateProcess(p r7, i 0) i .r2 ?e'
    Pop $9
    ${If} $2 == 0
      !insertmacro InstallerLog "[Processes] Termination failed for PID=$3, Win32 error=$9"
    ${EndIf}
    System::Call 'kernel32::WaitForSingleObject(p r7, i 10000) i .r2'
    ${If} $2 != 0
      StrCpy $ProcessStopFailed 1
      !insertmacro InstallerLog "[Processes] PID=$3 did not exit, wait=$2"
    ${Else}
      !insertmacro InstallerLog "[Processes] PID=$3 exited; file handles released."
    ${EndIf}
  ${EndIf}
  System::Call 'kernel32::CloseHandle(p r7)'
FunctionEnd

Function StopInstallationProcesses
  GetFullPathName $INSTDIR "$INSTDIR"
  StrCpy $ProcessScanMode "stop"
  Call ScanRunningApplications
  ${If} $ProcessStopFailed == 0
    ; A second snapshot also catches clients restarted while others were stopped.
    StrCpy $ProcessScanMode "verify"
    Call ScanRunningApplications
  ${EndIf}
  ${If} $ProcessStopFailed != 0
    !insertmacro InstallerLog "[Processes] Installation blocked: running processes could not be stopped or verified."
    SetErrors
  ${Else}
    !insertmacro InstallerLog "[Processes] Selected installation is ready for file replacement."
    ClearErrors
  ${EndIf}
FunctionEnd
