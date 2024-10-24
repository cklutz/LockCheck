using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace LockCheck;

/// <summary>
/// Provides information about a process that is holding a lock on a file.
/// </summary>
[DebuggerDisplay("{ProcessId} {StartTime} {ExecutableName}")]
public abstract class ProcessInfo
{
    protected ProcessInfo(int processId, DateTime startTime)
    {
        ProcessId = processId;
        StartTime = startTime;
    }

    /// <summary>
    /// The process identifier of the process holding a lock on a file.
    /// </summary>
    public int ProcessId { get; }

    /// <summary>
    /// The start time (local) of the process holding a lock o a file.
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    /// The executable name of the process holding a lock.
    /// </summary>
    public string? ExecutableName { get; protected set; }

    /// <summary>
    /// The descriptive application name, if available. Otherwise
    /// the same as the executable name or another informative
    /// string.
    /// </summary>
    public string? ApplicationName { get; protected set; }

    /// <summary>
    /// The owner of the process.
    /// </summary>
    public string? Owner { get; protected set; }

    /// <summary>
    /// The full path to the process' executable, if available.
    /// </summary>
    public string? ExecutableFullPath { get; protected set; }

    /// <summary>
    /// The platform specific session ID of the process.
    /// </summary>
    /// <value>
    /// On Windows, the Terminal Services ID. On Linux
    /// the process' session ID.
    /// </value>
    public int SessionId { get; protected set; }

    /// <summary>
    /// A platform specific string that specifies the type of lock, if available.
    /// </summary>
    public string? LockType { get; protected set; }

    /// <summary>
    /// A platform specific string that specifies the mode of the lock, if available.
    /// </summary>
    public string? LockMode { get; protected set; }

    /// <summary>
    /// A platform specific string that specifies the access lock requested, if available.
    /// </summary>
    public string? LockAccess { get; protected set; }

    public override int GetHashCode()
    {
#if NET
        return HashCode.Combine(ProcessId, StartTime);
#else
        int h1 = ProcessId.GetHashCode();
        int h2 = StartTime.GetHashCode();
        return ((h1 << 5) + h1) ^ h2;
#endif
    }

    public override bool Equals(object? obj)
    {
        var other = obj as ProcessInfo;
        if (other != null)
        {
            return other.ProcessId == ProcessId && other.StartTime == StartTime;
        }
        return false;
    }

    public override string ToString() => ProcessId + "@" + StartTime.ToString("O");

    public string ToString(string? format)
    {
        string baseFormat = ToString();

        if (format != null)
        {
            if (format == "F")
            {
                return $"{baseFormat}/{ApplicationName}";
            }
        }

        return baseFormat;
    }

    public static void Format(StringBuilder sb, IEnumerable<ProcessInfo> lockers, IEnumerable<string> fileNames, int? maxProcesses = null, string? ownerOverwrite = null)
    {
        if (fileNames == null)
            throw new ArgumentNullException(nameof(fileNames));

        if (lockers == null || !lockers.Any())
            return;

        int count = lockers.Count();
        int max = maxProcesses == -1 || !maxProcesses.HasValue ? int.MaxValue : maxProcesses.Value;

        sb.AppendFormat("File {0} locked by: ", string.Join(", ", fileNames));
        foreach (ProcessInfo locker in lockers.Take(max))
        {
            sb.AppendLine($"[{locker.ApplicationName}, pid={locker.ProcessId}, owner={ownerOverwrite ?? locker.Owner}, started={locker.StartTime:yyyy-MM-dd HH:mm:ss.fff}]");
        }

        if (count > max)
        {
            sb.AppendLine($"[{count - max} more processes...]");
        }
    }
}
