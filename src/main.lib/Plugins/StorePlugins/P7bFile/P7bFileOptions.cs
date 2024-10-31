using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Services;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    internal class P7bFileOptions : StorePluginOptions
    {
        /// <summary>
        /// Path to the folder
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// Name to use
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Show details to the user
        /// </summary>
        /// <param name="input"></param>
        public override void Show(IInputService input)
        {
            base.Show(input);
            input.Show("Path", string.IsNullOrEmpty(Path) ? "[Default from settings.json]" : Path, level: 2);
            input.Show("FileName", string.IsNullOrEmpty(FileName) ? "[Default (common name)]" : FileName, level: 2);
        }
    }
}
