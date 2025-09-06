using ACMESharp.Authorizations;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    public abstract class HttpValidationOptionsFactory<TOptions, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TArguments>(ArgumentsInputService arguments, Target target) : 
        PluginOptionsFactory<TOptions>
        where TOptions : HttpValidationOptions, new()
        where TArguments : HttpValidationArguments, new()
    {
        protected readonly ArgumentsInputService _arguments = arguments;
        protected readonly Target _target = target;

        private ArgumentResult<string?> WebRoot =>
            _arguments.
                GetString<TArguments>(x => x.WebRoot).
                Validate(p => Task.FromResult(PathIsValid(p!)), $"invalid path");

        private ArgumentResult<string?> ChallengeRoot => 
            _arguments.
                GetString<TArguments>(x => x.ChallengeRoot).
                Validate(p => Task.FromResult(PathIsValid(p!)), $"invalid path");

        private ArgumentResult<bool?> CopyWebConfig =>
            _arguments.
                GetBool<TArguments>(x => x.ManualTargetIsIIS).
                DefaultAsNull().
                WithDefault(false);

        /// <summary>
        /// Get webroot path manually
        /// </summary>
        public async Task<HttpValidationOptions?> BaseAquire(IInputService input)
        {
            var allowEmpty = AllowEmtpy();
            var webRootHint = WebrootHint(allowEmpty);
            var defaultPath =
                await _arguments.GetString<TArguments>(x => x.WebRoot).GetValue() ??
                await _arguments.GetString<TArguments>(x => x.ChallengeRoot).GetValue();
            var pathGetter = _arguments.
                GetString<TArguments>(x => x.Root).
                WithDefault(defaultPath).
                Required(!allowEmpty).
                Interactive(input).
                WithLabel(webRootHint[0]);
            if (webRootHint.Length > 1)
            {
                pathGetter = pathGetter.WithDescription(string.Join('\n', webRootHint[1..]));
            }
            var path = await pathGetter.GetValue();
            var isRootPath = await input.PromptYesNo($"Append \"{Http01ChallengeValidationDetails.HttpPathPrefix}\"?", true);
            return new TOptions
            {
                Path = path,
                IsRootPath = isRootPath,
                CopyWebConfig = _target.IIS || await CopyWebConfig.Interactive(input).WithLabel("Copy default web.config before validation?").GetValue() == true
            };
        }

        /// <summary>
        /// Basic parameters shared by http validation plugins
        /// </summary>
        public async Task<HttpValidationOptions> BaseDefault()
        {
            var allowEmpty = AllowEmtpy();
            var isRootPath = (bool?)null;
            var path = await ChallengeRoot.GetValue();
            if (!string.IsNullOrWhiteSpace(path))
            {
                isRootPath = false;
            }
            if (string.IsNullOrWhiteSpace(path))
            {
                path = await WebRoot.Required(!allowEmpty).GetValue();
            }
            return new TOptions
            {
                Path = path,
                IsRootPath = isRootPath,
                CopyWebConfig = _target.IIS || await CopyWebConfig.GetValue() == true
            };
        }

        /// <summary>
        /// By default we don't allow emtpy paths, but FileSystem 
        /// makes an exception because it can read from IIS
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public virtual bool AllowEmtpy() => false;

        /// <summary>
        /// Check if the webroot makes sense
        /// </summary>
        /// <returns></returns>
        public virtual bool PathIsValid(string path) => false;

        /// <summary>
        /// Hint to show to the user what the webroot should look like
        /// </summary>
        /// <returns></returns>
        public virtual string[] WebrootHint(bool allowEmpty)
        {
            var ret = new List<string> { "Path" };
            if (allowEmpty)
            {
                ret.Add("Leave empty to automatically read the path from IIS");
            }
            return [.. ret];
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(TOptions options)
        {
            yield return (CopyWebConfig.Meta, options.CopyWebConfig);
            if (options.IsRootPath == true)
            {
                yield return (ChallengeRoot.Meta, options.Path);
            }
            else
            {
                yield return (WebRoot.Meta, options.Path);
            }
        }
    }

}