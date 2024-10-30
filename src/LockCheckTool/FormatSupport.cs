using System;
using System.Buffers;
using System.Globalization;
using System.Text.Json;

namespace LockCheckTool;

internal static class FormatSupport
{
    public static void FormatAsRowsWithDelimiter(JsonElement element, char delimiter, IOutput tw, bool includeHeaders)
    {
        FormatAsRowsWithDelimiterCore(element, delimiter, tw, includeHeaders, true);
    }

#if NET
    private static SearchValues<char> s_tsvSearchValues = SearchValues.Create(['"', '\n', '\r', '\t']);
    private static SearchValues<char> s_csvSearchValues = SearchValues.Create(['"', '\n', '\r', ',']);
#endif

    private static void FormatAsRowsWithDelimiterCore(JsonElement element, char delimiter, IOutput tw, bool includeHeaders, bool top)
    {
        if (element.IsScalar())
        {
            WriteQuoted(tw, delimiter, element.GetValueApproximation()?.ToString());
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            if (includeHeaders)
            {
                bool firstHeader = true;
                foreach (var property in element.EnumerateObject())
                {
                    if (!firstHeader)
                    {
                        tw.Write(delimiter);
                    }
                    WriteQuoted(tw, delimiter, property.Name);
                    firstHeader = false;
                }
                tw.WriteLine();
            }

            bool first = true;
            foreach (var property in element.EnumerateObject())
            {
                if (!first)
                {
                    tw.Write(delimiter);
                }
                FormatAsRowsWithDelimiterCore(property.Value, delimiter, tw, false, false);
                first = false;
            }

            if (top)
            {
                tw.WriteLine();
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int length = element.GetArrayLength();
            for (int i = 0; i < length; i++)
            {
                FormatAsRowsWithDelimiterCore(element[i], delimiter, tw, (i == 0) && includeHeaders, false);
                tw.WriteLine();
            }
        }

        static void WriteQuoted(IOutput tw, char delimiter, string? value)
        {
            if (value != null)
            {
                int needQuotes =
#if NET
                    delimiter == ',' ? value.AsSpan().IndexOfAny(s_csvSearchValues)
                    : delimiter == '\t' ? value.AsSpan().IndexOfAny(s_tsvSearchValues)
                    :
#endif
                    value.IndexOfAny(['"', '\n', '\r', delimiter]);

                if (needQuotes >= 0)
                {
                    tw.Write("\"");
                    tw.Write(value.Replace("\"", "\"\""));
                    tw.Write("\"");
                }
                else
                {
                    tw.Write(value);
                }
            }
        }

    }

    public static bool IsScalar(this JsonElement element)
    {
        var kind = element.ValueKind;
        return kind == JsonValueKind.False ||
               kind == JsonValueKind.True ||
               kind == JsonValueKind.Null ||
               kind == JsonValueKind.String ||
               kind == JsonValueKind.Number;
    }

    public static object? GetValueApproximation(this JsonElement element, CultureInfo? cultureInfo = null)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                if (element.TryGetGuid(out var guid))
                {
                    return guid;
                }
                if (element.TryGetDateTime(out var dt))
                {
                    return dt;
                }
                if (element.TryGetDateTimeOffset(out var dto))
                {
                    return dto;
                }
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out long i64))
                {
                    return i64;
                }
                if (element.TryGetUInt64(out ulong u64))
                {
                    return u64;
                }
                if (element.TryGetDouble(out double dbl))
                {
                    return dbl;
                }
                if (element.TryGetDecimal(out decimal dec))
                {
                    return dec;
                }
                break;
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
                return null;
        }

        // Shouldn't get here. This returns the literal input JSON.
        return element.ToString();
    }
}
