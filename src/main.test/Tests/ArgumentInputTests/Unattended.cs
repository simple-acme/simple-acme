using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.InstallationPlugins;
using PKISharp.WACS.Plugins.StorePlugins;
using PKISharp.WACS.Plugins.ValidationPlugins.Http;
using PKISharp.WACS.Services;
using PKISharp.WACS.UnitTests.Mock;
using System;
using System.Threading.Tasks;

namespace PKISharp.WACS.UnitTests.Tests.ArgumentInputTests
{
    [TestClass]
    public class Unattended
    {
        [TestMethod]
        [DataRow("I:", "I:", DisplayName = "Base")]
        [DataRow("I:\\", "I:\\", DisplayName = "WithSlash")]
        [DataRow("\"I:\\\"", "I:\\", DisplayName = "WithDoubleQuotes")]
        [DataRow("'I:\\'", "'I:\\'", DisplayName = "WithSingleQuotes")]
        public void BasicString(string input, string output)
        {
            var container = MockContainer.TestScope(commandLine: $"--pfxfilepath {input}");
            var mock = container.Resolve<ArgumentsInputService>();
            var basic = mock.
                GetString<PfxFileArguments>(x => x.PfxFilePath).
                GetValue().
                Result;
            Assert.AreEqual(output, basic);
        }

        [OSCondition(OperatingSystems.Windows)]
        [TestMethod]
        [DataRow("4", 4)]
        public void BasicLong(string input, long output)
        {
            var container = MockContainer.TestScope(commandLine: $"--installationsiteid {input}");
            var mock = container.Resolve<ArgumentsInputService>();
            var basic = mock.
                GetLong<IISArguments>(x => x.InstallationSiteId).
                GetValue().
                Result;
            Assert.AreEqual(output, basic);
        }

        [TestMethod]
        [DataRow("4", 4)]
        public void BasicInt(string input, int output)
        {
            var container = MockContainer.TestScope(commandLine: $"--validationport {input}");
            var mock = container.Resolve<ArgumentsInputService>();
            var basic = mock.
                GetInt<SelfHostingArguments>(x => x.ValidationPort).
                GetValue().
                Result;
            Assert.AreEqual(output, basic);
        }

        [TestMethod]
        [DataRow("--pfxpassword 1234", true, "1234", DisplayName = "Normal")]
        [DataRow("", false, null, DisplayName = "Nothing")]
        [DataRow("", true, null, DisplayName = "AllowEmptyMissing")]
        [DataRow("--pfxpassword", true, "", DisplayName = "AllowEmptyNull")]
        [DataRow("--pfxpassword \"\"", true, "", DisplayName = "AllowEmptyEmpty")]
        public void BasicSecret(string commandLine, bool allowEmpty, string? output)
        {
            var container = MockContainer.TestScope(commandLine: commandLine);
            var mock = container.Resolve<ArgumentsInputService>();
            var result = mock.GetProtectedString<PfxFileArguments>(x => x.PfxPassword, allowEmpty).
                GetValue().
                Result;
            Assert.AreEqual(output, result?.Value);
        }

        [TestMethod]
        [DataRow("--pfxpassword 1234", true, "default", "1234", DisplayName = "Normal")]
        [DataRow("", false, "default", "default", DisplayName = "Nothing")]
        [DataRow("", true, "default", "default", DisplayName = "AllowEmptyMissing")]
        [DataRow("--pfxpassword", true, "default", "", DisplayName = "AllowEmptyNull")]
        [DataRow("--pfxpassword \"\"", true, "default", "", DisplayName = "AllowEmptyEmpty")]
        [DataRow("--pfxpassword", false, "default", "default", DisplayName = "AllowEmptyNull2")]
        [DataRow("--pfxpassword \"\"", false, "default", "default", DisplayName = "AllowEmptyEmpty2")]
        public void SecretWithDefault(string commandLine, bool allowEmpty, string defaultValue, string? output)
        {
            var container = MockContainer.TestScope(commandLine: commandLine);
            var mock = container.Resolve<ArgumentsInputService>();
            var result = mock.GetProtectedString<PfxFileArguments>(x => x.PfxPassword, allowEmpty).
                WithDefault(defaultValue.Protect(allowEmpty)).
                GetValue().
                Result;
            Assert.AreEqual(output, result?.Value);
        }

        [TestMethod]
        [DataRow(null, "default", "default", DisplayName = "Base")]
        [DataRow("--pfxfilepath a", "b", "a", DisplayName = "CommandLineBeatsDefault")]
        [DataRow("--pfxfilepath a", null, "a", DisplayName = "CommandLineBeatsNullDefault")]
        [DataRow("--pfxfilepath a", "", "a", DisplayName = "CommandLineBeatsEmptyDefault")]
        public void DefaultValue(string? commandLine, string @default, string output)
        {
            var container = MockContainer.TestScope(commandLine: commandLine ?? "");
            var mock = container.Resolve<ArgumentsInputService>();
            var result = mock.GetString<PfxFileArguments>(x => x.PfxFilePath).
                WithDefault(@default).
                GetValue().
                Result;
            Assert.AreEqual(output, result);
        }

        [TestMethod]
        [DataRow("--pfxfilepath a", "a", DisplayName = "Base")]
        [DataRow(null, null, DisplayName = "Nothing")]
        public void DefaultAsNullWithoutDefault(string? commandLine, string output)
        {
            var container = MockContainer.TestScope(commandLine: commandLine ?? "");
            var mock = container.Resolve<ArgumentsInputService>();
            var result = mock.
                GetString<PfxFileArguments>(x => x.PfxFilePath).
                DefaultAsNull().
                GetValue().
                Result;
            Assert.AreEqual(output, result);
        }

        [TestMethod]
        [DataRow(null, "default", null, DisplayName = "Base")]
        [DataRow("--pfxfilepath a", "b", "a", DisplayName = "CommandLineBeatsDefault")]
        [DataRow("--pfxfilepath a", null, "a", DisplayName = "CommandLineBeatsNullDefault")]
        [DataRow("--pfxfilepath a", "", "a", DisplayName = "CommandLineBeatsEmptyDefault")]
        [DataRow("--pfxfilepath b", "b", null, DisplayName = "BaseDefaultNull")]
        public void DefaultAsNull(string? commandLine, string @default, string output)
        {
            var container = MockContainer.TestScope(commandLine: commandLine ?? "");
            var mock = container.Resolve<ArgumentsInputService>();
            var result = mock.
                GetString<PfxFileArguments>(x => x.PfxFilePath).
                WithDefault(@default).
                DefaultAsNull().
                GetValue().
                Result;
            Assert.AreEqual(output, result);
        }

        [TestMethod]
        [DataRow(null, true, DisplayName = "Missing")]
        [DataRow("--pfxfilepath a", false, DisplayName = "Provided")]
        public void Required(string? commandLine, bool shouldThrow)
        {
            var container = MockContainer.TestScope(commandLine: commandLine ?? "");
            var mock = container.Resolve<ArgumentsInputService>();
            try
            {
                var result = mock.
                    GetString<PfxFileArguments>(x => x.PfxFilePath).
                    Required().
                    GetValue().
                    Result;
                Assert.IsFalse(shouldThrow, "No exception thrown though it should have been");
            } 
            catch
            {
                Assert.IsTrue(shouldThrow, "Exception throw when it should not have been");
            }

        }

        [TestMethod]
        [DataRow(null, null, true, null, DisplayName = "EmptyInputAndDefault")]
        [DataRow(null, "a", false, "a", DisplayName = "NormalDefault")]
        [DataRow("--pfxfilepath a", "b", false, "a", DisplayName = "CommandlineOverruleDefault")]
        [DataRow("--pfxfilepath a", null, false, "a", DisplayName = "NullDefault")]
        [DataRow("--pfxfilepath a", "", false, "a", DisplayName = "EmtpyDefault")]
        public void RequiredWithDefault(string? commandLine, string @default, bool shouldThrow, string expectedValue)
        {
            var container = MockContainer   .TestScope(commandLine: commandLine ?? "");
            var mock = container.Resolve<ArgumentsInputService>();
            try
            {
                var result = mock.
                    GetString<PfxFileArguments>(x => x.PfxFilePath).
                    WithDefault(@default).
                    Required().
                    GetValue().
                    Result;
                Assert.IsFalse(shouldThrow, "No exception thrown though it should have been");
                Assert.AreEqual(expectedValue, result);
            }
            catch
            {
                Assert.IsTrue(shouldThrow, "Exception throw when it should not have been");
            }
        }

        [TestMethod]
        [DataRow("--pfxfilepath valid", false, DisplayName = "Valid")]
        [DataRow("--pfxfilepath invalid", true, DisplayName = "Invalid")]
        [DataRow("--pfxfilepath \"\"", false, DisplayName = "Empty")]
        [DataRow("", false, DisplayName = "Null")]
        public void Validate(string input, bool shouldThrow)
        {
            var container = MockContainer.TestScope(commandLine: input);
            var mock = container.Resolve<ArgumentsInputService>();
            try
            {
                var result = mock.
                    GetString<PfxFileArguments>(x => x.PfxFilePath).
                    Validate(x => Task.FromResult(x == "valid"), "not valid").
                    GetValue().
                    Result;
                Assert.IsFalse(shouldThrow, "No exception thrown though it should have been");
            }
            catch
            {
                Assert.IsTrue(shouldThrow, "Exception throw when it should not have been");
            }
        }

        [TestMethod]
        [DataRow("--pfxfilepath ab", false, DisplayName = "Valid")]
        [DataRow("--pfxfilepath a", true, DisplayName = "OnlyA")]
        [DataRow("--pfxfilepath b", true, DisplayName = "OnlyB")]
        [DataRow("--pfxfilepath c", true, DisplayName = "Invalid")]
        [DataRow("--pfxfilepath \"\"", false, DisplayName = "Empty")]
        [DataRow("", false, DisplayName = "Null")]
        public void ValidateMultiple(string input, bool shouldThrow)
        {
            var container = MockContainer.TestScope(commandLine: input);
            var mock = container.Resolve<ArgumentsInputService>();
            try
            {
                var result = mock.
                    GetString<PfxFileArguments>(x => x.PfxFilePath).
                    Validate(x => Task.FromResult(x?.Contains('a') ?? false), "No A").
                    Validate(x => Task.FromResult(x?.Contains('b') ?? false), "No B").
                    GetValue().
                    Result;
                Assert.IsFalse(shouldThrow, "No exception thrown though it should have been");
            }
            catch
            {
                Assert.IsTrue(shouldThrow, "Exception throw when it should not have been");
            }
        }
    }
}
