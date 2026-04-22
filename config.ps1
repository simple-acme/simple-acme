Set-StrictMode -Version Latest

Import-Module "$PSScriptRoot/core/Env-Loader.psm1" -Force

function Initialize-CertificaatConfig {
    $values = Import-EnvFile
    return $values
}
