using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services.Serialization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.TargetPlugins
{
    [IPlugin.Plugin1<
        ManualOptions, ManualOptionsFactory, 
        DefaultCapability, WacsJsonPlugins, ManualArguments>
        ("e239db3b-b42f-48aa-b64f-46d4f3e9941b", 
        "Manual", "Manually enter host names", "Manual input")]
    internal class Manual(ManualOptions options) : ITargetPlugin
    {
        public Task<Target?> Generate()
        {
            return Task.FromResult<Target?>(
                new Target(
                    $"[{nameof(Manual)}] {options.CommonName ?? options.AlternativeNames.First()}",
                    options.CommonName,
                    [
                        new(options.AlternativeNames.Select(ParseIdentifier))
                    ]));
        }

        internal static Identifier ParseIdentifier(string identifier)
        {
            if (IPAddress.TryParse(identifier, out var address))
            {
                return new IpIdentifier(address);
            }
            return new DnsIdentifier(identifier);
        }
    }
}