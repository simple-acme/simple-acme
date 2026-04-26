Import-Module "$PSScriptRoot/../setup/Form-Runner.psm1" -Force

Describe 'Form runner deployment script wiring' {
    InModuleScope Form-Runner {
        It "Resolve-DeploymentScriptPath resolves cert2rds.ps1 from project root Scripts folder" {
            $resolved = Resolve-DeploymentScriptPath -ScriptFileName 'cert2rds.ps1'
            $expected = Join-Path (Split-Path $PSScriptRoot -Parent) 'Scripts/cert2rds.ps1'
            [System.IO.Path]::GetFullPath($resolved) | Should -Be ([System.IO.Path]::GetFullPath($expected))
        }

        It 'Guided RDS template uses cert2rds.ps1 and CertThumbprint parameter only' {
            $template = Get-GuidedPipelineTemplate -TargetSystem 'rds' -ValidationMode 'http-01'
            $expected = Join-Path (Split-Path $PSScriptRoot -Parent) 'Scripts/cert2rds.ps1'
            [System.IO.Path]::GetFullPath($template.ACME_SCRIPT_PATH) | Should -Be ([System.IO.Path]::GetFullPath($expected))
            $template.ACME_SCRIPT_PARAMETERS | Should -Be '{CertThumbprint}'
        }

        It 'Placeholder targets fail with a clear not implemented message' {
            { Get-ConnectorScriptByIntent -TargetIntent 'custom' } | Should -Throw '*This target type is not implemented yet.*'
            { Get-ConnectorScriptByIntent -TargetIntent 'mail' } | Should -Throw '*This target type is not implemented yet.*'
        }

        It 'Invoke-AcmeForm keeps existing valid ACME_SCRIPT_PATH for unchanged target' {
            $envPath = 'TestDrive:\certificate.env'
            Set-Content -Path $envPath -Value '# test' -Encoding UTF8
            $existingScript = Resolve-DeploymentScriptPath -ScriptFileName 'cert2rds.ps1'

            Mock -CommandName Read-EnvFile -MockWith {
                @{
                    ACME_TARGET_SYSTEM = 'rds'
                    ACME_SCRIPT_PATH = $existingScript
                    CERTIFICATE_CONFIG_DIR = 'TestDrive:\config'
                    CERTIFICATE_API_KEY = 'abc123'
                }
            }
            $script:menuAnswers = @('rds','this-server','single')
            Mock -CommandName Read-MenuChoice -MockWith {
                $next = $script:menuAnswers[0]
                $script:menuAnswers = @($script:menuAnswers | Select-Object -Skip 1)
                return $next
            }
            Mock -CommandName Read-DomainsInput -MockWith { 'example.com' }
            Mock -CommandName Test-RoleAvailable -MockWith { $true }
            Mock -CommandName Write-EnvFile
            Mock -CommandName Save-SecurePlatformConfig
            Mock -CommandName Save-RenewalMapping
            Mock -CommandName Read-Host -MockWith { 'host1' }

            Invoke-AcmeForm -EnvFilePath $envPath | Out-Null

            Should -Invoke -CommandName Write-EnvFile -Times 1 -ParameterFilter {
                $Values.ACME_SCRIPT_PATH -eq $existingScript -and $Values.ACME_SCRIPT_PARAMETERS -eq '{CertThumbprint}'
            }
        }
    }
}
