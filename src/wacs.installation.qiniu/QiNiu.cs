using Newtonsoft.Json;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using Qiniu.Http;
using Qiniu.Util;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PKISharp.WACS.Plugins.InstallationPlugins
{
    [IPlugin.Plugin1<
        QiNiuOptions, QiNiuOptionsFactory,
        QiNiuCapability, QiNiuJson, QiNiuArguments>
        ("662620b5-e512-8fb6-8206-7dfebf89cb44",
        "QiNiuCDN", "Enable HTTPS for QiNiu CDN",
        External = true)]
    internal class QiNiu(
        QiNiuOptions options,
        ILogService log,
        SecretServiceManager secretServiceManager) : IInstallationPlugin
    {
        public async Task<bool> Install(Dictionary<Type, StoreInfo> storeInfo, ICertificateInfo newCertificateInfo, ICertificateInfo? oldCertificateInfo)
        {
            var qiNiuServer = await secretServiceManager.EvaluateSecret(options.QiNiuServer);
            var accessKey = await secretServiceManager.EvaluateSecret(options.AccessKey);
            var secretKey = await secretServiceManager.EvaluateSecret(options.SecretKey);
            // QiNiu
            Mac mac = new(accessKey, secretKey);
            Auth auth = new(mac);
            HttpManager httpManager = new();
            var info = storeInfo.Values.FirstOrDefault(x=>x.Name == "QiNiuSSL");
            if (info == null) {
                var errorMessage = "The QiNiuCDN installation plugin requires the QiNiuSSL store plugin";
                log.Error(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
            var certId = info.Path;
            if (string.IsNullOrEmpty(certId)) {
                var errorMessage = $"The QiNiuCDN installation plugin requires the QiNiuSSL store plugin need Parameter: certID[{certId}]";
                log.Error(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
            int counter = 0;
            //Obtain domain name information and re-bind the certificate ID
            foreach (var sanName in newCertificateInfo.SanNames)
            {
                if (sanName.Type == IdentifierType.DnsName)
                {
                    //query info
                    var queryResult = httpManager.Get($"{qiNiuServer}/domain/{sanName.Value}",null, auth);
                    if (queryResult.Code != 200) {
                        //not found
                        log.Error($"qiniu {sanName.Value} {queryResult.Code} {queryResult.Text}");
                        continue;
                    }
                    dynamic domainInfo = JsonConvert.DeserializeObject<dynamic>(queryResult.Text);
                    //check domain exist
                    dynamic objData;
                    string meth;
                    if (domainInfo.protocol == "https")
                    {
                        //Modify the certificate
                        objData = new
                        {
                            certId,
                            domainInfo.https.forceHttps,
                            domainInfo.https.http2Enabled
                        };
                        meth = "httpsconf";
                    }
                    else {
                        //Enable https
                        objData = new
                        {
                            certId,
                            forceHttps = true,
                            http2Enabled = true,
                            TlsVersion = new string[] { "TLSv1.0", "TLSv1.1", "TLSv1.2", "TLSv1.3" }
                        };
                        meth = "sslize";
                    }
                    string jsonBody = JsonConvert.SerializeObject(objData);
                    StringDictionary headers = new StringDictionary();
                    headers["Content-Type"] = ContentType.APPLICATION_JSON;
                    string sendUrl = $"{qiNiuServer}/domain/{sanName.Value}/{meth}";
                    string token = auth.CreateManageTokenV2("PUT", sendUrl, headers, jsonBody);
                    //send put
                    var byteBody = Encoding.UTF8.GetBytes(jsonBody);
                    HttpRequestOptions httpRequestOptions = httpManager.CreateHttpRequestOptions("PUT", sendUrl, null, token);
                    httpRequestOptions.Headers.Add("Content-Type", ContentType.APPLICATION_JSON);
                    httpRequestOptions.AllowWriteStreamBuffering = true;
                    httpRequestOptions.RequestData = byteBody;
                    var changeResult = httpManager.SendRequest(httpRequestOptions);
                    if (changeResult.Code != 200)
                    {
                        //error
                        log.Error($"qiniu {meth} {sanName.Value} {changeResult.Code} {changeResult.Text}");
                    }
                    else {
                        //succeed
                        counter++;
                    }
                }
                else {
                    log.Warning($"ignore {sanName.Type} {sanName.Value} ");
                }
            }
            return counter > 0;
        }
    }
}
