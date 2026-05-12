param(
    [string]$VersionPropsPath = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($VersionPropsPath)) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $VersionPropsPath = Join-Path $repoRoot "Version.props"
}

if (-not (Test-Path -LiteralPath $VersionPropsPath)) {
    throw "Version.props was not found: $VersionPropsPath"
}

[xml]$versionProps = Get-Content -LiteralPath $VersionPropsPath
$namespaceManager = New-Object System.Xml.XmlNamespaceManager($versionProps.NameTable)
$namespaceManager.AddNamespace("msb", "http://schemas.microsoft.com/developer/msbuild/2003")

$node = $versionProps.SelectSingleNode("/msb:Project/msb:PropertyGroup/msb:UsbRelayVersion", $namespaceManager)
if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) {
    throw "UsbRelayVersion was not found in $VersionPropsPath"
}

$node.InnerText.Trim()
