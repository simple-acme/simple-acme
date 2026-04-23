Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$testFiles = @(Get-ChildItem -Path $PSScriptRoot -Filter '*.Tests.ps1' | Sort-Object Name)
$pass = 0
$fail = 0
$skip = 0

function Invoke-Assertion {
    param([string]$Name,[scriptblock]$Body)
    try {
        & $Body
        $script:pass++
        Write-Host "[PASS] $Name"
    } catch {
        $script:fail++
        Write-Host "[FAIL] $Name :: $($_.Exception.Message)"
    }
}

foreach ($file in $testFiles) {
    try {
        $raw = Get-Content -LiteralPath $file.FullName -Raw
        $looksLikePester = $raw -match '(?m)^\s*(Describe|Context|It)\b'
        $hasDescribe = $null -ne (Get-Command -Name 'Describe' -CommandType Function -ErrorAction SilentlyContinue)
        if ($looksLikePester -and -not $hasDescribe) {
            $skip++
            Write-Host "[SKIP] $($file.Name) :: Pester syntax detected but Pester is unavailable in this environment."
            continue
        }

        . $file.FullName
        $fns = @(Get-Command -Name 'Invoke-Test*' -CommandType Function | Where-Object { $_.ScriptBlock.File -eq $file.FullName })
        if ($fns.Count -eq 0) {
            $skip++
            Write-Host "[SKIP] $($file.Name) :: no Invoke-Test* function found (legacy/Pester style)."
            continue
        }
        foreach ($fn in $fns) {
            & $fn.Name -Assert ${function:Invoke-Assertion}
        }
    } catch {
        $fail++
        Write-Host "[FAIL] $($file.Name) :: $($_.Exception.Message)"
    }
}

Write-Host ("Summary: pass={0} fail={1} skip={2}" -f $pass, $fail, $skip)
if ($fail -gt 0) { exit 1 }
exit 0
