﻿using PKISharp.WACS.Configuration;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;

namespace PKISharp.WACS.Services
{
    public partial class ArgumentsInputService(
        ILogService log,
        ArgumentsParser arguments,
        IInputService input,
        SecretServiceManager secretService)
    {

        /// <summary>
        /// Slightly awkward construction here with the allowEmtpy parameter to 
        /// prevent trim warning due to the compiler creating a displayclass for 
        /// the closure if we use it directly within the input function call. We 
        /// may be able to restore the original code in a future version of the
        /// .NET tool chain.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expression"></param>
        /// <param name="allowEmtpy"></param>
        /// <returns></returns>
        public ArgumentResult<ProtectedString?> GetProtectedString<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>
            (Expression<Func<T, string?>> expression, bool allowEmtpy = false) where T : class, IArguments,
            new() => new(GetArgument(expression).Protect(allowEmtpy), GetMetaData(expression),
                allowEmtpy
                    ? async args => (await secretService.GetSecret(args.Label, args.Default?.Value, "", args.Required, args.Multiline)).Protect(true)
                    : async args => (await secretService.GetSecret(args.Label, args.Default?.Value, null, args.Required, args.Multiline)).Protect(false),
                log, allowEmtpy);

        public ArgumentResult<string?> GetString<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>
            (Expression<Func<T, string?>> expression) where T : class, IArguments, new() =>
            new(GetArgument(expression), GetMetaData(expression),
                async args => await input.RequestString(args.Label), log);

        public ArgumentResult<bool?> GetBool<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>
            (Expression<Func<T, bool?>> expression) where T : class, IArguments, new() =>
            new(GetArgument(expression), GetMetaData(expression),
                async args => await input.PromptYesNo(args.Label, args.Default == true), log);

        public ArgumentResult<long?> GetLong<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>
            (Expression<Func<T, long?>> expression) where T : class, IArguments, new() => 
            new(GetArgument(expression), GetMetaData(expression),
                async args => {
                    var str = await input.RequestString(args.Label);
                    if (long.TryParse(str, out var ret))
                    {
                        return ret;
                    }
                    else
                    {
                        log.Warning("Invalid number: {ret}", str);
                        return null;
                    }
                }, log);

        public ArgumentResult<int?> GetInt<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>
            (Expression<Func<T, int?>> expression) where T : class, IArguments, new() =>
            new(GetArgument(expression), GetMetaData(expression),
                args => input.RequestInt(args.Label), log);

        protected static CommandLineAttribute GetMetaData(LambdaExpression action)
        {
            if (action.Body is MemberExpression member)
            {
                var property = member.Member;
                return property.CommandLineOptions();
            }
            else if (action.Body is UnaryExpression unary)
            {
                if (unary.Operand is MemberExpression unaryMember)
                {
                    return unaryMember.Member.CommandLineOptions();
                }
            }
            throw new NotImplementedException("Unsupported expression");
        }

        /// <summary>
        /// Interactive
        /// </summary>
        /// <param name="optionName"></param>
        /// <param name="providedValue"></param>
        /// <param name="what"></param>
        /// <param name="secret"></param>
        /// <returns></returns>
        protected P GetArgument<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T, P>(Expression<Func<T, P>> action) where T : class, IArguments, new()
        {
            var returnValue = default(P);
            var args = arguments.GetArguments<T>();
            if (args != null)
            {
                var func = action.Compile();
                returnValue = func(args);
            }
            else
            {
                throw new InvalidOperationException($"Missing argumentprovider for type {typeof(T).Name}");
            }
            var meta = GetMetaData(action);
            var argumentName = meta.ArgumentName;
            if (returnValue == null)
            {
                if (!meta.Obsolete)
                {
                    log.Verbose("No value provided for {optionName}", $"--{argumentName}");
                }
            }
            else
            {
                var censor = arguments.SecretArguments.Contains(argumentName);
                if (returnValue is string returnString && string.IsNullOrWhiteSpace(returnString)) 
                {
                    log.Verbose("Parsed emtpy value for {optionName}", $"--{argumentName}");
                } 
                else if (returnValue is bool boolValue)
                {
                    if (boolValue)
                    {
                        log.Verbose("Flag {optionName} is present", $"--{argumentName}");
                    } 
                    else
                    {
                        log.Verbose("Flag {optionName} not present", $"--{argumentName}");
                    }
                }
                else
                {
                    log.Verbose("Parsed value for {optionName}: {providedValue}", $"--{argumentName}", censor ? "********" : returnValue);
                }
            }
            return returnValue;
        }
    }
}