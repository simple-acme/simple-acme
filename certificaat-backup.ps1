Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

param(
    [Parameter(Mandatory)][string]$OutputPath,
    [SecureString]$Passphrase,
    [switch]$Force
)

Import-Module "$PSScriptRoot/core/Crypto.psm1" -Force
Import-Module "$PSScriptRoot/core/Env-Loader.psm1" -Force
Import-Module "$PSScriptRoot/core/Config-Store.psm1" -Force

try {
    if ((Test-Path -LiteralPath $OutputPath) -and -not $Force) { throw "Output path already exists: $OutputPath. Use -Force to overwrite." }

    if (-not $Passphrase) {
        $p1 = Read-Host -AsSecureString -Prompt 'Enter backup passphrase (store this securely — required for restore):'
        $p2 = Read-Host -AsSecureString -Prompt 'Confirm backup passphrase'
        $s1 = ConvertTo-PlainText -SecureString $p1
        $s2 = ConvertTo-PlainText -SecureString $p2
        if ($s1 -ne $s2) { Write-Error 'Passphrase confirmation did not match.'; exit 1 }
        $Passphrase = $p1
    }

    $envValues = Import-EnvFile

    foreach ($requiredSecret in @('ACME_KID','ACME_HMAC_SECRET')) {
        if (-not $envValues.ContainsKey($requiredSecret) -or [string]::IsNullOrWhiteSpace([string]$envValues[$requiredSecret])) {
            throw "Cannot create backup: required credential '$requiredSecret' is empty."
        }
    }

    $devices = Get-AllDeviceConfigs -ConfigDir $env:CERTIFICAAT_CONFIG_DIR
    $policiesPath = Join-Path $env:CERTIFICAAT_CONFIG_DIR 'policies.json'
    $policies = if (Test-Path -LiteralPath $policiesPath) { (Get-Content -Raw -Path $policiesPath -Encoding UTF8 | ConvertFrom-Json) } else { @() }

    $payload = @{
        manifest = @{ created_at = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'); hostname = $env:COMPUTERNAME; version = '1.0'; contents = @('env','devices','policies') }
        env = @{
            ACME_DIRECTORY = $envValues.ACME_DIRECTORY
            ACME_KID = $envValues.ACME_KID
            ACME_HMAC_SECRET = $envValues.ACME_HMAC_SECRET
            DOMAINS = $envValues.DOMAINS
            CERTIFICAAT_CONFIG_DIR = $envValues.CERTIFICAAT_CONFIG_DIR
            CERTIFICAAT_DROP_DIR = $envValues.CERTIFICAAT_DROP_DIR
            CERTIFICAAT_STATE_DIR = $envValues.CERTIFICAAT_STATE_DIR
            CERTIFICAAT_LOG_DIR = $envValues.CERTIFICAAT_LOG_DIR
        }
        devices = $devices
        policies = $policies
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
}
