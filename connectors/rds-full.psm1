Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RdsFullThumbprint {
    param([hashtable]$Context,[switch]$UsePrevious)
    $value = if ($UsePrevious) { [string]$Context.previous_artifact_ref } else { [string]$Context.event.thumbprint }
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'RDS full connector requires a certificate thumbprint.' }
    return $value.Replace(' ','').ToUpperInvariant()
}

function Get-RdsConnectionBroker {
    param([hashtable]$Context)
    $rdcb = [string]$Context.config.settings.rdcb_fqdn
    if ([string]::IsNullOrWhiteSpace($rdcb)) { return $env:COMPUTERNAME }
    return $rdcb
}

function Set-RdsRoles {
    param([string]$Thumbprint,[string]$ConnectionBroker)

    $roles = @('RDGateway','RDWebAccess','RDRedirector','RDPublishing')
    foreach ($role in $roles) {
        $cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Thumbprint -eq $Thumbprint } | Select-Object -First 1
        if ($null -eq $cert) { throw "Certificate '$Thumbprint' missing from LocalMachine\\My for role '$role'." }
        Set-RDCertificate -Role $role -Thumbprint $Thumbprint -ConnectionBroker $ConnectionBroker -Force
    }
}

function Invoke-RdsFullProbe { param([hashtable]$Context)
    $rdcb = Get-RdsConnectionBroker -Context $Context
    $null = Get-RDCertificate -Role RDGateway -ConnectionBroker $rdcb -ErrorAction Stop
    @{ reachable = $true; auth_valid = $true; detail = "RDS deployment reachable via broker '$rdcb'." }
}
function Invoke-RdsFullDeploy { param([hashtable]$Context)
    $thumb = Get-RdsFullThumbprint -Context $Context
    $cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Thumbprint -eq $thumb } | Select-Object -First 1
    if ($null -eq $cert) {
        $pfxPath = [string]$Context.event.pfx_path
        if (-not [string]::IsNullOrWhiteSpace($pfxPath) -and (Test-Path -LiteralPath $pfxPath)) {
            $pwd = [string]$Context.event.pfx_password
            $secure = if ([string]::IsNullOrWhiteSpace($pwd)) { ConvertTo-SecureString -String '' -AsPlainText -Force } else { ConvertTo-SecureString -String $pwd -AsPlainText -Force }
            Import-PfxCertificate -FilePath $pfxPath -Password $secure -CertStoreLocation 'Cert:\LocalMachine\My' -Exportable | Out-Null
        }
    }
    @{ artifact_ref = $thumb; detail = 'RDS full deployment selected certificate thumbprint.' }
}
function Invoke-RdsFullBind { param([hashtable]$Context)
    $rdcb = Get-RdsConnectionBroker -Context $Context
    Set-RdsRoles -Thumbprint ([string]$Context.artifact_ref) -ConnectionBroker $rdcb
    @{ success = $true; detail = 'RDS role certificates updated.' }
}
function Invoke-RdsFullActivate { param([hashtable]$Context)
    foreach ($svc in @('TSGateway','Tssdis')) {
        if (Get-Service -Name $svc -ErrorAction SilentlyContinue) { Restart-Service -Name $svc -Force }
    }
    @{ success = $true; detail = 'RDS gateway-related services restarted.' }
}
function Invoke-RdsFullVerify { param([hashtable]$Context)
    $rdcb = Get-RdsConnectionBroker -Context $Context
    $expected = ([string]$Context.artifact_ref).ToUpperInvariant()
    $roles = @('RDGateway','RDWebAccess','RDRedirector','RDPublishing')
    $ok = $true
    foreach ($role in $roles) {
        $bound = Get-RDCertificate -Role $role -ConnectionBroker $rdcb -ErrorAction Stop
        if ([string]$bound.Thumbprint -ne $expected) { $ok = $false; break }
    }
    @{ verified = $ok; detail = 'RDS role thumbprints validated.' }
}
function Invoke-RdsFullRollback { param([hashtable]$Context)
    $old = Get-RdsFullThumbprint -Context $Context -UsePrevious
    $rdcb = Get-RdsConnectionBroker -Context $Context
    Set-RdsRoles -Thumbprint $old -ConnectionBroker $rdcb
    @{ success = $true; detail = 'RDS full rollback applied.' }
}

Export-ModuleMember -Function Invoke-RdsFullProbe,Invoke-RdsFullDeploy,Invoke-RdsFullBind,Invoke-RdsFullActivate,Invoke-RdsFullVerify,Invoke-RdsFullRollback
