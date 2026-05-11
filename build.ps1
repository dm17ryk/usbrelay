param(
    [string]$Configuration = "Debug",
    [string]$Platform = "Any CPU",
    [string]$Target = "Build",
    [string]$Verbosity = "minimal",
    [string]$MSBuildPath = "",
    [switch]$RunTests
)

$ErrorActionPreference = "Stop"

function Resolve-MSBuildPath {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (Test-Path -LiteralPath $RequestedPath) {
            return (Resolve-Path -LiteralPath $RequestedPath).Path
        }

        throw "MSBuild path was provided but does not exist: $RequestedPath"
    }

    $knownPath = "D:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    if (Test-Path -LiteralPath $knownPath) {
        return $knownPath
    }

    $vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vsWhere) {
        $installPath = & $vsWhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($installPath)) {
            $candidate = Join-Path $installPath "MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path -LiteralPath $candidate) {
                return $candidate
            }
        }
    }

    $fromPath = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    throw "MSBuild.exe was not found. Pass -MSBuildPath with the full path to MSBuild.exe."
}

$repoRoot = $PSScriptRoot
$solutionPath = Join-Path $repoRoot "usbrelay.sln"
if (-not (Test-Path -LiteralPath $solutionPath)) {
    throw "Solution was not found: $solutionPath"
}

$resolvedMSBuild = Resolve-MSBuildPath -RequestedPath $MSBuildPath

Write-Host "MSBuild: $resolvedMSBuild"
Write-Host "Solution: $solutionPath"
Write-Host "Configuration: $Configuration"
Write-Host "Platform: $Platform"
Write-Host "Target: $Target"

& $resolvedMSBuild $solutionPath `
    "/t:$Target" `
    "/p:Configuration=$Configuration" `
    "/p:Platform=$Platform" `
    "/v:$Verbosity"

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if ($RunTests) {
    $testsPath = Join-Path $repoRoot "usbrelay.Tests\bin\$Configuration\usbrelay.Tests.exe"
    if (-not (Test-Path -LiteralPath $testsPath)) {
        throw "Test executable was not found after build: $testsPath"
    }

    Write-Host "Running tests: $testsPath"
    & $testsPath
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
