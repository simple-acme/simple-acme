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
        [string]$ExpectedModulePath,

        [Parameter(Mandatory = $true)]
        [System.Management.Automation.PSModuleInfo]$ModuleInfo
    )

    $moduleCommand = $ModuleInfo.ExportedCommands[$CommandName]
    if ($null -eq $moduleCommand) {
        throw @"
Required setup command '$CommandName' was not exported by module '$($ModuleInfo.Name)'.
Expected module path: $ExpectedModulePath
Resolved module path: $($ModuleInfo.Path)
Current script root: $PSScriptRoot
Re-deploy the setup modules and ensure you are running certificate-setup.ps1 from the correct repository root.
"@
    }

    $resolved = Get-Command $CommandName -ErrorAction SilentlyContinue
    if ($null -eq $resolved -or [string]::IsNullOrWhiteSpace([string]$resolved.Source) -or $resolved.Source -ne $ModuleInfo.Name) {
        throw @"
Required setup command '$CommandName' is unavailable after module import.
Expected module path: $ExpectedModulePath
Resolved module path: $($ModuleInfo.Path)
Current script root: $PSScriptRoot
This usually indicates a path mismatch, stale deployment, or incomplete copy.
Run this script from a full repository checkout and confirm the module file exists at the expected path.
"@
    }
}

$tuiModule = Import-Module $tuiEngineModulePath -Force -Global -PassThru
if ($null -eq $tuiModule) {
    throw "Unable to import required TUI module from path: $tuiEngineModulePath"
}
Assert-SetupCommandAvailable -CommandName 'Show-TuiMenu' -ExpectedModulePath $tuiEngineModulePath -ModuleInfo $tuiModule
Assert-SetupCommandAvailable -CommandName 'Show-TuiStatus' -ExpectedModulePath $tuiEngineModulePath -ModuleInfo $tuiModule

$formRunnerModule = Import-Module $formRunnerModulePath -Force -Global -PassThru
if ($null -eq $formRunnerModule) {
    throw "Unable to import required setup module from path: $formRunnerModulePath"
}
Assert-SetupCommandAvailable -CommandName 'Invoke-FirstRunWizard' -ExpectedModulePath $formRunnerModulePath -ModuleInfo $formRunnerModule
Assert-SetupCommandAvailable -CommandName 'Invoke-AcmeForm' -ExpectedModulePath $formRunnerModulePath -ModuleInfo $formRunnerModule
Assert-SetupCommandAvailable -CommandName 'Invoke-PolicyEditor' -ExpectedModulePath $formRunnerModulePath -ModuleInfo $formRunnerModule
Assert-SetupCommandAvailable -CommandName 'Invoke-DeviceForm' -ExpectedModulePath $formRunnerModulePath -ModuleInfo $formRunnerModule
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
    $selected = Show-TuiMenu -Menu $currentMenu -DisableSubmenuRecursion

    if ($null -eq $selected -or $selected -eq 'exit') {
        if ($menuStack.Count -eq 1) { break }
        $menuStack = @($menuStack[0..($menuStack.Count - 2)])
        continue
    }

    $menuItem = $currentMenu.Items | Where-Object { $_.Key -eq $selected } | Select-Object -First 1
    if ($null -eq $menuItem) { continue }

    if ($menuItem.Type -eq 'submenu') {
        $menuStack += ,@{ Title = $menuItem.Label; Items = @($menuItem.Items) }
        continue
    }

    switch ($selected) {
        'acme'           { Invoke-AcmeForm -EnvFilePath $envPath | Out-Null }
        'policies'       { Invoke-PolicyEditor -ConfigDir $configDir | Out-Null }
        'policies-view'  { Invoke-PolicyEditor -ConfigDir $configDir -ViewOnly | Out-Null }
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
            Show-TuiStatus -Message 'Java KeyStore connector is disabled: requires JDK/keytool.exe.' -Type Warning -Row ([Console]::WindowHeight-2)
            Start-Sleep -Milliseconds 1800
        }
        'vbr_cloud_gateway_info'         {
            Show-TuiStatus -Message 'Veeam VBR connector is disabled: requires VBR PowerShell module.' -Type Warning -Row ([Console]::WindowHeight-2)
            Start-Sleep -Milliseconds 1800
        }
        'azure_application_gateway_info' {
            Show-TuiStatus -Message 'Azure Application Gateway connector is disabled: requires AzureRM module.' -Type Warning -Row ([Console]::WindowHeight-2)
            Start-Sleep -Milliseconds 1800
        }
        'azure_ad_app_proxy_info'        {
            Show-TuiStatus -Message 'Azure AD App Proxy connector is disabled: requires AzureAD module.' -Type Warning -Row ([Console]::WindowHeight-2)
            Start-Sleep -Milliseconds 1800
        }
        default          { Invoke-DeviceForm -ConnectorType $selected -ConfigDir $configDir | Out-Null }
    }
}
