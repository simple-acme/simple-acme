Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:tuiModule = $null

$tuiEngineModulePath = Join-Path $PSScriptRoot 'core/Tui-Engine.psm1'
$formRunnerModulePath = Join-Path $PSScriptRoot 'setup/Form-Runner.psm1'
$schedulerModulePath = Join-Path $PSScriptRoot 'core/Scheduler.psm1'
$envLoaderModulePath = Join-Path $PSScriptRoot 'core/Env-Loader.psm1'

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
Assert-SetupCommandAvailable -CommandName 'Invoke-AcmeSettingsMenu' -ExpectedModulePath $formRunnerModulePath -ModuleInfo $formRunnerModule
Assert-SetupCommandAvailable -CommandName 'Invoke-PolicyEditor' -ExpectedModulePath $formRunnerModulePath -ModuleInfo $formRunnerModule
Assert-SetupCommandAvailable -CommandName 'Invoke-PolicyViewer' -ExpectedModulePath $formRunnerModulePath -ModuleInfo $formRunnerModule
Assert-SetupCommandAvailable -CommandName 'Invoke-DeviceForm' -ExpectedModulePath $formRunnerModulePath -ModuleInfo $formRunnerModule
Assert-SetupCommandAvailable -CommandName 'Invoke-ManageCertificatesMenu' -ExpectedModulePath $formRunnerModulePath -ModuleInfo $formRunnerModule
Assert-SetupCommandAvailable -CommandName 'Invoke-ViewLogsDiagnostics' -ExpectedModulePath $formRunnerModulePath -ModuleInfo $formRunnerModule
Assert-SetupCommandAvailable -CommandName 'Show-SimpleAcmeDiagnosticSummary' -ExpectedModulePath $formRunnerModulePath -ModuleInfo $formRunnerModule
Assert-SetupCommandAvailable -CommandName 'Wait-ForOperatorReturn' -ExpectedModulePath $formRunnerModulePath -ModuleInfo $formRunnerModule
$schedulerModule = Import-Module $schedulerModulePath -Force -Global -PassThru
if ($null -eq $schedulerModule) {
    throw "Unable to import required scheduler module from path: $schedulerModulePath"
}
Assert-SetupCommandAvailable -CommandName 'Ensure-OrchestratorScheduledTask' -ExpectedModulePath $schedulerModulePath -ModuleInfo $schedulerModule
Import-Module $envLoaderModulePath -Force -Global | Out-Null
. "$PSScriptRoot/setup/Menu-Tree.ps1"

function Invoke-InitialAcmeReconcilePrompt {
    param(
        [Parameter(Mandatory)][string]$RootDir,
        [Parameter(Mandatory)][string]$EnvFilePath
    )

    $statusRow = [Math]::Max(0, [Console]::WindowHeight - 2)
    $envValues = Import-EnvFile -Path $EnvFilePath -Force

    if ([string]$envValues.ACME_PROVIDER -eq 'networking4all' -and [string]$envValues.ACME_DIRECTORY -notmatch 'networking4all\.com') {
        Show-TuiStatus -Message 'Internal state mismatch: selected provider is Networking4All but ACME_DIRECTORY is not Networking4All. Setup was not saved.' -Type Error -Row $statusRow
        Wait-ForOperatorReturn
        return
    }

    [Console]::WriteLine('')
    [Console]::WriteLine('Issuance ACME directory:')
    [Console]::WriteLine([string]$envValues.ACME_DIRECTORY)
    [Console]::WriteLine('')
    [Console]::WriteLine('Effective wacs command preview:')
    [Console]::WriteLine(("wacs.exe --accepttos --source manual --order single --baseuri {0} --validation none --globalvalidation none --host {1}" -f [string]$envValues.ACME_DIRECTORY, [string]$envValues.DOMAINS))
    if (-not [string]::IsNullOrWhiteSpace([string]$envValues.ACME_KID)) { [Console]::WriteLine('--eab-key-identifier <set>') }
    if (-not [string]::IsNullOrWhiteSpace([string]$envValues.ACME_HMAC_SECRET)) { [Console]::WriteLine('--eab-key <hidden>') }
    [Console]::WriteLine('')

    $answer = [string](Read-Host 'Run initial ACME reconcile now? [Y/N]')
    if ($answer.Trim().ToLowerInvariant() -notin @('y','yes')) {
        Show-TuiStatus -Message 'Skipped ACME reconcile. Run certificate-simple-acme-reconcile.ps1 later to bootstrap issuance.' -Type Warning -Row $statusRow
        Wait-ForOperatorReturn
        return
    }

    try {
        Import-Module (Join-Path $RootDir 'core/Simple-Acme-Reconciler.psm1') -Force | Out-Null
        $action = Invoke-SimpleAcmeReconcile -EnvValues $envValues
        Show-TuiStatus -Message "ACME reconcile completed successfully (action=$action)." -Type Success -Row $statusRow
        Show-SimpleAcmeDiagnosticSummary -ProjectRoot $RootDir -ShowNoobAdvice
        Invoke-PostSetupValidation -RootDir $RootDir -EnvValues $envValues
    } catch {
        Show-TuiStatus -Message "ACME reconcile failed: $($_.Exception.Message)" -Type Error -Row $statusRow
        Show-SimpleAcmeDiagnosticSummary -ProjectRoot $RootDir -ShowNoobAdvice
        Wait-ForOperatorReturn
    }
}

function Invoke-PostSetupValidation {
    param(
        [Parameter(Mandatory)][string]$RootDir,
        [Parameter(Mandatory)][hashtable]$EnvValues
    )

    $statusRow = [Math]::Max(0, [Console]::WindowHeight - 2)
    try {
        Import-Module (Join-Path $RootDir 'core/Simple-Acme-Reconciler.psm1') -Force | Out-Null
        $domains = Get-NormalizedDomains -Domains ([string]$EnvValues.DOMAINS)
        $renewals = @()
        foreach ($file in Get-RenewalFiles) {
            $summary = Get-RenewalSummary -File $file
            if (Test-ExactDomainSetMatch -Requested $domains -Actual $summary.Hosts) { $renewals += ,$summary }
        }
        if ($renewals.Count -lt 1) { throw 'No renewal JSON found for configured domains.' }
        $compare = Compare-RenewalWithEnv -RenewalSummary $renewals[0] -EnvValues $EnvValues
        if (-not $compare.Matches) { throw "Renewal JSON plugin mismatch: $($compare.Mismatches -join ', ')" }

        Show-TuiStatus -Message 'Post-setup validation passed (renewal JSON, plugins, and script wiring).' -Type Success -Row $statusRow
    } catch {
        Show-TuiStatus -Message "Post-setup validation warning: $($_.Exception.Message)" -Type Warning -Row $statusRow
        Wait-ForOperatorReturn
    }
}

function Invoke-OrchestratorTaskRegistration {
    param([Parameter(Mandatory)][string]$RootDir)

    $statusRow = [Math]::Max(0, [Console]::WindowHeight - 2)
    try {
        $envValues = Import-EnvFile -Path (Resolve-BootstrapEnvPath -ProjectRoot $RootDir) -Force
        $taskName = if (-not [string]::IsNullOrWhiteSpace([string]$envValues.CERTIFICATE_TASK_NAME)) { [string]$envValues.CERTIFICATE_TASK_NAME } else { 'Certificate-Orchestrator' }
        $interval = 5
        if (-not [string]::IsNullOrWhiteSpace([string]$envValues.CERTIFICATE_TASK_INTERVAL_MINUTES)) {
            if (-not [int]::TryParse([string]$envValues.CERTIFICATE_TASK_INTERVAL_MINUTES, [ref]$interval)) {
                throw "CERTIFICATE_TASK_INTERVAL_MINUTES must be an integer. Value: '$($envValues.CERTIFICATE_TASK_INTERVAL_MINUTES)'"
            }
        }
        $taskUser = if (-not [string]::IsNullOrWhiteSpace([string]$envValues.CERTIFICATE_TASK_USER)) { [string]$envValues.CERTIFICATE_TASK_USER } else { 'SYSTEM' }
        $psExe = if (-not [string]::IsNullOrWhiteSpace([string]$envValues.CERTIFICATE_TASK_POWERSHELL)) { [string]$envValues.CERTIFICATE_TASK_POWERSHELL } else { 'powershell.exe' }
        $scriptPath = Join-Path $RootDir 'certificate-orchestrator.ps1'
        $result = Ensure-OrchestratorScheduledTask -TaskName $taskName -ScriptPath $scriptPath -EveryMinutes $interval -TaskUser $taskUser -PowerShellExe $psExe
        Show-TuiStatus -Message "Scheduled task $($result.Action): $($result.TaskName) every $($result.EveryMinutes) minutes as $($result.TaskUser)." -Type Success -Row $statusRow
    } catch {
        Show-TuiStatus -Message "Scheduled task registration failed: $($_.Exception.Message)" -Type Error -Row $statusRow
    }
    Start-Sleep -Milliseconds 2200
}

$envPath = Resolve-BootstrapEnvPath -ProjectRoot $PSScriptRoot
$envPathSource = if ($env:CERTIFICATE_ENV_FILE) { 'CERTIFICATE_ENV_FILE override' } else { 'default project-root certificate.env' }
Write-Host ("Bootstrap env path: {0}" -f $envPath)
Write-Host ("Bootstrap env source: {0}" -f $envPathSource)

. "$PSScriptRoot/config.ps1"
Initialize-CertificateConfig -AllowIncomplete | Out-Null

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

    Clear-TuiScreen
    switch ($selected) {
        'setup-new'      {
            $result = Invoke-AcmeForm -EnvFilePath $envPath
            if ($null -ne $result) {
                Invoke-InitialAcmeReconcilePrompt -RootDir $PSScriptRoot -EnvFilePath $envPath
            }
        }
        'manage-certs'   { Invoke-ManageCertificatesMenu -ConfigDir $configDir }
        'acme'           {
            Invoke-AcmeSettingsMenu -EnvFilePath $envPath
        }
        'logs-diagnostics' { Invoke-ViewLogsDiagnostics -ProjectRoot $PSScriptRoot }
        'task-register'  { Invoke-OrchestratorTaskRegistration -RootDir $PSScriptRoot }
        'policies'       { Invoke-PolicyEditor -ConfigDir $configDir | Out-Null }
        'policies-view'  { Invoke-PolicyViewer -ConfigDir $configDir | Out-Null }
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
        default          {
            Show-TuiStatus -Message "No action implemented for '$selected'." -Type Warning -Row ([Console]::WindowHeight-2)
            Start-Sleep -Milliseconds 1200
        }
    }
    Clear-TuiScreen
}
