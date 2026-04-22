$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module "$PSScriptRoot/Logger.psm1" -Force

function Get-RetrySetting {
    param([string]$Name,[int]$Default)
    $raw = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($raw)) { return $Default }
    $parsed = 0
    if (-not [int]::TryParse($raw, [ref]$parsed) -or $parsed -lt 1) {
        throw "Invalid retry environment variable '$Name': '$raw'. Expected positive integer."
    }
    return $parsed
}

function Invoke-WithRetry {
    param(
        [scriptblock]$ScriptBlock,
        [int]$MaxAttempts,
        [int]$BackoffMs,
        [string]$Label = 'operation',
        [int]$MaxBackoffMs = 30000
    )

    if (-not $PSBoundParameters.ContainsKey('MaxAttempts')) { $MaxAttempts = Get-RetrySetting -Name 'CERTIFICAAT_RETRY_MAX_ATTEMPTS' -Default 3 }
    if (-not $PSBoundParameters.ContainsKey('BackoffMs')) { $BackoffMs = Get-RetrySetting -Name 'CERTIFICAAT_RETRY_BACKOFF_MS' -Default 1000 }

    $lastError = $null
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            return & $ScriptBlock
        } catch {
            $lastError = $_
            Write-CertificaatLog -Level 'WARN' -Message "Retryable failure for '$Label' on attempt $attempt/$MaxAttempts: $($_.Exception.Message)"
            if ($attempt -lt $MaxAttempts) {
                $base = $BackoffMs * [math]::Pow(2, $attempt - 1)
                $min = [int]($base * 0.8)
                $max = [int]($base * 1.2)
                $sleepMs = Get-Random -Minimum $min -Maximum ($max + 1)
                $sleepMs = [Math]::Min($sleepMs, $MaxBackoffMs)
                Start-Sleep -Milliseconds $sleepMs
            }
        }
    }

    throw $lastError
}

Export-ModuleMember -Function Invoke-WithRetry
