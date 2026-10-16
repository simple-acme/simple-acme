
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Configuration.Settings.Types.Validation
{
    public interface IHttpSettings
    {
        IFtpSettings Ftp { get; }
        IHttpSelfHostingSettings SelfHosting { get; }

        /// <summary>
        /// If set to True, it will cleanup the folder structure
        /// and files it creates under the site for authorization.
        /// </summary>
        bool CleanupFolders { get; }
    }

    internal class InheritHttpSettings(params IEnumerable<HttpSettings?> chain) : InheritSettings<HttpSettings>(chain), IHttpSettings
    {
        public bool CleanupFolders => Get(x => x.CleanupFolders) ?? false;
        public IFtpSettings Ftp => new InheritFtpSettings(Chain.Select(c => c?.Ftp));
        public IHttpSelfHostingSettings SelfHosting => new InheritHttpSelfHostingSettings(Chain.Select(c => c?.SelfHosting));
    }

    public class HttpSettings
    {
        [SettingsValue(
            Default = "true",
            Description = "If set to <code>true</code>, the program will automatically delete file it created after HTTP validation is complete. It will also cleanup the <code>./well-known/acme-challenge</code> folder, if (and only if) there are no other files present.")]
        public bool? CleanupFolders { get; set; }
        public FtpSettings? Ftp { get; set; }
        public HttpSelfHostingSettings? SelfHosting { get; set; }
    }
}
