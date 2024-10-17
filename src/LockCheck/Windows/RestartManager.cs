using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace LockCheck.Windows
{
    internal static class RestartManager
    {
        public static HashSet<ProcessInfo> GetLockingProcessInfos(string[] paths, ref List<string>? directories)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));

            const int maxRetries = 6;

            // See http://blogs.msdn.com/b/oldnewthing/archive/2012/02/17/10268840.aspx.
            var key = new StringBuilder(new string('\0', NativeMethods.CCH_RM_SESSION_KEY + 1));

            uint handle;
            int res = NativeMethods.RmStartSession(out handle, 0, key);
            if (res != 0)
                throw GetException(res, "Failed to begin restart manager session");

            try
            {
                var files = new HashSet<string>(paths.Length, StringComparer.OrdinalIgnoreCase);
                foreach (string path in paths)
                {
                    if (Directory.Exists(path))
                    {
                        directories?.Add(path);
                    }
                    else
                    {
                        files.Add(path);
                    }
                }

                res = NativeMethods.RmRegisterResources(handle, (uint)files.Count, files.ToArray(), 0, null, 0, null);
                if (res != 0)
                    throw GetException(res, "Could not register resources");

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
                NativeMethods.RM_PROCESS_INFO[]? rgAffectedApps = null;
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
                            return [];

                        Debug.Assert(rgAffectedApps != null);
                        var lockInfos = new HashSet<ProcessInfo>((int)pnProcInfo);
                        for (int i = 0; i < pnProcInfo; i++)
                        {
                            var info = ProcessInfoWindows.Create(rgAffectedApps![i]);
                            if (info != null)
                            {
                                lockInfos.Add(info);
                            }
                        }
                        return lockInfos;
                    }

                    if (res != NativeMethods.ERROR_MORE_DATA)
                        throw GetException(res, $"Failed to get entries (retry {retry})");

                    pnProcInfo = pnProcInfoNeeded;
                    rgAffectedApps = new NativeMethods.RM_PROCESS_INFO[pnProcInfo];
                } while ((res == NativeMethods.ERROR_MORE_DATA) && (retry++ < maxRetries));
            }
            finally
            {
                res = NativeMethods.RmEndSession(handle);
                if (res != 0)
                    throw GetException(res, "Failed to end the restart manager session");
            }

            return [];
        }

        internal static Win32Exception GetException(int res, string message)
        {
            return new Win32Exception(res, $"{message}: {NativeMethods.GetMessage(res)}");
        }
    }
}
