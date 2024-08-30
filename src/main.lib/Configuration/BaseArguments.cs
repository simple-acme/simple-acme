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
            foreach (var (meta, _, _) in GetType().CommandLineProperties())
            {
                if (args.Select(x => x.ToLower()).Contains(meta.ArgumentName.ToLower()))
                {
                    return true;
                }
            }
            return false;
        }
    }
}