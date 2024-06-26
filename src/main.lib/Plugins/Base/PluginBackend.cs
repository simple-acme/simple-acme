using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services.Serialization;

namespace PKISharp.WACS.Plugins.Base
{
    internal class PluginBackend<TBackend, TCapability, TOptions>(Plugin meta, TBackend backend, TCapability capability, TOptions options)
        where TCapability : IPluginCapability
        where TBackend : IPlugin
        where TOptions : PluginOptions
    {
        public TBackend Backend { get; } = backend;
        public TCapability Capability { get; } = capability;
        public Plugin Meta { get; } = meta;
        public TOptions Options { get; } = options;
    }
}
