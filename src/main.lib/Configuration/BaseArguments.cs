using PKISharp.WACS.Extensions;
using PKISharp.WACS.Services;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace PKISharp.WACS.Configuration.Arguments
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
    public abstract class BaseArguments : IArguments
    {
        public virtual string Name => "";
        public virtual string Group => "";
        public virtual bool Active(string[] args) 
        {
            var lower = args.Select(x => x.ToLowerInvariant()).ToList();
            foreach (var (meta, _, _) in GetType().CommandLineProperties())
            {
                var argumentName = meta.ArgumentName.ToLowerInvariant();
                if (lower.Any(x => x == $"--{argumentName}" || x == $"/{argumentName}"))
                {
                    return true;
                }
            }
            return false;
        }
    }
}