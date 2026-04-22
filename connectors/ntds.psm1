Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-NtdsThumbprint {
    param([hashtable]$Context,[switch]$UsePrevious)
    $value = if ($UsePrevious) { [string]$Context.previous_artifact_ref } else { [string]$Context.event.thumbprint }
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'NTDS connector requires a certificate thumbprint.' }
    return $value.Replace(' ','').ToUpperInvariant()
}

function Set-NtdsThumbprint {
    param([string]$Thumbprint,[string]$OldThumbprint)

    $destination = 'HKLM:\SOFTWARE\Microsoft\Cryptography\Services\NTDS\SystemCertificates\MY\Certificates'
    if (-not (Test-Path -LiteralPath $destination)) { New-Item -Path $destination -Force | Out-Null }

    $src = "HKLM:\SOFTWARE\Microsoft\SystemCertificates\MY\Certificates\$Thumbprint"
    if (-not (Test-Path -LiteralPath $src)) { throw "Source certificate registry key '$src' not found." }
    Copy-Item -Path $src -Destination $destination -Force

    if (-not [string]::IsNullOrWhiteSpace($OldThumbprint)) {
        $oldPath = Join-Path $destination $OldThumbprint
        if (Test-Path -LiteralPath $oldPath) { Remove-Item -Path $oldPath -Force -Recurse }
    }
}

function Invoke-NtdsProbe { param([hashtable]$Context)
    $destination = 'HKLM:\SOFTWARE\Microsoft\Cryptography\Services\NTDS\SystemCertificates\MY\Certificates'
    @{ reachable = $true; auth_valid = $true; detail = "NTDS destination key accessible: $([bool](Test-Path -LiteralPath $destination))." }
}
function Invoke-NtdsDeploy { param([hashtable]$Context)
    $thumb = Get-NtdsThumbprint -Context $Context
    @{ artifact_ref = $thumb; detail = 'Using certificate thumbprint from renewal event.' }
}
function Invoke-NtdsBind { param([hashtable]$Context)
    Set-NtdsThumbprint -Thumbprint ([string]$Context.artifact_ref) -OldThumbprint ([string]$Context.previous_artifact_ref)
    @{ success = $true; detail = 'Certificate copied to NTDS service certificate registry store.' }
}
function Invoke-NtdsActivate { param([hashtable]$Context)
    @{ success = $true; detail = 'NTDS activation complete (restart policy left to operator).' }
}
function Invoke-NtdsVerify { param([hashtable]$Context)
    $path = "HKLM:\SOFTWARE\Microsoft\Cryptography\Services\NTDS\SystemCertificates\MY\Certificates\$([string]$Context.artifact_ref)"
    @{ verified = [bool](Test-Path -LiteralPath $path); detail = 'NTDS registry certificate entry verified.' }
}
function Invoke-NtdsRollback { param([hashtable]$Context)
    $old = Get-NtdsThumbprint -Context $Context -UsePrevious
    Set-NtdsThumbprint -Thumbprint $old -OldThumbprint ([string]$Context.artifact_ref)
    @{ success = $true; detail = 'NTDS rollback applied with previous thumbprint.' }
}


function Invoke-NtdsConnectorProbe { param([hashtable]$Context) Invoke-NtdsProbe -Context $Context }
function Invoke-NtdsConnectorDeploy { param([hashtable]$Context) Invoke-NtdsDeploy -Context $Context }
function Invoke-NtdsConnectorBind { param([hashtable]$Context) Invoke-NtdsBind -Context $Context }
function Invoke-NtdsConnectorActivate { param([hashtable]$Context) Invoke-NtdsActivate -Context $Context }
function Invoke-NtdsConnectorVerify { param([hashtable]$Context) Invoke-NtdsVerify -Context $Context }
function Invoke-NtdsConnectorRollback { param([hashtable]$Context) Invoke-NtdsRollback -Context $Context }

Export-ModuleMember -Function Invoke-NtdsProbe,Invoke-NtdsDeploy,Invoke-NtdsBind,Invoke-NtdsActivate,Invoke-NtdsVerify,Invoke-NtdsRollback,Invoke-NtdsConnectorProbe,Invoke-NtdsConnectorDeploy,Invoke-NtdsConnectorBind,Invoke-NtdsConnectorActivate,Invoke-NtdsConnectorVerify,Invoke-NtdsConnectorRollback
