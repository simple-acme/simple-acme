Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module "$PSScriptRoot/core/Tui-Engine.psm1" -Force
Import-Module "$PSScriptRoot/setup/Form-Runner.psm1" -Force
. "$PSScriptRoot/setup/Menu-Tree.ps1"

$envPath = if ($env:CERTIFICAAT_CONFIG_DIR) { Join-Path $env:CERTIFICAAT_CONFIG_DIR 'certificaat.env' } else { Join-Path $PSScriptRoot 'certificaat.env' }
$configDir = if ($env:CERTIFICAAT_CONFIG_DIR) { $env:CERTIFICAAT_CONFIG_DIR } else { Join-Path $PSScriptRoot 'config' }
if (-not (Test-Path -LiteralPath $configDir)) { New-Item -ItemType Directory -Path $configDir -Force | Out-Null }

$menuStack = @($CertificaatMenuTree)
while ($menuStack.Count -gt 0) {
    $currentMenu = $menuStack[$menuStack.Count - 1]
    $selected = Show-TuiMenu -Menu $currentMenu

    if ($null -eq $selected -or $selected -eq 'exit') {
        if ($menuStack.Count -eq 1) { break }
        $menuStack = @($menuStack[0..($menuStack.Count - 2)])
        continue
    }

    $menuItem = $currentMenu.Items | Where-Object { $_.Key -eq $selected } | Select-Object -First 1
    if ($null -eq $menuItem) { continue }

    if ($menuItem.Type -eq 'submenu') {
        $menuStack += ,@{ Title = $menuItem.Label; Items = @($menuItem.Items + @{ Label='Back'; Key='exit'; Type='action' }) }
        continue
    }

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
