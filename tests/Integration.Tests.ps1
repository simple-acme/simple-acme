Set-StrictMode -Version Latest

function Invoke-TestIntegration {
    param([scriptblock]$Assert)

    & $Assert 'env-loader exports Import-EnvFile' {
        Import-Module "$PSScriptRoot/../core/Env-Loader.psm1" -Force
        (Get-Command Import-EnvFile -ErrorAction Stop) | Out-Null
    }

    & $Assert 'http-listener module imports' {
        Import-Module "$PSScriptRoot/../core/Http-Listener.psm1" -Force
        (Get-Command Start-CertificaatHttpListener -ErrorAction Stop) | Out-Null
    }
}
