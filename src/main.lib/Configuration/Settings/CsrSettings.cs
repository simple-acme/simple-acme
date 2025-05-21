namespace PKISharp.WACS.Configuration.Settings
{
    public class CsrSettings
    {
        /// <summary>
        /// Default plugin to select 
        /// </summary>
        public string? DefaultCsr { get; set; }
        /// <summary>
        /// RSA key settings
        /// </summary>
        public RsaSettings? Rsa { get; set; }
        /// <summary>
        /// EC key settings
        /// </summary>
        public EcSettings? Ec { get; set; }
    }
}