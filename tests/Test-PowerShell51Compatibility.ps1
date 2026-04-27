#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent

function Assert-NoParseErrors {
    param([Parameter(Mandatory)][string]$Path)

    $tokens = $null
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$errors)
    if ($errors -and $errors.Count -gt 0) {
        $messages = @($errors | ForEach-Object { $_.Message }) -join '; '
        throw "Parser errors in '$Path': $messages"
    }
}

function Assert-NoForbiddenConstructs {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Content
    )

    $forbiddenPatterns = @(
        [pscustomobject]@{ Pattern = ('\.Arg' + 'umentList'); AllowIn = @() },
        [pscustomobject]@{ Pattern = ('ConvertFrom-Json\s+-As' + 'Hashtable'); AllowIn = @() },
        [pscustomobject]@{ Pattern = ('ForEach-Object\s+-Par' + 'allel'); AllowIn = @() },
        [pscustomobject]@{ Pattern = ('Start-' + 'ThreadJob'); AllowIn = @() },
        [pscustomobject]@{ Pattern = ('\b' + 'pw' + 'sh' + '\b'); AllowIn = @('build/compile-local.ps1') },
        [pscustomobject]@{ Pattern = '\?\?'; AllowIn = @() }
    )

    $normalizedPath = $Path.Replace('\','/')
    foreach ($entry in $forbiddenPatterns) {
        if ($Content -match $entry.Pattern) {
            $allowed = $false
            foreach ($allowedPath in $entry.AllowIn) {
                if ($normalizedPath.EndsWith($allowedPath)) {
                    $allowed = $true
                    break
                }
            }

            if (-not $allowed) {
                throw "Forbidden construct '$($entry.Pattern)' found in '$Path'"
            }
        }
    }
}


Write-Host '[compat] Parsing all .ps1/.psm1 files for Windows PowerShell 5.1 syntax compatibility.'
$psFiles = @(Get-ChildItem -Path $repoRoot -Recurse -Include *.ps1,*.psm1 -File)
foreach ($file in $psFiles) {
    Assert-NoParseErrors -Path $file.FullName
    $raw = Get-Content -LiteralPath $file.FullName -Raw
    Assert-NoForbiddenConstructs -Path $file.FullName -Content $raw
}


Write-Host '[compat] Verifying no fragile Export-ModuleMember inline arrays remain in runtime modules.'
$moduleFiles = Get-ChildItem -Path $repoRoot -Filter '*.psm1' -Recurse
foreach ($file in $moduleFiles) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    if ($text -match 'Export-ModuleMember\s+-Function\s+@\s*\(') {
        throw "Forbidden fragile Export-ModuleMember inline array remains in $($file.FullName)"
    }
}

Write-Host '[compat] Importing core modules using PowerShell 5.1-compatible syntax.'
$coreModules = @(
    (Join-Path $repoRoot 'core/Native-Process.psm1'),
    (Join-Path $repoRoot 'core/Simple-Acme-Reconciler.psm1')
)
foreach ($modulePath in $coreModules) {
    Import-Module $modulePath -Force
}

Write-Host '[compat] Running Invoke-NativeProcess with a harmless command.'
if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) {
    $cmdExe = Join-Path $env:SystemRoot 'System32\cmd.exe'
    $result = Invoke-NativeProcess -FilePath $cmdExe -ArgumentList @('/c','echo','compat ok with spaces') -TimeoutSeconds 10
    if (-not $result.Succeeded) {
        throw 'Invoke-NativeProcess failed for cmd.exe compatibility check.'
    }
    if (-not (@($result.OutputLines | Where-Object { $_ -match 'compat ok with spaces' }).Count -gt 0)) {
        throw 'Invoke-NativeProcess output did not contain expected marker.'
    }
} else {
    Write-Host '[compat] Non-Windows environment detected; skipping cmd.exe execution check.'
}

Write-Host '[compat] Verifying bundled wacs resolver preference does not require PATH.'
$wacsPath = Join-Path $repoRoot 'wacs.exe'
$backupPath = Join-Path $env:TEMP ('wacs-backup-' + [Guid]::NewGuid().ToString('N') + '.exe')
$hadOriginal = Test-Path -LiteralPath $wacsPath
if ($hadOriginal) {
    Copy-Item -LiteralPath $wacsPath -Destination $backupPath -Force
}

try {
    'stub' | Set-Content -LiteralPath $wacsPath -Encoding ASCII
    $resolved = Resolve-WacsExecutable -EnvValues @{}
    $expected = Convert-Path -LiteralPath $wacsPath
    if ($resolved -ne $expected) {
        throw "Expected resolver to return bundled executable path '$expected' but got '$resolved'."
    }
} finally {
    Remove-Item -LiteralPath $wacsPath -Force -ErrorAction SilentlyContinue
    if ($hadOriginal) {
        Move-Item -LiteralPath $backupPath -Destination $wacsPath -Force
    } elseif (Test-Path -LiteralPath $backupPath) {
        Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
    }
}

Write-Host '[compat] Windows PowerShell 5.1 compatibility checks passed.'

Write-Host '[compat] Running phase-1 functional guard checks.'
Import-Module (Join-Path $repoRoot 'setup/Form-Runner.psm1') -Force
Import-Module (Join-Path $repoRoot 'core/Env-Loader.psm1') -Force

$tempRoot = Join-Path $env:TEMP ('simple-acme-compat-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
$tempEnv = Join-Path $tempRoot 'certificate.env'

try {
    if (Test-Path -LiteralPath $tempEnv) { Remove-Item -LiteralPath $tempEnv -Force }
    Write-EnvFile -Values @{ DOMAINS='remote.example.nl'; ACME_DIRECTORY='https://acme-v02.api.letsencrypt.org/directory'; ACME_SCRIPT_PATH=(Join-Path $repoRoot 'Scripts/cert2rds.ps1'); ACME_SCRIPT_PARAMETERS='{CertThumbprint}' } -Path $tempEnv
    $loaded = Import-EnvFile -Path $tempEnv -AllowIncomplete -Force
    if (-not $loaded.ContainsKey('ACME_WACS_PATH')) { throw 'Expected optional ACME_WACS_PATH default to be injected.' }

    $providerDv = Get-ProviderDefaults -Provider 'networking4all' -Networking4AllEnvironment 'test' -Networking4AllProduct 'dv'
    if ([string]$providerDv.ACME_DIRECTORY -ne 'https://test-acme.networking4all.com/dv') { throw 'Networking4All test dv URL construction failed.' }
    $providerDvSan = Get-ProviderDefaults -Provider 'networking4all' -Networking4AllEnvironment 'test' -Networking4AllProduct 'dv-san'
    if ([string]$providerDvSan.ACME_DIRECTORY -ne 'https://test-acme.networking4all.com/dv-san') { throw 'Networking4All test dv-san URL construction failed.' }
    $providerOvWild = Get-ProviderDefaults -Provider 'networking4all' -Networking4AllEnvironment 'production' -Networking4AllProduct 'ov-wildcard-san'
    if ([string]$providerOvWild.ACME_DIRECTORY -ne 'https://acme.networking4all.com/ov-wildcard-san') { throw 'Networking4All production ov-wildcard-san URL construction failed.' }
    if (-not [bool]$providerDv.RequiresEab) { throw 'Networking4All provider must require EAB.' }
    $expectedBootstrap = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'certificate.env'))
    $resolvedBootstrap = Resolve-BootstrapEnvPath -ProjectRoot $repoRoot
    if ($resolvedBootstrap -ne $expectedBootstrap) { throw "Resolve-BootstrapEnvPath default mismatch: '$resolvedBootstrap'" }

    $oldOverride = [Environment]::GetEnvironmentVariable('CERTIFICATE_ENV_FILE')
    try {
        $overridePath = Join-Path $tempRoot 'override.env'
        [Environment]::SetEnvironmentVariable('CERTIFICATE_ENV_FILE', $overridePath)
        $resolvedOverride = Resolve-BootstrapEnvPath -ProjectRoot $repoRoot
        if ($resolvedOverride -ne [System.IO.Path]::GetFullPath($overridePath)) {
            throw "Resolve-BootstrapEnvPath override mismatch: '$resolvedOverride'"
        }
    } finally {
        [Environment]::SetEnvironmentVariable('CERTIFICATE_ENV_FILE', $oldOverride)
    }

    $savedExpected = @{
        ACME_PROVIDER = 'networking4all'
        ACME_NETWORKING4ALL_ENVIRONMENT = 'test'
        ACME_NETWORKING4ALL_PRODUCT = 'dv'
        ACME_DIRECTORY = 'https://test-acme.networking4all.com/dv'
        ACME_REQUIRES_EAB = '1'
        DOMAINS = 'remote.example.nl'
    }
    Assert-SavedEnvMatchesSetup -Expected $savedExpected -Actual $savedExpected

    $pipeline = Get-GuidedPipelineTemplate -TargetSystem 'rds' -ValidationMode 'none'
    if ([string]$pipeline.ACME_SCRIPT_PARAMETERS -ne '{CertThumbprint}') { throw 'RDS script parameters must be {CertThumbprint}.' }
    if ([string]$pipeline.ACME_VALIDATION_MODE -ne 'none') { throw 'RDS validation mode must be none in guided phase-1 flow.' }

    $maskedKid = Mask-EnvDisplayValue -Name 'ACME_KID' -Value 'kid-value'
    $maskedSecret = Mask-EnvDisplayValue -Name 'ACME_HMAC_SECRET' -Value 'secret-value'
    if ($maskedKid -ne '<set>') { throw 'ACME_KID masking contract changed unexpectedly.' }
    if ($maskedSecret -ne '<hidden>') { throw 'ACME_HMAC_SECRET masking contract changed unexpectedly.' }

    $policyLines = @(Format-PolicySummaryLines -Policies @())
    if ($policyLines.Count -ne 0) { throw 'Empty policy list should return no summary rows.' }
} finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host '[compat] Functional checks passed.'

Write-Host '[compat] Running module import gate script.'
& (Join-Path $repoRoot 'tests/Test-ModuleImports.ps1')
