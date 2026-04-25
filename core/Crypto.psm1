Set-StrictMode -Version Latest

$script:DpapiEntropy = [System.Text.Encoding]::UTF8.GetBytes('certificate-dpapi-entropy-v1')

function ConvertTo-PlainText {
    param([Parameter(Mandatory)][SecureString]$SecureString)

    $bstr = [IntPtr]::Zero
    try {
        $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
        return [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    } finally {
        if ($bstr -ne [IntPtr]::Zero) {
            [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
    }
}

function ConvertTo-SecureStringValue {
    param([Parameter(Mandatory)][string]$PlainText)

    return ConvertTo-SecureString -String $PlainText -AsPlainText -Force
}

function Protect-DpapiValue {
    param(
        [Parameter(Mandatory)][string]$Plaintext,
        [ValidateSet('LocalMachine','CurrentUser')][string]$Scope = 'LocalMachine'
    )

    $scopeEnum = [System.Security.Cryptography.DataProtectionScope]::$Scope
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Plaintext)
    $protected = [System.Security.Cryptography.ProtectedData]::Protect($bytes, $script:DpapiEntropy, $scopeEnum)
    return [Convert]::ToBase64String($protected)
}

function Unprotect-DpapiValue {
    param(
        [Parameter(Mandatory)][string]$CiphertextBase64,
        [ValidateSet('LocalMachine','CurrentUser')][string]$Scope = 'LocalMachine'
    )

    try {
        $scopeEnum = [System.Security.Cryptography.DataProtectionScope]::$Scope
        $bytes = [Convert]::FromBase64String($CiphertextBase64)
        $plainBytes = [System.Security.Cryptography.ProtectedData]::Unprotect($bytes, $script:DpapiEntropy, $scopeEnum)
        return [System.Text.Encoding]::UTF8.GetString($plainBytes)
    } catch {
        throw "Unprotect-DpapiValue failed for scope '$Scope'. If the machine identity has changed, restore from backup using certificate-restore.ps1. Inner error: $($_.Exception.Message)"
    }
}

function New-AesKeyFromPassphrase {
    param(
        [Parameter(Mandatory)][SecureString]$Passphrase,
        [Parameter(Mandatory)][byte[]]$Salt,
        [int]$Iterations = 100000
    )

    $plain = ConvertTo-PlainText -SecureString $Passphrase
    try {
        $kdf = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($plain, $Salt, $Iterations, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
        return $kdf.GetBytes(32)
    } finally {
        $plain = $null
    }
}

function Protect-AesValue {
    param(
        [Parameter(Mandatory)][byte[]]$Plaintext,
        [Parameter(Mandatory)][byte[]]$Key
    )

    if ($Key.Length -ne 32) { throw 'Protect-AesValue requires a 32-byte key.' }

    $aes = [System.Security.Cryptography.Aes]::Create()
    try {
        $aes.Mode = [System.Security.Cryptography.CipherMode]::CBC
        $aes.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7
        $aes.KeySize = 256
        $aes.Key = $Key
        $aes.GenerateIV()

        $encryptor = $aes.CreateEncryptor()
        $cipher = $encryptor.TransformFinalBlock($Plaintext, 0, $Plaintext.Length)
        return @{ IV = $aes.IV; Ciphertext = $cipher }
    } finally {
        $aes.Dispose()
    }
}

function Unprotect-AesValue {
    param(
        [Parameter(Mandatory)][byte[]]$Ciphertext,
        [Parameter(Mandatory)][byte[]]$Key,
        [Parameter(Mandatory)][byte[]]$IV
    )

    if ($Key.Length -ne 32) { throw 'Unprotect-AesValue requires a 32-byte key.' }
    if ($IV.Length -ne 16) { throw 'Unprotect-AesValue requires a 16-byte IV.' }

    $aes = [System.Security.Cryptography.Aes]::Create()
    try {
        $aes.Mode = [System.Security.Cryptography.CipherMode]::CBC
        $aes.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7
        $aes.KeySize = 256
        $aes.Key = $Key
        $aes.IV = $IV

        $decryptor = $aes.CreateDecryptor()
        return $decryptor.TransformFinalBlock($Ciphertext, 0, $Ciphertext.Length)
    } catch {
        throw "Unprotect-AesValue failed: $($_.Exception.Message)"
    } finally {
        $aes.Dispose()
    }
}

function Get-Sha256 {
    param([Parameter(Mandatory)][string]$FilePath)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($FilePath)
        try {
            $hash = $sha.ComputeHash($stream)
        } finally {
            $stream.Dispose()
        }
        return ([System.BitConverter]::ToString($hash)).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

function Get-Sha256OfString {
    param([Parameter(Mandatory)][string]$Content)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Content)
        $hash = $sha.ComputeHash($bytes)
        return ([System.BitConverter]::ToString($hash)).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

function Get-RandomBytes {
    param([Parameter(Mandatory)][int]$Count)

    if ($Count -lt 1) { throw 'Count must be greater than zero.' }
    $bytes = New-Object byte[] $Count
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    } finally {
        $rng.Dispose()
    }
    return $bytes
}

Export-ModuleMember -Function @(
    'Protect-DpapiValue','Unprotect-DpapiValue','New-AesKeyFromPassphrase',
    'Protect-AesValue','Unprotect-AesValue','Get-Sha256','Get-Sha256OfString',
    'Get-RandomBytes','ConvertTo-PlainText','ConvertTo-SecureStringValue'
)
