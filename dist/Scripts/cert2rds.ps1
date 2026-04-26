param(
    [Parameter(Position = 0, Mandatory = $true)]
    [string]$NewCertThumbprint,

    [string]$ConnectionBroker,

    [string[]]$ComputerName,

    [switch]$ResetIis,

    [switch]$RestartGatewayService
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-IsAdministrator {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-SingleCertificateByThumbprint {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StorePath,

        [Parameter(Mandatory = $true)]
        [string]$Thumbprint
    )

    $matches = @(Get-ChildItem -Path $StorePath | Where-Object { $_.Thumbprint -eq $Thumbprint })

    if ($matches.Count -gt 1) {
        throw "Multiple certificates with thumbprint $Thumbprint found in $StorePath."
    }

    if ($matches.Count -eq 1) {
        return $matches[0]
    }

    return $null
}

function Invoke-CertDeployment {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Thumbprint,

        [string]$ConnectionBrokerName,

        [switch]$DoResetIis,

        [switch]$DoRestartGatewayService
    )

    Write-Output "Validating certificate thumbprint input."
    $normalizedThumbprint = ($Thumbprint -replace '\s', '').ToUpperInvariant()

    if ([string]::IsNullOrWhiteSpace($normalizedThumbprint)) {
        throw "Certificate thumbprint cannot be empty."
    }

    if ($normalizedThumbprint -notmatch '^[0-9A-F]+$') {
        throw "Certificate thumbprint must be hexadecimal."
    }

    if ($normalizedThumbprint.Length -ne 40) {
        throw "Certificate thumbprint must be 40 hexadecimal characters."
    }

    Write-Output "Checking certificate stores for thumbprint $normalizedThumbprint."
    $certInMy = Get-SingleCertificateByThumbprint -StorePath 'Cert:\LocalMachine\My' -Thumbprint $normalizedThumbprint

    if (-not $certInMy) {
        $certInWebHosting = Get-SingleCertificateByThumbprint -StorePath 'Cert:\LocalMachine\WebHosting' -Thumbprint $normalizedThumbprint

        if (-not $certInWebHosting) {
            throw "Certificate $normalizedThumbprint was not found in LocalMachine\\My or LocalMachine\\WebHosting."
        }

        Write-Output "Copying certificate from LocalMachine\\WebHosting to LocalMachine\\My."
        $store = New-Object System.Security.Cryptography.X509Certificates.X509Store("My", "LocalMachine")
        try {
            $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $store.Add($certInWebHosting)
        }
        finally {
            $store.Close()
        }

        $certInMy = Get-SingleCertificateByThumbprint -StorePath 'Cert:\LocalMachine\My' -Thumbprint $normalizedThumbprint
        if (-not $certInMy) {
            throw "Certificate copy verification failed for LocalMachine\\My."
        }

        Write-Output "Certificate copied to LocalMachine\\My."
    }
    else {
        Write-Output "Certificate already present in LocalMachine\\My."
    }

    $roles = @('RDGateway', 'RDWebAccess', 'RDPublishing', 'RDRedirector')

    foreach ($role in $roles) {
        $setParams = @{
            Role       = $role
            Thumbprint = $normalizedThumbprint
            Force      = $true
        }

        if (-not [string]::IsNullOrWhiteSpace($ConnectionBrokerName)) {
            $setParams.ConnectionBroker = $ConnectionBrokerName
        }

        Write-Output "Applying certificate to role $role."
        Set-RDCertificate @setParams
    }

    foreach ($role in $roles) {
        $getParams = @{ Role = $role }
        if (-not [string]::IsNullOrWhiteSpace($ConnectionBrokerName)) {
            $getParams.ConnectionBroker = $ConnectionBrokerName
        }

        $currentBinding = Get-RDCertificate @getParams
        $currentThumbprint = ($currentBinding.Thumbprint -replace '\s', '').ToUpperInvariant()

        if ($currentThumbprint -ne $normalizedThumbprint) {
            throw "Post-deployment verification failed for role $role. Expected $normalizedThumbprint but found $currentThumbprint."
        }

        Write-Output "Verified role $role is bound to thumbprint $normalizedThumbprint."
    }

    if ($DoResetIis) {
        Write-Output "Resetting IIS (RDWeb)."
        iisreset /noforce | Out-Null
        Write-Output "IIS reset completed."
    }

    if ($DoRestartGatewayService) {
        Write-Output "Restarting TSGateway service."
        Restart-Service -Name TSGateway -Force
        Write-Output "TSGateway service restarted."
    }

    Write-Output "All RDS roles successfully updated and verified with thumbprint $normalizedThumbprint."
}

try {
    if (-not (Test-IsAdministrator)) {
        throw "This script must be run as Administrator."
    }

    if ($ComputerName -and $ComputerName.Count -gt 0) {
        Write-Output "Remote execution mode enabled for $($ComputerName -join ', ')."
        $scriptBlock = {
            param(
                [string]$Thumbprint,
                [string]$ConnectionBrokerName,
                [bool]$DoResetIis,
                [bool]$DoRestartGatewayService
            )

            Set-StrictMode -Version Latest
            $ErrorActionPreference = "Stop"

            function Get-SingleCertificateByThumbprint {
                param(
                    [Parameter(Mandatory = $true)]
                    [string]$StorePath,
                    [Parameter(Mandatory = $true)]
                    [string]$Thumbprint
                )

                $matches = @(Get-ChildItem -Path $StorePath | Where-Object { $_.Thumbprint -eq $Thumbprint })
                if ($matches.Count -gt 1) {
                    throw "Multiple certificates with thumbprint $Thumbprint found in $StorePath."
                }
                if ($matches.Count -eq 1) {
                    return $matches[0]
                }
                return $null
            }

            $normalizedThumbprint = ($Thumbprint -replace '\s', '').ToUpperInvariant()
            if ([string]::IsNullOrWhiteSpace($normalizedThumbprint) -or $normalizedThumbprint -notmatch '^[0-9A-F]{40}$') {
                throw "Certificate thumbprint must be 40 hexadecimal characters."
            }

            $certInMy = Get-SingleCertificateByThumbprint -StorePath 'Cert:\LocalMachine\My' -Thumbprint $normalizedThumbprint
            if (-not $certInMy) {
                $certInWebHosting = Get-SingleCertificateByThumbprint -StorePath 'Cert:\LocalMachine\WebHosting' -Thumbprint $normalizedThumbprint
                if (-not $certInWebHosting) {
                    throw "Certificate $normalizedThumbprint was not found in LocalMachine\\My or LocalMachine\\WebHosting."
                }

                $store = New-Object System.Security.Cryptography.X509Certificates.X509Store("My", "LocalMachine")
                try {
                    $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
                    $store.Add($certInWebHosting)
                }
                finally {
                    $store.Close()
                }
            }

            $roles = @('RDGateway', 'RDWebAccess', 'RDPublishing', 'RDRedirector')
            foreach ($role in $roles) {
                $setParams = @{
                    Role       = $role
                    Thumbprint = $normalizedThumbprint
                    Force      = $true
                }
                if (-not [string]::IsNullOrWhiteSpace($ConnectionBrokerName)) {
                    $setParams.ConnectionBroker = $ConnectionBrokerName
                }
                Set-RDCertificate @setParams
            }

            foreach ($role in $roles) {
                $getParams = @{ Role = $role }
                if (-not [string]::IsNullOrWhiteSpace($ConnectionBrokerName)) {
                    $getParams.ConnectionBroker = $ConnectionBrokerName
                }
                $currentBinding = Get-RDCertificate @getParams
                $currentThumbprint = ($currentBinding.Thumbprint -replace '\s', '').ToUpperInvariant()
                if ($currentThumbprint -ne $normalizedThumbprint) {
                    throw "Post-deployment verification failed for role $role. Expected $normalizedThumbprint but found $currentThumbprint."
                }
            }

            if ($DoResetIis) {
                iisreset /noforce | Out-Null
            }

            if ($DoRestartGatewayService) {
                Restart-Service -Name TSGateway -Force
            }

            Write-Output "All RDS roles successfully updated and verified with thumbprint $normalizedThumbprint on $env:COMPUTERNAME."
        }

        foreach ($target in $ComputerName) {
            Write-Output "Starting deployment on remote computer $target."
            Invoke-Command -ComputerName $target -ScriptBlock $scriptBlock -ArgumentList $NewCertThumbprint, $ConnectionBroker, [bool]$ResetIis, [bool]$RestartGatewayService
            Write-Output "Completed deployment on remote computer $target."
        }
    }
    else {
        Invoke-CertDeployment -Thumbprint $NewCertThumbprint -ConnectionBrokerName $ConnectionBroker -DoResetIis:$ResetIis -DoRestartGatewayService:$RestartGatewayService
    }

    exit 0
}
catch {
    Write-Error "cert2rds deployment failed: $($_.Exception.Message)"
    exit 1
}
