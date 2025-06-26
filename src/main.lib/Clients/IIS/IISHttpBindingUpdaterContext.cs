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
        public byte[]? PreviousCertificate { get; set; }
        public ReplaceMode ReplaceMode { get; set; } = ReplaceMode.Default;
        public AddMode AddMode { get; set; } = AddMode.Default;
        public List<string> UpdatedBindings { get; } = [];
        public List<string> AddedBindings { get; } = [];
        public int TouchedBindings => UpdatedBindings.Count + AddedBindings.Count;
    }
}