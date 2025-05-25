﻿using PKISharp.WACS.Clients.Acme;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.CsrPlugins;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Linq;
using System.Threading.Tasks;
using Dns = PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using Http = PKISharp.WACS.Plugins.ValidationPlugins.Http;
using Install = PKISharp.WACS.Plugins.InstallationPlugins;
using Store = PKISharp.WACS.Plugins.StorePlugins;
using Target = PKISharp.WACS.Plugins.TargetPlugins;

namespace PKISharp.WACS.Services.Legacy
{
    internal class Importer(
        ILogService log, ILegacyRenewalService legacyRenewal,
        ISettings settings, IRenewalStore currentRenewal,
        IInputService input, MainArguments arguments,
        LegacyTaskSchedulerService legacyTaskScheduler,
        IAutoRenewService currentTaskScheduler,
        AcmeClientManager clientManager)
    {
        public async Task Import(RunLevel runLevel)
        {

            if (!legacyRenewal.Renewals.Any())
            {
                log.Warning("No legacy renewals found");
            }
            var currentRenewals = await currentRenewal.List();
            log.Information("Legacy renewals {x}", legacyRenewal.Renewals.Count().ToString());
            log.Information("Current renewals {x}", currentRenewals.Count.ToString());
            log.Information("Step {x}/3: convert renewals", 1);
            foreach (var legacyRenewal in legacyRenewal.Renewals)
            {
                var converted = Convert(legacyRenewal);
                await currentRenewal.Import(converted);
            }
            if (!arguments.NoTaskScheduler)
            {
                log.Information("Step {x}/3: create new scheduled task", 2);
                await currentTaskScheduler.EnsureAutoRenew(runLevel | RunLevel.ForceTaskScheduler);
                legacyTaskScheduler.StopTaskScheduler();
            }

            log.Information("Step {x}/3: ensure ACMEv2 account", 3);
            await clientManager.GetClient();
            var listCommand = "--list";
            var renewCommand = "--renew";
            if (runLevel.HasFlag(RunLevel.Interactive))
            {
                listCommand = "Manage renewals";
                renewCommand = "Run";
            }
            input.CreateSpace();
            input.Show(null,
                value: $"The renewals have now been imported into this new version " +
                "of the program. Nothing else will happen until new scheduled task is " +
                "first run *or* you trigger them manually. It is highly recommended " +
                $"to review the imported items with '{listCommand}' and to monitor the " +
                $"results of the first execution with '{renewCommand}'.");

        }

        public Renewal Convert(LegacyScheduledRenewal legacy)
        {
            // Note that history is not moved, so all imported renewals
            // will be due immediately. That's the ulimate test to see 
            // if they will actually work in the new ACMEv2 environment

            var ret = Renewal.Create();
            ConvertTarget(legacy, ret);
            ConvertValidation(legacy, ret);
            ConvertStore(legacy, ret);
            ConvertInstallation(legacy, ret);
            ret.CsrPluginOptions = new RsaOptions();
            ret.LastFriendlyName = legacy.Binding?.Host;
            ret.History = [
                new("Imported") { }
            ];
            return ret;
        }

        public static void ConvertTarget(LegacyScheduledRenewal legacy, Renewal ret)
        {
            if (legacy.Binding == null)
            {
                throw new Exception("Cannot convert renewal with empty binding");
            }
            if (string.IsNullOrEmpty(legacy.Binding.TargetPluginName))
            {
                legacy.Binding.TargetPluginName = legacy.Binding.PluginName switch
                {
                    "IIS" => legacy.Binding.HostIsDns == false ? "IISSite" : "IISBinding",
                    "IISSiteServer" => "IISSites",
                    _ => "Manual",
                };
            }
            switch (legacy.Binding.TargetPluginName.ToLower())
            {
                case "iisbinding":
                    var options = new Target.IISOptions();
                    if (!string.IsNullOrEmpty(legacy.Binding.Host))
                    {
                        options.IncludeHosts = [legacy.Binding.Host];
                    }
                    var siteId = legacy.Binding.TargetSiteId ?? legacy.Binding.SiteId ?? 0;
                    if (siteId > 0)
                    {
                        options.IncludeSiteIds = [siteId];
                    }
                    ret.TargetPluginOptions = options;
                    break;
                case "iissite":
                    options = new Target.IISOptions();
                    if (!string.IsNullOrEmpty(legacy.Binding.CommonName))
                    {
                        options.CommonName = legacy.Binding.CommonName.ConvertPunycode();
                    }
                    siteId = legacy.Binding.TargetSiteId ?? legacy.Binding.SiteId ?? 0;
                    if (siteId > 0)
                    {
                        options.IncludeSiteIds = [siteId];
                    }
                    options.ExcludeHosts = legacy.Binding.ExcludeBindings.ParseCsv();
                    ret.TargetPluginOptions = options;
                    break;
                case "iissites":
                    options = new Target.IISOptions();
                    if (!string.IsNullOrEmpty(legacy.Binding.CommonName))
                    {
                        options.CommonName = legacy.Binding.CommonName.ConvertPunycode();
                    }
                    if (!string.IsNullOrEmpty(legacy.Binding.Host))
                    {
                        options.IncludeSiteIds = legacy.Binding.Host.ParseCsv()!.Select(x => long.Parse(x)).ToList();
                    }
                    options.ExcludeHosts = legacy.Binding.ExcludeBindings.ParseCsv();
                    ret.TargetPluginOptions = options;
                    break;
                case "manual":
                    var manual = new Target.ManualOptions()
                    {
                        CommonName = string.IsNullOrEmpty(legacy.Binding.CommonName) ? legacy.Binding.Host : legacy.Binding.CommonName.ConvertPunycode(),
                        AlternativeNames = legacy.Binding.AlternativeNames.Select(x => x.ConvertPunycode()).ToList()
                    };
                    if (!string.IsNullOrEmpty(manual.CommonName) && 
                        !manual.AlternativeNames.Contains(manual.CommonName))
                    {
                        manual.AlternativeNames.Insert(0, manual.CommonName);
                    }
                    ret.TargetPluginOptions = manual;
                    break;
            }
        }

        public void ConvertValidation(LegacyScheduledRenewal legacy, Renewal ret)
        {
            if (legacy.Binding == null)
            {
                throw new Exception("Cannot convert renewal with empty binding");
            }
            // Configure validation
            if (legacy.Binding.ValidationPluginName == null)
            {
                legacy.Binding.ValidationPluginName = "http-01.filesystem";
            }
            switch (legacy.Binding.ValidationPluginName.ToLower())
            {
                case "dns-01.script":
                case "dns-01.dnsscript":
                    ret.ValidationPluginOptions = new Dns.ScriptOptions()
                    {
                        CreateScript = legacy.Binding.DnsScriptOptions?.CreateScript,
                        CreateScriptArguments = "{Identifier} {RecordName} {Token}",
                        DeleteScript = legacy.Binding.DnsScriptOptions?.DeleteScript,
                        DeleteScriptArguments = "{Identifier} {RecordName}"
                    };
                    break;
                case "dns-01.azure":
                    ret.ValidationPluginOptions = new CompatibleAzureOptions()
                    {
                        ClientId = legacy.Binding.DnsAzureOptions?.ClientId,
                        ResourceGroupName = legacy.Binding.DnsAzureOptions?.ResourceGroupName,
                        Secret = new ProtectedString(legacy.Binding.DnsAzureOptions?.Secret),
                        SubscriptionId = legacy.Binding.DnsAzureOptions?.SubscriptionId,
                        TenantId = legacy.Binding.DnsAzureOptions?.TenantId
                    };
                    break;
                case "http-01.ftp":
                    ret.ValidationPluginOptions = new CompatibleHttpOptions("bc27d719-dcf2-41ff-bf08-54db7ea49c48")
                    {
                        CopyWebConfig = legacy.Binding.IIS == true,
                        Path = legacy.Binding.WebRootPath,
                        Credential = new NetworkCredentialOptions(legacy.Binding.HttpFtpOptions?.UserName, legacy.Binding.HttpFtpOptions?.Password)
                    };
                    break;
                case "http-01.sftp":
                    ret.ValidationPluginOptions = new CompatibleHttpOptions("048aa2e7-2bce-4d3e-b731-6e0ed8b8170d")
                    {
                        CopyWebConfig = legacy.Binding.IIS == true,
                        Path = legacy.Binding.WebRootPath,
                        Credential = new NetworkCredentialOptions(legacy.Binding.HttpFtpOptions?.UserName, legacy.Binding.HttpFtpOptions?.Password)
                    };
                    break;
                case "http-01.webdav":
                    var options = new CompatibleHttpOptions("7e191d0e-30d1-47b3-ae2e-442499d33e16")
                    {
                        CopyWebConfig = legacy.Binding.IIS == true,
                        Path = legacy.Binding.WebRootPath
                    };
                    if (legacy.Binding.HttpWebDavOptions != null)
                    {
                        options.Credential = new NetworkCredentialOptions(
                            legacy.Binding.HttpWebDavOptions.UserName,
                            legacy.Binding.HttpWebDavOptions.Password);
                    }
                    ret.ValidationPluginOptions = options;
                    break;
                case "tls-sni-01.iis":
                    log.Warning("TLS-SNI-01 validation was removed from ACMEv2, changing to SelfHosting. Note that this requires port 80 to be public rather than port 443.");
                    ret.ValidationPluginOptions = new Http.SelfHostingOptions();
                    break;
                case "http-01.iis":
                case "http-01.selfhosting":
                    ret.ValidationPluginOptions = new Http.SelfHostingOptions()
                    {
                        Port = legacy.Binding.ValidationPort
                    };
                    break;
                case "http-01.filesystem":
                default:
                    ret.ValidationPluginOptions = new Http.FileSystemOptions()
                    {
                        CopyWebConfig = legacy.Binding.IIS == true,
                        Path = legacy.Binding.WebRootPath,
                        SiteId = legacy.Binding.ValidationSiteId
                    };
                    break;
            }
        }

        public void ConvertStore(LegacyScheduledRenewal legacy, Renewal ret)
        {
            // Configure store
            if (!string.IsNullOrEmpty(legacy.CentralSslStore))
            {
                ret.StorePluginOptions.Add(new Store.CentralSslOptions()
                {
                    Path = legacy.CentralSslStore,
                    KeepExisting = legacy.KeepExisting == true
                });
            }
            else
            {
                ret.StorePluginOptions.Add(new Store.CertificateStoreOptions()
                {
                    StoreName = legacy.CertificateStore,
                    KeepExisting = legacy.KeepExisting == true
                });
            }
            ret.StorePluginOptions.Add(new Store.PemFilesOptions()
            {
                Path = settings.Cache.CachePath
            });
            ret.StorePluginOptions.Add(new Store.PfxFileOptions()
            {
                Path = settings.Cache.CachePath
            });
        }

        public static void ConvertInstallation(LegacyScheduledRenewal legacy, Renewal ret)
        {
            if (legacy.Binding == null)
            {
                throw new Exception("Cannot convert renewal with empty binding");
            }
            if (legacy.InstallationPluginNames == null)
            {
                legacy.InstallationPluginNames = [];
                // Based on chosen target
                if (legacy.Binding.TargetPluginName == "IISSite" ||
                    legacy.Binding.TargetPluginName == "IISSites" ||
                    legacy.Binding.TargetPluginName == "IISBinding")
                {
                    legacy.InstallationPluginNames.Add("IIS");
                }

                // Based on command line
                if (!string.IsNullOrEmpty(legacy.Script) || !string.IsNullOrEmpty(legacy.ScriptParameters))
                {
                    legacy.InstallationPluginNames.Add("Manual");
                }

                // Cannot find anything, then it's no installation steps
                if (legacy.InstallationPluginNames.Count == 0)
                {
                    legacy.InstallationPluginNames.Add("None");
                }
            }
            foreach (var legacyName in legacy.InstallationPluginNames)
            {
                switch (legacyName.ToLower())
                {
                    case "iis":
                        ret.InstallationPluginOptions.Add(new Install.IISOptions()
                        {
                            SiteId = legacy.Binding.InstallationSiteId,
                            NewBindingIp = legacy.Binding.SSLIPAddress,
                            NewBindingPort = legacy.Binding.SSLPort
                        });
                        break;
                    case "iisftp":
                        ret.InstallationPluginOptions.Add(new Install.IISOptions()
                        {
                            SiteId = legacy.Binding.FtpSiteId ?? 
                                legacy.Binding.InstallationSiteId ?? 
                                legacy.Binding.SiteId ?? 
                                0
                        });
                        break;
                    case "manual":
                        ret.InstallationPluginOptions.Add(new Install.ScriptOptions()
                        {
                            Script = legacy.Script,
                            ScriptParameters = legacy.ScriptParameters
                        });
                        break;
                    case "none":
                        ret.InstallationPluginOptions.Add(new Install.NullOptions());
                        break;
                }
            }
        }
    }
}
