Set-StrictMode -Version Latest

$DeviceSchemas = @{
    iis = @{ ConnectorType='iis'; Label='IIS'; Category='local_windows'; Fields=@(
        @{ Name='site_name'; Label='Site name'; Type='string'; Required=$true; Placeholder='Default Web Site'; HelpText='IIS website name' },
        @{ Name='cert_store_location'; Label='Store location'; Type='choice'; Required=$true; Choices=@('My','WebHosting'); Placeholder='My'; HelpText='Certificate store location for IIS binding' }
    )}

    adfs = @{ ConnectorType='adfs'; Label='ADFS'; Category='local_windows'; Fields=@() }
    rdp_listener = @{ ConnectorType='rdp_listener'; Label='RDP Listener'; Category='local_windows'; Fields=@() }
    rd_gateway = @{ ConnectorType='rd_gateway'; Label='RD Gateway'; Category='local_windows'; Fields=@() }
    rds_full = @{ ConnectorType='rds_full'; Label='RDS Full Stack'; Category='local_windows'; Fields=@(
        @{ Name='rdcb_fqdn'; Label='RDCB FQDN'; Type='string'; Required=$false; Placeholder='rdcb.contoso.local'; HelpText='Optional Connection Broker FQDN (local machine when empty)' }
    )}
    ntds = @{ ConnectorType='ntds'; Label='NTDS (AD LDAPS)'; Category='local_windows'; Fields=@() }
    sstp = @{ ConnectorType='sstp'; Label='SSTP VPN'; Category='local_windows'; Fields=@(
        @{ Name='recreate_default_bindings'; Label='Recreate default IIS :443 binding'; Type='choice'; Required=$true; Choices=@('false','true'); Placeholder='false'; HelpText='Set true to re-create *:443: binding before assigning cert' }
    )}
    winrm = @{ ConnectorType='winrm'; Label='WinRM'; Category='local_windows'; Fields=@() }
    sql_server = @{ ConnectorType='sql_server'; Label='SQL Server'; Category='local_windows'; Fields=@(
        @{ Name='instance_name'; Label='Instance name'; Type='string'; Required=$false; Placeholder='MSSQLSERVER'; HelpText='SQL instance name (default MSSQLSERVER)' }
    )}
    windows_admin_center = @{ ConnectorType='windows_admin_center'; Label='Windows Admin Center'; Category='local_windows'; Fields=@() }

    exchange = @{ ConnectorType='exchange'; Label='Exchange (local)'; Category='exchange'; Fields=@(
        @{ Name='services'; Label='Exchange services'; Type='string'; Required=$false; Placeholder='SMTP,IIS,POP,IMAP'; HelpText='Comma-separated list for Enable-ExchangeCertificate -Services' }
    )}

    exchange_hybrid = @{ ConnectorType='exchange_hybrid'; Label='Exchange Hybrid'; Category='exchange'; Disabled=$true; Requires='Requires hybrid transport tuning not yet implemented in native connector set.'; Fields=@(
        @{ Name='requires'; Label='Requires'; Type='string'; Required=$false; Placeholder='Exchange hybrid transport'; HelpText='Read-only information: not currently available.' }
    )}

    f5_bigip = @{ ConnectorType='f5_bigip'; Label='F5 BIG-IP'; Category='network_appliance'; Fields=@(
        @{ Name='host'; Label='Management hostname or IP'; Type='string'; Required=$true; Placeholder='f5.example.com'; HelpText='FQDN or IP of the F5 management interface' },
        @{ Name='token_env'; Label='Token env-var name'; Type='string'; Required=$true; Placeholder='F5_API_TOKEN'; HelpText='Environment variable that stores the iControl REST Bearer token' },
        @{ Name='ssl_profile'; Label='Client SSL profile name'; Type='string'; Required=$true; Placeholder='clientssl-prod'; HelpText='Name of the client SSL profile to update' }
    )}
    citrix_adc = @{ ConnectorType='citrix_adc'; Label='Citrix ADC'; Category='network_appliance'; Fields=@(
        @{ Name='host'; Label='Host'; Type='string'; Required=$true; Placeholder='adc.example.com'; HelpText='Citrix ADC management endpoint' },
        @{ Name='user_env'; Label='User env-var name'; Type='string'; Required=$true; Placeholder='ADC_USER'; HelpText='Environment variable name for NITRO API username' },
        @{ Name='password_env'; Label='Password env-var name'; Type='string'; Required=$true; Placeholder='ADC_PASSWORD'; HelpText='Environment variable name for NITRO API password' },
        @{ Name='vserver'; Label='vServer'; Type='string'; Required=$true; Placeholder='prod-vsrv'; HelpText='Target virtual server name' }
    )}
    kemp = @{ ConnectorType='kemp'; Label='Kemp LoadMaster'; Category='network_appliance'; Fields=@(
        @{ Name='host'; Label='Host'; Type='string'; Required=$true; Placeholder='kemp.example.com'; HelpText='Kemp LoadMaster host' },
        @{ Name='user_env'; Label='User env-var name'; Type='string'; Required=$true; Placeholder='KEMP_USER'; HelpText='Environment variable name for API username' },
        @{ Name='password_env'; Label='Password env-var name'; Type='string'; Required=$true; Placeholder='KEMP_PASSWORD'; HelpText='Environment variable name for API password' },
        @{ Name='vs_id'; Label='Virtual service ID'; Type='string'; Required=$true; Placeholder='1'; HelpText='LoadMaster virtual service id' }
    )}

    java_keystore = @{ ConnectorType='java_keystore'; Label='Java KeyStore'; Category='external_dependency'; Disabled=$true; Requires='JDK (keytool.exe)'; Fields=@(
        @{ Name='requires'; Label='Requires'; Type='string'; Required=$false; Placeholder='JDK keytool.exe'; HelpText='Not available in native-only mode.' }
    )}
    kemp_module = @{ ConnectorType='kemp_module'; Label='Kemp (PowerShell module)'; Category='external_dependency'; Disabled=$true; Requires='Kemp.LoadBalancer.Powershell module'; Fields=@(
        @{ Name='requires'; Label='Requires'; Type='string'; Required=$false; Placeholder='Kemp PS module'; HelpText='Not available in native-only mode.' }
    )}
    vbr_cloud_gateway = @{ ConnectorType='vbr_cloud_gateway'; Label='Veeam VBR Cloud Gateway'; Category='external_dependency'; Disabled=$true; Requires='Veeam Backup & Replication PowerShell module'; Fields=@(
        @{ Name='requires'; Label='Requires'; Type='string'; Required=$false; Placeholder='VBR module'; HelpText='Not available in native-only mode.' }
    )}
    azure_ad_app_proxy = @{ ConnectorType='azure_ad_app_proxy'; Label='Azure AD Application Proxy'; Category='external_dependency'; Disabled=$true; Requires='AzureAD module'; Fields=@(
        @{ Name='requires'; Label='Requires'; Type='string'; Required=$false; Placeholder='AzureAD module'; HelpText='Not available in native-only mode.' }
    )}
    azure_application_gateway = @{ ConnectorType='azure_application_gateway'; Label='Azure Application Gateway'; Category='external_dependency'; Disabled=$true; Requires='AzureRM module'; Fields=@(
        @{ Name='requires'; Label='Requires'; Type='string'; Required=$false; Placeholder='AzureRM module'; HelpText='Not available in native-only mode.' }
    )}
    sparx_procloud = @{ ConnectorType='sparx_procloud'; Label='Sparx Pro Cloud'; Category='external_dependency'; Disabled=$true; Requires='PowerShell 7 and external tooling'; Fields=@(
        @{ Name='requires'; Label='Requires'; Type='string'; Required=$false; Placeholder='PowerShell 7'; HelpText='Not available in native-only mode.' }
    )}
}
