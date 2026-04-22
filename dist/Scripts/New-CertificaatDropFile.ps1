#Requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$RenewalId,
    [Parameter(Mandatory)][string]$CommonName,
    [Parameter(Mandatory)][string]$Thumbprint,
    [string]$OldThumbprint='', [string]$CacheFile='',
    [string]$CachePassword='', [string]$StorePath='', [string]$StoreType=''
)
$ErrorActionPreference = 'Stop'
$dropDir = [Environment]::GetEnvironmentVariable('CERTIFICAAT_DROP_DIR')
if (-not $dropDir) {
    try { Write-EventLog -LogName Application -Source 'Certificaat' -EventId 5001 -EntryType Error -Message 'CERTIFICAAT_DROP_DIR not set' } catch {}
    exit 1
}
if (-not (Test-Path $dropDir)) { New-Item -ItemType Directory -Path $dropDir -Force | Out-Null }
$payload = [ordered]@{ renewal_id=$RenewalId; common_name=$CommonName; thumbprint=$Thumbprint;
    old_thumbprint=$OldThumbprint; cache_file=$CacheFile; cache_password=$CachePassword;
    store_path=$StorePath; store_type=$StoreType; timestamp=(Get-Date -Format 'o') }
$tmp = Join-Path $dropDir "$([guid]::NewGuid()).tmp"
$dst = [System.IO.Path]::ChangeExtension($tmp, '.json')
[System.IO.File]::WriteAllText($tmp, ($payload | ConvertTo-Json -Compress), [System.Text.Encoding]::UTF8)
Move-Item -Path $tmp -Destination $dst -Force
exit 0
