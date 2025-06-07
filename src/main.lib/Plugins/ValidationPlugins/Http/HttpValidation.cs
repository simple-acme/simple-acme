using ACMESharp.Authorizations;
using PKISharp.WACS.Context;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    /// <summary>
    /// Base implementation for HTTP-01 validation plugins
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    /// <param name="log"></param>
    /// <param name="input"></param>
    /// <param name="options"></param>
    /// <param name="proxy"></param>
    /// <param name="renewal"></param>
    /// <param name="target"></param>
    /// <param name="runLevel"></param>
    /// <param name="identifier"></param>
    public abstract class HttpValidation<TOptions>(TOptions options, RunLevel runLevel, HttpValidationParameters pars) :
        HttpValidationBase(pars.LogService, runLevel, pars.InputService)
        where TOptions : HttpValidationOptions
    {
        private readonly List<string> _filesWritten = [];

        protected TOptions _options = options;
        protected IInputService _input = pars.InputService;
        protected ISettings _settings = pars.Settings;
        protected Renewal _renewal = pars.Renewal;
        protected RunLevel _runLevel = runLevel;
        protected ILogService _log = pars.LogService;

        /// <summary>
        /// Multiple http-01 validation challenges can be answered at the same time
        /// </summary>
        public override ParallelOperations Parallelism => ParallelOperations.Answer;

        /// <summary>
        /// Path used for the current renewal, may not be same as _options.Path
        /// because of the "Split" function employed by IISSites target
        /// </summary>
        protected string? _path = options.Path;

        /// <summary>
        /// Provides proxy settings for site warmup
        /// </summary>
        private readonly IProxyService _proxy = pars.ProxyService;

        /// <summary>
        /// Where to find the template for the web.config that's copied to the webroot
        /// </summary>
        protected static string TemplateWebConfig => Path.Combine(VersionService.ResourcePath, "web_config.xml");

        /// <summary>
        /// Character to separate folders, different for FTP 
        /// </summary>
        protected virtual char PathSeparator => '\\';

        /// <summary>
        /// Handle http challenge
        /// </summary>
        public async override Task<bool> PrepareChallenge(ValidationContext context, Http01ChallengeValidationDetails challenge)
        {
            // Should always have a value, confirmed by RenewalExecutor
            // check only to satifiy the compiler
            if (context.TargetPart != null)
            {
                Refresh(context.TargetPart);
            }
            await WriteAuthorizationFile(challenge);
            await WriteWebConfig();
            await TestChallenge(challenge);

            string? foundValue = null;
            try
            {
                var value = await WarmupSite(challenge);
                if (Equals(value, challenge.HttpResourceValue))
                {
                    log.Information("Preliminary validation looks good, but the ACME server will be more thorough");
                    return true;
                }
                else
                {
                    log.Warning("Preliminary validation failed, the server answered '{value}' instead of '{expected}'. The ACME server might have a different perspective",
                        foundValue ?? "(null)",
                        challenge.HttpResourceValue);
                }
            }
            catch (HttpRequestException hrex)
            {
                log.Warning(hrex, "Preliminary validation failed because '{hrex}'", hrex.Message);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Preliminary validation failed");
            }
            return false;
        }

        /// <summary>
        /// Default commit function, doesn't do anything because 
        /// default doesn't do parallel operation
        /// </summary>
        /// <returns></returns>
        public override Task Commit() => Task.CompletedTask;

        /// <summary>
        /// Warm up the target site, giving the application a little
        /// time to start up before the validation request comes in.
        /// Mostly relevant to classic FileSystem validation
        /// </summary>
        /// <param name="uri"></param>
        private async Task<string> WarmupSite(Http01ChallengeValidationDetails challenge)
        {
            using var client = await _proxy.GetHttpClient(false);
            var response = await client.GetAsync(challenge.HttpResourceUrl);
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Should create any directory structure needed and write the file for authorization
        /// </summary>
        /// <param name="answerPath">where the answerFile should be located</param>
        /// <param name="fileContents">the contents of the file to write</param>
        private async Task WriteAuthorizationFile(Http01ChallengeValidationDetails challenge)
        {
            // Create full path from the base path
            var path = CreatePath(challenge.HttpResourceName);
            await WriteFile(path, challenge.HttpResourceValue);
            if (!_filesWritten.Contains(path))
            {
                _filesWritten.Add(path);
            }
        }

        /// <summary>
        /// Create the full path for the file to be written
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private string CreatePath(string name)
        {
            if (_path == null)
            {
                throw new InvalidOperationException("No path specified for HttpValidation");
            }

            // Create full path from the base path
            var path = _path;
            if (_options.IsRootPath != false)
            {
                path = CombinePath(path, Http01ChallengeValidationDetails.HttpPathPrefix);
            }
            return CombinePath(path, name);
        }

        /// <summary>
        /// Can be used to write out server specific configuration, to handle extensionless files etc.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="answerPath"></param>
        /// <param name="token"></param>
        private async Task WriteWebConfig()
        {
            if (_path == null)
            {
                throw new InvalidOperationException("No path specified for HttpValidation");
            }
            if (_options.CopyWebConfig == true)
            {
                try
                {
                    var destination = CreatePath("web.config");
                    if (!_filesWritten.Contains(destination))
                    {
                        var content = HttpValidation<TOptions>.GetWebConfig().Value;
                        if (content != null)
                        {
                            log.Debug("Writing web.config");
                            await WriteFile(destination, content);
                            _filesWritten.Add(destination);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "Unable to write web.config");
                }
            }
        }

        /// <summary>
        /// Get the template for the web.config
        /// </summary>
        /// <returns></returns>
        private static Lazy<string?> GetWebConfig() => new(() => {
            try
            {
                return File.ReadAllText(TemplateWebConfig);
            } 
            catch 
            {
                return null;
            }
        });

        /// <summary>
        /// Combine root path with relative path
        /// </summary>
        /// <param name="root"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        protected virtual string CombinePath(string root, string path)
        {
            root ??= string.Empty;
            var expandedRoot = Environment.ExpandEnvironmentVariables(root);
            var trim = new[] { '/', '\\' };
            return $"{expandedRoot.TrimEnd(trim)}{PathSeparator}{path.TrimStart(trim).Replace('/', PathSeparator)}";
        }

        /// <summary>
        /// Delete folder if it's empty
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private async Task<bool> DeleteFolderIfEmpty(string path)
        {
            if (await IsEmpty(path))
            {
                await DeleteFolder(path);
                return true;
            }
            else
            {
                log.Debug("Not deleting {path} because it doesn't exist or it's not empty.", path);
                return false;
            }
        }

        /// <summary>
        /// Write file with content to a specific location
        /// </summary>
        /// <param name="root"></param>
        /// <param name="path"></param>
        /// <param name="content"></param>
        protected abstract Task WriteFile(string path, string content);

        /// <summary>
        /// Delete file from specific location
        /// </summary>
        /// <param name="root"></param>
        /// <param name="path"></param>
        protected abstract Task DeleteFile(string path);

        /// <summary>
        /// Check if folder is empty
        /// </summary>
        /// <param name="root"></param>
        /// <param name="path"></param>
        protected abstract Task<bool> IsEmpty(string path);

        /// <summary>
        /// Delete folder if not empty
        /// </summary>
        /// <param name="root"></param>
        /// <param name="path"></param>
        protected abstract Task DeleteFolder(string path);

        /// <summary>
        /// Refresh
        /// </summary>
        /// <param name="scheduled"></param>
        /// <returns></returns>
        protected virtual void Refresh(TargetPart targetPart) { }

        /// <summary>
        /// Dispose
        /// </summary>
        public override async Task CleanUp()
        {
            try
            {
                if (_path != null)
                {
                    var folders = new List<string>();
                    var written = new List<string>(_filesWritten);
                    foreach (var file in written)
                    {
                        log.Debug("Deleting files");
                        await DeleteFile(file);
                        _filesWritten.Remove(file);
                        var folder = file[..file.LastIndexOf(PathSeparator)];
                        if (!folders.Contains(folder))
                        {
                            folders.Add(folder);
                        }
                    }
                    if (_settings.Validation.CleanupFolders)
                    {
                        log.Debug("Deleting empty folders");
                        foreach (var folder in folders)
                        {
                            if (await DeleteFolderIfEmpty(folder))
                            {
                                var idx = folder.LastIndexOf(PathSeparator);
                                if (idx >= 0)
                                {
                                    var parent = folder[..folder.LastIndexOf(PathSeparator)];
                                    await DeleteFolderIfEmpty(parent);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Error occurred while deleting folder structure");
            }
        }
    }
}
