using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics;
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
        var commandLineBuilder = new CommandLineBuilder(rootCommand)
            .AddMiddleware(async (context, next) =>
            {
                LCCommand.Verbose = context.ParseResult.GetValueForOption(rootCommand.Verbose);
                LCCommand.NoColor = context.ParseResult.GetValueForOption(rootCommand.NoColor);

                await next(context);
            })
            .UseDefaults()
            .UseExceptionHandler((ex, context) =>
            {
                if (ex is not OperationCanceledException)
                {
                    LCCommand.LogError(context.Console, ex.ToStringDemystified());
                }
                context.ExitCode = ex.HResult;
            });

            var parser = commandLineBuilder.Build();
            var parseResult = parser.Parse(args);

            return await parseResult.InvokeAsync();
    }
}
