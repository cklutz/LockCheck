using System;
using System.IO;
using LockCheck.Windows;

namespace LockCheck
{
    public partial class ProcessInfo
    {
        internal ProcessInfo(NativeMethods.RM_PROCESS_INFO processInfo)
        {
            ProcessId = (int)processInfo.Process.dwProcessId;
            // ProcessStartTime is returned as local time, not UTC.
            StartTime = DateTime.FromFileTime((((long)processInfo.Process.ProcessStartTime.dwHighDateTime) << 32) |
                                              processInfo.Process.ProcessStartTime.dwLowDateTime);
            ApplicationName = processInfo.strAppName;
            SessionId = (int)processInfo.TSSessionId;

            FillAuxiliaryInfo();
        }

        internal ProcessInfo(int processId)
        {
            ProcessId = processId;
            SessionId = -1;

            FillAuxiliaryInfo();
        }

        private void FillAuxiliaryInfo()
        {
            using (var handle = NativeMethods.OpenProcessLimited(ProcessId))
            {
                if (!handle.IsInvalid)
                {
                    string imagePath = NativeMethods.GetProcessImagePath(handle);
                    FilePath = NativeMethods.GetProcessImagePath(handle);
                    UserName = NativeMethods.GetProcessOwner(handle);
                    ExecutableName = Path.GetFileName(imagePath);

                    if (StartTime == DateTime.MinValue)
                    {
                        StartTime = NativeMethods.GetProcessStartTime(handle);
                    }

                    if (string.IsNullOrEmpty(ApplicationName))
                    {
                        ApplicationName = Path.GetFileName(imagePath);
                    }

                    if (SessionId == -1)
                    {
                        SessionId = NativeMethods.GetProcessSessionId(ProcessId);
                    }
                }
            }
        }
    }
}