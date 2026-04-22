param(
  [Parameter(Mandatory=$true)][string]$AcmeDirectory,
  [Parameter(Mandatory=$false)][string]$EabKid,
  [Parameter(Mandatory=$false)][string]$EabHmac,
  [Parameter(Mandatory=$true)][string]$Domain,
  [Parameter(Mandatory=$true)][string]$PfxPassword
)

$ErrorActionPreference = 'Stop'

function Log-Info([string]$Message) { Write-Output "[INFO] $Message" }
function Log-Warn([string]$Message) { Write-Output "[WARN] $Message" }
function Log-Error([string]$Message) { Write-Output "[ERROR] $Message" }

if (($EabKid -and -not $EabHmac) -or (-not $EabKid -and $EabHmac)) {
  Log-Error 'EAB parameters must be provided as a pair.'
  exit 2
}

$pfxPath = Join-Path $AcmeDirectory "$Domain.pfx"
if (-not (Test-Path $pfxPath)) {
  Log-Error "PFX not found at $pfxPath"
  exit 2
}

try {
  $secure = ConvertTo-SecureString -String $PfxPassword -AsPlainText -Force
  $imported = Import-PfxCertificate -FilePath $pfxPath -Password $secure -CertStoreLocation Cert:\LocalMachine\My
  $thumb = $imported.Thumbprint
  Log-Info "Imported certificate with thumbprint $thumb"
} catch {
  Log-Error "Failed to import PFX: $($_.Exception.Message)"
  exit 2
}

$roles = @('RDGateway', 'RDWebAccess', 'RDRedirector', 'RDPublishing')
$failed = @()
$changedGateway = $false
$needsChange = $false

foreach ($role in $roles) {
  try {
    $existing = Get-RDCertificate -Role $role
    if ($existing.Thumbprint -ne $thumb) {
      $needsChange = $true
    }
  } catch {
    $needsChange = $true
  }
}

if (-not $needsChange) {
  Log-Info 'All RDS roles already bound to active certificate. No changes made.'
  exit 0
}

foreach ($role in $roles) {
  try {
    Set-RDCertificate -Role $role -Thumbprint $thumb -Force
    $updated = Get-RDCertificate -Role $role
    if ($updated.Thumbprint -ne $thumb) {
      $failed += $role
      Log-Warn "Verification failed for role $role"
    } else {
      Log-Info "Role $role updated and verified"
      if ($role -eq 'RDGateway') { $changedGateway = $true }
    }
  } catch {
    $failed += $role
    Log-Error "Role $role update failed: $($_.Exception.Message)"
  }
}

if ($changedGateway) {
  try {
    Restart-Service -Name TSGateway -Force
    Log-Info 'Restarted TSGateway service after RDGateway certificate change'
  } catch {
    Log-Warn "Unable to restart TSGateway: $($_.Exception.Message)"
  }
}

if ($failed.Count -eq 0) {
  Log-Info 'All RDS roles successfully configured'
  exit 0
}

if ($failed.Count -lt $roles.Count) {
  Log-Warn ("Partial failure for roles: " + ($failed -join ', '))
  exit 1
}

Log-Error 'Complete failure: no role bindings succeeded'
exit 2
