$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$AcmeSchema = @(
    @{ Name='ACME_DIRECTORY'; Label='ACME directory URL'; Type='string'; Required=$true; Placeholder='https://acme.example.com/directory'; HelpText='ACME directory endpoint' },
    @{ Name='ACME_KID'; Label='ACME KID'; Type='secret'; Required=$true; Placeholder=''; HelpText='ACME external account KID' },
    @{ Name='ACME_HMAC_SECRET'; Label='ACME HMAC secret'; Type='secret'; Required=$true; Placeholder=''; HelpText='ACME external account HMAC secret' },
    @{ Name='DOMAINS'; Label='Domains (comma-separated)'; Type='string'; Required=$true; Placeholder='example.com,www.example.com'; HelpText='Domain list for certificate issuance' },
    @{ Name='ACME_SCRIPT_PATH'; Label='ACME script path'; Type='string'; Required=$true; Placeholder='C:\certificate\dist\Scripts\New-CertificateDropFile.ps1'; HelpText='Absolute path to New-CertificateDropFile.ps1' }
)

$CertificateMenuTree = @{
    Title = 'Certificate setup'
    Items = @(
        @{ Label='ACME settings'; Key='acme'; Type='form'; Schema=$AcmeSchema },
        @{ Label='Local Windows services'; Key='local_windows'; Type='submenu'; Items=@(
            @{ Label='IIS'; Key='iis'; Type='device-form' },@{ Label='ADFS'; Key='adfs'; Type='device-form' },@{ Label='RDS Full Stack'; Key='rds_full'; Type='device-form' },@{ Label='RD Gateway'; Key='rd_gateway'; Type='device-form' },@{ Label='RDP Listener'; Key='rdp_listener'; Type='device-form' },@{ Label='WinRM'; Key='winrm'; Type='device-form' },@{ Label='SQL Server'; Key='sql_server'; Type='device-form' },@{ Label='NTDS'; Key='ntds'; Type='device-form' },@{ Label='SSTP'; Key='sstp'; Type='device-form' },@{ Label='Windows Admin Center'; Key='windows_admin_center'; Type='device-form' },@{Key='back';Label='.. Back';Type='back'}
        )},
        @{ Label='Exchange'; Key='exchange_menu'; Type='submenu'; Items=@(@{ Label='Exchange'; Key='exchange'; Type='device-form' },@{Key='back';Label='.. Back';Type='back'})},
        @{ Label='Network appliances'; Key='network_appliances'; Type='submenu'; Items=@(@{Label='F5 BIG-IP';Key='f5_bigip';Type='device-form'},@{Label='Citrix ADC';Key='citrix_adc';Type='device-form'},@{Label='Kemp';Key='kemp';Type='device-form'},@{Key='back';Label='.. Back';Type='back'})},
        @{ Label='Not available — stubs'; Key='stubs'; Type='submenu'; Items=@(@{Label='Nginx';Key='nginx';Type='device-form'},@{Label='Apache';Key='apache';Type='device-form'},@{Label='HAProxy';Key='haproxy';Type='device-form'},@{Label='Palo Alto';Key='palo_alto';Type='device-form'},@{Label='Fortigate';Key='fortigate';Type='device-form'},@{Label='Sophos';Key='sophos';Type='device-form'},@{Label='Check Point';Key='check_point';Type='device-form'},@{Label='Barracuda';Key='barracuda';Type='device-form'},@{Key='back';Label='.. Back';Type='back'})},
        @{ Label='Deployment policies'; Key='policies'; Type='action' },
        @{ Label='Backup / Restore'; Key='backup'; Type='submenu'; Items=@(@{ Label='Create backup'; Key='backup-create'; Type='action' },@{ Label='Restore from backup'; Key='backup-restore'; Type='action' },@{ Label='Verify backup'; Key='backup-verify'; Type='action' },@{Key='back';Label='.. Back';Type='back'})},
        @{ Label='Exit'; Key='exit'; Type='action' }
    )
}
