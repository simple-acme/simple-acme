$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module "$PSScriptRoot/Native-Process.psm1" -Force


function ConvertTo-HashtableRecursive {
    param($InputObject)

    if ($null -eq $InputObject) { return $null }

    if ($InputObject -is [System.Collections.IEnumerable] -and -not ($InputObject -is [string]) -and -not ($InputObject -is [System.Management.Automation.PSCustomObject])) {
        $array = @()
        foreach ($item in $InputObject) {
            $array += ConvertTo-HashtableRecursive -InputObject $item
        }
        return $array
    }

    if ($InputObject -is [System.Management.Automation.PSCustomObject]) {
        $hash = @{}
        foreach ($prop in $InputObject.PSObject.Properties) {
            $hash[$prop.Name] = ConvertTo-HashtableRecursive -InputObject $prop.Value
        }
        return $hash
    }

    return $InputObject
}

function Get-NormalizedDomains {
    param([Parameter(Mandatory)][string]$Domains)

    return @(
        $Domains -split ',' |
            ForEach-Object { $_.Trim().ToLowerInvariant() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Sort-Object -Unique
    )
}


function Resolve-WacsExecutablePath {
    param([hashtable]$EnvValues)

    $projectRoot = Split-Path $PSScriptRoot -Parent
    $candidates = @()
    if ($null -ne $EnvValues -and $EnvValues.ContainsKey('ACME_WACS_PATH') -and -not [string]::IsNullOrWhiteSpace([string]$EnvValues.ACME_WACS_PATH)) {
        $candidates += [string]$EnvValues.ACME_WACS_PATH
    } elseif (-not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable('ACME_WACS_PATH'))) {
        $candidates += [string][Environment]::GetEnvironmentVariable('ACME_WACS_PATH')
    }
    $candidates += (Join-Path $projectRoot 'wacs.exe')
    $candidates += (Join-Path $projectRoot 'simple-acme.exe')

    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        $resolvedCandidate = $candidate
        if (-not [System.IO.Path]::IsPathRooted([string]$resolvedCandidate)) {
            $resolvedCandidate = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $resolvedCandidate))
        }
        if (Test-Path -LiteralPath $resolvedCandidate -PathType Leaf) {
            return $resolvedCandidate
        }
    }

    $wacsCommand = Get-Command 'wacs' -ErrorAction SilentlyContinue
    if ($null -ne $wacsCommand -and -not [string]::IsNullOrWhiteSpace([string]$wacsCommand.Source)) {
        if ([System.IO.Path]::IsPathRooted([string]$wacsCommand.Source)) {
            return [string]$wacsCommand.Source
        }
    }

    throw "Unable to resolve simple-acme executable. Checked ACME_WACS_PATH, '$projectRoot\wacs.exe', '$projectRoot\simple-acme.exe', and PATH."
}

function Test-ValidDomainName {
    param([Parameter(Mandatory)][string]$Domain)
    $candidate = $Domain.Trim().ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($candidate)) { return $false }
    if ($candidate.Length -gt 253) { return $false }
    if ($candidate -notmatch '^(?=.{1,253}$)(?!-)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$') { return $false }
    return $true
}

function Get-RenewalFiles {
    param([string]$SimpleAcmeDir = (Join-Path $env:ProgramData 'simple-acme'))

    if ([string]::IsNullOrWhiteSpace($SimpleAcmeDir) -or -not (Test-Path -LiteralPath $SimpleAcmeDir)) {
        return @()
    }

    return @(Get-ChildItem -LiteralPath $SimpleAcmeDir -Filter '*.renewal.json' -File -ErrorAction SilentlyContinue)
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


function Get-NestedValue {
    param([Parameter(Mandatory)]$InputObject,[Parameter(Mandatory)][string[]]$Path)
    $current = $InputObject
    foreach ($part in $Path) {
        if ($null -eq $current) { return $null }
        $prop = $current.PSObject.Properties[$part]
        if ($null -eq $prop) { return $null }
        $current = $prop.Value
    }
    return $current
}

function Get-RenewalSummarySafe {
    param([Parameter(Mandatory)][System.IO.FileInfo]$File)
    try { return Get-RenewalSummary -File $File }
    catch { Write-Warning "Skipping malformed renewal JSON '$($File.FullName)': $($_.Exception.Message)"; return $null }
}

function Get-RenewalSummary {
    param([Parameter(Mandatory)][System.IO.FileInfo]$File)

    try {
        $renewal = Get-Content -LiteralPath $File.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
        throw "Failed to parse renewal JSON '$($File.FullName)': $($_.Exception.Message)"
    }
    if ($null -eq $renewal) {
        throw "Renewal JSON '$($File.FullName)' parsed as null."
    }
    $baseUriCandidates = Find-PropertyValues -InputObject $renewal -Names @('BaseUri')
    $kidCandidates = Find-PropertyValues -InputObject $renewal -Names @('KeyIdentifier','Kid','EabKeyIdentifier')
    $validationCandidates = Find-PropertyValues -InputObject $renewal -Names @('Plugin','Name','ValidationPlugin')
    $storeCandidates = Find-PropertyValues -InputObject $renewal -Names @('StorePlugin','StoreType','Store')
    $installationCandidates = Find-PropertyValues -InputObject $renewal -Names @('InstallationPlugin','InstallationPlugins','Installation')
    $accountCandidates = Find-PropertyValues -InputObject $renewal -Names @('Account','AccountName')
    $sourceCandidates = Find-PropertyValues -InputObject $renewal -Names @('SourcePlugin','Source')
    $orderCandidates = Find-PropertyValues -InputObject $renewal -Names @('OrderPlugin','Order')
    $renewalIdCandidates = Find-PropertyValues -InputObject $renewal -Names @('Id','RenewalId')
    $scriptCandidates = Find-PropertyValues -InputObject $renewal -Names @('Script','ScriptFileName')
    $scriptParameterCandidates = Find-PropertyValues -InputObject $renewal -Names @('ScriptParameters','Parameters')
    $csrCandidates = Find-PropertyValues -InputObject $renewal -Names @('CsrPlugin','Csr')
    $keyTypeCandidates = Find-PropertyValues -InputObject $renewal -Names @('KeyType','KeyAlgorithm','Algorithm')

    $hosts = Get-RenewalHosts -Renewal $renewal

    $normalizedValidationCandidates = @($validationCandidates | Where-Object { $_ -is [string] } | ForEach-Object { $_.Trim().ToLowerInvariant() })
    $normalizedStoreCandidates = @($storeCandidates | Where-Object { $_ -is [string] } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
    $normalizedInstallCandidates = @($installationCandidates | Where-Object { $_ -is [string] } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)

    $resolvedRenewalId = ($renewalIdCandidates | Where-Object { $_ -is [string] } | Select-Object -First 1)
    $resolvedSourcePlugin = ($sourceCandidates | Where-Object { $_ -is [string] } | Select-Object -First 1)
    $resolvedOrderPlugin = ($orderCandidates | Where-Object { $_ -is [string] } | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace([string]$resolvedRenewalId)) {
        throw "Renewal JSON '$($File.FullName)' did not contain a usable renewal identifier."
    }
    if ([string]::IsNullOrWhiteSpace([string]$resolvedSourcePlugin)) {
        throw "Renewal JSON '$($File.FullName)' did not contain source plugin metadata."
    }
    if ([string]::IsNullOrWhiteSpace([string]$resolvedOrderPlugin)) {
        throw "Renewal JSON '$($File.FullName)' did not contain order plugin metadata."
    }

    [pscustomobject]@{
        File             = $File
        Renewal          = $renewal
        RenewalId        = $resolvedRenewalId
        Hosts            = $hosts
        BaseUri          = ($baseUriCandidates | Where-Object { $_ -is [string] } | Select-Object -First 1)
        EabKid           = ($kidCandidates | Where-Object { $_ -is [string] } | Select-Object -First 1)
        SourcePlugin     = $resolvedSourcePlugin
        OrderPlugin      = $resolvedOrderPlugin
        StorePlugin      = ($normalizedStoreCandidates | Select-Object -First 1)
        StorePlugins     = $normalizedStoreCandidates
        InstallationPlugins = $normalizedInstallCandidates
        AccountName      = ($accountCandidates | Where-Object { $_ -is [string] } | Select-Object -First 1)
        HasValidationNone = @($normalizedValidationCandidates | Where-Object { $_ -eq 'none' }).Count -gt 0
        HasScriptInstallation = @($normalizedInstallCandidates | Where-Object { $_ -eq 'script' }).Count -gt 0
        ScriptPaths      = @($scriptCandidates | Where-Object { $_ -is [string] })
        ScriptParameters = @($scriptParameterCandidates | Where-Object { $_ -is [string] })
        CsrPlugin        = ($csrCandidates | Where-Object { $_ -is [string] } | Select-Object -First 1)
        KeyType          = ($keyTypeCandidates | Where-Object { $_ -is [string] } | Select-Object -First 1)
    }
}

function Get-NormalizedCsvValues {
    param([string]$InputText)
    if ([string]::IsNullOrWhiteSpace($InputText)) { return @() }
    return @(
        $InputText -split ',' |
            ForEach-Object { $_.Trim().ToLowerInvariant() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Sort-Object -Unique
    )
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
    if ([string]$RenewalSummary.SourcePlugin -ne 'manual') {
        $mismatches.Add('Source plugin')
    }
    if ([string]$RenewalSummary.OrderPlugin -ne [string]$EnvValues.ACME_ORDER_PLUGIN) {
        $mismatches.Add('Order plugin')
    }
    $expectedStores = @('certificatestore')
    $actualStores = @($RenewalSummary.StorePlugins | Sort-Object -Unique)
    if (($expectedStores -join ',') -ne ($actualStores -join ',')) {
        $mismatches.Add('Store plugin')
    }
    if ([string]$RenewalSummary.AccountName -ne [string]$EnvValues.ACME_ACCOUNT_NAME) {
        $mismatches.Add('Account name')
    }

    if (-not $RenewalSummary.HasValidationNone) {
        $mismatches.Add('Validation plugin none')
    }

    $expectedInstallers = @('script')
    $actualInstallers = @($RenewalSummary.InstallationPlugins | Sort-Object -Unique)
    if (($expectedInstallers -join ',') -ne ($actualInstallers -join ',')) {
        $mismatches.Add('Installation plugins')
    }
    $normalizedScriptPaths = @($RenewalSummary.ScriptPaths | ForEach-Object { [string]$_ })
    if (-not ($normalizedScriptPaths -contains $expectedScriptPath)) {
        $mismatches.Add('Script path')
    }
    $normalizedScriptParameters = @($RenewalSummary.ScriptParameters | ForEach-Object { [string]$_ })
    if (-not ($normalizedScriptParameters -contains '{CertThumbprint}')) {
        $mismatches.Add('Script parameters')
    }

    $requestedCsr = (Get-CsrAlgorithms -EnvValues $EnvValues | Select-Object -First 1)
    if (-not [string]::IsNullOrWhiteSpace($requestedCsr) -and -not [string]::IsNullOrWhiteSpace([string]$RenewalSummary.CsrPlugin)) {
        if ([string]$RenewalSummary.CsrPlugin -ne $requestedCsr) {
            $mismatches.Add('CSR plugin')
        }
    }

    if (-not [string]::IsNullOrWhiteSpace([string]$EnvValues.ACME_KEY_TYPE) -and -not [string]::IsNullOrWhiteSpace([string]$RenewalSummary.KeyType)) {
        if ([string]$RenewalSummary.KeyType -ne [string]$EnvValues.ACME_KEY_TYPE) {
            $mismatches.Add('Key type')
        }
    }

    return [pscustomobject]@{
        Matches    = ($mismatches.Count -eq 0)
        Mismatches = @($mismatches)
    }
}

function Assert-ReconcilePreflight {
    param([Parameter(Mandatory)][hashtable]$EnvValues)

    $wacsPath = Resolve-WacsExecutablePath -EnvValues $EnvValues
    $detectedVersion = Get-WacsVersion -EnvValues $EnvValues
    $minimumVersion = [version]'2.2'
    $testedRangeNote = 'Tested with simple-acme/wacs 2.2.x through 2.4.x.'
    if ($detectedVersion -lt $minimumVersion) {
        throw "Unsupported simple-acme/wacs version '$detectedVersion'. Minimum supported version is '$minimumVersion'. $testedRangeNote"
    }

    $missing = @()
    foreach ($key in @('ACME_DIRECTORY','DOMAINS')) {
        if (-not $EnvValues.ContainsKey($key) -or [string]::IsNullOrWhiteSpace([string]$EnvValues[$key])) {
            $missing += $key
        }
    }
    if ($missing.Count -gt 0) {
        throw "Missing required environment values for reconcile: $($missing -join ', ')"
    }

    $defaultScriptPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'Scripts/cert2rds.ps1'
    $scriptPath = if ($EnvValues.ContainsKey('ACME_SCRIPT_PATH') -and -not [string]::IsNullOrWhiteSpace([string]$EnvValues.ACME_SCRIPT_PATH)) { [string]$EnvValues.ACME_SCRIPT_PATH } else { $defaultScriptPath }
    if (-not [System.IO.Path]::IsPathRooted($scriptPath)) {
        $scriptPath = [System.IO.Path]::GetFullPath((Join-Path (Split-Path $PSScriptRoot -Parent) $scriptPath))
    }
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        throw "Script installation path does not exist: '$scriptPath'"
    }
    $EnvValues.ACME_SCRIPT_PATH = $scriptPath
    $EnvValues.ACME_SCRIPT_PARAMETERS = '{CertThumbprint}'
    $requiredRolesRaw = [string]$EnvValues.CERTIFICATE_REQUIRED_WINDOWS_ROLES
    if (-not [string]::IsNullOrWhiteSpace($requiredRolesRaw) -and (Get-Command -Name Get-WindowsFeature -ErrorAction SilentlyContinue)) {
        $requiredRoles = @(
            $requiredRolesRaw -split ',' |
                ForEach-Object { $_.Trim() } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        )
        foreach ($role in $requiredRoles) {
            $feature = Get-WindowsFeature -Name $role -ErrorAction SilentlyContinue
            if ($null -eq $feature -or -not $feature.Installed) {
                throw "Required Windows role/feature '$role' is not installed."
            }
        }
    }

    $domains = Get-NormalizedDomains -Domains ([string]$EnvValues.DOMAINS)
    if ($domains.Count -eq 0) {
        throw "DOMAINS did not contain any valid hostnames. Current value: '$($EnvValues.DOMAINS)'"
    }
    foreach ($domain in $domains) {
        if (-not (Test-ValidDomainName -Domain $domain)) {
            throw "Invalid domain format in DOMAINS: '$domain'"
        }
    }

    return [pscustomobject]@{
        WacsPath = [string]$wacsPath
        WacsVersion = [string]$detectedVersion
        DomainCount = $domains.Count
        ScriptPath = $scriptPath
        InstallationPlugins = @('script')
    }
}

function Ensure-SimpleAcmeSettings {
    param(
        [string]$SimpleAcmeDir = (Join-Path $env:ProgramData 'simple-acme'),
        [hashtable]$EnvValues
    )

    if (-not (Test-Path -LiteralPath $SimpleAcmeDir)) {
        New-Item -ItemType Directory -Path $SimpleAcmeDir -Force | Out-Null
    }

    $settingsPath = Join-Path $SimpleAcmeDir 'settings.json'
    $settings = @{}
    if (Test-Path -LiteralPath $settingsPath) {
        try {
            $existingJson = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $existing = ConvertTo-HashtableRecursive -InputObject $existingJson
        } catch {
            throw "Failed to parse settings JSON '$settingsPath': $($_.Exception.Message)"
        }
        if ($existing) { $settings = $existing }
    }

    if (-not $settings.ContainsKey('ScheduledTask') -or $null -eq $settings.ScheduledTask) {
        $settings.ScheduledTask = @{}
    }

    $settings.ScheduledTask.RenewalDays = 199
    $settings.ScheduledTask.RenewalMinimumValidDays = 16

    if (-not $settings.ContainsKey('Store') -or $null -eq $settings.Store) { $settings.Store = @{} }
    if (-not $settings.Store.ContainsKey('CertificateStore') -or $null -eq $settings.Store.CertificateStore) {
        $settings.Store.CertificateStore = @{}
    }
    $requiresExportable = $false
    if ($null -ne $EnvValues) {
        $targetSystem = [string]$EnvValues.TARGET_SYSTEM
        $targetLocation = [string]$EnvValues.TARGET_LOCATION
        $explicitExportable = [string]$EnvValues.ACME_PRIVATEKEY_EXPORTABLE
        if ($targetSystem -eq 'rds' -or $targetLocation -eq 'cluster-farm' -or $targetLocation -eq 'another-server' -or $explicitExportable -eq 'true') {
            $requiresExportable = $true
        }
    }
    $settings.Store.CertificateStore.PrivateKeyExportable = $requiresExportable

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($settingsPath, ($settings | ConvertTo-Json -Depth 12), $utf8NoBom)
}

function Get-InstallationPlugins {
    param([Parameter(Mandatory)][hashtable]$EnvValues)

    $raw = [string]$EnvValues.ACME_INSTALLATION_PLUGINS
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return @('script')
    }

    $valid = @('script','iis')
    $plugins = Get-NormalizedCsvValues -InputText $raw
    if ($plugins.Count -eq 0) {
        throw 'ACME_INSTALLATION_PLUGINS does not contain any valid values.'
    }

    $unknown = @($plugins | Where-Object { $valid -notcontains $_ })
    if ($unknown.Count -gt 0) {
        throw "ACME_INSTALLATION_PLUGINS contains unsupported values: $($unknown -join ', ')"
    }

    return $plugins
}

function Get-CsrAlgorithms {
    param([Parameter(Mandatory)][hashtable]$EnvValues)

    $preferred = [string]$EnvValues.ACME_CSR_ALGORITHM
    if ([string]::IsNullOrWhiteSpace($preferred)) {
        return @('ec','rsa')
    }

    $normalized = $preferred.Trim().ToLowerInvariant()
    switch ($normalized) {
        'ec' { return @('ec','rsa') }
        'rsa' { return @('rsa') }
        default { throw "Unsupported ACME_CSR_ALGORITHM value '$preferred'. Supported values: ec, rsa." }
    }
}

function Invoke-WacsWithRetry {
    param(
        [Parameter(Mandatory)][string[]]$Args,
        [Parameter(Mandatory)][hashtable]$EnvValues,
        [int]$TimeoutSeconds = 300
    )

    $attempts = 3
    [void][int]::TryParse([string]$EnvValues.ACME_WACS_RETRY_ATTEMPTS, [ref]$attempts)
    if ($attempts -lt 1) { $attempts = 1 }
    $delaySeconds = 2
    [void][int]::TryParse([string]$EnvValues.ACME_WACS_RETRY_DELAY_SECONDS, [ref]$delaySeconds)
    if ($delaySeconds -lt 0) { $delaySeconds = 0 }

    $wacsPath = Resolve-WacsExecutablePath -EnvValues $EnvValues
    if (-not [System.IO.Path]::IsPathRooted([string]$wacsPath)) {
        throw "Resolved wacs path is not absolute: '$wacsPath'"
    }

    $last = $null
    for ($attempt = 1; $attempt -le $attempts; $attempt++) {
        $last = Invoke-NativeProcess -FilePath $wacsPath -ArgumentList $Args -TimeoutSeconds $TimeoutSeconds -FatalPatterns @('(?i)\bfatal\b')
        foreach ($line in $last.OutputLines) { Write-Host ([string]$line) }
        if ($last.Succeeded) { return $last }
        if ($attempt -lt $attempts) {
            $effectiveDelay = [math]::Pow(2, ($attempt - 1)) * $delaySeconds
            Start-Sleep -Seconds ([int][math]::Ceiling($effectiveDelay))
        }
    }

    if ($last.TimedOut) { throw "wacs timed out after $attempts attempt(s)." }
    throw "wacs failed with exit code $($last.ExitCode) after $attempts attempt(s)."
}

function Wait-RenewalFileRemoval {
    param(
        [Parameter(Mandatory)][string]$Path,
        [int]$TimeoutSeconds = 30
    )

    $deadline = (Get-Date).ToUniversalTime().AddSeconds($TimeoutSeconds)
    while ((Get-Date).ToUniversalTime() -lt $deadline) {
        if (-not (Test-Path -LiteralPath $Path)) {
            return
        }
        Start-Sleep -Milliseconds 300
    }
    throw "Timed out waiting for renewal file to be removed: $Path"
}

function New-ReconcileConfigHash {
    param([Parameter(Mandatory)][hashtable]$EnvValues)

    $domains = Get-NormalizedDomains -Domains ([string]$EnvValues.DOMAINS)
    $installers = Get-InstallationPlugins -EnvValues $EnvValues
    $stores = Get-NormalizedCsvValues -InputText ([string]$EnvValues.ACME_STORE_PLUGIN)
    $hashInput = @(
        "domains=$($domains -join ',')"
        "validation=$([string]$EnvValues.ACME_VALIDATION_MODE)"
        "csr=$([string]$EnvValues.ACME_CSR_ALGORITHM)"
        "keytype=$([string]$EnvValues.ACME_KEY_TYPE)"
        "script=$([string]$EnvValues.ACME_SCRIPT_PATH)"
        "installation=$($installers -join ',')"
        "store=$($stores -join ',')"
    ) -join '|'

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($hashInput)
        $hash = $sha.ComputeHash($bytes)
        return [System.BitConverter]::ToString($hash).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

function Get-WacsVersion {
    param([hashtable]$EnvValues)

    $fromEnv = ''
    if ($null -ne $EnvValues -and $EnvValues.ContainsKey('ACME_WACS_VERSION')) {
        $fromEnv = [string]$EnvValues.ACME_WACS_VERSION
    }
    $versionOutput = if (-not [string]::IsNullOrWhiteSpace($fromEnv)) {
        $fromEnv
    } else {
        (Invoke-NativeProcess -FilePath (Resolve-WacsExecutablePath -EnvValues $EnvValues) -ArgumentList @('--version') -TimeoutSeconds 30).OutputLines | Select-Object -First 1
    }

    if ([string]::IsNullOrWhiteSpace([string]$versionOutput)) {
        throw 'Unable to detect simple-acme/wacs version.'
    }

    $versionMatch = [regex]::Match([string]$versionOutput, '(\d+\.\d+\.\d+|\d+\.\d+)')
    if (-not $versionMatch.Success) {
        throw "Unable to parse simple-acme/wacs version from output: '$versionOutput'."
    }
    return [version]$versionMatch.Groups[1].Value
}

function Invoke-WacsIssue {
    param([Parameter(Mandatory)][hashtable]$EnvValues)

    $storePlugins = @('certificatestore')
    $csrAlgorithms = Get-CsrAlgorithms -EnvValues $EnvValues
    $args = @(
        '--accepttos',
        '--source', 'manual',
        '--order', 'single',
        '--baseuri', [string]$EnvValues.ACME_DIRECTORY,
        '--validation', 'none',
        '--globalvalidation', 'none',
        '--host', [string]$EnvValues.DOMAINS
    )
    $args += @('--store', ($storePlugins -join ','))
    if ([string]$EnvValues.ACME_REQUIRES_EAB -eq '1' -and -not [string]::IsNullOrWhiteSpace([string]$EnvValues.ACME_KID)) {
        $args += @('--eab-key-identifier', [string]$EnvValues.ACME_KID)
    }
    if ([string]$EnvValues.ACME_REQUIRES_EAB -eq '1' -and -not [string]::IsNullOrWhiteSpace([string]$EnvValues.ACME_HMAC_SECRET)) {
        $args += @('--eab-key', [string]$EnvValues.ACME_HMAC_SECRET)
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$EnvValues.ACME_ACCOUNT_NAME)) {
        $args += @('--account', [string]$EnvValues.ACME_ACCOUNT_NAME)
    }

    $args += @('--installation', 'script')
    $args += @('--script', [string]$EnvValues.ACME_SCRIPT_PATH, '--scriptparameters', '{CertThumbprint}')

    $lastError = $null
    foreach ($algorithm in $csrAlgorithms) {
        try {
            Invoke-WacsWithRetry -Args ($args + @('--csr', $algorithm)) -EnvValues $EnvValues
            return
        } catch {
            $lastError = $_
            Write-Warning "wacs issuance with CSR '$algorithm' failed: $($_.Exception.Message)"
        }
    }

    if ($null -ne $lastError) { throw $lastError }
    throw 'wacs issuance failed for unknown reason.'
}

# Regression guard: exact-set comparison must stay strict (no subset/superset acceptance).
function Test-ExactDomainSetMatch {
    param([string[]]$Requested,[string[]]$Actual)
    $left = @($Requested | Sort-Object -Unique)
    $right = @($Actual | Sort-Object -Unique)
    return (($left -join ',') -eq ($right -join ','))
}

function Get-RenewalIdForCancel {
    param([Parameter(Mandatory)]$RenewalSummary)
    if (-not [string]::IsNullOrWhiteSpace([string]$RenewalSummary.RenewalId)) { return [string]$RenewalSummary.RenewalId }
    throw "Unable to determine renewal id from renewal JSON file '$($RenewalSummary.File.FullName)'"
}

function Write-ReconcileLog {
    param(
        [Parameter(Mandatory)][ValidateSet('create','update','no-op')][string]$Action,
        [Parameter(Mandatory)][string[]]$Domains,
        [Parameter(Mandatory)][ValidateSet('success','failure')][string]$Result,
        [Parameter(Mandatory)][string]$Message
    )

    $timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    $entry = [ordered]@{
        timestamp = $timestamp
        action = $Action
        domains = @($Domains)
        result = $Result
        message = $Message
    }
    $serialized = $entry | ConvertTo-Json -Compress -Depth 5
    Write-Host $serialized
    $logDir = [string][Environment]::GetEnvironmentVariable('CERTIFICATE_LOG_DIR')
    if (-not [string]::IsNullOrWhiteSpace($logDir)) {
        if (-not (Test-Path -LiteralPath $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
        $logPath = Join-Path $logDir ("reconcile-{0}.log" -f (Get-Date).ToUniversalTime().ToString('yyyyMMdd'))
        Add-Content -LiteralPath $logPath -Value $serialized -Encoding UTF8
    }
}

function Invoke-SimpleAcmeReconcile {
    param(
        [Parameter(Mandatory)][hashtable]$EnvValues,
        [switch]$SkipWacs,
        [switch]$DryRun
    )

    Assert-ReconcilePreflight -EnvValues $EnvValues | Out-Null
    $simpleAcmeDir = Join-Path $env:ProgramData 'simple-acme'
    if (-not (Test-Path -LiteralPath $simpleAcmeDir)) {
        New-Item -ItemType Directory -Path $simpleAcmeDir -Force | Out-Null
    }
    $lockFilePath = Join-Path $simpleAcmeDir 'reconcile.lock'
    $lockFileStream = $null
    $hasLock = $false
    try {
        $deadline = (Get-Date).ToUniversalTime().AddMinutes(5)
        while ((Get-Date).ToUniversalTime() -lt $deadline -and -not $hasLock) {
            try {
                $lockFileStream = [System.IO.File]::Open($lockFilePath, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
                $hasLock = $true
            } catch {
                Start-Sleep -Milliseconds 300
            }
        }
        if (-not $hasLock) {
            throw "Another reconcile run is in progress (could not acquire file lock '$lockFilePath')."
        }

    if ($DryRun) {
        Write-ReconcileLog -Action 'no-op' -Domains (Get-NormalizedDomains -Domains $EnvValues.DOMAINS) -Result 'success' -Message 'Dry-run preflight passed; no wacs actions executed.'
        return 'dry-run'
    }

    $domains = Get-NormalizedDomains -Domains $EnvValues.DOMAINS
    if ($domains.Count -eq 0) {
        throw 'DOMAINS did not contain any valid host names.'
    }

    Ensure-SimpleAcmeSettings -EnvValues $EnvValues

    $allRenewalFiles = Get-RenewalFiles
    $matching = @()
    foreach ($file in $allRenewalFiles) {
        $summary = Get-RenewalSummarySafe -File $file
        if ($null -eq $summary) { continue }
        if (Test-ExactDomainSetMatch -Requested $domains -Actual $summary.Hosts) {
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
            $summary = Get-RenewalSummarySafe -File $file
            if ($null -eq $summary) { continue }
            if (Test-ExactDomainSetMatch -Requested $domains -Actual $summary.Hosts) { $postMatch += ,$summary }
        }

        if ($postMatch.Count -eq 0) {
            Write-ReconcileLog -Action 'create' -Domains $domains -Result 'failure' -Message 'No matching renewal file found after issuance.'
            if ($allRenewalFiles.Count -gt 0) { throw 'No matching renewal file found after issuance; at least one renewal file may be malformed.' }
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
        $renewalId = Get-RenewalIdForCancel -RenewalSummary $current
        $cancelPath = $current.File.FullName
        # Regression guard: keep cancellation by renewal id (`--cancel --id <renewal-id>`).
        Invoke-WacsWithRetry -Args @('--cancel', '--id', $renewalId) -EnvValues $EnvValues
        Wait-RenewalFileRemoval -Path $cancelPath
        Start-Sleep -Seconds 2
        Invoke-WacsIssue -EnvValues $EnvValues
    }

    $freshFiles = Get-RenewalFiles
    $postUpdate = @()
    foreach ($file in $freshFiles) {
        $summary = Get-RenewalSummarySafe -File $file
        if ($null -eq $summary) { continue }
        if (Test-ExactDomainSetMatch -Requested $domains -Actual $summary.Hosts) { $postUpdate += ,$summary }
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
    } finally {
        if ($null -ne $lockFileStream) {
            $lockFileStream.Dispose()
        }
    }
}

Export-ModuleMember -Function @(
    'Resolve-WacsExecutablePath',
    'Compare-RenewalWithEnv',
    'Assert-ReconcilePreflight',
    'Ensure-SimpleAcmeSettings',
    'Get-NormalizedDomains',
    'Get-RenewalFiles',
    'Get-RenewalSummary',
    'Get-RenewalSummarySafe',
    'Get-InstallationPlugins',
    'Get-RenewalIdForCancel',
    'Invoke-SimpleAcmeReconcile',
    'Get-WacsVersion',
    'Invoke-WacsWithRetry',
    'Invoke-WacsIssue',
    'Get-NormalizedCsvValues',
    'Wait-RenewalFileRemoval',
    'New-ReconcileConfigHash',
    'Test-ExactDomainSetMatch',
    'Write-ReconcileLog'
)
