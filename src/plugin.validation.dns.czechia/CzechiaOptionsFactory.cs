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
        .GetString<CzechiaArguments>(a => a.ZoneName)
        .Required();

        private ArgumentResult<int?> Ttl => arguments.GetInt<CzechiaArguments>(a => a.Ttl);

        public override async Task<CzechiaOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            var baseUri = (await ApiBaseUri.Interactive(input)
            .WithDefault("https://api.czechia.com/api")
            .GetValue()) ?? "https://api.czechia.com/api";

            var token = await ApiToken.Interactive(input).GetValue();
            var zone = await ZoneName.Interactive(input).GetValue();
            var ttl = await Ttl.Interactive(input).WithDefault(3600).GetValue() ?? 3600;

            return new CzechiaOptions
            {
                ApiBaseUri = baseUri,
                ApiToken = token,
                ZoneName = zone ?? "",
                Ttl = ttl
            };
        }

        public override async Task<CzechiaOptions?> Default()
        {
            var baseUri = await ApiBaseUri.GetValue() ?? "https://api.czechia.com/api";
            var token = await ApiToken.GetValue();
            var zone = await ZoneName.GetValue();
            var ttl = await Ttl.GetValue() ?? 3600;

            return new CzechiaOptions
            {
                ApiBaseUri = baseUri,
                ApiToken = token,
                ZoneName = zone ?? "",
                Ttl = ttl
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
