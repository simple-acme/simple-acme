using Microsoft.Web.Administration;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.DomainObjects.Identifiers;
using PKISharp.WACS.Extensions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PKISharp.WACS.Clients.IIS
{
    /// <summary>
    /// Standard real implementation for IIS-site. Other 
    /// </summary>
    [DebuggerDisplay("Site {Id}")]
    internal class IISSiteWrapper : IIISSite<IISBindingWrapper>
    {
        internal Site Site { get; }

        public long Id => Site.Id;
        public string Name => Site.Name;
        public string Path => Site.WebRoot();

        IEnumerable<IIISBinding> IIISSite.Bindings => Bindings;
        public IEnumerable<IISBindingWrapper> Bindings { get; private set; }
        public IISSiteType Type { get; private set; } = IISSiteType.Unknown;

        public IISSiteWrapper(Site site)
        {
            var ftpSsl = !string.IsNullOrWhiteSpace(site.
                GetChildElement("ftpServer")?.
                GetChildElement("security")?.
                GetChildElement("ssl")?.
                GetAttributeValue("serverCertHash")?.
                ToString());

            Site = site;
            Bindings = [.. site.Bindings.Select(x =>
            {
                var secure = x.Protocol == "https" || ((x.Protocol == "ftp") && ftpSsl);
                try
                {
                    return new IISBindingWrapper(x, secure);
                }
                catch
                {
                    // Invalid binding, e.g. when we're unable to parse the host name
                    return null;
                }
            }).OfType<IISBindingWrapper>()];

            if (Bindings.All(b => b.Protocol == "ftp" || b.Protocol == "ftps"))
            {
                Type = IISSiteType.Ftp;
            } 
            else if (Bindings.Any(b => b.Protocol == "http" || b.Protocol == "https"))
            {
                Type = IISSiteType.Web;
            }
        }
    }

    [DebuggerDisplay("{BindingInformation}")]
    internal class IISBindingWrapper(Binding binding, bool secure) : IIISBinding
    {
        internal Binding Binding { get; } = binding;

        public HostName Host 
        {
            get
            {
                if (field == null)
                {
                    if (Binding.Protocol == "ftp")
                    {
                        var split = BindingInformation.Split(":");
                        field = split.Length == 3 ? HostName.Parse(split[2]) : HostName.Empty;
                    }
                    else
                    {
                        field = HostName.Parse(Binding.Host);
                    }
                }
                return field;
            }
        }

        public string Protocol => Binding.Protocol;
        public int Port => Binding.EndPoint?.Port ?? -1;
        public string? IP 
        {
            get
            {
                var address = Binding.EndPoint?.Address;
                if (address == null || address.GetAddressBytes().All(b => b == 0))
                {
                    return null;
                } 
                else
                {
                    return address.ToString();
                }
            }
        }

        public IEnumerable<byte>? CertificateHash
        {
            get
            {
                try
                {
                    return Binding.CertificateHash;
                }
                catch
                {
                    return null;
                }
            }
        }

        public string CertificateStoreName => Binding.CertificateStoreName;
        public string BindingInformation => Binding.NormalizedBindingInformation();
        public SSLFlags SSLFlags => Binding.SSLFlags();
        public bool Secure { get; private set; } = secure;
    }
}