using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using LockCheck.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests.Tooling
{
    internal static class TestHelper
    {
        public static void TryDelete(this FileSystemInfo fi)
        {
            try
            {
                fi?.Delete();
            }
            catch (Exception ex)
            {
                // This can be a test (logic) error. Or temporary lock situation was not
                // properly resolved before attempting to delete the file/directory.
                // In either case we do not throw this onward because it could hide
                // an actual Assert-failure.
                Console.WriteLine($"WARNING: Could not delete '{fi.FullName}': {ex}");
            }
        }


        public static NativeMethods.FILETIME ToNativeFileTime(this DateTime dateTime)
        {
            // Convert DateTime to a long value representing the file time
            long fileTime = dateTime.ToFileTime();

            // Split the long value into high and low parts
            NativeMethods.FILETIME fileTimeStruct;
            fileTimeStruct.dwLowDateTime = (uint)(fileTime & 0xFFFFFFFF);
            fileTimeStruct.dwHighDateTime = (uint)(fileTime >> 32);

            return fileTimeStruct;
        }

        public static void CreateLockSituation(Action<Exception, string> action)
        {
            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".test");

            try
            {
                using (var file = File.Open(tempFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    try
                    {
                        var file2 = File.Open(tempFile, FileMode.Open, FileAccess.ReadWrite);
                        Assert.Fail("Expected exception");
                    }
                    catch (Exception ex)
                    {
                        action(ex, tempFile);
                    }
                }
            }
            finally
            {
                new FileInfo(tempFile).TryDelete();
            }
        }

        // Assign all child processes we launch to this job object. This ensures that they
        // will get terminated at the very least, when the test (testhost.exe) itself exits.
        private static readonly Lazy<IJobObject> s_selfJob = new(() => JobObject.Create(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static void CreateProcessWithCurrentDirectory(
            bool target64Bit,
            Action<(string TemporaryDirectory, int ProcessId, int SessionId, DateTime ProcessStartTime, string ProcessName, string ExecutableFullPath)> action)
        {
            string id = Guid.NewGuid().ToString("N");
            string tempDirectoryName = Path.Combine(Path.GetTempPath(), id + ".test");
            int sleep = 0; // forever
            int wait = 20; // seconds

            Process? process = null;
            var tempDir = new DirectoryInfo(tempDirectoryName);
            try
            {
                tempDir.Create();

                var si = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                string clientFullPath = GetClientFullPath(target64Bit, out string? hostExecutable);
                if (hostExecutable != null)
                {
                    si.FileName = hostExecutable;
                    si.Arguments = $"\"{clientFullPath}\" {id} \"{tempDirectoryName}\" {sleep}";
                }
                else
                {
                    si.FileName = clientFullPath;
                    si.Arguments = $"{id} \"{tempDirectoryName}\" {sleep}";
                }

                Console.WriteLine($"Starting test target: {si.FileName} {si.Arguments}");

                process = new Process();
                process.StartInfo = si;
                process.OutputDataReceived += (p, e) =>
                {
                    if (e.Data != null)
                    {
                        Console.WriteLine($"{((Process)p).Id:00000}: {e.Data}");
                    }
                };
                process.ErrorDataReceived += (p, e) =>
                {
                    if (e.Data != null)
                    {
                        Console.WriteLine($"{((Process)p).Id:00000}: {e.Data}");
                    }
                };

                if (!process.Start())
                {
                    throw new InvalidOperationException($"Failed to start test target: {si.FileName} {si.Arguments}");
                }

                s_selfJob.Value.AttachProcess(process);

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (var server = new NamedPipeServerStream(id))
                {
                    var connectionTask = server.WaitForConnectionAsync();
                    if (!connectionTask.Wait(wait * 1000))
                    {
                        Assert.Fail($"Test target has not signaled ready after {wait} seconds. Test target process with ID {process.Id} {(process.HasExited ? "has already exited" : "appears to be still running")}.");
                    }
                    else
                    {
                        using (var reader = new StreamReader(server))
                        {
                            string? message = reader.ReadLine();
                            Console.WriteLine($"Test target send: {message}");
                        }

                        action((tempDir.FullName, process.Id, process.SessionId, process.StartTime, process.ProcessName, si.FileName));
                    }
                }
            }
            finally
            {
                try
                {
                    Console.WriteLine($"Killing test target process with process ID {process?.Id} ...");
                    process?.Kill();
                    process?.WaitForExit();
                    Console.WriteLine("Process killed.");
                }
                catch (InvalidOperationException)
                {
                    // Process gone.
                }
                finally
                {
                    process?.Dispose();
                }

                tempDir.TryDelete();
            }
        }

        private static string GetClientFullPath(bool target64Bit, out string? hostExecutable)
        {
            string targetRid;
            string extension;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                targetRid = target64Bit ? "win-x64" : "win-x86";
                extension = ".exe";
                hostExecutable = null;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                targetRid = "linux-x64";
                extension = ".dll";
                hostExecutable = "/usr/share/dotnet/dotnet";
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            // Attempt to locate the LCTestTarget binary for the given TargetRID.
            //
            // Assume that LockCheck.Tests (this assembly) is located as follows:
            //
            //       C:\...\test\LockCheck.Tests\bin\<Configuration>\<TargetFramework>[\<RID>]   (=> AppContext.BaseDirectory)
            //
            // Note that sometimes this path ends with the RID, sometimes it doesn't. That depends
            // on how the build/tests are invoked. So we have to cater for both possibilities.
            //
            // Assume further, that LCTestTarget projects are located as follows:
            //
            //       C:\...\test\LCTestTarget.<TargetRID>\bin\<Configuration>\<TargetFramework>\<TargetRid>\LCTestTarget<Extension>
            //
            // So, we want to run LCTestTarget from the same <TargetFramework> and <Configuration> as this assembly,
            // but obviously using the <TargetRid> requested.

            ReadOnlySpan<char> separators = ['/', '\\'];

            // Assume that the project (directory name) matches the assembly name.
            string projectName = typeof(TestHelper).Assembly.GetName().Name!;

            // Get the part of the path before the project directory (e.g. "C:\...\test")
            if (!TryGetPathBeforeLastSegment(AppContext.BaseDirectory, projectName, out var baseDir))
            {
                throw new InvalidOperationException($"Failed to get project base directory from '{AppContext.BaseDirectory}' and '{projectName}'.");
            }

            // Get the part of the path after the project directory (e.g. "\bin\<Configuration>\<TargetFramework>[\<RID>]")
            if (!TryGetPathAfterLastSegment(AppContext.BaseDirectory, projectName, out var outputDirString))
            {
                throw new InvalidOperationException($"Failed to get output directory from '{AppContext.BaseDirectory}' and '{projectName}'.");
            }

            // See if the path after the project directory ends with the RID (of this assembly)..
            var outputDir = outputDirString.AsSpan().Trim(separators);
            var currentRid = GetCurrentRuntimeIdentifier().AsSpan();
            if (outputDir.ToString().EndsWith(currentRid.ToString()))
            {
                // Remove the RID from the path, so that next we can always assume the same segment
                // position for <Configuration> and <TargetFramework>.
                outputDir = outputDir.Slice(0, outputDir.Length - currentRid.Length).Trim(separators);
            }

            // Now, outputDir should look like this bin\<Configuration>\<TargetFramework>
            int pos = outputDir.LastIndexOfAny(separators);
            Debug.Assert(pos != -1);
            var targetFramework = outputDir.Slice(pos + 1);
            outputDir = outputDir.Slice(0, pos).Trim(separators);
            pos = outputDir.LastIndexOfAny(separators);
            var configuration = outputDir.Slice(pos + 1);

            string clientFullPath = Path.Combine(
                baseDir.ToString(),
                $"LCTestTarget.{targetRid}",
                "bin",
                configuration.ToString(),
                targetFramework.ToString(),
                targetRid,
                $"LCTestTarget{extension}");

            clientFullPath = Path.GetFullPath(clientFullPath);
            return clientFullPath;
        }

        public static void CreateShellWithCurrentDirectory(Action<(string TemporaryDirectory, int ProcessId, int SessionId, DateTime ProcessStartTime, string ProcessName, string ExecutableFullPath)> action)
        {
            string tempDirectoryName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".test");
            const string sentinel = "TEST_READY";

            Process? process = null;
            var tempDir = new DirectoryInfo(tempDirectoryName);
            try
            {
                tempDir.Create();

                var si = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    si.FileName = @"C:\Windows\System32\cmd.exe";
                    si.Arguments = $"/K \"cd {tempDir.FullName} && echo {sentinel}\"";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    si.FileName = "/bin/sh";
                    si.Arguments = $"-c \"cd {tempDir.FullName};echo {sentinel}; sleep 10; exit\"";
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

                using var waitForStartupComplete = new ManualResetEventSlim();

                process = new Process();
                process.StartInfo = si;
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null && e.Data.Contains(sentinel))
                    {
                        waitForStartupComplete.Set();
                    }
                };

                if (!process.Start())
                {
                    throw new InvalidOperationException($"Failed to start process: {si.FileName} {si.Arguments}");
                }

                s_selfJob.Value.AttachProcess(process);

                process.BeginOutputReadLine();

                if (waitForStartupComplete.Wait(TimeSpan.FromSeconds(20)))
                {
                    action((tempDir.FullName, process.Id, process.SessionId, process.StartTime, process.ProcessName, si.FileName));
                }
                else
                {
                    Assert.Fail("Giving up after 20 secs");
                }
            }
            finally
            {
                try
                {
                    Console.WriteLine($"Killing test target process with process ID {process?.Id} ...");
                    process?.Kill();
                    process?.WaitForExit();
                    Console.WriteLine("Process killed.");
                }
                catch (InvalidOperationException)
                {
                    // Process gone.
                }
                finally
                {
                    process?.Dispose();
                }

                tempDir.TryDelete();
            }
        }

        /// <summary>
        /// Get the part of the <paramref name="path"/> before the <i>last</i> occurrence of <paramref name="segment"/>.
        /// </summary>
        /// <remarks>
        /// The specified <paramref name="segment"/> must appear between to directory separator chars (<c>\</c> or <c>/</c>).
        /// Both are considered on every platform. Partial matches (e.g. "foo" in "/foobar/x") are not considered. Only the
        /// last occurrence is considered (e.g. "foo" with "/x/foo/bar/foo" sets the prefix to "/x/foo/bar" not "/x/")
        /// <br/>
        /// Consider the following:
        /// <pre>
        ///   path               segment          prefix
        ///   /usr/local/bin     usr              "/"
        ///   usr/local/bin      usr              ""
        ///   \\User\\Admin      User             "\\"
        ///   User\\Admin        User             ""
        ///   C:\\User\\Admin    User             "C:\\"
        /// </pre>
        /// </remarks>
        /// <param name="path"></param>
        /// <param name="segment"></param>
        /// <param name="prefix"></param>
        /// <returns>
        /// <c>true</c> if <paramref name="segment"/> appears in <paramref name="path"/>.
        /// <c>false</c> if <paramref name="segment"/> does not appear in <paramref name="path"/>.
        /// </returns>
        internal static bool TryGetPathBeforeLastSegment(string path, string segment, [NotNullWhen(true)] out string? prefix)
        {
            int index = path.LastIndexOf(segment, StringComparison.Ordinal);

            if (index == -1)
            {
                prefix = null;
                return false;
            }

            // Check if the segment is at the end or followed by a path separator
            if (index + segment.Length == path.Length ||
                path[index + segment.Length] == '\\' ||
                path[index + segment.Length] == '/')
            {
                prefix = path.Substring(0, index);
                return true;
            }

            // An actual path segment contains only part of the search segment
            prefix = null;
            return false;
        }

        internal static bool TryGetPathAfterLastSegment(string path, string segment, [NotNullWhen(true)] out string? suffix)
        {
            int index = path.LastIndexOf(segment, StringComparison.Ordinal);

            if (index == -1)
            {
                suffix = null;
                return false;
            }

            // Check if the segment is at the end or followed by a path separator
            if (index + segment.Length == path.Length ||
                path[index + segment.Length] == '\\' ||
                path[index + segment.Length] == '/')
            {
                // Calculate the start index of the part after the segment
                int suffixStartIndex = index + segment.Length;

                // If the suffix starts at the end of the path, return an empty string
                if (suffixStartIndex >= path.Length)
                {
                    suffix = string.Empty;
                }
                else
                {
                    suffix = path.Substring(suffixStartIndex);
                }

                return true;
            }

            // An actual path segment contains only part of the search segment
            suffix = null;
            return false;
        }

        internal static string GetCurrentRuntimeIdentifier()
        {
#if NET
            return RuntimeInformation.RuntimeIdentifier;
#else
            // RuntimeInformation.RuntimeIdentifier does not exist in .NET Framework.

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return $"win-{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return $"linux-{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}";
            }

            throw new InvalidOperationException($"Cannot get RID for '{RuntimeInformation.OSDescription}' and '{RuntimeInformation.ProcessArchitecture}'.");
#endif
        }
    }
}
