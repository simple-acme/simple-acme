#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module "$PSScriptRoot/Logger.psm1" -Force

function Test-HttpAuth {
    param($Request,[string]$ApiKey)
    $xApi = [string]$Request.Headers['X-API-Key']
    $auth = [string]$Request.Headers['Authorization']
    if ($xApi -and $xApi -ceq $ApiKey) { return $true }
    if ($auth -and $auth.StartsWith('Bearer ')) { return ($auth.Substring(7) -ceq $ApiKey) }
    return $false
}

function Write-HttpJson {
    param($Response,[int]$StatusCode,[hashtable]$Body)
    $json = $Body | ConvertTo-Json -Depth 8
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $Response.StatusCode = $StatusCode
    $Response.ContentType = 'application/json'
    $Response.OutputStream.Write($bytes,0,$bytes.Length)
    $Response.OutputStream.Close()
}

function Get-JobById {
    param([string]$StateDir,[string]$JobId)
    $path = Join-Path $StateDir "$JobId.json"
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    Get-Content -Raw -Encoding UTF8 -Path $path | ConvertFrom-Json
}

function Get-JobsByRenewalId {
    param([string]$StateDir,[string]$RenewalId)
    $dir = $StateDir
    if (-not (Test-Path -LiteralPath $dir)) { return @() }
    @(Get-ChildItem -LiteralPath $dir -Filter '*.json' | ForEach-Object {
        $j = Get-Content -Raw -Encoding UTF8 -Path $_.FullName | ConvertFrom-Json
        if ($j.renewal_id -eq $RenewalId) { $j }
    })
}

function Get-EventFingerprint {
    param($EventObject)
    if ($null -eq $EventObject) { return '' }
    return ('{0}|{1}|{2}' -f [string]$EventObject.renewal_id,[string]$EventObject.deployment_policy_id,[string]$EventObject.domain)
}

function Start-CertificateHttpListener {
    param([string]$DropDir,[string]$StateDir)
    $prefix = [Environment]::GetEnvironmentVariable('CERTIFICATE_HTTP_PREFIX')
    if ([string]::IsNullOrWhiteSpace($prefix)) { $prefix = 'http://localhost:8443/' }
    $apiKey = [Environment]::GetEnvironmentVariable('CERTIFICATE_API_KEY')
    if ([string]::IsNullOrWhiteSpace($apiKey)) { throw 'CERTIFICATE_API_KEY is required.' }

    $listener = New-Object System.Net.HttpListener
    $listener.Prefixes.Add($prefix)
    $listener.Start()
    Write-CertificateLog -Level Info -Message "HTTP listener started on $prefix"

    $recentEvents = @{}
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        try {
            $path = [string]$context.Request.Url.AbsolutePath
            if ($context.Request.HttpMethod -eq 'GET' -and $path -eq '/health') {
                Write-HttpJson -Response $context.Response -StatusCode 200 -Body @{ status='ok' }
                continue
            }
            if (-not (Test-HttpAuth -Request $context.Request -ApiKey $apiKey)) {
                Write-HttpJson -Response $context.Response -StatusCode 401 -Body @{ error='unauthorized' }
                continue
            }
            if ($context.Request.HttpMethod -eq 'POST' -and $path -eq '/events') {
                $reader = New-Object System.IO.StreamReader($context.Request.InputStream, $context.Request.ContentEncoding)
                $raw = $reader.ReadToEnd(); $reader.Dispose()
                $obj = $raw | ConvertFrom-Json
                $fingerprint = Get-EventFingerprint -EventObject $obj
                $now = (Get-Date).ToUniversalTime()
                if ($recentEvents.ContainsKey($fingerprint) -and $recentEvents[$fingerprint].AddSeconds(10) -gt $now) {
                    Write-HttpJson -Response $context.Response -StatusCode 202 -Body @{ accepted=$true; deduped=$true }
                    continue
                }
                $recentEvents[$fingerprint] = $now
                $tmp = Join-Path $DropDir "$([guid]::NewGuid()).tmp"
                $dst = [System.IO.Path]::ChangeExtension($tmp, '.json')
                [System.IO.File]::WriteAllText($tmp, ($obj | ConvertTo-Json -Compress), [System.Text.Encoding]::UTF8)
                Move-Item -Path $tmp -Destination $dst -Force
                Write-HttpJson -Response $context.Response -StatusCode 202 -Body @{ accepted=$true }
                continue
            }
            if ($context.Request.HttpMethod -eq 'GET' -and $path -match '^/jobs/([^/]+)$') {
                $jobs = Get-JobsByRenewalId -StateDir $StateDir -RenewalId $matches[1]
                Write-HttpJson -Response $context.Response -StatusCode 200 -Body @{ jobs=$jobs }
                continue
            }
            if ($context.Request.HttpMethod -eq 'GET' -and $path -match '^/jobs/status/([^/]+)$') {
                $job = Get-JobById -StateDir $StateDir -JobId $matches[1]
                if ($null -eq $job) { Write-HttpJson -Response $context.Response -StatusCode 404 -Body @{ error='not_found' }; continue }
                Write-HttpJson -Response $context.Response -StatusCode 200 -Body @{ job_id=$job.job_id; status=$job.status; step=$job.step }
                continue
            }
            Write-HttpJson -Response $context.Response -StatusCode 404 -Body @{ error='not_found' }
        } catch {
            Write-CertificateLog -Level Error -Message "HTTP listener request failed: $($_.Exception.Message)"
            Write-HttpJson -Response $context.Response -StatusCode 500 -Body @{ error='internal_error' }
        }
    }
}

Export-ModuleMember -Function Start-CertificateHttpListener
