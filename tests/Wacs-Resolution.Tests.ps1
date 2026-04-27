Import-Module "$PSScriptRoot/../core/Simple-Acme-Reconciler.psm1" -Force

Describe 'WACS executable resolution' {
    BeforeAll {
        $script:moduleRoot = Split-Path $PSScriptRoot -Parent
    }

    It 'finds <root>\\wacs.exe' {
        $rootWacs = Join-Path $script:moduleRoot 'wacs.exe'
        $backup = Join-Path $TestDrive 'wacs.backup.exe'
        $hadOriginal = Test-Path -LiteralPath $rootWacs
        if ($hadOriginal) {
            Copy-Item -LiteralPath $rootWacs -Destination $backup -Force
        }
        'stub' | Set-Content -Path $rootWacs -Encoding UTF8
        try {
            $resolved = Resolve-WacsExecutable -EnvValues @{}
            $resolved | Should -Be (Convert-Path -LiteralPath $rootWacs)
        } finally {
            Remove-Item -LiteralPath $rootWacs -Force -ErrorAction SilentlyContinue
            if ($hadOriginal) {
                Move-Item -LiteralPath $backup -Destination $rootWacs -Force
            }
        }
    }

    It 'finds <root>\\simple-acme.exe' {
        $rootWacs = Join-Path $script:moduleRoot 'wacs.exe'
        $rootSimpleAcme = Join-Path $script:moduleRoot 'simple-acme.exe'
        $backupSimple = Join-Path $TestDrive 'simple-acme.backup.exe'
        $hadOriginalSimple = Test-Path -LiteralPath $rootSimpleAcme
        if ($hadOriginalSimple) {
            Copy-Item -LiteralPath $rootSimpleAcme -Destination $backupSimple -Force
        }
        Remove-Item -LiteralPath $rootWacs -Force -ErrorAction SilentlyContinue
        'stub' | Set-Content -Path $rootSimpleAcme -Encoding UTF8
        try {
            $resolved = Resolve-WacsExecutable -EnvValues @{}
            $resolved | Should -Be (Convert-Path -LiteralPath $rootSimpleAcme)
        } finally {
            Remove-Item -LiteralPath $rootSimpleAcme -Force -ErrorAction SilentlyContinue
            if ($hadOriginalSimple) {
                Move-Item -LiteralPath $backupSimple -Destination $rootSimpleAcme -Force
            }
        }
    }

    It 'honors ACME_WACS_PATH' {
        $custom = Join-Path $TestDrive 'custom-wacs.exe'
        'stub' | Set-Content -Path $custom -Encoding UTF8
        $resolved = Resolve-WacsExecutable -EnvValues @{ ACME_WACS_PATH = $custom }
        $resolved | Should -Be (Convert-Path -LiteralPath $custom)
    }

    It 'uses PATH as fallback only' {
        $rootWacs = Join-Path $script:moduleRoot 'wacs.exe'
        $backup = Join-Path $TestDrive 'wacs.backup.exe'
        $hadOriginal = Test-Path -LiteralPath $rootWacs
        if ($hadOriginal) {
            Copy-Item -LiteralPath $rootWacs -Destination $backup -Force
        }
        'stub' | Set-Content -Path $rootWacs -Encoding UTF8
        Mock Get-Command { $null } -ParameterFilter { $name -in @('wacs.exe','wacs') }
        try {
            $resolved = Resolve-WacsExecutable -EnvValues @{}
            $resolved | Should -Be (Convert-Path -LiteralPath $rootWacs)
        } finally {
            Remove-Item -LiteralPath $rootWacs -Force -ErrorAction SilentlyContinue
            if ($hadOriginal) {
                Move-Item -LiteralPath $backup -Destination $rootWacs -Force
            }
        }
    }
}

Describe 'WACS consumers use resolved executable path' {
    It 'Invoke-WacsWithRetry uses the resolved absolute path' {
        $absolute = Join-Path $TestDrive 'resolved-wacs.exe'
        'stub' | Set-Content -Path $absolute -Encoding UTF8
        Mock Resolve-WacsExecutable { $absolute }
        Mock Invoke-NativeProcess {
            [pscustomobject]@{
                Succeeded = $true
                TimedOut = $false
                ExitCode = 0
                OutputLines = @()
            }
        } -ParameterFilter { $FilePath -eq $absolute }

        $null = Invoke-WacsWithRetry -Args @('--version') -EnvValues @{}

        Should -Invoke Resolve-WacsExecutable -Times 1 -Exactly
        Should -Invoke Invoke-NativeProcess -Times 1 -Exactly -ParameterFilter { $FilePath -eq $absolute }
    }
}
