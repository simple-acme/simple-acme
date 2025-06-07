using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    public sealed class Route53Arguments : BaseArguments
    {
        [CommandLine(Description = "AWS region to use for authentication (default: us-east-1)")]
        public string? Route53Region { get; set; }

        [CommandLine(Description = "AWS IAM role for the current EC2 instance to login. Note that you should provide the IAM name instead of the ARN.")]
        public string? Route53IAMRole { get; set; }

        [CommandLine(Description = "AWS role ARN for the current EC2 instance to login. This may be a full ARN.")]
        public string? Route53ArnRole { get; set; }

        [CommandLine(Description = "Access key ID to login into Amazon Route 53.")]
        public string? Route53AccessKeyId { get; set; }

        [CommandLine(Description = "Secret access key to login into Amazon Route 53.", Secret = true)]
        public string? Route53SecretAccessKey { get; set; }
    }
}