Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. "$PSScriptRoot/config.ps1"
Import-Module "$PSScriptRoot/core/Logger.psm1" -Force
Import-Module "$PSScriptRoot/core/State-Store.psm1" -Force
Import-Module "$PSScriptRoot/core/Fanout-Runner.psm1" -Force
Import-Module "$PSScriptRoot/core/Config-Store.psm1" -Force
Import-Module "$PSScriptRoot/core/Http-Listener.psm1" -Force
. "$PSScriptRoot/core/Types.ps1"

function Assert-OrchestratorInputs {
    param(
        [Parameter(Mandatory)][string]$DropDir,
        [Parameter(Mandatory)][string]$StateDir
    )

    $domains = [Environment]::GetEnvironmentVariable('DOMAINS')
    if ([string]::IsNullOrWhiteSpace($domains)) {
        throw 'Missing config: DOMAINS'
    }
    foreach ($domain in ($domains -split ',' | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        if ($domain -notmatch '^(?=.{1,253}$)(?!-)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$') {
            throw "Invalid domain format in DOMAINS: '$domain'"
        }
    }

    foreach ($pathValue in @($DropDir, $StateDir)) {
        if ([string]::IsNullOrWhiteSpace($pathValue)) {
            throw 'CERTIFICATE_DROP_DIR and CERTIFICATE_STATE_DIR must be non-empty.'
        }
        if (-not [System.IO.Path]::IsPathRooted($pathValue)) {
            throw "Directory path must be absolute: '$pathValue'"
        }
        if (-not (Test-Path -LiteralPath $pathValue)) {
            New-Item -ItemType Directory -Path $pathValue -Force | Out-Null
        }
    }
}

function Resolve-DeploymentPolicy {
    param([string]$PolicyId)
    $policyFile = if (-not [string]::IsNullOrWhiteSpace($env:CERTIFICATE_CONFIG_DIR) -and
                   (Test-Path -LiteralPath (Join-Path $env:CERTIFICATE_CONFIG_DIR 'policies.json'))) {
        Join-Path $env:CERTIFICATE_CONFIG_DIR 'policies.json'
    } else {
        Join-Path $PSScriptRoot 'policies.json'
    }
    if (-not (Test-Path -LiteralPath $policyFile)) {
        Write-CertificateLog -Level Warn -Message "policies.json not found at '$policyFile'. Run certificate-setup.ps1 and configure at least one deployment policy before processing events."
        return $null
    }
    $rawText = Get-Content -Raw -Encoding UTF8 -Path $policyFile
    if ([string]::IsNullOrWhiteSpace($rawText)) {
        throw "Policy file '$policyFile' is empty."
    }
    $raw = $rawText | ConvertFrom-Json
    $policies = ConvertTo-Hashtable -InputObject $raw
    if (-not $policies -or @($policies).Count -eq 0) {
        Write-CertificateLog -Level Warn -Message "policies.json exists but contains no policies. Run certificate-setup.ps1 and add at least one deployment policy."
        return $null
    }
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
        Write-CertificateLog -Level 'WARN' -Message 'Pending job found from previous run; marking as failed for operator review.' -JobId $job.job_id
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

function Acquire-DropFileLock {
    param([Parameter(Mandatory)][string]$Path)
    $processingDir = Join-Path ([System.IO.Path]::GetDirectoryName($Path)) 'processing'
    if (-not (Test-Path -LiteralPath $processingDir)) { New-Item -ItemType Directory -Path $processingDir -Force | Out-Null }
    $lockedPath = Join-Path $processingDir ([System.IO.Path]::GetFileName($Path))
    Move-Item -LiteralPath $Path -Destination $lockedPath -ErrorAction Stop
    return $lockedPath
}

function Process-DropFile {
    param([string]$Path,[string]$DropDir,[string]$StateDir)
    Start-Sleep -Milliseconds 500
    try {
        if (-not (Test-Path -LiteralPath $Path)) { return }
        $lockedPath = Acquire-DropFileLock -Path $Path
        $rawJson = Get-Content -Raw -Encoding UTF8 -Path $lockedPath
        if ([string]::IsNullOrWhiteSpace($rawJson)) { throw "Drop file '$Path' is empty." }
        $eventData = ConvertTo-Hashtable -InputObject ($rawJson | ConvertFrom-Json)
        if ($null -eq $eventData) { throw "Drop file '$Path' did not contain a JSON object." }
        Process-EventData -EventData $eventData -StateDir $StateDir

        $processed = Join-Path $DropDir 'processed'
        if (-not (Test-Path $processed)) { New-Item -ItemType Directory -Path $processed -Force | Out-Null }
        Move-Item -Path $lockedPath -Destination (Join-Path $processed ([IO.Path]::GetFileName($lockedPath))) -Force
    } catch {
        $failed = Join-Path $DropDir 'failed'
        if (-not (Test-Path $failed)) { New-Item -ItemType Directory -Path $failed -Force | Out-Null }
        if ($lockedPath -and (Test-Path $lockedPath)) { Move-Item -Path $lockedPath -Destination (Join-Path $failed ([IO.Path]::GetFileName($lockedPath))) -Force }
        Write-CertificateLog -Level 'ERROR' -Message "Failed processing drop file '$Path': $($_.Exception.ToString())"
    }
}

try {
    Write-Output 'Starting certificate orchestrator.'
    Initialize-CertificateConfig | Out-Null

    $DropDir = $env:CERTIFICATE_DROP_DIR
    $StateDir = $env:CERTIFICATE_STATE_DIR
    Assert-OrchestratorInputs -DropDir $DropDir -StateDir $StateDir

    $devices = Get-AllDeviceConfigs -ConfigDir $env:CERTIFICATE_CONFIG_DIR -SkipIntegrityFailures
    if ($devices.Count -eq 0) {
        throw 'No device configs loaded. Failing startup because orchestrator cannot safely deploy without configured connectors.'
    }
    Resume-PendingJobs -StateDir $StateDir

    $useHttp = [Environment]::GetEnvironmentVariable('CERTIFICATE_HTTP_ENABLED')
    if ($useHttp -eq '1') {
        Start-Job -ArgumentList $PSScriptRoot,$DropDir,$StateDir -ScriptBlock {
            param($root,$drop,$state)
            Import-Module (Join-Path $root 'core/Logger.psm1') -Force
            Import-Module (Join-Path $root 'core/Http-Listener.psm1') -Force
            Start-CertificateHttpListener -DropDir $drop -StateDir $state
        } | Out-Null
    }

    $watcher = New-Object System.IO.FileSystemWatcher
    $watcher.Path = $DropDir
    $watcher.Filter = '*.json'
    $watcher.EnableRaisingEvents = $true

    Write-Output "Watching drop directory: $DropDir"
    $msgData = @{ StateDir=$StateDir; DropDir=$DropDir }
    Register-ObjectEvent -InputObject $watcher -EventName Created -MessageData $msgData -Action {
        $d = $Event.MessageData
        Process-DropFile -Path $Event.SourceEventArgs.FullPath -DropDir $d.DropDir -StateDir $d.StateDir
    } | Out-Null

    while ($true) { Wait-Event -Timeout 5 | Out-Null }
} catch {
    Write-Error $_
    exit 1
}
