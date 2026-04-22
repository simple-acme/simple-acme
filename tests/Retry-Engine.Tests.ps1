Import-Module "$PSScriptRoot/../core/Retry-Engine.psm1" -Force

Describe 'Retry-Engine' {
    It 'succeeds on first attempt' {
        $calls = 0
        $result = Invoke-WithRetry -ScriptBlock { $script:calls++; 'ok' } -MaxAttempts 3 -BackoffMs 10
        $result | Should -Be 'ok'
        $calls | Should -Be 1
    }

    It 'succeeds on third attempt' {
        $calls = 0
        $result = Invoke-WithRetry -ScriptBlock { $script:calls++; if ($script:calls -lt 3) { throw 'x' }; 'ok' } -MaxAttempts 3 -BackoffMs 1
        $result | Should -Be 'ok'
        $calls | Should -Be 3
    }

    It 'throws after max attempts' {
        $calls = 0
        { Invoke-WithRetry -ScriptBlock { $script:calls++; throw 'nope' } -MaxAttempts 2 -BackoffMs 1 } | Should -Throw
        $calls | Should -Be 2
    }
}
