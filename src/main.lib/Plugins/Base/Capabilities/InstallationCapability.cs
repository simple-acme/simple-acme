using PKISharp.WACS.Plugins.Interfaces;
using System;
using System.Collections.Generic;

namespace PKISharp.WACS.Plugins.Base.Capabilities
{
    public class InstallationCapability : DefaultCapability, IInstallationPluginCapability
    {
        public virtual State CanInstall(IEnumerable<Type> storeTypes, IEnumerable<Type> installationTypes) => State.EnabledState();
    }
}
