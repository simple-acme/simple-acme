using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace PKISharp.WACS.Configuration
{
    public class ArgumentsParser(ILogService log, AssemblyService assemblyService, string[] args)
    {
        private IEnumerable<IArgumentsProvider>? _providers;
        private IEnumerable<CommandLineAttribute>? _arguments;

        public IEnumerable<IArgumentsProvider> Providers
        {
            get
            {
                if (_providers == null)
                {
                    var argumentGroups = assemblyService.GetResolvable<IArguments>();
                    _providers = argumentGroups.Select(x => {
                        var type = typeof(BaseArgumentsProvider<>).MakeGenericType(x.Type);
                        var constr = type.GetConstructor([]) ?? throw new Exception("IArgumentsProvider should have parameterless constructor");
                        try
                        {
                            var ret = (IArgumentsProvider)constr.Invoke([]);
                            ret.Log = log;
                            return ret;
                        }
                        catch (Exception ex)
                        {
                            if (ex.InnerException != null)
                            {
                                ex = ex.InnerException;
                            }
                            log.Error(ex, ex.Message);
                            return null;
                        }
                    }).
                    OfType<IArgumentsProvider>().
                    ToList();
                }
                return _providers;
            }
        }

        public IEnumerable<CommandLineAttribute> Arguments
        {
            get
            {
                _arguments ??= Providers.SelectMany(x => x.Configuration).ToList();
                return _arguments;
            }
        }

        public T? GetArguments<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, new()
        {
            foreach (var provider in Providers)
            {
                if (provider is IArgumentsProvider<T> typedProvider)
                {
                    return typedProvider.GetResult(args);
                }
            }
            throw new InvalidOperationException($"Unable to find class that implements IArgumentsProvider<{typeof(T).Name}>");
        }

        /// <summary>
        /// Test if the arguments can be resolved by any of the known providers
        /// </summary>
        /// <returns></returns>
        internal bool Validate()
        {
            var extraOptions = Providers.First().GetExtraArguments(args);
            foreach (var extraOption in extraOptions)
            {
                var super = Arguments.FirstOrDefault(x => string.Equals(x.Name, extraOption, StringComparison.InvariantCultureIgnoreCase));
                if (super == null)
                {
                    log.Error("Unknown argument --{0}, use --help to get a list of possible arguments", extraOption);
                    return false;
                }
            }

            // Run indivual result validations
            var main = GetArguments<MainArguments>();
            if (main == null)
            {
                return false;
            }
            var mainProvider = Providers.OfType<IArgumentsProvider<MainArguments>>().First();
            if (mainProvider.Validate(main, main, args))
            {
                // Validate the others
                var others = Providers.Except(new[] { mainProvider });
                foreach (var other in others)
                {
                    var opt = other.GetResult(args);
                    if (opt == null)
                    {
                        return false;
                    }
                    if (!other.Validate(opt, main, args))
                    {
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Test if any arguments are active, so that we can warn users
        /// that these arguments have no effect on renewals.
        /// </summary>
        /// <returns></returns>
        public bool Active()
        {
            var mainProvider = Providers.OfType<IArgumentsProvider<MainArguments>>().First();
            var others = Providers.Except(new[] { mainProvider });
            foreach (var other in others)
            {
                var opt = other.GetResult(args);
                if (opt != null && other.Active(opt, args))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get list of secret arguments that should be censored in the logs
        /// </summary>
        internal IEnumerable<string> SecretArguments => Arguments.Where(x => x.Secret).Select(x => x.ArgumentName);

        /// <summary>
        /// Show current command line
        /// </summary>
        internal void ShowCommandLine()
        {
            try
            {
                var censoredArgs = new List<string>();
                var censor = false;
                for (var i = 0; i < args.Length; i++)
                {
                    if (!censor)
                    {
                        var value = args[i];
                        value = value.Replace("\"", "\\\"");
                        if (value.Contains(' '))
                        {
                            value = $"\"{value}\"";
                        }
                        censoredArgs.Add(value);
                        censor = SecretArguments.Any(c => 
                            args[i].Equals($"--{c}", StringComparison.CurrentCultureIgnoreCase) || 
                            args[i].Equals($"/{c}", StringComparison.CurrentCultureIgnoreCase));
                    }
                    else
                    {
                        censoredArgs.Add("********");
                        censor = false;
                    }
                }
                var argsFormat = censoredArgs.Count != 0 ? $"Arguments: {string.Join(" ", censoredArgs)}" : "No command line arguments provided";
                log.Verbose(argsFormat);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Error censoring command line");
            }
        }
    }
}