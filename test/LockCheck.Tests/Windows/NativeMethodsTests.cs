using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using LockCheck.Tests.Tooling;
using LockCheck.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;

namespace LockCheck.Tests.Windows;

[SupportedTestClassPlatform("windows")]
[TestCategory("windows")]
public class NativeMethodsTests
{
    public TestContext TestContext { get; set; } = null!;

    [SupportedTestMethodPlatform("windows")]
    [DataRow(true)]
    [DataRow(false)]
    public void GetProcessImagePath_ShouldAlwaysReturnNativePath_RegardlessOfSelf(bool as32Bit)
    {
        TestHelper.CreateSampleProcess(as32Bit,
            (process, nativePath) =>
            {
                string? imagePath = NativeMethods.GetProcessImagePath(process.SafeHandle, throwOnError: true);
                Assert.IsNotNull(imagePath);
                Assert.AreEqual(nativePath, imagePath, ignoreCase: true);
            });
    }

    [TestMethod]
    public void IsProcessCriticalByImagePath_ShouldReturnTrue_ForCriticalProcess()
    {
        int count = 0;
        foreach (object[] args in KnownProcessNames)
        {
            string processName = (string)args[0];
            string fullPath = (string)args[1];

            TestContext.WriteLine($"testing: {processName}, {fullPath}");

            bool ok = Test(processName,
                processes =>
                {
                    foreach (var process in processes)
                    {
                        using var handle = NativeMethods.OpenProcessLimited(process.Id);
                        if (!handle.IsInvalid)
                        {
                            string imagePath = NativeMethods.GetProcessImagePath(handle, throwOnError: true)!;

                            if (imagePath.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                            {
                                return process.Id;
                            }
                        }
                        else
                        {
                            int code = Marshal.GetLastWin32Error();
                            TestContext.WriteLine($"Could open process '{processName}: {NativeMethods.GetMessage(code)}");
                        }
                    }

                    return null;
                },
                NativeMethods.IsProcessCriticalByImagePath,
                // Not all of the possible processes run on every system.
                // This will ensure that Test() does not assert in this case
                // and we can continue with other processes.
                ignoreNoMatchingInstances: true);

            if (ok)
            {
                count++;
            }
        }

        // Ensure that we have successfully tested at least one process.
        // It would be strange, if on a Windows system there would be
        // non of them.
        Assert.IsFalse(count == 0, "Not a single process was found.");
    }

    [SupportedTestMethodPlatform("windows", requiresAdminRights: true)]
    [DataRow("csrss")]
    [DataRow("wininit")]
    [DataRow("smss")]
    [DataRow("services")]
    public void IsProcessCriticalByHandle_ShouldReturnTrue_ForCriticalProcess(string processName)
    {
        Test(processName, processes => processes.First().Id, NativeMethods.IsProcessCriticalByHandle);
    }
    private static IEnumerable<object[]> KnownProcessNames =>
        NativeMethods.GetKnownCriticalProcesses()
        .Select(fullPath => new object[] { Path.GetFileNameWithoutExtension(fullPath), fullPath });

    private bool Test(string processName,
        Func<IEnumerable<Process>, int?> select,
        Func<SafeProcessHandle, IHasErrorState?, bool?> test,
        bool ignoreNoMatchingInstances = false)
    {
        // All our test processes may actually have multiple instances (when multiple user sessions are active).
        // But there must be at least on occurrence (for the current user's session). So we simply take the first
        // one we find.

        var processes = Process.GetProcessesByName(processName);
        if (processes.Length == 0)
        {
            string msg = $"No instances of process {processName} found.";
            if (!ignoreNoMatchingInstances)
            {
                Assert.IsTrue(processes.Length >= 1, msg);
            }
            TestContext.WriteLine(msg);
            return false;
        }

        try
        {
            var errorState = new TestErrorState();
            int? processId = select(processes);
            if (processId == null)
            {
                string msg = $"No matching process {processName} instance found.";
                if (!ignoreNoMatchingInstances)
                {
                    Assert.IsNotNull(processId, msg);
                }
                TestContext.WriteLine(msg);
                return false;
            }

            // Use our own OpenProcessLimited() because Process.SafeHandle does open the handle requesting full
            // access. That can lead to access denied errors, even when running this tests as Administrator.
            using var hProcess = NativeMethods.OpenProcessLimited(processId.Value);
            Assert.IsFalse(hProcess.IsInvalid, $"Failed to open {processName} (pid {processId}): {NativeMethods.GetMessage(Marshal.GetLastWin32Error())}");

            bool? result = test(hProcess, errorState);
            Assert.IsNotNull(result, $"Failed to get critical status for {processName} (pid {processId}): {errorState}");
            Assert.AreEqual(true, result, $"Process {processName} (pid {processId}) should be critical.");
        }
        finally
        {
            foreach (var process in processes)
            {
                process?.Dispose();
            }
        }

        return true;
    }
}
