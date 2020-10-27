using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LockCheck
{
    public partial class ProcessInfo
    {
        public int ProcessId { get; }
        public DateTime StartTime { get; private set; }
        public string ExecutableName { get; private set; }
        public string ApplicationName { get; private set; }
        public string UserName { get; internal set; }
        public string FilePath { get; internal set; }
        public int SessionId { get; internal set; }

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

        public string ToString(string format)
        {
            if (format == null)
            {
                return ToString();
            }

            if (format == "F")
            {
                return ToString() + "/" + ApplicationName;
            }

            if (format == "A")
            {
                var sb = new StringBuilder();
                sb.Append(nameof(ProcessId)).Append(": ").Append(ProcessId).AppendLine();
                sb.Append(nameof(StartTime)).Append(": ").Append(StartTime).AppendLine();
                sb.Append(nameof(ExecutableName)).Append(": ").Append(ExecutableName).AppendLine();
                sb.Append(nameof(ApplicationName)).Append(": ").Append(ApplicationName).AppendLine();
                sb.Append(nameof(UserName)).Append(": ").Append(UserName).AppendLine();
                sb.Append(nameof(FilePath)).Append(": ").Append(FilePath).AppendLine();
                sb.Append(nameof(SessionId)).Append(": ").Append(SessionId).AppendLine();
                return sb.ToString();
            }

            return ToString();
        }

        public static void Format(StringBuilder sb, IEnumerable<ProcessInfo> lockers, IEnumerable<string> fileNames, int? max = null)
        {
            if (lockers == null || !lockers.Any())
                return;

            int count = lockers.Count();
            sb.AppendFormat("File {0} locked by: ", string.Join(", ", fileNames));
            foreach (var locker in lockers.Take(max ?? Int32.MaxValue))
            {
                sb.AppendLine($"[{locker.ApplicationName}, pid={locker.ProcessId}, user={locker.UserName}, started={locker.StartTime:yyyy-MM-dd HH:mm:ss.fff}]");
            }

            if (count > max)
            {
                sb.AppendLine($"[{count - max} more processes...]");
            }
        }
    }
}