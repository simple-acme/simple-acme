. "$PSScriptRoot/Device-Schemas.ps1"
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

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
            $base.ACME_VALIDATION_MODE = 'none'
            $base.ACME_INSTALLATION_PLUGINS = 'script'
            $base.ACME_SCRIPT_PATH = Join-Path (Split-Path $PSScriptRoot -Parent) 'dist/Scripts/ImportRDSFull.ps1'
            $base.ACME_SCRIPT_PARAMETERS = $script:DefaultScriptParameters
        }
        default {
            throw "Unsupported guided target '$TargetSystem'."
        }
    }
    return $base
}

function Get-ProviderDefaults {
    param([Parameter(Mandatory)][string]$Provider)

    switch ($Provider) {
        'networking4all' { return @{ ACME_DIRECTORY='https://acme.networking4all.com/dv'; RequiresEab=$true; ForceValidation='none' } }
        'letsencrypt'    { return @{ ACME_DIRECTORY='https://acme-v02.api.letsencrypt.org/directory'; RequiresEab=$false; ForceValidation='' } }
        'custom'         { return @{ ACME_DIRECTORY=''; RequiresEab=$false; ForceValidation='' } }
        default { throw "Unsupported provider '$Provider'." }
    }
}

function Read-DomainsInput {
    while ($true) {
        [Console]::WriteLine('Enter domain(s), one per line. Submit an empty line to finish:')
        $lines = New-Object System.Collections.Generic.List[string]
        while ($true) {
            $line = [string](Read-Host 'domain')
            if ([string]::IsNullOrWhiteSpace($line)) { break }
            $lines.Add($line.Trim())
        }
        $domains = @($lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        if ($domains.Count -gt 0) { return ($domains -join ',') }
        Write-Warning 'At least one domain is required.'
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

    $installations = @([string]$Values.ACME_INSTALLATION_PLUGINS -split ',' | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
    if ($installations -contains 'script') {
        $scriptPath = [string]$Values.ACME_SCRIPT_PATH
        if ([string]::IsNullOrWhiteSpace($scriptPath)) { throw 'Script installation selected but ACME_SCRIPT_PATH is empty.' }
        if (-not (Test-Path -LiteralPath $scriptPath)) { throw "Script installation selected but script path does not exist: $scriptPath" }
        if ([string]::IsNullOrWhiteSpace([string]$Values.ACME_SCRIPT_PARAMETERS)) { throw 'Script installation selected but ACME_SCRIPT_PARAMETERS is empty.' }
    }

    if ($Values.ACME_REQUIRES_EAB -eq '1') {
        if ([string]::IsNullOrWhiteSpace([string]$Values.ACME_KID)) { throw 'Provider requires EAB. ACME_KID is required.' }
        if ([string]::IsNullOrWhiteSpace([string]$Values.ACME_HMAC_SECRET)) { throw 'Provider requires EAB. ACME_HMAC_SECRET is required.' }
    }
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
    param([Parameter(Mandatory)][object[]]$Policies)
    if (@($Policies).Count -eq 0) { return @('No deployment policies found.') }
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
    Clear-TuiScreen
    $bounds = Get-TuiLayoutBounds
    $height = [Math]::Min($bounds.ContentHeight, [Math]::Max(8, $lines.Count + 4))
    Write-TuiBox -X $bounds.BoxX -Y $bounds.BoxY -Width $bounds.BoxWidth -Height $height -Title ' Existing deployment policies '
    $visible = [Math]::Max(1, $height - 2)
    for ($i=0; $i -lt [Math]::Min($visible, $lines.Count); $i++) {
        Write-TuiAt -X ($bounds.BoxX + 2) -Y ($bounds.BoxY + 1 + $i) -Text (Get-TuiClippedText -Text $lines[$i] -Width ($bounds.BoxWidth - 4))
    }
    Show-TuiStatus -Message 'Press Enter/Esc to return.' -Type Info -Row $bounds.HelpRow
    do { $k = Read-TuiKey } while ($k.Key -notin @([ConsoleKey]::Enter,[ConsoleKey]::Escape,[ConsoleKey]::Backspace))
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

function Invoke-AcmeForm {
    param([string]$EnvFilePath)

    $curr = @{}
    if ($EnvFilePath -and (Test-Path -LiteralPath $EnvFilePath)) { $curr = Read-EnvFile -Path $EnvFilePath }

    [Console]::WriteLine('')
    [Console]::WriteLine('Select setup mode:')
    [Console]::WriteLine('')
    [Console]::WriteLine('[1] Guided (recommended)')
    [Console]::WriteLine('[2] Advanced (expert)')
    [Console]::WriteLine('')

    $mode = Read-MenuChoice -Prompt 'Mode' -Options @{ '1'='guided'; '2'='advanced' } -DefaultKey '1'
    $values = @{}
    if ($mode -eq 'guided') {
        [Console]::WriteLine('')
        [Console]::WriteLine('What do you want to secure?')
        [Console]::WriteLine('[1] IIS website')
        [Console]::WriteLine('[2] RDS Gateway')
        [Console]::WriteLine('[3] Other / custom')
        $target = Read-MenuChoice -Prompt 'Target' -Options @{ '1'='iis'; '2'='rds'; '3'='custom' } -DefaultKey '1'

        [Console]::WriteLine('')
        [Console]::WriteLine('Certificate provider:')
        [Console]::WriteLine('[1] Networking4All (recommended)')
        [Console]::WriteLine("[2] Let's Encrypt")
        [Console]::WriteLine('[3] Custom ACME server')
        $provider = Read-MenuChoice -Prompt 'Provider' -Options @{ '1'='networking4all'; '2'='letsencrypt'; '3'='custom' } -DefaultKey '1'
        $providerDefaults = Get-ProviderDefaults -Provider $provider

        $domains = Read-DomainsInput
        $values.DOMAINS = $domains
        $values.ACME_DIRECTORY = if ($provider -eq 'custom') { [string](Read-Host 'ACME directory URL') } else { [string]$providerDefaults.ACME_DIRECTORY }

        if ([bool]$providerDefaults.RequiresEab) {
            $values.ACME_KID = [string](Read-Host 'Enter KID')
            $values.ACME_HMAC_SECRET = [string](Read-Host 'Enter HMAC key')
            $values.ACME_REQUIRES_EAB = '1'
        } else {
            $values.ACME_KID = if ($curr.ContainsKey('ACME_KID')) { [string]$curr.ACME_KID } else { '' }
            $values.ACME_HMAC_SECRET = if ($curr.ContainsKey('ACME_HMAC_SECRET')) { [string]$curr.ACME_HMAC_SECRET } else { '' }
            $values.ACME_REQUIRES_EAB = '0'
        }

        $validationMode = [string]$providerDefaults.ForceValidation
        if ([string]::IsNullOrWhiteSpace($validationMode)) {
            if ($target -eq 'iis') {
                $validationMode = 'http-01'
            } elseif ($target -eq 'rds') {
                $validationMode = 'none'
            } else {
                $validationMode = Read-MenuChoice -Prompt 'Validation mode [1=http-01,2=dns-01,3=tls-alpn-01,4=none]' -Options @{
                    '1'='http-01'; '2'='dns-01'; '3'='tls-alpn-01'; '4'='none'
                } -DefaultKey '4'
            }
        }

        if ($target -in @('iis','rds')) {
            $pipeline = Get-GuidedPipelineTemplate -TargetSystem $target -ValidationMode $validationMode
            foreach ($key in $pipeline.Keys) { $values[$key] = [string]$pipeline[$key] }
            if ($target -eq 'iis') {
                $addScript = Read-MenuChoice -Prompt 'Add optional deployment script? [1=No,2=Yes]' -Options @{ '1'='no'; '2'='yes' } -DefaultKey '1'
                if ($addScript -eq 'yes') {
                    $values.ACME_INSTALLATION_PLUGINS = 'iis,script'
                    $defaultScript = if ($curr.ContainsKey('ACME_SCRIPT_PATH')) { [string]$curr.ACME_SCRIPT_PATH } else { Join-Path (Split-Path $PSScriptRoot -Parent) 'dist/Scripts/New-CertificateDropFile.ps1' }
                    $values.ACME_SCRIPT_PATH = [string](Read-Host "Script path [$defaultScript]")
                    if ([string]::IsNullOrWhiteSpace($values.ACME_SCRIPT_PATH)) { $values.ACME_SCRIPT_PATH = $defaultScript }
                    $values.ACME_SCRIPT_PARAMETERS = [string](Read-Host "Script parameters [$script:DefaultScriptParameters]")
                    if ([string]::IsNullOrWhiteSpace($values.ACME_SCRIPT_PARAMETERS)) { $values.ACME_SCRIPT_PARAMETERS = $script:DefaultScriptParameters }
                }
            }
        } else {
            $source = [string](Read-Host 'Source plugin')
            $order = [string](Read-Host 'Order plugin [single]')
            $store = [string](Read-Host 'Store plugin [certificatestore]')
            $install = [string](Read-Host 'Installation plugin(s) comma-separated')
            $values.ACME_SOURCE_PLUGIN = if ([string]::IsNullOrWhiteSpace($source)) { 'manual' } else { $source }
            $values.ACME_ORDER_PLUGIN = if ([string]::IsNullOrWhiteSpace($order)) { 'single' } else { $order }
            $values.ACME_STORE_PLUGIN = if ([string]::IsNullOrWhiteSpace($store)) { 'certificatestore' } else { $store }
            $values.ACME_INSTALLATION_PLUGINS = if ([string]::IsNullOrWhiteSpace($install)) { 'script' } else { $install }
            $values.ACME_VALIDATION_MODE = $validationMode
            if ($values.ACME_INSTALLATION_PLUGINS -match 'script') {
                $defaultScript = if ($curr.ContainsKey('ACME_SCRIPT_PATH')) { [string]$curr.ACME_SCRIPT_PATH } else { '' }
                $values.ACME_SCRIPT_PATH = [string](Read-Host "Script path [$defaultScript]")
                if ([string]::IsNullOrWhiteSpace($values.ACME_SCRIPT_PATH)) { $values.ACME_SCRIPT_PATH = $defaultScript }
                $values.ACME_SCRIPT_PARAMETERS = [string](Read-Host "Script parameters [$script:DefaultScriptParameters]")
                if ([string]::IsNullOrWhiteSpace($values.ACME_SCRIPT_PARAMETERS)) { $values.ACME_SCRIPT_PARAMETERS = $script:DefaultScriptParameters }
            } else {
                $values.ACME_SCRIPT_PATH = ''
                $values.ACME_SCRIPT_PARAMETERS = ''
            }
        }

        $values.ACME_TARGET_SYSTEM = $target
    } else {
        Clear-TuiScreen
        $values = Show-TuiForm -Fields $AcmeSchema -CurrentValues $curr -Title 'ACME settings (advanced)'
        if ($null -eq $values) { return $null }
        $values.ACME_TARGET_SYSTEM = if ($curr.ContainsKey('ACME_TARGET_SYSTEM')) { [string]$curr.ACME_TARGET_SYSTEM } else { 'custom' }
        $values.ACME_REQUIRES_EAB = if ($curr.ContainsKey('ACME_REQUIRES_EAB')) { [string]$curr.ACME_REQUIRES_EAB } else { '0' }
    }

    if ([string]::IsNullOrWhiteSpace([string]$values.ACME_ACCOUNT_NAME)) { $values.ACME_ACCOUNT_NAME = '' }
    foreach ($k in (Get-SafeDefaultPipeline).Keys) {
        if ([string]::IsNullOrWhiteSpace([string]$values[$k])) { $values[$k] = [string](Get-SafeDefaultPipeline)[$k] }
    }
    Assert-AcmeSetupValues -Values $values

    $summaryLines = @(
        '',
        'Summary:',
        '',
        "Target: $($values.ACME_TARGET_SYSTEM)",
        "Domains: $($values.DOMAINS)",
        "Provider: $($values.ACME_DIRECTORY)",
        "Validation: $($values.ACME_VALIDATION_MODE)",
        "Deployment: $($values.ACME_INSTALLATION_PLUGINS)"
    )
    if ($values.ACME_INSTALLATION_PLUGINS -match 'script') {
        $summaryLines += "Script: $($values.ACME_SCRIPT_PATH)"
    }
    $summaryLines | ForEach-Object { [Console]::WriteLine($_) }
    $proceed = [string](Read-Host 'Proceed? [Y/N]')
    if ($proceed.Trim().ToLowerInvariant() -notin @('y','yes')) { return $null }

    $wacsPreview = @(
        'wacs',
        "--baseuri $($values.ACME_DIRECTORY)",
        "--account $($values.ACME_ACCOUNT_NAME)",
        "--source $($values.ACME_SOURCE_PLUGIN)",
        "--order $($values.ACME_ORDER_PLUGIN)",
        "--validation $($values.ACME_VALIDATION_MODE)",
        "--store $($values.ACME_STORE_PLUGIN)",
        "--installation $($values.ACME_INSTALLATION_PLUGINS)",
        "--host $($values.DOMAINS)"
    )
    if ($values.ACME_INSTALLATION_PLUGINS -match 'script') {
        $wacsPreview += @("--script $($values.ACME_SCRIPT_PATH)", "--scriptparameters $($values.ACME_SCRIPT_PARAMETERS)")
    }
    [Console]::WriteLine('')
    [Console]::WriteLine('Generated pipeline:')
    [Console]::WriteLine(($wacsPreview -join " `n  "))

    Write-EnvFile -Values $values -Path $EnvFilePath
    Show-TuiStatus -Message 'ACME credentials saved to certificate.env. Ensure this file has restricted NTFS permissions.' -Type Warning -Row ([Console]::WindowHeight-2)
    return $values
}

function Get-PolicyFilePath {
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

function Save-Policies {
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

function Invoke-PolicyEditor {
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
        @{ Name='ACME_DIRECTORY';   Label='ACME directory URL';          Type='string'; Required=$true; Placeholder='https://acme.networking4all.com/dv'; HelpText='ACME directory endpoint' },
        @{ Name='ACME_KID';         Label='ACME KID (EAB key ID)';       Type='secret'; Required=$true; Placeholder=''; HelpText='External account binding key identifier' },
        @{ Name='ACME_HMAC_SECRET'; Label='ACME HMAC secret';            Type='secret'; Required=$true; Placeholder=''; HelpText='External account binding HMAC secret' },
        @{ Name='DOMAINS';          Label='Domains (comma-separated)';   Type='string'; Required=$true; Placeholder='example.com,www.example.com'; HelpText='Domain list for certificate issuance' },
        @{ Name='ACME_SOURCE_PLUGIN'; Label='Source plugin'; Type='string'; Required=$true; Placeholder='manual'; HelpText='simple-acme source plugin' },
        @{ Name='ACME_ORDER_PLUGIN'; Label='Order plugin'; Type='string'; Required=$true; Placeholder='single'; HelpText='simple-acme order plugin' },
        @{ Name='ACME_STORE_PLUGIN'; Label='Store plugin'; Type='string'; Required=$true; Placeholder='certificatestore'; HelpText='simple-acme store plugin' },
        @{ Name='ACME_ACCOUNT_NAME'; Label='Account name'; Type='string'; Required=$false; Placeholder=''; HelpText='Optional ACME account name' },
        @{ Name='ACME_INSTALLATION_PLUGINS'; Label='Installation plugins'; Type='string'; Required=$true; Placeholder='script'; HelpText='Comma-separated installation plugins' },
        @{ Name='ACME_SCRIPT_PARAMETERS'; Label='Script parameters'; Type='string'; Required=$true; Placeholder="'default' {RenewalId} '{CertCommonName}' {CertThumbprint} {OldCertThumbprint} '{CacheFile}' '{CachePassword}' '{StorePath}' {StoreType}"; HelpText='wacs scriptparameters template' },
        @{ Name='ACME_VALIDATION_MODE'; Label='Validation mode'; Type='string'; Required=$true; Placeholder='none'; HelpText='Locked to none' },
        @{ Name='ACME_WACS_RETRY_ATTEMPTS'; Label='WACS retry attempts'; Type='string'; Required=$true; Placeholder='3'; HelpText='Retry attempts for wacs operations' },
        @{ Name='ACME_WACS_RETRY_DELAY_SECONDS'; Label='WACS retry delay seconds'; Type='string'; Required=$true; Placeholder='2'; HelpText='Delay between retries' }
    )

    $pathFields = @(
        @{ Name='CERTIFICATE_DROP_DIR';   Label='Drop directory';   Type='string'; Required=$true; Placeholder=(Join-Path $scriptRoot 'drop');   HelpText='Watched folder for certificate events' },
        @{ Name='ACME_SCRIPT_PATH';       Label='Drop file script path'; Type='string'; Required=$true; Placeholder=(Join-Path $scriptRoot 'Scripts\New-CertificateDropFile.ps1'); HelpText='Absolute path to New-CertificateDropFile.ps1' },
        @{ Name='CERTIFICATE_CONFIG_DIR'; Label='Config directory'; Type='string'; Required=$true; Placeholder=(Join-Path $scriptRoot 'config'); HelpText='Where device configs are stored' },
        @{ Name='CERTIFICATE_STATE_DIR';  Label='State directory';  Type='string'; Required=$true; Placeholder=(Join-Path $scriptRoot 'state');  HelpText='Job state persistence directory' },
        @{ Name='CERTIFICATE_LOG_DIR';    Label='Log directory';    Type='string'; Required=$true; Placeholder=(Join-Path $scriptRoot 'log');    HelpText='Log output directory' }
    )

    Clear-TuiScreen

    $acmeValues = Show-TuiForm -Fields $acmeFields -CurrentValues @{} -Title 'Step 1 of 2 - ACME credentials'
    if ($null -eq $acmeValues) { return $null }

    $pathDefaults = @{
        CERTIFICATE_DROP_DIR   = Join-Path $scriptRoot 'drop'
        ACME_SCRIPT_PATH       = Join-Path $scriptRoot 'Scripts\New-CertificateDropFile.ps1'
        CERTIFICATE_CONFIG_DIR = Join-Path $scriptRoot 'config'
        CERTIFICATE_STATE_DIR  = Join-Path $scriptRoot 'state'
        CERTIFICATE_LOG_DIR    = Join-Path $scriptRoot 'log'
        ACME_SOURCE_PLUGIN     = 'manual'
        ACME_ORDER_PLUGIN      = 'single'
        ACME_STORE_PLUGIN      = 'certificatestore'
        ACME_ACCOUNT_NAME      = ''
        ACME_INSTALLATION_PLUGINS = 'script'
        ACME_SCRIPT_PARAMETERS = "'default' {RenewalId} '{CertCommonName}' {CertThumbprint} {OldCertThumbprint} '{CacheFile}' '{CachePassword}' '{StorePath}' {StoreType}"
        ACME_VALIDATION_MODE   = 'none'
        ACME_WACS_RETRY_ATTEMPTS = '3'
        ACME_WACS_RETRY_DELAY_SECONDS = '2'
    }
    $pathValues = Show-TuiForm -Fields $pathFields -CurrentValues $pathDefaults -Title 'Step 2 of 2 - Paths'
    if ($null -eq $pathValues) { return $null }

    $apiKey = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))

    $all = @{}
    foreach ($k in $acmeValues.Keys) { $all[$k] = $acmeValues[$k] }
    foreach ($k in $pathValues.Keys) { $all[$k] = $pathValues[$k] }
    $all['CERTIFICATE_API_KEY'] = $apiKey

    foreach ($dir in @($pathValues.CERTIFICATE_CONFIG_DIR, $pathValues.CERTIFICATE_DROP_DIR,
                        $pathValues.CERTIFICATE_STATE_DIR, $pathValues.CERTIFICATE_LOG_DIR)) {
        if ($dir -and -not (Test-Path -LiteralPath $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
        }
    }

    $envTarget = Join-Path $pathValues.CERTIFICATE_CONFIG_DIR 'certificate.env'
    Write-EnvFile -Values $all -Path $envTarget
    $policiesPath = Join-Path $pathValues.CERTIFICATE_CONFIG_DIR 'policies.json'
    if (-not (Test-Path -LiteralPath $policiesPath)) {
        $tmp = [System.IO.Path]::GetTempFileName()
        [System.IO.File]::WriteAllText($tmp, '[]', [System.Text.Encoding]::UTF8)
        Move-Item -LiteralPath $tmp -Destination $policiesPath -Force
    }
    [Environment]::SetEnvironmentVariable('CERTIFICATE_CONFIG_DIR', $pathValues.CERTIFICATE_CONFIG_DIR)

    Show-TuiStatus -Message "Wizard complete. certificate.env saved to: $envTarget" `
        -Type Success -Row ([Math]::Max(0, [Console]::WindowHeight) - 2)
    Start-Sleep -Milliseconds 1500

    return $envTarget
}

Export-ModuleMember -Function @('Invoke-DeviceForm','Invoke-AcmeForm','Invoke-PolicyEditor','Invoke-FirstRunWizard','Test-FanoutPolicyValue','Test-QuorumThreshold','Show-PoliciesView','Read-Policies')
