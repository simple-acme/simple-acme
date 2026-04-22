Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module "$PSScriptRoot/core/Tui-Engine.psm1" -Force
Import-Module "$PSScriptRoot/setup/Form-Runner.psm1" -Force
. "$PSScriptRoot/setup/Menu-Tree.ps1"

# NOTE: Default environment file path is relative to config directory when available.
$envPath = if ($env:CERTIFICAAT_CONFIG_DIR) { Join-Path $env:CERTIFICAAT_CONFIG_DIR 'certificaat.env' } else { Join-Path $PSScriptRoot 'certificaat.env' }
$configDir = if ($env:CERTIFICAAT_CONFIG_DIR) { $env:CERTIFICAAT_CONFIG_DIR } else { Join-Path $PSScriptRoot 'config' }
if (-not (Test-Path -LiteralPath $configDir)) { New-Item -ItemType Directory -Path $configDir -Force | Out-Null }

$currentMenu = $CertificaatMenuTree
while ($true) {
    $selected = Show-TuiMenu -Menu $currentMenu
    if ($null -eq $selected -or $selected -eq 'exit') { break }

    switch ($selected) {
        'acme' { Invoke-AcmeForm -EnvFilePath $envPath | Out-Null }
        'policies' { Invoke-PolicyEditor -ConfigDir $configDir | Out-Null }
        'backup-create' { & "$PSScriptRoot/certificaat-backup.ps1" -OutputPath (Join-Path $PSScriptRoot ("certificaat-{0}.certbak" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))) }
        'backup-restore' {
            $path = Read-Host 'Backup path'
            if ($path) { & "$PSScriptRoot/certificaat-restore.ps1" -BackupPath $path }
        }
        'backup-verify' {
            $path = Read-Host 'Backup path'
            if ($path) { & "$PSScriptRoot/certificaat-restore.ps1" -BackupPath $path -DryRun }
        }
        default {
            Invoke-DeviceForm -ConnectorType $selected -ConfigDir $configDir | Out-Null
        }
    }
}
