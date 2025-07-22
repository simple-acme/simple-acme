using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.TargetPlugins;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    internal class RenewalStoreDisk(
        ISettings settings,
        DueDateStaticService dueDate,
        ILogService log,
        WacsJson wacsJson) : object(), IRenewalStoreBackend
    {

        /// <summary>
        /// Local cache to prevent superfluous reading and
        /// JSON parsing
        /// </summary>
        internal List<Renewal>? _renewalsCache;

        /// <summary>
        /// Parse renewals from store
        /// </summary>
        public async Task<IEnumerable<Renewal>> Read()
        {
            if (_renewalsCache == null)
            {
                var list = new List<Renewal>();
                var di = new DirectoryInfo(settings.Client.ConfigurationPath);
                var postFix = ".renewal.json";
                var renewalFiles = di.EnumerateFiles($"*{postFix}", SearchOption.AllDirectories);
                foreach (var rj in renewalFiles)
                {
                    try
                    {
                        // Just checking if we have write permission
                        using var writeStream = rj.OpenWrite();
                    }
                    catch (Exception ex)
                    {
                        log.Warning(ex, "No write access to all renewals");
                        break;
                    }
                }
                foreach (var rj in renewalFiles)
                {
                    try
                    {
                        var text = await File.ReadAllTextAsync(rj.FullName);
                        var result = JsonSerializer.Deserialize(text, wacsJson.Renewal) ?? throw new Exception("result is empty");
                        if (result.Id != rj.Name.Replace(postFix, ""))
                        {
                            throw new Exception($"mismatch between filename and id {result.Id}");
                        }
                        if (result.TargetPluginOptions == null || result.TargetPluginOptions.Plugin == null)
                        {
                            throw new Exception("missing source plugin options");
                        }
                        if (result.ValidationPluginOptions == null || result.ValidationPluginOptions.Plugin == null)
                        {
                            throw new Exception("missing validation plugin options");
                        }
                        if (result.StorePluginOptions == null)
                        {
                            throw new Exception("missing store plugin options");
                        }
                        if (result.CsrPluginOptions == null && result.TargetPluginOptions is not CsrOptions)
                        {
                            throw new Exception("missing csr plugin options");
                        }
                        if (result.InstallationPluginOptions == null)
                        {
                            throw new Exception("missing installation plugin options");
                        }
                        if (string.IsNullOrEmpty(result.LastFriendlyName))
                        {
                            result.LastFriendlyName = result.FriendlyName;
                        }
                        result.History ??= [];
                        list.Add(result);
                    }
                    catch (Exception ex)
                    {
                        log.Error("Unable to read renewal {renewal}: {reason}", rj.Name, ex.Message);
                    }
                }
                _renewalsCache = [.. list.OrderBy(x => dueDate.DueDate(x)?.Start)];
            }
            return _renewalsCache;
        }

        /// <summary>
        /// Serialize renewal information to store
        /// </summary>
        /// <param name="BaseUri"></param>
        /// <param name="Renewals"></param>
        public async Task Write(IEnumerable<Renewal> Renewals)
        {
            var list = Renewals.ToList();
            foreach (var renewal in list)
            {
                if (renewal.Deleted)
                {
                    var file = RenewalFile(renewal, settings.Client.ConfigurationPath);
                    if (file != null && file.Exists)
                    {
                        file.Delete();
                    }
                }
                else if (renewal.Updated || renewal.New)
                {
                    var file = RenewalFile(renewal, settings.Client.ConfigurationPath);
                    if (file != null)
                    {
                        try
                        {
                            var renewalContent = JsonSerializer.Serialize(renewal, wacsJson.Renewal);
                            if (string.IsNullOrWhiteSpace(renewalContent))
                            {
                                throw new Exception("Serialization yielded empty result");
                            }
                            await file.SafeWrite(renewalContent);
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex, "Unable to write {renewal} to disk", renewal.LastFriendlyName);
                        }
                    }
                    renewal.New = false;
                    renewal.Updated = false;
                }
            }
            // Update cache
            _renewalsCache = [.. list.Where(x => !x.Deleted).OrderBy(x => dueDate.DueDate(x)?.Start)];
        }

        /// <summary>
        /// Determine location and name of the history file
        /// </summary>
        /// <param name="renewal"></param>
        /// <param name="configPath"></param>
        /// <returns></returns>
        private static FileInfo RenewalFile(Renewal renewal, string configPath) => new(Path.Combine(configPath, $"{renewal.Id}.renewal.json"));
    }
}
