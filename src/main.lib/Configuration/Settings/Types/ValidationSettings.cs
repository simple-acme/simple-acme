using PKISharp.WACS.Configuration.Settings.Types.Validation;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Configuration.Settings.Types
{
    public interface IValidationSettings
    {
        /// <summary>
        /// Default plugin to select in the Advanced menu (if
        /// supported for the target), or when nothing is 
        /// specified on the command line.
        /// </summary>
        string DefaultValidation { get; }

        /// <summary>
        /// Default plugin type, e.g. HTTP-01 (default), DNS-01, etc.
        /// </summary>
        string? DefaultValidationMode { get; }

        /// <summary>
        /// Gets the DNS settings.
        /// </summary>
        IDnsSettings Dns { get; }

        /// <summary>
        /// Settings for multithreading
        /// </summary>
        IMultiThreadingSettings Parallelism { get; }

        /// <summary>
        /// Settings for HTTP validation
        /// </summary>
        IHttpSettings Http { get; }

        /// <summary>
        /// Settings for TLS validation
        /// </summary>
        ITlsSettings Tls { get; }
    }

    internal class InheritValidationSettings(params IEnumerable<ValidationSettings?> chain) : InheritSettings<ValidationSettings>(chain), IValidationSettings
    {
        public string DefaultValidation => Get(x => x.DefaultValidation) ?? "selfhosting";
        public string? DefaultValidationMode => Get(x => x.DefaultValidationMode);
        public IHttpSettings Http => new InheritHttpSettings(Chain.Select(c => c?.Http));
        public IDnsSettings Dns => new InheritDnsSettings(Chain.Select(c => c?.Dns));
        public ITlsSettings Tls => new InheritTlsSettings(Chain.Select(c => c?.Tls));
        public IMultiThreadingSettings Parallelism => new InheritMultiThreadingSettings(Chain.Select(c => c?.Parallelism));
    }

    public class ValidationSettings
    {
        [SettingsValue(
            Description = "Default validation plugin.",
            NullBehaviour = "equivalent to <code>\"selfhosting\"</code>, with <code>\"filesystem\"</code> as backup for unprivileged users.")]
        public string? DefaultValidation { get; set; }

        [SettingsValue(
            Description = "Default validation method.",
            NullBehaviour = "equivalent to <code>\"http-01\"</code>")]
        public string? DefaultValidationMode { get; set; }
        public DnsSettings? Dns { get; set; }
        public HttpSettings? Http { get; set; }
        public TlsSettings? Tls { get; set; }
        public MultiThreadingSettings? Parallelism { get; set; }

        #region Backwards compatibility


        [SettingsValue(Hidden = true)]
        public bool? DisableMultiThreading { get; set; }

        [SettingsValue(Hidden = true)]
        public int? ParallelBatchSize { get; set; }

        [SettingsValue(Hidden = true)]
        public bool? CleanupFolders { get; set; }

        [SettingsValue(Hidden = true)]
        public bool? PreValidateDns { get; set; }

        [SettingsValue(Hidden = true)]
        public int? PreValidateDnsRetryCount { get; set; }

        [SettingsValue(Hidden = true)]
        public int? PreValidateDnsRetryInterval { get; set; }

        [SettingsValue(Hidden = true)] 
        public bool? PreValidateDnsLocal { get; set; }

        [SettingsValue(Hidden = true)]
        public int? DnsPropagationDelay { get; set; }

        [SettingsValue(Hidden = true)]
        public bool? AllowDnsSubstitution { get; set; }

        [SettingsValue(Hidden = true)]
        public List<string>? DnsServers { get; set; }

        [SettingsValue(Hidden = true)]
        public FtpSettings? Ftp { get; set; }

        #endregion
    }
}