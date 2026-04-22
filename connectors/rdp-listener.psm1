Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RdpListenerThumbprint {
    param([hashtable]$Context,[switch]$UsePrevious)
    $value = if ($UsePrevious) { [string]$Context.previous_artifact_ref } else { [string]$Context.event.thumbprint }
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'RDP listener connector requires a certificate thumbprint.' }
    return $value.Replace(' ','').ToUpperInvariant()
}

function Invoke-RdpListenerProbe { param([hashtable]$Context)
    $null = Get-CimInstance -Namespace root/CIMV2/TerminalServices -ClassName Win32_TSGeneralSetting -ErrorAction Stop
    @{ reachable = $true; auth_valid = $true; detail = 'RDP listener CIM class available.' }
}
function Invoke-RdpListenerDeploy { param([hashtable]$Context)
    $thumb = Get-RdpListenerThumbprint -Context $Context
    @{ artifact_ref = $thumb; detail = 'Using certificate thumbprint from renewal event.' }
}
function Invoke-RdpListenerBind { param([hashtable]$Context)
    Get-CimInstance -Namespace root/CIMV2/TerminalServices -ClassName Win32_TSGeneralSetting | Set-CimInstance -Property @{ SSLCertificateSHA1Hash = [string]$Context.artifact_ref }
    @{ success = $true; detail = 'RDP listener certificate thumbprint updated.' }
}
function Invoke-RdpListenerActivate { param([hashtable]$Context)
    @{ success = $true; detail = 'RDP listener activation completed (no explicit restart required).' }
}
function Invoke-RdpListenerVerify { param([hashtable]$Context)
    $target = ([string]$Context.artifact_ref).ToUpperInvariant()
    $match = Get-CimInstance -Namespace root/CIMV2/TerminalServices -ClassName Win32_TSGeneralSetting | Where-Object { ([string]$_.SSLCertificateSHA1Hash).ToUpperInvariant() -eq $target }
    @{ verified = [bool]($null -ne $match); detail = 'RDP listener binding verified.' }
}
function Invoke-RdpListenerRollback { param([hashtable]$Context)
    $old = Get-RdpListenerThumbprint -Context $Context -UsePrevious
    Get-CimInstance -Namespace root/CIMV2/TerminalServices -ClassName Win32_TSGeneralSetting | Set-CimInstance -Property @{ SSLCertificateSHA1Hash = $old }
    @{ success = $true; detail = 'RDP listener rollback applied.' }
}

Export-ModuleMember -Function Invoke-RdpListenerProbe,Invoke-RdpListenerDeploy,Invoke-RdpListenerBind,Invoke-RdpListenerActivate,Invoke-RdpListenerVerify,Invoke-RdpListenerRollback
