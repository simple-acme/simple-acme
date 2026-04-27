Import-Module "$PSScriptRoot/../core/Simple-Acme-Reconciler.psm1" -Force

Describe 'Simple ACME reconcile preflight' {
    It 'passes when wacs exists in package root and env values are valid' {
        $scriptFile = Join-Path $TestDrive 'hook.ps1'
        'Write-Host ok' | Set-Content -Path $scriptFile -Encoding UTF8
        $moduleRoot = Split-Path $PSScriptRoot -Parent
        $rootWacs = Join-Path $moduleRoot 'wacs.exe'
        $rootWacsBackup = Join-Path $TestDrive 'wacs.backup.exe'
        $hadOriginalWacs = Test-Path -LiteralPath $rootWacs
        if ($hadOriginalWacs) {
            Copy-Item -LiteralPath $rootWacs -Destination $rootWacsBackup -Force
        }
        'stub' | Set-Content -Path $rootWacs -Encoding UTF8

        try {
            $result = Assert-ReconcilePreflight -EnvValues @{
                ACME_DIRECTORY = 'https://acme.example.com/directory'
                ACME_KID = 'kid'
                ACME_HMAC_SECRET = 'secret'
                DOMAINS = 'example.com,www.example.com'
                ACME_SOURCE_PLUGIN = 'manual'
                ACME_ORDER_PLUGIN = 'single'
                ACME_STORE_PLUGIN = 'certificatestore'
                ACME_VALIDATION_MODE = 'none'
                ACME_INSTALLATION_PLUGINS = 'script'
                ACME_SCRIPT_PATH = $scriptFile
                ACME_SCRIPT_PARAMETERS = "'default' {RenewalId} {CertThumbprint} {OldCertThumbprint}"
                ACME_WACS_VERSION = '2.3.0'
            }

            $result.WacsPath | Should -Be (Convert-Path -LiteralPath $rootWacs)
            $result.DomainCount | Should -Be 2
            $result.ScriptPath | Should -Be $scriptFile
        } finally {
            Remove-Item -LiteralPath $rootWacs -Force -ErrorAction SilentlyContinue
            if ($hadOriginalWacs) {
                Move-Item -LiteralPath $rootWacsBackup -Destination $rootWacs -Force
            }
        }
    }

    It 'fails when wacs is missing' {
        $scriptFile = Join-Path $TestDrive 'hook.ps1'
        'Write-Host ok' | Set-Content -Path $scriptFile -Encoding UTF8
        Mock Test-Path { $false } -ParameterFilter { $PathType -eq 'Leaf' }
        Mock Get-Command { $null } -ParameterFilter { $Name -in @('wacs','wacs.exe') }

        {
            Assert-ReconcilePreflight -EnvValues @{
                ACME_DIRECTORY = 'https://acme.example.com/directory'
                ACME_KID = 'kid'
                ACME_HMAC_SECRET = 'secret'
                DOMAINS = 'example.com'
                ACME_SOURCE_PLUGIN = 'manual'
                ACME_ORDER_PLUGIN = 'single'
                ACME_STORE_PLUGIN = 'certificatestore'
                ACME_VALIDATION_MODE = 'none'
                ACME_INSTALLATION_PLUGINS = 'script'
                ACME_SCRIPT_PATH = $scriptFile
                ACME_SCRIPT_PARAMETERS = "'default' {RenewalId} {CertThumbprint} {OldCertThumbprint}"
                ACME_WACS_VERSION = '2.3.0'
            }
        } | Should -Throw '*simple-acme executable not found*'
    }

    It 'fails when script path is not absolute' {
        $moduleRoot = Split-Path $PSScriptRoot -Parent
        $rootWacs = Join-Path $moduleRoot 'wacs.exe'
        $rootWacsBackup = Join-Path $TestDrive 'wacs.backup.exe'
        $hadOriginalWacs = Test-Path -LiteralPath $rootWacs
        if ($hadOriginalWacs) {
            Copy-Item -LiteralPath $rootWacs -Destination $rootWacsBackup -Force
        }
        'stub' | Set-Content -Path $rootWacs -Encoding UTF8
        try {
        {
            Assert-ReconcilePreflight -EnvValues @{
                ACME_DIRECTORY = 'https://acme.example.com/directory'
                ACME_KID = 'kid'
                ACME_HMAC_SECRET = 'secret'
                DOMAINS = 'example.com'
                ACME_SOURCE_PLUGIN = 'manual'
                ACME_ORDER_PLUGIN = 'single'
                ACME_STORE_PLUGIN = 'certificatestore'
                ACME_VALIDATION_MODE = 'none'
                ACME_INSTALLATION_PLUGINS = 'script'
                ACME_SCRIPT_PATH = '.\relative.ps1'
                ACME_SCRIPT_PARAMETERS = "'default' {RenewalId} {CertThumbprint} {OldCertThumbprint}"
                ACME_WACS_VERSION = '2.3.0'
            }
            } | Should -Throw '*Script installation path does not exist*'
        } finally {
            Remove-Item -LiteralPath $rootWacs -Force -ErrorAction SilentlyContinue
            if ($hadOriginalWacs) {
                Move-Item -LiteralPath $rootWacsBackup -Destination $rootWacs -Force
            }
        }
    }
}
