using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    internal class ScriptArguments : BaseArguments
    {
        [CommandLine(Description = $"Path to script that handles both preparation and cleanup, " +
            $"depending on its parameters. If this parameter is provided then " +
            $"--{nameof(ValidationPrepareScript)} and --{nameof(ValidationCleanupScript)} are ignored.")]
        public string? ValidationScript { get; set; }

        [CommandLine( Description = "Script that prepares to handle the validation challenge.")]
        public string? ValidationPrepareScript { get; set; }

        [CommandLine(Description = $"Arguments passed to the preparation script. If not specified " +
            $"these will be \"{ScriptDns.DefaultPrepareArguments}\" for DNS challenges " +
            $"or \"{ScriptHttp.DefaultPrepareArguments}\" for HTTP challenges.")]
        public string? ValidationPrepareScriptArguments { get; set; }

        [CommandLine(Description = "Script that cleans up after validation has completed.")]
        public string? ValidationCleanupScript { get; set; }

        [CommandLine(Description = $"Arguments passed to the cleanup script. If not specified " +
            $"these will be \"{ScriptDns.DefaultCleanupArguments}\" for DNS challenges " +
            $"or \"{ScriptHttp.DefaultCleanupArguments}\" for HTTP challenges.")]
        public string? ValidationCleanupScriptArguments { get; set; }

        [CommandLine(Description = "Configure parallelism mode. " +
            "0 is fully serial (default), " +
            "1 allows multiple preparations to run simultaneously, " +
            "2 allows multiple validations to run simultaneously and " +
            "3 is a combination of both forms of parallelism.")]
        public int? ValidationScriptParallelism { get; set; }

        [CommandLine(Obsolete = true)]
        public string? DnsScript { get; set; }

        [CommandLine(Obsolete = true)]
        public string? DnsCreateScript { get; set; }

        [CommandLine(Obsolete = true)]
        public string? DnsCreateScriptArguments { get; set; }

        [CommandLine(Obsolete = true)]
        public string? DnsDeleteScript { get; set; }

        [CommandLine(Obsolete = true)]
        public string? DnsDeleteScriptArguments { get; set; }

        [CommandLine(Obsolete = true)]
        public int? DnsScriptParallelism { get; set; }
    }
}
