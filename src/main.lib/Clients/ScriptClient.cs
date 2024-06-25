using PKISharp.WACS.Services;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace PKISharp.WACS.Clients
{
    public class ScriptClient(ILogService logService, ISettingsService settings)
    {
        public async Task<bool> RunScript(string script, string parameters, string? censoredParameters = null)
        {
            if (!string.IsNullOrWhiteSpace(script))
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
                    actualParameters = $"{baseParameters} -command \"&{{&'{script.Replace("'", "''")}' {parameters.Replace("\"", "\"\"\"")}; exit $LastExitCode}}\"";
                } 
                else if (actualScript.EndsWith(".sh"))
                {
                    actualScript = "sh";
                    actualParameters = $"{script} {parameters}";
                }
                var PSI = new ProcessStartInfo(actualScript)
                {
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                if (!string.IsNullOrWhiteSpace(actualParameters))
                {
                    logService.Information(LogType.All, "Script {script} starting with parameters {parameters}", script, censoredParameters ?? parameters);
                    PSI.Arguments = actualParameters;
                }
                else
                {
                    logService.Information(LogType.All, "Script {script} starting", script);
                }
                try
                {
                    using var process = new Process { StartInfo = PSI };
                    var output = new StringBuilder();
                    process.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            output.AppendLine(e.Data);
                            logService.Verbose(e.Data);
                        }
                        else
                        {
                            logService.Verbose("Process output without data received");
                        }
                    };
                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data) && !string.Equals(e.Data, "null"))
                        {
                            output.AppendLine($"Error: {e.Data}");
                            logService.Error("Script error: {0}", e.Data);
                        }
                        else
                        {
                            logService.Verbose("Process error without data received");
                        }
                    };
                    var exited = false;
                    process.EnableRaisingEvents = true;
                    process.Exited += (s, e) =>
                    {
                        logService.Information(LogType.Event | LogType.Disk | LogType.Notification, output.ToString());
                        exited = true;
                        if (process.ExitCode != 0)
                        {
                            logService.Error("Script finished with exit code {code}", process.ExitCode);
                        }
                        else
                        {
                            logService.Information("Script finished");
                        }
                    };
                    if (process.Start())
                    {
                        logService.Debug("Process launched: {actualScript} (ID: {Id})", actualScript, process.Id);
                    }
                    else
                    {
                        throw new Exception("Process.Start() returned false");
                    }

                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();
                    process.StandardInput.Close(); // Helps end the process
                    var totalWait = 0;
                    var interval = 2000;
                    while (!exited && totalWait < settings.Script.Timeout * 1000)
                    {
                        await Task.Delay(interval);
                        totalWait += interval;
                        logService.Verbose("Waiting for process to finish...");
                    }
                    if (!exited)
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
                        return false;
                    } 
                    else
                    {
                        return process.ExitCode == 0;
                    }
                }
                catch (Exception ex)
                {
                    logService.Error(ex, "Script is unable to start");
                    return false;
                }
            }
            else
            {
                logService.Warning("No script configured.");
                return false;
            }
        }
    }
}