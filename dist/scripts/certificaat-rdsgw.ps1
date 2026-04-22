param(
    [Parameter(Mandatory)][string]$Domain,
    [Parameter(Mandatory)][string]$PfxPath,
    [Parameter(Mandatory)][string]$PfxPassword,
    [string]$AcmeDirectory = '',
    [string]$EabKid = '',
    [string]$EabHmac = ''
)

$ErrorActionPreference = 'Stop'
$roles = @('RDGateway','RDWebAccess','RDRedirector','RDPublishing')

function Get-RoleThumbprint { param([string]$Role)
    try { (Get-RDCertificate -Role $Role -ErrorAction Stop).Thumbprint } catch { $null }
}

try {
    $securePassword = ConvertTo-SecureString $PfxPassword -AsPlainText -Force
    $incoming = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
    $incoming.Import($PfxPath, $PfxPassword, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::DefaultKeySet)

    $current = @{}
    foreach ($r in $roles) { $current[$r] = Get-RoleThumbprint -Role $r }
    if (($current.Values | Where-Object { $_ -ne $incoming.Thumbprint }).Count -eq 0) {
        Write-Output '[INFO] No change required'
        exit 0
    }

    $cert = Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation Cert:\LocalMachine\My -Password $securePassword
    Write-Output "[INFO] Imported cert thumbprint $($cert.Thumbprint)"

    $failures = @()
    $updatedGateway = $false
    foreach ($role in $roles) {
        try {
            Set-RDCertificate -Role $role -ImportPath $PfxPath -Password $securePassword -Force
            Write-Output "[INFO] Updated role $role"
            if ($role -eq 'RDGateway') { $updatedGateway = $true }
        } catch {
            $failures += $role
            Write-Output "[ERROR] Failed updating role $role: $($_.Exception.Message)"
        }
    }

    if ($updatedGateway) {
        try {
            Restart-Service TSGateway -ErrorAction Stop
            Write-Output '[INFO] Restarted TSGateway service'
        } catch {
            $failures += 'TSGateway'
            Write-Output "[ERROR] Failed restarting TSGateway: $($_.Exception.Message)"
        }
    }

    $verifyFailures = @()
    foreach ($role in $roles) {
        $thumb = Get-RoleThumbprint -Role $role
        if ($thumb -ne $cert.Thumbprint) {
            $verifyFailures += $role
            Write-Output "[WARN] Verification mismatch for role $role"
        } else {
            Write-Output "[INFO] Verification passed for role $role"
        }
    }

    if ($failures.Count -eq $roles.Count) { exit 2 }
    if ($failures.Count -gt 0 -or $verifyFailures.Count -gt 0) { exit 1 }
    exit 0
} catch {
    Write-Output "[ERROR] Complete failure: $($_.Exception.Message)"
    exit 2
}
