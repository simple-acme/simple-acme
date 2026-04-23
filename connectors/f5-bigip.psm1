#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module "$PSScriptRoot/../core/Logger.psm1" -Force

function Get-F5Headers {
    param([hashtable]$Context)
    $token = [string]$Context.config.settings.token
    if ([string]::IsNullOrWhiteSpace($token) -and $Context.config.settings.ContainsKey('token_env')) {
        $tokenName = [string]$Context.config.settings.token_env
        $token = [Environment]::GetEnvironmentVariable($tokenName)
    }
    if ([string]::IsNullOrWhiteSpace($token)) { throw 'F5 token is not configured (set settings.token or settings.token_env).' }
    @{ Authorization = "Bearer $token" }
}

function Invoke-F5Rest {
    param([string]$Method,[string]$Uri,$Body,[hashtable]$Headers)
    $skip = $env:CERTIFICAAT_SKIP_TLS_CHECK -eq '1'
    if ($skip) { Write-CertificaatLog -Level 'WARN' -Message 'CERTIFICAAT_SKIP_TLS_CHECK is enabled for F5 connector.' }
    try {
        $params = @{ Method = $Method; Uri = $Uri; Headers = $Headers; ErrorAction = 'Stop' }
        if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Depth 10); $params.ContentType = 'application/json' }
        if ($skip) { [Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }; Write-CertificaatLog -Level Warning -Message 'CERTIFICAAT_SKIP_TLS_CHECK is enabled for F5 connector.' }
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        try { Invoke-RestMethod @params } finally { [Net.ServicePointManager]::ServerCertificateValidationCallback = $null }
    } catch {
        throw "F5 API call failed for $Method ${Uri}: $($_.Exception.Message)"
    }
}

function Invoke-F5BigipConnectorProbe { param([hashtable]$Context)
    $base = "https://$($Context.config.settings.host)/mgmt/tm"
    $null = Invoke-F5Rest -Method 'GET' -Uri "$base/sys/version" -Headers (Get-F5Headers -Context $Context)
    @{ reachable = $true; auth_valid = $true; detail = 'F5 reachable and authenticated.' }
}
function Invoke-F5BigipConnectorDeploy { param([hashtable]$Context)
    $base = "https://$($Context.config.settings.host)/mgmt/tm"
    $name = "cert-$($Context.event.thumbprint)"
    $certPem = Get-Content -Raw -Encoding UTF8 -Path $Context.event.cert_path
    $keyPem = Get-Content -Raw -Encoding UTF8 -Path $Context.event.key_path
    $h = Get-F5Headers -Context $Context
    $null = Invoke-F5Rest -Method 'POST' -Uri "$base/sys/crypto/cert" -Body @{ name = $name; content = $certPem } -Headers $h
    $null = Invoke-F5Rest -Method 'POST' -Uri "$base/sys/crypto/key" -Body @{ name = $name; content = $keyPem } -Headers $h
    @{ artifact_ref = $name; detail = 'F5 certificate and key uploaded.' }
}
function Invoke-F5BigipConnectorBind { param([hashtable]$Context)
    $base = "https://$($Context.config.settings.host)/mgmt/tm"
    $null = Invoke-F5Rest -Method 'PATCH' -Uri "$base/ltm/profile/client-ssl/$($Context.config.settings.ssl_profile)" -Body @{ cert = $Context.artifact_ref; key = $Context.artifact_ref } -Headers (Get-F5Headers -Context $Context)
    @{ success = $true; detail = 'F5 certificate bound.' }
}
function Invoke-F5BigipConnectorActivate { param([hashtable]$Context)
    $base = "https://$($Context.config.settings.host)/mgmt/tm"
    $null = Invoke-F5Rest -Method 'POST' -Uri "$base/sys/config" -Body @{ command = 'save' } -Headers (Get-F5Headers -Context $Context)
    @{ success = $true; detail = 'F5 config saved.' }
}
function Invoke-F5BigipConnectorVerify { param([hashtable]$Context)
    $base = "https://$($Context.config.settings.host)/mgmt/tm"
    $resp = Invoke-F5Rest -Method 'GET' -Uri "$base/ltm/profile/client-ssl/$($Context.config.settings.ssl_profile)" -Headers (Get-F5Headers -Context $Context)
    $isMatch = $resp.cert -eq $Context.artifact_ref
    @{ verified = [bool]$isMatch; detail = 'F5 profile verification completed.' }
}
function Invoke-F5BigipConnectorRollback { param([hashtable]$Context)
    if ([string]::IsNullOrWhiteSpace($Context.previous_artifact_ref)) { throw 'No previous_artifact_ref set for F5 rollback.' }
    $base = "https://$($Context.config.settings.host)/mgmt/tm"
    $null = Invoke-F5Rest -Method 'PATCH' -Uri "$base/ltm/profile/client-ssl/$($Context.config.settings.ssl_profile)" -Body @{ cert = $Context.previous_artifact_ref; key = $Context.previous_artifact_ref } -Headers (Get-F5Headers -Context $Context)
    @{ success = $true; detail = 'F5 rollback binding applied.' }
}

function Invoke-F5BigipRollback { param([hashtable]$Context) Invoke-F5BigipConnectorRollback -Context $Context }

Export-ModuleMember -Function Invoke-F5BigipConnectorProbe,Invoke-F5BigipConnectorDeploy,Invoke-F5BigipConnectorBind,Invoke-F5BigipConnectorActivate,Invoke-F5BigipConnectorVerify,Invoke-F5BigipConnectorRollback,Invoke-F5BigipRollback
