using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using PKISharp.WACS.Plugins.ValidationPlugins.Dns.Internal.Models;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns.Internal;

internal interface IHetznerClient : IDisposable
{
    Task<HetznerZone?> GetZoneAsync(string zoneId);

    Task<IReadOnlyCollection<HetznerZone>> GetAllActiveZonesAsync();

    Task<bool> CreateRecordAsync(HetznerRecord record);

    Task DeleteRecordAsync(HetznerRecord record);
}