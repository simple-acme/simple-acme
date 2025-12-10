using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.InstallationPlugins
{
    internal class QiNiuArguments: BaseArguments
    {

        [CommandLine(Description = "Qiniu Cloud Server https://api.qiniu.com")]
        public string? QiNiuServer { get; set; } = "https://api.qiniu.com";


        [CommandLine(Description = "Qiniu Cloud AccessKey", Secret= true )]
        public string? AccessKey { get; set; }

        [CommandLine(Description = "Qiniu Cloud SecretKey", Secret = true)]
        public string? SecretKey { get; set; }

    }
}
