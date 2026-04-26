#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Firewall,

    [Parameter(Mandatory = $false)]
    [string]$ApiKey,

    [Parameter(Mandatory = $false)]
    [string]$ApiKeySecureFile,

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
    [ValidateSet('waf', 'globalprotect', 'management', 'ssl-decrypt')]
    [string]$BindingType,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$BindingTarget,

    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$Vsys = 'vsys1',

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, 600)]
    [int]$TimeoutSeconds = 120,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun,

    [Parameter(Mandatory = $false)]
    [switch]$VerboseLog
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:RetryCount = 3
$script:LogDir = if ($IsWindows) { 'C:\ProgramData\acme-connector\logs' } else { Join-Path $PSScriptRoot 'logs' }
$script:LogFile = Join-Path $script:LogDir ("deploy-paloalto-{0}.log" -f (Get-Date -Format 'yyyyMMdd'))

function Write-StructuredLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Action,
        [Parameter(Mandatory = $true)][string]$Target,
        [Parameter(Mandatory = $true)][ValidateSet('success', 'fail', 'info')][string]$Result,
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
        details   = $Details
    }

    $json = $entry | ConvertTo-Json -Depth 8 -Compress
    Add-Content -Path $script:LogFile -Value $json -Encoding UTF8
    if ($VerboseLog) { Write-Host $json }
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
            Write-StructuredLog -Action 'retry' -Target $Operation -Result ($(if ($isFinal) { 'fail' } else { 'info' })) -Details @{
                attempt = $attempt
                maxAttempts = $script:RetryCount
                error = $_.Exception.Message
            }
            if ($isFinal) { throw }
            $delay = [Math]::Pow(2, $attempt)
            Start-Sleep -Seconds $delay
        }
    }
}

function Unprotect-DpapiValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$CiphertextBase64,
        [ValidateSet('LocalMachine', 'CurrentUser')][string]$Scope = 'LocalMachine'
    )

    $entropy = [System.Text.Encoding]::UTF8.GetBytes('certificate-dpapi-entropy-v1')
    $scopeEnum = [System.Security.Cryptography.DataProtectionScope]::$Scope
    $bytes = [Convert]::FromBase64String($CiphertextBase64)
    $plainBytes = [System.Security.Cryptography.ProtectedData]::Unprotect($bytes, $entropy, $scopeEnum)
    [System.Text.Encoding]::UTF8.GetString($plainBytes)
}

function Resolve-ApiKey {
    [CmdletBinding()]
    param(
        [string]$RawApiKey,
        [string]$EncryptedFile
    )

    if (-not [string]::IsNullOrWhiteSpace($RawApiKey)) {
        return $RawApiKey
    }

    if ([string]::IsNullOrWhiteSpace($EncryptedFile)) {
        throw 'ApiKey or ApiKeySecureFile is required.'
    }

    if (-not (Test-Path -LiteralPath $EncryptedFile)) {
        throw "Encrypted API key file not found: $EncryptedFile"
    }

    $raw = Get-Content -Path $EncryptedFile -Raw -Encoding UTF8
    $payload = $raw | ConvertFrom-Json
    if (-not $payload.ciphertext -or -not $payload.scope) {
        throw 'Invalid encrypted API key file. Expected JSON with ciphertext and scope.'
    }

    return Unprotect-DpapiValue -CiphertextBase64 $payload.ciphertext -Scope $payload.scope
}

function Invoke-PanApi {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Firewall,
        [Parameter(Mandatory = $true)][string]$ApiKey,
        [Parameter(Mandatory = $true)][ValidateSet('GET','POST')][string]$Method,
        [Parameter(Mandatory = $true)][hashtable]$Query,
        [Parameter(Mandatory = $false)][hashtable]$Form,
        [Parameter(Mandatory = $false)][string]$FilePath,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $uriBuilder = [System.UriBuilder]::new("https://$Firewall/api/")
    $queryPairs = New-Object System.Collections.Generic.List[string]
    foreach ($k in $Query.Keys) {
        $queryPairs.Add(('{0}={1}' -f [System.Uri]::EscapeDataString($k), [System.Uri]::EscapeDataString([string]$Query[$k])))
    }
    $queryPairs.Add(('key={0}' -f [System.Uri]::EscapeDataString($ApiKey)))
    $uriBuilder.Query = ($queryPairs -join '&')

    $invoke = {
        if ($DryRun) {
            Write-StructuredLog -Action 'api-dry-run' -Target $uriBuilder.Uri.AbsoluteUri -Result 'info' -Details @{ method = $Method }
            return [xml]'<response status="success" code="19"><msg>dry-run</msg></response>'
        }

        if ($Method -eq 'GET') {
            return Invoke-RestMethod -Method Get -Uri $uriBuilder.Uri.AbsoluteUri -TimeoutSec $TimeoutSeconds
        }

        if ($null -ne $FilePath) {
            $fileName = [System.IO.Path]::GetFileName($FilePath)
            $bytes = [System.IO.File]::ReadAllBytes($FilePath)
            $boundary = [System.Guid]::NewGuid().ToString('N')
            $crlf = "`r`n"
            $header = "--$boundary$crlf" +
                "Content-Disposition: form-data; name=\"file\"; filename=\"$fileName\"$crlf" +
                "Content-Type: application/octet-stream$crlf$crlf"
            $footer = "$crlf--$boundary--$crlf"
            $headerBytes = [System.Text.Encoding]::ASCII.GetBytes($header)
            $footerBytes = [System.Text.Encoding]::ASCII.GetBytes($footer)
            $bodyBytes = New-Object byte[] ($headerBytes.Length + $bytes.Length + $footerBytes.Length)
            [Array]::Copy($headerBytes, 0, $bodyBytes, 0, $headerBytes.Length)
            [Array]::Copy($bytes, 0, $bodyBytes, $headerBytes.Length, $bytes.Length)
            [Array]::Copy($footerBytes, 0, $bodyBytes, $headerBytes.Length + $bytes.Length, $footerBytes.Length)
            return Invoke-RestMethod -Method Post -Uri $uriBuilder.Uri.AbsoluteUri -Body $bodyBytes -ContentType "multipart/form-data; boundary=$boundary" -TimeoutSec $TimeoutSeconds
        }

        return Invoke-RestMethod -Method Post -Uri $uriBuilder.Uri.AbsoluteUri -Body $Form -TimeoutSec $TimeoutSeconds
    }

    $response = Invoke-WithRetry -Operation "PAN API $Method $($Query.type)" -ScriptBlock $invoke
    if ($response -is [xml]) {
        if ($response.response.status -ne 'success') {
            throw "PAN API failed: $($response.response.msg.InnerText)"
        }
        return $response
    }

    $xmlResponse = [xml]$response.OuterXml
    if ($xmlResponse.response.status -ne 'success') {
        throw "PAN API failed: $($xmlResponse.response.msg.InnerText)"
    }
    return $xmlResponse
}

function Get-XmlEscaped {
    param([Parameter(Mandatory = $true)][string]$Text)
    [System.Security.SecurityElement]::Escape($Text)
}

function Get-CertificateInfo {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$CertPath,
        [Parameter(Mandatory = $true)][string]$KeyPath
    )

    foreach ($path in @($CertPath, $KeyPath)) {
        if (-not (Test-Path -LiteralPath $path)) { throw "Required file does not exist: $path" }
        $raw = Get-Content -Path $path -Raw -Encoding UTF8
        if ([string]::IsNullOrWhiteSpace($raw)) { throw "Required file is empty: $path" }
    }

    $pemRaw = Get-Content -Path $CertPath -Raw -Encoding UTF8
    $match = [regex]::Match($pemRaw, '-----BEGIN CERTIFICATE-----(?<b64>[\s\S]+?)-----END CERTIFICATE-----')
    if (-not $match.Success) { throw 'Certificate file does not contain a valid PEM certificate block.' }

    $base64 = ($match.Groups['b64'].Value -replace '\s', '')
    $certBytes = [Convert]::FromBase64String($base64)
    $cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certBytes)

    return [pscustomobject]@{
        Certificate = $cert
        Thumbprint  = ($cert.Thumbprint -replace '\s', '').ToUpperInvariant()
    }
}

function Get-ExistingCertificateFingerprint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Firewall,
        [Parameter(Mandatory = $true)][string]$ApiKey,
        [Parameter(Mandatory = $true)][string]$CertName,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $xpath = "/config/shared/certificate/entry[@name='$(Get-XmlEscaped -Text $CertName)']"
    $resp = Invoke-PanApi -Firewall $Firewall -ApiKey $ApiKey -Method GET -Query @{ type = 'config'; action = 'show'; xpath = $xpath } -TimeoutSeconds $TimeoutSeconds
    $certNode = $resp.response.result.entry
    if ($null -eq $certNode) { return $null }

    if ($certNode.'fingerprint') {
        return ([string]$certNode.'fingerprint').Replace(':', '').ToUpperInvariant()
    }

    return $null
}

function Upload-Certificate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Firewall,
        [Parameter(Mandatory = $true)][string]$ApiKey,
        [Parameter(Mandatory = $true)][string]$CertName,
        [Parameter(Mandatory = $true)][string]$CertPath,
        [Parameter(Mandatory = $true)][string]$KeyPath,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    Write-StructuredLog -Action 'upload-certificate' -Target $Firewall -Result 'info' -Details @{ certName = $CertName }

    $certQuery = @{ type = 'import'; category = 'certificate'; certificate-name = $CertName; format = 'pem' }
    $keyQuery = @{ type = 'import'; category = 'private-key'; certificate-name = $CertName; format = 'pem'; passphrase = '' }

    $null = Invoke-PanApi -Firewall $Firewall -ApiKey $ApiKey -Method POST -Query $certQuery -FilePath $CertPath -TimeoutSeconds $TimeoutSeconds
    $null = Invoke-PanApi -Firewall $Firewall -ApiKey $ApiKey -Method POST -Query $keyQuery -FilePath $KeyPath -TimeoutSeconds $TimeoutSeconds

    Write-StructuredLog -Action 'upload-certificate' -Target $Firewall -Result 'success' -Details @{ certName = $CertName }
}

function Set-SSLProfile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Firewall,
        [Parameter(Mandatory = $true)][string]$ApiKey,
        [Parameter(Mandatory = $true)][string]$CertName,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $profileName = "$CertName-tls-profile"
    $profileXPath = "/config/shared/ssl-tls-service-profile/entry[@name='$(Get-XmlEscaped -Text $profileName)']"
    $element = "<entry name='$(Get-XmlEscaped -Text $profileName)'><protocol-settings><min-version>tls1-2</min-version><max-version>tls1-3</max-version></protocol-settings><certificate><member>$(Get-XmlEscaped -Text $CertName)</member></certificate></entry>"

    Write-StructuredLog -Action 'set-ssl-profile' -Target $profileName -Result 'info' -Details @{}
    $null = Invoke-PanApi -Firewall $Firewall -ApiKey $ApiKey -Method GET -Query @{ type = 'config'; action = 'set'; xpath = $profileXPath; element = $element } -TimeoutSeconds $TimeoutSeconds
    Write-StructuredLog -Action 'set-ssl-profile' -Target $profileName -Result 'success' -Details @{ minVersion = 'tls1-2'; maxVersion = 'tls1-3' }

    return $profileName
}

function Resolve-BindingXPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][ValidateSet('waf', 'globalprotect', 'management', 'ssl-decrypt')][string]$BindingType,
        [Parameter(Mandatory = $true)][string]$BindingTarget,
        [Parameter(Mandatory = $true)][string]$Vsys
    )

    switch ($BindingType) {
        'waf' {
            return "/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='$(Get-XmlEscaped -Text $Vsys)']/rulebase/decryption/rules/entry[@name='$(Get-XmlEscaped -Text $BindingTarget)']/ssl-tls-service-profile"
        }
        'globalprotect' {
            if ($BindingTarget -notmatch '^(portal|gateway):(.+)$') {
                throw "globalprotect BindingTarget must be formatted as 'portal:<name>' or 'gateway:<name>'"
            }
            $kind = $Matches[1]
            $name = $Matches[2]
            if ($kind -eq 'portal') {
                return "/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='$(Get-XmlEscaped -Text $Vsys)']/global-protect/global-protect-portal/entry[@name='$(Get-XmlEscaped -Text $name)']/ssl-tls-service-profile"
            }
            return "/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='$(Get-XmlEscaped -Text $Vsys)']/global-protect/global-protect-gateway/entry[@name='$(Get-XmlEscaped -Text $name)']/ssl-tls-service-profile"
        }
        'management' {
            return "/config/devices/entry[@name='localhost.localdomain']/deviceconfig/system/ssl-tls-service-profile"
        }
        'ssl-decrypt' {
            return "/config/shared/ssl-decrypt/ssl-tls-service-profile"
        }
    }
}

function Bind-Certificate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Firewall,
        [Parameter(Mandatory = $true)][string]$ApiKey,
        [Parameter(Mandatory = $true)][string]$ProfileName,
        [Parameter(Mandatory = $true)][ValidateSet('waf', 'globalprotect', 'management', 'ssl-decrypt')][string]$BindingType,
        [Parameter(Mandatory = $true)][string]$BindingTarget,
        [Parameter(Mandatory = $true)][string]$Vsys,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $bindingXPath = Resolve-BindingXPath -BindingType $BindingType -BindingTarget $BindingTarget -Vsys $Vsys
    $element = "<ssl-tls-service-profile>$(Get-XmlEscaped -Text $ProfileName)</ssl-tls-service-profile>"

    Write-StructuredLog -Action 'bind-certificate' -Target $bindingXPath -Result 'info' -Details @{ bindingType = $BindingType; bindingTarget = $BindingTarget }
    $null = Invoke-PanApi -Firewall $Firewall -ApiKey $ApiKey -Method GET -Query @{ type = 'config'; action = 'set'; xpath = $bindingXPath; element = $element } -TimeoutSeconds $TimeoutSeconds
    Write-StructuredLog -Action 'bind-certificate' -Target $bindingXPath -Result 'success' -Details @{ profile = $ProfileName }

    return $bindingXPath
}

function Commit-Changes {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Firewall,
        [Parameter(Mandatory = $true)][string]$ApiKey,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    Write-StructuredLog -Action 'commit-start' -Target $Firewall -Result 'info' -Details @{}
    $commitResponse = Invoke-PanApi -Firewall $Firewall -ApiKey $ApiKey -Method GET -Query @{ type = 'commit'; cmd = '<commit></commit>' } -TimeoutSeconds $TimeoutSeconds

    $jobId = [string]$commitResponse.response.result.job
    if ([string]::IsNullOrWhiteSpace($jobId)) {
        throw 'Commit did not return a job id.'
    }

    $maxPolls = 90
    for ($poll = 1; $poll -le $maxPolls; $poll++) {
        Start-Sleep -Seconds 2
        $job = Invoke-PanApi -Firewall $Firewall -ApiKey $ApiKey -Method GET -Query @{ type = 'op'; cmd = "<show><jobs><id>$jobId</id></jobs></show>" } -TimeoutSeconds $TimeoutSeconds
        $status = [string]$job.response.result.job.status
        $result = [string]$job.response.result.job.result

        if ($status -eq 'FIN' -or $status -eq 'FIN OK') {
            if ($result -eq 'OK') {
                Write-StructuredLog -Action 'commit-complete' -Target $Firewall -Result 'success' -Details @{ jobId = $jobId }
                return $jobId
            }

            if ($result -eq 'PEND' -or $result -eq 'FAIL') {
                throw "Commit job $jobId ended with result '$result'"
            }
        }

        if ($status -eq 'ACT' -and $result -eq 'PEND') {
            continue
        }
    }

    throw "Commit job $jobId did not finish within polling window."
}

function Validate-Deployment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Firewall,
        [Parameter(Mandatory = $true)][string]$ApiKey,
        [Parameter(Mandatory = $true)][string]$ProfileName,
        [Parameter(Mandatory = $true)][string]$CertName,
        [Parameter(Mandatory = $true)][string]$BindingXPath,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $profileXPath = "/config/shared/ssl-tls-service-profile/entry[@name='$(Get-XmlEscaped -Text $ProfileName)']"
    $profileResp = Invoke-PanApi -Firewall $Firewall -ApiKey $ApiKey -Method GET -Query @{ type = 'config'; action = 'show'; xpath = $profileXPath } -TimeoutSeconds $TimeoutSeconds
    $profileCert = [string]$profileResp.response.result.entry.certificate.member
    if ($profileCert -ne $CertName) {
        throw "Validation failed: profile '$ProfileName' references '$profileCert' instead of '$CertName'."
    }

    $bindResp = Invoke-PanApi -Firewall $Firewall -ApiKey $ApiKey -Method GET -Query @{ type = 'config'; action = 'show'; xpath = $BindingXPath } -TimeoutSeconds $TimeoutSeconds
    $boundValue = [string]$bindResp.response.result.'ssl-tls-service-profile'
    if ([string]::IsNullOrWhiteSpace($boundValue)) {
        $boundValue = [string]$bindResp.response.result
    }

    if ($boundValue -notmatch [regex]::Escape($ProfileName)) {
        throw "Validation failed: binding '$BindingXPath' does not contain profile '$ProfileName'."
    }

    Write-StructuredLog -Action 'validate-deployment' -Target $Firewall -Result 'success' -Details @{ profile = $ProfileName; bindingXPath = $BindingXPath }
}

try {
    Write-StructuredLog -Action 'start' -Target $Firewall -Result 'info' -Details @{ certName = $CertName; bindingType = $BindingType; dryRun = [bool]$DryRun }

    $resolvedApiKey = Resolve-ApiKey -RawApiKey $ApiKey -EncryptedFile $ApiKeySecureFile
    $certInfo = Get-CertificateInfo -CertPath $CertPath -KeyPath $KeyPath
    Write-StructuredLog -Action 'certificate-loaded' -Target $CertPath -Result 'success' -Details @{ thumbprint = $certInfo.Thumbprint; notAfter = $certInfo.Certificate.NotAfter.ToUniversalTime().ToString('o') }

    $existingFingerprint = Get-ExistingCertificateFingerprint -Firewall $Firewall -ApiKey $resolvedApiKey -CertName $CertName -TimeoutSeconds $TimeoutSeconds
    if ($existingFingerprint -and ($existingFingerprint -eq $certInfo.Thumbprint)) {
        Write-StructuredLog -Action 'idempotency-check' -Target $CertName -Result 'success' -Details @{ decision = 'no-op'; reason = 'fingerprint-match' }
        exit 0
    }

    Write-StructuredLog -Action 'idempotency-check' -Target $CertName -Result 'info' -Details @{ decision = 'deploy'; existing = $existingFingerprint; incoming = $certInfo.Thumbprint }

    Upload-Certificate -Firewall $Firewall -ApiKey $resolvedApiKey -CertName $CertName -CertPath $CertPath -KeyPath $KeyPath -TimeoutSeconds $TimeoutSeconds
    $profileName = Set-SSLProfile -Firewall $Firewall -ApiKey $resolvedApiKey -CertName $CertName -TimeoutSeconds $TimeoutSeconds
    $bindingXPath = Bind-Certificate -Firewall $Firewall -ApiKey $resolvedApiKey -ProfileName $profileName -BindingType $BindingType -BindingTarget $BindingTarget -Vsys $Vsys -TimeoutSeconds $TimeoutSeconds

    $commitJob = Commit-Changes -Firewall $Firewall -ApiKey $resolvedApiKey -TimeoutSeconds $TimeoutSeconds
    Validate-Deployment -Firewall $Firewall -ApiKey $resolvedApiKey -ProfileName $profileName -CertName $CertName -BindingXPath $bindingXPath -TimeoutSeconds $TimeoutSeconds

    Write-StructuredLog -Action 'completed' -Target $Firewall -Result 'success' -Details @{ commitJob = $commitJob; profileName = $profileName }
    exit 0
} catch {
    Write-StructuredLog -Action 'completed' -Target $Firewall -Result 'fail' -Details @{ error = $_.Exception.Message; stack = $_.ScriptStackTrace }
    throw
}
