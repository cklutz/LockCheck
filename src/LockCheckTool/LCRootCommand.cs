using System.CommandLine;

namespace LockCheckTool;

internal class LCRootCommand : RootCommand
{
    public Option<bool> Verbose { get; } = new("--verbose", "Show verbose output");

    public LCRootCommand()
    {
        AddGlobalOption(Verbose);
        AddCommand(new ListLockingProcessesCommand());
        AddCommand(new KillLockingProcessesCommand());
    }
}
