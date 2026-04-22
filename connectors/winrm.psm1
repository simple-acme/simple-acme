Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-WinrmThumbprint {
    param([hashtable]$Context,[switch]$UsePrevious)
    $value = if ($UsePrevious) { [string]$Context.previous_artifact_ref } else { [string]$Context.event.thumbprint }
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'WinRM connector requires a certificate thumbprint.' }
    return $value.Replace(' ','').ToUpperInvariant()
}

function Set-WinrmHttpsListener {
    param([string]$Thumbprint)

    $resource = 'winrm/config/listener'
    $listeners = @(Get-WSManInstance -ResourceURI $resource -Enumerate | Where-Object { $_.Transport -eq 'HTTPS' })

    if ($listeners.Count -eq 0) {
        $hostname = ([System.Net.Dns]::GetHostByName($env:COMPUTERNAME)).HostName.ToLowerInvariant()
        New-WSManInstance -ResourceURI $resource -SelectorSet @{ Transport='HTTPS'; Address='*' } -ValueSet @{ Hostname=$hostname; CertificateThumbprint=$Thumbprint } | Out-Null
        return
    }

    foreach ($listener in $listeners) {
        Set-WSManInstance -ResourceURI $resource -SelectorSet @{ Address=$listener.Address; Transport=$listener.Transport } -ValueSet @{ CertificateThumbprint=$Thumbprint } | Out-Null
    }
}

function Invoke-WinrmProbe { param([hashtable]$Context)
    $svc = Get-Service -Name 'WinRM' -ErrorAction Stop
    @{ reachable = $true; auth_valid = $true; detail = "WinRM service state: $($svc.Status)." }
}
function Invoke-WinrmDeploy { param([hashtable]$Context)
    $thumb = Get-WinrmThumbprint -Context $Context
    @{ artifact_ref = $thumb; detail = 'Using certificate thumbprint from renewal event.' }
}
function Invoke-WinrmBind { param([hashtable]$Context)
    Set-WinrmHttpsListener -Thumbprint ([string]$Context.artifact_ref)
    @{ success = $true; detail = 'WinRM HTTPS listener thumbprint updated.' }
}
function Invoke-WinrmActivate { param([hashtable]$Context)
    Restart-Service -Name 'WinRM' -Force
    @{ success = $true; detail = 'WinRM service restarted.' }
}
function Invoke-WinrmVerify { param([hashtable]$Context)
    $expected = ([string]$Context.artifact_ref).ToUpperInvariant()
    $listeners = @(Get-WSManInstance -ResourceURI 'winrm/config/listener' -Enumerate | Where-Object { $_.Transport -eq 'HTTPS' })
    $allMatch = ($listeners.Count -gt 0)
    foreach ($listener in $listeners) {
        if (([string]$listener.CertificateThumbprint).ToUpperInvariant() -ne $expected) { $allMatch = $false; break }
    }
    @{ verified = $allMatch; detail = 'WinRM HTTPS listener thumbprints verified.' }
}
function Invoke-WinrmRollback { param([hashtable]$Context)
    $old = Get-WinrmThumbprint -Context $Context -UsePrevious
    Set-WinrmHttpsListener -Thumbprint $old
    Restart-Service -Name 'WinRM' -Force
    @{ success = $true; detail = 'WinRM rollback applied.' }
}

Export-ModuleMember -Function Invoke-WinrmProbe,Invoke-WinrmDeploy,Invoke-WinrmBind,Invoke-WinrmActivate,Invoke-WinrmVerify,Invoke-WinrmRollback
