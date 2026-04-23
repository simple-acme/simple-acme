Set-StrictMode -Version Latest
Import-Module WebAdministration -ErrorAction Stop

function Import-IisPfx {
    param([hashtable]$Context)

    $pfxPath = [string]$Context.event.pfx_path
    if ([string]::IsNullOrWhiteSpace($pfxPath) -or -not (Test-Path -LiteralPath $pfxPath)) {
        throw 'IIS connector requires event.pfx_path to exist.'
    }

    $passwordEnv = [string]$Context.config.settings.pfx_password_env
    $password = if ([string]::IsNullOrWhiteSpace($passwordEnv)) { '' } else { [Environment]::GetEnvironmentVariable($passwordEnv) }

    $args = @('-f','-p',$password,'-importpfx',$pfxPath,'NoExport')
    $output = & certutil.exe @args 2>&1
    if ($LASTEXITCODE -ne 0) { throw "certutil import failed: $($output -join ' ')" }

    $thumbprint = [string]$Context.event.thumbprint
    if ([string]::IsNullOrWhiteSpace($thumbprint)) { throw 'IIS connector requires event.thumbprint.' }
    return $thumbprint.Replace(' ','').ToUpperInvariant()
}

function Set-IisBindingCertificate {
    param([string]$SiteName,[string]$Host,[int]$Port,[string]$Thumbprint)

    $bindingPath = "IIS:\SslBindings\0.0.0.0!$Port!$Host"
    if (Test-Path $bindingPath) { Remove-Item $bindingPath -Force }

    $cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Thumbprint -eq $Thumbprint } | Select-Object -First 1
    if ($null -eq $cert) { throw "Certificate thumbprint '$Thumbprint' not found in LocalMachine\\My." }

    New-Item -Path $bindingPath -Thumbprint $Thumbprint -SSLFlags 1 | Out-Null
}

function Invoke-IisProbe { param([hashtable]$Context)
    $siteName = [string]$Context.config.settings.site_name
    if ([string]::IsNullOrWhiteSpace($siteName)) { throw 'IIS connector setting site_name is required.' }
    $site = Get-Website -Name $siteName -ErrorAction Stop
    @{ reachable = $true; auth_valid = $true; detail = "IIS site '$($site.Name)' is present." }
}

function Invoke-IisDeploy { param([hashtable]$Context)
    $thumbprint = Import-IisPfx -Context $Context
    @{ artifact_ref = $thumbprint; detail = 'PFX imported into LocalMachine\\My.' }
}

function Invoke-IisBind { param([hashtable]$Context)
    $siteName = [string]$Context.config.settings.site_name
    $host = [string]$Context.config.settings.host
    if ([string]::IsNullOrWhiteSpace($host)) { $host = '' }
    $portRaw = [string]$Context.config.settings.port
    $port = if ([string]::IsNullOrWhiteSpace($portRaw)) { 443 } else { [int]$portRaw }
    Set-IisBindingCertificate -SiteName $siteName -Host $host -Port $port -Thumbprint $Context.artifact_ref
    @{ success = $true; detail = 'IIS SSL binding updated.' }
}

function Invoke-IisActivate { param([hashtable]$Context)
    iisreset /noforce | Out-Null
    @{ success = $true; detail = 'IIS service reloaded.' }
}

function Invoke-IisVerify { param([hashtable]$Context)
    $thumb = [string]$Context.artifact_ref
    $cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Thumbprint -eq $thumb } | Select-Object -First 1
    @{ verified = [bool]($null -ne $cert); detail = 'Certificate presence verified in LocalMachine\\My.' }
}

function Invoke-IisRollback { param([hashtable]$Context)
    if ([string]::IsNullOrWhiteSpace($Context.previous_artifact_ref)) { throw 'No previous_artifact_ref set for IIS rollback.' }
    $siteName = [string]$Context.config.settings.site_name
    $host = [string]$Context.config.settings.host
    if ([string]::IsNullOrWhiteSpace($host)) { $host = '' }
    $portRaw = [string]$Context.config.settings.port
    $port = if ([string]::IsNullOrWhiteSpace($portRaw)) { 443 } else { [int]$portRaw }
    Set-IisBindingCertificate -SiteName $siteName -Host $host -Port $port -Thumbprint $Context.previous_artifact_ref
    @{ success = $true; detail = 'IIS rollback binding restored.' }
}

function Invoke-IisConnectorProbe    { param([hashtable]$Context) Invoke-IisProbe    -Context $Context }
function Invoke-IisConnectorDeploy   { param([hashtable]$Context) Invoke-IisDeploy   -Context $Context }
function Invoke-IisConnectorBind     { param([hashtable]$Context) Invoke-IisBind     -Context $Context }
function Invoke-IisConnectorActivate { param([hashtable]$Context) Invoke-IisActivate -Context $Context }
function Invoke-IisConnectorVerify   { param([hashtable]$Context) Invoke-IisVerify   -Context $Context }
function Invoke-IisConnectorRollback { param([hashtable]$Context) Invoke-IisRollback -Context $Context }

Export-ModuleMember -Function Invoke-IisProbe,Invoke-IisDeploy,Invoke-IisBind,Invoke-IisActivate,Invoke-IisVerify,Invoke-IisRollback,Invoke-IisConnectorProbe,Invoke-IisConnectorDeploy,Invoke-IisConnectorBind,Invoke-IisConnectorActivate,Invoke-IisConnectorVerify,Invoke-IisConnectorRollback
