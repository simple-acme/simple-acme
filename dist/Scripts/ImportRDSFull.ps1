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
    [Parameter(Position=3,Mandatory=$false)]
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

$ErrorActionPreference = 'Stop'
$RDCBPS = $null
$tempPfxPath = $null

try {
    $feature = Get-WindowsFeature -Name RDS-Gateway -ErrorAction Stop
    if (-not $feature.Installed) {
        throw 'RDS role validation failed: RDS-Gateway feature is not installed.'
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

    $currentThumbprint = Get-CurrentRdsGatewayThumbprint
    $newThumbprint = ([string]$CertInStore.Thumbprint).Trim().ToUpperInvariant()
    if ($currentThumbprint -eq $newThumbprint) {
        "RDS binding already uses thumbprint $newThumbprint. No changes required."
        exit 0
    }

    Set-Item -Path RDS:\GatewayServer\SSLCertificate\Thumbprint -Value $CertInStore.Thumbprint -ErrorAction Stop
    Restart-TSGatewayService
    "Cert thumbprint set to RD Gateway listener and service restarted"

    wmic /namespace:\\root\cimv2\TerminalServices PATH Win32_TSGeneralSetting Set SSLCertificateSHA1Hash="$($CertInStore.Thumbprint)"
    "Cert thumbprint set to RDP listener"

    Add-Type -AssemblyName 'System.Web'
    $tempPasswordPfx = [System.Web.Security.Membership]::GeneratePassword(10, 5) | ConvertTo-SecureString -Force -AsPlainText
    $tempPfxPath = New-TemporaryFile | Rename-Item -PassThru -NewName { $_.name -Replace '\.tmp$','.pfx' }
    (Export-PfxCertificate -Cert $CertInStore -FilePath $tempPfxPath -Force -NoProperties -Password $tempPasswordPfx) | Out-Null

    Set-RDCertificate -Role RDPublishing -ImportPath $tempPfxPath -Password $tempPasswordPfx -ConnectionBroker $RDCB -Force
    "RDPublishing Certificate for RDS was set"

    Set-RDCertificate -Role RDWebAccess -ImportPath $tempPfxPath -Password $tempPasswordPfx -ConnectionBroker $RDCB -Force
    "RDWebAccess Certificate for RDS was set"

    Set-RDCertificate -Role RDRedirector -ImportPath $tempPfxPath -Password $tempPasswordPfx -ConnectionBroker $RDCB -Force
    "RDRedirector Certificate for RDS was set"

    if ((Get-Command -Module RDWebClientManagement | Measure-Object).Count -eq 0) {
        "RDWebClient not installed, skipping"
    } else {
        Remove-RDWebClientBrokerCert
        Import-RDWebClientBrokerCert -Path $tempPfxPath -Password $tempPasswordPfx
        "RDWebClient Certificate for RDS was set"
    }

    Set-RDCertificate -Role RDGateway -ImportPath $tempPfxPath -Password $tempPasswordPfx -ConnectionBroker $RDCB -Force
    Restart-TSGatewayService
    "RDGateway Certificate for RDS was set"

    $appliedGatewayThumbprint = Get-CurrentRdsGatewayThumbprint
    if ($appliedGatewayThumbprint -ne $newThumbprint) {
        throw "Post-deploy validation failed: RD Gateway thumbprint is '$appliedGatewayThumbprint', expected '$newThumbprint'."
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
    if ($tempPfxPath -and (Test-Path -LiteralPath $tempPfxPath)) {
        Remove-Item -Path $tempPfxPath -ErrorAction SilentlyContinue
    }
    if ($RDCBPS) {
        Remove-PSSession $RDCBPS
    }
}
