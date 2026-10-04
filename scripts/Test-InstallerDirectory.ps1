param([string]$MakeNsisPath = "", [string]$OutputDirectory = "")
$ErrorActionPreference = "Stop"
if (!$MakeNsisPath) { $MakeNsisPath = (Get-Command makensis.exe -ErrorAction Stop).Source }
if (!$OutputDirectory) { $OutputDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) "artifacts\installer-tests" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
& $MakeNsisPath "/DTEST_OUTPUT=$OutputDirectory" (Join-Path $PSScriptRoot "..\installer\tests\InstallDirectoryTest.nsi")
if ($LASTEXITCODE -ne 0) { throw "Failed to compile installer directory regression harness." }
$testKey = "Software\Classes\CLSID\{ECD27471-CC17-4ADC-B2F6-A44F78A21B73}"
$base32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, [Microsoft.Win32.RegistryView]::Registry32)
$base64 = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, [Microsoft.Win32.RegistryView]::Registry64)
function Set-TestLocation($registryBase, [string]$location) {
    $key = $registryBase.CreateSubKey($testKey)
    try { $key.SetValue("InstallLocation", $location) } finally { $key.Dispose() }
}
function Assert-Location([string]$expected, [string]$arguments = "/S") {
    $process = Start-Process -FilePath (Join-Path $OutputDirectory "directory-test.exe") -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Harness failed: $($process.ExitCode)" }
    $actual = [IO.File]::ReadAllText((Join-Path $OutputDirectory "resolved.txt"))
    if ($actual -ne $expected) { throw "Expected '$expected', got '$actual'" }
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
    $custom = "C:\Essence_SC\usbrelay"
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
    $base32.DeleteSubKeyTree($testKey, $false)
    $base64.DeleteSubKeyTree($testKey, $false)
    $base32.Dispose()
    $base64.Dispose()
}
