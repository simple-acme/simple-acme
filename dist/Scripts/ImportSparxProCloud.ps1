<#
.SYNOPSIS
Imports a cert from WACS renewal into SPARX ProCloud Server.

.DESCRIPTION
Note that this script is intended to be run via the install script plugin from win-acme via the batch script wrapper. As such, we use positional parameters to avoid issues with using a dash in the cmd line. 

Proper information should be available here

https://www.win-acme.com/reference/plugins/installation/script

or more generally, here

https://github.com/win-acme/win-acme/tree/master/dist/Scripts

.PARAMETER NewCertThumbprint
The exact thumbprint of the cert to be imported.

.PARAMETER PemFile
The output PEM file for Server Cert+PrivKey, usually C:\Program Files\Sparx Systems\Pro Cloud Server\Service\server.pem

.PARAMETER CaFile
The output PEM file for CA Cert, usually C:\Program Files\Sparx Systems\Pro Cloud Server\Service\cacert.pem

.NOTES
Requires Powershell7 installed (invokes itself using interpreter pwsh.exe).
PS7 is based on .Net Core and has this method: $cert.PrivateKey.ExportPkcs8PrivateKey()

#>

param(
	[Parameter(Position=0,Mandatory=$true)]
    [string]
	$NewCertThumbprint,
	
	[Parameter(Position=1,Mandatory=$true)]
	[string]
	$PemFile,
	
	[Parameter(Position=2,Mandatory=$true)]
	[string]
	$CaFile
)


# Check current PowerShell version
$psVersion = $PSVersionTable.PSVersion

# Check if we are using PowerShell older than 7
if ($psVersion.Major -lt 7) {
    Write-Host "PowerShell <7 detected. Restarting with pwsh.exe..."

    # Get the script path
    $scriptPath = $MyInvocation.MyCommand.Path

    # Execute the script using pwsh.exe (PowerShell Core), passing the 3 positional parameters
    & "pwsh.exe" -File "$scriptPath" "$NewCertThumbprint" "$PemFile" "$CaFile"
    exit
}


$cert = Get-ChildItem -Path Cert:\LocalMachine -Recurse | Where-Object { $_.HasPrivateKey } | Where-Object { $_.thumbprint -eq $NewCertThumbprint } | Sort-Object -Descending | Select-Object -f 1

if ($null -eq $cert) {
    Write-Host "No valid certificate with a private key found. Exiting..."
    exit 1
}

# Retrieve the certificate chain
$chain = New-Object System.Security.Cryptography.X509Certificates.X509Chain
$chain.Build($cert)

if ($chain.ChainElements.Count -lt 2) {
    Write-Host "No CA certificate found in the chain. Exiting..."
    exit 1
}

# Extract the CA certificate (Issuer Certificate)
$caCert = $chain.ChainElements[1].Certificate  # The second certificate in the chain is the CA

Write-Host "Found CA Certificate: $($caCert.Subject)"

# Convert certificates to PEM format
$certPem = "-----BEGIN CERTIFICATE-----`n" + [Convert]::ToBase64String($cert.RawData, 'InsertLineBreaks') + "`n-----END CERTIFICATE-----"
$caCertPem = "-----BEGIN CERTIFICATE-----`n" + [Convert]::ToBase64String($caCert.RawData, 'InsertLineBreaks') + "`n-----END CERTIFICATE-----"

# Save the CA certificate separately
$caCertPem | Set-Content -Path $CaFile -Encoding Ascii
Write-Host "CA Certificate saved to: $CaFile"

# Export the private key to PKCS#8 format
# We use the ExportPkcs8PrivateKey method from .NET Core (unavailable in old Powershell)
$privateKeyBytes = $cert.PrivateKey.ExportPkcs8PrivateKey()
$privateKeyPem = "-----BEGIN PRIVATE KEY-----`n" + [Convert]::ToBase64String($privateKeyBytes, 'InsertLineBreaks') + "`n-----END PRIVATE KEY-----"

# Write to PEM file (both Cert and Key)
$certPem + "`n`n" + $privateKeyPem | Set-Content -Path $PemFile -Encoding Ascii
Write-Host "PEM file created successfully: $PemFile"

# Restart service
Restart-Service 'Sparx Systems Professional Cloud' -Force -ErrorAction Stop
"Sparx Systems Professional Cloud - service restarted."
