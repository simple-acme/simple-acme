Import-Module "$PSScriptRoot/../core/Native-Process.psm1" -Force

function Get-TestPowerShellExecutable {
    $candidates = @(
        (Join-Path $PSHOME 'powershell.exe'),
        (Join-Path $PSHOME 'powershell')
    )

    foreach ($candidate in $candidates) {
        if ([System.IO.Path]::IsPathRooted($candidate) -and (Test-Path -LiteralPath $candidate)) {
            return (Convert-Path -LiteralPath $candidate)
        }
    }

    $resolved = Get-Command -Name 'powershell' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $resolved) {
        throw 'Unable to locate a PowerShell executable for Native-Process tests.'
    }

    return $resolved.Source
}

Describe 'Native process invocation compatibility' {
    BeforeAll {
        $script:exe = Get-TestPowerShellExecutable
        $script:repoRoot = Split-Path $PSScriptRoot -Parent
    }

    It 'runs wacs.exe --version when bundled executable is present' {
        $wacs = Join-Path $script:repoRoot 'wacs.exe'
        if (-not (Test-Path -LiteralPath $wacs)) {
            $true | Should -BeTrue
            return
        }

        $result = Invoke-NativeProcess -FilePath (Convert-Path -LiteralPath $wacs) -ArgumentList @('--version') -TimeoutSeconds 30
        $result.Succeeded | Should -BeTrue
        $result.OutputLines.Count | Should -BeGreaterThan 0
    }

    It 'preserves arguments containing spaces' {
        $scriptPath = Join-Path $TestDrive 'emit.ps1'
        @'
param([string]$Value)
Write-Output "VALUE=$Value"
'@ | Set-Content -LiteralPath $scriptPath -Encoding UTF8

        $result = Invoke-NativeProcess -FilePath $script:exe -ArgumentList @('-NoProfile', '-File', $scriptPath, '-Value', 'hello spaced value')

        $result.Succeeded | Should -BeTrue
        $result.OutputLines | Should -Contain 'VALUE=hello spaced value'
    }

    It 'preserves script path and token arguments with spaces and braces' {
        $scriptDir = Join-Path $TestDrive 'path with spaces'
        New-Item -ItemType Directory -Path $scriptDir -Force | Out-Null
        $scriptPath = Join-Path $scriptDir 'echo args.ps1'
        @'
param([string]$ScriptPathArg, [string]$Token)
Write-Output "SCRIPT=$ScriptPathArg"
Write-Output "TOKEN=$Token"
'@ | Set-Content -LiteralPath $scriptPath -Encoding UTF8

        $inputScriptPath = Join-Path $scriptDir 'inner script.ps1'
        $result = Invoke-NativeProcess -FilePath $script:exe -ArgumentList @('-NoProfile', '-File', $scriptPath, '-ScriptPathArg', $inputScriptPath, '-Token', '{CertThumbprint}')

        $result.Succeeded | Should -BeTrue
        $result.OutputLines | Should -Contain "SCRIPT=$inputScriptPath"
        $result.OutputLines | Should -Contain 'TOKEN={CertThumbprint}'
    }

    It 'does not use ProcessStartInfo argument-list API in the module implementation' {
        $nativeProcessSource = Join-Path $script:repoRoot 'core/Native-Process.psm1'
        $raw = Get-Content -LiteralPath $nativeProcessSource -Raw
        $pattern = '\$psi\.' + 'ArgumentList'
        $raw | Should -Not -Match $pattern
    }
}
