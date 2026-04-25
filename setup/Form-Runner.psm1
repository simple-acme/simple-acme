. "$PSScriptRoot/Device-Schemas.ps1"
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Import-Module "$PSScriptRoot/../core/Tui-Engine.psm1" -Force
Import-Module "$PSScriptRoot/../core/Config-Store.psm1" -Force
Import-Module "$PSScriptRoot/../core/Env-Loader.psm1" -Force
. "$PSScriptRoot/Device-Schemas.ps1"
. "$PSScriptRoot/Menu-Tree.ps1"

function New-DeviceId {
    param([Parameter(Mandatory)][string]$Label)
    $slug = ($Label.ToLowerInvariant() -replace '[^a-z0-9]+','-').Trim('-')
    $suffix = ([guid]::NewGuid().ToString('N')).Substring(0,8)
    return "$slug-$suffix"
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

    # NOTE: For now each connector stores a single config; this keeps setup deterministic.
    $existing = Get-AllDeviceConfigs -ConfigDir $ConfigDir -SkipIntegrityFailures | Where-Object { $_.connector_type -eq $ConnectorType } | Select-Object -First 1
    $current = if ($existing) { $existing.settings } else { @{} }

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

    $values = Show-TuiForm -Fields $AcmeSchema -CurrentValues $curr -Title 'ACME settings'
    if ($null -eq $values) { return $null }

    foreach ($k in @('CERTIFICATE_CONFIG_DIR','CERTIFICATE_DROP_DIR','CERTIFICATE_STATE_DIR','CERTIFICATE_LOG_DIR','CERTIFICATE_VERIFY_MAX_ATTEMPTS','CERTIFICATE_ACTIVATE_TIMEOUT_MS','CERTIFICATE_DEFAULT_FANOUT','CERTIFICATE_SKIP_TLS_CHECK')) {
        if ($curr.ContainsKey($k)) { $values[$k] = $curr[$k] }
    }

    Write-EnvFile -Values $values -Path $EnvFilePath
    Show-TuiStatus -Message 'ACME credentials saved to certificate.env. Ensure this file has restricted NTFS permissions.' -Type Warning -Row ([Console]::WindowHeight-2)
    return $values
}

function Invoke-PolicyEditor {
    param([string]$ConfigDir)
    $path = Join-Path $ConfigDir 'policies.json'
    if (-not (Test-Path -LiteralPath $path)) {
        $tmpInit = [System.IO.Path]::GetTempFileName(); [System.IO.File]::WriteAllText($tmpInit, '[]', [System.Text.Encoding]::UTF8); Move-Item -LiteralPath $tmpInit -Destination $path -Force
    }
    $policies = @((Get-Content -Raw -Encoding UTF8 -Path $path | ConvertFrom-Json))
    while($true){
        $choice = Read-Host 'Policy editor: [N]ew [E]dit [D]elete [L]ist [Q]uit'
        if($choice -match '^[Qq]'){ break }
        if($choice -match '^[Ll]'){ $policies | ForEach-Object { [Console]::WriteLine("{0} fanout={1} quorum={2}" -f $_.policy_id,$_.fanout_policy,$_.quorum_threshold) }; continue }
        if($choice -match '^[Nn]'){ $pid=Read-Host 'policy_id'; $fan=Read-Host 'fanout_policy'; $q=Read-Host 'quorum_threshold'; $policies += [pscustomobject]@{ policy_id=$pid; fanout_policy=$fan; quorum_threshold=[int]$q; connectors=@() }; continue }
        if($choice -match '^[Dd]'){ $pid=Read-Host 'policy_id to delete'; $policies=@($policies | Where-Object { $_.policy_id -ne $pid }); continue }
        if($choice -match '^[Ee]'){ $pid=Read-Host 'policy_id to edit'; $p=@($policies|Where-Object{$_.policy_id -eq $pid})[0]; if($null -eq $p){continue}; $p.fanout_policy=Read-Host "fanout_policy [$($p.fanout_policy)]"; $p.quorum_threshold=[int](Read-Host "quorum_threshold [$($p.quorum_threshold)]"); $conn=Read-Host 'connector types comma-separated'; if($conn){ $p.connectors=@($conn -split ',' | ForEach-Object { [pscustomobject]@{ connector_type=$_.Trim(); label=$_.Trim(); settings=@{} } }) }; }
    }
    $tmp = [System.IO.Path]::GetTempFileName(); [System.IO.File]::WriteAllText($tmp, ($policies | ConvertTo-Json -Depth 20), [System.Text.Encoding]::UTF8); Move-Item -LiteralPath $tmp -Destination $path -Force
    return $policies
}


function Invoke-FirstRunWizard {
    param([Parameter(Mandatory)][string]$DefaultEnvPath)

    $scriptRoot = Split-Path $DefaultEnvPath -Parent

    $acmeFields = @(
        @{ Name='ACME_DIRECTORY';   Label='ACME directory URL';          Type='string'; Required=$true; Placeholder='https://acme.networking4all.com/dv'; HelpText='ACME directory endpoint' },
        @{ Name='ACME_KID';         Label='ACME KID (EAB key ID)';       Type='secret'; Required=$true; Placeholder=''; HelpText='External account binding key identifier' },
        @{ Name='ACME_HMAC_SECRET'; Label='ACME HMAC secret';            Type='secret'; Required=$true; Placeholder=''; HelpText='External account binding HMAC secret' },
        @{ Name='DOMAINS';          Label='Domains (comma-separated)';   Type='string'; Required=$true; Placeholder='example.com,www.example.com'; HelpText='Domain list for certificate issuance' },
        @{ Name='ACME_SCRIPT_PATH'; Label='Drop file script path';       Type='string'; Required=$true; Placeholder='C:\certificate\dist\Scripts\New-CertificateDropFile.ps1'; HelpText='Absolute path to New-CertificateDropFile.ps1' }
    )

    $pathFields = @(
        @{ Name='CERTIFICATE_CONFIG_DIR'; Label='Config directory'; Type='string'; Required=$true; Placeholder=(Join-Path $scriptRoot 'config'); HelpText='Where device configs are stored' },
        @{ Name='CERTIFICATE_DROP_DIR';   Label='Drop directory';   Type='string'; Required=$true; Placeholder=(Join-Path $scriptRoot 'drop');   HelpText='Watched folder for certificate events' },
        @{ Name='CERTIFICATE_STATE_DIR';  Label='State directory';  Type='string'; Required=$true; Placeholder=(Join-Path $scriptRoot 'state');  HelpText='Job state persistence directory' },
        @{ Name='CERTIFICATE_LOG_DIR';    Label='Log directory';    Type='string'; Required=$true; Placeholder=(Join-Path $scriptRoot 'log');    HelpText='Log output directory' }
    )

    Clear-Host
    Write-Host "`n  Certificate Setup - First Run`n" -ForegroundColor Cyan
    Write-Host "  No certificate.env found. This wizard will guide you through initial setup.`n"

    $acmeValues = Show-TuiForm -Fields $acmeFields -CurrentValues @{} -Title 'Step 1 of 2 - ACME credentials'
    if ($null -eq $acmeValues) { return $null }

    $pathDefaults = @{
        CERTIFICATE_CONFIG_DIR = Join-Path $scriptRoot 'config'
        CERTIFICATE_DROP_DIR   = Join-Path $scriptRoot 'drop'
        CERTIFICATE_STATE_DIR  = Join-Path $scriptRoot 'state'
        CERTIFICATE_LOG_DIR    = Join-Path $scriptRoot 'log'
    }
    $pathValues = Show-TuiForm -Fields $pathFields -CurrentValues $pathDefaults -Title 'Step 2 of 2 - Directories'
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
    [Environment]::SetEnvironmentVariable('CERTIFICATE_CONFIG_DIR', $pathValues.CERTIFICATE_CONFIG_DIR)

    Show-TuiStatus -Message "Wizard complete. certificate.env saved to: $envTarget" `
        -Type Success -Row ([Math]::Max(0, [Console]::WindowHeight) - 2)
    Start-Sleep -Milliseconds 1500

    Write-Host "`n  Auto-generated CERTIFICATE_API_KEY (store this securely):" -ForegroundColor Yellow
    Write-Host "  $apiKey`n" -ForegroundColor White
    Write-Host "  Press any key to continue to the main menu..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')

    return $envTarget
}

Export-ModuleMember -Function @('Invoke-DeviceForm','Invoke-AcmeForm','Invoke-PolicyEditor','Invoke-FirstRunWizard')
