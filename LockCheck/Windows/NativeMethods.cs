using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
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
                FileShare.Read|FileShare.Write|FileShare.Delete,
                IntPtr.Zero,
                FileMode.Open,
                (int)FileAttributes.Normal,
                IntPtr.Zero);
        }
    }
}
