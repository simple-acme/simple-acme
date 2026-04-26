Import-Module "$PSScriptRoot/../core/Tui-Engine.psm1" -Force
Import-Module "$PSScriptRoot/../setup/Form-Runner.psm1" -Force

Describe 'Setup TUI and policy reliability' {
    It 'Tui layout defines required keys and is not reassigned' {
        $modulePath = Join-Path $PSScriptRoot '../core/Tui-Engine.psm1'
        $raw = Get-Content -LiteralPath $modulePath -Raw
        ([regex]::Matches($raw, '(?m)^\$TuiLayout\s*=\s*@\{')).Count | Should -Be 1

        $requiredKeys = @(
            'MinWidth'
            'MinHeight'
            'MarginX'
            'HeaderRows'
            'FooterRows'
            'HeaderY'
            'ContentTop'
            'ContentBottomPadding'
            'LabelWidth'
            'MinBoxWidth'
            'MaxBoxWidth'
            'FieldRowsMax'
            'StatusRows'
        )

        foreach ($key in $requiredKeys) {
            $TuiLayout.ContainsKey($key) | Should -BeTrue
        }
    }

    It 'Show-TuiForm does not call Read-Host' {
        $modulePath = Join-Path $PSScriptRoot '../core/Tui-Engine.psm1'
        $raw = Get-Content -LiteralPath $modulePath -Raw
        $match = [regex]::Match($raw, 'function\s+Show-TuiForm\s*\{(?s).*?\n\}')
        $match.Success | Should -BeTrue
        $match.Value | Should -Not -Match '\bRead-Host\b'
    }

    It 'Policy editor warns and stays in loop when no policy exists for edit/delete' {
        $cfg = 'TestDrive:\cfg-no-policy'
        New-Item -ItemType Directory -Path $cfg -Force | Out-Null

        $script:answers = @('E','Q')
        Mock -CommandName Read-Host -MockWith {
            $script:answers[0]
            $script:answers = @($script:answers | Select-Object -Skip 1)
        }
        Mock -CommandName Show-TuiStatus -Verifiable -ParameterFilter { $Type -eq 'Warning' -and $Message -like 'No policies exist*' }

        Invoke-PolicyEditor -ConfigDir $cfg | Out-Null

        Should -InvokeVerifiable
    }

    It 'Policy editor warns and returns to loop for invalid policy id' {
        $cfg = 'TestDrive:\cfg-invalid-id'
        New-Item -ItemType Directory -Path $cfg -Force | Out-Null
        Set-Content -Path (Join-Path $cfg 'policies.json') -Value '[{"policy_id":"p1","fanout_policy":"all","quorum_threshold":1,"connectors":[]}]' -Encoding UTF8

        $script:answers = @('E','missing-policy','Q')
        Mock -CommandName Read-Host -MockWith {
            $script:answers[0]
            $script:answers = @($script:answers | Select-Object -Skip 1)
        }
        Mock -CommandName Show-TuiStatus -Verifiable -ParameterFilter { $Type -eq 'Warning' -and $Message -like "Policy 'missing-policy' was not found*" }

        Invoke-PolicyEditor -ConfigDir $cfg | Out-Null

        Should -InvokeVerifiable
    }

    It 'Format-PolicySummaryLines supports empty and populated policy lists' {
        $empty = @(Format-PolicySummaryLines -Policies @())
        $empty.Count | Should -Be 1
        $empty[0] | Should -Match 'No deployment policies found'

        $policies = @(
            [pscustomobject]@{ policy_id='alpha'; fanout_policy='all'; quorum_threshold=2; connectors=@([pscustomobject]@{ connector_type='iis' }) },
            [pscustomobject]@{ policy_id='beta'; fanout_policy='any'; quorum_threshold=1; connectors=@([pscustomobject]@{ connector_type='f5' },[pscustomobject]@{ connector_type='kemp' }) }
        )
        $lines = @(Format-PolicySummaryLines -Policies $policies)
        $lines.Count | Should -Be 2
        $lines[0] | Should -Match 'policy_id=alpha'
        $lines[1] | Should -Match 'connectors=2'
    }

    It 'Menu ACME script-parameters placeholder uses valid PowerShell quoting' {
        $menuPath = Join-Path $PSScriptRoot '../setup/Menu-Tree.ps1'
        $runnerPath = Join-Path $PSScriptRoot '../setup/Form-Runner.psm1'
        $menuRaw = Get-Content -LiteralPath $menuPath -Raw
        $runnerRaw = Get-Content -LiteralPath $runnerPath -Raw

        $expectedPlaceholder = "'default' {RenewalId} '{CertCommonName}' {CertThumbprint} {OldCertThumbprint} '{CacheFile}' '{CachePassword}' '{StorePath}' {StoreType}"

        $menuRaw | Should -Match ([regex]::Escape("Name='ACME_SCRIPT_PARAMETERS'"))
        $menuRaw | Should -Not -Match ([regex]::Escape('Placeholder=\"'))
        $menuRaw | Should -Match ([regex]::Escape(('Placeholder="' + $expectedPlaceholder + '"')) )
        $runnerRaw | Should -Match ([regex]::Escape(('Placeholder="' + $expectedPlaceholder + '"')) )
    }

    It 'Setup bootstrap initializes config in AllowIncomplete mode before menu renders' {
        $setupPath = Join-Path $PSScriptRoot '../certificate-setup.ps1'
        $setupRaw = Get-Content -LiteralPath $setupPath -Raw
        $setupRaw | Should -Match 'Initialize-CertificateConfig\s+-AllowIncomplete'
    }

}
