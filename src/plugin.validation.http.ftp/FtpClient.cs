using FluentFTP;
#if PLUGGABLE
using FluentFTP.GnuTLS;
#endif
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace PKISharp.WACS.Clients
{
    internal class FtpClient(
        NetworkCredentialOptions? options,
        ISettingsService settings,
        ILogService log,
        SecretServiceManager secretService)
    {
        private NetworkCredential? _credential;
        private async Task<NetworkCredential?> GetNetworkCredential()
        {
            _credential ??= options != null ? await options.GetCredential(secretService) : null;
            return _credential;
        }

        private AsyncFtpClient? _cacheClient;
        private async Task<AsyncFtpClient> CreateClient(Uri uri)
        {
            if (_cacheClient == null || 
                !_cacheClient.IsConnected || 
                !_cacheClient.IsAuthenticated)
            {
                var port = uri.Port == -1 ? 21 : uri.Port;
                var options = new FtpConfig()
                {
                    ValidateAnyCertificate = true
                };
                if (settings.Validation?.Ftp?.UseGnuTls == true)
                {
#if PLUGGABLE
                    options.CustomStream = typeof(GnuTlsStream);
#else
                    log.Warning("Unable to use GnuTLS with trimmed build of simple-acme, please download a pluggable build.");
#endif
                }
                var client = new AsyncFtpClient(uri.Host, port, options)
                {
                    Credentials = await GetNetworkCredential()
                };
                client.LegacyLogger += (level, message) =>
                {
                    switch (level)
                    {
                        case FtpTraceLevel.Verbose:
                            log.Verbose("FTP: {message}", message);
                            break;
                        case FtpTraceLevel.Info:
                            log.Information("FTP: {message}", message);
                            break;
                        case FtpTraceLevel.Warn:
                            log.Warning("FTP: {message}", message);
                            break;
                        case FtpTraceLevel.Error:
                            log.Error("FTP: {message}", message);
                            break;
                    }
                };
                await client.AutoConnect();
                _cacheClient = client;
                log.Information("Established connection with ftp server at {host}:{port}, encrypted: {enc}", uri.Host, port, _cacheClient.IsEncrypted);
            }
            return _cacheClient;
        }

        public async Task Upload(string ftpPath, string content)
        {
            var stream = new MemoryStream();
            using var writer = new StreamWriter(stream);
            writer.Write(content);
            writer.Flush();
            stream.Position = 0;

            var uri = new Uri(ftpPath);
            var client = await CreateClient(uri);
            var status = await client.UploadStream(stream, uri.PathAndQuery, FtpRemoteExists.Overwrite, true);
            if (status == FtpStatus.Success)
            {
                log.Debug("Upload {ftpPath} status {StatusDescription}", ftpPath, status);
            }
            else
            {
                log.Warning("Upload {ftpPath} status {StatusDescription}", ftpPath, status);
            }
        }

        public async Task<IEnumerable<string>> GetFiles(string ftpPath)
        {
            var uri = new Uri(ftpPath);
            var client = await CreateClient(uri);
            var list = await client.GetListing(uri.PathAndQuery);
            if (list == null)
            {
                return [];
            }
            return list.Select(x => x.Name);
        }

        public async Task DeleteFolder(string ftpPath)
        {
            var uri = new Uri(ftpPath);
            var client = await CreateClient(uri);
            await client.DeleteDirectory(uri.PathAndQuery);
            log.Debug("Delete folder {ftpPath}", ftpPath);
        }

        public async Task DeleteFile(string ftpPath)
        {
            var uri = new Uri(ftpPath);
            var client = await CreateClient(uri);
            await client.DeleteFile(uri.PathAndQuery);
            log.Debug("Delete file {ftpPath}", ftpPath);
        }
    }
}