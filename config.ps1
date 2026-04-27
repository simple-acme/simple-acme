Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module "$PSScriptRoot/core/Env-Loader.psm1" -Force

function Initialize-CertificateConfig {
    param([switch]$AllowIncomplete)

    try {
        $values = Import-EnvFile -AllowIncomplete:$AllowIncomplete
    } catch {
        if ($AllowIncomplete) {
            return @{}
        }
        throw
    }
    if (-not $AllowIncomplete) {
        foreach ($required in @('ACME_DIRECTORY','DOMAINS')) {
            $value = if ($values.ContainsKey($required)) { [string]$values[$required] } else { '' }
            if ([string]::IsNullOrWhiteSpace($value)) {
                throw "Missing config: $required"
            }
        }
    }
    return $values
}
