using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests
{
    internal static class TestHelper
    {
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
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        public static void CreateProcessWithCurrentDirectory(Action<(string TemporaryDirectory, int ProcessId, int SessionId, DateTime ProcessStartTime, string ProcessName, string ExecutableFullPath)> action)
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
                    Console.WriteLine($"KILL IT .... {process.Id}");
                    process?.Kill();
                    process?.WaitForExit();
                    Console.WriteLine("KILLED IT.");
                }
                catch (InvalidOperationException)
                {
                    // Process gone.
                }
                finally
                {
                    process?.Dispose();
                }

                tempDir.Delete();
            }
        }
    }
}
