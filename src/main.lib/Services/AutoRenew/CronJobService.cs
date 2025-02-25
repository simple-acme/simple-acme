using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Extensions;
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services.AutoRenew
{
    [SupportedOSPlatform("linux")]
    internal class CronJobService(ILogService log, ISettingsService settings) : IAutoRenewService
    {
        private string CronFileName => $"{settings.Client.ClientName.CleanPath()}-{settings.BaseUri.CleanUri()}";
        private FileInfo CronFile => new($"/etc/cron.daily/{CronFileName}");
        private string CronScriptTemplate => $@"
# Automatically created by simple-acme: https://github.com/simple-acme/simple-acme/
cd {Path.GetDirectoryName(VersionService.ExePath)}
./wacs --{nameof(MainArguments.Renew).ToLowerInvariant()} --{nameof(MainArguments.BaseUri).ToLowerInvariant()} ""{settings.BaseUri}""";

        /// <summary>
        /// Test current status of the cronjob
        /// </summary>
        /// <returns></returns>
        public bool ConfirmAutoRenew()
        {
            if (CronFile.Exists)
            { 
                log.Warning("Cronjob configured");
                return true;
            }
            else
            {
                log.Warning("Cronjob not configured yet");
                return false;
            }
        }

        /// <summary>
        /// Create cronjob file only if/when needed
        /// </summary>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        public async Task EnsureAutoRenew(RunLevel runLevel)
        {
            var create = runLevel.HasFlag(RunLevel.ForceTaskScheduler) || !CronFile.Exists;
            if (create)
            {
                await SetupAutoRenew(runLevel);
            }
        }

        /// <summary>
        /// Create cronjob file
        /// </summary>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        public async Task SetupAutoRenew(RunLevel runLevel) 
        {
            if (!Environment.IsPrivilegedProcess)
            {
                log.Warning("Unable to configure cronjob, not running a superuser.");
                return;
            }
            var bytes = new UTF8Encoding(true).GetBytes(CronScriptTemplate.Trim().Replace("\r", Environment.NewLine));
            using var fileStream = CronFile.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            await fileStream.WriteAsync(bytes);
            File.SetUnixFileMode(CronFile.FullName,
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            log.Information("Created or updated {file}", CronFile.FullName);
        }
    }
}
