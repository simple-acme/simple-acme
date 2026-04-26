Import-Module "$PSScriptRoot/../core/Env-Loader.psm1" -Force

Describe 'Env loader' {
    BeforeEach {
        $script:path = 'TestDrive:\certificate.env'
        @(
            'ACME_DIRECTORY=https://acme.example.com/directory',
            'DOMAINS=example.com',
            'ACME_SCRIPT_PATH=C:\\simple-acme\\dist\\Scripts\\New-CertificateDropFile.ps1',
            'ACME_SCRIPT_PARAMETERS={CertThumbprint}',
            'CERTIFICATE_CONFIG_DIR=C:\cfg',
            'CERTIFICATE_DROP_DIR=C:\drop',
            'CERTIFICATE_STATE_DIR=C:\state',
            'CERTIFICATE_LOG_DIR=C:\log',
            'CERTIFICATE_API_KEY=apikey'
        ) | Set-Content -Path $script:path -Encoding UTF8
    }

    It 'Parse valid file' {
        $values = Read-EnvFile -Path $script:path
        $values.DOMAINS | Should -Be 'example.com'
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
        { Import-EnvFile -Path $script:path -Force } | Should -Throw '*DOMAINS*CERTIFICATE_API_KEY*'
    }

    It 'Import applies optional defaults' {
        $values = Import-EnvFile -Path $script:path -Force
        $values.CERTIFICATE_VERIFY_MAX_ATTEMPTS | Should -Be '3'
    }

    It 'Import does not overwrite existing without force' {
        [Environment]::SetEnvironmentVariable('DOMAINS','preset.example')
        Import-EnvFile -Path $script:path | Out-Null
        [Environment]::GetEnvironmentVariable('DOMAINS') | Should -Be 'preset.example'
    }

    It 'Import force overwrites existing vars' {
        [Environment]::SetEnvironmentVariable('DOMAINS','preset.example')
        Import-EnvFile -Path $script:path -Force | Out-Null
        [Environment]::GetEnvironmentVariable('DOMAINS') | Should -Be 'example.com'
    }

    It 'Lets Encrypt style config does not require EAB values' {
        @(
            'ACME_DIRECTORY=https://acme-v02.api.letsencrypt.org/directory',
            'ACME_REQUIRES_EAB=0',
            'DOMAINS=example.com',
            'ACME_SCRIPT_PATH=C:\\simple-acme\\Scripts\\cert2rds.ps1',
            'ACME_SCRIPT_PARAMETERS={CertThumbprint}',
            'CERTIFICATE_CONFIG_DIR=C:\cfg',
            'CERTIFICATE_DROP_DIR=C:\drop',
            'CERTIFICATE_STATE_DIR=C:\state',
            'CERTIFICATE_LOG_DIR=C:\log',
            'CERTIFICATE_API_KEY=apikey'
        ) | Set-Content -Path $script:path -Encoding UTF8
        { Import-EnvFile -Path $script:path -Force } | Should -Not -Throw
    }

    It 'EAB values are required only when ACME_REQUIRES_EAB is enabled' {
        @(
            'ACME_DIRECTORY=https://acme.networking4all.com/dv',
            'ACME_REQUIRES_EAB=1',
            'DOMAINS=example.com',
            'ACME_SCRIPT_PATH=C:\\simple-acme\\Scripts\\cert2rds.ps1',
            'ACME_SCRIPT_PARAMETERS={CertThumbprint}',
            'CERTIFICATE_CONFIG_DIR=C:\cfg',
            'CERTIFICATE_DROP_DIR=C:\drop',
            'CERTIFICATE_STATE_DIR=C:\state',
            'CERTIFICATE_LOG_DIR=C:\log',
            'CERTIFICATE_API_KEY=apikey'
        ) | Set-Content -Path $script:path -Encoding UTF8
        { Import-EnvFile -Path $script:path -Force } | Should -Throw '*ACME_KID*ACME_HMAC_SECRET*'
    }

    It 'Script path is not required when script plugin is not selected' {
        @(
            'ACME_DIRECTORY=https://acme-v02.api.letsencrypt.org/directory',
            'DOMAINS=example.com',
            'ACME_INSTALLATION_PLUGINS=iis',
            'CERTIFICATE_CONFIG_DIR=C:\cfg',
            'CERTIFICATE_DROP_DIR=C:\drop',
            'CERTIFICATE_STATE_DIR=C:\state',
            'CERTIFICATE_LOG_DIR=C:\log',
            'CERTIFICATE_API_KEY=apikey'
        ) | Set-Content -Path $script:path -Encoding UTF8
        { Import-EnvFile -Path $script:path -Force } | Should -Not -Throw
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
