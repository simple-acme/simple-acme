#Requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$DeploymentPolicyId,
    [Parameter(Mandatory)][string]$RenewalId,
    [Parameter(Mandatory)][string]$CommonName,
    [Parameter(Mandatory)][string]$Thumbprint,
    [string]$OldThumbprint='', [string]$CacheFile='',
    [string]$CachePassword='', [string]$StorePath='', [string]$StoreType=''
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Assert-ValidThumbprint {
    param([Parameter(Mandatory)][string]$Value,[string]$ParameterName='Thumbprint')
    $normalized = ($Value -replace '\s','').ToUpperInvariant()
    if ([string]::IsNullOrWhiteSpace($normalized)) { throw "$ParameterName cannot be empty." }
    if ($normalized -notmatch '^[A-F0-9]{40}$') { throw "$ParameterName must be a valid SHA-1 thumbprint (40 hex chars)." }
    return $normalized
}

try {
    foreach ($requiredString in @(
        @{ Name='DeploymentPolicyId'; Value=$DeploymentPolicyId },
        @{ Name='RenewalId'; Value=$RenewalId },
        @{ Name='CommonName'; Value=$CommonName },
        @{ Name='Thumbprint'; Value=$Thumbprint }
    )) {
        if ([string]::IsNullOrWhiteSpace([string]$requiredString.Value)) {
            throw "$($requiredString.Name) cannot be empty."
        }
    }

    $dropDir = [Environment]::GetEnvironmentVariable('CERTIFICATE_DROP_DIR')
    if ([string]::IsNullOrWhiteSpace($dropDir)) {
        throw 'CERTIFICATE_DROP_DIR not set.'
    }
    if (-not (Test-Path -LiteralPath $dropDir)) { New-Item -ItemType Directory -Path $dropDir -Force | Out-Null }

    $newNorm = Assert-ValidThumbprint -Value $Thumbprint -ParameterName 'Thumbprint'
    $oldNorm = ''
    if (-not [string]::IsNullOrWhiteSpace($OldThumbprint)) {
        $oldNorm = Assert-ValidThumbprint -Value $OldThumbprint -ParameterName 'OldThumbprint'
        if ($newNorm -eq $oldNorm) { exit 0 }
    }

    $storeName = if ([string]::IsNullOrWhiteSpace($StoreType)) { 'My' } else { [string]$StoreType }
    $storeLocation = if ([string]::IsNullOrWhiteSpace($StorePath)) { 'LocalMachine' } else { [string]$StorePath }
    $certPath = ('Cert:\{0}\{1}\{2}' -f $storeLocation, $storeName, $newNorm)
    if (-not (Test-Path -LiteralPath $certPath)) {
        throw "Certificate '$newNorm' was not found in store path '$certPath'."
    }
    $cert = Get-Item -LiteralPath $certPath -ErrorAction Stop
    if (-not $cert.HasPrivateKey) {
        throw "Certificate '$newNorm' does not contain a private key in store '$certPath'."
    }

    $payload = [ordered]@{
        event                = 'certificate_renewed'
        renewal_id           = $RenewalId
        deployment_policy_id = $DeploymentPolicyId
        domain               = $CommonName
        thumbprint           = $newNorm
        cert_path            = $CacheFile
        key_path             = ''
        fullchain_path       = ''
        issuer               = ''
        not_before           = ''
        not_after            = ''
        old_thumbprint       = $oldNorm
        cache_password       = $CachePassword
        store_path           = $storeLocation
        store_type           = $storeName
        timestamp            = (Get-Date -Format 'o')
    }
    $tmp = Join-Path $dropDir "$([guid]::NewGuid()).tmp"
    $dst = [System.IO.Path]::ChangeExtension($tmp, '.json')
    [System.IO.File]::WriteAllText($tmp, ($payload | ConvertTo-Json -Compress), [System.Text.Encoding]::UTF8)
    Move-Item -Path $tmp -Destination $dst -Force
    exit 0
} catch {
    try { Write-EventLog -LogName Application -Source 'Certificate' -EventId 5001 -EntryType Error -Message $_.Exception.Message } catch {}
    Write-Error $_
    exit 1
}
