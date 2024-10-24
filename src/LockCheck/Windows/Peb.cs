using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using static LockCheck.Windows.NativeMethods;

namespace LockCheck.Windows;

[DebuggerDisplay("{HasError} {ProcessId} {ExecutableFullPath}")]
internal class Peb : IHasErrorState
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
    public string? WindowTitle { get; private set; }
    public string? ExecutableFullPath { get; private set; }
    public string? DesktopInfo { get; private set; }
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

    internal Peb(SYSTEM_PROCESS_INFORMATION pi)
    {
        // Convert as many members as possible, without actually needing to open the process handle.
        // Also, ProcessId/StartTime serve as identity. So it is "useful" to have them.
        ProcessId = pi.UniqueProcessId.ToInt32();
        StartTime = DateTime.FromFileTime(pi.CreateTime);

        using var process = OpenProcessRead(ProcessId);

        if (!SUCCEEDED(!process.IsInvalid, this))
        {
            return;
        }

        // Need to check if either the current process, or the target process is 32bit or 64bit.
        // Additionally, if it is a 32bit process on a 64bit OS (WOW64).

        bool os64 = Environment.Is64BitOperatingSystem;
        bool self64 = Environment.Is64BitProcess;
        bool target64 = false;

        if (os64)
        {
            if (!SUCCEEDED(IsWow64Process(process, out bool isWow64Target), this))
            {
                return;
            }

            target64 = !isWow64Target;
        }

        var offsets = PebOffsets.Get(target64);

        if (os64)
        {
            if (!target64)
            {
                // os: 64bit, self: any, target: 32bit
                InitTarget32SelfAny(process, offsets, this);
            }
            else if (!self64)
            {
                // os: 64bit, self: 32bit, target: 64bit
                InitTarget64Self32(process, offsets, this);
            }
            else
            {
                // os: 64bit, self: 64bit, target: 64bit
                InitTargetAnySelfAny(process, offsets, this);
            }
        }
        else
        {
            // os: 32bit, self: 32bit, target: 32bit
            InitTargetAnySelfAny(process, offsets, this);
        }

        // Make sure that the current directory always ends with a backslash. AFAICT that is always the case,
        // so this should really be a noop, but we need to make sure to ensure hassle free comparison later.
        if (!string.IsNullOrEmpty(CurrentDirectory) && CurrentDirectory[CurrentDirectory.Length - 1] != '\\')
        {
            CurrentDirectory += "\\";
        }

        // Owner is not really part of the native PEB, but since we have the process handle
        // here anyway, and going to need this value later on, we get it here as well.
        Owner = GetProcessOwner(process);
    }

    private static void InitTargetAnySelfAny(SafeProcessHandle handle, PebOffsets offsets, Peb peb)
    {
        var pbi = new PROCESS_BASIC_INFORMATION();
        if (SUCCEEDED(NtQueryInformationProcess(handle, PROCESS_INFORMATION_CLASS.ProcessBasicInformation, ref pbi, Marshal.SizeOf(pbi), IntPtr.Zero), peb))
        {
            var pp = new IntPtr();
            if (SUCCEEDED(ReadProcessMemory(handle, new IntPtr(pbi.PebBaseAddress.ToInt64() + offsets.ProcessParametersOffset), ref pp, new IntPtr(Marshal.SizeOf(pp)), IntPtr.Zero), peb))
            {
                peb.CommandLine = GetString(handle, pp, offsets.CommandLineOffset, peb);
                peb.CurrentDirectory = GetString(handle, pp, offsets.CurrentDirectoryOffset, peb);
                peb.ExecutableFullPath = GetString(handle, pp, offsets.ImagePathNameOffset, peb);
                peb.WindowTitle = GetString(handle, pp, offsets.WindowTitleOffset, peb);
                peb.DesktopInfo = GetString(handle, pp, offsets.DesktopInfoOffset, peb);
            }

            peb.SessionId = GetInt32(handle, pbi.PebBaseAddress, offsets.SessionIdOffset, peb);
        }
    }


    private static void InitTarget64Self32(SafeProcessHandle handle, PebOffsets offsets, Peb peb)
    {
        var pbi = new PROCESS_BASIC_INFORMATION_WOW64();
        if (SUCCEEDED(NtWow64QueryInformationProcess64(handle, PROCESS_INFORMATION_CLASS.ProcessBasicInformation, ref pbi, Marshal.SizeOf(pbi), IntPtr.Zero), peb))
        {
            long pp = 0;
            if (SUCCEEDED(NtWow64ReadVirtualMemory64(handle, pbi.PebBaseAddress + offsets.ProcessParametersOffset, ref pp, Marshal.SizeOf(pp), IntPtr.Zero), peb))
            {
                peb.CommandLine = GetStringTarget64(handle, pp, offsets.CommandLineOffset, peb);
                peb.CurrentDirectory = GetStringTarget64(handle, pp, offsets.CurrentDirectoryOffset, peb);
                peb.ExecutableFullPath = GetStringTarget64(handle, pp, offsets.ImagePathNameOffset, peb);
                peb.WindowTitle = GetStringTarget64(handle, pp, offsets.WindowTitleOffset, peb);
                peb.DesktopInfo = GetStringTarget64(handle, pp, offsets.DesktopInfoOffset, peb);
            }

            peb.SessionId = GetInt32Target64(handle, pbi.PebBaseAddress, offsets.SessionIdOffset, peb);
        }
    }

    private static void InitTarget32SelfAny(SafeProcessHandle handle, PebOffsets offsets, Peb peb)
    {
        var pbi = new PROCESS_BASIC_INFORMATION();
        if (SUCCEEDED(NtQueryInformationProcess(handle, PROCESS_INFORMATION_CLASS.ProcessBasicInformation, ref pbi, Marshal.SizeOf(pbi), IntPtr.Zero), peb))
        {
            // A 32bit process on a 64bit OS has a separate PEB structure.
            var peb32 = new IntPtr();
            if (SUCCEEDED(NtQueryInformationProcessWow64(handle, PROCESS_INFORMATION_CLASS.ProcessWow64Information, ref peb32, IntPtr.Size, IntPtr.Zero), peb))
            {
                var pp = new IntPtr();
                if (SUCCEEDED(ReadProcessMemory(handle, new IntPtr(peb32.ToInt64() + offsets.ProcessParametersOffset), ref pp, new IntPtr(Marshal.SizeOf(pp)), IntPtr.Zero), peb))
                {
                    peb.CommandLine = GetStringTarget32(handle, pp, offsets.CommandLineOffset, peb);
                    peb.CurrentDirectory = GetStringTarget32(handle, pp, offsets.CurrentDirectoryOffset, peb);
                    peb.ExecutableFullPath = GetStringTarget32(handle, pp, offsets.ImagePathNameOffset, peb);
                    peb.WindowTitle = GetStringTarget32(handle, pp, offsets.WindowTitleOffset, peb);
                    peb.DesktopInfo = GetStringTarget32(handle, pp, offsets.DesktopInfoOffset, peb);
                }

                peb.SessionId = GetInt32Target32(handle, new IntPtr(peb32.ToInt64()), offsets.SessionIdOffset, peb);
            }
        }
    }

    private static int GetInt32Target32(SafeProcessHandle handle, IntPtr pp, int offset, Peb he)
    {
        var ptr = IntPtr.Zero;
        if (SUCCEEDED(ReadProcessMemory(handle, pp + offset, ref ptr, new IntPtr(sizeof(int)), IntPtr.Zero), he))
        {
            return ptr.ToInt32();
        }

        return default;
    }

    private static string? GetStringTarget32(SafeProcessHandle handle, IntPtr pp, int offset, Peb he)
    {
        var us = new UNICODE_STRING_32();
        if (SUCCEEDED(ReadProcessMemory(handle, pp + offset, ref us, new IntPtr(Marshal.SizeOf(us)), IntPtr.Zero), he))
        {
            if (us.Buffer != 0)
            {
                if (us.Length == 0)
                {
                    return string.Empty;
                }

                string lpBuffer = us.GetEmptyBuffer();
                if (SUCCEEDED(ReadProcessMemory(handle, new IntPtr(us.Buffer), lpBuffer, new IntPtr(us.Length), IntPtr.Zero), he))
                {
                    return lpBuffer;
                }
            }
        }

        return null;
    }

    private static int GetInt32Target64(SafeProcessHandle handle, long pp, int offset, Peb he)
    {
        var ptr = IntPtr.Zero;
        uint buf = 0;
        if (SUCCEEDED(NtWow64ReadVirtualMemory64(handle, pp + offset, ref buf, sizeof(uint), IntPtr.Zero), he))
        {
            ptr = new IntPtr(buf);
            return ptr.ToInt32();
        }

        return default;
    }

    private static string? GetStringTarget64(SafeProcessHandle handle, long pp, int offset, Peb he)
    {
        var us = new UNICODE_STRING_WOW64();
        if (SUCCEEDED(NtWow64ReadVirtualMemory64(handle, pp + offset, ref us, Marshal.SizeOf(us), IntPtr.Zero), he))
        {
            if (us.Buffer != 0)
            {
                if (us.Length == 0)
                {
                    return string.Empty;
                }

                string lpBuffer = us.GetEmptyBuffer();
                if (SUCCEEDED(NtWow64ReadVirtualMemory64(handle, us.Buffer, lpBuffer, us.Length, IntPtr.Zero), he))
                {
                    return lpBuffer;
                }
            }
        }

        return null;
    }

    private static int GetInt32(SafeProcessHandle handle, IntPtr pp, int offset, Peb he)
    {
        var ptr = IntPtr.Zero;
        if (SUCCEEDED(ReadProcessMemory(handle, pp + offset, ref ptr, new IntPtr(IntPtr.Size), IntPtr.Zero), he))
        {
            return ptr.ToInt32();
        }

        return 0;
    }

    private static string? GetString(SafeProcessHandle handle, IntPtr pp, int offset, Peb he)
    {
        var us = new UNICODE_STRING();
        if (SUCCEEDED(ReadProcessMemory(handle, pp + offset, ref us, new IntPtr(Marshal.SizeOf(us)), IntPtr.Zero), he))
        {
            if (us.Buffer != IntPtr.Zero)
            {
                if (us.Length == 0)
                {
                    return string.Empty;
                }

                string lpBuffer = us.GetEmptyBuffer();
                if (SUCCEEDED(ReadProcessMemory(handle, us.Buffer, lpBuffer, new IntPtr(us.Length), IntPtr.Zero), he))
                {
                    return lpBuffer;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if <paramref name="status"/> is success, otherwise sets <c><paramref name="he"/>.SetError(errorCode: <paramref name="status"/>)</c>.
    /// </summary>
    private static bool SUCCEEDED(uint status, Peb he, [CallerMemberName] string? callerName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (status != STATUS_SUCCESS)
        {
            he.SetError(errorCode: (int)status);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if <paramref name="status"/> is success, otherwise sets <c><paramref name="he"/>.SetError(errorCode: <paramref name="status"/>)</c>.
    /// </summary>
    private static bool SUCCEEDED(int status, Peb he, [CallerMemberName] string? callerName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (status != STATUS_SUCCESS)
        {
            he.SetError(errorCode: status);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if <paramref name="status"/> is success, otherwise sets <c><paramref name="he"/>.SetError(errorCode: <see cref="Marshal.GetLastWin32Error()"/>)</c>.
    /// </summary>
    private static bool SUCCEEDED(bool result, Peb he, [CallerMemberName] string? callerName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (!result)
        {
            he.SetError(errorCode: Marshal.GetLastWin32Error());
            return false;
        }

        return true;
    }
}
