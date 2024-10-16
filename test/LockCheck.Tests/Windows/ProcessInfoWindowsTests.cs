using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using LockCheck.Tests;

namespace LockCheck.Windows.Tests
{
    [SupportedTestClassPlatform("windows")]
    public class ProcessInfoWindowsTests
    {
        [TestMethod]
        public void Create_ShouldReturnProcessInfoWindows_WhenGivenValidRMProcessInfo()
        {
            var ts = DateTime.Now;
            var rmProcessInfo = new NativeMethods.RM_PROCESS_INFO
            {
                Process = new NativeMethods.RM_UNIQUE_PROCESS
                {
                    dwProcessId = (uint)Process.GetCurrentProcess().Id,
                    ProcessStartTime = ts.ToNativeFileTime()
                },
                strAppName = "TestApp",
                strServiceShortName = "TestService",
                ApplicationType = NativeMethods.RM_APP_TYPE.RmUnknownApp,
                AppStatus = 0,
                TSSessionId = 1,
                bRestartable = true
            };

            var result = ProcessInfoWindows.Create(rmProcessInfo);

            Assert.IsNotNull(result);
            Assert.AreEqual(rmProcessInfo.Process.dwProcessId, (uint)result.ProcessId);
            Assert.AreEqual(ts, result.StartTime);
        }

        [TestMethod]
        public void Create_ShouldReturnProcessInfoWindows_WhenGivenValidProcessId()
        {
            int processId = Process.GetCurrentProcess().Id;

            var result = ProcessInfoWindows.Create(processId);

            Assert.IsNotNull(result);
            Assert.AreEqual(processId, result.ProcessId);
        }

        [TestMethod]
        public void Create_ShouldReturnNull_WhenGivenInvalidProcessId()
        {
            var result = ProcessInfoWindows.Create(-1);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Create_ShouldReturnProcessInfoWindows_WhenGivenValidPeb()
        {
            var ts = DateTime.Now;
            var pi = new NativeMethods.SYSTEM_PROCESS_INFORMATION
            {
                UniqueProcessId = (IntPtr)Process.GetCurrentProcess().Id,
                CreateTime = ts.ToFileTime()
            };
            var peb = new Peb(pi);

            var result = ProcessInfoWindows.Create(peb);

            Assert.IsNotNull(result);
            Assert.AreEqual(peb.ProcessId, result.ProcessId);
            Assert.AreEqual(peb.ProcessId, result.ProcessId);
            Assert.AreEqual(ts, result.StartTime);
        }
    }
}
