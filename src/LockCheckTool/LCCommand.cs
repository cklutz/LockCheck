using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using LockCheck;
using Spectre.Console;
using Spectre.Console.Json;
using Spectre.Console.Rendering;
using System.Runtime.InteropServices;
using System.CommandLine.IO;


#if FEATURE_JMSE_QUERY
using JsonCons.JmesPath;
#endif

namespace LockCheckTool;

internal abstract class LCCommand : Command
{
    internal static bool Verbose { get; set; }
    internal static bool NoColor { get; set; }

    protected LCCommand(string name, string? description = null)
        : base(name, description)
    {
        this.SetHandler((context) => context.ExitCode = RunCommand(context));
    }

    protected abstract int RunCommand(InvocationContext context);

    protected internal static void LogVerbose(IConsole console, string message)
    {
        if (Verbose)
        {
            LogCore(console, LogLevel.Verbose, message);
        }
    }

#if NET
    protected internal static void LogVerbose(IConsole console, ref DefaultInterpolatedStringHandler handler)
    {
        if (Verbose)
        {
            LogCore(console, LogLevel.Verbose, handler.ToStringAndClear());
        }
    }
#endif

    protected internal static void LogInfo(IConsole console, string message) => LogCore(console, LogLevel.Info, message);
#if NET
    protected internal static void LogInfo(IConsole console, ref DefaultInterpolatedStringHandler handler) => LogCore(console, LogLevel.Info, handler.ToStringAndClear());
#endif

    protected internal static void LogWarning(IConsole console, string message) => LogCore(console, LogLevel.Warning, message);
#if NET
    protected internal static void LogWarning(IConsole console, ref DefaultInterpolatedStringHandler handler) => LogCore(console, LogLevel.Warning, handler.ToStringAndClear());
#endif

    protected internal static void LogError(IConsole console, string message) => LogCore(console, LogLevel.Error, message);
#if NET
    protected internal static void LogError(IConsole console, ref DefaultInterpolatedStringHandler handler) => LogCore(console, LogLevel.Error, handler.ToStringAndClear());
#endif

    private enum LogLevel { Verbose, Info, Warning, Error };
    private static void LogCore(IConsole console, LogLevel logLevel, string message)
    {
        bool isRedirected = false;
        string? prefix = null;
        var writer = console.Out;

        switch (logLevel)
        {
            case LogLevel.Verbose:
            case LogLevel.Info:
                isRedirected = console.IsOutputRedirected;
                writer = console.Out;
                break;
            case LogLevel.Warning:
                isRedirected = console.IsErrorRedirected;
                writer = console.Error;
                prefix = "warning: ";
                break;
            case LogLevel.Error:
                isRedirected = console.IsErrorRedirected;
                writer = console.Error;
                prefix = "error: ";
                break;
        }

        if (isRedirected)
        {
            writer.Write($"{prefix}{message}{Environment.NewLine}");
        }
        else
        {
            FormattableString markup;
            switch (logLevel)
            {
                case LogLevel.Verbose:
                    markup = $"[gray]{message}[/]";
                    break;
                case LogLevel.Info:
                    markup = $"{message}";
                    break;
                case LogLevel.Warning:
                    markup = $"[bold yellow]{message}[/]";
                    break;
                case LogLevel.Error:
                default:
                    markup = $"[bold red]{message}[/]";
                    break;
            }

            // If not redirected, there is no difference of whether we write to stderr/stdout.
            AnsiConsole.MarkupLineInterpolated(markup);
        }
   }

    protected IOutput GetActualOutput(InvocationContext context, string? outputPath)
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
            actualOutput = new ConsoleOutput(context.Console.Out, NoColor);
        }

        return actualOutput;
    }


    protected void HandleCommonOutputFormats<T>(IOutput output, OutputFormats outputFormat, string? query, IEnumerable<T> data)
    {
        switch (outputFormat)
        {
            case OutputFormats.PlainJson:
            case OutputFormats.Json:
                OutputJson(output, outputFormat, query, data);
                break;
            case OutputFormats.Csv:
                OutputDelimited(output, ',', query, data);
                break;
            case OutputFormats.Tsv:
                OutputDelimited(output, '\t', query, data);
                break;
            case OutputFormats.Table:
                OutputTable(output, query, data);
                break;
            default:
                OutputPlain(output, query, data);
                break;
        }
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            .WithAddedModifier(
                // Adding this here, not in LockCheck.dll, because we don't want to introduce a dependency
                // to System.Text.Json there, especially not for net481.
                typeInfo =>
                {
                    if (typeInfo.Type == typeof(IProcessDetails))
                    {
                        typeInfo.PolymorphismOptions = new()
                        {
                            UnknownDerivedTypeHandling = System.Text.Json.Serialization.JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor,
                            DerivedTypes =
                            {
                                new(typeof(IWin32ProcessDetails), "windows"),
                                new(typeof(ILinuxProcessDetails), "linux")
                            }
                        };
                    }
                })
    };

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

    protected virtual void OutputDelimited<T>(IOutput output, char delimiter, string? query, IEnumerable<T> data)
    {
        var element = GetAsJsonElement(query, data);
        FormatSupport.FormatAsRowsWithDelimiter(element, delimiter, output, true);
    }

    protected virtual void OutputTable<T>(IOutput output, string? query, IEnumerable<T> data)
    {
        if (output.NoColor)
        {
            OutputDelimited(output, ' ', query, data);
            return;
        }

        // Use query if specified, to allow user to select the columns to display via "--query".
        // So first convert to respective JSON and then format this as a table.
        var element = GetAsJsonElement(query, data);
        AnsiConsole.Write(RenderJsonArrayAsTable(element));
    }

    static IRenderable RenderJsonArrayAsTable(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Unexpected value kind {element.ValueKind}.");
        }

        var table = new Table();
        table.SimpleBorder();

        bool first = true;
        List<IRenderable>? rowValues = null;
        foreach (var entry in element.EnumerateArray())
        {
            if (first)
            {
                rowValues = new List<IRenderable>();
                foreach (JsonProperty property in entry.EnumerateObject())
                {
                    var field = GetField(property);
                    if (field != null)
                    {
                        var column = table.AddColumn(property.Name);
                        rowValues.Add(field);
                    }
                }

                first = false;
            }
            else
            {
                rowValues!.Clear();
                foreach (JsonProperty property in entry.EnumerateObject())
                {
                    var field = GetField(property);
                    if (field != null)
                    {
                        rowValues.Add(field);
                    }
                }
            }

            table.AddRow(rowValues);
        }

        return table;
    }

#if false
    // TODO: Currently unused, because generically using them for bool "true"/"false" has subtile effects
    // on output. For example, if "HasErrors = false" it is actually "a good thing", but on first sight
    // representing this with a "red cross mark" looks like an error.

    // If these don't show up correctly, ensure that your console output encoding is UTF8
    // Powershell: [console]::InputEncoding = [console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
    // We could also set this in Program.Main(), but then things like "lockchecktool | more" will return
    // garbage.
    private static readonly string s_checkMark = Emoji.Replace(":check_mark:");
    private static readonly string s_crossMark = Emoji.Replace(":cross_mark:");
#endif

    static IRenderable? GetField(JsonProperty property)
    {
        if (property.NameEquals("$type") || property.Value.ValueKind == JsonValueKind.Object)
        {
            return null;
        }

        object? value = property.Value.GetValueApproximation();

        switch (property.Value.ValueKind)
        {
            case JsonValueKind.String:
                if (value == null) goto case JsonValueKind.Null;
                return new Text(value?.ToString()!).LeftJustified();
            case JsonValueKind.Number:
                if (value == null) goto case JsonValueKind.Null;
                return new Text(value?.ToString()!).RightJustified();
            case JsonValueKind.True:
            case JsonValueKind.False:
                if (value == null) goto case JsonValueKind.Null;
                return new Text(((bool)value) ? "true" : "false").Centered();
            case JsonValueKind.Null:
                return new Text("");
            case JsonValueKind.Array:
                return RenderJsonArrayAsTable(property.Value);
        }

        return new Text(value?.ToString() ?? "");
    }

    protected virtual void OutputJson<T>(IOutput output, OutputFormats outputFormat, string? query, IEnumerable<T> data)
    {
        if (output.NoColor || outputFormat == OutputFormats.PlainJson)
        {
            string json = GetJson(query, data, outputFormat == OutputFormats.Json);
            output.WriteLine(json);
        }
        else
        {
            string json = GetJson(query, data, false);
            var formatted = new JsonText(json);
            AnsiConsole.Write(formatted);
        }
    }

    protected virtual void OutputPlain<T>(IOutput output, string? query, IEnumerable<T> data)
    {
        // Use query if specified, to allow user to select the columns to display via "--query".
        // So first convert to respective JSON and then format this as a table.
        var element = GetAsJsonElement(query, data);

        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Unexpected value kind {element.ValueKind}.");
        }

        if (element.GetArrayLength() == 0)
        {
            return;
        }

        bool first = true;
        int maxLen = 0;
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Unexpected value kind {item.ValueKind}.");
            }

            // Ignore nested objects and arrays
            var properties = item.EnumerateObject().Where(v => v.Value.IsScalar() && !v.NameEquals("$type"));

            if (first)
            {
                maxLen = properties.Max(p => p.Name.Length);
            }
            else
            {
                output.WriteLine("----------------------------------------------------------");
            }

            foreach (var property in properties)
            {
                output.WriteLine($"{property.Name.PadRight(maxLen)}: {property.Value.GetValueApproximation()}");
            }

            first = false;
        }
    }
}
