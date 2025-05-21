namespace PKISharp.WACS.Configuration.Settings
{
    public interface ICsrSettings
    {        
        /// <summary> 
        /// Default plugin to select 
        /// </summary>
        string? DefaultCsr { get; }

        /// <summary>
        /// EC key settings
        /// </summary>
        IEcSettings? Ec { get; }

        /// <summary>
        /// RSA key settings
        /// </summary>
        IRsaSettings? Rsa { get; }
    }

    internal class CsrSettings : ICsrSettings
    {
        public string? DefaultCsr { get; set; }
        public RsaSettings? Rsa { get; set; }
        public EcSettings? Ec { get; set; }
        IEcSettings? ICsrSettings.Ec => Ec;
        IRsaSettings? ICsrSettings.Rsa => Rsa;
    }
}