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
            string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".test");

            Process? process = null;
            var info = new DirectoryInfo(tempDirectory);
            try
            {
                info.Create();
                var si = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    si.FileName = Environment.ExpandEnvironmentVariables(@"%windir%\system32\WindowsPowerShell\v1.0\powershell.exe");
                    si.Arguments = $"-NoProfile -Command \"cd '{tempDirectory}';echo TEST_READY; sleep 10; exit\"";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    si.FileName = "/bin/sh";
                    si.Arguments = $"-c \"cd {tempDirectory};echo TEST_READY; sleep 10; exit\"";
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

                using var semaphore = new SemaphoreSlim(1);
                process = Process.Start(si);
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null && e.Data.Contains("TEST_READY"))
                    {
                        semaphore.Release();
                    }
                };
                process.BeginOutputReadLine();
                process.Start();

                if (semaphore.Wait(TimeSpan.FromSeconds(20)))
                {
                    action((tempDirectory, process.Id, process.SessionId, process.StartTime, process.ProcessName, si.FileName));
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
                    process?.Kill();
                    process?.WaitForExit();
                }
                catch (InvalidOperationException)
                {
                    // Process gone.
                }
                finally
                {
                    process?.Dispose();
                }

                info.Delete();
            }
        }
    }
}
