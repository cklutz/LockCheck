using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;

namespace LockCheckTool;

internal class ListLockingProcessesCommand : ProcessInfoBaseCommand
{
    public Option<string> OutputPath { get; } = new Option<string>("--output", "Write output into specified file").LegalFilePathsOnly();
    public Option<OutputFormats> OutputFormat { get; } = new(["--output-format", "-o"], "Output format");
#if FEATURE_JMSE_QUERY
    public Option<string> Query { get; } = new("--query", "JMESPath query string. See http://jmespath.org/ for more information and examples");
#endif

    public ListLockingProcessesCommand()
        : base("list-processes", "List processes that lock a specified path")
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

            IOutput? actualOutput = null;
            try
            {
                if (outputPath != null)
                {
                    string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                    if (directory != null && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    actualOutput = new FileOutput(File.OpenWrite(outputPath));
                }
                else
                {
                    actualOutput = new ConsoleOutput(context.Console.Out);
                }

                switch (outputFormat)
                {
                    case OutputFormats.None:
                        OutputPlain(actualOutput, infos);
                        break;
                    case OutputFormats.Json:
                    case OutputFormats.PrettyJson:
                        OutputJson(actualOutput, outputFormat, query, infos);
                        break;
                    case OutputFormats.Csv:
                        OutputDelimited(actualOutput, ',', query, infos);
                        break;
                    case OutputFormats.Tsv:
                        OutputDelimited(actualOutput, '\t', query, infos);
                        break;
                }
            }
            finally
            {
                actualOutput?.Dispose();
            }
        }

        return 0;
    }
}
