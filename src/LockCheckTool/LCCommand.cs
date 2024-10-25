using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.CompilerServices;

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

}
