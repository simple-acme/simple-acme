using System.Collections.Generic;

namespace PKISharp.WACS.Plugins.TargetPlugins
{
    internal class IISSiteOptions : IISOptions
    {
        public long? SiteId
        {
            get => null;
            set
            {
                if (IncludeSiteIds == null && value.HasValue)
                {
                    IncludeSiteIds = [value.Value];
                }
            }
        }

        public List<string>? ExcludeBindings {
            get => null;
            set => ExcludeHosts ??= value;
        }
    }
}
