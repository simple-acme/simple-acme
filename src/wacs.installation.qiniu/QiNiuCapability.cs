using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using System;
using System.Collections.Generic;
using PKISharp.WACS.Plugins.StorePlugins;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.InstallationPlugins
{
    internal class QiNiuCapability: InstallationCapability
    {

        public override State CanInstall(IEnumerable<Type> storeTypes, IEnumerable<Type> installationTypes)
        {
            foreach (var type in storeTypes)
            {
                if (type.Name == "QiNiu") { 
                    return State.EnabledState();
                }
            }
            return State.DisabledState("Requires QiNiu store plugin");
        }

    }
}
