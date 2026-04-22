Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ExchangeThumbprint {
    param([hashtable]$Context,[switch]$UsePrevious)
    $value = if ($UsePrevious) { [string]$Context.previous_artifact_ref } else { [string]$Context.event.thumbprint }
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'Exchange connector requires a certificate thumbprint.' }
    return $value.Replace(' ','').ToUpperInvariant()
}

function Get-ExchangeServicesFromContext {
    param([hashtable]$Context)
    $services = [string]$Context.config.settings.services
    if ([string]::IsNullOrWhiteSpace($services)) { return 'IIS,SMTP' }
    return $services
}

function Invoke-ExchangeRemote {
    param(
        [Parameter(Mandatory=$true)][scriptblock]$ScriptBlock,
        [object[]]$ArgumentList = @()
    )

    $session = New-PSSession -ConfigurationName Microsoft.Exchange -ConnectionUri 'http://localhost/PowerShell/'
    try {
        $null = Import-PSSession $session -DisableNameChecking -AllowClobber
        Invoke-Command -Session $session -ScriptBlock $ScriptBlock -ArgumentList $ArgumentList
    } finally {
        if ($null -ne $session) { Remove-PSSession -Session $session }
    }
}

function Invoke-ExchangeProbe { param([hashtable]$Context)
    $null = Invoke-ExchangeRemote -ScriptBlock { Get-Command Enable-ExchangeCertificate -ErrorAction Stop | Out-Null }
    @{ reachable = $true; auth_valid = $true; detail = 'Exchange management endpoint is available.' }
}
function Invoke-ExchangeDeploy { param([hashtable]$Context)
    $thumb = Get-ExchangeThumbprint -Context $Context
    @{ artifact_ref = $thumb; detail = 'Using certificate thumbprint from renewal event.' }
}
function Invoke-ExchangeBind { param([hashtable]$Context)
    $thumb = [string]$Context.artifact_ref
    $services = Get-ExchangeServicesFromContext -Context $Context
    $null = Invoke-ExchangeRemote -ScriptBlock {
        param($tp, $svc)
        Enable-ExchangeCertificate -Thumbprint $tp -Services $svc -Force -ErrorAction Stop
    } -ArgumentList @($thumb, $services)
    @{ success = $true; detail = "Exchange certificate enabled for: $services." }
}
function Invoke-ExchangeActivate { param([hashtable]$Context)
    @{ success = $true; detail = 'Exchange activation completed.' }
}
function Invoke-ExchangeVerify { param([hashtable]$Context)
    $thumb = ([string]$Context.artifact_ref).ToUpperInvariant()
    $cert = Invoke-ExchangeRemote -ScriptBlock {
        param($tp)
        Get-ExchangeCertificate -Thumbprint $tp -ErrorAction SilentlyContinue
    } -ArgumentList @($thumb)
    @{ verified = [bool]($null -ne $cert); detail = 'Exchange certificate lookup completed.' }
}
function Invoke-ExchangeRollback { param([hashtable]$Context)
    $old = Get-ExchangeThumbprint -Context $Context -UsePrevious
    $services = Get-ExchangeServicesFromContext -Context $Context
    $null = Invoke-ExchangeRemote -ScriptBlock {
        param($tp, $svc)
        Enable-ExchangeCertificate -Thumbprint $tp -Services $svc -Force -ErrorAction Stop
    } -ArgumentList @($old, $services)
    @{ success = $true; detail = 'Exchange rollback applied using previous thumbprint.' }
}


function Invoke-ExchangeConnectorProbe { param([hashtable]$Context) Invoke-ExchangeProbe -Context $Context }
function Invoke-ExchangeConnectorDeploy { param([hashtable]$Context) Invoke-ExchangeDeploy -Context $Context }
function Invoke-ExchangeConnectorBind { param([hashtable]$Context) Invoke-ExchangeBind -Context $Context }
function Invoke-ExchangeConnectorActivate { param([hashtable]$Context) Invoke-ExchangeActivate -Context $Context }
function Invoke-ExchangeConnectorVerify { param([hashtable]$Context) Invoke-ExchangeVerify -Context $Context }
function Invoke-ExchangeConnectorRollback { param([hashtable]$Context) Invoke-ExchangeRollback -Context $Context }

Export-ModuleMember -Function Invoke-ExchangeProbe,Invoke-ExchangeDeploy,Invoke-ExchangeBind,Invoke-ExchangeActivate,Invoke-ExchangeVerify,Invoke-ExchangeRollback,Invoke-ExchangeConnectorProbe,Invoke-ExchangeConnectorDeploy,Invoke-ExchangeConnectorBind,Invoke-ExchangeConnectorActivate,Invoke-ExchangeConnectorVerify,Invoke-ExchangeConnectorRollback
