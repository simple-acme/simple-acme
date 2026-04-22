Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. "$PSScriptRoot/config.ps1"
Import-Module "$PSScriptRoot/core/Logger.psm1" -Force
Import-Module "$PSScriptRoot/core/State-Store.psm1" -Force
Import-Module "$PSScriptRoot/core/Fanout-Runner.psm1" -Force
Import-Module "$PSScriptRoot/core/Config-Store.psm1" -Force
Import-Module "$PSScriptRoot/core/Http-Listener.psm1" -Force
. "$PSScriptRoot/core/Types.ps1"

function Resolve-DeploymentPolicy {
    param([string]$PolicyId)
    $policyFile = Join-Path $PSScriptRoot 'policies.json'
    if (-not (Test-Path -LiteralPath $policyFile)) {
        Write-CertificaatLog -Level Error -Message "policies.json missing: $policyFile"
        return $null
    }
    $raw = Get-Content -Raw -Encoding UTF8 -Path $policyFile | ConvertFrom-Json
    $policies = ConvertTo-Hashtable -InputObject $raw
    $policy = $policies | Where-Object { $_.policy_id -eq $PolicyId } | Select-Object -First 1
    if ($null -eq $policy) { throw "Deployment policy '$PolicyId' was not found in policies.json." }

    foreach ($connector in $policy.connectors) {
        foreach ($k in @($connector.settings.Keys)) {
            if ($k -like '*_env') {
                $envName = $connector.settings[$k]
                $resolved = [Environment]::GetEnvironmentVariable($envName)
                if ([string]::IsNullOrWhiteSpace($resolved)) { throw "Environment variable '$envName' referenced by setting '$k' is not set." }
                $connector.settings[($k -replace '_env$','')] = $resolved
            }
        }
    }

    Assert-DeploymentPolicy -Policy $policy
    return $policy
}

function Resume-PendingJobs {
    param([string]$StateDir)
    $pending = Get-PendingConnectorJobs -StateDir $StateDir
    foreach ($job in $pending) {
        Write-Warning "Pending job '$($job.job_id)' found from previous run; auto-failing."
        Write-CertificaatLog -Level 'WARN' -Message 'Pending job found from previous run; marking as failed for operator review.' -JobId $job.job_id
        Update-ConnectorJobStep -JobId $job.job_id -Step $job.step -Status 'failed' -StateDir $StateDir -ErrorDetail 'Recovered as pending during startup.' | Out-Null
    }
}


function Process-EventData {
    param([hashtable]$EventData,[string]$StateDir)
    Assert-CertificateEvent -Event $EventData
    $policy = Resolve-DeploymentPolicy -PolicyId $EventData.deployment_policy_id
    if ($null -eq $policy) { throw "deployment policy not available" }
    Invoke-FanoutRunner -Event $EventData -Policy $policy -StateDir $StateDir
}

function Process-DropFile {
    param([string]$Path,[string]$DropDir,[string]$StateDir)
    Start-Sleep -Milliseconds 500
    try {
        $eventData = ConvertTo-Hashtable -InputObject (Get-Content -Raw -Encoding UTF8 -Path $Path | ConvertFrom-Json)
        Process-EventData -EventData $eventData -StateDir $StateDir

        $processed = Join-Path $DropDir 'processed'
        if (-not (Test-Path $processed)) { New-Item -ItemType Directory -Path $processed -Force | Out-Null }
        Move-Item -Path $Path -Destination (Join-Path $processed ([IO.Path]::GetFileName($Path))) -Force
    } catch {
        $failed = Join-Path $DropDir 'failed'
        if (-not (Test-Path $failed)) { New-Item -ItemType Directory -Path $failed -Force | Out-Null }
        if (Test-Path $Path) { Move-Item -Path $Path -Destination (Join-Path $failed ([IO.Path]::GetFileName($Path))) -Force }
        Write-CertificaatLog -Level 'ERROR' -Message "Failed processing drop file '$Path': $($_.Exception.ToString())"
    }
}

try {
    Initialize-CertificaatConfig
} catch {
    Write-CertificaatLog -Level 'ERROR' -Message "Configuration validation failed: $($_.Exception.Message)"
    exit 2
}

$DropDir = $env:CERTIFICAAT_DROP_DIR
$StateDir = $env:CERTIFICAAT_STATE_DIR

$devices = Get-AllDeviceConfigs -ConfigDir $env:CERTIFICAAT_CONFIG_DIR -SkipIntegrityFailures
if ($devices.Count -eq 0) {
    throw 'No device configs loaded. Failing startup because orchestrator cannot safely deploy without configured connectors.'
}
Resume-PendingJobs -StateDir $StateDir



$useHttp = [Environment]::GetEnvironmentVariable('CERTIFICAAT_HTTP_ENABLED')
if ($useHttp -eq '1') {
    Start-Job -ArgumentList $PSScriptRoot,$DropDir,$StateDir -ScriptBlock {
        param($root,$drop,$state)
        Import-Module (Join-Path $root 'core/Logger.psm1') -Force
        Import-Module (Join-Path $root 'core/Http-Listener.psm1') -Force
        Start-CertificaatHttpListener -DropDir $drop -StateDir $state
    } | Out-Null
}

$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = $DropDir
$watcher.Filter = '*.json'
$watcher.EnableRaisingEvents = $true

$msgData = @{ StateDir=$StateDir; DropDir=$DropDir }
Register-ObjectEvent -InputObject $watcher -EventName Created -MessageData $msgData -Action {
    $d = $Event.MessageData
    Process-DropFile -Path $Event.SourceEventArgs.FullPath -DropDir $d.DropDir -StateDir $d.StateDir
} | Out-Null

while ($true) { Wait-Event -Timeout 5 | Out-Null }
