using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    /// <summary>
    /// Domeneshop DNS validation
    /// </summary>
    internal class DomeneshopOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<DomeneshopOptions>
    {
        private ArgumentResult<ProtectedString?> ClientId => arguments.
            GetProtectedString<DomeneshopArguments>(a => a.ClientId).
            Required();

        private ArgumentResult<ProtectedString?> ClientSecret => arguments.
            GetProtectedString<DomeneshopArguments>(a => a.ClientSecret);

        public override async Task<DomeneshopOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            return new DomeneshopOptions()
            {
                ClientId = await ClientId.Interactive(input).GetValue(),
                ClientSecret = await ClientSecret.Interactive(input).GetValue(),
            };
        }

        public override async Task<DomeneshopOptions?> Default()
        {
            return new DomeneshopOptions()
            {
                ClientId = await ClientId.GetValue(),
                ClientSecret = await ClientSecret.GetValue(),
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(DomeneshopOptions options)
        {
            yield return (ClientId.Meta, options.ClientId);
            yield return (ClientSecret.Meta, options.ClientSecret);
        }
    }
}
