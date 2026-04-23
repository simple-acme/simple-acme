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
            $json = Get-Content -Path $path -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
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
            HasValidationNone = $true
            HasScriptInstallation = $true
            ScriptPaths = @('C:\wrong.ps1')
        }
        $envValues = @{
            DOMAINS = 'example.com'
            ACME_DIRECTORY = 'https://acme.networking4all.com/dv'
            ACME_KID = 'kid-1'
            ACME_SCRIPT_PATH = 'C:\correct.ps1'
        }

        $result = Compare-RenewalWithEnv -RenewalSummary $summary -EnvValues $envValues
        if ($result.Matches) { throw 'Expected mismatch.' }
        if (-not ($result.Mismatches -contains 'Script path')) { throw 'Expected Script path mismatch.' }
    }
}
