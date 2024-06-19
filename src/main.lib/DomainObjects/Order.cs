using PKISharp.WACS.Clients.Acme;
using System.Diagnostics;
using Protocol = ACMESharp.Protocol;

namespace PKISharp.WACS.DomainObjects
{
    [DebuggerDisplay("{CacheKeyPart}")]
    public class Order(
        Renewal renewal,
        Target target,
        string? cacheKeyPart = null,
        string? friendlyNamePart = null)
    {
        public string? CacheKeyPart { get; set; } = cacheKeyPart;
        public string? FriendlyNamePart { get; set; } = friendlyNamePart;
        public string? KeyPath { get; set; }
        public Target Target { get; set; } = target;
        public Renewal Renewal { get; set; } = renewal;
        public Protocol.AcmeOrderDetails? Details { get; set; } = null;

        public bool? Valid => Details == null ? 
            null : 
            Details.Payload.Status == AcmeClient.OrderValid || 
            Details.Payload.Status == AcmeClient.OrderReady;

        public string FriendlyNameBase
        {
            get
            {
                var friendlyNameBase = Renewal.FriendlyName;
                if (string.IsNullOrEmpty(friendlyNameBase))
                {
                    friendlyNameBase = Target.FriendlyName;
                }
                if (string.IsNullOrEmpty(friendlyNameBase))
                {
                    friendlyNameBase = Target.DisplayName.Unicode(true).Value;
                }
                return friendlyNameBase;
            }
        }

        public string FriendlyNameIntermediate
        {
            get
            {
                var friendlyNameIntermediate = FriendlyNameBase;
                if (!string.IsNullOrEmpty(FriendlyNamePart))
                {
                    friendlyNameIntermediate += $" [{FriendlyNamePart}]";
                }
                return friendlyNameIntermediate;
            }
        }

    }
}
