#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent

$modules = @(
    'core\Tui-Engine.psm1',
    'core\Config-Store.psm1',
    'core\Env-Loader.psm1',
    'core\Native-Process.psm1',
    'core\Simple-Acme-Reconciler.psm1',
    'setup\Form-Runner.psm1'
)

foreach ($relative in $modules) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Module not found: $path"
    }

    Write-Host "Importing $path"
    Import-Module $path -Force
}
