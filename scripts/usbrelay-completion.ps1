[CmdletBinding()]
param(
    [string]$CommandPath = 'usbrelay',
    [string[]]$CommandName = @('usbrelay', 'usbrelay.exe'),
    [string]$AliasName = 'usbrelay',
    [switch]$NoAlias,
    [switch]$PassThru
)

$completionCommand = $CommandPath
if (Test-Path -LiteralPath $CommandPath) {
    $completionCommand = (Resolve-Path -LiteralPath $CommandPath).ProviderPath
}

$completionScript = {
    param($wordToComplete, $commandAst, $cursorPosition)

    $line = $commandAst.Extent.Text
    if ($cursorPosition -gt $line.Length) {
        $line += ' ' * ($cursorPosition - $line.Length)
    }

    $items = & $completionCommand complete --position $cursorPosition --line $line 2>$null
    foreach ($item in $items) {
        if ([string]::IsNullOrWhiteSpace($item)) {
            continue
        }

        [System.Management.Automation.CompletionResult]::new(
            $item,
            $item,
            [System.Management.Automation.CompletionResultType]::ParameterValue,
            $item)
    }
}.GetNewClosure()

$registeredCommandNames = foreach ($name in $CommandName) {
    if ([string]::IsNullOrWhiteSpace($name)) {
        continue
    }

    if ($name -match '[\\/]') {
        Write-Warning "Skipping path-shaped CommandName '$name'. Register-ArgumentCompleter command names should be bare command names. Use -CommandPath for the executable path and run '$AliasName ...' instead."
        continue
    }

    $name
}

foreach ($name in ($registeredCommandNames | Select-Object -Unique)) {
    Register-ArgumentCompleter -Native -CommandName $name -ScriptBlock $completionScript
}

if (-not $NoAlias -and -not [string]::IsNullOrWhiteSpace($AliasName)) {
    if ($registeredCommandNames -notcontains $AliasName) {
        Register-ArgumentCompleter -Native -CommandName $AliasName -ScriptBlock $completionScript
        $registeredCommandNames = @($registeredCommandNames) + $AliasName
    }

    Set-Alias -Name $AliasName -Value $completionCommand -Scope Global -Force
}

if ($PassThru) {
    [pscustomobject]@{
        CommandPath = $completionCommand
        CommandName = @($registeredCommandNames | Select-Object -Unique)
        AliasName = if ($NoAlias) { $null } else { $AliasName }
    }
}
