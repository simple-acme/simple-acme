Set-StrictMode -Version Latest
Import-Module "$PSScriptRoot/State-Store.psm1" -Force
Import-Module "$PSScriptRoot/Retry-Engine.psm1" -Force
Import-Module "$PSScriptRoot/Rollback-Engine.psm1" -Force
Import-Module "$PSScriptRoot/Logger.psm1" -Force
. "$PSScriptRoot/Types.ps1"

function Invoke-ConnectorJob {
    param([hashtable]$Job,[hashtable]$Event,[hashtable]$Config,[string]$StateDir)

    $steps = @('Probe','Deploy','Bind','Activate','Verify')
    foreach ($step in $steps) {
        $stepLower = $step.ToLowerInvariant()
        Write-CertificaatLog -Level 'INFO' -Message "Starting step '$stepLower'." -JobId $Job.job_id -Domain $Event.domain -Step $stepLower
        Update-ConnectorJobStep -JobId $Job.job_id -Step $stepLower -Status 'running' -StateDir $StateDir -Attempt 0 | Out-Null

        $context = @{
            job_id                = $Job.job_id
            event                 = $Event
            config                = $Config
            artifact_ref          = (Get-ConnectorJob -JobId $Job.job_id -StateDir $StateDir).artifact_ref
            previous_artifact_ref = (Get-ConnectorJob -JobId $Job.job_id -StateDir $StateDir).previous_artifact_ref
        }
        Assert-ConnectorContext -Context $context

        try {
            $result = Invoke-WithRetry -Label "$($Job.connector_type)-$stepLower" -ScriptBlock {
                $connectorFn = (($Job.connector_type -split "[_-]") | ForEach-Object { if ($_.Length -gt 0) { $_.Substring(0,1).ToUpper()+$_.Substring(1) } }) -join ""
                $fn = "Invoke-${connectorFn}$step"
                & $fn -Context $context
            }

            if ($stepLower -eq 'deploy') {
                $artifact = $result.artifact_ref
                Update-ConnectorJobStep -JobId $Job.job_id -Step $stepLower -Status 'running' -StateDir $StateDir -ArtifactRef $artifact -PreviousArtifactRef $artifact | Out-Null
            }

            if ($stepLower -eq 'verify') {
                if (-not $result.verified) { throw "Verification returned false for connector '$($Job.connector_type)'" }
                Update-ConnectorJobStep -JobId $Job.job_id -Step $stepLower -Status 'succeeded' -StateDir $StateDir | Out-Null
                Write-CertificaatLog -Level 'INFO' -Message 'Job succeeded.' -JobId $Job.job_id -Domain $Event.domain -Step $stepLower
                return Get-ConnectorJob -JobId $Job.job_id -StateDir $StateDir
            }
            Write-CertificaatLog -Level 'INFO' -Message "Completed step '$stepLower'." -JobId $Job.job_id -Domain $Event.domain -Step $stepLower
        } catch {
            Update-ConnectorJobStep -JobId $Job.job_id -Step $stepLower -Status 'failed' -StateDir $StateDir -ErrorDetail $_.Exception.Message | Out-Null
            $reason = switch ($stepLower) {
                'verify' { 'VerifyExhausted' }
                'activate' { 'ActivateTimeout' }
                'deploy' { 'DeployInvalid' }
                default { 'FanoutFastFail' }
            }

            if (Test-ShouldRollback -Reason $reason) {
                $fresh = Get-ConnectorJob -JobId $Job.job_id -StateDir $StateDir
                $rollbackContext = @{
                    job_id                = $fresh.job_id
                    event                 = $Event
                    config                = $Config
                    artifact_ref          = $fresh.artifact_ref
                    previous_artifact_ref = $fresh.previous_artifact_ref
                }
                Invoke-ConnectorRollback -Context $rollbackContext -ConnectorType $connectorFn -StateDir $StateDir
            }
            return Get-ConnectorJob -JobId $Job.job_id -StateDir $StateDir
        }
    }
}

function Start-ConnectorBackgroundJob {
    param([hashtable]$Job,[hashtable]$Event,[hashtable]$Config,[string]$StateDir,[string]$ModulePath)

    $jobData = @{ Job = $Job; Event = $Event; Config = $Config; StateDir = $StateDir; ModulePath = $ModulePath }
    Start-Job -ArgumentList $jobData -ScriptBlock {
        param($data)
        Import-Module $data.ModulePath -Force
        Invoke-ConnectorJob -Job $data.Job -Event $data.Event -Config $data.Config -StateDir $data.StateDir
    }
}

function Invoke-FanoutRunner {
    param([hashtable]$Event,[hashtable]$Policy,[string]$StateDir)

    Assert-CertificateEvent -Event $Event
    Assert-DeploymentPolicy -Policy $Policy

    $modulePath = Join-Path $PSScriptRoot 'Fanout-Runner.psm1'
    $jobs = @()
    foreach ($connector in $Policy.connectors) {
        $config = ConvertTo-Hashtable -InputObject $connector
        Assert-ConnectorConfig -Config $config
        $new = New-ConnectorJob -RenewalId $Event.renewal_id -DeploymentPolicyId $Policy.policy_id -ConnectorType $config.connector_type -StateDir $StateDir
        $jobs += ,@{ job = $new; config = $config }

        $connectorFile = Join-Path (Split-Path $PSScriptRoot -Parent) "connectors/$($config.connector_type.Replace('_','-')).psm1"
        Import-Module $connectorFile -Force
    }

    $fanout = if ([string]::IsNullOrWhiteSpace($Policy.fanout_policy)) { 'fail-fast' } else { $Policy.fanout_policy }

    if ($fanout -eq 'fail-fast') {
        $succeeded = @()
        foreach ($entry in $jobs) {
            $result = Invoke-ConnectorJob -Job $entry.job -Event $Event -Config $entry.config -StateDir $StateDir
            if ($result.status -eq 'succeeded') {
                $succeeded += ,@{ job = $result; config = $entry.config }
                continue
            }

            foreach ($prior in $succeeded) {
                $ctx = @{
                    job_id                = $prior.job.job_id
                    event                 = $Event
                    config                = $prior.config
                    artifact_ref          = $prior.job.artifact_ref
                    previous_artifact_ref = $prior.job.previous_artifact_ref
                }
                Invoke-ConnectorRollback -Context $ctx -ConnectorType ((($prior.job.connector_type -split "[_-]") | ForEach-Object { if ($_.Length -gt 0) { $_.Substring(0,1).ToUpper()+$_.Substring(1) } }) -join "") -StateDir $StateDir
            }
            break
        }
        return
    }

    $bg = @()
    foreach ($entry in $jobs) {
        $bg += ,(Start-ConnectorBackgroundJob -Job $entry.job -Event $Event -Config $entry.config -StateDir $StateDir -ModulePath $modulePath)
    }
    Wait-Job -Job $bg | Out-Null
    $null = $bg | Receive-Job
    $bg | Remove-Job -Force

    $all = Get-ConnectorJobsByRenewal -RenewalId $Event.renewal_id -StateDir $StateDir
    $succeeded = @($all | Where-Object { $_.status -eq 'succeeded' })

    if ($fanout -eq 'best-effort') { return }

    if ($fanout -eq 'quorum') {
        if ($succeeded.Count -lt [int]$Policy.quorum_threshold) {
            foreach ($job in $succeeded) {
                $cfg = $jobs | Where-Object { $_.job.job_id -eq $job.job_id } | Select-Object -First 1
                $ctx = @{
                    job_id                = $job.job_id
                    event                 = $Event
                    config                = $cfg.config
                    artifact_ref          = $job.artifact_ref
                    previous_artifact_ref = $job.previous_artifact_ref
                }
                Invoke-ConnectorRollback -Context $ctx -ConnectorType ((($job.connector_type -split "[_-]") | ForEach-Object { if ($_.Length -gt 0) { $_.Substring(0,1).ToUpper()+$_.Substring(1) } }) -join "") -StateDir $StateDir
            }
        }
    }
}

Export-ModuleMember -Function Invoke-ConnectorJob,Invoke-FanoutRunner
