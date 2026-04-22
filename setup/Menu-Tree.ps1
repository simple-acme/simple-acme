Set-StrictMode -Version Latest

$AcmeSchema = @(
    @{ Name='ACME_DIRECTORY'; Label='ACME directory URL'; Type='text'; Required=$true; Placeholder='https://acme.example.com/directory'; HelpText='ACME directory endpoint' },
    @{ Name='ACME_KID'; Label='ACME KID'; Type='secret'; Required=$true; Placeholder=''; HelpText='ACME external account KID' },
    @{ Name='ACME_HMAC_SECRET'; Label='ACME HMAC secret'; Type='secret'; Required=$true; Placeholder=''; HelpText='ACME external account HMAC secret' },
    @{ Name='DOMAINS'; Label='Domains (comma-separated)'; Type='text'; Required=$true; Placeholder='example.com,www.example.com'; HelpText='Domain list used by certificaat' }
)

$CertificaatMenuTree = @{
    Title = 'Certificaat setup'
    Items = @(
        @{ Label='ACME settings'; Key='acme'; Type='form'; Schema=$AcmeSchema },
        @{ Label='Local endpoints'; Key='local'; Type='submenu'; Items=@(
            @{ Label='IIS'; Key='iis'; Type='device-form'; ConnectorType='iis' },
            @{ Label='NGINX'; Key='nginx'; Type='device-form'; ConnectorType='nginx' },
            @{ Label='Apache'; Key='apache'; Type='device-form'; ConnectorType='apache' },
            @{ Label='HAProxy'; Key='haproxy'; Type='device-form'; ConnectorType='haproxy' }
        )},
        @{ Label='Remote — Firewalls'; Key='firewalls'; Type='submenu'; Items=@(
            @{ Label='Palo Alto'; Key='palo_alto'; Type='device-form'; ConnectorType='palo_alto' },
            @{ Label='FortiGate'; Key='fortigate'; Type='device-form'; ConnectorType='fortigate' },
            @{ Label='Sophos'; Key='sophos'; Type='device-form'; ConnectorType='sophos' },
            @{ Label='Check Point'; Key='check_point'; Type='device-form'; ConnectorType='check_point' }
        )},
        @{ Label='Remote — Load balancers'; Key='loadbalancers'; Type='submenu'; Items=@(
            @{ Label='F5 BIG-IP'; Key='f5_bigip'; Type='device-form'; ConnectorType='f5_bigip' },
            @{ Label='Kemp'; Key='kemp'; Type='device-form'; ConnectorType='kemp' },
            @{ Label='Citrix ADC'; Key='citrix_adc'; Type='device-form'; ConnectorType='citrix_adc' }
        )},
        @{ Label='Remote — WAF'; Key='waf'; Type='submenu'; Items=@(
            @{ Label='Barracuda'; Key='barracuda'; Type='device-form'; ConnectorType='barracuda' }
        )},
        @{ Label='Deployment policies'; Key='policies'; Type='action' },
        @{ Label='Backup & restore'; Key='backup'; Type='submenu'; Items=@(
            @{ Label='Create backup'; Key='backup-create'; Type='action' },
            @{ Label='Restore from backup'; Key='backup-restore'; Type='action' },
            @{ Label='Verify backup integrity'; Key='backup-verify'; Type='action' }
        )},
        @{ Label='Exit'; Key='exit'; Type='action' }
    )
}
