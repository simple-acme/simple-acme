Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module "$PSScriptRoot/core/Env-Loader.psm1" -Force
Import-Module "$PSScriptRoot/core/Simple-Acme-Reconciler.psm1" -Force

try {
    $envValues = Import-EnvFile -Force
    $action = Invoke-SimpleAcmeReconcile -EnvValues $envValues
    Write-Host "simple-acme reconcile complete: $action"
    exit 0
} catch {
    Write-Error $_
    exit 1
}
