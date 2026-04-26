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
$endpoints = if ($mapping.endpoints) { @($mapping.endpoints) } else { @([pscustomobject]@{ host = $env:COMPUTERNAME; method = 'local' }) }

$apply = {
    param($endpoint, $cert, $storePath)
    if (([string]$endpoint.method).ToLowerInvariant() -eq 'winrm') {
        Invoke-Command -ComputerName ([string]$endpoint.host) -ScriptBlock {
            param($thumbprint)
            Set-RDCertificate -Role RDGateway -Thumbprint $thumbprint -Force -ErrorAction Stop
        } -ArgumentList $cert.Thumbprint -ErrorAction Stop
    } else {
        Set-RDCertificate -Role RDGateway -Thumbprint $cert.Thumbprint -Force -ErrorAction Stop
    }
}

$verify = {
    param($endpoint, $cert)
    if (([string]$endpoint.method).ToLowerInvariant() -eq 'winrm') {
        $thumb = Invoke-Command -ComputerName ([string]$endpoint.host) -ScriptBlock {
            (Get-RDCertificate -Role RDGateway -ErrorAction Stop).Thumbprint
        } -ErrorAction Stop
        return (($thumb -replace '\s','').ToUpperInvariant() -eq $cert.Thumbprint)
    }
    $localThumb = (Get-RDCertificate -Role RDGateway -ErrorAction Stop).Thumbprint
    return (($localThumb -replace '\s','').ToUpperInvariant() -eq $cert.Thumbprint)
}

Invoke-ConnectorPipeline -CertThumbprint $CertThumbprint -Apply $apply -Verify $verify -Endpoints $endpoints
exit 0
