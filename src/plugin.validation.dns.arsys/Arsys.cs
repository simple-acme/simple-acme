using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    /// <summary>
    /// This validates a domain in Arsys hosting (https://www.arsys.es/)
    /// 
    /// This class is the heart of the plugin, it implements the functions
    /// required to create and delete DNS records for challengeDomain validation.
    /// 
    /// This IPlugin.Plugin1 decorator registers the plugin with the WACS 
    /// framework, Its generic parameters are:
    /// 
    /// - ArsysArguments: The arguments class that defines the command line options for the plugin
    /// - ArsysOptions: The options class that holds the configuration for the plugin as stored on disk
    /// - ArsysOptionsFactory: The factory class that creates instances of ArsysOptions during renewal setup
    /// - ArsysJson: JSON serialization helper for ArsysOptions, required for saving and loading options
    /// - DnsValidationCapability: The capability that indicates this plugin can perform DNS validation
    /// 
    /// Other parameters:
    /// - GUID:                A unique identifier for the plugin:
    ///                        TODO: you should generate your own GUID
    /// - Trigger:             A human-readable name for the plugin, shown in the WACS UI and used 
    ///                        as command line option (e.g. --validation reference)
    ///                        TODO: make this unique to your plugin
    /// - Description:         A short description of what the plugin does, shown in the WACS UI
    ///                        TODO: make this unique to your plugin
    /// - External:            Indicates that this plugin is an external plugin, meaning it is not
    ///                        part of the core WACS distribution
    /// - Provider:            Use only if the DNS service has a strong seperate brand from its 
    ///                        parent company, e.g. "Route53" has provider "Amazon AWS".
    ///                        TODO: fill this only when applicable
    /// </summary>
    /// <param name="options">The options as stored in the JSON for the renewal</param>
    /// <param name="dnsClient">Required for the base class to do DNS lookups</param>
    /// <param name="log">Allow you to write to the logger</param>
    /// <param name="settings">Effective settings.json settings for the renewal</param>
    /// <param name="ssm">Can be used to access secrets from the secret manager</param>
    [IPlugin.Plugin1<
        ArsysOptions, ArsysOptionsFactory,
        DnsValidationCapability, ArsysJson, ArsysArguments>
        ("2472d0c3-d3de-4bbc-806d-f36786ae94ed",
        "Arsys", "Create verification records in Arsys DNS",
        External = true)]
    internal class Arsys(
        ArsysOptions options,
        LookupClientProvider dnsClient,
        ILogService log,
        ISettings settings,
        SecretServiceManager ssm,
        DomainParseService domainParser) : DnsValidation<Arsys>(dnsClient, log, settings)
    {
        public ArsysClient.ArsysClient? _instance;

        /// <summary>
        /// Create a reusable Arsys DNS client
        /// </summary>
        /// <param name="domain">The domain. This value will be parsed, so abc.bcd.com becomes bcd.com</param>
        /// <returns></returns>
        private async Task<ArsysClient.ArsysClient> GetClient(string domain)
        {
            if (_instance == null)
            {
                var registeredDomain = domainParser.GetRegisterableDomain(domain);
                var dnsApiKey = await ssm.EvaluateSecret(options.DNSApiKey) ?? "";
                _instance = new ArsysClient.ArsysClient(dnsApiKey, registeredDomain);
            }

            return _instance;
        }

        /// <summary>
        /// Create a DNS record required by the ACME server
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            try
            {
                var client = await GetClient(record.Authority.Domain);
                var res = await client.CreateTxtRecord(record.Authority.Domain, record.Value);
                if (res.@return.errorMsg != "")
                {
                    _log.Error("Arsys DNS error when attempting to create record: {error}", res.@return.errorMsg);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Unhandled exception when attempting to create record");
                return false;
            }
        }

        /// <summary>
        /// Delete the TXT record after validation has been completed
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            try
            {
                var client = await GetClient(record.Authority.Domain);
                var res = await client.DeleteTxtRecord(record.Authority.Domain, record.Value);
                if (res.@return.errorMsg != "")
                {
                    _log.Error("Arsys DNS error when attempting to delete record: {error}", res.@return.errorMsg);
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unhandled exception when attempting to delete record");
            }
        }
    }
}
