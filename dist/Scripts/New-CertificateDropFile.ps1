#Requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$DeploymentPolicyId,
    [Parameter(Mandatory)][string]$RenewalId,
    [Parameter(Mandatory)][string]$CommonName,
    [Parameter(Mandatory)][string]$Thumbprint,
    [string]$OldThumbprint='', [string]$CacheFile='',
    [string]$CachePassword='', [string]$StorePath='', [string]$StoreType=''
)
$ErrorActionPreference = 'Stop'
$dropDir = [Environment]::GetEnvironmentVariable('CERTIFICATE_DROP_DIR')
if (-not $dropDir) {
    try { Write-EventLog -LogName Application -Source 'Certificate' -EventId 5001 -EntryType Error -Message 'CERTIFICATE_DROP_DIR not set' } catch {}
    exit 1
}
if (-not (Test-Path $dropDir)) { New-Item -ItemType Directory -Path $dropDir -Force | Out-Null }

if (-not [string]::IsNullOrWhiteSpace($OldThumbprint)) {
    $newNorm = ($Thumbprint -replace '\s','').ToUpperInvariant()
    $oldNorm = ($OldThumbprint -replace '\s','').ToUpperInvariant()
    if ($newNorm -eq $oldNorm) { exit 0 }
}

$payload = [ordered]@{
    event                = 'certificate_renewed'
    renewal_id           = $RenewalId
    deployment_policy_id = $DeploymentPolicyId
    domain               = $CommonName
    thumbprint           = $Thumbprint
    cert_path            = $CacheFile
    key_path             = ''
    fullchain_path       = ''
    issuer               = ''
    not_before           = ''
    not_after            = ''
    old_thumbprint       = $OldThumbprint
    cache_password       = $CachePassword
    store_path           = $StorePath
    store_type           = $StoreType
    timestamp            = (Get-Date -Format 'o')
}
$tmp = Join-Path $dropDir "$([guid]::NewGuid()).tmp"
$dst = [System.IO.Path]::ChangeExtension($tmp, '.json')
[System.IO.File]::WriteAllText($tmp, ($payload | ConvertTo-Json -Compress), [System.Text.Encoding]::UTF8)
Move-Item -Path $tmp -Destination $dst -Force
exit 0
