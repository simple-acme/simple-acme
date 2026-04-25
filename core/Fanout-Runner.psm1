#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module "$PSScriptRoot/State-Store.psm1" -Force
Import-Module "$PSScriptRoot/Retry-Engine.psm1" -Force
Import-Module "$PSScriptRoot/Rollback-Engine.psm1" -Force
Import-Module "$PSScriptRoot/Logger.psm1" -Force
. "$PSScriptRoot/Types.ps1"

function Get-ConnectorFnName {
    param([string]$ConnectorType,[string]$Step)
    $connectorFn = (($ConnectorType -split '[_-]') | ForEach-Object { if ($_.Length -gt 0) { $_.Substring(0,1).ToUpper()+$_.Substring(1) } }) -join ''
    "Invoke-${connectorFn}Connector$Step"
}

function Invoke-ConnectorJob {
    param([hashtable]$Job,[hashtable]$Event,[hashtable]$Config,[string]$StateDir,[string]$RootPath = $PSScriptRoot)

    $steps = @('Probe','Deploy','Bind','Activate','Verify')
    $rawMax = [Environment]::GetEnvironmentVariable('CERTIFICATE_VERIFY_MAX_ATTEMPTS')
    $maxAttempts = if ([string]::IsNullOrWhiteSpace($rawMax)) { 3 } else { [int]$rawMax }
    $rawTimeout = [Environment]::GetEnvironmentVariable('CERTIFICATE_ACTIVATE_TIMEOUT_MS')
    $activateTimeoutMs = if ([string]::IsNullOrWhiteSpace($rawTimeout)) { 1000 } else { [int]$rawTimeout }
    foreach ($step in $steps) {
        $stepLower = $step.ToLowerInvariant()
        Update-ConnectorJobStep -JobId $Job.job_id -Step $stepLower -Status 'running' -StateDir $StateDir -Attempt 0 | Out-Null
        $existing = Get-ConnectorJob -JobId $Job.job_id -StateDir $StateDir
        $context = @{ job_id=$Job.job_id; event=$Event; config=$Config; artifact_ref=$existing.artifact_ref; previous_artifact_ref=$existing.previous_artifact_ref }
        try {
            $fn = Get-ConnectorFnName -ConnectorType $Job.connector_type -Step $step
            $result = Invoke-WithRetry -Label "$($Job.connector_type)-$stepLower" -MaxAttempts $maxAttempts -BackoffMs $activateTimeoutMs -ScriptBlock { & $fn -Context $context }
            if ($stepLower -eq 'deploy') {
                $existing = Get-ConnectorJob -JobId $Job.job_id -StateDir $StateDir
                $previousArt = if (-not [string]::IsNullOrWhiteSpace($existing.artifact_ref)) { $existing.artifact_ref } else { $null }
                Update-ConnectorJobStep -JobId $Job.job_id -Step $stepLower -Status 'running' -StateDir $StateDir -ArtifactRef $result.artifact_ref -PreviousArtifactRef $previousArt | Out-Null
            }
            if ($stepLower -eq 'verify') {
                if (-not $result.verified) { throw "Verification returned false for connector '$($Job.connector_type)'" }
                Update-ConnectorJobStep -JobId $Job.job_id -Step $stepLower -Status 'succeeded' -StateDir $StateDir | Out-Null
                return Get-ConnectorJob -JobId $Job.job_id -StateDir $StateDir
            }
        } catch {
            Update-ConnectorJobStep -JobId $Job.job_id -Step $stepLower -Status 'failed' -StateDir $StateDir -ErrorDetail $_.Exception.Message | Out-Null
            if (Test-ShouldRollback -Reason 'FanoutFastFail') {
                $fresh = Get-ConnectorJob -JobId $Job.job_id -StateDir $StateDir
                $rbCtx = @{ job_id=$fresh.job_id; event=$Event; config=$Config; artifact_ref=$fresh.artifact_ref; previous_artifact_ref=$fresh.previous_artifact_ref }
                $connectorFn = (($Job.connector_type -split '[_-]') | ForEach-Object { if ($_.Length -gt 0) { $_.Substring(0,1).ToUpper()+$_.Substring(1) } }) -join ''
                Invoke-ConnectorRollback -Context $rbCtx -ConnectorType $connectorFn -StateDir $StateDir
            }
            return Get-ConnectorJob -JobId $Job.job_id -StateDir $StateDir
        }
    }
}

function Start-ConnectorBackgroundJob {
    param([hashtable]$Job,[hashtable]$Event,[hashtable]$Config,[string]$StateDir)
    $root = (Split-Path $PSScriptRoot -Parent)
    Start-Job -ArgumentList $root,$Job,$Event,$Config,$StateDir -ScriptBlock {
        param($r,$Job,$Event,$Config,$StateDir)
        Import-Module (Join-Path $r 'core/Logger.psm1') -Force
        Import-Module (Join-Path $r 'core/State-Store.psm1') -Force
        Import-Module (Join-Path $r 'core/Retry-Engine.psm1') -Force
        Import-Module (Join-Path $r 'core/Rollback-Engine.psm1') -Force
        Import-Module (Join-Path $r 'core/Fanout-Runner.psm1') -Force
        $cf = Join-Path $r "connectors/$($Job.connector_type.Replace('_','-')).psm1"
        Import-Module $cf -Force
        Invoke-ConnectorJob -Job $Job -Event $Event -Config $Config -StateDir $StateDir -RootPath $r
    }
}

function Invoke-FanoutRunner {
    param([hashtable]$Event,[hashtable]$Policy,[string]$StateDir)
    $jobs = @()
    foreach ($connector in $Policy.connectors) {
        $config = ConvertTo-Hashtable -InputObject $connector
        $new = New-ConnectorJob -RenewalId $Event.renewal_id -DeploymentPolicyId $Policy.policy_id -ConnectorType $config.connector_type -StateDir $StateDir
        $jobs += ,@{ job = $new; config = $config }
        $connectorFile = Join-Path (Split-Path $PSScriptRoot -Parent) "connectors/$($config.connector_type.Replace('_','-')).psm1"
        if (-not (Test-Path $connectorFile)) {
            Update-ConnectorJobStep -JobId $new.job_id -Step 'probe' -Status 'failed' -StateDir $StateDir -ErrorDetail "connector_not_implemented:$($new.connector_type)" | Out-Null
            Write-CertificateLog -Level Warning -Message "No connector for '$($new.connector_type)' — job failed cleanly"
            return
        }
        Import-Module $connectorFile -Force
    }

    $defaultFanout = [Environment]::GetEnvironmentVariable('CERTIFICATE_DEFAULT_FANOUT')
    if ([string]::IsNullOrWhiteSpace($defaultFanout)) { $defaultFanout = 'fail-fast' }
    $fanout = if ([string]::IsNullOrWhiteSpace($Policy.fanout_policy)) { $defaultFanout } else { $Policy.fanout_policy }

    if ($fanout -eq 'fail-fast') {
        $succeeded = @()
        foreach ($entry in $jobs) {
            $result = Invoke-ConnectorJob -Job $entry.job -Event $Event -Config $entry.config -StateDir $StateDir
            if ($result.status -eq 'succeeded') { $succeeded += ,$result; continue }
            foreach ($done in $succeeded) {
                $cfg = ($jobs | Where-Object { $_.job.job_id -eq $done.job_id } | Select-Object -First 1).config
                $rbConnectorFn = (($done.connector_type -split '[_-]') | ForEach-Object { if ($_.Length -gt 0) { $_.Substring(0,1).ToUpper()+$_.Substring(1) } }) -join ''
                Invoke-ConnectorRollback -Context @{ job_id=$done.job_id; event=$Event; config=$cfg; artifact_ref=$done.artifact_ref; previous_artifact_ref=$done.previous_artifact_ref } -ConnectorType $rbConnectorFn -StateDir $StateDir
            }
            break
        }
        return
    }

    $bg = @(); foreach ($entry in $jobs) { $bg += ,(Start-ConnectorBackgroundJob -Job $entry.job -Event $Event -Config $entry.config -StateDir $StateDir) }
    Wait-Job -Job $bg | Out-Null; $null = $bg | Receive-Job; $bg | Remove-Job -Force

    $all = Get-ConnectorJobsByRenewal -RenewalId $Event.renewal_id -StateDir $StateDir
    $succeeded = @($all | Where-Object { $_.status -eq 'succeeded' })
    if ($fanout -eq 'best-effort') { return }
    if ($fanout -eq 'quorum' -and $succeeded.Count -lt [int]$Policy.quorum_threshold) {
        $cfgMap = @{}; foreach ($e in $jobs) { $cfgMap[$e.job.job_id] = $e.config }
        foreach ($done in $succeeded) {
            $cfg = $cfgMap[$done.job_id]
            if (-not $cfg) { Write-CertificateLog -Level Warning -Message "Quorum rollback: no config for $($done.job_id)"; continue }
            $rbConnectorFn = (($done.connector_type -split '[_-]') | ForEach-Object { if ($_.Length -gt 0) { $_.Substring(0,1).ToUpper()+$_.Substring(1) } }) -join ''
            Invoke-ConnectorRollback -Context @{ job_id=$done.job_id; event=$Event; config=$cfg; artifact_ref=$done.artifact_ref; previous_artifact_ref=$done.previous_artifact_ref } -ConnectorType $rbConnectorFn -StateDir $StateDir
        }
    }
}

Export-ModuleMember -Function Invoke-ConnectorJob,Invoke-FanoutRunner
