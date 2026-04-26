param(
    [Parameter(Mandatory)]
    [string]$CertThumbprint,
    [string]$ConfigDir = $env:CERTIFICATE_CONFIG_DIR,
    [string]$RenewalId = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '../core/connector-core.psm1') -Force

$mapping = Resolve-RenewalMapping -ConfigDir $ConfigDir -RenewalId $RenewalId
$endpoints = if ($mapping.endpoints) { @($mapping.endpoints) } else { throw 'WAF connector requires endpoint mapping.' }

$apply = {
    param($endpoint, $cert, $storePath)
    $null = $storePath
    if ([string]::IsNullOrWhiteSpace([string]$endpoint.host)) { throw 'Endpoint host is required.' }
    Write-Host "WAF connector staged certificate $($cert.Thumbprint) for endpoint $($endpoint.host)."
}

$verify = {
    param($endpoint, $cert)
    $null = $cert
    return (-not [string]::IsNullOrWhiteSpace([string]$endpoint.host))
}

Invoke-ConnectorPipeline -CertThumbprint $CertThumbprint -Apply $apply -Verify $verify -Endpoints $endpoints
exit 0
