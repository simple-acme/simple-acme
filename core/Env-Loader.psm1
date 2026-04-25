$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$script:RequiredEnvKeys = @(
    'ACME_DIRECTORY',
    'ACME_KID',
    'ACME_HMAC_SECRET',
    'DOMAINS',
    'ACME_SCRIPT_PATH',
    'CERTIFICATE_CONFIG_DIR',
    'CERTIFICATE_DROP_DIR',
    'CERTIFICATE_STATE_DIR',
    'CERTIFICATE_LOG_DIR',
    'CERTIFICATE_API_KEY'
)

$script:OptionalEnvDefaults = @{
    CERTIFICATE_VERIFY_MAX_ATTEMPTS = '3'
    CERTIFICATE_ACTIVATE_TIMEOUT_MS = '120000'
    CERTIFICATE_DEFAULT_FANOUT      = 'fail-fast'
    CERTIFICATE_SKIP_TLS_CHECK      = '0'
    CERTIFICATE_RETRY_MAX_ATTEMPTS  = '3'
    CERTIFICATE_RETRY_BACKOFF_MS    = '1000'
    CERTIFICATE_HTTP_ENABLED        = '0'
    CERTIFICATE_HTTP_PREFIX         = 'http://localhost:8443/'
    CERTIFICATE_DISABLE_ROLLBACK    = '0'
    CERTIFICATE_HTTP_HOST           = '127.0.0.1'
    CERTIFICATE_HTTP_PORT           = '8088'
}

function Resolve-EnvPath {
    param([string]$Path = '')

    if (-not [string]::IsNullOrWhiteSpace($Path) -and (Test-Path -LiteralPath $Path)) { return $Path }

    $fromEnv = [Environment]::GetEnvironmentVariable('CERTIFICATE_ENV_FILE')
    if (-not [string]::IsNullOrWhiteSpace($fromEnv) -and (Test-Path -LiteralPath $fromEnv)) { return $fromEnv }

    $cwdPath = Join-Path (Get-Location).Path 'certificate.env'
    if (Test-Path -LiteralPath $cwdPath) { return $cwdPath }

    $configDir = [Environment]::GetEnvironmentVariable('CERTIFICATE_CONFIG_DIR')
    if (-not [string]::IsNullOrWhiteSpace($configDir)) {
        $configPath = Join-Path $configDir 'certificate.env'
        if (Test-Path -LiteralPath $configPath) { return $configPath }
    }

    throw 'No certificate.env could be resolved. Set CERTIFICATE_ENV_FILE or create .\certificate.env.'
}

function Read-EnvFile {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) { throw "Env file not found: $Path" }

    $result = @{}
    $lineNo = 0
    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        $lineNo++
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $trimStart = $line.TrimStart()
        if ($trimStart.StartsWith('#')) { continue }

        $idx = $line.IndexOf('=')
        if ($idx -lt 1) { throw "Invalid .env line $lineNo in '$Path'. Expected KEY=VALUE format." }

        $key = $line.Substring(0, $idx).Trim()
        $value = $line.Substring($idx + 1)
        if ($value.Length -ge 2 -and $value.StartsWith('"') -and $value.EndsWith('"')) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        if ($result.ContainsKey($key)) { throw "Duplicate key '$key' found at line $lineNo in '$Path'." }
        $result[$key] = $value
    }

    return $result
}

function Import-EnvFile {
    param([string]$Path = '', [switch]$Force)

    $resolved = Resolve-EnvPath -Path $Path
    $values = Read-EnvFile -Path $resolved

    $missing = @($script:RequiredEnvKeys | Where-Object { -not $values.ContainsKey($_) -or [string]::IsNullOrWhiteSpace([string]$values[$_]) })
    if ($missing.Count -gt 0) {
        throw "Missing required environment keys in '$resolved': $($missing -join ', ')"
    }

    foreach ($key in $script:OptionalEnvDefaults.Keys) {
        if (-not $values.ContainsKey($key)) { $values[$key] = $script:OptionalEnvDefaults[$key] }
    }

    foreach ($key in $values.Keys) {
        $existing = [Environment]::GetEnvironmentVariable($key)
        if (-not $Force -and -not [string]::IsNullOrWhiteSpace($existing)) {
            Write-Warning "Skipping existing env var '$key' because -Force was not specified."
            continue
        }
        [Environment]::SetEnvironmentVariable($key, [string]$values[$key])
    }

    return $values
}

function Set-EnvFileAcl {
    param([Parameter(Mandatory)][string]$Path)

    if (-not ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT)) { return }

    $acl = New-Object System.Security.AccessControl.FileSecurity
    $acl.SetAccessRuleProtection($true, $false)

    $systemAccount = New-Object System.Security.Principal.NTAccount('SYSTEM')
    $currentAccount = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name

    $fullControl = [System.Security.AccessControl.FileSystemRights]::FullControl
    $inheritFlags = [System.Security.AccessControl.InheritanceFlags]::None
    $propagationFlags = [System.Security.AccessControl.PropagationFlags]::None
    $allow = [System.Security.AccessControl.AccessControlType]::Allow

    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($systemAccount, $fullControl, $inheritFlags, $propagationFlags, $allow)))
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($currentAccount, $fullControl, $inheritFlags, $propagationFlags, $allow)))

    [System.IO.File]::SetAccessControl($Path, $acl)
}

function Write-EnvFile {
    param(
        [Parameter(Mandatory)][hashtable]$Values,
        [Parameter(Mandatory)][string]$Path,
        [string]$Header = '# Certificate configuration - generated by certificate-setup.ps1'
    )

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add($Header)
    $lines.Add('# NOTE: ACME_KID and ACME_HMAC_SECRET are plaintext in this file; protect with NTFS ACLs.')
    $lines.Add('')

    foreach ($key in ($Values.Keys | Sort-Object)) {
        $value = [string]$Values[$key]
        if ($value.Contains('=') -or $value.Contains('#')) {
            $escaped = '"{0}"' -f $value.Replace('"', '""')
            $lines.Add("$key=$escaped")
        } else {
            $lines.Add("$key=$value")
        }
    }

    $directory = Split-Path -Path $Path -Parent
    if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $tmpPath = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllLines($tmpPath, $lines, [System.Text.Encoding]::UTF8)
    Move-Item -LiteralPath $tmpPath -Destination $Path -Force
    Set-EnvFileAcl -Path $Path
}

Export-ModuleMember -Function @('Read-EnvFile','Import-EnvFile','Write-EnvFile')
