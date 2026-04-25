Set-StrictMode -Version Latest

Import-Module "$PSScriptRoot/Crypto.psm1" -Force

function ConvertFrom-JsonToHashtable {
    param([Parameter(Mandatory)]$InputObject)

    if ($null -eq $InputObject) { return $null }
    if ($InputObject -is [System.Collections.IDictionary]) {
        $hash = @{}
        foreach ($key in $InputObject.Keys) { $hash[$key] = ConvertFrom-JsonToHashtable -InputObject $InputObject[$key] }
        return $hash
    }
    if ($InputObject -is [System.Collections.IEnumerable] -and -not ($InputObject -is [string])) {
        $items = @()
        foreach ($item in $InputObject) { $items += ,(ConvertFrom-JsonToHashtable -InputObject $item) }
        return $items
    }

    $props = $InputObject | Get-Member -MemberType NoteProperty
    if ($props.Count -gt 0) {
        $hash = @{}
        foreach ($prop in $props) { $hash[$prop.Name] = ConvertFrom-JsonToHashtable -InputObject $InputObject.$($prop.Name) }
        return $hash
    }

    return $InputObject
}

function Write-AtomicFile {
    param([string]$Path,[string]$Content)

    $tmp = "$Path.tmp"
    [System.IO.File]::WriteAllText($tmp, $Content, [System.Text.Encoding]::UTF8)
    if (Test-Path -LiteralPath $Path) { Remove-Item -Path $Path -Force }
    Move-Item -Path $tmp -Destination $Path -Force
}

function Save-DeviceConfig {
    param(
        [Parameter(Mandatory)][hashtable]$Device,
        [Parameter(Mandatory)][string]$ConfigDir,
        [string[]]$SecretFields = @()
    )

    $devicesDir = Join-Path $ConfigDir 'devices'
    if (-not (Test-Path -LiteralPath $devicesDir)) { New-Item -ItemType Directory -Path $devicesDir -Force | Out-Null }

    $deviceCopy = @{}
    foreach ($k in $Device.Keys) {
        if ($k -eq 'settings') {
            $settingsCopy = @{}
            foreach ($s in $Device.settings.Keys) { $settingsCopy[$s] = [string]$Device.settings[$s] }
            $deviceCopy[$k] = $settingsCopy
        } else {
            $deviceCopy[$k] = $Device[$k]
        }
    }

    foreach ($field in $SecretFields) {
        if ($deviceCopy.settings.ContainsKey($field) -and -not [string]::IsNullOrWhiteSpace($deviceCopy.settings[$field])) {
            $cipher = Protect-DpapiValue -Plaintext $deviceCopy.settings[$field]
            $deviceCopy.settings[$field] = "__DPAPI__:$cipher"
        }
    }

    $json = $deviceCopy | ConvertTo-Json -Depth 10
    $jsonPath = Join-Path $devicesDir ("{0}.json" -f $deviceCopy.device_id)
    Write-AtomicFile -Path $jsonPath -Content $json

    $sha = Get-Sha256OfString -Content $json
    $shaPath = Join-Path $devicesDir ("{0}.sha256" -f $deviceCopy.device_id)
    [System.IO.File]::WriteAllText($shaPath, $sha, [System.Text.Encoding]::UTF8)

    return [string]$deviceCopy.device_id
}

function Get-DeviceConfig {
    param(
        [Parameter(Mandatory)][string]$DeviceId,
        [Parameter(Mandatory)][string]$ConfigDir,
        [string[]]$SecretFields = @()
    )

    $devicesDir = Join-Path $ConfigDir 'devices'
    $jsonPath = Join-Path $devicesDir ("{0}.json" -f $DeviceId)
    $shaPath = Join-Path $devicesDir ("{0}.sha256" -f $DeviceId)

    if (-not (Test-Path -LiteralPath $jsonPath) -or -not (Test-Path -LiteralPath $shaPath)) { return $null }

    $json = [System.IO.File]::ReadAllText($jsonPath, [System.Text.Encoding]::UTF8)
    $expected = [System.IO.File]::ReadAllText($shaPath, [System.Text.Encoding]::UTF8).Trim()
    $actual = Get-Sha256OfString -Content $json

    if ($expected -ne $actual) {
        throw "Integrity check failed for device '$DeviceId'. File may have been tampered with. Restore from backup using certificate-restore.ps1 or reconfigure this device."
    }

    $obj = $json | ConvertFrom-Json
    $device = ConvertFrom-JsonToHashtable -InputObject $obj

    foreach ($k in @($device.settings.Keys)) {
        $v = [string]$device.settings[$k]
        if ($v.StartsWith('__DPAPI__:')) {
            $cipher = $v.Substring(10)
            $device.settings[$k] = Unprotect-DpapiValue -CiphertextBase64 $cipher
        }
    }

    return $device
}

function Get-AllDeviceConfigs {
    param(
        [Parameter(Mandatory)][string]$ConfigDir,
        [switch]$SkipIntegrityFailures
    )

    $devicesDir = Join-Path $ConfigDir 'devices'
    if (-not (Test-Path -LiteralPath $devicesDir)) { return @() }

    $results = @()
    foreach ($jsonFile in (Get-ChildItem -Path $devicesDir -Filter '*.json' -File | Sort-Object Name)) {
        $deviceId = [System.IO.Path]::GetFileNameWithoutExtension($jsonFile.Name)
        try {
            $config = Get-DeviceConfig -DeviceId $deviceId -ConfigDir $ConfigDir
            if ($null -ne $config) { $results += ,$config }
        } catch {
            if (-not $SkipIntegrityFailures) { throw }
            if (Get-Command Write-CertificateLog -ErrorAction SilentlyContinue) {
                Write-CertificateLog -Level ERROR -Message "Integrity check failed. This device will not receive certificate deployments until the config is restored or reconfigured. Run certificate-restore.ps1 or certificate-setup.ps1. File: $($jsonFile.FullName). Error: $($_.Exception.Message)"
            } else {
                Write-Error "Integrity check failed for '$($jsonFile.FullName)': $($_.Exception.Message)"
            }
        }
    }

    return $results
}

function Remove-DeviceConfig {
    param(
        [Parameter(Mandatory)][string]$DeviceId,
        [Parameter(Mandatory)][string]$ConfigDir
    )

    $devicesDir = Join-Path $ConfigDir 'devices'
    $jsonPath = Join-Path $devicesDir ("{0}.json" -f $DeviceId)
    $shaPath = Join-Path $devicesDir ("{0}.sha256" -f $DeviceId)

    if (-not (Test-Path -LiteralPath $jsonPath) -and -not (Test-Path -LiteralPath $shaPath)) {
        throw "Device '$DeviceId' not found."
    }

    if (Test-Path -LiteralPath $jsonPath) { Remove-Item -Path $jsonPath -Force }
    if (Test-Path -LiteralPath $shaPath) { Remove-Item -Path $shaPath -Force }
}

Export-ModuleMember -Function @('Save-DeviceConfig','Get-DeviceConfig','Get-AllDeviceConfigs','Remove-DeviceConfig')
