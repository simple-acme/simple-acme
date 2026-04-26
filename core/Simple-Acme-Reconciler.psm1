$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-NormalizedDomains {
    param([Parameter(Mandatory)][string]$Domains)

    return @(
        $Domains -split ',' |
            ForEach-Object { $_.Trim().ToLowerInvariant() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Sort-Object -Unique
    )
}

function Get-RenewalFiles {
    param([string]$SimpleAcmeDir = (Join-Path $env:ProgramData 'simple-acme'))

    if ([string]::IsNullOrWhiteSpace($SimpleAcmeDir) -or -not (Test-Path -LiteralPath $SimpleAcmeDir)) {
        return @()
    }

    return @(Get-ChildItem -LiteralPath $SimpleAcmeDir -Filter 'acme-v02.api*.renewal.json' -File -ErrorAction SilentlyContinue)
}

function Find-PropertyValues {
    param(
        [Parameter(Mandatory)]$InputObject,
        [Parameter(Mandatory)][string[]]$Names
    )

    $matches = New-Object System.Collections.Generic.List[object]

    function Visit-Node {
        param($Node)
        if ($null -eq $Node) { return }

        if ($Node -is [System.Collections.IDictionary]) {
            foreach ($key in $Node.Keys) {
                if ($Names -contains [string]$key) {
                    $matches.Add($Node[$key])
                }
                Visit-Node -Node $Node[$key]
            }
            return
        }

        if ($Node -is [System.Management.Automation.PSCustomObject]) {
            foreach ($property in $Node.PSObject.Properties) {
                if ($Names -contains [string]$property.Name) {
                    $matches.Add($property.Value)
                }
                Visit-Node -Node $property.Value
            }
            return
        }

        if ($Node -is [System.Collections.IEnumerable] -and -not ($Node -is [string])) {
            foreach ($item in $Node) {
                Visit-Node -Node $item
            }
        }
    }

    Visit-Node -Node $InputObject
    return @($matches)
}

function Get-RenewalHosts {
    param([Parameter(Mandatory)]$Renewal)

    $hostValues = New-Object System.Collections.Generic.List[string]
    $hostCandidates = Find-PropertyValues -InputObject $Renewal -Names @('Host','Hosts','Identifiers','Identifier')
    foreach ($candidate in $hostCandidates) {
        if ($candidate -is [string]) {
            foreach ($part in ($candidate -split ',')) {
                $v = $part.Trim().ToLowerInvariant()
                if (-not [string]::IsNullOrWhiteSpace($v)) { $hostValues.Add($v) }
            }
        } elseif ($candidate -is [System.Collections.IEnumerable] -and -not ($candidate -is [string])) {
            foreach ($item in $candidate) {
                if ($item -is [string]) {
                    $v = $item.Trim().ToLowerInvariant()
                    if (-not [string]::IsNullOrWhiteSpace($v)) { $hostValues.Add($v) }
                }
            }
        }
    }

    return @($hostValues | Sort-Object -Unique)
}

function Get-RenewalSummary {
    param([Parameter(Mandatory)][System.IO.FileInfo]$File)

    $renewal = Get-Content -LiteralPath $File.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $baseUriCandidates = Find-PropertyValues -InputObject $renewal -Names @('BaseUri')
    $kidCandidates = Find-PropertyValues -InputObject $renewal -Names @('KeyIdentifier','Kid','EabKeyIdentifier')
    $validationCandidates = Find-PropertyValues -InputObject $renewal -Names @('Plugin','Name')
    $scriptCandidates = Find-PropertyValues -InputObject $renewal -Names @('Script','ScriptFileName','Path')

    $hosts = Get-RenewalHosts -Renewal $renewal

    [pscustomobject]@{
        File             = $File
        Renewal          = $renewal
        Hosts            = $hosts
        BaseUri          = ($baseUriCandidates | Where-Object { $_ -is [string] } | Select-Object -First 1)
        EabKid           = ($kidCandidates | Where-Object { $_ -is [string] } | Select-Object -First 1)
        HasValidationNone = @($validationCandidates | Where-Object { $_ -is [string] -and $_.ToLowerInvariant() -eq 'none' }).Count -gt 0
        HasScriptInstallation = @($validationCandidates | Where-Object { $_ -is [string] -and $_.ToLowerInvariant() -eq 'script' }).Count -gt 0
        ScriptPaths      = @($scriptCandidates | Where-Object { $_ -is [string] })
    }
}

function Compare-RenewalWithEnv {
    param(
        [Parameter(Mandatory)]$RenewalSummary,
        [Parameter(Mandatory)][hashtable]$EnvValues
    )

    $expectedHosts = Get-NormalizedDomains -Domains $EnvValues.DOMAINS
    $actualHosts = @($RenewalSummary.Hosts | Sort-Object -Unique)
    $expectedScriptPath = [string]$EnvValues.ACME_SCRIPT_PATH

    $mismatches = New-Object System.Collections.Generic.List[string]

    if ([string]$RenewalSummary.BaseUri -ne [string]$EnvValues.ACME_DIRECTORY) {
        $mismatches.Add('BaseUri')
    }

    if (($expectedHosts -join ',') -ne ($actualHosts -join ',')) {
        $mismatches.Add('Domains')
    }

    if ([string]$RenewalSummary.EabKid -ne [string]$EnvValues.ACME_KID) {
        $mismatches.Add('EAB kid')
    }

    if (-not $RenewalSummary.HasValidationNone) {
        $mismatches.Add('Validation plugin none')
    }

    if (-not $RenewalSummary.HasScriptInstallation) {
        $mismatches.Add('Installation plugin script')
    }

    $normalizedScriptPaths = @($RenewalSummary.ScriptPaths | ForEach-Object { [string]$_ })
    if (-not ($normalizedScriptPaths -contains $expectedScriptPath)) {
        $mismatches.Add('Script path')
    }

    return [pscustomobject]@{
        Matches    = ($mismatches.Count -eq 0)
        Mismatches = @($mismatches)
    }
}

function Assert-ReconcilePreflight {
    param([Parameter(Mandatory)][hashtable]$EnvValues)

    $wacsCommand = Get-Command 'wacs' -ErrorAction SilentlyContinue
    if ($null -eq $wacsCommand) {
        throw "Required executable 'wacs' was not found on PATH. Install simple-acme/wacs and retry."
    }

    $missing = @()
    foreach ($key in @('ACME_DIRECTORY','ACME_KID','ACME_HMAC_SECRET','DOMAINS','ACME_SCRIPT_PATH')) {
        if (-not $EnvValues.ContainsKey($key) -or [string]::IsNullOrWhiteSpace([string]$EnvValues[$key])) {
            $missing += $key
        }
    }
    if ($missing.Count -gt 0) {
        throw "Missing required environment values for reconcile: $($missing -join ', ')"
    }

    $scriptPath = [string]$EnvValues.ACME_SCRIPT_PATH
    if (-not [System.IO.Path]::IsPathRooted($scriptPath)) {
        throw "ACME_SCRIPT_PATH must be an absolute path. Current value: '$scriptPath'"
    }
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        throw "ACME_SCRIPT_PATH does not exist: '$scriptPath'"
    }

    $domains = Get-NormalizedDomains -Domains ([string]$EnvValues.DOMAINS)
    if ($domains.Count -eq 0) {
        throw "DOMAINS did not contain any valid hostnames. Current value: '$($EnvValues.DOMAINS)'"
    }

    return [pscustomobject]@{
        WacsPath = [string]$wacsCommand.Source
        DomainCount = $domains.Count
        ScriptPath = $scriptPath
    }
}

function Ensure-SimpleAcmeSettings {
    param([string]$SimpleAcmeDir = (Join-Path $env:ProgramData 'simple-acme'))

    if (-not (Test-Path -LiteralPath $SimpleAcmeDir)) {
        New-Item -ItemType Directory -Path $SimpleAcmeDir -Force | Out-Null
    }

    $settingsPath = Join-Path $SimpleAcmeDir 'settings.json'
    $settings = @{}
    if (Test-Path -LiteralPath $settingsPath) {
        $existing = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
        if ($existing) { $settings = $existing }
    }

    if (-not $settings.ContainsKey('ScheduledTask') -or $null -eq $settings.ScheduledTask) {
        $settings.ScheduledTask = @{}
    }

    $settings.ScheduledTask.RenewalDays = 199
    $settings.ScheduledTask.RenewalMinimumValidDays = 16

    $settings | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $settingsPath -Encoding UTF8
}

function Invoke-WacsIssue {
    param([Parameter(Mandatory)][hashtable]$EnvValues)

    $args = @(
        '--accepttos',
        '--baseuri', [string]$EnvValues.ACME_DIRECTORY,
        '--eab-key-identifier', [string]$EnvValues.ACME_KID,
        '--eab-key', [string]$EnvValues.ACME_HMAC_SECRET,
        '--validation', 'none',
        '--host', [string]$EnvValues.DOMAINS,
        '--store', 'certificatestore',
        '--installation', 'script',
        '--script', [string]$EnvValues.ACME_SCRIPT_PATH,
        '--scriptparameters', "{RenewalId} '{CertCommonName}' {CertThumbprint} {OldCertThumbprint} '{CacheFile}' '{CachePassword}' '{StorePath}' {StoreType}"
    )

    & wacs @args
    if ($LASTEXITCODE -ne 0) { throw "wacs issuance failed with exit code $LASTEXITCODE" }
}

function Write-ReconcileLog {
    param(
        [Parameter(Mandatory)][ValidateSet('create','update','no-op')][string]$Action,
        [Parameter(Mandatory)][string[]]$Domains,
        [Parameter(Mandatory)][ValidateSet('success','failure')][string]$Result,
        [Parameter(Mandatory)][string]$Message
    )

    $timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    Write-Host ("{0} action={1} domains={2} result={3} message={4}" -f $timestamp, $Action, ($Domains -join ','), $Result, $Message)
}

function Invoke-SimpleAcmeReconcile {
    param(
        [Parameter(Mandatory)][hashtable]$EnvValues,
        [switch]$SkipWacs
    )

    $domains = Get-NormalizedDomains -Domains $EnvValues.DOMAINS
    if ($domains.Count -eq 0) {
        throw 'DOMAINS did not contain any valid host names.'
    }

    Ensure-SimpleAcmeSettings

    $allRenewalFiles = Get-RenewalFiles
    $matching = @()
    foreach ($file in $allRenewalFiles) {
        $summary = Get-RenewalSummary -File $file
        if (@($summary.Hosts | Where-Object { $domains -contains $_ }).Count -gt 0) {
            $matching += ,$summary
        }
    }

    if ($matching.Count -eq 0) {
        if (-not $SkipWacs) {
            Invoke-WacsIssue -EnvValues $EnvValues
            $allRenewalFiles = Get-RenewalFiles
        }

        $postMatch = @()
        foreach ($file in $allRenewalFiles) {
            $summary = Get-RenewalSummary -File $file
            if (@($summary.Hosts | Where-Object { $domains -contains $_ }).Count -gt 0) { $postMatch += ,$summary }
        }

        if ($postMatch.Count -eq 0) {
            Write-ReconcileLog -Action 'create' -Domains $domains -Result 'failure' -Message 'No matching renewal file found after issuance.'
            throw 'No matching renewal file found after issuance.'
        }

        $validation = Compare-RenewalWithEnv -RenewalSummary $postMatch[0] -EnvValues $EnvValues
        if (-not $validation.Matches) {
            Write-ReconcileLog -Action 'create' -Domains $domains -Result 'failure' -Message ("Post-create validation failed: {0}" -f ($validation.Mismatches -join ', '))
            throw "Post-create validation failed: $($validation.Mismatches -join ', ')"
        }

        Write-ReconcileLog -Action 'create' -Domains $domains -Result 'success' -Message 'Initial issuance completed.'
        return 'create'
    }

    if ($matching.Count -gt 1) {
        throw "Multiple renewal entries match requested domains: $($domains -join ', ')"
    }

    $current = $matching[0]
    $compare = Compare-RenewalWithEnv -RenewalSummary $current -EnvValues $EnvValues
    if ($compare.Matches) {
        Write-ReconcileLog -Action 'no-op' -Domains $domains -Result 'success' -Message 'Renewal configuration already matches .env.'
        return 'no-op'
    }

    if (-not $SkipWacs) {
        & wacs --cancel --friendlyname $domains[0]
        if ($LASTEXITCODE -ne 0) { throw "wacs cancel failed with exit code $LASTEXITCODE" }
        Invoke-WacsIssue -EnvValues $EnvValues
    }

    $freshFiles = Get-RenewalFiles
    $postUpdate = @()
    foreach ($file in $freshFiles) {
        $summary = Get-RenewalSummary -File $file
        if (@($summary.Hosts | Where-Object { $domains -contains $_ }).Count -gt 0) { $postUpdate += ,$summary }
    }

    if ($postUpdate.Count -ne 1) {
        Write-ReconcileLog -Action 'update' -Domains $domains -Result 'failure' -Message 'Expected exactly one renewal after update.'
        throw 'Expected exactly one renewal after update.'
    }

    $postCompare = Compare-RenewalWithEnv -RenewalSummary $postUpdate[0] -EnvValues $EnvValues
    if (-not $postCompare.Matches) {
        Write-ReconcileLog -Action 'update' -Domains $domains -Result 'failure' -Message ("Post-update validation failed: {0}" -f ($postCompare.Mismatches -join ', '))
        throw "Post-update validation failed: $($postCompare.Mismatches -join ', ')"
    }

    Write-ReconcileLog -Action 'update' -Domains $domains -Result 'success' -Message 'Renewal was recreated safely.'
    return 'update'
}

Export-ModuleMember -Function @(
    'Compare-RenewalWithEnv',
    'Assert-ReconcilePreflight',
    'Ensure-SimpleAcmeSettings',
    'Get-NormalizedDomains',
    'Get-RenewalFiles',
    'Get-RenewalSummary',
    'Invoke-SimpleAcmeReconcile',
    'Invoke-WacsIssue',
    'Write-ReconcileLog'
)
