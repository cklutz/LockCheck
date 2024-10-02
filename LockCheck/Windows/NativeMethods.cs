using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace LockCheck.Windows
{
    internal static class NativeMethods
    {
        private const string NtDll = "ntdll.dll";

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

        internal enum FILE_INFORMATION_CLASS { FileProcessIdsUsingFileInformation = 47 }

        [DllImport(NtDll)]
        internal static extern uint NtQueryInformationFile(SafeFileHandle fileHandle, ref IO_STATUS_BLOCK IoStatusBlock,
            IntPtr pInfoBlock, uint length, FILE_INFORMATION_CLASS fileInformation);

        internal enum PROCESS_INFORMATION_CLASS
        {
            ProcessBasicInformation = 0,
            ProcessWow64Information = 26
        }

        [DllImport(NtDll)]
        internal static extern uint NtQueryInformationProcess(SafeProcessHandle hProcess,
            PROCESS_INFORMATION_CLASS processInformationClass,
            ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, IntPtr returnLength);

        [DllImport(NtDll)]
        internal static extern int NtWow64QueryInformationProcess64(SafeProcessHandle hProcess, 
            PROCESS_INFORMATION_CLASS processInformationClass,
            ref PROCESS_BASIC_INFORMATION_WOW64 processInformation, int processInformationLength, IntPtr returnLength);

        [DllImport(NtDll, EntryPoint = "NtQueryInformationProcess")]
        internal static extern int NtQueryInformationProcessWow64(SafeProcessHandle hProcess, PROCESS_INFORMATION_CLASS processInformationClass, 
            ref IntPtr processInformation, int processInformationLength, IntPtr returnLength);

        internal const int NtQuerySystemProcessInformation = 5;

        [DllImport(NtDll)]
        internal static extern int NtQuerySystemInformation(int query, IntPtr dataPtr, int size, out int returnedSize);

        [DllImport(NtDll)]
        internal static extern int RtlNtStatusToDosError(uint status);

        internal const int ERROR_MR_MID_NOT_FOUND = 317;

        internal const uint STATUS_SUCCESS = 0;
        internal const uint STATUS_INFO_LENGTH_MISMATCH = 0xC0000004;

        // ----------------------------------------------------------------------------------------------

        private const string RestartManagerDll = "rstrtmgr.dll";
        private const string AdvApi32Dll = "advapi32.dll";
        private const string KernelDll = "kernel32.dll";

        [DllImport(RestartManagerDll, CharSet = CharSet.Unicode)]
        internal static extern int RmRegisterResources(uint pSessionHandle,
            uint nFiles,
            string[] rgsFilenames,
            uint nApplications,
            [In] RM_UNIQUE_PROCESS[] rgApplications,
            uint nServices,
            string[] rgsServiceNames);

        [DllImport(RestartManagerDll, CharSet = CharSet.Unicode)]
        internal static extern int RmStartSession(out uint pSessionHandle,
            int dwSessionFlags, StringBuilder strSessionKey);

        [DllImport(RestartManagerDll)]
        internal static extern int RmEndSession(uint pSessionHandle);

        [DllImport(RestartManagerDll, CharSet = CharSet.Unicode)]
        internal static extern int RmGetList(uint dwSessionHandle,
            out uint pnProcInfoNeeded,
            ref uint pnProcInfo,
            [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
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
        internal const int ERROR_SEM_TIMEOUT = 121;
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

        [DllImport(AdvApi32Dll, SetLastError = true)]
        internal static extern bool OpenProcessToken(SafeProcessHandle processHandle,
            int desiredAccess, out SafeAccessTokenHandle tokenHandle);

        [DllImport(AdvApi32Dll, CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool GetTokenInformation(SafeAccessTokenHandle hToken, TOKEN_INFORMATION_CLASS tokenInfoClass, IntPtr tokenInformation, int tokeInfoLength, ref int reqLength);

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

        [DllImport(KernelDll, SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        internal static SafeProcessHandle OpenProcessLimited(int pid)
        {
            return OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        }

        internal static SafeProcessHandle OpenProcessRead(int pid)
        {
            return OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, pid);
        }

        [DllImport(KernelDll, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(SafeProcessHandle hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        internal static string GetProcessImagePath(SafeProcessHandle hProcess)
        {
            var sb = new StringBuilder(4096);
            int size = sb.Capacity;
            if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
            {
                return sb.ToString();
            }
            return null;
        }

        [DllImport(KernelDll, CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetProcessTimes(SafeProcessHandle handle, out long creation, out long exit, out long kernel, out long user);

        internal static DateTime GetProcessStartTime(SafeProcessHandle handle)
        {
            if (GetProcessTimes(handle, out long creation, out _, out _, out _))
            {
                return DateTime.FromFileTime(creation);
            }

            return DateTime.MinValue;
        }

        [DllImport(KernelDll, SetLastError = true)]
        private static extern bool ProcessIdToSessionId(int dwProcessId, out int sessionId);

        internal static int GetProcessSessionId(int dwProcessId)
        {
            if (ProcessIdToSessionId(dwProcessId, out int sessionId))
            {
                return sessionId;
            }

            return -1;
        }

        internal static string GetProcessOwner(SafeProcessHandle handle)
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
                var ret = GetTokenInformation(token, NativeMethods.TOKEN_INFORMATION_CLASS.TokenUser, tu, cb, ref cb);
                if (ret)
                {
                    var tokUser = (TOKEN_USER)Marshal.PtrToStructure(tu, typeof(TOKEN_USER));
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


        [DllImport(KernelDll, SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            int dwDesiredAccess,
            FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            FileMode dwCreationDisposition,
            int dwFlagsAndAttributes,
            IntPtr hTemplateFile);

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

            public static PebOffsets Get(bool target64)
            {
                var result = new PebOffsets();

                // Use "windbg.exe" (the 32bit and 64bit version respectively!)
                // and start an arbitrary (32bit and 64bit process). Then run
                // "dt ntdll!_PEB"
                // "dt ntdll!_RTL_USER_PROCESS_PARAMETERS"
                // __ PEB __
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
        internal class SYSTEM_PROCESS_INFORMATION
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


        // for 32-bit process in a 64-bit OS only


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

            public string GetLpBuffer() => new string('\0', Length / 2);
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

            public string GetLpBuffer() => new string('\0', Length / 2);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct UNICODE_STRING_32
        {
            public short Length;
            public short MaximumLength;
            public int Buffer;

            public string GetLpBuffer() => new string('\0', Length / 2);
        }

    }
}
