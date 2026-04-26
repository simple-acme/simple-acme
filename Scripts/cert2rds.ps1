param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [object[]]$Args
)

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ScriptPath = Join-Path $ScriptRoot ("connectors/{0}" -f [System.IO.Path]::GetFileName($MyInvocation.MyCommand.Path))

if (-not (Test-Path -LiteralPath $ScriptPath)) {
throw @"
Required deployment script not found.

Expected:
$ScriptPath

Check:

* Scripts folder location
* Installation directory

Tip:
Re-run installer or verify installation.
"@
}

$ResolvedScriptPath = (Resolve-Path -LiteralPath $ScriptPath -ErrorAction Stop).Path
& $ResolvedScriptPath @Args
exit $LASTEXITCODE
