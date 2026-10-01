param(
    [string]$CommandPath,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($CommandPath)) {
    $CommandPath = Join-Path $PSScriptRoot '..\usbrelay.exe'
    if (-not (Test-Path -LiteralPath $CommandPath)) {
        $CommandPath = Join-Path $PSScriptRoot '..\usbrelay\bin\Release\usbrelay.exe'
    }
}
$executable = (Resolve-Path -LiteralPath $CommandPath).Path
$configuration = [ordered]@{
    mcpServers = [ordered]@{
        usbrelay = [ordered]@{ command = $executable; args = @('mcp') }
    }
} | ConvertTo-Json -Depth 5

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $configuration
} else {
    [System.IO.File]::WriteAllText($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath), $configuration, (New-Object System.Text.UTF8Encoding($false)))
}
