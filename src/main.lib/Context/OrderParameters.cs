using PKISharp.WACS.DomainObjects;
using System;

namespace PKISharp.WACS.Context
{
    class OrderParameters
    {
        public DateTime? NotAfter { get; set; }
        public string? Profile { get; set; }
        public ICertificateInfo? Replaces { get; set; }
    }
}
