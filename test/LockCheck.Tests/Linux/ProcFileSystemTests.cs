using System;
using System.Diagnostics;
using System.Text;
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

        [DataTestMethod]
        [DataRow("", new string[] { })]
        [DataRow("\0\0", new string[] { })]
        [DataRow("/\0\0", new string[] { "/" })]
        [DataRow("/usr/bin/sh\0\0", new string[] { "/usr/bin/sh" })]
        [DataRow("/usr/bin/sh\0-c echo \"Hello World\"\0\0", new string[] { "/usr/bin/sh", "-c echo \"Hello World\"" })]
        [DataRow("/usr/bin/sh\0-c echo \"Hello World\"\0123\0\0", new string[] { "/usr/bin/sh", "-c echo \"Hello World\"", "123" })]
        public void ConvertToArgs(string cmdline, string[] expected)
        {
            var span = Encoding.UTF8.GetBytes(cmdline).AsSpan();
            string[] args = ProcFileSystem.ConvertToArgs(ref span);
            CollectionAssert.AreEqual(expected, args);
        }
    }
}
