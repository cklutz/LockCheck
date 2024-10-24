using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LockCheck.Tests.Tooling;
using Microsoft.Win32.SafeHandles;

namespace LockCheck.Tests.Windows;

public partial class Win32JobObject : SafeHandleZeroOrMinusOneIsInvalid, IJobObject
{
    private readonly IntPtr _jobHandle;

    public Win32JobObject(string? name)
        : base(ownsHandle: true)
    {
        _jobHandle = CreateJobObject(IntPtr.Zero, name);

        if (_jobHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        SetJobLimits();
    }

    protected override bool ReleaseHandle() => CloseHandle(_jobHandle);

    private void SetJobLimits()
    {
        var basicLimit = new JOBOBJECT_BASIC_LIMIT_INFORMATION { LimitFlags = JOB_OBJECT_LIMIT.KILL_ON_JOB_CLOSE };
        var extendedLimit = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION { BasicLimitInformation = basicLimit };
        int extendedLimitSize = Marshal.SizeOf(extendedLimit);
        IntPtr extendedLimitPtr = IntPtr.Zero;
        try
        {
            extendedLimitPtr = Marshal.AllocHGlobal(extendedLimitSize);
            Marshal.StructureToPtr(extendedLimit, extendedLimitPtr, false);

            if (!SetInformationJobObject(_jobHandle, JOB_OBJECT_INFO_CLASS.ExtendedLimitInformation, extendedLimitPtr, (uint)extendedLimitSize))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            if (extendedLimitPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(extendedLimitPtr);
            }
        }
    }

    public void AttachProcess(Process process)
    {
        if (process == null)
            throw new ArgumentNullException(nameof(process));

        if (!AssignProcessToJobObject(_jobHandle, process.Handle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    // Native declarations current live here, not in LockCheck/Windows/NativeMethods.cs, because all of the following
    // is only needed in this class. Which, in turn, is only needed here in LockCheck.Tests.dll.

#if NET
    [LibraryImport("kernel32.dll", SetLastError = true, EntryPoint = "CreateJobObjectW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(IntPtr hJob, JOB_OBJECT_INFO_CLASS JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr hObject);
#else
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr hJob, JOB_OBJECT_INFO_CLASS JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
#endif

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public IntPtr ProcessMemoryLimit;
        public IntPtr JobMemoryLimit;
        public IntPtr PeakProcessMemoryUsed;
        public IntPtr PeakJobMemoryUsed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public UInt64 ReadOperationCount;
        public UInt64 WriteOperationCount;
        public UInt64 OtherOperationCount;
        public UInt64 ReadTransferCount;
        public UInt64 WriteTransferCount;
        public UInt64 OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public Int64 PerProcessUserTimeLimit;
        public Int64 PerJobUserTimeLimit;
        public JOB_OBJECT_LIMIT LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public UInt32 ActiveProcessLimit;
        public IntPtr Affinity;
        public UInt32 PriorityClass;
        public UInt32 SchedulingClass;
    }

    [Flags]
    private enum JOB_OBJECT_LIMIT : uint
    {
        KILL_ON_JOB_CLOSE = 0x2000
    }

    private enum JOB_OBJECT_INFO_CLASS
    {
        ExtendedLimitInformation = 9
    }
}
