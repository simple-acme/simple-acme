Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:tuiModule = $null

$tuiEngineModulePath = Join-Path $PSScriptRoot 'core/Tui-Engine.psm1'
$formRunnerModulePath = Join-Path $PSScriptRoot 'setup/Form-Runner.psm1'

function Assert-SetupCommandAvailable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandName,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedModulePath
    )

    if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
        throw @"
Required setup command '$CommandName' is unavailable after module import.
Expected module path: $ExpectedModulePath
Current script root: $PSScriptRoot
This usually indicates a path mismatch, stale deployment, or incomplete copy.
Run this script from a full repository checkout and confirm the module file exists at the expected path.
"@
    }
}

$script:tuiModule = Import-Module "$PSScriptRoot/core/Tui-Engine.psm1" -Force -PassThru -ErrorAction Stop
if ($null -eq $script:tuiModule) { throw "TUI module failed to load from $PSScriptRoot/core/Tui-Engine.psm1" }
if (-not (Get-Command Show-TuiMenu -Module $script:tuiModule.Name -ErrorAction SilentlyContinue)) { throw "Show-TuiMenu not exported by module $($script:tuiModule.Name)" }

Import-Module $formRunnerModulePath -Force -Global
Assert-SetupCommandAvailable -CommandName 'Invoke-FirstRunWizard' -ExpectedModulePath $formRunnerModulePath
Assert-SetupCommandAvailable -CommandName 'Invoke-AcmeForm' -ExpectedModulePath $formRunnerModulePath
Assert-SetupCommandAvailable -CommandName 'Invoke-PolicyEditor' -ExpectedModulePath $formRunnerModulePath
. "$PSScriptRoot/setup/Menu-Tree.ps1"

$envPath = if ($env:CERTIFICATE_CONFIG_DIR) {
    Join-Path $env:CERTIFICATE_CONFIG_DIR 'certificate.env'
} else {
    Join-Path $PSScriptRoot 'certificate.env'
}

if (-not (Test-Path -LiteralPath $envPath)) {
    $envPath = Invoke-FirstRunWizard -DefaultEnvPath $envPath
    if (-not $envPath) { exit 0 }
}

. "$PSScriptRoot/config.ps1"
Initialize-CertificateConfig | Out-Null

$configDir = if ($env:CERTIFICATE_CONFIG_DIR) { $env:CERTIFICATE_CONFIG_DIR } else { Join-Path $PSScriptRoot 'config' }
if (-not (Test-Path -LiteralPath $configDir)) { New-Item -ItemType Directory -Path $configDir -Force | Out-Null }

$menuStack = @($CertificateMenuTree)
while ($menuStack.Count -gt 0) {
    $currentMenu = $menuStack[$menuStack.Count - 1]
    if ($null -eq $script:tuiModule) { throw "Internal error: tuiModule not initialized before menu rendering." }
    $selected = & $script:tuiModule { param($m) Show-TuiMenu -Menu $m } $currentMenu

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
        'acme'           { Invoke-AcmeForm -EnvFilePath $envPath | Out-Null }
        'policies'       { Invoke-PolicyEditor -ConfigDir $configDir | Out-Null }
        'backup-create'  { & "$PSScriptRoot/certificate-backup.ps1" -OutputPath (Join-Path $PSScriptRoot ("certificate-{0}.certbak" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))) }
        'backup-restore' {
            $path = Read-Host 'Backup path'
            if ($path) { & "$PSScriptRoot/certificate-restore.ps1" -BackupPath $path }
        }
        'backup-verify'  {
            $path = Read-Host 'Backup path'
            if ($path) { & "$PSScriptRoot/certificate-restore.ps1" -BackupPath $path -DryRun }
        }
        'java_keystore_info'             {
            & $script:tuiModule {
                param($message, $row)
                Show-TuiStatus -Message $message -Type Warning -Row $row
            } 'Java KeyStore connector is disabled: requires JDK/keytool.exe.' ([Console]::WindowHeight-2)
            Start-Sleep -Milliseconds 1800
        }
        'vbr_cloud_gateway_info'         {
            & $script:tuiModule {
                param($message, $row)
                Show-TuiStatus -Message $message -Type Warning -Row $row
            } 'Veeam VBR connector is disabled: requires VBR PowerShell module.' ([Console]::WindowHeight-2)
            Start-Sleep -Milliseconds 1800
        }
        'azure_application_gateway_info' {
            & $script:tuiModule {
                param($message, $row)
                Show-TuiStatus -Message $message -Type Warning -Row $row
            } 'Azure Application Gateway connector is disabled: requires AzureRM module.' ([Console]::WindowHeight-2)
            Start-Sleep -Milliseconds 1800
        }
        'azure_ad_app_proxy_info'        {
            & $script:tuiModule {
                param($message, $row)
                Show-TuiStatus -Message $message -Type Warning -Row $row
            } 'Azure AD App Proxy connector is disabled: requires AzureAD module.' ([Console]::WindowHeight-2)
            Start-Sleep -Milliseconds 1800
        }
        default          { Invoke-DeviceForm -ConnectorType $selected -ConfigDir $configDir | Out-Null }
    }
}
