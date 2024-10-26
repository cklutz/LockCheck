using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;

namespace LockCheckTool;

internal class ListLockingProcessCommand : ProcessInfoBaseCommand
{
    public Option<string> OutputPath { get; } = new Option<string>("--output", "Write output into specified file").LegalFilePathsOnly();
    public Option<OutputFormats> OutputFormat { get; } = new(["--output-format", "-o"], "Output format");
#if FEATURE_JMSE_QUERY
    public Option<string> Query { get; } = new("--query", "JMESPath query string. See http://jmespath.org/ for more information and examples");
#endif

    public ListLockingProcessCommand()
        : base("list", "List processes that lock a specified path")
    {
        AddOption(OutputPath);
        AddOption(OutputFormat);
#if FEATURE_JMSE_QUERY
        AddOption(Query);
#endif
    }

    protected override int RunCommand(InvocationContext context)
    {
        var infos = GetLockingProcessInfos(context);
        if (infos.Any())
        {
            string? outputPath = context.ParseResult.GetValueForOption(OutputPath);
            var outputFormat = context.ParseResult.GetValueForOption(OutputFormat);

#if FEATURE_JMSE_QUERY
            string? query = context.ParseResult.GetValueForOption(Query);
#else
            string? query = null;
#endif

            using var actualOutput = GetActualOutput(context, outputPath);
            HandleCommonOutputFormats(actualOutput, outputFormat, query, infos);
        }

        return 0;
    }
}
