Set-StrictMode -Version Latest
Import-Module "$PSScriptRoot/State-Store.psm1" -Force
Import-Module "$PSScriptRoot/Logger.psm1" -Force

function Test-ShouldRollback {
    param(
        [ValidateSet('VerifyExhausted','ActivateTimeout','DeployInvalid','FanoutFastFail')]
        [string]$Reason
    )
    return $true
}

function Invoke-ConnectorRollback {
    param(
        [hashtable]$Context,
        [string]$ConnectorType,
        [string]$StateDir
    )

    $job = Get-ConnectorJob -JobId $Context.job_id -StateDir $StateDir
    if ($null -eq $job) {
        Write-CertificaatLog -Level 'ERROR' -Message "Cannot rollback missing job '$($Context.job_id)'" -JobId $Context.job_id -Domain $Context.event.domain -Step 'rollback'
        return
    }

    if ($job.status -in @('rolled_back','rolled_back_failed')) {
        Write-CertificaatLog -Level 'INFO' -Message 'Rollback already completed previously, skipping.' -JobId $job.job_id -Domain $Context.event.domain -Step 'rollback'
        return
    }

    Update-ConnectorJobStep -JobId $job.job_id -Step 'rollback' -Status 'running' -StateDir $StateDir -Attempt 0 | Out-Null
    try {
        $fn = "Invoke-${ConnectorType}Rollback"
        & $fn -Context $Context | Out-Null
        Update-ConnectorJobStep -JobId $job.job_id -Step 'rollback' -Status 'rolled_back' -StateDir $StateDir -Attempt 0 | Out-Null
        Write-CertificaatLog -Level 'INFO' -Message 'Rollback succeeded.' -JobId $job.job_id -Domain $Context.event.domain -Step 'rollback'
    } catch {
        Update-ConnectorJobStep -JobId $job.job_id -Step 'rollback' -Status 'rolled_back_failed' -StateDir $StateDir -Attempt 0 -ErrorDetail $_.Exception.ToString() | Out-Null
        Write-CertificaatLog -Level 'ERROR' -Message "Rollback failed: $($_.Exception.ToString())" -JobId $job.job_id -Domain $Context.event.domain -Step 'rollback'
        throw
    }
}

Export-ModuleMember -Function Test-ShouldRollback,Invoke-ConnectorRollback
