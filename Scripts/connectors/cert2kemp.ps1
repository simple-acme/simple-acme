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
$endpoints = @($mapping.endpoints)
if ($endpoints.Count -lt 1) { throw 'Kemp connector requires at least one endpoint in mappings.json.' }

$apply = {
    param($endpoint, $cert, $storePath)
    # Placeholder: implement Kemp REST upload/import call with Invoke-RestMethod.
    $null = $endpoint
    $null = $cert
}

$verify = {
    param($endpoint, $cert)
    # Placeholder verification call.
    return $true
}

Invoke-ConnectorPipeline -CertThumbprint $CertThumbprint -Apply $apply -Verify $verify -Endpoints $endpoints
exit 0
