using System;
using System.Diagnostics.CodeAnalysis;

namespace PKISharp.WACS.Plugins.Interfaces
{
    /// <summary>
    /// Base class for the attribute is used to find it easily
    /// </summary>
    public interface IPluginMeta
    {
        public Guid Id { get; }
        public string? Name { get; }
        public string Trigger { get; }
        public string Description { get; }
        public bool Hidden { get; }
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public Type Capability { get; }
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        public Type Options { get; }
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public Type OptionsFactory { get; }
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public Type OptionsJson { get; }
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public Type? Arguments { get; }
        public bool External { get; }
        public bool JsonSchemaPublished { get; }
        public string? Provider { get; }
        public string? Page { get; }
        public string? Download { get; }
    }

}
