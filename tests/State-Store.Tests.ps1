Import-Module "$PSScriptRoot/../core/State-Store.psm1" -Force

Describe 'State-Store' {
    It 'creates job with defaults' {
        $job = New-ConnectorJob -RenewalId 'r1' -DeploymentPolicyId 'p1' -ConnectorType 'kemp' -StateDir 'TestDrive:\state'
        $job.status | Should -Be 'pending'
        $job.step | Should -Be 'probe'
        $job.attempt | Should -Be 0
    }

    It 'uses atomic write with no tmp residue' {
        $job = New-ConnectorJob -RenewalId 'r2' -DeploymentPolicyId 'p1' -ConnectorType 'kemp' -StateDir 'TestDrive:\state'
        (Test-Path "TestDrive:\state\$($job.job_id).tmp") | Should -BeFalse
        (Test-Path "TestDrive:\state\$($job.job_id).json") | Should -BeTrue
    }

    It 'updates step and fields' {
        $job = New-ConnectorJob -RenewalId 'r3' -DeploymentPolicyId 'p1' -ConnectorType 'kemp' -StateDir 'TestDrive:\state'
        $updated = Update-ConnectorJobStep -JobId $job.job_id -Step 'deploy' -Status 'running' -StateDir 'TestDrive:\state' -Attempt 2 -ArtifactRef 'a1'
        $updated.step | Should -Be 'deploy'
        $updated.attempt | Should -Be 2
        $updated.artifact_ref | Should -Be 'a1'
    }

    It 'filters by renewal and pending' {
        $j1 = New-ConnectorJob -RenewalId 'r4' -DeploymentPolicyId 'p1' -ConnectorType 'kemp' -StateDir 'TestDrive:\state'
        $j2 = New-ConnectorJob -RenewalId 'other' -DeploymentPolicyId 'p1' -ConnectorType 'kemp' -StateDir 'TestDrive:\state'
        Update-ConnectorJobStep -JobId $j2.job_id -Step 'probe' -Status 'failed' -StateDir 'TestDrive:\state' | Out-Null
        (Get-ConnectorJobsByRenewal -RenewalId 'r4' -StateDir 'TestDrive:\state').Count | Should -Be 1
        (Get-PendingConnectorJobs -StateDir 'TestDrive:\state').Count | Should -Be 1
    }

    It 'returns null for missing job' {
        $x = Get-ConnectorJob -JobId 'missing' -StateDir 'TestDrive:\state'
        $null -eq $x | Should -BeTrue
    }
}
