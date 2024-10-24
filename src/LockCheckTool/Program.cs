using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LockCheck;
using System.Diagnostics;

#if FEATURE_JMSE_QUERY
using JsonCons.JmesPath;
#endif

namespace LockCheckTool;

internal class Program
{

    private class FileOutput : IOutput
    {
        private static readonly byte[] s_newLineBytes = Environment.NewLine.Select(c => (byte)c).ToArray();
        private readonly Stream _fileStream;

        public FileOutput(Stream fileStream)
        {
            _fileStream = fileStream;
        }

        public void Dispose()
        {
            _fileStream.Dispose();
        }

        public void Write(char c)
        {
            _fileStream.Write([(byte)c], 0, 1);
        }

        public void Write(string? text)
        {
            if (text != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                _fileStream.Write(bytes, 0, bytes.Length);
            }
        }

        public void WriteLine(string? text)
        {
            if (text != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text + Environment.NewLine);
                _fileStream.Write(bytes, 0, bytes.Length);
            }
        }

        public void WriteLine()
        {
            _fileStream.Write(s_newLineBytes, 0, s_newLineBytes.Length);
        }
    }

    private class ConsoleOutput : IOutput
    {
        private readonly IStandardStreamWriter _out;
        public ConsoleOutput(IStandardStreamWriter @out) => _out = @out;
        public void Dispose() { }
        public void Write(char c) => _out.Write(c.ToString());
        public void Write(string? value) => _out.Write(value);
        public void WriteLine(string? value) => _out.WriteLine(value ?? "");
        public void WriteLine() => _out.WriteLine();
    }

    /*
     *  lockchecktool locked list <PATH>
     * 
     */



    private static bool s_verbose;


#if NET
    private static void Verbose(IConsole console, ref DefaultInterpolatedStringHandler handler)
    {
        if (s_verbose)
        {
            console.Out.WriteLine(handler.ToStringAndClear());
        }
    }
#else
    private static void Verbose(IConsole console, string str)
    {
        if (s_verbose)
        {
            console.Out.WriteLine(str);
        }
    }
#endif

    private static async Task<int> Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("LOCKCHECKTOOL_DEBUG") == "1")
        {
            Debugger.Launch();
        }

        try
        {
            var verboseOption = new Option<bool>("--verbose", "Show additional output");

            var rootCommand = new RootCommand("Check for files/directories that are locked (in-use) by processes");
            rootCommand.AddGlobalOption(verboseOption);

            var lockedCommand = new Command("locked", "Handled locked files/directories");
            rootCommand.AddCommand(lockedCommand);

            var includeCwdOption = new Option<bool>(["--include-cwd", "-d"], "Check for processes' current working directories");
            var useRmOption = new Option<bool>("--use-rm", "Use RestartManager API (Windows only)");
            var outputPathOption = new Option<string>("--output", "Write output into specified file")
                .LegalFilePathsOnly();
            var outputFormatOption = new Option<OutputFormat>(["--output-format", "-o"], "Output format");
#if FEATURE_JMSE_QUERY
            var queryOption = new Option<string>("--query", "JMESPath query string. See http://jmespath.org/ for more information and examples");
#endif
            var pathsArgument = new Argument<IEnumerable<string>>("path", "The path or paths to check for")
                .LegalFilePathsOnly();

            var listCommand = new Command("list", "List locked files/directories");
            lockedCommand.AddCommand(listCommand);
            listCommand.AddOption(includeCwdOption);
            listCommand.AddOption(useRmOption);
            listCommand.AddOption(outputFormatOption);
            listCommand.AddOption(outputPathOption);
#if FEATURE_JMSE_QUERY
            listCommand.AddOption(queryOption);
#endif
            listCommand.AddArgument(pathsArgument);

            listCommand.SetHandler((context) =>
            {
                bool useRm = context.ParseResult.GetValueForOption(useRmOption);
                var features = useRm ? default : LockManagerFeatures.UseLowLevelApi;

                bool includeCwd = context.ParseResult.GetValueForOption(includeCwdOption);
                if (includeCwd)
                {
                    features |= LockManagerFeatures.CheckDirectories;
                }

                var paths = context.ParseResult.GetValueForArgument(pathsArgument);
                var infos = LockManager.GetLockingProcessInfos(paths.ToArray(), features);
                Verbose(context.Console, $"Found {infos.Count():N0} matching processes");

                string? outputPath = context.ParseResult.GetValueForOption(outputPathOption);
                var outputFormat = context.ParseResult.GetValueForOption(outputFormatOption);

#if FEATURE_JMSE_QUERY
                string? query = context.ParseResult.GetValueForOption(queryOption);
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
                        case OutputFormat.None:
                            OutputPlain(actualOutput, infos);
                            break;
                        case OutputFormat.Json:
                        case OutputFormat.PrettyJson:
                            OutputJson(actualOutput, outputFormat, query, infos);
                            break;
                        case OutputFormat.Csv:
                            OutputDelimited(actualOutput, ',', query, infos);
                            break;
                        case OutputFormat.Tsv:
                            OutputDelimited(actualOutput, '\t', query, infos);
                            break;
                    }
                }
                finally
                {
                    actualOutput?.Dispose();
                }
            });

            var commandLineBuilder = new CommandLineBuilder(rootCommand);
            commandLineBuilder.AddMiddleware(async (context, next) =>
            {
                if (context.ParseResult.GetValueForOption(verboseOption))
                {
                    s_verbose = true;
                }

                await next(context);
            });
            commandLineBuilder.UseDefaults();
            var parser = commandLineBuilder.Build();
            var parseResult = parser.Parse(args);
            return await parseResult.InvokeAsync();
        }
        catch (Win32Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ex.ErrorCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return ex.HResult;
        }
    }

    private enum OutputFormat
    {
        None,
        Json,
        PrettyJson,
        Csv,
        Tsv
    }

    private static void OutputDelimited(IOutput output, char delimiter, string? query, IEnumerable<ProcessInfo> processInfos)
    {
        var element = GetAsJsonElement(query, processInfos);
        FormatSupport.FormatAsRowsWithDelimiter(element, delimiter, output, true);
    }

    private static void OutputJson(IOutput output, OutputFormat outputFormat, string? query, IEnumerable<ProcessInfo> processInfos)
    {
        string json = GetJson(query, processInfos, outputFormat == OutputFormat.PrettyJson);
        output.WriteLine(json);
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    private static JsonElement GetAsJsonElement(string? query, IEnumerable<ProcessInfo> processInfos)
    {
#if FEATURE_JMSE_QUERY
        if (query != null)
        {
            using var doc = JsonSerializer.SerializeToDocument(processInfos, s_jsonOptions);
            var transformed = JsonTransformer.Transform(doc.RootElement, query);
            return transformed.RootElement;
        }
#endif

        return JsonSerializer.SerializeToDocument(processInfos, s_jsonOptions).RootElement;
    }

    private static string GetJson(string? query, IEnumerable<ProcessInfo> processInfos, bool pretty)
    {
        var options = pretty ? new JsonSerializerOptions(s_jsonOptions) { WriteIndented = true } : s_jsonOptions;

        string json;
        if (query != null)
        {
            var transformed = GetAsJsonElement(query, processInfos);
            json = JsonSerializer.Serialize(transformed, options);
        }
        else
        {
            json = JsonSerializer.Serialize(processInfos, options);
        }
        return json;
    }

    private static void OutputPlain(IOutput output, IEnumerable<ProcessInfo> processInfos)
    {
        bool first = true;
        foreach (var p in processInfos)
        {
            if (!first)
            {
                output.WriteLine("----------------------------------------------------");
            }

            output.WriteLine($"Process ID        : {p.ProcessId}");
            output.WriteLine($"Application Name  : {p.ApplicationName}");
            output.WriteLine($"Path              : {p.ExecutableFullPath}");
            output.WriteLine($"Process Start Time: {p.StartTime:F}");
            output.WriteLine($"Owner             : {p.Owner}");
            output.WriteLine($"SessionId         : {p.SessionId}");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                output.WriteLine($"LockAccess        : {p.LockAccess}");
                output.WriteLine($"LockMode          : {p.LockMode}");
                output.WriteLine($"LockType          : {p.LockType}");
            }

            first = false;
        }
    }
}
