using ACMESharp.Protocol.Resources;
using PKISharp.WACS.Context;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    [IPlugin.Plugin<
        NullOptions, PluginOptionsFactory<NullOptions>,
        AnyValidationCapability, WacsJsonPlugins>
        ("a37b41dc-b45a-42fe-8d81-82ca409a5491",
        "none", "Certificate(s) are pre-authorized outside of simple-acme",
        Name = "None")]
    class Null : IValidationPlugin
    {
        public ParallelOperations Parallelism => ParallelOperations.Answer | ParallelOperations.Prepare | ParallelOperations.Reuse;
        public Task CleanUp() => Task.CompletedTask;
        public Task Commit() => Task.CompletedTask;
        public Task<bool> PrepareChallenge(ValidationContext context) => Task.FromResult(true);
        public Task<AcmeChallenge?> SelectChallenge(List<AcmeChallenge> supportedChallenges) => Task.FromResult(supportedChallenges.FirstOrDefault());
    }
}
