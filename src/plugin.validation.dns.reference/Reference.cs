﻿using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Services;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
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
    [IPlugin.Plugin1<
        ReferenceOptions, ReferenceOptionsFactory,
        DnsValidationCapability, ReferenceJson, ReferenceArguments>
        ("554b02ba-9e8e-4fcc-9132-d03c5778e227",
        "Reference", "Create verification records in Reference DNS",
        External = true, Provider = null)]
    internal class Reference(
        ReferenceOptions options,
        LookupClientProvider dnsClient,
        ILogService log,
        ISettings settings,
        IProxyService proxy,
        SecretServiceManager ssm,
        DomainParseService domainParser) : DnsValidation<Reference, ReferenceClient>(dnsClient, log, settings, proxy)
    {
        /// <summary>
        /// Create a new instance of your DNS accessor
        /// </summary>
        /// <param name="httpClient">
        /// HttpClient generated by simple-acme, this client is 
        /// already hooked up to the logger, respects the users 
        /// proxy settings and identifies itself as the 
        /// current simple-acme version using the User-Agent header
        /// </param>
        /// <returns></returns>
        protected override async Task<ReferenceClient> CreateClient(HttpClient httpClient)
        {
            var clientId = options.ClientId ?? "";
            var clientSecret = await ssm.EvaluateSecret(options.ClientSecret) ?? "";
            return new ReferenceClient(httpClient, clientId, clientSecret);
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
                // Select zone
                var zone = await GetHostZone(record.Authority.Domain);
                if (zone == null)
                {
                    _log.Error("Unable to find zone for {challengeDomain}", record.Authority.Domain);
                    return false;
                }

                // Host name relative to the zone name, e.g. "@" if they are identical
                // or "_acme-challenge" if the zone is "example.com" and the required
                // record is "_acme-challenge.example.com"
                var host = RelativeRecordName(zone.Name, record.Authority.Domain);

                // Get client (cached) and create the record
                var client = await GetClient();
                await client.CreateTxtRecord(zone, host, record.Value);
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
                var zone = await GetHostZone(record.Authority.Domain);
                if (zone == null)
                {
                    _log.Warning("Unable to find zone for {challengeDomain}", record.Authority.Domain);
                    return;
                }
                var client = await GetClient();
                var host = RelativeRecordName(zone.Name, record.Authority.Domain);
                await client.DeleteTxtRecord(zone, host, record.Value);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unable to delete record");
            }
        }


        /// <summary>
        /// Select the best matching zone for the given host name
        /// </summary>
        /// <param name="challengeDomain"></param>
        /// <returns></returns>
        private async Task<ReferenceZone?> GetHostZone(string challengeDomain)
        {
            // Get the client
            var client = await GetClient();

            // Scenario A: If there is only one zone per registered challengeDomain,
            // get the zone directly from the client, our challengeDomain is
            // application.example.com, so we need the zone for example.com
            var registeredDomain = domainParser.GetRegisterableDomain(challengeDomain);
            var zone = await client.GetZone(registeredDomain);
            // return zone;

            // Scenario B: If it is possible to create multiple zones, e.g.
            // example.com and sub.example.com, get all of them and choose
            // the best match. 
            var zones = await client.GetZones();
            return FindBestMatch(zones.ToDictionary(x => x.Name), challengeDomain);
        }
    }
}
