param(
    [Parameter(Position=0, Mandatory=$true)]
    [string]$NewCertThumbprint
)

# Copy from WebHosting to LocalMachine\My if not already there
$certInMy = Get-ChildItem -Path Cert:\LocalMachine\My | 
            Where-Object { $_.Thumbprint -eq $NewCertThumbprint }

if (-not $certInMy) {
    $cert = Get-ChildItem -Path Cert:\LocalMachine\WebHosting | 
            Where-Object { $_.Thumbprint -eq $NewCertThumbprint }
    
    if (-not $cert) {
        "Certificate $NewCertThumbprint not found in WebHosting or My store"
        exit 1
    }

    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store("My","LocalMachine")
    $store.Open("ReadWrite")
    $store.Add($cert)
    $store.Close()
    "Certificate copied to LocalMachine\My"
}

Set-RDCertificate -Role RDGateway    -Thumbprint $NewCertThumbprint -Force
Set-RDCertificate -Role RDWebAccess  -Thumbprint $NewCertThumbprint -Force
Set-RDCertificate -Role RDPublishing -Thumbprint $NewCertThumbprint -Force
Set-RDCertificate -Role RDRedirector -Thumbprint $NewCertThumbprint -Force

"All RDS roles updated with thumbprint $NewCertThumbprint"