Import-Module "$PSScriptRoot/../core/Crypto.psm1" -Force
Import-Module "$PSScriptRoot/../core/Config-Store.psm1" -Force

Describe 'Config store' {
    BeforeEach {
        $script:cfg = 'TestDrive:\cfg'
        New-Item -ItemType Directory -Path (Join-Path $script:cfg 'devices') -Force | Out-Null
    }

    It 'Save/load round-trip with encrypted secrets on disk' -Skip:(-not $IsWindows) {
        $device = @{ device_id='dev1'; connector_type='f5_bigip'; label='F5'; created_at='2026-01-01T00:00:00Z'; updated_at='2026-01-01T00:00:00Z'; settings=@{ host='a'; token='secret' } }
        Save-DeviceConfig -Device $device -ConfigDir $script:cfg -SecretFields @('token') | Out-Null
        $raw = Get-Content -Raw -Path (Join-Path $script:cfg 'devices/dev1.json')
        $raw | Should -Match '__DPAPI__:'
        $loaded = Get-DeviceConfig -DeviceId dev1 -ConfigDir $script:cfg
        $loaded.settings.token | Should -Be 'secret'
    }

    It 'Checksum sidecar is correct' {
        $device = @{ device_id='dev2'; connector_type='x'; label='X'; created_at='a'; updated_at='a'; settings=@{ host='h' } }
        Save-DeviceConfig -Device $device -ConfigDir $script:cfg | Out-Null
        $json = Get-Content -Raw -Path (Join-Path $script:cfg 'devices/dev2.json')
        $sha = Get-Content -Raw -Path (Join-Path $script:cfg 'devices/dev2.sha256')
        (Get-Sha256OfString -Content $json) | Should -Be $sha
    }

    It 'Tamper detection throws' {
        $device = @{ device_id='dev3'; connector_type='x'; label='X'; created_at='a'; updated_at='a'; settings=@{ host='h' } }
        Save-DeviceConfig -Device $device -ConfigDir $script:cfg | Out-Null
        Set-Content -Path (Join-Path $script:cfg 'devices/dev3.json') -Value '{"bad":1}' -Encoding UTF8
        { Get-DeviceConfig -DeviceId dev3 -ConfigDir $script:cfg } | Should -Throw '*Integrity check failed*'
    }

    It 'Non-secret fields remain plaintext' {
        $device = @{ device_id='dev4'; connector_type='x'; label='X'; created_at='a'; updated_at='a'; settings=@{ host='plain' } }
        Save-DeviceConfig -Device $device -ConfigDir $script:cfg -SecretFields @('token') | Out-Null
        (Get-Content -Raw -Path (Join-Path $script:cfg 'devices/dev4.json')) | Should -Match 'plain'
    }

    It 'Get all with skip integrity failures returns valid entries' {
        $d1 = @{ device_id='ok'; connector_type='x'; label='X'; created_at='a'; updated_at='a'; settings=@{ host='h' } }
        $d2 = @{ device_id='bad'; connector_type='x'; label='X'; created_at='a'; updated_at='a'; settings=@{ host='h' } }
        Save-DeviceConfig -Device $d1 -ConfigDir $script:cfg | Out-Null
        Save-DeviceConfig -Device $d2 -ConfigDir $script:cfg | Out-Null
        Set-Content -Path (Join-Path $script:cfg 'devices/bad.json') -Value '{"x":1}'
        $all = Get-AllDeviceConfigs -ConfigDir $script:cfg -SkipIntegrityFailures
        @($all).Count | Should -Be 1
        $all[0].device_id | Should -Be 'ok'
    }

    It 'Remove removes both files and Get returns null' {
        $device = @{ device_id='dev5'; connector_type='x'; label='X'; created_at='a'; updated_at='a'; settings=@{ host='h' } }
        Save-DeviceConfig -Device $device -ConfigDir $script:cfg | Out-Null
        Remove-DeviceConfig -DeviceId dev5 -ConfigDir $script:cfg
        (Test-Path (Join-Path $script:cfg 'devices/dev5.json')) | Should -BeFalse
        (Get-DeviceConfig -DeviceId dev5 -ConfigDir $script:cfg) | Should -Be $null
    }
}
