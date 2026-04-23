#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module "$PSScriptRoot/../core/Logger.psm1" -Force
$script:NitroSession = $null

function Invoke-CitrixRest {
    param([hashtable]$Context,[string]$Method,[string]$Path,$Body)
    $base = "https://$($Context.config.settings.host)/nitro/v1"
    $uri = "$base$Path"
    $skip = $env:CERTIFICAAT_SKIP_TLS_CHECK -eq '1'
    $params = @{ Method=$Method; Uri=$uri; ErrorAction='Stop'; WebSession=$script:NitroSession }
    if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Depth 10); $params.ContentType = 'application/json' }
    if ($skip) { [Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }; Write-CertificaatLog -Level Warning -Message 'CERTIFICAAT_SKIP_TLS_CHECK is enabled for Citrix connector.' }
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    try {
        return Invoke-RestMethod @params
    } catch {
        if ($_.Exception.Message -match '401') {
            Connect-CitrixNitro -Context $Context
            $params.WebSession = $script:NitroSession
            return Invoke-RestMethod @params
        }
        throw "Citrix API call failed for $Method ${uri}: $($_.Exception.Message)"
    } finally {
        [Net.ServicePointManager]::ServerCertificateValidationCallback = $null
    }
}

function Connect-CitrixNitro {
    param([hashtable]$Context)
    $user = [Environment]::GetEnvironmentVariable($Context.config.settings.user_env)
    $pass = [Environment]::GetEnvironmentVariable($Context.config.settings.password_env)
    if ([string]::IsNullOrWhiteSpace($user) -or [string]::IsNullOrWhiteSpace($pass)) { throw 'Citrix credentials are not configured via *_env variables.' }
    $script:NitroSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $body = @{ login = @{ username = $user; password = $pass } }
    $null = Invoke-CitrixRest -Context $Context -Method 'POST' -Path '/config/login' -Body $body
}

function Invoke-CitrixAdcConnectorProbe { param([hashtable]$Context)
    Connect-CitrixNitro -Context $Context
    $null = Invoke-CitrixRest -Context $Context -Method 'GET' -Path '/config/nsversion'
    @{ reachable = $true; auth_valid = $true; detail = 'Citrix reachable and authenticated.' }
}
function Invoke-CitrixAdcConnectorDeploy { param([hashtable]$Context)
    $body = @{ sslcertkey = @{ certkey = $Context.event.thumbprint; cert = $Context.event.cert_path; key = $Context.event.key_path } }
    $null = Invoke-CitrixRest -Context $Context -Method 'POST' -Path '/config/sslcertkey' -Body $body
    @{ artifact_ref = $Context.event.thumbprint; detail = 'Citrix sslcertkey created.' }
}
function Invoke-CitrixAdcConnectorBind { param([hashtable]$Context)
    $body = @{ sslvserver_sslcertkey_binding = @{ certkeyname = $Context.artifact_ref } }
    $null = Invoke-CitrixRest -Context $Context -Method 'PUT' -Path "/config/sslvserver_sslcertkey_binding/$($Context.config.settings.vserver)" -Body $body
    @{ success = $true; detail = 'Citrix certificate bound.' }
}
function Invoke-CitrixAdcConnectorActivate { param([hashtable]$Context)
    $null = Invoke-CitrixRest -Context $Context -Method 'POST' -Path '/config/nsconfig?action=save'
    @{ success = $true; detail = 'Citrix config saved.' }
}
function Invoke-CitrixAdcConnectorVerify { param([hashtable]$Context)
    $resp = Invoke-CitrixRest -Context $Context -Method 'GET' -Path "/config/sslvserver_sslcertkey_binding/$($Context.config.settings.vserver)"
    $name = $resp.sslvserver_sslcertkey_binding.certkeyname
    @{ verified = ($name -eq $Context.artifact_ref); detail = 'Citrix verification completed.' }
}
function Invoke-CitrixAdcConnectorRollback { param([hashtable]$Context)
    if ([string]::IsNullOrWhiteSpace($Context.previous_artifact_ref)) { throw 'No previous_artifact_ref set for Citrix rollback.' }
    $body = @{ sslvserver_sslcertkey_binding = @{ certkeyname = $Context.previous_artifact_ref } }
    $null = Invoke-CitrixRest -Context $Context -Method 'PUT' -Path "/config/sslvserver_sslcertkey_binding/$($Context.config.settings.vserver)" -Body $body
    @{ success = $true; detail = 'Citrix rollback binding applied.' }
}

function Invoke-CitrixAdcRollback { param([hashtable]$Context) Invoke-CitrixAdcConnectorRollback -Context $Context }

Export-ModuleMember -Function Invoke-CitrixAdcConnectorProbe,Invoke-CitrixAdcConnectorDeploy,Invoke-CitrixAdcConnectorBind,Invoke-CitrixAdcConnectorActivate,Invoke-CitrixAdcConnectorVerify,Invoke-CitrixAdcConnectorRollback,Invoke-CitrixAdcRollback
