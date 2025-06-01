using Microsoft.Win32.TaskScheduler;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Extensions;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Net = System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    [SupportedOSPlatform("windows")]
    internal class TaskSchedulerService(
        ISettings settings,
        MainArguments arguments,
        IInputService input,
        ILogService log) : IAutoRenewService
    {
        private string TaskName => $"{settings.Client.ClientName.CleanPath()} renew ({settings.BaseUri.CleanUri()})";
        private static string WorkingDirectory => Path.GetDirectoryName(VersionService.ExePath) ?? "";
        private static string ExecutingFile => Path.GetFileName(VersionService.ExePath);

        private Task? ExistingTask
        {
            get
            {
                using var taskService = new TaskService();
                return taskService.GetTask(TaskName);
            }
        }

        public bool ConfirmAutoRenew()
        {
            try
            {
                var existingTask = ExistingTask;
                if (existingTask != null)
                {
                    return IsHealthy(existingTask);
                }
                else
                {
                    log.Warning("Scheduled task not configured yet");
                    return false;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Scheduled task health check failed");
                return false;
            }
        }

        private bool IsHealthy(Task task)
        {
            var healthy = true;
            var action = task.Definition.Actions.OfType<ExecAction>().
                Where(action => string.Equals(action.Path?.Trim('"'), VersionService.ExePath, StringComparison.OrdinalIgnoreCase)).
                Where(action => string.Equals(action.WorkingDirectory?.Trim('"'), WorkingDirectory, StringComparison.OrdinalIgnoreCase)).
                FirstOrDefault();
            var trigger = task.Definition.Triggers.FirstOrDefault();
            if (action == null)
            {
                healthy = false;
                log.Warning("Scheduled task points to different location for .exe and/or working directory");
            }
            else
            {
                var filtered = action.Arguments.Replace("--verbose", "").Trim();
                if (!string.Equals(filtered, Arguments, StringComparison.OrdinalIgnoreCase))
                {
                    healthy = false;
                    log.Warning("Scheduled task arguments do not match with expected value");
                }
            }
            if (trigger == null)
            {
                healthy = false;
                log.Warning("Scheduled task doesn't have a trigger configured");
            }
            else
            {
                if (!trigger.Enabled)
                {
                    healthy = false;
                    log.Warning("Scheduled task trigger is disabled");
                }
                if (trigger is DailyTrigger dt)
                {
                    if (dt.StartBoundary.TimeOfDay != settings.ScheduledTask.StartBoundary)
                    {
                        healthy = false;
                        log.Warning("Scheduled task start time mismatch");
                    }
                    if (dt.RandomDelay != settings.ScheduledTask.RandomDelay)
                    {
                        healthy = false;
                        log.Warning("Scheduled task random delay mismatch");
                    }
                }
                else
                {
                    healthy = false;
                    log.Warning("Scheduled task trigger is not daily");
                }
            }
            if (task.Definition.Settings.ExecutionTimeLimit != settings.ScheduledTask.ExecutionTimeLimit)
            {
                healthy = false;
                log.Warning("Scheduled task execution time limit mismatch");
            }
            if (!task.Enabled)
            {
                healthy = false;
                log.Warning("Scheduled task is disabled");
            }

            // Report final result
            if (healthy)
            {
                log.Debug("Scheduled task looks healthy");
                return true;
            }
            else
            {
                log.Warning("Scheduled task exists but does not look healthy");
                return false;
            }
        }

        /// <summary>
        /// Arguments that are supposed to be passed to wacs.exe when the
        /// scheduled task runs
        /// </summary>
        private string Arguments =>
            $"--{nameof(MainArguments.Renew).ToLowerInvariant()} " +
            $"--{nameof(MainArguments.BaseUri).ToLowerInvariant()} " +
            $"\"{settings.BaseUri}\"";

        /// <summary>
        /// Decide to (re)create scheduled task or not
        /// </summary>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        public async Net.Task EnsureAutoRenew(RunLevel runLevel)
        {
            var existingTask = ExistingTask;
            var create = runLevel.HasFlag(RunLevel.ForceTaskScheduler) || existingTask == null;
            if (!create && existingTask != null && !IsHealthy(existingTask))
            {
                if (runLevel.HasFlag(RunLevel.Interactive))
                {
                    create = await input.PromptYesNo($"Do you want to replace the existing task?", false);
                }
                else
                {
                    log.Warning("Proceeding with unhealthy scheduled task, automatic renewals may not work until this is addressed");
                }
            }
            if (create)
            {
                await SetupAutoRenew(runLevel);
            }
        }

        /// <summary>
        /// (Re)create the scheduled task
        /// </summary>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        public async Net.Task SetupAutoRenew(RunLevel runLevel)
        {
            using var taskService = new TaskService();
            var existingTask = ExistingTask;
            if (existingTask != null)
            {
                log.Information("Deleting existing task {taskName} from Windows Task Scheduler.", TaskName);
                taskService.RootFolder.DeleteTask(TaskName, false);
            }

            log.Information("Adding Task Scheduler entry with the following settings", TaskName);
            log.Information("- Name {name}", TaskName);
            log.Information("- Path {action}", WorkingDirectory);
            log.Information("- Command {exec} {action}", ExecutingFile, Arguments);
            log.Information("- Start at {start}", settings.ScheduledTask.StartBoundary);
            if (settings.ScheduledTask.RandomDelay.TotalMinutes > 0)
            {
                log.Information("- Random delay {delay}", settings.ScheduledTask.RandomDelay);
            }
            log.Information("- Time limit {limit}", settings.ScheduledTask.ExecutionTimeLimit);

            // Create a new task definition and assign properties
            var task = taskService.NewTask();
            task.RegistrationInfo.Description = "Check for renewal of ACME certificates.";

            var now = DateTime.Now;
            var runtime = new DateTime(now.Year, now.Month, now.Day,
                settings.ScheduledTask.StartBoundary.Hours,
                settings.ScheduledTask.StartBoundary.Minutes,
                settings.ScheduledTask.StartBoundary.Seconds);

            task.Triggers.Add(new DailyTrigger
            {
                DaysInterval = 1,
                StartBoundary = runtime,
                RandomDelay = settings.ScheduledTask.RandomDelay
            });
            task.Settings.ExecutionTimeLimit = settings.ScheduledTask.ExecutionTimeLimit;
            task.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew;
            task.Settings.RunOnlyIfNetworkAvailable = true;
            task.Settings.DisallowStartIfOnBatteries = false;
            task.Settings.StopIfGoingOnBatteries = false;
            task.Settings.StartWhenAvailable = true;

            // Create an action that will launch the app with the renew parameters whenever the trigger fires
            var actionPath = VersionService.ExePath;
            if (actionPath.IndexOf(' ') > -1)
            {
                actionPath = $"\"{actionPath}\"";
            }
            var workingPath = WorkingDirectory;
            _ = task.Actions.Add(new ExecAction(actionPath, Arguments, workingPath));

            task.Principal.RunLevel = TaskRunLevel.Highest;
            while (true)
            {
                try
                {
                    if (!arguments.UseDefaultTaskUser &&
                        runLevel.HasFlag(RunLevel.Interactive | RunLevel.Advanced) &&
                        await input.PromptYesNo($"Do you want to specify the user the task will run as?", false))
                    {
                        // Ask for the login and password to allow the task to run 
                        var username = await input.RequestString("Enter the username (Domain\\username)");
                        var password = await input.ReadPassword("Enter the user's password");
                        log.Debug("Creating task to run as {username}", username);
                        try
                        {
                            taskService.RootFolder.RegisterTaskDefinition(
                                TaskName,
                                task,
                                TaskCreation.Create,
                                username,
                                password,
                                TaskLogonType.Password);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            log.Error("Unable to register scheduled task, please run as administrator or equivalent");
                        }
                    }
                    else if (existingTask != null)
                    {
                        log.Debug("Creating task to run with previously chosen credentials");
                        string? password = null;
                        string? username = null;
                        if (existingTask.Definition.Principal.LogonType == TaskLogonType.Password)
                        {
                            username = existingTask.Definition.Principal.UserId;
                            password = await input.ReadPassword($"Password for {username}");
                        }
                        task.Principal.UserId = existingTask.Definition.Principal.UserId;
                        task.Principal.LogonType = existingTask.Definition.Principal.LogonType;
                        try
                        {
                            taskService.RootFolder.RegisterTaskDefinition(
                                TaskName,
                                task,
                                TaskCreation.CreateOrUpdate,
                                username,
                                password,
                                existingTask.Definition.Principal.LogonType);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            log.Error("Unable to register scheduled task, please run as administrator or equivalent");
                        }
                    }
                    else
                    {
                        log.Debug("Creating task to run as system user");
                        task.Principal.UserId = "SYSTEM";
                        task.Principal.LogonType = TaskLogonType.ServiceAccount;
                        try
                        {
                            taskService.RootFolder.RegisterTaskDefinition(
                                TaskName,
                                task,
                                TaskCreation.CreateOrUpdate,
                                null,
                                null,
                                TaskLogonType.ServiceAccount);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            log.Error("Unable to register scheduled task, please run as administrator or equivalent");
                        }
                    }
                    break;
                }
                catch (COMException cex)
                {
                    if (cex.HResult == -2147023570)
                    {
                        log.Warning("Invalid username/password, please try again");
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }
    }
}
