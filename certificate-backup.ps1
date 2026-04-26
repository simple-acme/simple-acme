#Requires -Version 5.1

param(
    [Parameter(Mandatory)][string]$OutputPath,
    [SecureString]$Passphrase,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$cryptoModulePath = Join-Path $PSScriptRoot 'core/Crypto.psm1'
$cryptoModule = Import-Module $cryptoModulePath -Force -PassThru
Import-Module "$PSScriptRoot/core/Env-Loader.psm1" -Force
Import-Module "$PSScriptRoot/core/Config-Store.psm1" -Force

$plainTextCommand = Get-Command 'ConvertTo-PlainText' -ErrorAction SilentlyContinue
if ($null -eq $plainTextCommand -or [string]::IsNullOrWhiteSpace([string]$plainTextCommand.Source) -or $plainTextCommand.Source -ne $cryptoModule.Name) {
    throw @"
Required command 'ConvertTo-PlainText' is unavailable after importing Crypto module.
Expected module path: $cryptoModulePath
Resolved module path: $($cryptoModule.Path)
Current script root: $PSScriptRoot
Re-deploy the setup modules and ensure you are running certificate-backup.ps1 from the correct repository root.
"@
}

$p1 = $null
$p2 = $null

try {
    if ((Test-Path -LiteralPath $OutputPath) -and -not $Force) { throw "Output path already exists: $OutputPath. Use -Force to overwrite." }

    if (-not $Passphrase) {
        $p1 = Read-Host -AsSecureString -Prompt 'Enter backup passphrase (store this securely — required for restore):'
        $p2 = Read-Host -AsSecureString -Prompt 'Confirm backup passphrase'
        $s1 = & "$($cryptoModule.Name)\ConvertTo-PlainText" -SecureString $p1
        $s2 = & "$($cryptoModule.Name)\ConvertTo-PlainText" -SecureString $p2
        if ($s1 -ne $s2) { Write-Error 'Passphrase confirmation did not match.'; exit 1 }
        $Passphrase = $p1
    }

    Import-EnvFile | Out-Null

    $envValues = @{
        ACME_DIRECTORY=[Environment]::GetEnvironmentVariable('ACME_DIRECTORY')
        ACME_KID=[Environment]::GetEnvironmentVariable('ACME_KID')
        ACME_HMAC_SECRET=[Environment]::GetEnvironmentVariable('ACME_HMAC_SECRET')
        DOMAINS=[Environment]::GetEnvironmentVariable('DOMAINS')
        CERTIFICATE_CONFIG_DIR=[Environment]::GetEnvironmentVariable('CERTIFICATE_CONFIG_DIR')
        CERTIFICATE_DROP_DIR=[Environment]::GetEnvironmentVariable('CERTIFICATE_DROP_DIR')
        CERTIFICATE_STATE_DIR=[Environment]::GetEnvironmentVariable('CERTIFICATE_STATE_DIR')
        CERTIFICATE_LOG_DIR=[Environment]::GetEnvironmentVariable('CERTIFICATE_LOG_DIR')
        CERTIFICATE_API_KEY=[Environment]::GetEnvironmentVariable('CERTIFICATE_API_KEY')
    }

    foreach ($requiredSecret in @('ACME_KID','ACME_HMAC_SECRET','CERTIFICATE_API_KEY')) {
        if (-not $envValues.ContainsKey($requiredSecret) -or [string]::IsNullOrWhiteSpace([string]$envValues[$requiredSecret])) {
            throw "Cannot create backup: required credential '$requiredSecret' is empty."
        }
    }

    $devices = Get-AllDeviceConfigs -ConfigDir $env:CERTIFICATE_CONFIG_DIR
    $policiesPath = Join-Path $env:CERTIFICATE_CONFIG_DIR 'policies.json'
    $mappingPath = Join-Path $env:CERTIFICATE_CONFIG_DIR 'mappings.json'
    $mappingCompatPath = Join-Path $env:CERTIFICATE_CONFIG_DIR 'mapping.json'
    $secureEnvPath = Join-Path $env:CERTIFICATE_CONFIG_DIR 'env.secure'
    $credPath = Join-Path $env:CERTIFICATE_CONFIG_DIR 'credentials.sec'
    $renewalsDir = Join-Path $env:CERTIFICATE_CONFIG_DIR 'renewals'
    $policies = if (Test-Path -LiteralPath $policiesPath) { (Get-Content -Raw -Path $policiesPath -Encoding UTF8 | ConvertFrom-Json) } else { @() }
    $mappings = if (Test-Path -LiteralPath $mappingPath) {
        (Get-Content -Raw -Path $mappingPath -Encoding UTF8 | ConvertFrom-Json)
    } elseif (Test-Path -LiteralPath $mappingCompatPath) {
        (Get-Content -Raw -Path $mappingCompatPath -Encoding UTF8 | ConvertFrom-Json)
    } else {
        @()
    }
    $secureConfig = @{
        env_secure = if (Test-Path -LiteralPath $secureEnvPath) { [Convert]::ToBase64String([IO.File]::ReadAllBytes($secureEnvPath)) } else { '' }
        credentials_sec = if (Test-Path -LiteralPath $credPath) { [Convert]::ToBase64String([IO.File]::ReadAllBytes($credPath)) } else { '' }
    }
    $renewals = @()
    if (Test-Path -LiteralPath $renewalsDir) {
        foreach ($f in Get-ChildItem -LiteralPath $renewalsDir -Filter '*.json' -File) {
            $renewals += [pscustomobject]@{ file = $f.Name; content = Get-Content -Raw -LiteralPath $f.FullName -Encoding UTF8 }
        }
    }

    $payload = @{
        manifest = @{ created_at = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'); hostname = $env:COMPUTERNAME; version = '1.1'; contents = @('env','devices','policies','secure_config','mappings','renewals') }
        env = @{
            ACME_DIRECTORY = $envValues.ACME_DIRECTORY
            ACME_KID = $envValues.ACME_KID
            ACME_HMAC_SECRET = $envValues.ACME_HMAC_SECRET
            DOMAINS = $envValues.DOMAINS
            CERTIFICATE_CONFIG_DIR = $envValues.CERTIFICATE_CONFIG_DIR
            CERTIFICATE_DROP_DIR = $envValues.CERTIFICATE_DROP_DIR
            CERTIFICATE_STATE_DIR = $envValues.CERTIFICATE_STATE_DIR
            CERTIFICATE_LOG_DIR = $envValues.CERTIFICATE_LOG_DIR
            CERTIFICATE_API_KEY = $envValues.CERTIFICATE_API_KEY
        }
        devices = $devices
        policies = $policies
        mappings = $mappings
        secure_config = $secureConfig
        renewals = $renewals
    }

    $json = $payload | ConvertTo-Json -Depth 20
    $plainBytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $salt = Get-RandomBytes -Count 32
    $key = New-AesKeyFromPassphrase -Passphrase $Passphrase -Salt $salt
    $encrypted = Protect-AesValue -Plaintext $plainBytes -Key $key

    $outDir = Split-Path -Path $OutputPath -Parent
    if ($outDir -and -not (Test-Path -LiteralPath $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

    $tmpOutput = [System.IO.Path]::GetTempFileName()
    $fs = [System.IO.File]::Open($tmpOutput, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    try {
        $bw = New-Object System.IO.BinaryWriter($fs)
        $bw.Write([byte[]](0x43,0x45,0x52,0x54))
        $bw.Write([byte]0x01)
        $bw.Write($salt)
        $bw.Write($encrypted.IV)
        $bw.Write([uint32]$encrypted.Ciphertext.Length)
        $bw.Write($encrypted.Ciphertext)
        $bw.Flush()
    } finally { $fs.Dispose() }
    Move-Item -LiteralPath $tmpOutput -Destination $OutputPath -Force

    [Array]::Clear($key, 0, $key.Length)
    [Array]::Clear($plainBytes, 0, $plainBytes.Length)

    Write-Host ("Backup created: {0} bytes, devices: {1}, timestamp: {2}" -f (Get-Item $OutputPath).Length, $devices.Count, (Get-Date).ToUniversalTime().ToString('o'))
    exit 0
} catch {
    if ($_.Exception.Message -like '*Passphrase*' -or $_.Exception.Message -like '*required*') { exit 1 }
    Write-Error $_
    exit 2
} finally {
    if ($p1) { $p1.Dispose() }
    if ($p2) { $p2.Dispose() }
}
