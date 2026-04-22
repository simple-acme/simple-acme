$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$script:EventSource = 'Certificaat'
$script:EventLogName = 'Application'

function Ensure-EventSource {
    try {
        if (-not [System.Diagnostics.EventLog]::SourceExists($script:EventSource)) {
            New-EventLog -LogName $script:EventLogName -Source $script:EventSource
        }
    } catch {
        Write-Warning "Event Log: $($_.Exception.Message)"
    }
}

function Write-CertificaatLog {
    param(
        [ValidateSet('INFO','WARN','ERROR')][string]$Level,
        [string]$Message,
        [string]$JobId = '',
        [string]$Domain = '',
        [string]$Step = ''
    )

    $timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    $line = '{0} [{1,-5}] [job:{2}] [domain:{3}] [step:{4}] {5}' -f $timestamp, $Level, $JobId, $Domain, $Step, $Message

    $entryType = switch ($Level) {
        'INFO' { 'Information' }
        'WARN' { 'Warning' }
        'ERROR' { 'Error' }
    }

    Ensure-EventSource
    try {
        Write-EventLog -LogName $script:EventLogName -Source $script:EventSource -EventId 1000 -EntryType $entryType -Message $line
    } catch {
        Write-Warning "Event Log: $($_.Exception.Message)"
    }

    $logDir = $env:CERTIFICAAT_LOG_DIR
    if ($logDir -and (Test-Path -LiteralPath $logDir)) {
        $path = Join-Path $logDir 'orchestrator.log'
        try { Add-Content -Path $path -Value $line -Encoding UTF8 } catch { Write-Warning "Log file write failed: $($_.Exception.Message)" }
    }
}

Export-ModuleMember -Function Write-CertificaatLog
