using ACMESharp.Protocol;
using PKISharp.WACS.Clients.Acme;
using PKISharp.WACS.Plugins.Interfaces;
using System.Diagnostics;

namespace PKISharp.WACS.DomainObjects
{
    [DebuggerDisplay("CacheKeyPart: {CacheKeyPart}")]
    public class Order(
        Renewal renewal,
        Target target,
        string? cacheKeyPart = null,
        string? friendlyNamePart = null) : IFriendlyNameInfo
    {
        public string? CacheKeyPart { get; } = cacheKeyPart;
        public string? FriendlyNamePart { get; } = friendlyNamePart;
        public string? KeyPath { get; set; }
        public Target Target { get; } = target;
        public Renewal Renewal { get; } = renewal;
        public AcmeOrderDetails? Details { get; set; } = null;

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

        public string GetIntermediate(string? baseName = null) 
        { 
            baseName ??= FriendlyNameBase;
            if (!string.IsNullOrEmpty(FriendlyNamePart))
            {
                return $"{baseName} [{FriendlyNamePart}]";
            }
            return baseName;
        }

    }
}
