using Nager.PublicSuffix.RuleProviders.CacheProviders;
using PKISharp.WACS.Extensions;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    public partial class DomainParseService
    {
        /// <summary>
        /// Regular 30 day file cache in the configuration folder
        /// </summary>
        private class FileCacheProvider : ICacheProvider
        {
            private readonly FileInfo? _file;
            private string? _memoryCache;
            private readonly ILogService _log;

            public FileCacheProvider(ILogService log, ISettings settings)
            {
                _log = log;
                var path = Path.Combine(settings.Client.ConfigurationPath, "public_suffix_list.dat");
                _file = new FileInfo(path);
            }

            public async Task<string?> GetAsync()
            {
                if (_file != null)
                {
                    try
                    {
                        _memoryCache = await File.ReadAllTextAsync(_file.FullName);
                    }
                    catch (Exception ex)
                    {
                        _log.Warning(ex, "Unable to read public suffix list cache from {path}", _file.FullName);
                    };
                }
                return _memoryCache;
            }

            public bool IsCacheValid()
            {
                if (_file != null)
                {
                    return _file.Exists && _file.LastWriteTimeUtc > DateTime.UtcNow.AddDays(-30);
                }
                else
                {
                    return !string.IsNullOrEmpty(_memoryCache);
                }
            }

            public async Task SetAsync(string val) 
            {
                if (_file != null)
                {
                    try
                    {
                        await _file.SafeWrite(val);
                    } 
                    catch (Exception ex)
                    {
                        _log.Warning(ex, "Unable to write public suffix list cache to {path}", _file.FullName);
                    }
                }
                _memoryCache = val;
            }
        }

    }
}
