[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = "Medium")]
param(
    [ValidateSet("Build", "Patch", "Minor", "Major")]
    [string]$Increment = "Build",
    [string]$Version = "",
    [string]$MSBuildPath = "",
    [string]$MakeNsisPath = "",
    [string]$OutputDirectory = "",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$versionPropsPath = Join-Path $repoRoot "Version.props"
$installerScriptPath = Join-Path $PSScriptRoot "Build-Installer.ps1"

if (-not (Test-Path -LiteralPath $versionPropsPath)) {
    throw "Version.props was not found: $versionPropsPath"
}

if (-not (Test-Path -LiteralPath $installerScriptPath)) {
    throw "Build-Installer.ps1 was not found: $installerScriptPath"
}

function Read-VersionText {
    param([string]$Path)

    $content = Get-Content -LiteralPath $Path -Raw
    $match = [regex]::Match($content, '<UsbRelayVersion>\s*(?<version>\d+\.\d+\.\d+\.\d+)\s*</UsbRelayVersion>')
    if (-not $match.Success) {
        throw "UsbRelayVersion was not found in $Path"
    }

    return $match.Groups["version"].Value
}

function Get-IncrementedVersion {
    param(
        [Version]$CurrentVersion,
        [string]$Part
    )

    switch ($Part) {
        "Major" { return "{0}.0.0.0" -f ($CurrentVersion.Major + 1) }
        "Minor" { return "{0}.{1}.0.0" -f $CurrentVersion.Major, ($CurrentVersion.Minor + 1) }
        "Patch" { return "{0}.{1}.{2}.0" -f $CurrentVersion.Major, $CurrentVersion.Minor, ($CurrentVersion.Build + 1) }
        "Build" { return "{0}.{1}.{2}.{3}" -f $CurrentVersion.Major, $CurrentVersion.Minor, $CurrentVersion.Build, ($CurrentVersion.Revision + 1) }
        default { throw "Unsupported version increment: $Part" }
    }
}

$oldVersionText = Read-VersionText -Path $versionPropsPath
$oldVersion = [Version]::Parse($oldVersionText)

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    if ($PSBoundParameters.ContainsKey("Increment")) {
        throw "Use either -Version or -Increment, not both."
    }

    if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw "Version must have four numeric components, for example 1.0.0.9."
    }

    $newVersionText = $Version
}
else {
    $newVersionText = Get-IncrementedVersion -CurrentVersion $oldVersion -Part $Increment
}

Write-Host "Current version: $oldVersionText"
Write-Host "Release version: $newVersionText"

$versionContent = Get-Content -LiteralPath $versionPropsPath -Raw
$versionPattern = '(<UsbRelayVersion>\s*)' + [regex]::Escape($oldVersionText) + '(\s*</UsbRelayVersion>)'
$updatedVersionContent = [regex]::Replace(
    $versionContent,
    $versionPattern,
    [System.Text.RegularExpressions.MatchEvaluator]{ param($match) $match.Groups[1].Value + $newVersionText + $match.Groups[2].Value },
    1)

if ($updatedVersionContent -eq $versionContent -and $newVersionText -ne $oldVersionText) {
    throw "Could not update UsbRelayVersion in $versionPropsPath"
}

if (-not $PSCmdlet.ShouldProcess($versionPropsPath, "Set UsbRelayVersion to $newVersionText")) {
    return
}

$versionChanged = $updatedVersionContent -ne $versionContent
try {
    if ($versionChanged) {
        [System.IO.File]::WriteAllText($versionPropsPath, $updatedVersionContent, (New-Object System.Text.UTF8Encoding($false)))
        Write-Host "Updated: $versionPropsPath"
    }

    $buildArguments = @{
        Configuration = "Release"
        Platform = "Any CPU"
        MSBuildPath = $MSBuildPath
        MakeNsisPath = $MakeNsisPath
        OutputDirectory = $OutputDirectory
    }
    if (-not $SkipTests) {
        $buildArguments.RunTests = $true
    }

    Write-Host "Building Release and creating the installer..."
    & $installerScriptPath @buildArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Build-Installer.ps1 failed with exit code $LASTEXITCODE."
    }

    Write-Host "Release $newVersionText completed successfully."
}
catch {
    if ($versionChanged) {
        [System.IO.File]::WriteAllText($versionPropsPath, $versionContent, (New-Object System.Text.UTF8Encoding($false)))
        Write-Warning "Release failed; restored $versionPropsPath to $oldVersionText."
    }

    throw
}
