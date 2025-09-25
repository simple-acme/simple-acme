﻿using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    internal class SelfHostingArguments : BaseArguments, ISelfHosterOptions
    {
        [CommandLine(Description = "Port to use for listening to validation requests. Note that the ACME server will always send requests to port 80. This option is only useful in combination with a port forwarding.")]
        public int? ValidationPort { get; set; }
        
        [CommandLine(Description = "Protocol to use to handle validation requests. Defaults to http but may be set to https if you have automatic redirects setup in your infrastructure before requests hit the web server.")]
        public string? ValidationProtocol { get; set; }

        public int? Port => ValidationPort;
        public bool? Https => ValidationProtocol?.ToLower() == "https";
    }
}
