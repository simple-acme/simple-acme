using PKISharp.WACS.Clients;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System.Net;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    [IPlugin.Plugin1<
        SftpOptions, SftpOptionsFactory,
        HttpValidationCapability, SftpJson, SftpArguments>   
        ("048aa2e7-2bce-4d3e-b731-6e0ed8b8170d",
        "SFTP", "Upload verification files via SSH-FTP")]
    public class Sftp(
        SftpOptions options,
        HttpValidationParameters pars,
        RunLevel runLevel,
        SecretServiceManager secretService) : HttpValidation<SftpOptions>(options, runLevel, pars)
    {
        private SshFtpClient? _sshFtpClient;
        private async Task<SshFtpClient> GetClient()
        {
            if (_sshFtpClient != null)
            {
                return _sshFtpClient;
            }
            var credential = default(NetworkCredential);
            if (_options.Credential != null)
            {
                credential = await _options.Credential.GetCredential(secretService);
            }
            _sshFtpClient = new SshFtpClient(credential, _log);
            return _sshFtpClient;
        }

        protected override char PathSeparator => '/';

        protected override async Task DeleteFile(string path)
        {
            var client = await GetClient();
            client.Delete(path, SshFtpClient.FileType.File);
        } 

        protected override async Task DeleteFolder(string path)
        {
            var client = await GetClient();
            client.Delete(path, SshFtpClient.FileType.Directory);
        }

        protected override async Task<bool> IsEmpty(string path)
        {
            var client = await GetClient();
            return client.GetFiles(path).Length == 0;
        }

        protected override async Task WriteFile(string path, string content)
        {
            var client = await GetClient();
            client.Upload(path, content);
        }
    }
}
