using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace LockCheckTool;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("LOCKCHECKTOOL_DEBUG") == "1")
        {
            Debugger.Launch();
        }

        var rootCommand = new LCRootCommand();
        var commandLineBuilder = new CommandLineBuilder(rootCommand);
        commandLineBuilder.AddMiddleware(async (context, next) =>
        {
            LCCommand.Verbose = context.ParseResult.GetValueForOption(rootCommand.Verbose);
            LCCommand.NoColor = context.ParseResult.GetValueForOption(rootCommand.NoColor);

            await next(context);
        });
        commandLineBuilder.UseDefaults();
        var parser = commandLineBuilder.Build();
        var parseResult = parser.Parse(args);

        return await parseResult.InvokeAsync();
    }
}
