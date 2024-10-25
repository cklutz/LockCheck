using System.CommandLine;
using System.CommandLine.IO;

namespace LockCheckTool;

internal class ConsoleOutput : IOutput
{
    private readonly IStandardStreamWriter _out;
    public ConsoleOutput(IStandardStreamWriter @out) => _out = @out;
    public void Dispose() { }
    public void Write(char c) => _out.Write(c.ToString());
    public void Write(string? value) => _out.Write(value);
    public void WriteLine(string? value) => _out.WriteLine(value ?? "");
    public void WriteLine() => _out.WriteLine();
}
