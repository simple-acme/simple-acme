Describe 'Backup/Restore scripts' {
    It 'Backup script exists and includes CERT magic write' {
        $content = Get-Content -Raw -Path "$PSScriptRoot/../certificaat-backup.ps1"
        $content | Should -Match '0x43,0x45,0x52,0x54'
        $content | Should -Match '0x01'
    }

    It 'Restore script validates magic and decryption message' {
        $content = Get-Content -Raw -Path "$PSScriptRoot/../certificaat-restore.ps1"
        $content | Should -Match 'valid Certificaat backup'
        $content | Should -Match 'Decryption failed. Passphrase may be incorrect.'
    }

    It 'Restore script contains DryRun behavior' {
        (Get-Content -Raw -Path "$PSScriptRoot/../certificaat-restore.ps1") | Should -Match 'Dry-run complete'
    }

    It 'Restore script writes DPAPI-encrypted device configs through Save-DeviceConfig' {
        (Get-Content -Raw -Path "$PSScriptRoot/../certificaat-restore.ps1") | Should -Match 'Save-DeviceConfig'
    }

    It 'Restore script exposes Test-BackupIntegrity' {
        (Get-Content -Raw -Path "$PSScriptRoot/../certificaat-restore.ps1") | Should -Match 'function Test-BackupIntegrity'
    }
}
