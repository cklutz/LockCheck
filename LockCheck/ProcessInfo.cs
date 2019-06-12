using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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

            try
            {
                var process = Process.GetProcessById(ProcessId);
                if (string.IsNullOrWhiteSpace(ApplicationName))
                {
                    ApplicationName = process.ProcessName;
                }

                FilePath = process.MainModule.FileName;
            }
            catch
            {
            }
        }

        public int ProcessId { get; private set; }
        public DateTime StartTime { get; private set; }
        public string ApplicationName { get; private set; }
        public string FilePath { get; private set; }
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

        public static void Format(StringBuilder sb, IEnumerable<ProcessInfo> lockers, IEnumerable<string> fileNames, int? max = null)
        {
            if (lockers == null || !lockers.Any())
                return;

            int count = lockers.Count();
            sb.AppendFormat("File {0} locked by: ", string.Join(", ", fileNames));
            foreach (var locker in lockers.Take(max ?? Int32.MaxValue))
            {
                sb.AppendLine($"[{locker.ApplicationName}, pid={locker.ProcessId}, started {locker.StartTime:yyyy-MM-dd HH:mm:ss.fff}]");
            }

            if (count > max)
            {
                sb.AppendLine($"[{count - max} more processes...]");
            }
        }
    }
}