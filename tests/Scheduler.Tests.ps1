Import-Module "$PSScriptRoot/../core/Scheduler.psm1" -Force

Describe 'Scheduler module' {
    It 'creates task when missing' -Skip:(-not $IsWindows) {
        Mock Get-ScheduledTask { $null }
        Mock New-ScheduledTaskAction { [pscustomobject]@{ Execute = $Execute; Arguments = $Argument } }
        Mock New-ScheduledTaskTrigger { [pscustomobject]@{ Repetition = [pscustomobject]@{ Interval = [string](New-TimeSpan -Minutes 5) } } }
        Mock New-ScheduledTaskPrincipal { [pscustomobject]@{ UserId = $UserId } }
        Mock New-ScheduledTaskSettingsSet { [pscustomobject]@{} }
        Mock Register-ScheduledTask {}

        $result = Ensure-OrchestratorScheduledTask -TaskName 'Certificate-Orchestrator' -ScriptPath 'C:\certificate\certificate-orchestrator.ps1'
        $result.Action | Should -Be 'create'
        Should -Invoke Register-ScheduledTask -Times 1
    }

    It 'no-ops when existing task matches' -Skip:(-not $IsWindows) {
        Mock New-ScheduledTaskAction { [pscustomobject]@{ Execute = $Execute; Arguments = $Argument } }
        Mock New-ScheduledTaskTrigger { [pscustomobject]@{ Repetition = [pscustomobject]@{ Interval = [string](New-TimeSpan -Minutes 5) } } }
        Mock New-ScheduledTaskPrincipal { [pscustomobject]@{ UserId = $UserId } }
        Mock New-ScheduledTaskSettingsSet { [pscustomobject]@{} }
        Mock Get-ScheduledTask {
            [pscustomobject]@{
                Actions = @([pscustomobject]@{
                    Execute = 'powershell.exe'
                    Arguments = '-NoProfile -ExecutionPolicy Bypass -File "C:\certificate\certificate-orchestrator.ps1"'
                })
                Triggers = @([pscustomobject]@{
                    Repetition = [pscustomobject]@{ Interval = [string](New-TimeSpan -Minutes 5) }
                })
                Principal = [pscustomobject]@{ UserId = 'SYSTEM' }
            }
        }
        Mock Register-ScheduledTask {}

        $result = Ensure-OrchestratorScheduledTask -TaskName 'Certificate-Orchestrator' -ScriptPath 'C:\certificate\certificate-orchestrator.ps1'
        $result.Action | Should -Be 'no-op'
        Should -Invoke Register-ScheduledTask -Times 0
    }
}
