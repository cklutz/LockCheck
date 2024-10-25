using System;

namespace LockCheckTool;

internal interface IOutput : IDisposable
{
    void Write(char c);
    void Write(string? text);
    void WriteLine(string? text);
    void WriteLine();
}
