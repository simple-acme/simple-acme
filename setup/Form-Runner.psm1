. "$PSScriptRoot/Device-Schemas.ps1"
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Import-Module "$PSScriptRoot/../core/Tui-Engine.psm1" -Force -Global
Import-Module "$PSScriptRoot/../core/Config-Store.psm1" -Force -Global
Import-Module "$PSScriptRoot/../core/Env-Loader.psm1" -Force -Global
. "$PSScriptRoot/Device-Schemas.ps1"
. "$PSScriptRoot/Menu-Tree.ps1"

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

    Clear-TuiScreen
    $values = Show-TuiForm -Fields $AcmeSchema -CurrentValues $curr -Title 'ACME settings'
    if ($null -eq $values) { return $null }

    foreach ($k in @('CERTIFICATE_CONFIG_DIR','CERTIFICATE_DROP_DIR','CERTIFICATE_STATE_DIR','CERTIFICATE_LOG_DIR','CERTIFICATE_VERIFY_MAX_ATTEMPTS','CERTIFICATE_ACTIVATE_TIMEOUT_MS','CERTIFICATE_DEFAULT_FANOUT','CERTIFICATE_SKIP_TLS_CHECK')) {
        if ($curr.ContainsKey($k)) { $values[$k] = $curr[$k] }
    }

    Write-EnvFile -Values $values -Path $EnvFilePath
    Show-TuiStatus -Message 'ACME credentials saved to certificate.env. Ensure this file has restricted NTFS permissions.' -Type Warning -Row ([Console]::WindowHeight-2)
    return $values
}

function Invoke-FirstRunWizard {
    param([Parameter(Mandatory)][string]$DefaultEnvPath)

    $scriptRoot = Split-Path $PSScriptRoot -Parent

    $acmeFields = @(
        @{ Name='ACME_DIRECTORY';   Label='ACME directory URL';          Type='string'; Required=$true; Placeholder='https://acme.networking4all.com/dv'; HelpText='ACME directory endpoint' },
        @{ Name='ACME_KID';         Label='ACME KID (EAB key ID)';       Type='secret'; Required=$true; Placeholder=''; HelpText='External account binding key identifier' },
        @{ Name='ACME_HMAC_SECRET'; Label='ACME HMAC secret';            Type='secret'; Required=$true; Placeholder=''; HelpText='External account binding HMAC secret' },
        @{ Name='DOMAINS';          Label='Domains (comma-separated)';   Type='string'; Required=$true; Placeholder='example.com,www.example.com'; HelpText='Domain list for certificate issuance' }
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

Export-ModuleMember -Function @('Invoke-DeviceForm','Invoke-AcmeForm','Invoke-PolicyEditor','Invoke-PolicyViewer','Invoke-FirstRunWizard','Get-Policies','Find-PolicyById','Format-PolicySummaryLines')
