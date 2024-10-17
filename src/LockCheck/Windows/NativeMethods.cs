using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

#pragma warning disable IDE1006 // Naming Styles - off here, because we want to use native names

namespace LockCheck.Windows
{
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
            ProcessWow64Information = 26
        }

        internal enum SYSTEM_INFORMATION_CLASS
        {
            SystemProcessInformation = 5
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

#if NET
        [LibraryImport(KernelDll, SetLastError = true, EntryPoint = "QueryFullProcessImageNameW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool QueryFullProcessImageName(SafeProcessHandle hProcess, int dwFlags, char* lpExeName, ref int lpdwSize);
#else
        [DllImport(KernelDll, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(SafeProcessHandle hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);
#endif

        internal static unsafe string? GetProcessImagePath(SafeProcessHandle hProcess, bool throwOnError = false)
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

                            throw new System.ComponentModel.Win32Exception(code);
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
            return null;
#endif
        }

#if NET
        [LibraryImport(KernelDll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetProcessTimes(SafeProcessHandle handle, out long creation, out long exit, out long kernel, out long user);
#else
        [DllImport(KernelDll, CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetProcessTimes(SafeProcessHandle handle, out long creation, out long exit, out long kernel, out long user);
#endif

        internal static DateTime GetProcessStartTime(SafeProcessHandle handle)
        {
            if (GetProcessTimes(handle, out long creation, out _, out _, out _))
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

        // native struct defined in ntexapi.h
        [StructLayout(LayoutKind.Sequential)]
        internal struct SYSTEM_PROCESS_INFORMATION
        {
            internal uint NextEntryOffset;
            internal uint NumberOfThreads;
            internal long SpareLi1;
            internal long SpareLi2;
            internal long SpareLi3;
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
            internal UIntPtr PageDirectoryBase;
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
    }
}
