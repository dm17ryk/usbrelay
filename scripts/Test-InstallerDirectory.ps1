param([string]$MakeNsisPath = "", [string]$OutputDirectory = "")
$ErrorActionPreference = "Stop"
if (!$MakeNsisPath) { $MakeNsisPath = (Get-Command makensis.exe -ErrorAction Stop).Source }
if (!$OutputDirectory) { $OutputDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) "artifacts\installer-tests" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
& $MakeNsisPath "/DTEST_OUTPUT=$OutputDirectory" (Join-Path $PSScriptRoot "..\installer\tests\InstallDirectoryTest.nsi")
if ($LASTEXITCODE -ne 0) { throw "Failed to compile installer directory regression harness." }
& $MakeNsisPath "/DTEST_OUTPUT=$OutputDirectory" (Join-Path $PSScriptRoot "..\installer\tests\RunningApplicationTest.nsi")
if ($LASTEXITCODE -ne 0) { throw "Failed to compile running application fixture." }
$ownedProcesses = [Collections.Generic.List[Diagnostics.Process]]::new()
$cleanupHandles = [Collections.Generic.List[IntPtr]]::new()
if (!("UsbRelayInstallerProcessTest" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class UsbRelayInstallerProcessTest
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string text, uint revision, out IntPtr descriptor, IntPtr size);
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool SetKernelObjectSecurity(IntPtr handle, uint information, IntPtr descriptor);
    [DllImport("kernel32.dll")]
    public static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool TerminateProcess(IntPtr handle, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr handle);
}
'@
}
function Deny-TestProcessAccess($process, [uint32]$deniedAccess) {
    # Retain an existing handle so cleanup remains possible after changing the
    # owned fixture's DACL. Never change permissions on real application processes.
    $handle = [UsbRelayInstallerProcessTest]::OpenProcess(0x1fffff, $false, $process.Id)
    if ($handle -eq [IntPtr]::Zero) { throw "Cannot retain fixture cleanup handle." }
    $cleanupHandles.Add($handle)
    $descriptor = [IntPtr]::Zero
    try {
        $sddl = "D:(D;;0x" + $deniedAccess.ToString("x8") + ";;;WD)(A;;GA;;;WD)"
        if (![UsbRelayInstallerProcessTest]::ConvertStringSecurityDescriptorToSecurityDescriptorW($sddl, 1, [ref]$descriptor, [IntPtr]::Zero)) { throw "Cannot create fixture permissions." }
        if (![UsbRelayInstallerProcessTest]::SetKernelObjectSecurity($handle, 4, $descriptor)) { throw "Cannot set fixture permissions." }
    } finally {
        if ($descriptor -ne [IntPtr]::Zero) { [void][UsbRelayInstallerProcessTest]::LocalFree($descriptor) }
    }
    return $handle
}
function Stop-ProtectedFixture([IntPtr]$handle) {
    if ([UsbRelayInstallerProcessTest]::WaitForSingleObject($handle, 0) -ne 0) {
        if (![UsbRelayInstallerProcessTest]::TerminateProcess($handle, 0)) { throw "Cannot clean up protected fixture." }
        if ([UsbRelayInstallerProcessTest]::WaitForSingleObject($handle, 10000) -ne 0) { throw "Protected fixture did not exit." }
    }
}
function Start-TestApplication([string]$directory, [switch]$Existing) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $executable = Join-Path $directory "usbrelay-directory-test-child.exe"
    if (!$Existing) { Copy-Item -LiteralPath (Join-Path $OutputDirectory "usbrelay-directory-test-child.exe") -Destination $executable -Force }
    $process = Start-Process -FilePath $executable -WindowStyle Hidden -PassThru
    $ownedProcesses.Add($process)
    return $process
}
$testKey = "Software\Classes\CLSID\{ECD27471-CC17-4ADC-B2F6-A44F78A21B73}"
$base32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, [Microsoft.Win32.RegistryView]::Registry32)
$base64 = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, [Microsoft.Win32.RegistryView]::Registry64)
function Set-TestLocation($registryBase, [string]$location) {
    $key = $registryBase.CreateSubKey($testKey)
    try { $key.SetValue("InstallLocation", $location) } finally { $key.Dispose() }
}
function Show-HarnessLog($process) {
    $logPath = Join-Path ([IO.Path]::GetTempPath()) "usbrelay-installer-$($process.Id).log"
    Write-Host "INFO Installer harness PID=$($process.Id), exit=$($process.ExitCode), log=$logPath"
    if (Test-Path -LiteralPath $logPath) {
        Get-Content -LiteralPath $logPath | ForEach-Object { Write-Host $_ }
    } else {
        Write-Host "INFO Installer harness did not create a diagnostic log."
    }
}
function Assert-HarnessDiagnostic($process, [string]$expected) {
    $logPath = Join-Path ([IO.Path]::GetTempPath()) "usbrelay-installer-$($process.Id).log"
    if (!(Test-Path -LiteralPath $logPath) -or !([IO.File]::ReadAllText($logPath).Contains($expected))) {
        Show-HarnessLog $process
        throw "Expected installer diagnostic '$expected'."
    }
}
function Assert-Location([string]$expected, [string]$arguments = "/S", [string]$requiredDiagnostic = "") {
    $process = Start-Process -FilePath (Join-Path $OutputDirectory "directory-test.exe") -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { Show-HarnessLog $process; throw "Harness failed: $($process.ExitCode)" }
    $actual = [IO.File]::ReadAllText((Join-Path $OutputDirectory "resolved.txt"))
    if ($actual -ne $expected) { Show-HarnessLog $process; throw "Expected '$expected', got '$actual'" }
    if ($requiredDiagnostic) { Assert-HarnessDiagnostic $process $requiredDiagnostic }
    Write-Host "PASS Installer folder: $actual"
}
$existing32 = $base32.OpenSubKey($testKey)
$existing64 = $base64.OpenSubKey($testKey)
if ($existing32 -or $existing64) {
    if ($existing32) { $existing32.Dispose() }
    if ($existing64) { $existing64.Dispose() }
    $base32.Dispose()
    $base64.Dispose()
    throw "Regression registry key already exists; refusing to overwrite it."
}
try {
    Write-Host "INFO Installer regression compiler: $(& $MakeNsisPath /VERSION)"
    & whoami.exe /priv
    $custom = "C:\Essence_SC\usbrelay"
    $activeDirectory = Join-Path $OutputDirectory ("Running app " + [char]0x05E9 + [char]0x05DC + [char]0x05D5 + [char]0x05DD)
    $activeProcess = Start-TestApplication $activeDirectory
    $registryBase = if ([Environment]::Is64BitOperatingSystem) { $base64 } else { $base32 }
    Set-TestLocation $registryBase (Join-Path $env:ProgramFiles "usbrelay")
    Assert-Location $activeDirectory
    Assert-Location "D:\Explicit folder\USB Relay" "/S /D=D:\Explicit folder\USB Relay"
    $duplicateProcess = Start-TestApplication $activeDirectory -Existing
    Assert-Location $activeDirectory
    $otherDirectory = Join-Path $OutputDirectory "Another installation"
    $otherProcess = Start-TestApplication $otherDirectory
    Assert-Location (Join-Path $env:ProgramFiles "usbrelay")
    Assert-Location $activeDirectory "/S /STOP /D=$activeDirectory"
    if (!$activeProcess.HasExited -or !$duplicateProcess.HasExited) { throw "Installer must stop every process in the selected folder." }
    if ($otherProcess.HasExited) { throw "Installer stopped a process in another installation folder." }
    Copy-Item -LiteralPath (Join-Path $OutputDirectory "usbrelay-directory-test-child.exe") -Destination (Join-Path $activeDirectory "usbrelay-directory-test-child.exe") -Force
    Write-Host "PASS Installer stopped selected processes, released executable locks, and preserved other installations."
    $otherProcess.Kill()
    $otherProcess.WaitForExit()
    $protectedOther = Start-TestApplication $otherDirectory -Existing
    $protectedHandle = Deny-TestProcessAccess $protectedOther 1
    Assert-Location $activeDirectory "/S /STOP /D=$activeDirectory"
    if ($protectedOther.HasExited) { throw "Unrelated protected installation must remain running." }
    Write-Host "PASS Unrelated process denying termination does not block installation."
    Stop-ProtectedFixture $protectedHandle
    $activeProcess = Start-TestApplication $activeDirectory -Existing
    $unknownProcess = Start-TestApplication $otherDirectory -Existing
    $unknownHandle = Deny-TestProcessAccess $unknownProcess 0x1000
    Assert-Location (Join-Path $env:ProgramFiles "usbrelay") "/S" "Cannot open PID=$($unknownProcess.Id), Win32 error=5"
    Write-Host "PASS Unresolved running process prevents assuming a unique installation folder."
    Stop-ProtectedFixture $unknownHandle
    $activeProcess.Kill()
    $activeProcess.WaitForExit()
    $protectedTarget = Start-TestApplication $activeDirectory -Existing
    $targetHandle = Deny-TestProcessAccess $protectedTarget 1
    $blockedSetup = Start-Process -FilePath (Join-Path $OutputDirectory "directory-test.exe") -ArgumentList "/S /STOP /D=$activeDirectory" -WindowStyle Hidden -PassThru -Wait
    Write-Host "INFO Termination-denied fixture PID=$($protectedTarget.Id), exited=$($protectedTarget.HasExited)"
    Show-HarnessLog $blockedSetup
    if ($blockedSetup.ExitCode -ne 1 -or $protectedTarget.HasExited) { throw "Setup must fail if a selected process cannot be terminated." }
    Assert-HarnessDiagnostic $blockedSetup "Cannot stop selected PID=$($protectedTarget.Id), Win32 error=5"
    Write-Host "PASS Installer fails when selected process cannot be terminated."
    Stop-ProtectedFixture $targetHandle
    if ([Environment]::Is64BitOperatingSystem) {
        Set-TestLocation $base64 $custom
        Assert-Location $custom
        Set-TestLocation $base64 "D:\Custom apps\USB Relay"
        Assert-Location "D:\Custom apps\USB Relay"
        $unicodeLocation = "D:\USB Relay " + [char]0x05E9 + [char]0x05DC + [char]0x05D5 + [char]0x05DD
        Set-TestLocation $base64 $unicodeLocation
        Assert-Location $unicodeLocation
        Assert-Location "D:\Explicit folder\USB Relay" "/S /D=D:\Explicit folder\USB Relay"
        $base64.DeleteSubKeyTree($testKey, $false)
        Set-TestLocation $base32 "D:\Legacy install\usbrelay"
        Assert-Location "D:\Legacy install\usbrelay"
        $base32.DeleteSubKeyTree($testKey, $false)
        Assert-Location (Join-Path $env:ProgramW6432 "usbrelay")
    } else {
        Set-TestLocation $base32 $custom
        Assert-Location $custom
        Assert-Location "D:\Explicit folder\USB Relay" "/S /D=D:\Explicit folder\USB Relay"
        $base32.DeleteSubKeyTree($testKey, $false)
        Assert-Location (Join-Path $env:ProgramFiles "usbrelay")
    }
} finally {
    foreach ($handle in $cleanupHandles) {
        try { Stop-ProtectedFixture $handle } finally { [void][UsbRelayInstallerProcessTest]::CloseHandle($handle) }
    }
    foreach ($process in $ownedProcesses) {
        if (!$process.HasExited) { $process.Kill(); $process.WaitForExit() }
        $process.Dispose()
    }
    $base32.DeleteSubKeyTree($testKey, $false)
    $base64.DeleteSubKeyTree($testKey, $false)
    $base32.Dispose()
    $base64.Dispose()
}
