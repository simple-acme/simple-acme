Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-SqlServerThumbprint {
    param([hashtable]$Context,[switch]$UsePrevious)
    $value = if ($UsePrevious) { [string]$Context.previous_artifact_ref } else { [string]$Context.event.thumbprint }
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'SQL Server connector requires a certificate thumbprint.' }
    return $value.Replace(' ','').ToUpperInvariant()
}

function Get-SqlServiceName {
    param([hashtable]$Context)
    $instance = [string]$Context.config.settings.instance_name
    if ([string]::IsNullOrWhiteSpace($instance) -or $instance -eq 'MSSQLSERVER') { return 'MSSQLSERVER' }
    return "MSSQL`$$instance"
}

function Get-SqlSuperSocketNetLibKey {
    param([string]$InstanceName)

    $roots = Get-ChildItem -Path 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SQL Server' -Recurse -ErrorAction Stop |
        Where-Object { $_.Name -like '*SuperSocketNetLib' }

    if ([string]::IsNullOrWhiteSpace($InstanceName) -or $InstanceName -eq 'MSSQLSERVER') {
        return $roots | Sort-Object Name -Descending | Select-Object -First 1
    }

    $match = $roots | Where-Object { $_.Name -like "*$InstanceName*SuperSocketNetLib" } | Select-Object -First 1
    if ($null -ne $match) { return $match }
    return $roots | Sort-Object Name -Descending | Select-Object -First 1
}

function Set-SqlCertificateThumbprint {
    param([hashtable]$Context,[string]$Thumbprint)
    $instance = [string]$Context.config.settings.instance_name
    if ([string]::IsNullOrWhiteSpace($instance)) { $instance = 'MSSQLSERVER' }
    $key = Get-SqlSuperSocketNetLibKey -InstanceName $instance
    if ($null -eq $key) { throw 'Unable to locate SQL Server SuperSocketNetLib registry key.' }
    Set-ItemProperty -Path $key.PSPath -Name 'Certificate' -Value $Thumbprint.ToLowerInvariant()
}

function Invoke-SqlServerProbe { param([hashtable]$Context)
    $serviceName = Get-SqlServiceName -Context $Context
    $svc = Get-Service -Name $serviceName -ErrorAction Stop
    @{ reachable = $true; auth_valid = $true; detail = "SQL service '$serviceName' state: $($svc.Status)." }
}
function Invoke-SqlServerDeploy { param([hashtable]$Context)
    $thumb = Get-SqlServerThumbprint -Context $Context
    @{ artifact_ref = $thumb; detail = 'Using certificate thumbprint from renewal event.' }
}
function Invoke-SqlServerBind { param([hashtable]$Context)
    Set-SqlCertificateThumbprint -Context $Context -Thumbprint ([string]$Context.artifact_ref)
    @{ success = $true; detail = 'SQL Server certificate thumbprint set in registry.' }
}
function Invoke-SqlServerActivate { param([hashtable]$Context)
    $serviceName = Get-SqlServiceName -Context $Context
    Restart-Service -Name $serviceName -Force
    @{ success = $true; detail = "SQL service '$serviceName' restarted." }
}
function Invoke-SqlServerVerify { param([hashtable]$Context)
    $instance = [string]$Context.config.settings.instance_name
    if ([string]::IsNullOrWhiteSpace($instance)) { $instance = 'MSSQLSERVER' }
    $key = Get-SqlSuperSocketNetLibKey -InstanceName $instance
    $actual = [string](Get-ItemProperty -Path $key.PSPath -Name 'Certificate').Certificate
    @{ verified = ($actual.ToUpperInvariant() -eq ([string]$Context.artifact_ref).ToUpperInvariant()); detail = 'SQL Server certificate registry value verified.' }
}
function Invoke-SqlServerRollback { param([hashtable]$Context)
    $old = Get-SqlServerThumbprint -Context $Context -UsePrevious
    Set-SqlCertificateThumbprint -Context $Context -Thumbprint $old
    $serviceName = Get-SqlServiceName -Context $Context
    Restart-Service -Name $serviceName -Force
    @{ success = $true; detail = 'SQL Server rollback applied.' }
}


function Invoke-SqlServerConnectorProbe { param([hashtable]$Context) Invoke-SqlServerProbe -Context $Context }
function Invoke-SqlServerConnectorDeploy { param([hashtable]$Context) Invoke-SqlServerDeploy -Context $Context }
function Invoke-SqlServerConnectorBind { param([hashtable]$Context) Invoke-SqlServerBind -Context $Context }
function Invoke-SqlServerConnectorActivate { param([hashtable]$Context) Invoke-SqlServerActivate -Context $Context }
function Invoke-SqlServerConnectorVerify { param([hashtable]$Context) Invoke-SqlServerVerify -Context $Context }
function Invoke-SqlServerConnectorRollback { param([hashtable]$Context) Invoke-SqlServerRollback -Context $Context }

Export-ModuleMember -Function Invoke-SqlServerProbe,Invoke-SqlServerDeploy,Invoke-SqlServerBind,Invoke-SqlServerActivate,Invoke-SqlServerVerify,Invoke-SqlServerRollback,Invoke-SqlServerConnectorProbe,Invoke-SqlServerConnectorDeploy,Invoke-SqlServerConnectorBind,Invoke-SqlServerConnectorActivate,Invoke-SqlServerConnectorVerify,Invoke-SqlServerConnectorRollback
