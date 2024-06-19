using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    [SupportedOSPlatform("windows")]
    internal class CertificateStoreOptionsFactory(
        ArgumentsInputService arguments,
        ISettingsService settings,
        IIISClient iisClient) : PluginOptionsFactory<CertificateStoreOptions>
    {
        public override async Task<CertificateStoreOptions?> Aquire(IInputService inputService, RunLevel runLevel)
        {
            var ret = await Default();
            if (ret != null &&
                (await arguments.GetString<CertificateStoreArguments>(x => x.CertificateStore).GetValue()) == null &&
                runLevel.HasFlag(RunLevel.Advanced))
            {
                var currentDefault = CertificateStore.DefaultStore(settings, iisClient);
                var choices = new List<Choice<string?>>();
                if (iisClient.Version.Major > 8)
                {
                    choices.Add(Choice.Create<string?>(
                        "WebHosting", 
                        description: "[WebHosting] - Dedicated store for IIS"));
                }
                choices.Add(Choice.Create<string?>(
                        "My",
                        description: "[My] - General computer store (for Exchange/RDS)"));
                choices.Add(Choice.Create<string?>(
                    null, 
                    description: $"[Default] - Use global default, currently {currentDefault}",
                    @default: true));
                var choice = await inputService.ChooseFromMenu(
                    "Choose store to use, or type the name of another unlisted store",
                    choices,
                    other => Choice.Create<string?>(other));

                // final save
                ret.StoreName = string.IsNullOrWhiteSpace(choice) ? null : choice;
            }
            return ret;
        }

        private ArgumentResult<bool?> KeepExisting => arguments.
            GetBool<CertificateStoreArguments>(x => x.KeepExisting).
            WithDefault(false).
            DefaultAsNull();

        private ArgumentResult<string?> StoreName => arguments.
            GetString<CertificateStoreArguments>(x => x.CertificateStore).
            WithDefault(CertificateStore.DefaultStore(settings, iisClient)).
            DefaultAsNull();

        private ArgumentResult<string?> AclFullControl => arguments.
            GetString<CertificateStoreArguments>(x => x.AclFullControl);

        private ArgumentResult<string?> AclRead => arguments.
            GetString<CertificateStoreArguments>(x => x.AclRead);

        public override async Task<CertificateStoreOptions?> Default()
        {
            return new CertificateStoreOptions
            {
                StoreName = await StoreName.GetValue(),
                KeepExisting = await KeepExisting.GetValue(),
                AclFullControl = (await AclFullControl.GetValue()).ParseCsv(),
                AclRead = (await AclRead.GetValue()).ParseCsv()
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(CertificateStoreOptions options)
        {
            yield return (KeepExisting.Meta, options.KeepExisting);
            yield return (StoreName.Meta, options.StoreName);
            yield return (AclFullControl.Meta, options.AclFullControl);
            yield return (AclRead.Meta, options.AclRead);
        }
    }
}
