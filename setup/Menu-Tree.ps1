Set-StrictMode -Version Latest

$AcmeSchema = @(
    @{ Name='ACME_DIRECTORY'; Label='ACME directory URL'; Type='string'; Required=$true; Placeholder='https://acme.example.com/directory'; HelpText='ACME directory endpoint' },
    @{ Name='ACME_KID'; Label='ACME KID'; Type='secret'; Required=$true; Placeholder=''; HelpText='ACME external account KID' },
    @{ Name='ACME_HMAC_SECRET'; Label='ACME HMAC secret'; Type='secret'; Required=$true; Placeholder=''; HelpText='ACME external account HMAC secret' },
    @{ Name='DOMAINS'; Label='Domains (comma-separated)'; Type='string'; Required=$true; Placeholder='example.com,www.example.com'; HelpText='Domain list used by certificaat' }
)

$CertificaatMenuTree = @{
    Title = 'Certificaat setup'
    Items = @(
        @{ Label='ACME settings'; Key='acme'; Type='form'; Schema=$AcmeSchema },
        @{ Label='Local Windows services'; Key='local_windows'; Type='submenu'; Items=@(
            @{ Label='IIS'; Key='iis'; Type='device-form'; ConnectorType='iis' },
            @{ Label='RDS Full Stack'; Key='rds_full'; Type='device-form'; ConnectorType='rds_full' },
            @{ Label='RD Gateway'; Key='rd_gateway'; Type='device-form'; ConnectorType='rd_gateway' },
            @{ Label='RDP Listener'; Key='rdp_listener'; Type='device-form'; ConnectorType='rdp_listener' },
            @{ Label='ADFS'; Key='adfs'; Type='device-form'; ConnectorType='adfs' },
            @{ Label='WinRM'; Key='winrm'; Type='device-form'; ConnectorType='winrm' },
            @{ Label='SQL Server'; Key='sql_server'; Type='device-form'; ConnectorType='sql_server' },
            @{ Label='NTDS (AD LDAPS)'; Key='ntds'; Type='device-form'; ConnectorType='ntds' },
            @{ Label='SSTP VPN'; Key='sstp'; Type='device-form'; ConnectorType='sstp' },
            @{ Label='Windows Admin Center'; Key='windows_admin_center'; Type='device-form'; ConnectorType='windows_admin_center' }
        )},
        @{ Label='Exchange'; Key='exchange_menu'; Type='submenu'; Items=@(
            @{ Label='Exchange (local)'; Key='exchange'; Type='device-form'; ConnectorType='exchange' },
            @{ Label='Exchange Hybrid'; Key='exchange_hybrid'; Type='device-form'; ConnectorType='exchange_hybrid' }
        )},
        @{ Label='Network appliances'; Key='network_appliances'; Type='submenu'; Items=@(
            @{ Label='F5 BIG-IP'; Key='f5_bigip'; Type='device-form'; ConnectorType='f5_bigip' },
            @{ Label='Citrix ADC'; Key='citrix_adc'; Type='device-form'; ConnectorType='citrix_adc' },
            @{ Label='Kemp LoadMaster'; Key='kemp'; Type='device-form'; ConnectorType='kemp' }
        )},
        @{ Label='External dependencies (read-only info)'; Key='external_dependencies'; Type='submenu'; Items=@(
            @{ Label='Java KeyStore (requires JDK)'; Key='java_keystore_info'; Type='action' },
            @{ Label='Veeam VBR (requires VBR module)'; Key='vbr_cloud_gateway_info'; Type='action' },
            @{ Label='Azure App Gateway (requires AzureRM)'; Key='azure_application_gateway_info'; Type='action' },
            @{ Label='Azure AD App Proxy (requires AzureAD)'; Key='azure_ad_app_proxy_info'; Type='action' }
        )},
        @{ Label='Deployment policies'; Key='policies'; Type='action' },
        @{ Label='Backup / Restore'; Key='backup'; Type='submenu'; Items=@(
            @{ Label='Create backup'; Key='backup-create'; Type='action' },
            @{ Label='Restore from backup'; Key='backup-restore'; Type='action' },
            @{ Label='Verify backup integrity'; Key='backup-verify'; Type='action' }
        )},
        @{ Label='Exit'; Key='exit'; Type='action' }
    )
}
