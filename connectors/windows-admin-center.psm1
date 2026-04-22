Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-WindowsAdminCenterThumbprint {
    param([hashtable]$Context,[switch]$UsePrevious)
    $value = if ($UsePrevious) { [string]$Context.previous_artifact_ref } else { [string]$Context.event.thumbprint }
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'Windows Admin Center connector requires a certificate thumbprint.' }
    return $value.Replace(' ','').ToUpperInvariant()
}

function Get-WacProductGuid {
    $wac = Get-WmiObject Win32_Product | Where-Object { $_.Name -eq 'Windows Admin Center' } | Select-Object -First 1
    if ($null -eq $wac) { throw 'Windows Admin Center is not installed.' }
    return [string]$wac.IdentifyingNumber
}

function Invoke-WindowsAdminCenterProbe { param([hashtable]$Context)
    $guid = Get-WacProductGuid
    @{ reachable = $true; auth_valid = $true; detail = "Windows Admin Center MSI product GUID detected: $guid." }
}
function Invoke-WindowsAdminCenterDeploy { param([hashtable]$Context)
    $thumb = Get-WindowsAdminCenterThumbprint -Context $Context
    @{ artifact_ref = $thumb; detail = 'Using certificate thumbprint from renewal event.' }
}
function Invoke-WindowsAdminCenterBind { param([hashtable]$Context)
    $guid = Get-WacProductGuid
    $args = @('/i', $guid, '/qn', "SME_THUMBPRINT=$([string]$Context.artifact_ref)", 'SSL_CERTIFICATE_OPTION=installed')
    $proc = Start-Process -FilePath "$env:SystemRoot\System32\msiexec.exe" -ArgumentList $args -PassThru -Wait
    if ($proc.ExitCode -ne 0) { throw "msiexec failed with exit code $($proc.ExitCode)." }
    @{ success = $true; detail = 'Windows Admin Center reconfigured via msiexec.' }
}
function Invoke-WindowsAdminCenterActivate { param([hashtable]$Context)
    Restart-Service -Name 'ServerManagementGateway' -Force
    @{ success = $true; detail = 'ServerManagementGateway service restarted.' }
}
function Invoke-WindowsAdminCenterVerify { param([hashtable]$Context)
    $svc = Get-Service -Name 'ServerManagementGateway' -ErrorAction Stop
    @{ verified = ($svc.Status -eq 'Running'); detail = 'Windows Admin Center service is running after configuration.' }
}
function Invoke-WindowsAdminCenterRollback { param([hashtable]$Context)
    $old = Get-WindowsAdminCenterThumbprint -Context $Context -UsePrevious
    $guid = Get-WacProductGuid
    $args = @('/i', $guid, '/qn', "SME_THUMBPRINT=$old", 'SSL_CERTIFICATE_OPTION=installed')
    $proc = Start-Process -FilePath "$env:SystemRoot\System32\msiexec.exe" -ArgumentList $args -PassThru -Wait
    if ($proc.ExitCode -ne 0) { throw "Rollback msiexec failed with exit code $($proc.ExitCode)." }
    Restart-Service -Name 'ServerManagementGateway' -Force
    @{ success = $true; detail = 'Windows Admin Center rollback applied.' }
}


function Invoke-WindowsAdminCenterConnectorProbe { param([hashtable]$Context) Invoke-WindowsAdminCenterProbe -Context $Context }
function Invoke-WindowsAdminCenterConnectorDeploy { param([hashtable]$Context) Invoke-WindowsAdminCenterDeploy -Context $Context }
function Invoke-WindowsAdminCenterConnectorBind { param([hashtable]$Context) Invoke-WindowsAdminCenterBind -Context $Context }
function Invoke-WindowsAdminCenterConnectorActivate { param([hashtable]$Context) Invoke-WindowsAdminCenterActivate -Context $Context }
function Invoke-WindowsAdminCenterConnectorVerify { param([hashtable]$Context) Invoke-WindowsAdminCenterVerify -Context $Context }
function Invoke-WindowsAdminCenterConnectorRollback { param([hashtable]$Context) Invoke-WindowsAdminCenterRollback -Context $Context }

Export-ModuleMember -Function Invoke-WindowsAdminCenterProbe,Invoke-WindowsAdminCenterDeploy,Invoke-WindowsAdminCenterBind,Invoke-WindowsAdminCenterActivate,Invoke-WindowsAdminCenterVerify,Invoke-WindowsAdminCenterRollback,Invoke-WindowsAdminCenterConnectorProbe,Invoke-WindowsAdminCenterConnectorDeploy,Invoke-WindowsAdminCenterConnectorBind,Invoke-WindowsAdminCenterConnectorActivate,Invoke-WindowsAdminCenterConnectorVerify,Invoke-WindowsAdminCenterConnectorRollback
