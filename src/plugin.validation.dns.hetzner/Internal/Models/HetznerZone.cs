using System.Diagnostics.CodeAnalysis;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns.Internal.Models;

internal sealed record HetznerZone(string Id, string Name)
{
    public HetznerZone(int id, string name)
        : this(id.ToString(), name)
        { }
}