param(
    [string]$Configuration = "Release",
    [string]$Platform = "Any CPU",
    [string]$MSBuildPath = "",
    [string]$MakeNsisPath = "",
    [string]$OutputDirectory = "",
    [switch]$RunTests
)

$ErrorActionPreference = "Stop"

function Resolve-MakeNsisPath {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (Test-Path -LiteralPath $RequestedPath) {
            return (Resolve-Path -LiteralPath $RequestedPath).Path
        }

        throw "makensis.exe path was provided but does not exist: $RequestedPath"
    }

    $fromPath = Get-Command makensis.exe -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    $knownPaths = @(
        "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
        "${env:ProgramFiles}\NSIS\makensis.exe"
    )

    foreach ($knownPath in $knownPaths) {
        if (Test-Path -LiteralPath $knownPath) {
            return $knownPath
        }
    }

    throw "makensis.exe was not found. Install NSIS or pass -MakeNsisPath with the full path to makensis.exe."
}

function Reset-Directory {
    param([string]$Path)

    Assert-SafeResetDirectory -Path $Path

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Assert-SafeResetDirectory {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "Refusing to reset an empty directory path."
    }

    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $rootPath = [System.IO.Path]::GetPathRoot($fullPath).TrimEnd('\', '/')
    if ($fullPath.Length -lt 10 -or [string]::Equals($fullPath, $rootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to reset unsafe directory path: $fullPath"
    }

    $knownUnsafePaths = @(
        $repoRoot,
        [Environment]::GetFolderPath("Windows"),
        [Environment]::GetFolderPath("ProgramFiles"),
        [Environment]::GetFolderPath("ProgramFilesX86"),
        [Environment]::GetFolderPath("UserProfile")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object {
        [System.IO.Path]::GetFullPath($_).TrimEnd('\', '/')
    }

    foreach ($unsafePath in $knownUnsafePaths) {
        if ([string]::Equals($fullPath, $unsafePath, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to reset protected directory path: $fullPath"
        }
    }

    $repoRootFullPath = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd('\', '/')
    if ($repoRootFullPath.StartsWith($fullPath + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to reset a parent directory of the repository: $fullPath"
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\release"
}

$version = & (Join-Path $PSScriptRoot "Get-UsbRelayVersion.ps1")
$resolvedMakeNsis = Resolve-MakeNsisPath -RequestedPath $MakeNsisPath
if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $outputDirectoryPath = [System.IO.Path]::GetFullPath($OutputDirectory)
} else {
    $outputDirectoryPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
}

Write-Host "Version: $version"
Write-Host "NSIS: $resolvedMakeNsis"
Write-Host "Output: $outputDirectoryPath"

$buildArguments = @{
    Configuration = $Configuration
    Platform = $Platform
    Target = "Build"
    MSBuildPath = $MSBuildPath
}
if ($RunTests) {
    $buildArguments.RunTests = $true
}

& (Join-Path $repoRoot "build.ps1") @buildArguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$appOutputDirectory = Join-Path $repoRoot "usbrelay\bin\$Configuration"
$appExecutable = Join-Path $appOutputDirectory "usbrelay.exe"
if (-not (Test-Path -LiteralPath $appExecutable)) {
    throw "Application executable was not found after build: $appExecutable"
}

Reset-Directory -Path $outputDirectoryPath

$portableRoot = Join-Path $outputDirectoryPath "usbrelay-v$version"
Reset-Directory -Path $portableRoot
Copy-Item -Path (Join-Path $appOutputDirectory "*") -Destination $portableRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination $portableRoot -Force

$portableScripts = Join-Path $portableRoot "scripts"
New-Item -ItemType Directory -Path $portableScripts -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot "scripts\usbrelay-completion.ps1") -Destination $portableScripts -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "scripts\clink") -Destination $portableScripts -Recurse -Force

$zipPath = Join-Path $outputDirectoryPath "usbrelay-v$version.zip"
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $portableRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal

$installerScript = Join-Path $repoRoot "installer\usbrelay.nsi"
if (-not (Test-Path -LiteralPath $installerScript)) {
    throw "NSIS script was not found: $installerScript"
}

& $resolvedMakeNsis `
    "/DVERSION=$version" `
    "/DAPP_OUTPUT_DIR=$appOutputDirectory" `
    "/DOUTPUT_DIR=$outputDirectoryPath" `
    "/DREPO_ROOT=$repoRoot" `
    $installerScript
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$installerPath = Join-Path $outputDirectoryPath "usbrelay-setup-v$version.exe"
if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Installer was not created: $installerPath"
}

Write-Host "Portable package: $zipPath"
Write-Host "Installer: $installerPath"
