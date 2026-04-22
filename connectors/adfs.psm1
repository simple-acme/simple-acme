Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-AdfsThumbprint {
    param([hashtable]$Context,[switch]$UsePrevious)
    $value = if ($UsePrevious) { [string]$Context.previous_artifact_ref } else { [string]$Context.event.thumbprint }
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'ADFS connector requires a certificate thumbprint.' }
    return $value.Replace(' ','').ToUpperInvariant()
}

function Ensure-CertInLocalMachineMy {
    param([string]$Thumbprint)

    $cert = Get-ChildItem -Path Cert:\LocalMachine -Recurse | Where-Object { $_.Thumbprint -eq $Thumbprint } | Select-Object -First 1
    if ($null -eq $cert) { throw "Certificate '$Thumbprint' was not found in LocalMachine stores." }
    if ($cert.PSPath -like '*LocalMachine\My\*') { return $cert }

    $srcStoreName = $cert.PSParentPath.Split('\\')[-1]
    $srcStore = New-Object System.Security.Cryptography.X509Certificates.X509Store($srcStoreName, 'LocalMachine')
    $srcStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
    $copy = $srcStore.Certificates | Where-Object { $_.Thumbprint -eq $Thumbprint } | Select-Object -First 1

    $dstStore = New-Object System.Security.Cryptography.X509Certificates.X509Store('My', 'LocalMachine')
    $dstStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    $dstStore.Add($copy)

    $srcStore.Close()
    $dstStore.Close()

    return Get-ChildItem -Path Cert:\LocalMachine\My | Where-Object { $_.Thumbprint -eq $Thumbprint } | Select-Object -First 1
}

function Set-AdfsThumbprint {
    param([string]$Thumbprint)
    $null = Ensure-CertInLocalMachineMy -Thumbprint $Thumbprint
    Set-AdfsCertificate -CertificateType Service-Communications -Thumbprint $Thumbprint
    Set-AdfsSslCertificate -Thumbprint $Thumbprint
}

function Invoke-AdfsProbe { param([hashtable]$Context)
    $svc = Get-Service -Name 'adfssrv' -ErrorAction Stop
    @{ reachable = $true; auth_valid = $true; detail = "ADFS service state: $($svc.Status)." }
}
function Invoke-AdfsDeploy { param([hashtable]$Context)
    $thumb = Get-AdfsThumbprint -Context $Context
    $null = Ensure-CertInLocalMachineMy -Thumbprint $thumb
    @{ artifact_ref = $thumb; detail = 'Certificate ensured in LocalMachine\\My.' }
}
function Invoke-AdfsBind { param([hashtable]$Context)
    Set-AdfsThumbprint -Thumbprint ([string]$Context.artifact_ref)
    @{ success = $true; detail = 'ADFS certificate bindings updated.' }
}
function Invoke-AdfsActivate { param([hashtable]$Context)
    Restart-Service -Name 'adfssrv' -Force
    @{ success = $true; detail = 'ADFS service restarted.' }
}
function Invoke-AdfsVerify { param([hashtable]$Context)
    $cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Thumbprint -eq [string]$Context.artifact_ref } | Select-Object -First 1
    @{ verified = [bool]($null -ne $cert); detail = 'Certificate exists in LocalMachine\\My.' }
}
function Invoke-AdfsRollback { param([hashtable]$Context)
    $old = Get-AdfsThumbprint -Context $Context -UsePrevious
    Set-AdfsThumbprint -Thumbprint $old
    Restart-Service -Name 'adfssrv' -Force
    @{ success = $true; detail = 'ADFS rollback applied using previous thumbprint.' }
}


function Invoke-AdfsConnectorProbe { param([hashtable]$Context) Invoke-AdfsProbe -Context $Context }
function Invoke-AdfsConnectorDeploy { param([hashtable]$Context) Invoke-AdfsDeploy -Context $Context }
function Invoke-AdfsConnectorBind { param([hashtable]$Context) Invoke-AdfsBind -Context $Context }
function Invoke-AdfsConnectorActivate { param([hashtable]$Context) Invoke-AdfsActivate -Context $Context }
function Invoke-AdfsConnectorVerify { param([hashtable]$Context) Invoke-AdfsVerify -Context $Context }
function Invoke-AdfsConnectorRollback { param([hashtable]$Context) Invoke-AdfsRollback -Context $Context }

Export-ModuleMember -Function Invoke-AdfsProbe,Invoke-AdfsDeploy,Invoke-AdfsBind,Invoke-AdfsActivate,Invoke-AdfsVerify,Invoke-AdfsRollback,Invoke-AdfsConnectorProbe,Invoke-AdfsConnectorDeploy,Invoke-AdfsConnectorBind,Invoke-AdfsConnectorActivate,Invoke-AdfsConnectorVerify,Invoke-AdfsConnectorRollback
