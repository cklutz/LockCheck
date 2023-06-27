using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace LockCheck.Windows
{
    internal static class RestartManager
    {
        public static IEnumerable<ProcessInfo> GetLockingProcessInfos(params string[] paths)
        {
            if (paths == null)
                throw new ArgumentNullException("paths");

            const int maxRetries = 6;

            // See http://blogs.msdn.com/b/oldnewthing/archive/2012/02/17/10268840.aspx.
            var key = new StringBuilder(new string('\0', NativeMethods.CCH_RM_SESSION_KEY + 1));

            uint handle;
            int res = NativeMethods.RmStartSession(out handle, 0, key);
            if (res != 0)
                throw GetException(res, "RmStartSession", "Failed to begin restart manager session.");

            try
            {
                string[] resources = paths;
                res = NativeMethods.RmRegisterResources(handle, (uint)resources.Length, resources, 0, null, 0, null);
                if (res != 0)
                    throw GetException(res, "RmRegisterResources", "Could not register resources.");

                //
                // Obtain the list of affected applications/services.
                //
                // NOTE: Restart Manager returns the results into the buffer allocated by the caller. The first call to 
                // RmGetList() will return the size of the buffer (i.e. nProcInfoNeeded) the caller needs to allocate. 
                // The caller then needs to allocate the buffer (i.e. rgAffectedApps) and make another RmGetList() 
                // call to ask Restart Manager to write the results into the buffer. However, since Restart Manager 
                // refreshes the list every time RmGetList()is called, it is possible that the size returned by the first 
                // RmGetList()call is not sufficient to hold the results discovered by the second RmGetList() call. Therefore, 
                // it is recommended that the caller follows the following practice to handle this race condition:
                //
                //    Use a loop to call RmGetList() in case the buffer allocated according to the size returned in previous 
                //    call is not enough.
                // 
                uint pnProcInfo = 0;
                NativeMethods.RM_PROCESS_INFO[] rgAffectedApps = null;
                int retry = 0;
                do
                {
                    uint lpdwRebootReasons = (uint)NativeMethods.RM_REBOOT_REASON.RmRebootReasonNone;
                    uint pnProcInfoNeeded;
                    res = NativeMethods.RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, rgAffectedApps, ref lpdwRebootReasons);
                    if (res == 0)
                    {
                        // If pnProcInfo == 0, then there is simply no locking process (found), in this case rgAffectedApps is "null".
                        if (pnProcInfo == 0)
                            return Enumerable.Empty<ProcessInfo>();

                        Debug.Assert(rgAffectedApps != null, "rgAffectedApps != null");
                        var lockInfos = new List<ProcessInfo>((int)pnProcInfo);
                        for (int i = 0; i < pnProcInfo; i++)
                        {
                            lockInfos.Add(ProcessInfoWindows.Create(rgAffectedApps[i]));
                        }
                        return lockInfos;
                    }

                    if (res != NativeMethods.ERROR_MORE_DATA)
                        throw GetException(res, "RmGetList", string.Format("Failed to get entries (retry {0}).", retry));

                    pnProcInfo = pnProcInfoNeeded;
                    rgAffectedApps = new NativeMethods.RM_PROCESS_INFO[pnProcInfo];
                } while ((res == NativeMethods.ERROR_MORE_DATA) && (retry++ < maxRetries));
            }
            finally
            {
                res = NativeMethods.RmEndSession(handle);
                if (res != 0)
                    throw GetException(res, "RmEndSession", "Failed to end the restart manager session.");
            }

            return Enumerable.Empty<ProcessInfo>();
        }

        private static Exception GetException(int res, string apiName, string message)
        {
            string reason;
            switch (res)
            {
                case NativeMethods.ERROR_ACCESS_DENIED:
                    reason = "Access is denied.";
                    break;
                case NativeMethods.ERROR_SEM_TIMEOUT:
                    reason = "A Restart Manager function could not obtain a Registry write mutex in the allotted time. " +
                             "A system restart is recommended because further use of the Restart Manager is likely to fail.";
                    break;
                case NativeMethods.ERROR_BAD_ARGUMENTS:
                    reason = "One or more arguments are not correct. This error value is returned by the Restart Manager " +
                             "function if a NULL pointer or 0 is passed in a parameter that requires a non-null and non-zero value.";
                    break;
                case NativeMethods.ERROR_MAX_SESSIONS_REACHED:
                    reason = "The maximum number of sessions has been reached.";
                    break;
                case NativeMethods.ERROR_WRITE_FAULT:
                    reason = "An operation was unable to read or write to the registry.";
                    break;
                case NativeMethods.ERROR_OUTOFMEMORY:
                    reason = "A Restart Manager operation could not complete because not enough memory was available.";
                    break;
                case NativeMethods.ERROR_CANCELLED:
                    reason = "The current operation is canceled by user.";
                    break;
                case NativeMethods.ERROR_MORE_DATA:
                    reason = "More data is available.";
                    break;
                case NativeMethods.ERROR_INVALID_HANDLE:
                    reason = "No Restart Manager session exists for the handle supplied.";
                    break;
                default:
                    reason = string.Format("0x{0:x8}", res);
                    break;
            }

            return new Win32Exception(res, string.Format("{0} ({1}() error {2}: {3})", message, apiName, res, reason));
        }
    }
}