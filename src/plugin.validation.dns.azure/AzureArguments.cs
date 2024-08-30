using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Azure.Common;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    public class AzureArguments : AzureArgumentsCommon
    {
        [CommandLine(Description = "Subscription ID to login into Microsoft Azure DNS (blank to use default).")]
        public string? AzureSubscriptionId { get; set; }

        [CommandLine(Description = "The name of the resource group within Microsoft Azure DNS.", Obsolete = true)]
        public string? AzureResourceGroupName { get; set; }

        [CommandLine(Description = "Hosted zone (blank to find best match)")]
        public string? AzureHostedZone { get; set; }
    }
}