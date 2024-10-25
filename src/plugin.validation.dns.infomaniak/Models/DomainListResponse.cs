using Newtonsoft.Json;
using System.Collections.Generic;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Models;

internal class DomainListResponse
{
    [JsonProperty("data")]
    public ICollection<DomainListResponseDomain>? Data { get; set; }
}