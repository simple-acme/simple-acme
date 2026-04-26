$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$AcmeSchema = @(
    @{ Name='ACME_DIRECTORY'; Label='ACME directory URL'; Type='string'; Required=$true; Placeholder='https://acme.example.com/directory'; HelpText='ACME directory endpoint' },
    @{ Name='ACME_KID'; Label='ACME KID'; Type='secret'; Required=$true; Placeholder=''; HelpText='ACME external account KID' },
    @{ Name='ACME_HMAC_SECRET'; Label='ACME HMAC secret'; Type='secret'; Required=$true; Placeholder=''; HelpText='ACME external account HMAC secret' },
    @{ Name='DOMAINS'; Label='Domains (comma-separated)'; Type='string'; Required=$true; Placeholder='example.com,www.example.com'; HelpText='Domain list for certificate issuance' },
    @{ Name='ACME_SOURCE_PLUGIN'; Label='Source plugin'; Type='string'; Required=$true; Placeholder='manual'; HelpText='simple-acme source plugin (manual)' },
    @{ Name='ACME_ORDER_PLUGIN'; Label='Order plugin'; Type='string'; Required=$true; Placeholder='single'; HelpText='simple-acme order plugin (single)' },
    @{ Name='ACME_STORE_PLUGIN'; Label='Store plugin'; Type='string'; Required=$true; Placeholder='certificatestore'; HelpText='simple-acme store plugin' },
    @{ Name='ACME_ACCOUNT_NAME'; Label='Account name'; Type='string'; Required=$false; Placeholder=''; HelpText='Optional ACME account name override' },
    @{ Name='ACME_SCRIPT_PATH'; Label='ACME script path'; Type='string'; Required=$true; Placeholder='C:\certificate\Scripts\New-CertificateDropFile.ps1'; HelpText='Absolute path to New-CertificateDropFile.ps1' },
    @{ Name='ACME_SCRIPT_PARAMETERS'; Label='Script parameters'; Type='string'; Required=$true; Placeholder="'default' {RenewalId} '{CertCommonName}' {CertThumbprint} {OldCertThumbprint} '{CacheFile}' '{CachePassword}' '{StorePath}' {StoreType}"; HelpText='wacs scriptparameters template' },
    @{ Name='ACME_VALIDATION_MODE'; Label='Validation mode'; Type='string'; Required=$true; Placeholder='none'; HelpText='Locked to none' },
    @{ Name='ACME_INSTALLATION_PLUGINS'; Label='Installation plugins'; Type='string'; Required=$true; Placeholder='script'; HelpText='Comma-separated installation plugins' },
    @{ Name='ACME_WACS_RETRY_ATTEMPTS'; Label='WACS retry attempts'; Type='string'; Required=$true; Placeholder='3'; HelpText='Retry attempts for wacs operations' },
    @{ Name='ACME_WACS_RETRY_DELAY_SECONDS'; Label='WACS retry delay seconds'; Type='string'; Required=$true; Placeholder='2'; HelpText='Delay between wacs retries' }
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
        @{ Label='Not available - stubs'; Key='stubs'; Type='submenu'; Items=@(@{Label='Nginx';Key='nginx';Type='device-form'},@{Label='Apache';Key='apache';Type='device-form'},@{Label='HAProxy';Key='haproxy';Type='device-form'},@{Label='Palo Alto';Key='palo_alto';Type='device-form'},@{Label='Fortigate';Key='fortigate';Type='device-form'},@{Label='Sophos';Key='sophos';Type='device-form'},@{Label='Check Point';Key='check_point';Type='device-form'},@{Label='Barracuda';Key='barracuda';Type='device-form'},@{Label='Java KeyStore (info)';Key='java_keystore_info';Type='action'},@{Label='VBR Cloud Gateway (info)';Key='vbr_cloud_gateway_info';Type='action'},@{Label='Azure Application Gateway (info)';Key='azure_application_gateway_info';Type='action'},@{Label='Azure AD App Proxy (info)';Key='azure_ad_app_proxy_info';Type='action'},@{Key='back';Label='.. Back';Type='back'})},
        @{ Label='Deployment policies'; Key='policies'; Type='action' },
        @{ Label='View existing policies'; Key='policies-view'; Type='action' },
        @{ Label='Register/Repair orchestrator task'; Key='task-register'; Type='action' },
        @{ Label='Backup / Restore'; Key='backup'; Type='submenu'; Items=@(@{ Label='Create backup'; Key='backup-create'; Type='action' },@{ Label='Restore from backup'; Key='backup-restore'; Type='action' },@{ Label='Verify backup'; Key='backup-verify'; Type='action' },@{Key='back';Label='.. Back';Type='back'})},
        @{ Label='Exit'; Key='exit'; Type='action' }
    )
}
