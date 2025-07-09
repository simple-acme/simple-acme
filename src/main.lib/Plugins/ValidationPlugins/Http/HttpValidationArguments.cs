using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    public abstract class HttpValidationArguments : BaseArguments
    {
        [CommandLine(Description = "Path for the website.", Obsolete = true)]
        public string? Root { get; set; }

        [CommandLine(Description = "Root path of the website. Note that /.well-known/acme-challenge/ will be appended automatically. Use --challengeroot instead if you do not want this to happen, e.g. to use a credential with limited access.")]
        public string? WebRoot { get; set; }

        [CommandLine(Description = "Root path for the /.well-known/acme-challenge/ folder for this domain.")]
        public string? ChallengeRoot { get; set; }

        [CommandLine(Obsolete = true, Description = "Not used (warmup is the new default).")]
        public bool Warmup { get; set; }

        [CommandLine(Description = "Copy default web.config to the .well-known directory.")]
        public bool ManualTargetIsIIS { get; set; }
    }
}
