﻿using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using System.Linq;

namespace PKISharp.WACS.Plugins.OrderPlugins
{
    internal class SiteCapability(Target target) : DefaultCapability
    {
        protected readonly Target Target = target;

        public override State ExecutionState
        {
            get
            {
                if (Target.UserCsrBytes != null)
                {
                    return State.DisabledState("Renewals sourced from a custom CSR cannot be split up");
                }
                if (!Target.Parts.Any(p => p.SiteId > 0))
                {
                    return State.DisabledState("No site information included in source");
                }
                return State.EnabledState();
            }
        }    
    }
}
