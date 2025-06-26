using PKISharp.WACS.DomainObjects;
using System;
using System.Collections.Generic;

namespace PKISharp.WACS.Clients.IIS
{
    public enum IISSiteType
    {
        Web,
        Ftp,
        Unknown
    }

    [Flags]
    public enum ReplaceMode
    {
        None = 0,
        Thumbprint = 1,
        ExactMatch = 2,
        WildcardMatch = 4,
        Default = Thumbprint
    }

    [Flags]
    public enum AddMode
    {
        None = 0,
        Single = 1,
        Multiple = 2,
        Default = Single
    }

    public interface IIISClient
    {
        void Refresh();
        IEnumerable<IIISSite> Sites { get; }
        IIISSite GetSite(long id, IISSiteType? type = null);
        bool HasFtpSites { get; }
        bool HasWebSites { get; }
        Version Version { get; }
        IISHttpBindingUpdaterContext UpdateHttpSite(
            IEnumerable<Identifier> partIdentifiers, 
            BindingOptions bindingOptions, 
            byte[]? oldCertificate = null, 
            IEnumerable<Identifier>? allIdentifiers = null, 
            ReplaceMode replaceMode = ReplaceMode.Default, 
            AddMode addMode = AddMode.Default);
        void UpdateFtpSite(long? id, string? store, ICertificateInfo newCertificate, ICertificateInfo? oldCertificate);
    }

    public interface IIISClient<TSite, TBinding> : IIISClient
        where TSite : IIISSite<TBinding>
        where TBinding : IIISBinding
    {
        IIISBinding AddBinding(TSite site, BindingOptions bindingOptions);
        void UpdateBinding(TSite site, TBinding binding, BindingOptions bindingOptions);
        new IEnumerable<TSite> Sites { get; }
        new TSite GetSite(long id, IISSiteType? type);

    }

    public interface IIISSite
    {
        long Id { get; }
        IISSiteType Type { get; }
        string Name { get; }
        string Path { get; }
        IEnumerable<IIISBinding> Bindings { get; }
    }

    public interface IIISSite<TBinding> : IIISSite
        where TBinding : IIISBinding
    {
        new IEnumerable<TBinding> Bindings { get; }
    }

    public interface IIISBinding
    {
        string Host { get; }
        string Protocol { get; }
        bool Secure { get; }
        IEnumerable<byte>? CertificateHash { get; }
        string CertificateStoreName { get; }
        string BindingInformation { get; }
        string? IP { get; }
        SSLFlags SSLFlags { get; }
        int Port { get; }
    }
}