param(
    [switch]$PreflightOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module "$PSScriptRoot/core/Env-Loader.psm1" -Force
Import-Module "$PSScriptRoot/core/Simple-Acme-Reconciler.psm1" -Force

try {
    $envValues = Import-EnvFile -Force
    $preflight = Assert-ReconcilePreflight -EnvValues $envValues
    Write-Host "preflight ok: wacs=$($preflight.WacsPath) domains=$($preflight.DomainCount) script=$($preflight.ScriptPath)"
    if ($PreflightOnly) {
        Write-Host 'preflight only mode: reconcile skipped.'
        exit 0
    }
    $action = Invoke-SimpleAcmeReconcile -EnvValues $envValues
    Write-Host "simple-acme reconcile complete: $action"
    exit 0
} catch {
    Write-Error $_
    exit 1
}
