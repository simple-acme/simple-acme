Import-Module "$PSScriptRoot/../core/Crypto.psm1" -Force

Describe 'Crypto module' {
    It 'DPAPI round-trip protect/unprotect returns original value' -Skip:(-not $IsWindows) {
        $plain = 'hello-secret'
        $cipher = Protect-DpapiValue -Plaintext $plain -Scope LocalMachine
        (Unprotect-DpapiValue -CiphertextBase64 $cipher -Scope LocalMachine) | Should -Be $plain
    }

    It 'DPAPI wrong scope throws when underlying unprotect fails' {
        Mock -CommandName ([System.Security.Cryptography.ProtectedData]::Unprotect) -MockWith { throw 'bad' }
        { Unprotect-DpapiValue -CiphertextBase64 ([Convert]::ToBase64String([byte[]](1,2,3))) -Scope CurrentUser } | Should -Throw
    }

    It 'AES round-trip returns original bytes' {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes('abc123')
        $key = Get-RandomBytes -Count 32
        $enc = Protect-AesValue -Plaintext $bytes -Key $key
        $dec = Unprotect-AesValue -Ciphertext $enc.Ciphertext -Key $key -IV $enc.IV
        [System.Text.Encoding]::UTF8.GetString($dec) | Should -Be 'abc123'
    }

    It 'AES IV differs between encryptions' {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes('same')
        $key = Get-RandomBytes -Count 32
        $one = Protect-AesValue -Plaintext $bytes -Key $key
        $two = Protect-AesValue -Plaintext $bytes -Key $key
        ([Convert]::ToBase64String($one.IV)) | Should -Not -Be ([Convert]::ToBase64String($two.IV))
    }

    It 'PBKDF2 deterministic with same parameters' {
        $salt = [byte[]](1..32)
        $pass = ConvertTo-SecureStringValue -PlainText 'pass'
        $k1 = New-AesKeyFromPassphrase -Passphrase $pass -Salt $salt -Iterations 100000
        $k2 = New-AesKeyFromPassphrase -Passphrase $pass -Salt $salt -Iterations 100000
        ([Convert]::ToBase64String($k1)) | Should -Be ([Convert]::ToBase64String($k2))
    }

    It "SHA-256 known vector for 'abc'" {
        Get-Sha256OfString -Content 'abc' | Should -Be 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad'
    }

    It 'Random bytes are correct length and differ' {
        $a = Get-RandomBytes -Count 16
        $b = Get-RandomBytes -Count 16
        $a.Length | Should -Be 16
        ([Convert]::ToBase64String($a)) | Should -Not -Be ([Convert]::ToBase64String($b))
    }
}
