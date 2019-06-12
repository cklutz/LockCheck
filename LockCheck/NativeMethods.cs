using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace LockCheck
{
    internal static class NativeMethods
    {
        // ReSharper disable InconsistentNaming

        private const string RestartManagerDll = "rstrtmgr.dll";
        private const string AdvApi32Dll = "advapi32.dll";

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
        }

        [DllImport(AdvApi32Dll, SetLastError = true)]
        internal static extern bool OpenProcessToken(SafeProcessHandle processHandle,
            int desiredAccess, out SafeAccessTokenHandle tokenHandle);

        [DllImport(AdvApi32Dll, CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool GetTokenInformation(SafeAccessTokenHandle hToken, TOKEN_INFORMATION_CLASS tokenInfoClass, IntPtr tokenInformation, int tokeInfoLength, ref int reqLength);

        internal static string GetProcessOwner(SafeProcessHandle handle)
        {
            if (OpenProcessToken(handle, TOKEN_QUERY, out var token))
            {
                if (ProcessTokenToSid(token, out var sid))
                {
                    var x = new SecurityIdentifier(sid);
                    return x.Translate(typeof(NTAccount)).Value;
                }
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

        // ReSharper restore InconsistentNaming
    }
}