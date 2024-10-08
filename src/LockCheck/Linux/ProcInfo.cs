using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static LockCheck.Linux.NativeMethods;

namespace LockCheck.Linux
{
    [DebuggerDisplay("{HasError} {ProcessId} {ExecutableFullPath}")]
    internal class ProcInfo : IHasErrorState
    {
        private bool _hasError;
#if DEBUG
        private string _errorStack;
        private Exception _errorCause;
#endif

        public int ProcessId { get; private set; }
        public int SessionId { get; private set; }
        public string CommandLine { get; private set; }
        public string CurrentDirectory { get; private set; }
        public string ExecutableFullPath { get; private set; }
        public string Owner { get; private set; }
        public DateTime StartTime { get; private set; }

        public bool HasError => _hasError;

        public void SetError(Exception ex = null)
        {
            if (!_hasError)
            {
                _hasError = true;
#if DEBUG
                _errorStack = Environment.StackTrace;
                _errorCause = ex;
#endif
            }
        }

        public ProcInfo(int processId)
        {
            ProcessId = processId;

            if (!Directory.Exists($"/proc/{processId}"))
            {
                SetError();
                return;
            }

            CommandLine = GetCommandLine(processId, this);
            CurrentDirectory = GetCurrentDirectory(processId, this);
            ExecutableFullPath = GetExecutablePath(processId, this);
            Owner = GetProcessOwner(processId);
            StartTime = GetStartTime(processId, this);
            SessionId = GetSessionId(processId, this);

            // Make sure that the current directory always ends with a slash. AFAICT that is never the case,
            // using DirectoryInfo and procfs. We do this for symmetry with the Windows code and also because
            // it makes "starts-with" checks easier.
            if (!string.IsNullOrEmpty(CurrentDirectory) && CurrentDirectory[CurrentDirectory.Length - 1] != '\\')
            {
                CurrentDirectory += "/";
            }
        }

        private static string GetProcessOwner(int pid)
        {
            try
            {
                if (TryGetUid($"/proc/{pid}", out uint uid))
                {
                    return GetUserName(uid);
                }
            }
            catch (IOException)
            {
                // Don't set error state, just like the Windows version does not in this case.
                // The "Owner" field is not vital.
            }

            return null;
        }

        private static string GetCommandLine(int pid, IHasErrorState he)
        {
            try
            {
                return File.ReadAllText($"/proc/{pid}/cmdline");
            }
            catch (Exception ex)
            {
                he.SetError(ex);
                return null;
            }
        }

        private static string GetCurrentDirectory(int pid, IHasErrorState he)
        {
#if NETFRAMEWORK
            he.SetError();
            return null;
#else
            try
            {
                return Directory.ResolveLinkTarget($"/proc/{pid}/cwd", true)?.FullName;
            }
            catch (Exception ex)
            {
                he.SetError(ex);
                return null;
            }
#endif
        }

        private static string GetExecutablePath(int pid, IHasErrorState he)
        {
#if NETFRAMEWORK
            he.SetError();
            return null;
#else
            try
            {
                return File.ResolveLinkTarget($"/proc/{pid}/exe", true)?.FullName;
            }
            catch (Exception ex)
            {
                he.SetError(ex);
                return null;
            }
#endif
        }

        private static int GetSessionId(int pid, IHasErrorState he)
        {
#if NETFRAMEWORK
            he.SetError();
            return default;
#else
            try
            {
                string fileName = $"/proc/{pid}/stat";
                var content = File.ReadAllText(fileName).AsSpan().Trim();

                if (!int.TryParse(GetField(content, ' ', 5).Trim(), CultureInfo.InvariantCulture, out int sessionId))
                {
                    throw new IOException($"Invalid session ID in '{fileName}': {content}");
                }

                return sessionId;
            }
            catch (Exception ex)
            {
                he.SetError(ex);
                return default;
            }
#endif
        }

        private static unsafe DateTime GetStartTime(int pid, IHasErrorState he)
        {
#if NETFRAMEWORK
            he.SetError();
            return default;
#else
            try
            {
                // Apparently it is impossible to fully recreate the time that Process.StartTime calculates in 
                // the background. It uses clock_gettime(CLOCK_BOOTTIME) (see https://github.com/dotnet/runtime/pull/83966)
                // internally and calcuates the start time relative to that using /proc/<pid>/stat.
                // However we shave the yack, we get a different time than what Process.StartTime would return.
                // Debugging has it, that this is due to the fact that we get a different "boot time" (later time).
                // I'm not sure if that is a bug in the CLR or just a fact of life on Linux.
                // In any case, we need to get the exact same Timestamp for our hash keys to work properly.
                using var process = Process.GetProcessById(pid);
                return process.StartTime;
            }
            catch (Exception ex)
            {
                he.SetError(ex);
                return default;
            }
#endif
        }

#if NET
        private static ReadOnlySpan<char> GetField(ReadOnlySpan<char> content, char delimiter, int index)
        {
            int count = content.Count(delimiter) + 1;
            Span<Range> ranges = count < 128 ? stackalloc Range[count] : new Range[count];
            int num = MemoryExtensions.Split(content, ranges, delimiter);
            if (index >= num)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Cannot access field at index {index}, only {num} fields available.");
            }
            return content[ranges[index]];
        }
#endif
    }
}
