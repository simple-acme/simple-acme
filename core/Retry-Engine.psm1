Set-StrictMode -Version Latest
Import-Module "$PSScriptRoot/Logger.psm1" -Force

function Invoke-WithRetry {
    param(
        [scriptblock]$ScriptBlock,
        [int]$MaxAttempts = 3,
        [int]$BackoffMs = 1000,
        [string]$Label = 'operation'
    )

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
                Start-Sleep -Milliseconds $sleepMs
            }
        }
    }

    throw $lastError
}

Export-ModuleMember -Function Invoke-WithRetry
