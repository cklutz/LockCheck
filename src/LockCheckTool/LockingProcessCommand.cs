using System.CommandLine;

namespace LockCheckTool;

internal class LockingProcessCommand : Command
{
    public LockingProcessCommand()
        : base("locking-process", "Handle processes locking paths")
    {
        AddAlias("lp");

        AddCommand(new ListLockingProcessCommand());
        AddCommand(new KillLockingProcessCommand());
    }
}

