using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
#if FEATURE_JMSE_QUERY
using JsonCons.JmesPath;
#endif

namespace LockCheckTool;

internal abstract class LCCommand : Command
{
    internal static bool Verbose { get; set; }

    protected LCCommand(string name, string? description = null)
        : base(name, description)
    {
        this.SetHandler((context) =>
        {
            try
            {
                context.ExitCode = RunCommand(context);
            }
            catch (Exception ex)
            {
                LogVerbose(context.Console, ex.ToString());
                context.ExitCode = ex.HResult;
            }
        });
    }

    protected abstract int RunCommand(InvocationContext context);

    protected void LogVerbose(IConsole console, string message)
    {
        if (Verbose)
        {
            console.WriteLine(message);
        }
    }

#if NET
    protected void LogVerbose(IConsole console, ref DefaultInterpolatedStringHandler handler)
    {
        if (Verbose)
        {
            console.WriteLine(handler.ToStringAndClear());
        }
    }
#endif

    protected void LogInfo(IConsole console, string message) => console.WriteLine(message);
#if NET
    protected void LogInfo(IConsole console, ref DefaultInterpolatedStringHandler handler) => console.WriteLine(handler.ToStringAndClear());
#endif

    protected void LogWarning(IConsole console, string message) => console.Error.Write("warning: " + message + Environment.NewLine);
#if NET
    protected void LogWarning(IConsole console, ref DefaultInterpolatedStringHandler handler) => console.Error.Write("warning: " + handler.ToStringAndClear() + Environment.NewLine);
#endif

    protected void LogError(IConsole console, string message) => console.Error.Write("error: " + message + Environment.NewLine);
#if NET
    protected void LogError(IConsole console, ref DefaultInterpolatedStringHandler handler) => console.Error.Write("error: " + handler.ToStringAndClear() + Environment.NewLine);
#endif

    protected static IOutput GetActualOutput(InvocationContext context, string? outputPath)
    {
        IOutput? actualOutput;
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

        return actualOutput;
    }


    protected void HandleCommonOutputFormats<T>(IOutput output, OutputFormats outputFormat, string? query, IEnumerable<T> data)
    {
        switch (outputFormat)
        {
            case OutputFormats.Json:
            case OutputFormats.PrettyJson:
                OutputJson(output, outputFormat, query, data);
                break;
            case OutputFormats.Csv:
                OutputDelimited(output, ',', query, data);
                break;
            case OutputFormats.Tsv:
                OutputDelimited(output, '\t', query, data);
                break;
            default:
                // TODO: Dump objects using reflection as "fall back"
                throw new ArgumentOutOfRangeException(nameof(outputFormat), outputFormat, null);
        }
    }


    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    protected static JsonElement GetAsJsonElement<T>(string? query, IEnumerable<T> data)
    {
#if FEATURE_JMSE_QUERY
        if (query != null)
        {
            using var doc = JsonSerializer.SerializeToDocument(data, s_jsonOptions);
            var transformed = JsonTransformer.Transform(doc.RootElement, query);
            return transformed.RootElement;
        }
#endif

        return JsonSerializer.SerializeToDocument(data, s_jsonOptions).RootElement;
    }

    private static string GetJson<T>(string? query, IEnumerable<T> data, bool pretty)
    {
        var options = pretty ? new JsonSerializerOptions(s_jsonOptions) { WriteIndented = true } : s_jsonOptions;

        string json;
        if (query != null)
        {
            var transformed = GetAsJsonElement(query, data);
            json = JsonSerializer.Serialize(transformed, options);
        }
        else
        {
            json = JsonSerializer.Serialize(data, options);
        }
        return json;
    }

    protected static void OutputDelimited<T>(IOutput output, char delimiter, string? query, IEnumerable<T> data)
    {
        var element = GetAsJsonElement(query, data);
        FormatSupport.FormatAsRowsWithDelimiter(element, delimiter, output, true);
    }

    protected static void OutputJson<T>(IOutput output, OutputFormats outputFormat, string? query, IEnumerable<T> data)
    {
        string json = GetJson(query, data, outputFormat == OutputFormats.PrettyJson);
        output.WriteLine(json);
    }
}
