Set-StrictMode -Version Latest

Import-Module "$PSScriptRoot/core/Env-Loader.psm1" -Force

function Initialize-CertificateConfig {
    $values = Import-EnvFile
    return $values
}
