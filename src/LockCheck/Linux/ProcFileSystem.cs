using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace LockCheck.Linux
{
    internal static class ProcFileSystem
    {
        internal static Dictionary<(int, DateTime), ProcessInfo> GetProcessesByWorkingDirectory(List<string> directories)
        {
            var result = new Dictionary<(int, DateTime), ProcessInfo>();

            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.PlatformDefault,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false
            };

            foreach (string fullPath in Directory.EnumerateDirectories("/proc", "*", options))
            {
                if (int.TryParse(Path.GetFileName(fullPath.AsSpan()), NumberStyles.Integer, CultureInfo.InvariantCulture, out int processId))
                {
                    var pi = new ProcInfo(processId);

                    if (!pi.HasError && !string.IsNullOrEmpty(pi.CurrentDirectory))
                    {
                        // If the process' current directory is the search path itself, or it is somewhere nested below it,
                        // we have to take it into account. This will also account for differences in the two when the
                        // search path does not end with a '/'.
                        if (directories.FindIndex(d => pi.CurrentDirectory.StartsWith(d, StringComparison.Ordinal)) != -1)
                        {
                            result[(processId, pi.StartTime)] = ProcessInfoLinux.Create(pi);
                        }
                    }
                }
            }

            return result;
        }

        public static HashSet<ProcessInfo> GetLockingProcessInfos(string[] paths, ref List<string> directories)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));

            Dictionary<long, string> inodesToPaths = null;
            var result = new HashSet<ProcessInfo>();

            var xpaths = new HashSet<string>(paths.Length, StringComparer.Ordinal);

            foreach (string path in paths)
            {
                // Get directories, but don't exclude them from lookup via procfs (in contrast to Windows).
                // On Linux /proc/locks may also contain directory locks.
                if (Directory.Exists(path))
                {
                    directories?.Add(path);
                }

                xpaths.Add(path);
            }

            using (var reader = new StreamReader("/proc/locks"))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (inodesToPaths == null)
                    {
                        inodesToPaths = GetInodeToPaths(xpaths);
                    }

                    var lockInfo = LockInfo.ParseLine(line);
                    if (inodesToPaths.ContainsKey(lockInfo.InodeInfo.INodeNumber))
                    {
                        var processInfo = ProcessInfoLinux.Create(lockInfo);
                        if (processInfo != null)
                        {
                            result.Add(processInfo);
                        }
                    }
                }
            }

            return result;
        }

        private static Dictionary<long, string> GetInodeToPaths(HashSet<string> paths)
        {
            var inodesToPaths = new Dictionary<long, string>();

            foreach (string path in paths)
            {
                long inode = NativeMethods.GetInode(path);
                if (inode != -1)
                {
                    inodesToPaths.Add(inode, path);
                }
            }

            return inodesToPaths;
        }

        internal static string GetProcessExecutablePathFromCmdLine(int processId)
        {
            byte[] rentedBuffer = null;
            try
            {
                using (var file = new FileStream($"/proc/{processId}/cmdline", FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, useAsync: false))
                {
                    Span<byte> buffer = stackalloc byte[256];
                    int bytesRead = 0;
                    while (true)
                    {
                        if (bytesRead == buffer.Length)
                        {
                            // Resize buffer
                            uint newLength = (uint)buffer.Length * 2;
                            // Store what was read into new buffer
                            byte[] tmp = ArrayPool<byte>.Shared.Rent((int)newLength);
                            buffer.CopyTo(tmp);
                            // Remember current "rented" buffer (might be null)
                            byte[] lastRentedBuffer = rentedBuffer;
                            // From now on, we did rent a buffer. And it will be used for further reads.
                            buffer = tmp;
                            rentedBuffer = tmp;
                            // Return previously rented buffer, if any.
                            if (lastRentedBuffer != null)
                            {
                                ArrayPool<byte>.Shared.Return(lastRentedBuffer);
                            }
                        }

                        Debug.Assert(bytesRead < buffer.Length);
                        int n = file.Read(buffer.Slice(bytesRead));
                        bytesRead += n;

                        // "/proc/<pid>/cmdline" contains the original argument vector (argv), where each part is separated by a null byte.
                        // See if we have read enough for argv[0], which contains the process name.
                        ReadOnlySpan<byte> argRemainder = buffer.Slice(0, bytesRead);
                        int argEnd = argRemainder.IndexOf((byte)'\0');
                        if (argEnd != -1)
                        {
                            return Encoding.UTF8.GetString(argRemainder.Slice(0, argEnd));
                        }
                    }
                }
            }
            catch (IOException)
            {
                return null;
            }
            finally
            {
                if (rentedBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                }
            }
        }
    }
}
