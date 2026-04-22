Set-StrictMode -Version Latest

$DeviceSchemas = @{
    iis = @{ ConnectorType='iis'; Label='IIS'; Category='local'; Fields=@(
        @{ Name='site_name'; Label='Site name'; Type='text'; Required=$true; Placeholder='Default Web Site'; HelpText='IIS website name' },
        @{ Name='binding_ip'; Label='Binding IP'; Type='text'; Required=$true; Placeholder='0.0.0.0'; HelpText='IP address for HTTPS binding' },
        @{ Name='binding_port'; Label='Binding port'; Type='text'; Required=$true; Placeholder='443'; HelpText='Port for TLS binding' },
        @{ Name='cert_store_location'; Label='Certificate store location'; Type='choice'; Required=$true; Choices=@('LocalMachine','CurrentUser'); Placeholder='LocalMachine'; HelpText='Store location for certificate install' }
    )}
    nginx = @{ ConnectorType='nginx'; Label='NGINX'; Category='local'; Fields=@(
        @{ Name='config_file_path'; Label='Config file path'; Type='text'; Required=$true; Placeholder='C:\nginx\conf\nginx.conf'; HelpText='Main NGINX configuration file' },
        @{ Name='reload_command'; Label='Reload command override'; Type='text'; Required=$false; Placeholder='nginx -s reload'; HelpText='Leave blank for default nginx -s reload' }
    )}
    apache = @{ ConnectorType='apache'; Label='Apache'; Category='local'; Fields=@(
        @{ Name='config_file_path'; Label='Config file path'; Type='text'; Required=$true; Placeholder='C:\Apache24\conf\httpd.conf'; HelpText='Apache config file path' },
        @{ Name='reload_command'; Label='Reload command override'; Type='text'; Required=$false; Placeholder='httpd -k restart'; HelpText='Leave blank to use service restart defaults' }
    )}
    haproxy = @{ ConnectorType='haproxy'; Label='HAProxy'; Category='local'; Fields=@(
        @{ Name='socket_path'; Label='Socket path'; Type='text'; Required=$true; Placeholder='C:\haproxy\admin.sock'; HelpText='HAProxy admin socket path' },
        @{ Name='config_file_path'; Label='Config file path'; Type='text'; Required=$true; Placeholder='C:\haproxy\haproxy.cfg'; HelpText='HAProxy config file' },
        @{ Name='reload_command'; Label='Reload command override'; Type='text'; Required=$false; Placeholder='haproxy -f C:\haproxy\haproxy.cfg -sf <pid>'; HelpText='Optional custom reload command' }
    )}
    palo_alto = @{ ConnectorType='palo_alto'; Label='Palo Alto'; Category='firewall'; Fields=@(
        @{ Name='host'; Label='Host'; Type='text'; Required=$true; Placeholder='pa.example.com'; HelpText='Firewall management address' },
        @{ Name='api_key'; Label='API key'; Type='secret'; Required=$true; Placeholder=''; HelpText='API key for Palo Alto XML API' },
        @{ Name='vsys'; Label='VSYS'; Type='text'; Required=$true; Placeholder='vsys1'; HelpText='Virtual system name' }
    )}
    fortigate = @{ ConnectorType='fortigate'; Label='FortiGate'; Category='firewall'; Fields=@(
        @{ Name='host'; Label='Host'; Type='text'; Required=$true; Placeholder='fortigate.example.com'; HelpText='Firewall host' },
        @{ Name='api_token'; Label='API token'; Type='secret'; Required=$true; Placeholder=''; HelpText='REST API token' },
        @{ Name='vdom'; Label='VDOM'; Type='text'; Required=$true; Placeholder='root'; HelpText='Virtual domain' }
    )}
    sophos = @{ ConnectorType='sophos'; Label='Sophos'; Category='firewall'; Fields=@(
        @{ Name='host'; Label='Host'; Type='text'; Required=$true; Placeholder='sophos.example.com'; HelpText='Sophos management host' },
        @{ Name='api_token'; Label='API token'; Type='secret'; Required=$true; Placeholder=''; HelpText='API token for Sophos firewall' }
    )}
    check_point = @{ ConnectorType='check_point'; Label='Check Point'; Category='firewall'; Fields=@(
        @{ Name='host'; Label='Host'; Type='text'; Required=$true; Placeholder='checkpoint.example.com'; HelpText='Check Point host' },
        @{ Name='api_key'; Label='API key'; Type='secret'; Required=$true; Placeholder=''; HelpText='Management API key' },
        @{ Name='policy_package'; Label='Policy package'; Type='text'; Required=$true; Placeholder='Standard'; HelpText='Policy package to publish/install' }
    )}
    f5_bigip = @{ ConnectorType='f5_bigip'; Label='F5 BIG-IP'; Category='loadbalancer'; Fields=@(
        @{ Name='host'; Label='Management hostname or IP'; Type='text'; Required=$true; Placeholder='f5.example.com'; HelpText='FQDN or IP of the F5 management interface' },
        @{ Name='token'; Label='API token'; Type='secret'; Required=$true; Placeholder=''; HelpText='iControl REST Bearer token' },
        @{ Name='ssl_profile'; Label='Client SSL profile name'; Type='text'; Required=$true; Placeholder='clientssl-prod'; HelpText='Name of the client SSL profile to update' }
    )}
    kemp = @{ ConnectorType='kemp'; Label='Kemp'; Category='loadbalancer'; Fields=@(
        @{ Name='host'; Label='Host'; Type='text'; Required=$true; Placeholder='kemp.example.com'; HelpText='Kemp LoadMaster host' },
        @{ Name='user'; Label='User'; Type='secret'; Required=$true; Placeholder=''; HelpText='API user for kemp' },
        @{ Name='password'; Label='Password'; Type='secret'; Required=$true; Placeholder=''; HelpText='API password' },
        @{ Name='vs_id'; Label='Virtual service ID'; Type='text'; Required=$true; Placeholder='1'; HelpText='LoadMaster virtual service id' }
    )}
    citrix_adc = @{ ConnectorType='citrix_adc'; Label='Citrix ADC'; Category='loadbalancer'; Fields=@(
        @{ Name='host'; Label='Host'; Type='text'; Required=$true; Placeholder='adc.example.com'; HelpText='Citrix ADC management endpoint' },
        @{ Name='user'; Label='User'; Type='secret'; Required=$true; Placeholder=''; HelpText='NITRO API username' },
        @{ Name='password'; Label='Password'; Type='secret'; Required=$true; Placeholder=''; HelpText='NITRO API password' },
        @{ Name='vserver'; Label='vServer'; Type='text'; Required=$true; Placeholder='prod-vsrv'; HelpText='Target virtual server name' }
    )}
    barracuda = @{ ConnectorType='barracuda'; Label='Barracuda'; Category='waf'; Fields=@(
        @{ Name='host'; Label='Host'; Type='text'; Required=$true; Placeholder='waf.example.com'; HelpText='Barracuda WAF host' },
        @{ Name='api_token'; Label='API token'; Type='secret'; Required=$true; Placeholder=''; HelpText='Barracuda API token' },
        @{ Name='service_name'; Label='Service name'; Type='text'; Required=$true; Placeholder='prod-service'; HelpText='WAF service name to update' }
    )}
}
