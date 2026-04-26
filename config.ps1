Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module "$PSScriptRoot/core/Env-Loader.psm1" -Force

function Initialize-CertificateConfig {
    param([switch]$AllowIncomplete)

    $values = Import-EnvFile -AllowIncomplete:$AllowIncomplete
    if (-not $AllowIncomplete) {
        foreach ($required in @('CERTIFICATE_CONFIG_DIR','CERTIFICATE_DROP_DIR','CERTIFICATE_STATE_DIR','CERTIFICATE_LOG_DIR','DOMAINS')) {
            $value = if ($values.ContainsKey($required)) { [string]$values[$required] } else { '' }
            if ([string]::IsNullOrWhiteSpace($value)) {
                throw "Missing config: $required"
            }
        }
    }
    return $values
}
