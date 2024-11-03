using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

#pragma warning disable IDE1006 // Naming Styles - off here, because we want to use native names

namespace LockCheck.Windows;

internal static partial class NativeMethods
{
    private const string NtDll = "ntdll.dll";
    private const string RestartManagerDll = "rstrtmgr.dll";
    private const string AdvApi32Dll = "advapi32.dll";
    private const string KernelDll = "kernel32.dll";

    internal const int ERROR_SEM_TIMEOUT = 121;
    internal const int ERROR_INSUFFICIENT_BUFFER = 122;
    internal const int ERROR_BAD_ARGUMENTS = 160;
    internal const int ERROR_MAX_SESSIONS_REACHED = 353;
    internal const int ERROR_WRITE_FAULT = 29;
    internal const int ERROR_OUTOFMEMORY = 14;
    internal const int ERROR_MORE_DATA = 234;
    internal const int ERROR_ACCESS_DENIED = 5;
    internal const int ERROR_INVALID_HANDLE = 6;
    internal const int ERROR_GEN_FAILURE = 31;
    internal const int ERROR_SHARING_VIOLATION = 32;
    internal const int ERROR_LOCK_VIOLATION = 33;
    internal const int ERROR_CANCELLED = 1223;

    internal const uint STATUS_SUCCESS = 0;
    internal const uint STATUS_INFO_LENGTH_MISMATCH = 0xC0000004;

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    internal struct IO_STATUS_BLOCK
    {
        public uint Status;
        public IntPtr Information;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FILE_PROCESS_IDS_USING_FILE_INFORMATION
    {
        public uint NumberOfProcessIdsInList;
        public IntPtr ProcessIdList;
    }

    internal enum FILE_INFORMATION_CLASS
    {
        FileProcessIdsUsingFileInformation = 47
    }

    internal enum PROCESS_INFORMATION_CLASS
    {
        ProcessBasicInformation = 0,
        ProcessWow64Information = 26,
        ProcessSequenceNumber = 92,
    }

    internal enum SYSTEM_INFORMATION_CLASS
    {
        SystemProcessInformation = 5,
        SystemExtendedProcessInformation = 0x39,
        SystemFullProcessInformation = 0x94
    }

#if NET
    [LibraryImport(NtDll)]
    internal static partial uint NtQueryInformationFile(SafeFileHandle fileHandle, ref IO_STATUS_BLOCK IoStatusBlock,
        IntPtr pInfoBlock, uint length, FILE_INFORMATION_CLASS fileInformation);
#else
    [DllImport(NtDll)]
    internal static extern uint NtQueryInformationFile(SafeFileHandle fileHandle, ref IO_STATUS_BLOCK IoStatusBlock,
        IntPtr pInfoBlock, uint length, FILE_INFORMATION_CLASS fileInformation);
#endif

#if NET
    [LibraryImport(NtDll)]
    internal static partial uint NtQueryInformationProcess(SafeProcessHandle hProcess,
        PROCESS_INFORMATION_CLASS processInformationClass,
        ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, IntPtr returnLength);
#else
    [DllImport(NtDll)]
    internal static extern uint NtQueryInformationProcess(SafeProcessHandle hProcess,
        PROCESS_INFORMATION_CLASS processInformationClass,
        ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, IntPtr returnLength);
#endif

#if NET
    [LibraryImport(NtDll)]
    internal static partial uint NtQueryInformationProcess(SafeProcessHandle hProcess,
        PROCESS_INFORMATION_CLASS processInformationClass,
        ref IntPtr processInformation, int processInformationLength, IntPtr returnLength);
#else
    [DllImport(NtDll)]
    internal static extern uint NtQueryInformationProcess(SafeProcessHandle hProcess,
        PROCESS_INFORMATION_CLASS processInformationClass,
        ref IntPtr processInformation, int processInformationLength, IntPtr returnLength);
#endif


#if NET
    [LibraryImport(NtDll)]
    internal static partial int NtWow64QueryInformationProcess64(SafeProcessHandle hProcess,
        PROCESS_INFORMATION_CLASS processInformationClass,
        ref PROCESS_BASIC_INFORMATION_WOW64 processInformation, int processInformationLength, IntPtr returnLength);
#else
    [DllImport(NtDll)]
    internal static extern int NtWow64QueryInformationProcess64(SafeProcessHandle hProcess,
        PROCESS_INFORMATION_CLASS processInformationClass,
        ref PROCESS_BASIC_INFORMATION_WOW64 processInformation, int processInformationLength, IntPtr returnLength);
#endif

#if NET
    [LibraryImport(NtDll)]
    internal static partial int NtWow64QueryInformationProcess64(SafeProcessHandle hProcess,
        PROCESS_INFORMATION_CLASS processInformationClass,
        ref IntPtr processInformation, int processInformationLength, IntPtr returnLength);
#else
    [DllImport(NtDll)]
    internal static extern int NtWow64QueryInformationProcess64(SafeProcessHandle hProcess,
        PROCESS_INFORMATION_CLASS processInformationClass,
        ref IntPtr processInformation, int processInformationLength, IntPtr returnLength);
#endif

#if NET
    [LibraryImport(NtDll, EntryPoint = "NtQueryInformationProcess")]
    internal static partial int NtQueryInformationProcessWow64(SafeProcessHandle hProcess, PROCESS_INFORMATION_CLASS processInformationClass,
        ref IntPtr processInformation, int processInformationLength, IntPtr returnLength);
#else
    [DllImport(NtDll, EntryPoint = "NtQueryInformationProcess")]
    internal static extern int NtQueryInformationProcessWow64(SafeProcessHandle hProcess, PROCESS_INFORMATION_CLASS processInformationClass,
        ref IntPtr processInformation, int processInformationLength, IntPtr returnLength);
#endif

#if NET
    [LibraryImport(NtDll)]
    internal static unsafe partial uint NtQuerySystemInformation(SYSTEM_INFORMATION_CLASS systemInformationClass, void* dataPtr, uint size, uint* returnedSize);
#else
    [DllImport(NtDll)]
    internal static extern int NtQuerySystemInformation(SYSTEM_INFORMATION_CLASS systemInformationClass, IntPtr dataPtr, int size, out int returnedSize);
#endif

#if NET
    [LibraryImport(NtDll)]
    internal static partial int RtlNtStatusToDosError(uint status);
#else
    [DllImport(NtDll)]
    internal static extern int RtlNtStatusToDosError(uint status);
#endif


    [DllImport(RestartManagerDll, CharSet = CharSet.Unicode)]
    internal static extern int RmRegisterResources(uint pSessionHandle,
        uint nFiles,
        string[] rgsFilenames,
        uint nApplications,
        [In] RM_UNIQUE_PROCESS[]? rgApplications,
        uint nServices,
        string[]? rgsServiceNames);

    [DllImport(RestartManagerDll, CharSet = CharSet.Unicode)]
    internal static extern int RmStartSession(out uint pSessionHandle,
        int dwSessionFlags, StringBuilder strSessionKey);

    [DllImport(RestartManagerDll)]
    internal static extern int RmEndSession(uint pSessionHandle);

    [DllImport(RestartManagerDll, CharSet = CharSet.Unicode)]
    internal static extern int RmGetList(uint dwSessionHandle,
        out uint pnProcInfoNeeded,
        ref uint pnProcInfo,
        [In, Out] RM_PROCESS_INFO[]? rgAffectedApps,
        ref uint lpdwRebootReasons);

    [StructLayout(LayoutKind.Sequential)]
    internal struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RM_UNIQUE_PROCESS
    {
        public uint dwProcessId;
        public FILETIME ProcessStartTime;
    }

    internal const int RM_INVALID_SESSION = -1;
    internal const int RM_INVALID_PROCESS = -1;

    internal const int CCH_RM_MAX_APP_NAME = 255;
    internal const int CCH_RM_MAX_SVC_NAME = 63;

    internal static readonly int RM_SESSION_KEY_LEN = Guid.Empty.ToByteArray().Length; // 16-byte
    internal static readonly int CCH_RM_SESSION_KEY = RM_SESSION_KEY_LEN * 2;

    internal enum RM_APP_TYPE
    {
        RmUnknownApp = 0,
        RmMainWindow = 1,
        RmOtherWindow = 2,
        RmService = 3,
        RmExplorer = 4,
        RmConsole = 5,
        RmCritical = 1000
    }

    internal enum RM_APP_STATUS
    {
        RmStatusUnknown = 0x0,
        RmStatusRunning = 0x1,
        RmStatusStopped = 0x2,
        RmStatusStoppedOther = 0x4,
        RmStatusRestarted = 0x8,
        RmStatusErrorOnStop = 0x10,
        RmStatusErrorOnRestart = 0x20,
        RmStatusShutdownMasked = 0x40,
        RmStatusRestartMasked = 0x80
    }

    internal enum RM_REBOOT_REASON
    {
        RmRebootReasonNone = 0x0,
        RmRebootReasonPermissionDenied = 0x1,
        RmRebootReasonSessionMismatch = 0x2,
        RmRebootReasonCriticalProcess = 0x4,
        RmRebootReasonCriticalService = 0x8,
        RmRebootReasonDetectedSelf = 0x10
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
        public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
        public string strServiceShortName;
        public RM_APP_TYPE ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;

        public DateTime GetStartTime() => DateTime.FromFileTime((((long)Process.ProcessStartTime.dwHighDateTime) << 32) | Process.ProcessStartTime.dwLowDateTime);
    }

#if NET
    [LibraryImport(AdvApi32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool OpenProcessToken(SafeProcessHandle processHandle, int desiredAccess, out SafeAccessTokenHandle tokenHandle);
#else
    [DllImport(AdvApi32Dll, SetLastError = true)]
    internal static extern bool OpenProcessToken(SafeProcessHandle processHandle, int desiredAccess, out SafeAccessTokenHandle tokenHandle);
#endif

#if NET
    [LibraryImport(AdvApi32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetTokenInformation(SafeAccessTokenHandle hToken, TOKEN_INFORMATION_CLASS tokenInfoClass, IntPtr tokenInformation, int tokeInfoLength, ref int reqLength);
#else
    [DllImport(AdvApi32Dll, CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern bool GetTokenInformation(SafeAccessTokenHandle hToken, TOKEN_INFORMATION_CLASS tokenInfoClass, IntPtr tokenInformation, int tokeInfoLength, ref int reqLength);
#endif

    internal const int PROCESS_TERMINATE = 0x0001;
    internal const int PROCESS_CREATE_THREAD = 0x0002;
    internal const int PROCESS_DUP_HANDLE = 0x0040;
    internal const int PROCESS_CREATE_PROCESS = 0x0080;
    internal const int PROCESS_SET_QUOTA = 0x0100;
    internal const int PROCESS_SET_INFORMATION = 0x0200;
    internal const int PROCESS_SUSPEND_RESUME = 0x0800;
    internal const int PROCESS_QUERY_INFORMATION = 0x400;
    internal const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    internal const int PROCESS_VM_OPERATION = 0x08;
    internal const int PROCESS_VM_READ = 0x10;
    internal const int PROCESS_VM_WRITE = 0x20;

#if NET
    [LibraryImport(KernelDll, SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);
#else
    [DllImport(KernelDll, SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);
#endif

    internal static SafeProcessHandle OpenProcessLimited(int pid) => OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
    internal static SafeProcessHandle OpenProcessRead(int pid) => OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, pid);

    internal static bool IsCurrentProcessWow64Process { get; } = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess;

#if NET
    [LibraryImport(KernelDll, SetLastError = true)]
    internal static partial int GetProcessId(SafeProcessHandle handle);
#else
    [DllImport(KernelDll, SetLastError = true)]
    internal static extern int GetProcessId(SafeProcessHandle handle);
#endif

#if NET
    [LibraryImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsProcessCritical(SafeProcessHandle hProcess, [MarshalAs(UnmanagedType.Bool)] out bool critical);
#else
    [DllImport(KernelDll, SetLastError = true)]
    private static extern bool IsProcessCritical(SafeProcessHandle hProcess, out bool critical);
#endif

    private static readonly string[] s_criticalNames =
    {
        // List taken from taskmgr.exe "strings"
        "%windir%\\explorer.exe",
        "%windir%\\system32\\ntoskrnl.exe",
        "%windir%\\system32\\winlogon.exe",
        "%windir%\\system32\\wininit.exe",
        "%windir%\\system32\\csrss.exe",
        "%windir%\\system32\\lsass.exe",
        "%windir%\\system32\\smss.exe",
        "%windir%\\system32\\services.exe",
        "%windir%\\system32\\taskeng.exe",
        "%windir%\\system32\\taskhost.exe",
        "%windir%\\system32\\dwm.exe",
        "%windir%\\system32\\conhost.exe",
        "%windir%\\system32\\svchost.exe",
        "%windir%\\system32\\sihost.exe",
        "%windir%\\system32\\backgroundTaskHost.exe",
        "%windir%\\system32\\backgroundTransferHost.exe",
        "%windir%\\system32\\WerFault.exe",
        "%programfiles%\\Windows Defender\\msmpeng.exe",
        "%programfiles%\\Windows Defender\\nissrv.exe",
    };

    private static readonly Lazy<HashSet<string>> s_critical = new(() =>
    {
        var result = new HashSet<string>(s_criticalNames.Length, StringComparer.OrdinalIgnoreCase);

        foreach (string name in s_criticalNames)
        {
            if (IsCurrentProcessWow64Process)
            {
                // 32 bit process on 64 bit OS. Make sure we use 64 bit directories.
                // Note: we don't have to replace "%windir%\system32" with "%windir%\sysnative"
                // because the full path we compare with is ultimately retrieved by the QueryFullProcessImageName() Win32 API.
                // That in turn, seems to always return the "actual" path. So even when running as 32 bit app on a 64 bit Windows
                // (i.e. WOW64), it will return the true path.
                string nativeName = name.Replace("%programfiles%", "%programw6432%");
                result.Add(Environment.ExpandEnvironmentVariables(nativeName));
            }
            else
            {
                result.Add(Environment.ExpandEnvironmentVariables(name));
            }
        }

        return result;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    // The following lazy initializes whether ProcessSequenceNumber is available or not.
    // Doing it the following way saves us a Lazy<> instance's overhead at the cost of
    // potentially doing the logic multiple times if multiple threads make it inside the
    // "if (.. == 0)". 
    private static int s_supportsProcessSequenceNumber;
    internal static bool SupportsProcessSequenceNumber
    {
        get
        {
            if (s_supportsProcessSequenceNumber == 0)
            {
                // Not available when self is WOW64.
                // NtQuerySystemInformation() does not return the SYSTEM_PROCESS_INFORMATION_EXTENSION then it seems.
                // Also PROCESS_INFORMATION_CLASS.ProcessSequenceNumber is not available.
                if (!IsCurrentProcessWow64Process)
                {
                    // According to: https://learn.microsoft.com/en-us/windows/win32/api/evntrace/ns-evntrace-enable_trace_parameters
                    // "Supported on Windows 10, version 1507 and later. This is also supported on Windows 8.1 and Windows 7 with SP1 via a patch."
                    // We ignore versions 8.1 and 7. Version 1507 is build 10240.
                    var ver = Environment.OSVersion.Version;
                    s_supportsProcessSequenceNumber = ver.Major > 10 || (ver.Major == 10 && ver.Build >= 10240) ? 1 : 2;
                }
                else
                {
                    s_supportsProcessSequenceNumber = 2;
                }
            }

            return s_supportsProcessSequenceNumber == 1;
        }
    }


    internal static IEnumerable<string> GetKnownCriticalProcesses() => s_critical.Value;

    internal static bool? IsProcessCritical(SafeProcessHandle hProcess, IHasErrorState? errorState = null)
    {
        if (hProcess.IsInvalid)
        {
            errorState?.SetError();
            return null;
        }

        bool? result = IsProcessCriticalByHandle(hProcess, errorState);
        if (result != null)
        {
            return result;
        }

        return IsProcessCriticalByImagePath(hProcess, errorState);
    }

    // internal for unit test access
    internal static bool? IsProcessCriticalByHandle(SafeProcessHandle hProcess, IHasErrorState? errorState)
    {
        if (!IsProcessCritical(hProcess, out bool critical))
        {
            errorState?.SetError(errorCode: Marshal.GetLastWin32Error());
            return null;
        }

        return critical;
    }

    // internal for unit test access
    internal static bool? IsProcessCriticalByImagePath(SafeProcessHandle hProcess, IHasErrorState? errorState)
    {
        // Check hardcoded list
        string? imagePath = GetProcessImagePath(hProcess, throwOnError: false);
        if (imagePath == null)
        {
            errorState?.SetError(errorCode: Marshal.GetLastWin32Error());
            return null;
        }

        return s_critical.Value.Contains(imagePath);
    }

#if NET
    [LibraryImport(KernelDll, SetLastError = true, EntryPoint = "QueryFullProcessImageNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryFullProcessImageName(SafeProcessHandle hProcess, int dwFlags, char* lpExeName, ref int lpdwSize);
#else
    [DllImport(KernelDll, SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);
#endif

    private class DisableWow64FsRedirectionScope : IDisposable
    {
        private IntPtr _oldValue = IntPtr.Zero;
        private bool _shouldDispose;

        public DisableWow64FsRedirectionScope()
        {
            if (IsCurrentProcessWow64Process)
            {
                if (!Wow64DisableWow64FsRedirection(ref _oldValue))
                {
                    // Shouldn't happen, but since we haven't actually changed the thread's state,
                    // an exception is sufficient.
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                _shouldDispose = true;
            }
        }

        public void Dispose()
        {
            if (_shouldDispose)
            {
                if (!Wow64RevertWow64FsRedirection(_oldValue))
                {
                    // This is catastrophic; any FS related function could not return unexpected values.
                    // It shouldn't *really* happen either, these APIs really just set a TLS slot for the current thread.
                    int code = Marshal.GetLastWin32Error();
                    Environment.FailFast($"Failed to restore WOW64 FS redirection: 0x{code:X8}");
                }

                _shouldDispose = false;
            }
        }
    }

    internal static unsafe string? GetProcessImagePath(SafeProcessHandle hProcess, bool throwOnError = false)
    {
        // It *seems* as if QueryFullProcessImageName() always returns the "true" path, so no redirections
        // applied (e.g. for 64 bit C:\Windows\System32\notepad.exe it really does return that path and
        // not C:\Windows\sysnative\notepad.exe). However, I couldn't find any affirmative documentation
        // on that. So disable FS redirection anyway.
        using var disableFsRedirect = new DisableWow64FsRedirectionScope();
        {
#if NET
            const int stackSize = 260; // Actual Windows MAX_PATH value. But paths can get larger (up to 32k).
            int bufferSize = stackSize;
            Span<char> buffer = stackalloc char[bufferSize];

            while (true)
            {
                fixed (char* bufferPtr = buffer)
                {
                    bool ret = QueryFullProcessImageName(hProcess, 0, bufferPtr, ref bufferSize);
                    if (!ret)
                    {
                        int code = Marshal.GetLastWin32Error();
                        if (code != ERROR_INSUFFICIENT_BUFFER)
                        {
                            if (!throwOnError)
                            {
                                return null;
                            }

                            throw new Win32Exception(code);
                        }

                        // Buffer too small. Double size; from now on need heap alloc to conserve stack space.
                        bufferSize *= 2;
                        buffer = new char[bufferSize];
                    }
                    else
                    {
                        return buffer.Slice(0, bufferSize).Trim('\0').ToString();
                    }
                }
            }
#else
            var sb = new StringBuilder(4096);
            int size = sb.Capacity;
            if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
            {
                return sb.ToString();
            }

            if (throwOnError)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return null;
#endif
        }
    }

#if NET
    [LibraryImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessTimes(SafeProcessHandle handle, out long creation, out long exit, out long kernel, out long user);
#else
    [DllImport(KernelDll, CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetProcessTimes(SafeProcessHandle handle, out long creation, out long exit, out long kernel, out long user);
#endif

    internal static DateTime GetProcessStartTime(int processId)
    {
        using var handle = OpenProcessLimited(processId);

        if (!handle.IsInvalid && GetProcessTimes(handle, out long creation, out _, out _, out _))
        {
            return DateTime.FromFileTime(creation);
        }

        return DateTime.MinValue;
    }

#if NET
    [LibraryImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ProcessIdToSessionId(int dwProcessId, out int sessionId);
#else
    [DllImport(KernelDll, SetLastError = true)]
    private static extern bool ProcessIdToSessionId(int dwProcessId, out int sessionId);
#endif

    internal static int GetProcessSessionId(int dwProcessId)
    {
        if (ProcessIdToSessionId(dwProcessId, out int sessionId))
        {
            return sessionId;
        }

        return -1;
    }

    internal static string? GetSystemAccountName()
    {
        try
        {
            var sid = new SecurityIdentifier("S-1-5-18");
            return sid.Translate(typeof(NTAccount)).Value;
        }
        catch
        {
        }

        return null;
    }


    internal static string? GetProcessOwner(SafeProcessHandle handle)
    {
        try
        {
            if (OpenProcessToken(handle, TOKEN_QUERY, out var token))
            {
                if (ProcessTokenToSid(token, out var sid))
                {
                    var x = new SecurityIdentifier(sid);
                    return x.Translate(typeof(NTAccount)).Value;
                }
            }
        }
        catch
        {
            // If the computer is domain joined, and the connection to the domain controller is "broken", you may get this error (sometimes):
            //
            // System.ComponentModel.Win32Exception (1789): The trust relationship between this workstation and the primary domain failed.
            //   at System.Security.Principal.SecurityIdentifier.TranslateToNTAccounts(IdentityReferenceCollection sourceSids, Boolean& someFailed)
            //   at System.Security.Principal.SecurityIdentifier.Translate(IdentityReferenceCollection sourceSids, Type targetType, Boolean forceSuccess)
            //   at System.Security.Principal.SecurityIdentifier.Translate(Type targetType)
            //   at LockCheck.Windows.NativeMethods.GetProcessOwner(SafeProcessHandle handle)
        }

        return null;
    }

    internal static bool ProcessTokenToSid(SafeAccessTokenHandle token, out IntPtr sid)
    {
        const int bufLength = 256;
        sid = IntPtr.Zero;
        var tu = IntPtr.Zero;
        try
        {
            tu = Marshal.AllocHGlobal(bufLength);
            int cb = bufLength;
            var ret = GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenUser, tu, cb, ref cb);
            if (ret)
            {
                var tokUser = Marshal.PtrToStructure<TOKEN_USER>(tu);
                sid = tokUser.User.Sid;
            }
            return ret;
        }
        finally
        {
            if (tu != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(tu);
            }
        }
    }

    internal const int TOKEN_QUERY = 0x0008;

    [StructLayout(LayoutKind.Sequential)]
    internal struct TOKEN_USER
    {
        public SID_AND_ATTRIBUTES User;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SID_AND_ATTRIBUTES
    {
        public IntPtr Sid;
        public int Attributes;
    }

    internal enum TOKEN_INFORMATION_CLASS
    {
        TokenUser = 1,
    }


#if NET
    [LibraryImport(KernelDll, SetLastError = true, StringMarshalling = StringMarshalling.Utf16, EntryPoint = "CreateFileW")]
    private static partial SafeFileHandle CreateFile(
        string lpFileName,
        int dwDesiredAccess,
        FileShare dwShareMode,
        IntPtr lpSecurityAttributes,
        FileMode dwCreationDisposition,
        int dwFlagsAndAttributes,
        IntPtr hTemplateFile);
#else
    [DllImport(KernelDll, SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        int dwDesiredAccess,
        FileShare dwShareMode,
        IntPtr lpSecurityAttributes,
        FileMode dwCreationDisposition,
        int dwFlagsAndAttributes,
        IntPtr hTemplateFile);

#endif

    internal static SafeFileHandle GetFileHandle(string name)
    {
        return CreateFile(name,
            0, // "FileAccess.Neither" Read nor Write
            FileShare.Read | FileShare.Write | FileShare.Delete,
            IntPtr.Zero,
            FileMode.Open,
            (int)FileAttributes.Normal,
            IntPtr.Zero);
    }

    internal struct PebOffsets
    {
        public int ProcessParametersOffset;
        public int CommandLineOffset;
        public int CurrentDirectoryOffset;
        public int WindowTitleOffset;
        public int DesktopInfoOffset;
        public int ImagePathNameOffset;
        public int EnvironmentOffset;
        public int EnvironmentSizeOffset;
        public int SessionIdOffset;

        public static PebOffsets Get(bool target64)
        {
            var result = new PebOffsets();

            // Use "windbg.exe" (the 32bit and 64bit version respectively!)
            // and start an arbitrary (32bit and 64bit process). Then run
            // "dt ntdll!_PEB"
            // "dt ntdll!_RTL_USER_PROCESS_PARAMETERS"
            // __ PEB __
            result.SessionIdOffset = target64 ? 0x02c0 : 0x01d4;
            result.ProcessParametersOffset = target64 ? 0x20 : 0x10;
            // __ RTL_USER_PROCESS_PARAMTERS __
            result.CommandLineOffset = target64 ? 0x70 : 0x40;
            result.CurrentDirectoryOffset = target64 ? 0x38 : 0x24;
            result.WindowTitleOffset = target64 ? 0xb0 : 0x70;
            result.DesktopInfoOffset = target64 ? 0xc0 : 0x78;
            // Note: we could use QueryFullProcessImageName() for this,
            // but since we're already mocking around, we might as well
            // use the following.
            result.ImagePathNameOffset = target64 ? 0x60 : 0x38;
            result.EnvironmentOffset = target64 ? 0x80 : 0x48;
            result.EnvironmentSizeOffset = target64 ? 0x03f0 : 0x0290;

            return result;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KSYSTEM_TIME
    {
        public uint LowPart;
        public int High1Time;
        public int High2Time;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private unsafe readonly struct KUSER_SHARED_DATA
    {
        // The kernel maps KUSER_SHARED_DATA at this address into each process.
        // Regardless of the bitness of the process. Also, the structure has the
        // same field-width, regardless of the bitness of the process.
        internal const nint Address = 0x7ffe_0000;

        // Only part of the KUSER_SHARED_DATA up to "BootId", which is really the only field we need.
        // More fields. See https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntddk/ns-ntddk-kuser_shared_data

        public readonly uint TickCountLowDeprecated;
        public readonly uint TickCountMultiplier;
        public readonly KSYSTEM_TIME InterruptTime;
        public readonly KSYSTEM_TIME SystemTime;
        public readonly KSYSTEM_TIME TimeZoneBias;
        public readonly ushort ImageNumberLow;
        public readonly ushort ImageNumberHigh;

        public readonly STRING_260 NtSystemRoot;

        public readonly uint MaxStackTraceDepth;
        public readonly uint CryptoExponent;
        public readonly uint TimeZoneId;
        public readonly uint LargePageMinimum;
        public readonly uint AitSamplingValue;
        public readonly uint AppCompatFlag;
        public readonly ulong RNGSeedVersion;
        public readonly uint GlobalValidationRunlevel;
        public readonly int TimeZoneBiasStamp;
        public readonly uint NtBuildNumber;
        public readonly int NtProductType;
        public readonly byte ProductTypeIsValid;

        public readonly byte Reserved0;

        public readonly ushort NativeProcessorArchitecture;
        public readonly uint NtMajorVersion;
        public readonly uint NtMinorVersion;

        public readonly BOOL_ARRAY_64 ProcessorFeatures;

        public readonly uint Reserved1;
        public readonly uint Reserved3;
        public readonly uint TimeSlip;
        public readonly int AlternativeArchitecture;
        public readonly uint BootId;

        // ...

        // Helpers so we can make this structure readonly. Currently we don't read the members using these
        // types, but for completeness we account for them properly.

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STRING_260
        {
            private fixed char _buffer[260];

            public override string ToString()
            {
                fixed (char* s = _buffer)
                {
                    return new string(s);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct BOOL_ARRAY_64
        {
            private fixed byte _buffer[64];

            public bool[] Values
            {
                get
                {
                    fixed (byte* s = _buffer)
                    {
                        bool[] res = new bool[64];
                        for (int i = 0; i < res.Length; i++)
                        {
                            res[i] = 1 == *(s + i);
                        }
                        return res;
                    }
                }
            }
        }
    }

    internal static unsafe uint GetBootId()
    {
        // If we ever need other fields from KUSER_SHARED_DATA - please don't - we can
        // simple unmarshall the whole thing using the typical:
        //
        //     var sharedData = Marshal.PtrToStructure<KUSER_SHARED_DATA>(KUSER_SHARED_DATA.Address);
        //
        // However, currently we only need the BootId, thus the following is more efficient.

        var ptr = IntPtr.Add(KUSER_SHARED_DATA.Address, (int)Marshal.OffsetOf<KUSER_SHARED_DATA>(nameof(KUSER_SHARED_DATA.BootId)));
        return (uint)Marshal.ReadInt32(ptr);
    }

    internal static ulong GetProcessStartKey(ulong processSequenceNumber)
    {
        // Apparently, this is how the ETW ProcessStartKey is calculated.
        // Reference: disassembly of PsGetProcessStartKey()
        //
        //    PsGetProcessStartKey proc near
        //       mov     rax, 0FFFFF780000002C4h  // Load memory address of field "BootId" (offset 0x2C4 in KUSER_SHARED_DATA)
        //       mov     eax, [rax]               // store BootId in eax
        //       shl     rax, 30h                 // BootId >> 48 (0x30)
        //       or      rax, [rcx+8F8h]          // SequenceNumber | rax
        //       retn
        //    PsGetProcessStartKey endp
        //
        // Other, random, "art" on the internet does it the same way.

        return ((ulong)GetBootId() << 0x30) | processSequenceNumber;
    }

    // native struct defined in ntexapi.h
    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_PROCESS_INFORMATION
    {
        internal uint NextEntryOffset;
        internal uint NumberOfThreads;
        internal long WorkingSetPrivateSize;
        internal uint HardFaultCount;
        internal uint NumberOfThreadsHighWatermark;
        internal long CycleTime;
        internal long CreateTime;
        internal long UserTime;
        internal long KernelTime;

        internal ushort NameLength;
        internal ushort MaximumNameLength;
        internal IntPtr NamePtr;

        internal int BasePriority;
        internal IntPtr UniqueProcessId;
        internal IntPtr InheritedFromUniqueProcessId;
        internal uint HandleCount;
        internal uint SessionId;

        // This member looks promising in that it could contain the same value that the WMI "UniqueProcessKey"
        // and thus also ETW "UniqueProcessKey". However, unofficial research has this to say:
        //
        // (see https://www.geoffchappell.com/studies/windows/km/ntoskrnl/api/ex/sysinfo/process.htm):
        // "The UniqueProcessKey is undefined for SystemProcessInformation [bug requires SystemExtendedProcessInformation,
        // which in turn requires administration privileges] For the newer information classes it originally revealed the
        // page number of the process’s page directory base. Version 6.0 instead reveals the address of the EPROCESS
        // structure that represents the process as a kernel object. Whether the member was named UniqueProcessKey in
        // these versions is not known. Whatever it was named, what it contained may have been thought to disclose too
        // much: [>>] since version 6.1 the UniqueProcessKey is set identically to the UniqueProcessId. [<<]"
        //
        // FWIW, WMI still documents "UniqueProcessKey" as "The address of the process object in the kernel."
        // This could of course be a totally different "address" than the one cited above, however (WMI/ETW)
        // traces show values that look like this: UniqueProcessKey=0xFFFF8905CFFF1080. Which suspiciously looks
        // like a kernel address.
        //
        // Anyway, I leave this comment here, should I (again!) attempt to use this member ;-)
        // Still it would be nice if we could determine this value for the processes we find to be locking
        // stuff and present them together with their PID, etc.
        internal UIntPtr UniqueProcessKey;

        internal UIntPtr PeakVirtualSize;
        internal UIntPtr VirtualSize;
        internal uint PageFaultCount;
        internal UIntPtr PeakWorkingSetSize;
        internal UIntPtr WorkingSetSize;
        internal UIntPtr QuotaPeakPagedPoolUsage;
        internal UIntPtr QuotaPagedPoolUsage;
        internal UIntPtr QuotaPeakNonPagedPoolUsage;
        internal UIntPtr QuotaNonPagedPoolUsage;
        internal UIntPtr PagefileUsage;
        internal UIntPtr PeakPagefileUsage;
        internal UIntPtr PrivatePageCount;
        internal long ReadOperationCount;
        internal long WriteOperationCount;
        internal long OtherOperationCount;
        internal long ReadTransferCount;
        internal long WriteTransferCount;
        internal long OtherTransferCount;
        internal IntPtr Threads;
    }

    public static int GetExtensionOffset(this SYSTEM_PROCESS_INFORMATION si)
    {
        // This is only valid when PROCESS_INFORMATION_CLASS.ProcessInformation was used.
        // ProcessFullInformation (only as Admin) and ProcessExtendedInformation are different.

        int threadStructSize = Marshal.SizeOf<SYSTEM_THREAD_INFORMATION>();
        return (int)(
        IntPtr.Add(Marshal.OffsetOf(typeof(SYSTEM_PROCESS_INFORMATION), nameof(SYSTEM_PROCESS_INFORMATION.Threads)),
        (int)(threadStructSize * si.NumberOfThreads)));
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CLIENT_ID
    {
        public IntPtr UniqueProcess; // HANDLE to the process
        public IntPtr UniqueThread;  // HANDLE to the thread
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_THREAD_INFORMATION
    {
        public ulong KernelTime;         // Total time in kernel mode
        public ulong UserTime;           // Total time in user mode
        public ulong CreateTime;         // Time thread was created
        public uint WaitTime;            // Time the thread has been in the wait state
        public IntPtr StartAddress;      // Pointer to the thread start address
        public CLIENT_ID ClientId;       // Identifies the thread
        public int Priority;             // Thread priority
        public int BasePriority;         // Base priority of the thread
        public uint ContextSwitchCount;  // Number of context switches
        public uint ThreadState;         // State of the thread
        public uint WaitReason;          // Reason the thread is in the wait state
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_EXTENDED_THREAD_INFORMATION
    {
        public SYSTEM_THREAD_INFORMATION ThreadInfo;
        public IntPtr StackBase;
        public IntPtr StackLimit;
        public IntPtr Win32StartAddress;
        public IntPtr TebBase;
        public UIntPtr Reserved2;
        public UIntPtr Reserved3;
        public UIntPtr Reserved4;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_DISK_COUNTERS
    {
        public ulong BytesRead;
        public ulong BytesWritten;
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong FlushOperationCount;
    }


    [StructLayout(LayoutKind.Sequential)]
    internal struct ENERGY_STATE_DURATION
    {
        public ulong Value; // Single ulong member to hold the combined data

        public uint LastChangeTime => (uint)(Value & 0xFFFFFFFF); // LastChangeTime: occupies the first 4 bytes
        public uint Duration => (uint)((Value >> 32) & 0x7FFFFFFF);  // Duration: 31 bits (bits 32-62)
        public bool IsInState => (Value & 0x8000000000000000UL) != 0;  // IsInState: 1 bit (bit 63)
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct PROCESS_ENERGY_VALUES
    {
        public fixed ulong Cycles[8]; // This represents array[4][2]

        public ulong DiskEnergy;
        public ulong NetworkTailEnergy;
        public ulong MBBTailEnergy;
        public ulong NetworkTxRxBytes;
        public ulong MBBTxRxBytes;

        // Array of ENERGY_STATE_DURATION structs with a fixed size of 3
        public ENERGY_STATE_DURATION ForegroundDuration;
        public ENERGY_STATE_DURATION DesktopVisibleDuration;
        public ENERGY_STATE_DURATION PSMForegroundDuration;

        public uint CompositionRendered;
        public uint CompositionDirtyGenerated;
        public uint CompositionDirtyPropagated;
        public uint Reserved1;

        public fixed ulong AttributedCycles[8]; // This represents array[4][2]
        public fixed ulong WorkOnBehalfCycles[8]; // This represents array[4][2]

        public static ulong GetElement(int row, int column, ulong[] value)
        {
            if (row < 0 || row >= 4)
            {
                throw new ArgumentOutOfRangeException(nameof(row), row, null);
            }

            if (column < 0 || column >= 2)
            {
                throw new ArgumentOutOfRangeException(nameof(column), column, null);
            }

            return value[(row * 2) + column];
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_PROCESS_INFORMATION_EXTENSION
    {
        public PROCESS_DISK_COUNTERS DiskCounters;
        public ulong ContextSwitches;
        public uint Flags;
        public uint UserSidOffset;
        public uint PackageFullNameOffset;
        public PROCESS_ENERGY_VALUES EnergyValues;
        public uint AppIdOffset;
        public IntPtr SharedCommitCharge;
        public uint JobObjectId;
        public uint SpareUlong;
        public ulong ProcessSequenceNumber;
    }
#pragma warning restore 169

#if NET
    [LibraryImport(KernelDll, SetLastError = true)]
    internal static partial SafeProcessHandle GetCurrentProcess();

    [LibraryImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWow64Process(SafeProcessHandle hProcess, [MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

    [LibraryImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Wow64DisableWow64FsRedirection(ref IntPtr oldValue);

    [LibraryImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Wow64RevertWow64FsRedirection(IntPtr oldValue);

    [LibraryImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ReadProcessMemory(SafeProcessHandle hProcess, IntPtr lpBaseAddress, ref IntPtr lpBuffer, IntPtr dwSize, IntPtr lpNumberOfBytesRead);

    [LibraryImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ReadProcessMemory(SafeProcessHandle hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, IntPtr dwSize, IntPtr lpNumberOfBytesRead);


    [LibraryImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ReadProcessMemory(SafeProcessHandle hProcess, IntPtr lpBaseAddress, ref UNICODE_STRING_32 lpBuffer, IntPtr dwSize, IntPtr lpNumberOfBytesRead);

    [LibraryImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ReadProcessMemory(SafeProcessHandle hProcess, IntPtr lpBaseAddress, ref UNICODE_STRING lpBuffer, IntPtr dwSize, IntPtr lpNumberOfBytesRead);

    [LibraryImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ReadProcessMemory(SafeProcessHandle hProcess, IntPtr lpBaseAddress, [MarshalAs(UnmanagedType.LPWStr)] string lpBuffer, IntPtr dwSize, IntPtr lpNumberOfBytesRead);

    [LibraryImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ReadProcessMemory(SafeProcessHandle hProcess, IntPtr lpBaseAddress, ref uint data, IntPtr dwSize, IntPtr lpNumberOfBytesRead);
#else
    [DllImport(KernelDll, SetLastError = true)]
    internal static extern SafeProcessHandle GetCurrentProcess();

    [DllImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWow64Process(SafeProcessHandle hProcess, [MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

    [DllImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Wow64DisableWow64FsRedirection(ref IntPtr oldValue);

    [DllImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Wow64RevertWow64FsRedirection(IntPtr oldValue);

    [DllImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadProcessMemory(SafeProcessHandle hProcess, IntPtr lpBaseAddress, ref IntPtr lpBuffer, IntPtr dwSize, IntPtr lpNumberOfBytesRead);

    [DllImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadProcessMemory(SafeProcessHandle hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, IntPtr dwSize, IntPtr lpNumberOfBytesRead);


    [DllImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadProcessMemory(SafeProcessHandle hProcess, IntPtr lpBaseAddress, ref UNICODE_STRING_32 lpBuffer, IntPtr dwSize, IntPtr lpNumberOfBytesRead);

    [DllImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadProcessMemory(SafeProcessHandle hProcess, IntPtr lpBaseAddress, ref UNICODE_STRING lpBuffer, IntPtr dwSize, IntPtr lpNumberOfBytesRead);

    [DllImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadProcessMemory(SafeProcessHandle hProcess, IntPtr lpBaseAddress, [MarshalAs(UnmanagedType.LPWStr)] string lpBuffer, IntPtr dwSize, IntPtr lpNumberOfBytesRead);

    [DllImport(KernelDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadProcessMemory(SafeProcessHandle hProcess, IntPtr lpBaseAddress, ref uint data, IntPtr dwSize, IntPtr lpNumberOfBytesRead);
#endif

    // for 32-bit process in a 64-bit OS only

#if NET
    [LibraryImport(NtDll)]
    internal static partial int NtWow64ReadVirtualMemory64(SafeProcessHandle hProcess, long lpBaseAddress, IntPtr data, long dwSize, IntPtr lpNumberOfBytesRead);

    [LibraryImport(NtDll)]
    internal static partial int NtWow64ReadVirtualMemory64(SafeProcessHandle hProcess, long lpBaseAddress, ref long lpBuffer, long dwSize, IntPtr lpNumberOfBytesRead);

    [LibraryImport(NtDll)]
    internal static partial int NtWow64ReadVirtualMemory64(SafeProcessHandle hProcess, long lpBaseAddress, ref UNICODE_STRING_WOW64 lpBuffer, long dwSize, IntPtr lpNumberOfBytesRead);

    [LibraryImport(NtDll)]
    internal static partial int NtWow64ReadVirtualMemory64(SafeProcessHandle hProcess, long lpBaseAddress, [MarshalAs(UnmanagedType.LPWStr)] string lpBuffer, long dwSize, IntPtr lpNumberOfBytesRead);

    [LibraryImport(NtDll)]
    internal static partial int NtWow64ReadVirtualMemory64(SafeProcessHandle hProcess, long lpBaseAddress, ref uint data, long dwSize, IntPtr lpNumberOfBytesRead);
#else
    [DllImport(NtDll)]
    internal static extern int NtWow64ReadVirtualMemory64(SafeProcessHandle hProcess, long lpBaseAddress, IntPtr data, long dwSize, IntPtr lpNumberOfBytesRead);

    [DllImport(NtDll)]
    internal static extern int NtWow64ReadVirtualMemory64(SafeProcessHandle hProcess, long lpBaseAddress, ref long lpBuffer, long dwSize, IntPtr lpNumberOfBytesRead);

    [DllImport(NtDll)]
    internal static extern int NtWow64ReadVirtualMemory64(SafeProcessHandle hProcess, long lpBaseAddress, ref UNICODE_STRING_WOW64 lpBuffer, long dwSize, IntPtr lpNumberOfBytesRead);

    [DllImport(NtDll)]
    internal static extern int NtWow64ReadVirtualMemory64(SafeProcessHandle hProcess, long lpBaseAddress, [MarshalAs(UnmanagedType.LPWStr)] string lpBuffer, long dwSize, IntPtr lpNumberOfBytesRead);

    [DllImport(NtDll)]
    internal static extern int NtWow64ReadVirtualMemory64(SafeProcessHandle hProcess, long lpBaseAddress, ref uint data, long dwSize, IntPtr lpNumberOfBytesRead);
#endif
    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct UNICODE_STRING
    {
        public short Length;
        public short MaximumLength;
        public IntPtr Buffer;

        public readonly string GetEmptyBuffer() => new('\0', Length / 2);
    }

    // for 32-bit process in a 64-bit OS only
    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_BASIC_INFORMATION_WOW64
    {
        public long Reserved1;
        public long PebBaseAddress;
        public long Reserved2_0;
        public long Reserved2_1;
        public long UniqueProcessId;
        public long InheritedFromUniqueProcessId;
    }

    // for 32-bit process in a 64-bit OS only
    [StructLayout(LayoutKind.Sequential)]
    internal struct UNICODE_STRING_WOW64
    {
        public short Length;
        public short MaximumLength;
        public long Buffer;

        public readonly string GetEmptyBuffer() => new('\0', Length / 2);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct UNICODE_STRING_32
    {
        public short Length;
        public short MaximumLength;
        public int Buffer;

        public readonly string GetEmptyBuffer() => new('\0', Length / 2);
    }


    internal const int FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100;
    internal const int FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
    internal const int FORMAT_MESSAGE_FROM_STRING = 0x00000400;
    internal const int FORMAT_MESSAGE_FROM_HMODULE = 0x00000800;
    internal const int FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
    internal const int FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x00002000;

#if NET
    internal static string GetMessage(int errorCode) => $"{Marshal.GetPInvokeErrorMessage(errorCode)} (0x{errorCode:X8})";
#else
    internal static string GetMessage(int errorCode) => $"{new Win32Exception(errorCode).Message}  (0x{errorCode:X8})";
#endif

    internal unsafe readonly struct ScopedNativeMemory : IDisposable
    {
#if NET
        private readonly void* _buffer;
#else
        private readonly IntPtr _buffer;
#endif
        private readonly uint _size;

        public ScopedNativeMemory(uint size)
        {
            _size = size;
#if NET
            _buffer = NativeMemory.Alloc(size);
#else
            _buffer = Marshal.AllocHGlobal((int)size);
#endif
        }

        public ScopedNativeMemory(int size)
        {
            _size = (uint)size;
#if NET
            _buffer = NativeMemory.Alloc((UIntPtr)size);
#else
            _buffer = Marshal.AllocHGlobal(size);
#endif
        }

        public int Size => (int)_size;

        public static explicit operator IntPtr(ScopedNativeMemory memory)
        {
#if NET
            return (IntPtr)memory._buffer;
#else
            return memory._buffer;
#endif
        }

        public static explicit operator void*(ScopedNativeMemory memory)
        {
#if NET
            return memory._buffer;
#else
            return (void*)memory._buffer;
#endif
        }

        public void Dispose()
        {
#if NET
            NativeMemory.Free(_buffer);
#else
            Marshal.FreeHGlobal(_buffer);
#endif
        }
    }
}
