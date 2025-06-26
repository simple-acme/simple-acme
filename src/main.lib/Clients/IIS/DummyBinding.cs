using PKISharp.WACS.DomainObjects;
using System;
using System.Collections.Generic;

namespace PKISharp.WACS.Clients.IIS
{
    internal partial class IISHttpBindingUpdater<TSite, TBinding> 
        where TSite : IIISSite<TBinding>
        where TBinding : IIISBinding
    {
        /// <summary>
        /// Fake binding to break out of the algorithm
        /// when we cannot safely create one
        /// </summary>
        private class DummyBinding : IIISBinding
        {
            private readonly string _host;
            private readonly string _ip;
            public DummyBinding(Identifier identifier)
            {
                if (identifier is DnsIdentifier dns)
                {
                    _ip = "";
                    _host = dns.Value;
                }
                else if (identifier is IpIdentifier ip)
                {
                    _ip = ip.Value;
                    _host = "";
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            public bool Secure => throw new NotImplementedException();
            string IIISBinding.Host => _host;
            string IIISBinding.Protocol => throw new NotImplementedException();
            IEnumerable<byte>? IIISBinding.CertificateHash => throw new NotImplementedException();
            string IIISBinding.CertificateStoreName => throw new NotImplementedException();
            string IIISBinding.BindingInformation => throw new NotImplementedException();
            string? IIISBinding.IP => _ip;
            SSLFlags IIISBinding.SSLFlags => throw new NotImplementedException();
            int IIISBinding.Port => throw new NotImplementedException();
        }
    }
}