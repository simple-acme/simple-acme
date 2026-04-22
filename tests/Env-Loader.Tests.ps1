Import-Module "$PSScriptRoot/../core/Env-Loader.psm1" -Force

Describe 'Env loader' {
    BeforeEach {
        $script:path = 'TestDrive:\certificaat.env'
        @(
            'ACME_DIRECTORY=https://acme.example.com/directory',
            'ACME_KID=kid',
            'ACME_HMAC_SECRET=secret',
            'DOMAINS=example.com',
            'CERTIFICAAT_CONFIG_DIR=C:\cfg',
            'CERTIFICAAT_DROP_DIR=C:\drop',
            'CERTIFICAAT_STATE_DIR=C:\state',
            'CERTIFICAAT_LOG_DIR=C:\log'
        ) | Set-Content -Path $script:path -Encoding UTF8
    }

    It 'Parse valid file' {
        $values = Read-EnvFile -Path $script:path
        $values.ACME_KID | Should -Be 'kid'
    }

    It 'Duplicate key throws with key and line' {
        @('A=1','A=2') | Set-Content -Path $script:path -Encoding UTF8
        { Read-EnvFile -Path $script:path } | Should -Throw '*A*line 2*'
    }

    It 'Value containing equals is preserved' {
        @('X=a=b=c') | Set-Content -Path $script:path -Encoding UTF8
        (Read-EnvFile -Path $script:path).X | Should -Be 'a=b=c'
    }

    It 'Quoted value strips outer quotes only' {
        @('X="  abc = #  "') | Set-Content -Path $script:path -Encoding UTF8
        (Read-EnvFile -Path $script:path).X | Should -Be '  abc = #  '
    }

    It 'Import missing required lists all in one error' {
        @('ACME_DIRECTORY=x') | Set-Content -Path $script:path -Encoding UTF8
        { Import-EnvFile -Path $script:path -Force } | Should -Throw '*ACME_KID*ACME_HMAC_SECRET*DOMAINS*'
    }

    It 'Import applies optional defaults' {
        $values = Import-EnvFile -Path $script:path -Force
        $values.CERTIFICAAT_VERIFY_MAX_ATTEMPTS | Should -Be '3'
    }

    It 'Import does not overwrite existing without force' {
        [Environment]::SetEnvironmentVariable('ACME_KID','preset')
        Import-EnvFile -Path $script:path | Out-Null
        [Environment]::GetEnvironmentVariable('ACME_KID') | Should -Be 'preset'
    }

    It 'Import force overwrites existing vars' {
        [Environment]::SetEnvironmentVariable('ACME_KID','preset')
        Import-EnvFile -Path $script:path -Force | Out-Null
        [Environment]::GetEnvironmentVariable('ACME_KID') | Should -Be 'kid'
    }

    It 'Write round-trips through Read-EnvFile' {
        $vals = @{ A='1'; B='2=3'; C='with # hash' }
        Write-EnvFile -Values $vals -Path $script:path
        $read = Read-EnvFile -Path $script:path
        $read.A | Should -Be '1'
        $read.B | Should -Be '2=3'
        $read.C | Should -Be 'with # hash'
    }

    It 'Write sets NTFS permissions on Windows' -Skip:(-not $IsWindows) {
        $vals = @{ A='1' }
        Write-EnvFile -Values $vals -Path $script:path
        $acl = Get-Acl -Path $script:path
        $acl.Access.IdentityReference.Value | Should -Contain 'NT AUTHORITY\SYSTEM'
    }
}
