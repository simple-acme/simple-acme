. "$PSScriptRoot/Device-Schemas.ps1"
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
[Console]::OutputEncoding = [System.Text.Encoding]::ASCII

Import-Module "$PSScriptRoot/../core/Tui-Engine.psm1" -Force -Global
Import-Module "$PSScriptRoot/../core/Config-Store.psm1" -Force -Global
Import-Module "$PSScriptRoot/../core/Env-Loader.psm1" -Force -Global
. "$PSScriptRoot/Device-Schemas.ps1"
. "$PSScriptRoot/Menu-Tree.ps1"

$script:DefaultScriptParameters = "'default' {RenewalId} '{CertCommonName}' {CertThumbprint} {OldCertThumbprint} '{CacheFile}' '{CachePassword}' '{StorePath}' {StoreType}"

function Read-MenuChoice {
    param(
        [Parameter(Mandatory)][string]$Prompt,
        [Parameter(Mandatory)][hashtable]$Options,
        [string]$DefaultKey = ''
    )

    while ($true) {
        $suffix = if ([string]::IsNullOrWhiteSpace($DefaultKey)) { '' } else { " [$DefaultKey]" }
        $value = [string](Read-Host "$Prompt$suffix")
        if ([string]::IsNullOrWhiteSpace($value)) { $value = $DefaultKey }
        $value = $value.Trim()
        if ($Options.ContainsKey($value)) { return [string]$Options[$value] }
        Write-Warning "Invalid selection '$value'. Valid choices: $($Options.Keys -join ', ')"
    }
}

function Read-SetupChoice {
    param(
        [Parameter(Mandatory)][string]$Prompt,
        [Parameter(Mandatory)][hashtable]$Options,
        [string]$DefaultKey = '',
        [switch]$AllowBack
    )

    while ($true) {
        $suffix = if ([string]::IsNullOrWhiteSpace($DefaultKey)) { '' } else { " [$DefaultKey]" }
        $backText = if ($AllowBack) { ', B=Back' } else { '' }
        $value = [string](Read-Host "$Prompt$suffix (Q=Cancel$backText)")
        if ([string]::IsNullOrWhiteSpace($value)) { $value = $DefaultKey }
        $value = $value.Trim()

        if ($value -match '^[Qq]$') { return '__CANCEL__' }
        if ($AllowBack -and $value -match '^[Bb]$') { return '__BACK__' }

        if ($Options.ContainsKey($value)) { return [string]$Options[$value] }

        Write-Warning "Invalid selection '$value'. Valid choices: $($Options.Keys -join ', '), Q$backText"
    }
}

function Get-SafeDefaultPipeline {
    return @{
        ACME_ORDER_PLUGIN = 'single'
        ACME_STORE_PLUGIN = 'certificatestore'
        ACME_WACS_RETRY_ATTEMPTS = '3'
        ACME_WACS_RETRY_DELAY_SECONDS = '2'
    }
}

function Get-GuidedPipelineTemplate {
    param(
        [Parameter(Mandatory)][string]$TargetSystem,
        [Parameter(Mandatory)][string]$ValidationMode
    )

    $base = Get-SafeDefaultPipeline
    switch ($TargetSystem) {
        'iis' {
            $base.ACME_SOURCE_PLUGIN = 'iis'
            $base.ACME_VALIDATION_MODE = $ValidationMode
            $base.ACME_INSTALLATION_PLUGINS = 'iis'
            $base.ACME_SCRIPT_PATH = ''
            $base.ACME_SCRIPT_PARAMETERS = ''
        }
        'rds' {
            $base.ACME_SOURCE_PLUGIN = 'manual'
            $base.ACME_VALIDATION_MODE = $ValidationMode
            $base.ACME_INSTALLATION_PLUGINS = 'script'
            $base.ACME_SCRIPT_PATH = Resolve-DeploymentScriptPath -ScriptFileName 'cert2rds.ps1'
            $base.ACME_SCRIPT_PARAMETERS = '{CertThumbprint}'
        }
        default {
            throw "Unsupported guided target '$TargetSystem'."
        }
    }
    return $base
}

function Get-ProviderDefaults {
    param(
        [Parameter(Mandatory)][string]$Provider,
        [string]$Networking4AllEnvironment = 'production',
        [string]$Networking4AllProduct = 'dv'
    )

    switch ($Provider) {
        'networking4all' {
            $envValue = $Networking4AllEnvironment.ToLowerInvariant()
            $product = $Networking4AllProduct.ToLowerInvariant()
            $prefix = if ($envValue -eq 'test') { 'https://test-acme.networking4all.com' } else { 'https://acme.networking4all.com' }
            return @{ ACME_DIRECTORY=($prefix + '/' + $product); RequiresEab=$true; ForceValidation='none' }
        }
        'letsencrypt'    { return @{ ACME_DIRECTORY='https://acme-v02.api.letsencrypt.org/directory'; RequiresEab=$false; ForceValidation='' } }
        'custom'         { return @{ ACME_DIRECTORY=''; RequiresEab=$false; ForceValidation='' } }
        default { throw "Unsupported provider '$Provider'." }
    }
}

function Read-DomainsInput {
    [Console]::WriteLine('Enter domain(s), one per line.')
    [Console]::WriteLine('Empty line = finish, Q = cancel setup.')

    $lines = New-Object System.Collections.Generic.List[string]

    while ($true) {
        $line = [string](Read-Host 'domain')
        if ($line -match '^[Qq]$') { return '__CANCEL__' }
        if ($line -match '^[Bb]$') { return '__BACK__' }

        if ([string]::IsNullOrWhiteSpace($line)) {
            if ($lines.Count -gt 0) {
                return ($lines -join ',')
            }
            Write-Warning 'At least one domain is required, or type Q to cancel.'
            continue
        }

        $lines.Add($line.Trim())
    }
}

function Wait-ForOperatorReturn {
    param([string]$Message = 'Press Enter, Esc, Backspace, or Q to return.')

    [Console]::WriteLine('')
    [Console]::WriteLine($Message)
    while ($true) {
        $key = [Console]::ReadKey($true)
        if ($key.Key -in @([ConsoleKey]::Enter, [ConsoleKey]::Escape, [ConsoleKey]::Backspace)) { return }
        if ($key.KeyChar -match '^[Qq]$') { return }
    }
}

function Test-RoleAvailable {
    param([Parameter(Mandatory)][string]$Role)
    $command = Get-Command -Name Get-WindowsFeature -ErrorAction SilentlyContinue
    if ($null -eq $command) { return $true }
    try {
        $feature = Get-WindowsFeature -Name $Role -ErrorAction Stop
        return [bool]$feature.Installed
    } catch {
        return $false
    }
}

function Assert-AcmeSetupValues {
    param([Parameter(Mandatory)][hashtable]$Values)

    foreach ($key in @('ACME_DIRECTORY','DOMAINS','ACME_SOURCE_PLUGIN','ACME_ORDER_PLUGIN','ACME_STORE_PLUGIN','ACME_VALIDATION_MODE')) {
        if ([string]::IsNullOrWhiteSpace([string]$Values[$key])) { throw "Missing required value: $key" }
    }

    if ($Values.ACME_TARGET_SYSTEM -eq 'iis' -and -not (Test-RoleAvailable -Role 'Web-Server')) {
        throw 'IIS target selected but IIS role Web-Server is not installed.'
    }
    if ($Values.ACME_TARGET_SYSTEM -eq 'rds' -and -not (Test-RoleAvailable -Role 'RDS-Gateway')) {
        throw 'RDS Gateway target selected but role RDS-Gateway is not installed.'
    }

    $domains = @([string]$Values.DOMAINS -split ',' | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
    foreach ($domain in $domains) {
        if ($domain -notmatch '^(?=.{1,253}$)(?!-)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$') {
            throw "Invalid domain format: $domain"
        }
    }

    $installations = @([string]$Values.ACME_INSTALLATION_PLUGINS -split ',' | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
    if ($installations -contains 'script') {
        $scriptPath = [string]$Values.ACME_SCRIPT_PATH
        if ([string]::IsNullOrWhiteSpace($scriptPath)) { throw 'Script installation selected but ACME_SCRIPT_PATH is empty.' }
        if (-not [System.IO.Path]::IsPathRooted($scriptPath)) {
            $scriptPath = Resolve-AbsoluteSetupPath -PathValue $scriptPath
            $Values.ACME_SCRIPT_PATH = $scriptPath
        }
        if (-not (Test-Path -LiteralPath $scriptPath)) { throw "Script installation selected but script path does not exist: $scriptPath" }
        if ([string]::IsNullOrWhiteSpace([string]$Values.ACME_SCRIPT_PARAMETERS)) { throw 'Script installation selected but ACME_SCRIPT_PARAMETERS is empty.' }
    }

    if ($Values.ACME_REQUIRES_EAB -eq '1') {
        if ([string]::IsNullOrWhiteSpace([string]$Values.ACME_KID)) { throw 'Provider requires EAB. ACME_KID is required.' }
        if ([string]::IsNullOrWhiteSpace([string]$Values.ACME_HMAC_SECRET)) { throw 'Provider requires EAB. ACME_HMAC_SECRET is required.' }
    }
}

function Resolve-AbsoluteSetupPath {
    param([Parameter(Mandatory)][string]$PathValue)
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return $PathValue }
    if ([System.IO.Path]::IsPathRooted($PathValue)) { return [System.IO.Path]::GetFullPath($PathValue) }
    return [System.IO.Path]::GetFullPath((Join-Path (Split-Path $PSScriptRoot -Parent) $PathValue))
}

function Resolve-DeploymentScriptPath {
    param([Parameter(Mandatory)][string]$ScriptFileName)
    if ([string]::IsNullOrWhiteSpace($ScriptFileName)) {
        throw 'Deployment script file name is empty.'
    }

    if ([System.IO.Path]::IsPathRooted($ScriptFileName)) {
        $candidate = $ScriptFileName
    } else {
        $projectRoot = Split-Path $PSScriptRoot -Parent
        $candidate = Join-Path $projectRoot (Join-Path 'Scripts' $ScriptFileName)
    }

    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw @"
Required deployment script not found.

Expected:
$candidate

Deployment scripts must be located in:
<project-root>\Scripts\n

Current requested script:
$ScriptFileName
"@
    }

    return [string](Convert-Path -LiteralPath $candidate -ErrorAction Stop)
}

function New-DeviceId {
    param([Parameter(Mandatory)][string]$Label)
    $slug = ($Label.ToLowerInvariant() -replace '[^a-z0-9]+','-').Trim('-')
    $suffix = ([guid]::NewGuid().ToString('N')).Substring(0,8)
    return "$slug-$suffix"
}

function Get-PolicyFilePath {
    param([Parameter(Mandatory)][string]$ConfigDir)
    Join-Path $ConfigDir 'policies.json'
}

function Get-Policies {
    param([Parameter(Mandatory)][string]$ConfigDir)
    $path = Get-PolicyFilePath -ConfigDir $ConfigDir
    if (-not (Test-Path -LiteralPath $path)) { return @() }
    $raw = Get-Content -Raw -Encoding UTF8 -Path $path | ConvertFrom-Json
    return @($raw)
}

function Save-Policies {
    param([Parameter(Mandatory)][string]$ConfigDir,[Parameter(Mandatory)][object[]]$Policies)
    $path = Get-PolicyFilePath -ConfigDir $ConfigDir
    if (-not (Test-Path -LiteralPath $ConfigDir)) { New-Item -ItemType Directory -Path $ConfigDir -Force | Out-Null }
    $tmp = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($tmp, ($Policies | ConvertTo-Json -Depth 20), [System.Text.Encoding]::UTF8)
    Move-Item -LiteralPath $tmp -Destination $path -Force
}

function Find-PolicyById {
    param([Parameter(Mandatory)][object[]]$Policies,[Parameter(Mandatory)][string]$PolicyId)
    @($Policies | Where-Object { $_.policy_id -eq $PolicyId }) | Select-Object -First 1
}

function Format-PolicySummaryLines {
    param([AllowEmptyCollection()][object[]]$Policies = @())
    if (@($Policies).Count -eq 0) { return @() }
    $lines = @()
    foreach ($p in $Policies) {
        $connectors = @($p.connectors)
        $connectorNames = @($connectors | ForEach-Object { if ($_.connector_type) { [string]$_.connector_type } elseif ($_.label) { [string]$_.label } })
        $names = if ($connectorNames.Count -gt 0) { ($connectorNames -join ', ') } else { '-' }
        $lines += "policy_id=$($p.policy_id) fanout=$($p.fanout_policy) quorum=$($p.quorum_threshold) connectors=$($connectors.Count) [$names]"
    }
    return $lines
}

function Invoke-PolicyViewer {
    param([Parameter(Mandatory)][string]$ConfigDir)
    $policies = @(Get-Policies -ConfigDir $ConfigDir)
    $lines = @(Format-PolicySummaryLines -Policies $policies)
    if ($lines.Count -eq 0) {
        $lines = @('No deployment policies found.')
    }
    Clear-TuiScreen
    $bounds = Get-TuiLayoutBounds
    $height = [Math]::Min($bounds.ContentHeight, [Math]::Max(8, $lines.Count + 4))
    Write-TuiBox -X $bounds.BoxX -Y $bounds.BoxY -Width $bounds.BoxWidth -Height $height -Title ' Existing deployment policies '
    $visible = [Math]::Max(1, $height - 2)
    for ($i=0; $i -lt [Math]::Min($visible, $lines.Count); $i++) {
        Write-TuiAt -X ($bounds.BoxX + 2) -Y ($bounds.BoxY + 1 + $i) -Text (Get-TuiClippedText -Text $lines[$i] -Width ($bounds.BoxWidth - 4))
    }
    Show-TuiStatus -Message 'Press Enter/Esc to return.' -Type Info -Row $bounds.HelpRow
    Wait-ForOperatorReturn
}

function Resolve-ProjectRoot {
    param([string]$EnvFilePath)
    if (-not [string]::IsNullOrWhiteSpace([string]$EnvFilePath)) {
        $resolvedEnvPath = Resolve-AbsoluteSetupPath -PathValue $EnvFilePath
        return [System.IO.Path]::GetFullPath((Split-Path -Path $resolvedEnvPath -Parent))
    }
    return (Split-Path $PSScriptRoot -Parent)
}

function Get-SimpleAcmeDataRoot {
    if (-not [string]::IsNullOrWhiteSpace([string]$env:ACME_DATA_DIR)) {
        return [string]$env:ACME_DATA_DIR
    }
    return (Join-Path $env:ProgramData 'simple-acme')
}

function Get-SimpleAcmeSettingsPaths {
    $baseDir = Get-SimpleAcmeDataRoot
    $paths = New-Object System.Collections.Generic.List[string]
    $rootSettings = Join-Path $baseDir 'settings.json'
    if (Test-Path -LiteralPath $rootSettings -PathType Leaf) { $paths.Add((Convert-Path -LiteralPath $rootSettings -ErrorAction Stop)) }
    if (Test-Path -LiteralPath $baseDir -PathType Container) {
        foreach ($child in @(Get-ChildItem -LiteralPath $baseDir -Directory -ErrorAction SilentlyContinue)) {
            $childSettings = Join-Path $child.FullName 'settings.json'
            if (Test-Path -LiteralPath $childSettings -PathType Leaf) {
                $paths.Add((Convert-Path -LiteralPath $childSettings -ErrorAction Stop))
            }
        }
    }
    return @($paths | Select-Object -Unique)
}

function Read-SimpleAcmeSettings {
    param([Parameter(Mandatory)][string]$Path)
    try {
        $json = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
        throw "Failed to parse simple-acme settings '$Path': $($_.Exception.Message)"
    }
    return $json
}

function ConvertTo-HashtableRecursiveLocal {
    param($InputObject)
    if ($null -eq $InputObject) { return $null }
    if ($InputObject -is [System.Collections.IDictionary]) {
        $hash = @{}
        foreach ($key in $InputObject.Keys) {
            $hash[[string]$key] = ConvertTo-HashtableRecursiveLocal -InputObject $InputObject[$key]
        }
        return $hash
    }
    if ($InputObject -is [System.Management.Automation.PSCustomObject]) {
        $hash = @{}
        foreach ($prop in $InputObject.PSObject.Properties) {
            $hash[$prop.Name] = ConvertTo-HashtableRecursiveLocal -InputObject $prop.Value
        }
        return $hash
    }
    if ($InputObject -is [System.Collections.IEnumerable] -and -not ($InputObject -is [string])) {
        $items = @()
        foreach ($item in $InputObject) { $items += ConvertTo-HashtableRecursiveLocal -InputObject $item }
        return $items
    }
    return $InputObject
}

function Mask-EnvDisplayValue {
    param(
        [Parameter(Mandatory)][string]$Name,
        [AllowNull()][string]$Value
    )
    $upper = $Name.ToUpperInvariant()
    if ($upper -eq 'ACME_KID') {
        if ([string]::IsNullOrWhiteSpace([string]$Value)) { return '<not set>' }
        return '<set>'
    }
    if ($upper -eq 'ACME_HMAC_SECRET' -or $upper -eq 'CERTIFICATE_API_KEY' -or $upper.Contains('SECRET') -or $upper.Contains('KEY')) {
        if ([string]::IsNullOrWhiteSpace([string]$Value)) { return '<not set>' }
        return '<hidden>'
    }
    return [string]$Value
}

function Get-SimpleAcmeLogLocations {
    param([string]$ProjectRoot)
    $result = [ordered]@{
        WrapperCandidates = @()
        WrapperExisting = @()
        SimpleAcmeDirectories = @()
    }

    $projectRootResolved = if ([string]::IsNullOrWhiteSpace([string]$ProjectRoot)) { Split-Path $PSScriptRoot -Parent } else { [System.IO.Path]::GetFullPath($ProjectRoot) }
    $wrapperCandidates = @(
        (Join-Path $projectRootResolved 'log'),
        (Join-Path $projectRootResolved 'logs')
    )
    $result.WrapperCandidates = @($wrapperCandidates | Select-Object -Unique)
    $result.WrapperExisting = @($wrapperCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | ForEach-Object { Convert-Path -LiteralPath $_ -ErrorAction Stop } | Select-Object -Unique)

    $baseDir = Get-SimpleAcmeDataRoot
    $locations = New-Object System.Collections.Generic.List[string]
    $rootLog = Join-Path $baseDir 'Log'
    if (Test-Path -LiteralPath $rootLog -PathType Container) { $locations.Add((Convert-Path -LiteralPath $rootLog -ErrorAction Stop)) }
    if (Test-Path -LiteralPath $baseDir -PathType Container) {
        foreach ($child in @(Get-ChildItem -LiteralPath $baseDir -Directory -ErrorAction SilentlyContinue)) {
            $candidate = Join-Path $child.FullName 'Log'
            if (Test-Path -LiteralPath $candidate -PathType Container) {
                $locations.Add((Convert-Path -LiteralPath $candidate -ErrorAction Stop))
            }
        }
    }
    $result.SimpleAcmeDirectories = @($locations | Select-Object -Unique)
    return [pscustomobject]$result
}

function Get-SimpleAcmeLatestLogFile {
    param([string[]]$LogDirectories)
    $files = @()
    foreach ($dir in @($LogDirectories)) {
        if (Test-Path -LiteralPath $dir -PathType Container) {
            $files += @(Get-ChildItem -LiteralPath $dir -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending)
        }
    }
    if ($files.Count -eq 0) { return $null }
    return ($files | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1)
}

function Get-SimpleAcmeLogDiagnostics {
    param([string]$LatestLogPath)
    if ([string]::IsNullOrWhiteSpace([string]$LatestLogPath) -or -not (Test-Path -LiteralPath $LatestLogPath -PathType Leaf)) {
        return [pscustomobject]@{
            LogPath = $LatestLogPath
            WarningCount = 0
            ErrorCount = 0
            HasAssemblyLoadErrors = $false
            WarningLines = @()
            ErrorLines = @()
            LastLines = @()
        }
    }
    $lines = @(Get-Content -LiteralPath $LatestLogPath -Encoding UTF8 -ErrorAction SilentlyContinue)
    $warningLines = @($lines | Where-Object { $_ -match '\[WARN\]' })
    $errorLines = @($lines | Where-Object { $_ -match '\[EROR\]' })
    $assemblyErrors = @($errorLines | Where-Object { $_ -match 'Error loading assembly' })
    $last50 = @()
    if ($lines.Count -gt 50) { $last50 = @($lines[($lines.Count - 50)..($lines.Count - 1)]) } else { $last50 = $lines }
    return [pscustomobject]@{
        LogPath = $LatestLogPath
        WarningCount = $warningLines.Count
        ErrorCount = $errorLines.Count
        HasAssemblyLoadErrors = ($assemblyErrors.Count -gt 0)
        WarningLines = $warningLines
        ErrorLines = $errorLines
        LastLines = $last50
    }
}

function Show-SimpleAcmeDiagnosticSummary {
    param(
        [string]$ProjectRoot,
        [switch]$ShowNoobAdvice
    )
    $locations = Get-SimpleAcmeLogLocations -ProjectRoot $ProjectRoot
    $latest = Get-SimpleAcmeLatestLogFile -LogDirectories $locations.SimpleAcmeDirectories
    if ($null -eq $latest) {
        [Console]::WriteLine('simple-acme diagnostics: no log files found yet under ProgramData\simple-acme.')
        return
    }
    $diag = Get-SimpleAcmeLogDiagnostics -LatestLogPath $latest.FullName
    [Console]::WriteLine("simple-acme diagnostics: errors=$($diag.ErrorCount) warnings=$($diag.WarningCount) latest=$($latest.FullName)")
    if ($diag.HasAssemblyLoadErrors) {
        [Console]::WriteLine('simple-acme logged assembly load errors. This may indicate blocked DLLs, incompatible bundle files, or optional plugin load failures. Inspect:')
        [Console]::WriteLine($latest.FullName)
        if ($ShowNoobAdvice) {
            [Console]::WriteLine('Advice (do not run automatically): Get-ChildItem C:\certificaat -Recurse | Unblock-File')
        }
    }
}

function Invoke-ViewLogsDiagnostics {
    param([string]$ProjectRoot)
    $locations = Get-SimpleAcmeLogLocations -ProjectRoot $ProjectRoot
    [Console]::WriteLine('')
    [Console]::WriteLine('Logs / diagnostics')
    [Console]::WriteLine('-------------------')
    [Console]::WriteLine('Wrapper log directories:')
    foreach ($candidate in @($locations.WrapperCandidates)) {
        $exists = if ($locations.WrapperExisting -contains $candidate -or $locations.WrapperExisting -contains (Resolve-AbsoluteSetupPath -PathValue $candidate)) { 'exists' } else { 'missing' }
        [Console]::WriteLine(" - $candidate [$exists]")
    }
    [Console]::WriteLine('')
    [Console]::WriteLine('simple-acme log directories:')
    if (@($locations.SimpleAcmeDirectories).Count -eq 0) {
        [Console]::WriteLine(' - none discovered under ProgramData\simple-acme yet.')
        Wait-ForOperatorReturn
        return
    }
    foreach ($dir in @($locations.SimpleAcmeDirectories)) { [Console]::WriteLine(" - $dir") }

    $latest = Get-SimpleAcmeLatestLogFile -LogDirectories $locations.SimpleAcmeDirectories
    if ($null -eq $latest) {
        [Console]::WriteLine('')
        [Console]::WriteLine('No log files found in discovered simple-acme log directories.')
        Wait-ForOperatorReturn
        return
    }

    $diag = Get-SimpleAcmeLogDiagnostics -LatestLogPath $latest.FullName
    [Console]::WriteLine('')
    [Console]::WriteLine("Newest log file: $($latest.FullName)")
    [Console]::WriteLine("Warnings found: $($diag.WarningCount)")
    [Console]::WriteLine("Errors found: $($diag.ErrorCount)")
    if ($diag.HasAssemblyLoadErrors) {
        [Console]::WriteLine('simple-acme logged assembly load errors.')
        [Console]::WriteLine('This may indicate blocked DLLs, incompatible bundle files, or optional plugin load failures.')
        [Console]::WriteLine('Inspect:')
        [Console]::WriteLine($latest.FullName)
        [Console]::WriteLine('')
        [Console]::WriteLine('Suggested manual command (do not run automatically):')
        [Console]::WriteLine('Get-ChildItem C:\certificaat -Recurse | Unblock-File')
    }
    [Console]::WriteLine('')
    [Console]::WriteLine('Last 50 lines:')
    [Console]::WriteLine('--------------')
    foreach ($line in @($diag.LastLines)) { [Console]::WriteLine([string]$line) }
    Wait-ForOperatorReturn
}

function Invoke-AcmeSettingsMenu {
    param([Parameter(Mandatory)][string]$EnvFilePath)

    $resolvedEnvFilePath = if ([string]::IsNullOrWhiteSpace([string]$EnvFilePath)) {
        Resolve-BootstrapEnvPath -ProjectRoot (Split-Path $PSScriptRoot -Parent)
    } else {
        [System.IO.Path]::GetFullPath($EnvFilePath)
    }

    while ($true) {
        [Console]::WriteLine('')
        [Console]::WriteLine('ACME settings')
        [Console]::WriteLine('[1] View current bootstrap certificate.env')
        [Console]::WriteLine('[2] View simple-acme settings.json')
        [Console]::WriteLine('[3] View renewal files')
        [Console]::WriteLine('[4] Change ACME provider')
        [Console]::WriteLine('[5] Change EAB credentials')
        [Console]::WriteLine('[6] Change validation mode')
        [Console]::WriteLine('[7] Set private key export setting')
        [Console]::WriteLine('[8] View effective wacs command preview')
        [Console]::WriteLine('[9] View logs / diagnostics')
        [Console]::WriteLine('[0] Back')
        $choice = Read-SetupChoice -Prompt 'ACME settings' -Options @{
            '1'='view-bootstrap'
            '2'='view-settings'
            '3'='view-renewals'
            '4'='provider'
            '5'='eab'
            '6'='validation'
            '7'='set-exportable'
            '8'='preview'
            '9'='diagnostics'
            '0'='back'
        } -DefaultKey '0'

        if ($choice -in @('back','__CANCEL__','__BACK__')) { return }

        switch ($choice) {
            'view-bootstrap' {
                [Console]::WriteLine('')
                [Console]::WriteLine("Bootstrap env file: $resolvedEnvFilePath")
                if ($env:CERTIFICATE_ENV_FILE) {
                    [Console]::WriteLine('Source: CERTIFICATE_ENV_FILE override')
                } else {
                    [Console]::WriteLine('Source: default project-root certificate.env')
                }
                if (-not (Test-Path -LiteralPath $resolvedEnvFilePath -PathType Leaf)) {
                    [Console]::WriteLine('certificate.env not found.')
                    Wait-ForOperatorReturn
                    continue
                }
                $envValues = Read-EnvFile -Path $resolvedEnvFilePath
                foreach ($name in @($envValues.Keys | Sort-Object)) {
                    $displayValue = Mask-EnvDisplayValue -Name [string]$name -Value ([string]$envValues[$name])
                    [Console]::WriteLine("$name=$displayValue")
                }
                Wait-ForOperatorReturn
            }
            'view-settings' {
                [Console]::WriteLine('')
                $settingsPaths = @(Get-SimpleAcmeSettingsPaths)
                if ($settingsPaths.Count -eq 0) {
                    [Console]::WriteLine('simple-acme settings.json not found yet. It may be created after first wacs run.')
                    Wait-ForOperatorReturn
                    continue
                }
                foreach ($path in $settingsPaths) {
                    [Console]::WriteLine("settings.json: $path")
                    try {
                        $settings = Read-SimpleAcmeSettings -Path $path
                        $renewalDays = $null
                        $renewalMinValid = $null
                        $privateExportable = $null
                        if ($settings.PSObject.Properties['ScheduledTask']) {
                            $renewalDays = $settings.ScheduledTask.RenewalDays
                            $renewalMinValid = $settings.ScheduledTask.RenewalMinimumValidDays
                        }
                        if ($settings.PSObject.Properties['Store'] -and $settings.Store.PSObject.Properties['CertificateStore']) {
                            $privateExportable = $settings.Store.CertificateStore.PrivateKeyExportable
                        }
                        [Console]::WriteLine(" - ScheduledTask.RenewalDays=$renewalDays")
                        [Console]::WriteLine(" - ScheduledTask.RenewalMinimumValidDays=$renewalMinValid")
                        if ($null -ne $privateExportable) {
                            [Console]::WriteLine(" - Store.CertificateStore.PrivateKeyExportable=$privateExportable")
                        } else {
                            [Console]::WriteLine(' - Store.CertificateStore.PrivateKeyExportable=(not present)')
                        }
                    } catch {
                        [Console]::WriteLine(" - warning: $($_.Exception.Message)")
                    }
                }
                Wait-ForOperatorReturn
            }
            'view-renewals' {
                $baseDir = Get-SimpleAcmeDataRoot
                [Console]::WriteLine('')
                [Console]::WriteLine("Renewal file discovery root: $baseDir")
                if (-not (Test-Path -LiteralPath $baseDir -PathType Container)) {
                    [Console]::WriteLine('simple-acme data directory not found yet.')
                    Wait-ForOperatorReturn
                    continue
                }
                $files = @(Get-ChildItem -LiteralPath $baseDir -Filter '*.renewal.json' -File -Recurse -ErrorAction SilentlyContinue)
                if ($files.Count -eq 0) {
                    [Console]::WriteLine('No renewal JSON files found.')
                    Wait-ForOperatorReturn
                    continue
                }
                foreach ($file in $files) {
                    [Console]::WriteLine("file: $($file.FullName)")
                    try {
                        $obj = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
                        $rawHosts = @()
                        if ($obj.PSObject.Properties['Identifiers']) { $rawHosts += @($obj.Identifiers) }
                        if ($obj.PSObject.Properties['Host']) { $rawHosts += @([string]$obj.Host -split ',') }
                        if ($obj.PSObject.Properties['Hosts']) { $rawHosts += @($obj.Hosts) }
                        $hosts = @($rawHosts | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Sort-Object -Unique)

                        $scriptPath = '(none)'
                        $sourcePlugin = '(unknown)'
                        $validationPlugin = '(unknown)'
                        $storePlugin = '(unknown)'
                        if ($obj.PSObject.Properties['Installation'] -and $obj.Installation.PSObject.Properties['Plugin']) {
                            $installPlugin = [string]$obj.Installation.Plugin
                        } else {
                            $installPlugin = '(unknown)'
                        }
                        if ($obj.PSObject.Properties['Source'] -and $obj.Source.PSObject.Properties['Plugin']) {
                            $sourcePlugin = [string]$obj.Source.Plugin
                        }
                        if ($obj.PSObject.Properties['Validation'] -and $obj.Validation.PSObject.Properties['Plugin']) {
                            $validationPlugin = [string]$obj.Validation.Plugin
                        }
                        if ($obj.PSObject.Properties['Store'] -and $obj.Store.PSObject.Properties['Plugin']) {
                            $storePlugin = [string]$obj.Store.Plugin
                        }
                        if ($obj.PSObject.Properties['Installation'] -and $obj.Installation.PSObject.Properties['PluginOptions'] -and $obj.Installation.PluginOptions.PSObject.Properties['Script']) {
                            $scriptPath = [string]$obj.Installation.PluginOptions.Script
                        }

                        [Console]::WriteLine(" - domains/hosts: $(if ($hosts.Count -gt 0) { $hosts -join ',' } else { '(unknown)' })")
                        [Console]::WriteLine(" - source plugin: $sourcePlugin")
                        [Console]::WriteLine(" - validation plugin: $validationPlugin")
                        [Console]::WriteLine(" - store plugin: $storePlugin")
                        [Console]::WriteLine(" - script path: $scriptPath")
                        [Console]::WriteLine(" - installation plugin: $installPlugin")
                    } catch {
                        [Console]::WriteLine(" - warning: malformed JSON ($($_.Exception.Message))")
                    }
                }
                Wait-ForOperatorReturn
            }
            'provider' {
                $envValues = @{}
                if (Test-Path -LiteralPath $resolvedEnvFilePath -PathType Leaf) { $envValues = Read-EnvFile -Path $resolvedEnvFilePath }
                $providerValues = Select-AcmeProviderValues -CurrentValues $envValues
                if ($null -eq $providerValues) { continue }
                foreach ($k in $providerValues.Keys) { $envValues[$k] = $providerValues[$k] }
                Write-EnvFile -Values $envValues -Path $resolvedEnvFilePath
                [Console]::WriteLine('Updated ACME provider settings in bootstrap certificate.env.')
                Wait-ForOperatorReturn
            }
            'eab' {
                $envValues = @{}
                if (Test-Path -LiteralPath $resolvedEnvFilePath -PathType Leaf) { $envValues = Read-EnvFile -Path $resolvedEnvFilePath }
                $envValues.ACME_KID = [string](Read-Host 'ACME_KID')
                $envValues.ACME_HMAC_SECRET = [string](Read-Host 'ACME_HMAC_SECRET')
                Write-EnvFile -Values $envValues -Path $resolvedEnvFilePath
                [Console]::WriteLine('Updated EAB credentials.')
                Wait-ForOperatorReturn
            }
            'validation' {
                $envValues = @{}
                if (Test-Path -LiteralPath $resolvedEnvFilePath -PathType Leaf) { $envValues = Read-EnvFile -Path $resolvedEnvFilePath }
                [Console]::WriteLine('[1] none (recommended for Networking4All)')
                [Console]::WriteLine('[2] http-01 (advanced/unsupported in this guided flow)')
                $mode = Read-SetupChoice -Prompt 'Validation mode' -Options @{ '1'='none'; '2'='http-01' } -DefaultKey '1' -AllowBack
                if ($mode -in @('__CANCEL__','__BACK__')) { continue }
                $envValues.ACME_VALIDATION_MODE = $mode
                Write-EnvFile -Values $envValues -Path $resolvedEnvFilePath
                if ($mode -eq 'http-01') {
                    [Console]::WriteLine('Warning: http-01 flow is not fully implemented in this noob-guided setup.')
                }
                Wait-ForOperatorReturn
            }
            'preview' {
                $envValues = @{}
                if (Test-Path -LiteralPath $resolvedEnvFilePath -PathType Leaf) { $envValues = Read-EnvFile -Path $resolvedEnvFilePath }
                if ([string]$envValues.ACME_PROVIDER -eq 'networking4all' -and [string]$envValues.ACME_DIRECTORY -notmatch 'networking4all\.com') {
                    [Console]::WriteLine('Internal state mismatch: selected provider is Networking4All but ACME_DIRECTORY is not Networking4All.')
                    [Console]::WriteLine('Setup was not saved.')
                    Wait-ForOperatorReturn
                    continue
                }
                $scriptParameters = if ($envValues.ContainsKey('ACME_SCRIPT_PARAMETERS')) { [string]$envValues.ACME_SCRIPT_PARAMETERS } else { '{CertThumbprint}' }
                $line = "wacs.exe --accepttos --source manual --order single --baseuri $([string]$envValues.ACME_DIRECTORY) --validation none --globalvalidation none --host $([string]$envValues.DOMAINS) --store certificatestore --installation script --script $([string]$envValues.ACME_SCRIPT_PATH) --scriptparameters `"$scriptParameters`" --csr ec"
                if (-not [string]::IsNullOrWhiteSpace([string]$envValues.ACME_KID)) { $line += ' --eab-key-identifier <set>' }
                if (-not [string]::IsNullOrWhiteSpace([string]$envValues.ACME_HMAC_SECRET)) { $line += ' --eab-key <hidden>' }
                [Console]::WriteLine($line)
                Wait-ForOperatorReturn
            }
            'diagnostics' {
                Invoke-ViewLogsDiagnostics -ProjectRoot (Split-Path $PSScriptRoot -Parent)
            }
            'set-exportable' {
                $settingsPaths = @(Get-SimpleAcmeSettingsPaths)
                $targetSettingsPath = ''
                if ($settingsPaths.Count -gt 0) {
                    $targetSettingsPath = $settingsPaths[0]
                } else {
                    $targetSettingsPath = Join-Path (Get-SimpleAcmeDataRoot) 'settings.json'
                }

                if (-not (Test-Path -LiteralPath (Split-Path $targetSettingsPath -Parent))) {
                    New-Item -ItemType Directory -Path (Split-Path $targetSettingsPath -Parent) -Force | Out-Null
                }

                $settings = @{}
                if (Test-Path -LiteralPath $targetSettingsPath -PathType Leaf) {
                    try {
                        $existingObj = Get-Content -LiteralPath $targetSettingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
                        $settings = ConvertTo-HashtableRecursiveLocal -InputObject $existingObj
                    } catch {
                        [Console]::WriteLine("warning: failed to parse existing settings.json, creating minimal settings object. $($_.Exception.Message)")
                        $settings = @{}
                    }
                }
                if (-not $settings.ContainsKey('Store') -or $null -eq $settings.Store) { $settings.Store = @{} }
                if (-not $settings.Store.ContainsKey('CertificateStore') -or $null -eq $settings.Store.CertificateStore) { $settings.Store.CertificateStore = @{} }

                [Console]::WriteLine('Choose private key export setting:')
                [Console]::WriteLine('[1] PrivateKeyExportable=false')
                [Console]::WriteLine('[2] PrivateKeyExportable=true')
                $mode = Read-SetupChoice -Prompt 'Export setting' -Options @{ '1'='false'; '2'='true' } -DefaultKey '1' -AllowBack
                if ($mode -in @('__CANCEL__','__BACK__')) { continue }

                $settings.Store.CertificateStore.PrivateKeyExportable = ($mode -eq 'true')
                [System.IO.File]::WriteAllText($targetSettingsPath, ($settings | ConvertTo-Json -Depth 12), (New-Object System.Text.UTF8Encoding($false)))
                [Console]::WriteLine("Updated: $targetSettingsPath")
                [Console]::WriteLine('This applies only to newly issued certificates.')
                [Console]::WriteLine('Existing certificates must be reissued to change private key exportability.')
                Wait-ForOperatorReturn
            }
        }
    }
}

function Invoke-PolicyEditor {
    param([string]$ConfigDir)
    $path = Get-PolicyFilePath -ConfigDir $ConfigDir
    if (-not (Test-Path -LiteralPath $path)) { Save-Policies -ConfigDir $ConfigDir -Policies @() }

    $statusRow = [Math]::Max(0,[Console]::WindowHeight - 2)
    $policies = @(Get-Policies -ConfigDir $ConfigDir)
    while($true){
        $choice = Read-Host 'Policy editor: [N]ew [E]dit [D]elete [L]ist [Q]uit'
        if($choice -match '^[Qq]'){ break }
        if($choice -match '^[Ll]'){
            Clear-TuiScreen
            Invoke-PolicyViewer -ConfigDir $ConfigDir
            $policies = @(Get-Policies -ConfigDir $ConfigDir)
            continue
        }

        if (($choice -match '^[EeDd]') -and @($policies).Count -eq 0) {
            Show-TuiStatus -Message 'No policies exist. Create one first.' -Type Warning -Row $statusRow
            continue
        }

        if($choice -match '^[Nn]'){
            $pid = Read-Host 'policy_id'
            if ([string]::IsNullOrWhiteSpace($pid)) {
                Show-TuiStatus -Message 'policy_id is required.' -Type Warning -Row $statusRow
                continue
            }
            if ($null -ne (Find-PolicyById -Policies $policies -PolicyId $pid)) {
                Show-TuiStatus -Message "Policy '$pid' already exists." -Type Warning -Row $statusRow
                continue
            }
            $fan = Read-Host 'fanout_policy'
            $q = 0
            if (-not [int]::TryParse((Read-Host 'quorum_threshold'), [ref]$q)) {
                Show-TuiStatus -Message 'quorum_threshold must be an integer.' -Type Warning -Row $statusRow
                continue
            }
            $policies += [pscustomobject]@{ policy_id=$pid; fanout_policy=$fan; quorum_threshold=$q; connectors=@() }
            Save-Policies -ConfigDir $ConfigDir -Policies $policies
            continue
        }

        if($choice -match '^[Dd]'){
            $pid = Read-Host 'policy_id to delete'
            if ($null -eq (Find-PolicyById -Policies $policies -PolicyId $pid)) {
                Show-TuiStatus -Message "Policy '$pid' was not found. Returning to editor." -Type Warning -Row $statusRow
                continue
            }
            $policies = @($policies | Where-Object { $_.policy_id -ne $pid })
            Save-Policies -ConfigDir $ConfigDir -Policies $policies
            continue
        }

        if($choice -match '^[Ee]'){
            $pid = Read-Host 'policy_id to edit'
            $p = Find-PolicyById -Policies $policies -PolicyId $pid
            if($null -eq $p){
                Show-TuiStatus -Message "Policy '$pid' was not found. Returning to editor." -Type Warning -Row $statusRow
                continue
            }
            $fan = Read-Host "fanout_policy [$($p.fanout_policy)]"
            if (-not [string]::IsNullOrWhiteSpace($fan)) { $p.fanout_policy = $fan }
            $qInput = Read-Host "quorum_threshold [$($p.quorum_threshold)]"
            if (-not [string]::IsNullOrWhiteSpace($qInput)) {
                $qParsed = 0
                if (-not [int]::TryParse($qInput, [ref]$qParsed)) {
                    Show-TuiStatus -Message 'Invalid quorum_threshold; keeping previous value.' -Type Warning -Row $statusRow
                } else {
                    $p.quorum_threshold = $qParsed
                }
            }
            $conn = Read-Host 'connector types comma-separated'
            if($conn){ $p.connectors=@($conn -split ',' | ForEach-Object { [pscustomobject]@{ connector_type=$_.Trim(); label=$_.Trim(); settings=@{} } }) }
            Save-Policies -ConfigDir $ConfigDir -Policies $policies
            continue
        }

        Show-TuiStatus -Message 'Invalid selection. Choose N/E/D/L/Q.' -Type Warning -Row $statusRow
    }

    return @(Get-Policies -ConfigDir $ConfigDir)
}

function Invoke-DeviceForm {
    param([Parameter(Mandatory)][string]$ConnectorType,[Parameter(Mandatory)][string]$ConfigDir)

    if (-not $DeviceSchemas.ContainsKey($ConnectorType)) { throw "Unknown connector type: $ConnectorType" }
    $schema = $DeviceSchemas[$ConnectorType]
    if ($schema.ContainsKey('Disabled') -and [bool]$schema.Disabled) {
        $requires = if ($schema.ContainsKey('Requires')) { [string]$schema.Requires } else { 'Connector currently unavailable.' }
        Show-TuiStatus -Message "$($schema.Label) is not available: $requires" -Type Warning -Row ([Math]::Max(0,[Console]::WindowHeight)-2)
        Start-Sleep -Milliseconds 1800
        return $null
    }

    $existing = Get-AllDeviceConfigs -ConfigDir $ConfigDir -SkipIntegrityFailures | Where-Object { $_.connector_type -eq $ConnectorType } | Select-Object -First 1
    $current = if ($existing) { $existing.settings } else { @{} }

    Clear-TuiScreen
    try {
        $values = Show-TuiForm -Fields $schema.Fields -CurrentValues $current -Title "Configure $($schema.Label)"
    } catch {
        Show-TuiStatus -Message "Error loading existing config: $($_.Exception.Message)" -Type Error -Row ([Math]::Max(0,[Console]::WindowHeight)-2)
        return $null
    }

    if ($null -eq $values) { return $null }

    $deviceId = if ($existing) { $existing.device_id } else { New-DeviceId -Label $schema.Label }
    $now = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    $device = @{
        device_id = $deviceId
        connector_type = $ConnectorType
        label = $schema.Label
        created_at = if ($existing) { $existing.created_at } else { $now }
        updated_at = $now
        settings = $values
    }

    $secretFields = @($schema.Fields | Where-Object { $_.Type -eq 'secret' } | Select-Object -ExpandProperty Name)
    Save-DeviceConfig -Device $device -ConfigDir $ConfigDir -SecretFields $secretFields | Out-Null
    Show-TuiStatus -Message 'Saved successfully.' -Type Success -Row ([Math]::Max(0,[Console]::WindowHeight)-2)
    Start-Sleep -Milliseconds 1500
    return $device
}

function Save-SecurePlatformConfig {
    param(
        [Parameter(Mandatory)][string]$ConfigDir,
        [Parameter(Mandatory)][hashtable]$Values
    )

    if (-not (Test-Path -LiteralPath $ConfigDir)) { New-Item -ItemType Directory -Path $ConfigDir -Force | Out-Null }

    $secureEnvPath = Join-Path $ConfigDir 'env.secure'
    $credPath = Join-Path $ConfigDir 'credentials.sec'
    $mappingPath = Join-Path $ConfigDir 'mappings.json'
    $mappingCompatPath = Join-Path $ConfigDir 'mapping.json'

    $envSnapshot = @{}
    foreach ($k in $Values.Keys) {
        if ($k -notin @('ACME_KID','ACME_HMAC_SECRET','CERTIFICATE_API_KEY')) {
            $envSnapshot[$k] = [string]$Values[$k]
        }
    }
    $envSnapshot | Export-Clixml -Path $secureEnvPath

    $credObject = [pscustomobject]@{
        ACME_KID = ConvertTo-SecureString ([string]$Values.ACME_KID) -AsPlainText -Force
        ACME_HMAC_SECRET = ConvertTo-SecureString ([string]$Values.ACME_HMAC_SECRET) -AsPlainText -Force
        CERTIFICATE_API_KEY = ConvertTo-SecureString ([string]$Values.CERTIFICATE_API_KEY) -AsPlainText -Force
    }
    $credObject | Export-Clixml -Path $credPath

    if (-not (Test-Path -LiteralPath $mappingPath)) {
        @() | ConvertTo-Json | Set-Content -LiteralPath $mappingPath -Encoding UTF8
    }
    if (-not (Test-Path -LiteralPath $mappingCompatPath)) {
        @() | ConvertTo-Json | Set-Content -LiteralPath $mappingCompatPath -Encoding UTF8
    }
}

function Save-RenewalMapping {
    param(
        [Parameter(Mandatory)][string]$ConfigDir,
        [Parameter(Mandatory)][hashtable]$Mapping
    )

    $mappingPath = Join-Path $ConfigDir 'mappings.json'
    $entries = @()
    if (Test-Path -LiteralPath $mappingPath) {
        $entries = @(Get-Content -Raw -LiteralPath $mappingPath -Encoding UTF8 | ConvertFrom-Json)
    }

    $entries = @($entries | Where-Object { $_.renewalId -ne $Mapping.renewalId })
    $entries += [pscustomobject]$Mapping
    $entries | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $mappingPath -Encoding UTF8
}

function Get-ConnectorScriptByIntent {
    param([Parameter(Mandatory)][string]$TargetIntent)
    switch ($TargetIntent) {
        'rds' { return 'cert2rds.ps1' }
        'iis' { return 'cert2iis.ps1' }
        'mail' { throw 'This target type is not implemented yet.' }
        'firewall' { throw 'This target type is not implemented yet.' }
        'waf' { throw 'This target type is not implemented yet.' }
        'custom' { throw 'This target type is not implemented yet.' }
        default { throw "Unsupported target intent: $TargetIntent" }
    }
}

function Select-AcmeProviderValues {
    param([hashtable]$CurrentValues = @{})

    [Console]::WriteLine('')
    [Console]::WriteLine('Select ACME provider')
    [Console]::WriteLine('[1] Let''s Encrypt - public ACME, no EAB')
    [Console]::WriteLine('[2] Networking4All - commercial ACME, EAB required')
    [Console]::WriteLine('[3] Custom ACME server')
    [Console]::WriteLine('[0] Back')
    $providerChoice = Read-SetupChoice -Prompt 'Provider' -Options @{ '1'='letsencrypt'; '2'='networking4all'; '3'='custom'; '0'='back' } -DefaultKey '1' -AllowBack
    if ($providerChoice -in @('__CANCEL__','__BACK__','back')) { return $null }

    $values = @{
        ACME_PROVIDER = [string]$providerChoice
        ACME_REQUIRES_EAB = '0'
        ACME_KID = ''
        ACME_HMAC_SECRET = ''
        ACME_VALIDATION_MODE = 'none'
        ACME_NETWORKING4ALL_ENVIRONMENT = ''
        ACME_NETWORKING4ALL_PRODUCT = ''
    }

    if ($providerChoice -eq 'letsencrypt') {
        $defaults = Get-ProviderDefaults -Provider 'letsencrypt'
        $values.ACME_DIRECTORY = [string]$defaults.ACME_DIRECTORY
        return $values
    }

    if ($providerChoice -eq 'networking4all') {
        [Console]::WriteLine('')
        [Console]::WriteLine('Networking4All environment')
        [Console]::WriteLine('[1] Test')
        [Console]::WriteLine('[2] Production')
        [Console]::WriteLine('[0] Back')
        $envChoice = Read-SetupChoice -Prompt 'Environment' -Options @{ '1'='test'; '2'='production'; '0'='back' } -DefaultKey '2' -AllowBack
        if ($envChoice -in @('__CANCEL__','__BACK__','back')) { return $null }

        [Console]::WriteLine('')
        [Console]::WriteLine('Networking4All certificate product')
        [Console]::WriteLine('[1] dv')
        [Console]::WriteLine('[2] dv-san')
        [Console]::WriteLine('[3] dv-wildcard')
        [Console]::WriteLine('[4] dv-wildcard-san')
        [Console]::WriteLine('[5] ov')
        [Console]::WriteLine('[6] ov-san')
        [Console]::WriteLine('[7] ov-wildcard')
        [Console]::WriteLine('[8] ov-wildcard-san')
        [Console]::WriteLine('[0] Back')
        $productChoice = Read-SetupChoice -Prompt 'Product' -Options @{
            '1'='dv'; '2'='dv-san'; '3'='dv-wildcard'; '4'='dv-wildcard-san';
            '5'='ov'; '6'='ov-san'; '7'='ov-wildcard'; '8'='ov-wildcard-san'; '0'='back'
        } -DefaultKey '1' -AllowBack
        if ($productChoice -in @('__CANCEL__','__BACK__','back')) { return $null }

        $defaults = Get-ProviderDefaults -Provider 'networking4all' -Networking4AllEnvironment $envChoice -Networking4AllProduct $productChoice
        $values.ACME_NETWORKING4ALL_ENVIRONMENT = [string]$envChoice
        $values.ACME_NETWORKING4ALL_PRODUCT = [string]$productChoice
        $values.ACME_DIRECTORY = [string]$defaults.ACME_DIRECTORY
        $values.ACME_REQUIRES_EAB = '1'
        $eabState = Resolve-EabCredentialsForSetup -CurrentValues $CurrentValues -TargetValues $values
        if ($eabState -in @('__BACK__','__CANCEL__')) { return $null }
        $warnings = New-Object System.Collections.Generic.List[string]
        $domains = @()
        if ($null -ne $CurrentValues -and $CurrentValues.ContainsKey('DOMAINS')) {
            $domains = @(
                [string]$CurrentValues.DOMAINS -split ',' |
                    ForEach-Object { $_.Trim().ToLowerInvariant() } |
                    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
                    Sort-Object -Unique
            )
        }
        $hasWildcardDomain = @($domains | Where-Object { $_.StartsWith('*.') }).Count -gt 0
        $isWildcardProduct = ([string]$productChoice -like '*wildcard*')
        $isSanProduct = ([string]$productChoice -like '*-san*')

        if ($isWildcardProduct -and -not $hasWildcardDomain) {
            $warnings.Add('Selected wildcard product, but no wildcard domain (*.example.com) is configured.')
        }
        if ($hasWildcardDomain -and -not $isWildcardProduct) {
            $warnings.Add('Configured wildcard domain detected, but selected product is non-wildcard.')
        }
        if (@($domains).Count -gt 1 -and -not $isSanProduct) {
            $warnings.Add('Multiple domains detected, but selected product is non-SAN.')
        }
        if (@($domains).Count -le 1 -and $isSanProduct) {
            $warnings.Add('SAN product selected with one domain configured.')
        }

        if ($warnings.Count -gt 0) {
            [Console]::WriteLine('')
            [Console]::WriteLine('Domain/product sanity warnings:')
            foreach ($warning in $warnings) {
                [Console]::WriteLine(" - $warning")
            }
            $confirm = Read-SetupChoice -Prompt 'Continue and save these provider settings? [1] Yes [2] No' -Options @{ '1'='yes'; '2'='no' } -DefaultKey '2' -AllowBack
            if ($confirm -in @('__CANCEL__','__BACK__','no')) { return $null }
        }
        return $values
    }

    $directory = [string](Read-Host 'Custom ACME directory URL (https://...)')
    if ([string]::IsNullOrWhiteSpace($directory) -or $directory -notmatch '^https://') {
        throw 'Custom ACME directory must be an absolute HTTPS URL.'
    }
    $requiresEab = Read-SetupChoice -Prompt 'Requires EAB? [1] Yes [2] No' -Options @{ '1'='yes'; '2'='no' } -DefaultKey '2' -AllowBack
    if ($requiresEab -in @('__CANCEL__','__BACK__')) { return $null }
    $values.ACME_DIRECTORY = $directory
    if ($requiresEab -eq 'yes') {
        $values.ACME_REQUIRES_EAB = '1'
        $eabState = Resolve-EabCredentialsForSetup -CurrentValues $CurrentValues -TargetValues $values
        if ($eabState -in @('__BACK__','__CANCEL__')) { return $null }
    }
    return $values
}

function Resolve-EabCredentialsForSetup {
    param(
        [Parameter(Mandatory)][hashtable]$CurrentValues,
        [Parameter(Mandatory)][hashtable]$TargetValues
    )

    $existingKid = ''
    $existingSecret = ''
    if ($CurrentValues.ContainsKey('ACME_KID')) { $existingKid = [string]$CurrentValues.ACME_KID }
    if ($CurrentValues.ContainsKey('ACME_HMAC_SECRET')) { $existingSecret = [string]$CurrentValues.ACME_HMAC_SECRET }

    $hasKid = -not [string]::IsNullOrWhiteSpace($existingKid)
    $hasSecret = -not [string]::IsNullOrWhiteSpace($existingSecret)

    if ($hasKid -and $hasSecret) {
        [Console]::WriteLine('')
        [Console]::WriteLine('Existing EAB credentials found:')
        [Console]::WriteLine('- KID: set')
        [Console]::WriteLine('- HMAC secret: set')
        [Console]::WriteLine('')
        [Console]::WriteLine('[1] Reuse existing EAB credentials')
        [Console]::WriteLine('[2] Replace EAB credentials')
        [Console]::WriteLine('[0] Back')
        $choice = Read-SetupChoice -Prompt 'EAB credentials' -Options @{ '1'='reuse'; '2'='replace'; '0'='back' } -DefaultKey '1' -AllowBack
        if ($choice -in @('__CANCEL__','__BACK__','back')) { return '__BACK__' }
        if ($choice -eq 'reuse') {
            $TargetValues.ACME_KID = $existingKid
            $TargetValues.ACME_HMAC_SECRET = $existingSecret
            return 'ok'
        }
    } elseif ($hasKid -or $hasSecret) {
        $kidState = 'not set'
        $secretState = 'not set'
        if ($hasKid) { $kidState = 'set' }
        if ($hasSecret) { $secretState = 'set' }
        [Console]::WriteLine('')
        [Console]::WriteLine('Incomplete EAB credentials found:')
        [Console]::WriteLine("- KID: $kidState")
        [Console]::WriteLine("- HMAC secret: $secretState")
        [Console]::WriteLine('Networking4All requires both values.')
    }

    $kid = [string](Read-Host 'ACME_KID')
    if ([string]::IsNullOrWhiteSpace($kid)) { return '__BACK__' }
    $secret = [string](Read-Host 'ACME_HMAC_SECRET')
    if ([string]::IsNullOrWhiteSpace($secret)) { return '__BACK__' }
    $TargetValues.ACME_KID = $kid
    $TargetValues.ACME_HMAC_SECRET = $secret
    return 'ok'
}

function Assert-SavedEnvMatchesSetup {
    param(
        [Parameter(Mandatory)][hashtable]$Expected,
        [Parameter(Mandatory)][hashtable]$Actual
    )
    foreach ($key in @('ACME_PROVIDER','ACME_DIRECTORY','ACME_REQUIRES_EAB','ACME_NETWORKING4ALL_ENVIRONMENT','ACME_NETWORKING4ALL_PRODUCT','DOMAINS')) {
        $expectedValue = ''
        $actualValue = ''
        if ($Expected.ContainsKey($key)) { $expectedValue = [string]$Expected[$key] }
        if ($Actual.ContainsKey($key)) { $actualValue = [string]$Actual[$key] }
        if ($expectedValue -ne $actualValue) {
            throw "Saved environment mismatch for '$key'. expected='$expectedValue' actual='$actualValue'"
        }
    }
}

function Invoke-AcmeForm {
    param([string]$EnvFilePath)

    $resolvedEnvFilePath = if ([string]::IsNullOrWhiteSpace([string]$EnvFilePath)) {
        Resolve-BootstrapEnvPath -ProjectRoot (Split-Path $PSScriptRoot -Parent)
    } else {
        [System.IO.Path]::GetFullPath($EnvFilePath)
    }

    $curr = @{}
    if (Test-Path -LiteralPath $resolvedEnvFilePath) { $curr = Read-EnvFile -Path $resolvedEnvFilePath }

    [Console]::WriteLine('')
    [Console]::WriteLine('What do you want to secure?')
    [Console]::WriteLine('[1] Remote Desktop Gateway (RDS)')
    [Console]::WriteLine('[2] Website (IIS)')
    [Console]::WriteLine('[3] Mail server (SMTP/IMAP/POP3)')
    [Console]::WriteLine('[4] Firewall / VPN')
    [Console]::WriteLine('[5] Load balancer / WAF')
    [Console]::WriteLine('[6] Custom system')
    $target = Read-SetupChoice -Prompt 'Target' -Options @{ '1'='rds'; '2'='iis'; '3'='mail'; '4'='firewall'; '5'='waf'; '6'='custom' } -DefaultKey '1'
    if ($target -in @('__CANCEL__','__BACK__')) {
        Show-TuiStatus -Message 'Setup cancelled.' -Type Warning -Row ([Console]::WindowHeight-2)
        return $null
    }

    [Console]::WriteLine('')
    [Console]::WriteLine('Where is this system located?')
    [Console]::WriteLine('[1] This server')
    [Console]::WriteLine('[2] Another server / device')
    [Console]::WriteLine('[3] Multiple systems (cluster / farm)')
    $location = Read-SetupChoice -Prompt 'Location' -Options @{ '1'='this-server'; '2'='another-server'; '3'='cluster-farm' } -DefaultKey '1' -AllowBack
    if ($location -in @('__CANCEL__','__BACK__')) {
        Show-TuiStatus -Message 'Setup cancelled.' -Type Warning -Row ([Console]::WindowHeight-2)
        return $null
    }

    [Console]::WriteLine('')
    [Console]::WriteLine('Will this certificate be used on more than one system?')
    [Console]::WriteLine('[1] No - only this system (recommended, most secure)')
    [Console]::WriteLine('[2] Yes - multiple systems (RDS farm, firewall, etc.)')
    $distributionMode = Read-SetupChoice -Prompt 'Distribution' -Options @{ '1'='single'; '2'='multi' } -DefaultKey '1' -AllowBack
    if ($distributionMode -in @('__CANCEL__','__BACK__')) {
        Show-TuiStatus -Message 'Setup cancelled.' -Type Warning -Row ([Console]::WindowHeight-2)
        return $null
    }

    $values = @{}
    $domains = Read-DomainsInput
    if ($domains -in @('__CANCEL__','__BACK__')) {
        Show-TuiStatus -Message 'Setup cancelled.' -Type Warning -Row ([Console]::WindowHeight-2)
        return $null
    }
    $providerValues = Select-AcmeProviderValues -CurrentValues $curr
    if ($null -eq $providerValues) {
        Show-TuiStatus -Message 'Setup cancelled.' -Type Warning -Row ([Console]::WindowHeight-2)
        return $null
    }
    $values.DOMAINS = $domains
    foreach ($k in $providerValues.Keys) { $values[$k] = $providerValues[$k] }
    $values.ACME_SOURCE_PLUGIN = 'manual'
    $values.ACME_ORDER_PLUGIN = 'single'
    $values.ACME_INSTALLATION_PLUGINS = 'script'
    $values.ACME_VALIDATION_MODE = 'none'
    $values.ACME_ACCOUNT_NAME = ''
    $values.ACME_SCRIPT_PARAMETERS = '{CertThumbprint}'
    $selectedScriptPath = Resolve-DeploymentScriptPath -ScriptFileName (Get-ConnectorScriptByIntent -TargetIntent $target)
    $values.ACME_SCRIPT_PATH = $selectedScriptPath
    if ($curr.ContainsKey('ACME_SCRIPT_PATH')) {
        $existingScriptPath = Resolve-AbsoluteSetupPath -PathValue ([string]$curr.ACME_SCRIPT_PATH)
        $sameTarget = $curr.ContainsKey('ACME_TARGET_SYSTEM') -and ([string]$curr.ACME_TARGET_SYSTEM).ToLowerInvariant() -eq $target
        if ($sameTarget -and -not [string]::IsNullOrWhiteSpace($existingScriptPath) -and (Test-Path -LiteralPath $existingScriptPath -PathType Leaf)) {
            $values.ACME_SCRIPT_PATH = $existingScriptPath
        }
    }

    $previousExportable = ''
    if ($curr.ContainsKey('ACME_PRIVATEKEY_EXPORTABLE')) { $previousExportable = ([string]$curr.ACME_PRIVATEKEY_EXPORTABLE).ToLowerInvariant() }
    $previousStrategy = if ($curr.ContainsKey('ACME_PRIVATE_KEY_STRATEGY')) { ([string]$curr.ACME_PRIVATE_KEY_STRATEGY).ToLowerInvariant() } else { '' }

    if ($distributionMode -eq 'multi') {
        [Console]::WriteLine('')
        [Console]::WriteLine('Certificates used on multiple systems require access to the private key.')
        [Console]::WriteLine('Choose distribution method:')
        [Console]::WriteLine('[1] Windows systems (enable exportable key)')
        [Console]::WriteLine('[2] Appliances / mixed systems (use PFX distribution) [recommended]')
        $multiChoice = Read-SetupChoice -Prompt 'Multi-system key mode' -Options @{ '1'='exportable'; '2'='pfx' } -DefaultKey '2' -AllowBack
        if ($multiChoice -in @('__CANCEL__','__BACK__')) {
            Show-TuiStatus -Message 'Setup cancelled.' -Type Warning -Row ([Console]::WindowHeight-2)
            return $null
        }
        [Console]::WriteLine('Enabling exportable keys reduces security because private keys can be transferred.')
        $continue = Read-SetupChoice -Prompt 'Continue? [1] Yes [2] No' -Options @{ '1'='yes'; '2'='no' } -DefaultKey '2' -AllowBack
        if ($continue -in @('__CANCEL__','__BACK__','no')) {
            Show-TuiStatus -Message 'Setup cancelled.' -Type Warning -Row ([Console]::WindowHeight-2)
            return $null
        }

        if ($multiChoice -eq 'exportable') {
            $values.ACME_PRIVATE_KEY_STRATEGY = 'exportable-store'
            $values.ACME_STORE_PLUGIN = 'certificatestore'
            $values.ACME_PRIVATEKEY_EXPORTABLE = 'true'
            $values.Store_CertificateStore_PrivateKeyExportable = 'true'
        } else {
            $values.ACME_PRIVATE_KEY_STRATEGY = 'pfx-distribution'
            $values.ACME_STORE_PLUGIN = 'pfxfile'
            $values.ACME_PRIVATEKEY_EXPORTABLE = 'false'
            $values.Store_CertificateStore_PrivateKeyExportable = 'false'
            $values.ACME_INSTALLATION_PLUGINS = 'script,pfxfile'
        }

        $values.ACME_RENEWAL_MODE = 'multi-endpoint'
        [Console]::WriteLine('This setting only applies to new certificates.')
        $reissueChoice = Read-SetupChoice -Prompt '[1] Apply on next renewal [2] Re-issue certificate now' -Options @{ '1'='next-renewal'; '2'='force-reissue' } -DefaultKey '1' -AllowBack
        if ($reissueChoice -in @('__CANCEL__','__BACK__')) {
            Show-TuiStatus -Message 'Setup cancelled.' -Type Warning -Row ([Console]::WindowHeight-2)
            return $null
        }
        $values.ACME_REISSUE_STRATEGY = $reissueChoice
    } else {
        $values.ACME_RENEWAL_MODE = 'single-system'
        $values.ACME_PRIVATE_KEY_STRATEGY = 'local-store'
        $values.ACME_STORE_PLUGIN = 'certificatestore'
        $values.ACME_PRIVATEKEY_EXPORTABLE = 'false'
        $values.ACME_REISSUE_STRATEGY = 'next-renewal'
    }

    $values.TARGET_SYSTEM = $target
    $values.TARGET_LOCATION = $location
    $values.ACME_TARGET_SYSTEM = $target
    $values.ACME_TARGET_LOCATION = $location
    $values.ACME_WACS_RETRY_ATTEMPTS = '3'
    $values.ACME_WACS_RETRY_DELAY_SECONDS = '2'

    Assert-AcmeSetupValues -Values $values

    Write-EnvFile -Values $values -Path $resolvedEnvFilePath
    $reloaded = Read-EnvFile -Path $resolvedEnvFilePath
    Assert-SavedEnvMatchesSetup -Expected $values -Actual $reloaded
    if ([string]$reloaded.ACME_PROVIDER -eq 'networking4all' -and [string]$reloaded.ACME_DIRECTORY -match 'letsencrypt') {
        throw @'
Internal state mismatch: selected provider is Networking4All but ACME_DIRECTORY is not Networking4All.
Setup was not saved.
'@
    }
    if ([string]$reloaded.ACME_PROVIDER -eq 'networking4all' -and ([string]$reloaded.ACME_REQUIRES_EAB -ne '1')) {
        throw "Saved environment mismatch for 'ACME_REQUIRES_EAB'. expected='1' actual='$([string]$reloaded.ACME_REQUIRES_EAB)'"
    }
    Show-TuiStatus -Message 'Saved bootstrap certificate.env for initial simple-acme setup.' -Type Success -Row ([Console]::WindowHeight-2)
    return $reloaded
}

function Invoke-ManageCertificatesMenu {
    param([Parameter(Mandatory)][string]$ConfigDir)

    while ($true) {
        [Console]::WriteLine('')
        [Console]::WriteLine('Manage existing certificates')
        [Console]::WriteLine('[0] Back')
        [Console]::WriteLine('[1] View configured certificates')
        [Console]::WriteLine('[2] Reissue certificate (placeholder)')
        [Console]::WriteLine('[3] Change targets (placeholder)')
        [Console]::WriteLine('[4] Test deployment (placeholder)')
        [Console]::WriteLine('[5] Remove certificate mapping')
        $choice = Read-SetupChoice -Prompt 'Manage' -Options @{
            '0'='back'
            '1'='view'
            '2'='reissue'
            '3'='targets'
            '4'='test'
            '5'='remove'
        } -DefaultKey '0'

        if ($choice -in @('back','__CANCEL__','__BACK__')) { return }

        $simpleAcmeDir = Get-SimpleAcmeDataRoot
        $mappings = @()
        if (Test-Path -LiteralPath $simpleAcmeDir) {
            $mappings = @(Get-ChildItem -LiteralPath $simpleAcmeDir -Filter '*.renewal.json' -File -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
                $domains = @()
                $installPlugin = ''
                $scriptPath = ''
                $nextInfo = ''
                try {
                    $obj = Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
                    if ($obj.PSObject.Properties['Host']) { $domains += @([string]$obj.Host -split ',') }
                    if ($obj.PSObject.Properties['Hosts']) { $domains += @($obj.Hosts) }
                    if ($obj.PSObject.Properties['Installation'] -and $obj.Installation.PSObject.Properties['Plugin']) { $installPlugin = [string]$obj.Installation.Plugin }
                    if ($obj.PSObject.Properties['Installation'] -and $obj.Installation.PSObject.Properties['PluginOptions'] -and $obj.Installation.PluginOptions.PSObject.Properties['Script']) { $scriptPath = [string]$obj.Installation.PluginOptions.Script }
                    if ($obj.PSObject.Properties['History'] -and $obj.History.PSObject.Properties['LastRenewal']) { $nextInfo = [string]$obj.History.LastRenewal }
                } catch {}
                [pscustomobject]@{
                    renewalFile = $_.FullName
                    renewalId = $_.BaseName
                    domains = @($domains | ForEach-Object { [string]$_ } | Where-Object { $_ } | Sort-Object -Unique)
                    scriptPath = $scriptPath
                    installationPlugin = $installPlugin
                    nextRenewalInfo = $nextInfo
                }
            })
        }

        switch ($choice) {
            'view' {
                [Console]::WriteLine('')
                [Console]::WriteLine('Configured certificates')
                [Console]::WriteLine('-----------------------')

                if (@($mappings).Count -eq 0) {
                    [Console]::WriteLine('No simple-acme renewals found yet.')
                    [Console]::WriteLine('Run setup and initial issuance first.')
                } else {
                    foreach ($m in $mappings) {
                        [Console]::WriteLine("renewal=$($m.renewalFile)")
                        [Console]::WriteLine(" - domains=$($(if (@($m.domains).Count -gt 0) { @($m.domains) -join ',' } else { '(unknown)' }))")
                        [Console]::WriteLine(" - script=$($m.scriptPath)")
                        [Console]::WriteLine(" - installation=$($m.installationPlugin)")
                        [Console]::WriteLine(" - next renewal info=$($m.nextRenewalInfo)")
                    }
                }

                Wait-ForOperatorReturn
            }
            'reissue' {
                [Console]::WriteLine('')
                [Console]::WriteLine('Reissue is not implemented yet.')
                Wait-ForOperatorReturn
            }
            'targets' {
                [Console]::WriteLine('')
                [Console]::WriteLine('Change targets is not implemented yet.')
                Wait-ForOperatorReturn
            }
            'test' {
                [Console]::WriteLine('')
                [Console]::WriteLine('Test deployment is not implemented yet.')
                Wait-ForOperatorReturn
            }
            'remove' {
                [Console]::WriteLine('')
                [Console]::WriteLine('Remove mapping is not implemented yet.')
                Wait-ForOperatorReturn
            }
        }
    }
}

function Get-PolicyFilePathLegacy {
    param([Parameter(Mandatory)][string]$ConfigDir)
    return (Join-Path $ConfigDir 'policies.json')
}

function Read-Policies {
    param([Parameter(Mandatory)][string]$ConfigDir)
    $path = Get-PolicyFilePath -ConfigDir $ConfigDir
    if (-not (Test-Path -LiteralPath $path)) {
        $tmpInit = [System.IO.Path]::GetTempFileName()
        [System.IO.File]::WriteAllText($tmpInit, '[]', [System.Text.Encoding]::UTF8)
        Move-Item -LiteralPath $tmpInit -Destination $path -Force
    }

    $raw = Get-Content -Raw -Encoding UTF8 -Path $path
    try {
        return @($raw | ConvertFrom-Json)
    } catch {
        throw "Failed to parse policies.json. Fix JSON format before continuing. File: $path. Error: $($_.Exception.Message)"
    }
}

function Save-PoliciesLegacy {
    param(
        [Parameter(Mandatory)][string]$ConfigDir,
        [Parameter(Mandatory)][object[]]$Policies
    )
    $path = Get-PolicyFilePath -ConfigDir $ConfigDir
    $tmp = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($tmp, ($Policies | ConvertTo-Json -Depth 20), [System.Text.Encoding]::UTF8)
    Move-Item -LiteralPath $tmp -Destination $path -Force
}

function Test-FanoutPolicyValue {
    param([Parameter(Mandatory)][string]$FanoutPolicy)
    return @('fail-fast','best-effort','quorum') -contains $FanoutPolicy
}

function Test-QuorumThreshold {
    param(
        [string]$FanoutPolicy,
        [string]$ThresholdText,
        [int]$ConnectorCount = 0
    )

    $isQuorum = ([string]$FanoutPolicy).ToLowerInvariant() -eq 'quorum'
    if (-not $isQuorum) {
        return @{ IsValid = $true; Value = $null; Warning = $null; Error = $null }
    }

    if ([string]::IsNullOrWhiteSpace($ThresholdText)) {
        return @{ IsValid = $false; Value = $null; Warning = $null; Error = 'quorum_threshold is required when fanout_policy is quorum.' }
    }

    $parsed = 0
    if (-not [int]::TryParse($ThresholdText, [ref]$parsed)) {
        return @{ IsValid = $false; Value = $null; Warning = $null; Error = 'quorum_threshold must be a whole number.' }
    }
    if ($parsed -lt 1) {
        return @{ IsValid = $false; Value = $null; Warning = $null; Error = 'quorum_threshold must be greater than or equal to 1.' }
    }
    if ($ConnectorCount -gt 0 -and $parsed -gt $ConnectorCount) {
        return @{ IsValid = $false; Value = $null; Warning = $null; Error = "quorum_threshold ($parsed) cannot exceed connector count ($ConnectorCount)." }
    }

    $warning = $null
    if ($ConnectorCount -eq 0) {
        $warning = 'No connectors are assigned yet. quorum_threshold >= 1 is accepted but will not be enforceable until connectors are configured.'
    }

    return @{ IsValid = $true; Value = $parsed; Warning = $warning; Error = $null }
}

function Show-PoliciesView {
    param([object[]]$Policies = @())
    $Policies = @($Policies)
    if ($Policies.Count -eq 0) {
        [Console]::WriteLine('No deployment policies exist yet.')
        return
    }

    foreach ($policy in $Policies) {
        $connectors = @($policy.connectors)
        $connectorNames = @($connectors | ForEach-Object {
            if ($_.PSObject.Properties.Match('label').Count -gt 0 -and -not [string]::IsNullOrWhiteSpace([string]$_.label)) { [string]$_.label } else { [string]$_.connector_type }
        }) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        $connectorLabel = if ($connectorNames.Count -gt 0) { $connectorNames -join ', ' } else { '(none)' }
        [Console]::WriteLine("policy_id={0} fanout={1} quorum={2} connectors={3} [{4}]" -f $policy.policy_id, $policy.fanout_policy, $policy.quorum_threshold, $connectors.Count, $connectorLabel)
    }
}

function Invoke-PolicyEditorLegacy {
    param(
        [string]$ConfigDir,
        [switch]$ViewOnly
    )

    $policies = Read-Policies -ConfigDir $ConfigDir
    if ($ViewOnly) {
        Show-PoliciesView -Policies $policies
        return $policies
    }

    while($true){
        $choice = Read-Host 'Policy editor: [N]ew [E]dit [D]elete [V]iew [Q]uit'
        if($choice -match '^[Qq]'){ break }

        if($choice -match '^[Vv]'){
            Show-PoliciesView -Policies $policies
            continue
        }

        if(($choice -match '^[EeDd]') -and $policies.Count -eq 0){
            Write-Warning 'No policies exist. Routing to create policy flow.'
            $choice = 'N'
        }

        if($choice -match '^[Nn]'){
            $pid = Read-Host 'policy_id'
            if ([string]::IsNullOrWhiteSpace($pid)) { Write-Warning 'policy_id is required.'; continue }

            $fanoutHelp = 'fanout_policy [fail-fast | best-effort | quorum]'
            $fanout = ''
            while (-not (Test-FanoutPolicyValue -FanoutPolicy $fanout)) {
                $fanout = ([string](Read-Host $fanoutHelp)).Trim().ToLowerInvariant()
                if (-not (Test-FanoutPolicyValue -FanoutPolicy $fanout)) {
                    Write-Warning "Invalid fanout_policy '$fanout'. Accepted values: fail-fast, best-effort, quorum."
                }
            }

            $connectors = @()
            $thresholdText = Read-Host 'quorum_threshold (required only for quorum)'
            $validation = Test-QuorumThreshold -FanoutPolicy $fanout -ThresholdText $thresholdText -ConnectorCount $connectors.Count
            while (-not $validation.IsValid) {
                Write-Warning $validation.Error
                $thresholdText = Read-Host 'quorum_threshold (required only for quorum)'
                $validation = Test-QuorumThreshold -FanoutPolicy $fanout -ThresholdText $thresholdText -ConnectorCount $connectors.Count
            }
            if ($validation.Warning) { Write-Warning $validation.Warning }

            $policies += [pscustomobject]@{
                policy_id = $pid
                fanout_policy = $fanout
                quorum_threshold = $validation.Value
                connectors = @()
            }
            continue
        }

        if($choice -match '^[Dd]'){
            $pid=Read-Host 'policy_id to delete'
            if ([string]::IsNullOrWhiteSpace($pid)) { Write-Warning 'policy_id is required.'; continue }
            $existingCount = $policies.Count
            $policies=@($policies | Where-Object { $_.policy_id -ne $pid })
            if ($policies.Count -eq $existingCount) { Write-Warning "Policy '$pid' not found. Returning to editor."; continue }
            continue
        }

        if($choice -match '^[Ee]'){
            $pid=Read-Host 'policy_id to edit'
            $p=@($policies|Where-Object{$_.policy_id -eq $pid}) | Select-Object -First 1
            if($null -eq $p){ Write-Warning "Policy '$pid' not found. Returning to editor."; continue }

            $fanoutInput = Read-Host "fanout_policy [$($p.fanout_policy)] (accepted: fail-fast | best-effort | quorum)"
            $fanout = if ([string]::IsNullOrWhiteSpace($fanoutInput)) { [string]$p.fanout_policy } else { $fanoutInput.Trim().ToLowerInvariant() }
            while (-not (Test-FanoutPolicyValue -FanoutPolicy $fanout)) {
                Write-Warning "Invalid fanout_policy '$fanout'. Accepted values: fail-fast, best-effort, quorum."
                $fanout = ([string](Read-Host 'fanout_policy')).Trim().ToLowerInvariant()
            }

            $conn=Read-Host 'connector types comma-separated (optional)'
            $connectorCount = @($p.connectors).Count
            if($conn){
                $p.connectors=@($conn -split ',' | ForEach-Object { [pscustomobject]@{ connector_type=$_.Trim(); label=$_.Trim(); settings=@{} } })
                $connectorCount = @($p.connectors).Count
            }

            $currentThresholdText = if ($null -ne $p.quorum_threshold) { [string]$p.quorum_threshold } else { '' }
            $qInput = Read-Host "quorum_threshold [$currentThresholdText] (required only for quorum)"
            $thresholdText = if ([string]::IsNullOrWhiteSpace($qInput)) { $currentThresholdText } else { $qInput }
            $validation = Test-QuorumThreshold -FanoutPolicy $fanout -ThresholdText $thresholdText -ConnectorCount $connectorCount
            while (-not $validation.IsValid) {
                Write-Warning $validation.Error
                $thresholdText = Read-Host 'quorum_threshold (required only for quorum)'
                $validation = Test-QuorumThreshold -FanoutPolicy $fanout -ThresholdText $thresholdText -ConnectorCount $connectorCount
            }
            if ($validation.Warning) { Write-Warning $validation.Warning }

            $p.fanout_policy = $fanout
            $p.quorum_threshold = $validation.Value
            continue
        }
    }

    Save-Policies -ConfigDir $ConfigDir -Policies $policies
    return $policies
}


function Invoke-FirstRunWizard {
    param([Parameter(Mandatory)][string]$DefaultEnvPath)

    $scriptRoot = Split-Path $PSScriptRoot -Parent

    $acmeFields = @(
        @{ Name='ACME_PROVIDER';    Label='ACME provider';              Type='string'; Required=$false; Placeholder='letsencrypt'; HelpText='Provider label for operator reference' },
        @{ Name='ACME_DIRECTORY';   Label='ACME directory URL';         Type='string'; Required=$true; Placeholder='https://acme-v02.api.letsencrypt.org/directory'; HelpText='ACME directory endpoint' },
        @{ Name='ACME_REQUIRES_EAB';Label='Requires EAB (0/1)';         Type='string'; Required=$false; Placeholder='0'; HelpText='Set 1 only when provider requires EAB' },
        @{ Name='ACME_KID';         Label='ACME KID (EAB key ID)';      Type='secret'; Required=$false; Placeholder=''; HelpText='External account binding key identifier' },
        @{ Name='ACME_HMAC_SECRET'; Label='ACME HMAC secret';           Type='secret'; Required=$false; Placeholder=''; HelpText='External account binding HMAC secret' },
        @{ Name='DOMAINS';          Label='Domains (comma-separated)';  Type='string'; Required=$true; Placeholder='example.com,www.example.com'; HelpText='Domain list for certificate issuance' },
        @{ Name='TARGET_SYSTEM';    Label='Target system';              Type='string'; Required=$false; Placeholder='rds'; HelpText='phase 1 targets: rds or iis' },
        @{ Name='TARGET_LOCATION';  Label='Target location';            Type='string'; Required=$false; Placeholder='this-server'; HelpText='this-server, another-server, or cluster-farm' },
        @{ Name='ACME_ACCOUNT_NAME';Label='Account name';               Type='string'; Required=$false; Placeholder=''; HelpText='Optional ACME account name' },
        @{ Name='ACME_CSR_ALGORITHM';Label='CSR algorithm';             Type='string'; Required=$false; Placeholder='ec'; HelpText='ec (default) or rsa' },
        @{ Name='ACME_PRIVATEKEY_EXPORTABLE';Label='Private key exportable'; Type='string'; Required=$false; Placeholder='false'; HelpText='Only true for multi-system or export workflows' }
    )

    Clear-TuiScreen

    $acmeValues = Show-TuiForm -Fields $acmeFields -CurrentValues @{} -Title 'Bootstrap certificate.env (phase 1)'
    if ($null -eq $acmeValues) { return $null }

    $all = @{}
    foreach ($k in $acmeValues.Keys) { $all[$k] = $acmeValues[$k] }
    if (-not $all.ContainsKey('ACME_PROVIDER') -or [string]::IsNullOrWhiteSpace([string]$all.ACME_PROVIDER)) { $all.ACME_PROVIDER = 'letsencrypt' }
    if (-not $all.ContainsKey('ACME_REQUIRES_EAB') -or [string]::IsNullOrWhiteSpace([string]$all.ACME_REQUIRES_EAB)) { $all.ACME_REQUIRES_EAB = '0' }
    if (-not $all.ContainsKey('TARGET_SYSTEM') -or [string]::IsNullOrWhiteSpace([string]$all.TARGET_SYSTEM)) { $all.TARGET_SYSTEM = 'rds' }
    if (-not $all.ContainsKey('TARGET_LOCATION') -or [string]::IsNullOrWhiteSpace([string]$all.TARGET_LOCATION)) { $all.TARGET_LOCATION = 'this-server' }
    if (-not $all.ContainsKey('ACME_CSR_ALGORITHM') -or [string]::IsNullOrWhiteSpace([string]$all.ACME_CSR_ALGORITHM)) { $all.ACME_CSR_ALGORITHM = 'ec' }
    if (-not $all.ContainsKey('ACME_PRIVATEKEY_EXPORTABLE') -or [string]::IsNullOrWhiteSpace([string]$all.ACME_PRIVATEKEY_EXPORTABLE)) { $all.ACME_PRIVATEKEY_EXPORTABLE = 'false' }

    Write-EnvFile -Values $all -Path $DefaultEnvPath

    Show-TuiStatus -Message "Wizard complete. certificate.env saved to: $DefaultEnvPath" `
        -Type Success -Row ([Math]::Max(0, [Console]::WindowHeight) - 2)
    Start-Sleep -Milliseconds 1500

    return $DefaultEnvPath
}

$FunctionsToExport = @(
    'Invoke-DeviceForm',
    'Invoke-AcmeForm',
    'Invoke-AcmeSettingsMenu',
    'Invoke-PolicyEditor',
    'Invoke-PolicyViewer',
    'Invoke-FirstRunWizard',
    'Test-FanoutPolicyValue',
    'Test-QuorumThreshold',
    'Show-PoliciesView',
    'Read-Policies',
    'Invoke-ManageCertificatesMenu',
    'Get-SimpleAcmeLogLocations',
    'Show-SimpleAcmeDiagnosticSummary',
    'Invoke-ViewLogsDiagnostics',
    'Wait-ForOperatorReturn',
    'Resolve-EabCredentialsForSetup',
    'Assert-SavedEnvMatchesSetup',
    'Get-ProviderDefaults'
)

$FunctionsToExport = @($FunctionsToExport | Where-Object {
    -not [string]::IsNullOrWhiteSpace([string]$_)
} | ForEach-Object {
    [string]$_
})

foreach ($fn in $FunctionsToExport) {
    if (-not (Get-Command -Name $fn -CommandType Function -ErrorAction SilentlyContinue)) {
        throw "Export list contains missing function: $fn"
    }
}

Export-ModuleMember -Function $FunctionsToExport
