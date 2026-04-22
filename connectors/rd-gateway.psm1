Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module RemoteDesktopServices -ErrorAction Stop

function Get-RdGatewayThumbprint {
    param([hashtable]$Context,[switch]$UsePrevious)
    $value = if ($UsePrevious) { [string]$Context.previous_artifact_ref } else { [string]$Context.event.thumbprint }
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'RD Gateway connector requires a certificate thumbprint.' }
    return $value.Replace(' ','').ToUpperInvariant()
}

function Set-RdGatewayBinding {
    param([string]$Thumbprint)
    Set-Item -Path 'RDS:\GatewayServer\SSLCertificate\Thumbprint' -Value $Thumbprint
}

function Invoke-RdGatewayProbe { param([hashtable]$Context)
    $null = Get-Item -Path 'RDS:\GatewayServer\SSLCertificate\Thumbprint' -ErrorAction Stop
    @{ reachable = $true; auth_valid = $true; detail = 'RD Gateway certificate path available.' }
}
function Invoke-RdGatewayDeploy { param([hashtable]$Context)
    $thumb = Get-RdGatewayThumbprint -Context $Context
    @{ artifact_ref = $thumb; detail = 'Using certificate thumbprint from renewal event.' }
}
function Invoke-RdGatewayBind { param([hashtable]$Context)
    Set-RdGatewayBinding -Thumbprint ([string]$Context.artifact_ref)
    @{ success = $true; detail = 'RD Gateway thumbprint binding updated.' }
}
function Invoke-RdGatewayActivate { param([hashtable]$Context)
    Restart-Service -Name 'TSGateway' -Force
    @{ success = $true; detail = 'TSGateway service restarted.' }
}
function Invoke-RdGatewayVerify { param([hashtable]$Context)
    $value = [string](Get-Item -Path 'RDS:\GatewayServer\SSLCertificate\Thumbprint').Value
    @{ verified = ($value.ToUpperInvariant() -eq ([string]$Context.artifact_ref).ToUpperInvariant()); detail = 'RD Gateway thumbprint verified.' }
}
function Invoke-RdGatewayRollback { param([hashtable]$Context)
    $old = Get-RdGatewayThumbprint -Context $Context -UsePrevious
    Set-RdGatewayBinding -Thumbprint $old
    Restart-Service -Name 'TSGateway' -Force
    @{ success = $true; detail = 'RD Gateway rollback applied.' }
}

Export-ModuleMember -Function Invoke-RdGatewayProbe,Invoke-RdGatewayDeploy,Invoke-RdGatewayBind,Invoke-RdGatewayActivate,Invoke-RdGatewayVerify,Invoke-RdGatewayRollback
