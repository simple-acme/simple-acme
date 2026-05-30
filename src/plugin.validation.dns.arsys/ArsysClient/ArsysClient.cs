using PKISharp.WACS.Plugins.ValidationPlugins.Dns.Arsys;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;

// TODO: REMOVE THESE DIRECTIVES - the final code should be without any warnings
#pragma warning disable CS1998
#pragma warning disable CA1822
#pragma warning disable IDE0060

namespace PKISharp.WACS.Plugins.ValidationPlugins.ArsysClient
{
    class ArsysClient
    {
        private readonly WebServicesPortTypeClient _client;
        private readonly string _domain;

        /// <summary>
        /// Create a Arsys DNS client
        /// </summary>
        /// <param name="DNSApiKey">The API key needed for the user</param>
        /// <param name="domain">The SLD domain, This is also the user of the API key</param>
        public ArsysClient(string DNSApiKey, string domain)
        {
            _domain = domain;
            var customEncodingElement = new CustomTextMessageBindingElement(); // Only ISO-8859-1 is allowed by the API. C# SOAP generator will only allow UTF-8, so a custom class needs to be created
            var transportElement = new HttpsTransportBindingElement(); // Only Https
            var customBinding = new CustomBinding(customEncodingElement, transportElement);

            _client = new WebServicesPortTypeClient(customBinding, new EndpointAddress("https://api.servidoresdns.net:54321/hosting/api/soap/index.php"));

            _client.Endpoint.EndpointBehaviors.Add(new BasicAuthEndpointBehavior(domain, DNSApiKey)); // We need to add the auth before each call. This makes this step automatic.
        }

        /// <summary>
        /// Create a TXT record at Arsys DNS
        /// </summary>
        /// <param name="host"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal async Task<CreateDNSEntryResponse1> CreateTxtRecord(string host, string value)
        {
            return await _client.CreateDNSEntryAsync(new CreateDNSEntryRequest
            {
                domain = _domain,
                dns = host,
                type = "txt",
                value = value
            });
        }

        /// <summary>
        /// Delete an TXT record at Arsys DNS
        /// </summary>
        /// <param name="host"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal async Task<DeleteDNSEntryResponse1> DeleteTxtRecord(string host, string value)
        {
            return await _client.DeleteDNSEntryAsync(new DeleteDNSEntryRequest
            {
                domain = _domain,
                dns = host,
                type = "txt",
                value = value
            });
        }
    }
}
