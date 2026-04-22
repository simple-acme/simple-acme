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

    # NOTE: For now each connector stores a single config; this keeps setup deterministic.
    $existing = Get-AllDeviceConfigs -ConfigDir $ConfigDir -SkipIntegrityFailures | Where-Object { $_.connector_type -eq $ConnectorType } | Select-Object -First 1
    $current = if ($existing) { $existing.settings } else { @{} }

    try {
        $values = Show-TuiForm -Fields $schema.Fields -CurrentValues $current -Title "Configure $($schema.Label)"
    } catch {
        Show-TuiStatus -Message "Error loading existing config: $($_.Exception.Message)" -Type Error -Row ([Console]::WindowHeight-2)
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
    Show-TuiStatus -Message 'Saved successfully.' -Type Success -Row ([Console]::WindowHeight-2)
    Start-Sleep -Milliseconds 1500
    return $device
}

function Invoke-AcmeForm {
    param([string]$EnvFilePath)

    $curr = @{}
    if ($EnvFilePath -and (Test-Path -LiteralPath $EnvFilePath)) { $curr = Read-EnvFile -Path $EnvFilePath }

    $values = Show-TuiForm -Fields $AcmeSchema -CurrentValues $curr -Title 'ACME settings'
    if ($null -eq $values) { return $null }

    foreach ($k in @('CERTIFICAAT_CONFIG_DIR','CERTIFICAAT_DROP_DIR','CERTIFICAAT_STATE_DIR','CERTIFICAAT_LOG_DIR','CERTIFICAAT_VERIFY_MAX_ATTEMPTS','CERTIFICAAT_ACTIVATE_TIMEOUT_MS','CERTIFICAAT_DEFAULT_FANOUT','CERTIFICAAT_SKIP_TLS_CHECK')) {
        if ($curr.ContainsKey($k)) { $values[$k] = $curr[$k] }
    }

    Write-EnvFile -Values $values -Path $EnvFilePath
    Show-TuiStatus -Message 'ACME credentials saved to certificaat.env. Ensure this file has restricted NTFS permissions.' -Type Warning -Row ([Console]::WindowHeight-2)
    return $values
}

function Invoke-PolicyEditor {
    param([string]$ConfigDir)
    $path = Join-Path $ConfigDir 'policies.json'
    if (-not (Test-Path -LiteralPath $path)) { [System.IO.File]::WriteAllText($path, '[]', [System.Text.Encoding]::UTF8) }
    $raw = Get-Content -Raw -Path $path -Encoding UTF8
    $policies = if ([string]::IsNullOrWhiteSpace($raw)) { @() } else { $raw | ConvertFrom-Json }
    # NOTE: Minimal non-interactive policy editor placeholder keeps persistence deterministic.
    [System.IO.File]::WriteAllText($path, ($policies | ConvertTo-Json -Depth 10), [System.Text.Encoding]::UTF8)
    return $policies
}

Export-ModuleMember -Function @('Invoke-DeviceForm','Invoke-AcmeForm','Invoke-PolicyEditor')
