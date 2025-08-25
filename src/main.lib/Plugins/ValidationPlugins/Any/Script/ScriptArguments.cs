using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    internal class ScriptArguments : BaseArguments
    {
        [CommandLine(Description = "Path to script that handles both preparation and cleanup, depending on its parameters. If this parameter is provided then --dnscreatescript and --dnsdeletescript are ignored.")]
        public string? Script { get; set; }

        [CommandLine(Obsolete = true, Description = "Path to script that handles both preparation and cleanup, depending on its parameters. If this parameter is provided then --dnscreatescript and --dnsdeletescript are ignored.")]
        public string? DnsScript { get; set; }

        [CommandLine(Description = "Script that prepares to handle the validation challenge.")]
        public string? DnsCreateScript { get; set; }

        [CommandLine(Description = $"Arguments passed to the preparation script. If not specified these will be \"{ScriptDns.DefaultCreateArguments}\" for DNS challenges or \"{ScriptHttp.DefaultCreateArguments}\" for HTTP challenges.")]
        public string? DnsCreateScriptArguments { get; set; }

        [CommandLine(Description = "Script that cleans up after validation has completed.")]
        public string? DnsDeleteScript { get; set; }

        [CommandLine(Description = $"Arguments passed to the cleanup script. If not specified these will be \"{ScriptDns.DefaultDeleteArguments} \" for DNS challenges or \" {ScriptHttp.DefaultDeleteArguments}\" for HTTP challenges.")]
        public string? DnsDeleteScriptArguments { get; set; }

        [CommandLine(Description = "Configure parallelism mode. " +
            "0 is fully serial (default), " +
            "1 allows multiple preparations to run simultaneously, " +
            "2 allows multiple validations to run simultaneously and " +
            "3 is a combination of both forms of parallelism.")]
        public int? DnsScriptParallelism { get; set; }
    }
}
