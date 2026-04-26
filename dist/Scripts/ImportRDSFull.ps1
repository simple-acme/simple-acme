  <#
.SYNOPSIS
Imports a cert from WACS renewal into the RD Gateway, RD Listener, RD WebAccess, RD Redirector and RD Connection Broker

.DESCRIPTION
Note that this script is intended to be run via the install script plugin from win-acme via the batch script wrapper. As such, we use positional parameters to avoid issues with using a dash in the cmd line. 

Proper information should be available here

https://github.com/PKISharp/win-acme/wiki/Install-Script

or more generally, here

https://github.com/PKISharp/win-acme/wiki/Example-Scripts

.PARAMETER NewCertThumbprint
The exact thumbprint of the cert to be imported. The script will copy this cert to the Personal store if not already there.

.PARAMETER RDCB
This parameter specifies the Remote Desktop Connection Broker (RD Connection Broker) server for a Remote Desktop deployment.

If you don't specify a value, the script uses the local computer's fully qualified domain name (FQDN).

.PARAMETER OldCertThumbprint
The exact thumbprint of the cert to be replaced. The script will delete this cert from the Personal store of the RD Connection Broker upon successful completion.

If you don't specify this value and the RD Connection Broker is not the local machine, the replaced cert will remain in the store.

.EXAMPLE 

ImportRDS.ps1 <certThumbprint> <ConnectionBroker.contoso.com> <oldCertThumbprint>

.NOTES
The private key of the letsencrypt certificate needs to be exportable. Set "PrivateKeyExportable" in settings.json to true.

In order for this script to update the cert on a remote RD Connection Broker, PowerShell on the RD Connection Broker needs to be configured to receive remote commands and the scheduled task needs to be configured to run with highest privileges as a domain user who is an admin on both the machine running the update and the RD Connection Broker.

#>

param(
    [Parameter(Position=0,Mandatory=$true)]
    [string]$NewCertThumbprint,
    [Parameter(Position=1,Mandatory=$false)]
    [string]$RDCB,
    [Parameter(Position=2,Mandatory=$false)]
    [string]$OldCertThumbprint

)
$LocalHost = (Get-WmiObject win32_computersystem).DNSHostName+"."+(Get-WmiObject win32_computersystem).Domain
if (-not $PSBoundParameters.ContainsKey('RDCB')) {$RDCB = (Get-WmiObject win32_computersystem).DNSHostName+"."+(Get-WmiObject win32_computersystem).Domain} 
function Restart-TSGatewayService {
    Stop-Service TSGateway -Force -ErrorAction Stop
    $retry = 0
    do {
        Start-Sleep -Seconds $retry
        Start-Service TSGateway -ErrorAction SilentlyContinue
        $service = Get-Service TSGateway
        $retry++
    } while ($service.Status -ne 'Running' -and $retry -lt 5)
    Start-Service TSGateway -ErrorAction Stop
}

function Get-CurrentRdsGatewayThumbprint {
    $value = (Get-Item -Path RDS:\GatewayServer\SSLCertificate\Thumbprint -ErrorAction Stop).Value
    return ([string]$value).Trim().ToUpperInvariant()
}

function Get-RdsRoleThumbprint {
    param(
        [Parameter(Mandatory=$true)]
        [ValidateSet('RDGateway','RDWebAccess','RDPublishing','RDRedirector')]
        [string]$Role,
        [Parameter(Mandatory=$false)]
        [string]$ConnectionBroker
    )

    $queryArgs = @{ Role = $Role; ErrorAction = 'Stop' }
    if (-not [string]::IsNullOrWhiteSpace($ConnectionBroker)) {
        $queryArgs.ConnectionBroker = $ConnectionBroker
    }

    $entry = Get-RDCertificate @queryArgs
    return ([string]$entry.Thumbprint).Trim().ToUpperInvariant()
}

$ErrorActionPreference = 'Stop'
$RDCBPS = $null

try {
    $gatewayService = Get-Service -Name TSGateway -ErrorAction Stop
    if (-not $gatewayService) {
        throw 'RDS role validation failed: TSGateway service not found.'
    }

    if ($RDCB -ne $LocalHost) { $RDCBPS = New-PSSession -ComputerName $RDCB }
    if ($RDCB -ne $LocalHost) {
        Invoke-Command -Session $RDCBPS { Import-Module RemoteDesktopServices -ErrorAction Stop }
    }
    Import-Module RemoteDesktopServices -ErrorAction Stop

    $null = Get-Item -Path RDS:\GatewayServer\SSLCertificate\Thumbprint -ErrorAction Stop

    $CertInStore = Get-ChildItem -Path Cert:\LocalMachine\My -Recurse | Where-Object { $_.Thumbprint -eq $NewCertThumbprint } | Select-Object -First 1
    if (-not $CertInStore) {
        throw "Cert thumbprint '$NewCertThumbprint' not found in Cert:\LocalMachine\My."
    }
    if (-not $CertInStore.HasPrivateKey) {
        throw "Certificate '$NewCertThumbprint' does not contain a private key."
    }

    $currentThumbprint = Get-CurrentRdsGatewayThumbprint
    $newThumbprint = ([string]$CertInStore.Thumbprint).Trim().ToUpperInvariant()
    $roles = @('RDGateway','RDWebAccess','RDPublishing','RDRedirector')
    $allRolesMatch = $true
    foreach ($role in $roles) {
        $existingRoleThumbprint = Get-RdsRoleThumbprint -Role $role -ConnectionBroker $RDCB
        if ($existingRoleThumbprint -ne $newThumbprint) {
            $allRolesMatch = $false
            break
        }
    }

    if ($currentThumbprint -eq $newThumbprint -and $allRolesMatch) {
        "RDS binding already uses thumbprint $newThumbprint. No changes required."
        exit 0
    }

    wmic /namespace:\\root\cimv2\TerminalServices PATH Win32_TSGeneralSetting Set SSLCertificateSHA1Hash="$($CertInStore.Thumbprint)"
    "Cert thumbprint set to RDP listener"

    Set-RDCertificate -Role RDGateway -Thumbprint $newThumbprint -ConnectionBroker $RDCB -Force -ErrorAction Stop
    Set-RDCertificate -Role RDWebAccess -Thumbprint $newThumbprint -ConnectionBroker $RDCB -Force -ErrorAction Stop
    Set-RDCertificate -Role RDPublishing -Thumbprint $newThumbprint -ConnectionBroker $RDCB -Force -ErrorAction Stop
    Set-RDCertificate -Role RDRedirector -Thumbprint $newThumbprint -ConnectionBroker $RDCB -Force -ErrorAction Stop
    "Certificates applied to all RDS roles"

    if ((Get-Command -Module RDWebClientManagement | Measure-Object).Count -eq 0) {
        "RDWebClient not installed, skipping"
    } else {
        Remove-RDWebClientBrokerCert -ErrorAction SilentlyContinue
        $rdWebImportCommand = Get-Command -Name Import-RDWebClientBrokerCert -ErrorAction Stop
        if ($rdWebImportCommand.Parameters.ContainsKey('CertificateThumbprint')) {
            Import-RDWebClientBrokerCert -CertificateThumbprint $newThumbprint -ErrorAction Stop
        } else {
            Add-Type -AssemblyName 'System.Web'
            $tempPasswordPfx = [System.Web.Security.Membership]::GeneratePassword(10, 5) | ConvertTo-SecureString -Force -AsPlainText
            $tempPfxPath = New-TemporaryFile | Rename-Item -PassThru -NewName { $_.name -Replace '\.tmp$','.pfx' }
            (Export-PfxCertificate -Cert $CertInStore -FilePath $tempPfxPath -Force -NoProperties -Password $tempPasswordPfx) | Out-Null
            Import-RDWebClientBrokerCert -Path $tempPfxPath -Password $tempPasswordPfx -ErrorAction Stop
            Remove-Item -Path $tempPfxPath -ErrorAction SilentlyContinue
        }
        "RDWebClient Certificate for RDS was set"
    }

    Set-Item -Path RDS:\GatewayServer\SSLCertificate\Thumbprint -Value $newThumbprint -ErrorAction Stop
    Restart-TSGatewayService
    try {
        & iisreset | Out-Null
    } catch {
        Write-Warning "iisreset encountered an issue: $($_.Exception.Message)"
    }
    "Services restarted"

    $appliedGatewayThumbprint = Get-CurrentRdsGatewayThumbprint
    if ($appliedGatewayThumbprint -ne $newThumbprint) {
        throw "Post-deploy validation failed: RD Gateway thumbprint is '$appliedGatewayThumbprint', expected '$newThumbprint'."
    }
    foreach ($role in $roles) {
        $appliedRoleThumbprint = Get-RdsRoleThumbprint -Role $role -ConnectionBroker $RDCB
        if ($appliedRoleThumbprint -ne $newThumbprint) {
            throw "Post-deploy validation failed: role '$role' thumbprint is '$appliedRoleThumbprint', expected '$newThumbprint'."
        }
    }
    $tsGatewayService = Get-Service TSGateway -ErrorAction Stop
    if ($tsGatewayService.Status -ne 'Running') {
        throw "Post-deploy validation failed: TSGateway service state is '$($tsGatewayService.Status)'."
    }
    "Post-deploy validation passed: new cert is bound and active."

    if ($RDCB -ne $LocalHost -and $PSBoundParameters.ContainsKey('OldCertThumbprint')) {
        $RemoteCert = Invoke-Command -Session $RDCBPS { Get-ChildItem -Path Cert:\LocalMachine\My -Recurse | Where-Object { $_.thumbprint -eq $Using:NewCertThumbprint } }
        if ($RemoteCert -and $RemoteCert.thumbprint -ne $OldCertThumbprint) {
            Invoke-Command -Session $RDCBPS { Get-ChildItem -Path Cert:\LocalMachine\My -Recurse | Where-Object { $_.thumbprint -eq $Using:OldCertThumbprint } | Remove-Item }
        } else {
            "Remote cert not changed, skipping deletion."
        }
    }

    exit 0
} catch {
    Write-Error "RDS deployment failed: $($_.Exception.Message)"
    exit 1
} finally {
    if ($RDCBPS) {
        Remove-PSSession $RDCBPS
    }
}
