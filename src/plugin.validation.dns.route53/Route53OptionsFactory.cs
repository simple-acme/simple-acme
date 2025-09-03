using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    internal sealed partial class Route53OptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<Route53Options>
    {
        private ArgumentResult<ProtectedString?> AccessKey => arguments.
            GetProtectedString<Route53Arguments>(a => a.Route53SecretAccessKey);

        private ArgumentResult<string?> Region => arguments.
            GetString<Route53Arguments>(a => a.Route53Region);

        private ArgumentResult<string?> AccessKeyId => arguments.
            GetString<Route53Arguments>(a => a.Route53AccessKeyId);

        /// <summary>
        /// https://docs.aws.amazon.com/IAM/latest/UserGuide/reference_iam-quotas.html
        /// IAM name requirements
        /// </summary>
        private ArgumentResult<string?> IamRole => arguments.
            GetString<Route53Arguments>(a => a.Route53IAMRole).
            Validate(x => Task.FromResult(!x?.Contains(':') ?? true), "ARN instead of IAM name").
            Validate(x => Task.FromResult(AimRegex().Match(x ?? "").Success), "invalid IAM name");
        
        private ArgumentResult<string?> ArnRole => arguments.
            GetString<Route53Arguments>(a => a.Route53ArnRole);

        internal const string IAMdefault = "IAM (default role)";
        internal const string IAMspecific = "IAM (specific role)";
        internal const string AccessKeySecret = "Access key";
        internal static string[] AuthenticationOptions = [IAMdefault, IAMspecific, AccessKeySecret];

        public override async Task<Route53Options?> Aquire(IInputService input, RunLevel runLevel)
        {
            var ret = new Route53Options();
            var menuOption = await input.ChooseRequired("Authentication method", AuthenticationOptions, x => Choice.Create(x));
            switch (menuOption)
            {
                case IAMdefault:
                    break;
                case IAMspecific:
                    ret.IAMRole = await IamRole.Interactive(input).Required().GetValue();
                    break;
                case AccessKeySecret:
                    ret.AccessKeyId = await AccessKeyId.Interactive(input).Required().GetValue();
                    ret.SecretAccessKey = await AccessKey.Interactive(input).Required().GetValue();
                    break;
                default:
                    throw new InvalidOperationException();
            }
            ret.ARNRole = await ArnRole.Interactive(input).WithLabel("Assume STS role? (provide ARN or press enter to skip)").GetValue();
            ret.Region = await Region.Interactive(input).WithLabel("AWS region to connect to (press enter for default 'us-east-1')").GetValue();
            return ret;
        }

        public override async Task<Route53Options?> Default()
        {
            var options = new Route53Options
            {
                IAMRole = await IamRole.GetValue(),
                ARNRole = await ArnRole.GetValue(),
                AccessKeyId = await AccessKeyId.GetValue(),
                Region = await Region.GetValue()
            };
            if (options.AccessKeyId != null)
            {
                options.SecretAccessKey = await AccessKey.Required().GetValue();
            }
            return options;
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(Route53Options options)
        {
            yield return (IamRole.Meta, options.IAMRole);
            yield return (ArnRole.Meta, options.ARNRole);
            yield return (AccessKeyId.Meta, options.AccessKeyId);
            yield return (AccessKey.Meta, options.SecretAccessKey);
            yield return (Region.Meta, options.Region);
        }

        [GeneratedRegex("^[A-Za-z0-9+=,.@_-]+$")]
        private static partial Regex AimRegex();
    }
}
