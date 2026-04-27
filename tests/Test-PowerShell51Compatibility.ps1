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
    $result = Invoke-NativeProcess -FilePath $cmdExe -ArgumentList @('/c','echo','compat-ok') -TimeoutSeconds 10
    if (-not $result.Succeeded) {
        throw 'Invoke-NativeProcess failed for cmd.exe compatibility check.'
    }
    if (-not ($result.OutputLines -contains 'compat-ok')) {
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
    $resolved = Resolve-WacsExecutablePath -EnvValues @{}
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
