using PKISharp.WACS.DomainObjects;
using System.Collections.Generic;

namespace PKISharp.WACS.Plugins.Interfaces
{
    public interface IOrderPlugin : IPlugin
    {
        List<Order> Split(Renewal renewal, Target target);
    }
}
