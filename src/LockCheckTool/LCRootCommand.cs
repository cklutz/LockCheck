using System;
using System.CommandLine;
using System.Linq;

namespace LockCheckTool;

internal class LCRootCommand : RootCommand
{
    public Option<bool> Verbose { get; } = new("--verbose", "Show verbose output");
    public Option<bool> NoColor { get; } = new("--no-color", "Use simple output");

    public LCRootCommand()
    {
        NoColor.SetDefaultValueFactory(static () =>
        {
            // https://no-color.org/: "Command-line software which adds ANSI color to its
            // output by default should check for a NO_COLOR environment variable that,
            // when present and not an empty string (regardless of its value),
            // prevents the addition of ANSI color."
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
        });

        AddGlobalOption(Verbose);
        AddGlobalOption(NoColor);

        AddCommand(new LockingProcessCommand());
        AddCommand(new ProcessCommand());
    }
}
