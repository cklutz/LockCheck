using System.CommandLine;
using System.CommandLine.IO;

namespace LockCheckTool;

internal class ConsoleOutput : IOutput
{
    private readonly IStandardStreamWriter _out;
    private readonly bool _noColor;

    public ConsoleOutput(IStandardStreamWriter @out, bool noColor)
    {
        _out = @out;
        _noColor = noColor;
    }

    public void Dispose() { }
    public void Write(char c) => _out.Write(c.ToString());
    public void Write(string? value) => _out.Write(value);
    public void WriteLine(string? value) => _out.WriteLine(value ?? "");
    public void WriteLine() => _out.WriteLine();
    public bool NoColor => _noColor;
}
