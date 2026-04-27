Describe 'Backup/Restore scripts' {
    It 'Backup script exists and includes CERT magic write' {
        $content = Get-Content -Raw -Path "$PSScriptRoot/../certificate-backup.ps1"
        $content | Should -Match '0x43,0x45,0x52,0x54'
        $content | Should -Match '0x01'
    }

    It 'Backup script includes inventory-based warnings and does not enforce secret presence' {
        $content = Get-Content -Raw -Path "$PSScriptRoot/../certificate-backup.ps1"
        $content | Should -Match 'certificate\\.env missing'
        $content | Should -Match 'simple-acme data directory not found'
        $content | Should -Match 'optional phase2 mappings not found'
        $content | Should -Not -Match \"required credential 'ACME_KID' is empty\"
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

    It 'Restore script supports secrets in certificate.env and restores secure files' {
        $content = Get-Content -Raw -Path "$PSScriptRoot/../certificate-restore.ps1"
        $content | Should -Match 'CERTIFICATE_CONFIG_DIR\s*='
        $content | Should -Match 'CERTIFICATE_API_KEY\s*='
        $content | Should -Match 'ACME_KID\s*='
        $content | Should -Match 'ACME_HMAC_SECRET\s*='
        $content | Should -Match 'credentials.sec'
        $content | Should -Match 'env.secure'
    }

    It 'Restore script emits tolerant summary output' {
        $content = Get-Content -Raw -Path "$PSScriptRoot/../certificate-restore.ps1"
        $content | Should -Match 'Restored:'
        $content | Should -Match 'certificate\\.env'
        $content | Should -Match 'simple-acme settings\\.json'
        $content | Should -Match 'phase2 config'
    }
}
