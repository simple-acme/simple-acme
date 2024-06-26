using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services.Serialization;

namespace PKISharp.WACS.Plugins.Base
{
    internal class PluginFrontend<TCapability, TOptions>(Plugin meta, IPluginOptionsFactory<TOptions> factory, TCapability capability)
        where TCapability : IPluginCapability
        where TOptions : PluginOptionsBase, new()
    {
        public IPluginOptionsFactory<TOptions> OptionsFactory { get; } = factory;
        public TCapability Capability { get; } = capability;
        public Plugin Meta { get; } = meta;
    }
}
