Set-StrictMode -Version Latest
Import-Module "$PSScriptRoot/Logger.psm1" -Force

function Get-HttpListenerConfig {
    $host = [Environment]::GetEnvironmentVariable('CERTIFICAAT_HTTP_HOST')
    $port = [Environment]::GetEnvironmentVariable('CERTIFICAAT_HTTP_PORT')
    $token = [Environment]::GetEnvironmentVariable('CERTIFICAAT_HTTP_BEARER_TOKEN')

    if ([string]::IsNullOrWhiteSpace($host)) { $host = '127.0.0.1' }
    if ([string]::IsNullOrWhiteSpace($port)) { $port = '8088' }
    if ([string]::IsNullOrWhiteSpace($token)) { throw 'CERTIFICAAT_HTTP_BEARER_TOKEN is required when HTTP listener mode is enabled.' }

    return @{ Host = $host; Port = $port; Token = $token }
}

function Write-JsonResponse {
    param([Parameter(Mandatory)]$Response,[int]$StatusCode = 200,[hashtable]$Body = @{})

    $json = $Body | ConvertTo-Json -Depth 8
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $Response.StatusCode = $StatusCode
    $Response.ContentType = 'application/json'
    $Response.ContentEncoding = [System.Text.Encoding]::UTF8
    $Response.OutputStream.Write($bytes, 0, $bytes.Length)
    $Response.OutputStream.Close()
}

function Assert-BearerAuth {
    param([Parameter(Mandatory)]$Request,[Parameter(Mandatory)][string]$ExpectedToken)
    $hdr = [string]$Request.Headers['Authorization']
    if ([string]::IsNullOrWhiteSpace($hdr) -or -not $hdr.StartsWith('Bearer ')) { return $false }
    return $hdr.Substring(7) -ceq $ExpectedToken
}

function Read-JsonRequestBody {
    param([Parameter(Mandatory)]$Request)

    $reader = New-Object System.IO.StreamReader($Request.InputStream, $Request.ContentEncoding)
    try {
        $body = $reader.ReadToEnd()
    } finally {
        $reader.Dispose()
    }
    if ([string]::IsNullOrWhiteSpace($body)) { throw 'Request body is empty.' }
    return $body | ConvertFrom-Json
}

function Start-CertificaatHttpListener {
    param([Parameter(Mandatory)][scriptblock]$OnEvent)

    $cfg = Get-HttpListenerConfig
    $prefix = "http://$($cfg.Host):$($cfg.Port)/"

    $listener = New-Object System.Net.HttpListener
    $listener.Prefixes.Add($prefix)
    $listener.Start()
    Write-CertificaatLog -Level 'INFO' -Message "HTTP listener started on $prefix"

    try {
        while ($listener.IsListening) {
            $context = $listener.GetContext()
            try {
                $path = $context.Request.Url.AbsolutePath
                if ($context.Request.HttpMethod -eq 'GET' -and $path -eq '/health') {
                    Write-JsonResponse -Response $context.Response -StatusCode 200 -Body @{ status = 'ok'; service = 'certificaat-orchestrator' }
                    continue
                }

                if (-not (Assert-BearerAuth -Request $context.Request -ExpectedToken $cfg.Token)) {
                    Write-JsonResponse -Response $context.Response -StatusCode 401 -Body @{ error = 'unauthorized' }
                    continue
                }

                if ($context.Request.HttpMethod -eq 'POST' -and $path -eq '/events/certificate-renewed') {
                    $eventObj = Read-JsonRequestBody -Request $context.Request
                    & $OnEvent $eventObj
                    Write-JsonResponse -Response $context.Response -StatusCode 202 -Body @{ accepted = $true }
                    continue
                }

                Write-JsonResponse -Response $context.Response -StatusCode 404 -Body @{ error = 'not_found' }
            } catch {
                Write-CertificaatLog -Level 'ERROR' -Message "HTTP listener request failed: $($_.Exception.Message)"
                Write-JsonResponse -Response $context.Response -StatusCode 500 -Body @{ error = 'internal_error'; message = $_.Exception.Message }
            }
        }
    } finally {
        if ($listener.IsListening) { $listener.Stop() }
        $listener.Close()
    }
}

Export-ModuleMember -Function Start-CertificaatHttpListener
