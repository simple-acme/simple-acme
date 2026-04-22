Set-StrictMode -Version Latest

function Invoke-TestFanoutRunner {
    param([scriptblock]$Assert)

    & $Assert 'fanout runner module imports' {
        Import-Module "$PSScriptRoot/../core/Fanout-Runner.psm1" -Force
        (Get-Command Invoke-FanoutRunner -ErrorAction Stop) | Out-Null
    }

    & $Assert 'retry engine defaults can be resolved from environment' {
        [Environment]::SetEnvironmentVariable('CERTIFICAAT_RETRY_MAX_ATTEMPTS', '2')
        [Environment]::SetEnvironmentVariable('CERTIFICAAT_RETRY_BACKOFF_MS', '1')
        Import-Module "$PSScriptRoot/../core/Retry-Engine.psm1" -Force
        $count = 0
        try {
            Invoke-WithRetry -Label 'test' -ScriptBlock { $script:count++; throw 'x' }
        } catch { }
        if ($script:count -ne 2) { throw "expected 2 attempts, got $script:count" }
    }
}
