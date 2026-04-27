$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function New-OrchestratorTaskDefinition {
    param(
        [Parameter(Mandatory)][string]$ScriptPath,
        [string]$PowerShellExe = 'powershell.exe',
        [int]$EveryMinutes = 5,
        [string]$TaskUser = 'SYSTEM'
    )

    if (-not [System.IO.Path]::IsPathRooted($ScriptPath)) {
        throw "ScriptPath must be absolute. Provided: '$ScriptPath'"
    }

    $absoluteScriptPath = [System.IO.Path]::GetFullPath($ScriptPath)
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$absoluteScriptPath`""

    $startBoundary = (Get-Date).Date
    $interval = New-TimeSpan -Minutes $EveryMinutes
    $duration = New-TimeSpan -Days 3650

    $action = New-ScheduledTaskAction -Execute $PowerShellExe -Argument $arguments
    $trigger = New-ScheduledTaskTrigger -Once -At $startBoundary -RepetitionInterval $interval -RepetitionDuration $duration
    $principal = New-ScheduledTaskPrincipal -UserId $TaskUser -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 2) -StartWhenAvailable

    return @{
        Action = $action
        Trigger = $trigger
        Principal = $principal
        Settings = $settings
        ScriptPath = $absoluteScriptPath
        PowerShellExe = $PowerShellExe
        EveryMinutes = $EveryMinutes
        TaskUser = $TaskUser
        ArgumentText = $arguments
    }
}

function Test-OrchestratorTaskMatches {
    param(
        [Parameter(Mandatory)]$ExistingTask,
        [Parameter(Mandatory)][hashtable]$Expected
    )

    $actions = @($ExistingTask.Actions)
    $triggers = @($ExistingTask.Triggers)
    if ($actions.Count -lt 1 -or $triggers.Count -lt 1) { return $false }

    $firstAction = $actions[0]
    $firstTrigger = $triggers[0]
    $existingExecute = [string]$firstAction.Execute
    $existingArgs = [string]$firstAction.Arguments
    $existingUser = [string]$ExistingTask.Principal.UserId
    $existingInterval = [string]$firstTrigger.Repetition.Interval

    $expectedInterval = [string](New-TimeSpan -Minutes $Expected.EveryMinutes)

    if ($existingExecute -ne [string]$Expected.PowerShellExe) { return $false }
    if ($existingArgs -ne [string]$Expected.ArgumentText) { return $false }
    if ($existingUser -ne [string]$Expected.TaskUser) { return $false }
    if ($existingInterval -ne $expectedInterval) { return $false }

    return $true
}

function Ensure-OrchestratorScheduledTask {
    param(
        [string]$TaskName = 'Certificate-Orchestrator',
        [Parameter(Mandatory)][string]$ScriptPath,
        [int]$EveryMinutes = 5,
        [string]$TaskUser = 'SYSTEM',
        [string]$PowerShellExe = 'powershell.exe'
    )

    if (-not $IsWindows) {
        throw 'Scheduled task registration is only supported on Windows.'
    }

    if ($EveryMinutes -lt 1) {
        throw "EveryMinutes must be >= 1. Provided: $EveryMinutes"
    }

    $expected = New-OrchestratorTaskDefinition -ScriptPath $ScriptPath -PowerShellExe $PowerShellExe -EveryMinutes $EveryMinutes -TaskUser $TaskUser
    $existing = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue

    if ($null -eq $existing) {
        Register-ScheduledTask -TaskName $TaskName -Action $expected.Action -Trigger $expected.Trigger -Principal $expected.Principal -Settings $expected.Settings | Out-Null
        return [pscustomobject]@{
            Action = 'create'
            TaskName = $TaskName
            ScriptPath = $expected.ScriptPath
            EveryMinutes = $EveryMinutes
            TaskUser = $TaskUser
        }
    }

    if (Test-OrchestratorTaskMatches -ExistingTask $existing -Expected $expected) {
        return [pscustomobject]@{
            Action = 'no-op'
            TaskName = $TaskName
            ScriptPath = $expected.ScriptPath
            EveryMinutes = $EveryMinutes
            TaskUser = $TaskUser
        }
    }

    Register-ScheduledTask -TaskName $TaskName -Action $expected.Action -Trigger $expected.Trigger -Principal $expected.Principal -Settings $expected.Settings -Force | Out-Null
    return [pscustomobject]@{
        Action = 'update'
        TaskName = $TaskName
        ScriptPath = $expected.ScriptPath
        EveryMinutes = $EveryMinutes
        TaskUser = $TaskUser
    }
}

$FunctionsToExport = @(
    'Ensure-OrchestratorScheduledTask',
    'New-OrchestratorTaskDefinition',
    'Test-OrchestratorTaskMatches'
)

Export-ModuleMember -Function $FunctionsToExport
