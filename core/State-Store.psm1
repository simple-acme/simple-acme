Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Types.ps1"

function Get-UtcTimestamp {
    (Get-Date).ToUniversalTime().ToString('o')
}

function Enter-StateStoreLock {
    param([Parameter(Mandatory)][string]$StateDir)
    $name = 'Global\simple_acme_state_' + ([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($StateDir)).Replace('=','').Replace('/','_').Replace('+','-'))
    $mutex = New-Object System.Threading.Mutex($false, $name)
    if (-not $mutex.WaitOne(30000)) { throw "Timed out acquiring state store lock for '$StateDir'." }
    return $mutex
}

function Save-ConnectorJob {
    param([hashtable]$Job, [string]$StateDir)
    if (-not (Test-Path -LiteralPath $StateDir)) { New-Item -ItemType Directory -Path $StateDir -Force | Out-Null }
    Assert-ConnectorJobRecord -Job $Job

    $lock = Enter-StateStoreLock -StateDir $StateDir
    try {
        $tmp = Join-Path $StateDir "$($Job.job_id).tmp"
        $final = Join-Path $StateDir "$($Job.job_id).json"
        $Job | ConvertTo-Json -Depth 10 | Set-Content -Path $tmp -Encoding UTF8
        Move-Item -Path $tmp -Destination $final -Force
    } finally {
        $lock.ReleaseMutex() | Out-Null
        $lock.Dispose()
    }
}

function Get-ConnectorJob {
    param([string]$JobId, [string]$StateDir)
    $path = Join-Path $StateDir "$JobId.json"
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    $raw = Get-Content -Path $path -Raw -Encoding UTF8 | ConvertFrom-Json
    return ConvertTo-Hashtable -InputObject $raw
}

function New-ConnectorJob {
    param(
        [string]$RenewalId,
        [string]$DeploymentPolicyId,
        [string]$ConnectorType,
        [string]$StateDir
    )

    $now = Get-UtcTimestamp
    $job = @{
        job_id                = ([guid]::NewGuid()).Guid
        renewal_id            = $RenewalId
        deployment_policy_id  = $DeploymentPolicyId
        connector_type        = $ConnectorType
        step                  = 'probe'
        status                = 'pending'
        artifact_ref          = ''
        previous_artifact_ref = ''
        attempt               = 0
        error_detail          = ''
        created_at            = $now
        updated_at            = $now
    }

    Save-ConnectorJob -Job $job -StateDir $StateDir
    return $job
}

function Update-ConnectorJobStep {
    param(
        [string]$JobId,
        [string]$Step,
        [string]$Status,
        [string]$StateDir,
        [string]$ArtifactRef = $null,
        [string]$PreviousArtifactRef = $null,
        [int]$Attempt = 0,
        [string]$ErrorDetail = $null
    )

    $job = Get-ConnectorJob -JobId $JobId -StateDir $StateDir
    if ($null -eq $job) { throw "Connector job '$JobId' not found in state directory '$StateDir'." }

    $job.step = $Step
    $job.status = $Status
    $job.attempt = $Attempt
    if ($PSBoundParameters.ContainsKey('ArtifactRef')) { $job.artifact_ref = if ($null -eq $ArtifactRef) { '' } else { $ArtifactRef } }
    if ($PSBoundParameters.ContainsKey('PreviousArtifactRef')) { $job.previous_artifact_ref = if ($null -eq $PreviousArtifactRef) { '' } else { $PreviousArtifactRef } }
    if ($PSBoundParameters.ContainsKey('ErrorDetail')) { $job.error_detail = if ($null -eq $ErrorDetail) { '' } else { $ErrorDetail } }
    $job.updated_at = Get-UtcTimestamp

    Save-ConnectorJob -Job $job -StateDir $StateDir
    return $job
}

function Get-ConnectorJobsByRenewal {
    param([string]$RenewalId, [string]$StateDir)
    if (-not (Test-Path -LiteralPath $StateDir)) { return @() }
    $jobs = @()
    Get-ChildItem -Path $StateDir -Filter '*.json' -File | ForEach-Object {
        $job = ConvertTo-Hashtable -InputObject (Get-Content -Path $_.FullName -Raw -Encoding UTF8 | ConvertFrom-Json)
        if ($job.renewal_id -eq $RenewalId) { $jobs += ,$job }
    }
    return $jobs
}

function Get-PendingConnectorJobs {
    param([string]$StateDir)
    if (-not (Test-Path -LiteralPath $StateDir)) { return @() }
    $jobs = @()
    Get-ChildItem -Path $StateDir -Filter '*.json' -File | ForEach-Object {
        $job = ConvertTo-Hashtable -InputObject (Get-Content -Path $_.FullName -Raw -Encoding UTF8 | ConvertFrom-Json)
        if ($job.status -eq 'pending') { $jobs += ,$job }
    }
    return $jobs
}

Export-ModuleMember -Function New-ConnectorJob,Update-ConnectorJobStep,Get-ConnectorJob,Get-ConnectorJobsByRenewal,Get-PendingConnectorJobs
