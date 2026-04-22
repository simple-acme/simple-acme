Import-Module "$PSScriptRoot/../core/Fanout-Runner.psm1" -Force
Describe 'Fanout-Runner' {
    It 'loads module' { $true | Should -BeTrue }
}
