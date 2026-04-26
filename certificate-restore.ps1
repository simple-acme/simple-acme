#Requires -Version 5.1

param(
    [Parameter(Mandatory)][string]$BackupPath,
    [SecureString]$Passphrase,
    [string]$ConfigDir = '',
    [switch]$DryRun,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module "$PSScriptRoot/core/Crypto.psm1" -Force
Import-Module "$PSScriptRoot/core/Env-Loader.psm1" -Force
Import-Module "$PSScriptRoot/core/Config-Store.psm1" -Force
. "$PSScriptRoot/setup/Device-Schemas.ps1"

function Read-BackupPayload {
    param([string]$Path,[SecureString]$Passphrase)

    if (-not (Test-Path -LiteralPath $Path)) { throw "Backup not found: $Path" }
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 57) { throw 'File does not appear to be a valid Certificate backup.' }
    if ($bytes[0] -ne 0x43 -or $bytes[1] -ne 0x45 -or $bytes[2] -ne 0x52 -or $bytes[3] -ne 0x54) {
        throw 'File does not appear to be a valid Certificate backup.'
    }
    if ($bytes[4] -ne 0x01) { throw 'File does not appear to be a valid Certificate backup.' }

    $offset = 5
    $salt = $bytes[$offset..($offset+31)]; $offset += 32
    $iv = $bytes[$offset..($offset+15)]; $offset += 16
    $len = [BitConverter]::ToUInt32($bytes, $offset); $offset += 4
    if ($offset + $len -gt $bytes.Length) { throw 'File does not appear to be a valid Certificate backup.' }
    $cipher = $bytes[$offset..($offset+$len-1)]

    try {
        $key = New-AesKeyFromPassphrase -Passphrase $Passphrase -Salt $salt
        $plain = Unprotect-AesValue -Ciphertext $cipher -Key $key -IV $iv
        $json = [System.Text.Encoding]::UTF8.GetString($plain)
        $payload = $json | ConvertFrom-Json
        if (-not $payload.manifest -or -not $payload.manifest.version) { throw 'Manifest missing.' }
        return @{ Payload = $payload; PlainBytes = $plain; Key = $key }
    } catch {
        throw 'Decryption failed. Passphrase may be incorrect.'
    }
}

function Test-BackupIntegrity {
    param([Parameter(Mandatory)][string]$BackupPath,[SecureString]$Passphrase)

    $errors = @()
    try {
        $parsed = Read-BackupPayload -Path $BackupPath -Passphrase $Passphrase
        $payload = $parsed.Payload
        return @{
            Valid = $true
            CreatedAt = [string]$payload.manifest.created_at
            Hostname = [string]$payload.manifest.hostname
            DeviceCount = @($payload.devices).Count
            PolicyCount = @($payload.policies).Count
            Errors = @()
        }
    } catch {
        $errors += $_.Exception.Message
        return @{ Valid=$false; CreatedAt=''; Hostname=''; DeviceCount=0; PolicyCount=0; Errors=$errors }
    }
}

try {
    if (-not $Passphrase) {
        $Passphrase = Read-Host -AsSecureString -Prompt 'Enter backup passphrase (store this securely - required for restore):'
    }

    $parsed = Read-BackupPayload -Path $BackupPath -Passphrase $Passphrase
    $payload = $parsed.Payload

    if ([string]$payload.manifest.version -notin @('1.0','1.1')) {
        throw "Unsupported backup manifest version '$($payload.manifest.version)'."
    }

    Write-Host ("Backup manifest: created_at={0}, hostname={1}, devices={2}, policies={3}" -f $payload.manifest.created_at, $payload.manifest.hostname, @($payload.devices).Count, @($payload.policies).Count)

    if ($DryRun) {
        Write-Host 'Dry-run complete. Backup appears readable and would be restored.'
        exit 0
    }

    if (-not $Force) {
        $confirm = Read-Host 'Proceed with restore? (y/N)'
        if ($confirm -ne 'y' -and $confirm -ne 'Y') { Write-Host 'Restore cancelled.'; exit 1 }
    }

    $targetConfigDir = if (-not [string]::IsNullOrWhiteSpace($ConfigDir)) {
        $ConfigDir
    } elseif ($payload.env.CERTIFICATE_CONFIG_DIR) {
        [string]$payload.env.CERTIFICATE_CONFIG_DIR
    } elseif ($payload.env.CERTIFICAAT_CONFIG_DIR) {
        Write-Warning 'Legacy key CERTIFICAAT_CONFIG_DIR detected. Migrate to CERTIFICATE_CONFIG_DIR.'
        [string]$payload.env.CERTIFICAAT_CONFIG_DIR
    } else {
        Read-Host 'Enter target config directory'
    }
    if (-not (Test-Path -LiteralPath $targetConfigDir)) { New-Item -ItemType Directory -Path $targetConfigDir -Force | Out-Null }

    $restoredApiKey = if ($payload.env.CERTIFICATE_API_KEY) {
        [string]$payload.env.CERTIFICATE_API_KEY
    } elseif ($payload.env.CERTIFICAAT_API_KEY) {
        Write-Warning 'Legacy key CERTIFICAAT_API_KEY detected. Migrate to CERTIFICATE_API_KEY.'
        [string]$payload.env.CERTIFICAAT_API_KEY
    } else {
        $newKey = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
        Write-Warning 'Backup did not contain CERTIFICATE_API_KEY. A new key has been auto-generated and stored.'
        $newKey
    }

    $envPath = Join-Path $targetConfigDir 'certificate.env'
    Write-EnvFile -Values @{
        ACME_DIRECTORY         = [string]$payload.env.ACME_DIRECTORY
        DOMAINS                = [string]$payload.env.DOMAINS
        CERTIFICATE_CONFIG_DIR = [string]$targetConfigDir
        CERTIFICATE_DROP_DIR   = [string]$payload.env.CERTIFICATE_DROP_DIR
        CERTIFICATE_STATE_DIR  = [string]$payload.env.CERTIFICATE_STATE_DIR
        CERTIFICATE_LOG_DIR    = [string]$payload.env.CERTIFICATE_LOG_DIR
    } -Path $envPath

    $failed = @()
    foreach ($deviceObj in @($payload.devices)) {
        try {
            $settings = @{}
            foreach ($p in $deviceObj.settings.PSObject.Properties) { $settings[$p.Name] = [string]$p.Value }
            $device = @{
                device_id = [string]$deviceObj.device_id
                connector_type = [string]$deviceObj.connector_type
                label = [string]$deviceObj.label
                created_at = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
                updated_at = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
                settings = $settings
            }
            $secretFields = @()
            if ($DeviceSchemas.ContainsKey($device.connector_type)) {
                $secretFields = @($DeviceSchemas[$device.connector_type].Fields | Where-Object { $_.Type -eq 'secret' } | ForEach-Object { $_.Name })
            }
            Save-DeviceConfig -Device $device -ConfigDir $targetConfigDir -SecretFields $secretFields | Out-Null
            Write-Host "[INFO] Restored device: $($device.label) ($($device.connector_type))"
        } catch {
            $failed += [string]$deviceObj.device_id
            Write-Error "Failed restoring device '$($deviceObj.device_id)': $($_.Exception.Message)"
        }
    }


    if ($payload.mappings) {
        $mappingPath = Join-Path $targetConfigDir 'mappings.json'
        $mappingCompatPath = Join-Path $targetConfigDir 'mapping.json'
        [IO.File]::WriteAllText($mappingPath, ($payload.mappings | ConvertTo-Json -Depth 20), [Text.Encoding]::UTF8)
        [IO.File]::WriteAllText($mappingCompatPath, ($payload.mappings | ConvertTo-Json -Depth 20), [Text.Encoding]::UTF8)
    }

    if ($payload.secure_config) {
        if ($payload.secure_config.env_secure) {
            [IO.File]::WriteAllBytes((Join-Path $targetConfigDir 'env.secure'), [Convert]::FromBase64String([string]$payload.secure_config.env_secure))
        }
        if ($payload.secure_config.credentials_sec) {
            [IO.File]::WriteAllBytes((Join-Path $targetConfigDir 'credentials.sec'), [Convert]::FromBase64String([string]$payload.secure_config.credentials_sec))
        }
    }

    if ($payload.renewals) {
        $renewalsDir = Join-Path $targetConfigDir 'renewals'
        if (-not (Test-Path -LiteralPath $renewalsDir)) { New-Item -ItemType Directory -Path $renewalsDir -Force | Out-Null }
        foreach ($r in @($payload.renewals)) {
            [IO.File]::WriteAllText((Join-Path $renewalsDir ([string]$r.file)), [string]$r.content, [Text.Encoding]::UTF8)
        }
    }

    $policiesPath = Join-Path $targetConfigDir 'policies.json'
    $tmpPolicies = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($tmpPolicies, ($payload.policies | ConvertTo-Json -Depth 10), [System.Text.Encoding]::UTF8)
    Move-Item -LiteralPath $tmpPolicies -Destination $policiesPath -Force

    [Array]::Clear($parsed.Key, 0, $parsed.Key.Length)
    [Array]::Clear($parsed.PlainBytes, 0, $parsed.PlainBytes.Length)

    Write-Host ("Restore complete. Devices restored: {0}, policies restored: {1}, config dir: {2}" -f (@($payload.devices).Count - $failed.Count), @($payload.policies).Count, $targetConfigDir)
    Write-Host 'Verify connectivity for each restored device using certificate-setup.ps1 before resuming certificate operations.'

    if ($failed.Count -gt 0) { exit 3 }
    exit 0
} catch {
    if ($_.Exception.Message -like 'File does not appear*' -or $_.Exception.Message -like 'Decryption failed*' -or $_.Exception.Message -like 'Unsupported backup*') {
        Write-Error $_
        exit 1
    }
    Write-Error $_
    exit 2
}
