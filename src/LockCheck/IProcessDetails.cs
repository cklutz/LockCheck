using System;

namespace LockCheck;

public interface IProcessDetails
{
    int ProcessId { get; }
    DateTime StartTime { get; }
    int? ParentProcessId { get; }
    int SessionId { get; }
    string? ProcessName { get; }
    string? CommandLine { get; }
    string? CurrentDirectory { get; }
    string? ExecutableFullPath { get; }
    string? Owner { get; }
    bool HasError { get; }
    bool? IsCritical { get; }
}

public interface IWin32ProcessDetails : IProcessDetails
{
    bool IsPseudoProcess { get; }
    ulong? ProcessSequenceNumber { get; }
    ulong? ProcessStartKey { get;  }
}

public interface ILinuxProcessDetails : IProcessDetails
{
    bool? IsKernelThread { get; }
}
