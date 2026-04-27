#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Firewall,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Username,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Password,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9._-]+$')]
    [string]$CertName,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$CertPath,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$KeyPath,

    [Parameter(Mandatory = $true)]
    [ValidateSet('waf', 'sslvpn', 'admin', 'userportal')]
    [string]$BindingType,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$BindingTarget,

    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$TemporaryCertificateName = 'Default',

    [Parameter(Mandatory = $false)]
    [ValidateRange(10, 600)]
    [int]$TimeoutSeconds = 120,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:RetryCount = 3
$script:LogDir = 'C:\ProgramData\acme-connector\logs'
$script:LogFile = Join-Path $script:LogDir ('deploy-sophos-{0}.log' -f (Get-Date -Format 'yyyyMMdd'))

function Write-StructuredLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Action,
        [Parameter(Mandatory = $true)][string]$Target,
        [Parameter(Mandatory = $true)][ValidateSet('success', 'fail', 'info')][string]$Result,
        [Parameter(Mandatory = $false)][string]$ErrorMessage,
        [Parameter(Mandatory = $false)][hashtable]$Details = @{}
    )

    if (-not (Test-Path -LiteralPath $script:LogDir)) {
        New-Item -Path $script:LogDir -ItemType Directory -Force | Out-Null
    }

    $entry = [ordered]@{
        timestamp = (Get-Date).ToUniversalTime().ToString('o')
        action    = $Action
        target    = $Target
        result    = $Result
        error     = $ErrorMessage
        details   = $Details
    }

    $entry | ConvertTo-Json -Depth 8 -Compress | Add-Content -Path $script:LogFile -Encoding UTF8
}

function Invoke-WithRetry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Operation,
        [Parameter(Mandatory = $true)][scriptblock]$ScriptBlock
    )

    for ($attempt = 1; $attempt -le $script:RetryCount; $attempt++) {
        try {
            return & $ScriptBlock
        } catch {
            $isFinal = $attempt -eq $script:RetryCount
            Write-StructuredLog -Action 'retry' -Target $Operation -Result ($(if ($isFinal) { 'fail' } else { 'info' })) -ErrorMessage $_.Exception.Message -Details @{
                attempt = $attempt
                maxAttempts = $script:RetryCount
                backoffSeconds = [int][Math]::Pow(2, $attempt)
            }

            if ($isFinal) { throw }
            Start-Sleep -Seconds ([int][Math]::Pow(2, $attempt))
        }
    }
}

function Unprotect-DpapiValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$CiphertextBase64,
        [ValidateSet('CurrentUser', 'LocalMachine')][string]$Scope = 'LocalMachine'
    )

    $entropy = [System.Text.Encoding]::UTF8.GetBytes('simple-acme-sophos-connector-v1')
    $cipherBytes = [Convert]::FromBase64String($CiphertextBase64)
    $scopeEnum = [System.Security.Cryptography.DataProtectionScope]::$Scope
    $plainBytes = [System.Security.Cryptography.ProtectedData]::Unprotect($cipherBytes, $entropy, $scopeEnum)
    return [System.Text.Encoding]::UTF8.GetString($plainBytes)
}

function Resolve-EncryptedPassword {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$PasswordInput)

    # Supported modes:
    # 1) Path to JSON payload: {"ciphertext":"...","scope":"LocalMachine"}
    # 2) Literal ciphertext base64 (assumes LocalMachine scope)
    if (Test-Path -LiteralPath $PasswordInput) {
        $raw = Get-Content -Path $PasswordInput -Raw -Encoding UTF8
        $payload = $raw | ConvertFrom-Json
        if (-not $payload.ciphertext) { throw 'Password JSON must contain ciphertext.' }
        $scope = if ($payload.scope) { [string]$payload.scope } else { 'LocalMachine' }
        return Unprotect-DpapiValue -CiphertextBase64 ([string]$payload.ciphertext) -Scope $scope
    }

    return Unprotect-DpapiValue -CiphertextBase64 $PasswordInput -Scope 'LocalMachine'
}

function Get-XmlEscaped {
    param([Parameter(Mandatory = $true)][string]$Text)
    return [System.Security.SecurityElement]::Escape($Text)
}

function Invoke-SophosApi {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Firewall,
        [Parameter(Mandatory = $true)][string]$Username,
        [Parameter(Mandatory = $true)][string]$PasswordPlain,
        [Parameter(Mandatory = $true)][string]$InnerXml,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $escapedUser = Get-XmlEscaped -Text $Username
    $escapedPass = Get-XmlEscaped -Text $PasswordPlain

    # Sophos Firewall XML API endpoint for XG/XGS
    $requestXml = @"
<Request>
  <Login>
    <Username>$escapedUser</Username>
    <Password>$escapedPass</Password>
  </Login>
  $InnerXml
</Request>
"@

    if ($DryRun) {
        Write-StructuredLog -Action 'api-dry-run' -Target $Firewall -Result 'info' -Details @{ body = $InnerXml }
        return [xml]'<Response APIVersion="1805.1"><Status code="200">Configuration applied successfully.</Status></Response>'
    }

    $apiUri = "https://$Firewall:4444/webconsole/APIController"

    $result = Invoke-WithRetry -Operation "Sophos API call" -ScriptBlock {
        Invoke-RestMethod -Uri $apiUri -Method Post -Body $requestXml -ContentType 'application/xml' -TimeoutSec $TimeoutSeconds
    }

    [xml]$xmlResult = $result
    if (-not $xmlResult.Response.Status) {
        throw 'Invalid XML response: missing Response/Status element.'
    }

    $statusText = [string]$xmlResult.Response.Status.'#text'
    if ($statusText -match 'Authentication Failure|Access denied|failed') {
        throw "Sophos API rejected request: $statusText"
    }

    return $xmlResult
}

function Get-CertificatePayload {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$CertPath,
        [Parameter(Mandatory = $true)][string]$KeyPath
    )

    if (-not (Test-Path -LiteralPath $CertPath)) { throw "Certificate file not found: $CertPath" }
    $certExt = [System.IO.Path]::GetExtension($CertPath).ToLowerInvariant()
    if ($certExt -eq '.pfx') {
        $pfxBytes = [System.IO.File]::ReadAllBytes($CertPath)
        $certObj = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($pfxBytes)
        return [pscustomobject]@{
            Format = 'pfx'
            CertificateData = [Convert]::ToBase64String($pfxBytes)
            PrivateKeyData = ''
            Fingerprint = ($certObj.Thumbprint).ToUpperInvariant()
            NotAfter = $certObj.NotAfter.ToUniversalTime().ToString('o')
        }
    }

    if (-not (Test-Path -LiteralPath $KeyPath)) { throw "Private key file not found: $KeyPath" }

    $certRaw = Get-Content -Path $CertPath -Raw -Encoding UTF8
    $keyRaw = Get-Content -Path $KeyPath -Raw -Encoding UTF8
    if ($certRaw -notmatch '-----BEGIN CERTIFICATE-----') { throw 'Certificate input must include PEM certificate block.' }

    $certObj = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem($certRaw, $keyRaw)
    return [pscustomobject]@{
        Format = 'pem'
        CertificateData = $certRaw.Trim()
        PrivateKeyData = $keyRaw.Trim()
        Fingerprint = ($certObj.Thumbprint).ToUpperInvariant()
        NotAfter = $certObj.NotAfter.ToUniversalTime().ToString('o')
    }
}

function Get-ExistingCertificate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Firewall,
        [Parameter(Mandatory = $true)][string]$Username,
        [Parameter(Mandatory = $true)][string]$PasswordPlain,
        [Parameter(Mandatory = $true)][string]$CertName,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $inner = @"
<Get>
  <Certificate>
    <Name>$(Get-XmlEscaped -Text $CertName)</Name>
  </Certificate>
</Get>
"@

    $response = Invoke-SophosApi -Firewall $Firewall -Username $Username -PasswordPlain $PasswordPlain -InnerXml $inner -TimeoutSeconds $TimeoutSeconds

    $certNode = $response.SelectSingleNode('//Certificate')
    if ($null -eq $certNode) { return $null }

    $fingerNode = $certNode.SelectSingleNode('Fingerprint')
    $fingerprint = if ($null -ne $fingerNode) { ([string]$fingerNode.InnerText).ToUpperInvariant() } else { $null }

    return [pscustomobject]@{
        Exists = $true
        Fingerprint = $fingerprint
    }
}

function Check-CertificateUsage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Firewall,
        [Parameter(Mandatory = $true)][string]$Username,
        [Parameter(Mandatory = $true)][string]$PasswordPlain,
        [Parameter(Mandatory = $true)][string]$CertName,
        [Parameter(Mandatory = $true)][ValidateSet('waf', 'sslvpn', 'admin', 'userportal')][string]$BindingType,
        [Parameter(Mandatory = $true)][string]$BindingTarget,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $queryXml = switch ($BindingType) {
        'waf' {
@"
<Get>
  <WebServerProtectionPolicy>
    <Name>$(Get-XmlEscaped -Text $BindingTarget)</Name>
  </WebServerProtectionPolicy>
</Get>
"@
        }
        'sslvpn' {
@"
<Get>
  <SSLVPN>
    <Name>SSLVPN</Name>
  </SSLVPN>
</Get>
"@
        }
        'admin' {
@"
<Get>
  <AdminSettings></AdminSettings>
</Get>
"@
        }
        'userportal' {
@"
<Get>
  <UserPortal></UserPortal>
</Get>
"@
        }
    }

    $response = Invoke-SophosApi -Firewall $Firewall -Username $Username -PasswordPlain $PasswordPlain -InnerXml $queryXml -TimeoutSeconds $TimeoutSeconds

    $currentCert = switch ($BindingType) {
        'waf' { [string]$response.SelectSingleNode('//WebServerProtectionPolicy/Certificate').InnerText }
        'sslvpn' { [string]$response.SelectSingleNode('//SSLVPN/Certificate').InnerText }
        'admin' { [string]$response.SelectSingleNode('//AdminSettings/Certificate').InnerText }
        'userportal' { [string]$response.SelectSingleNode('//UserPortal/Certificate').InnerText }
    }

    return [pscustomobject]@{
        IsInUse = ($currentCert -eq $CertName)
        CurrentCertificate = $currentCert
    }
}

function Set-TemporaryCertificate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Firewall,
        [Parameter(Mandatory = $true)][string]$Username,
        [Parameter(Mandatory = $true)][string]$PasswordPlain,
        [Parameter(Mandatory = $true)][ValidateSet('waf', 'sslvpn', 'admin', 'userportal')][string]$BindingType,
        [Parameter(Mandatory = $true)][string]$BindingTarget,
        [Parameter(Mandatory = $true)][string]$TemporaryCertificateName,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    Write-StructuredLog -Action 'detach' -Target "$BindingType/$BindingTarget" -Result 'info' -Details @{ temporaryCertificate = $TemporaryCertificateName }
    Bind-Certificate -Firewall $Firewall -Username $Username -PasswordPlain $PasswordPlain -BindingType $BindingType -BindingTarget $BindingTarget -CertName $TemporaryCertificateName -TimeoutSeconds $TimeoutSeconds
}

function Upload-Certificate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Firewall,
        [Parameter(Mandatory = $true)][string]$Username,
        [Parameter(Mandatory = $true)][string]$PasswordPlain,
        [Parameter(Mandatory = $true)][string]$CertName,
        [Parameter(Mandatory = $true)][pscustomobject]$CertificatePayload,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $certificateEscaped = Get-XmlEscaped -Text $CertificatePayload.CertificateData
    $privateKeyEscaped = Get-XmlEscaped -Text $CertificatePayload.PrivateKeyData

    # Update existing cert if present, otherwise add it.
    $innerXml = @"
<Set operation="update">
  <Certificate>
    <Name>$(Get-XmlEscaped -Text $CertName)</Name>
    <Action>UploadCertificate</Action>
    <CertificateFormat>$(Get-XmlEscaped -Text $CertificatePayload.Format)</CertificateFormat>
    <CertificateFile>$certificateEscaped</CertificateFile>
    <PrivateKeyFile>$privateKeyEscaped</PrivateKeyFile>
  </Certificate>
</Set>
"@

    $null = Invoke-SophosApi -Firewall $Firewall -Username $Username -PasswordPlain $PasswordPlain -InnerXml $innerXml -TimeoutSeconds $TimeoutSeconds
    Write-StructuredLog -Action 'upload-certificate' -Target $CertName -Result 'success' -Details @{ fingerprint = $CertificatePayload.Fingerprint; notAfter = $CertificatePayload.NotAfter }
}

function Bind-Certificate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Firewall,
        [Parameter(Mandatory = $true)][string]$Username,
        [Parameter(Mandatory = $true)][string]$PasswordPlain,
        [Parameter(Mandatory = $true)][ValidateSet('waf', 'sslvpn', 'admin', 'userportal')][string]$BindingType,
        [Parameter(Mandatory = $true)][string]$BindingTarget,
        [Parameter(Mandatory = $true)][string]$CertName,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $innerXml = switch ($BindingType) {
        'waf' {
@"
<Set operation="update">
  <WebServerProtectionPolicy>
    <Name>$(Get-XmlEscaped -Text $BindingTarget)</Name>
    <Certificate>$(Get-XmlEscaped -Text $CertName)</Certificate>
  </WebServerProtectionPolicy>
</Set>
"@
        }
        'sslvpn' {
@"
<Set operation="update">
  <SSLVPN>
    <Name>SSLVPN</Name>
    <Certificate>$(Get-XmlEscaped -Text $CertName)</Certificate>
  </SSLVPN>
</Set>
"@
        }
        'admin' {
@"
<Set operation="update">
  <AdminSettings>
    <Certificate>$(Get-XmlEscaped -Text $CertName)</Certificate>
  </AdminSettings>
</Set>
"@
        }
        'userportal' {
@"
<Set operation="update">
  <UserPortal>
    <Certificate>$(Get-XmlEscaped -Text $CertName)</Certificate>
  </UserPortal>
</Set>
"@
        }
    }

    $null = Invoke-SophosApi -Firewall $Firewall -Username $Username -PasswordPlain $PasswordPlain -InnerXml $innerXml -TimeoutSeconds $TimeoutSeconds
    Write-StructuredLog -Action 'bind-certificate' -Target "$BindingType/$BindingTarget" -Result 'success' -Details @{ certificate = $CertName }
}

function Validate-Deployment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Firewall,
        [Parameter(Mandatory = $true)][string]$Username,
        [Parameter(Mandatory = $true)][string]$PasswordPlain,
        [Parameter(Mandatory = $true)][string]$CertName,
        [Parameter(Mandatory = $true)][ValidateSet('waf', 'sslvpn', 'admin', 'userportal')][string]$BindingType,
        [Parameter(Mandatory = $true)][string]$BindingTarget,
        [Parameter(Mandatory = $true)][string]$ExpectedFingerprint,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $existing = Get-ExistingCertificate -Firewall $Firewall -Username $Username -PasswordPlain $PasswordPlain -CertName $CertName -TimeoutSeconds $TimeoutSeconds
    if ($null -eq $existing) {
        throw "Validation failed: certificate '$CertName' is not present on firewall."
    }

    if ($existing.Fingerprint -and $existing.Fingerprint -ne $ExpectedFingerprint) {
        throw "Validation failed: certificate fingerprint mismatch. Expected $ExpectedFingerprint got $($existing.Fingerprint)."
    }

    $usage = Check-CertificateUsage -Firewall $Firewall -Username $Username -PasswordPlain $PasswordPlain -CertName $CertName -BindingType $BindingType -BindingTarget $BindingTarget -TimeoutSeconds $TimeoutSeconds
    if (-not $usage.IsInUse) {
        throw "Validation failed: binding $BindingType/$BindingTarget not assigned to certificate '$CertName'. Current='$($usage.CurrentCertificate)'"
    }

    Write-StructuredLog -Action 'validate-deployment' -Target "$BindingType/$BindingTarget" -Result 'success' -Details @{ certificate = $CertName; fingerprint = $ExpectedFingerprint }
}

try {
    Write-StructuredLog -Action 'start' -Target $Firewall -Result 'info' -Details @{ certName = $CertName; bindingType = $BindingType; bindingTarget = $BindingTarget; dryRun = [bool]$DryRun }

    $plainPassword = Resolve-EncryptedPassword -PasswordInput $Password
    $payload = Get-CertificatePayload -CertPath $CertPath -KeyPath $KeyPath

    $existing = Get-ExistingCertificate -Firewall $Firewall -Username $Username -PasswordPlain $plainPassword -CertName $CertName -TimeoutSeconds $TimeoutSeconds
    if ($null -ne $existing -and $existing.Fingerprint -eq $payload.Fingerprint) {
        Write-StructuredLog -Action 'skip-upload' -Target $CertName -Result 'success' -Details @{ reason = 'fingerprint-match'; fingerprint = $payload.Fingerprint }
        exit 0
    }

    $usage = Check-CertificateUsage -Firewall $Firewall -Username $Username -PasswordPlain $plainPassword -CertName $CertName -BindingType $BindingType -BindingTarget $BindingTarget -TimeoutSeconds $TimeoutSeconds

    if ($usage.IsInUse) {
        Set-TemporaryCertificate -Firewall $Firewall -Username $Username -PasswordPlain $plainPassword -BindingType $BindingType -BindingTarget $BindingTarget -TemporaryCertificateName $TemporaryCertificateName -TimeoutSeconds $TimeoutSeconds
    }

    Upload-Certificate -Firewall $Firewall -Username $Username -PasswordPlain $plainPassword -CertName $CertName -CertificatePayload $payload -TimeoutSeconds $TimeoutSeconds

    Bind-Certificate -Firewall $Firewall -Username $Username -PasswordPlain $plainPassword -BindingType $BindingType -BindingTarget $BindingTarget -CertName $CertName -TimeoutSeconds $TimeoutSeconds

    Validate-Deployment -Firewall $Firewall -Username $Username -PasswordPlain $plainPassword -CertName $CertName -BindingType $BindingType -BindingTarget $BindingTarget -ExpectedFingerprint $payload.Fingerprint -TimeoutSeconds $TimeoutSeconds

    Write-StructuredLog -Action 'finish' -Target $Firewall -Result 'success' -Details @{ certName = $CertName }
    exit 0
} catch {
    Write-StructuredLog -Action 'finish' -Target $Firewall -Result 'fail' -ErrorMessage $_.Exception.Message
    throw
}
