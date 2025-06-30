using PKISharp.WACS.DomainObjects;
using System.Collections.Generic;

namespace PKISharp.WACS.Clients.IIS
{
    public class IISHttpBindingUpdaterContext
    {
        public IEnumerable<Identifier> PartIdentifiers { get; set; } = [];
        public IEnumerable<Identifier>? AllIdentifiers { get; set; }
        public IEnumerable<(IIISSite site, IIISBinding binding)> AllBindings { get; set; } = [];
        public BindingOptions BindingOptions { get; set; } = new BindingOptions();
        public IEnumerable<byte>? PreviousCertificate { get; set; }
        public List<string> UpdatedBindings { get; } = [];
        public List<string> AddedBindings { get; } = [];
        public int TouchedBindings => UpdatedBindings.Count + AddedBindings.Count;
    }
}