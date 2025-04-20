using PKISharp.WACS.Configuration;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WebDav;

namespace PKISharp.WACS.Client
{
    internal class WebDavClientWrapper(
        NetworkCredentialOptions? options,
        ILogService log,
        IProxyService proxy,
        SecretServiceManager secretService) : IDisposable
    {
        private WebDavClient? _client;
        private async Task<WebDavClient> GetWebDavClientAsync()
        {
            if (_client != null)
            {
                return _client;
            }
            var credential = default(NetworkCredential);
            if (options != null && options.UserName != null)
            {
                credential = await options.GetCredential(secretService);
            }
            var temp = new WebDavClient(new WebDavClientParams()
            {
                Proxy = await proxy.GetWebProxy(),
                UseDefaultCredentials = proxy.ProxyType == WindowsProxyUsePolicy.UseWinInetProxy,
                Credentials = credential
            });
            _client = temp;
            return _client;
        }

        private static string NormalizePath(string path)
        {
            return path.
                Replace("webdav:", "https:").
                Replace("dav:", "https:").
                Replace("\\\\", "https://").
                Replace("\\", "/");
        }

        public async Task Upload(string originalPath, string content)
        {
            try
            {
                var client = await GetWebDavClientAsync();
                var path = NormalizePath(originalPath);
                var uri = new Uri(path);
                var stream = new MemoryStream();
                using var writer = new StreamWriter(stream);
                writer.Write(content);
                writer.Flush();
                stream.Position = 0;
                var currentPath = $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? "" : $":{uri.Port}")}";
                var directories = uri.AbsolutePath.Trim('/').Split('/');
                for (var i = 0; i < directories.Length - 1; i++)
                {
                    currentPath += $"/{directories[i]}";
                    if (!await FolderExists(currentPath))
                    {
                        var dirCreated = await client.Mkcol(currentPath);
                        if (!dirCreated.IsSuccessful)
                        {
                            throw new Exception($"path {currentPath} - {dirCreated.StatusCode} ({dirCreated.Description})");
                        }
                    }
                }
                // Upload file
                currentPath += $"/{directories[^1]}";
                var fileUploaded = await client.PutFile(currentPath, stream);
                if (!fileUploaded.IsSuccessful)
                {
                    throw new Exception($"{fileUploaded.StatusCode} ({fileUploaded.Description})");
                }
            }
            catch (Exception ex)
            {
                log.Error("Error uploading file {path} {Message}", originalPath, ex.Message);
                throw;
            }
        }

        private async Task<bool> FolderExists(string path)
        {
            var client = await GetWebDavClientAsync();
            var exists = await client.Propfind(path);
            return exists.IsSuccessful &&
                exists.Resources.Count != 0 &&
                exists.Resources.First().IsCollection;
        }

        internal async Task<bool> IsEmpty(string path)
        {
            var client = await GetWebDavClientAsync();
            var exists = await client.Propfind(path);
            return exists.IsSuccessful && exists.Resources.Count == 0;
        }

        public async Task Delete(string path)
        {
            var client = await GetWebDavClientAsync();
            path = NormalizePath(path);
            try
            {
                var x = client.Delete(path);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Error deleting file/folder {path}", path);
            }
        }

        public async Task<IEnumerable<string>> GetFiles(string path)
        {
            try
            {
                var client = await GetWebDavClientAsync();
                path = NormalizePath(path);
                var folderFiles = await client.Propfind(path);
                if (folderFiles.IsSuccessful)
                {
                    return folderFiles.Resources.Select(r => r.DisplayName).OfType<string>();
                }
            }
            catch (Exception ex)
            {
                log.Verbose("WebDav error {@ex}", ex);
            }
            return [];
        }

        #region IDisposable

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _client?.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose() => Dispose(true);

        #endregion
    }
}