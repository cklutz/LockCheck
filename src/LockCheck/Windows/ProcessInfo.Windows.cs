using System;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace LockCheck.Windows
{
    internal class ProcessInfoWindows : ProcessInfo
    {
        public static ProcessInfoWindows? Create(NativeMethods.RM_PROCESS_INFO pi)
            => Create((int)pi.Process.dwProcessId, pi, (pid, _, data) => new ProcessInfoWindows(pid, data.GetStartTime()));

        public static ProcessInfoWindows? Create(int processId)
            => Create(processId, 0, (pid, handle, _) => new ProcessInfoWindows(pid, NativeMethods.GetProcessStartTime(handle)));

        private static ProcessInfoWindows? Create<T>(int processId, T data, Func<int, SafeProcessHandle, T, ProcessInfoWindows> createInstance)
        {
            using (var handle = NativeMethods.OpenProcessLimited(processId))
            {
                if (!handle.IsInvalid)
                {
                    var result = createInstance(processId, handle, data);

                    string? imagePath = NativeMethods.GetProcessImagePath(handle);
                    result.ExecutableFullPath = imagePath;
                    result.Owner = NativeMethods.GetProcessOwner(handle);
                    result.ExecutableName = Path.GetFileName(imagePath);
                    result.ApplicationName = Path.GetFileName(imagePath);
                    result.SessionId = NativeMethods.GetProcessSessionId(processId);

                    return result;
                }

                return null;
            }
        }

        internal static ProcessInfoWindows Create(Peb peb)
        {
            var result = new ProcessInfoWindows(peb.ProcessId, peb.StartTime);
            result.ExecutableFullPath = peb.ExecutableFullPath;
            result.ExecutableName = Path.GetFileName(peb.ExecutableFullPath);
            result.ApplicationName = Path.GetFileName(peb.ExecutableFullPath);
            result.SessionId = peb.SessionId;
            result.Owner = peb.Owner;

            return result;
        }

        private ProcessInfoWindows(int processId, DateTime startTime)
            : base(processId, startTime)
        {
        }
    }
}
