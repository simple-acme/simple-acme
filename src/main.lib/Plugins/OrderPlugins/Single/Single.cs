using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;

namespace PKISharp.WACS.Plugins.OrderPlugins
{
    [IPlugin.Plugin<
        SingleOptions, PluginOptionsFactory<SingleOptions>, 
        DefaultCapability, WacsJsonPlugins>
        ("b705fa7c-1152-4436-8913-e433d7f84c82", 
        "Single", "Single certificate")]
    internal class Single : IOrderPlugin
    {
        public List<Order> Split(Renewal renewal, Target target) => [new(renewal, target)];
    }
}
