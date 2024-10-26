using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Runtime.InteropServices;
using LockCheck;

namespace LockCheckTool;

internal abstract class ProcessInfoBaseCommand : LCCommand
{
    public Option<bool> IncludeCwd { get; } = new(["--include-cwd", "-D"], "Also check processes' current working directories");
    public Option<bool> UseRestartManager { get; } = new("--use-restart-manager", "Use RestartManager API (Windows only)");
    public Argument<IEnumerable<string>> Paths { get; } = new Argument<IEnumerable<string>>("path", "Paths to consider").LegalFilePathsOnly();

    protected ProcessInfoBaseCommand(string name, string? description = null)
        : base(name, description)
    {
        AddOption(IncludeCwd);
        AddOption(UseRestartManager);
        AddArgument(Paths);
    }

    protected List<ProcessInfo> GetLockingProcessInfos(InvocationContext context)
    {
        bool useRm = context.ParseResult.GetValueForOption(UseRestartManager);
        var features = useRm ? default : LockManagerFeatures.UseLowLevelApi;

        bool includeCwd = context.ParseResult.GetValueForOption(IncludeCwd);
        if (includeCwd)
        {
            features |= LockManagerFeatures.CheckDirectories;
        }

        LogVerbose(context.Console, "Querying processes ...");
        var paths = context.ParseResult.GetValueForArgument(Paths);
        var infos = LockManager.GetLockingProcessInfos(paths.ToArray(), features);
        LogVerbose(context.Console, $"Found {infos.Count():N0} processes");

        return infos.ToList();
    }
}
