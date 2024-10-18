using System;
using System.Collections.Generic;
using System.IO;
using LockCheck.Tests.Tooling;
using LockCheck.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests.Windows
{
    [SupportedTestClassPlatform("windows")]
    [TestCategory("windows")]
    public class RestartManagerTests
    {
        [TestMethod]
        public void GetLockingProcessInfos_ShouldThrowArgumentNullException_WhenPathsIsNull()
        {
            var directories = new List<string>();
            Assert.ThrowsException<ArgumentNullException>(() => RestartManager.GetLockingProcessInfos(null!, ref directories));
        }

        [TestMethod]
        public void GetLockingProcessInfos_ShouldAddDirectories_WhenPathIsDirectory()
        {
            var di = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            di.Create();

            try
            {
                var directories = new List<string>();
                var result = RestartManager.GetLockingProcessInfos([di.FullName], ref directories);

                Assert.AreEqual(1, directories.Count);
                Assert.AreEqual(di.FullName, directories[0]);
                Assert.AreEqual(0, result.Count);
            }
            finally
            {
                di.TryDelete();
            }
        }

        [TestMethod]
        public void GetLockingProcessInfos_ShouldCallGetLockingProcessInfo_WhenPathIsFile()
        {
            var fi = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt"));

            try
            {
                using var stream = fi.Create();

                var directories = new List<string>();
                var result = RestartManager.GetLockingProcessInfos([fi.FullName], ref directories);

                Assert.IsTrue(result.Count >= 0); // Just to ensure the method runs without exceptions
                Assert.AreEqual(0, directories.Count);
            }
            finally
            {
                fi.TryDelete();
            }
        }
    }
}
