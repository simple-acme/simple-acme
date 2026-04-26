param(
    [Parameter(Mandatory)]
    [string]$CertThumbprint,
    [string]$ConfigDir = $env:CERTIFICATE_CONFIG_DIR,
    [string]$RenewalId = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module WebAdministration -ErrorAction Stop
Import-Module (Join-Path $PSScriptRoot '../core/connector-core.psm1') -Force

$mapping = Resolve-RenewalMapping -ConfigDir $ConfigDir -RenewalId $RenewalId
$endpoints = if ($mapping.endpoints) { @($mapping.endpoints) } else { @([pscustomobject]@{ host = $env:COMPUTERNAME; method = 'local' }) }

$apply = {
    param($endpoint, $cert, $storePath)
    if (([string]$endpoint.method).ToLowerInvariant() -eq 'winrm') {
        Invoke-Command -ComputerName ([string]$endpoint.host) -ScriptBlock {
            param($thumbprint)
            Import-Module WebAdministration -ErrorAction Stop
            Get-ChildItem IIS:\SslBindings | ForEach-Object { $_ | Remove-Item -Force }
            New-Item "IIS:\SslBindings\0.0.0.0!443" -Thumbprint $thumbprint -SSLFlags 0 | Out-Null
        } -ArgumentList $cert.Thumbprint -ErrorAction Stop
    } else {
        Get-ChildItem IIS:\SslBindings | ForEach-Object { $_ | Remove-Item -Force }
        New-Item "IIS:\SslBindings\0.0.0.0!443" -Thumbprint $cert.Thumbprint -SSLFlags 0 | Out-Null
    }
}

$verify = {
    param($endpoint, $cert)
    if (([string]$endpoint.method).ToLowerInvariant() -eq 'winrm') {
        $thumb = Invoke-Command -ComputerName ([string]$endpoint.host) -ScriptBlock {
            Import-Module WebAdministration -ErrorAction Stop
            $binding = Get-Item "IIS:\SslBindings\0.0.0.0!443" -ErrorAction SilentlyContinue
            if ($null -eq $binding) { return '' }
            return [string]$binding.Thumbprint
        } -ErrorAction Stop
        return (($thumb -replace '\s','').ToUpperInvariant() -eq $cert.Thumbprint)
    }
    $binding = Get-Item "IIS:\SslBindings\0.0.0.0!443" -ErrorAction SilentlyContinue
    return ($null -ne $binding -and (($binding.Thumbprint -replace '\s','').ToUpperInvariant() -eq $cert.Thumbprint))
}

Invoke-ConnectorPipeline -CertThumbprint $CertThumbprint -Apply $apply -Verify $verify -Endpoints $endpoints
exit 0
