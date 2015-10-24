using System;

namespace LockCheck
{
    public class ProcessInfo
    {
        internal ProcessInfo(NativeMethods.RM_PROCESS_INFO processInfo)
        {
            ProcessId = (int)processInfo.Process.dwProcessId;
            // ProcessStartTime is returned as local time, not UTC.
            StartTime = DateTime.FromFileTime((((long)processInfo.Process.ProcessStartTime.dwHighDateTime) << 32) |
                                              processInfo.Process.ProcessStartTime.dwLowDateTime);
            ApplicationName = processInfo.strAppName;
            ServiceShortName = processInfo.strServiceShortName;
            ApplicationType = (ApplicationType)processInfo.ApplicationType;
            ApplicationStatus = (ApplicationStatus)processInfo.AppStatus;
            Restartable = processInfo.bRestartable;
            TerminalServicesSessionId = (int)processInfo.TSSessionId;
        }

        public int ProcessId { get; private set; }
        public DateTime StartTime { get; private set; }
        public string ApplicationName { get; private set; }
        public string ServiceShortName { get; private set; }
        public ApplicationType ApplicationType { get; private set; }
        public ApplicationStatus ApplicationStatus { get; private set; }
        public int TerminalServicesSessionId { get; private set; }
        public bool Restartable { get; private set; }

        public override int GetHashCode()
        {
            var h1 = ProcessId.GetHashCode();
            var h2 = StartTime.GetHashCode();
            return ((h1 << 5) + h1) ^ h2;
        }

        public override bool Equals(object obj)
        {
            var other = obj as ProcessInfo;
            if (other != null)
            {
                return other.ProcessId == ProcessId && other.StartTime == StartTime;
            }
            return false;
        }

        public override string ToString()
        {
            return ProcessId + "@" + StartTime.ToString("s");
        }
    }
}