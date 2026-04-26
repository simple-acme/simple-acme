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

function Resolve-RenewalMapping {
    param(
        [Parameter(Mandatory)][string]$ConfigDir,
        [string]$RenewalId = ''
    )

    $mappingPath = Join-Path $ConfigDir 'mappings.json'
    if (-not (Test-Path -LiteralPath $mappingPath)) {
        throw "Mapping file not found: $mappingPath"
    }

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

    $found = Get-CertificateByThumbprint -Thumbprint $CertThumbprint
    if ($null -eq $found.Certificate) { throw 'Certificate lookup returned null.' }

    $failures = @()
    foreach ($endpoint in @($Endpoints)) {
        try {
            & $Apply $endpoint $found.Certificate $found.StorePath
            $ok = & $Verify $endpoint $found.Certificate
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

Export-ModuleMember -Function @('Get-CertificateByThumbprint','Resolve-RenewalMapping','Invoke-EndpointAction','Invoke-ConnectorPipeline')
