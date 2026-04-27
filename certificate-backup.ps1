#Requires -Version 5.1

param(
    [Parameter(Mandatory)][string]$OutputPath,
    [SecureString]$Passphrase,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$cryptoModulePath = Join-Path $PSScriptRoot 'core/Crypto.psm1'
$cryptoModule = Import-Module $cryptoModulePath -Force -PassThru
Import-Module "$PSScriptRoot/core/Config-Store.psm1" -Force

$plainTextCommand = Get-Command 'ConvertTo-PlainText' -ErrorAction SilentlyContinue
if ($null -eq $plainTextCommand -or [string]::IsNullOrWhiteSpace([string]$plainTextCommand.Source) -or $plainTextCommand.Source -ne $cryptoModule.Name) {
    throw @"
Required command 'ConvertTo-PlainText' is unavailable after importing Crypto module.
Expected module path: $cryptoModulePath
Resolved module path: $($cryptoModule.Path)
Current script root: $PSScriptRoot
Re-deploy the setup modules and ensure you are running certificate-backup.ps1 from the correct repository root.
"@
}

function Read-EnvFileBestEffort {
    param([Parameter(Mandatory)][string]$Path)

    $values = @{}
    $warnings = New-Object System.Collections.Generic.List[string]
    if (-not (Test-Path -LiteralPath $Path)) {
        return @{ Values = $values; Warnings = @('certificate.env missing') }
    }

    $lineNo = 0
    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        $lineNo++
        if ([string]::IsNullOrWhiteSpace($line)) { continue }

        $trimStart = $line.TrimStart()
        if ($trimStart.StartsWith('#')) { continue }

        $idx = $line.IndexOf('=')
        if ($idx -lt 1) {
            $warnings.Add("certificate.env line $lineNo ignored (expected KEY=VALUE)")
            continue
        }

        $key = $line.Substring(0, $idx).Trim()
        if ([string]::IsNullOrWhiteSpace($key)) {
            $warnings.Add("certificate.env line $lineNo ignored (empty key)")
            continue
        }

        $value = $line.Substring($idx + 1)
        if ($value.Length -ge 2 -and $value.StartsWith('"') -and $value.EndsWith('"')) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        $values[$key] = $value
    }

    return @{ Values = $values; Warnings = @($warnings) }
}

function Add-BackupFileEntry {
    param(
        [Parameter(Mandatory)][System.Collections.Generic.List[object]]$Collection,
        [Parameter(Mandatory)][string]$Category,
        [Parameter(Mandatory)][string]$Path,
        [string]$Destination = ''
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }

    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    foreach ($existing in $Collection) {
        if ([string]$existing.path -eq $resolvedPath) {
            return $false
        }
    }

    $content = [System.IO.File]::ReadAllText($resolvedPath, [System.Text.Encoding]::UTF8)
    $entry = [ordered]@{
        category = $Category
        path = $resolvedPath
        destination = if ([string]::IsNullOrWhiteSpace($Destination)) { '' } else { $Destination }
        content = $content
    }
    $Collection.Add([pscustomobject]$entry)
    return $true
}

$p1 = $null
$p2 = $null

try {
    if ((Test-Path -LiteralPath $OutputPath) -and -not $Force) { throw "Output path already exists: $OutputPath. Use -Force to overwrite." }

    if (-not $Passphrase) {
        $p1 = Read-Host -AsSecureString -Prompt 'Enter backup passphrase (store this securely - required for restore):'
        $p2 = Read-Host -AsSecureString -Prompt 'Confirm backup passphrase'
        $s1 = & "$($cryptoModule.Name)\ConvertTo-PlainText" -SecureString $p1
        $s2 = & "$($cryptoModule.Name)\ConvertTo-PlainText" -SecureString $p2
        if ($s1 -ne $s2) { Write-Error 'Passphrase confirmation did not match.'; exit 1 }
        $Passphrase = $p1
    }

    $ProjectRoot = $PSScriptRoot
    $warnings = New-Object System.Collections.Generic.List[string]
    $files = New-Object System.Collections.Generic.List[object]

    $envPath = Join-Path $ProjectRoot 'certificate.env'
    $parsedEnv = Read-EnvFileBestEffort -Path $envPath
    foreach ($warning in $parsedEnv.Warnings) { $warnings.Add([string]$warning) }
    if (Test-Path -LiteralPath $envPath) {
        [void](Add-BackupFileEntry -Collection $files -Category 'bootstrap' -Path $envPath -Destination 'project/certificate.env')
    }

    $envValues = @{}
    foreach ($key in @('ACME_DIRECTORY','ACME_KID','ACME_HMAC_SECRET','DOMAINS','CERTIFICATE_CONFIG_DIR','CERTIFICATE_DROP_DIR','CERTIFICATE_STATE_DIR','CERTIFICATE_LOG_DIR','CERTIFICATE_API_KEY','ACME_DATA_DIR')) {
        $envValues[$key] = [Environment]::GetEnvironmentVariable($key)
    }
    foreach ($key in $parsedEnv.Values.Keys) {
        if ([string]::IsNullOrWhiteSpace([string]$envValues[$key])) {
            $envValues[$key] = [string]$parsedEnv.Values[$key]
        }
    }

    foreach ($secretKey in @('ACME_KID','ACME_HMAC_SECRET','CERTIFICATE_API_KEY')) {
        if ([string]::IsNullOrWhiteSpace([string]$envValues[$secretKey])) {
            $warnings.Add("$secretKey missing or empty")
        }
    }

    $simpleAcmeDir = if (-not [string]::IsNullOrWhiteSpace([string]$envValues.ACME_DATA_DIR)) { [string]$envValues.ACME_DATA_DIR } else { Join-Path $env:ProgramData 'simple-acme' }
    $simpleAcmeStateFound = $false
    $renewalCount = 0
    if (Test-Path -LiteralPath $simpleAcmeDir -PathType Container) {
        if (Add-BackupFileEntry -Collection $files -Category 'simple_acme' -Path (Join-Path $simpleAcmeDir 'settings.json') -Destination 'simple-acme/settings.json') {
            $simpleAcmeStateFound = $true
        }

        $renewalFiles = @(Get-ChildItem -LiteralPath $simpleAcmeDir -Filter '*.renewal.json' -File -ErrorAction SilentlyContinue)
        foreach ($renewalFile in $renewalFiles) {
            if (Add-BackupFileEntry -Collection $files -Category 'simple_acme' -Path $renewalFile.FullName -Destination (Join-Path 'simple-acme' $renewalFile.Name)) {
                $simpleAcmeStateFound = $true
                $renewalCount++
            }
        }

        foreach ($extraName in @('accounts.json','settings_default.json','list.txt')) {
            if (Add-BackupFileEntry -Collection $files -Category 'simple_acme' -Path (Join-Path $simpleAcmeDir $extraName) -Destination (Join-Path 'simple-acme' $extraName)) {
                $simpleAcmeStateFound = $true
            }
        }
    } else {
        $warnings.Add('simple-acme data directory not found')
    }

    if ($renewalCount -eq 0) {
        $warnings.Add('no renewal json files found')
    }

    $projectPatterns = @(
        'Scripts/*.ps1',
        'core/*.psm1',
        'setup/*.ps1',
        'setup/*.psm1',
        'certificate-*.ps1',
        'config.ps1'
    )

    foreach ($pattern in $projectPatterns) {
        foreach ($f in @(Get-Item -Path (Join-Path $ProjectRoot $pattern) -ErrorAction SilentlyContinue)) {
            if ($f -is [System.IO.FileInfo]) {
                [void](Add-BackupFileEntry -Collection $files -Category 'project' -Path $f.FullName -Destination ([System.IO.Path]::GetRelativePath($ProjectRoot, $f.FullName).Replace('\\','/')))
            }
        }
    }

    $configDirCandidate = ''
    if (-not [string]::IsNullOrWhiteSpace([string]$envValues.CERTIFICATE_CONFIG_DIR)) {
        $configDirCandidate = [string]$envValues.CERTIFICATE_CONFIG_DIR
    } elseif (Test-Path -LiteralPath (Join-Path $ProjectRoot 'config') -PathType Container) {
        $configDirCandidate = Join-Path $ProjectRoot 'config'
    }

    $phase2Found = $false
    if (-not [string]::IsNullOrWhiteSpace($configDirCandidate) -and (Test-Path -LiteralPath $configDirCandidate -PathType Container)) {
        foreach ($optionalName in @('env.secure','credentials.sec','mappings.json','mapping.json','policies.json')) {
            if (Add-BackupFileEntry -Collection $files -Category 'phase2' -Path (Join-Path $configDirCandidate $optionalName) -Destination (Join-Path 'config' $optionalName)) {
                $phase2Found = $true
            }
        }

        $deviceDir = Join-Path $configDirCandidate 'devices'
        if (Test-Path -LiteralPath $deviceDir -PathType Container) {
            foreach ($deviceFile in @(Get-ChildItem -LiteralPath $deviceDir -File -ErrorAction SilentlyContinue)) {
                if (Add-BackupFileEntry -Collection $files -Category 'phase2' -Path $deviceFile.FullName -Destination (Join-Path 'config/devices' $deviceFile.Name)) {
                    $phase2Found = $true
                }
            }
        }

        foreach ($metadataDir in @('drop','state','logs')) {
            $metaPath = Join-Path $configDirCandidate $metadataDir
            if (Test-Path -LiteralPath $metaPath -PathType Container) {
                foreach ($metaFile in @(Get-ChildItem -LiteralPath $metaPath -File -ErrorAction SilentlyContinue)) {
                    if (Add-BackupFileEntry -Collection $files -Category 'metadata' -Path $metaFile.FullName -Destination (Join-Path "config/$metadataDir" $metaFile.Name)) {
                        $phase2Found = $true
                    }
                }
            }
        }
    }

    if (-not $phase2Found) {
        $warnings.Add('optional phase2 mappings not found')
    }

    $meaningfulFileCount = @($files | Where-Object { $_.category -in @('bootstrap','simple_acme','project','phase2') }).Count
    if ($meaningfulFileCount -eq 0 -and -not $simpleAcmeStateFound) {
        throw 'No meaningful files could be found to include in backup.'
    }

    $payload = [ordered]@{
        manifest = [ordered]@{
            created_at = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
            hostname = $env:COMPUTERNAME
            version = '1.2'
            contents = @('env','files','devices','policies','secure_config','mappings','renewals')
            warnings = @($warnings)
        }
        env = [ordered]@{
            ACME_DIRECTORY = [string]$envValues.ACME_DIRECTORY
            ACME_KID = [string]$envValues.ACME_KID
            ACME_HMAC_SECRET = [string]$envValues.ACME_HMAC_SECRET
            DOMAINS = [string]$envValues.DOMAINS
            CERTIFICATE_CONFIG_DIR = [string]$envValues.CERTIFICATE_CONFIG_DIR
            CERTIFICATE_DROP_DIR = [string]$envValues.CERTIFICATE_DROP_DIR
            CERTIFICATE_STATE_DIR = [string]$envValues.CERTIFICATE_STATE_DIR
            CERTIFICATE_LOG_DIR = [string]$envValues.CERTIFICATE_LOG_DIR
            CERTIFICATE_API_KEY = [string]$envValues.CERTIFICATE_API_KEY
            ACME_DATA_DIR = [string]$simpleAcmeDir
        }
        files = @($files)
        devices = @()
        policies = @()
        mappings = @()
        secure_config = @{ env_secure = ''; credentials_sec = '' }
        renewals = @()
    }

    $json = $payload | ConvertTo-Json -Depth 20
    $plainBytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $salt = Get-RandomBytes -Count 32
    $key = New-AesKeyFromPassphrase -Passphrase $Passphrase -Salt $salt
    $encrypted = Protect-AesValue -Plaintext $plainBytes -Key $key

    $outDir = Split-Path -Path $OutputPath -Parent
    if ($outDir -and -not (Test-Path -LiteralPath $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

    $tmpOutput = [System.IO.Path]::GetTempFileName()
    $fs = [System.IO.File]::Open($tmpOutput, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    try {
        $bw = New-Object System.IO.BinaryWriter($fs)
        $bw.Write([byte[]](0x43,0x45,0x52,0x54))
        $bw.Write([byte]0x01)
        $bw.Write($salt)
        $bw.Write($encrypted.IV)
        $bw.Write([uint32]$encrypted.Ciphertext.Length)
        $bw.Write($encrypted.Ciphertext)
        $bw.Flush()
    } finally { $fs.Dispose() }
    Move-Item -LiteralPath $tmpOutput -Destination $OutputPath -Force

    [Array]::Clear($key, 0, $key.Length)
    [Array]::Clear($plainBytes, 0, $plainBytes.Length)

    Write-Host ("Backup created: {0} bytes, files: {1}, warnings: {2}, timestamp: {3}" -f (Get-Item $OutputPath).Length, @($files).Count, @($warnings).Count, (Get-Date).ToUniversalTime().ToString('o'))
    exit 0
} catch {
    if ($_.Exception.Message -like '*Passphrase*') { exit 1 }
    Write-Error $_
    exit 2
} finally {
    if ($p1) { $p1.Dispose() }
    if ($p2) { $p2.Dispose() }
}
