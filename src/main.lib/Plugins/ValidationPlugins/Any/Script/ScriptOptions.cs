using PKISharp.WACS.Plugins.Base.Options;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    internal class ScriptOptions : ValidationPluginOptions
    {
        public string? ChallengeType { get; set; }
        public string? Script { get; set; }
        public string? CreateScript { get; set; }
        public string? CreateScriptArguments { get; set; }
        public string? DeleteScript { get; set; }
        public string? DeleteScriptArguments { get; set; }
        public int? Parallelism { get; set; }
    }
}
