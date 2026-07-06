using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Services;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins;

/// <summary>
/// This is a reference implementation of a DNS validation plugin.
/// Designed as an example for developers to create their own.
///
/// This class is the heart of the plugin, it implements the functions
/// required to create and delete DNS records for challengeDomain validation.
///
/// This IPlugin.Plugin1 decorator registers the plugin with the WACS
/// framework, Its generic parameters are:
///
/// - ReferenceArguments: The arguments class that defines the command line options for the plugin
/// - ReferenceOptions: The options class that holds the configuration for the plugin as stored on disk
/// - ReferenceOptionsFactory: The factory class that creates instances of ReferenceOptions during renewal setup
/// - ReferenceJson: JSON serialization helper for ReferenceOptions, required for saving and loading options
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
/// <param name="proxy">Used by base class to generate HttpClient with proper proxy settings</param>
/// <param name="ssm">Can be used to access secrets from the secret manager</param>
[IPlugin.Plugin1<IPProjectsOptions, IPProjectsOptionsFactory,
                 DnsValidationCapability, IPProjectsJson, IPProjectsArguments>
                ("6370923c-7840-4e8c-b003-a3b514312e5a",
                 "IPProjects",
                 "Create verification records at IP-Projects.de",
                 External = true,
                 Provider = null)]
internal class IPProjects(IPProjectsOptions options,
                          LookupClientProvider dnsClient,
                          ILogService log,
                          ISettings settings,
                          IProxyService proxy,
                          SecretServiceManager ssm,
                          DomainParseService domainParser)
    : DnsValidation<IPProjects, IPProjectsClient>(dnsClient, log, settings, proxy)
{
    protected override async Task<IPProjectsClient> CreateClient(HttpClient httpClient)
    {
        string apiKey = await ssm.EvaluateSecret(options.ApiKey) ?? "";
        return new IPProjectsClient(httpClient, apiKey);
    }

    public override async Task<bool> CreateRecord(DnsValidationRecord record)
    {
        try
        {
            string domain = domainParser.GetRegisterableDomain(record.Authority.Domain);
            string recordName = RelativeRecordName(domain, record.Authority.Domain);
            recordName = recordName == "@" ? string.Empty : recordName;

            IPProjectsClient client = await GetClient();
            return await client.AddRecord(domain, recordName, record.Value);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Unhandled exception when attempting to create record");
            return false;
        }
    }

    public override async Task DeleteRecord(DnsValidationRecord record)
    {
        try
        {
            string domain = domainParser.GetRegisterableDomain(record.Authority.Domain);
            string recordName = RelativeRecordName(domain, record.Authority.Domain);
            recordName = recordName == "@" ? string.Empty : recordName;

            IPProjectsClient client = await GetClient();
            await client.AddRecord(domain, recordName, record.Value);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Unhandled exception when attempting to delete record");
        }
    }
}
