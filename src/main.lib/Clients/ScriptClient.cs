using PKISharp.WACS.Extensions;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PKISharp.WACS.Clients
{
    public record ScriptResult
    {
        public bool Success;
        public string? Output;
    }

    public partial class ScriptClient(ILogService logService, ISettings settings)
    {
        /// <summary>
        /// Replace {Tokens} with values defined in the replacements dictionary
        /// </summary>
        /// <param name="input"></param>
        /// <param name="replacements"></param>
        /// <returns></returns>
        /// Decorator currently broken, might work in a future C# version
        /// [return: NotNullIfNotNull(nameof(input))]
        public static async Task<string> ReplaceTokens(string? input, Dictionary<string, string?> replacements, SecretServiceManager? secretServiceManager = null, bool censor = false)
        {
            // Don't bother with emtpy strings
            if (string.IsNullOrWhiteSpace(input)) 
            {
                return input ?? "";
            }

            // Replace special tokens
            var comparer = StringComparer.OrdinalIgnoreCase;
            var caseInsenstive = new Dictionary<string, string?>(replacements, comparer);
            var ret = new StringBuilder();
            var pos = 0;
            foreach (Match m in TokenRegex().Matches(input))
            {
                // Append part of the string between the current position and the next match
                if (m.Index > pos)
                {
                    ret.Append(input.AsSpan(pos, m.Index - pos));
                    pos = m.Index;
                }

                // Append the replacement value
                var replacement = m.Value;
                var key = m.Value.Trim('{', '}').ToLower();
                if (caseInsenstive.TryGetValue(key, out var value))
                {
                    replacement = value;
                }
                else if (key.StartsWith(SecretServiceManager.VaultPrefix))
                {
                    if (secretServiceManager != null && !censor)
                    {
                        replacement = await secretServiceManager.EvaluateSecret(key) ?? replacement;
                    }
                }
                ret.Append(replacement);
                pos += m.Length;
            }

            // Append trailing part of the string after the last match
            if (pos < input.Length)
            {
                ret.Append(input.AsSpan(pos, input.Length - pos));
            }
            return ret.ToString();
        }

        /// <summary>
        /// Start .ps1/.exe/.bat/.sh file
        /// </summary>
        /// <param name="script"></param>
        /// <param name="parameters"></param>
        /// <param name="censoredParameters"></param>
        /// <returns></returns>
        public async Task<ScriptResult> RunScript(string script, string? parameters = null, string? censoredParameters = null, bool hideOutput = false)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                logService.Warning("No script configured.");
                return new ScriptResult() { Success = false };
            }
            if (!script.ValidFile(logService))
            {
                return new ScriptResult() { Success = false };
            }
            if (!string.IsNullOrWhiteSpace(parameters))
            {
                logService.Information(LogType.All, "Script {script} starting with parameters {parameters}", script, censoredParameters ?? parameters);
            }
            else
            {
                logService.Information(LogType.All, "Script {script} starting", script);
            }

            // Start process and monitor output/status
            var psi = CreatePsi(script, parameters);
            using var process = new Process { StartInfo = psi };
            var output = new StringBuilder();
            var ret = new StringBuilder();
            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null)
                {
                    return;
                }
                ret.AppendLine(e.Data);
                if (!hideOutput)
                {
                    output.AppendLine(e.Data);
                    logService.Verbose(e.Data);
                }
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data) && !string.Equals(e.Data, "null"))
                {
                    output.AppendLine($"Error: {e.Data}");
                    logService.Error("Script error: {0}", e.Data);
                }
            };
            process.EnableRaisingEvents = true;
            process.Exited += (s, e) =>
            {
                logService.Information(LogType.Event | LogType.Disk | LogType.Notification, output.ToString());
                if (process.ExitCode != 0)
                {
                    logService.Error("Script finished with exit code {code}", process.ExitCode);
                }
                else
                {  
                    logService.Information("Script finished");
                }
            };

            try
            {
                await RunProcess(process);
            }
            catch (Exception ex)
            {
                logService.Error(ex, "Script is unable to start");
                return new ScriptResult() { Success = false };
            }
            if (!process.HasExited)
            {
                logService.Error($"Script execution timed out after {settings.Script.Timeout} seconds, trying to kill");
                try
                {
                    process.Kill();
                }
                catch (Exception ex)
                {
                    logService.Error(ex, "Killing process {Id} failed", process.Id);
                }
                return new ScriptResult() { Success = false };
            }

            return new ScriptResult()
            {
                Success = process.ExitCode == 0,
                Output = ret.ToString()
            };
        }

        /// <summary>
        /// Start the process
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task RunProcess(Process process)
        {
            if (process.Start())
            {
                logService.Debug("Process launched: {actualScript} (ID: {Id})", process.StartInfo.FileName, process.Id);
            }
            else
            {
                throw new Exception("Process.Start failed");
            }
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            process.StandardInput.Close(); // Helps end the process
            var totalWait = 0;
            var interval = 2000;
            do
            {
                await Task.Delay(interval);
                totalWait += interval;
                if (process.HasExited || totalWait > settings.Script.Timeout * 1000)
                {
                    break;
                }
                else
                {
                    logService.Verbose("Waiting for process to finish...");
                }
            } 
            while (true);
        }

        /// <summary>
        /// Create new ProcessStartInfo object based on provided inputs
        /// </summary>
        /// <param name="script"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private ProcessStartInfo CreatePsi(string script, string? parameters)
        {
            var actualScript = Environment.ExpandEnvironmentVariables(script);
            var actualParameters = parameters;
            if (actualScript.EndsWith(".ps1"))
            {
                actualScript = settings.Script.PowershellExecutablePath ??
                    (OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh");
                var baseParameters = "-noninteractive -executionpolicy bypass";
                if (OperatingSystem.IsWindows())
                {
                    baseParameters += " -windowstyle hidden";
                }
                actualParameters = $"{baseParameters} -command \"&{{&'{script.Replace("'", "''")}' {parameters?.Replace("\"", "\"\"\"")}; exit $LastExitCode}}\"";
            }
            else if (actualScript.EndsWith(".sh"))
            {
                actualScript = "sh";
                actualParameters = $"{script} {parameters}";
            }
            var ret = new ProcessStartInfo(actualScript)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                RedirectStandardError = true,
                UseShellExecute = false,
                Arguments = actualParameters,
                CreateNoWindow = true
            };
            return ret;
        }

        [GeneratedRegex("{.+?}")]
        private static partial Regex TokenRegex();
    }
}