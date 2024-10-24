using System;
using System.Diagnostics;
using System.IO;

namespace LockCheck.Linux;

[DebuggerDisplay("{HasError} {ProcessId} {ExecutableFullPath}")]
internal class ProcInfo : IHasErrorState
{
#if DEBUG
#pragma warning disable IDE0052
    private string? _errorStack;
    private Exception? _errorCause;
    private int _errorCode;
#pragma warning restore IDE0052
#endif

    public int ProcessId { get; private set; }
    public int SessionId { get; private set; }
    public string? CommandLine { get; private set; }
    public string? CurrentDirectory { get; private set; }
    public string? ExecutableFullPath { get; private set; }
    public string? Owner { get; private set; }
    public DateTime StartTime { get; private set; }
    public bool HasError { get; private set; }

    public void SetError(Exception? ex = null, int errorCode = 0)
    {
        if (!HasError)
        {
            HasError = true;
#if DEBUG
            if (Debugger.IsAttached)
            {
                // Support manual inspection at a later point
                _errorStack = Environment.StackTrace;
                _errorCause = ex;
                _errorCode = errorCode;
            }
#endif
        }
    }

    public ProcInfo(int processId)
    {
        ProcessId = processId;

        // Note: this is up front check. The process could vanish at any time later
        // while we attempt to get its properties.
        if (!ProcFileSystem.Exists(processId))
        {
            SetError();
            return;
        }

        CommandLine = GetCommandLine(processId, this);
        CurrentDirectory = GetCurrentDirectory(processId, this);
        ExecutableFullPath = GetExecutablePath(processId, this);
        Owner = GetProcessOwner(processId);
        StartTime = GetStartTime(processId, this);
        SessionId = GetSessionId(processId, this);

        // Make sure that the current directory always ends with a slash. AFAICT that is never the case,
        // using DirectoryInfo and procfs. We do this for symmetry with the Windows code and also because
        // it makes "starts-with" checks easier.
        if (!string.IsNullOrEmpty(CurrentDirectory) && CurrentDirectory[CurrentDirectory.Length - 1] != '\\')
        {
            CurrentDirectory += "/";
        }
    }

    private static string? GetProcessOwner(int pid)
    {
        try
        {
            return ProcFileSystem.GetProcessOwner(pid);
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
            // Don't set error state, just like the Windows version does not in this case.
            // The "Owner" field is not vital.
        }

        return null;
    }

    private static string? GetCommandLine(int pid, ProcInfo he)
    {
        try
        {
            string[]? args = ProcFileSystem.GetProcessCommandLineArgs(pid);

            if (args == null)
            {
                he.SetError();
                return null;
            }

            return string.Join(" ", args);
        }
        catch (UnauthorizedAccessException ex)
        {
            he.SetError(ex);
            return null;
        }
        catch (IOException ex)
        {
            he.SetError(ex);
            return null;
        }
    }

    private static string? GetCurrentDirectory(int pid, ProcInfo he)
    {
        try
        {
            string? cwd = ProcFileSystem.GetProcessCurrentDirectory(pid);

            if (cwd == null)
            {
                he.SetError();
            }

            return cwd;
        }
        catch (UnauthorizedAccessException ex)
        {
            he.SetError(ex);
            return null;
        }
        catch (IOException ex)
        {
            he.SetError(ex);
            return null;
        }
    }

    private static string? GetExecutablePath(int pid, ProcInfo he)
    {
        try
        {
            string? name = ProcFileSystem.GetProcessExecutablePath(pid);

            if (name == null)
            {
                name = ProcFileSystem.GetProcessExecutablePathFromCmdLine(pid);
            }

            if (name == null)
            {
                he.SetError();
            }

            return name;
        }
        catch (UnauthorizedAccessException ex)
        {
            he.SetError(ex);
            return null;
        }
        catch (IOException ex)
        {
            he.SetError(ex);
            return null;
        }
    }

    private static int GetSessionId(int pid, ProcInfo he)
    {
        try
        {
            int sessionId = ProcFileSystem.GetProcessSessionId(pid);

            if (sessionId == -1)
            {
                he.SetError();
            }

            return sessionId;
        }
        catch (UnauthorizedAccessException ex)
        {
            he.SetError(ex);
            return -1;
        }
        catch (IOException ex)
        {
            he.SetError(ex);
            return -1;
        }
    }

    private static unsafe DateTime GetStartTime(int pid, ProcInfo he)
    {
        try
        {
            var startTime = ProcFileSystem.GetProcessStartTime(pid);

            if (startTime == default)
            {
                he.SetError();
            }

            return startTime;
        }
        catch (UnauthorizedAccessException ex)
        {
            he.SetError(ex);
            return default;
        }
        catch (IOException ex)
        {
            he.SetError(ex);
            return default;
        }
    }
}
