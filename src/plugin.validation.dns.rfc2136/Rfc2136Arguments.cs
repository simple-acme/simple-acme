using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    public sealed class Rfc2136Arguments : BaseArguments
    {
        [CommandLine(Description = "DNS server host/ip")]
        public string? ServerHost { get; set; }

        [CommandLine(Description = "DNS server port")]
        public int? ServerPort { get; set; }

        [CommandLine(Description = "TSIG key name")]
        public string? TsigKeyName { get; set; }

        [CommandLine(Description = "TSIG key secret (Base64 encoded)", Secret = true)]
        public string? TsigKeySecret { get; set; }

        [CommandLine(Description = "TSIG key algorithm")]
        public string? TsigKeyAlgorithm { get; set; }
    }
}