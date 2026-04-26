Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot
try {
    $tracked = @(git ls-files)
    $privateKeyPatterns = @('*.pfx', '*.p12', '*.key')

    $allowList = @(
        'src/main.test/Tests/CsrPluginTests/working.pfx',
        'src/main.test/Tests/CsrPluginTests/original.pfx',
        'src/main.test/Tests/CsrPluginTests/messedup.pfx'
    )

    $violations = @()
    foreach ($pattern in $privateKeyPatterns) {
        $matches = @($tracked | Where-Object { $_ -like $pattern })
        foreach ($m in $matches) {
            if ($allowList -notcontains $m) {
                $violations += $m
            }
        }
    }

    if ($violations.Count -gt 0) {
        Write-Host '[FAIL] Tracked private-key artifacts detected:' -ForegroundColor Red
        $violations | Sort-Object -Unique | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
        throw 'Remove private-key artifacts from git and store them in secure secret storage.'
    }

    Write-Host '[PASS] No tracked private-key artifacts detected.' -ForegroundColor Green
} finally {
    Pop-Location
}
