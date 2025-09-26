using PKISharp.WACS.Plugins.Interfaces;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PKISharp.WACS.Plugins
{
    /// <summary>
    /// Metadata for a specific plugin
    /// </summary>
    [DebuggerDisplay("{Backend.Name}")]
    public class BasePlugin([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type source)
    {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public Type Backend { get; set; } = source;
    }

    /// <summary>
    /// Metadata for a specific plugin
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    [DebuggerDisplay("{Backend.Name}")]
    public class Plugin(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] 
        Type source,
        IPluginMeta meta, 
        Steps step) : BasePlugin(source)
    {
        public Guid Id { get; } = meta.Id;
        public Steps Step { get; } = step;

        public string Trigger => meta.Trigger;
        public string Name => meta.Name ?? meta.Trigger;
        public string Description => meta.Description;
        public bool Hidden => meta.Hidden;
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        public Type Options => meta.Options;
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public Type OptionsFactory => meta.OptionsFactory;
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public Type OptionsJson => meta.OptionsJson;
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public Type Capability => meta.Capability;
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public Type? Arguments => meta.Arguments;
        public bool External => meta.External;
        public bool Schema => meta.JsonSchemaPublished;
        public string? Provider => meta.Provider;
        public string? Page => meta.Page;
        public string? Download => meta.Download;
    }
}
