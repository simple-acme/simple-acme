$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$modules = Get-ChildItem -Path $root -Filter '*.psm1' -Recurse

foreach ($module in $modules) {
    Write-Host "Importing $($module.FullName)"
    Import-Module $module.FullName -Force
}
