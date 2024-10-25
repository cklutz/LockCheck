using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using LockCheck;

namespace LockCheckTool;

internal class KillLockingProcessesCommand : ProcessInfoBaseCommand
{
    public Option<bool> Kill { get; } = new("--kill", "Actually kill processes. By default this command only shows which processes would be killed");
    public Option<bool> AllUsers { get; } = new("--all-users", "Attempt to kill all processes. By default only processes belonging to the current user are considered");
    public Option<int> MaxWait { get; } = new("--max-wait", "Wait a maximum of seconds each process to exist. By default process' exit is not awaited");
    public Option<bool> IncludeCritical { get; } = new("--include-critical", "Allow killing critical system processes, if they lock a path." +
        " By default heuristics, like those of Task Manager, prevent critical processes from being killed, even if they lock a path");

    public KillLockingProcessesCommand()
        : base("kill-processes", "Kill processes that lock a specified path")
    {
        AddOption(Kill);
        AddOption(AllUsers);
        AddOption(IncludeCritical);

        var known = LockManager.GetKnownCriticalProcesses();
        if (known.Any())
        {
            var sb = new StringBuilder(IncludeCritical.Description);
            sb.AppendLine(".");
            sb.AppendLine("Known critical system processes:");
            sb.Append("  ");
            sb.AppendLine(string.Join($"{Environment.NewLine}  ", known.OrderBy(s => s)));
            IncludeCritical.Description = sb.ToString();
        }
    }

    protected override int RunCommand(InvocationContext context)
    {
        string currentUser = $"{Environment.UserDomainName}\\{Environment.UserName}";
        var infos = GetLockingProcessInfos(context);
        if (infos.Any())
        {
            bool shouldKill = context.ParseResult.GetValueForOption(Kill);
            bool allProcesses = context.ParseResult.GetValueForOption(AllUsers);
            bool includeCritical = context.ParseResult.GetValueForOption(IncludeCritical);
            int maxWaitSeconds = context.ParseResult.GetValueForOption(MaxWait);

            if (!allProcesses)
            {
                infos = infos.Where(i => currentUser.Equals(i.Owner, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!includeCritical)
            {
                infos = infos.Where(i => !i.IsCritical.HasValue || i.IsCritical.Value == false).ToList();
            }

            int current = 0;
            int total = infos.Count;

            foreach (var info in infos)
            {
                current++;

                try
                {
                    using var process = System.Diagnostics.Process.GetProcessById(info.ProcessId);
                    if (process.StartTime != info.StartTime)
                    {
                        LogWarning(context.Console,
                            $"{Progress(current, total)}Process {GetDisplay(info)} has unexpected start time '{process.StartTime:F}' ('{process.ProcessName}'). " +
                            "This might be because the process ID has been recycled by the operation system");
                    }
                    else
                    {
                        if (!shouldKill)
                        {
                            LogInfo(context.Console, $"{Progress(current, total)}Kill process {GetDisplay(info)}, maximum wait {maxWaitSeconds}");
                        }
                        else
                        {
                            try
                            {
                                process.Kill();

                                if (maxWaitSeconds > 0)
                                {
                                    process.WaitForExit(maxWaitSeconds * 1_000);
                                }
                            }
                            catch (InvalidOperationException ex)
                            {
                                LogInfo(context.Console, $"{Progress(current, total)}Process {GetDisplay(info)} is no longer running: {ex.Message}");
                            }
                            catch (Exception ex)
                            {
                                LogWarning(context.Console, $"{Progress(current, total)}Failed to kill process {GetDisplay(info)}: {ex.Message}");
                            }
                        }
                    }
                }
                catch (ArgumentException ex)
                {
                    LogVerbose(context.Console, $"{Progress(current, total)}Process {GetDisplay(info)} is no longer running: {ex.Message}");
                }
            }
        }

        return 0;

        static string Progress(int current, int total) => $"[{current}/{total}] ";
        static string GetDisplay(ProcessInfo info) => $"{info.ProcessId} ({info.ExecutableName}, owner {info.Owner})";
    }
}
