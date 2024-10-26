using System.CommandLine;

namespace LockCheckTool;

internal class ProcessCommand : Command
{
    public ProcessCommand()
        : base("process", "Handle processes")
    {
        AddAlias("proc");

        AddCommand(new ListProcessCommand());
    }
}

