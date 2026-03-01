
using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Text;

namespace PKISharp.WACS.Plugins.ValidationPlugins.ArsysClient
{
    public class BasicAuthMessageInspector : IClientMessageInspector
    {
        private readonly string _authHeader;

        public BasicAuthMessageInspector(string username, string password)
        {
            string raw = $"{username}:{password}";
            _authHeader = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            HttpRequestMessageProperty httpRequestProperty;
            if (request.Properties.TryGetValue(HttpRequestMessageProperty.Name, out object property))
            {
                httpRequestProperty = (HttpRequestMessageProperty)property;
            }
            else
            {
                httpRequestProperty = new HttpRequestMessageProperty();
                request.Properties.Add(HttpRequestMessageProperty.Name, httpRequestProperty);
            }

            httpRequestProperty.Headers["Authorization"] = _authHeader;
            return null;
        }

        public void AfterReceiveReply(ref Message reply, object correlationState) { }
    }

    public class BasicAuthEndpointBehavior(string user, string pass) : IEndpointBehavior
    {
        private readonly string _user = user, _pass = pass;

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.ClientMessageInspectors.Add(new BasicAuthMessageInspector(_user, _pass));
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters) { }
        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher) { }
        public void Validate(ServiceEndpoint endpoint) { }
    }
}
