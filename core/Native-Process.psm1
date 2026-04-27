Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-WindowsCommandLineArgument {
    [CmdletBinding()]
    param([AllowNull()][string]$Value)

    if ($null -eq $Value) { return '""' }

    if ($Value -notmatch '[\s"`]') {
        return $Value
    }

    $escaped = $Value -replace '(\\*)"', '$1$1\"'
    $escaped = $escaped -replace '(\\+)$', '$1$1'
    return '"' + $escaped + '"'
}

function Invoke-NativeProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$ArgumentList = @(),
        [int]$TimeoutSeconds = 300,
        [string[]]$FatalPatterns = @()
    )

    if (-not [System.IO.Path]::IsPathRooted($FilePath)) {
        throw "Invoke-NativeProcess requires an absolute FilePath. Provided: '$FilePath'"
    }
    if (-not (Test-Path -LiteralPath $FilePath)) {
        throw "Native executable not found: '$FilePath'"
    }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $FilePath
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.Arguments = (@($ArgumentList) | ForEach-Object {
        ConvertTo-WindowsCommandLineArgument -Value ([string]$_)
    }) -join ' '

    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $psi
    $startedAt = Get-Date
    [void]$proc.Start()
    if (-not $proc.WaitForExit($TimeoutSeconds * 1000)) {
        try { $proc.Kill() } catch {}
        return [pscustomobject]@{ Succeeded=$false; TimedOut=$true; ExitCode=-1; StdOut=''; StdErr='Timed out'; OutputLines=@(); FilePath=$FilePath; Arguments=@($ArgumentList); DurationMs=[int]((Get-Date)-$startedAt).TotalMilliseconds }
    }

    $stdout = $proc.StandardOutput.ReadToEnd()
    $stderr = $proc.StandardError.ReadToEnd()
    $lines = @()
    if (-not [string]::IsNullOrWhiteSpace($stdout)) { $lines += @($stdout -split "`r?`n" | Where-Object { $_ -ne '' }) }
    if (-not [string]::IsNullOrWhiteSpace($stderr)) { $lines += @($stderr -split "`r?`n" | Where-Object { $_ -ne '' }) }

    $fatalHit = $false
    foreach ($pattern in $FatalPatterns) {
        if ($lines | Where-Object { [string]$_ -match $pattern } | Select-Object -First 1) { $fatalHit = $true; break }
    }

    return [pscustomobject]@{
        Succeeded = ($proc.ExitCode -eq 0 -and -not $fatalHit)
        TimedOut = $false
        ExitCode = $proc.ExitCode
        StdOut = $stdout
        StdErr = $stderr
        OutputLines = $lines
        FilePath = $FilePath
        Arguments = @($ArgumentList)
        FatalPatternHit = $fatalHit
        DurationMs = [int]((Get-Date)-$startedAt).TotalMilliseconds
    }
}

Export-ModuleMember -Function Invoke-NativeProcess
