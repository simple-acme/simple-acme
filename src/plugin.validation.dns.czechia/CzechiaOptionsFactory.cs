using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    internal sealed class CzechiaOptionsFactory(ArgumentsInputService arguments)
        : PluginOptionsFactory<CzechiaOptions>
    {
        private ArgumentResult<string?> ApiBaseUri => arguments.GetString<CzechiaArguments>(a => a.ApiBaseUri);

        private ArgumentResult<ProtectedString?> ApiToken => arguments
            .GetProtectedString<CzechiaArguments>(a => a.ApiToken)
            .Required();

        private ArgumentResult<string?> ZoneName => arguments
            .GetString<CzechiaArguments>(a => a.ZoneName);

        private ArgumentResult<int?> Ttl => arguments.GetInt<CzechiaArguments>(a => a.Ttl);

        public override async Task<CzechiaOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            var baseUri = await ApiBaseUri.Interactive(input).GetValue();
            var token = await ApiToken.Interactive(input).GetValue();
            var zone = await ZoneName.Interactive(input).GetValue();
            var ttl = await Ttl.Interactive(input).GetValue();

            return new CzechiaOptions
            {
                ApiBaseUri = baseUri,
                ApiToken = token,
                ZoneName = zone,
                Ttl = ttl
            };
        }

        public override async Task<CzechiaOptions?> Default()
        {
            return new CzechiaOptions
            {
                ApiBaseUri = await ApiBaseUri.GetValue(),
                ApiToken = await ApiToken.GetValue(),
                ZoneName = await ZoneName.GetValue(),
                Ttl = await Ttl.GetValue()
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(CzechiaOptions options)
        {
            yield return (ApiBaseUri.Meta, options.ApiBaseUri);
            yield return (ApiToken.Meta, options.ApiToken);
            yield return (ZoneName.Meta, options.ZoneName);
            yield return (Ttl.Meta, options.Ttl);
        }
    }
}
