using Newtonsoft.Json;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using Qiniu.Http;
using Qiniu.Util;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    [IPlugin.Plugin1<
        QiNiuOptions, QiNiuOptionsFactory, 
        DefaultCapability, QiNiuJson, QiNiuArguments>
        ("3ab6e0fd-fc50-fa7b-6e90-3e1aedd0886f",
        Trigger, "Create PEM encoded to qiniu ssl",External = true)]
    internal class QiNiu(
            ILogService log,
            ISettings settings,
            QiNiuOptions options,
            SecretServiceManager secretServiceManager) : IStorePlugin
    {

        internal const string Trigger = "QiNiuSSL";

        public async Task<StoreInfo?> Save(ICertificateInfo input)
        {
            var password = await secretServiceManager.EvaluateSecret(options.Password?.Value ?? settings.Store.PemFiles.DefaultPassword);
            var qiNiuServer = await secretServiceManager.EvaluateSecret(options.QiNiuServer);
            var accessKey = await secretServiceManager.EvaluateSecret(options.AccessKey);
            var secretKey = await secretServiceManager.EvaluateSecret(options.SecretKey);
            try
            {

                // Base certificate
                var certificateExport = input.Certificate.GetEncoded();
                var certString = PemService.GetPem("CERTIFICATE", certificateExport);
                var chainString = "";
                foreach (var chainCertificate in input.Chain)
                {
                    if (chainCertificate.SubjectDN.ToString() != chainCertificate.IssuerDN.ToString())
                    {
                        var chainCertificateExport = chainCertificate.GetEncoded();
                        chainString += PemService.GetPem("CERTIFICATE", chainCertificateExport);
                    }
                }
                string ca = certString + chainString;
                string pri = string.Empty;

                // Private key
                if (input.PrivateKey != null)
                {
                    var pkPem = PemService.GetPem(input.PrivateKey, password);
                    if (!string.IsNullOrEmpty(pkPem))
                    {
                        pri = pkPem;
                    }
                }
                if (string.IsNullOrEmpty(pri)) {
                    log.Error("QiNiu does not have a private key and thus cannot create.");
                    return null;
                }
                // upload ssl
                Mac mac = new Mac(accessKey, secretKey);
                Auth auth = new Auth(mac);
                HttpManager httpManager = new();
                dynamic sslObj = new {
                    name = input.FriendlyName,
                    common_name = input.CommonName?.Value ?? input.SanNames.First().Value,
                    ca,
                    pri
                };
                string url = $"{qiNiuServer}/sslcert";
                string jsonBody = JsonConvert.SerializeObject(sslObj);
                StringDictionary headers = new StringDictionary();
                headers["Content-Type"] = ContentType.APPLICATION_JSON;
                string token = auth.CreateManageTokenV2("POST",url, headers, jsonBody);
                //send post
                var httpResult = httpManager.PostJson(url,jsonBody,token);
                if (httpResult.Code != 200)
                {
                    //Erro 
                    log.Error("An exception occurred while creating the certificate using the QiNiu service. " + httpResult.Code);
                    return null;
                }
                else {
                    dynamic resultObj = JsonConvert.DeserializeObject<dynamic>(httpResult.Text);
                    //读取Id值
                    return new StoreInfo()
                    {
                        Name = Trigger,
                        Path = resultObj.certID
                    };
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error exporting Qiniu operation failed");
                return null;
            }
        }

        public Task Delete(ICertificateInfo input) => Task.CompletedTask;
    }
}
