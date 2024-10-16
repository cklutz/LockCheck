using System;
using System.Diagnostics;
using LockCheck.Linux;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests.Linux
{
    [SupportedTestClassPlatform("linux")]
    public class ProcFileSystemTests
    {
        [TestMethod]
        public void GetProcessExecutablePathFromCmdLine_ShouldReturnExecutableName_WhenNoPermissionToProcess()
        {
            using var init = Process.GetProcessById(1);

            // Assume unit tests are not run as root.
            Assert.IsNull(init.MainModule);

            Assert.AreEqual("/sbin/init", ProcFileSystem.GetProcessExecutablePathFromCmdLine(1));
        }

        [TestMethod]
        public void GetProcessExecutablePathFromCmdLine_ShouldReturnNull_WhenProcessDoesNotExist()
        {
            Assert.IsNull(ProcFileSystem.GetProcessExecutablePathFromCmdLine(-1));
        }

        [TestMethod]
        public void Test()
        {

        }
    }
}
