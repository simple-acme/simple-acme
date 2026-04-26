Import-Module "$PSScriptRoot/../core/Simple-Acme-Reconciler.psm1" -Force

Describe 'Simple ACME reconcile preflight' {
    It 'passes when wacs exists and env values are valid' {
        $scriptFile = Join-Path $TestDrive 'hook.ps1'
        'Write-Host ok' | Set-Content -Path $scriptFile -Encoding UTF8
        Mock Get-Command { [pscustomobject]@{ Source = 'C:\tools\wacs.exe' } } -ParameterFilter { $Name -eq 'wacs' }

        $result = Assert-ReconcilePreflight -EnvValues @{
            ACME_DIRECTORY = 'https://acme.example.com/directory'
            ACME_KID = 'kid'
            ACME_HMAC_SECRET = 'secret'
            DOMAINS = 'example.com,www.example.com'
            ACME_SCRIPT_PATH = $scriptFile
            ACME_SCRIPT_PARAMETERS = "'default' {RenewalId} {CertThumbprint} {OldCertThumbprint}"
        }

        $result.WacsPath | Should -Be 'C:\tools\wacs.exe'
        $result.DomainCount | Should -Be 2
        $result.ScriptPath | Should -Be $scriptFile
    }

    It 'fails when wacs is missing' {
        $scriptFile = Join-Path $TestDrive 'hook.ps1'
        'Write-Host ok' | Set-Content -Path $scriptFile -Encoding UTF8
        Mock Get-Command { $null } -ParameterFilter { $Name -eq 'wacs' }

        {
            Assert-ReconcilePreflight -EnvValues @{
                ACME_DIRECTORY = 'https://acme.example.com/directory'
                ACME_KID = 'kid'
                ACME_HMAC_SECRET = 'secret'
                DOMAINS = 'example.com'
                ACME_SCRIPT_PATH = $scriptFile
                ACME_SCRIPT_PARAMETERS = "'default' {RenewalId} {CertThumbprint} {OldCertThumbprint}"
            }
        } | Should -Throw '*wacs*not found*'
    }

    It 'fails when script path is not absolute' {
        Mock Get-Command { [pscustomobject]@{ Source = 'C:\tools\wacs.exe' } } -ParameterFilter { $Name -eq 'wacs' }
        {
            Assert-ReconcilePreflight -EnvValues @{
                ACME_DIRECTORY = 'https://acme.example.com/directory'
                ACME_KID = 'kid'
                ACME_HMAC_SECRET = 'secret'
                DOMAINS = 'example.com'
                ACME_SCRIPT_PATH = '.\relative.ps1'
                ACME_SCRIPT_PARAMETERS = "'default' {RenewalId} {CertThumbprint} {OldCertThumbprint}"
            }
        } | Should -Throw '*absolute path*'
    }
}
