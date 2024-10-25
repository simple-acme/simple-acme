using ACMESharp.Protocol.Resources;
using PKISharp.WACS.DomainObjects;

namespace PKISharp.WACS.Context
{
    public class AuthorizationContext(OrderContext order, AcmeAuthorization authorization, string uri)
    {
        public AcmeAuthorization Authorization { get; } = authorization;
        public OrderContext Order { get; } = order;
        public string Uri { get; } = uri;
        public string Label { get; } = Identifier.Parse(authorization).Unicode(true).Value;
    }
}
