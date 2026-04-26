Describe 'Backup/Restore scripts' {
    It 'Backup script exists and includes CERT magic write' {
        $content = Get-Content -Raw -Path "$PSScriptRoot/../certificate-backup.ps1"
        $content | Should -Match '0x43,0x45,0x52,0x54'
        $content | Should -Match '0x01'
    }

    It 'Restore script validates magic and decryption message' {
        $content = Get-Content -Raw -Path "$PSScriptRoot/../certificate-restore.ps1"
        $content | Should -Match 'valid Certificate backup'
        $content | Should -Match 'Decryption failed. Passphrase may be incorrect.'
    }

    It 'Restore script contains DryRun behavior' {
        (Get-Content -Raw -Path "$PSScriptRoot/../certificate-restore.ps1") | Should -Match 'Dry-run complete'
    }

    It 'Restore script writes DPAPI-encrypted device configs through Save-DeviceConfig' {
        (Get-Content -Raw -Path "$PSScriptRoot/../certificate-restore.ps1") | Should -Match 'Save-DeviceConfig'
    }

    It 'Restore script exposes Test-BackupIntegrity' {
        (Get-Content -Raw -Path "$PSScriptRoot/../certificate-restore.ps1") | Should -Match 'function Test-BackupIntegrity'
    }

    It 'Restore script prefers canonical CERTIFICATE_* keys and supports legacy fallback with warning' {
        $content = Get-Content -Raw -Path "$PSScriptRoot/../certificate-restore.ps1"
        $content | Should -Match '\$payload\.env\.CERTIFICATE_CONFIG_DIR'
        $content | Should -Match '\$payload\.env\.CERTIFICATE_API_KEY'
        $content | Should -Match 'Legacy key CERTIFICAAT_CONFIG_DIR detected'
        $content | Should -Match 'Legacy key CERTIFICAAT_API_KEY detected'
    }

    It 'Restore script writes only non-secret env keys and restores secure files' {
        $content = Get-Content -Raw -Path "$PSScriptRoot/../certificate-restore.ps1"
        $content | Should -Match 'CERTIFICATE_CONFIG_DIR\s*='
        $content | Should -Not -Match 'CERTIFICATE_API_KEY\s*='
        $content | Should -Match 'credentials.sec'
        $content | Should -Match 'env.secure'
    }
}
