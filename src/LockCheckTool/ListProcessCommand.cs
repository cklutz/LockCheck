using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading;
using LockCheck;

namespace LockCheckTool;

internal class ListProcessCommand : LCCommand
{
    public Option<string> OutputPath { get; } = new Option<string>("--output", "Write output into specified file").LegalFilePathsOnly();
    public Option<OutputFormats> OutputFormat { get; } = new(["--output-format", "-o"], "Output format");
#if FEATURE_JMSE_QUERY
    public Option<string> Query { get; } = new("--query", "JMESPath query string. See http://jmespath.org/ for more information and examples");
#endif

    public ListProcessCommand()
        : base("list", "List system processes")
    {
        AddOption(OutputPath);
        AddOption(OutputFormat);
#if FEATURE_JMSE_QUERY
        AddOption(Query);
#endif
    }

    protected override int RunCommand(InvocationContext context)
    {
        var processes = LockManager.GetAllProcesses();
        if (processes.Any())
        {
            string? outputPath = context.ParseResult.GetValueForOption(OutputPath);
            var outputFormat = context.ParseResult.GetValueForOption(OutputFormat);

#if FEATURE_JMSE_QUERY
            string? query = context.ParseResult.GetValueForOption(Query);
#else
            string? query = null;
#endif

            using var actualOutput = GetActualOutput(context, outputPath);
            HandleCommonOutputFormats(actualOutput, outputFormat, query, processes);
        }

        return 0;
    }
}

