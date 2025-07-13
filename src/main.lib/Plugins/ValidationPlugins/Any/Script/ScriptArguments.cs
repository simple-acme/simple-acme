﻿using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    internal class ScriptArguments : BaseArguments
    {
        [CommandLine(Description = "Path to script that creates and deletes validation records, depending on its parameters. If this parameter is provided then --dnscreatescript and --dnsdeletescript are ignored.")]
        public string? DnsScript { get; set; }

        [CommandLine(Description = "Path to script that creates the validation TXT record.")]
        public string? DnsCreateScript { get; set; }

        [CommandLine(Description = "Default parameters passed to the script are \"" + Script.DefaultCreateArguments + "\", but that can be customized using this argument.")]
        public string? DnsCreateScriptArguments { get; set; }

        [CommandLine(Description = "Path to script to remove TXT record.")]
        public string? DnsDeleteScript { get; set; }

        [CommandLine(Description = "Default parameters passed to the script are \"" + Script.DefaultDeleteArguments + "\", but that can be customized using this argument.")]
        public string? DnsDeleteScriptArguments { get; set; }

        [CommandLine(Description = "Configure parallelism mode. " +
            "0 is fully serial (default), " +
            "1 allows multiple records to be created simultaneously, " +
            "2 allows multiple records to be validated simultaneously and " +
            "3 is a combination of both forms of parallelism.")]
        public int? DnsScriptParallelism { get; set; }
    }
}
