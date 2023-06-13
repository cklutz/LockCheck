using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests
{
    [TestClass]
    public class LockManagerTests
    {
        [DataTestMethod]
        [DataRow(LockManagerFeatures.None)]
        [DataRow(LockManagerFeatures.UseLowLevelApi)]
        public void LockInformationAvailable(LockManagerFeatures features)
        {
            var process = Process.GetCurrentProcess();

            TestHelper.CreateLockSituation((_, fileName) =>
            {
                var processInfos = LockManager.GetLockingProcessInfos(new[] { fileName }, features).ToList();
                Assert.AreEqual(1, processInfos.Count);
                Assert.AreEqual(process.Id, processInfos[0].ProcessId);
                Assert.AreEqual(process.SessionId, processInfos[0].SessionId);
                Assert.AreEqual(process.StartTime, processInfos[0].StartTime);
                Assert.IsNotNull(processInfos[0].ApplicationName);
                Assert.AreEqual(processInfos[0].ExecutableFullPath?.ToLowerInvariant(), process.MainModule.FileName.ToLowerInvariant());
                // Might contain domain, computername, etc. in SAM form
                StringAssert.Contains(processInfos[0].Owner?.ToLowerInvariant(), Environment.UserName.ToLowerInvariant());
                // Might have an .exe suffix or not.
                StringAssert.Contains(processInfos[0].ExecutableName?.ToLowerInvariant(), process.ProcessName.ToLowerInvariant());
            });
        }

        [DataTestMethod]
        public void LockInformationAvailableForDirectory()
        {
            TestHelper.CreateFolderWithOpenedProcess((tempFolder, process) =>
            {
                var processInfosUsingRestartManager = LockManager.GetLockingProcessInfos(new[] { tempFolder }).ToList();
                Assert.AreEqual(0, processInfosUsingRestartManager.Count);

                var processInfosUsingNtDll = LockManager.GetLockingProcessInfos(new[] { tempFolder }, LockManagerFeatures.UseLowLevelApi).ToList();
                Assert.AreEqual(1, processInfosUsingNtDll.Count);
                Assert.AreEqual(process.Id, processInfosUsingNtDll[0].ProcessId);
            });
        }

        [DataTestMethod]
        public void LockInformationNotAvailableForSubDirectory()
        {
            TestHelper.CreateFolderWithOpenedProcessInSubDir((tempFolder, process) =>
            {
                var processInfos = LockManager.GetLockingProcessInfos(new[] { tempFolder }, LockManagerFeatures.UseLowLevelApi).ToList();
                Assert.AreEqual(0, processInfos.Count, "NtDll is not able to find locks for subdirs");
            });
        }
    }
}
