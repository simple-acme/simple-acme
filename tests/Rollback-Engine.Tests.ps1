Import-Module "$PSScriptRoot/../core/State-Store.psm1" -Force
Import-Module "$PSScriptRoot/../core/Rollback-Engine.psm1" -Force

function Invoke-MockRollback { param([hashtable]$Context) @{ success = $true; detail = 'ok' } }

Describe 'Rollback-Engine' {
    It 'is idempotent for rolled_back' {
        $job = New-ConnectorJob -RenewalId 'r1' -DeploymentPolicyId 'p' -ConnectorType 'mock' -StateDir 'TestDrive:\state'
        Update-ConnectorJobStep -JobId $job.job_id -Step 'rollback' -Status 'rolled_back' -StateDir 'TestDrive:\state' | Out-Null
        { Invoke-ConnectorRollback -Context @{ job_id=$job.job_id; event=@{domain='d'}; config=@{}; previous_artifact_ref='x'; artifact_ref='x' } -ConnectorType 'Mock' -StateDir 'TestDrive:\state' } | Should -Not -Throw
    }
}
