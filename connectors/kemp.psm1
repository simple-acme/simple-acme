#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module "$PSScriptRoot/../core/Logger.psm1" -Force

function Get-KempAuthHeader { param([hashtable]$Context)
    $user = [Environment]::GetEnvironmentVariable($Context.config.settings.user_env)
    $pass = [Environment]::GetEnvironmentVariable($Context.config.settings.password_env)
    if ([string]::IsNullOrWhiteSpace($user) -or [string]::IsNullOrWhiteSpace($pass)) { throw 'Kemp credentials are not configured via *_env variables.' }
    $bytes = [Text.Encoding]::ASCII.GetBytes("$user`:$pass")
    @{ Authorization = "Basic $([Convert]::ToBase64String($bytes))" }
}

function Invoke-KempGet { param([string]$Uri,[hashtable]$Headers)
    $params = @{ Method='GET'; Uri=$Uri; Headers=$Headers; ErrorAction='Stop' }
    if ($env:CERTIFICATE_SKIP_TLS_CHECK -eq '1') { [Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }; Write-CertificateLog -Level Warning -Message 'CERTIFICATE_SKIP_TLS_CHECK is enabled for Kemp connector.' }
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    try { Invoke-RestMethod @params } finally { [Net.ServicePointManager]::ServerCertificateValidationCallback = $null }
}

function Invoke-MultipartPost {
    param([string]$Uri,[hashtable]$Headers,[string]$CertPath,[string]$KeyPath)
    $handler = [System.Net.Http.HttpClientHandler]::new()
    if ($env:CERTIFICATE_SKIP_TLS_CHECK -eq '1') {
        $handler.ServerCertificateCustomValidationCallback = { $true }
        Write-CertificateLog -Level 'WARN' -Message 'CERTIFICATE_SKIP_TLS_CHECK is enabled for Kemp connector.'
    }
    $client = [System.Net.Http.HttpClient]::new($handler)
    foreach ($key in $Headers.Keys) { $client.DefaultRequestHeaders.Add($key, $Headers[$key]) }
    $form = [System.Net.Http.MultipartFormDataContent]::new()
    $form.Add([System.Net.Http.StringContent]::new('1'), 'replace')
    $form.Add([System.Net.Http.ByteArrayContent]::new([IO.File]::ReadAllBytes($CertPath)), 'cert', [IO.Path]::GetFileName($CertPath))
    $form.Add([System.Net.Http.ByteArrayContent]::new([IO.File]::ReadAllBytes($KeyPath)), 'key', [IO.Path]::GetFileName($KeyPath))
    $response = $client.PostAsync($Uri, $form).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) { throw "Kemp multipart upload failed ($([int]$response.StatusCode)): $body" }
    return $body
}

function Invoke-KempConnectorProbe { param([hashtable]$Context)
    $base = "https://$($Context.config.settings.host)/access"
    $null = Invoke-KempGet -Uri "$base/stats" -Headers (Get-KempAuthHeader -Context $Context)
    @{ reachable = $true; auth_valid = $true; detail = 'Kemp reachable and authenticated.' }
}
function Invoke-KempConnectorDeploy { param([hashtable]$Context)
    $base = "https://$($Context.config.settings.host)/access"
    $null = Invoke-MultipartPost -Uri "$base/addcert" -Headers (Get-KempAuthHeader -Context $Context) -CertPath $Context.event.fullchain_path -KeyPath $Context.event.key_path
    @{ artifact_ref = [IO.Path]::GetFileName($Context.event.fullchain_path); detail = 'Kemp certificate uploaded.' }
}
function Invoke-KempConnectorBind { param([hashtable]$Context)
    $base = "https://$($Context.config.settings.host)/access"
    $null = Invoke-KempGet -Uri "$base/modvs?vs=$($Context.config.settings.vs_id)&cert=$($Context.artifact_ref)" -Headers (Get-KempAuthHeader -Context $Context)
    @{ success = $true; detail = 'Kemp certificate bound.' }
}
function Invoke-KempConnectorActivate { param([hashtable]$Context) @{ success = $true; detail = 'Kemp applies on bind' } }
function Invoke-KempConnectorVerify { param([hashtable]$Context)
    $base = "https://$($Context.config.settings.host)/access"
    [xml]$resp = Invoke-KempGet -Uri "$base/showvs?vs=$($Context.config.settings.vs_id)" -Headers (Get-KempAuthHeader -Context $Context)
    $match = $resp.SelectSingleNode('//CertFile')
    @{ verified = ($match -and $match.InnerText -eq $Context.artifact_ref); detail = 'Kemp verification completed.' }
}
function Invoke-KempConnectorRollback { param([hashtable]$Context)
    if ([string]::IsNullOrWhiteSpace($Context.previous_artifact_ref)) { throw 'No previous_artifact_ref set for Kemp rollback.' }
    $base = "https://$($Context.config.settings.host)/access"
    $null = Invoke-KempGet -Uri "$base/modvs?vs=$($Context.config.settings.vs_id)&cert=$($Context.previous_artifact_ref)" -Headers (Get-KempAuthHeader -Context $Context)
    @{ success = $true; detail = 'Kemp rollback binding applied.' }
}

function Invoke-KempRollback { param([hashtable]$Context) Invoke-KempConnectorRollback -Context $Context }

Export-ModuleMember -Function Invoke-KempConnectorProbe,Invoke-KempConnectorDeploy,Invoke-KempConnectorBind,Invoke-KempConnectorActivate,Invoke-KempConnectorVerify,Invoke-KempConnectorRollback,Invoke-KempRollback
