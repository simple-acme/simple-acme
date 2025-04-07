using PKISharp.WACS.Context;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [IPlugin.Plugin<
        NullOptions, PluginOptionsFactory<NullOptions>,
        DnsValidationCapability, WacsJsonPlugins>
        ("a37b41dc-b45a-42fe-8d81-82ca409a5491",
        "none", "Domains should be pre-authorized with the server outside of simple-acme",
        Name = "None")]
    class Null : IValidationPlugin
    {
        public ParallelOperations Parallelism => ParallelOperations.Answer | ParallelOperations.Prepare | ParallelOperations.Reuse;
        public Task CleanUp() => Task.CompletedTask;
        public Task Commit() => Task.CompletedTask;
        public Task PrepareChallenge(ValidationContext context) => throw new InvalidOperationException();
    }
}
