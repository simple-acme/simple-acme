[CmdletBinding()]
param(
    [Parameter(Position=0,Mandatory=$true)]
    [string]$RenewalId,
    [Parameter(Position=1,Mandatory=$true)]
    [string]$CommonName,
    [Parameter(Position=2,Mandatory=$true)]
    [string]$CertThumbprint,
    [Parameter(Position=3,Mandatory=$false)]
    [string]$OldCertThumbprint,
    [Parameter(Position=4,Mandatory=$true)]
    [string]$CacheFile,
    [Parameter(Position=5,Mandatory=$false)]
    [string]$CachePassword,
    [Parameter(Position=6,Mandatory=$true)]
    [string]$StorePath,
    [Parameter(Position=7,Mandatory=$true)]
    [string]$StoreType
)

$ErrorActionPreference = 'Stop'

function Write-BridgeEvent {
    param(
        [Parameter(Mandatory=$true)][string]$Message,
        [ValidateSet('Error','Information','Warning')][string]$EntryType = 'Error',
        [int]$EventId = 3001
    )

    $source = 'Certificaat'
    if (-not [System.Diagnostics.EventLog]::SourceExists($source)) {
        New-EventLog -LogName Application -Source $source
    }

    Write-EventLog -LogName Application -Source $source -EntryType $EntryType -EventId $EventId -Message $Message
}

if ([string]::IsNullOrWhiteSpace($env:CERTIFICAAT_DROP_DIR)) {
    Write-BridgeEvent -Message 'CERTIFICAAT_DROP_DIR is not set. Unable to write certificaat drop file.' -EntryType Error -EventId 3002
    exit 1
}

$dropDir = $env:CERTIFICAAT_DROP_DIR
if (-not (Test-Path -LiteralPath $dropDir)) {
    Write-BridgeEvent -Message "CERTIFICAAT_DROP_DIR path '$dropDir' does not exist." -EntryType Error -EventId 3003
    exit 1
}

$timestamp = (Get-Date).ToUniversalTime().ToString('o')
$safeTs = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssfffZ')
$rand = ([guid]::NewGuid().ToString('N')).Substring(0,8)
$fileName = "{0}_{1}_{2}.json" -f $RenewalId, $safeTs, $rand
$targetPath = Join-Path -Path $dropDir -ChildPath $fileName

$payload = [ordered]@{
    renewal_id = $RenewalId
    common_name = $CommonName
    thumbprint = $CertThumbprint
    old_thumbprint = $OldCertThumbprint
    cache_file = $CacheFile
    cache_password = $CachePassword
    store_path = $StorePath
    store_type = $StoreType
    timestamp = $timestamp
}

try {
    $json = $payload | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText($targetPath, $json, [System.Text.Encoding]::UTF8)
} catch {
    Write-BridgeEvent -Message "Failed to write drop file '$targetPath'. Error: $($_.Exception.Message)" -EntryType Error -EventId 3004
    exit 1
}

Write-Output "Drop file created: $targetPath"
exit 0
