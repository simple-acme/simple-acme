Set-StrictMode -Version Latest

$script:EventSource = 'Certificaat'
$script:EventLogName = 'Application'

function Ensure-EventSource {
    try {
        if (-not [System.Diagnostics.EventLog]::SourceExists($script:EventSource)) {
            New-EventLog -LogName $script:EventLogName -Source $script:EventSource
        }
    } catch {
        # NOTE: source creation may fail without elevation. Continue with Write-EventLog attempt.
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
        # NOTE: Event Log write may fail in non-Windows test environments.
    }

    $logDir = $env:CERTIFICAAT_LOG_DIR
    if ($logDir -and (Test-Path -LiteralPath $logDir)) {
        $path = Join-Path $logDir 'orchestrator.log'
        Add-Content -Path $path -Value $line -Encoding UTF8
    }
}

Export-ModuleMember -Function Write-CertificaatLog
