using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.DomainObjects.Identifiers;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PKISharp.WACS.UnitTests.Mock.Clients
{
    internal class MockIISClient : IIISClient<MockSite, MockBinding>
    {
        private readonly ILogService _log;

        public MockIISClient(ILogService log, int version = 10)
        {
            _log = log;
            Version = new Version(version, 0);
            MockSites = [
                new MockSite()
                {
                    Id = 1,
                    Name = "example.com",
                    Path = "C:\\wwwroot\\example",
                    Bindings =
                    [
                        new MockBinding()
                        {
                            Host = HostName.Parse("test.example.com"),
                            Protocol = "http",
                            Port = 80
                        },
                        new MockBinding()
                        {
                            Host = HostName.Parse("alt.example.com"),
                            Protocol = "http",
                            Port = 80
                        },
                        new MockBinding()
                        {
                            Host = HostName.Parse("经/已經.example.com"),
                            Protocol = "http",
                            Port = 80
                        },
                        new MockBinding()
                        {
                            Host = HostName.Parse("four.example.com"),
                            Protocol = "http",
                            Port = 80
                        },
                    ]
                },
                new MockSite()
                {
                    Id = 2,
                    Name = "contoso.com",
                    Path = "C:\\wwwroot\\contoso",
                    Bindings =
                    [
                        new MockBinding()
                        {
                            Host = HostName.Parse("test.contoso.com"),
                            Protocol = "http",
                            Port = 80
                        },
                        new MockBinding()
                        {
                            Host = HostName.Parse("alt.contoso.com"),
                            Protocol = "http",
                            Port = 80
                        },
                        new MockBinding()
                        {
                            Host = HostName.Parse("经/已經.contoso.com"),
                            Protocol = "http",
                            Port = 80
                        },
                        new MockBinding()
                        {
                            Host = HostName.Parse("four.contoso.com"),
                            Protocol = "http",
                            Port = 80
                        },
                    ]
                }
            ];
        }

        public Version Version { get; set; }
        public MockSite[] MockSites { get; set; }

        IEnumerable<IIISSite> IIISClient.Sites => Sites;
        public IEnumerable<MockSite> Sites => MockSites;

        public bool HasFtpSites => Sites.Any(x => x.Type == IISSiteType.Ftp);
        public bool HasWebSites => Sites.Any(x => x.Type == IISSiteType.Web);

        public IISBindingUpdaterContext UpdateHttpSite(IEnumerable<Identifier> identifiers, BindingOptions bindingOptions, byte[]? oldCertificate = null, IEnumerable<Identifier>? allIdentifiers = null)
        {
            var updater = new IISHttpBindingUpdater<MockSite, MockBinding>(this, _log);
            var context = new IISBindingUpdaterContext()
            {
                PartIdentifiers = identifiers,
                BindingOptions = bindingOptions,
                AllIdentifiers = allIdentifiers,
                PreviousCertificate = oldCertificate
            };
            updater.AddOrUpdateBindings(context);
            if (context.TouchedBindings > 0)
            {
                if (bindingOptions.SiteId == null)
                {
                    _log.Information("Committing {count} {type} binding changes to IIS", context.TouchedBindings, "https");
                }
                else
                {
                    _log.Information("Committing {count} {type} binding changes to IIS while updating site {site}", context.TouchedBindings, "https", bindingOptions.SiteId);
                }
            }
            else
            {
                if (bindingOptions.SiteId == null)
                {
                    _log.Information("No bindings have been changed in IIS");
                }
                else
                {
                    _log.Information("No bindings have been changed while updating site {site}", bindingOptions.SiteId);
                }
            }
            return context;
        }
        public MockSite GetSite(long id, IISSiteType? type = null) => Sites.First(x => id == x.Id && (type == null || x.Type == type));
        public IISBindingUpdaterContext UpdateFtpSite(long? FtpSiteId, string? store, ICertificateInfo newCertificate, ICertificateInfo? oldCertificate) => new();
        IIISSite IIISClient.GetSite(long id, IISSiteType? type) => GetSite(id, type);

        public IIISBinding AddBinding(MockSite site, BindingOptions bindingOptions) {
            var newBinding = new MockBinding(bindingOptions);
            site.Bindings.Add(newBinding);
            return newBinding;
        } 

        public void UpdateBinding(MockSite site, MockBinding binding, BindingOptions bindingOptions)
        {
            _ = site.Bindings.Remove(binding);
            var updateOptions = bindingOptions
                .WithHost(binding.Host)
                .WithIP(binding.IP)
                .WithPort(binding.Port);
            site.Bindings.Add(new MockBinding(updateOptions) { Id = binding.Id });
        }

        public void Refresh()
        {
        }
        public void ReplaceCertificate(CertificateInfo newCertificate, CertificateInfo oldCertificate)
        {
            throw new NotImplementedException();
        }
    }

    [DebuggerDisplay("{Id}: {Name}")]
    internal class MockSite : IIISSite<MockBinding>
    {
        IEnumerable<IIISBinding> IIISSite.Bindings => Bindings;
        public List<MockBinding> Bindings { get; set; } = [];
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        IEnumerable<MockBinding> IIISSite<MockBinding>.Bindings => Bindings;
        public IISSiteType Type => IISSiteType.Web;
    }

    [DebuggerDisplay("{Id}: {BindingInformation}")]
    internal class MockBinding : IIISBinding
    {
        public MockBinding() { }
        public MockBinding(BindingOptions options)
        {
            Host = options.Host;
            Protocol = "https";
            Port = options.Port;
            CertificateHash = options.Thumbprint;
            CertificateStoreName = options.Store ?? "";
            IP = options.IP;
            SSLFlags = options.Flags;
        }

        public int Id { get; set; }
        public HostName Host { get; set; } = HostName.Emtpy;
        public string Protocol { get; set; } = "";
        public int Port { get; set; }
        public string IP { get; set; } = "";
        public IEnumerable<byte>? CertificateHash { get; set; }
        public string CertificateStoreName { get; set; } = "";
        public string BindingInformation
        {
            get
            {
                if (field != null)
                {
                    return field;
                }
                else
                {
                    return $"{IP}:{Port}:{Host}";
                }
            }
            set;
        }

        public SSLFlags SSLFlags { get; set; }
        public bool Secure => Protocol == "https" || Protocol == "ftps";
    }
}
