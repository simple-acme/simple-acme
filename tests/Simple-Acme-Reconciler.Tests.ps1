Import-Module "$PSScriptRoot/../core/Simple-Acme-Reconciler.psm1" -Force

function Invoke-TestSimpleAcmeReconciler {
    param([scriptblock]$Assert)

    & $Assert 'normalizes domains' {
        $actual = Get-NormalizedDomains -Domains 'WWW.Example.com, example.com ,api.example.com'
        if (($actual -join ',') -ne 'api.example.com,example.com,www.example.com') {
            throw "Unexpected domains: $($actual -join ',')"
        }
    }

    & $Assert 'settings merge writes scheduled task values' {
        $root = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        try {
            $path = Join-Path $root 'settings.json'
            @{ Existing = @{ Keep = 'yes' } } | ConvertTo-Json -Depth 5 | Set-Content -Path $path -Encoding UTF8
            Ensure-SimpleAcmeSettings -SimpleAcmeDir $root
            $jsonObject = Get-Content -Path $path -Raw -Encoding UTF8 | ConvertFrom-Json
            $json = ConvertTo-HashtableRecursive -InputObject $jsonObject
            if ($json.Existing.Keep -ne 'yes') { throw 'Existing key not preserved.' }
            if ($json.ScheduledTask.RenewalDays -ne 199) { throw 'RenewalDays not set.' }
            if ($json.ScheduledTask.RenewalMinimumValidDays -ne 16) { throw 'RenewalMinimumValidDays not set.' }
        } finally {
            Remove-Item -Path $root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    & $Assert 'compare detects mismatch when script path differs' {
        $summary = [pscustomobject]@{
            Hosts = @('example.com')
            BaseUri = 'https://acme.networking4all.com/dv'
            EabKid = 'kid-1'
            SourcePlugin = 'manual'
            OrderPlugin = 'single'
            StorePlugin = 'certificatestore'
            AccountName = ''
            HasValidationNone = $true
            HasScriptInstallation = $true
            InstallationPlugins = @('script')
            ScriptPaths = @('C:\wrong.ps1')
            StorePlugins = @('certificatestore')
        }
        $envValues = @{
            DOMAINS = 'example.com'
            ACME_DIRECTORY = 'https://acme.networking4all.com/dv'
            ACME_KID = 'kid-1'
            ACME_SCRIPT_PATH = 'C:\correct.ps1'
            ACME_SOURCE_PLUGIN = 'manual'
            ACME_ORDER_PLUGIN = 'single'
            ACME_STORE_PLUGIN = 'certificatestore'
            ACME_VALIDATION_MODE = 'none'
            ACME_INSTALLATION_PLUGINS = 'script'
            ACME_ACCOUNT_NAME = ''
        }

        $result = Compare-RenewalWithEnv -RenewalSummary $summary -EnvValues $envValues
        if ($result.Matches) { throw 'Expected mismatch.' }
        if (-not ($result.Mismatches -contains 'Script path')) { throw 'Expected Script path mismatch.' }
    }

    & $Assert 'exact domain set matching rejects partial overlap' {
        if (-not (Test-ExactDomainSetMatch -Requested @('a.example.com','b.example.com') -Actual @('b.example.com','a.example.com'))) {
            throw 'Expected exact set match.'
        }
        if (Test-ExactDomainSetMatch -Requested @('a.example.com') -Actual @('a.example.com','b.example.com')) {
            throw 'Expected partial overlap to fail exact matching.'
        }
    }

    & $Assert 'installation plugins are parsed and normalized' {
        $plugins = Get-InstallationPlugins -EnvValues @{ ACME_INSTALLATION_PLUGINS = 'script, iis,script' }
        if (($plugins -join ',') -ne 'iis,script') {
            throw "Unexpected plugins: $($plugins -join ',')"
        }
    }

    & $Assert 'config hash is deterministic for equivalent values' {
        $envA = @{
            DOMAINS = 'b.example.com, a.example.com'
            ACME_VALIDATION_MODE = 'none'
            ACME_CSR_ALGORITHM = 'ec'
            ACME_KEY_TYPE = 'ec'
            ACME_SCRIPT_PATH = 'C:\scripts\install.ps1'
            ACME_INSTALLATION_PLUGINS = 'script,iis'
            ACME_STORE_PLUGIN = 'certificatestore'
        }
        $envB = @{
            DOMAINS = 'a.example.com,b.example.com'
            ACME_VALIDATION_MODE = 'none'
            ACME_CSR_ALGORITHM = 'ec'
            ACME_KEY_TYPE = 'ec'
            ACME_SCRIPT_PATH = 'C:\scripts\install.ps1'
            ACME_INSTALLATION_PLUGINS = 'iis,script'
            ACME_STORE_PLUGIN = 'certificatestore'
        }

        $hashA = New-ReconcileConfigHash -EnvValues $envA
        $hashB = New-ReconcileConfigHash -EnvValues $envB
        if ($hashA -ne $hashB) {
            throw "Expected deterministic hash but got '$hashA' and '$hashB'."
        }
    }

    & $Assert 'wacs resolver prefers ACME_WACS_PATH and supports package-local exe names' {
        $root = Split-Path $PSScriptRoot -Parent
        $wacsPath = Join-Path $root 'wacs.exe'
        $simpleAcmePath = Join-Path $root 'simple-acme.exe'
        $hadWacs = Test-Path -LiteralPath $wacsPath
        $hadSimpleAcme = Test-Path -LiteralPath $simpleAcmePath
        $backupWacs = ''
        $backupSimpleAcme = ''

        try {
            if ($hadWacs) {
                $backupWacs = [System.IO.File]::ReadAllText($wacsPath, [System.Text.Encoding]::UTF8)
            }
            if ($hadSimpleAcme) {
                $backupSimpleAcme = [System.IO.File]::ReadAllText($simpleAcmePath, [System.Text.Encoding]::UTF8)
            }

            [System.IO.File]::WriteAllText($simpleAcmePath, 'placeholder', [System.Text.Encoding]::UTF8)
            $resolvedPackageLocal = Resolve-WacsExecutablePath -EnvValues @{}
            if ($resolvedPackageLocal -ne $simpleAcmePath) {
                throw "Expected package-local simple-acme.exe, got '$resolvedPackageLocal'"
            }

            [System.IO.File]::WriteAllText($wacsPath, 'placeholder', [System.Text.Encoding]::UTF8)
            $resolvedWacs = Resolve-WacsExecutablePath -EnvValues @{}
            if ($resolvedWacs -ne $wacsPath) {
                throw "Expected package-local wacs.exe, got '$resolvedWacs'"
            }

            $resolvedOverride = Resolve-WacsExecutablePath -EnvValues @{ ACME_WACS_PATH = $simpleAcmePath }
            if ($resolvedOverride -ne $simpleAcmePath) {
                throw "Expected ACME_WACS_PATH override, got '$resolvedOverride'"
            }
        } finally {
            if ($hadWacs) {
                [System.IO.File]::WriteAllText($wacsPath, $backupWacs, [System.Text.Encoding]::UTF8)
            } elseif (Test-Path -LiteralPath $wacsPath) {
                Remove-Item -LiteralPath $wacsPath -Force -ErrorAction SilentlyContinue
            }

            if ($hadSimpleAcme) {
                [System.IO.File]::WriteAllText($simpleAcmePath, $backupSimpleAcme, [System.Text.Encoding]::UTF8)
            } elseif (Test-Path -LiteralPath $simpleAcmePath) {
                Remove-Item -LiteralPath $simpleAcmePath -Force -ErrorAction SilentlyContinue
            }
        }
    }
}
