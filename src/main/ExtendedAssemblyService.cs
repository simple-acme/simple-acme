namespace PKISharp.WACS.Services
{
    public class ExtendedAssemblyService : AssemblyService
    {
        public ExtendedAssemblyService(ILogService logger) : base(logger)
        {
            _allTypes.AddRange([ 
                new(typeof(Plugins.ValidationPlugins.Http.Ftp)), new(typeof(Plugins.ValidationPlugins.Http.FtpArguments)), 
                new(typeof(Plugins.ValidationPlugins.Http.Sftp)), new(typeof(Plugins.ValidationPlugins.Http.SftpArguments)),
                new(typeof(Plugins.ValidationPlugins.Http.WebDav)), new(typeof(Plugins.ValidationPlugins.Http.WebDavArguments)),
                new(typeof(Plugins.ValidationPlugins.Dns.Acme)), new(typeof(Plugins.ValidationPlugins.Dns.AcmeArguments))
            ]);
        }
    }
}