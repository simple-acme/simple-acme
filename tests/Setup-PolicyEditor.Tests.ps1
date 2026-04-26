Set-StrictMode -Version Latest

Import-Module "$PSScriptRoot/../setup/Form-Runner.psm1" -Force

Describe 'Setup policy editor safety and UX helpers' {
    It 'backup script has param block before strict mode to avoid startup parser errors' {
        $lines = Get-Content -Path "$PSScriptRoot/../certificate-backup.ps1"
        $firstExecutable = $lines |
            Where-Object {
                $t = $_.Trim()
                -not [string]::IsNullOrWhiteSpace($t) -and -not $t.StartsWith('#')
            } |
            Select-Object -First 1

        $firstExecutable | Should -Be 'param('
    }

    It 'rejects invalid fanout values' {
        (Test-FanoutPolicyValue -FanoutPolicy 'invalid-mode') | Should -BeFalse
        (Test-FanoutPolicyValue -FanoutPolicy 'quorum') | Should -BeTrue
    }

    It 'requires quorum threshold and enforces numeric and range rules' {
        $missing = Test-QuorumThreshold -FanoutPolicy 'quorum' -ThresholdText '' -ConnectorCount 2
        $missing.IsValid | Should -BeFalse

        $nonNumeric = Test-QuorumThreshold -FanoutPolicy 'quorum' -ThresholdText 'abc' -ConnectorCount 2
        $nonNumeric.IsValid | Should -BeFalse

        $tooHigh = Test-QuorumThreshold -FanoutPolicy 'quorum' -ThresholdText '3' -ConnectorCount 2
        $tooHigh.IsValid | Should -BeFalse

        $valid = Test-QuorumThreshold -FanoutPolicy 'quorum' -ThresholdText '2' -ConnectorCount 2
        $valid.IsValid | Should -BeTrue
        $valid.Value | Should -Be 2
    }

    It 'routes edit/delete fail-safe to create flow when no policies exist' {
        $configDir = Join-Path $TestDrive 'config'
        New-Item -ItemType Directory -Path $configDir | Out-Null

        $answers = @('E', 'new-policy', 'quorum', '1', 'Q')
        $script:idx = 0
        Mock Read-Host {
            $value = $answers[$script:idx]
            $script:idx += 1
            return $value
        }

        $result = Invoke-PolicyEditor -ConfigDir $configDir
        @($result).Count | Should -Be 1
        $result[0].policy_id | Should -Be 'new-policy'
        $result[0].fanout_policy | Should -Be 'quorum'
    }

    It 'shows empty and populated policy views' {
        $writer = New-Object System.IO.StringWriter
        $originalOut = [Console]::Out
        try {
            [Console]::SetOut($writer)
            Show-PoliciesView -Policies @()

            $nonEmpty = @(
                [pscustomobject]@{ policy_id='p1'; fanout_policy='best-effort'; quorum_threshold=$null; connectors=@() },
                [pscustomobject]@{ policy_id='p2'; fanout_policy='quorum'; quorum_threshold=2; connectors=@([pscustomobject]@{ connector_type='iis'; label='IIS' }) }
            )
            Show-PoliciesView -Policies $nonEmpty
        } finally {
            [Console]::SetOut($originalOut)
        }

        $output = $writer.ToString()
        $output | Should -Match 'No deployment policies exist yet'
        $output | Should -Match 'policy_id=p1'
        $output | Should -Match 'policy_id=p2'
        $output | Should -Match 'connectors=1'
    }
}
