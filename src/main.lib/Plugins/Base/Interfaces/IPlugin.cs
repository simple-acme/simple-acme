using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.Interfaces
{
    public interface IPlugin
    {
        /// <summary>
        /// Mark a class as a plugin
        /// Only possible on types that implement IPlugin, as per 
        /// https://blog.marcgravell.com/2009/06/restricting-attribute-usage.html
        /// </summary>
        /// <typeparam name="TOptions"></typeparam>
        /// <typeparam name="TOptionsFactory"></typeparam>
        /// <typeparam name="TJson"></typeparam>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
        protected class PluginAttribute<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TOptions,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TOptionsFactory,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCapability,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJson>
            (string id, string name, string description) : 
            Attribute, IPluginMeta
            where TOptions : PluginOptions, new()
            where TOptionsFactory : IPluginOptionsFactory<TOptions>
            where TJson : JsonSerializerContext
        {
            public Guid Id { get; } = Guid.Parse(id);
            public bool Hidden { get; set; } = false;
            public string Name { get; set; } = name;
            public string Description { get; set; } = description;
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            public Type Options { get; } = typeof(TOptions);
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            public Type OptionsFactory { get; } = typeof(TOptionsFactory);
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            public Type OptionsJson { get; } = typeof(TJson);
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            public Type Capability { get; } = typeof(TCapability);
            public Type? Arguments { get; internal set; } = null;
        }

        /// <summary>
        /// Derivative class with single argument type
        /// </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
        protected sealed class Plugin1Attribute<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TOptions,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TOptionsFactory,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCapability,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJson,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TArguments> :
            PluginAttribute<TOptions, TOptionsFactory, TCapability, TJson>
            where TOptions : PluginOptions, new()
            where TOptionsFactory : IPluginOptionsFactory<TOptions>
            where TJson : JsonSerializerContext
            where TArguments : IArguments
        {
            public Plugin1Attribute(string id, string name, string description) : base(id, name, description) => Arguments = typeof(TArguments);
        }
    }

}
