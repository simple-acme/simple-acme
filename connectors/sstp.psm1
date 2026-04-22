Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module RemoteAccess -ErrorAction Stop
Import-Module WebAdministration -ErrorAction SilentlyContinue

function Get-SstpThumbprint {
    param([hashtable]$Context,[switch]$UsePrevious)
    $value = if ($UsePrevious) { [string]$Context.previous_artifact_ref } else { [string]$Context.event.thumbprint }
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'SSTP connector requires a certificate thumbprint.' }
    return $value.Replace(' ','').ToUpperInvariant()
}

function Update-DefaultIisBinding {
    param([string]$Thumbprint,[string]$Mode)
    if ($Mode -ne 'true' -and $Mode -ne '1' -and $Mode -ne 'yes') { return }

    $bindings = Get-WebBinding -Name 'Default Web Site' -Protocol 'https' -ErrorAction SilentlyContinue
    foreach ($b in @($bindings)) {
        if ([string]$b.bindingInformation -eq '*:443:') { Remove-WebBinding -Name 'Default Web Site' -BindingInformation '*:443:' -Protocol 'https' }
    }

    New-WebBinding -Name 'Default Web Site' -Protocol 'https' -IPAddress '*' -Port 443 -Force | Out-Null
    $binding = Get-WebBinding -Name 'Default Web Site' -Protocol 'https' | Where-Object { [string]$_.bindingInformation -eq '*:443:' } | Select-Object -First 1
    if ($null -ne $binding) { $binding.AddSslCertificate($Thumbprint, 'my') }
}

function Invoke-SstpProbe { param([hashtable]$Context)
    $svc = Get-Service -Name 'RemoteAccess' -ErrorAction Stop
    @{ reachable = $true; auth_valid = $true; detail = "RemoteAccess service state: $($svc.Status)." }
}
function Invoke-SstpDeploy { param([hashtable]$Context)
    $thumb = Get-SstpThumbprint -Context $Context
    @{ artifact_ref = $thumb; detail = 'Using certificate thumbprint from renewal event.' }
}
function Invoke-SstpBind { param([hashtable]$Context)
    $thumb = [string]$Context.artifact_ref
    $cert = Get-ChildItem -Path Cert:\LocalMachine\My | Where-Object { $_.Thumbprint -eq $thumb } | Select-Object -First 1
    if ($null -eq $cert) { throw "SSTP certificate '$thumb' not found in LocalMachine\\My." }

    Stop-Service -Name 'RemoteAccess' -ErrorAction Stop
    Update-DefaultIisBinding -Thumbprint $thumb -Mode ([string]$Context.config.settings.recreate_default_bindings).ToLowerInvariant()
    Set-RemoteAccess -SslCertificate $cert
    @{ success = $true; detail = 'SSTP SSL certificate binding updated.' }
}
function Invoke-SstpActivate { param([hashtable]$Context)
    Start-Service -Name 'RemoteAccess' -ErrorAction Stop
    @{ success = $true; detail = 'RemoteAccess service started.' }
}
function Invoke-SstpVerify { param([hashtable]$Context)
    $ssl = Get-RemoteAccess | Select-Object -ExpandProperty SslCertificate -ErrorAction SilentlyContinue
    $actual = if ($ssl -and $ssl.Thumbprint) { [string]$ssl.Thumbprint } else { '' }
    @{ verified = ($actual.ToUpperInvariant() -eq ([string]$Context.artifact_ref).ToUpperInvariant()); detail = 'SSTP certificate assignment verified.' }
}
function Invoke-SstpRollback { param([hashtable]$Context)
    $old = Get-SstpThumbprint -Context $Context -UsePrevious
    $cert = Get-ChildItem -Path Cert:\LocalMachine\My | Where-Object { $_.Thumbprint -eq $old } | Select-Object -First 1
    if ($null -eq $cert) { throw "Rollback certificate '$old' not found in LocalMachine\\My." }
    Set-RemoteAccess -SslCertificate $cert
    Restart-Service -Name 'RemoteAccess' -Force
    @{ success = $true; detail = 'SSTP rollback applied.' }
}


function Invoke-SstpConnectorProbe { param([hashtable]$Context) Invoke-SstpProbe -Context $Context }
function Invoke-SstpConnectorDeploy { param([hashtable]$Context) Invoke-SstpDeploy -Context $Context }
function Invoke-SstpConnectorBind { param([hashtable]$Context) Invoke-SstpBind -Context $Context }
function Invoke-SstpConnectorActivate { param([hashtable]$Context) Invoke-SstpActivate -Context $Context }
function Invoke-SstpConnectorVerify { param([hashtable]$Context) Invoke-SstpVerify -Context $Context }
function Invoke-SstpConnectorRollback { param([hashtable]$Context) Invoke-SstpRollback -Context $Context }

Export-ModuleMember -Function Invoke-SstpProbe,Invoke-SstpDeploy,Invoke-SstpBind,Invoke-SstpActivate,Invoke-SstpVerify,Invoke-SstpRollback,Invoke-SstpConnectorProbe,Invoke-SstpConnectorDeploy,Invoke-SstpConnectorBind,Invoke-SstpConnectorActivate,Invoke-SstpConnectorVerify,Invoke-SstpConnectorRollback
