using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    [IPlugin.Plugin1<
        P7bFileOptions, P7bFileOptionsFactory,
        DefaultCapability, WacsJsonPlugins, P7bFileArguments>
        ("221310a3-14d8-4478-a0f1-3c6d90f2ea51",
        Trigger, "Create P7B archive file (no private key!)",
        Name = "P7B file")]
    internal class P7bFile : IStorePlugin
    {
        internal const string Trigger = "P7bFile";

        private readonly ILogService _log;
        private readonly string _path;
        private readonly string? _name;

        public static string? DefaultPath(ISettingsService settings) =>
            settings.Store.P7bFile?.DefaultPath;

        public P7bFile(
            ILogService log,
            ISettingsService settings,
            P7bFileOptions options)
        {
            _log = log;
            _name = options.FileName;
            var path = !string.IsNullOrWhiteSpace(options.Path) ?
                options.Path :
                DefaultPath(settings);

            if (path != null && path.ValidPath(log))
            {
                _path = path;
                _log.Debug("Using p7b file path: {_path}", _path);
            }
            else
            {
                throw new Exception($"Specified p7b file path {path} is not valid.");
            }
        }

        private string PathForIdentifier(string identifier) => Path.Combine(_path, $"{identifier.Replace("*", "_")}.p7b");

        /// <summary>
        /// Save P7B file
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<StoreInfo?> Save(ICertificateInfo input)
        {
            try
            {
                var dest = PathForIdentifier(_name ?? input.CommonName?.Value ?? input.SanNames.First().Value);
                var data = input.AsCollection(X509KeyStorageFlags.EphemeralKeySet).Export(X509ContentType.Pkcs7) ?? throw new Exception();
                var fi = new FileInfo(dest);
                using var fs = fi.Open(FileMode.Create);
                using var stream = new MemoryStream(data);
                await stream.CopyToAsync(fs);
                await fs.FlushAsync();
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error copying certificate to p7b path");
            }
            return new StoreInfo()
            {
                Name = Trigger,
                Path = _path
            };
        }

        public Task Delete(ICertificateInfo input) => Task.CompletedTask;
    }
}
