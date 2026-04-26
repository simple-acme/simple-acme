param(
    [switch]$PreflightOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module "$PSScriptRoot/core/Env-Loader.psm1" -Force
Import-Module "$PSScriptRoot/core/Simple-Acme-Reconciler.psm1" -Force

$transcriptStarted = $false
try {
    if ([Environment]::GetEnvironmentVariable('CERTIFICATE_TRANSCRIPT_LOGGING') -eq '1') {
        $logDir = [Environment]::GetEnvironmentVariable('CERTIFICATE_LOG_DIR')
        if (-not [string]::IsNullOrWhiteSpace($logDir)) {
            if (-not (Test-Path -LiteralPath $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
            $transcriptPath = Join-Path $logDir ("reconcile-transcript-{0}.log" -f (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss'))
            Start-Transcript -Path $transcriptPath -Force | Out-Null
            $transcriptStarted = $true
        }
    }
    $envValues = Import-EnvFile -Force
    $preflight = Assert-ReconcilePreflight -EnvValues $envValues
    Write-Output "preflight ok: wacs=$($preflight.WacsPath) domains=$($preflight.DomainCount) script=$($preflight.ScriptPath)"
    if ($PreflightOnly) {
        Write-Output 'preflight only mode: reconcile skipped.'
        exit 0
    }
    $action = Invoke-SimpleAcmeReconcile -EnvValues $envValues
    Write-Output "simple-acme reconcile complete: $action"
    exit 0
} catch {
    Write-Error $_
    exit 1
} finally {
    if ($transcriptStarted) {
        Stop-Transcript | Out-Null
    }
}
