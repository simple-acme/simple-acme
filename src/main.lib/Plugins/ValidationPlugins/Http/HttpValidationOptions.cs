using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Services;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    public abstract class HttpValidationOptions : ValidationPluginOptions
    {
        public string? Path { get; set; }
        public bool? CopyWebConfig { get; set; }
        public bool? IsRootPath { get; set; }

        public HttpValidationOptions() { }
        public HttpValidationOptions(HttpValidationOptions? source)
        {
            Path = source?.Path;
            CopyWebConfig = source?.CopyWebConfig;
            IsRootPath = source?.IsRootPath;
        }

        public override void Show(IInputService input)
        {
            base.Show(input);
            if (!string.IsNullOrEmpty(Path))
            {
                input.Show("Path", Path, level: 1);
            }
            if (CopyWebConfig == true)
            {
                input.Show("Web.config", "Yes", level: 1);
            }
            input.Show("Root path", IsRootPath == false ? "No" : "Yes", level: 1);
        }
    }
}
