using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace PKISharp.WACS.Clients.IIS
{
    /// <summary>
    /// Class to communicate desired binding state to the IISclient
    /// Follows the fluent/immutable pattern
    /// </summary>
    /// <remarks>
    /// Regular constructor
    /// </remarks>
    /// <param name="flags"></param>
    /// <param name="port"></param>
    /// <param name="ip"></param>
    /// <param name="thumbprint"></param>
    /// <param name="store"></param>
    /// <param name="host"></param>
    /// <param name="siteId"></param>
    [DebuggerDisplay("Binding {Binding}")]
    public class BindingOptions(
        SSLFlags flags = SSLFlags.None,
        int port = IISClient.DefaultBindingPort,
        string ip = IISClient.DefaultBindingIp,
        IEnumerable<byte>? thumbprint = null,
        string? store = null,
        string host = "",
        long? siteId = null)
    {
        /// <summary>
        /// Desired flags 
        /// </summary>
        public SSLFlags Flags { get; } = flags;

        /// <summary>
        /// Port to use when a new binding has to be created
        /// </summary>
        public int Port { get; } = port;

        /// <summary>
        /// IP address to use when a new binding has to be created
        /// </summary>
        public string IP { get; } = ip;

        /// <summary>
        /// Certificate thumbprint that should be set for the binding
        /// </summary>
        public IEnumerable<byte>? Thumbprint { get; } = thumbprint;

        /// <summary>
        /// Certificate store where the certificate can be found
        /// </summary>
        public string? Store { get; } = store;

        /// <summary>
        /// Hostname that should be set for the binding
        /// </summary>
        public string Host { get; } = host;

        /// <summary>
        /// Optional: SiteId where new binding are supposed to be created
        /// </summary>
        public long? SiteId { get; } = siteId;

        /// <summary>
        /// Binding string to use in IIS
        /// </summary>
        public string Binding 
        {
            get
            {
                var formattedIP = IP;
                if (!string.IsNullOrEmpty(formattedIP)) 
                {
                    if (formattedIP != "*")
                    {
                        if (IPAddress.TryParse(formattedIP, out var address))
                        {
                            if (address.AddressFamily == AddressFamily.InterNetworkV6)
                            {
                                formattedIP = $"[{formattedIP}]";
                            }
                        }
                    }
                }
                return $"{formattedIP}:{Port}:{Host}";
            }
        }

        public string Debug => $"{Binding} (site:{SiteId ?? 0}, flags:{Flags})";

        public override string ToString() => Binding;

        public BindingOptions WithFlags(SSLFlags flags) => new(flags, Port, IP, Thumbprint, Store, Host, SiteId);
        public BindingOptions WithPort(int port) => new(Flags, port, IP, Thumbprint, Store, Host, SiteId);
        public BindingOptions WithIP(string ip) => new(Flags, Port, ip, Thumbprint, Store, Host, SiteId);
        public BindingOptions WithThumbprint(byte[] thumbprint) => new(Flags, Port, IP, thumbprint, Store, Host, SiteId);
        public BindingOptions WithStore(string? store) => new(Flags, Port, IP, Thumbprint, store, Host, SiteId);
        public BindingOptions WithHost(string hostName) => new(Flags, Port, IP, Thumbprint, Store, hostName, SiteId);
        public BindingOptions WithSiteId(long? siteId) => new(Flags, Port, IP, Thumbprint, Store, Host, siteId);
    }
}