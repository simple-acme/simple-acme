Set-StrictMode -Version Latest

$script:RequiredEnvVars = @(
    'CERTIFICAAT_DROP_DIR',
    'CERTIFICAAT_STATE_DIR',
    'CERTIFICAAT_LOG_DIR'
)

$script:OptionalEnvVars = @{
    CERTIFICAAT_VERIFY_MAX_ATTEMPTS = 3
    CERTIFICAAT_ACTIVATE_TIMEOUT_MS = 120000
    CERTIFICAAT_DEFAULT_FANOUT      = 'fail-fast'
}

function Initialize-CertificaatConfig {
    $missing = @()
    foreach ($name in $script:RequiredEnvVars) {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) { $missing += $name }
    }

    if ($missing.Count -gt 0) {
        throw "Missing required environment variables: $($missing -join ', ')"
    }

    foreach ($name in $script:OptionalEnvVars.Keys) {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
            [Environment]::SetEnvironmentVariable($name, [string]$script:OptionalEnvVars[$name])
        }
    }
}
