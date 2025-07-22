using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.InstallationPlugins
{
    internal class IISOptionsFactory<TOptions>(IIISClient iisClient, Target target, ArgumentsInputService arguments) : PluginOptionsFactory<TOptions>
        where TOptions: IISOptions, new()
    {
        public override int Order => 5;

        private ArgumentResult<int?> NewBindingPort => arguments.
            GetInt<IISArguments>(x => x.SSLPort).
            WithDefault(IISClient.DefaultBindingPort).
            DefaultAsNull().
            Validate(x => Task.FromResult(x >= 1), "invalid port").
            Validate(x => Task.FromResult(x <= 65535), "invalid port");

        private ArgumentResult<string?> NewBindingIp => arguments.
            GetString<IISArguments>(x => x.SSLIPAddress).
            WithDefault(IISClient.DefaultBindingIp).
            DefaultAsNull().
            Validate(x => Task.FromResult(x == "*" || IPAddress.Parse(x!) != null), "invalid address");

        private ArgumentResult<long?> InstallationSite => arguments.
            GetLong<IISArguments>(x => x.InstallationSiteId).
            Validate(x => Task.FromResult(iisClient.GetSite(x!.Value) != null), "invalid site");

        private ArgumentResult<long?> FtpSite => arguments.
            GetLong<IISArguments>(x => x.FtpSiteId).
            Validate(x => Task.FromResult(iisClient.GetSite(x!.Value) != null), "invalid site").
            Validate(x => Task.FromResult(iisClient.GetSite(x!.Value).Type == IISSiteType.Ftp), "not an ftp site");

        public override async Task<TOptions?> Aquire(IInputService inputService, RunLevel runLevel)
        {
            var ret = new TOptions()
            {
                NewBindingPort = await NewBindingPort.GetValue(),
                NewBindingIp = await NewBindingIp.GetValue()
            };

            var explained = false;
            void explain()
            {
                if (explained)
                {
                    return;
                }
                inputService.CreateSpace();
                inputService.Show(null,
                       "This plugin will update *all* binding using the previous certificate in both Web and " +
                       "FTP sites, regardless of whether those bindings were created manually or by the program " +
                       "itself. Therefore, you'll never need to run this installation step twice.");
                inputService.CreateSpace();
                inputService.Show(null,
                    "If new bindings are needed, by default it will create those at " +
                    "the same site where the HTTP binding for that host was found.");
                explained = true;
            }

            var askSite = !target.IIS;
            if (target.IIS && runLevel.HasFlag(RunLevel.Advanced))
            {
                explain();
                askSite = await inputService.PromptYesNo("Create new bindings in a different site?", false);
            }
            if (askSite)
            {
                explain();
                var chosen = await inputService.ChooseOptional("Choose site to create new https bindings",
                   iisClient.Sites,
                   x => Choice.Create<long?>(x.Id, x.Name, x.Id.ToString()), "Do not create new bindings (only update existing ones)");
                ret.SiteId = chosen;
            }
            return ret;
        }

        public override async Task<TOptions?> Default()
        {
            var siteId = await FtpSite.GetValue();
            siteId ??= await InstallationSite.GetValue();
            var ret = new TOptions()
            {
                NewBindingPort = await NewBindingPort.GetValue(),
                NewBindingIp = await NewBindingIp.GetValue(),
                SiteId = siteId
            };
            return ret;
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(TOptions options)
        {
            yield return (NewBindingPort.Meta, options.NewBindingPort);
            yield return (NewBindingIp.Meta, options.NewBindingIp);
            yield return (InstallationSite.Meta, options.SiteId);
        }
    }

    /// <summary>
    /// FTP options factory
    /// </summary>
    internal class IISFTPOptionsFactory(IIISClient iisClient, Target target, ArgumentsInputService arguments) : IISOptionsFactory<IISFtpOptions>(iisClient, target, arguments)
    {
    }

    /// <summary>
    /// Regular options factory
    /// </summary>
    internal class IISOptionsFactory(IIISClient iisClient, Target target, ArgumentsInputService arguments) : IISOptionsFactory<IISOptions>(iisClient, target, arguments)
    {
    }
}