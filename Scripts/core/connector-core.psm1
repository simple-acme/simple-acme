Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-CertificateByThumbprint {
    param([Parameter(Mandatory)][string]$Thumbprint)

    $normalized = ($Thumbprint -replace '\s','').ToUpperInvariant()
    $stores = @('Cert:\LocalMachine\WebHosting','Cert:\LocalMachine\My')
    foreach ($store in $stores) {
        $cert = Get-ChildItem -Path $store -ErrorAction SilentlyContinue | Where-Object { $_.Thumbprint -eq $normalized } | Select-Object -First 1
        if ($null -ne $cert) {
            return [pscustomobject]@{ Certificate = $cert; StorePath = $store }
        }
    }

    throw "Certificate with thumbprint '$Thumbprint' was not found in WebHosting or My stores."
}

function Test-ThumbprintFormat {
    param([Parameter(Mandatory)][string]$Thumbprint)
    $normalized = ($Thumbprint -replace '\s','').ToUpperInvariant()
    return ($normalized -match '^[A-F0-9]{40}$')
}

function Ensure-CertificateInMyStore {
    param(
        [Parameter(Mandatory)]$Certificate,
        [Parameter(Mandatory)][string]$StorePath
    )

    if ($StorePath -eq 'Cert:\LocalMachine\My') {
        return [pscustomobject]@{ Certificate = $Certificate; StorePath = $StorePath }
    }

    if ($StorePath -ne 'Cert:\LocalMachine\WebHosting') {
        throw "Unsupported source store '$StorePath'."
    }

    $targetStorePath = 'Cert:\LocalMachine\My'
    $existing = Get-ChildItem -Path $targetStorePath -ErrorAction SilentlyContinue | Where-Object { $_.Thumbprint -eq $Certificate.Thumbprint } | Select-Object -First 1
    if ($null -ne $existing) {
        return [pscustomobject]@{ Certificate = $existing; StorePath = $targetStorePath }
    }

    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store('My','LocalMachine')
    try {
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $store.Add($Certificate)
    } finally {
        $store.Close()
    }

    $copied = Get-ChildItem -Path $targetStorePath -ErrorAction Stop | Where-Object { $_.Thumbprint -eq $Certificate.Thumbprint } | Select-Object -First 1
    if ($null -eq $copied) {
        throw "Failed to normalize certificate into LocalMachine\\My for thumbprint '$($Certificate.Thumbprint)'."
    }

    return [pscustomobject]@{ Certificate = $copied; StorePath = $targetStorePath }
}

function Resolve-RenewalMapping {
    param(
        [Parameter(Mandatory)][string]$ConfigDir,
        [string]$RenewalId = ''
    )

    $candidatePaths = @(
        (Join-Path $ConfigDir 'mappings.json'),
        (Join-Path $ConfigDir 'mapping.json')
    )
    $mappingPath = @($candidatePaths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1)
    if ($mappingPath.Count -lt 1) {
        throw "Mapping file not found. Expected one of: $($candidatePaths -join ', ')"
    }
    $mappingPath = [string]$mappingPath[0]

    $mappings = @(Get-Content -Raw -LiteralPath $mappingPath -Encoding UTF8 | ConvertFrom-Json)
    if ([string]::IsNullOrWhiteSpace($RenewalId)) {
        return $mappings | Select-Object -First 1
    }

    $mapping = @($mappings | Where-Object { $_.renewalId -eq $RenewalId }) | Select-Object -First 1
    if ($null -eq $mapping) {
        throw "No mapping found for renewalId '$RenewalId'."
    }

    return $mapping
}

function Invoke-EndpointAction {
    param(
        [Parameter(Mandatory)][object]$Endpoint,
        [Parameter(Mandatory)][scriptblock]$Action
    )

    $method = ([string]$Endpoint.method).ToLowerInvariant()
    $hostName = [string]$Endpoint.host
    if ([string]::IsNullOrWhiteSpace($hostName)) { throw 'Endpoint.host is required.' }

    switch ($method) {
        'winrm' {
            Invoke-Command -ComputerName $hostName -ScriptBlock $Action -ErrorAction Stop
        }
        'local' {
            & $Action
        }
        default {
            throw "Unsupported endpoint method '$method' for host '$hostName'."
        }
    }
}

function Invoke-ConnectorPipeline {
    param(
        [Parameter(Mandatory)][string]$CertThumbprint,
        [Parameter(Mandatory)][scriptblock]$Apply,
        [Parameter(Mandatory)][scriptblock]$Verify,
        [object[]]$Endpoints = @([pscustomobject]@{ host = $env:COMPUTERNAME; method = 'local' })
    )

    if (-not (Test-ThumbprintFormat -Thumbprint $CertThumbprint)) {
        throw "CertThumbprint '$CertThumbprint' is not a valid SHA-1 thumbprint."
    }

    $found = Get-CertificateByThumbprint -Thumbprint $CertThumbprint
    if ($null -eq $found.Certificate) { throw 'Certificate lookup returned null.' }
    $normalized = Ensure-CertificateInMyStore -Certificate $found.Certificate -StorePath $found.StorePath

    $failures = @()
    foreach ($endpoint in @($Endpoints)) {
        try {
            & $Apply $endpoint $normalized.Certificate $normalized.StorePath
            $ok = & $Verify $endpoint $normalized.Certificate
            if (-not $ok) { throw "Verification failed for endpoint '$($endpoint.host)'." }
        } catch {
            $failures += [pscustomobject]@{ host = [string]$endpoint.host; error = $_.Exception.Message }
        }
    }

    if ($failures.Count -gt 0) {
        $details = ($failures | ForEach-Object { "[$($_.host)] $($_.error)" }) -join '; '
        throw "Connector deployment failed (no partial success allowed): $details"
    }
}

$FunctionsToExport = New-Object System.Collections.Generic.List[string]
$FunctionsToExport.Add('Get-CertificateByThumbprint')
$FunctionsToExport.Add('Test-ThumbprintFormat')
$FunctionsToExport.Add('Ensure-CertificateInMyStore')
$FunctionsToExport.Add('Resolve-RenewalMapping')
$FunctionsToExport.Add('Invoke-EndpointAction')
$FunctionsToExport.Add('Invoke-ConnectorPipeline')

$MissingExports = @()
foreach ($fn in $FunctionsToExport) {
    if (-not (Get-Command -Name $fn -CommandType Function -ErrorAction SilentlyContinue)) {
        $MissingExports += $fn
    }
}

if ($MissingExports.Count -gt 0) {
    throw ('Export list contains missing function(s): ' + ($MissingExports -join ', '))
}

Export-ModuleMember -Function ([string[]]$FunctionsToExport.ToArray())
