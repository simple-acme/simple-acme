<#
.SYNOPSIS
Phase-1 native simple-acme flow for RDS Gateway certificate lifecycle.

.DESCRIPTION
- Uses simple-acme directly (CLI).
- Requests/renews certificate.
- Exports and imports PFX.
- Applies Set-RDCertificate for RDGateway, RDWebAccess, RDRedirector and RDPublishing.
- Verifies role bindings.
- Idempotent: only applies bindings when thumbprint changed.

.NOTES
Run elevated. Requires RemoteDesktop module/cmdlets on the host.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AcmeDirectory,

    [Parameter(Mandatory = $true)]
    [string]$Subdomain,

    [Parameter(Mandatory = $false)]
    [string]$Relation = "default",

    [Parameter(Mandatory = $false)]
    [string]$Kid,

    [Parameter(Mandatory = $false)]
    [string]$Hmac,

    [Parameter(Mandatory = $false)]
    [string]$RenewalId,

    [Parameter(Mandatory = $false)]
    [string]$WacsPath = ".\wacs.exe",

    [Parameter(Mandatory = $false)]
    [string]$ConnectionBroker = $env:COMPUTERNAME,

    [Parameter(Mandatory = $false)]
    [string]$PfxPath = ".\rdsgw-latest.pfx",

    [Parameter(Mandatory = $false)]
    [SecureString]$PfxPassword,

    [Parameter(Mandatory = $false)]
    [int]$MinValidDays = 14
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host "[certificaat-rdsgw] $Message"
}

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Ensure-PfxPassword {
    if ($PfxPassword) {
        return $PfxPassword
    }
    Add-Type -AssemblyName System.Web
    $plain = [System.Web.Security.Membership]::GeneratePassword(32, 8)
    return (ConvertTo-SecureString -String $plain -AsPlainText -Force)
}

function Invoke-SimpleAcme {
    if (-not (Test-Path -LiteralPath $WacsPath)) {
        throw "wacs executable not found at: $WacsPath"
    }

    $args = @(
        "--baseuri", $AcmeDirectory,
        "--accepttos",
        "--closeonfinish"
    )

    if ($RenewalId) {
        $args += @("--renew", "--id", $RenewalId)
    }
    else {
        $args += @(
            "--source", "manual",
            "--host", $Subdomain,
            "--validation", "selfhosting",
            "--store", "certificatestore",
            "--certificatestore", "My",
            "--installation", "none",
            "--friendlyname", "RDSGW-$Subdomain",
            "--account", $Relation
        )
    }

    if ($Kid -and $Hmac) {
        $args += @(
            "--eab-key-identifier", $Kid,
            "--eab-key", $Hmac
        )
    }

    Write-Step "Requesting/renewing certificate via simple-acme"
    $proc = Start-Process -FilePath $WacsPath -ArgumentList $args -Wait -NoNewWindow -PassThru
    if ($proc.ExitCode -ne 0) {
        throw "simple-acme exited with code $($proc.ExitCode)"
    }
}

function Get-LatestCertificate {
    param([string]$DnsName)

    $candidates = Get-ChildItem Cert:\LocalMachine\My |
        Where-Object {
            $_.HasPrivateKey -and (
                $_.Subject -like "*CN=$DnsName*" -or
                ($_.DnsNameList.Unicode -contains $DnsName)
            )
        } |
        Sort-Object NotAfter -Descending

    if (-not $candidates) {
        throw "No matching certificate found in LocalMachine\\My for $DnsName"
    }

    return $candidates[0]
}

function Export-And-ImportPfx {
    param(
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [SecureString]$Password
    )

    Write-Step "Exporting PFX to $PfxPath"
    Export-PfxCertificate -Cert $Certificate -FilePath $PfxPath -Password $Password -Force | Out-Null

    Write-Step "Importing PFX to LocalMachine\\My"
    Import-PfxCertificate -FilePath $PfxPath -Password $Password -CertStoreLocation Cert:\LocalMachine\My -Exportable | Out-Null
}

function Get-RoleThumbprint {
    param([string]$Role)
    try {
        $current = Get-RDCertificate -Role $Role -ConnectionBroker $ConnectionBroker -ErrorAction Stop
        return $current.Thumbprint
    }
    catch {
        return $null
    }
}

function Set-RolesIfNeeded {
    param(
        [string]$Thumbprint,
        [SecureString]$Password
    )

    $roles = @("RDGateway", "RDWebAccess", "RDRedirector", "RDPublishing")
    $changed = $false

    foreach ($role in $roles) {
        $currentThumb = Get-RoleThumbprint -Role $role
        if ($currentThumb -and $currentThumb.ToUpperInvariant() -eq $Thumbprint.ToUpperInvariant()) {
            Write-Step "Role $role already uses thumbprint $Thumbprint (idempotent no-op)"
            continue
        }

        Write-Step "Applying certificate to role $role"
        Set-RDCertificate -Role $role -ImportPath $PfxPath -Password $Password -ConnectionBroker $ConnectionBroker -Force
        $changed = $true
    }

    return $changed
}

function Restart-GatewayServicesIfNeeded {
    param([bool]$BindingsChanged)

    if (-not $BindingsChanged) {
        Write-Step "No binding changes detected, skipping service restart"
        return
    }

    $services = @("TSGateway", "Tssdis")
    foreach ($svc in $services) {
        $obj = Get-Service -Name $svc -ErrorAction SilentlyContinue
        if ($null -ne $obj) {
            Write-Step "Restarting service $svc"
            Restart-Service -Name $svc -Force -ErrorAction Stop
        }
    }
}

function Verify-State {
    param([string]$ExpectedThumbprint)

    $roles = @("RDGateway", "RDWebAccess", "RDRedirector", "RDPublishing")
    foreach ($role in $roles) {
        $bound = Get-RDCertificate -Role $role -ConnectionBroker $ConnectionBroker
        if ($bound.Thumbprint.ToUpperInvariant() -ne $ExpectedThumbprint.ToUpperInvariant()) {
            throw "Verification failed for $role. Expected $ExpectedThumbprint but found $($bound.Thumbprint)"
        }
    }

    $cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object Thumbprint -eq $ExpectedThumbprint | Select-Object -First 1
    if (-not $cert) {
        throw "Verification failed: certificate not present in LocalMachine\\My"
    }

    $days = ($cert.NotAfter - (Get-Date)).TotalDays
    if ($days -lt $MinValidDays) {
        throw "Verification failed: certificate validity is only $([math]::Round($days, 2)) days"
    }

    Write-Step "Verification passed: thumbprint $ExpectedThumbprint, valid until $($cert.NotAfter)"
}

try {
    if (-not (Test-Admin)) {
        throw "Administrator privileges are required"
    }

    Import-Module RemoteDesktop -ErrorAction Stop
    $pfxPass = Ensure-PfxPassword

    Invoke-SimpleAcme
    $latest = Get-LatestCertificate -DnsName $Subdomain

    Export-And-ImportPfx -Certificate $latest -Password $pfxPass
    $changed = Set-RolesIfNeeded -Thumbprint $latest.Thumbprint -Password $pfxPass
    Restart-GatewayServicesIfNeeded -BindingsChanged:$changed
    Verify-State -ExpectedThumbprint $latest.Thumbprint

    Write-Step "Completed successfully"
    exit 0
}
catch {
    Write-Error $_
    exit 1
}
finally {
    if (Test-Path -LiteralPath $PfxPath) {
        Remove-Item -LiteralPath $PfxPath -Force -ErrorAction SilentlyContinue
    }
}
